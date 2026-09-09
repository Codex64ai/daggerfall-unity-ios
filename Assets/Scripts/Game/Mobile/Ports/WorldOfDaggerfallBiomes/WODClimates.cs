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
        /// MOBILE: pinned atlas size, replacing upstream's 4096 (or 2048). Upstream allocated the full
        /// square up front: a transient 4096x4096 ARGB32 with mips is ~85 MB, on a device that has to
        /// survive a jetsam limit. Archive 10030's 32 records are NOT 64x64 - measured with
        /// tools/dfmod_inspect.py on the Daggerfall Expanded Textures iOS bundle they run from 25x11 up
        /// to 130x153, 115x272 and 113x296, ~202 k px of content and ~225 k px once padded by 4. So
        /// 1024^2 (1,048,576 px) is the right pin, with ~4.6x headroom - and 512^2 (262,144 px) would be
        /// marginal, not comfortable. Do not "optimise" this down.
        /// </summary>
        public const int AtlasMaxSize = 1024;

        // MOBILE: there is deliberately no set of already-swapped batches here. Plan Step 2 asked for one,
        // and the first cut had it, but it could never change control flow: a swapped batch reports
        // TextureArchive == NEW_ARCHIVE, and the `TextureArchive != NATURE_ARCHIVE` filter below already
        // skips exactly those batches - for one int compare instead of a hash lookup plus a RemoveWhere
        // scan per terrain update. The archive filter alone is what makes the swap once-per-batch and what
        // lets RevisedSetMaterial be called with force:false; the engine putting a batch back on 501
        // (StreamingWorld.UpdateTerrainNature, DaggerfallLocation.ApplyClimateSettings) is likewise handled
        // by the filter, since such a batch simply matches again.

        // MOBILE: one line per distinct failure, not one per terrain update.
        static readonly HashSet<string> loggedErrors = new HashSet<string>();
        static bool warnedUnreadableMap;
        static bool loggedReadableCopies;

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

        /// <summary>
        /// MOBILE: pure, so the self-test can pin it. True when a texture exists but cannot be sampled on
        /// the CPU, and therefore has to go through TextureReplacement.EnsureReadable before it can be fed
        /// to Texture2D.PackTextures. Archive 10030's records arrive from the Daggerfall Expanded Textures
        /// bundle, whose textures the pack importer imports with isReadable = false, so this is the normal
        /// case on device - not an edge case. See CustomBillboardHelper.Readable.
        /// </summary>
        public static bool NeedsReadableCopy(Texture2D tex)
        {
            return tex != null && !tex.isReadable;
        }

        /// <summary>
        /// MOBILE: pure, so the self-test can pin it. True when the probe loop actually imported
        /// something to pack. An empty albedo list is not a success: PackTextures over a zero-length
        /// array yields no rects, and DaggerfallBillboardBatch.Apply then indexes an empty atlasRects
        /// for billboard items laid out for the vanilla archive - an exception in the editor, an
        /// out-of-bounds read in an IL2CPP player with safety checks off, and either way our own log
        /// would claim the swap worked. See CustomBillboardHelper.RevisedGetTextureResults.
        /// </summary>
        public static bool HasRecords(int count)
        {
            return count > 0;
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
                // MOBILE: the one filter that matters. It skips non-nature batches, and it skips the ones
                // already swapped (they report NEW_ARCHIVE) before the parent walk, the GetPixel and above
                // all batch.Apply(), which rebuilds the batch mesh.
                if (batch.TextureArchive != NATURE_ARCHIVE)
                    continue;

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
                    // unbounded leak plus a Shader.Find each time. The material and the atlas are cached
                    // per archive in CustomBillboardHelper now, and the archive filter above stops
                    // Apply() re-running.
                    // MOBILE: and it returns false when the atlas could not be built from readable
                    // records - then the batch keeps its vanilla nature rather than turning blank, and
                    // n is not incremented, so the "swapped N" line cannot claim a swap that failed.
                    if (!CustomBillboardHelper.RevisedSetMaterial(batch, NEW_ARCHIVE, false))
                        continue;
                    batch.Apply();
                    n++;
                }
            }

            // MOBILE: only when something actually changed - this used to be silent, and the simulator
            // and device runs grep for this line as the proof the swap fired.
            if (n > 0)
                Debug.Log("[Biomes] swapped " + n + " nature batches to archive " + NEW_ARCHIVE);

            // MOBILE: Task 9 left one question open. Every simulator launch showed zero
            // "[TextureReplacement] made a readable copy of ..." lines even where the whole 10030 atlas
            // was built out of the ASTC DET bundle, so whether CustomBillboardHelper.Readable's
            // EnsureReadable fallback is load-bearing on real hardware is still unsettled. One line per
            // session, printed only once the atlas exists, answers it from the device log.
            if (!loggedReadableCopies && CustomBillboardHelper.TryGetReadableCopyCount(NEW_ARCHIVE, out int copies))
            {
                Debug.Log("[Biomes] archive " + NEW_ARCHIVE + ": " + copies + " records copied to readable memory");
                loggedReadableCopies = true;
            }
        }
    }

    static class CustomBillboardHelper
    {
        // MOBILE: the three FieldInfo caches and the static constructor that filled them are gone.
        // DaggerfallBillboardBatch.currentArchive and cachedMaterial are internal in this fork, so the
        // fields below are assigned directly. The wins are compile-time checking (a rename now breaks
        // the build instead of the swap), no static constructor, and no three BindingFlags lookups.
        // Stripping was never the issue: Assets/link.xml preserves Assembly-CSharp whole, and the batch
        // reads and writes these fields itself, so managed stripping would have kept them either way.

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

        // MOBILE: archives whose atlas could not be built from readable records - warned about once each.
        static readonly HashSet<int> _warnedUnreadableArchives = new HashSet<int>();

        // MOBILE: archive -> how many of its records Readable() had to copy through EnsureReadable to
        // make them packable. Written only on a successful build, so a present entry means the atlas for
        // that archive exists. NatureBatchOverrider prints it once; see the note there.
        static readonly Dictionary<int, int> _readableCopies = new Dictionary<int, int>();
        static int _readableCopiesThisBuild;

        /// <summary>MOBILE: the readable-copy count for a built archive, for the one-line log.</summary>
        public static bool TryGetReadableCopyCount(int archive, out int count)
        {
            return _readableCopies.TryGetValue(archive, out count);
        }

        /// <summary>
        /// MOBILE: Texture2D.PackTextures needs CPU-readable inputs, and it does not fail loudly when it
        /// does not get them - it logs "Texture atlas needs textures to have Readable flag set!" and packs
        /// nothing, so every billboard on the atlas renders blank. This fork already hit that (see the note
        /// at TextureReplacement.cs:845-851) and fixed the engine's own copy of this routine at
        /// TextureReader.cs:488. It matters here more than anywhere: archive 10030's records come from the
        /// Daggerfall Expanded Textures *bundle*, and the pack importer's default rule imports bundle
        /// textures with isReadable = false (MobileModBuilder.cs), so without this every record is
        /// unreadable on device and in the simulator alike. Returns null when the texture is missing or
        /// cannot be made readable, so the caller abandons the swap instead of packing garbage.
        /// </summary>
        static Texture2D Readable(Texture2D tex)
        {
            if (tex == null)
                return null;
            if (!NatureBatchOverrider.NeedsReadableCopy(tex))
                return tex;
            Texture2D copy = TextureReplacement.EnsureReadable(tex);
            if (copy == null || !copy.isReadable)
                return null;
            _readableCopiesThisBuild++;                     // MOBILE: counted for the one-line log
            return copy;
        }

        // MOBILE: one line per archive, not one per record per batch.
        static void WarnUnreadableRecord(int archive, int record, int frame)
        {
            if (_warnedUnreadableArchives.Add(archive))
                Debug.LogError($"[Biomes] archive {archive} record {record} frame {frame} could not be made "
                             + "CPU-readable; the atlas would pack blank, so the nature swap is skipped");
        }

        // MOBILE: the other way an atlas build can come back useless - nothing was imported at all.
        // Same once-per-archive mechanism (and the same set) as the unreadable warning above: both are
        // terminal for that archive, so one line per archive is the whole story worth logging.
        static void WarnEmptyAtlas(int archive)
        {
            if (_warnedUnreadableArchives.Add(archive))
                Debug.LogError($"[Biomes] archive {archive} imported no records; the atlas would be empty, "
                             + "so the nature swap is skipped");
        }

        /// <summary>
        /// Replacement for DaggerfallBillboardBatch.SetMaterial that supports archives > 511.
        /// MOBILE: returns true only when the batch was actually put on the new archive. False means the
        /// batch was left exactly as it was - either it is already on this archive, or the atlas could not
        /// be built from readable records - so the caller must not call Apply() or count a swap.
        /// </summary>
        public static bool RevisedSetMaterial(DaggerfallBillboardBatch batch, int archive, bool force)
        {
            int cur  = batch.currentArchive;                    // MOBILE: was reflection
            if (archive == cur && !force) return false;

            // 1) pull down all atlas data
            // MOBILE: false = the atlas could not be built (unreadable records). Leave the batch alone.
            if (!RevisedGetTextureResults(
                archive,
                out Rect[]       atlasRects,
                out RecordIndex[] atlasIndices,
                out int[]        frameCounts,
                out Vector2[]    recordSizes,
                out Vector2[]    recordScales,
                out int          key))
                return false;

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
            return true;                                        // MOBILE
        }

        /// <summary>
        /// Packs both vanilla and mod textures into a single atlas, and returns
        /// all the arrays you need plus an integer key.
        /// </summary>
        /// MOBILE: returns false when the atlas could not be built from readable records.
        public static bool RevisedGetTextureResults(
            int archive,
            out Rect[]       atlasRects,
            out RecordIndex[] atlasIndices,
            out int[]        frameCounts,
            out Vector2[]    recordSizes,
            out Vector2[]    recordScales,
            out int          key)
        {
            // MOBILE: every out is assigned up front, so the failure returns below are legal and a caller
            // that ignores the bool gets empty arrays rather than nulls.
            atlasRects   = new Rect[0];
            atlasIndices = new RecordIndex[0];
            frameCounts  = new int[0];
            recordSizes  = new Vector2[0];
            recordScales = new Vector2[0];
            key          = 0;

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
                return true;
            }

            _readableCopiesThisBuild = 0;                   // MOBILE: counts this build only

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
                        // MOBILE: PackTextures needs readable inputs - see Readable(). This branch reads
                        // Arena2, which is readable already, but a texture replacement for a vanilla
                        // archive can still arrive from a bundle, so it goes through the same gate.
                        Texture2D albedo = Readable(r.albedoMap);
                        if (albedo == null)
                        {
                            WarnUnreadableRecord(archive, rec, f);
                            return false;
                        }
                        albedos.Add(albedo);
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
                // MOBILE: false = a record could not be made readable. This is the branch archive 10030
                // takes, so it is the one that actually matters.
                if (!ProcessCustomTextures(settings, albedos, normalsList, emissions, indicesList, results))
                    return false;
            }

            // MOBILE: fail closed on an empty import. The probe loop can legitimately find nothing -
            // AssetInjection off in settings.ini, or a Daggerfall Expanded Textures build without
            // archive 10030 - and upstream treated that as success: PackTextures over a zero-length
            // array, empty atlasIndices/frameCounts into the CachedMaterial, the batch put on 10030 and
            // the swap counted, then Apply() indexing an empty atlasRects for items laid out for archive
            // 501. Returning false here keeps vanilla nature, and because the cache write is below this
            // point it leaves _atlasCache empty, so a later terrain update retries once DET resolves.
            if (!NatureBatchOverrider.HasRecords(albedos.Count))
            {
                WarnEmptyAtlas(archive);
                return false;
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

            // MOBILE: _atlasCache was declared and read but never written (upstream never wrote it
            // either), so every batch rebuilt the whole thing: the 256-record TryImportTexture probe
            // loop, a fresh 1024x1024 ARGB32-with-mips Texture2D (~5.3 MB) and another PackTextures -
            // some 49 times on first entry to a subtropical region at TerrainDistance 3, with every
            // atlas but the first orphaned on the spot and reclaimed only by the 180-second-throttled
            // Resources.UnloadUnusedAssets. Stored after the x2 size scaling above, so the replay path
            // (which returns before that loop) serves already-scaled sizes and cannot double-scale.
            // This is also what finally puts the cached Material beside the atlas its rects came from,
            // instead of relying on PackTextures being deterministic across rebuilds.
            _atlasCache[archive] = new CachedAtlas
            {
                atlas       = _lastAtlas,
                rects       = atlasRects,
                indices     = atlasIndices,
                frameCounts = frameCounts,
                sizes       = recordSizes,
                scales      = recordScales
            };
            _readableCopies[archive] = _readableCopiesThisBuild; // MOBILE: only a built archive counts

            return true;                                        // MOBILE
        }

        /// <summary>
        /// Your existing mod‐texture fallback logic, verbatim.
        /// </summary>
        /// MOBILE: returns false when a record could not be made CPU-readable.
        static bool ProcessCustomTextures(
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
                        // MOBILE: the load-bearing fix. These come out of the Daggerfall Expanded
                        // Textures bundle unreadable (TryImportTextureFromMods only logs "Texture
                        // 10030_x-0 is not readable"), and PackTextures would then pack nothing while
                        // our own log claimed the swap worked. See Readable().
                        Texture2D readable = Readable(modAlbedo);
                        if (readable == null)
                        {
                            WarnUnreadableRecord(settings.archive, record, frameCount);
                            return false;
                        }
                        modFrames.Add(readable);
                        albedoTextures.Add(readable);
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

            return true;                                        // MOBILE
        }
    }
}

