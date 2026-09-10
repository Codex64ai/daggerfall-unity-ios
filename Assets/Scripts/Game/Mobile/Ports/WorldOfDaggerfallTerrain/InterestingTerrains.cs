// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/InterestingTerrains.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Monobelisk.Compatibility;

namespace Monobelisk
{
    public class InterestingTerrains : MonoBehaviour
    {
        public static readonly TileDataCache tileDataCache = new TileDataCache();

        public static InterestingTerrains instance;
        public static Mod Mod { get; private set; }

        public static Settings settings = new Settings();
        public TerrainComputerParams csParams;
        public static Texture2D biomeMap;
        public static Texture2D derivMap;
        public static Texture2D portMap;
        public static Texture2D roadMap;
        public static Texture2D tileableNoise;
        public static ComputeShader csPrototype;
        public static ComputeShader mainHeightComputer;

        // MOBILE: (M2) the parameters and the world heightmap are prepared BEFORE the GameObject
        // MOBILE: exists, and Awake - the thing that replaces DaggerfallUnity.TerrainSampler - picks
        // MOBILE: them up from here. So the sampler is never installed with a null or half-parsed
        // MOBILE: csParams, not even for the length of one call stack.
        private static TerrainComputerParams preparedParams;

        /// <summary>
        /// MOBILE: (T5-1) did this session's Init get all the way through the sampler swap? Every one of
        /// Init's four refusals logs "[WoDTerrain] not available: ..." and returns NORMALLY - Init is
        /// void and rethrows nothing - so a caller that only watches for a throw cannot tell a device
        /// with no compute support from a working one, and MobilePortedMods would write its "started"
        /// line either way. This is the answer to that question, and nothing else was: `instance` is an
        /// incidental MonoBehaviour handle, not a statement about the sampler. False until Init runs;
        /// set false at the top of every Init, true only on its last line.
        /// </summary>
        public static bool Installed { get; private set; }

        #region Invoke
        // MOBILE: the [Invoke(StateManager.StateTypes.Start, 0)] attribute is gone - this port has no
        // MOBILE: .dfmod assembly, so MobilePortedMods.StartEnabled calls Init directly (started last).

        /// <summary>
        /// MOBILE: (g) pure capability gate, so the "can this run at all" rule is testable headlessly.
        /// The mod has no CPU heightmap generator of any kind: without compute support, or without both
        /// compute shaders compiled and carrying their kernels, the only safe answer is to leave DFU's
        /// own sampler in place rather than replace it and produce garbage terrain.
        /// </summary>
        public static bool Available(bool supportsCompute, bool shadersLoaded)
        {
            return supportsCompute && shadersLoaded;
        }

        public static void Init(InitParams initParams)
        {
            Installed = false;      // MOBILE: (T5-1) nothing is installed until the last line says so
            Mod = initParams.Mod;

            // MOBILE: (g) the compute shaders ship inside the app (Assets/Resources/WoDTerrain), not in
            // MOBILE: the bundle, so they load with Resources.Load rather than Mod.GetAsset. A .compute
            // MOBILE: that fails to compile still loads as a non-null asset with no kernels, hence the
            // MOBILE: HasKernel checks as well as the null checks.
            csPrototype = Resources.Load<ComputeShader>("WoDTerrain/TerrainComputer");
            mainHeightComputer = Resources.Load<ComputeShader>("WoDTerrain/MainHeightmapComputer");

            bool shadersLoaded = csPrototype != null && mainHeightComputer != null
                && csPrototype.HasKernel("TerrainComputer")
                && csPrototype.HasKernel("TilemapComputer")
                && mainHeightComputer.HasKernel("CSMain");

            if (!Available(SystemInfo.supportsComputeShaders, shadersLoaded))
            {
                Debug.LogWarning("[WoDTerrain] not available: " + (SystemInfo.supportsComputeShaders
                    ? "compute shaders WoDTerrain/TerrainComputer + WoDTerrain/MainHeightmapComputer did not load with their kernels (check the shader compiler log)"
                    : "this device reports no compute shader support"));
                ReleaseAssets();    // MOBILE: (M1)
                return;
            }

            // MOBILE: (g) the bundle assets are loaded and checked BEFORE the GameObject is created,
            // MOBILE: because AddComponent runs Awake synchronously and Awake is what replaces
            // MOBILE: DaggerfallUnity.TerrainSampler. Upstream loaded them afterwards, so a missing map
            // MOBILE: or INI left the GPU sampler installed with null textures - garbage terrain instead
            // MOBILE: of vanilla terrain. Same rule as the compute gate: fail before touching the sampler.
            TextAsset paramIni;
            if (!TryLoadBundleAssets(out paramIni))
                return;

            // MOBILE: (M2) the last fallible stage - the INI parse and the banded world-heightmap
            // MOBILE: dispatch - runs before any GameObject exists. Everything that mutates global
            // MOBILE: state (the sampler, TerrainScale, farClipPlane, WoodsFileReader.Buffer) is
            // MOBILE: below this line, so a failure above it leaves DFU's own terrain untouched.
            if (!TryPrepareWorld(paramIni))
                return;

            var go = new GameObject(Mod.Title);
            // MOBILE: (M9) every other driver this port installs is marked (MobilePortedMods.cs:150,
            // MOBILE: WODClimates.cs:66); this one was left in the game scene. A reload of that scene -
            // MOBILE: reachable through DaggerfallUnitySetupGameWizard's SceneControl - would destroy
            // MOBILE: it, run OnDestroy -> TerrainComputer.Cleanup() and hand the new DaggerfallUnity
            // MOBILE: its default sampler, while MobilePortedMods' static "started" flag blocks any
            // MOBILE: re-Init: the MODS entry would read ON over vanilla terrain with nothing in the
            // MOBILE: log to say so. Marked before AddComponent, matching WODClimates.
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<InterestingTerrains>();

            // MOBILE: (R4) everything from here on runs with the GPU sampler ALREADY LIVE - Awake
            // replaced DaggerfallUnity.TerrainSampler and hooked OnPromoteTerrainData synchronously
            // inside AddComponent above. So a throw in these two statements (GameManager.Instance
            // .StreamingWorld is a getter that THROWS when it cannot find the component, and
            // ModMessageHandler.Init is not guaranteed not to) leaves Installed false and the caller
            // logging "did not start" over a world whose terrain this port is generating - at
            // TerrainScale 1.5 instead of 1, and deaf to mod messages. A rollback is not attempted
            // here: undoing Awake means putting back a sampler, unhooking an event, un-readying the
            // mod and choosing what the far clip plane should revert to, which is a change to the
            // install path rather than a guard on it (deferred, terrain-evidence ledger D2). What is
            // fixed is the log lying about it: the state is named, once, before the exception carries
            // on to the caller that reports the failed start.
            try
            {
                GameManager.Instance.StreamingWorld.TerrainScale = 1f;

                ModMessageHandler.Init();
            }
            catch (System.Exception)
            {
                Debug.LogError("[WoDTerrain] start-up failed AFTER the GPU terrain sampler was "
                    + "installed - the terrain in this session is this port's, but TerrainScale and "
                    + "the mod-message handler were not set up. Restart with the entry off if the "
                    + "terrain looks wrong.");
                throw;
            }

            // MOBILE: ConsoleHandler.RegisterConsoleCommands() removed - dev console commands not shipped.

            // MOBILE: (T5-1) last line, after AddComponent's Awake has replaced
            // MOBILE: DaggerfallUnity.TerrainSampler and after the message handler is listening. Every
            // MOBILE: earlier return leaves this false, which is what the launcher's log line reads.
            Installed = true;
        }

        /// <summary>
        /// MOBILE: (g) split out of LoadAssetsAndParams so the bundle contents are part of the
        /// availability gate. Returns false, having logged the reason, if anything is missing.
        /// </summary>
        static bool TryLoadBundleAssets(out TextAsset paramIni)
        {
            paramIni = null;

            if (!Mod.LoadAllAssetsFromBundle())
            {
                Debug.LogWarning("[WoDTerrain] not available: the wod-terrain bundle could not be loaded");
                ReleaseAssets();    // MOBILE: (M1)
                return false;
            }

            biomeMap = Mod.GetAsset<Texture2D>("daggerfall_heightmap");
            derivMap = Mod.GetAsset<Texture2D>("daggerfall_deriv_map");
            portMap = Mod.GetAsset<Texture2D>("daggerfall_port_map");
            roadMap = Mod.GetAsset<Texture2D>("daggerfall_road_map");
            tileableNoise = Mod.GetAsset<Texture2D>("tileable_noise");
            paramIni = Mod.GetAsset<TextAsset>("interesting_terrains");

            string missing = null;
            if (biomeMap == null) missing = "daggerfall_heightmap";
            else if (derivMap == null) missing = "daggerfall_deriv_map";
            else if (portMap == null) missing = "daggerfall_port_map";
            else if (roadMap == null) missing = "daggerfall_road_map";
            else if (tileableNoise == null) missing = "tileable_noise";
            else if (paramIni == null) missing = "interesting_terrains";

            if (missing != null)
            {
                Debug.LogWarning("[WoDTerrain] not available: " + missing + " is missing from the wod-terrain bundle");
                // MOBILE: (M1) whatever DID load - up to four 2048x1024 RGBA32 world maps - is dropped
                // MOBILE: here rather than pinned by statics for the rest of the session.
                paramIni = null;
                ReleaseAssets();
                return false;
            }

            return true;
        }

        /// <summary>
        /// MOBILE: (M2) what remains of LoadAssetsAndParams, moved AHEAD of the sampler swap. Upstream -
        /// and this port before review - parsed the INI and generated the world heightmap after
        /// AddComponent had already run Awake, and Awake is what replaces DaggerfallUnity.TerrainSampler.
        /// A malformed INI, or a failure in the banded dispatch, therefore left the GPU sampler installed
        /// with a null or half-populated csParams: garbage terrain instead of vanilla terrain, the exact
        /// failure the capability gate exists to prevent, one step later. Now a throw here logs
        /// "[WoDTerrain] not available: ..." and returns having put WOODS.WLD back and dropped every
        /// asset, with DFU's own sampler never touched.
        ///
        /// Not pure and not exercisable headlessly - it dispatches a compute shader and rewrites the
        /// world heightmap. The one decision inside it that IS pure, ShouldRestoreWoodsBuffer, is pinned
        /// by the self test; the rest is runtime-only and is what Task 8's simulator run judges.
        /// </summary>
        private static bool TryPrepareWorld(TextAsset paramIni)
        {
            try
            {
#if UNITY_EDITOR
                preparedParams = ScriptableObject.CreateInstance<TerrainComputerParams>();
#else
                preparedParams = new TerrainComputerParams();
#endif

                var ini = new IniParser.Parser.IniDataParser().Parse(paramIni.text);
                preparedParams.FromIniData(ini);

                TerrainComputer.InitializeWoodsFileHeightmap(preparedParams);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WoDTerrain] not available: " + ex);
                RestoreWoodsFileBuffer();
                ReleaseAssets();
                return false;
            }
        }

        /// <summary>
        /// MOBILE: (M2) the pure half of the WOODS.WLD restore, so the rule is pinned by the self test
        /// rather than only by a device run. InitializeWoodsFileHeightmap copies the reader's original
        /// buffer first and swaps in the generated one last, so a throw in between can leave the reader
        /// holding either. Restore whenever a copy of the original exists and the reader is not already
        /// holding it - and never when there is no copy, because then nothing was replaced.
        /// </summary>
        public static bool ShouldRestoreWoodsBuffer(byte[] originalCopy, byte[] currentBuffer)
        {
            return originalCopy != null && !ReferenceEquals(originalCopy, currentBuffer);
        }

        // MOBILE: (M2) puts the travel map's world heightmap back the way it was found and drops the
        // MOBILE: generated basemap, the two ComputeShader clones and the location buffer, so a refused
        // MOBILE: start leaves nothing behind for the rest of the session.
        private static void RestoreWoodsFileBuffer()
        {
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            var reader = dfUnity != null ? dfUnity.ContentReader : null;
            var woodsFile = reader != null ? reader.WoodsFileReader : null;

            if (woodsFile != null && ShouldRestoreWoodsBuffer(TerrainComputer.originalHeightmapBuffer, woodsFile.Buffer))
                woodsFile.Buffer = TerrainComputer.originalHeightmapBuffer;

            TerrainComputer.originalHeightmapBuffer = null;
            TerrainComputer.alteredHeightmapBuffer = null;

            if (TerrainComputer.baseHeightmap != null)
            {
                UnityEngine.Object.Destroy(TerrainComputer.baseHeightmap);
                TerrainComputer.baseHeightmap = null;
            }

            TerrainComputer.Cleanup();
        }

        /// <summary>
        /// MOBILE: (M1) a refused start drops every reference it took, so the four 2048x1024 RGBA32
        /// world maps (~34 MB GPU with mips off - the LinearData importer rule turns them off, which is
        /// where the design's ~43 MB estimate went) and the two compute shader assets are collectable instead of pinned
        /// by statics for the life of the session - on the one device where the gate actually fires,
        /// which is the device that could least afford it. DFU's own Mod.loadedAssets cache still holds
        /// its entries (private, stamped -1 = never pruned, no public clear), so full reclamation would
        /// need a DFU engine change; this port deliberately makes none. What can be dropped here, is.
        /// Safe to call at any time, including before Init has ever run.
        /// </summary>
        public static void ReleaseAssets()
        {
            biomeMap = null;
            derivMap = null;
            portMap = null;
            roadMap = null;
            tileableNoise = null;
            csPrototype = null;
            mainHeightComputer = null;
            preparedParams = null;
        }
        #endregion

        private void Awake()
        {
            // MOBILE: (M2) the parameters are already parsed and the world heightmap already generated
            // MOBILE: by the time this runs - see TryPrepareWorld. Taking them before the sampler swap
            // MOBILE: means there is no instant, however brief, at which the GPU sampler is installed
            // MOBILE: without them.
            csParams = preparedParams;
            preparedParams = null;

            DaggerfallUnity.Instance.TerrainSampler = new InterestingTerrainSampler();

            //DaggerfallUnity.Instance.TerrainTexturing = new WOTerrainTexturing();

            DaggerfallTerrain.OnPromoteTerrainData += tileDataCache.UncacheTileData;

            Mod.IsReady = true;

            // MOBILE: (M2) guarded rather than dereferenced. This is the last statement of an Awake that
            // MOBILE: runs synchronously inside AddComponent, i.e. AFTER the sampler has been replaced
            // MOBILE: and OnPromoteTerrainData hooked - so an NRE here would land in exactly the
            // MOBILE: half-installed state the rest of this port is built to make impossible, and (if
            // MOBILE: Unity propagated it out of AddComponent) would also skip TerrainScale = 1f and
            // MOBILE: report "start failed" with a live GPU sampler at scale 1.5. Camera.main is very
            // MOBILE: probably non-null here - the MainCamera-tagged camera is an active child of the
            // MOBILE: PlayerAdvanced prefab from scene load - but this file's neighbour is the
            // MOBILE: counter-example: Dynamic Skies had to be deferred out of StartEnabled entirely
            // MOBILE: because at this same state "on device one of them comes back null"
            // MOBILE: (Ports/DynamicSkies/BLBSkybox.cs:120-127). The write is load-bearing, not
            // MOBILE: cosmetic: the prefab's far clip is 2600 (PlayerAdvanced.prefab:750) and this
            // MOBILE: sampler's relief goes to 5000, so without it distant terrain is clipped away.
            var cam = Camera.main;
            if (cam != null)
                cam.farClipPlane = 10000f;
            else
                Debug.LogWarning("[WoDTerrain] no main camera at start-up; far clip plane left at its default - distant terrain will be clipped");
        }

        private void Start()
        {
            // MOBILE: (R1) unconditional now. This call used to sit behind
            // CompatibilityUtils.BasicRoadsLoaded, which asks ModManager for a mod titled "BasicRoads"
            // - never true on iOS, where Basic Roads is compiled in - so Init never ran and the road
            // smoothing was dead code behind a test that could not pass on this platform. Init makes
            // the decision itself, over the compiled-in network, and logs which source it took. The
            // self test reads this file for the gate's absence, so do not quote it back in a comment.
            BasicRoadsUtils.Init();

            //DaggerfallUnity.Instance.TerrainTexturing = new WildernessOverhaul.WOTerrainTexturing(true, true);
        }

        private void OnDestroy()
        {
            TerrainComputer.Cleanup();
        }

        // MOBILE: ClearNoonRoutine removed - it existed only for the dropped "clearnoon" console command.
    }
}
