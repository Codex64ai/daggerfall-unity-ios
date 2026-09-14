// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: native autosave. Daggerfall Unity has a quick save and a save window and nothing
// else; on a tablet the app is killed by the OS whenever it goes to the background under
// memory pressure, and a player who never thinks about saving loses hours. This keeps three
// rotating saves of its own and writes the oldest of them at the moments a loss hurts most.
//
// Design notes worth keeping:
//
//  * THREE SLOTS, OLDEST FIRST. One autosave slot is a trap: the save written on entering a
//    dungeon overwrites the one from before the fight that is about to go wrong. Three named
//    saves ("Autosave 1".."Autosave 3") rotate oldest-first, so there is always a save from
//    two events ago to fall back to. They are ordinary saves in every respect - same folders,
//    same SaveInfo, listed and loadable in the normal load window.
//
//  * MANUAL SAVES ARE NEVER TOUCHED. The rotation only ever picks a folder whose SaveInfo
//    carries one of the three autosave names for the current character. QuickSave has its own
//    name and is left alone too.
//
//  * THE GUARD RAILS ARE THE FEATURE. An autosave taken mid-fight, or one frame before the
//    player dies, is worse than no autosave: it is a save the player cannot escape. Every
//    trigger therefore goes through CanSaveNow, and a blocked trigger is remembered and taken
//    at the first clear frame rather than dropped.
//
//  * THE DECISIONS ARE PURE FUNCTIONS. NextSlot and CanSaveNow take plain arguments and are
//    exercised by the self test; the MonoBehaviour half only collects the arguments. Nothing
//    here can be tested by running a game headlessly, so this split is what makes it testable.

using System;
using UnityEngine;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileAutosave : MonoBehaviour
    {
        #region Constants

        /// <summary>Number of rotating autosave slots.</summary>
        public const int SlotCount = 3;

        /// <summary>Save names are "Autosave 1".."Autosave 3" - shown verbatim in the load window.</summary>
        public const string SlotPrefix = "Autosave ";

        /// <summary>Shortest gap between two autosaves. A burst of transitions collapses into one save.</summary>
        public const float MinSecondsBetweenSaves = 60f;

        /// <summary>Interval setting bounds. 0 turns the timer off; the master switch is separate.</summary>
        public const int MinIntervalMinutes = 0;
        public const int MaxIntervalMinutes = 60;

        /// <summary>
        /// A save that never reports back must not wedge the feature forever. SaveLoadManager
        /// raises OnSave at the end of its coroutine; if a mod's save handler throws before that,
        /// the flag is released after this long instead.
        /// </summary>
        const float saveWatchdogSeconds = 20f;

        #endregion

        #region Triggers

        public enum Trigger
        {
            None,
            Timer,
            Travel,
            DungeonEnter,
            DungeonExit,
        }

        /// <summary>The word that goes in the log line and tells one save apart from another.</summary>
        public static string TriggerName(Trigger t)
        {
            switch (t)
            {
                case Trigger.Timer: return "timer";
                case Trigger.Travel: return "travel";
                case Trigger.DungeonEnter: return "dungeon enter";
                case Trigger.DungeonExit: return "dungeon exit";
                default: return "none";
            }
        }

        #endregion

        #region Pure decisions

        /// <summary>
        /// PURE. Which of the three slots the next autosave goes to.
        ///
        /// An empty slot always wins, lowest first, so the first three autosaves of a character
        /// fill 1, 2, 3 in order and the player sees the rotation working. After that the oldest
        /// save is the one that can be spared. A tie (two saves with the same timestamp, or two
        /// empty slots) takes the lower index, so the choice is deterministic and the self test
        /// can pin it.
        /// </summary>
        /// <param name="slotTimes">
        /// One entry per slot, in slot order; null where that slot holds no save yet.
        /// </param>
        /// <returns>Zero-based slot index, always within the array.</returns>
        public static int NextSlot(DateTime?[] slotTimes)
        {
            if (slotTimes == null || slotTimes.Length == 0)
                return 0;

            // An empty slot is free real estate - take the first one.
            for (int i = 0; i < slotTimes.Length; i++)
            {
                if (!slotTimes[i].HasValue)
                    return i;
            }

            int oldest = 0;
            for (int i = 1; i < slotTimes.Length; i++)
            {
                if (slotTimes[i].Value < slotTimes[oldest].Value)
                    oldest = i;
            }
            return oldest;
        }

        /// <summary>
        /// PURE. Why an autosave cannot be taken right now, or null when it can.
        ///
        /// Kept as the reason rather than a bare bool so the deferred log line can say what is in
        /// the way; <see cref="CanSaveNow"/> is this function asked as a yes/no question. The
        /// order is the order the reasons are worth reporting in: the master switch first (there
        /// is nothing to report about a feature that is off), then the states that make a save
        /// actively harmful, then the ones that merely make it badly timed.
        /// </summary>
        public static string BlockReason(bool masterOn, bool gameStarted, bool playerDead,
                                         bool enemiesNearby, bool inCombat, bool uiOpen,
                                         bool journeyDriving, bool saveInProgress)
        {
            if (!masterOn) return "autosave is off";
            if (!gameStarted) return "no game in progress";
            if (playerDead) return "the player is dead";

            // A save taken with a foe swinging at the player reloads into that same swing, with
            // no chance to have done anything differently. Both halves matter: enemies nearby is
            // the situation, attacking is the instant.
            if (enemiesNearby) return "enemies nearby";
            if (inCombat) return "weapon in use";

            // Anything above the HUD - an inventory, a message box, the travel map - means the
            // player is mid-decision, and several of those windows mutate state on close.
            if (uiOpen) return "a window is open";

            // The autopilot walks the player at up to 20x time scale with the camera driven for
            // them; a save there reloads into a journey that no longer exists.
            if (journeyDriving) return "travelling";

            if (saveInProgress) return "a save is already running";
            return null;
        }

        /// <summary>
        /// PURE. <see cref="BlockReason"/> as a yes/no. Every trigger goes through this.
        /// </summary>
        public static bool CanSaveNow(bool masterOn, bool gameStarted, bool playerDead,
                                      bool enemiesNearby, bool inCombat, bool uiOpen,
                                      bool journeyDriving, bool saveInProgress)
        {
            return BlockReason(masterOn, gameStarted, playerDead, enemiesNearby, inCombat,
                               uiOpen, journeyDriving, saveInProgress) == null;
        }

        /// <summary>
        /// PURE. The interval setting as the game will use it. 0 means the timer is off; anything
        /// longer than an hour is not an autosave interval, it is a setting someone mistyped.
        /// </summary>
        public static int ClampIntervalMinutes(int minutes)
        {
            return Mathf.Clamp(minutes, MinIntervalMinutes, MaxIntervalMinutes);
        }

        /// <summary>
        /// PURE. Has enough real time passed since the last autosave for another one?
        /// A negative "never saved" sentinel always passes.
        /// </summary>
        public static bool ThrottleOpen(float secondsSinceLastSave)
        {
            return secondsSinceLastSave < 0f || secondsSinceLastSave >= MinSecondsBetweenSaves;
        }

        /// <summary>PURE. The save name for a zero-based slot index.</summary>
        public static string SlotName(int index)
        {
            return SlotPrefix + (index + 1);
        }

        /// <summary>PURE. True when the save name is one of ours - the rotation touches nothing else.</summary>
        public static bool IsAutosaveName(string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
                return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (saveName == SlotName(i))
                    return true;
            }
            return false;
        }

        #endregion

        #region Fields

        static MobileAutosave instance;
        public static bool HasInstance { get { return instance != null; } }

        float playSeconds;              // real seconds of unpaused play since the last autosave
        float sinceLastSave = -1f;      // real seconds since the last autosave; -1 = none this session
        Trigger pending = Trigger.None; // a trigger that fired while blocked, waiting for a clear frame
        bool deferredLogged;            // one "deferred" line per pending trigger, not one per frame
        bool saveInProgress;
        float saveStarted;

        #endregion

        #region Install

        /// <summary>
        /// One host object for the life of the app, as MobileJourneyController does. Autosave has
        /// to outlive the scene swap between the title screen and the game, and the trigger events
        /// it listens to are static.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            GameObject host = new GameObject("MobileAutosave");
            host.AddComponent<MobileAutosave>();
            DontDestroyOnLoad(host);
        }

        void Awake()
        {
            instance = this;
        }

        void OnEnable()
        {
            // Static engine events: subscribing twice would save twice, and not unsubscribing
            // would fire into a destroyed instance after a return to the menu.
            DaggerfallTravelPopUp.OnPostFastTravel += OnFastTravelArrived;
            MobileJourneyController.OnJourneyArrived += OnJourneyArrived;
            PlayerEnterExit.OnTransitionDungeonInterior += OnDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnDungeonExterior;
            SaveLoadManager.OnSave += OnSaveFinished;
        }

        void OnDisable()
        {
            DaggerfallTravelPopUp.OnPostFastTravel -= OnFastTravelArrived;
            MobileJourneyController.OnJourneyArrived -= OnJourneyArrived;
            PlayerEnterExit.OnTransitionDungeonInterior -= OnDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior -= OnDungeonExterior;
            SaveLoadManager.OnSave -= OnSaveFinished;
        }

        void OnDestroy()
        {
            // OnDisable already ran the unsubscribes; this only clears the singleton so a fresh
            // Install can happen if the host is ever destroyed deliberately.
            OnDisable();
            if (instance == this)
                instance = null;
        }

        #endregion

        #region Settings

        /// <summary>Master switch. Read live so the setting takes effect without a relaunch.</summary>
        public static bool Enabled
        {
            get { return DaggerfallUnity.Settings != null && DaggerfallUnity.Settings.Autosave; }
        }

        static bool TravelTriggerOn
        {
            get { return DaggerfallUnity.Settings != null && DaggerfallUnity.Settings.AutosaveOnTravel; }
        }

        static bool DungeonTriggerOn
        {
            get { return DaggerfallUnity.Settings != null && DaggerfallUnity.Settings.AutosaveOnDungeon; }
        }

        static int IntervalMinutes
        {
            get
            {
                return DaggerfallUnity.Settings == null
                    ? 0
                    : ClampIntervalMinutes(DaggerfallUnity.Settings.AutosaveIntervalMinutes);
            }
        }

        #endregion

        #region Trigger handlers

        void OnFastTravelArrived()
        {
            if (TravelTriggerOn)
                Request(Trigger.Travel);
        }

        void OnJourneyArrived()
        {
            if (TravelTriggerOn)
                Request(Trigger.Travel);
        }

        void OnDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            if (DungeonTriggerOn)
                Request(Trigger.DungeonEnter);
        }

        void OnDungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            if (DungeonTriggerOn)
                Request(Trigger.DungeonExit);
        }

        /// <summary>
        /// A trigger asks for a save; Update decides when it happens. Nothing saves inside an
        /// event handler - the dungeon transition events fire while the player is still being
        /// moved, and the world one frame later is the one worth recording.
        /// </summary>
        void Request(Trigger trigger)
        {
            if (trigger == Trigger.None || !Enabled)
                return;

            // The newest trigger names the state the save will actually capture, so it wins; the
            // deferred line is re-armed with it.
            if (pending != trigger)
                deferredLogged = false;
            pending = trigger;
        }

        #endregion

        #region Update

        void Update()
        {
            if (saveInProgress && Time.realtimeSinceStartup - saveStarted > saveWatchdogSeconds)
            {
                Debug.LogWarning("[Autosave] the save never reported back; releasing the lock");
                saveInProgress = false;
            }

            bool playing = GameManager.HasInstance && GameManager.Instance.IsPlayingGame();

            // Real play time, not game time: a journey runs the clock at 20x and resting runs it
            // at thousands, and neither is a reason to save more often. IsPlayingGame is false
            // while paused or under any window that pauses, which is exactly "not playing".
            if (playing)
                playSeconds += Time.unscaledDeltaTime;

            if (sinceLastSave >= 0f)
                sinceLastSave += Time.unscaledDeltaTime;

            int interval = IntervalMinutes;
            if (Enabled && interval > 0 && playSeconds >= interval * 60f)
            {
                playSeconds = 0f;
                Request(Trigger.Timer);
            }

            if (pending == Trigger.None)
                return;

            if (!ThrottleOpen(sinceLastSave))
            {
                Defer("less than " + (int)MinSecondsBetweenSaves + "s since the last autosave");
                return;
            }

            string reason = LiveBlockReason();
            if (reason != null)
            {
                Defer(reason);
                return;
            }

            Trigger trigger = pending;
            pending = Trigger.None;
            deferredLogged = false;
            SaveNow(trigger);
        }

        void Defer(string reason)
        {
            if (deferredLogged)
                return;
            deferredLogged = true;
            Debug.Log("[Autosave] deferred (" + reason + ")");
        }

        /// <summary>
        /// <see cref="BlockReason"/> asked of the running game, plus the two gates that only the
        /// engine can answer and that have no place in a pure function: the save system being
        /// ready at all, and a quest or mod having forbidden saving (PreventSaveConditions - the
        /// same gate the quick save obeys).
        /// </summary>
        string LiveBlockReason()
        {
            SaveLoadManager slm = SaveLoadManager.Instance;
            if (slm == null || !slm.IsReady() || slm.LoadInProgress)
                return "the save system is not ready";
            if (slm.IsSavingPrevented)
                return "saving is prevented here";

            bool playerDead = false;
            bool enemiesNearby = false;
            bool inCombat = false;
            bool uiOpen = false;

            if (GameManager.HasInstance)
            {
                GameManager gm = GameManager.Instance;

                if (gm.PlayerDeath != null && gm.PlayerDeath.DeathInProgress)
                    playerDead = true;
                if (gm.PlayerEntity != null && gm.PlayerEntity.CurrentHealth <= 0)
                    playerDead = true;

                enemiesNearby = gm.AreEnemiesNearby();

                if (gm.WeaponManager != null && gm.WeaponManager.ScreenWeapon != null)
                    inCombat = gm.WeaponManager.ScreenWeapon.IsAttacking();

                uiOpen = !gm.IsPlayerOnHUD;
            }

            return BlockReason(Enabled,
                               GameManager.HasInstance && GameManager.Instance.IsPlayingGame(),
                               playerDead, enemiesNearby, inCombat, uiOpen,
                               MobileJourneyPilot.Active, saveInProgress);
        }

        #endregion

        #region Saving

        void SaveNow(Trigger trigger)
        {
            string characterName = GameManager.Instance.PlayerEntity.Name;
            if (string.IsNullOrEmpty(characterName))
                return;

            int slot = NextSlot(SlotTimes(characterName));
            string slotName = SlotName(slot);

            saveInProgress = true;
            saveStarted = Time.realtimeSinceStartup;
            sinceLastSave = 0f;
            playSeconds = 0f;

            try
            {
                SaveLoadManager.Instance.Save(characterName, slotName);
            }
            catch (Exception ex)
            {
                saveInProgress = false;
                Debug.LogWarning("[Autosave] save failed: " + ex.Message);
                return;
            }

            Debug.Log("[Autosave] saved '" + slotName + "' (" + TriggerName(trigger) + ")");
            DaggerfallUI.AddHUDText("Autosaved");
        }

        void OnSaveFinished(SaveData_v1 saveData)
        {
            saveInProgress = false;
        }

        /// <summary>
        /// When each of the three slots was last written, in slot order, null where the slot is
        /// empty. Reads the same enumeration the load window does, so a save deleted by hand from
        /// the load window shows up here as an empty slot on the next rotation.
        /// </summary>
        DateTime?[] SlotTimes(string characterName)
        {
            DateTime?[] times = new DateTime?[SlotCount];

            SaveLoadManager slm = SaveLoadManager.Instance;
            if (slm == null)
                return times;

            slm.EnumerateSaves();

            for (int i = 0; i < SlotCount; i++)
            {
                int key = slm.FindSaveFolderByNames(characterName, SlotName(i));
                if (key == -1)
                    continue;

                SaveInfo_v1 info = slm.GetSaveInfo(key);
                if (info == null || info.dateAndTime == null)
                    continue;

                times[i] = TicksToDate(info.dateAndTime.realTime);
            }

            return times;
        }

        /// <summary>
        /// SaveInfo stores DateTime.Now.Ticks. A save written by a different build, or a file
        /// edited by hand, can carry a value outside DateTime's range - treat that as "very old"
        /// rather than throwing out of the rotation.
        /// </summary>
        public static DateTime TicksToDate(long ticks)
        {
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                return DateTime.MinValue;

            return new DateTime(ticks);
        }

        #endregion
    }
}
