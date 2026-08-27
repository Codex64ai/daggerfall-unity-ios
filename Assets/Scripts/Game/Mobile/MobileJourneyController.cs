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
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
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
        // Lowered from 20/50 on device evidence: 21x already outran terrain streaming on an
        // M4 iPad and produced untextured ground. The throttle below is the real protection,
        // but a lower ceiling means fewer people meet the problem at all.
        public const int DefaultTimeCompression = 20;
        public const int MinTimeCompression = 1;

        // Cautious travel is watchable travel: 50x is about the fastest the world can still
        // be seen going by. Reckless throws that away along with the safety, and the trade is
        // honest - at 200x the player outruns terrain streaming badly and the journey becomes
        // a blur of throttle bursts. That is the player's choice to make, not ours to forbid.
        public const int MaxCautiousCompression = 50;
        public const int MaxRecklessCompression = 200;

        public static int MaxTimeCompression
        {
            get
            {
                return (HasInstance && !Instance.SpeedCautious)
                    ? MaxRecklessCompression : MaxCautiousCompression;
            }
        }

        // Speed used while the streaming world is catching up, and how long terrain must stay
        // settled before full speed resumes.
        // Raised from 3x. The throttle exists to stop the player outrunning terrain, not to
        // make journeys crawl, and 3x meant every terrain build felt like a stall. 8x still
        // gives streaming a large head start while remaining visibly travel-paced.
        const int throttledCompression = 8;

        // Ceiling on the physics step. 0.05s is 2.5x the default 0.02 - loose enough to keep
        // the step count affordable, tight enough that a character controller still resolves
        // slopes and stairs instead of jamming.
        // Ceiling on the physics step. Steps per real second are timeScale / fixedDeltaTime,
        // so a hard 0.05 cap costs 1000 steps/s at 50x - unshippable. This is the compromise:
        // small enough that a CharacterController still resolves slopes (the 0.24s steps that
        // came from scaling linearly jammed the player outright), large enough that high
        // compression stays affordable.
        const float maxFixedDeltaTime = 0.10f;
        // Shortened from 0.35s. Terrain builds in bursts while travelling, so a long settle
        // requirement meant the journey spent most of its time throttled - which is what "it's
        // slow" was measuring. Short enough to recover promptly, long enough not to oscillate.
        const float terrainSettleSeconds = 0.15f;

        // How far to look for a road when snapping the ends of a journey onto the network.
        //
        // Generous on purpose. The pilot walks overland to the first waypoint and overland from
        // the last one to the destination, so a distant snap costs nothing but a stretch of
        // open country at each end - whereas a tight radius threw away the ENTIRE road route
        // whenever either end happened to be off-network. Most of a long journey being on a
        // road is worth a few pixels of field at the start and finish.
        const int snapRadius = 20;

        // Cautious travel's safety net, matching the vanilla mod defaults.
        const int defaultMaxAvoidChance = 95;
        const int defaultHealthMinPercent = 5;

        // PERCENT, not an absolute value. This was 5 flat, which looked reasonable and was
        // wrong by a factor of 64: DaggerfallEntity.FatigueMultiplier means fatigue is stored
        // x64, so on a typical character 5 is 5 out of ~6400 - about 0.08%. The guard could
        // never fire, and the player walked until the engine's own exhaustion collapse.
        //
        // 20% rather than something tighter because stopping has to be USEFUL: a journey that
        // halts at 5% leaves the player collapsing again a minute after they resume. At 20%
        // there is room to make camp, rest, and carry on.
        const int defaultFatigueMinPercent = 20;

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
        PlayerEntity exhaustedPlayer;
        bool promptOpen;

        // Places already offered this journey, by map id, so passing the same hamlet twice
        // does not ask twice. Cleared per journey rather than kept: on a later trip through
        // the same country the offer is worth making again.
        readonly HashSet<int> offeredPlaces = new HashSet<int>();

        // The road route for this journey, and how far along it we are. Empty means travelling
        // straight to the destination, which is what happens when no road route exists.
        List<DFPosition> route;
        int routeStep;

        /// <summary>How much of the road route is left, for the travel bar.</summary>
        public int RouteRemaining
        {
            get { return (route == null) ? 0 : Mathf.Max(0, route.Count - routeStep); }
        }

        public bool FollowingRoad { get { return route != null && routeStep < route.Count; } }
        bool askedToCampTonight;
        bool wasNight;
        ContentReader.MapSummary destinationSummary;
        string destinationName;
        bool destinationValid;

        // TERRAIN THROTTLE
        // Time compression multiplies PHYSICAL movement, not just the clock - at 21x the
        // player crosses map pixels 21x faster than StreamingWorld can build and paint
        // terrain, and walks into geometry that has no texture yet. Device report: a large
        // untextured wedge across the lower half of the view.
        //
        // So a journey yields to the world. While terrain is being built, compression drops
        // to a crawl; when the world catches up, full speed resumes. The journey regulates
        // itself instead of guessing a safe fixed speed for every device and biome.
        bool terrainBuilding;
        float terrainSettledAt;

        // Journey diagnostics, shown on the travel bar. Three device-only bugs in a row came
        // from state that headless tests cannot see, so the bar reports what it is actually
        // doing rather than leaving us to infer it from a screenshot of the scenery.
        float lastSampleX, lastSampleZ, lastSampleTime;
        float measuredSpeed;          // world units per real second
        public bool TerrainBuilding { get { return terrainBuilding; } }
        public int ActiveCompression { get { return Mathf.RoundToInt(Time.timeScale); } }
        public float MeasuredSpeed { get { return measuredSpeed; } }

        float baseFixedDeltaTime;
        int diseaseCount;
        bool combatDelayed;
        uint combatDelayUntil;

        // Weather particle systems are detached during a journey and put back afterwards.
        // Held here because the weather manager's own references are nulled while suppressed.
        GameObject rainParticles;
        GameObject snowParticles;
        bool weatherSuppressed;

        // CAPTURED, not assumed. Restoring a hardcoded "normal" value is how a journey
        // silently edits the player's game: RidingVolumeScale defaults to 0.6, and putting it
        // back as 1.0 raised the horse volume a little more every trip. Nothing here is ours,
        // so nothing here is restored from a guess.
        float priorRidingVolume = 1f;
        bool priorFootstepsEnabled = true;
        bool noiseSuppressed;

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

            StreamingWorld.OnUpdateTerrainsStart -= OnTerrainBuildStart;
            StreamingWorld.OnUpdateTerrainsEnd -= OnTerrainBuildEnd;

            if (DaggerfallUI.HasInstance)
                DaggerfallUI.UIManager.OnWindowChange -= OnWindowChange;

            if (instance == this)
                instance = null;
        }

        void OnTerrainBuildStart()
        {
            terrainBuilding = true;
        }

        void OnTerrainBuildEnd()
        {
            terrainBuilding = false;

            // Unscaled: the whole point is a real-time settle, and Time.time is being
            // multiplied by the very compression this is trying to govern.
            terrainSettledAt = Time.unscaledTime;
        }

        /// <summary>
        /// Unpause when nothing on screen should be pausing us.
        ///
        /// UserInterfaceManager.RemoveWindow() only unpauses once the stack is back to a single
        /// window. The travel bar IS a window, so it holds the count at two - which means any
        /// OTHER window opened and closed during a journey (the map, an inventory, a message
        /// box) leaves the game paused with the travel bar still showing and nothing moving.
        /// Reported as the MAP button "stopping travel entirely".
        ///
        /// The bar really belongs on the HUD rather than the window stack, which would avoid
        /// this entirely; until then, a journey takes responsibility for undoing a pause that
        /// no visible window is asking for.
        /// </summary>
        void ReleaseStalePause()
        {
            if (!GameManager.HasInstance || !GameManager.IsGamePaused)
                return;

            if (!DaggerfallUI.HasInstance)
                return;

            // Only when OUR bar is what the stack is topped by. Anything else - the map, a
            // prompt, an inventory - is legitimately pausing and must be left alone.
            if (DaggerfallUI.UIManager.TopWindow != window)
                return;

            GameManager.Instance.PauseGame(false);
        }

        /// <summary>
        /// Measure how fast the player is ACTUALLY moving, in world units per real second.
        /// Derived from position rather than asked of the motor, because the question being
        /// answered is "is the player moving at all" - and a motor can report an intended
        /// velocity while a character controller is jammed against a slope going nowhere.
        /// Unscaled time, since scaled time is the thing under suspicion.
        /// </summary>
        void SampleSpeed()
        {
            if (!GameManager.HasInstance || GameManager.Instance.PlayerGPS == null)
                return;

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            float now = Time.unscaledTime;
            float dt = now - lastSampleTime;

            if (dt < 0.25f)
                return;

            if (lastSampleTime > 0f)
            {
                float dx = gps.WorldX - lastSampleX;
                float dz = gps.WorldZ - lastSampleZ;
                measuredSpeed = Mathf.Sqrt(dx * dx + dz * dz) / dt;
            }

            lastSampleX = gps.WorldX;
            lastSampleZ = gps.WorldZ;
            lastSampleTime = now;
        }

        /// <summary>
        /// Compression the world can currently keep up with. Full speed once terrain has been
        /// settled for a moment; a crawl while it is still building.
        ///
        /// The settle delay matters: terrain builds in bursts, so reacting to the very frame a
        /// build ends would snap back to 21x just in time for the next tile to fall behind,
        /// and the journey would oscillate instead of running smoothly.
        /// </summary>
        int SustainableCompression()
        {
            if (terrainBuilding || Time.unscaledTime - terrainSettledAt < terrainSettleSeconds)
                return Mathf.Min(throttledCompression, TimeCompression);

            return TimeCompression;
        }

        void Start()
        {
            // A destination is meaningless across a save load or a new character.
            SaveLoadManager.OnLoad += (saveData) => ForgetDestination();
            StartGameBehaviour.OnNewGame += ForgetDestination;
            GameManager.OnEncounter += OnEncounter;

            // Public static events, so the throttle needs no engine change.
            StreamingWorld.OnUpdateTerrainsStart += OnTerrainBuildStart;
            StreamingWorld.OnUpdateTerrainsEnd += OnTerrainBuildEnd;

            if (DaggerfallUI.HasInstance)
                DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;
        }

        #region Begin

        /// <summary>
        /// Can a journey walk to this popup's destination? Answered WITHOUT starting anything
        /// and without touching the UI, so the caller can still fall back to classic fast
        /// travel. Stores the destination on success, ready for BeginStoredJourney().
        ///
        /// Split from starting the journey because the travel UI has to come down first - see
        /// the call site in DaggerfallTravelPopUp for why.
        /// </summary>
        public static bool CanBeginJourney(DaggerfallTravelPopUp popup)
        {
            if (!JourneyModeEnabled || !HasInstance || popup == null)
                return false;

            return Instance.StoreDestination(popup.EndPos);
        }

        /// <summary>
        /// Start walking to the destination stored by CanBeginJourney. Call only after the
        /// travel windows have closed.
        /// </summary>
        public static bool BeginStoredJourney()
        {
            return HasInstance && Instance.Resume();
        }

        bool StoreDestination(DFPosition endPos)
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
            return true;
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

            PlanRoute();
            pilot.OnArrival += OnPilotArrived;
            pilot.OnBlocked += OnPilotBlocked;

            // Collapsing has to END the journey. Passing out raises time by hours, and with a
            // journey still running the player was walked onward while unconscious and simply
            // woke up at the destination - reported as "it sent me a walk-through of me getting
            // all the way there". Subscribed per journey rather than once, because PlayerEntity
            // is rebuilt on load and a stale handler would point at the previous character.
            exhaustedPlayer = GameManager.Instance.PlayerEntity;
            if (exhaustedPlayer != null)
                exhaustedPlayer.OnExhausted += OnPlayerExhausted;

            offeredPlaces.Clear();
            route = null;
            routeStep = 0;

            // The place we are setting out FROM must not be offered as somewhere to stop.
            // Resuming a journey after stopping in a town would otherwise ask, immediately and
            // absurdly, whether to stop at the town the player is standing in.
            PlayerGPS startGps = GameManager.Instance.PlayerGPS;
            if (startGps != null && startGps.HasCurrentLocation)
                offeredPlaces.Add(startGps.CurrentMapID);
            askedToCampTonight = false;
            wasNight = false;

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

        /// <summary>
        /// Offer to resume an interrupted journey when the player next opens the travel map.
        ///
        /// Without this, an interrupted journey kept its destination but there was no way to
        /// use it - the player had to find the same place on the map and pick it again, which
        /// after being stopped by a bandit three days from anywhere is tedious rather than
        /// atmospheric.
        /// </summary>
        void OnWindowChange(object sender, EventArgs e)
        {
            if (!JourneyModeEnabled || IsTravelling || !destinationValid || promptOpen)
                return;

            if (!DaggerfallUI.HasInstance)
                return;

            IUserInterfaceManager ui = DaggerfallUI.UIManager;
            if (!(ui.TopWindow is DaggerfallTravelMapWindow))
                return;

            promptOpen = true;

            DaggerfallMessageBox prompt = new DaggerfallMessageBox(ui,
                DaggerfallMessageBox.CommonMessageBoxButtons.YesNo,
                "Resume your journey to " + destinationName + "?",
                ui.TopWindow);

            prompt.OnButtonClick += (box, button) =>
            {
                promptOpen = false;
                box.CloseWindow();

                if (button != DaggerfallMessageBox.MessageBoxButtons.Yes)
                    return;

                // Close the map before travelling, for the same reason the travel popup does:
                // the manager only unpauses once the stack is back to the HUD, and a journey
                // started above an open window would be popped by that window closing.
                DaggerfallTravelMapWindow map = ui.TopWindow as DaggerfallTravelMapWindow;
                if (map != null)
                    map.CloseTravelWindows(true);

                Resume();
            };

            // A cancelled box (Back, or a tap outside) must clear the flag too, or the prompt
            // never offers itself again for the rest of the session.
            prompt.OnCancel += (box) => { promptOpen = false; };
            prompt.Show();
        }

        /// <summary>
        /// Work out a road route to the destination, if there is one.
        ///
        /// Both ends are snapped to the network first: journeys almost never start or finish
        /// exactly on a road, so without snapping the search would begin off-network and find
        /// nothing. A failure here is not an error - plenty of destinations have no road to
        /// them - it just means walking straight there, which is what happened before roads.
        /// </summary>
        void PlanRoute()
        {
            route = null;
            routeStep = 0;

            if (!MobileRoads.Enabled || !MobileRoadNetwork.Available)
                return;

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
                return;

            DFPosition here = gps.CurrentMapPixel;
            DFPosition target = MapsFile.GetPixelFromPixelID(destinationSummary.ID);

            DFPosition from = MobileRoadNetwork.NearestPathPixel(here.X, here.Y, snapRadius);
            DFPosition to = MobileRoadNetwork.NearestPathPixel(target.X, target.Y, snapRadius);

            if (from == null || to == null)
                return;

            List<DFPosition> found = MobileRoadNetwork.FindRoute(from.X, from.Y, to.X, to.Y);

            // Worth following only if the road actually saves walking. A three-pixel road
            // reached by a twenty-pixel trudge across country is a worse journey than simply
            // heading for the destination, so compare the route against the detours it costs.
            int detour = Distance(here, from) + Distance(to, target);
            if (found == null || found.Count < 2 || found.Count < detour)
                return;

            route = found;
            routeStep = 0;
            pilot.SetWaypoint(route[0]);

            DaggerfallUI.AddHUDText("You set out along the road.", 3f);
        }

        /// <summary>
        /// The pilot could not get around something after repeated attempts - in practice a
        /// building, since a journey steers a straight bearing and towns are full of walls.
        ///
        /// Stopping is the right answer rather than continuing to shove the player into
        /// masonry. The destination is kept, so the travel map offers to resume once they have
        /// walked clear themselves.
        /// </summary>
        void OnPilotBlocked()
        {
            if (!IsTravelling)
                return;

            Stop(JourneyEnd.Interrupted);
            DaggerfallUI.MessageBox("Your way is blocked. You will have to find your own way " +
                                    "clear before travelling on.");
        }

        /// <summary>Chebyshev distance in map pixels - diagonals cost one step here.</summary>
        static int Distance(DFPosition a, DFPosition b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dy = Mathf.Abs(a.Y - b.Y);
            return Mathf.Max(dx, dy);
        }

        /// <summary>
        /// A waypoint was reached: aim at the next one, or at the destination once the road runs
        /// out. Arriving at the FINAL target is what ends a journey.
        /// </summary>
        void OnPilotArrived()
        {
            if (pilot == null)
                return;

            if (pilot.AtFinalTarget)
            {
                Stop(JourneyEnd.Arrived);
                return;
            }

            routeStep++;

            if (route != null && routeStep < route.Count)
                pilot.SetWaypoint(route[routeStep]);
            else
                pilot.SetFinalTarget();
        }

        #region Update

        void Update()
        {
            if (pilot == null)
            {
                // WATCHDOG. Nothing in this game runs above 1x time except a journey, so a
                // compressed scale with no journey running means something escaped - and the
                // consequence is severe, because the player's own movement is scaled too and a
                // few steps throw them across the landscape.
                //
                // Belt and braces alongside the fix in RestoreNormalTime(): that closes the
                // path we found, this one heals any path we have not. Skipped while paused,
                // where timeScale is legitimately 0.
                if (GameManager.HasInstance && !GameManager.IsGamePaused && Time.timeScale > 1.01f)
                    RestoreNormalTime();

                return;
            }

            // RE-ASSERT THE TIME SCALE EVERY FRAME.
            // GameManager.PauseGame(false) restores Time.timeScale from its own savedTimeScale,
            // so any UI window opening and closing during a journey - inventory, the map, a
            // message box - silently resets travel to 1x. Setting it once at departure is not
            // enough. Same reasoning as re-asserting mouse look in the pilot.
            ReleaseStalePause();

            int target = SustainableCompression();
            if (!Mathf.Approximately(Time.timeScale, target))
                SetTimeScale(target);

            SampleSpeed();

            pilot.Update();

            // pilot.Update() may have arrived and stopped us mid-frame.
            if (pilot == null)
                return;

            if (CheckVitals())
                return;
            if (CheckDisease())
                return;
            if (CheckPassingPlace())
                return;
            if (CheckNightfall())
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
            bool fatigueLow = player.MaxFatigue > 0 &&
                              player.CurrentFatigue * 100 / player.MaxFatigue <= defaultFatigueMinPercent;

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

        /// <summary>
        /// Offer to stop when passing through a settlement that is not the destination.
        ///
        /// This is most of what makes a journey feel like travelling rather than waiting: the
        /// places between here and there become real, and an inn three days out is somewhere
        /// you chose to stop rather than scenery you clipped through at 50x.
        ///
        /// Only settlements, and only once each. Farms, dungeons, temples and graveyards are
        /// skipped - a prompt every time the player passes a shack is an interruption, not a
        /// feature.
        /// </summary>
        bool CheckPassingPlace()
        {
            if (promptOpen || !GameManager.HasInstance)
                return false;

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if (gps == null || !gps.HasCurrentLocation)
                return false;

            int mapId = gps.CurrentMapID;

            // The destination itself is arrival, not a place to be asked about.
            if (mapId == destinationSummary.ID || offeredPlaces.Contains(mapId))
                return false;

            if (!IsSettlement(gps.CurrentLocationType))
            {
                offeredPlaces.Add(mapId);
                return false;
            }

            offeredPlaces.Add(mapId);
            string name = gps.CurrentLocation.Name;

            AskToInterrupt("You are passing " + name + ". Stop here?",
                           "You continue past " + name + ".");
            return true;
        }

        static bool IsSettlement(DFRegion.LocationTypes type)
        {
            return type == DFRegion.LocationTypes.TownCity ||
                   type == DFRegion.LocationTypes.TownHamlet ||
                   type == DFRegion.LocationTypes.TownVillage ||
                   type == DFRegion.LocationTypes.Tavern;
        }

        /// <summary>
        /// Offer to make camp at dusk. Asked once per night, and only when the player chose to
        /// camp out rather than take inns - someone paying for lodging has already said they
        /// would rather not sleep in a field.
        /// </summary>
        bool CheckNightfall()
        {
            if (promptOpen || DaggerfallUnity.Instance.WorldTime == null)
                return false;

            bool night = DaggerfallUnity.Instance.WorldTime.Now.IsNight;

            // Reset at dawn so tomorrow night asks again.
            if (!night)
            {
                wasNight = false;
                askedToCampTonight = false;
                return false;
            }

            if (wasNight || askedToCampTonight || SleepModeInn)
                return false;

            wasNight = true;
            askedToCampTonight = true;

            AskToInterrupt("Night is falling. Make camp here?",
                           "You travel on through the night.");
            return true;
        }

        /// <summary>
        /// Pause the journey and ask. Yes stops travel but KEEPS the destination, so the
        /// travel map will offer to resume; No carries on at the same speed.
        /// </summary>
        void AskToInterrupt(string question, string declineText)
        {
            promptOpen = true;

            DaggerfallMessageBox box = new DaggerfallMessageBox(
                DaggerfallUI.UIManager,
                DaggerfallMessageBox.CommonMessageBoxButtons.YesNo,
                question,
                DaggerfallUI.UIManager.TopWindow);

            box.OnButtonClick += (sender, button) =>
            {
                promptOpen = false;
                sender.CloseWindow();

                if (button == DaggerfallMessageBox.MessageBoxButtons.Yes)
                    Stop(JourneyEnd.Interrupted);
                else if (!string.IsNullOrEmpty(declineText))
                    DaggerfallUI.AddHUDText(declineText, 2f);
            };

            // Dismissing without choosing carries on, and must clear the flag or no further
            // prompt is ever offered.
            box.OnCancel += (sender) => { promptOpen = false; };
            box.Show();
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

        void OnPlayerExhausted(DaggerfallEntity entity)
        {
            if (!IsTravelling)
                return;

            // No message of our own: the engine already shows its exhaustion popup, and a
            // second box on top of it would just be in the way.
            Stop(JourneyEnd.Interrupted);
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
            RestoreNormalTime();

            if (pilot != null)
            {
                pilot.Release();
                pilot = null;
            }

            if (exhaustedPlayer != null)
            {
                exhaustedPlayer.OnExhausted -= OnPlayerExhausted;
                exhaustedPlayer = null;
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

        /// <summary>Pure form for tests: the ceiling depends on the travel speed chosen.</summary>
        public static int ClampCompression(int scale, bool cautious)
        {
            return Mathf.Clamp(scale, MinTimeCompression,
                cautious ? MaxCautiousCompression : MaxRecklessCompression);
        }

        void SetTimeScale(int scale)
        {
            Time.timeScale = scale;

            // DO NOT scale fixedDeltaTime linearly, which is the usual advice for timeScale.
            // It keeps the physics COST constant by making each step simulate more time - at
            // 12x that is a 0.24s step, and a CharacterController asked to move a quarter of a
            // second's travel in one go jams on slopes and tunnels through terrain. The player
            // stops dead while the clock keeps running, which is exactly the reported symptom:
            // "you freeze and time just clicks down".
            //
            // Capped instead, so steps stay small enough for collision to behave. This costs
            // more CPU (more steps per real second) and that is the right trade - a journey
            // that stalls is worthless, a journey that costs frames is merely slower.
            Time.fixedDeltaTime = Mathf.Min(scale * baseFixedDeltaTime, maxFixedDeltaTime);
        }

        /// <summary>
        /// Put time back to normal and make it STAY there.
        ///
        /// Resetting Time.timeScale is not enough on its own. GameManager.PauseGame() snapshots
        /// the time scale when a window opens and replays it when the window closes - so an
        /// encounter that interrupts a journey captures the compressed scale, and dismissing
        /// the message box afterwards restores it, leaving the entire game running fast. The
        /// player's own movement scales with it, so walking a few steps throws them across the
        /// landscape; the device report read as "teleported me far from where I started".
        ///
        /// Correcting the snapshot as well as the live value closes that path.
        /// </summary>
        void RestoreNormalTime()
        {
            SetTimeScale(1);

            if (GameManager.HasInstance)
                GameManager.Instance.SavedTimeScale = 1f;
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
                SetTimeScale(SustainableCompression());
        }

        public bool SpeedCautious { get; private set; }
        public bool SleepModeInn { get; private set; }

        /// <summary>Read the player's chosen travel options off the vanilla popup.</summary>
        public void AdoptTravelOptions(DaggerfallTravelPopUp popup)
        {
            if (popup == null)
                return;

            SpeedCautious = popup.SpeedCautious;
            SleepModeInn = popup.SleepModeInn;

            // Reckless raises the ceiling; switching back to cautious must pull an existing
            // 200x setting down with it, or the next cautious journey inherits a speed it is
            // not allowed to use.
            TimeCompression = ClampCompression(TimeCompression);
        }

        /// <summary>
        /// Footsteps at 20x are a machine-gun rattle, and a horse's neigh every few frames is
        /// worse. Both are silenced for the journey rather than played faster.
        /// </summary>
        void SuppressJourneyNoise()
        {
            if (noiseSuppressed)
                return;

            PlayerFootsteps footsteps = GetFootsteps();
            if (footsteps != null)
            {
                priorFootstepsEnabled = footsteps.enabled;
                footsteps.enabled = false;
            }

            TransportManager transport = GameManager.HasInstance
                ? GameManager.Instance.TransportManager : null;
            if (transport != null)
            {
                priorRidingVolume = transport.RidingVolumeScale;
                transport.RidingVolumeScale = 0f;
            }

            noiseSuppressed = true;
        }

        void RestoreJourneyNoise()
        {
            if (!noiseSuppressed)
                return;

            noiseSuppressed = false;

            PlayerFootsteps footsteps = GetFootsteps();
            if (footsteps != null)
                footsteps.enabled = priorFootstepsEnabled;

            TransportManager transport = GameManager.HasInstance
                ? GameManager.Instance.TransportManager : null;
            if (transport != null)
                transport.RidingVolumeScale = priorRidingVolume;
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
