// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Starts the desktop mods whose code is compiled into this app: Roleplay and Realism, its Items
// module, Climates & Calories, Dynamic Skies, Location Loader, World of Daggerfall, World of
// Daggerfall - Biomes, World of Daggerfall - Terrain and Distant Terrain of the World of Daggerfall
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
// Daggerfall - only Daggerfall Expanded Textures. Default off, and its two Inits run terrain-then-
// nature so that a terrain Init which threw stops the nature side from starting at all.
//
// World of Daggerfall - Terrain is a bundle as well, and gated by nothing but its own switch: it
// replaces the terrain sampler and computes ground height on the GPU. It goes last, because that
// replacement is the most invasive thing started here and its own Init declines to install the
// sampler on a device without compute shaders. Default off - turning it on moves the ground under
// existing saves and redraws the travel map.
//
// MOBILE: Distant Terrain of the World of Daggerfall is a bundle too - three mountain tables and a
// river/coast map - and, like Terrain, gated by nothing but its own switch. It draws the far
// terrain out to the horizon on a second, stacked camera. It goes after Terrain: both are off by
// default, and on a launch with both on the near sampler is the more invasive of the two, so it
// keeps its "started last of the invasive pair" position. Distant Terrain still runs before the
// sky is handed back below, because the sky now waits on the camera this mod creates.
//
// The survival mods start as soon as the bundles are loaded, at the title. Dynamic Skies cannot:
// its Init reaches into the scene for the sun light and the camera, and on a player build neither
// exists at the title. So the sky waits here, polling once a second, and starts when the scene has
// them - which is when a game is running.
//
// MOBILE: and when Distant Terrain is running, the sky waits for one thing more. BLBSkybox.Init
// decides ONCE, from what is in the scene at that moment, whether to clear the sky on the main
// camera or on Distant Terrain's stacked camera. Distant Terrain builds that camera only at world
// entry - the same moment the sun light and the main camera appear - so without the extra wait the
// branch is settled by a race between two deferred starts, and losing it leaves the far terrain
// drawn over a sky that was never cleared for it. The wait costs nothing when Distant Terrain is
// off or declined to install: the flag it asks is false, and the poll is the two-argument one.

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
        // MOBILE: the same note WoD uses, under the name the plan gave it - one literal, so the two
        // cannot drift apart.
        public const string BiomesDetNote = WoDDetNote;
        public const string TerrainTitle = "World of Daggerfall - Terrain";
        // MOBILE: the ModTitle declared in distantterrain.dfmod.json, verbatim. The launcher resolves
        // the entry by title, so a wrong literal here does not fail - the mod is simply never there.
        public const string DistantTitle = "Distant Terrain of the World of Daggerfall";
        // MOBILE: composed from SkyTitle so the two lines the sky writes while it waits can never
        // disagree about its name. Reads "[PortedMods] Dynamic Skies waiting for Distant Terrain's
        // stacked camera".
        public const string SkyStackedCameraWait = "[PortedMods] " + SkyTitle + " waiting for Distant Terrain's stacked camera";
        // MOBILE: and the line that ends that wait. Composed the same way, for the same reason.
        // Reads "[PortedMods] Dynamic Skies starting without Distant Terrain's stacked camera".
        public const string SkyStackedCameraGiveUp = "[PortedMods] " + SkyTitle + " starting without Distant Terrain's stacked camera";

        /// <summary>
        /// How many 1 Hz passes the sky waits for Distant Terrain's stacked camera once the rest of
        /// the scene is up. The far terrain is built synchronously inside StreamingWorld.OnReady, so
        /// the camera either appears on the pass after world entry or it is not coming - fifteen
        /// seconds is generous for a slow device and short enough that a player never notices.
        /// </summary>
        public const int SkyStackedCameraWaitPasses = 15;

        /// <summary>Pure: which of (rr, rrItems, cc) may run. Items needs RR; C&C needs both.</summary>
        public static bool[] Gate(bool rr, bool rrItems, bool cc)
        {
            bool items = rr && rrItems;
            return new[] { rr, items, items && cc };
        }

        /// <summary>Pure: Dynamic Skies runs only when its launcher entry exists (bundle installed) and is on.</summary>
        public static bool SkyRuns(bool entryPresent, bool enabled) => entryPresent && enabled;

        /// <summary>
        /// Pure: the sky's Init can only work once the scene holds the sun light and the camera it
        /// looks up - and, when Distant Terrain is running, not before its stacked camera exists
        /// either. BLBSkybox.Init reads the scene once and picks the camera it clears the sky on
        /// from what it finds; Distant Terrain creates that camera at world entry, the same moment
        /// the sun light and the main camera appear. Without this the choice is a race.
        /// </summary>
        public static bool SkySceneReady(bool sunLightPresent, bool mainCameraPresent, bool distantRunning, bool stackedCameraPresent)
            => sunLightPresent && mainCameraPresent && (!distantRunning || stackedCameraPresent);

        /// <summary>
        /// Pure: the two-argument form, kept for callers that know nothing of Distant Terrain. It
        /// asks the same question with the mod not running, which is exactly what "no extra wait"
        /// means - not "the stacked camera is already up".
        /// </summary>
        public static bool SkySceneReady(bool sunLightPresent, bool mainCameraPresent) => SkySceneReady(sunLightPresent, mainCameraPresent, false, false);

        /// <summary>
        /// Pure: should the poll stop waiting for Distant Terrain's stacked camera and start the sky
        /// anyway? Only the passes spent with the rest of the scene already up and that one camera
        /// still missing count - waiting on the title screen is not waiting on Distant Terrain. The
        /// give-up is a policy of the poll, not of SkySceneReady, which stays the plain contract.
        /// Without a bound, any reason for an absent stacked camera strands Dynamic Skies for the
        /// whole session; with it, the worst case is a sky that starts fifteen seconds late on the
        /// normal branch (see BLBSkybox, which keys that branch on the camera).
        /// </summary>
        public static bool GiveUpOnStackedCamera(int stackedWaitPasses) => stackedWaitPasses >= SkyStackedCameraWaitPasses;

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
        public static readonly string[] Titles = { RRTitle, RRItemsTitle, CCTitle, SkyTitle, LLTitle, WoDTitle, BiomesTitle, TerrainTitle, DistantTitle };

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
            bool stackedLogged = false;
            // MOBILE: passes spent with the scene up and only Distant Terrain's camera missing.
            int stackedWaitPasses = 0;
            while (true)
            {
                bool sunLight = GameObject.Find("SunLight") != null;
                bool mainCamera = GameObject.FindGameObjectWithTag("MainCamera") != null;
                // MOBILE: asked every pass rather than captured once. Distant Terrain's Init has
                // already run by the time this coroutine does - StartEnabled is synchronous - but
                // reading the flag here keeps the poll's answer a function of the world as it is,
                // and the stacked camera is a scene object that can come and go with a reload.
                bool distantRunning = DistantTerrain.DistantTerrainPort.Running;
                bool stackedCamera = GameObject.Find("stackedCamera") != null;
                if (SkySceneReady(sunLight, mainCamera, distantRunning, stackedCamera)) break;
                if (!sunLight || !mainCamera)
                {
                    if (!logged)
                    {
                        Debug.Log("[PortedMods] " + SkyTitle + " waiting for the scene (SunLight, MainCamera)");
                        logged = true;
                    }
                }
                // MOBILE: the scene is up and the only thing still missing is Distant Terrain's
                // camera. Said once, and only in that case, so a reader who sees it knows the wait
                // is this mod's and not the title screen's.
                else
                {
                    if (!stackedLogged)
                    {
                        Debug.Log(SkyStackedCameraWait);
                        stackedLogged = true;
                    }
                    // MOBILE: and it is bounded. A far terrain that threw or refused tears its
                    // camera down and clears Running, which releases this poll on the next pass -
                    // but any other reason for an absent stacked camera would otherwise strand the
                    // sky for the whole session on one log line. After fifteen passes the sky starts
                    // anyway; BLBSkybox keys its stacked-camera branch on the camera rather than on
                    // Distant Terrain, so a start without it takes the normal branch intact.
                    if (GiveUpOnStackedCamera(++stackedWaitPasses))
                    {
                        Debug.Log(SkyStackedCameraGiveUp);
                        break;
                    }
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
        /// As above, for a mod whose Init reports a refusal by LOGGING and RETURNING rather than by
        /// throwing - World of Daggerfall - Terrain declines that way on all four of its paths (no
        /// compute support, shaders without kernels, a missing bundle asset, a failed world heightmap).
        /// Containment is identical; what changes is the closing line, which is the line a Player.log
        /// reader trusts to mean "this mod is running". <paramref name="installed"/> is asked once,
        /// after a clean return; when it says no, the reader is pointed at <paramref name="hint"/> -
        /// the log prefix whose lines carry the reason - the way the sky's deferred start already does.
        /// Returns whether the mod is actually running, not merely whether Init returned.
        /// </summary>
        public static bool StartOne(string title, System.Action init, System.Func<bool> installed, string hint)
        {
            try { init(); }
            catch (System.Exception ex)
            {
                Debug.LogError("[PortedMods] " + title + " start failed: " + ex);
                return false;
            }
            if (!installed())
            {
                Debug.Log("[PortedMods] " + title + " did not start (see " + hint + " lines)");
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
                // Explicit order, not the manifest's. What the order buys is the `terrainStarted &&`
                // short-circuit below: a terrain Init that threw leaves no nature overrider installed,
                // rather than one running against a VEModEnabled stuck false, which would cache a
                // wrongly-scaled atlas for the rest of the session. The VEModEnabled read itself is
                // safe either way - WODBiomes.Init sets it (WODBiomes.cs:29-33) and the nature side
                // reads it only when it builds its atlas, on the first OnUpdateTerrainsEnd, long after
                // both Inits have returned inside this one synchronous StartEnabled. The terrain
                // material provider is installed later still, in WODBiomes.Start().
                int bi = ModManager.Instance.GetModIndex(BiomesTitle);
                bool terrainStarted = StartOne(BiomesTitle + " (terrain)", () => WorldOfDaggerfall.WODBiomes.Init(new InitParams(biomes, bi, count)));
                bool natureStarted = terrainStarted && StartOne(BiomesTitle + " (nature)", () => WorldOfDaggerfall.NatureBatchOverriderInstaller.Init(new InitParams(biomes, bi, count)));
                BiomesRunning = terrainStarted && natureStarted;
                if (BiomesRunning) Debug.Log("[PortedMods] started " + BiomesTitle);
            }

            // MOBILE: World of Daggerfall - Terrain. No dependency gate: Basic Roads is optional to it
            // (its road map is simply unused without it) and Daggerfall Expanded Textures is not its
            // dependency at all - its own switch is the whole condition. Started LAST of everything
            // here because it is the one port that replaces DaggerfallUnity.TerrainSampler outright; an
            // Init that threw at that point cannot cost any mod before it its start, and Init itself
            // refuses to install the sampler when the device has no compute support or the shaders did
            // not load ("[WoDTerrain] not available: ..."), leaving DFU's own terrain in place.
            Mod terrain = Entry(TerrainTitle);
            if (terrain != null && terrain.Enabled)
            {
                // Init declines by logging "[WoDTerrain] not available: ..." and returning normally on
                // all four of its refusal paths, so the plain StartOne would write "started" for a
                // device that is still on DFU's own sampler. Ask the mod itself instead.
                if (StartOne(TerrainTitle,
                        () => Monobelisk.InterestingTerrains.Init(new InitParams(terrain, ModManager.Instance.GetModIndex(TerrainTitle), count)),
                        () => Monobelisk.InterestingTerrains.Installed,
                        "[WoDTerrain]"))
                {
                    // Said once per launch, not a warning: the height curve it computes is a different
                    // world to the vanilla one, so a character standing on ground that has moved is the
                    // expected outcome, not a bug report. AFTER the install, so the player is only told
                    // the ground moved on the launch where it actually did.
                    Debug.Log("[PortedMods] " + TerrainTitle + ": this changes ground height under existing saves and the travel map (by design)");
                }
            }

            // MOBILE: Distant Terrain of the World of Daggerfall. No dependency gate, for the same
            // reason the Terrain port has none: its own switch is the whole condition, and its Init
            // declines by logging "[DistantTerrain] not available: ..." and returning when the
            // far-terrain shader did not compile or the bundle is missing its three mountain tables
            // or the river/coast map. So it needs the four-argument StartOne, and the flag it is
            // asked is Running - the gate passed, the world-entry hooks are subscribed - not the
            // flag that only turns true once StreamingWorld.OnReady has actually built the far
            // terrain, which is long after this Init returns and would always read "did not start"
            // here. That the far terrain really got built is said by "[DistantTerrain] far terrain
            // ready", from the build itself.
            //
            // After the Terrain block, before the sky is handed back: the sky's deferred start now
            // waits on the stackedCamera this Init's mod creates, so the flag it polls must already
            // be settled by the time StartSkyWhenSceneReady begins.
            Mod distant = Entry(DistantTitle);
            if (distant != null && distant.Enabled)
                StartOne(DistantTitle,
                    () => DistantTerrain.DistantTerrainPort.Init(new InitParams(distant, ModManager.Instance.GetModIndex(DistantTitle), count)),
                    () => DistantTerrain.DistantTerrainPort.Running,
                    "[DistantTerrain]");

            return SkyRuns(sky != null, sky != null && sky.Enabled) ? sky : null;
        }

        class Driver : MonoBehaviour { }
    }
}
