// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Diagnostic: start a NEW character in play mode (StartInDungeon = Privateer's Hold), wait,
// report whether the dungeon actually built, then exit. Every mod under Assets/Game/Mods loads
// as a virtual mod in the editor, so this reproduces "a bundled mod breaks dungeons" on the
// Mac without a device. Exceptions land in the -logFile; grep it.
//
//   Unity -batchmode -projectPath <proj> -executeMethod
//     DaggerfallWorkshop.Game.Mobile.EditorTools.MobileDungeonProbe.Run -logFile <log>
//   (NO -quit: the probe exits the editor itself once done.)
//
// Play mode reloads the domain, so the second half runs from InitializeOnLoad guarded by a
// SessionState flag. Nothing here touches the scene on disk.

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    [InitializeOnLoad]
    public static class MobileDungeonProbe
    {
        const string Flag = "DFMobile.DungeonProbe.Armed";
        const string StartedAt = "DFMobile.DungeonProbe.StartedAt";
        const float WaitSeconds = 60f;
        const string PickIndex = "DFMobile.DungeonProbe.PickIndex";

        static MobileDungeonProbe()
        {
            if (SessionState.GetBool(Flag, false) && EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.update += Tick;
        }

        public static void Run()
        {
            // Mods moved in or out of Assets/Game/Mods between runs must be seen by this run.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(MobileBuildSetup.GameScenePath, OpenSceneMode.Single);
            var start = UnityEngine.Object.FindFirstObjectByType<StartGameBehaviour>();
            if (start == null)
            {
                Debug.LogError("[DungeonProbe] no StartGameBehaviour in scene");
                EditorApplication.Exit(2);
                return;
            }
            // DFU_PROBE_DUNGEON=N: start in the Nth dungeon-type location found (any region)
            // instead of Privateer's Hold. Chosen once the content reader is up (see Tick).
            string pick = Environment.GetEnvironmentVariable("DFU_PROBE_DUNGEON");
            SessionState.SetInt(PickIndex, string.IsNullOrEmpty(pick) ? -1 : int.Parse(pick));
            start.StartMethod = SessionState.GetInt(PickIndex, -1) < 0
                ? StartGameBehaviour.StartMethods.NewCharacter
                : StartGameBehaviour.StartMethods.DoNothing;

            // ModManager normally comes from the startup scene and survives the scene change.
            // Starting straight in the game scene skips that, so add one here (never saved).
            if (UnityEngine.Object.FindFirstObjectByType<DaggerfallWorkshop.Game.Utility.ModSupport.ModManager>() == null)
            {
                new GameObject("ModManager (probe)").AddComponent<DaggerfallWorkshop.Game.Utility.ModSupport.ModManager>();
                Debug.Log("[DungeonProbe] added ModManager to the scene so Assets/Game/Mods load as virtual mods");
            }
            Debug.Log("[DungeonProbe] StartMethod -> NewCharacter; entering play mode");
            SessionState.SetBool(Flag, true);
            SessionState.SetFloat(StartedAt, -1f);
            EditorApplication.isPlaying = true;
        }

        /// <summary>Point the new-character start at the Nth dungeon location in MAPS.BSA order.</summary>
        static bool PickDungeon(DaggerfallUnity dfu, int n)
        {
            var maps = dfu.ContentReader.MapFileReader;
            int seen = 0;
            for (int r = 0; r < maps.RegionCount; r++)
            {
                DaggerfallConnect.DFRegion region = maps.GetRegion(r);
                if (region.MapTable == null) continue;
                for (int i = 0; i < region.MapTable.Length; i++)
                {
                    if (!region.MapTable[i].LocationType.ToString().StartsWith("Dungeon")) continue;
                    if (seen++ != n) continue;
                    DaggerfallConnect.DFLocation loc = maps.GetLocation(r, i);
                    var px = DaggerfallConnect.Arena2.MapsFile.LongitudeLatitudeToMapPixel(
                        loc.MapTableData.Longitude, loc.MapTableData.Latitude);
                    DaggerfallUnity.Settings.StartCellX = px.X;
                    DaggerfallUnity.Settings.StartCellY = px.Y;
                    DaggerfallUnity.Settings.StartInDungeon = true;
                    Debug.Log(string.Format("[DungeonProbe] start -> dungeon #{0} '{1}' in {2} ({3}) at pixel {4},{5} type {6}",
                        n, loc.Name, loc.RegionName, region.MapTable[i].LocationType, px.X, px.Y, region.MapTable[i].DungeonType));
                    return true;
                }
            }
            return false;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;
            float t0 = SessionState.GetFloat(StartedAt, -1f);
            if (t0 < 0f)
            {
                int pick = SessionState.GetInt(PickIndex, -1);
                if (pick >= 0)
                {
                    var dfu = DaggerfallUnity.Instance;
                    if (dfu == null || !dfu.IsReady)
                        return;
                    var start = UnityEngine.Object.FindFirstObjectByType<StartGameBehaviour>();
                    if (!PickDungeon(dfu, pick))
                    {
                        Debug.LogError("[DungeonProbe] could not find dungeon #" + pick);
                        EditorApplication.isPlaying = false;
                        EditorApplication.delayCall += () => EditorApplication.Exit(3);
                        return;
                    }
                    start.StartMethod = StartGameBehaviour.StartMethods.NewCharacter;
                }
                SessionState.SetFloat(StartedAt, Time.realtimeSinceStartup);
                return;
            }
            if (Time.realtimeSinceStartup - t0 < WaitSeconds)
                return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(Flag, false);
            try
            {
                var gm = GameManager.Instance;
                var pee = gm != null ? gm.PlayerEnterExit : null;
                bool inside = pee != null && pee.IsPlayerInsideDungeon;
                int blocks = (pee != null && pee.Dungeon != null) ? pee.Dungeon.transform.childCount : -1;
                string loc = (pee != null && pee.Dungeon != null) ? pee.Dungeon.Summary.LocationName : "(none)";
                Vector3 pos = gm != null && gm.PlayerObject != null ? gm.PlayerObject.transform.position : Vector3.zero;
                Debug.Log(string.Format(
                    "[DungeonProbe] RESULT insideDungeon={0} dungeon='{1}' dungeonChildren={2} playerPos={3} mods={4}",
                    inside, loc, blocks, pos,
                    DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance != null
                        ? DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance.LoadedModCount.ToString() : "?"));
            }
            catch (Exception ex)
            {
                Debug.LogError("[DungeonProbe] RESULT inspection threw: " + ex);
            }
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
