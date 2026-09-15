// Project:         Daggerfall Unity - iOS Touch Layer
// License:         MIT License
//
// MOBILE 2026-09-15: WEATHER THAT CHANGES DURING A DAY. Switched by [Enhancements] WeatherChanges,
// with [Enhancements] WeatherChangeHours for the cadence.
//
// WHY THIS EXISTS. Daggerfall Unity has a weather poll and does not use it: WeatherManager.Update
// carries the line
//
//     //PollWeatherChanges();
//
// commented out since upstream commit 5e98cf919 "Disable sudden weather changes" (2017), because
// the poll ran every 30 seconds of REAL time and re-rolled the weather from the climate table each
// time - sun, storm, sun, snow, several times a minute. With the poll off, the weather is rolled
// only three times: on a new game (StartGameBehaviour -> SetClimateWeathers), whenever the in-game
// DATE turns over (PlayerEntity: SetClimateWeathers + UpdateWeatherFromClimateArray), and when the
// player respawns into a different climate TYPE (PlayerEnterExit_OnRespawnerComplete). So a day
// that starts sunny is sunny until midnight, every time, in every region.
//
// WHAT THIS DOES INSTEAD. The same roll, off the same table, on a GAME clock rather than a real
// one: every N in-game hours (default 6, so four rolls a day) while the player is outdoors. The
// cadence is the whole fix - 30 real seconds is a flicker, 6 game hours is a morning that clouds
// over by noon.
//
// ONE ROLL PER CHECK, NEVER A BURST. Resting and fast travel jump the game clock by hours or days
// in a single frame. Rolling once per elapsed boundary would fire a dozen SetWeather calls on the
// frame a three-day rest ends, each one clearing and rebuilding the sky. So a check that finds any
// number of boundaries passed rolls exactly once and moves the marker to NOW: a three-day rest
// yields one roll on waking, which is also what a player would expect to see.
//
// NOT WHILE INSIDE, AND NOT RE-ARMED WHILE INSIDE. WeatherManager.Update returns early when the
// player is indoors and so does this; but the marker is deliberately left alone in there, so a
// player who spends a game week in a dungeon walks out into one fresh roll rather than into the
// weather they left behind.
//
// A LOADED SAVE KEEPS ITS OWN WEATHER. WeatherManager restores the saved weather in its OnLoad
// handler; if this component rolled on the first frame after a load, that restore would be thrown
// away instantly. So the marker is armed from "now" on OnLoad and on StreamingWorld.OnInitWorld -
// the same two events WeatherManager.Awake hooks - and the first roll comes one cadence later.
//
// NO TRANSITION WORK. Dynamic Skies already lerps its sky and fog colours toward the new weather;
// the stock painted sky switches instantly, exactly as it does on upstream's own midnight roll.
//
// THE DECISIONS ARE PURE FUNCTIONS. ShouldRoll and ClampCadence take plain numbers and are what
// the self test exercises; the MonoBehaviour half only collects the arguments.

using UnityEngine;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Weather;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileWeatherCycle : MonoBehaviour
    {
        #region Constants

        /// <summary>Fastest cadence offered. One game hour is already four rolls before lunch.</summary>
        public const int MinCadenceHours = 1;

        /// <summary>Slowest cadence offered. 24 game hours is upstream's own once-a-day behaviour.</summary>
        public const int MaxCadenceHours = 24;

        /// <summary>
        /// Four rolls a day: morning, midday, evening, night. Slow enough that a storm is an event
        /// rather than a flicker, fast enough that the sky is not a fixed backdrop for the day.
        /// </summary>
        public const int DefaultCadenceHours = 6;

        /// <summary>Daggerfall's clock counts whole minutes; the cadence is stated in hours.</summary>
        public const int MinutesPerHour = 60;

        #endregion

        #region Pure decisions

        /// <summary>
        /// PURE. The cadence as the roller will actually use it: 1..24 game hours, and the default
        /// for anything outside that. A hand-edited settings.ini with WeatherChangeHours=0 must not
        /// mean "roll every frame" and must not mean "never roll" either - both are worse than the
        /// default, so out-of-range lands on the default rather than being clamped to an edge.
        /// </summary>
        public static int ClampCadence(int hours)
        {
            if (hours < MinCadenceHours || hours > MaxCadenceHours)
                return DefaultCadenceHours;
            return hours;
        }

        /// <summary>
        /// PURE. Whether this check should roll the weather.
        ///
        /// <para>
        /// <paramref name="lastRollGameMinutes"/> below zero means "not armed yet" - a fresh world,
        /// a just-loaded save - and never rolls: the caller arms the marker from the current time
        /// instead, so the save's own restored weather survives its first frame.
        /// </para>
        /// <para>
        /// Otherwise it is one comparison: at least cadence*60 game minutes since the last roll.
        /// Note what this does NOT do - it does not count how many boundaries went by. Resting and
        /// fast travel move the clock by days in one frame, and the caller answers a true by
        /// setting the marker to NOW, so a three-day rest produces exactly one roll on waking
        /// rather than twelve SetWeather calls in a single frame.
        /// </para>
        /// <para>
        /// A clock that went BACKWARDS (a load into an earlier save while the marker still holds a
        /// later time) also rolls, because the alternative is a marker sitting in the future and a
        /// feature that is silently dead until the player catches up to it.
        /// </para>
        /// </summary>
        public static bool ShouldRoll(long nowGameMinutes, long lastRollGameMinutes, int cadenceHours)
        {
            if (lastRollGameMinutes < 0)
                return false;
            if (nowGameMinutes < lastRollGameMinutes)
                return true;
            return nowGameMinutes - lastRollGameMinutes >= (long)ClampCadence(cadenceHours) * MinutesPerHour;
        }

        #endregion

        #region Settings

        /// <summary>Master switch, read live so the panel's toggle needs no relaunch and no apply.</summary>
        public static bool Enabled
        {
            get { return DaggerfallUnity.Settings != null && DaggerfallUnity.Settings.WeatherChanges; }
        }

        /// <summary>The cadence in game hours, already clamped.</summary>
        public static int CadenceHours
        {
            get
            {
                return DaggerfallUnity.Settings == null
                    ? DefaultCadenceHours
                    : ClampCadence(DaggerfallUnity.Settings.WeatherChangeHours);
            }
        }

        #endregion

        #region Fields

        static MobileWeatherCycle instance;
        public static bool HasInstance { get { return instance != null; } }

        /// <summary>Game minutes at the last roll; below zero until the world is ready.</summary>
        long lastRoll = -1;

        /// <summary>
        /// Our own copy of the climate/season table. WeatherManager keeps its parsed copy private
        /// and there is no reason to widen an engine field for this - the table is a few hundred
        /// bytes of JSON parsed once per session.
        /// </summary>
        WeatherTable table;

        #endregion

        #region Install

        /// <summary>
        /// One host object for the life of the app, as MobileAutosave does. The events this hooks
        /// are static engine events, and the component has to survive the scene swap between the
        /// title screen and the game.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            GameObject host = new GameObject("MobileWeatherCycle");
            host.AddComponent<MobileWeatherCycle>();
            DontDestroyOnLoad(host);
        }

        void Awake()
        {
            instance = this;
        }

        void OnEnable()
        {
            // The same two events WeatherManager.Awake hooks, for the same reason: they are the
            // moments the world's weather is decided by somebody else, and our marker must start
            // from there rather than from whenever this object happened to wake up.
            StreamingWorld.OnInitWorld += OnInitWorld;
            SaveLoadManager.OnLoad += OnLoad;
        }

        void OnDisable()
        {
            StreamingWorld.OnInitWorld -= OnInitWorld;
            SaveLoadManager.OnLoad -= OnLoad;
        }

        void OnDestroy()
        {
            OnDisable();
            if (instance == this)
                instance = null;
        }

        #endregion

        #region Update

        void Update()
        {
            if (!Enabled)
            {
                // Off means off, and re-arming here means switching it back on starts a fresh
                // cadence from that moment instead of rolling on the first frame after the toggle.
                lastRoll = -1;
                return;
            }

            if (!GameManager.HasInstance || !GameManager.Instance.IsPlayingGame())
                return;

            PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;
            if (enterExit == null || enterExit.IsPlayerInside)
                return;     // as WeatherManager.Update does - and without touching the marker

            if (DaggerfallUnity.Instance == null || DaggerfallUnity.Instance.WorldTime == null)
                return;

            long now = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();

            if (lastRoll < 0)
            {
                lastRoll = now;
                return;
            }

            if (!ShouldRoll(now, lastRoll, CadenceHours))
                return;

            // The marker moves whether or not the roll changed anything, so the cadence is a
            // cadence and not a queue of missed boundaries.
            lastRoll = now;
            Roll();
        }

        void Roll()
        {
            WeatherManager wm = GameManager.Instance.WeatherManager;
            if (wm == null || wm.PlayerWeather == null)
                return;

            if (table == null)
                table = WeatherTable.ParseJsonTable();
            if (table == null)
                return;

            int climate = GameManager.Instance.PlayerGPS.CurrentClimateIndex;
            DaggerfallDateTime.Seasons season = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;

            WeatherType prev = wm.PlayerWeather.WeatherType;
            WeatherType next = table.GetWeather(climate, season);
            if (next == prev)
                return;

            wm.SetWeather(next);
            Debug.LogFormat("[Weather] rolled {0} -> {1} (climate {2}, {3}, every {4}h)",
                prev, next, climate, season, CadenceHours);
        }

        #endregion

        #region Event Handlers

        // Both of these arm the marker rather than rolling: on a load the save's own weather has
        // just been restored by WeatherManager, and on world init the engine has just set the
        // weather from the climate array. Either way the sky is already correct for right now.
        void OnInitWorld()
        {
            lastRoll = -1;
        }

        void OnLoad(SaveData_v1 saveData)
        {
            lastRoll = -1;
        }

        #endregion
    }
}
