// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Diagnostic: start a new character outdoors, teleport into the nearest city with the built mod
// bundles loaded (StreamingAssets/Mods, what the pack zip ships), and audit every building
// renderer: empty material slots, materials with no texture, Unity's fallback material, and
// custom-model prefabs whose RuntimeMaterials component never applied. Written for the "all
// buildings are untextured boxes" report after the third pack wave (2026-09-03).
//
//   Unity -batchmode -projectPath . -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileTownProbe.Run
//   (NO -quit; the probe exits the editor itself.)  DFU_TOWN_MODS=1 also loads virtual mods.

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
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    [InitializeOnLoad]
    public static class MobileTownProbe
    {
        const string Armed = "DFMobile.TownProbe.Armed";
        const string Started = "DFMobile.TownProbe.Started";
        const string Phase = "DFMobile.TownProbe.Phase";   // 0 wait world, 1 teleported, 2 done
        static float phaseAt = -1f;
        static readonly List<string> problems = new List<string>();

        static MobileTownProbe()
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
            if (start == null) { Debug.LogError("[TownProbe] no StartGameBehaviour"); EditorApplication.Exit(2); return; }
            var mm = UnityEngine.Object.FindFirstObjectByType<ModManager>();
            if (mm == null)
                mm = new GameObject("ModManager (probe)").AddComponent<ModManager>();
            mm.LoadVirtualMods = System.Environment.GetEnvironmentVariable("DFU_TOWN_MODS") == "1";
            start.StartMethod = StartGameBehaviour.StartMethods.DoNothing;
            SessionState.SetBool(Armed, true);
            SessionState.SetBool(Started, false);
            SessionState.SetInt(Phase, 0);
            Debug.Log("[TownProbe] entering play mode");
            EditorApplication.isPlaying = true;
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || condition.StartsWith("[TownProbe]")) return;
            if (problems.Count < 60 && !problems.Contains(condition))
                problems.Add(type + ": " + condition);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (!GameManager.HasInstance || GameManager.Instance.PlayerGPS == null) return;
            var dfu = DaggerfallUnity.Instance;
            if (dfu == null || !dfu.IsReady) return;
            var sw = GameManager.Instance.StreamingWorld;
            var gps = GameManager.Instance.PlayerGPS;

            if (!SessionState.GetBool(Started, false))
            {
                var sgb = UnityEngine.Object.FindFirstObjectByType<StartGameBehaviour>();
                if (sgb == null) return;
                SessionState.SetBool(Started, true);
                DaggerfallUnity.Settings.StartInDungeon = false;
                sgb.StartMethod = StartGameBehaviour.StartMethods.NewCharacter;
                Debug.Log("[TownProbe] new character outdoors");
                return;
            }
            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEnterExit == null
                || GameManager.Instance.PlayerEnterExit.IsPlayerInside || sw == null || !sw.IsInit || !sw.IsReady)
                return;

            if (SessionState.GetInt(Phase, 0) != 0) return;
            SessionState.SetInt(Phase, 2);
            try { AuditModels(); }
            catch (Exception ex) { Debug.LogError("[TownProbe] audit threw: " + ex); }
            Finish();
        }

        // Import every UBLaMF building model exactly the way RDBLayout/RMBLayout do
        // (MeshReplacement.ImportCustomGameobject), then audit the resulting renderers. Also load
        // a handful of vanilla building textures through the same replacement path VE uses.
        static void AuditModels()
        {
            var parent = new GameObject("TownProbe models").transform;
            string json = System.IO.File.ReadAllText("Assets/Game/Mods/UBLaMF-Models/UBLaMF - Models.dfmod.json");
            int imported = 0, notImported = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, @"Prefabs/(\d+)\.prefab"))
            {
                uint id = uint.Parse(m.Groups[1].Value);
                GameObject go = MeshReplacement.ImportCustomGameobject(id, parent, Matrix4x4.identity);
                if (go == null) notImported++; else imported++;
            }
            Debug.Log("[TownProbe] UBLaMF models imported=" + imported + " notImported=" + notImported);
            Audit(parent.gameObject, "UBLaMF models");

            var sb = new System.Text.StringBuilder();
            foreach (var ar in new[] { new[] { 67, 14 }, new[] { 160, 0 }, new[] { 147, 5 }, new[] { 302, 1 }, new[] { 0, 0 } })
            {
                Texture2D tex;
                bool ok = TextureReplacement.TryImportTexture(ar[0], ar[1], 0, out tex);
                sb.Append(ar[0].ToString("000")).Append('_').Append(ar[1]).Append(ok && tex != null ? "=" + tex.format + " " + tex.width + "x" + tex.height : "=none").Append("; ");
            }
            Debug.Log("[TownProbe] loose texture imports (VE): " + sb);
        }

        static void Audit(GameObject loc, string locName)
        {
            var mods = ModManager.Instance != null ? ModManager.Instance.GetAllMods() : new Mod[0];
            var sbMods = new System.Text.StringBuilder();
            foreach (Mod m in mods) sbMods.Append(m.Title).Append(m.Enabled ? "" : "(off)").Append("; ");
            Debug.Log("[TownProbe] mods loaded=" + mods.Length + ": " + sbMods);

            int renderers = 0, replacements = 0, nullSlots = 0, noTexture = 0, fallbackMat = 0, missingScripts = 0;
            int rtmComponents = 0, rtmNotApplied = 0;
            var texFormats = new Dictionary<string, int>();
            var samples = new List<string>();
            var untexturedNames = new Dictionary<string, int>();
            FieldInfo applied = typeof(RuntimeMaterials).GetField("hasAppliedMaterials", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (MeshRenderer mr in loc.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                bool isReplacement = mr.gameObject.name.Contains("[Replacement]");
                if (isReplacement) replacements++;
                foreach (Component c in mr.gameObject.GetComponents<Component>())
                    if (c == null) missingScripts++;
                var rtm = mr.GetComponent<RuntimeMaterials>();
                if (rtm != null)
                {
                    rtmComponents++;
                    if (applied != null && !(bool)applied.GetValue(rtm)) rtmNotApplied++;
                }
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) { nullSlots++; Note(untexturedNames, mr.gameObject.name + " slot" + i + " NULL"); continue; }
                    if (mats[i].name.StartsWith("Default-Material")) fallbackMat++;
                    Texture t = mats[i].mainTexture;
                    if (t == null)
                    {
                        noTexture++;
                        Note(untexturedNames, mr.gameObject.name + " mat '" + mats[i].name + "' shader " + mats[i].shader.name + " NO TEXTURE");
                        continue;
                    }
                    string fmt = t is Texture2D ? ((Texture2D)t).format.ToString() : t.GetType().Name;
                    int n; texFormats.TryGetValue(fmt, out n); texFormats[fmt] = n + 1;
                    if (samples.Count < 6 && isReplacement)
                        samples.Add(mr.gameObject.name + " -> '" + mats[i].name + "' tex '" + t.name + "' " + fmt + " " + t.width + "x" + t.height);
                }
            }
            var sbF = new System.Text.StringBuilder();
            foreach (var kv in texFormats) sbF.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log(string.Format("[TownProbe] RESULT location='{0}' renderers={1} replacements={2} nullSlots={3} noTexture={4} fallbackMaterial={5} missingScripts={6} runtimeMaterials={7} notApplied={8} formats: {9}",
                locName, renderers, replacements, nullSlots, noTexture, fallbackMat, missingScripts, rtmComponents, rtmNotApplied, sbF));
            foreach (string s in samples) Debug.Log("[TownProbe]   sample " + s);
            int shown = 0;
            foreach (var kv in untexturedNames) { if (shown++ >= 25) break; Debug.Log("[TownProbe]   untextured x" + kv.Value + " " + kv.Key); }
            Debug.Log("[TownProbe] warnings/errors during run: " + problems.Count);
            foreach (string p in problems) Debug.Log("[TownProbe]   " + p);
        }

        static void Note(Dictionary<string, int> d, string key)
        {
            int n; d.TryGetValue(key, out n); d[key] = n + 1;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Armed, false);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
