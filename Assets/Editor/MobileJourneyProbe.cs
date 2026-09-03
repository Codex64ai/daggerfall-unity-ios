// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Diagnostic: drive REAL journeys on the Mac and record what the pilot does, so travel bugs
// reported from the device ("gets stuck behind walls", "town never appeared", "just stalls",
// "just died") can be measured instead of guessed at.
//
//   Unity -batchmode -projectPath <proj> -executeMethod
//     DaggerfallWorkshop.Game.Mobile.EditorTools.MobileJourneyProbe.Run -logFile <log>
//   env: DFU_JOURNEY_SECONDS=300   real seconds to keep travelling (default 300)
//        DFU_JOURNEY_CAUTIOUS=1    cautious travel (default reckless - the mode with fewer guards)
//        DFU_JOURNEY_COMPRESSION=N time compression (default: the transport's maximum)
//        DFU_JOURNEY_MINPIXELS=N   nearest settlement must be at least N pixels away (default 3)
//   (NO -quit: the probe exits the editor itself.)
//
// Starts a new character OUTSIDE at the configured start cell, walks to the nearest unvisited
// settlement, and on arrival picks the next one, until the time budget runs out. Every 2 s it
// logs one "[JourneyProbe]" line; it flags STALL (no movement for 10 s while travelling),
// TOWN-NOT-BUILT (GPS says we are in a settlement but StreamingWorld has no location object),
// DEATH, and every "[Journey]" line the controller writes. Prompts are answered like a player
// who wants to keep going: message boxes are dismissed (which the controller treats as "carry
// on"), rest screens are closed after restoring vitals (we are testing steering, not fatigue).
//
// Everything here is editor-only diagnostics; nothing touches the scene on disk.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    [InitializeOnLoad]
    public static class MobileJourneyProbe
    {
        const string Armed = "DFMobile.JourneyProbe.Armed";
        const string T0 = "DFMobile.JourneyProbe.T0";
        const string Started = "DFMobile.JourneyProbe.Started";
        const float TickSeconds = 2f;
        const float StallSeconds = 10f;
        const float StallEpsilonWorldUnits = 40f;

        static float lastTick, lastMoveTime, startedAt;
        static int lastWorldX, lastWorldZ;
        static bool haveLastPos, journeyStarted, reportedStall;
        static int legs, arrivals, stalls, blockedEvents, townsNotBuilt, promptsDismissed, restsHandled, deaths;
        static readonly HashSet<int> visited = new HashSet<int>();
        static readonly List<string> journeyLog = new List<string>();
        static int currentDestMapId = -1;
        static string currentDestName = "";
        static float legStartedAt;
        static int instantFailures, enemiesCleared;
        static float watchUntil = -1f, lastWatch;
        static float lastRestClosedAt = -100f;
        static string townGapName; static float townGapStart; static int townGapCount;

        static MobileJourneyProbe()
        {
            if (SessionState.GetBool(Armed, false) && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
        }

        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(MobileBuildSetup.GameScenePath, OpenSceneMode.Single);
            var start = UnityEngine.Object.FindFirstObjectByType<StartGameBehaviour>();
            if (start == null) { Debug.LogError("[JourneyProbe] no StartGameBehaviour"); EditorApplication.Exit(2); return; }
            if (UnityEngine.Object.FindFirstObjectByType<ModManager>() == null)
                new GameObject("ModManager (probe)").AddComponent<ModManager>();

            // Settings are static and reload with the domain when play mode starts, so the
            // outdoor start is applied from Tick (after the reload) and only then is the start
            // method flipped to NewCharacter. Setting it here was silently undone (runs 1-4).
            start.StartMethod = StartGameBehaviour.StartMethods.DoNothing;
            Debug.Log("[JourneyProbe] new character outdoors; entering play mode");
            SessionState.SetBool(Armed, true);
            SessionState.SetBool(Started, false);
            SessionState.SetFloat(T0, -1f);
            EditorApplication.isPlaying = true;
        }

        static int Env(string name, int fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            int n;
            return (!string.IsNullOrEmpty(v) && int.TryParse(v, out n)) ? n : fallback;
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("[Journey]"))
            {
                journeyLog.Add(condition);
                if (condition.Contains("blocked")) blockedEvents++;
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (!GameManager.HasInstance || GameManager.Instance.PlayerGPS == null) return;
            var dfu = DaggerfallUnity.Instance;
            if (dfu == null || !dfu.IsReady) return;

            if (SessionState.GetFloat(T0, -1f) < 0f)
            {
                var sgb = UnityEngine.Object.FindFirstObjectByType<StartGameBehaviour>();
                if (sgb != null && !SessionState.GetBool(Started, false))
                {
                    SessionState.SetBool(Started, true);     // the engine resets StartMethod after use; fire once
                    DaggerfallUnity.Settings.StartInDungeon = false;     // outdoors, at the start cell
                    sgb.StartMethod = StartGameBehaviour.StartMethods.NewCharacter;
                    Debug.Log("[JourneyProbe] StartInDungeon=false applied after reload; starting new character outdoors");
                    return;
                }
                // Wait until the new character is actually in the world.
                if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.CurrentHealth <= 0) return;
                if (GameManager.Instance.PlayerEnterExit == null || GameManager.Instance.PlayerEnterExit.IsPlayerInside) return;
                if (!GameManager.Instance.StreamingWorld.IsReady) return;
                if (GameManager.Instance.StreamingWorld == null || !GameManager.Instance.StreamingWorld.IsInit) return;
                SessionState.SetFloat(T0, Time.realtimeSinceStartup);
                startedAt = Time.realtimeSinceStartup;
                lastMoveTime = startedAt;
                lastTick = startedAt;
                Debug.Log("[JourneyProbe] world ready at " + Time.realtimeSinceStartup.ToString("F0") + "s");
                if (Environment.GetEnvironmentVariable("DFU_JOURNEY_POISON") == "1")
                {
                    // Reproduce the device report: poisoned and already hurt, then travel.
                    var pe = GameManager.Instance.PlayerEntity;
                    var fx = GameManager.Instance.PlayerEffectManager;
                    var bundle = fx.CreatePoison(DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects.Poisons.Nux_Vomica);
                    fx.AssignBundle(bundle, DaggerfallWorkshop.Game.MagicAndEffects.AssignBundleFlags.BypassSavingThrows);
                    pe.CurrentHealth = Mathf.Max(3, pe.MaxHealth / 3);
                    Debug.Log(string.Format("[JourneyProbe] POISONED with Nux Vomica, hp set to {0}/{1}; poisons={2}", pe.CurrentHealth, pe.MaxHealth, fx.PoisonCount));
                }
                return;
            }

            float now = Time.realtimeSinceStartup;
            var ctl = MobileJourneyController.Instance;
            var gps = GameManager.Instance.PlayerGPS;
            var player = GameManager.Instance.PlayerEntity;

            // Death is the headline result.
            if (player != null && player.CurrentHealth <= 0)
            {
                deaths++;
                Debug.LogError(string.Format("[JourneyProbe] DEATH at pixel {0},{1} fatigue={2}/{3} travelling={4} dest='{5}'",
                    gps.CurrentMapPixel.X, gps.CurrentMapPixel.Y, player.CurrentFatigue, player.MaxFatigue,
                    ctl != null && ctl.IsTravelling, currentDestName));
                Finish();
                return;
            }

            AnswerPrompts();

            if (ctl == null) return;

            if (!ctl.IsTravelling)
            {
                // Arrived or interrupted? Arrival = standing in the destination.
                if (journeyStarted && now - lastRestClosedAt < 4f)
                    return;                                    // camping mid-leg, not the end of it
                if (journeyStarted && DaggerfallUI.UIManager.WindowCount > 0 && DaggerfallUI.UIManager.TopWindow is DaggerfallRestWindow)
                    return;
                if (journeyStarted)
                {
                    bool atDest = gps.HasCurrentLocation && gps.CurrentLocation.MapTableData.MapId == currentDestMapId;
                    if (atDest && GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject == null)
                        Debug.LogWarning("[JourneyProbe] ARRIVED-BEFORE-BUILT: standing in '" + currentDestName + "' but its geometry does not exist yet");
                    Debug.Log(string.Format("[JourneyProbe] leg ended after {0:F0}s: {1} (dest '{2}', now at {3})",
                        now - legStartedAt, atDest ? "ARRIVED" : "INTERRUPTED", currentDestName,
                        gps.HasCurrentLocation ? gps.CurrentLocation.Name : "wilderness"));
                    if (atDest) arrivals++;
                    if (!atDest && now - legStartedAt < 5f && enemiesCleared == 0) instantFailures++; else instantFailures = 0;
                    if (instantFailures >= 3)
                    {
                        Debug.LogError("[JourneyProbe] three legs failed within seconds of starting - the pilot cannot move the player here. State: " + Status(ctl, gps) + " pilot=" + PilotState(ctl));
                        Finish();
                        return;
                    }
                    journeyStarted = false;
                }
                if (now - startedAt > Env("DFU_JOURNEY_SECONDS", 300)) { Finish(); return; }
                if (DaggerfallUI.UIManager.WindowCount > 0 && !(DaggerfallUI.UIManager.TopWindow is DaggerfallHUD)) return;
                if (now - lastRestClosedAt < 4f) return;      // the controller resumes by itself after a rest
                StartNextLeg(ctl, gps);
                return;
            }

            // Travelling: movement / stall bookkeeping and the periodic status line.
            if (!haveLastPos || Mathf.Abs(gps.WorldX - lastWorldX) > StallEpsilonWorldUnits || Mathf.Abs(gps.WorldZ - lastWorldZ) > StallEpsilonWorldUnits)
            {
                lastWorldX = gps.WorldX; lastWorldZ = gps.WorldZ; haveLastPos = true;
                lastMoveTime = now; reportedStall = false;
            }
            else if (MobileJourneyPilot.Holding)
                lastMoveTime = now;                       // a deliberate hold is not a stall
            else if (!reportedStall && now - lastMoveTime > StallSeconds && !GameManager.IsGamePaused && DaggerfallUI.UIManager.WindowCount <= 1)
            {
                stalls++; reportedStall = true;
                Debug.LogWarning("[JourneyProbe] STALL " + Status(ctl, gps) + " pilot=" + PilotState(ctl));
            }

            bool inUnbuiltTown = gps.HasCurrentLocation && IsSettlement(gps.CurrentLocationType) &&
                                 GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject == null;
            if (inUnbuiltTown && townGapName == null)
            {
                townGapName = gps.CurrentLocation.Name; townGapStart = now; townsNotBuilt++;
                Debug.LogWarning("[JourneyProbe] TOWN-NOT-BUILT begins '" + townGapName + "' " + Status(ctl, gps));
            }
            if (inUnbuiltTown && now - lastTick >= TickSeconds)
            {
                // Is the town REALLY absent, or is CurrentPlayerLocationObject merely stale? Look for it.
                int found = 0, blocks = 0;
                foreach (var loc in UnityEngine.Object.FindObjectsByType<DaggerfallLocation>(FindObjectsSortMode.None))
                    if (loc.Summary.MapID == gps.CurrentMapID) { found++; blocks = loc.transform.childCount; }
                Debug.Log(string.Format("[JourneyProbe] town-object-check '{0}': objects={1} childBlocks={2} (property says not built)", townGapName, found, blocks));
            }
            else if (!inUnbuiltTown && townGapName != null)
            {
                Debug.LogWarning(string.Format("[JourneyProbe] TOWN-NOT-BUILT ends '{0}' after {1:F1}s real ({2})", townGapName, now - townGapStart,
                    gps.HasCurrentLocation && gps.CurrentLocation.Name == townGapName ? "geometry appeared around the player" : "player left the pixel first"));
                townGapName = null;
            }

            if (now < watchUntil && now - lastWatch >= 0.25f)
            {
                lastWatch = now;
                var p = GameManager.Instance.PlayerEntity;
                Debug.Log(string.Format("[JourneyProbe] WATCH hp={0}/{1} fat={2}% y={3:F1} grounded={4} falling={5} timeScale={6} holding={7} terrainHere={8}",
                    p.CurrentHealth, p.MaxHealth, p.MaxFatigue > 0 ? p.CurrentFatigue * 100 / p.MaxFatigue : -1,
                    GameManager.Instance.PlayerObject.transform.position.y, GameManager.Instance.PlayerMotor.IsGrounded,
                    GameManager.Instance.AcrobatMotor.Falling, Time.timeScale, MobileJourneyPilot.Holding,
                    GameManager.Instance.StreamingWorld.GetTerrainFromPixel(gps.CurrentMapPixel.X, gps.CurrentMapPixel.Y) != null));
            }
            if (now - lastTick >= TickSeconds)
            {
                lastTick = now;
                Debug.Log("[JourneyProbe] " + Status(ctl, gps));
            }

            if (now - startedAt > Env("DFU_JOURNEY_SECONDS", 300)) { Finish(); }
        }

        static string Status(MobileJourneyController ctl, PlayerGPS gps)
        {
            var player = GameManager.Instance.PlayerEntity;
            var now = DaggerfallUnity.Instance.WorldTime.Now;
            string top = DaggerfallUI.UIManager.WindowCount > 0 && DaggerfallUI.UIManager.TopWindow != null
                ? DaggerfallUI.UIManager.TopWindow.GetType().Name : "(none)";
            string gates = string.Format(" paused={0} inputPaused={1} timeScale={2:F1} focused={3} pilotActive={4} windows={5} top={6} standingStill={7}",
                GameManager.IsGamePaused, InputManager.Instance != null && InputManager.Instance.IsPaused, Time.timeScale,
                Application.isFocused, MobileJourneyPilot.Active, DaggerfallUI.UIManager.WindowCount, top,
                GameManager.Instance.PlayerMotor != null && GameManager.Instance.PlayerMotor.IsStandingStill);
            Vector3 tp = GameManager.Instance.PlayerObject != null ? GameManager.Instance.PlayerObject.transform.position : Vector3.zero;
            var sw = GameManager.Instance.StreamingWorld;
            var gpsNow = GameManager.Instance.PlayerGPS;
            bool terrainHere = sw != null && sw.GetTerrainFromPixel(gpsNow.CurrentMapPixel.X, gpsNow.CurrentMapPixel.Y) != null;
            gates += string.Format(" pos=({0:F1},{1:F1},{2:F1}) worldReady={3} terrainHere={4} grounded={5}", tp.x, tp.y, tp.z,
                sw != null && sw.IsReady, terrainHere,
                GameManager.Instance.PlayerMotor != null && GameManager.Instance.PlayerMotor.IsGrounded);
            return gates + string.Format(" t={0:F0}s game={1:00}:{2:00} px={3},{4} world={5},{6} dest='{7}' speed={8:F1} x{9} road={10} terrainBuilding={11} loc='{12}' locObj={13} hp={14}/{15} fat={16}%",
                Time.realtimeSinceStartup - startedAt, now.Hour, now.Minute,
                gps.CurrentMapPixel.X, gps.CurrentMapPixel.Y, gps.WorldX, gps.WorldZ, currentDestName,
                ctl.MeasuredSpeed, ctl.ActiveCompression, ctl.FollowingRoad, ctl.TerrainBuilding,
                gps.HasCurrentLocation ? gps.CurrentLocation.Name : "-",
                GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject != null,
                player.CurrentHealth, player.MaxHealth,
                player.MaxFatigue > 0 ? player.CurrentFatigue * 100 / player.MaxFatigue : -1);
        }

        static string PilotState(MobileJourneyController ctl)
        {
            try
            {
                object pilot = typeof(MobileJourneyController).GetField("pilot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ctl);
                if (pilot == null) return "(none)";
                Func<string, object> f = n => pilot.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pilot);
                var p = (MobileJourneyPilot)pilot;
                return string.Format("dist={0:F0} yaw={1:F0} blockedFor={2:F2} sidestep={3} nudged={4} best={5:F0} steerOffset={6:F0} final={7}",
                    p.DistanceToTarget, p.JourneyYaw, f("blockedFor"), f("sidestepAttempt"), f("nudged"), f("bestDistanceToTarget"), f("steerOffset"), p.AtFinalTarget);
            }
            catch (Exception e) { return "(reflection failed: " + e.Message + ")"; }
        }

        static string MessageBoxText(DaggerfallMessageBox box)
        {
            try
            {
                var label = (MultiFormatTextLabel)typeof(DaggerfallMessageBox).GetField("label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(box);
                var parts = new List<string>();
                foreach (var tl in label.TextLabels) parts.Add(tl.Text);
                return string.Join(" ", parts.ToArray());
            }
            catch (Exception e) { return "(text unavailable: " + e.Message + ")"; }
        }

        static bool IsSettlement(DFRegion.LocationTypes t)
        {
            return t == DFRegion.LocationTypes.TownCity || t == DFRegion.LocationTypes.TownHamlet || t == DFRegion.LocationTypes.TownVillage;
        }

        /// <summary>Dismiss what a player would click through, so the journey never sits waiting.</summary>
        static void AnswerPrompts()
        {
            var ui = DaggerfallUI.UIManager;
            if (ui.WindowCount == 0) return;
            var top = ui.TopWindow;
            if (top is DaggerfallRestWindow)
            {
                // Rest like a player would: sleep until 6:00, restore vitals, then close. Closing with
                // no time passed exposed a resume->camp loop (finding #2) but is not what players do.
                var player = GameManager.Instance.PlayerEntity;
                var now = DaggerfallUnity.Instance.WorldTime.Now;
                // DFU_JOURNEY_REST=cancel: close the rest screen with no time passing, the way a
                // player who dismisses it does - the reproduction for the resume->camp loop.
                int hours = Environment.GetEnvironmentVariable("DFU_JOURNEY_REST") == "cancel" ? 0 : MobileJourneyController.HoursUntilDawn(now.Hour);
                if (hours > 0) now.RaiseTime(hours * DaggerfallDateTime.SecondsPerHour);
                player.CurrentFatigue = player.MaxFatigue;
                player.CurrentHealth = player.MaxHealth;
                restsHandled++;
                lastRestClosedAt = Time.realtimeSinceStartup;
                Debug.Log("[JourneyProbe] rest screen -> slept " + hours + "h to dawn, vitals restored, closing");
                ((DaggerfallRestWindow)top).CloseWindow();
            }
            else if (top is DaggerfallMessageBox)
            {
                promptsDismissed++;
                string text = MessageBoxText((DaggerfallMessageBox)top);
                if (text.StartsWith("You are passing") && Environment.GetEnvironmentVariable("DFU_JOURNEY_PASS") == "no")
                {
                    // Answer NO like the player did: this takes the pass-through TELEPORT path, which a
                    // dismissal does not. Then watch health and height closely for a few seconds.
                    var p = GameManager.Instance.PlayerEntity;
                    Debug.Log(string.Format("[JourneyProbe] answering NO to \"{0}\" hp={1} y={2:F1}", text, p.CurrentHealth,
                        GameManager.Instance.PlayerObject.transform.position.y));
                    var raise = typeof(DaggerfallMessageBox).GetMethod("RaiseOnButtonClickEvent", BindingFlags.Instance | BindingFlags.NonPublic);
                    raise.Invoke(top, new object[] { top, DaggerfallMessageBox.MessageBoxButtons.No });
                    watchUntil = Time.realtimeSinceStartup + 8f;
                    return;
                }
                Debug.Log("[JourneyProbe] message box dismissed: \"" + text + "\"");
                if (text.StartsWith("An enemy is seeking"))
                {
                    // Reckless travel keeps spawns, and the same enemy interrupts every new leg on its
                    // first frame until dealt with. The probe tests travel, not combat: remove them.
                    int cleared = 0;
                    foreach (var eb in UnityEngine.Object.FindObjectsByType<DaggerfallEntityBehaviour>(FindObjectsSortMode.None))
                        if (eb.EntityType == EntityTypes.EnemyMonster || eb.EntityType == EntityTypes.EnemyClass)
                        { UnityEngine.Object.Destroy(eb.gameObject); cleared++; }
                    // AreEnemiesNearby also counts a PENDING FoeSpawner within 1024 units; one that never
                    // finds a place to put its foe (headless: no camera to hide behind) blocks every leg.
                    int spawners = 0;
                    foreach (var sp in UnityEngine.Object.FindObjectsByType<FoeSpawner>(FindObjectsSortMode.None))
                    { UnityEngine.Object.Destroy(sp.gameObject); spawners++; }
                    enemiesCleared += cleared + spawners;
                    Debug.Log("[JourneyProbe] cleared " + cleared + " enemies and " + spawners + " pending spawners so travel can continue");
                }
                ((DaggerfallMessageBox)top).CloseWindow();
            }
        }

        static bool movedToWilderness;

        static void StartNextLeg(MobileJourneyController ctl, PlayerGPS gps)
        {
            MobileJourneyController.JourneyModeEnabled = true;

            // A new character spawns at Privateer's Hold's door, facing the mound. Begin the first
            // leg from open ground one pixel east instead, so a wall at the start does not masquerade
            // as a travel bug. Later legs start wherever the previous one ended, like a player's would.
            if (!movedToWilderness && Env("DFU_JOURNEY_FROM_START", 0) == 0)
            {
                movedToWilderness = true;
                DFPosition px = gps.CurrentMapPixel;
                DFPosition target = new DFPosition(px.X + 1, px.Y);
                DaggerfallWorkshop.Utility.ContentReader.MapSummary ignored;
                if (DaggerfallUnity.Instance.ContentReader.HasLocation(target.X, target.Y, out ignored))
                    target = new DFPosition(px.X, px.Y + 1);
                DFPosition world = MapsFile.MapPixelToWorldCoord(target.X, target.Y);
                GameManager.Instance.StreamingWorld.TeleportToWorldCoordinates(world.X + 16384, world.Y + 16384);
                Debug.Log("[JourneyProbe] moved to open ground at pixel " + target.X + "," + target.Y + " before the first leg");
                return;     // let the world settle; next tick starts the leg
            }

            DFPosition here = gps.CurrentMapPixel;
            int minPixels = Env("DFU_JOURNEY_MINPIXELS", 3);
            if (gps.HasCurrentLocation) visited.Add(gps.CurrentLocation.MapTableData.MapId);

            var maps = DaggerfallUnity.Instance.ContentReader.MapFileReader;
            int bestD = int.MaxValue; DFPosition bestPx = null; string bestName = null; int bestId = -1;
            for (int r = 0; r < maps.RegionCount; r++)
            {
                DFRegion region = maps.GetRegion(r);
                if (region.MapTable == null) continue;
                for (int i = 0; i < region.MapTable.Length; i++)
                {
                    if (!IsSettlement(region.MapTable[i].LocationType) || visited.Contains(region.MapTable[i].MapId)) continue;
                    var px = MapsFile.LongitudeLatitudeToMapPixel(region.MapTable[i].Longitude, region.MapTable[i].Latitude);
                    int d = Mathf.Abs(px.X - here.X) + Mathf.Abs(px.Y - here.Y);
                    if (d < minPixels || d >= bestD) continue;
                    bestD = d; bestPx = px; bestName = region.MapNames[i]; bestId = region.MapTable[i].MapId;
                }
            }
            if (bestPx == null) { Debug.LogError("[JourneyProbe] no settlement to go to"); Finish(); return; }

            var store = typeof(MobileJourneyController).GetMethod("StoreDestination", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(bool)store.Invoke(ctl, new object[] { bestPx })) { Debug.LogError("[JourneyProbe] StoreDestination refused " + bestName); Finish(); return; }

            bool cautious = Env("DFU_JOURNEY_CAUTIOUS", 0) == 1;
            typeof(MobileJourneyController).GetProperty("SpeedCautious").GetSetMethod(true).Invoke(ctl, new object[] { cautious });
            int comp = Env("DFU_JOURNEY_COMPRESSION", MobileJourneyController.MaxTimeCompression);
            ctl.SetTimeCompression(comp);

            if (!ctl.Resume()) { Debug.LogError("[JourneyProbe] Resume() refused"); Finish(); return; }
            visited.Add(bestId);
            currentDestMapId = bestId; currentDestName = bestName;
            legs++; journeyStarted = true; legStartedAt = Time.realtimeSinceStartup; haveLastPos = false; reportedStall = false;
            lastTick = 0f;   // force a status line on the very next tick
            Debug.Log(string.Format("[JourneyProbe] LEG {0}: {1},{2} -> '{3}' at {4},{5} ({6} px) {7} x{8}",
                legs, here.X, here.Y, bestName, bestPx.X, bestPx.Y, bestD, cautious ? "cautious" : "reckless", comp));
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Armed, false);
            Debug.Log(string.Format("[JourneyProbe] SUMMARY legs={0} arrivals={1} stalls={2} blockedEvents={3} townsNotBuilt={4} promptsDismissed={5} rests={6} deaths={7} enemiesCleared={9} journeyLogLines={8}",
                legs, arrivals, stalls, blockedEvents, townsNotBuilt, promptsDismissed, restsHandled, deaths, journeyLog.Count, enemiesCleared));
            foreach (string line in journeyLog)
                Debug.Log("[JourneyProbe]   " + line);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
