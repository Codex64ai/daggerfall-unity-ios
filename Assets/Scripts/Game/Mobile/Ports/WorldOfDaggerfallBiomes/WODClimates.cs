// MOBILE PORT - source: github.com/drcarademono/wod-biomes @ 40449fc5fc55c85c8089b068acefe1db4b61534c
// File WODClimates.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using UnityEngine.Rendering;                          // for ShadowCastingMode
using System;
using System.IO;                                     // for File.Exists
// MOBILE: using System.Reflection removed - see the note on CustomBillboardHelper below.
using System.Collections.Generic;
using DaggerfallWorkshop;                            // for DaggerfallUnity
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;    // for TextureReplacement
using DaggerfallWorkshop.Utility;                    // for TextureReader
using DaggerfallWorkshop.Utility.AssetInjection;     // for TextureImport
using DaggerfallConnect;
using DaggerfallConnect.Arena2;                      // for TextureFile, RecordIndex
using DaggerfallConnect.Utility;                     // for FileUsage

namespace WorldOfDaggerfall
{
    public static class NatureBatchOverriderInstaller
    {
        // MOBILE: private now, so nothing can read the raw asset around EnsureReadableMap and get a
        // texture the CPU cannot sample. Everything goes through the ClimateMap property.
        static Texture2D climateMap;

        // MOBILE: Location Loader's type-5 nature swap reads the climate map from here instead of
        // GetAsset-ing a copy out of its own bundle. A property, so it always tracks climateMap - and
        // it serves the readable copy, because EnsureReadableMap writes that copy back into the field.
        public static Texture2D ClimateMap => EnsureReadableMap();

        /// <summary>
        /// MOBILE: the colour key is compared exactly, so the map has to be sampled on the CPU. The pack
        /// importer already imports this one texture readable and uncompressed (MobileModPackTextureRules),
        /// but a bundle built before that rule - or one loaded from Documents/Mods - would come back
        /// unreadable and GetPixel would throw. EnsureReadable makes a readable RGBA32 copy through a GPU
        /// blit; the result is written back to climateMap so the blit happens at most once per session.
        /// Idempotent: a readable map is returned untouched.
        /// </summary>
        public static Texture2D EnsureReadableMap()
        {
            if (climateMap != null && !climateMap.isReadable)
                climateMap = TextureReplacement.EnsureReadable(climateMap);
            return climateMap;
        }

        // MOBILE: started by MobilePortedMods when the launcher entry is on
        // (upstream: [Invoke(StateManager.StateTypes.Start, 0)])
        public static void Init(InitParams initParams)
        {
            Debug.Log("[NatureBatch] Installer.Init");

            var mod = initParams.Mod;
            mod.LoadAllAssetsFromBundle();

            // try loading our map
            climateMap = mod.GetAsset<Texture2D>("climate_map");
            // MOBILE: convert here, at start-up, rather than on the first terrain update mid-travel.
            EnsureReadableMap();
            if (climateMap == null)
                Debug.LogError("[NatureBatch] 🌡️ climate_map asset NOT found!");
            else
                Debug.Log($"[NatureBatch] 🌡️ climate_map loaded: {climateMap.width}×{climateMap.height}, readable={climateMap.isReadable}");

            var go = new GameObject("NatureBatchOverrider");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<NatureBatchOverrider>();
        }
    }

    public class NatureBatchOverrider : MonoBehaviour
    {
        const int NEW_ARCHIVE = 10030;

        /// <summary>
        /// MOBILE: the vanilla nature archive whose batches this mod re-skins. Everything else is left
        /// alone, and a batch we have already swapped reports NEW_ARCHIVE, so it never matches again.
        /// </summary>
        const int NATURE_ARCHIVE = 501;

        /// <summary>
        /// MOBILE: pinned atlas size, replacing upstream's 4096 (or 2048). Archive 10030 is 32 records of
        /// 64x64, which pack into ~512x512 with padding, but upstream allocated the full square up front:
        /// a transient 4096x4096 ARGB32 with mips is ~85 MB, on a device that has to survive a jetsam
        /// limit. 1024 is a fourfold headroom over what the atlas actually needs.
        /// </summary>
        public const int AtlasMaxSize = 1024;

        // MOBILE: batches this overrider has put on NEW_ARCHIVE. Skipping them is what keeps the
        // per-update cost near zero: it avoids the parent-component walk, the GetPixel and above all
        // batch.Apply(), which rebuilds the batch mesh. The batch's own archive is the real invariant
        // (a swapped batch reports NEW_ARCHIVE, so the archive filter below also excludes it); this set
        // is the cheap first test and the record that lets RevisedSetMaterial be called with force:false.
        static readonly HashSet<DaggerfallBillboardBatch> swapped = new HashSet<DaggerfallBillboardBatch>();

        // MOBILE: one line per distinct failure, not one per terrain update.
        static readonly HashSet<string> loggedErrors = new HashSet<string>();
        static bool warnedUnreadableMap;

        void OnEnable()  => StreamingWorld.OnUpdateTerrainsEnd += ApplyOverrides;
        void OnDisable() => StreamingWorld.OnUpdateTerrainsEnd -= ApplyOverrides;

        /// <summary>
        /// MOBILE: pure, so the self-test can pin it. The map is a colour-key image and the test is an
        /// exact match - #FFA500 and nothing near it. That is why the climate map must import raw and
        /// uncompressed (MobileModPackTextureRules): one ASTC block and every key pixel drifts.
        /// </summary>
        public static bool IsSubtropicalKey(Color32 c)
        {
            return c.r == 255 && c.g == 165 && c.b == 0;
        }

        /// <summary>
        /// MOBILE: pure guard, shared with Location Loader's BiomesClimateSwap. A map that is missing or
        /// not CPU-readable cannot be sampled, and GetPixel on it throws rather than returning anything.
        /// </summary>
        public static bool MapReadable(Texture2D map)
        {
            return map != null && map.isReadable && map.width > 0;
        }

        void ApplyOverrides()
        {
            // MOBILE: whole body guarded. This runs on every OnUpdateTerrainsEnd - the busiest frame in
            // the game - and an exception thrown here would take the rest of the terrain-update
            // subscribers with it. Logged once per distinct message.
            try
            {
                ApplyOverridesInner();
            }
            catch (System.Exception ex)
            {
                if (loggedErrors.Add(ex.Message))
                    Debug.LogError("[Biomes] nature swap failed: " + ex);
            }
        }

        void ApplyOverridesInner()
        {
            // MOBILE: the property hands back the readable copy (see EnsureReadableMap). Read once.
            Texture2D map = NatureBatchOverriderInstaller.ClimateMap;
            if (!MapReadable(map))
            {
                // MOBILE: was an unconditional LogWarning on every terrain update.
                if (!warnedUnreadableMap)
                {
                    Debug.LogWarning("[Biomes] climate map missing or not readable; nature swap disabled");
                    warnedUnreadableMap = true;
                }
                return;
            }

            int mapW = map.width;
            int mapH = map.height;

            // MOBILE: terrain objects are pooled and destroyed as the player travels; drop the dead ones
            // so the set cannot grow without bound over a long session.
            swapped.RemoveWhere(b => b == null);

            // MOBILE: was FindObjectsOfType<DaggerfallBillboardBatch>() - a full scene scan with an
            // allocation on every terrain update. StreamingWorld parents both kinds of nature batch under
            // StreamingTarget: a terrain's batch hangs off its terrain object (StreamingWorld.cs:1157 +
            // :1165) and a location's off its DaggerfallLocation object (:747 + :1198), so one walk from
            // that root sees everything the scan used to. true = include inactive: pooled-out terrains
            // are deactivated, not destroyed, and are reactivated without a fresh nature layout.
            StreamingWorld world = GameManager.Instance.StreamingWorld;
            if (world == null)
                return;
            DaggerfallBillboardBatch[] batches = world.StreamingTarget.GetComponentsInChildren<DaggerfallBillboardBatch>(true);

            int n = 0;
            foreach (var batch in batches)
            {
                // MOBILE: already ours - no parent walk, no GetPixel, no Apply().
                if (batch.TextureArchive == NEW_ARCHIVE && swapped.Contains(batch))
                    continue;

                if (batch.TextureArchive != NATURE_ARCHIVE)
                    continue;

                // MOBILE: back on the vanilla archive, so the engine laid this terrain's or location's
                // nature out again (StreamingWorld.UpdateTerrainNature / DaggerfallLocation) and undid
                // our swap. Forget it, so it is swapped afresh below.
                swapped.Remove(batch);

                // 1) Try location first
                int mx = -1, my = -1;
                var loc = batch.GetComponentInParent<DaggerfallLocation>();
                if (loc != null)
                {
                    mx = loc.Summary.MapPixelX;
                    my = loc.Summary.MapPixelY;
                }
                else if (batch.GetComponentInParent<DaggerfallTerrain>() is var terrain && terrain != null)
                {
                    mx = terrain.MapPixelX;
                    my = terrain.MapPixelY;
                }
                else
                {
                    // not in a location or terrain → skip
                    continue;
                }

                // 2) Bounds check
                if (mx < 0 || mx >= mapW || my < 0 || my >= mapH)
                {
                    Debug.LogError($"[NatureBatch] Pixel coords ({mx},{my}) out of range for climateMap {mapW}×{mapH}");
                    continue;
                }

                // 3) Flip Y, sample
                int ty = mapH - 1 - my;
                Color32 c = map.GetPixel(mx, ty);

                // 4) Match #FFA500
                if (IsSubtropicalKey(c))                    // MOBILE: pure, self-tested
                {
                    // MOBILE: force:false. Upstream passed true, so the "already on this archive"
                    // early-out never fired and a new Material was built per batch per update - an
                    // unbounded leak plus a Shader.Find each time. The material is cached per archive
                    // in CustomBillboardHelper now, and this set stops Apply() re-running.
                    CustomBillboardHelper.RevisedSetMaterial(batch, NEW_ARCHIVE, false);
                    batch.Apply();
                    swapped.Add(batch);
                    n++;
                }
            }

            // MOBILE: only when something actually changed - this used to be silent, and the simulator
            // and device runs grep for this line as the proof the swap fired.
            if (n > 0)
                Debug.Log("[Biomes] swapped " + n + " nature batches to archive " + NEW_ARCHIVE);
        }
    }

    static class CustomBillboardHelper
    {
        // MOBILE: the three FieldInfo caches and the static constructor that filled them are gone.
        // DaggerfallBillboardBatch.currentArchive and cachedMaterial are internal in this fork, so the
        // fields below are assigned directly - reflection on private fields does not survive IL2CPP
        // managed stripping reliably.

        class CachedAtlas
        {
            public Texture2D       atlas;
            public Rect[]          rects;
            public RecordIndex[]   indices;
            public int[]           frameCounts;
            public Vector2[]       sizes;
            public Vector2[]       scales;
        }

        // archive → already baked atlas data
        static readonly Dictionary<int, CachedAtlas> _atlasCache = new Dictionary<int, CachedAtlas>();

        // stash our generated albedo atlas so RevisedSetMaterial can grab it
        static Texture2D _lastAtlas = null;

        // MOBILE: archive → the Material built over that archive's atlas. Upstream cached the atlas but
        // not the Material, so every swap ran `new Material(Shader.Find(...))` again and the old ones
        // were never released - the per-update material leak in the research notes (Risk 3). The shader
        // choice below depends only on a settings flag, so one Material per archive is all that is ever
        // needed, and every batch on that archive can share it (sharedMaterial, not material).
        static readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();

        /// <summary>
        /// Replacement for DaggerfallBillboardBatch.SetMaterial that supports archives > 511.
        /// </summary>
        public static void RevisedSetMaterial(DaggerfallBillboardBatch batch, int archive, bool force)
        {
            int cur  = batch.currentArchive;                    // MOBILE: was reflection
            if (archive == cur && !force) return;

            // 1) pull down all atlas data
            RevisedGetTextureResults(
                archive,
                out Rect[]       atlasRects,
                out RecordIndex[] atlasIndices,
                out int[]        frameCounts,
                out Vector2[]    recordSizes,
                out Vector2[]    recordScales,
                out int          key);

            // 2) build your material using the freshly‐packed albedo atlas
            // MOBILE: cached per archive - built once, then reused by every batch on that archive.
            Material mat;
            if (!_materialCache.TryGetValue(archive, out mat) || mat == null)
            {
                string shaderName = DaggerfallUnity.Settings.NatureBillboardShadows
                    ? MaterialReader._DaggerfallBillboardBatchShaderName
                    : MaterialReader._DaggerfallBillboardBatchNoShadowsShaderName;
                // MOBILE: MobileShaders.Find, so this is the player's own billboard-batch shader and never
                // the copy a mod bundle embeds alongside its materials.
                mat = new Material(DaggerfallWorkshop.Game.Mobile.MobileShaders.Find(shaderName))
                {
                    mainTexture = _lastAtlas
                };
                mat.name = "WODBiomesNature" + archive;
                _materialCache[archive] = mat;
            }

            // 3) assemble a CachedMaterial
            var cm = new CachedMaterial
            {
                atlasRects       = atlasRects,
                atlasIndices     = atlasIndices,
                atlasFrameCounts = frameCounts,
                recordSizes      = recordSizes,
                recordScales     = recordScales,
                key              = key
            };

            // 4) shove everything back into DaggerfallBillboardBatch
            // MOBILE: direct assignment, was reflection (see the note on CustomBillboardHelper).
            batch.cachedMaterial = cm;
            batch.TextureArchive = archive;
            batch.currentArchive = archive;

            // 5) assign material to renderer
            var rend = batch.GetComponent<MeshRenderer>();
            rend.sharedMaterial    = mat;
            rend.receiveShadows    = false;
            rend.shadowCastingMode = (archive == TextureReader.LightsTextureArchive)
                                     ? ShadowCastingMode.Off
                                     : ShadowCastingMode.TwoSided;
        }

        /// <summary>
        /// Packs both vanilla and mod textures into a single atlas, and returns
        /// all the arrays you need plus an integer key.
        /// </summary>
        public static void RevisedGetTextureResults(
            int archive,
            out Rect[]       atlasRects,
            out RecordIndex[] atlasIndices,
            out int[]        frameCounts,
            out Vector2[]    recordSizes,
            out Vector2[]    recordScales,
            out int          key)
        {
            // if we've already built this archive, replay it directly
            if (_atlasCache.TryGetValue(archive, out var ca))
            {
                _lastAtlas    = ca.atlas;
                atlasRects    = ca.rects;
                atlasIndices  = ca.indices;
                frameCounts   = ca.frameCounts;
                recordSizes   = ca.sizes;
                recordScales  = ca.scales;
                key           = archive;
                return;
            }

            // prepare settings
            var settings = new GetTextureSettings
            {
                archive      = archive,
                stayReadable = true,
                // MOBILE: pinned - see NatureBatchOverrider.AtlasMaxSize. Was `AssetInjection ? 4096 : 2048`.
                atlasMaxSize = NatureBatchOverrider.AtlasMaxSize,
                atlasPadding = 4
            };

            // results container
            var results = new GetTextureResults
            {
                atlasSizes       = new List<Vector2>(),
                atlasScales      = new List<Vector2>(),
                atlasOffsets     = new List<Vector2>(),
                atlasFrameCounts = new List<int>()
            };

            // load Arena2 .tex if present
            if (settings.textureFile == null)
            {
                string path = Path.Combine(DaggerfallUnity.Instance.Arena2Path,
                                           TextureFile.IndexToFileName(settings.archive));
                if (File.Exists(path))
                    settings.textureFile = new TextureFile(path, FileUsage.UseMemory, true);
            }
            var texFile = settings.textureFile;

            bool hasNormals = false, hasEmissive = false, hasAnim = false;
            // MOBILE: upstream derived this from `atlasMaxSize == 4096`, which was itself just
            // `Settings.AssetInjection`. The atlas size is pinned now, so read the setting directly and
            // the import mode stays exactly what it was upstream.
            var importMode = DaggerfallUnity.Settings.AssetInjection
                         ? TextureImport.AllLocations
                         : TextureImport.None;

            var albedos     = new List<Texture2D>();
            var normalsList = new List<Texture2D>();
            var emissions   = new List<Texture2D>();
            var indicesList = new List<RecordIndex>();

            int recordCount = texFile?.RecordCount ?? 0;
            var reader = new TextureReader(DaggerfallUnity.Instance.Arena2Path);

            if (recordCount > 0)
            {
                // vanilla Arena2 archive
                for (int rec = 0; rec < recordCount; rec++)
                {
                    settings.record = rec;
                    int frames = texFile.GetFrameCount(rec);
                    if (frames > 1) hasAnim = true;

                    var size   = texFile.GetSize(rec);
                    var scale  = texFile.GetScale(rec);
                    var offset = texFile.GetOffset(rec);

                    indicesList.Add(new RecordIndex
                    {
                        startIndex = albedos.Count,
                        frameCount = frames,
                        width      = size.Width,
                        height     = size.Height
                    });

                    for (int f = 0; f < frames; f++)
                    {
                        settings.frame = f;
                        var r = reader.GetTexture2D(settings, SupportedAlphaTextureFormats.ARGB32, importMode);
                        albedos.Add(r.albedoMap);
                        if (r.normalMap  != null) { normalsList.Add(r.normalMap);   hasNormals = true; }
                        if (r.emissionMap!= null) { emissions.Add(r.emissionMap); hasEmissive= true; }
                    }

                    results.atlasSizes.Add (new Vector2(size.Width, size.Height));
                    results.atlasScales.Add(new Vector2(scale.Width, scale.Height));
                    results.atlasOffsets.Add(new Vector2(offset.X, offset.Y));
                    results.atlasFrameCounts.Add(frames);
                }
            }
            else
            {
                // no vanilla archive → load mods
                ProcessCustomTextures(settings, albedos, normalsList, emissions, indicesList, results);
            }

            // pack albedo into _lastAtlas
            _lastAtlas   = new Texture2D(settings.atlasMaxSize, settings.atlasMaxSize, TextureFormat.ARGB32, true);
            atlasRects   = _lastAtlas.PackTextures(albedos.ToArray(), settings.atlasPadding, settings.atlasMaxSize, false);
            if (albedos.Count > 0)
            {
                var sample = albedos[0];
                _lastAtlas.filterMode = sample.filterMode;
                _lastAtlas.wrapMode   = sample.wrapMode;
                _lastAtlas.anisoLevel = sample.anisoLevel;
            }
            atlasIndices = indicesList.ToArray();
            frameCounts  = results.atlasFrameCounts.ToArray();

            // (you can also pack normals/emissions here if desired)

            recordSizes  = results.atlasSizes.ToArray();
            recordScales = results.atlasScales.ToArray();

            if (!WODBiomes.VEModEnabled)for (int i = 0; i < recordSizes.Length; i++) // Scale textures x2
                recordSizes[i] *= 2f;

            key          = archive; // or hash all arrays for a unique key
        }

        /// <summary>
        /// Your existing mod‐texture fallback logic, verbatim.
        /// </summary>
        static void ProcessCustomTextures(
            GetTextureSettings  settings,
            List<Texture2D>     albedoTextures,
            List<Texture2D>     normalTextures,
            List<Texture2D>     emissionTextures,
            List<RecordIndex>   indices,
            GetTextureResults   results)
        {
            for (int record = 0; record < 256; record++)
            {
                settings.record = record;
                int frameCount = 0;
                var modFrames = new List<Texture2D>();

                while (true)
                {
                    settings.frame = frameCount;
                    if (TextureReplacement.TryImportTexture(
                        settings.archive, record, frameCount, out Texture2D modAlbedo))
                    {
                        modFrames.Add(modAlbedo);
                        albedoTextures.Add(modAlbedo);
                        frameCount++;
                    }
                    else break;
                }

                if (frameCount > 0)
                {
                    indices.Add(new RecordIndex
                    {
                        startIndex = albedoTextures.Count - frameCount,
                        frameCount = frameCount,
                        width      = modFrames[0].width,
                        height     = modFrames[0].height
                    });

                    results.atlasSizes.Add       (new Vector2(modFrames[0].width,  modFrames[0].height));
                    results.atlasScales.Add      (Vector2.one);
                    results.atlasOffsets.Add     (Vector2.zero);
                    results.atlasFrameCounts.Add (frameCount);
                }
            }
        }
    }
}

