// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Starts the desktop mods whose code is compiled into this app: Roleplay and Realism, its Items
// module, Climates & Calories, Dynamic Skies, Location Loader and World of Daggerfall
// (Assets/Scripts/Game/Mobile/Ports/). This class calls the mod's own Init exactly as DFU would
// have called its [Invoke] loader, but only when the entry is enabled, and only with its
// dependencies on. Nothing runs otherwise.
//
// The three survival mods ship their DATA as a bundle inside the app, so each always has an
// ordinary entry in the launcher's MODS window with settings. Dynamic Skies' data is NOT built in:
// the player installs that bundle themselves, so its entry - and therefore the mod - simply is not
// there until they do, and the code stays dormant.
//
// Location Loader is code only - it has no data of its own and does nothing alone - so it is a
// built-in entry registered by MobileMods. World of Daggerfall is the location mod it reads: a
// bundle, so its entry appears only once that bundle is installed, and it needs Location Loader
// switched on AND started, plus Daggerfall Expanded Textures - the mod its manifest depends on for
// the textures its scenery uses - installed and switched on. Both default off.
//
// World of Daggerfall - Biomes is a bundle too, but a standalone one: it re-skins the terrain and
// swaps the nature billboards by itself, so it needs neither Location Loader nor World of
// Daggerfall - only Daggerfall Expanded Textures. Default off, and its two Inits run in that order
// (terrain, then nature) because the nature overrider looks the terrain provider up.
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
        public const string LLTitle = "Location Loader";
        public const string WoDTitle = "World of Daggerfall";
        public const string DynamicSkiesShaderName = "BLB/SkyBox/BLBProceduralSkybox";
        public const string GateNote = " Needs RoleplayRealism and RoleplayRealism-Items switched on; it was switched off because one of them is not.";
        public const string WoDGateNote = " Needs Location Loader switched on; it was switched off because Location Loader is not.";
        public const string DETFileName = "daggerfall expanded textures";
        public const string WoDDetNote = " Needs Daggerfall Expanded Textures switched on; it was switched off because that mod is off or not installed.";
        public const string BiomesTitle = "World of Daggerfall - Biomes";
        public const string BiomesDetNote = " Needs Daggerfall Expanded Textures switched on; it was switched off because that mod is off or not installed.";

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

        /// <summary>
        /// Pure: World of Daggerfall is a location mod - it is nothing without Location Loader reading it,
        /// and its scenery is textured out of Daggerfall Expanded Textures, which its manifest depends on.
        /// The loader argument is whether Location Loader actually STARTED, not whether it is switched on:
        /// an Init that threw leaves nothing to read the locations either.
        /// </summary>
        public static bool WodRuns(bool locationLoaderStarted, bool wodOn, bool detOn) => locationLoaderStarted && wodOn && detOn;

        /// <summary>
        /// Pure: World of Daggerfall - Biomes re-skins the terrain and swaps the nature billboards on its
        /// own, so it needs neither Location Loader nor World of Daggerfall - only its own switch and
        /// Daggerfall Expanded Textures, the mod its manifest depends on for the tiles it draws from.
        /// </summary>
        public static bool BiomesRuns(bool biomesOn, bool detOn) => biomesOn && detOn;

        /// <summary>True once both Biomes Inits returned this session; Location Loader's type-5 nature swap keys on it.</summary>
        public static bool BiomesRunning;

        /// <summary>Titles of the compiled-in mods, in dependency order.</summary>
        public static readonly string[] Titles = { RRTitle, RRItemsTitle, CCTitle, SkyTitle, LLTitle, WoDTitle, BiomesTitle };

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
            catch (System.Exception ex)
            {
                Debug.LogError("[PortedMods] " + SkyTitle + " start failed: " + ex);
                BLBSkybox.Teardown();   // Init builds Instance up front, so a part-way throw leaves it alive
            }
            Debug.Log(BLBSkybox.Instance != null
                ? "[PortedMods] started " + SkyTitle
                : "[PortedMods] " + SkyTitle + " did not start (see [DynamicSkies] lines)");
        }

        /// <summary>
        /// Is Daggerfall Expanded Textures installed and switched on? WoD's manifest declares it as a
        /// dependency by the name "daggerfall expanded textures", and DFU resolves a dependency name
        /// against Mod.FileName - CheckModDependencies -> GetModFromName -> ModManager.FileNameMatches,
        /// an ordinal Equals. This asks the same question through the same comparison, so this gate and
        /// DFU's own dependency warning can never disagree about whether the dependency is satisfied.
        /// DET ships as the converted bundle "daggerfall expanded textures.dfmod"; the title inside the
        /// bundle differs, so the file name is the only thing worth matching on.
        /// </summary>
        static bool DetOn(ModManager manager)
        {
            if (manager == null) return false;
            foreach (Mod mod in manager.Mods)
                if (ModManager.FileNameMatches(mod, DETFileName) && mod.Enabled)
                    return true;
            return false;
        }

        static Mod Entry(string title)
        {
            return ModManager.Instance != null && ModManager.Instance.GetModIndex(title) >= 0 ? ModManager.Instance.GetMod(title) : null;
        }

        /// <summary>
        /// Runs one mod's Init and says whether it got through. Each mod is contained: a throw from
        /// one used to abandon every mod after it AND the sky's deferred start, for the whole session.
        /// The "started" line is only written when Init returned normally.
        /// </summary>
        public static bool StartOne(string title, System.Action init)
        {
            try { init(); }
            catch (System.Exception ex)
            {
                Debug.LogError("[PortedMods] " + title + " start failed: " + ex);
                return false;
            }
            Debug.Log("[PortedMods] started " + title);
            return true;
        }

        /// <summary>
        /// Starts the survival mods now and returns the Dynamic Skies entry when it is to be started
        /// later, once the scene is ready; null when the sky is not to run at all.
        /// </summary>
        static Mod StartEnabled()
        {
            // Resolved before any Init runs: one mod throwing must not cost the sky its deferred start.
            Mod sky = Entry(SkyTitle);

            Mod rr = Entry(RRTitle), items = Entry(RRItemsTitle), cc = Entry(CCTitle);
            EnsureOrder(rr, items, cc);
            bool[] run = Gate(rr != null && rr.Enabled, items != null && items.Enabled, cc != null && cc.Enabled);
            if (cc != null && cc.Enabled && !run[2])
            {
                cc.Enabled = false;
                if (!(cc.ModInfo.ModDescription ?? "").Contains(GateNote))
                    cc.ModInfo.ModDescription = (cc.ModInfo.ModDescription ?? "") + GateNote;
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
            if (run[0]) StartOne(RRTitle, () => RoleplayRealism.RoleplayRealism.Init(new InitParams(rr, ModManager.Instance.GetModIndex(RRTitle), count)));
            if (run[1]) StartOne(RRItemsTitle, () => RoleplayRealism.RoleplayRealismItemsMod.Init(new InitParams(items, ModManager.Instance.GetModIndex(RRItemsTitle), count)));
            if (run[2]) StartOne(CCTitle, () => ClimatesCalories.ClimateCalories.Init(new InitParams(cc, ModManager.Instance.GetModIndex(CCTitle), count)));

            Mod ll = Entry(LLTitle), wod = Entry(WoDTitle);
            bool llOn = ll != null && ll.Enabled;
            bool detOn = DetOn(ModManager.Instance);
            // The player's own choice, read before either gate can clear it: with both dependencies
            // absent both notes apply, so neither gate may be conditioned on the other's result.
            bool wodChosen = wod != null && wod.Enabled;
            if (wodChosen && !llOn)
            {
                wod.Enabled = false;
                // Contains, not EndsWith: both notes can apply, and once the other one is on the
                // end the first is not, so EndsWith would stop recognizing it. Nothing here spans
                // launches - the manifest rebuilds ModDescription every time.
                if (!(wod.ModInfo.ModDescription ?? "").Contains(WoDGateNote)) wod.ModInfo.ModDescription += WoDGateNote;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] World of Daggerfall switched off: Location Loader must be on");
            }
            if (wodChosen && !detOn)
            {
                wod.Enabled = false;
                if (!(wod.ModInfo.ModDescription ?? "").Contains(WoDDetNote)) wod.ModInfo.ModDescription += WoDDetNote;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] World of Daggerfall off: Daggerfall Expanded Textures is not enabled");
            }
            bool llStarted = llOn && StartOne(LLTitle, () => LocationLoader.LocationModLoader.Init(new InitParams(ll, ModManager.Instance.GetModIndex(LLTitle), count)));
            bool wodEnabled = wod != null && wod.Enabled;
            if (WodRuns(llStarted, wodEnabled, detOn))
                StartOne(WoDTitle, () => WODRocksMaterials.WODRocksMaterials.Init(new InitParams(wod, ModManager.Instance.GetModIndex(WoDTitle), count)));
            else if (wodEnabled && detOn && llOn)
                // Both switches are on and the textures are there, so the only way here is LL's Init
                // throwing. Nothing is left to read the locations; a runtime failure is not a user
                // choice, so the entry keeps its setting and only the log says what happened.
                Debug.Log("[PortedMods] World of Daggerfall not started: Location Loader failed");

            // Biomes is independent of Location Loader and of WoD: it re-skins terrain and swaps
            // nature billboards itself. Only Daggerfall Expanded Textures gates it.
            Mod biomes = Entry(BiomesTitle);
            bool biomesChosen = biomes != null && biomes.Enabled;
            if (biomesChosen && !detOn)
            {
                biomes.Enabled = false;
                if (!(biomes.ModInfo.ModDescription ?? "").Contains(BiomesDetNote)) biomes.ModInfo.ModDescription += BiomesDetNote;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] World of Daggerfall - Biomes off: Daggerfall Expanded Textures is not enabled");
            }
            if (BiomesRuns(biomes != null && biomes.Enabled, detOn))
            {
                // Explicit order, not the manifest's: the terrain material provider must exist before
                // the nature overrider installs itself, which is the upstream VEModEnabled race.
                int bi = ModManager.Instance.GetModIndex(BiomesTitle);
                bool terrainStarted = StartOne(BiomesTitle + " (terrain)", () => WorldOfDaggerfall.WODBiomes.Init(new InitParams(biomes, bi, count)));
                bool natureStarted = terrainStarted && StartOne(BiomesTitle + " (nature)", () => WorldOfDaggerfall.NatureBatchOverriderInstaller.Init(new InitParams(biomes, bi, count)));
                BiomesRunning = terrainStarted && natureStarted;
                if (BiomesRunning) Debug.Log("[PortedMods] started " + BiomesTitle);
            }

            return SkyRuns(sky != null, sky != null && sky.Enabled) ? sky : null;
        }

        class Driver : MonoBehaviour { }
    }
}
