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
                csPrototype = null;
                mainHeightComputer = null;
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

            var go = new GameObject(Mod.Title);
            instance = go.AddComponent<InterestingTerrains>();

            GameManager.Instance.StreamingWorld.TerrainScale = 1f;

            ApplyParams(paramIni);

            ModMessageHandler.Init();

            // MOBILE: ConsoleHandler.RegisterConsoleCommands() removed - dev console commands not shipped.
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
                return false;
            }

            return true;
        }

        // MOBILE: what remains of LoadAssetsAndParams once the asset loading moved into the gate above.
        private static void ApplyParams(TextAsset paramIni)
        {
#if UNITY_EDITOR
            instance.csParams = ScriptableObject.CreateInstance<TerrainComputerParams>();
#else
            instance.csParams = new TerrainComputerParams();
#endif

            var ini = new IniParser.Parser.IniDataParser().Parse(paramIni.text);
            instance.csParams.FromIniData(ini);

            TerrainComputer.InitializeWoodsFileHeightmap();
        }
        #endregion

        private void Awake()
        {
            DaggerfallUnity.Instance.TerrainSampler = new InterestingTerrainSampler();

            //DaggerfallUnity.Instance.TerrainTexturing = new WOTerrainTexturing();

            DaggerfallTerrain.OnPromoteTerrainData += tileDataCache.UncacheTileData;

            Mod.IsReady = true;
            Camera.main.farClipPlane = 10000f;
        }

        private void Start()
        {
            if (CompatibilityUtils.BasicRoadsLoaded)
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
