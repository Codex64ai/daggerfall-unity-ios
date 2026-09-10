// MOBILE PORT - source: github.com/TheLacus/daggerfall-unity-mods @ 556ef6e1dd0f2da95aa34275a30861daf58fee86
// File RealGrass/Scripts/RealGrass.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (RealGrass/LICENSE): MIT, Copyright (c) 2016-2019 Uncanny_Valley, TheLacus.
//
// MOBILE: the mod is compiled into the app and started by MobilePortedMods, not by DFU's [Invoke]
// loader, and it runs in ONE configuration - the cheap one: Classic style, billboard prototypes,
// no stones, no water plants, no fireflies, detail resolution 128 at 16 per patch (DFU's own patch
// density, four times DFU's detail resolution) instead of upstream's 256/8. See RealGrassPort.
//
// MOBILE: there is no modsettings.json in the iOS bundle (it ships the two Classic textures and
// nothing else), so mod.GetSettings() has nothing to read and the settings callback is gone. Every
// value below is a code constant; the two dials a player would plausibly want are the public
// static fields RealGrassPort.DetailDistance / .DetailDensity, and a settings hook can be added
// later without touching anything else.

// Project:         Real Grass for Daggerfall Unity
// Web Site:        http://forums.dfworkshop.net/viewtopic.php?f=14&t=17
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/TheLacus/realgrass-du-mod
// Original Author: Uncanny_Valley (original Real Grass)
// Contributors:    TheLacus (mod version, additional terrain details) 
//                  Midopa

// #define TEST_PERFORMANCE

using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections;
using System.Collections.Generic;   // MOBILE: the once-per-message log set
using System.Linq;
using UnityEngine;
using Climates = DaggerfallConnect.Arena2.MapsFile.Climates;

namespace RealGrass
{
    [Flags]
    public enum GrassStyle
    {
        Classic = 0,
        Mixed = 1,
        Full = 1 | 2,
    }

    public static class DaysOfYear
    {
        public const int

            GrowDay = 1 * 30 + 15,
            Spring = 2 * 30 + 1,
            Summer = 5 * 30 + 1,
            MidYear = 6 * 30 + 15,
            Fall = 8 * 30 + 1,
            Winter = 11 * 30 + 1,
            DieDay = 12 * 30 - 15;
    }

    public class RealGrassOptions
    {
        internal GrassStyle GrassStyle { get; set; }
        public bool WaterPlants { get; set; }
        public bool TerrainStones { get; set; }
        public bool FlyingInsects { get; set; }
        public float DetailObjectDistance { get; set; }
        public float DetailObjectDensity { get; set; }
    }

    /// <summary>
    /// MOBILE: upstream's static <c>RealGrass.Init</c>, lifted out of the MonoBehaviour into a
    /// loader of its own with the <c>[Invoke(StateManager.StateTypes.Start, 0)]</c> attribute
    /// REMOVED - MobilePortedMods calls this directly when the launcher entry is on, exactly as
    /// DFU's mod loader would have. Nothing here runs otherwise.
    ///
    /// It also owns the forced configuration. Upstream read all of it from modsettings.json; this
    /// bundle has no settings file, and the shipped defaults were the worst possible ones for a
    /// phone anyway (Full style, mesh prototypes on the Standard shader, stones, water plants,
    /// 120 m detail distance, density 1.0, detail resolution 256 at 8 per patch = 1,024 detail
    /// patches per terrain against DFU's own ~65).
    /// </summary>
    public static class RealGrassPort
    {
        #region The forced configuration

        /// <summary>
        /// MOBILE: upstream's <c>SetDetailResolution(256, 8)</c> becomes (128, 16). 16 per patch is
        /// DFU's own patch density (DaggerfallTerrain.resolutionPerPatch) and Unity's recommended
        /// value; 128 is still four times DFU's detail resolution. Together they quarter the
        /// resident detail data and cut detail patches per terrain from 1,024 to 64.
        /// </summary>
        public const int DetailResolution = 128;

        /// <summary>MOBILE: see DetailResolution. Upstream shipped 8 - half of Unity's advice.</summary>
        public const int DetailResolutionPerPatch = 16;

        /// <summary>
        /// MOBILE: forced. Classic is both the cheap path (one grass layer, no flower/accent
        /// layers) and the licence-clean one (the Mixed and Full textures are VMblast's, not
        /// shipped). Upstream's default was Full.
        /// </summary>
        public const GrassStyle ForcedStyle = GrassStyle.Classic;

        /// <summary>
        /// MOBILE: forced. Billboard means DetailRenderMode.GrassBillboard fed by a texture -
        /// Unity's cheapest detail path. Upstream's default (false) renders FBX prototypes with
        /// their own Standard-shader materials, which on a phone backbuffer with alpha-tested
        /// overdraw is the single most expensive thing this mod can do.
        /// </summary>
        public const bool ForcedBillboard = true;

        /// <summary>MOBILE: forced off - a whole extra detail layer, and the 13.7 MB rock textures.</summary>
        public const bool ForcedTerrainStones = false;

        /// <summary>MOBILE: forced off - another detail layer, and mesh prototypes from the bundle.</summary>
        public const bool ForcedWaterPlants = false;

        /// <summary>MOBILE: forced off - a particle system per night-time water tile.</summary>
        public const bool ForcedFlyingInsects = false;

        /// <summary>
        /// MOBILE: forced off, which is also upstream's default. With it on, every texture load
        /// first stats a loose file under Textures/Grass/ - on iOS a Documents-folder round trip
        /// inside terrain promotion.
        /// </summary>
        public const bool ForcedTextureOverride = false;

        /// <summary>MOBILE: 40 m, not upstream's 120. The detail draw radius, in metres.</summary>
        public const float DefaultDetailDistance = 40f;

        /// <summary>MOBILE: 0.6, not upstream's 1.0.</summary>
        public const float DefaultDetailDensity = 0.6f;

        /// <summary>
        /// MOBILE: the two dials, as fields rather than constants. There is no modsettings.json in
        /// the bundle, so nothing reads them from a file today; they are public and static so a
        /// settings hook (or a console command, or the launcher) can move them later without
        /// touching the port. Read by Init, so a change takes effect on the next start.
        /// </summary>
        public static float DetailDistance = DefaultDetailDistance;

        /// <summary>MOBILE: see DetailDistance.</summary>
        public static float DetailDensity = DefaultDetailDensity;

        #endregion

        #region Pins and gates

        /// <summary>
        /// MOBILE: Unity's built-in terrain detail shaders, verified by name against
        /// 6000.3.23f1's unity_builtin_extra. The mod has no shaders of its own - it renders
        /// through Unity's stock terrain detail path, and an iOS player build is free to strip
        /// these because nothing in the project references them (DFU creates every Terrain at
        /// runtime and no scene holds one). They are pinned into GraphicsSettings' Always
        /// Included Shaders list by MobileBuildSetup.EnsureAlwaysIncludedShaders; Init checks
        /// them at runtime so a build that lost them says so instead of drawing nothing.
        /// </summary>
        public static readonly string[] ShaderNames =
        {
            "Hidden/TerrainEngine/Details/Vertexlit",
            "Hidden/TerrainEngine/Details/WavingDoublePass",
            "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
        };

        /// <summary>
        /// MOBILE: the two Classic billboard textures the bundle ships. BrownGrass_tex is used by
        /// Mountain, Swamp and (see DesertGrassTexture) Desert; GreenGrass_tex by Temperate.
        /// Both must be there or the gate refuses: a null prototypeTexture draws nothing.
        /// </summary>
        public static readonly string[] GrassTextureNames = { "BrownGrass_tex", "GreenGrass_tex" };

        /// <summary>
        /// MOBILE: did Init get past the gate and subscribe to DaggerfallTerrain.OnPromoteTerrainData?
        /// This is the flag MobilePortedMods' four-argument StartOne asks, because Init declines by
        /// logging "[RealGrass] not available: ..." and returning rather than by throwing.
        /// </summary>
        public static bool Installed { get; private set; }

        /// <summary>
        /// MOBILE pure: nothing is created unless Unity's three detail shaders resolved AND the
        /// bundle's Classic grass textures are there. Either half missing means grass that renders
        /// as nothing at all, with no error - which is the failure this gate exists to name.
        /// </summary>
        public static bool Available(bool shadersFound, bool grassTexturePresent)
        {
            return shadersFound && grassTexturePresent;
        }

        #endregion

        #region Cost arithmetic (pure)

        /// <summary>
        /// MOBILE pure: detail patches per terrain, the number Unity culls every frame. Upstream's
        /// (256, 8) is 1,024; this port's (128, 16) is 64, which is DFU's own patch density.
        /// </summary>
        public static int DetailPatchesPerTerrain(int res, int perPatch)
        {
            if (res <= 0 || perPatch <= 0) return 0;
            int perSide = res / perPatch;
            return perSide * perSide;
        }

        /// <summary>
        /// MOBILE pure: resident detail-density data across the live terrains, in MB. TerrainData
        /// keeps one byte per detail cell per layer. Upstream at its defaults - (256, 8), five
        /// layers, 49 live terrains at TerrainDistance 3 - is ~15.3 MB; this port is ~0.77 MB.
        /// </summary>
        public static float DetailDataMegabytes(int res, int layers, int terrains)
        {
            if (res <= 0 || layers <= 0 || terrains <= 0) return 0f;
            return (float)((double)res * res * layers * terrains / (1024.0 * 1024.0));
        }

        /// <summary>
        /// MOBILE pure: live terrains at a given StreamingWorld.TerrainDistance - the ring the
        /// promotion handler is called for on every map-pixel crossing. 3 (the shipped value) is 49.
        /// </summary>
        public static int LiveTerrainCount(int terrainDistance)
        {
            if (terrainDistance < 0) return 0;
            int side = terrainDistance * 2 + 1;
            return side * side;
        }

        /// <summary>
        /// MOBILE: Unity's ceiling on a single detail-map cell, read from
        /// TerrainData.maxDetailScatterPerRes on the first promotion (it depends on the terrain's
        /// detail scatter mode, which this port does not change). The fallback is the coverage-mode
        /// value; nothing here ever gets near it at Classic densities.
        /// </summary>
        public const int FallbackMaxDetailValue = 255;

        /// <summary>MOBILE: see FallbackMaxDetailValue.</summary>
        public static int MaxDetailValue = FallbackMaxDetailValue;

        /// <summary>
        /// MOBILE pure: the fold. Upstream addressed a 256x256 detail map as (tile * 2 + sub), so
        /// each of the 128x128 tilemap cells owned four detail cells. At detail resolution 128 the
        /// map is 1:1 with the tilemap and those four fold into one, so the values ADD rather than
        /// overwrite - a detail cell now covers four times the ground, and adding is what keeps
        /// grass per square metre the same as upstream instead of quartering it. Clamped because a
        /// detail-map cell cannot hold more than Unity's scatter ceiling.
        /// </summary>
        public static int FoldDetailValue(int existing, int added, int max)
        {
            if (max < 0) max = 0;
            long sum = (long)existing + added;
            if (sum < 0) return 0;
            return sum > max ? max : (int)sum;
        }

        #endregion

        #region Init

        public static Mod mod;

        /// <summary>MOBILE: the mod whose bundle holds the two Classic textures. Null until Init.</summary>
        public static Mod Mod { get { return mod; } }

        static readonly HashSet<string> loggedOnce = new HashSet<string>();

        /// <summary>
        /// MOBILE: log a message the first time only. The promotion handler is called for a ring
        /// of terrains on every map-pixel crossing, so an exception in there would otherwise fill
        /// the log at the frame rate of walking.
        /// </summary>
        internal static void LogOnce(string message)
        {
            if (loggedOnce.Add(message))
                Debug.Log(message);
        }

        internal static void MarkInstalled()
        {
            Installed = true;
        }

        internal static void MarkStopped()
        {
            Installed = false;
        }

        /// <summary>
        /// MOBILE: rename of upstream's <c>RealGrass.Init</c>, with the [Invoke] attribute removed
        /// and an availability gate in front of everything. Upstream created the component
        /// unconditionally and let a stripped shader or a missing texture surface as grass that
        /// simply is not there.
        /// </summary>
        public static void Init(InitParams initParams)
        {
            Installed = false;
            mod = initParams.Mod;

            // MOBILE: Shader.Find, not MobileShaders.Find - these are engine shaders, not the
            // port's own, so there is no captured copy to prefer and nothing for a mod bundle to
            // shadow. All three, because the gate cannot tell which render mode a later climate
            // will ask for and a half-present set is a build problem worth naming.
            string missingShader = null;
            foreach (string name in ShaderNames)
            {
                if (Shader.Find(name) == null)
                    missingShader = missingShader == null ? name : missingShader + ", " + name;
            }

            string missingTexture = null;
            foreach (string name in GrassTextureNames)
            {
                if (mod == null || mod.GetAsset<Texture2D>(name) == null)
                    missingTexture = missingTexture == null ? name : missingTexture + ", " + name;
            }

            if (!Available(missingShader == null, missingTexture == null))
            {
                string reason = missingShader != null
                    ? "Unity terrain detail shaders not in this build (" + missingShader + ")"
                    : "the bundle has no Classic grass texture (" + missingTexture + ")";
                Debug.Log("[RealGrass] not available: " + reason);
                return;
            }

            GameObject go = new GameObject(mod != null && !string.IsNullOrEmpty(mod.Title) ? mod.Title : "Real Grass");
            RealGrass component = go.AddComponent<RealGrass>();

            // MOBILE: started HERE rather than from the component's Start(). MobilePortedMods asks
            // Installed the moment Init returns, and Start() does not run until the next frame -
            // which would always read "did not start". AddComponent has already run Awake().
            component.StartPort();
        }

        #endregion
    }

    /// <summary>
    /// Places grass and other details on Daggerall Unity terrain.
    /// </summary>
    public class RealGrass : MonoBehaviour
    {
        #region Fields

        internal const string TexturesFolder = "Grass";

        // MOBILE: upstream kept its own static `mod`; there is one copy now, on RealGrassPort.
        private static Mod mod { get { return RealGrassPort.mod; } }

        private readonly RealGrassOptions options = new RealGrassOptions();
        DetailPrototypesManager detailPrototypesManager;
        DensityManager densityManager;
        bool isEnabled;
        Coroutine initTerrains;

        // MOBILE: the two log lines the simulator and device runs are read for. `promotions` counts
        // handled promotions (not terrains alive), so the counter line is a heartbeat that also
        // says the handler is still being reached; the memory line is said once, on the first
        // promotion, because only then are the layer count and the terrain ring both known.
        int promotions;
        bool memoryLogged;

        // MOBILE: every 25th promotion. At TerrainDistance 3 a map-pixel crossing promotes a ring
        // of about 7 terrains, so this is roughly one line per four crossings.
        public const int CounterInterval = 25;

        #endregion

        #region MOBILE: the forced configuration

        /// <summary>
        /// MOBILE: upstream's LoadSettings, with the settings reads replaced by the values
        /// modsettings.json shipped for everything this port keeps (grass size, noise spread,
        /// seasonal colours, thick/thin density) and by RealGrassPort's constants for everything it
        /// forces. There is no modsettings.json in the iOS bundle, so mod.LoadSettings() /
        /// mod.GetSettings() would have nothing to read; nothing here can throw.
        /// </summary>
        void ApplyForcedSettings()
        {
            options.GrassStyle = RealGrassPort.ForcedStyle;
            options.WaterPlants = RealGrassPort.ForcedWaterPlants;
            options.TerrainStones = RealGrassPort.ForcedTerrainStones;
            options.FlyingInsects = RealGrassPort.ForcedFlyingInsects;
            options.DetailObjectDistance = RealGrassPort.DetailDistance;
            options.DetailObjectDensity = RealGrassPort.DetailDensity;

            var properties = new PrototypesProperties()
            {
                // Upstream defaults: Grass/Height (0.65, 1.05), Grass/Width (0.8, 1.0),
                // Grass/NoiseSpread 0.4, Grass/SeasonInterpolation false.
                GrassHeight = new Range<float>(0.65f, 1.05f),
                GrassWidth = new Range<float>(0.8f, 1.0f),
                NoiseSpread = 0.4f,
                GrassColors = new GrassColors()
                {
                    SpringHealthy = new Color32(130, 206, 112, 255),
                    SpringDry = new Color32(137, 180, 75, 255),
                    SummerHealty = new Color32(163, 204, 162, 255),
                    SummerDry = new Color32(128, 125, 104, 255),
                    FallHealty = new Color32(166, 116, 43, 255),
                    FallDry = new Color32(105, 51, 29, 255),
                    SeasonInterpolation = false
                },
                // MOBILE: the billboard path. Upstream computed this as !settings Style/Billboard.
                UseGrassShader = !RealGrassPort.ForcedBillboard,
                TextureOverride = RealGrassPort.ForcedTextureOverride
            };

            var density = new Density()
            {
                // Upstream defaults: Grass/ThickDensity (6, 20), Grass/ThinDensity (2, 9).
                GrassThick = new Range<int>(6, 20),
                GrassThin = new Range<int>(2, 9),
                // MOBILE: both features are off, so these are never read. Kept non-null because a
                // Range<int> field the disabled branches would touch is worse than a dead value.
                WaterPlants = new Range<int>(1, 5),
                DesertPlants = new Range<int>(4, 8),
                Rocks = 0,
            };

            detailPrototypesManager = new DetailPrototypesManager(mod, transform, options, properties);
            densityManager = new DensityManager(mod, options, density);
        }

        /// <summary>
        /// MOBILE: what upstream's Start() did, called straight from RealGrassPort.Init so that
        /// Installed is settled before Init returns. No console commands (the External/
        /// RealGrassConsoleCommands.cs file is not part of this port - Wenzil.Console commands
        /// have no place on a touch build).
        /// </summary>
        internal void StartPort()
        {
            ApplyForcedSettings();
            StartMod(false, false);
            isEnabled = true;

            if (mod != null)
            {
                mod.MessageReceiver = MessageReceiver;
                mod.IsReady = true;
            }
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            if (mod == null)
                return base.ToString();

            return string.Format("{0} v.{1}", mod.Title, mod.ModInfo.ModVersion);
        }

        /// <summary>
        /// Toggle mod and add/remove grass on existing terrains.
        /// </summary>
        /// <returns>New status of mod.</returns>
        public bool ToggleMod()
        {
            ToggleMod(!isEnabled);
            return isEnabled;
        }

        /// <summary>
        /// Set status of mod and add/remove grass on existing terrains.
        /// </summary>
        /// <param name="enable">New status to set.</param>
        public void ToggleMod(bool enable)
        {
            if (isEnabled == enable)
                return;

            if (enable)
                StartMod(false, true);
            else
                StopMod();

            isEnabled = enable;
        }

        /// <summary>
        /// Restart mod to apply changes.
        /// </summary>
        public void RestartMod()
        {
            if (enabled)
                StopMod();

            StartMod(false, true);

            isEnabled = true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Add Grass and other details on terrain.
        /// </summary>
        private void AddTerrainDetails(DaggerfallTerrain daggerTerrain, TerrainData terrainData)
        {
#if TEST_PERFORMANCE

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

#endif

            UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY));

            // Terrain settings
            // MOBILE: (128, 16), not upstream's (256, 8) - and only when the store is not already
            // that shape. SetDetailResolution REALLOCATES the terrain's detail store and clears
            // every layer; StreamingWorld pools and reuses TerrainData, so upstream paid for that
            // reallocation on every promotion of every recycled terrain. DFU creates its own store
            // at (heightmapDimension, 16), so this still fires once per TerrainData, then never.
            if (terrainData.detailWidth != RealGrassPort.DetailResolution
                || terrainData.detailHeight != RealGrassPort.DetailResolution
                || terrainData.detailResolutionPerPatch != RealGrassPort.DetailResolutionPerPatch)
            {
                terrainData.SetDetailResolution(RealGrassPort.DetailResolution, RealGrassPort.DetailResolutionPerPatch);
            }
            // MOBILE: Unity's ceiling for one detail-map cell, which DensityManager's fold clamps
            // to. It depends on the terrain's detail scatter mode, which this port does not change.
            RealGrassPort.MaxDetailValue = terrainData.maxDetailScatterPerRes > 0
                ? terrainData.maxDetailScatterPerRes
                : RealGrassPort.FallbackMaxDetailValue;
            terrainData.wavingGrassTint = Color.gray;
            Terrain terrain = daggerTerrain.gameObject.GetComponent<Terrain>();
            terrain.detailObjectDistance = options.DetailObjectDistance;
            terrain.detailObjectDensity = options.DetailObjectDensity;

            // Get the current season and climate
            var currentSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
            ClimateBases climate = GetClimate(daggerTerrain.MapData.worldClimate);

            // Update detail layers
            Color32[] tilemap = daggerTerrain.TileMap;
            densityManager.InitDetailsLayers();
            switch (climate)
            {
                case ClimateBases.Temperate:
                case ClimateBases.Mountain:
                case ClimateBases.Swamp:
                    if (currentSeason != DaggerfallDateTime.Seasons.Winter)
                    {
                        detailPrototypesManager.UpdateClimateSummer(climate);
                        densityManager.SetDensitySummer(terrain, tilemap, climate);
                    }
                    else
                    {
                        detailPrototypesManager.UpdateClimateWinter(climate);
                        densityManager.SetDensityWinter(terrain, tilemap, climate);
                    }
                    break;

                case ClimateBases.Desert:
                    detailPrototypesManager.UpdateClimateDesert();
                    densityManager.SetDensityDesert(tilemap);
                    break;
            }

            // Assign detail prototypes to the terrain
            terrainData.detailPrototypes = detailPrototypesManager.DetailPrototypes;

            // Assign detail layers to the terrain
            // MOBILE: .Cells - the layers are DetailMap now (a cached int[128,128] addressed in
            // upstream's 256-space), not a fresh int[256,256] per layer per promotion.
            terrainData.SetDetailLayer(0, 0, detailPrototypesManager.Grass, densityManager.Grass.Cells);
            if ((options.GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed && climate != ClimateBases.Desert)
            {
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.GrassDetails, densityManager.GrassDetails.Cells);
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.GrassAccents, densityManager.GrassAccents.Cells);
            }
            if (options.WaterPlants)
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.WaterPlants, densityManager.WaterPlants.Cells);
            if (options.TerrainStones)
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.Rocks, densityManager.Rocks.Cells);

            // MOBILE: the two lines a Player.log is read for.
            promotions++;
            if (!memoryLogged)
            {
                memoryLogged = true;
                int layers = detailPrototypesManager.DetailPrototypes != null ? detailPrototypesManager.DetailPrototypes.Length : 0;
                int terrains = RealGrassPort.LiveTerrainCount(TerrainDistance());
                Debug.Log(string.Format("[RealGrass] detail data ~{0:0.0} MB (res {1}, layers {2}, terrains {3})",
                    RealGrassPort.DetailDataMegabytes(RealGrassPort.DetailResolution, layers, terrains),
                    RealGrassPort.DetailResolution, layers, terrains));
            }
            if (promotions % CounterInterval == 0)
                Debug.Log("[RealGrass] details on " + promotions + " terrains");

#if TEST_PERFORMANCE

            stopwatch.Stop();
            Debug.LogFormat("RealGrass - Time elapsed: {0} ms.", stopwatch.Elapsed.Milliseconds);

#endif
        }

        /// <summary>
        /// MOBILE: StreamingWorld's terrain ring radius, or DFU's shipped 3 when the world is not
        /// there yet. Only the memory line reads it.
        /// </summary>
        static int TerrainDistance()
        {
            StreamingWorld world = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            return world != null ? world.TerrainDistance : 3;
        }

        /// <summary>
        /// Start mod and optionally add grass to existing terrains.
        /// </summary>
        /// <param name="loadSettings">Load user settings.</param>
        /// <param name="initTerrains">Add Grass to existing terrains (unnecessary at startup).</param>
        private void StartMod(bool loadSettings, bool initTerrains)
        {
            // MOBILE: the settings load is gone with modsettings.json - see ApplyForcedSettings.
            // The parameter is kept so the call sites read as upstream's do.
            if (loadSettings)
                ApplyForcedSettings();

            // Subscribe to events
            DaggerfallTerrain.OnPromoteTerrainData += DaggerfallTerrain_OnPromoteTerrainData;

            // Place details on existing terrains
            if (initTerrains)
                RefreshTerrainDetailsAsync();

            // MOBILE: the flag MobilePortedMods asks, set the moment the subscription exists.
            RealGrassPort.MarkInstalled();

            Debug.Log("[RealGrass] enabled; subscribed to terrain promotion");
        }

        /// <summary>
        /// Stop mod and remove grass fom existing terrains.
        /// </summary>
        private void StopMod()
        {
            // Unsubscribe from events
            DaggerfallTerrain.OnPromoteTerrainData -= DaggerfallTerrain_OnPromoteTerrainData;
            RealGrassPort.MarkStopped();   // MOBILE: symmetric with StartMod

            // Remove details from terrains
            // MOBILE: guarded. Upstream reached straight into StreamingTarget, which is null until
            // world entry - and this can now be reached from a mod message at the title screen.
            StreamingWorld streamingWorld = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            if (streamingWorld != null && streamingWorld.StreamingTarget != null)
            {
                Terrain[] terrains = streamingWorld.StreamingTarget.GetComponentsInChildren<Terrain>();
                foreach (TerrainData terrainData in terrains.Select(x => x.terrainData))
                {
                    foreach (var layer in terrainData.GetSupportedLayers(0, 0, terrainData.detailWidth, terrainData.detailHeight))
                        terrainData.SetDetailLayer(0, 0, layer, DensityManager.Empty);
                    terrainData.detailPrototypes = null;
                }
            }

            Debug.Log("[RealGrass] disabled; unsubscribed from terrain promotion");
        }

        /// <summary>
        /// Re-applies detail layers to terrains.
        /// </summary>
        private void RefreshTerrainDetailsAsync()
        {
            if (initTerrains != null)
                StopCoroutine(initTerrains);

            initTerrains = StartCoroutine(RefreshTerrainDetailsCoroutine());
        }

        private IEnumerator RefreshTerrainDetailsCoroutine()
        {
            // MOBILE: guarded, for the same reason StopMod is - and each terrain is wrapped, so one
            // bad terrain does not abandon the rest of the ring.
            StreamingWorld streamingWorld = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            if (streamingWorld == null || streamingWorld.StreamingTarget == null || streamingWorld.PlayerTerrainTransform == null)
            {
                initTerrains = null;
                yield break;
            }

            // Do player terrain first
            var playerTerrain = streamingWorld.PlayerTerrainTransform.GetComponentInChildren<DaggerfallTerrain>();
            if (playerTerrain != null)
            {
                AddTerrainDetailsContained(playerTerrain);
                yield return null;
            }

            // Do other terrains
            var terrains = streamingWorld.StreamingTarget.GetComponentsInChildren<DaggerfallTerrain>();
            foreach (DaggerfallTerrain daggerTerrain in terrains.Where(x => x != playerTerrain))
            {
                AddTerrainDetailsContained(daggerTerrain);
                yield return null;
            }

            initTerrains = null;
        }

        private static ClimateBases GetClimate(int climateIndex)
        {
            switch ((Climates)climateIndex)
            {
                case Climates.Swamp:
                case Climates.Rainforest:
                    return ClimateBases.Swamp;

                case Climates.Desert:
                case Climates.Desert2:
                case Climates.Subtropical:
                    return ClimateBases.Desert;

                case Climates.Mountain:
                case Climates.MountainWoods:
                    return ClimateBases.Mountain;

                case Climates.Woodlands:
                case Climates.HauntedWoodlands:
                    return ClimateBases.Temperate;

                case Climates.Ocean:
                default:
                    return (ClimateBases)(-1);
            }
        }

        private void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            switch (message)
            {
                case "toggle":
                    if (data is bool)
                        ToggleMod((bool)data);
                    break;

                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        /// <summary>
        /// MOBILE: the containment. OnPromoteTerrainData fires for a ring of terrains on every
        /// map-pixel crossing - the most latency-sensitive path in the port - and a throw in here
        /// used to propagate into DaggerfallTerrain.PromoteTerrainData, i.e. into DFU's own world
        /// streaming. The terrain keeps DFU's empty detail layers instead, and the message is
        /// logged once however many times it happens.
        /// </summary>
        private void AddTerrainDetailsContained(DaggerfallTerrain sender)
        {
            try
            {
                Terrain terrain = sender.gameObject.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null)
                    return;
                AddTerrainDetails(sender, terrain.terrainData);
            }
            catch (Exception ex)
            {
                RealGrassPort.LogOnce("[RealGrass] terrain details failed: " + ex.Message);
            }
        }

        #endregion

        #region Event Handlers

        private void DaggerfallTerrain_OnPromoteTerrainData(DaggerfallTerrain sender, TerrainData terrainData)
        {
            // MOBILE: contained - see AddTerrainDetailsContained. The TerrainData the event hands
            // over is the one to use (the component's own may not be assigned yet on a first
            // promotion), so this path passes it through rather than looking it up.
            try
            {
                AddTerrainDetails(sender, terrainData);
            }
            catch (Exception ex)
            {
                RealGrassPort.LogOnce("[RealGrass] terrain details failed: " + ex.Message);
            }
        }

        #endregion
    }
}
