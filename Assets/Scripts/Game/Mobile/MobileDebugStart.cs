// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Device-side diagnostic for the "every building is a flat grey box" report (2026-09-03). The
// editor cannot reproduce it: the same bundles import every UBLaMF model fully textured there,
// so the evidence has to come from a player build. When Documents/debug-newchar.txt exists the
// app skips the title menu, starts a new character outdoors, and writes one line per custom
// model (and the first vanilla meshes) describing its materials - shader, texture, format - to
// the log MobileLog mirrors into Documents/Player.log. Inert without the file.

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileDebugStart
    {
        public const string FileName = "debug-newchar.txt";
        public static bool Active { get; private set; }
        static readonly HashSet<string> audited = new HashSet<string>();
        static int targetX = -1, targetY = -1;   // "pixel X Y" on the file's first line teleports there once in the world

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (!MobileContentPath.Active)
                return;
            string file = Path.Combine(Application.persistentDataPath, FileName);
            if (!File.Exists(file))
                return;
            Active = true;
            try
            {
                string[] words = File.ReadAllText(file).Split(new[] { ' ', '\n', '\r', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 3 && words[0] == "pixel") { targetX = int.Parse(words[1]); targetY = int.Parse(words[2]); }
            }
            catch (System.Exception) { }
            Debug.Log(string.Format("[DebugStart] {0} present: new character outdoors + material audit. gfx={1} astc6x6={2} copyTexture={3}",
                FileName, SystemInfo.graphicsDeviceType, SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_6x6), SystemInfo.copyTextureSupport));
            var go = new GameObject("MobileDebugStart");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        class Driver : MonoBehaviour
        {
            float readyAt = -1f, teleportedAt = -1f;
            bool fired, popped, teleported, done;

            void Update()
            {
                if (done) return;
                if (popped) { AfterStart(); return; }
                var dfu = DaggerfallUnity.Instance;
                if (dfu == null || !dfu.IsReady) return;
                var sgb = FindFirstObjectByType<StartGameBehaviour>();
                if (sgb == null) return;
                if (readyAt < 0f) { readyAt = Time.realtimeSinceStartup; return; }
                float t = Time.realtimeSinceStartup - readyAt;
                if (!fired && t > 3f)
                {
                    fired = true;
                    DaggerfallUnity.Settings.StartInDungeon = false;
                    sgb.StartMethod = StartGameBehaviour.StartMethods.NewCharacter;
                    Debug.Log("[DebugStart] StartMethod -> NewCharacter (outdoors)");
                }
                else if (fired && t > 6f)
                {
                    popped = true;
                    try { DaggerfallUI.Instance.PopToHUD(); } catch (System.Exception) { }
                    Shader sh = Shader.Find("Daggerfall/Default");
                    Debug.Log("[DebugStart] popped UI to HUD; Daggerfall/Default " + (sh ? "found supported=" + sh.isSupported + " id=" + sh.GetInstanceID() : "MISSING")
                        + " captured id=" + (MobileShaders.Find("Daggerfall/Default") ? MobileShaders.Find("Daggerfall/Default").GetInstanceID() : 0));
                    LogRenderState("after start");
                }
            }

            float lastWaitLog;

            void AfterStart()
            {
                var gm = GameManager.Instance;
                var sw = gm.StreamingWorld;
                if (gm.PlayerEntity == null || gm.PlayerEnterExit == null || gm.PlayerEnterExit.IsPlayerInside || sw == null || sw.IsInit || !sw.IsReady)   // IsInit means "init still pending"
                {
                    // Quest mods greet a new character with message boxes; the world does not init while paused.
                    var top = DaggerfallUI.UIManager.TopWindow as DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox;
                    if (top != null && Time.realtimeSinceStartup - lastWaitLog > 1f) { lastWaitLog = Time.realtimeSinceStartup - 9f; top.CloseWindow(); Debug.Log("[DebugStart] dismissed a message box"); return; }
                    if (Time.realtimeSinceStartup - lastWaitLog > 10f)
                    {
                        lastWaitLog = Time.realtimeSinceStartup;
                        Debug.Log("[DebugStart] waiting: entity=" + (gm.PlayerEntity != null) + " enterExit=" + (gm.PlayerEnterExit != null)
                            + " inside=" + (gm.PlayerEnterExit != null && gm.PlayerEnterExit.IsPlayerInside) + " sw=" + (sw != null)
                            + " init=" + (sw != null && sw.IsInit) + " ready=" + (sw != null && sw.IsReady)
                            + " pixel=" + gm.PlayerGPS.CurrentMapPixel.X + "," + gm.PlayerGPS.CurrentMapPixel.Y
                            + " paused=" + GameManager.IsGamePaused + " top=" + (DaggerfallUI.UIManager.TopWindow != null ? DaggerfallUI.UIManager.TopWindow.GetType().Name : "none"));
                    }
                    return;
                }
                if (!teleported)
                {
                    teleported = true;
                    teleportedAt = Time.realtimeSinceStartup;
                    if (targetX >= 0)
                    {
                        Debug.Log("[DebugStart] teleporting to map pixel " + targetX + "," + targetY);
                        sw.TeleportToCoordinates(targetX, targetY, StreamingWorld.RepositionMethods.DirectionFromStartMarker);
                    }
                    return;
                }
                if (sw.IsRepositioningPlayer || Time.realtimeSinceStartup - teleportedAt < 10f) return;
                var loc = sw.CurrentPlayerLocationObject;
                if (loc == null)
                {
                    if (Time.realtimeSinceStartup - teleportedAt > 60f) { done = true; Debug.Log("[DebugStart] no location object after 60 s at pixel " + gm.PlayerGPS.CurrentMapPixel.X + "," + gm.PlayerGPS.CurrentMapPixel.Y); }
                    return;
                }
                done = true;
                LogRenderState("at location");
                try { AuditLocation(loc.gameObject, gm.PlayerGPS.HasCurrentLocation ? gm.PlayerGPS.CurrentLocation.Name : "?"); }
                catch (System.Exception ex) { Debug.LogError("[DebugStart] location audit threw: " + ex); }
            }
        }

        static void LogRenderState(string when)
        {
            var sb = new StringBuilder();
            sb.Append("[DebugStart] render ").Append(when).Append(": fog=").Append(RenderSettings.fog).Append(' ').Append(RenderSettings.fogMode)
              .Append(" color=").Append(RenderSettings.fogColor).Append(" density=").Append(RenderSettings.fogDensity)
              .Append(" start/end=").Append(RenderSettings.fogStartDistance).Append('/').Append(RenderSettings.fogEndDistance)
              .Append(" ambient=").Append(RenderSettings.ambientMode).Append(' ').Append(RenderSettings.ambientLight).Append(" x").Append(RenderSettings.ambientIntensity);
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) sb.Append(" sun[").Append(l.name).Append(" on=").Append(l.enabled).Append(" i=").Append(l.intensity).Append(' ').Append(l.color).Append(']');
            Camera cam = Camera.main;
            if (cam) sb.Append(" cam clear=").Append(cam.clearFlags).Append(" bg=").Append(cam.backgroundColor).Append(" far=").Append(cam.farClipPlane);
            var mm = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance;
            if (mm != null) { sb.Append(" mods=").Append(mm.LoadedModCount).Append(':'); foreach (var m in mm.GetAllMods()) sb.Append(' ').Append(m.Title).Append(m.Enabled ? "" : "(off)"); }
            Debug.Log(sb.ToString());
        }

        /// <summary>Renderer-level summary of a built location, the same numbers the editor town probe reports.</summary>
        static void AuditLocation(GameObject loc, string locName)
        {
            int renderers = 0, replacements = 0, nullSlots = 0, noTexture = 0, fallbackMat = 0, missingScripts = 0, rtm = 0, rtmNotApplied = 0;
            var formats = new Dictionary<string, int>();
            var untextured = new Dictionary<string, int>();
            var applied = typeof(DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials).GetField("hasAppliedMaterials",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (MeshRenderer mr in loc.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                if (mr.gameObject.name.Contains("[Replacement]")) replacements++;
                foreach (Component c in mr.gameObject.GetComponents<Component>()) if (c == null) missingScripts++;
                var r = mr.GetComponent<DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials>();
                if (r != null) { rtm++; if (applied != null && !(bool)applied.GetValue(r)) rtmNotApplied++; }
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string key;
                    if (mats[i] == null) { nullSlots++; key = mr.gameObject.name + " slot" + i + " NULL"; }
                    else
                    {
                        if (mats[i].name.StartsWith("Default-Material")) fallbackMat++;
                        Texture t = mats[i].mainTexture;
                        if (t != null)
                        {
                            string fmt = t is Texture2D ? ((Texture2D)t).format.ToString() : t.GetType().Name;
                            int n; formats.TryGetValue(fmt, out n); formats[fmt] = n + 1;
                            continue;
                        }
                        noTexture++;
                        key = mr.gameObject.name + " mat '" + mats[i].name + "' sh=" + (mats[i].shader ? mats[i].shader.name : "null") + " NO-TEX";
                    }
                    int c; untextured.TryGetValue(key, out c); untextured[key] = c + 1;
                }
            }
            var sb = new StringBuilder();
            foreach (var kv in formats) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log(string.Format("[DebugStart] LOCATION '{0}' renderers={1} replacements={2} nullSlots={3} noTexture={4} fallbackMaterial={5} missingScripts={6} runtimeMaterials={7} notApplied={8} formats: {9}",
                locName, renderers, replacements, nullSlots, noTexture, fallbackMat, missingScripts, rtm, rtmNotApplied, sb));
            int shown = 0;
            foreach (var kv in untextured) { if (shown++ >= 25) break; Debug.Log("[DebugStart]   untextured x" + kv.Value + " " + kv.Key); }
        }

        /// <summary>One log line per distinct model describing every material slot.</summary>
        public static void Audit(string label, GameObject go, int cap)
        {
            if (!Active || go == null || audited.Count > cap * 4 || !audited.Add(label)) return;
            var sb = new StringBuilder();
            sb.Append("[DebugStart] ").Append(label).Append(':');
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = mr.sharedMaterials;
                sb.Append(" [").Append(mr.gameObject.name).Append(" slots=").Append(mats.Length);
                sb.Append(mr.GetComponent<DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials>() ? " rtm" : "");
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) { sb.Append(" NULL"); continue; }
                    sb.Append(" {").Append(m.name).Append(" sh=").Append(m.shader ? m.shader.name + (m.shader.isSupported ? "" : "!unsupported") : "null");
                    Texture tex = m.mainTexture;
                    if (tex == null) sb.Append(" NO-TEX");
                    else sb.Append(" tex=").Append(tex.name).Append(' ').Append(tex.width).Append('x').Append(tex.height).Append(tex is Texture2D ? " " + ((Texture2D)tex).format : "");
                    if (m.shaderKeywords.Length > 0) sb.Append(" kw=").Append(string.Join(",", m.shaderKeywords));
                    sb.Append('}');
                }
                sb.Append(']');
            }
            Debug.Log(sb.ToString());
        }
    }
}
