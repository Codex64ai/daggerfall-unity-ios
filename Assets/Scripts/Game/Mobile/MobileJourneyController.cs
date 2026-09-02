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
using DaggerfallWorkshop.Game.Formulas;
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
        // Historical note: 21x once outran terrain streaming on an M4 iPad and produced
        // untextured ground; the streaming throttle (SustainableCompression) is the protection
        // that made the per-transport ceilings below viable. This default is the value a
        // journey has before any transport has been considered.
        public const int DefaultTimeCompression = 20;
        public const int MinTimeCompression = 1;

        // The ceiling follows HOW the player travels, not how carefully (device decision,
        // 2026-08-30). On foot 50x is about the fastest the world can still be seen going by;
        // a horse or cart covers ground quickly enough that 150x still reads as riding; a
        // ship gets 200x. Terrain streaming throttles below these when it must
        // (SustainableCompression), so a high cap is a permission, not a promise.
        public const int MaxFootCompression = 50;
        public const int MaxMountCompression = 150;
        public const int MaxShipCompression = 200;

        /// <summary>Pure: the speed ceiling for a way of travelling.</summary>
        public static int CapForTransport(TransportModes mode)
        {
            switch (mode)
            {
                case TransportModes.Horse:
                case TransportModes.Cart:
                    return MaxMountCompression;
                case TransportModes.Ship:
                    return MaxShipCompression;
                default:
                    return MaxFootCompression;
            }
        }

        static TransportModes CurrentTransport()
        {
            if (GameManager.HasInstance && GameManager.Instance.TransportManager != null)
                return GameManager.Instance.TransportManager.TransportMode;
            return TransportModes.Foot;
        }

        public static int MaxTimeCompression
        {
            get { return CapForTransport(CurrentTransport()); }
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
        bool nightHandled;              // this night's stop already decided
        bool travellingOnToInn;         // inn mode, dark, no town yet: stop at the next one
        bool resumeAfterRestQueued;     // the camp rest screen closed; pick the journey up
        DaggerfallRestWindow restWindow;
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

        // Cautious travel and encounters (tuned 2026-08-31, device feedback): total
        // suppression meant ZERO encounters and "a ton" was the state before it - Ikram wants
        // "a healthy medium of a little bit of encounters". The gate below leaves vanilla
        // spawns enabled for this percentage of in-game hours; the rest stay suppressed.
        // AttemptAvoid then still gives Running/Stealth a say about whatever does spawn.
        public int cautiousEncounterPercent = 25;

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

            // Static engine events, and this is a per-scene object: without these the handlers
            // pile up across return-to-menu cycles and fire on destroyed instances.
            SaveLoadManager.OnLoad -= OnSaveLoaded;
            StartGameBehaviour.OnNewGame -= ForgetDestination;
            GameManager.OnEncounter -= OnEncounter;

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
            SaveLoadManager.OnLoad += OnSaveLoaded;
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

            // Historically the route was reset here, after PlanRoute() had just filled it, so
            // journeys walked to the first road pixel and then went straight. Do not reset it.
            offeredPlaces.Clear();

            // The place we are setting out FROM must not be offered as somewhere to stop.
            // Resuming a journey after stopping in a town would otherwise ask, immediately and
            // absurdly, whether to stop at the town the player is standing in.
            PlayerGPS startGps = GameManager.Instance.PlayerGPS;
            if (startGps != null && startGps.HasCurrentLocation)
                offeredPlaces.Add(startGps.CurrentMapID);

            // THE NIGHT DECISION SURVIVES A RESUME. This used to reset nightHandled here, so a
            // player who closed the camp screen without sleeping (or slept an hour) resumed into
            // the same night, was asked to camp again on the very next frame, and again, and
            // again - measured on the Mac as three camps in under a second (journey probe run 6).
            // Reported from the device as the journey "just getting stuck". The flag now clears
            // only when night actually ends (CheckNightfall), never on resume.
            bool nightNow = DaggerfallUnity.Instance.WorldTime != null && DaggerfallUnity.Instance.WorldTime.Now.IsNight;
            nightHandled = NightFlagOnResume(nightNow, nightHandled);
            wasNight = nightNow;
            travellingOnToInn = false;

            diseaseCount = GameManager.Instance.PlayerEffectManager.DiseaseCount;
            SuppressJourneyNoise();
            SuppressWeather();

            // Set out at the speed the player last chose for this way of travelling (the
            // ceiling until they choose otherwise), so a Slower tap survives the next journey.
            TimeCompression = LoadPreferredCompression(CurrentTransport());
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

            // Roads are cautious travel's choice: the long way round, in company, on a known
            // path. Reckless travel is the straight line across country (device decision).
            if (!SpeedCautious)
            {
                Debug.Log("[Journey] route: reckless travel, heading straight for the destination");
                return;
            }

            // Road DATA is independent of road DRAWING: cautious journeys follow the network
            // even when Roads & tracks is off and the roads are invisible (the mod description
            // says so). Only missing data forces the straight line.
            if (!MobileRoadNetwork.Available)
            {
                Debug.Log("[Journey] route: road data unavailable - direct travel");
                return;
            }

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
                return;

            DFPosition here = gps.CurrentMapPixel;
            DFPosition target = MapsFile.GetPixelFromPixelID(destinationSummary.ID);

            DFPosition from = MobileRoadNetwork.NearestPathPixel(here.X, here.Y, snapRadius);
            DFPosition to = MobileRoadNetwork.NearestPathPixel(target.X, target.Y, snapRadius);

            if (from == null || to == null)
            {
                Debug.Log(string.Format("[Journey] route: no road within {0} pixels of {1} - direct travel",
                                        snapRadius, from == null ? "the start" : "the destination"));
                return;
            }

            List<DFPosition> found = MobileRoadNetwork.FindRoute(from.X, from.Y, to.X, to.Y);
            int detour = Distance(here, from) + Distance(to, target);
            int straight = Distance(here, target);

            if (found == null)
            {
                Debug.Log("[Journey] route: the network does not connect start and destination - direct travel");
                return;
            }

            if (!RouteWorthTaking(found.Count, detour, straight))
            {
                Debug.Log(string.Format("[Journey] route: rejected (road {0} px, off-road {1} px, straight {2} px) - direct travel",
                                        found.Count, detour, straight));
                return;
            }

            route = found;
            routeStep = 0;
            pilot.SetWaypoint(route[0]);

            Debug.Log(string.Format("[Journey] route: following the road, {0} px of road, {1} px off-road at the ends",
                                    found.Count, detour));
            DaggerfallUI.AddHUDText("You set out along the road.", 3f);
        }

        /// <summary>
        /// Pure. A road is worth taking when it exists and reaching it does not cost more
        /// walking than the whole trip would in a straight line. The old rule demanded the
        /// road stretch be longer than the off-road ends, which binned most medium trips.
        /// </summary>
        public static bool RouteWorthTaking(int routeLength, int detour, int straightLine)
        {
            return routeLength >= 2 && detour <= Mathf.Max(straightLine, 1);
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

            PlayerGPS gps = GameManager.Instance.PlayerGPS;

            // Pinned in the destination's own pixel - against its city wall, typically (device
            // report: Burgwall). That IS arrival: the player is at the gates.
            if (gps != null && gps.HasCurrentLocation && gps.CurrentMapID == destinationSummary.ID)
            {
                Debug.Log("[Journey] blocked at the destination's walls - counting it as arrival");
                Stop(JourneyEnd.Arrived);
                return;
            }

            // In a town the block is a building, and the journey is only passing through:
            // cross to the far side rather than leaving the player against a wall.
            if (gps != null && gps.HasCurrentLocation && gps.CurrentMapID != destinationSummary.ID &&
                IsSettlement(gps.CurrentLocationType) && PassThroughSettlement(gps.CurrentMapPixel))
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
                if (resumeAfterRestQueued)
                {
                    resumeAfterRestQueued = false;
                    ResumeAfterRest();
                    return;
                }

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
            ApplySpawnSuppression(SpeedCautious);
            LogPixelPaths();

            // Stand still while the town (or dungeon exterior) under the player is still being
            // built, at 1x, and do not judge arrival or offer a stop until it exists.
            bool holding = UpdateLocationHold();
            pilot.Update();

            // pilot.Update() may have arrived and stopped us mid-frame.
            if (pilot == null)
                return;

            if (CheckVitals())
                return;
            if (CheckDisease())
                return;
            if (!holding && CheckPassingPlace())
                return;
            if (CheckNightfall())
                return;
            CheckEnemies();
        }

        const float maxLocationHoldSeconds = 8f;
        float locationHoldStarted = -1f;
        bool holdCapWarned;

        /// <summary>See ShouldHoldForLocation. Returns true while holding.</summary>
        bool UpdateLocationHold()
        {
            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            StreamingWorld world = GameManager.Instance.StreamingWorld;
            bool hasLocation = gps != null && gps.HasCurrentLocation;
            bool built = world != null && world.CurrentPlayerLocationObject != null;

            if (hasLocation && !built)
            {
                if (locationHoldStarted < 0f)
                {
                    locationHoldStarted = Time.unscaledTime;
                    Debug.Log("[Journey] holding: '" + gps.CurrentLocation.Name + "' is not built yet");
                }
            }
            else if (locationHoldStarted >= 0f)
            {
                Debug.Log(string.Format("[Journey] hold released after {0:F1}s", Time.unscaledTime - locationHoldStarted));
                locationHoldStarted = -1f;
            }

            bool hold = ShouldHoldForLocation(hasLocation, built,
                locationHoldStarted < 0f ? 0f : Time.unscaledTime - locationHoldStarted, maxLocationHoldSeconds);
            if (hasLocation && !built && !hold && locationHoldStarted >= 0f && !holdCapWarned)
            {
                holdCapWarned = true;
                Debug.LogWarning("[Journey] hold cap reached; travelling on through an unbuilt location");
            }
            if (built || !hasLocation)
                holdCapWarned = false;

            pilot.SetHold(hold);
            if (hold && !Mathf.Approximately(Time.timeScale, 1f))
                SetTimeScale(1);
            return hold;
        }

        public enum VitalsAction { Continue, Stop, Camp }

        /// <summary>
        /// Pure: what a journey does about the player's health and fatigue this frame.
        ///
        /// FATIGUE IS GUARDED IN EVERY MODE. The engine's own exhaustion handler
        /// (PlayerEntity_OnExhausted) kills the player outright when fatigue reaches zero with
        /// enemies nearby or in water, and otherwise drops them for an hour. At 20-30x a
        /// journey reaches zero in seconds of real time, so reckless travel with no fatigue
        /// check walked a healthy player straight into that death (device report: "healthy,
        /// only stamina was low, I just died"). Nobody chose reckless to die of tiredness -
        /// its stated trade is encounters and a straight line, so LOW HEALTH stays its own
        /// risk, but low fatigue makes camp when resting is possible and stops when it is not.
        /// </summary>
        public static VitalsAction DecideVitals(int healthPercent, int fatiguePercent, bool cautious,
                                                bool enemiesNearby, bool swimming)
        {
            if (fatiguePercent <= defaultFatigueMinPercent)
                return (enemiesNearby || swimming) ? VitalsAction.Stop : VitalsAction.Camp;
            if (cautious && healthPercent <= defaultHealthMinPercent)
                return VitalsAction.Stop;
            return VitalsAction.Continue;
        }

        bool CheckVitals()
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (player == null)
                return false;

            int healthPct = player.MaxHealth > 0 ? player.CurrentHealth * 100 / player.MaxHealth : 100;
            int fatiguePct = player.MaxFatigue > 0 ? player.CurrentFatigue * 100 / player.MaxFatigue : 100;
            bool fatigueLow = fatiguePct <= defaultFatigueMinPercent;
            bool enemies = fatigueLow && GameManager.Instance.AreEnemiesNearby(resting: true);
            bool swimming = fatigueLow && GameManager.Instance.PlayerEnterExit != null &&
                            GameManager.Instance.PlayerEnterExit.IsPlayerSwimming;

            switch (DecideVitals(healthPct, fatiguePct, SpeedCautious, enemies, swimming))
            {
                case VitalsAction.Camp:
                    BeginCampNight("You are exhausted. You make camp to rest.");
                    return true;

                case VitalsAction.Stop:
                    Stop(JourneyEnd.Interrupted);
                    if (fatigueLow)
                        DaggerfallUI.MessageBox(swimming
                            ? "You are too exhausted to swim on. You stop before the water takes you."
                            : "You are too exhausted to continue, and cannot rest with enemies nearby.");
                    else
                        DaggerfallUI.MessageBox("You are too badly hurt to continue your journey.");
                    return true;

                default:
                    return false;
            }
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

            // Walking on to the next inn after dark: this is it.
            if (SleepModeInn && travellingOnToInn && !nightHandled &&
                DaggerfallUnity.Instance.WorldTime != null && DaggerfallUnity.Instance.WorldTime.Now.IsNight)
            {
                PlayerEntity player = GameManager.Instance.PlayerEntity;
                nightHandled = true;
                if (player != null && player.GoldPieces >= InnCost())
                    SpendNightAtInn(name);
                else
                    BeginCampNight("You cannot afford a room in " + name + ", so you make camp outside the walls.");
                return true;
            }

            AskToInterrupt("You are passing " + name + ". Stop here?",
                           "You continue past " + name + ".",
                           () => PassThroughSettlement(gps.CurrentMapPixel));
            return true;
        }

        static bool IsSettlement(DFRegion.LocationTypes type)
        {
            return type == DFRegion.LocationTypes.TownCity ||
                   type == DFRegion.LocationTypes.TownHamlet ||
                   type == DFRegion.LocationTypes.TownVillage ||
                   type == DFRegion.LocationTypes.Tavern;
        }

        public enum NightAction
        {
            None,           // daytime, or tonight already dealt with
            Camp,           // camp out where we stand
            Inn,            // take a room here
            CampNoGold,     // wanted an inn, cannot pay: camp instead
            TravelOn,       // inn mode, no town here: walk on to the next one
        }

        /// <summary>
        /// Pure: what nightfall means for this journey. "Camp out" camps; "inns" takes a room
        /// where there is one and walks on to the next town where there is not - and camps if
        /// the purse is empty. Once per night.
        /// </summary>
        /// <summary>Pure: nightHandled after a resume - kept while it is still the same night.</summary>
        public static bool NightFlagOnResume(bool isNightNow, bool wasHandled)
        {
            return isNightNow && wasHandled;
        }

        /// <summary>
        /// Pure: should the journey stand still because the location under the player has not
        /// been built yet? StreamingWorld builds a town some seconds after the player's pixel
        /// enters it - 7 s real at 8x on the Mac (probe runs 6-7), longer on an iPad - and in that
        /// window the pilot used to walk on through the empty footprint, count arrival, and
        /// offer "stop here?" for a town that was not there. Holding caps at maxHoldSeconds so a
        /// location that never builds cannot pin the journey forever.
        /// </summary>
        public static bool ShouldHoldForLocation(bool hasLocation, bool locationBuilt, float heldSeconds, float maxHoldSeconds)
        {
            return hasLocation && !locationBuilt && heldSeconds < maxHoldSeconds;
        }

        public static NightAction DecideNight(bool night, bool handledTonight, bool sleepModeInn,
                                              bool inSettlement, int gold, int innCost)
        {
            if (!night || handledTonight)
                return NightAction.None;
            if (!sleepModeInn)
                return NightAction.Camp;
            if (!inSettlement)
                return NightAction.TravelOn;
            return gold >= innCost ? NightAction.Inn : NightAction.CampNoGold;
        }

        /// <summary>Vanilla lodging: 5 gold a night, free with a knightly order's privileges.</summary>
        static int InnCost()
        {
            var order = GameManager.Instance.GuildManager.GetGuild(FactionFile.GuildGroups.KnightlyOrder);
            return (order != null && order.FreeTavernRooms()) ? 0 : 5;
        }

        /// <summary>
        /// Nightfall does what the travel popup's option says (device report: it used to only
        /// ask, then leave the player to rest by hand and re-open the map). See DecideNight.
        /// </summary>
        bool CheckNightfall()
        {
            if (promptOpen || DaggerfallUnity.Instance.WorldTime == null)
                return false;

            bool night = DaggerfallUnity.Instance.WorldTime.Now.IsNight;

            // Reset at dawn so tomorrow night is decided afresh.
            if (!night)
            {
                wasNight = false;
                nightHandled = false;
                travellingOnToInn = false;
                return false;
            }
            wasNight = true;

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            bool inSettlement = gps != null && gps.HasCurrentLocation && IsSettlement(gps.CurrentLocationType);
            PlayerEntity player = GameManager.Instance.PlayerEntity;

            NightAction action = DecideNight(night, nightHandled, SleepModeInn, inSettlement,
                                             player != null ? player.GoldPieces : 0, InnCost());
            switch (action)
            {
                case NightAction.Camp:
                    nightHandled = true;
                    BeginCampNight("Night is falling. You make camp.");
                    return true;

                case NightAction.CampNoGold:
                    nightHandled = true;
                    BeginCampNight("You cannot afford a room, so you make camp outside the walls.");
                    return true;

                case NightAction.Inn:
                    nightHandled = true;
                    SpendNightAtInn(gps.CurrentLocation.Name);
                    return false;       // the journey carries on at dawn, still running

                case NightAction.TravelOn:
                    if (!travellingOnToInn)
                    {
                        travellingOnToInn = true;
                        DaggerfallUI.AddHUDText("Night is falling. You travel on to the next inn.", 3f);
                    }
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Camp: the journey stops (destination kept), Daggerfall's own Rest screen comes up so
        /// the player chooses how long to sleep and the game applies its normal wilderness
        /// rules, and when that screen closes the journey resumes by itself.
        /// </summary>
        void BeginCampNight(string message)
        {
            Stop(JourneyEnd.Resting);
            DaggerfallUI.AddHUDText(message, 3f);

            if (!DaggerfallUI.HasInstance)
                return;

            restWindow = new DaggerfallRestWindow(DaggerfallUI.UIManager);
            restWindow.OnClose += OnRestWindowClosed;
            DaggerfallUI.UIManager.PushWindow(restWindow);
            Debug.Log("[Journey] night: camping; will resume when the rest screen closes");
        }

        void OnRestWindowClosed()
        {
            if (restWindow != null)
                restWindow.OnClose -= OnRestWindowClosed;
            restWindow = null;
            // Resume on the next Update, once the UI stack has settled: resuming from inside
            // the close would push the travel bar under a window still on its way out.
            resumeAfterRestQueued = true;
        }

        /// <summary>
        /// Inn: pay for the room, sleep until dawn with the same hourly recovery the Rest screen
        /// applies (the Rest screen itself refuses to run inside a town), and carry straight on.
        /// The journey never stops, so there is nothing for the player to re-open.
        /// </summary>
        void SpendNightAtInn(string townName)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int cost = InnCost();
            if (player != null && cost > 0)
                player.GoldPieces = Mathf.Max(0, player.GoldPieces - cost);

            DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now;
            int hoursToDawn = HoursUntilDawn(now.Hour);
            for (int h = 0; h < hoursToDawn; h++)
            {
                now.RaiseTime(DaggerfallDateTime.SecondsPerHour);
                if (player != null)
                {
                    player.CurrentHealth += FormulaHelper.CalculateHealthRecoveryRate(player);
                    player.CurrentFatigue += FormulaHelper.CalculateFatigueRecoveryRate(player.MaxFatigue);
                    player.CurrentMagicka += FormulaHelper.CalculateSpellPointRecoveryRate(player);
                }
                Questing.QuestMachine.Instance.Tick();
            }

            travellingOnToInn = false;
            DaggerfallUI.AddHUDText(cost > 0
                ? string.Format("You take a room at the inn in {0} ({1} gold) and set out again at dawn.", townName, cost)
                : string.Format("You spend the night at the inn in {0} and set out again at dawn.", townName), 4f);
            Debug.Log("[Journey] night: inn at " + townName + ", slept " + hoursToDawn + "h");
        }

        /// <summary>Pure: whole hours from this hour to the next dawn (DaggerfallDateTime.DawnHour).</summary>
        public static int HoursUntilDawn(int hour)
        {
            int dawn = DaggerfallDateTime.DawnHour;
            return hour < dawn ? dawn - hour : 24 - hour + dawn;
        }

        /// <summary>After a camp rest: pick the journey back up, unless something is waiting outside the tent.</summary>
        void ResumeAfterRest()
        {
            if (!destinationValid || IsTravelling)
                return;

            if (GameManager.HasInstance && GameManager.Instance.AreEnemiesNearby())
            {
                DaggerfallUI.AddHUDText("Something is nearby. Your journey waits.", 3f);
                return;
            }

            if (Resume())
                DaggerfallUI.AddHUDText("You break camp and travel on.", 3f);
        }

        /// <summary>
        /// Pause the journey and ask. Yes stops travel but KEEPS the destination, so the
        /// travel map will offer to resume; No carries on at the same speed.
        /// </summary>
        void AskToInterrupt(string question, string declineText, Action onDecline = null)
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
                {
                    Stop(JourneyEnd.Interrupted);
                }
                else
                {
                    if (!string.IsNullOrEmpty(declineText))
                        DaggerfallUI.AddHUDText(declineText, 2f);
                    if (onDecline != null)
                        onDecline();
                }
            };

            // Dismissing without choosing carries on, and must clear the flag or no further
            // prompt is ever offered.
            box.OnCancel += (sender) => { promptOpen = false; };
            box.Show();
        }

        /// <summary>
        /// Cross a settlement the journey is only passing through: put the player at the far
        /// edge of its footprint along the current bearing. Towns are building blocks; steering
        /// a straight line through one is how the player ended up pressed against walls
        /// (device report). The prompt to stop there has already been offered.
        /// </summary>
        bool PassThroughSettlement(DFPosition pixel)
        {
            if (pilot == null)
                return false;

            ContentReader.MapSummary summary;
            if (!DaggerfallUnity.Instance.ContentReader.HasLocation(pixel.X, pixel.Y, out summary))
                return false;

            Rect footprint;
            try { footprint = MobileJourneyPilot.GetLocationRect(summary); }
            catch (ArgumentException) { return false; }

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            Vector2 exit = MobileJourneyPilot.ExitPointThroughRect(footprint,
                new Vector2(gps.WorldX, gps.WorldZ), pilot.JourneyYaw, passThroughMarginWorldUnits);

            if (!pilot.TeleportTo(exit.x, exit.y))
                return false;

            Debug.Log("[Journey] passed through " + gps.CurrentLocation.Name);
            return true;
        }

        // How far beyond a settlement's footprint to land when crossing it. Roughly two tiles.
        const float passThroughMarginWorldUnits = 600f;

        /// <summary>
        /// Cautious travel is Daggerfall's safe travel: no random encounters. PlayerEntity clears
        /// its own flag every game minute, so this is re-asserted each frame while walking and
        /// dropped in Stop(). Reckless keeps the spawns - that is its trade for the straight line.
        /// </summary>
        void ApplySpawnSuppression(bool travellingCautiously)
        {
            bool suppress = travellingCautiously;
            if (suppress && DaggerfallUnity.HasInstance)
            {
                uint hour = DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime() / 60u;
                if (CautiousEncounterGateOpen(hour, cautiousEncounterPercent))
                    suppress = false;
            }

            PlayerEntity player = GameManager.HasInstance ? GameManager.Instance.PlayerEntity : null;
            if (player != null && player.PreventEnemySpawns != suppress)
                player.PreventEnemySpawns = suppress;
        }

        /// <summary>
        /// Pure: is this in-game hour one where cautious travel lets vanilla spawns roll?
        /// Deterministic per hour (Knuth multiplicative hash), so the gate holds for the whole
        /// hour instead of flickering per frame, saves/loads agree, and the long-run open rate
        /// is the given percentage.
        /// </summary>
        public static bool CautiousEncounterGateOpen(uint classicHour, int percent)
        {
            if (percent <= 0)
                return false;
            if (percent >= 100)
                return true;
            return (classicHour * 2654435761u) % 100u < (uint)percent;
        }

        DFPosition lastLoggedPixel;

        /// <summary>One console line per map pixel entered: what the road data says is here.</summary>
        void LogPixelPaths()
        {
            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
                return;
            DFPosition px = gps.CurrentMapPixel;
            if (lastLoggedPixel != null && lastLoggedPixel.X == px.X && lastLoggedPixel.Y == px.Y)
                return;
            lastLoggedPixel = new DFPosition(px.X, px.Y);
            Debug.Log(string.Format("[Journey] pixel {0},{1}: road=0x{2:X2} track=0x{3:X2} {4}",
                px.X, px.Y, MobileRoadNetwork.RoadsAt(px.X, px.Y), MobileRoadNetwork.TracksAt(px.X, px.Y),
                FollowingRoad ? "following the route" : "direct"));
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
            Resting,        // camping for the night; destination kept, resumes by itself
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
            ApplySpawnSuppression(false);

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

        void OnSaveLoaded(SaveData_v1 saveData)
        {
            ForgetDestination();
        }

        void ForgetDestination()
        {
            destinationValid = false;
            destinationName = null;
            resumeAfterRestQueued = false;
            if (restWindow != null)
                restWindow.OnClose -= OnRestWindowClosed;
            restWindow = null;
        }

        #endregion

        #region World state

        /// <summary>
        /// Pure, static, and therefore testable headlessly. Below 1x time would run backwards;
        /// above the transport ceiling the player outruns terrain streaming and walks into unloaded world.
        /// </summary>
        public static int ClampCompression(int scale)
        {
            return Mathf.Clamp(scale, MinTimeCompression, MaxTimeCompression);
        }

        const string speedPrefPrefix = "DFMobile.journeyspeed.";

        /// <summary>The player's last chosen speed for this transport, clamped to its ceiling; the ceiling if none.</summary>
        public static int LoadPreferredCompression(TransportModes mode)
        {
            int cap = CapForTransport(mode);
            int saved = PlayerPrefs.GetInt(speedPrefPrefix + mode, cap);
            return Mathf.Clamp(saved, MinTimeCompression, cap);
        }

        static void SavePreferredCompression(TransportModes mode, int scale)
        {
            PlayerPrefs.SetInt(speedPrefPrefix + mode, scale);
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
        /// Clamped: below 1x time would run backwards, and above the transport ceiling the player outruns
        /// terrain streaming and walks into unloaded world.
        /// </summary>
        public void SetTimeCompression(int scale)
        {
            TimeCompression = ClampCompression(scale);
            SavePreferredCompression(CurrentTransport(), TimeCompression);

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
