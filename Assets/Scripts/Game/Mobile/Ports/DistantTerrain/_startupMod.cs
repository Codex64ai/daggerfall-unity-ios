// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File Scripts/_startupMod.cs, copied for iOS except lines marked MOBILE.
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce.
// The World of Daggerfall-flavour additions carry no licence; private draft only.
//
// MOBILE: this port has no .dfmod assembly, so there is no [Invoke] loader: MobilePortedMods calls
// DistantTerrainPort.Init directly when the launcher entry is on. The fly-map, the "13th Passage"
// spell and its console command are dropped with their files, so everything that referenced them
// (the FlyMap creation, the spell registration, the ThirteenthPassage settings section and the
// location-highlight hotkey) is gone from this file.
//
//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//Contributor: MaoDeVaca
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Mobile;                                 // MOBILE: MobileShaders.Find
using DaggerfallWorkshop.Game.Utility.ModSupport;                     //required for modding features
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;         //required for mod settings

namespace DistantTerrain
{
    /// <summary>
    /// Static holder for the three camera-depth values loaded from mod settings.
    /// DistantTerrain.SetUpCameras() reads these every time it runs, so live changes
    /// to the depths take effect on the next transition / save load / retro-mode toggle.
    /// Defaults match the spaced-out "buffer" layout (Skybox=-10, DistantTerrain=2, Main=3)
    /// so other mods can drop their own stacked cameras into one of the free integer depths
    /// without colliding. If LoadCameraStackingSettings() never runs (e.g. missing
    /// modsettings.json), these defaults still preserve the original render order
    /// Skybox -> Distant -> Main.
    /// </summary>
    public static class DistantTerrainCameraConfig
    {
        public static int SkyboxCameraDepth = -10;
        public static int DistantTerrainCameraDepth = 2;
        public static int MainCameraDepth = 3;
    }

    /// <summary>
    /// MOBILE: the two reach dials, settings-backed with iOS defaults (the plan's perf dial).
    /// <para>
    /// BlendEnd is the far (stacked) camera's far clip plane AND the distance at which the
    /// far-terrain shader discards; upstream shipped 120,000, this port halves it to 60,000 so a
    /// phone draws half the world. MainCameraFarClipPlane (15,000) is upstream's own value, made a
    /// setting here so the two can be tuned together on device.
    /// </para>
    /// BlendStart is NOT a separate dial: the shader's fade band is
    /// (_BlendEnd - _BlendStart + 1), so a start above the end inverts the band and fades the whole
    /// far terrain to nothing. It is derived from BlendEnd by DistantTerrainPort.BlendStartFor,
    /// which keeps upstream's 100,000 / 120,000 proportion.
    /// </summary>
    public static class DistantTerrainRenderConfig
    {
        public static float BlendEnd = DistantTerrainPort.DefaultBlendEnd;
        public static float MainCameraFarClipPlane = DistantTerrainPort.DefaultMainCameraFarClipPlane;
    }

    /// <summary>
    /// Static holder for per-weather fog density values loaded from mod settings.
    /// DistantTerrain.Start() pushes these into GameManager.WeatherManager's
    /// FogSettings the moment the world is ready, so any user-tweak takes effect
    /// on the next game launch. Defaults match the values that were previously
    /// hard-coded on DistantTerrain's inspector fields, so a missing or
    /// malformed Fog section gives identical visuals to the pre-settings build.
    /// </summary>
    public static class FogConfig
    {
        public static float SunnyFogDensity = 0.0000f;
        public static float OvercastFogDensity = 0.000075f;
        public static float RainyFogDensity = 0.0001f;
        public static float SnowyFogDensity = 0.00025f;
        public static float HeavyFogDensity = 0.05f;
    }

    /// <summary>
    /// Static holder for the distant-location "beacon" feature. When HighlightLocations
    /// is true, DistantTerrain.InitFarTerrain() writes per-tile location markers into the
    /// B/A channels of the terrain info tilemap, and the shader draws those tiles as
    /// type-coded, softly glowing markers on the LOD mesh so the player can spot distant
    /// cities, dungeons, farms, etc. at a glance. Setting it false skips the HasLocation
    /// lookup entirely (B/A channels stay zero) and the shader sees _HighlightLocations=0
    /// and bypasses the beacon branch.
    ///
    /// MOBILE: upstream had a second, live gate (RuntimeVisible) flipped in game by a KeyCode
    /// read from modsettings.json and polled by the fly-map's Update. Both are dropped with the
    /// fly-map - a phone has no End key - so RuntimeVisible now starts TRUE and the master switch
    /// alone decides. Left in place rather than deleted because DistantTerrain pushes
    /// (HighlightLocations &amp;&amp; RuntimeVisible) as the shader's _HighlightLocations uniform.
    ///
    /// MOBILE: and because RuntimeVisible no longer hides them, the master switch is the whole
    /// gate - so its default is the iOS preset, which is OFF. Upstream shipped the pair as
    /// true/false (baked but hidden until the player pressed End); this port ships false/true
    /// (not baked at all unless asked for). The bundle's modsettings.json still says true, which
    /// is fetched content this port does not patch: DistantTerrainPort.HighlightLocationsFrom
    /// treats that as unset. Only a settings file the player has on disk can turn beacons on.
    /// </summary>
    public static class DistantTerrainLocationConfig
    {
        public static bool HighlightLocations = false;

        // MOBILE: true, not false - with the hotkey gone nothing would ever reveal the markers.
        public static bool RuntimeVisible = true;
    }

    /// <summary>
    /// Static holder for the "basic" rendering mode. Intended for setups where the
    /// "World of Daggerfall" companion mod (which supplies the in-world mountain prefabs
    /// the Mountains*.csv files are keyed to) is NOT active. When EnableBasicMode is true,
    /// DistantTerrain.GenerateWorldTerrain() skips the CSV mountain-prefab lifts entirely
    /// and reverts to the simple water strategy from the reference DistantTerrainBasic.cs:
    /// sub-ocean heights are clamped straight to ocean level and the daggerfall_deriv_map.png
    /// river/coast carving (and its persistent oceanMask) is bypassed. Default is false, so
    /// existing installs running World of Daggerfall keep the current heightmap-baked
    /// mountains and deriv-map water with no change. Read once when the LOD terrain is built
    /// (world entry), so the toggle takes effect on the next world load.
    /// </summary>
    public static class DistantTerrainBasicModeConfig
    {
        public static bool EnableBasicMode = false;
    }

    /// <summary>
    /// Static holder for the optional PER-CLIMATE "disable winter snow" toggles. The far-terrain
    /// shader paints the LOD mesh from four biome tilesets (Desert, Woodland, Mountain, Swamp),
    /// each shared by several Daggerfall climates. In winter the shader normally renders every
    /// climate from its snow-covered winter tileset. When a climate's flag here is true it keeps
    /// the summer look through winter, so that climate never goes white in the distance -
    /// resolved PER FRAGMENT by climate index in the shader (DistantTerrain pushes these as the
    /// _DisableSnow[] uniform), so e.g. Rainforest (jungle) can stay green while true Swamp still
    /// freezes, even though they share the Swamp tileset. Each flag is independent. Default is
    /// all-false. Read live every seasonal-texture update, so changes take effect on the
    /// next in-game season/weather transition.
    ///
    /// Field name -> Daggerfall climate index:
    ///   Desert 224, Desert2 225, Mountain 226, Rainforest 227 (jungle), Swamp 228,
    ///   Subtropical 229, MountainWoods 230, Woodlands 231, HauntedWoodlands 232, Maquis 233.
    /// </summary>
    public static class DistantTerrainWinterSnowConfig
    {
        // Desert (224) / Desert2 (225): intentionally NOT user-exposed in modsettings.json -
        // deserts never show distant snow. Kept as always-false internal defaults so the
        // _DisableSnow[] uniform still has a slot for every climate index. Do not re-add
        // WinterSnow keys for these (the loader no longer reads them).
        public static bool DisableSnowDesert = false;            // 224 (internal default only)
        public static bool DisableSnowDesert2 = false;           // 225 (internal default only)
        public static bool DisableSnowMountain = false;          // 226
        public static bool DisableSnowRainforest = false;        // 227 (jungle)
        public static bool DisableSnowSwamp = false;             // 228
        public static bool DisableSnowSubtropical = false;       // 229
        public static bool DisableSnowMountainWoods = false;     // 230
        public static bool DisableSnowWoodlands = false;         // 231
        public static bool DisableSnowHauntedWoodlands = false;  // 232
        public static bool DisableSnowMaquis = false;            // 233
    }

    /// <summary>
    /// Static holder for the optional PER-CLIMATE "disable winter rivers" toggles. The deriv-map
    /// pass (DistantTerrain.ApplyDerivativeHeightmap) carves the world's water cells - rivers,
    /// coasts and ocean - down to ocean level so the far-terrain shader renders them as water.
    /// When a climate's flag here is true AND the in-game season is winter, water cells whose
    /// climate matches are left as land instead of being carved, so the distant rivers/inland
    /// water in that climate "freeze over" and disappear for the season. Ocean-climate cells
    /// (climate index 223) are never affected, so coastlines and the sea are preserved. Default
    /// is all-false, i.e. identical heightmap to the pre-setting build.
    ///
    /// Field name -> Daggerfall climate index: same mapping as DistantTerrainWinterSnowConfig.
    /// </summary>
    public static class DistantTerrainWinterRiverConfig
    {
        // Desert (224) / Desert2 (225): intentionally NOT user-exposed in modsettings.json.
        // Kept as always-false internal defaults; do not re-add WinterRivers keys for these
        // (the loader no longer reads them).
        public static bool DisableRiversDesert = false;            // 224 (internal default only)
        public static bool DisableRiversDesert2 = false;           // 225 (internal default only)
        public static bool DisableRiversMountain = false;          // 226
        public static bool DisableRiversRainforest = false;        // 227 (jungle)
        public static bool DisableRiversSwamp = false;             // 228
        public static bool DisableRiversSubtropical = false;       // 229
        public static bool DisableRiversMountainWoods = false;     // 230
        public static bool DisableRiversWoodlands = false;         // 231
        public static bool DisableRiversHauntedWoodlands = false;  // 232
        public static bool DisableRiversMaquis = false;            // 233
    }

    /// <summary>
    /// CODE-ONLY per-REGION treatment for the distant (LOD) terrain. The WOD mountains are placed by
    /// region, not biome, so the distant terrain's mountain COLOUR (a tint multiplier, replacing the
    /// old per-biome brightness) and SNOW behaviour are configured per Daggerfall region here. Edit
    /// and recompile to retune; not wired to modsettings.json.
    ///
    /// Each treatment packs an RGB tint (multiplies the region's distant terrain - brown vs stone vs
    /// neutral) and a snow mode:
    ///   0 = ORIGINAL season tileset, no caps (winter = full winter set, summer = summer set).
    ///   1 = snow-CAPPED in winter, BALD in summer.
    ///   2 = COMPLETELY snow-covered in winter, snow-CAPPED in summer.
    /// The snow itself is always drawn white (the mountain winter tileset), only the rock is tinted.
    /// Treatment 0 is the DEFAULT for every region not mapped in TreatmentForRegion below.
    /// DistantTerrain bakes the per-pixel treatment index into the tilemap and pushes this table as
    /// the _RegionData[] uniform (rgb = tint, w = snow mode).
    /// </summary>
    public static class DistantTerrainRegionConfig
    {
        // Treatment table: { tintR, tintG, tintB, snowMode }. Index 0 = default. Keep <= 8 entries
        // (the shader's _RegionData[] is sized 8). Tweak the tints to taste.
        public static readonly float[][] Treatments = new float[][]
        {
            new float[] { 1.00f, 1.00f, 1.00f, 0f }, // 0 default  - neutral, original behaviour (rest of map)
            new float[] { 1.22f, 0.90f, 0.58f, 1f }, // 1 brown    - capped winter / bare summer (CURRENTLY UNUSED: Dragontail/Ephesus moved to treatment 3 for white snow)
            new float[] { 0.92f, 0.95f, 1.00f, 2f }, // 2 stone    - full winter / capped summer
            new float[] { 0.92f, 0.95f, 1.00f, 1f }, // 3 stone    - capped winter / bare summer
        };

        // Daggerfall region name -> treatment index. Unlisted regions fall through to 0 (default).
        public static int TreatmentForRegion(string regionName)
        {
            // If Basic Mode is enabled in settings, enforce default look (0) for all regions
            if (DistantTerrainBasicModeConfig.EnableBasicMode)
            {
                return 0;
            }

            switch (regionName)
            {
                case "Wrothgarian Mountains":
                case "Orsinium Area":
                case "Gavaudon":
                    return 2;                       // stone, full winter / capped summer
                case "Isle of Balfiera":
                case "Dragontail Mountains":
                case "Ephesus":
                    return 3;                       // stone, capped winter / bare summer
                    // Dragontail + Ephesus were treatment 1 (brown) but the strong brown rock tint
                    // bled through the snow-cap transition and made the distant snow read tan; moved
                    // to the Balfiera treatment (near-neutral stone) so the caps read white. To
                    // restore brown mountains here, point them back at treatment 1 (and expect the
                    // tan-snow tradeoff, or soften treatment 1's tint toward neutral).
                default:
                    return 0;                       // rest of map: original look
            }
        }
    }

    /// <summary>
    /// CODE-ONLY tuning hooks for the distant snow caps. Caps are ALTITUDE-based: the shader
    /// (FarTerrainCommon.cginc applySnowCaps) snows terrain above a world-Y snow line, drawn with the
    /// real winter snow tileset. DistantTerrain.PushSnowlineHeights converts the fractions below (of
    /// MaxTerrainHeight) to world-Y each frame so the line tracks the terrain's vertical scale +
    /// floating origin. The PER-REGION snow mode (which seasons get a cap / full snow / nothing) lives
    /// in DistantTerrainRegionConfig.
    /// </summary>
    public static class DistantTerrainSnowCapConfig
    {
        public static bool EnableSnowCaps = true;     // false -> original season-tileset-only snow behaviour

        // Snow line as fractions of MaxTerrainHeight (the same 0..1 space the mountain lifts use).
        // Snow begins at StartFraction and is solid by FullFraction. Raise to push snow higher up the
        // peaks (less snow); lower to bring the snow line down (more snow).
        public static float StartFraction = 0.26f;
        public static float FullFraction  = 0.40f;

        // Random per-pixel deviation of the snow line, as a fraction of MaxTerrainHeight, so the caps
        // don't all sit at exactly the same height (a bit of variety between neighbouring peaks).
        // 0 = a perfectly level line. ~0.03-0.08 reads naturally.
        public static float NoiseFraction = 0.05f;
    }

    /// <summary>
    /// Holder for the user-facing "trees and dirt" toggle. When on, the far-terrain shader draws
    /// the procedural tree specks and the woodland dirt patches; when off, the distant ground
    /// reverts to the plain tileset (no specks, no dirt). Loaded from modsettings.json by
    /// LoadTreesAndDirtSettings() and pushed to the shader as the _EnableTreesAndDirt uniform.
    /// </summary>
    public static class DistantTerrainFeatureConfig
    {
        public static bool EnableTreesAndDirt = true;
    }

    /// <summary>
    /// Static holder for the "Iliac Puddle No More" water mode settings. When IPNMWater is true,
    /// the distant water uses the brightness and opacity values below to match near-water appearance.
    /// Loaded from modsettings.json by LoadIPNMWaterSettings(). The shader reads these as
    /// uniforms (_IPNMWaterBrightness, _IPNMWaterOpacity) and applies them conditionally based
    /// on the _IPNMWater toggle.
    /// </summary>
    public static class IPNMWaterConfig
    {
        public static bool IPNMWater = false;
        public static float IPNMWaterBrightness = 1.0f;
        public static float IPNMWaterOpacity = 1.0f;
    }

    /// <summary>
    /// MOBILE: upstream's `_startupMod` MonoBehaviour, renamed and reduced to a static loader.
    /// There is no [Invoke] attribute and no mod assembly: MobilePortedMods calls Init directly
    /// when the launcher entry is on, exactly as DFU's mod loader would have. Nothing here runs
    /// otherwise.
    /// </summary>
    public static class DistantTerrainPort
    {
        /// <summary>The far-terrain shader, compiled into the app and pinned Always-Included (Task 3).</summary>
        public const string ShaderName = "Daggerfall/DistantTerrain/DistantTerrainTilemap";

        /// <summary>MOBILE: iOS default reach - half of upstream's 120,000 (see DistantTerrainRenderConfig).</summary>
        public const float DefaultBlendEnd = 60000f;

        /// <summary>MOBILE: upstream's own main-camera far clip, now a setting.</summary>
        public const float DefaultMainCameraFarClipPlane = 15000f;

        /// <summary>The three mountain-prefab tables the far heightmap lifts from (bundle TextAssets).</summary>
        public static readonly string[] CsvFileNames = { "Mountains.csv", "Mountains_Small.csv", "Mountains_Foothills.csv" };

        /// <summary>The river/coast mask carved into the far heightmap (bundle Texture2D).</summary>
        public const string DerivMapFileName = "daggerfall_deriv_map.png";

        public static Mod mod;

        /// <summary>MOBILE: the mod whose bundle holds the CSVs and the deriv map. Null until Init.</summary>
        public static Mod Mod { get { return mod; } }

        private static GameObject gameobjectDistantTerrain = null;
        private static DistantTerrain componentDistantTerrain = null;

        private static Shader shaderDistantTerrainTilemap = null;

        /// <summary>
        /// MOBILE: did the far terrain actually get built? False until StreamingWorld.OnReady has run
        /// InitFarTerrain through to its last line at least once - which is much later than Init, and
        /// is the only moment anything is on screen. A failed or refused build tears everything down
        /// and leaves this false. The launcher's "started" line cannot use it (nothing has happened
        /// yet when Init returns); Task 8's docs and the simulator run do.
        /// </summary>
        public static bool Installed { get; private set; }

        /// <summary>
        /// MOBILE: did Init get past the availability gate and put the component in the scene? This is
        /// what MobilePortedMods' 4-arg StartOne asks, and what the sky's deferred start waits on
        /// before looking for the stacked camera. False when the shader or the bundle data is missing.
        /// </summary>
        public static bool Running { get; private set; }

        /// <summary>
        /// MOBILE: the fade band's start, derived from its end rather than configured.
        /// The shader computes fadeRange = _BlendEnd - _BlendStart + 1 and alpha = (_BlendEnd - dist)
        /// / fadeRange, so a start ABOVE the end makes fadeRange negative and every fragment inside
        /// the band comes out at alpha 0 - the whole far terrain disappears. Upstream shipped
        /// 100,000 / 120,000; this keeps that 5:6 proportion for whatever end the settings give.
        /// </summary>
        public static float BlendStartFor(float blendEnd)
        {
            return blendEnd * 5f / 6f;
        }

        /// <summary>MOBILE: set by DistantTerrain.InitFarTerrain, on its last line, once.</summary>
        internal static void MarkInstalled()
        {
            Installed = true;
        }

        /// <summary>
        /// MOBILE: the symmetric counterpart, called from DistantTerrain.TearDownFarTerrain - which
        /// runs both when the build throws and when it refuses. Without it a torn-down far terrain
        /// leaves Running true with no stacked camera in the scene, and the sky's poll (which waits
        /// for that camera whenever Running is true) never releases; Installed likewise kept
        /// claiming a far terrain that had just removed itself.
        /// </summary>
        internal static void MarkStopped()
        {
            Installed = false;
            Running = false;
        }

        /// <summary>
        /// MOBILE pure: the beacons' effective state. The bundle's own modsettings.json ships
        /// HighlightDistantLocations TRUE, and DFU merges the player's settings file on top of that
        /// with no API to ask which of the two a value came from - so the bundle default is treated
        /// as UNSET and only a settings file the player actually has on disk can turn the beacons
        /// on. Everything else lands on the code default, which is the iOS preset: off.
        /// </summary>
        public static bool HighlightLocationsFrom(bool userSettingsFilePresent, bool settingValue)
        {
            return userSettingsFilePresent && settingValue;
        }

        /// <summary>
        /// MOBILE: rename of upstream's `InitStart`, with `[Invoke(StateManager.StateTypes.Start, 0)]`
        /// removed - MobilePortedMods calls this directly. Everything that follows the settings loads
        /// is new: upstream created the component unconditionally and let a missing shader or a
        /// missing CSV surface as a null-reference much later, in the middle of world entry.
        /// </summary>
        public static void Init(InitParams initParams)
        {
            // MOBILE: nothing is running or installed until the lines that say so are reached.
            Installed = false;
            Running = false;
            mod = initParams.Mod;

            // Load camera stacking depths BEFORE creating the DistantTerrain component,
            // so the values are populated by the time DistantTerrain's Awake/Start runs.
            LoadCameraStackingSettings();

            // MOBILE: the two reach dials, likewise before the component's Awake reads them.
            LoadRenderSettings();

            // Load fog densities BEFORE the DistantTerrain component runs. DistantTerrain.Start()
            // reads FogConfig directly and pushes the values into WeatherManager - the timing
            // matters because Start() also pushes the WeatherManager fog settings, and we want
            // the user's values applied on that first push rather than the built-in defaults.
            LoadFogSettings();

            // Load the distant-location highlight toggle BEFORE the component runs.
            // DistantTerrain.InitFarTerrain() reads DistantTerrainLocationConfig directly when
            // building the terrain info tilemap; that path runs once on world entry, so the
            // setting needs to be populated by then.
            LoadLocationHighlightSettings();

            // Load the "basic mode" toggle BEFORE the component runs. DistantTerrain reads
            // DistantTerrainBasicModeConfig directly while building the LOD heightmap (world
            // entry), so the value needs to be populated by then.
            LoadBasicModeSettings();

            // Load the per-climate "disable winter snow" toggles. DistantTerrain reads
            // DistantTerrainWinterSnowConfig live on every seasonal-texture update, so the
            // values only need to be populated before the first such update (well after this).
            LoadWinterSnowSettings();

            // Load the per-climate "disable winter rivers" toggles BEFORE the component runs.
            // DistantTerrain.ApplyDerivativeHeightmap reads DistantTerrainWinterRiverConfig while
            // carving the LOD heightmap (world entry), so the values must be populated by then.
            LoadWinterRiverSettings();

            // Load the "trees and dirt" toggle BEFORE the component runs. DistantTerrain pushes
            // _EnableTreesAndDirt to the material when the LOD terrain is built (world entry).
            LoadTreesAndDirtSettings();

            // Load the IPNM water settings BEFORE the component runs. DistantTerrain reads
            // these and pushes them to the shader material.
            LoadIPNMWaterSettings();

            // MOBILE: upstream loaded the shader out of the mod bundle
            // (mod.GetAsset<Shader>("Shaders/DistantTerrainTilemap.shader")); this port compiles it
            // into the app, where MobileShaders.Find returns the player's own copy rather than a
            // stripped one embedded in some other bundle.
            shaderDistantTerrainTilemap = MobileShaders.Find(ShaderName);
            bool shaderOk = shaderDistantTerrainTilemap != null && shaderDistantTerrainTilemap.isSupported;

            // MOBILE: the bundle data is checked HERE, before anything is created. The CSVs feed the
            // mountain lifts and the deriv map carves the rivers and coasts; without them the far
            // terrain is a smooth, waterless world map, which is worse than no far terrain at all.
            bool csvsPresent = true;
            string missingCsv = null;
            foreach (string csv in CsvFileNames)
            {
                if (mod == null || mod.GetAsset<TextAsset>(csv) == null)
                {
                    csvsPresent = false;
                    missingCsv = missingCsv == null ? csv : missingCsv + ", " + csv;
                }
            }
            bool derivPresent = mod != null && mod.GetAsset<Texture2D>(DerivMapFileName) != null;

            if (!DistantTerrain.Available(shaderOk, csvsPresent, derivPresent))
            {
                string reason = !shaderOk
                    ? "shader " + ShaderName + (shaderDistantTerrainTilemap == null ? " is not in this build" : " does not compile on this device")
                    : !csvsPresent
                        ? "the mountain tables are not in the bundle (" + missingCsv + ")"
                        : DerivMapFileName + " is not in the bundle";
                Debug.LogWarning("[DistantTerrain] not available: " + reason);
                return;
            }

            gameobjectDistantTerrain = new GameObject("DistantTerrain");
            // MOBILE: the cameras and the event subscriptions live on this object; a scene reload
            // would destroy it while MobilePortedMods' one-shot start could never bring it back, so
            // the launcher entry would read ON over vanilla view distance with nothing in the log to
            // say otherwise. Same fix as the WoD Terrain port's driver (InterestingTerrains.cs).
            Object.DontDestroyOnLoad(gameobjectDistantTerrain);
            componentDistantTerrain = gameobjectDistantTerrain.AddComponent<DistantTerrain>();

            // Assign only the core shader to the component
            componentDistantTerrain.ShaderDistantTerrainTilemap = shaderDistantTerrainTilemap;

            // MOBILE: the gate passed and the component is in the scene, subscribed to
            // StreamingWorld.OnReady. The far terrain itself is built at world entry.
            Running = true;
        }

        /// <summary>
        /// Pulls the three CameraStacking slider values from modsettings.json
        /// into DistantTerrainCameraConfig. Wrapped in try/catch so a missing or
        /// malformed settings file falls back to the built-in defaults rather
        /// than breaking mod startup.
        /// </summary>
        private static void LoadCameraStackingSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();

                DistantTerrainCameraConfig.SkyboxCameraDepth =
                    settings.GetValue<int>("CameraStacking", "SkyboxCameraDepth");

                DistantTerrainCameraConfig.DistantTerrainCameraDepth =
                    settings.GetValue<int>("CameraStacking", "DistantTerrainCameraDepth");

                DistantTerrainCameraConfig.MainCameraDepth =
                    settings.GetValue<int>("CameraStacking", "MainCameraDepth");

                Debug.Log(string.Format(
                    "[DistantTerrain] Camera stacking depths loaded -- Skybox: {0}, Distant Terrain: {1}, Main: {2}",
                    DistantTerrainCameraConfig.SkyboxCameraDepth,
                    DistantTerrainCameraConfig.DistantTerrainCameraDepth,
                    DistantTerrainCameraConfig.MainCameraDepth));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load camera-depth settings, " +
                                 "using built-in defaults. Reason: " + ex.Message);
            }
        }

        /// <summary>
        /// MOBILE: the two reach dials. Both keys are OPTIONAL - the shipped modsettings.json has no
        /// Rendering section yet, and a settings file that predates it must not reset the iOS
        /// defaults to something wilder. Read through GetFloatOrDefault, which is silent on a missing
        /// key, and clamped: blendEnd below the main camera's far clip would put the far terrain
        /// nearer than the near terrain, and above 145,000 exceeds what the shader's fade band and
        /// the stacked camera's depth buffer were ever tuned for.
        /// </summary>
        private static void LoadRenderSettings()
        {
            float mainFar = DefaultMainCameraFarClipPlane;
            float blendEnd = DefaultBlendEnd;
            try
            {
                ModSettings settings = mod.GetSettings();
                mainFar = GetFloatOrDefault(settings, "Rendering", "MainCameraFarClipPlane", DefaultMainCameraFarClipPlane);
                blendEnd = GetFloatOrDefault(settings, "Rendering", "BlendEnd", DefaultBlendEnd);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load rendering settings, " +
                                 "using the iOS defaults. Reason: " + ex.Message);
            }

            DistantTerrainRenderConfig.MainCameraFarClipPlane = Mathf.Clamp(mainFar, 1000f, 30000f);
            DistantTerrainRenderConfig.BlendEnd = Mathf.Clamp(blendEnd, DistantTerrainRenderConfig.MainCameraFarClipPlane, 145000f);

            Debug.Log(string.Format(
                "[DistantTerrain] reach: blendEnd {0} (blendStart {1}), main camera far clip {2}",
                DistantTerrainRenderConfig.BlendEnd,
                BlendStartFor(DistantTerrainRenderConfig.BlendEnd),
                DistantTerrainRenderConfig.MainCameraFarClipPlane));
        }

        /// <summary>
        /// Loads the five per-weather fog densities from the 'Fog' settings section.
        /// Wrapped in try/catch so an older modsettings.json (no Fog section) silently
        /// falls back to the values DistantTerrain previously hard-coded on its
        /// inspector fields - the user sees identical visuals until they touch a slider.
        /// </summary>
        private static void LoadFogSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                FogConfig.SunnyFogDensity = settings.GetValue<float>("Fog", "SunnyFogDensity") / 1000f;
                FogConfig.OvercastFogDensity = settings.GetValue<float>("Fog", "OvercastFogDensity") / 1000f;
                FogConfig.RainyFogDensity = settings.GetValue<float>("Fog", "RainyFogDensity") / 1000f;
                FogConfig.SnowyFogDensity = settings.GetValue<float>("Fog", "SnowyFogDensity") / 1000f;
                FogConfig.HeavyFogDensity = settings.GetValue<float>("Fog", "HeavyFogDensity") / 1000f;

                Debug.Log(string.Format(
                    "[DistantTerrain] Fog densities loaded -- Sunny: {0}, Overcast: {1}, Rainy: {2}, Snowy: {3}, Heavy: {4}",
                    FogConfig.SunnyFogDensity,
                    FogConfig.OvercastFogDensity,
                    FogConfig.RainyFogDensity,
                    FogConfig.SnowyFogDensity,
                    FogConfig.HeavyFogDensity));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load fog density settings, " +
                                 "using built-in defaults. Reason: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads the HighlightDistantLocations toggle from the 'LocationHighlight' settings
        /// section. Wrapped in try/catch so an older modsettings.json (no LocationHighlight
        /// section) silently falls back to the default of enabled.
        /// MOBILE: the ToggleKey read is gone with the hotkey (see DistantTerrainLocationConfig).
        /// </summary>
        private static void LoadLocationHighlightSettings()
        {
            // MOBILE: the SETTING still defaults to enabled - that is what the bundle ships and what
            // the catch below falls back to. What changed is that the setting alone no longer
            // decides: DFU merges the player's settings file over the bundle's own modsettings.json
            // and offers no way to ask which file a value came from, so the bundle's `true` is
            // treated as unset and the beacons stay at the port's code default (off) unless the
            // player actually has a settings file on disk. See HighlightLocationsFrom.
            bool settingValue;
            try
            {
                ModSettings settings = mod.GetSettings();
                settingValue = settings.GetValue<bool>("LocationHighlight", "HighlightDistantLocations");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load location-highlight settings, " +
                                 "defaulting to enabled. Reason: " + ex.Message);
                settingValue = true;
            }

            DistantTerrainLocationConfig.HighlightLocations =
                HighlightLocationsFrom(UserSettingsFilePresent(), settingValue);

            Debug.Log("[DistantTerrain] HighlightDistantLocations enabled: " +
                      DistantTerrainLocationConfig.HighlightLocations);
        }

        /// <summary>
        /// MOBILE: does the player have a settings file of their own for this mod? DFU writes one to
        /// Mod.ConfigurationDirectory/modsettings.json the first time settings are changed or a
        /// preset is applied, and reads it over the bundle's copy; the merged ModSettings object
        /// carries no provenance, so the file's existence is the only thing that separates "the
        /// player asked for this" from "the bundle shipped it".
        /// </summary>
        private static bool UserSettingsFilePresent()
        {
            try
            {
                return mod != null &&
                       System.IO.File.Exists(System.IO.Path.Combine(mod.ConfigurationDirectory, "modsettings.json"));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the EnableTreesAndDirt toggle from the 'TreesAndDirt' settings section. Wrapped in
        /// try/catch so an older modsettings.json (no TreesAndDirt section) silently falls back to the
        /// default of enabled - i.e. the distant tree specks + woodland dirt are on out of the box.
        /// </summary>
        private static void LoadTreesAndDirtSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                DistantTerrainFeatureConfig.EnableTreesAndDirt =
                    settings.GetValue<bool>("TreesAndDirt", "EnableTreesAndDirt");

                Debug.Log("[DistantTerrain] EnableTreesAndDirt: " +
                          DistantTerrainFeatureConfig.EnableTreesAndDirt);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load trees-and-dirt setting, " +
                                 "defaulting to enabled. Reason: " + ex.Message);
                DistantTerrainFeatureConfig.EnableTreesAndDirt = true;
            }
        }

        /// <summary>
        /// Loads the IPNM water settings (IPNMWater toggle, brightness, and opacity multipliers)
        /// from the 'Iliac Puddle No More Water Mode' settings section. Wrapped in try/catch so
        /// an older modsettings.json (no IPNM section) silently falls back to defaults: IPNM
        /// disabled, brightness=1.0, opacity=1.0 (no adjustment).
        /// </summary>
        private static void LoadIPNMWaterSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                IPNMWaterConfig.IPNMWater =
                    settings.GetValue<bool>("Iliac Puddle No More Water Mode", "IPNMWater");
                IPNMWaterConfig.IPNMWaterBrightness =
                    settings.GetValue<float>("Iliac Puddle No More Water Mode", "IPNMWaterBrightness");
                IPNMWaterConfig.IPNMWaterOpacity =
                    settings.GetValue<float>("Iliac Puddle No More Water Mode", "IPNMWaterOpacity");

                Debug.Log(string.Format(
                    "[DistantTerrain] IPNM Water settings loaded -- Enabled: {0}, Brightness: {1}, Opacity: {2}",
                    IPNMWaterConfig.IPNMWater,
                    IPNMWaterConfig.IPNMWaterBrightness,
                    IPNMWaterConfig.IPNMWaterOpacity));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load IPNM water settings, " +
                                 "defaulting to disabled. Reason: " + ex.Message);
                IPNMWaterConfig.IPNMWater = false;
                IPNMWaterConfig.IPNMWaterBrightness = 1.0f;
                IPNMWaterConfig.IPNMWaterOpacity = 1.0f;
            }
        }

        /// <summary>
        /// Loads the EnableBasicMode toggle from the 'BasicMode' settings section. Wrapped in
        /// try/catch so an older modsettings.json (no BasicMode section) silently falls back to
        /// the default of disabled - i.e. the full World-of-Daggerfall rendering (CSV mountain
        /// prefabs + deriv-map water) used by the pre-feature build.
        /// </summary>
        private static void LoadBasicModeSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                DistantTerrainBasicModeConfig.EnableBasicMode =
                    settings.GetValue<bool>("BasicMode", "EnableBasicMode");

                Debug.Log("[DistantTerrain] Basic mode (no mountain prefabs, simple water) enabled: " +
                          DistantTerrainBasicModeConfig.EnableBasicMode);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load basic-mode settings, " +
                                 "defaulting to disabled (full World of Daggerfall rendering). Reason: " + ex.Message);
                DistantTerrainBasicModeConfig.EnableBasicMode = false;
            }
        }

        /// <summary>
        /// Reads a bool setting but returns <paramref name="fallback"/> when the key is absent.
        /// DFU's ModSettings.GetValue throws KeyNotFoundException for a missing key, so reading
        /// keys directly in a shared try/catch meant ONE missing/renamed key aborted the whole
        /// section and silently reset every per-climate toggle to false. (That is exactly what
        /// broke all the winter snow/river toggles when the Desert keys were removed from
        /// modsettings.json.) Routing every read through here isolates each key.
        /// </summary>
        private static bool GetBoolOrDefault(ModSettings settings, string section, string key, bool fallback = false)
        {
            try
            {
                return settings.GetValue<bool>(section, key);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogWarning(string.Format(
                    "[DistantTerrain] Settings key '{0}/{1}' not found; using default ({2}).",
                    section, key, fallback));
                return fallback;
            }
        }

        /// <summary>
        /// MOBILE: the float twin of GetBoolOrDefault, and deliberately SILENT on a missing key -
        /// the Rendering section it reads is not in the shipped modsettings.json at all, so a
        /// warning per key would fire on every launch for a setting that is working as intended.
        /// LoadRenderSettings logs the effective values instead.
        /// </summary>
        private static float GetFloatOrDefault(ModSettings settings, string section, string key, float fallback)
        {
            try
            {
                return settings.GetValue<float>(section, key);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Loads the per-climate "disable winter snow" toggles from the 'WinterSnow' settings
        /// section. Wrapped in try/catch so an older modsettings.json (no WinterSnow section)
        /// silently falls back to all-false - i.e. every biome shows its snow-covered winter
        /// tileset in winter, identical to the pre-feature build.
        /// </summary>
        private static void LoadWinterSnowSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                // Per-key safe reads (see GetBoolOrDefault): a single missing/renamed key only
                // defaults THAT toggle to false instead of throwing and silently resetting every
                // per-climate toggle in the section. Desert/Desert2 (climates 224/225) are
                // intentionally not user-exposed - deserts never show distant snow - so their
                // config fields stay at their default false.
                DistantTerrainWinterSnowConfig.DisableSnowMountain =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowMountain");
                DistantTerrainWinterSnowConfig.DisableSnowRainforest =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowRainforest");
                DistantTerrainWinterSnowConfig.DisableSnowSwamp =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowSwamp");
                DistantTerrainWinterSnowConfig.DisableSnowSubtropical =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowSubtropical");
                DistantTerrainWinterSnowConfig.DisableSnowMountainWoods =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowMountainWoods");
                DistantTerrainWinterSnowConfig.DisableSnowWoodlands =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowWoodlands");
                DistantTerrainWinterSnowConfig.DisableSnowHauntedWoodlands =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowHauntedWoodlands");
                DistantTerrainWinterSnowConfig.DisableSnowMaquis =
                    GetBoolOrDefault(settings, "WinterSnow", "DisableSnowMaquis");

                Debug.Log(string.Format(
                    "[DistantTerrain] Winter snow disabled per climate -- Mountain: {0}, " +
                    "Rainforest: {1}, Swamp: {2}, Subtropical: {3}, MountainWoods: {4}, Woodlands: {5}, " +
                    "HauntedWoodlands: {6}, Maquis: {7}",
                    DistantTerrainWinterSnowConfig.DisableSnowMountain,
                    DistantTerrainWinterSnowConfig.DisableSnowRainforest,
                    DistantTerrainWinterSnowConfig.DisableSnowSwamp,
                    DistantTerrainWinterSnowConfig.DisableSnowSubtropical,
                    DistantTerrainWinterSnowConfig.DisableSnowMountainWoods,
                    DistantTerrainWinterSnowConfig.DisableSnowWoodlands,
                    DistantTerrainWinterSnowConfig.DisableSnowHauntedWoodlands,
                    DistantTerrainWinterSnowConfig.DisableSnowMaquis));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load winter-snow settings, " +
                                 "defaulting to snow enabled for all climates. Reason: " + ex.Message);
                DistantTerrainWinterSnowConfig.DisableSnowMountain = false;
                DistantTerrainWinterSnowConfig.DisableSnowRainforest = false;
                DistantTerrainWinterSnowConfig.DisableSnowSwamp = false;
                DistantTerrainWinterSnowConfig.DisableSnowSubtropical = false;
                DistantTerrainWinterSnowConfig.DisableSnowMountainWoods = false;
                DistantTerrainWinterSnowConfig.DisableSnowWoodlands = false;
                DistantTerrainWinterSnowConfig.DisableSnowHauntedWoodlands = false;
                DistantTerrainWinterSnowConfig.DisableSnowMaquis = false;
            }
        }

        /// <summary>
        /// Loads the per-climate "disable winter rivers" toggles from the 'WinterRivers'
        /// settings section. Wrapped in try/catch so an older modsettings.json (no WinterRivers
        /// section) silently falls back to all-false - i.e. the deriv map carves every water
        /// cell as before, identical to the pre-feature build.
        /// </summary>
        private static void LoadWinterRiverSettings()
        {
            try
            {
                ModSettings settings = mod.GetSettings();
                // Per-key safe reads (see GetBoolOrDefault): a single missing/renamed key only
                // defaults THAT toggle to false instead of throwing and silently resetting every
                // per-climate toggle in the section. Desert/Desert2 (climates 224/225) are
                // intentionally not user-exposed, so their config fields stay at their default false.
                DistantTerrainWinterRiverConfig.DisableRiversMountain =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversMountain");
                DistantTerrainWinterRiverConfig.DisableRiversRainforest =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversRainforest");
                DistantTerrainWinterRiverConfig.DisableRiversSwamp =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversSwamp");
                DistantTerrainWinterRiverConfig.DisableRiversSubtropical =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversSubtropical");
                DistantTerrainWinterRiverConfig.DisableRiversMountainWoods =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversMountainWoods");
                DistantTerrainWinterRiverConfig.DisableRiversWoodlands =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversWoodlands");
                DistantTerrainWinterRiverConfig.DisableRiversHauntedWoodlands =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversHauntedWoodlands");
                DistantTerrainWinterRiverConfig.DisableRiversMaquis =
                    GetBoolOrDefault(settings, "WinterRivers", "DisableRiversMaquis");

                Debug.Log(string.Format(
                    "[DistantTerrain] Winter rivers disabled per climate -- Mountain: {0}, " +
                    "Rainforest: {1}, Swamp: {2}, Subtropical: {3}, MountainWoods: {4}, Woodlands: {5}, " +
                    "HauntedWoodlands: {6}, Maquis: {7}",
                    DistantTerrainWinterRiverConfig.DisableRiversMountain,
                    DistantTerrainWinterRiverConfig.DisableRiversRainforest,
                    DistantTerrainWinterRiverConfig.DisableRiversSwamp,
                    DistantTerrainWinterRiverConfig.DisableRiversSubtropical,
                    DistantTerrainWinterRiverConfig.DisableRiversMountainWoods,
                    DistantTerrainWinterRiverConfig.DisableRiversWoodlands,
                    DistantTerrainWinterRiverConfig.DisableRiversHauntedWoodlands,
                    DistantTerrainWinterRiverConfig.DisableRiversMaquis));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DistantTerrain] Could not load winter-river settings, " +
                                 "defaulting to rivers enabled for all climates. Reason: " + ex.Message);
                DistantTerrainWinterRiverConfig.DisableRiversMountain = false;
                DistantTerrainWinterRiverConfig.DisableRiversRainforest = false;
                DistantTerrainWinterRiverConfig.DisableRiversSwamp = false;
                DistantTerrainWinterRiverConfig.DisableRiversSubtropical = false;
                DistantTerrainWinterRiverConfig.DisableRiversMountainWoods = false;
                DistantTerrainWinterRiverConfig.DisableRiversWoodlands = false;
                DistantTerrainWinterRiverConfig.DisableRiversHauntedWoodlands = false;
                DistantTerrainWinterRiverConfig.DisableRiversMaquis = false;
            }
        }
    }
}
