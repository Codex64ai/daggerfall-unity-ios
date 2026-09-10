// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File Scripts/DistantTerrain.cs, copied for iOS except lines marked MOBILE.
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce.
// The World of Daggerfall-flavour additions carry no licence; private draft only.
//
// MOBILE: the twelve 2048^2 tileset ATLASES this file built at world entry (~270 MB resident, twelve
// fragment samplers) are gone. It now packs three 224-slice Texture2DArrays - summer / winter / rain,
// four biome tilesets of 56 records each - and binds those to the rewritten shader (Task 3).
//
//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)
//Contributors: Hazelnut, Lypyl, Interkarma, MaoDeVaca

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;   // MOBILE: TextureMap.Albedo for the tile arrays
using DaggerfallWorkshop.Game.Mobile;      // MOBILE: nothing but the port's own neighbours

namespace DistantTerrain
{
    /// <summary>
    /// Manages a world terrain object built from the world height map for increased terrain/view distance
    /// </summary>
    public class DistantTerrain : MonoBehaviour
    {
        #region Fields

        // Streaming World Component
        public StreamingWorld streamingWorld = null;

        // Local player GPS for tracking player virtual position
        public PlayerGPS playerGPS;

        // WeatherManager is used for seasonal textures
        public WeatherManager weatherManager;

        public int mainCameraDepth = 3;
        public int stackedCameraDepth = 2;
        public int cameraRenderSkyboxToTextureDepth = -10;
        // MOBILE: settings-backed (DistantTerrainRenderConfig), re-read by SetupGameObjects so a
        // MOBILE: change takes effect on the next transition / save load, like the camera depths.
        public float mainCameraFarClipPlane = DistantTerrainPort.DefaultMainCameraFarClipPlane;
        public float nearClipPlaneStackedCamera = 980.0f;
        public FogMode sceneFogMode = FogMode.Exponential;
        public float SunnyFogDensity = 0.0000f;
        public float OvercastFogDensity = 0.000075f;
        public float RainyFogDensity = 0.0001f;
        public float SnowyFogDensity = 0.00025f;
        public float HeavyFogDensity = 0.05f;

        [HideInInspector]
        public bool disableTerrainCutout = false;

        [HideInInspector]
        public Vector3 virtualWorldOffset = Vector3.zero;

        int lastRetroMode = -1;

        // MOBILE: half of upstream's 100,000 / 120,000 - the iOS reach default (the perf dial).
        // blendStart is DERIVED from blendEnd (DistantTerrainPort.BlendStartFor): the shader's fade
        // band is (_BlendEnd - _BlendStart + 1), so a start above the end inverts it and fades the
        // whole far terrain to nothing. Both are refreshed from settings in SetupGameObjects.
        [Range(0.0f, 145000.0f)]
        public float blendStart = 50000.0f;

        [Range(0.0f, 145000.0f)]
        public float blendEnd = DistantTerrainPort.DefaultBlendEnd;

        // Width (in map tiles) of the fog band at the world boundary. Hides the
        // ocean-textured padding at the edge of the world when distance fog is
        // disabled. Position-based (proximity to world edge in tile space), not
        // distance-based, so being near coastal ocean inside the world is fine.
        // Set to 0 to disable.
        [Range(0.0f, 100.0f)]
        public float worldEdgeFadeWidthTiles = 5.0f;

        // World-space depth the far terrain's near-terrain-footprint boundary ring is pinned
        // straight down (in the vertex shader) to form a hidden skirt/curtain. That underlay
        // seals the hairline see-through cracks between the coarse far terrain and the detailed
        // near terrain at the cutout boundary (worst at Terrain Distance 1-2 over uneven ground).
        // It is hidden behind the near terrain except in the cracks themselves, so a generous
        // value is harmless; raise it if any crack still shows on very steep boundary terrain,
        // or set 0 to disable the skirt (reverting to a hard cutout edge).
        [Range(0.0f, 2000.0f)]
        public float farTerrainSkirtDepth = 600.0f;

        int layerWorldTerrain;

        private float extraTranslationY = 0.0f;
        private float extraWaterTranslationY = 0.0f;

        /// <summary>
        /// extra translation property is the amount of y-bias of the FarTerrain geometry
        /// </summary>
        public float ExtraTranslationY
        {
            get
            {
                return extraTranslationY;
            }
        }

        // is dfUnity ready?
        bool isReady = false;

        // the height values of the world height map used as input for unity terrain function SetHeights()
        float[,] worldHeights = null;

        int worldMapWidth = MapsFile.MaxMapPixelX - MapsFile.MinMapPixelX;
        int worldMapHeight = MapsFile.MaxMapPixelY - MapsFile.MinMapPixelY;

        // used to track changes of playerGPS x- resp. y-position on the world map (-> a change results in an update of the terrain object's translation)
        int MapPixelX = -1;
        int MapPixelY = -1;

        // unity terrain object which will hold the low-detail world map geometry, set to null initially for lazy creation
        GameObject worldTerrainGameObject = null;

        // terrain material used for world texturing (will be changed when daggerFallLocation.currentSeason changes from previous setting)
        Material terrainMaterial = null;

        // map holds info for tiles of terrain: r-channel... climate index, gb... currently unused, a-channel... discard-rendering flag for shader
        Color32[] terrainInfoTileMap = null;
        int terrainInfoTileMapDim;

        // texture for terrainInfoTileMap
        Texture2D textureTerrainInfoTileMap = null;

        ClimateSeason currentSeason = ClimateSeason.Summer;

        // MOBILE: three packed tile texture ARRAYS replace the twelve 2048^2 biome atlases. Each
        // holds the four biome tilesets' 56 records as slices, in the order the shader indexes them
        // (slice = biome * SlicesPerBiome + record; 0 desert, 1 mountain, 2 woodland, 3 swamp), and
        // ALL THREE are bound in every season: the shader picks per fragment, because the snow caps
        // always need winter and the snow-free climates always need summer.
        Texture2DArray tileArraySummer = null;
        Texture2DArray tileArrayWinter = null;
        Texture2DArray tileArrayRain = null;

        // MOBILE: the memory line is one per session, not one per world entry.
        static bool arraysMegabytesLogged = false;

        // MOBILE: so is the one-shot geometry/camera/material dump beside "far terrain ready".
        static bool farTerrainDiagnosticLogged = false;

        // MOBILE: timing, reported by InitFarTerrain as one line (see the log literals in the port's
        // README section). Written by GenerateWorldTerrain, read immediately afterwards.
        double lastHeightmapMs = 0;
        double lastCarveMs = 0;
        double lastLiftsMs = 0;
        // MOBILE: the tile-array pack - twelve GetTerrainTextureArray builds (672 record decodes and
        // twelve Apply(true) mip generations) plus 672 GPU slice copies. It runs near the end of
        // GenerateWorldTerrain, after the three stages above are already recorded and before the
        // tilemap stopwatch starts, so without its own stage it is the one part of the world-entry
        // cost the printed line cannot account for - and it is the part most likely to dominate it.
        // Zero on the second and later world entries: the arrays are packed once per session.
        double lastArraysMs = 0;

        // MOBILE: how many map-pixel updates have been timed this session, and the reposition cost
        // waiting to be reported with the near-terrain rebuild that completes the same cross.
        int mapPixelUpdateCount = 0;
        double pendingMapPixelMs = 0;
        bool mapPixelUpdatePending = false;

        // MOBILE: what the main camera looked like before this port touched it, so a failed build
        // can put it back rather than leaving the player with a 15,000-unit depth-only camera.
        bool mainCameraSaved = false;
        float savedMainCameraFarClipPlane = 0f;
        CameraClearFlags savedMainCameraClearFlags = CameraClearFlags.Skybox;

        // MOBILE: the fog overwrite is upstream behaviour, kept, but announced once.
        bool fogApplied = false;

        Shader shaderDistantTerrainTilemap = null;
        public Shader ShaderDistantTerrainTilemap
        {
            get { return shaderDistantTerrainTilemap; }
            set { shaderDistantTerrainTilemap = value; }
        }

        // stacked near camera (used for near terrain from range 1000-15000) to prevent floating-point rendering precision problems for huge clipping ranges
        Camera stackedNearCamera = null;

        // stacked camera (used for far terrain) to prevent floating-point rendering precision problems for huge clipping ranges
        Camera stackedCamera = null;

        public Camera getFarTerrainCamera() { return stackedCamera; }
        public Camera getStackedNearCamera() { return stackedNearCamera; }

        GameObject goRenderSkyboxToTexture = null;
        Camera cameraRenderSkyboxToTexture = null;

        const int renderTextureSkyWidth = 256;
        const int renderTextureSkyHeight = 256;
        const int renderTextureSkyDepth = 16;
        const RenderTextureFormat renderTextureSkyFormat = RenderTextureFormat.ARGB32;
        RenderTexture renderTextureSky = null;

        // instance of dfUnity
        DaggerfallUnity dfUnity;

        // 1. First, add the Terrain component and assign the terrain data to it
        Terrain terrain;


        private int _cachedFogMode = int.MinValue;
        private float _cachedFogDensity = float.NaN;
        private float _cachedFogStartDistance = float.NaN;
        private float _cachedFogEndDistance = float.NaN;
        private int _cachedFogFromSkyTex = int.MinValue;
        private float _cachedBlendStart = float.NaN;
        private float _cachedBlendEnd = float.NaN;
        private int _cachedDisableCutout = int.MinValue;
        private float _cachedSkirtDepth = float.NaN;
        // Display gate for the location markers. Pushed every frame from
        // (DistantTerrainLocationConfig.HighlightLocations && RuntimeVisible) so the in-game
        // toggle hotkey takes effect within a frame; cached so an unchanged value costs nothing.
        private int _cachedHighlightLocations = int.MinValue;

        // Scratch for the per-climate winter-snow disable uniform (_DisableSnow[11]).
        private static readonly float[] _disableSnowScratch = new float[11];

        // Scratch for the per-REGION treatment uniform (_RegionData[8]; rgb = colour tint, w = snow
        // mode). Built once from DistantTerrainRegionConfig.Treatments. See PushRegionData.
        private static readonly Vector4[] _regionDataScratch = new Vector4[8];


        private struct MountainDef
        {
            public float centerX, centerY;
            public float radius;
            public float maxLift;
            public int bbStartX, bbEndX;
            public int bbStartY, bbEndY;
        }
        private List<MountainDef> mountainDefs = new List<MountainDef>();
        private bool[] mountainLifted = null;
        private float[,] baseWorldHeights = null;
        private bool[,] oceanMask = null;

        // Pristine far-terrain heights (world-map sampled, BEFORE the deriv-map water carving and
        // before any mountain lifts). Captured in GenerateWorldTerrain and used to re-carve the
        // rivers for the current season when the calendar crosses the winter boundary: the deriv
        // carve only ever LOWERS cells, so restoring this pristine copy is what lets frozen winter
        // rivers reappear in spring/summer. Null in basic mode (no deriv carving).
        private float[,] preDerivWorldHeights = null;

        // Whether the most recent deriv-map carve treated the world as winter. Compared against the
        // live calendar season in Update() to detect a winter<->non-winter transition that requires
        // re-carving the distant rivers. Maintained by ApplyDerivativeHeightmap.
        private bool lastWinterForRivers = false;
        private int cachedPlayerHmX = int.MinValue;
        private int cachedPlayerHmY = int.MinValue;

        // Safety margin (in map pixels) added to the near-terrain cutout radius before deciding
        // whether a mountain is "fully clear" (un-lifted). A LARGER value un-lifts a mountain
        // EARLIER - while it is still further out and still visible far terrain - which is what
        // produced the "summer green flash": the peak dropped to its flat base height (green grass
        // under the slope shader) for a tile-cross or two before the near terrain covered it.
        //
        // 0 keeps a peak LIFTED (steep -> stone) until its tile actually reaches the near-terrain
        // coverage ring (tileDist == TerrainDistance-1), where it sits hidden under the detailed
        // near terrain - so the flatten-to-green now happens out of sight. Single-/few-cell peaks
        // (the common case) stop flashing entirely; a large multi-cell peak may still show a little
        // because the whole peak un-lifts at once when its nearest cell reaches the ring.
        //
        private const int mountainCutoutBuffer = 1;

        #endregion

        #region Properties

        public bool IsReady { get { return ReadyCheck(); } }

        #endregion

        #region MOBILE: tile texture arrays

        /// <summary>
        /// Records per biome tileset, and the stride of one biome's block of slices. Every Daggerfall
        /// terrain archive has exactly 56 records (TextureReader.GetTerrainTextureArray returns null
        /// for anything else), and all 56 are packed - not just the four the far terrain samples - so
        /// a replacement set stays whole and the arithmetic below stays trivially checkable.
        /// Must equal the shader's _SlicesPerBiome, which this class pushes.
        /// </summary>
        public const int SlicesPerBiome = 56;

        /// <summary>Vanilla terrain tiles are 64x64; a replacement pack may be larger (see BuildTileArrays).</summary>
        public const int VanillaSliceDim = 64;

        /// <summary>
        /// The slice-index contract the rewritten shader implements (Task 3):
        /// <c>slice = biome * _SlicesPerBiome + record</c>, biome 0 desert / 1 mountain / 2 woodland /
        /// 3 swamp, record 0 water / 1 dirt / 2 grass / 3 stone. Pure, so the maths that decides what
        /// the distant ground is painted with is checkable without a GPU.
        /// </summary>
        public static int SliceIndex(int biome, int record)
        {
            return biome * SlicesPerBiome + record;
        }

        /// <summary>
        /// Pure capability gate. The far terrain needs its shader (compiled into the app, pinned
        /// Always-Included) AND the bundle's three mountain tables AND the river/coast map: without
        /// the shader nothing can be drawn, and without the data it would draw a smooth, waterless
        /// world map that looks worse than the fog it replaces.
        /// </summary>
        public static bool Available(bool shaderOk, bool csvsPresent, bool derivPresent)
        {
            return shaderOk && csvsPresent && derivPresent;
        }

        /// <summary>
        /// Pure: can these tilesets share ONE texture array at all? Size and mip count must match -
        /// Graphics.ConvertTexture blits slice for slice and cannot rescale, and the shader derives
        /// one mip LOD from one dimension. FORMAT is deliberately not part of this: the pack converts
        /// (see PackSeason), because a mixed-format set is a real and legitimate configuration rather
        /// than a corruption. World of Daggerfall - Biomes installs a TextureArray terrain material
        /// provider and TextureReader.GetTerrainTextureArray then hands back RGBA32 for one climate
        /// variant of a season and ARGB32 for another - same size, same layout, different channel
        /// order - which refusing outright cost the whole far terrain in the Task 9 simulator run.
        /// A different SIZE still refuses: nothing can make two resolutions share slices.
        /// </summary>
        public static bool TilesetsSameSize(int[] widths, int[] heights, int[] mipCounts, out string detail)
        {
            detail = string.Empty;
            if (widths == null || heights == null || mipCounts == null ||
                widths.Length == 0 || heights.Length != widths.Length || mipCounts.Length != widths.Length)
            {
                detail = "no tilesets to compare";
                return false;
            }

            for (int i = 1; i < widths.Length; i++)
            {
                if (widths[i] != widths[0] || heights[i] != heights[0])
                {
                    detail = string.Format("tileset {0} is {1}x{2}, tileset 0 is {3}x{4}",
                        i, widths[i], heights[i], widths[0], heights[0]);
                    return false;
                }
                if (mipCounts[i] != mipCounts[0])
                {
                    detail = string.Format("tileset {0} has {1} mip levels, tileset 0 has {2}",
                        i, mipCounts[i], mipCounts[0]);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Pure: the stricter predicate Graphics.CopyTexture needs - same size, same mip count AND
        /// the same format. Only the fallback path asks it, for a device whose driver offers no
        /// format-converting blit (SystemInfo.copyTextureSupport == None): there, a mixed-format set
        /// still has to refuse rather than corrupt, exactly as it did before ConvertTexture landed.
        /// </summary>
        public static bool TilesetsCompatible(int[] widths, int[] heights, int[] formats, int[] mipCounts, out string detail)
        {
            if (!TilesetsSameSize(widths, heights, mipCounts, out detail))
                return false;
            if (formats == null || formats.Length != widths.Length)
            {
                detail = "no tilesets to compare";
                return false;
            }
            for (int i = 1; i < formats.Length; i++)
            {
                if (formats[i] != formats[0])
                {
                    detail = string.Format("tileset {0} is format {1}, tileset 0 is format {2}",
                        i, (TextureFormat)formats[i], (TextureFormat)formats[0]);
                    return false;
                }
            }
            return true;
        }

        /// <summary>Pure: do all these tilesets already carry one format (so no conversion is needed)?</summary>
        public static bool TilesetFormatsAgree(int[] formats)
        {
            if (formats == null || formats.Length == 0) return false;
            for (int i = 1; i < formats.Length; i++)
                if (formats[i] != formats[0]) return false;
            return true;
        }

        /// <summary>Pure: bytes a texture array of this shape occupies, mip chain included.</summary>
        public static long ArrayBytes(int width, int height, int slices, int mipCount, int bytesPerPixel)
        {
            long perSlice = 0;
            for (int level = 0; level < Mathf.Max(1, mipCount); level++)
            {
                int w = Mathf.Max(1, width >> level);
                int h = Mathf.Max(1, height >> level);
                perSlice += (long)w * h * bytesPerPixel;
            }
            return perSlice * slices;
        }

        /// <summary>
        /// Uncompressed bytes per texel. The vanilla tilesets are ARGB32; a replacement pack can be
        /// compressed, in which case this overestimates - deliberately, since the memory line exists
        /// to warn about growth, not to flatter it.
        /// </summary>
        public static int BytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    return 1;
                case TextureFormat.RGB24:
                    return 3;
                case TextureFormat.RGBAHalf:
                    return 8;
                default:
                    return 4;
            }
        }

        /// <summary>Pure: a replacement pack whose tiles are bigger than vanilla scales the arrays by (dim/64)^2.</summary>
        public static bool SlicesAreOversize(int sliceDim)
        {
            return sliceDim > VanillaSliceDim;
        }

        /// <summary>Pure: the first ten map-pixel updates are timed, then every twenty-fifth.</summary>
        public static bool ShouldLogMapPixelUpdate(int count)
        {
            return count <= 10 || count % 25 == 0;
        }

        /// <summary>
        /// MOBILE: the contract TearDownFarTerrain implements, written down so it can be checked
        /// without a scene. Every entry is a UnityEngine.Object this component ALLOCATED and whose
        /// memory does not go away when the far-terrain GameObject is destroyed: destroying a
        /// GameObject destroys its Terrain component, not the TerrainData asset behind it; a
        /// runtime Material and a 1024^2 Texture2D are free-standing objects that merely happen to
        /// be referenced from one. Dropping the reference leaks ~12-16 MB for the session - inside
        /// the very handler whose purpose is to undo a half-built world. The self-test walks this
        /// list against the teardown's body, so a new allocation that is not freed fails the suite.
        /// </summary>
        public static readonly string[] TeardownDestroys =
        {
            "worldTerrainGameObject",
            "terrain.terrainData",
            "terrainMaterial",
            "textureTerrainInfoTileMap",
            "stackedCamera.gameObject",
            "goRenderSkyboxToTexture",
            "renderTextureSky",
        };

        /// <summary>
        /// MOBILE: the fields the teardown must set to null once their objects are gone or dropped.
        /// Destroying a Unity object does not null the C# field that points at it, and the managed
        /// arrays here (the 4.2 MB Color32 tilemap, the three float[1025,1025] heightmaps) are held
        /// alive by nothing else. Checked the same way as TeardownDestroys.
        /// </summary>
        public static readonly string[] TeardownNulls =
        {
            "worldTerrainGameObject",
            "terrain",
            "terrainMaterial",
            "textureTerrainInfoTileMap",
            "terrainInfoTileMap",
            "worldHeights",
            "baseWorldHeights",
            "preDerivWorldHeights",
            "oceanMask",
            "mountainLifted",
            "stackedCamera",
            "goRenderSkyboxToTexture",
            "cameraRenderSkyboxToTexture",
            "renderTextureSky",
        };

        // The four biome archives per season, in slice-block order: desert, mountain, woodland, swamp.
        static readonly int[] SummerArchives = { 2, 102, 302, 402 };
        static readonly int[] WinterArchives = { 3, 103, 303, 403 };
        static readonly int[] RainArchives = { 4, 104, 304, 404 };

        /// <summary>
        /// MOBILE: builds the three arrays the rewritten shader samples, replacing the twelve
        /// GetTerrainTilesetTexture atlases. Once per session - nothing about them changes between
        /// world entries - and false, with the reason logged, when the four tilesets of any season
        /// cannot share one array.
        /// </summary>
        bool BuildTileArrays()
        {
            if (tileArraySummer != null && tileArrayWinter != null && tileArrayRain != null)
                return true;

            DestroyTileArrays();

            TextureReader reader = dfUnity != null && dfUnity.MaterialReader != null ? dfUnity.MaterialReader.TextureReader : null;
            if (reader == null)
            {
                Debug.LogWarning("[DistantTerrain] tileset arrays mismatch: the texture reader is not ready");
                return false;
            }

            tileArraySummer = PackSeason(reader, "summer", SummerArchives);
            tileArrayWinter = PackSeason(reader, "winter", WinterArchives);
            tileArrayRain = PackSeason(reader, "rain", RainArchives);

            if (tileArraySummer == null || tileArrayWinter == null || tileArrayRain == null)
            {
                DestroyTileArrays();
                return false;
            }

            // MOBILE: PackSeason only guarantees the FOUR archives within one season agree. The
            // shader takes one dimension for all three arrays - `sliceDim = max(
            // _TileArraySummer_TexelSize.z, _TileArraySummer_TexelSize.w)` in FarTerrainCommon.cginc
            // - and computes the mip LOD for winter and rain from it too, because the snow caps
            // always sample winter and a snow-free climate always samples summer. A replacement pack
            // that resizes one season and not another passes all three per-season checks and then
            // mip-selects two of the three arrays by the wrong dimension: two mip levels off per
            // doubling, i.e. visible aliasing or blur on exactly those fragments. The `dim` and the
            // memory line below read summer alone for the same reason. So the three packed arrays
            // are compared to each other before any of them is bound. Size and mip count only: the
            // three arrays are sampled independently, so a season packed as RGBA32 next to one packed
            // in its sources' own format is fine - it is the DIMENSION the shader shares.
            string crossDetail;
            if (!TilesetsSameSize(
                    new[] { tileArraySummer.width, tileArrayWinter.width, tileArrayRain.width },
                    new[] { tileArraySummer.height, tileArrayWinter.height, tileArrayRain.height },
                    new[] { tileArraySummer.mipmapCount, tileArrayWinter.mipmapCount, tileArrayRain.mipmapCount },
                    out crossDetail))
            {
                // Tileset 0 is summer, 1 winter, 2 rain - named in the line so the detail's index
                // means something to whoever reads it out of a Player.log.
                Debug.LogWarning(string.Format(
                    "[DistantTerrain] tileset arrays mismatch: {0} (across seasons; 0 summer, 1 winter, 2 rain)",
                    crossDetail));
                DestroyTileArrays();
                return false;
            }

            int dim = tileArraySummer.width;
            long bytes = ArrayBytes(tileArraySummer.width, tileArraySummer.height, tileArraySummer.depth,
                             tileArraySummer.mipmapCount, BytesPerPixel(tileArraySummer.format))
                       + ArrayBytes(tileArrayWinter.width, tileArrayWinter.height, tileArrayWinter.depth,
                             tileArrayWinter.mipmapCount, BytesPerPixel(tileArrayWinter.format))
                       + ArrayBytes(tileArrayRain.width, tileArrayRain.height, tileArrayRain.depth,
                             tileArrayRain.mipmapCount, BytesPerPixel(tileArrayRain.format));
            long megabytes = bytes / (1024 * 1024);

            if (SlicesAreOversize(dim))
            {
                // A replacement pack with larger tiles scales this by (dim/64)^2 - 256x256 tiles are
                // sixteen times the vanilla footprint, which is the difference between 15 MB and
                // 240 MB on a phone. Worth a warning even though it is a legitimate configuration.
                Debug.LogWarning(string.Format("[DistantTerrain] tileset arrays are {0}x{0} slices ({1} MB)", dim, megabytes));
            }

            if (!arraysMegabytesLogged)
            {
                Debug.Log(string.Format("[DistantTerrain] arrays {0} MB", megabytes));
                arraysMegabytesLogged = true;
            }
            return true;
        }

        /// <summary>
        /// MOBILE: packs one season's four biome tilesets into a single 224-slice array,
        /// <c>Graphics.CopyTexture(src, record, dst, biome * SlicesPerBiome + record)</c> - a GPU-side
        /// blit that needs only CopyTextureSupport.Basic and works on non-readable textures, so this
        /// is 224 slice copies rather than a GetPixels32 round trip. When the four archives come back
        /// in different formats (Biomes' TextureArray provider does exactly that) the same 224 blits
        /// go through <c>Graphics.ConvertTexture</c> instead, which converts as it copies. Returns
        /// null (reason logged) if an archive does not load or the four cannot share one array.
        /// </summary>
        Texture2DArray PackSeason(TextureReader reader, string season, int[] archives)
        {
            Texture2DArray[] src = new Texture2DArray[archives.Length];
            try
            {
                int[] widths = new int[archives.Length];
                int[] heights = new int[archives.Length];
                int[] formats = new int[archives.Length];
                int[] mipCounts = new int[archives.Length];

                for (int b = 0; b < archives.Length; b++)
                {
                    src[b] = reader.GetTerrainTextureArray(archives[b], TextureMap.Albedo);
                    if (src[b] == null)
                    {
                        Debug.LogWarning(string.Format(
                            "[DistantTerrain] tileset arrays mismatch: {0} archive {1} did not load as a 56-record array",
                            season, archives[b]));
                        return null;
                    }
                    widths[b] = src[b].width;
                    heights[b] = src[b].height;
                    formats[b] = (int)src[b].format;
                    mipCounts[b] = src[b].mipmapCount;
                }

                string detail;
                if (!TilesetsSameSize(widths, heights, mipCounts, out detail))
                {
                    Debug.LogWarning(string.Format(
                        "[DistantTerrain] tileset arrays mismatch: {0} ({1}; archives {2})",
                        detail, season, string.Join(", ", System.Array.ConvertAll(archives, a => a.ToString()))));
                    return null;
                }

                // MOBILE: the four archives of a season may legitimately differ in FORMAT (Biomes'
                // TextureArray provider - see TilesetsSameSize). Graphics.ConvertTexture is a
                // format-converting GPU blit with the same slice-to-slice signature as CopyTexture,
                // works on non-readable textures, and costs the same 224 blits; so a mixed set is
                // converted into one array rather than refused. RGBA32 is the destination when the
                // four disagree - it is the format the near terrain and the shader already expect,
                // and converting into one of the two competing layouts would be arbitrary.
                bool formatsAgree = TilesetFormatsAgree(formats);
                if (!formatsAgree && SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
                {
                    // No converting blit on this device: back to the strict rule, which refuses.
                    string strictDetail;
                    TilesetsCompatible(widths, heights, formats, mipCounts, out strictDetail);
                    Debug.LogWarning(string.Format(
                        "[DistantTerrain] tileset arrays mismatch: {0} ({1}; archives {2}; no format-converting blit on this device)",
                        strictDetail, season, string.Join(", ", System.Array.ConvertAll(archives, a => a.ToString()))));
                    return null;
                }
                TextureFormat dstFormat = formatsAgree ? src[0].format : TextureFormat.RGBA32;

                Texture2DArray dst = new Texture2DArray(widths[0], heights[0], archives.Length * SlicesPerBiome,
                    dstFormat, mipCounts[0], false);
                // Match what the sources and DFU's own near-terrain array carry: Point filtering (the
                // MaterialReader setting upstream applied to all twelve atlases, FilterMode.Point by
                // default - a new Texture2DArray would otherwise be Bilinear and blur the tiles),
                // Repeat wrapping (the shader takes frac() of an in-tile coordinate and the four
                // sampled records tile in both directions; Clamp would smear the edge texel at every
                // repeat under bilinear), and the sources' anisotropy.
                dst.filterMode = dfUnity.MaterialReader.MainFilterMode;
                dst.wrapMode = TextureWrapMode.Repeat;
                dst.anisoLevel = src[0].anisoLevel;

                for (int b = 0; b < archives.Length; b++)
                {
                    // MOBILE: copy-or-convert is decided per ARCHIVE, not per season. `formatsAgree`
                    // is false as soon as ONE of the four disagrees, and the previous code then sent
                    // all four - including the ones already in the destination's format - through
                    // Graphics.ConvertTexture. On Metal (and the iOS simulator in particular) a
                    // same-format ConvertTexture returns FALSE, so a Biomes install refused the
                    // whole far terrain with the self-contradictory line "could not convert archive 3
                    // record 0 from RGBA32 to RGBA32". A source already in the destination format is
                    // a plain slice copy and must stay one; only a genuinely different format needs
                    // the converting blit.
                    bool convertThisArchive = src[b].format != dstFormat;
                    for (int record = 0; record < SlicesPerBiome; record++)
                    {
                        int slice = SliceIndex(b, record);
                        if (!convertThisArchive)
                        {
                            Graphics.CopyTexture(src[b], record, dst, slice);
                        }
                        else if (!Graphics.ConvertTexture(src[b], record, dst, slice))
                        {
                            // The driver refused this pair after all. Refuse the season rather than
                            // bind an array with holes in it - a half-converted array draws garbage.
                            Debug.LogWarning(string.Format(
                                "[DistantTerrain] tileset arrays mismatch: could not convert archive {0} record {1} " +
                                "from {2} to {3} ({4}; archives {5})",
                                archives[b], record, (TextureFormat)formats[b], dstFormat, season,
                                string.Join(", ", System.Array.ConvertAll(archives, a => a.ToString()))));
                            Destroy(dst);
                            return null;
                        }
                    }
                }

                if (!formatsAgree)
                {
                    var distinct = new System.Collections.Generic.List<string>();
                    for (int b = 0; b < formats.Length; b++)
                    {
                        string name = ((TextureFormat)formats[b]).ToString();
                        if (!distinct.Contains(name)) distinct.Add(name);
                    }
                    Debug.Log(string.Format("[DistantTerrain] tileset arrays converted: {0} -> {1} ({2})",
                        string.Join(", ", distinct.ToArray()), dstFormat, season));
                }

                return dst;
            }
            finally
            {
                // A source built at runtime by GetTerrainTextureArray is ours to free; one that came
                // out of a mod's asset bundle (TryImportTextureArray hands back the bundle's own
                // Texture2DArray) is NOT - destroying it corrupts that mod for the rest of the
                // session, the same trap the deriv map carries a comment about. Runtime-built arrays
                // have no name, bundle assets do, so only the nameless ones are destroyed.
                for (int b = 0; b < src.Length; b++)
                    if (src[b] != null && string.IsNullOrEmpty(src[b].name))
                        Destroy(src[b]);
            }
        }

        #endregion

        #region Unity

        static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        /// <summary>
        /// MOBILE: the three scene references upstream demanded in Awake, resolved whenever they are
        /// asked for instead. Upstream called Application.Quit() (or Debug.Break in the editor) when
        /// any of them was missing - and on this build they ARE missing when Init runs: the launcher
        /// starts the ported mods at the Start state, on the title screen, where there is no
        /// StreamingWorld, no Player and no WeatherManager. Upstream's Awake would have closed the
        /// app. They all exist by StreamingWorld.OnReady, which is where the far terrain is built, so
        /// the lookup is retried there (and by SetupGameObjects) and only reported then.
        /// </summary>
        bool TryResolveSceneRefs(bool log)
        {
            if (dfUnity == null)
                dfUnity = DaggerfallUnity.Instance;

            if (!streamingWorld)
            {
                GameObject go = GameObject.Find("StreamingWorld");
                if (go != null)
                    streamingWorld = go.GetComponent<StreamingWorld>();
            }
            if (!playerGPS)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null)
                    playerGPS = go.GetComponent<PlayerGPS>();
            }
            if (!weatherManager)
            {
                GameObject wmGo = GameObject.Find("WeatherManager");
                if (wmGo != null)
                    weatherManager = wmGo.GetComponent<WeatherManager>();
            }

            string missing = null;
            if (!streamingWorld) missing = "StreamingWorld";
            if (!playerGPS) missing = missing == null ? "PlayerGPS" : missing + ", PlayerGPS";
            if (!weatherManager) missing = missing == null ? "WeatherManager" : missing + ", WeatherManager";
            if (missing == null)
                return true;

            if (log)
                Debug.LogWarning("[DistantTerrain] not available: the scene has no " + missing);
            return false;
        }

        void Awake()
        {
            // MOBILE: quiet - at the title none of these exist yet, and that is not an error.
            TryResolveSceneRefs(false);

            // NOTE: the DistantTerrainFlyMap (location-highlight + teleport controller) upstream
            // created here and in _startupMod.InitStart is dropped from this port together with its
            // file, the "13th Passage" spell and the console command - see the port header.

            layerWorldTerrain = LayerMask.NameToLayer("WorldTerrain");
            if (layerWorldTerrain == -1)
            {
                DaggerfallUnity.LogMessage("Did not find Layer with name \"WorldTerrain\"! Defaulting to Layer 31\nIt is prefered that Layer \"WorldTerrain\" is set in Unity Editor under \"Edit/Project Settings/Tags and Layers!\"", true);
                layerWorldTerrain = 31;
            }
            SetupGameObjects();
        }

        void OnEnable()
        {
            FloatingOrigin.OnPositionUpdate += WorldTerrainUpdatePosition;
            StreamingWorld.OnReady += InitFarTerrain;
            StreamingWorld.OnTeleportToCoordinates += UpdateWorldTerrain;
            // Advance the near-terrain cutout only after StreamingWorld has finished rebuilding
            // the near terrain for the new map pixel. Fixes the transient near/far seam (see
            // WorldTerrainAfterTerrainsUpdated for the full explanation).
            StreamingWorld.OnUpdateTerrainsEnd += WorldTerrainAfterTerrainsUpdated;
            PlayerEnterExit.OnTransitionExterior += TransitionToExterior;
            PlayerEnterExit.OnTransitionDungeonExterior += TransitionToExterior;
            SaveLoadManager.OnLoad += OnLoadEvent;
        }

        void OnDisable()
        {
            FloatingOrigin.OnPositionUpdate -= WorldTerrainUpdatePosition;
            StreamingWorld.OnReady -= InitFarTerrain;
            StreamingWorld.OnTeleportToCoordinates -= UpdateWorldTerrain;
            StreamingWorld.OnUpdateTerrainsEnd -= WorldTerrainAfterTerrainsUpdated;
            PlayerEnterExit.OnTransitionExterior -= TransitionToExterior;
            PlayerEnterExit.OnTransitionDungeonExterior -= TransitionToExterior;
            SaveLoadManager.OnLoad -= OnLoadEvent;
        }

        void WorldTerrainUpdatePosition(Vector3 offset)
        {
            if (worldTerrainGameObject != null)
            {
                // A floating-origin re-base shifts the world transform but does NOT change the
                // player's map pixel, so the cutout center (_PlayerPosX/_PlayerPosY) is still
                // valid and is intentionally NOT touched here. The cutout is advanced only once
                // the near terrain has finished rebuilding, in WorldTerrainAfterTerrainsUpdated
                // (StreamingWorld.OnUpdateTerrainsEnd) - see the seam-fix note there.
                UpdatePositionWorldTerrain(ref worldTerrainGameObject, offset);
            }
        }

        /// <summary>
        /// MOBILE: StreamingWorld.OnReady calls this, and upstream let whatever it threw escape into
        /// that event's invocation list, where one exception stops every later subscriber for the
        /// rest of the session. Everything it does - ~4 MB of managed heightmap, three GPU texture
        /// arrays packed from bundle-replaceable tilesets, a Unity Terrain, two cameras, a render
        /// texture - can fail on a device or under a texture-replacement pack. Contained here, and
        /// what was half-built is torn down: the far terrain object, the two stacked cameras, the sky
        /// render texture, and the main camera's own far clip plane and clear flags.
        /// </summary>
        void InitFarTerrain()
        {
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!BuildFarTerrain(total))
                    TearDownFarTerrain();
            }
            catch (Exception ex)
            {
                Debug.LogError("[DistantTerrain] far terrain failed: " + ex);
                TearDownFarTerrain();
            }
        }

        /// <summary>
        /// MOBILE: upstream's InitFarTerrain body. Returns false for a refusal it has already
        /// explained (no scene, or tilesets that cannot be packed into one array); throws for
        /// anything unforeseen. Either way InitFarTerrain above cleans up.
        /// </summary>
        bool BuildFarTerrain(System.Diagnostics.Stopwatch total)
        {
            // MOBILE: the world exists by now even though it did not when Init ran (title screen).
            if (!TryResolveSceneRefs(true))
                return false;

            // MOBILE: Start() ran at the title, before there was a WeatherManager to write to.
            ApplyFogSettings();

            if (!GenerateWorldTerrain())
                return false;       // the tileset arrays refused; GenerateWorldTerrain said why

            System.Diagnostics.Stopwatch tilemapWatch = System.Diagnostics.Stopwatch.StartNew();

            GameObject goExterior = GameObject.Find("Exterior");

            if (goExterior != null)
            {
                worldTerrainGameObject.transform.SetParent(goExterior.transform);
            }
            else
            {
                DaggerfallUnity.LogMessage("[DistantTerrain] Could not find /Exterior GameObject; world terrain will not be reparented.", true);
            }

            SetUpCameras();

            terrain = worldTerrainGameObject.GetComponent<Terrain>();

            int worldMapResolution = Math.Max(worldMapWidth, worldMapHeight);

            int[] climateMap = new int[worldMapResolution * worldMapResolution];
            for (int y = 0; y < worldMapHeight; y++)
            {
                for (int x = 0; x < worldMapWidth; x++)
                {
                    // get climate record for this map pixel
                    int worldClimate = dfUnity.ContentReader.MapFileReader.GetClimateIndex(x, y);
                    climateMap[(worldMapHeight - 1 - y) * worldMapResolution + x] = worldClimate;
                }
            }

            terrainInfoTileMapDim = terrain.terrainData.heightmapResolution - 1;
            terrainInfoTileMap = new Color32[terrainInfoTileMapDim * terrainInfoTileMapDim];

            // Per-pixel region treatment (colour tint + snow mode) is baked into the tilemap GREEN
            // channel. Build the region-index -> treatment-byte map once, then look it up per pixel.
            byte[] regionTreatment = BuildRegionTreatmentMap();

            // Assign tile data to tilemap
            Color32 tileColor = new Color32(0, 0, 0, 0);
            for (int y = 0; y < worldMapHeight; y++)
            {
                // Texture-row y corresponds to world-Y (worldMapHeight - 1 - y) because the
                // climate-write loop above stored row (worldMapHeight - 1 - y) for world-Y y.
                // HasLocation expects world-space coords, so flip back here to query it.
                int worldY = worldMapHeight - 1 - y;
                for (int x = 0; x < worldMapWidth; x++)
                {
                    // Get sample tile data
                    int climateIndex = climateMap[y * worldMapWidth + x];

                    // B channel = "is there a location here?" marker (non-zero -> yes).
                    //   Kept as a small positive byte rather than just 1 so the shader's
                    //   pre-existing "* _MaxIndex" unscaling pattern (used for R-channel
                    //   climate index) stays numerically uniform across channels.
                    // A channel = compact LocationType category (1..6); see
                    //   MapLocationTypeToCategory below for the mapping. 0 means "no
                    //   location" (matches B == 0) and bypasses the shader's beacon
                    //   branch entirely.
                    byte locationMarker = 0;
                    byte locationCategory = 0;

                    // G channel = per-pixel region treatment index (see DistantTerrainRegionConfig).
                    // DFU stores region as politicIndex - 128 (matches GetRegionName indexing).
                    byte regionTreatmentByte = 0;
                    int politicIndex = dfUnity.ContentReader.MapFileReader.GetPoliticIndex(x, worldY);
                    int regionIndex = politicIndex - 128;
                    if (regionIndex >= 0 && regionIndex < regionTreatment.Length)
                        regionTreatmentByte = regionTreatment[regionIndex];

                    if (DistantTerrainLocationConfig.HighlightLocations)
                    {
                        // MapSummary is a struct nested inside ContentReader; HasLocation lives
                        // on ContentReader itself (not on MapFileReader). This is the canonical
                        // DFU API path used by DaggerfallTravelMapWindow, TerrainHelper, etc.
                        ContentReader.MapSummary summary;
                        if (dfUnity.ContentReader.HasLocation(x, worldY, out summary))
                        {
                            locationMarker = 4;
                            locationCategory = MapLocationTypeToCategory(summary.LocationType);
                        }
                    }

                    // Assign to tileMap
                    tileColor.r = Convert.ToByte(climateIndex);
                    tileColor.g = regionTreatmentByte;
                    tileColor.b = locationMarker;
                    tileColor.a = locationCategory;
                    terrainInfoTileMap[y * terrainInfoTileMapDim + x] = tileColor;
                }
            }

            textureTerrainInfoTileMap = new Texture2D(terrainInfoTileMapDim, terrainInfoTileMapDim, TextureFormat.RGBA32, false, true);
            textureTerrainInfoTileMap.filterMode = FilterMode.Point;
            textureTerrainInfoTileMap.wrapMode = TextureWrapMode.Clamp;

            // Promote tileMap
            textureTerrainInfoTileMap.SetPixels32(terrainInfoTileMap);
            // MOBILE: Apply(updateMipmaps: false, makeNoLongerReadable: TRUE), and drop the staging
            // array with it. Nothing reads either copy back: terrainInfoTileMap is written here and
            // never sampled again, and the texture is a shader input only. Upstream's Apply(false)
            // keeps the 4.2 MB CPU-side copy of a 1024^2 RGBA32 texture alive for the session on top
            // of the 4.2 MB Color32[] - 8.4 MB, in the one file whose whole argument is memory.
            textureTerrainInfoTileMap.Apply(false, true);
            terrainInfoTileMap = null;

            terrainMaterial.SetTexture("_MainTex", textureTerrainInfoTileMap);
            terrainMaterial.SetTexture("_FarTerrainTilemapTex", textureTerrainInfoTileMap);
            terrainMaterial.SetInt("_FarTerrainTilemapDim", terrainInfoTileMapDim);
            terrainMaterial.mainTexture = textureTerrainInfoTileMap;

            // Push the highlight-locations display gate to the shader. The B/A channels were
            // already zeroed for tiles without a location, so the shader's beacon branch
            // would naturally skip them; this uniform is a belt-and-braces second gate so
            // a user toggling the feature off in mod settings (next world load) sees the
            // location-pixels go dark even if any non-zero markers somehow leaked through.
            //
            // The gate combines the master switch with the live in-game RuntimeVisible flag,
            // so if the player had toggled markers off, a freshly (re)built LOD terrain comes
            // back up matching that choice rather than flashing on. Update()'s cached per-frame
            // push then keeps it tracking the hotkey; seed the cache here so that push sees the
            // fresh material as already in-sync and only fires again when the toggle changes.
            int highlightGate =
                (DistantTerrainLocationConfig.HighlightLocations && DistantTerrainLocationConfig.RuntimeVisible) ? 1 : 0;
            terrainMaterial.SetInt("_HighlightLocations", highlightGate);
            _cachedHighlightLocations = highlightGate;

            // MOBILE: one line for the whole world-entry cost, split into the five stages that can
            // each be slow for a different reason: sampling WOODS.WLD, carving the deriv map,
            // parsing the three mountain CSVs and lifting their peaks, baking the climate /
            // region / location tilemap, and packing the three tile arrays. Task 8 documents them;
            // Task 9 and the device run read them. The arrays stage reads 0 on the second and later
            // world entries of a session - they are packed once and kept.
            tilemapWatch.Stop();
            total.Stop();
            // MOBILE: `other` is the total minus the five named stages - the TerrainData allocation,
            // the reparent, the camera and material setup, and anything a future edit adds without
            // a stage of its own. Without it the five figures can add up to well under the total and
            // there is no way to tell an unmeasured cost from a rounding artefact. Clamped at zero:
            // the stages are five independent stopwatches and their sum can round a millisecond past
            // the total, which would otherwise print a negative and read as a bug in the timing.
            long totalMs = (long)total.Elapsed.TotalMilliseconds;
            long tilemapMs = (long)tilemapWatch.Elapsed.TotalMilliseconds;
            long otherMs = totalMs - ((long)lastHeightmapMs + (long)lastCarveMs + (long)lastLiftsMs
                                      + tilemapMs + (long)lastArraysMs);
            if (otherMs < 0) otherMs = 0;
            Debug.Log(string.Format(
                "[DistantTerrain] far terrain built in {0} ms (heightmap {1} ms, carve {2} ms, lifts {3} ms, tilemap {4} ms, arrays {5} ms, other {6} ms)",
                totalMs,
                (long)lastHeightmapMs,
                (long)lastCarveMs,
                (long)lastLiftsMs,
                tilemapMs,
                (long)lastArraysMs,
                otherMs));

            // MOBILE: the far terrain is on screen. This is the only place Installed becomes true,
            // and it is the last line of the build - a throw or a refusal above never reaches it.
            DistantTerrainPort.MarkInstalled();
            Debug.Log("[DistantTerrain] far terrain ready");
            LogFarTerrainDiagnostic();
            return true;
        }

        /// <summary>
        /// MOBILE: one shot, right after "far terrain ready", and the answer to Task 9's Finding 1 -
        /// the far terrain built on every launch and nothing of it reached the screen, and every
        /// log line the port already emitted said the build was fine. The four candidates that
        /// survived that investigation are all read here: the stacked camera's near clip (a value
        /// that clips the whole ring), the terrain's position and vertical scale (a mesh sitting
        /// below sea level), its layer against the camera's culling mask, and the camera's
        /// targetTexture (rendering somewhere the frame buffer never shows). The material and the
        /// shader are named too, because "the shader compiles" is not "the shader shades".
        /// Everything is a field read; nothing here allocates or costs a frame.
        /// </summary>
        void LogFarTerrainDiagnostic()
        {
            if (farTerrainDiagnosticLogged) return;
            farTerrainDiagnosticLogged = true;
            try
            {
                Vector3 pos = worldTerrainGameObject != null ? worldTerrainGameObject.transform.position : Vector3.zero;
                TerrainData data = terrain != null ? terrain.terrainData : null;
                Vector3 size = data != null ? data.size : Vector3.zero;
                RenderTexture target = stackedCamera != null ? stackedCamera.targetTexture : null;
                Camera main = Camera.main;
                // MOBILE: activeInHierarchy and the parent's name, because "the Terrain component is
                // enabled" is not "the object renders": the far terrain is reparented under
                // /Exterior, and an inactive parent - or no reparent at all - draws nothing while
                // every other field on this line still reads healthy. They are the two fields the
                // Task 9 / diag round wanted first and had to add by hand.
                Transform parent = worldTerrainGameObject != null ? worldTerrainGameObject.transform.parent : null;
                Debug.Log(string.Format(
                    "[DistantTerrain] far terrain: pos={0:F1},{1:F1},{2:F1} size={3:F1},{4:F1},{5:F1} heightScale={6:F1} " +
                    "layer={7} stackedCamera mask={8} near={9} far={10} depth={11} targetTexture={12} main.far={13} " +
                    "shader={14} supported={15} material={16} renderer={17} drawHeightmap={18} " +
                    "activeInHierarchy={19} parent={20}",
                    pos.x, pos.y, pos.z,
                    size.x, size.y, size.z,
                    size.y,
                    worldTerrainGameObject != null ? LayerMask.LayerToName(worldTerrainGameObject.layer) : "null",
                    stackedCamera != null ? stackedCamera.cullingMask : 0,
                    stackedCamera != null ? stackedCamera.nearClipPlane : 0f,
                    stackedCamera != null ? stackedCamera.farClipPlane : 0f,
                    stackedCamera != null ? stackedCamera.depth : 0f,
                    target != null ? target.name : "null",
                    main != null ? main.farClipPlane : 0f,
                    shaderDistantTerrainTilemap != null ? shaderDistantTerrainTilemap.name : "null",
                    shaderDistantTerrainTilemap != null && shaderDistantTerrainTilemap.isSupported,
                    terrainMaterial != null ? terrainMaterial.name : "null",
                    terrain != null && terrain.enabled,
                    terrain != null && terrain.drawHeightmap,
                    worldTerrainGameObject != null && worldTerrainGameObject.activeInHierarchy,
                    parent != null ? parent.name : "none"));
            }
            catch (Exception ex)
            {
                // A diagnostic must never be the thing that breaks the build it is describing.
                Debug.LogWarning("[DistantTerrain] far terrain: diagnostic unavailable (" + ex.Message + ")");
            }
        }

        /// <summary>
        /// MOBILE: undoes a failed or refused build. Everything created by SetupGameObjects and
        /// GenerateWorldTerrain goes, and the main camera gets back the far clip plane and clear
        /// flags it had before this port touched them - without that the player is left looking
        /// through a depth-only camera clipped at 15,000 units with nothing drawing the distance.
        /// </summary>
        void TearDownFarTerrain()
        {
            // MOBILE: destroying the GameObject destroys the Terrain COMPONENT, not the TerrainData
            // behind it (a free-standing UnityEngine.Object holding the 1025^2 heightmap and Unity's
            // heightmap texture, ~4-6 MB), so it goes first, while `terrain` still points at it.
            // Same for the runtime Material and the 1024^2 readable tilemap Texture2D (~8.4 MB, GPU
            // plus CPU copy) - nulling the fields would drop the only references and leak them for
            // the session. See TeardownDestroys / TeardownNulls, which the self-test reads.
            if (terrain != null && terrain.terrainData != null)
                Destroy(terrain.terrainData);
            if (terrainMaterial != null)
                Destroy(terrainMaterial);
            if (textureTerrainInfoTileMap != null)
                Destroy(textureTerrainInfoTileMap);
            textureTerrainInfoTileMap = null;
            // 4.2 MB of managed Color32, held alive by nothing else. A completed build already
            // dropped it at the upload; this covers a build that threw between the allocation and
            // the upload, which is the case the teardown exists for.
            terrainInfoTileMap = null;

            if (worldTerrainGameObject != null)
            {
                Destroy(worldTerrainGameObject);
                worldTerrainGameObject = null;
            }
            terrain = null;
            terrainMaterial = null;
            worldHeights = null;
            baseWorldHeights = null;
            preDerivWorldHeights = null;
            oceanMask = null;
            mountainLifted = null;
            if (mountainDefs != null) mountainDefs.Clear();

            if (stackedCamera != null)
            {
                Destroy(stackedCamera.gameObject);
                stackedCamera = null;
            }
            if (goRenderSkyboxToTexture != null)
            {
                Destroy(goRenderSkyboxToTexture);
                goRenderSkyboxToTexture = null;
                cameraRenderSkyboxToTexture = null;
            }
            if (renderTextureSky != null)
            {
                renderTextureSky.Release();
                Destroy(renderTextureSky);
                renderTextureSky = null;
            }

            DestroyTileArrays();

            if (mainCameraSaved && Camera.main != null)
            {
                Camera.main.farClipPlane = savedMainCameraFarClipPlane;
                Camera.main.clearFlags = savedMainCameraClearFlags;
            }
            mainCameraSaved = false;

            // MOBILE: the flags the build sets, cleared the way the build sets them. Without this a
            // failed or refused build leaves DistantTerrainPort.Running true with no stacked camera
            // in the scene, and MobilePortedMods' sky poll - which waits for that camera whenever
            // Running is true - is stranded for the rest of the session, so Dynamic Skies never
            // starts on exactly the launch where there is no far terrain left to wait for.
            DistantTerrainPort.MarkStopped();
        }

        /// <summary>MOBILE: releases the three packed arrays (~15 MB of GPU memory this port owns).</summary>
        void DestroyTileArrays()
        {
            if (tileArraySummer != null) Destroy(tileArraySummer);
            if (tileArrayWinter != null) Destroy(tileArrayWinter);
            if (tileArrayRain != null) Destroy(tileArrayRain);
            tileArraySummer = null;
            tileArrayWinter = null;
            tileArrayRain = null;
        }

        /// <summary>
        /// Compresses the sparse DFRegion.LocationTypes enum (values up to 135 with gaps)
        /// into a compact 1..6 category byte for the shader. Returns 0 for unrecognized
        /// types so any future LocationType additions degrade gracefully (the shader's
        /// beacon branch defaults to the "city gold" color when category is 1 and uses
        /// other colors only on explicit match, so an unknown 0 just skips the beacon).
        ///
        /// Categories (kept in sync with the if-chain in FarTerrainCommon.cginc's
        /// updateColorWithInfoForTreeCoverageAndLocations):
        ///   1 = TownCity                     -> warm gold
        ///   2 = TownHamlet, TownVillage      -> amber
        ///   3 = HomeFarms, HomeWealthy,
        ///       HomePoor, Tavern             -> green
        ///   4 = ReligionTemple, ReligionCult,
        ///       Coven                        -> pale blue
        ///   5 = Graveyard                   -> violet
        ///   6 = DungeonLabyrinth,
        ///       DungeonKeep, DungeonRuin     -> red
        /// </summary>
        private static byte MapLocationTypeToCategory(DFRegion.LocationTypes locationType)
        {
            switch (locationType)
            {
                case DFRegion.LocationTypes.TownCity:
                    return 1;

                case DFRegion.LocationTypes.TownHamlet:
                case DFRegion.LocationTypes.TownVillage:
                    return 2;

                case DFRegion.LocationTypes.HomeFarms:
                case DFRegion.LocationTypes.HomeWealthy:
                case DFRegion.LocationTypes.HomePoor:
                case DFRegion.LocationTypes.Tavern:
                    return 3;

                case DFRegion.LocationTypes.ReligionTemple:
                case DFRegion.LocationTypes.ReligionCult:
                case DFRegion.LocationTypes.Coven:
                    return 4;

                case DFRegion.LocationTypes.Graveyard:
                    return 5;

                case DFRegion.LocationTypes.DungeonLabyrinth:
                case DFRegion.LocationTypes.DungeonKeep:
                case DFRegion.LocationTypes.DungeonRuin:
                    return 6;

                default:
                    return 0;
            }
        }

        void Start()
        {
            SetUpCameras();

            if (worldTerrainGameObject == null) // lazy creation
            {
                if (!ReadyCheck())
                    return;

                if (!dfUnity.MaterialReader.IsReady)
                    return;

                SetupGameObjects();
            }


            // MOBILE: was five unguarded writes through GameManager.Instance.WeatherManager. Start()
            // runs at the title on this build, where there is no WeatherManager to write to, so the
            // push moved into a helper that is retried at world entry and says once, in the log, that
            // this mod - not the weather system or Dynamic Skies - owns these five fog settings.
            ApplyFogSettings();
        }

        /// <summary>
        /// MOBILE: upstream's Start()-time overwrite of WeatherManager's five fog settings, kept
        /// (it is what makes the distant terrain fade into fog rather than end in a hard line), but
        /// idempotent, null-safe and announced. Called from Start() and again from InitFarTerrain,
        /// because on this build Start() happens before the WeatherManager exists.
        /// </summary>
        void ApplyFogSettings()
        {
            if (fogApplied)
                return;
            WeatherManager wm = GameManager.HasInstance ? GameManager.Instance.WeatherManager : null;
            if (wm == null)
                return;

            wm.SunnyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Exponential, density = FogConfig.SunnyFogDensity, startDistance = 0, endDistance = 0, excludeSkybox = true };
            wm.OvercastFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Exponential, density = FogConfig.OvercastFogDensity, startDistance = 0, endDistance = 0, excludeSkybox = true };
            wm.RainyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Exponential, density = FogConfig.RainyFogDensity, startDistance = 0, endDistance = 0, excludeSkybox = true };
            wm.SnowyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Exponential, density = FogConfig.SnowyFogDensity, startDistance = 0, endDistance = 0, excludeSkybox = true };
            wm.HeavyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Exponential, density = FogConfig.HeavyFogDensity, startDistance = 0, endDistance = 0, excludeSkybox = false };

            fogApplied = true;
            Debug.Log(string.Format(
                "[DistantTerrain] fog settings overwritten -- sunny {0}, overcast {1}, rainy {2}, snowy {3}, heavy {4}",
                FogConfig.SunnyFogDensity, FogConfig.OvercastFogDensity, FogConfig.RainyFogDensity,
                FogConfig.SnowyFogDensity, FogConfig.HeavyFogDensity));
        }

        void OnDestroy()
        {
            // MOBILE: upstream only dropped the references here. The TerrainData, the runtime
            // Material and the 1024^2 readable tilemap texture are free-standing UnityEngine.Objects
            // that outlive their fields, so they are destroyed for the same reason the tile arrays
            // below are - see TearDownFarTerrain, which owes the same debt on the failure path.
            if (terrain != null && terrain.terrainData != null)
                Destroy(terrain.terrainData);
            if (terrainMaterial != null)
                Destroy(terrainMaterial);
            if (textureTerrainInfoTileMap != null)
                Destroy(textureTerrainInfoTileMap);
            terrain = null;

            worldHeights = null;
            worldTerrainGameObject = null;
            terrainMaterial = null;
            terrainInfoTileMap = null;
            textureTerrainInfoTileMap = null;

            // MOBILE: the three arrays are ~15 MB of GPU memory this component allocated itself
            // (the twelve atlases they replace were owned by TextureReader's cache and only
            // dereferenced here), so they are destroyed, not just dropped.
            DestroyTileArrays();

            // Drop the dynamic-mountain state too; these can be several MB combined
            // (worldMapResolution^2 floats + bools), so let the GC reclaim them
            // rather than holding refs alive if the component is destroyed.
            baseWorldHeights = null;
            oceanMask = null;
            preDerivWorldHeights = null;
            mountainLifted = null;
            if (mountainDefs != null) mountainDefs.Clear();
            cachedPlayerHmX = int.MinValue;
            cachedPlayerHmY = int.MinValue;
        }

        void TransitionToExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            SetUpCameras();
        }

        void OnLoadEvent(SaveData_v1 saveData)
        {
            SetUpCameras();
        }

        void SetupGameObjects()
        {
            // MOBILE: nothing is created until the world exists. Upstream ran this from Awake, which
            // on this build happens on the TITLE screen: it would have re-clipped the title camera,
            // parented a stacked camera and a skybox render-to-texture camera to it, and left
            // MobilePortedMods' "is the stacked camera up" poll answering yes before there was any
            // terrain to render. The far terrain is built at StreamingWorld.OnReady; the cameras
            // appear with it.
            if (!TryResolveSceneRefs(false))
                return;

            // prevent NullReferenceException on application close
            if (Camera.main == null)
                return;

            // MOBILE: the reach dials are re-read here (like the camera depths below) so a settings
            // change lands on the next transition / save load, and blendStart stays derived from
            // blendEnd - the shader's fade band inverts if it does not.
            mainCameraFarClipPlane = DistantTerrainRenderConfig.MainCameraFarClipPlane;
            blendEnd = DistantTerrainRenderConfig.BlendEnd;
            blendStart = DistantTerrainPort.BlendStartFor(blendEnd);

            // MOBILE: remembered once, before the first change, so a failed build can restore it.
            if (!mainCameraSaved)
            {
                savedMainCameraFarClipPlane = Camera.main.farClipPlane;
                savedMainCameraClearFlags = Camera.main.clearFlags;
                mainCameraSaved = true;
            }

            // Set main camera settings
            Camera.main.farClipPlane = mainCameraFarClipPlane;

            if (!stackedCamera)
            {
                GameObject goStackedCamera = new GameObject("stackedCamera");
                stackedCamera = goStackedCamera.AddComponent<Camera>();
                stackedCamera.gameObject.AddComponent<CloneCameraRotationFromMainCamera>();
                stackedCamera.gameObject.AddComponent<CloneCameraPositionFromMainCamera>();
                stackedCamera.transform.SetParent(this.transform);
            }

            stackedCamera.cullingMask = (1 << layerWorldTerrain) + (1 << LayerMask.NameToLayer("Water"));
            stackedCamera.fieldOfView = Camera.main.fieldOfView;
            // Adapt the near clip plane to the current TerrainDistance + FOV (see
            // ComputeSafeNearClipPlane). The configured nearClipPlaneStackedCamera value acts as
            // an upper bound, so behaviour at high TerrainDistance settings is unchanged.
            stackedCamera.nearClipPlane = ComputeSafeNearClipPlane();
            stackedCamera.farClipPlane = blendEnd;
            stackedCamera.renderingPath = Camera.main.renderingPath;
            stackedCamera.allowHDR = Camera.main.allowHDR;
            stackedCamera.allowMSAA = Camera.main.allowMSAA;

            Camera.main.farClipPlane = mainCameraFarClipPlane;

            if (!renderTextureSky)
            {
                renderTextureSky = new RenderTexture(renderTextureSkyWidth, renderTextureSkyHeight, renderTextureSkyDepth, renderTextureSkyFormat);
            }

            if (!goRenderSkyboxToTexture)
            {
                goRenderSkyboxToTexture = new GameObject("stackedCameraSkyboxRenderToTextureGeneric", typeof(Camera));
                goRenderSkyboxToTexture.transform.SetParent(this.transform);
            }

            if (!cameraRenderSkyboxToTexture)
            {
                cameraRenderSkyboxToTexture = goRenderSkyboxToTexture.GetComponent<Camera>();

                goRenderSkyboxToTexture.AddComponent<CloneCameraRotationFromMainCamera>();
                goRenderSkyboxToTexture.AddComponent<RenderSkyboxWithoutSun>();

                cameraRenderSkyboxToTexture.clearFlags = CameraClearFlags.Skybox;
                cameraRenderSkyboxToTexture.cullingMask = 0; // nothing
                cameraRenderSkyboxToTexture.nearClipPlane = Camera.main.nearClipPlane;
                cameraRenderSkyboxToTexture.farClipPlane = Camera.main.farClipPlane;
                cameraRenderSkyboxToTexture.fieldOfView = Camera.main.fieldOfView;
            }
            cameraRenderSkyboxToTexture.fieldOfView = Camera.main.fieldOfView;
        }

        void SetUpCameras()
        {
            // Ensure these are setup first or SetUpCameras() will barf
            SetupGameObjects();

            if (Camera.main == null)
                return;

            // MOBILE: and SetupGameObjects itself now declines before the world exists (title
            // screen), so the cameras it would have made are not there to configure. Upstream could
            // assume they were; this port is started at the title, where dereferencing them is a
            // NullReferenceException in Start() on every launch with the entry switched on.
            if (stackedCamera == null || cameraRenderSkyboxToTexture == null)
                return;

            // Pull camera stacking depths from mod settings (populated by DistantTerrainModSettings.Init).
            // Doing this here (rather than once in Awake) means any later setting change will be
            // picked up the next time SetUpCameras() runs (transitions, save loads, retro toggle).
            cameraRenderSkyboxToTextureDepth = DistantTerrainCameraConfig.SkyboxCameraDepth;
            stackedCameraDepth = DistantTerrainCameraConfig.DistantTerrainCameraDepth;
            mainCameraDepth = DistantTerrainCameraConfig.MainCameraDepth;

            Camera.main.clearFlags = CameraClearFlags.Depth;
            stackedCamera.clearFlags = CameraClearFlags.Depth;
            stackedCamera.depth = stackedCameraDepth; // rendered first
            Camera.main.depth = mainCameraDepth; // renders over stacked camera

            cameraRenderSkyboxToTexture.depth = cameraRenderSkyboxToTextureDepth; // make sure to render first
            cameraRenderSkyboxToTexture.renderingPath = Camera.main.renderingPath;
            cameraRenderSkyboxToTexture.targetTexture = renderTextureSky;

            stackedCamera.targetTexture = Camera.main.targetTexture;
        }

        void setMaterialFogParameters(ref Material terrainMaterial)
        {
            if (terrainMaterial != null)
            {
                if (RenderSettings.fog == true)
                {
                    if (RenderSettings.fogMode == FogMode.Linear)
                    {
                        terrainMaterial.SetInt("_FogMode", 1);
                        terrainMaterial.SetFloat("_FogStartDistance", RenderSettings.fogStartDistance);
                        terrainMaterial.SetFloat("_FogEndDistance", RenderSettings.fogEndDistance);
                    }
                    else if (RenderSettings.fogMode == FogMode.Exponential)
                    {
                        terrainMaterial.SetInt("_FogMode", 2);
                        terrainMaterial.SetFloat("_FogDensity", RenderSettings.fogDensity);
                    }
                    else if (RenderSettings.fogMode == FogMode.ExponentialSquared)
                    {
                        terrainMaterial.SetInt("_FogMode", 3);
                        terrainMaterial.SetFloat("_FogDensity", RenderSettings.fogDensity);
                    }
                }
                else
                {
                    terrainMaterial.SetInt("_FogMode", 0);
                }
            }
        }

        // --- Per-frame uniform push helpers ---
        // These mirror the relevant subset of setMaterialFogParameters / direct Set* calls
        // but skip the push when the value matches what was last pushed. setMaterialFogParameters
        // itself is left untouched so cold-path callers (init in GenerateWorldTerrain) keep
        // their straightforward semantics; only the per-frame caller in Update uses these.

        private static void pushIntCached(Material mat, string name, int value, ref int cache)
        {
            if (cache != value)
            {
                mat.SetInt(name, value);
                cache = value;
            }
        }

        private static void pushFloatCached(Material mat, string name, float value, ref float cache)
        {
            // NaN != NaN, so a cache initialized to float.NaN guarantees the first push fires.
            if (cache != value)
            {
                mat.SetFloat(name, value);
                cache = value;
            }
        }

        private void pushFogParametersCached(Material mat)
        {
            if (mat == null) return;

            if (RenderSettings.fog)
            {
                if (RenderSettings.fogMode == FogMode.Linear)
                {
                    pushIntCached(mat, "_FogMode", 1, ref _cachedFogMode);
                    pushFloatCached(mat, "_FogStartDistance", RenderSettings.fogStartDistance, ref _cachedFogStartDistance);
                    pushFloatCached(mat, "_FogEndDistance", RenderSettings.fogEndDistance, ref _cachedFogEndDistance);
                }
                else if (RenderSettings.fogMode == FogMode.Exponential)
                {
                    pushIntCached(mat, "_FogMode", 2, ref _cachedFogMode);
                    pushFloatCached(mat, "_FogDensity", RenderSettings.fogDensity, ref _cachedFogDensity);
                }
                else if (RenderSettings.fogMode == FogMode.ExponentialSquared)
                {
                    pushIntCached(mat, "_FogMode", 3, ref _cachedFogMode);
                    pushFloatCached(mat, "_FogDensity", RenderSettings.fogDensity, ref _cachedFogDensity);
                }
            }
            else
            {
                pushIntCached(mat, "_FogMode", 0, ref _cachedFogMode);
            }
        }

        // Builds the _RegionData[8] uniform (rgb = colour tint, w = snow mode) from
        // DistantTerrainRegionConfig.Treatments and pushes it once. The per-pixel treatment index is
        // baked into the tilemap GREEN channel (see InitFarTerrain / BuildRegionTreatmentMap); the
        // shader reads the index and looks the tint + mode up in this array.
        private void PushRegionData(Material mat)
        {
            if (mat == null) return;
            float[][] t = DistantTerrainRegionConfig.Treatments;
            for (int i = 0; i < _regionDataScratch.Length; i++)
            {
                if (t != null && i < t.Length && t[i] != null && t[i].Length >= 4)
                    _regionDataScratch[i] = new Vector4(t[i][0], t[i][1], t[i][2], t[i][3]);
                else
                    _regionDataScratch[i] = new Vector4(1f, 1f, 1f, 0f); // neutral / original
            }
            mat.SetVectorArray("_RegionData", _regionDataScratch);
        }

        // Builds a region-index -> treatment-index byte map by matching each region's name (via
        // GetRegionName) against DistantTerrainRegionConfig.TreatmentForRegion. Used by InitFarTerrain
        // to bake the per-pixel treatment into the tilemap. Robust to region-index reordering since it
        // keys off names, not hard-coded indices.
        private byte[] BuildRegionTreatmentMap()
        {
            int regionCount = dfUnity.ContentReader.MapFileReader.RegionCount;
            byte[] map = new byte[Mathf.Max(0, regionCount)];
            for (int r = 0; r < map.Length; r++)
            {
                string name = dfUnity.ContentReader.MapFileReader.GetRegionName(r);
                map[r] = (byte)DistantTerrainRegionConfig.TreatmentForRegion(name);
            }
            return map;
        }

        // Pushes the per-climate winter-snow disable flags to the material as _DisableSnow
        // (1 = keep the summer look this winter). Called from updateMaterialSeasonalTextures, so
        // it is refreshed at init and on every season change.
        private void PushDisableSnowArray(Material mat)
        {
            if (mat == null) return;
            _disableSnowScratch[0]  = 0f;                                                                  // 223 Ocean (snow N/A)
            _disableSnowScratch[1]  = DistantTerrainWinterSnowConfig.DisableSnowDesert ? 1f : 0f;          // 224
            _disableSnowScratch[2]  = DistantTerrainWinterSnowConfig.DisableSnowDesert2 ? 1f : 0f;         // 225
            _disableSnowScratch[3]  = DistantTerrainWinterSnowConfig.DisableSnowMountain ? 1f : 0f;        // 226
            _disableSnowScratch[4]  = DistantTerrainWinterSnowConfig.DisableSnowRainforest ? 1f : 0f;      // 227 (jungle)
            _disableSnowScratch[5]  = DistantTerrainWinterSnowConfig.DisableSnowSwamp ? 1f : 0f;           // 228
            _disableSnowScratch[6]  = DistantTerrainWinterSnowConfig.DisableSnowSubtropical ? 1f : 0f;     // 229
            _disableSnowScratch[7]  = DistantTerrainWinterSnowConfig.DisableSnowMountainWoods ? 1f : 0f;   // 230
            _disableSnowScratch[8]  = DistantTerrainWinterSnowConfig.DisableSnowWoodlands ? 1f : 0f;       // 231
            _disableSnowScratch[9]  = DistantTerrainWinterSnowConfig.DisableSnowHauntedWoodlands ? 1f : 0f;// 232
            _disableSnowScratch[10] = DistantTerrainWinterSnowConfig.DisableSnowMaquis ? 1f : 0f;          // 233
            mat.SetFloatArray("_DisableSnow", _disableSnowScratch);
        }

        // Pushes the world-Y snow line (_SnowCapStartY / _SnowCapFullY) to the material, converted
        // from the normalized DistantTerrainSnowCapConfig fractions via the SAME transform + offset as
        // _WaterHeightTransformed so the shader can compare them directly against a fragment's true
        // world Y. Called from every site that pushes _WaterHeightTransformed (init + per-frame
        // position update) so the line stays correct as the floating origin re-bases.
        private void PushSnowlineHeights(Transform terrainTransform)
        {
            if (terrainMaterial == null || terrainTransform == null)
                return;

            // Local-space Y for a normalized height h is h * MaxTerrainHeight * TerrainScale (the
            // terrain's vertical size); mirrors how worldHeights are normalized in GenerateWorldTerrain.
            float scaleY = dfUnity.TerrainSampler.MaxTerrainHeight * streamingWorld.TerrainScale;
            terrainMaterial.SetFloat("_SnowCapStartY", TransformedSnowY(terrainTransform, DistantTerrainSnowCapConfig.StartFraction * scaleY));
            terrainMaterial.SetFloat("_SnowCapFullY",  TransformedSnowY(terrainTransform, DistantTerrainSnowCapConfig.FullFraction  * scaleY));
            // Snow-line deviation amount as a world-Y magnitude (a delta, so no transform offset - the
            // far-terrain object's scale is 1, so a local-Y delta equals a world-Y delta).
            terrainMaterial.SetFloat("_SnowCapNoiseY", DistantTerrainSnowCapConfig.NoiseFraction * scaleY);
        }

        // Transforms a local-space terrain height (already in world units, i.e.
        // normalizedHeight * MaxTerrainHeight * TerrainScale) into the same world-Y space used for
        // _WaterHeightTransformed: transform to world coordinates, then subtract extraWaterTranslationY.
        private float TransformedSnowY(Transform terrainTransform, float localHeightY)
        {
            Vector3 v = new Vector3(0.0f, localHeightY, 0.0f);
            return terrainTransform.TransformPoint(v).y - extraWaterTranslationY;
        }

        // Keeps the far-terrain (stacked) camera's FOV in sync with the main camera every
        // frame, and recomputes its near clip plane to match the current TerrainDistance/FOV.
        // Without this, a runtime FOV change (settings menu, sprint, etc.) leaves the stacked
        // camera rendering a narrower cone than the main camera — visible as a rectangular
        // hole at the periphery of the view where far terrain should be.
        void SyncStackedCameraToMain()
        {
            if (Camera.main == null || stackedCamera == null)
                return;

            float mainFov = Camera.main.fieldOfView;
            if (!Mathf.Approximately(stackedCamera.fieldOfView, mainFov))
            {
                stackedCamera.fieldOfView = mainFov;
                if (cameraRenderSkyboxToTexture != null)
                    cameraRenderSkyboxToTexture.fieldOfView = mainFov;
            }

            stackedCamera.nearClipPlane = ComputeSafeNearClipPlane();
        }

        // Computes a near clip plane for the stacked camera that's small enough to keep the
        // inner edge of the "far-terrain only" region visible at the worst-case frustum corner.
        //
        // The far-terrain shader discards tiles within streamingWorld.TerrainDistance map pixels
        // of the player (DistantTerrainTilemap.shader cutout). Near terrain extends
        // streamingWorld.TerrainDistance tiles in each direction, so the inner edge of the
        // far-terrain ring sits roughly (N + 0.5) map pixels perpendicular distance from the
        // player. The fixed default of 980 is far larger than that perpendicular distance projects
        // to in forward-Z at the frustum corner when TerrainDistance is small or FOV is high —
        // which is what was producing the blue rectangular clip at the periphery.
        private float ComputeSafeNearClipPlane()
        {
            if (streamingWorld == null || Camera.main == null)
                return nearClipPlaneStackedCamera;

            // When the near-terrain cutout is disabled (upstream held it OFF during a fly-map
            // teleport load or high-speed movement; MOBILE: with the fly-map dropped nothing sets
            // disableTerrainCutout, so this branch is dormant), the far terrain must be drawn
            // all the way up to the camera so it covers the player's OWN map pixel until the near
            // terrain finishes streaming. The adapted near clip computed below otherwise clips the
            // far terrain out to ~(TerrainDistance + 0.5) map pixels of perpendicular distance -
            // roughly one map pixel of forward distance directly in front of the player. With the
            // shader cutout already held off (so the rest of the would-be-near-terrain area shows
            // the LOD backdrop), this leftover near-plane clip is what punches the single-tile,
            // sky-coloured void seen under the player's feet right after a teleport. Collapsing the
            // near plane to the main camera's value removes that void; the brief depth-precision
            // cost is irrelevant for the sub-second teleport hitch, and this stays in lockstep with
            // the _DisableCutout shader uniform (both keyed off disableTerrainCutout).
            if (disableTerrainCutout)
                return Camera.main.nearClipPlane;

            int nearTerrainRadius = streamingWorld.TerrainDistance;
            float mapPixelSize = MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;
            float innerEdgePerp = (nearTerrainRadius + 0.5f) * mapPixelSize;

            // Worst-case corner of the frustum: Unity's fieldOfView is vertical FOV, so
            // tan(corner_from_axis) = tan(vFov/2) * sqrt(1 + aspect^2). A point at perpendicular
            // distance d projects to forward distance d * cos(corner_angle) at that worst corner.
            float vFov = Camera.main.fieldOfView;
            float aspect = Camera.main.aspect;
            float tanHalfV = Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad);
            float tanCorner = tanHalfV * Mathf.Sqrt(1.0f + aspect * aspect);
            float cosCorner = 1.0f / Mathf.Sqrt(1.0f + tanCorner * tanCorner);

            // 0.5 safety factor accounts for the player not being centered in their tile.
            float safeNearClip = innerEdgePerp * cosCorner * 0.5f;

            // Floor of 5 keeps depth-buffer precision sane; ceiling preserves the original
            // configured value as an upper bound, so high TerrainDistance settings are unchanged.
            return Mathf.Clamp(safeNearClip, 5.0f, nearClipPlaneStackedCamera);
        }

        void Update()
        {
            bool doSeasonalTexturesUpdate = shouldUpdateSeasonalTextures();
            if (worldTerrainGameObject != null)
            {
                // Handle moving to new map pixel or first-time init
                DFPosition curMapPixel = playerGPS.CurrentMapPixel;
                if (curMapPixel.X != MapPixelX ||
                    curMapPixel.Y != MapPixelY)
                {
                    UpdateWorldTerrain(curMapPixel);
                    MapPixelX = curMapPixel.X;
                    MapPixelY = curMapPixel.Y;
                }

                // (Was: a dead local 'terrain1' was re-fetched here every frame via
                //  GetComponent<Terrain>() and then never read - the body below uses
                //  this.terrain (assigned during InitFarTerrain). Removed.)
                if (terrain)
                {
                    // Cache materialTemplate once (it's a property getter and cheap, but
                    // there's no reason to dispatch it five times in a row).
                    Material mat = terrain.materialTemplate;

                    // Route all per-frame uniform pushes through the change-tracking
                    // helpers so a SetInt/SetFloat only fires when the value differs from
                    // the last frame's value. In practice almost none of these change
                    // frame-to-frame, so this collapses to ~zero managed/native crossings.
                    pushFogParametersCached(mat);
                    pushIntCached(mat, "_FogFromSkyTex", 0, ref _cachedFogFromSkyTex);
                    pushFloatCached(mat, "_BlendStart", blendStart, ref _cachedBlendStart);
                    pushFloatCached(mat, "_BlendEnd", blendEnd, ref _cachedBlendEnd);
                    pushIntCached(mat, "_DisableCutout", disableTerrainCutout ? 1 : 0, ref _cachedDisableCutout);
                    pushFloatCached(mat, "_SkirtDepth", farTerrainSkirtDepth, ref _cachedSkirtDepth);
                    // Live location-marker visibility: master switch AND the hotkey-driven show/hide
                    // state. Cached, so this is a no-op on the frames where the toggle hasn't changed.
                    pushIntCached(mat, "_HighlightLocations",
                        (DistantTerrainLocationConfig.HighlightLocations && DistantTerrainLocationConfig.RuntimeVisible) ? 1 : 0,
                        ref _cachedHighlightLocations);
                    // (Region tint/snow is baked per-pixel + pushed once via PushRegionData; nothing per-frame.)
                }

                if (doSeasonalTexturesUpdate)
                {
                    Material mat = terrain.materialTemplate;
                    updateMaterialSeasonalTextures(ref mat, currentSeason);
                    terrain.materialTemplate = mat;
                }

                // Distant rivers are baked into the heightmap, so (unlike snow, which the shader
                // resolves live per fragment) the per-climate winter-river opt-out must re-carve the
                // terrain when the calendar season crosses the winter boundary. Cheap gate: only when
                // the feature is enabled for some climate and the winter state actually flipped.
                if (AnyWinterRiverDisableSet())
                {
                    bool isWinterNow = dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
                    if (isWinterNow != lastWinterForRivers)
                        RebuildFarTerrainForSeasonRivers();
                }
            }

            if (DaggerfallUnity.Settings.RetroRenderingMode != lastRetroMode)
            {
                SetUpCameras();
                lastRetroMode = DaggerfallUnity.Settings.RetroRenderingMode;
            }

            // Keep the far-terrain (stacked) camera's FOV in lockstep with the main camera, and
            // recompute its adapted near clip plane every frame. This is what eliminates the
            // rectangular hole at the periphery of the view when TerrainDistance is small or the
            // FOV is high — the stacked camera's FOV is otherwise only set at setup time, and
            // its 980-unit default near plane is far too large when the far terrain begins less
            // than ~2 km from the player.
            SyncStackedCameraToMain();
        }

        void UpdateWorldTerrain()
        {
            UpdateWorldTerrain(playerGPS.CurrentMapPixel);
        }

        void UpdateWorldTerrain(DFPosition worldPos)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();   // MOBILE
            if (worldTerrainGameObject != null)
            {
                // NOTE: the near-terrain cutout center (_PlayerPosX/_PlayerPosY) and the mountain
                // visibility lifts are deliberately NOT advanced here. They are pushed in
                // WorldTerrainAfterTerrainsUpdated, hooked to StreamingWorld.OnUpdateTerrainsEnd,
                // once the near terrain for the new map pixel has finished loading. Repositioning
                // the far-terrain mesh stays immediate so the backdrop keeps tracking the player.
                Vector3 offset = new Vector3(0.0f, 0.0f, 0.0f);
                UpdatePositionWorldTerrain(ref worldTerrainGameObject, offset);

                bool doSeasonalTexturesUpdate = shouldUpdateSeasonalTextures();

                if (doSeasonalTexturesUpdate)
                {
                    Material mat = terrain.materialTemplate;
                    updateMaterialSeasonalTextures(ref mat, currentSeason);
                    terrain.materialTemplate = mat;
                }
            }

            // MOBILE: this is only half of a map-pixel cross. The other half - the mountain
            // visibility rebuild and its partial SetHeights, which is the expensive part - runs in
            // WorldTerrainAfterTerrainsUpdated once StreamingWorld has finished the near terrain, so
            // the cost is held here and reported there as one line.
            pendingMapPixelMs += watch.Elapsed.TotalMilliseconds;
            mapPixelUpdatePending = true;
        }

        // Advances the near-terrain cutout to the player's current map pixel ONLY after
        // StreamingWorld has finished rebuilding the near terrain (OnUpdateTerrainsEnd).
        //
        // The far-terrain shader discards every fragment within _TerrainDistance tiles of
        // (_PlayerPosX,_PlayerPosY); the near terrain covers one tile further out, so the two
        // overlap by exactly one tile. That ring is the only thing covering the height step
        // where near and far meet. When the player crosses into a new map pixel, StreamingWorld
        // rebuilds the near terrain over several frames (its UpdateTerrains coroutine: Job-system
        // heightmaps + WaitForEndOfFrame/WaitUntil yields, with edge tiles SetActive(false) until
        // their data lands). If the cutout center were moved the instant CurrentMapPixel flips,
        // the far terrain would be cut away at the new boundary while the near terrain there was
        // still loading - the overlap collapses to zero and, on uneven ground, a sky-colored
        // sliver shows through for those few frames. Deferring the cutout advance to here keeps
        // the far terrain authoritative over the boundary until the near terrain is actually
        // present, eliminating the seam. The mountain-visibility lifts use the same cutout
        // radius, so they are advanced here too to stay in lockstep.
        void WorldTerrainAfterTerrainsUpdated()
        {
            if (worldTerrainGameObject == null || terrain == null)
                return;

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();   // MOBILE

            terrain.materialTemplate.SetInt("_PlayerPosX", playerGPS.CurrentMapPixel.X);
            terrain.materialTemplate.SetInt("_PlayerPosY", playerGPS.CurrentMapPixel.Y);

            UpdateMountainVisibility();

            // MOBILE: one line per map-pixel cross, for the first ten crosses and then every
            // twenty-fifth - enough to see the shape of the cost on device without filling the log
            // on a long ride. This callback also fires for near-terrain updates that are not a
            // cross; those are not map-pixel updates and are not reported.
            double ms = pendingMapPixelMs + watch.Elapsed.TotalMilliseconds;
            pendingMapPixelMs = 0;
            if (!mapPixelUpdatePending)
                return;
            mapPixelUpdatePending = false;
            mapPixelUpdateCount++;
            if (ShouldLogMapPixelUpdate(mapPixelUpdateCount))
                Debug.Log(string.Format("[DistantTerrain] map-pixel update {0} ms", (long)ms));
        }

        #endregion

        #region Private Methods

        private void updateMaterialSeasonalTextures(ref Material terrainMaterial, ClimateSeason currentSeason)
        {

            // MOBILE: upstream rebound four atlas slots per season here. The three arrays are bound
            // once, at build time, and never reassigned - all a season change does now is move the
            // code the shader reads. Upstream's winter note still applies: every slot went to its
            // winter atlas and the per-CLIMATE snow opt-out was resolved per fragment from
            // _DisableSnow[] plus the always-bound summer set, which is what lets two climates that
            // share one tileset differ (Rainforest stays green while true Swamp freezes).
            switch (currentSeason)
            {
                case ClimateSeason.Winter:
                    terrainMaterial.SetInt("_TextureSetSeasonCode", 1);
                    break;
                case ClimateSeason.Rain:
                    terrainMaterial.SetInt("_TextureSetSeasonCode", 2);
                    break;
                case ClimateSeason.Summer:
                default:
                    terrainMaterial.SetInt("_TextureSetSeasonCode", 0);
                    break;
            }

            // Push the per-climate winter-snow disable flags (the shader consults them only in
            // winter). They never change at runtime, so setting them on every seasonal update is
            // cheap and guarantees the array is populated before the first render.
            PushDisableSnowArray(terrainMaterial);
        }

        private bool shouldUpdateSeasonalTextures()
        {
            if (!weatherManager)
                return false;

            ClimateSeason newSeason;

            // Get season and weather
            if (dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
            {
                newSeason = ClimateSeason.Winter;
            }
            else
            {
                newSeason = ClimateSeason.Summer;

                if (weatherManager.IsRaining)
                {
                    newSeason = ClimateSeason.Rain;
                }
                else if (weatherManager.IsSnowing)
                {
                    newSeason = ClimateSeason.Winter;
                }
            }

            if (newSeason != currentSeason)
            {
                currentSeason = newSeason;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void UpdatePositionWorldTerrain(ref GameObject terrainGameObject, Vector3 offset)
        {
            // world scale computed as in StreamingWorld.cs and DaggerfallTerrain.cs scripts
            float scale = MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;

            // get displacement in world map pixels
            float xdif = +1 - playerGPS.CurrentMapPixel.X;
            float zdif = worldMapHeight - 1 - playerGPS.CurrentMapPixel.Y;

            // world map level transform (for whole world map pixels)
            Vector3 worldMapLevelTransform;
            worldMapLevelTransform.x = xdif * scale;
            worldMapLevelTransform.y = extraTranslationY;
            worldMapLevelTransform.z = -zdif * scale;

            // local world level transform (for inter- world map pixels)
            float localTransformX = 0.0f;
            float localTransformZ = 0.0f;
            float localTransformY = 0.0f;

            localTransformX += streamingWorld.WorldCompensation.x;
            localTransformZ += streamingWorld.WorldCompensation.z;
            localTransformY += streamingWorld.WorldCompensation.y;

            float remainderX;
            if (offset.x != 0)
            {
                remainderX = playerGPS.transform.position.x - (float)Math.Floor((playerGPS.transform.position.x) / Math.Abs(offset.x)) * Math.Abs(offset.x);
            }
            else
            {
                remainderX = playerGPS.transform.position.x;
            }

            float remainderZ;
            if (offset.z != 0)
            {
                remainderZ = playerGPS.transform.position.z - (float)Math.Floor((playerGPS.transform.position.z) / Math.Abs(offset.z)) * Math.Abs(offset.z);
            }
            else
            {
                remainderZ = playerGPS.transform.position.z;
            }

            localTransformX += (float)Math.Floor((-streamingWorld.WorldCompensation.x + remainderX) / scale) * scale;
            localTransformZ += (float)Math.Floor((-streamingWorld.WorldCompensation.z + remainderZ) / scale) * scale;

            // compute composite transform and apply it to terrain object
            // compute composite transform and apply it to terrain object
            Vector3 finalTransform = new Vector3(worldMapLevelTransform.x + localTransformX, worldMapLevelTransform.y + localTransformY, worldMapLevelTransform.z + localTransformZ);

            // Apply the high-speed virtual offset
            finalTransform += virtualWorldOffset;

            terrainGameObject.gameObject.transform.localPosition = finalTransform;

            if (worldTerrainGameObject != null)
            {
                // update water height
                Vector3 vecWaterHeight = new Vector3(0.0f, (DaggerfallUnity.Instance.TerrainSampler.OceanElevation + 1.0f) * streamingWorld.TerrainScale, 0.0f); // water height level on y-axis (+1.0f some coastlines are incorrect otherwise)
                Vector3 vecWaterHeightTransformed = worldTerrainGameObject.transform.TransformPoint(vecWaterHeight); // transform to world coordinates
                terrainMaterial.SetFloat("_WaterHeightTransformed", vecWaterHeightTransformed.y - extraWaterTranslationY);

                // Keep the world-Y snow line in sync with the (floating-origin) transform.
                PushSnowlineHeights(worldTerrainGameObject.transform);
            }
        }
        // --- Mountain definitions: built once at world generation, never mutated by lifts ---
        // Used to bake mountain lifts directly into the world heightmap up front and have them
        // live there forever. That produced seams at the near-terrain boundary (see the long
        // comment by the mountainDefs field). Now we just parse the CSVs into a list of
        // MountainDef records here, and apply the lifts on demand in InitMountainVisibility /
        // RebuildHeightsForVisibilityChange depending on the player's current map pixel.
        private void BuildMountainDefinitions()
        {

            mountainDefs.Clear();

            int sizeY = worldHeights.GetLength(0);
            int sizeX = worldHeights.GetLength(1);

            // Define the files to process
            string[] csvFiles = { "Mountains.csv", "Mountains_Small.csv", "Mountains_Foothills.csv" };

            foreach (string fileName in csvFiles)
            {

                TextAsset csvAsset = DistantTerrainPort.Mod.GetAsset<TextAsset>(fileName);   // MOBILE: renamed loader
                if (csvAsset == null)
                {
                    DaggerfallUnity.LogMessage($"[DistantTerrain] Error: {fileName} not found in mod assets (is it flagged as a Mod Resource in the Mod Builder?)");
                    continue;
                }

                ProcessMountainCsvToDefs(fileName, csvAsset.text, sizeX, sizeY);
            }

            DaggerfallUnity.LogMessage(
                $"[DistantTerrain] Mountain defs built: {mountainDefs.Count} peaks (lifts applied dynamically based on player position).");
        }

        private void ProcessMountainCsvToDefs(string fileName, string content, int sizeX, int sizeY)
        {
            // Split on either line-ending style and drop empty rows. The original
            // File.ReadAllLines did similar normalization implicitly; this keeps parity.
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return; // Need at least a header and one row of data

            // 1. Dynamically parse the header row to find column indices
            string[] headers = lines[0].Split(',');
            int prefabIdx = -1, worldXIdx = -1, worldYIdx = -1, terrainXIdx = -1, terrainYIdx = -1, scaleIdx = -1;

            for (int c = 0; c < headers.Length; c++)
            {
                // Clean up the header string to make matching robust (ignore case and quotes)
                string header = headers[c].Replace("\"", "").Trim().ToLowerInvariant();

                if (header == "prefab") prefabIdx = c;
                else if (header == "worldx") worldXIdx = c;
                else if (header == "worldy") worldYIdx = c;
                else if (header == "terrainx") terrainXIdx = c;
                else if (header == "terrainy") terrainYIdx = c;
                else if (header == "scale") scaleIdx = c;
            }

            // Ensure we found all required columns before proceeding
            if (prefabIdx == -1 || worldXIdx == -1 || worldYIdx == -1 || terrainXIdx == -1 || terrainYIdx == -1 || scaleIdx == -1)
            {
                DaggerfallUnity.LogMessage($"[DistantTerrain] Error: Missing required columns in {fileName}. Check your headers.");
                return;
            }

            // Find the highest index we need to access to prevent out-of-bounds errors
            int maxRequiredIdx = Mathf.Max(prefabIdx, worldXIdx, worldYIdx, terrainXIdx, terrainYIdx, scaleIdx);

            // 2. Process the data rows
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                string[] parts = line.Split(',');

                // Skip malformed rows that don't have enough columns
                if (parts.Length <= maxRequiredIdx) continue;

                // Extract mountain name
                string mountainName = parts[prefabIdx].Replace("\"", "").Trim();

                // Parse the dynamically located columns
                if (int.TryParse(parts[worldXIdx], out int worldX) &&
                    int.TryParse(parts[worldYIdx], out int worldY) &&
                    int.TryParse(parts[terrainXIdx], out int terrainX) &&
                    int.TryParse(parts[terrainYIdx], out int terrainY) &&
                    float.TryParse(parts[scaleIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out float scale))
                {
                    // Calculate exact fractional map coordinates
                    float exactX = worldX + ((terrainX - 100) / 128f);
                    float exactY = (worldMapHeight - 1) - (worldY + ((terrainY - 200f) / 128f));

                    // Fetch custom multipliers based on the exact mountain type
                    GetMountainModifiers(mountainName, out float radiusMulti, out float liftMulti);

                    float radius = radiusMulti * scale;
                    float maxLift = liftMulti * scale;

                    // Precompute the integer heightmap-space bounding box, clamped to the
                    // worldHeights array bounds. For sub-pixel mountains (radius < 0.7f) the
                    // bbox collapses to the single nearest cell — matching the legacy
                    // LiftHexagonalPyramid sub-pixel fallback. We store the bbox on the def so
                    // the per-tile-cross overlap test in IsMountainOutsideNearTerrain is just
                    // four integer comparisons (no per-mountain math), and the dirty-rect
                    // union in RebuildHeightsForVisibilityChange is cheap.
                    MountainDef md;
                    md.centerX = exactX;
                    md.centerY = exactY;
                    md.radius = radius;
                    md.maxLift = maxLift;

                    if (radius < 0.7f)
                    {
                        int nx = Mathf.Clamp(Mathf.RoundToInt(exactX), 0, sizeX - 1);
                        int ny = Mathf.Clamp(Mathf.RoundToInt(exactY), 0, sizeY - 1);
                        md.bbStartX = md.bbEndX = nx;
                        md.bbStartY = md.bbEndY = ny;
                    }
                    else
                    {
                        md.bbStartX = Mathf.Clamp(Mathf.FloorToInt(exactX - radius), 0, sizeX - 1);
                        md.bbEndX = Mathf.Clamp(Mathf.CeilToInt(exactX + radius), 0, sizeX - 1);
                        md.bbStartY = Mathf.Clamp(Mathf.FloorToInt(exactY - radius), 0, sizeY - 1);
                        md.bbEndY = Mathf.Clamp(Mathf.CeilToInt(exactY + radius), 0, sizeY - 1);
                    }

                    mountainDefs.Add(md);
                }
            }
        }

        // Helper method to assign specific multipliers based on the exact mountain name.
        // Adjust these values to perfectly match the visual scale/height of your prefabs.
        private void GetMountainModifiers(string mountainName, out float radiusMulti, out float liftMulti)
        {
            switch (mountainName)
            {
                // --- Small Mountains ---
                case "WOD_Mountain_Small_01": radiusMulti = 0.10f; liftMulti = 0.20f; break;//0.20l
                case "WOD_Mountain_Small_02": radiusMulti = 0.10f; liftMulti = 0.20f; break;//0.20l
                case "WOD_Mountain_Small_03": radiusMulti = 0.10f; liftMulti = 0.12f; break;//0.12l
                case "WOD_Mountain_Small_04": radiusMulti = 0.10f; liftMulti = 0.14f; break;//0.14l
                case "WOD_Mountain_Small_05": radiusMulti = 0.10f; liftMulti = 0.10f; break;//0.10l
                case "WOD_Mountain_Small_06": radiusMulti = 0.10f; liftMulti = 0.20f; break;//0.20l
                case "WOD_Mountain_Small_07": radiusMulti = 0.10f; liftMulti = 0.20f; break;//0.20l

                // --- Base Mountain ---
                case "WOD_Mountain_Base": radiusMulti = 0.10f; liftMulti = 0.15f; break;

                // --- Standard Mountains ---
                case "WOD_Mountain_01": radiusMulti = 0.10f; liftMulti = 0.55f; break; //.55l
                case "WOD_Mountain_02": radiusMulti = 0.10f; liftMulti = 0.40f; break;//0.40l
                case "WOD_Mountain_03": radiusMulti = 0.10f; liftMulti = 0.40f; break;//0.40l
                case "WOD_Mountain_04": radiusMulti = 0.10f; liftMulti = 0.45f; break;//0.45l
                case "WOD_Mountain_05": radiusMulti = 0.10f; liftMulti = 0.30f; break;//0.30l
                case "WOD_Mountain_06": radiusMulti = 0.10f; liftMulti = 0.35f; break;//0.35l
                case "WOD_Mountain_07": radiusMulti = 0.10f; liftMulti = 0.36f; break;//0.36l
                case "WOD_Mountain_08": radiusMulti = 0.10f; liftMulti = 0.40f; break; //0.40l
                case "WOD_Mountain_09": radiusMulti = 0.10f; liftMulti = 0.25f; break;//0.25l
                case "WOD_Mountain_10": radiusMulti = 0.10f; liftMulti = 0.22f; break;//0.22l

                // --- Fallback ---
                default:
                    radiusMulti = 0.10f;
                    liftMulti = 0.50f;
                    break;
            }
        }

        // Lifts a single mountain into `heights` within the clipping rectangle
        // [clipX0..clipX1] x [clipY0..clipY1] (inclusive). Same hexagonal-pyramid shape and
        // sub-pixel fallback as the original LiftHexagonalPyramid; the only addition is the
        // clip rect so partial heightmap rebuilds don't write outside the dirty region.
        // Passing a clip equal to the full heightmap bounds yields the original behaviour
        // and zero extra cost (the inner loop range is identical).
        private static void LiftHexagonalPyramidClipped(float[,] heights, MountainDef md,
            int clipX0, int clipX1, int clipY0, int clipY1)
        {
            int sx0 = Mathf.Max(md.bbStartX, clipX0);
            int sx1 = Mathf.Min(md.bbEndX, clipX1);
            int sy0 = Mathf.Max(md.bbStartY, clipY0);
            int sy1 = Mathf.Min(md.bbEndY, clipY1);
            if (sx0 > sx1 || sy0 > sy1) return;

            // Sub-pixel mountain: bbox is a single cell. Already clipped above, so just write.
            // Matches the original LiftHexagonalPyramid radius<0.7 branch.
            if (md.radius < 0.7f)
            {
                heights[md.bbStartY, md.bbStartX] = Mathf.Max(heights[md.bbStartY, md.bbStartX], md.maxLift);
                return;
            }

            float invRadius = 1f / md.radius;
            for (int y = sy0; y <= sy1; y++)
            {
                float dy = y - md.centerY;
                float absY = Mathf.Abs(dy);
                for (int x = sx0; x <= sx1; x++)
                {
                    float dx = x - md.centerX;
                    float absX = Mathf.Abs(dx);

                    // Hexagonal metric (matches the original): max of the axis-aligned distance
                    // and a rotated-axis projection. Produces flat-sided pyramid slopes rather
                    // than concentric circular contours.
                    float hexDist = Mathf.Max(absX, absX * 0.5f + absY * 0.8660254f);

                    if (hexDist <= md.radius)
                    {
                        float heightMultiplier = 1f - (hexDist * invRadius);
                        float lift = md.maxLift * heightMultiplier;
                        heights[y, x] = Mathf.Max(heights[y, x], lift);
                    }
                }
            }
        }

        // True iff the mountain's heightmap bbox is fully outside the near-terrain footprint
        // centred on (pX, pY) with radius R (in map pixels). The shader's tile-cutout uses
        // _TerrainDistance = streamingWorld.TerrainDistance - 1; we add `mountainCutoutBuffer`
        // on top of that when picking R so a mountain whose bbox touches the cutout edge is
        // still considered overlapping, eliminating boundary seams under sub-pixel drift.
        private static bool IsMountainOutsideNearTerrain(MountainDef md, int pX, int pY, int R)
        {
            return md.bbEndX < pX - R
                || md.bbStartX > pX + R
                || md.bbEndY < pY - R
                || md.bbStartY > pY + R;
        }

        // Convert the player's PlayerGPS map pixel coordinates into the heightmap-space
        // (y-flipped) coordinates used by mountainDefs and worldHeights. worldHeights stores
        // map-pixel (x, y) at index [worldMapHeight - 1 - y, x]; mountain centerY is already
        // flipped at parse time. Keeping the conversion in one place avoids drift.
        private int PlayerHeightmapY(int playerMapPixelY)
        {
            return worldMapHeight - 1 - playerMapPixelY;
        }

        // Initial mountain lift pass, run once at the end of GenerateWorldTerrain. Mutates
        // worldHeights in place starting from baseWorldHeights' current contents (caller's
        // responsibility - GenerateWorldTerrain just finished cloning base into worldHeights),
        // then reapplies the ocean mask so deriv-map water cells stay water even if a peak
        // sits on top of them (parity with the original "mountains over water disappear"
        // behaviour where deriv-map ran *after* mountain bake).
        //
        // Does NOT call SetHeights - the caller pushes the final heightmap once at the end of
        // GenerateWorldTerrain. Subsequent partial updates go through RebuildHeightsForVisibility-
        // Change which DOES call SetHeights on its sub-rect.
        private void InitMountainVisibility()
        {
            if (mountainDefs.Count == 0)
            {
                mountainLifted = new bool[0];
                cachedPlayerHmX = playerGPS != null ? playerGPS.CurrentMapPixel.X : int.MinValue;
                cachedPlayerHmY = playerGPS != null ? PlayerHeightmapY(playerGPS.CurrentMapPixel.Y) : int.MinValue;
                return;
            }

            int pX = playerGPS.CurrentMapPixel.X;
            int pY = PlayerHeightmapY(playerGPS.CurrentMapPixel.Y);
            cachedPlayerHmX = pX;
            cachedPlayerHmY = pY;

            int cutoutRadius = Mathf.Max(0, streamingWorld.TerrainDistance - 1) + mountainCutoutBuffer;

            mountainLifted = new bool[mountainDefs.Count];
            int sizeY = worldHeights.GetLength(0);
            int sizeX = worldHeights.GetLength(1);

            for (int i = 0; i < mountainDefs.Count; i++)
            {
                MountainDef md = mountainDefs[i];
                bool lifted = IsMountainOutsideNearTerrain(md, pX, pY, cutoutRadius);
                mountainLifted[i] = lifted;
                if (lifted)
                    LiftHexagonalPyramidClipped(worldHeights, md, 0, sizeX - 1, 0, sizeY - 1);
            }

            // Re-apply the deriv-map ocean lowering after mountain lifts. Without this, a
            // mountain whose peak sits over a water cell would push the cell above ocean
            // level and visibly intrude into the sea on the distant terrain. The original
            // code achieved the same result by running deriv-map AFTER ApplyHexagonalMountains;
            // we apply it before-and-after-via-mask so baseWorldHeights doesn't need to know
            // about mountains at all.
            ReapplyOceanMask(0, sizeX - 1, 0, sizeY - 1);
        }

        // Re-carves the distant rivers for the CURRENT season and re-commits the whole far-terrain
        // heightmap. The deriv-map water carving (and its per-climate "disable winter rivers"
        // opt-out) is baked into the heightmap geometry, so - unlike the snow opt-out, which the
        // shader resolves live per fragment - it does not follow season changes on its own. Update()
        // calls this when the calendar season crosses the winter boundary while at least one
        // DisableRivers* toggle is set, so frozen winter rivers reappear in spring and re-freeze the
        // next winter. Full-world rebuild (mirrors the GenerateWorldTerrain init sequence), so it
        // runs at most twice per in-game year.
        private void RebuildFarTerrainForSeasonRivers()
        {
            if (DistantTerrainBasicModeConfig.EnableBasicMode)
                return;
            if (worldHeights == null || preDerivWorldHeights == null ||
                terrain == null || terrain.terrainData == null)
                return;

            // Restore the pristine pre-carve heights, re-run the deriv carve for the current season
            // (this also repopulates oceanMask and updates lastWinterForRivers), re-clone the
            // no-mountains reference, then re-lift the visible mountains - exactly the sequence
            // GenerateWorldTerrain runs at init. Finally push the full heightmap once.
            System.Array.Copy(preDerivWorldHeights, worldHeights, worldHeights.Length);
            ApplyDerivativeHeightmap(worldHeights);
            baseWorldHeights = (float[,])worldHeights.Clone();
            InitMountainVisibility();
            terrain.terrainData.SetHeights(0, 0, worldHeights);

            DaggerfallUnity.LogMessage(string.Format(
                "[DistantTerrain] Re-carved distant rivers for season change (winter = {0}).",
                lastWinterForRivers));
        }

        // Reapplies the deriv-map ocean lowering over a heightmap sub-rect. Used by both the
        // full-heightmap init pass and the partial rebuild after a tile cross. The mask is
        // already in heightmap space (built inside ApplyDerivativeHeightmap), so indexing here
        // mirrors the worldHeights[y, x] convention.
        private void ReapplyOceanMask(int x0, int x1, int y0, int y1)
        {
            if (oceanMask == null) return;
            float oceanLevel = Mathf.Clamp01(
                dfUnity.TerrainSampler.OceanElevation / dfUnity.TerrainSampler.MaxTerrainHeight);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (oceanMask[y, x] && worldHeights[y, x] > oceanLevel)
                        worldHeights[y, x] = oceanLevel;
                }
            }
        }

        // Called once per player map-pixel transition (from UpdateWorldTerrain(DFPosition)).
        // Cheap no-op when the player hasn't actually crossed a tile boundary, or when no
        // mountain's visibility state changed as a result.
        private void UpdateMountainVisibility()
        {
            if (playerGPS == null) return;
            if (worldTerrainGameObject == null || terrain == null || terrain.terrainData == null) return;
            if (mountainDefs.Count == 0 || baseWorldHeights == null || mountainLifted == null) return;

            int newHmX = playerGPS.CurrentMapPixel.X;
            int newHmY = PlayerHeightmapY(playerGPS.CurrentMapPixel.Y);
            if (newHmX == cachedPlayerHmX && newHmY == cachedPlayerHmY) return;

            cachedPlayerHmX = newHmX;
            cachedPlayerHmY = newHmY;

            RebuildHeightsForVisibilityChange(newHmX, newHmY);
        }

        // Re-evaluates every mountain's "should be lifted" state under the new player
        // position, unions the bboxes of the mountains whose state actually changed into a
        // dirty rect, then rebuilds that dirty rect from baseWorldHeights + the union of all
        // currently-lifted mountains that touch it. The result is pushed to the GPU via
        // terrainData.SetHeights(xBase, yBase, partialHeights).
        //
        // The dirty-rect union (rather than per-mountain partial SetHeights calls) makes the
        // SetHeights call count exactly one per tile-cross. SetHeights itself has per-call
        // overhead (bookkeeping for Unity's heightmap LOD pyramid), so consolidating is a
        // win even when the rect is conservative (i.e. larger than the strict union of
        // changed cells). For huge dirty rects (e.g. a long teleport), a single call covering
        // most of the heightmap is no slower than the full-heightmap SetHeights at init -
        // it bottoms out at the same cost.
        private void RebuildHeightsForVisibilityChange(int playerHmX, int playerHmY)
        {
            int sizeY = worldHeights.GetLength(0);
            int sizeX = worldHeights.GetLength(1);
            int cutoutRadius = Mathf.Max(0, streamingWorld.TerrainDistance - 1) + mountainCutoutBuffer;

            int dirtyX0 = int.MaxValue, dirtyX1 = int.MinValue;
            int dirtyY0 = int.MaxValue, dirtyY1 = int.MinValue;
            bool anyChanged = false;

            for (int i = 0; i < mountainDefs.Count; i++)
            {
                MountainDef md = mountainDefs[i];
                bool shouldBeLifted = IsMountainOutsideNearTerrain(md, playerHmX, playerHmY, cutoutRadius);
                if (shouldBeLifted != mountainLifted[i])
                {
                    mountainLifted[i] = shouldBeLifted;
                    anyChanged = true;
                    if (md.bbStartX < dirtyX0) dirtyX0 = md.bbStartX;
                    if (md.bbEndX > dirtyX1) dirtyX1 = md.bbEndX;
                    if (md.bbStartY < dirtyY0) dirtyY0 = md.bbStartY;
                    if (md.bbEndY > dirtyY1) dirtyY1 = md.bbEndY;
                }
            }

            if (!anyChanged) return;

            // Clamp to heightmap bounds. bb fields are already clamped at def-build time, so
            // this is just defence-in-depth.
            dirtyX0 = Mathf.Max(dirtyX0, 0);
            dirtyY0 = Mathf.Max(dirtyY0, 0);
            dirtyX1 = Mathf.Min(dirtyX1, sizeX - 1);
            dirtyY1 = Mathf.Min(dirtyY1, sizeY - 1);
            if (dirtyX0 > dirtyX1 || dirtyY0 > dirtyY1) return;

            // 1. Reset the dirty rect from the no-mountains base.
            for (int y = dirtyY0; y <= dirtyY1; y++)
                for (int x = dirtyX0; x <= dirtyX1; x++)
                    worldHeights[y, x] = baseWorldHeights[y, x];

            // 2. Re-apply every currently-lifted mountain that overlaps the dirty rect.
            //    The overlap test handles mountains whose bbox extends beyond the dirty rect;
            //    LiftHexagonalPyramidClipped restricts writes to the dirty rect itself.
            for (int i = 0; i < mountainDefs.Count; i++)
            {
                if (!mountainLifted[i]) continue;
                MountainDef md = mountainDefs[i];
                if (md.bbEndX < dirtyX0 || md.bbStartX > dirtyX1 ||
                    md.bbEndY < dirtyY0 || md.bbStartY > dirtyY1) continue;
                LiftHexagonalPyramidClipped(worldHeights, md, dirtyX0, dirtyX1, dirtyY0, dirtyY1);
            }

            // 3. Re-apply the ocean mask within the dirty rect (mountains-over-water parity).
            ReapplyOceanMask(dirtyX0, dirtyX1, dirtyY0, dirtyY1);

            // 4. Push the dirty rect to the GPU. SetHeights expects a [height, width] array
            //    indexed as partial[localY, localX], so the inner loop order matches our
            //    worldHeights[y, x] storage.
            int rectW = dirtyX1 - dirtyX0 + 1;
            int rectH = dirtyY1 - dirtyY0 + 1;
            float[,] sub = new float[rectH, rectW];
            for (int y = 0; y < rectH; y++)
                for (int x = 0; x < rectW; x++)
                    sub[y, x] = worldHeights[dirtyY0 + y, dirtyX0 + x];

            terrain.terrainData.SetHeights(dirtyX0, dirtyY0, sub);
        }

        // daggerfall_deriv_map.png is a binary (black/white) mask: black pixels are water
        // (rivers/coast/ocean) and white pixels are land. Pixels at or below this midpoint
        // value (0-255) are treated as water and clamped down to ocean level so the far-terrain
        // shader renders them as such. 127 is the standard binary split; any antialiased edge
        // pixels fall to whichever side they're closer to.
        private const int derivMapBinaryThreshold = 127;

        // Loads daggerfall_deriv_map.png from the mod's AssetBundle and lowers any terrain
        // pixel whose corresponding image area is dark (rivers/coasts/ocean) down to ocean
        // elevation. Only lowers, never raises, so mountains applied earlier are preserved
        // everywhere else.
        private void ApplyDerivativeHeightmap(float[,] heights)
        {
            const string fileName = "daggerfall_deriv_map.png";

            // Allocate the per-cell water mask sized to the supplied heightmap. This is used
            // later by ReapplyOceanMask() to re-lower any cell that a mountain lift pushed
            // back above ocean level (preserving the original "mountains over water disappear"
            // behaviour, which the legacy code achieved by running deriv-map after the mountain
            // bake; we instead record the mask here and stamp it on after every mountain
            // (re)apply). If asset loading or pixel reading below fails, we early-return with
            // the mask left allocated-but-empty, which is fine - ReapplyOceanMask is a no-op
            // on all-false cells.
            oceanMask = new bool[heights.GetLength(0), heights.GetLength(1)];

            // Record the season this carve reflects up-front, before the asset loads below (which
            // can early-return on failure), so the Update() season-change check won't re-fire every
            // frame if the deriv-map asset is unavailable. Reused for the per-cell winter opt-out.
            bool isWinter = dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
            lastWinterForRivers = isWinter;

            Texture2D derivMap = DistantTerrainPort.Mod.GetAsset<Texture2D>(fileName);   // MOBILE: renamed loader
            if (derivMap == null)
            {
                DaggerfallUnity.LogMessage($"[DistantTerrain] Error: {fileName} not found in mod assets (is it flagged as a Mod Resource in the Mod Builder?)");
                return;
            }

            int imgWidth = derivMap.width;
            int imgHeight = derivMap.height;
            Color32[] pixels;
            try
            {
                pixels = derivMap.GetPixels32();
            }
            catch (UnityException ex)
            {
                DaggerfallUnity.LogMessage($"[DistantTerrain] Error reading pixels from {fileName}: {ex.Message} -- ensure 'Read/Write Enabled' is checked in the texture's import settings.");
                return;
            }
            // IMPORTANT: do NOT Destroy(derivMap). Unlike the previous version, which built
            // a transient Texture2D from raw bytes that it owned and was free to destroy,
            // this texture is owned by the mod's AssetBundle. Destroying it would corrupt
            // the mod's asset state for the rest of the session. It lives for the
            // application lifetime, but its memory footprint is negligible.

            // Normalized ocean elevation in the same units used by worldHeights (0..1 of MaxTerrainHeight)
            float oceanLevelNormalized = Mathf.Clamp01(
                dfUnity.TerrainSampler.OceanElevation / dfUnity.TerrainSampler.MaxTerrainHeight);

            // Image-to-heightmap scale. The supplied 5000x2500 image is 5x the 1000x500 world map,
            // but compute it generically so any compatible resolution works.
            float scaleX = (float)imgWidth / worldMapWidth;
            float scaleY = (float)imgHeight / worldMapHeight;

            int loweredCount = 0;

            // Per-biome "disable winter rivers" support. When the in-game season is winter AND at
            // least one biome has its DisableRivers* toggle set, water cells whose climate belongs
            // to a disabled biome are left as land (neither carved nor recorded in oceanMask) so
            // their distant rivers/inland water "freeze over" and disappear for the season. This is
            // evaluated here, at heightmap build time - which happens both on world entry AND, now,
            // whenever the calendar season crosses the winter boundary (Update() calls
            // RebuildFarTerrainForSeasonRivers, which re-runs this carve from the pristine pre-deriv
            // heights), so the freeze/thaw follows the season mid-session instead of waiting for a
            // world reload. Ocean-climate cells (climate 223) are never matched by the helper, so the
            // sea and coastlines always carve as before. anyWinterRiverDisable gates the per-cell
            // climate lookup so the carve loop pays zero extra cost when the feature is off or it
            // isn't winter.
            // isWinter is computed at the top of this method (it also sets lastWinterForRivers).
            bool anyWinterRiverDisable = AnyWinterRiverDisableSet();
            bool applyWinterRiverDisable = isWinter && anyWinterRiverDisable;
            int skippedWinterRiverCount = 0;

            for (int y = 0; y < worldMapHeight; y++)
            {
                // Determine the image-block (in visual-top-down coords) covered by this heightmap row.
                // y==0 corresponds to the top (north) of the heightmap, matching the image's top row.
                int visTopRow = Mathf.FloorToInt(y * scaleY);
                int visBotRow = Mathf.Min(imgHeight - 1, Mathf.CeilToInt((y + 1) * scaleY) - 1);
                if (visBotRow < visTopRow) visBotRow = visTopRow;

                // Convert visual rows to Unity texture rows (Texture2D.GetPixels32 is bottom-up: pixel
                // index 0 is the bottom-left of the image in visual orientation).
                int texRowStart = imgHeight - 1 - visBotRow;
                int texRowEnd = imgHeight - 1 - visTopRow;

                for (int x = 0; x < worldMapWidth; x++)
                {
                    int colStart = Mathf.FloorToInt(x * scaleX);
                    int colEnd = Mathf.Min(imgWidth - 1, Mathf.CeilToInt((x + 1) * scaleX) - 1);
                    if (colEnd < colStart) colEnd = colStart;

                    // Find the minimum (darkest) pixel value across the image block for this terrain cell.
                    // Using min preserves thin features (single-pixel-wide rivers) when the image is
                    // higher resolution than the heightmap.
                    int minVal = 255;
                    for (int ty = texRowStart; ty <= texRowEnd && minVal > 0; ty++)
                    {
                        int rowBase = ty * imgWidth;
                        for (int tx = colStart; tx <= colEnd; tx++)
                        {
                            // Image is grayscale; R == G == B, so sampling R is sufficient.
                            byte v = pixels[rowBase + tx].r;
                            if (v < minVal)
                            {
                                minVal = v;
                                if (minVal == 0) break;
                            }
                        }
                    }

                    if (minVal <= derivMapBinaryThreshold)
                    {
                        // Winter-river opt-out: if this water cell's climate belongs to a biome
                        // whose DisableRivers* toggle is set (and it's winter), leave it as land.
                        // Skipping the oceanMask record too is important so ReapplyOceanMask won't
                        // re-lower it after a mountain lift.
                        if (applyWinterRiverDisable && IsWaterCellInDisabledWinterRiverBiome(x, y))
                        {
                            skippedWinterRiverCount++;
                            continue;
                        }

                        // Carve down to ocean level. Same Y-flip as the original heightmap loader.
                        int hy = worldMapHeight - 1 - y;
                        // Record the cell in oceanMask regardless of whether we actually lowered
                        // it this pass - cells that are already at/below ocean still need to be
                        // re-lowered after a future mountain lift might push them above it.
                        oceanMask[hy, x] = true;
                        if (heights[hy, x] > oceanLevelNormalized)
                        {
                            heights[hy, x] = oceanLevelNormalized;
                            loweredCount++;
                        }
                    }
                }
            }

            DaggerfallUnity.LogMessage(
                $"[DistantTerrain] Applied {fileName}: {loweredCount} terrain pixels lowered to ocean level " +
                $"(binary threshold <= {derivMapBinaryThreshold}/255, ocean level normalized = {oceanLevelNormalized:F4})" +
                (applyWinterRiverDisable ? $"; {skippedWinterRiverCount} winter-river cells left as land" : ""));
        }

        // True when at least one per-climate "disable winter rivers" toggle is set. Single source of
        // truth shared by ApplyDerivativeHeightmap (to skip per-cell work when nothing is disabled)
        // and Update() (to skip the season-change rebuild check entirely when the feature is off for
        // every climate).
        private static bool AnyWinterRiverDisableSet()
        {
            return DistantTerrainWinterRiverConfig.DisableRiversDesert ||
                   DistantTerrainWinterRiverConfig.DisableRiversDesert2 ||
                   DistantTerrainWinterRiverConfig.DisableRiversMountain ||
                   DistantTerrainWinterRiverConfig.DisableRiversRainforest ||
                   DistantTerrainWinterRiverConfig.DisableRiversSwamp ||
                   DistantTerrainWinterRiverConfig.DisableRiversSubtropical ||
                   DistantTerrainWinterRiverConfig.DisableRiversMountainWoods ||
                   DistantTerrainWinterRiverConfig.DisableRiversWoodlands ||
                   DistantTerrainWinterRiverConfig.DisableRiversHauntedWoodlands ||
                   DistantTerrainWinterRiverConfig.DisableRiversMaquis;
        }

        // Maps a world map-pixel's climate to its PER-CLIMATE winter-river disable toggle and
        // returns true when that climate currently has the toggle set. One case per Daggerfall
        // land climate (224..233) - so e.g. Rainforest (jungle, 227) can freeze its rivers while
        // true Swamp (228) keeps them, even though they share the Swamp tile atlas. Ocean (223)
        // and any unrecognised climate return false so they always carve as before. Caller gates
        // this on applyWinterRiverDisable, so it is only hit for water cells while the feature is
        // active in winter.
        private bool IsWaterCellInDisabledWinterRiverBiome(int x, int y)
        {
            int climateIndex = dfUnity.ContentReader.MapFileReader.GetClimateIndex(x, y);
            switch (climateIndex)
            {
                case 224: return DistantTerrainWinterRiverConfig.DisableRiversDesert;            // Desert
                case 225: return DistantTerrainWinterRiverConfig.DisableRiversDesert2;           // Desert2
                case 226: return DistantTerrainWinterRiverConfig.DisableRiversMountain;          // Mountain
                case 227: return DistantTerrainWinterRiverConfig.DisableRiversRainforest;        // Rainforest (jungle)
                case 228: return DistantTerrainWinterRiverConfig.DisableRiversSwamp;             // Swamp
                case 229: return DistantTerrainWinterRiverConfig.DisableRiversSubtropical;       // Subtropical
                case 230: return DistantTerrainWinterRiverConfig.DisableRiversMountainWoods;     // MountainWoods
                case 231: return DistantTerrainWinterRiverConfig.DisableRiversWoodlands;         // Woodlands
                case 232: return DistantTerrainWinterRiverConfig.DisableRiversHauntedWoodlands;  // HauntedWoodlands
                case 233: return DistantTerrainWinterRiverConfig.DisableRiversMaquis;            // Maquis
                default:  return false;                                                          // Ocean (223) / unknown: always carve
            }
        }

        /// <summary>
        /// MOBILE: void upstream. Returns false when the four biome tilesets cannot be packed into
        /// one texture array (a texture-replacement pack that resized or recompressed one archive
        /// and not the others), which is a refusal with a logged reason rather than an exception.
        /// Also times its three stages for InitFarTerrain's one timing line.
        /// </summary>
        private bool GenerateWorldTerrain()
        {
            System.Diagnostics.Stopwatch stageWatch = System.Diagnostics.Stopwatch.StartNew();

            // Create Unity Terrain game object
            GameObject terrainGameObject = Terrain.CreateTerrainGameObject(null);
            terrainGameObject.name = string.Format("WorldTerrain");

            terrainGameObject.gameObject.transform.localPosition = Vector3.zero;

            // assign terrainGameObject to layer layerWorldTerrain
            terrainGameObject.layer = layerWorldTerrain;

            int worldMapResolution = Math.Max(worldMapWidth, worldMapHeight);

            if (worldHeights == null)
            {
                worldHeights = new float[worldMapResolution, worldMapResolution];
            }

            // Basic mode (typically for setups without the World of Daggerfall mod) reverts to
            // the simple water strategy from the reference DistantTerrainBasic.cs: sub-ocean
            // pixels are clamped straight to ocean level and the deriv-map carving below is
            // skipped. Full mode lifts sub-ocean pixels just above ocean level so the deriv map
            // becomes the sole authority on what renders as water.
            bool basicMode = DistantTerrainBasicModeConfig.EnableBasicMode;

            for (int y = 0; y < worldMapHeight; y++)
            {
                for (int x = 0; x < worldMapWidth; x++)
                {
                    // get height data for this map pixel from world map and scale it to approximately match StreamingWorld's terrain heights
                    float sampleHeight = Convert.ToSingle(dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue(x, y));

                    sampleHeight *= dfUnity.TerrainSampler.TerrainHeightScale(x, y);

                    if (sampleHeight < dfUnity.TerrainSampler.OceanElevation)
                    {
                        // Basic mode: make ocean elevation the lower limit (reference behaviour).
                        // Full mode: lift any sub-ocean woods values just above ocean level so the
                        // deriv map is the sole authority on what becomes water in the distant
                        // terrain. Without this, native low-elevation pixels in WOODS.WLD render as
                        // water here (since the far-terrain shader decides water purely by height)
                        // even though the local terrain treats them as land via its separate
                        // tile/climate maps.
                        sampleHeight = basicMode
                            ? dfUnity.TerrainSampler.OceanElevation
                            : dfUnity.TerrainSampler.OceanElevation + 1.0f;
                    }

                    // normalize with TerrainHelper.maxTerrainHeight
                    worldHeights[worldMapHeight - 1 - y, x] = Mathf.Clamp01(sampleHeight / dfUnity.TerrainSampler.MaxTerrainHeight);
                }
            }
            // Sequence (full / World of Daggerfall mode):
            //   1. ApplyDerivativeHeightmap -- now also populates the persistent oceanMask
            //      bool[,] which records which cells the deriv map considers water.
            //   2. Clone the result into baseWorldHeights as the no-mountains reference;
            //      RebuildHeightsForVisibilityChange resets dirty rects from this each time
            //      a mountain's visibility flips.
            //   3. BuildMountainDefinitions -- parse the CSVs into MountainDef records (with
            //      precomputed bboxes) but do NOT mutate heights yet.
            //   4. InitMountainVisibility -- evaluate each mountain against the current player
            //      tile, lift the ones whose bbox is entirely outside the near-terrain cutout,
            //      then stamp the oceanMask back on so any peaks-over-water are re-flattened.
            // The SetHeights call further down pushes the fully-initialised heightmap once.
            //
            // Basic mode skips all of the above: no deriv-map water carving (the sub-ocean clamp
            // in the loop above already did the simple water lower-limit) and no CSV mountain
            // prefab lifts. We leave oceanMask null and the mountain state empty so the
            // Update-time mountain path (UpdateMountainVisibility) and ReapplyOceanMask are
            // no-ops, and clone the unmodified heightmap into baseWorldHeights for consistency.
            // MOBILE: stage boundary - everything above is the WOODS.WLD sample of the whole world.
            lastHeightmapMs = stageWatch.Elapsed.TotalMilliseconds;
            lastCarveMs = 0;
            lastLiftsMs = 0;
            if (!basicMode)
            {
                // Keep a pristine pre-carve, pre-mountain copy (worldHeights is freshly resampled
                // each call by the loop above) so winter<->non-winter season changes can re-carve
                // the rivers later via RebuildFarTerrainForSeasonRivers().
                preDerivWorldHeights = (float[,])worldHeights.Clone();
                double carveStart = stageWatch.Elapsed.TotalMilliseconds;   // MOBILE
                ApplyDerivativeHeightmap(worldHeights);
                lastCarveMs = stageWatch.Elapsed.TotalMilliseconds - carveStart;    // MOBILE
                baseWorldHeights = (float[,])worldHeights.Clone();
                double liftsStart = stageWatch.Elapsed.TotalMilliseconds;   // MOBILE
                BuildMountainDefinitions();
                InitMountainVisibility();
                lastLiftsMs = stageWatch.Elapsed.TotalMilliseconds - liftsStart;   // MOBILE
            }
            else
            {
                oceanMask = null;
                preDerivWorldHeights = null;
                baseWorldHeights = (float[,])worldHeights.Clone();
                mountainDefs.Clear();
                mountainLifted = new bool[0];
                cachedPlayerHmX = playerGPS != null ? playerGPS.CurrentMapPixel.X : int.MinValue;
                cachedPlayerHmY = playerGPS != null ? PlayerHeightmapY(playerGPS.CurrentMapPixel.Y) : int.MinValue;
                DaggerfallUnity.LogMessage("[DistantTerrain] Basic mode active: skipping deriv-map water carving and CSV mountain prefab lifts.");
            }
            // Basemap not used and is just pushed far away
            const float basemapDistance = 1000000f;

            // Ensure TerrainData is created
            Terrain terrain = terrainGameObject.GetComponent<Terrain>();
            if (terrain.terrainData == null)
            {
                // Setup terrain data
                TerrainData terrainData = new TerrainData();
                terrainData.name = "TerrainData";

                terrainData.heightmapResolution = worldMapResolution;

                float heightmapResolution = terrainData.heightmapResolution;
                // Calculate width and length of terrain in world units
                float terrainSize = ((MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale) * (heightmapResolution - 1.0f));

                terrainData.size = new Vector3(terrainSize, dfUnity.TerrainSampler.MaxTerrainHeight, terrainSize);

                // MOBILE: the far terrain paints nothing through the splat, basemap or detail
                // machinery - it is one custom material sampling a tilemap texture - but upstream
                // sized all three at the world resolution (1000). Unity allocates on those numbers:
                // a 1000x1000 alphamap plus its 1000x1000 basemap, per Terrain, for maps that are
                // never written or read. 16 is the smallest Unity accepts and costs nothing.
                terrainData.SetDetailResolution(16, 8);
                terrainData.alphamapResolution = 16;
                terrainData.baseMapResolution = 16;

                // Apply terrain data to the existing terrain component
                terrain.terrainData = terrainData;
                terrain.basemapDistance = basemapDistance;

                // The distant terrain is a visual LOD mesh only — it must NOT have a
                // collider. Terrain.CreateTerrainGameObject() auto-adds a TerrainCollider,
                // so we destroy it here. Leaving it in place lets the player walk on the
                // invisible LOD heightmap (especially over CSV-defined mountain lifts that
                // exist in the distant heightmap but not in the streamed near terrain),
                // and causes rain particles to collide with an invisible ground slightly
                // above the visible one — both of which produced longstanding bug reports
                // (NPCs walking in the air, rain stopping above the ground at altitude).
                TerrainCollider autoCollider = terrainGameObject.GetComponent<TerrainCollider>();
                if (autoCollider != null)
                {
                    // MOBILE: upstream disabled rather than destroyed it so the fly-map could raycast
                    // against it. The fly-map is dropped from this port, but disabling still costs
                    // nothing and keeps the object identical to upstream's.
                    autoCollider.enabled = false;
                }
            }

            terrain.heightmapPixelError = 0;

            // important to prevent wrong shadows
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Promote heights
            Vector3 size = terrain.terrainData.size;
            terrain.terrainData.size = new Vector3(size.x, dfUnity.TerrainSampler.MaxTerrainHeight * streamingWorld.TerrainScale, size.z);
            terrain.terrainData.SetHeights(0, 0, worldHeights);


            // update world terrain position - do this before terrainGameObject.transform invocation, so that object2world matrix is updated with correct values
            Vector3 offset = new Vector3(0.0f, 0.0f, 0.0f);
            UpdatePositionWorldTerrain(ref terrainGameObject, offset);

            // MOBILE: this is where the twelve GetTerrainTilesetTexture(...).albedoMap calls built
            // twelve 2048^2 atlases. They are packed into three 224-slice arrays instead; a refusal
            // here (see BuildTileArrays) means no far terrain, so nothing further is built.
            System.Diagnostics.Stopwatch arraysWatch = System.Diagnostics.Stopwatch.StartNew();   // MOBILE
            bool arraysBuilt = BuildTileArrays();
            arraysWatch.Stop();                                                                   // MOBILE
            lastArraysMs = arraysWatch.Elapsed.TotalMilliseconds;                                 // MOBILE
            if (!arraysBuilt)
            {
                // MOBILE: the Terrain object exists by now (it is created at the top of this method);
                // a refusal here must not leave it in the scene for the teardown to miss - and
                // destroying the GameObject does NOT destroy the TerrainData created above it, which
                // is the 1025^2 heightmap plus Unity's heightmap texture (~4-6 MB). `terrain` here is
                // this method's own local component reference; the FIELD of the same name is still
                // null (BuildFarTerrain assigns it only after this method returns true), so the
                // teardown that runs next cannot reach this TerrainData. It goes here or nowhere.
                if (terrain.terrainData != null)
                    Destroy(terrain.terrainData);
                Destroy(terrainGameObject);
                return false;
            }

            terrainMaterial = new Material(shaderDistantTerrainTilemap);
            terrainMaterial.name = string.Format("world terrain material");

            // Assign textures and parameters
            // MOBILE: three SetTexture calls where there were twelve, and no _TileAtlasTex*SnowFree /
            // *Snow slots - one array per season covers all three roles. ALL THREE are bound in
            // every season and never reassigned: the shader picks per fragment (_TextureSetSeasonCode
            // selects the seasonal one, the altitude snow caps always sample winter, and a climate
            // with winter snow disabled always samples summer). Binding only the current season's
            // array leaves the other two at Unity's default and the caps and snow-free climates
            // render black.
            terrainMaterial.SetTexture("_TileArraySummer", tileArraySummer);
            terrainMaterial.SetTexture("_TileArrayWinter", tileArrayWinter);
            terrainMaterial.SetTexture("_TileArrayRain", tileArrayRain);
            terrainMaterial.SetInt("_SlicesPerBiome", SlicesPerBiome);

            // Snow-cap feature toggle + the per-region treatment table (colour tint + snow mode).
            // Code-only tuning hooks; pushed once here (they never change). The world-Y snow line
            // itself is pushed per-frame from PushSnowlineHeights (it tracks the floating-origin
            // transform like the water level). The per-pixel treatment index is baked into the tilemap
            // G channel in the tile-assignment loop above.
            terrainMaterial.SetInt("_SnowCapsEnabled", DistantTerrainSnowCapConfig.EnableSnowCaps ? 1 : 0);
            PushRegionData(terrainMaterial);

            // User-facing toggle (modsettings, default on) for the procedural tree specks + woodland
            // dirt. Pushed once; never changes at runtime.
            terrainMaterial.SetInt("_EnableTreesAndDirt", DistantTerrainFeatureConfig.EnableTreesAndDirt ? 1 : 0);

            // IPNM water mode settings: when enabled, adjusts distant water brightness and opacity
            // to match Iliac Puddle No More's near-water appearance. Brightness is a multiplier
            // (1.0 = normal, >1.0 brightens), opacity is alpha (1.0 = fully opaque, <1.0 fades).
            terrainMaterial.SetInt("_IPNMWater", IPNMWaterConfig.IPNMWater ? 1 : 0);
            terrainMaterial.SetFloat("_IPNMWaterBrightness", IPNMWaterConfig.IPNMWaterBrightness);
            terrainMaterial.SetFloat("_IPNMWaterOpacity", IPNMWaterConfig.IPNMWaterOpacity);

            terrainMaterial.SetInt("_TextureSetSeasonCode", 0);

            updateMaterialSeasonalTextures(ref terrainMaterial, currentSeason);

            terrainMaterial.SetInt("_PlayerPosX", this.playerGPS.CurrentMapPixel.X);
            terrainMaterial.SetInt("_PlayerPosY", this.playerGPS.CurrentMapPixel.Y);

            terrainMaterial.SetInt("_TerrainDistance", streamingWorld.TerrainDistance - 1);

            Vector3 vecWaterHeight = new Vector3(0.0f, (dfUnity.TerrainSampler.OceanElevation + 1.0f) * streamingWorld.TerrainScale, 0.0f); // water height level on y-axis (+1.0f some coastlines are incorrect otherwise)
            Vector3 vecWaterHeightTransformed = terrainGameObject.transform.TransformPoint(vecWaterHeight); // transform to world coordinates
            terrainMaterial.SetFloat("_WaterHeightTransformed", vecWaterHeightTransformed.y - extraWaterTranslationY);

            // Seed the world-Y snow line so it is valid on the first render (kept live per-frame by
            // PushSnowlineHeights from UpdatePositionWorldTerrain).
            PushSnowlineHeights(terrainGameObject.transform);

            terrainMaterial.SetTexture("_SkyTex", renderTextureSky);

            setMaterialFogParameters(ref terrainMaterial);

            terrainMaterial.SetFloat("_BlendStart", blendStart);
            terrainMaterial.SetFloat("_BlendEnd", blendEnd);
            terrainMaterial.SetFloat("_WorldEdgeFadeWidthTiles", worldEdgeFadeWidthTiles);

            terrainMaterial.SetInt("_UseSeaReflectionTex", 0);

            // Seed the boundary-skirt depth so it is valid on the first render (the per-frame
            // cached push in Update() keeps it live afterward).
            terrainMaterial.SetFloat("_SkirtDepth", farTerrainSkirtDepth);

            // Cutout is performed in the fragment shader (fragments strictly inside the
            // near-terrain footprint are discarded). The innermost ring is kept and pinned down
            // in the vertex shader (see applyFarTerrainSkirt in FarTerrainCommon.cginc) to form a
            // skirt that seals the near/far seam.

            // Promote material            
            terrain.materialTemplate = terrainMaterial;

            terrainGameObject.SetActive(true);

            worldTerrainGameObject = terrainGameObject;
            return true;    // MOBILE
        }

        #endregion

        #region Startup/Shutdown Methods

        private bool ReadyCheck()
        {
            if (isReady)
                return true;

            if (dfUnity == null)
            {
                dfUnity = DaggerfallUnity.Instance;
            }

            // Do nothing if DaggerfallUnity not ready
            if (!dfUnity.IsReady)
            {
                DaggerfallUnity.LogMessage("ExtendedTerrainDistance: DaggerfallUnity component is not ready. Have you set your Arena2 path?");
                return false;
            }

            // Raise ready flag
            isReady = true;

            return true;
        }

        #endregion
    }
}