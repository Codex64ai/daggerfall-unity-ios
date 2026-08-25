// Project:         Daggerfall Unity iOS touch port
// Copyright:       Copyright (c) 2009-2023 Daggerfall Workshop
// License:         MIT License (LICENSE file)
//
// Derived from Tedious Travel by TheNewBob / Jedidia, used under the MIT License:
//     MIT License, Copyright (c) 2018 TheNewBob
//     https://github.com/TheNewBob/TediousTravel
//
// Adapted for this port: built in rather than a mod (no ModSettings, no [Invoke]), hooks the
// vanilla travel popup instead of forking the travel map window, no reflection into engine
// privates, and every exit path is funnelled through one Stop() so time scale and the camera
// cannot be left in a travelling state.

using System;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Weather;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Real travel: walk to the destination under accelerated time instead of teleporting.
    ///
    /// WHY THIS HOOKS THE POPUP AND NOT THE MAP
    /// The mod this derives from replaced the travel map window with a 1,958-line fork in
    /// order to add a travel button. Forking it here would mean inheriting six years of
    /// engine drift and colliding head-on with this port's own touch and classic-HUD work in
    /// that exact window. Instead the vanilla map and popup are left alone and the single
    /// moment that matters is diverted - the instant the popup would teleport. Everything the
    /// player already chose (cautious speed, transport, inn vs camping) is read straight off
    /// the popup, so the options keep their vanilla meaning.
    /// </summary>
    public class MobileJourneyController : MonoBehaviour
    {
        // Time compression. 1 is real time; the journey runs many game-hours per real second.
        // fixedDeltaTime MUST scale with it - Unity's physics step is fixed, so leaving it at
        // 0.02 while timeScale is 20 gives physics 20x the simulated distance per step and
        // the player tunnels through terrain and walks over water.
        public const int DefaultTimeCompression = 20;
        public const int MinTimeCompression = 1;
        public const int MaxTimeCompression = 50;

        // Cautious travel's safety net, matching the vanilla mod defaults.
        const int defaultMaxAvoidChance = 95;
        const int defaultHealthMinPercent = 5;
        const int defaultFatigueMin = 5;

        // Grace period after successfully slipping past an encounter, in classic minutes.
        // Without it the same nearby enemy re-triggers the check on the very next frame and
        // the journey stutters to a halt anyway.
        const uint avoidGraceClassicMinutes = 10;

        static MobileJourneyController instance;
        public static MobileJourneyController Instance { get { return instance; } }
        public static bool HasInstance { get { return instance != null; } }

        /// <summary>Player preference: walk journeys, or keep classic instant fast travel.</summary>
        public static bool JourneyModeEnabled { get; set; }

        public int TimeCompression { get; set; }
        public bool IsTravelling { get { return pilot != null; } }
        public string DestinationName { get { return destinationName; } }

        MobileJourneyPilot pilot;
        MobileJourneyWindow window;
        ContentReader.MapSummary destinationSummary;
        string destinationName;
        bool destinationValid;

        float baseFixedDeltaTime;
        int diseaseCount;
        bool combatDelayed;
        uint combatDelayUntil;

        // Weather particle systems are detached during a journey and put back afterwards.
        // Held here because the weather manager's own references are nulled while suppressed.
        GameObject rainParticles;
        GameObject snowParticles;
        bool weatherSuppressed;

        /// <summary>
        /// Own host object rather than a component on the HUD. A journey has to survive the
        /// HUD being torn down and rebuilt - entering a building, opening the classic menu -
        /// and losing the controller mid-journey would strand the game at 20x time scale with
        /// the camera still locked.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (HasInstance)
                return;

            GameObject host = new GameObject("MobileJourneyController");
            host.AddComponent<MobileJourneyController>();
            DontDestroyOnLoad(host);
        }

        void Awake()
        {
            instance = this;
            baseFixedDeltaTime = Time.fixedDeltaTime;
            TimeCompression = DefaultTimeCompression;
        }

        void OnDestroy()
        {
            // A journey in progress when the object dies would otherwise leave Time.timeScale
            // permanently accelerated - the whole game left running at 20x.
            if (IsTravelling)
                Stop(JourneyEnd.Cancelled);

            if (instance == this)
                instance = null;
        }

        void Start()
        {
            // A destination is meaningless across a save load or a new character.
            SaveLoadManager.OnLoad += (saveData) => ForgetDestination();
            StartGameBehaviour.OnNewGame += ForgetDestination;
            GameManager.OnEncounter += OnEncounter;
        }

        #region Begin

        /// <summary>
        /// Called from the travel popup at the instant it would teleport. Returns true if a
        /// journey took over, in which case the caller must NOT fast travel.
        /// </summary>
        public static bool TryBeginJourney(DaggerfallTravelPopUp popup)
        {
            if (!JourneyModeEnabled || !HasInstance || popup == null)
                return false;

            return Instance.BeginJourney(popup.EndPos);
        }

        bool BeginJourney(DFPosition endPos)
        {
            if (endPos == null || IsTravelling)
                return false;

            // The pilot needs a location to aim at. A map pixel with no location on it - open
            // wilderness, or a sea route - has nothing to walk to, so those trips fall back to
            // classic fast travel rather than sending the player off toward empty terrain.
            ContentReader.MapSummary summary;
            if (!DaggerfallUnity.Instance.ContentReader.HasLocation(endPos.X, endPos.Y, out summary))
                return false;

            DFLocation location;
            if (!DaggerfallUnity.Instance.ContentReader.GetLocation(
                    summary.RegionIndex, summary.MapIndex, out location))
                return false;

            destinationSummary = summary;
            destinationName = location.Name;
            destinationValid = true;

            return Resume();
        }

        /// <summary>Start (or restart) walking to the stored destination.</summary>
        public bool Resume()
        {
            if (!destinationValid || IsTravelling)
                return false;

            try
            {
                pilot = new MobileJourneyPilot(destinationSummary);
            }
            catch (ArgumentException e)
            {
                // Destination not present in map data. Better to say so and leave the player
                // standing still than to strand a half-initialised journey.
                Debug.LogWarning("Journey could not start: " + e.Message);
                ForgetDestination();
                return false;
            }

            pilot.OnArrival += () => Stop(JourneyEnd.Arrived);

            diseaseCount = GameManager.Instance.PlayerEffectManager.DiseaseCount;
            SuppressJourneyNoise();
            SuppressWeather();
            SetTimeScale(TimeCompression);
            ShowJourneyWindow();
            return true;
        }

        /// <summary>
        /// Put the travel bar on screen. Created fresh each journey rather than kept: the
        /// window caches label references built against a UI stack that does not survive a
        /// scene change, and a stale one renders as an empty bar.
        /// </summary>
        void ShowJourneyWindow()
        {
            if (!DaggerfallUI.HasInstance)
                return;

            window = new MobileJourneyWindow(DaggerfallUI.UIManager);
            DaggerfallUI.UIManager.PushWindow(window);
        }

        void CloseJourneyWindow()
        {
            if (window == null)
                return;

            MobileJourneyWindow closing = window;

            // Cleared BEFORE closing. Closing raises OnPop, which calls back into Stop() to
            // treat a closed bar as an interrupt - without this the two would call each other
            // until the stack gave out.
            window = null;

            if (closing.IsShowing)
                closing.CloseWindow();
        }

        #endregion

        #region Update

        void Update()
        {
            if (pilot == null)
                return;

            pilot.Update();

            // pilot.Update() may have arrived and stopped us mid-frame.
            if (pilot == null)
                return;

            if (CheckVitals())
                return;
            if (CheckDisease())
                return;
            CheckEnemies();
        }

        /// <summary>
        /// Cautious travel stops rather than letting the player arrive dead. Reckless travel
        /// accepts the risk, which is the whole point of choosing it.
        /// </summary>
        bool CheckVitals()
        {
            if (!SpeedCautious)
                return false;

            PlayerEntity player = GameManager.Instance.PlayerEntity;

            bool healthLow = player.MaxHealth > 0 &&
                             player.CurrentHealth * 100 / player.MaxHealth <= defaultHealthMinPercent;
            bool fatigueLow = player.CurrentFatigue <= defaultFatigueMin;

            if (!healthLow && !fatigueLow)
                return false;

            Stop(JourneyEnd.Interrupted);
            DaggerfallUI.MessageBox(healthLow
                ? "You are too badly hurt to continue your journey."
                : "You are too exhausted to continue your journey.");
            return true;
        }

        bool CheckDisease()
        {
            int current = GameManager.Instance.PlayerEffectManager.DiseaseCount;
            if (current <= diseaseCount)
            {
                diseaseCount = current;
                return false;
            }

            diseaseCount = current;
            Stop(JourneyEnd.Interrupted);
            DaggerfallUI.Instance.CreateHealthStatusBox(
                DaggerfallUI.Instance.UserInterfaceManager.TopWindow).Show();
            return true;
        }

        void CheckEnemies()
        {
            if (combatDelayed)
            {
                if (DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime() >= combatDelayUntil)
                    combatDelayed = false;
                return;
            }

            if (!GameManager.Instance.AreEnemiesNearby())
                return;

            // Reached when the core spawns enemies nearby without raising OnEncounter. Quest
            // encounters raise the event instead and are handled there.
            if (SpeedCautious)
                AttemptAvoid();
            else
            {
                Stop(JourneyEnd.Interrupted);
                DaggerfallUI.MessageBox("An enemy is seeking to bring a premature end to your journey...");
            }
        }

        /// <summary>
        /// Cautious travel tries to slip past. Running or Stealth carries it, whichever is
        /// better, scaled so even a master cannot be certain.
        /// </summary>
        void AttemptAvoid()
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int skill = Mathf.Max(player.Skills.GetLiveSkillValue(DFCareer.Skills.Running),
                                  player.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth));
            int chance = skill * defaultMaxAvoidChance / 100;

            if (Dice100.SuccessRoll(chance))
            {
                combatDelayed = true;
                combatDelayUntil = DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime()
                                   + avoidGraceClassicMinutes;
                return;
            }

            Stop(JourneyEnd.Interrupted);
            DaggerfallUI.MessageBox("You failed to avoid an encounter!");
        }

        void OnEncounter()
        {
            if (!IsTravelling)
                return;

            Stop(JourneyEnd.Interrupted);
            DaggerfallUI.MessageBox("You interrupt your journey.");
        }

        #endregion

        #region Stop

        public enum JourneyEnd
        {
            Arrived,        // reached the destination; nothing left to resume
            Interrupted,    // stopped en route; destination kept so travel can resume
            Cancelled,      // player gave up; destination discarded
        }

        /// <summary>
        /// The single exit path. Everything that ends a journey comes through here, because
        /// each of these four undos matters and skipping any one leaves the game visibly
        /// broken - stuck at 20x speed, or with a dead camera.
        /// </summary>
        public void Stop(JourneyEnd reason)
        {
            SetTimeScale(1);

            if (pilot != null)
            {
                pilot.Release();
                pilot = null;
            }

            CloseJourneyWindow();
            RestoreJourneyNoise();
            RestoreWeather();

            if (reason == JourneyEnd.Arrived)
            {
                DaggerfallUI.Instance.DaggerfallHUD.SetMidScreenText(
                    "You have arrived at your destination", 5f);
                ForgetDestination();
            }
            else if (reason == JourneyEnd.Cancelled)
            {
                ForgetDestination();
            }
            // Interrupted deliberately keeps the destination, so the travel map can offer to
            // resume rather than making the player pick the same place again.
        }

        void ForgetDestination()
        {
            destinationValid = false;
            destinationName = null;
        }

        #endregion

        #region World state

        /// <summary>
        /// Pure, static, and therefore testable headlessly. Below 1x time would run backwards;
        /// far above 50x the player outruns terrain streaming and walks into unloaded world.
        /// </summary>
        public static int ClampCompression(int scale)
        {
            return Mathf.Clamp(scale, MinTimeCompression, MaxTimeCompression);
        }

        void SetTimeScale(int scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = scale * baseFixedDeltaTime;
        }

        /// <summary>
        /// Change travel speed, taking effect immediately if a journey is already running.
        /// Clamped: below 1x time would run backwards, and far above 50x the player outruns
        /// terrain streaming and walks into unloaded world.
        /// </summary>
        public void SetTimeCompression(int scale)
        {
            TimeCompression = ClampCompression(scale);

            if (IsTravelling)
                SetTimeScale(TimeCompression);
        }

        bool SpeedCautious { get; set; }

        /// <summary>Read the player's chosen travel options off the vanilla popup.</summary>
        public void AdoptTravelOptions(DaggerfallTravelPopUp popup)
        {
            if (popup != null)
                SpeedCautious = popup.SpeedCautious;
        }

        /// <summary>
        /// Footsteps at 20x are a machine-gun rattle, and a horse's neigh every few frames is
        /// worse. Both are silenced for the journey rather than played faster.
        /// </summary>
        void SuppressJourneyNoise()
        {
            PlayerFootsteps footsteps = GetFootsteps();
            if (footsteps != null)
                footsteps.enabled = false;

            if (GameManager.Instance.TransportManager != null)
                GameManager.Instance.TransportManager.RidingVolumeScale = 0f;
        }

        void RestoreJourneyNoise()
        {
            PlayerFootsteps footsteps = GetFootsteps();
            if (footsteps != null)
                footsteps.enabled = true;

            if (GameManager.Instance.TransportManager != null)
                GameManager.Instance.TransportManager.RidingVolumeScale = 1f;
        }

        static PlayerFootsteps GetFootsteps()
        {
            if (!GameManager.HasInstance || GameManager.Instance.PlayerActivate == null)
                return null;

            return GameManager.Instance.PlayerActivate.GetComponentInParent<PlayerFootsteps>();
        }

        /// <summary>
        /// Rain and snow particles are emitted per frame, so at 20x they cost 20x for a view
        /// the player is travelling past anyway. Detached for the journey and put back after.
        /// </summary>
        void SuppressWeather()
        {
            if (weatherSuppressed || !GameManager.HasInstance ||
                GameManager.Instance.WeatherManager == null)
                return;

            PlayerWeather weather = GameManager.Instance.WeatherManager.PlayerWeather;
            if (weather == null)
                return;

            rainParticles = weather.RainParticles;
            snowParticles = weather.SnowParticles;

            if (rainParticles != null) rainParticles.SetActive(false);
            if (snowParticles != null) snowParticles.SetActive(false);

            weather.RainParticles = null;
            weather.SnowParticles = null;
            weatherSuppressed = true;
        }

        void RestoreWeather()
        {
            if (!weatherSuppressed)
                return;

            weatherSuppressed = false;

            if (!GameManager.HasInstance || GameManager.Instance.WeatherManager == null)
                return;

            PlayerWeather weather = GameManager.Instance.WeatherManager.PlayerWeather;
            if (weather == null)
                return;

            weather.RainParticles = rainParticles;
            weather.SnowParticles = snowParticles;

            // Re-activate only what the CURRENT weather calls for. Restoring whatever was
            // running when the journey began would leave rain falling in clear skies after a
            // three-day trip.
            bool rain = weather.WeatherType == WeatherType.Rain ||
                        weather.WeatherType == WeatherType.Thunder;
            bool snow = weather.WeatherType == WeatherType.Snow;

            if (rainParticles != null) rainParticles.SetActive(rain);
            if (snowParticles != null) snowParticles.SetActive(snow);
        }

        #endregion
    }
}
