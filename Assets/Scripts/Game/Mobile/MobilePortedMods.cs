// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Starts the desktop mods whose code is compiled into this app: Roleplay and Realism, its Items
// module, Climates & Calories and Dynamic Skies (Assets/Scripts/Game/Mobile/Ports/). This class
// calls the mod's own Init exactly as DFU would have called its [Invoke] loader, but only when the
// entry is enabled, and only with its dependencies on. Nothing runs otherwise.
//
// The three survival mods ship their DATA as a bundle inside the app, so each always has an
// ordinary entry in the launcher's MODS window with settings. Dynamic Skies' data is NOT built in:
// the player installs that bundle themselves, so its entry - and therefore the mod - simply is not
// there until they do, and the code stays dormant.
//
// The survival mods start as soon as the bundles are loaded, at the title. Dynamic Skies cannot:
// its Init reaches into the scene for the sun light and the camera, and on a player build neither
// exists at the title. So the sky waits here, polling once a second, and starts when the scene has
// them - which is when a game is running.

using System.Collections;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobilePortedMods
    {
        public const string RRTitle = "RoleplayRealism";
        public const string RRItemsTitle = "RoleplayRealism-Items";
        public const string CCTitle = "Climates & Calories";
        public const string SkyTitle = "Dynamic Skies";
        public const string DynamicSkiesShaderName = "BLB/SkyBox/BLBProceduralSkybox";
        public const string GateNote = " Needs RoleplayRealism and RoleplayRealism-Items switched on; it was switched off because one of them is not.";

        /// <summary>Pure: which of (rr, rrItems, cc) may run. Items needs RR; C&C needs both.</summary>
        public static bool[] Gate(bool rr, bool rrItems, bool cc)
        {
            bool items = rr && rrItems;
            return new[] { rr, items, items && cc };
        }

        /// <summary>Pure: Dynamic Skies runs only when its launcher entry exists (bundle installed) and is on.</summary>
        public static bool SkyRuns(bool entryPresent, bool enabled) => entryPresent && enabled;

        /// <summary>Pure: the sky's Init can only work once the scene holds the sun light and the camera it looks up.</summary>
        public static bool SkySceneReady(bool sunLightPresent, bool mainCameraPresent) => sunLightPresent && mainCameraPresent;

        /// <summary>Titles of the compiled-in mods, in dependency order.</summary>
        public static readonly string[] Titles = { RRTitle, RRItemsTitle, CCTitle, SkyTitle };

        /// <summary>
        /// Called by ModManager after it found the bundles and before it applies saved settings: a
        /// bundle the engine has just discovered defaults to enabled, but these are systems a player
        /// must choose, so they start off. A saved choice overrides this. Titles with no entry (Dynamic
        /// Skies until its bundle is installed) are skipped.
        /// </summary>
        public static void DefaultOff(ModManager manager)
        {
            foreach (string title in Titles)
                if (manager.GetModIndex(title) >= 0)
                    manager.GetMod(title).Enabled = false;
        }

        /// <summary>
        /// Pure: load priorities for (RR, Items, C&C) that keep RR &lt; Items &lt; C&amp;C, the order the
        /// desktop manifests' dependencies produce. C&amp;C registers the same bed and campfire activations
        /// as RR and the engine gives them to the mod with the higher priority, so C&amp;C must be last.
        /// Already ordered: unchanged. Otherwise the three move to the end, after <paramref name="maxPriority"/>.
        /// </summary>
        public static int[] OrderedPriorities(int rr, int items, int cc, int maxPriority)
        {
            if (rr < items && items < cc)
                return new[] { rr, items, cc };
            return new[] { maxPriority + 1, maxPriority + 2, maxPriority + 3 };
        }

        static void EnsureOrder(Mod rr, Mod items, Mod cc)
        {
            if (rr == null || items == null || cc == null) return;
            int max = -1;
            foreach (Mod m in ModManager.Instance.GetAllMods()) if (m.LoadPriority > max) max = m.LoadPriority;
            int[] p = OrderedPriorities(rr.LoadPriority, items.LoadPriority, cc.LoadPriority, max);
            if (p[0] == rr.LoadPriority && p[1] == items.LoadPriority && p[2] == cc.LoadPriority) return;
            rr.LoadPriority = p[0]; items.LoadPriority = p[1]; cc.LoadPriority = p[2];
            ModManager.Instance.SortMods();
            ModManager.WriteModSettings();
            Debug.Log("[PortedMods] load order fixed: RoleplayRealism < Items < Climates & Calories");
        }

        static bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            StateManager.OnStateChange -= OnStateChange;
            StateManager.OnStateChange += OnStateChange;
        }

        static void OnStateChange(StateManager.StateTypes state)
        {
            if (state != StateManager.StateTypes.Start || started) return;
            started = true;
            var go = new GameObject("MobilePortedMods");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>().StartCoroutine(StartAfterModManager(go));
        }

        static IEnumerator StartAfterModManager(GameObject go)
        {
            yield return null;      // ModManager's own Start-state handler loads the bundles first
            Mod sky = null;
            try { sky = StartEnabled(); }
            catch (System.Exception ex) { Debug.LogError("[PortedMods] start failed: " + ex); }
            if (sky != null)
                yield return StartSkyWhenSceneReady(sky);   // the driver must live until this finishes
            Object.Destroy(go);
        }

        /// <summary>
        /// Waits for the scene the sky's Init needs, then starts it. Polls at 1 Hz, not per frame:
        /// the wait normally spans the whole title screen.
        /// </summary>
        static IEnumerator StartSkyWhenSceneReady(Mod sky)
        {
            bool logged = false;
            while (!SkySceneReady(GameObject.Find("SunLight") != null, GameObject.FindGameObjectWithTag("MainCamera") != null))
            {
                if (!logged)
                {
                    Debug.Log("[PortedMods] " + SkyTitle + " waiting for the scene (SunLight, MainCamera)");
                    logged = true;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            try { BLBSkybox.Init(new InitParams(sky, ModManager.Instance.GetModIndex(SkyTitle), ModManager.Instance.LoadedModCount)); }
            catch (System.Exception ex) { Debug.LogError("[PortedMods] " + SkyTitle + " start failed: " + ex); }
            Debug.Log(BLBSkybox.Instance != null
                ? "[PortedMods] started " + SkyTitle
                : "[PortedMods] " + SkyTitle + " did not start (see [DynamicSkies] lines)");
        }

        static Mod Entry(string title)
        {
            return ModManager.Instance != null && ModManager.Instance.GetModIndex(title) >= 0 ? ModManager.Instance.GetMod(title) : null;
        }

        /// <summary>
        /// Starts the survival mods now and returns the Dynamic Skies entry when it is to be started
        /// later, once the scene is ready; null when the sky is not to run at all.
        /// </summary>
        static Mod StartEnabled()
        {
            Mod rr = Entry(RRTitle), items = Entry(RRItemsTitle), cc = Entry(CCTitle);
            EnsureOrder(rr, items, cc);
            bool[] run = Gate(rr != null && rr.Enabled, items != null && items.Enabled, cc != null && cc.Enabled);
            if (cc != null && cc.Enabled && !run[2])
            {
                cc.Enabled = false;
                if (!cc.ModInfo.ModDescription.EndsWith(GateNote)) cc.ModInfo.ModDescription += GateNote;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] Climates & Calories switched off: RoleplayRealism and its Items must be on");
            }
            if (items != null && items.Enabled && !run[1])
            {
                items.Enabled = false;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] RoleplayRealism-Items switched off: RoleplayRealism must be on");
            }
            int count = ModManager.Instance.LoadedModCount;
            if (run[0]) { RoleplayRealism.RoleplayRealism.Init(new InitParams(rr, ModManager.Instance.GetModIndex(RRTitle), count)); Debug.Log("[PortedMods] started " + RRTitle); }
            if (run[1]) { RoleplayRealism.RoleplayRealismItemsMod.Init(new InitParams(items, ModManager.Instance.GetModIndex(RRItemsTitle), count)); Debug.Log("[PortedMods] started " + RRItemsTitle); }
            if (run[2]) { ClimatesCalories.ClimateCalories.Init(new InitParams(cc, ModManager.Instance.GetModIndex(CCTitle), count)); Debug.Log("[PortedMods] started " + CCTitle); }

            Mod sky = Entry(SkyTitle);
            return SkyRuns(sky != null, sky != null && sky.Enabled) ? sky : null;
        }

        class Driver : MonoBehaviour { }
    }
}
