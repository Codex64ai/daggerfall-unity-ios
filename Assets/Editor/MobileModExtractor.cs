// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Extracts a desktop-built .dfmod back into loose project assets so MobileModBuilder can
// repack it for iOS. Mods are distributed only as desktop AssetBundles; iOS needs its own
// bundles, so conversion means: load bundle -> write assets to disk (short names preserved
// verbatim - every DFU runtime lookup is by short name) -> rewrite the manifest -> rebuild.
//
// v1 handles Texture2D, AudioClip and TextAsset (added per task); everything else is
// skipped and counted in the report. Extraction output goes under Assets/Game/Mods/Converted/,
// which is gitignored - converted third-party content must never be committed.
//
// Bundle textures are normally compressed and non-readable, so decoding them means a GPU
// blit: run the editor WITHOUT -nographics or extraction refuses (see TexturePng.Encode).
//
// Two limitations worth knowing before converting a real mod. Textures are always re-encoded
// as .png, and DFU keys runtime lookups on the lowercased short name WITH its extension
// (ModManager.GetAssetName), so a source foo.tga becomes reachable only as foo.png; DFU's own
// convention is .png (TextureReplacement), so this is normally a no-op, but it is counted in
// the report rather than left silent. And asset paths are rebuilt from the source manifest,
// because AssetBundle.GetAllAssetNames lowercases them while Mod.FindAssetNames matches
// case-sensitively - an asset the manifest does not list cannot have its casing recovered.
//
// Audio comes back as 16-bit PCM .wav and nothing else. A bundle keeps an AudioClip as decoded
// samples, not as the file the author imported, so there is no original to copy out - the
// container is rebuilt around the samples (EncodeWav) and a source .ogg therefore changes its
// runtime lookup name to .wav, counted like the texture case.
//
// AND IT ONLY REACHES CLIPS THE AUTHOR IMPORTED AS DecompressOnLoad. AudioClip.GetData reads
// decoded PCM, so a Streaming clip (samples never in memory) and a CompressedInMemory one
// (samples still encoded, with no API that returns the encoded bytes) are both unreachable.
// DecompressOnLoad is Unity's own default, so a clip nobody configured converts fine; a clip
// somebody DID configure - which is what a large mod's music tends to be - may not. Those are
// skipped, counted under "AudioClip(streaming)" / "AudioClip(compressed)" and warned about
// individually, because for a music module that skip may be the entire module and the operator
// has to learn it from the report rather than from a silent game. There is no workaround on
// this side: the fix is the source audio, or a desktop rebuild of that module with its clips
// set to DecompressOnLoad.
//
// Textures are not all colour. A compressed normal map has had its blue channel thrown away
// and the remaining two swizzled into whichever channels its block format codes best, so
// extracting one byte-for-byte produces an image that is not a normal map at all; DFU's
// *_Normal / *_Height / *_MetallicGloss suffixes are what tells them apart, and the extractor
// rebuilds z and keeps them out of sRGB (see NormalUnswizzlerFor).
//
// MobileConvertedModImporter at the bottom of this file decides how the extraction is then
// imported. That policy - compressed, not readable, size-capped, normal maps typed - is what
// makes a multi-gigabyte texture pack fit in an iPad's memory, so it is not cosmetic. Its
// levers are environment variables (MobileConvertedModPolicy), because they get tuned against
// a real device and recompiling per attempt is the wrong loop:
//
//   DFU_MOD_MAXTEXSIZE   cap, power of two 32-16384          default 1024
//   DFU_MOD_ASTC         iOS block size, 4x4|5x5|6x6|8x8|10x10|12x12   default 6x6
//   DFU_MOD_TEX_QUALITY  compressor effort 0-100             default 50
//   DFU_MOD_MIPS         master mipmap switch                default on
//   DFU_MOD_NOMIP        path substrings that get no mipmaps default DFU's 2D art
//   DFU_MOD_STREAM_MIPS  mipmap streaming                    default off (see the property)
//
// A change to one of these reaches a mod on its next conversion; Unity's import cache cannot
// see an environment variable move, so re-convert rather than expecting a reimport.
//
// A .dfmod is untrusted input: it is a file a stranger hands us, and the manifest inside it is
// the part of a mod an attacker fully controls. Every output path is therefore checked for
// containment under outputRoot before a single byte is written (see IsInsideRoot).
//
// Place in Assets/Editor/

using System;
using System.Collections.Generic;
using System.IO;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using UnityEditor;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    /// <summary>The outcome of one extraction. The two counters answer different questions and
    /// must never be summed together: skippedByType counts assets that did NOT reach the
    /// extraction - an unsupported type, a colliding output path, a path that escaped the root,
    /// a write that failed - so its total is a genuine loss report and is what a caller should
    /// show as "N assets skipped" - and for audio it is the ONLY record that a clip could not be
    /// decoded at all, so it has to be read per key and not merely totalled. notesByType counts
    /// assets that WERE extracted but needed something said about them: a texture re-encoded to
    /// .png (or a clip to .wav) under a new runtime lookup name, or a path whose capitalisation
    /// the source manifest did not record.</summary>
    public class ExtractReport
    {
        public string manifestPath;
        public List<string> extracted = new List<string>();
        public Dictionary<string, int> skippedByType = new Dictionary<string, int>();
        public Dictionary<string, int> notesByType = new Dictionary<string, int>();
    }

    public static class MobileModExtractor
    {
        public static ExtractReport Extract(string dfmodPath, string outputRoot)
        {
            if (!File.Exists(dfmodPath))
                throw new FileNotFoundException("Desktop .dfmod not found", dfmodPath);

            var report = new ExtractReport();
            AssetBundle ab = AssetBundle.LoadFromFile(dfmodPath);
            if (ab == null)
                throw new InvalidDataException("Could not load AssetBundle (wrong platform or corrupt): " + dfmodPath);

            try
            {
                // Read the manifest before anything else: besides identity it is the only record
                // of the author's original path capitalisation, which the extraction has to keep.
                ModInfo modInfo = ReadManifest(ab, dfmodPath);
                var originalCase = new Dictionary<string, string>(StringComparer.Ordinal);
                if (modInfo.Files != null)
                {
                    foreach (string file in modInfo.Files)
                    {
                        string norm = file.Replace('\\', '/');
                        originalCase[norm.ToLowerInvariant()] = norm;
                    }
                }

                // One output FILE may be claimed by only one bundle asset; see Claim().
                var claimed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (string assetName in ab.GetAllAssetNames())
                {
                    if (assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                        continue; // rewritten below, not copied verbatim

                    // Notes describe assets that SURVIVED, so they are held until the write has
                    // actually happened and thrown away if it has not; see CommitNotes.
                    var notes = new List<string>();
                    string outPath = OutputPathFor(assetName, outputRoot, originalCase, notes);
                    var obj = ab.LoadAsset<UnityEngine.Object>(assetName);
                    if (obj is Texture2D tex2d)
                    {
                        // Textures are always re-encoded as .png, which moves the runtime lookup
                        // key with them (it is the short name WITH extension). Report it.
                        if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            notes.Add("extension-rewritten");
                            Debug.LogWarning($"[MobileModExtractor] {assetName} is re-encoded as .png, " +
                                "so its runtime lookup name changes with it");
                            outPath = Path.ChangeExtension(outPath, ".png");
                        }
                        if (!Claim(claimed, outPath, assetName, report))
                            continue;
                        // Whether the blit degammas is decided by the SOURCE texture's graphics
                        // format and by nothing else. Choosing it from the file NAME instead
                        // looks equivalent and is not: a stranger's bundle can perfectly well
                        // contain an sRGB-flagged "*_Height" (nothing forces a height map's
                        // sRGBTexture off, and the importer default is on), and reading that
                        // through a Linear RenderTexture degammas on sample with nothing
                        // re-encoding on write - every mid-tone byte moved, silently. The name
                        // rule still decides the DESTINATION policy, where it belongs, in
                        // MobileConvertedModImporter. Here the source is the only authority.
                        bool linear = !tex2d.isDataSRGB;
                        Func<Color32, Color32> unswizzle = IsNormalMapName(assetName)
                            ? NormalUnswizzlerFor(tex2d.format, tex2d.isDataSRGB, notes) : null;
                        if (!TryWriteFile(outPath, TexturePng.Encode(tex2d, linear, unswizzle),
                                outputRoot, assetName, report))
                            continue;
                        report.extracted.Add(outPath);
                        CommitNotes(report, notes);
                    }
                    else if (obj is TextAsset textAsset)
                    {
                        if (!Claim(claimed, outPath, assetName, report))
                            continue;
                        if (!TryWriteFile(outPath, textAsset.bytes, outputRoot, assetName, report))
                            continue;
                        report.extracted.Add(outPath);
                        CommitNotes(report, notes);
                    }
                    else if (obj is AudioClip clip)
                    {
                        // Same rule as textures, and for the same reason. A bundle keeps an
                        // AudioClip as samples, not as the author's file, so the extraction has
                        // to re-author a container - and the only one that can be written from
                        // raw samples without an encoder is PCM WAV. A source .ogg therefore
                        // comes back as .wav, which moves DFU's runtime lookup key (the short
                        // name WITH its extension) with it. Counted, not left silent.
                        if (!outPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            notes.Add("extension-rewritten");
                            Debug.LogWarning($"[MobileModExtractor] {assetName} is re-encoded as .wav, " +
                                "so its runtime lookup name changes with it");
                            outPath = Path.ChangeExtension(outPath, ".wav");
                        }

                        // THE LIMIT OF THIS WHOLE APPROACH. AudioClip.GetData reads DECODED
                        // PCM, and only a DecompressOnLoad clip has any: Unity's own message is
                        // "Cannot get data on compressed samples for audio clip ... Changing the
                        // load type to DecompressOnLoad on the audio clip will fix this". A
                        // Streaming clip never had its samples in memory; a CompressedInMemory
                        // one holds them still encoded, and there is no API that hands the
                        // encoded bytes back either. Neither is recoverable here at all.
                        //
                        // Unity's own default for an audio import is DecompressOnLoad, so the
                        // clip an author never thought about DOES convert. The one an author
                        // thought about may not: CompressedInMemory and Streaming are exactly
                        // what a large mod's music gets set to, and a music module is where a
                        // whole-mod loss would land.
                        //
                        // They are refused up front rather than left to fail inside GetData for
                        // two reasons: GetData logs a Unity error per clip, and a music mod with
                        // a thousand of them would bury the report that matters under a thousand
                        // stack traces; and the load type is the actual diagnosis, so it belongs
                        // in the warning. Each is counted under its own key, because the two need
                        // different things from the mod author.
                        //
                        // Both refusals come BEFORE Claim on purpose: a clip whose samples
                        // cannot be read is not a writer, and letting it reserve the output path
                        // would cost a sibling that could actually have been written there (a
                        // streaming "song.ogg" beside a readable "song.wav" is one mod away).
                        if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
                        {
                            Skipped(report, clip.loadType == AudioClipLoadType.Streaming
                                ? "AudioClip(streaming)" : "AudioClip(compressed)");
                            Debug.LogWarning($"[MobileModExtractor] skipped {assetName}: its " +
                                $"load type is {clip.loadType}, and AudioClip.GetData can only " +
                                "read a clip imported as DecompressOnLoad. The samples are not " +
                                "reachable through any API here, so this clip cannot be " +
                                "converted from the bundle at all - it has to come from the " +
                                "module's source audio, or from a rebuild of the desktop mod " +
                                "with this clip set to DecompressOnLoad.");
                            continue;
                        }

                        var samples = new float[clip.samples * clip.channels];
                        if (!clip.GetData(samples, 0))
                        {
                            // Backstop: the load type said this should have worked. Something
                            // else did not - a clip with no samples, or a Unity-version change
                            // in what GetData accepts - and either way it is a loss, not a note.
                            Skipped(report, "AudioClip(nodata)");
                            Debug.LogWarning($"[MobileModExtractor] skipped {assetName}: GetData " +
                                $"failed on a {clip.loadType} clip ({clip.samples} samples, " +
                                $"{clip.channels} channels), which is not supposed to happen; " +
                                "the clip is skipped rather than written as silence.");
                            continue;
                        }
                        if (!Claim(claimed, outPath, assetName, report))
                            continue;
                        if (!TryWriteFile(outPath, EncodeWav(samples, clip.channels, clip.frequency),
                                outputRoot, assetName, report))
                            continue;
                        report.extracted.Add(outPath);
                        CommitNotes(report, notes);
                    }
                    else
                    {
                        string type = obj ? obj.GetType().Name : "null";
                        Skipped(report, type);
                        Debug.LogWarning($"[MobileModExtractor] skipped {assetName} ({type} not supported yet)");
                    }
                }

                // Manifest identity is preserved; only Files points at the extraction.
                modInfo.Files = new List<string>(report.extracted);
                ModManager._serializer.TrySerialize(modInfo, out fsData data);
                string manifestOut = Path.Combine(outputRoot,
                    Path.GetFileNameWithoutExtension(dfmodPath) + ModManager.MODINFOEXTENSION);
                // Same containment check; but a conversion with no manifest is not a mod, so
                // unlike a single bad asset this one cannot be skipped past.
                if (!TryWriteFile(manifestOut, System.Text.Encoding.UTF8.GetBytes(fsJsonPrinter.PrettyJson(data)),
                        outputRoot, "the rewritten manifest", report))
                    throw new InvalidDataException(
                        "Refusing to write the rewritten manifest outside the extraction root: " + manifestOut);
                report.manifestPath = manifestOut;
            }
            finally
            {
                ab.Unload(true);
            }

            AssetDatabase.Refresh();
            foreach (string p in report.extracted)
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            return report;
        }

        static ModInfo ReadManifest(AssetBundle ab, string dfmodPath)
        {
            ModInfo modInfo = null;
            foreach (string assetName in ab.GetAllAssetNames())
            {
                if (!assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                    continue;
                var manifestAsset = ab.LoadAsset<TextAsset>(assetName);
                if (manifestAsset != null)
                    ModManager._serializer.TryDeserialize(fsJsonParser.Parse(manifestAsset.text), ref modInfo);
                break;
            }
            if (modInfo == null || string.IsNullOrWhiteSpace(modInfo.ModTitle))
                throw new InvalidDataException("Bundle has no readable .dfmod.json manifest: " + dfmodPath);
            return modInfo;
        }

        /// <summary>Rebuilds the asset's path under outputRoot from the source manifest's own
        /// Files entry, keeping the author's capitalisation and the leading "Assets/".
        /// Both matter: DFU locates loose mod content with Mod.FindAssetNames, which accepts an
        /// asset whose directory *ends with* the requested one and compares with a case-sensitive
        /// CompareOrdinal - so the lowercase "assets/textures" that AssetBundle.GetAllAssetNames
        /// hands back would never match a caller's "Assets/Textures" (TextAssetReader and
        /// WorldDataReplacement both pass literal capitalised paths), and the converted mod would
        /// silently lose loose-file injection. Only when the manifest does not list the asset at
        /// all is the lowercase bundle path used, stripped of its "assets/" prefix.</summary>
        static string OutputPathFor(string bundleAssetName, string outputRoot,
            Dictionary<string, string> originalCase, List<string> notes)
        {
            string tail = bundleAssetName.Replace('\\', '/');
            string original;
            if (originalCase.TryGetValue(tail.ToLowerInvariant(), out original))
                return Path.Combine(outputRoot, original);

            notes.Add("unlisted-in-manifest");
            Debug.LogWarning($"[MobileModExtractor] {bundleAssetName} is not listed in the source " +
                "manifest, so its original capitalisation cannot be recovered; DFU's case-sensitive " +
                "directory lookups may not find it");
            if (tail.StartsWith("assets/", StringComparison.Ordinal))
                tail = tail.Substring("assets/".Length);
            return Path.Combine(outputRoot, tail);
        }

        /// <summary>Reserves an output path for one bundle asset, returning false if another
        /// already holds it - most easily a foo.tga and a foo.png, which collapse onto the same
        /// file once the texture extension is rewritten. Overwriting would lose one asset outright
        /// and list the survivor twice in the rebuilt manifest, so the first writer wins and the
        /// clash is reported rather than resolved silently.</summary>
        static bool Claim(Dictionary<string, string> claimed, string outPath, string assetName,
            ExtractReport report)
        {
            // Key on the RESOLVED path: the manifest is attacker-controlled and "A/x.png",
            // "A/./x.png" and "A/sub/../x.png" are three spellings of one file. Keyed on the raw
            // string they would look like three separate assets, so the later writes would
            // silently overwrite the earlier one and the rebuilt manifest would list the same
            // file more than once - which Unity then refuses to pack at all.
            string key;
            try { key = Path.GetFullPath(outPath); }
            catch (Exception) { key = outPath; }

            string owner;
            if (!claimed.TryGetValue(key, out owner))
            {
                claimed[key] = assetName;
                return true;
            }

            Skipped(report, "collision");
            Debug.LogWarning($"[MobileModExtractor] {assetName} and {owner} both map to {outPath}; " +
                $"keeping {owner} and skipping {assetName} rather than overwriting it");
            return false;
        }

        /// <summary>True when the name carries DFU's normal-map suffix. The name is all there is
        /// to go on: TextureReplacement.GetName writes "_" + the TextureMap enum name onto every
        /// non-albedo map ("004_0-0_Normal.png"), while a texture inside a bundle carries no
        /// record of the importer settings it was built with.</summary>
        public static bool IsNormalMapName(string assetName)
        {
            return HasMapSuffix(assetName, "Normal");
        }

        /// <summary>True when the name says the texture holds numbers rather than colours - the
        /// same three maps DFU itself treats as linear in TextureReplacement.IsLinearTextureMap.
        /// Emission and Mask are deliberately absent: those are colour, and forcing them linear
        /// would regrade them exactly as badly as leaving a normal map in sRGB does.</summary>
        public static bool IsLinearMapName(string assetName)
        {
            return HasMapSuffix(assetName, "Normal")
                || HasMapSuffix(assetName, "Height")
                || HasMapSuffix(assetName, "MetallicGloss");
        }

        static bool HasMapSuffix(string assetName, string map)
        {
            return Path.GetFileNameWithoutExtension(assetName)
                .EndsWith("_" + map, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Picks the per-pixel fixup a normal map needs to become an ordinary RGB
        /// normal map again, or null when it already is one.
        ///
        /// A compressed normal map is not an image of a normal map. Unity drops z at import
        /// time - it is recoverable, so spending bits on it is waste - and stores what is left
        /// in whichever two channels its block format codes best: DXT5nm puts y in green and x
        /// in ALPHA (rgb are left as 1s), BC5 is a two-channel format holding x and y in red
        /// and green with nothing in blue or alpha. Written straight to a .png either one is
        /// garbage: DXT5nm reads as a white image, BC5 as a flat blue-less one, and both
        /// re-import as ordinary colour textures that light nothing correctly. Rebuilding z
        /// with ReconstructNormalPixel restores the standard encoding every shader expects.
        ///
        /// The decision is made on the format because that is where the swizzle actually lives;
        /// an uncompressed or BC7 normal map already holds x,y,z in r,g,b and is passed through
        /// unchanged. An sRGB-flagged source is passed through too, and that check has to come
        /// first: Unity never marks a NormalMap-typed texture sRGB, so an sRGB source named
        /// "*_Normal" is an ordinary colour texture that merely shares the suffix, and
        /// unswizzling its perfectly good rgb would be the corruption rather than the fix.
        /// Every outcome is counted so a real conversion can be checked rather than trusted.</summary>
        static Func<Color32, Color32> NormalUnswizzlerFor(TextureFormat format, bool sourceIsSRGB,
            List<string> notes)
        {
            if (sourceIsSRGB)
            {
                notes.Add("normal-srgb-source-kept-as-is");
                return null;
            }
            switch (format)
            {
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                    notes.Add("normal-unswizzled-dxt5nm");
                    return c => ReconstructNormalPixel(c, true);
                case TextureFormat.BC5:
                    notes.Add("normal-unswizzled-bc5");
                    return c => ReconstructNormalPixel(c, false);
                default:
                    notes.Add("normal-rgb-kept-as-is");
                    return null;
            }
        }

        /// <summary>Rebuilds one standard tangent-space normal pixel from a swizzled one.
        /// x comes from alpha for DXT5nm and from red for BC5; y is always green. Both are
        /// decoded from [0,255] to [-1,1], z is recovered as sqrt(1 - x^2 - y^2) - which is
        /// what makes dropping it lossless-in-principle, since a tangent-space normal is a
        /// unit vector with z >= 0 - and all three are re-encoded as n * 0.5 + 0.5, the
        /// mapping Unity's UnpackNormal undoes. So a collapsed z encodes to 128, not to 0.
        /// The max(0, ...) matters: block compression error alone can push x^2 + y^2 just past
        /// 1, and Mathf.Sqrt of a negative is NaN, which casts to an arbitrary byte.</summary>
        public static Color32 ReconstructNormalPixel(Color32 swizzled, bool xInAlpha)
        {
            float x = ((xInAlpha ? swizzled.a : swizzled.r) / 255f) * 2f - 1f;
            float y = (swizzled.g / 255f) * 2f - 1f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
            return new Color32(
                (byte)Mathf.RoundToInt((x * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((y * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((z * 0.5f + 0.5f) * 255f), 255);
        }

        /// <summary>Wraps raw float samples in a canonical 44-byte RIFF/WAVE header as 16-bit
        /// little-endian PCM - the one audio container that can be written from samples alone,
        /// with no encoder. This is the whole of the audio extraction: an AssetBundle stores an
        /// AudioClip as decoded samples and keeps nothing of the file the author imported, so
        /// there is no original to copy out and a container has to be built around them.
        ///
        /// 16-bit is not a compromise here. It is the width Unity's own importer decodes to for
        /// everything it does not keep as float, it is what the converted mod is re-encoded FROM
        /// (MobileConvertedModImporter puts it straight back into Vorbis), and it halves an
        /// intermediate that is already hundreds of megabytes for a music pack.
        ///
        /// Samples are clamped rather than cast. GetData can hand back values outside [-1,1] -
        /// a hot master, or any DSP that overshot - and the naive cast WRAPS: 1.5 scales to
        /// 49150, which truncates to -16386 and turns a loud peak into an equally loud click of
        /// the opposite sign. A NaN is flattened to silence for the same reason: casting one to
        /// an integer is undefined and yields whatever the platform's conversion happens to do.
        /// </summary>
        public static byte[] EncodeWav(float[] samples, int channels, int frequency)
        {
            if (samples == null)
                samples = new float[0];
            // A clip that reports nonsense must still produce a file a decoder can open.
            channels = Mathf.Max(1, channels);
            frequency = Mathf.Max(1, frequency);

            const short formatPcm = 1;
            const short bitsPerSample = 16;
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int byteRate = frequency * blockAlign;
            int dataBytes = samples.Length * (bitsPerSample / 8);

            var stream = new MemoryStream(44 + dataBytes);
            // BinaryWriter is little-endian on every runtime .NET defines, which is exactly
            // what RIFF wants; nothing here needs a byte order swap.
            using (var w = new BinaryWriter(stream))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);              // everything after this field
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                          // PCM fmt chunk size
                w.Write(formatPcm);
                w.Write((short)channels);
                w.Write(frequency);
                w.Write(byteRate);
                w.Write(blockAlign);
                w.Write(bitsPerSample);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float sample in samples)
                {
                    float s = float.IsNaN(sample) ? 0f : Mathf.Clamp(sample, -1f, 1f);
                    w.Write((short)Mathf.RoundToInt(s * short.MaxValue));
                }
                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The asset did not make it into the extraction.</summary>
        static void Skipped(ExtractReport report, string key) { Bump(report.skippedByType, key); }

        /// <summary>Records the notes gathered for one asset - and is called only once that
        /// asset's bytes are on disk. Notes are the half of the report that describes SURVIVORS,
        /// so a note banked before the write would let a texture that TryWriteFile then refused
        /// be counted as "extracted, under a new name" in the same run that counts it as a
        /// write-failure: two contradictory claims about one asset, in one report.</summary>
        static void CommitNotes(ExtractReport report, List<string> notes)
        {
            foreach (string note in notes)
                Bump(report.notesByType, note);
        }

        static void Bump(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }

        /// <summary>The single choke point for every byte this tool writes - no other code here
        /// touches the filesystem - so containment enforced here cannot be bypassed by a later
        /// code path. A rejected entry is skipped and counted rather than fatal: one hostile or
        /// malformed path in a large mod must not cost the operator the other 99% of it.</summary>
        static bool TryWriteFile(string path, byte[] bytes, string outputRoot, string sourceName,
            ExtractReport report)
        {
            if (!IsInsideRoot(path, outputRoot))
            {
                Skipped(report, "path-escape");
                Debug.LogWarning($"[MobileModExtractor] refusing to write {sourceName}: '{path}' " +
                    $"resolves outside the extraction root '{outputRoot}'. A .dfmod is untrusted " +
                    "input, so this entry is skipped; the rest of the mod still converts.");
                return false;
            }

            // Containment is not the only way a legal-looking path fails to be writable. A mod
            // listing both "a" (a TextAsset) and "a/b.png" is entirely contained and entirely
            // legal, but one of the two must lose: CreateDirectory raises IOException when "a" is
            // already a file, WriteAllBytes raises UnauthorizedAccessException when "a" is already
            // a directory. An over-long path component fails the same way, after passing
            // GetFullPath. None of that may cost the operator the rest of a large mod.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Skipped(report, "write-failed");
                Debug.LogWarning($"[MobileModExtractor] could not write {sourceName} to '{path}': " +
                    $"{ex.GetType().Name}: {ex.Message}. Skipping it; the rest of the mod still converts.");
                return false;
            }
            return true;
        }

        /// <summary>True when candidate resolves to a location strictly inside root.
        ///
        /// The manifest and the bundle's own asset names are attacker-controlled, so a Files entry
        /// of "../../../.ssh/authorized_keys" - or an absolute path, which Path.Combine would let
        /// win outright and discard the root - would otherwise be an arbitrary file write.
        ///
        /// Both sides are resolved with GetFullPath first, so this is normalisation rather than
        /// naive string matching: "sub/../ok.png" stays inside and is accepted, while ".." that
        /// genuinely climbs out is not. The root is compared with a trailing separator so a
        /// sibling that merely shares a name prefix ("&lt;root&gt;-evil") cannot pass. The
        /// comparison is ordinal but case-insensitive, because APFS and HFS+ are case-insensitive
        /// by default and a case-flipped prefix would otherwise slip past on macOS.</summary>
        public static bool IsInsideRoot(string candidate, string root)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root))
                return false;

            string fullCandidate, fullRoot;
            try
            {
                fullCandidate = Path.GetFullPath(candidate);
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return false;   // malformed, too long, or illegal characters: never write there
            }

            if (fullRoot[fullRoot.Length - 1] != Path.DirectorySeparatorChar)
                fullRoot += Path.DirectorySeparatorChar;

            // Strictly inside: the root directory itself is not a valid destination for a file.
            return fullCandidate.Length > fullRoot.Length
                && fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>PNG encoding for bundle textures. Compressed/non-readable textures cannot
    /// EncodeToPNG directly; a GPU blit to an ARGB32 RenderTexture decodes any GPU-readable
    /// format (DXT included), then ReadPixels brings it back to the CPU.</summary>
    static class TexturePng
    {
        /// <param name="linear">The texture holds numbers, not colours (normal, height,
        /// metallic/gloss). The project renders in linear space, so an sRGB round trip would
        /// silently regrade every byte of a data map.</param>
        /// <param name="perPixel">Optional fixup applied to the decoded RGBA32 pixels before
        /// they are encoded - how a swizzled normal map gets its blue channel back.</param>
        public static byte[] Encode(Texture2D src, bool linear, Func<Color32, Color32> perPixel = null)
        {
            if (src.isReadable)
            {
                try
                {
                    if (perPixel == null)
                        return src.EncodeToPNG();
                    return EncodePixels(Transform(src.GetPixels32(), perPixel),
                                        src.width, src.height, linear);
                }
                catch (Exception) { /* fall through to GPU path */ }
            }
            // Graphics.Blit against the null device is a silent no-op: ReadPixels then returns
            // a uniform grey and the extraction looks entirely successful - right name, right
            // path, right size, no pixels. Corrupting a mod quietly is worse than not
            // converting it, so refuse instead of guessing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Cannot decode texture '" + src.name + "' (format " + src.format +
                    ", not readable): decoding a compressed or non-readable bundle texture needs a " +
                    "real graphics device, and this Unity process has none. Re-run the extraction " +
                    "WITHOUT the -nographics flag ('-batchmode -quit' on its own is fine).");

            var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, linear);
                tmp.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                if (perPixel != null)
                    tmp.SetPixels32(Transform(tmp.GetPixels32(), perPixel));
                tmp.Apply();
                byte[] png = tmp.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tmp);
                return png;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static Color32[] Transform(Color32[] pixels, Func<Color32, Color32> perPixel)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = perPixel(pixels[i]);
            return pixels;
        }

        static byte[] EncodePixels(Color32[] pixels, int width, int height, bool linear)
        {
            var tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            try
            {
                tmp.SetPixels32(pixels);
                tmp.Apply();
                return tmp.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(tmp); }
        }
    }

    /// <summary>The tunable half of the converted-mod import policy: the three settings that
    /// actually decide whether a multi-gigabyte pack fits in an iPad's memory, plus the rules
    /// that are content-dependent rather than universal.
    ///
    /// Every lever reads an environment variable, in the shape MobileModBuilder already uses for
    /// DFU_MOD_OUT / DFU_MOD_TARGETS, because these will be tuned AGAINST A REAL DEVICE and a
    /// loop that needs a recompile per attempt is the wrong loop. Values are read at import time,
    /// so a converted mod picks up a change on its next conversion (extraction rewrites the files
    /// and re-imports them); to re-apply a change to an already-converted mod, reconvert it or
    /// delete its folder under the extraction root, since Unity's import cache has no idea an
    /// environment variable moved.
    ///
    /// Nothing here is guessed silently. Where the right value depends on content this exposes
    /// the rule instead of inventing one, and the defaults are argued for in the members below.
    /// </summary>
    internal static class MobileConvertedModPolicy
    {
        public const string Root = "Assets/Game/Mods/Converted/";

        /// <summary>Unity's platform name for iOS texture overrides (BuildTargetGroup.iOS).</summary>
        public const string IosPlatform = "iPhone";

        public const string MaxSizeVar = "DFU_MOD_MAXTEXSIZE";
        public const string MipsVar = "DFU_MOD_MIPS";
        public const string StreamMipsVar = "DFU_MOD_STREAM_MIPS";
        public const string NoMipVar = "DFU_MOD_NOMIP";
        public const string AstcVar = "DFU_MOD_ASTC";
        public const string QualityVar = "DFU_MOD_TEX_QUALITY";

        /// <summary>1024, not Unity's 2048. This is the single biggest lever there is - it is
        /// quadratic - and 2048 means "never downscale anything", which is not a memory policy.
        /// A 2048 texture is ~1.87MB as ASTC 6x6 and ~0.47MB at 1024; against 1.72GB of DREAM
        /// textures plus ~3.7GB of sprite modules on a device that jetsams an app somewhere
        /// under 4GB, a 4x cut on the largest thing in the build is the difference the task
        /// exists to make. It is also not a wild quality claim: an iPad's panel is ~2360 across,
        /// so a wall filling a quarter of it is being sampled at roughly 600 pixels. Raise it
        /// with DFU_MOD_MAXTEXSIZE=2048 when a specific pack proves it needs it.</summary>
        public const int DefaultMaxTextureSize = 1024;

        /// <summary>ASTC 6x6 = 3.56 bits/pixel, and is Unity's own iOS default for Compressed;
        /// naming it explicitly is what makes it tunable rather than a platform accident. 8x8
        /// (2.0 bpp) is the next lever if 1024 is not enough; 4x4 (8.0 bpp) is where to go if
        /// normal maps band, since they share this setting.</summary>
        public const string DefaultAstcBlock = "6x6";

        /// <summary>Unity's "Normal" compressor quality. Quality here costs import time, not
        /// bytes - the block size fixes the size - so it is safe to raise for a final pack.</summary>
        public const int DefaultCompressionQuality = 50;

        /// <summary>Substrings of an asset path that mean "this is 2D art drawn at 1:1, mipmaps
        /// are 33% resident for nothing". Derived from DFU's own conventions rather than
        /// invented: TextureReplacement keeps IMG images under Textures/Img and CIF/RCI images
        /// (paperdolls, portraits, weapon animations, UI) under Textures/CifRci, and - which is
        /// what makes this work for a bundled mod, whose internal directory layout is the
        /// author's own - a mod can only serve those images at all under a short name carrying
        /// the original .IMG/.CIF/.RCI filename, because that name is the runtime lookup key
        /// (TryImportImage / TryImportCifRci -> ModManager.TryGetAsset).
        ///
        /// World textures and billboards are NOT in this list on purpose: those are minified
        /// constantly and the sampling cost of having no mipmaps outweighs their third.
        ///
        /// This is the one rule whose correctness depends on content the converter has not been
        /// pointed at yet, so it is overridable wholesale with DFU_MOD_NOMIP.</summary>
        public static readonly string[] DefaultNoMipMarkers =
            { ".img", ".cif", ".rci", "/textures/img/", "/textures/cifrci/" };

        public static int MaxTextureSize()
        {
            return ParseSize(Env(MaxSizeVar), DefaultMaxTextureSize);
        }

        public static bool MipmapsAllowed()
        {
            return ParseBool(Env(MipsVar), true);
        }

        /// <summary>Off by default, and not because it looked risky. Mipmap streaming is a
        /// QUALITY SETTING first: every level in this project's ProjectSettings/QualitySettings
        /// .asset carries streamingMipmapsActive: 0, so the importer flag would be inert today -
        /// it would cost a re-import and buy nothing. Turning the quality setting on is outside
        /// this converter, and beyond that the streaming system picks mip levels from renderer
        /// bounds, which is a question about how DFU assigns these textures to materials at
        /// runtime that this task has not answered. Unverified, therefore off, therefore
        /// exposed: DFU_MOD_STREAM_MIPS=1 once someone has checked both halves on a device.
        /// </summary>
        public static bool StreamingMipmaps()
        {
            return ParseBool(Env(StreamMipsVar), false);
        }

        public static int CompressionQuality()
        {
            return ParseQuality(Env(QualityVar), DefaultCompressionQuality);
        }

        public static TextureImporterFormat IosFormat()
        {
            return ParseAstcBlock(Env(AstcVar), DefaultAstcBlock);
        }

        public static string[] NoMipMarkers()
        {
            return ParseList(Env(NoMipVar), DefaultNoMipMarkers);
        }

        /// <summary>Vorbis quality for converted audio. 0.7 rather than Unity's 1.0: the top of
        /// the scale spends bandwidth on detail a phone speaker or a pair of earbuds cannot
        /// resolve, and this is applied to every clip in a 74MB sound pack and a 273MB music
        /// one at once.</summary>
        public const float VorbisQuality = 0.7f;

        /// <summary>Above this, a clip is treated as a song and streamed; below it, as a sound
        /// effect and kept compressed in memory. The split matters in both directions - a
        /// resident song is megabytes the device does not get back, and a streamed sound effect
        /// misses the frame it was triggered on - so neither setting is safe as a blanket rule.
        ///
        /// It is measured on the EXTRACTED file, which this converter always writes as
        /// uncompressed 16-bit PCM, so it is really a duration threshold wearing a size: 2MB is
        /// about 12 seconds of mono 22kHz or 6 seconds of stereo 44.1kHz. That is comfortably
        /// above any sound effect and far below any song, which is exactly where a threshold
        /// that has to separate the two wants to sit.</summary>
        public const long StreamingThresholdBytes = 2 * 1024 * 1024;

        /// <summary>The song/effect split, as a decision rather than an expression buried in the
        /// postprocessor - the streaming half is unreachable from a small test fixture, and an
        /// untested branch on the memory-critical path is the thing this suite exists to
        /// prevent.</summary>
        public static AudioClipLoadType LoadTypeForSize(long fileBytes)
        {
            return fileBytes > StreamingThresholdBytes
                ? AudioClipLoadType.Streaming            // songs
                : AudioClipLoadType.CompressedInMemory;  // sound effects
        }

        static string Env(string name) { return Environment.GetEnvironmentVariable(name); }

        /// <summary>True when this asset is minified in use and should carry mipmaps.</summary>
        public static bool ShouldMipmap(string assetPath, string[] noMipMarkers)
        {
            if (string.IsNullOrEmpty(assetPath) || noMipMarkers == null)
                return true;
            string path = assetPath.Replace('\\', '/');
            foreach (string marker in noMipMarkers)
            {
                if (string.IsNullOrEmpty(marker))
                    continue;
                if (path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            return true;
        }

        /// <summary>A texture size Unity will accept: powers of two from 32 to 16384. An
        /// operator typo must not quietly become a policy, so anything else keeps the default
        /// and says so.</summary>
        public static int ParseSize(string raw, int fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            int value;
            if (int.TryParse(raw.Trim(), out value) && value >= 32 && value <= 16384
                && (value & (value - 1)) == 0)
                return value;
            Debug.LogWarning($"[MobileConvertedModPolicy] {MaxSizeVar}='{raw}' is not a power of " +
                $"two between 32 and 16384; keeping {fallback}");
            return fallback;
        }

        public static bool ParseBool(string raw, bool fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "yes": case "on": return true;
                case "0": case "false": case "no": case "off": return false;
                default:
                    Debug.LogWarning($"[MobileConvertedModPolicy] '{raw}' is not a yes/no value; " +
                        $"keeping {fallback}");
                    return fallback;
            }
        }

        public static int ParseQuality(string raw, int fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            int value;
            if (int.TryParse(raw.Trim(), out value) && value >= 0 && value <= 100)
                return value;
            Debug.LogWarning($"[MobileConvertedModPolicy] {QualityVar}='{raw}' is not 0-100; " +
                $"keeping {fallback}");
            return fallback;
        }

        /// <summary>Maps an ASTC block spelling ("6x6") to the importer format. Only the block
        /// sizes Unity actually defines are accepted; a typo falls back rather than silently
        /// leaving the platform to choose, which is the state this lever exists to end.</summary>
        public static TextureImporterFormat ParseAstcBlock(string raw, string fallback)
        {
            string block = string.IsNullOrEmpty(raw) ? fallback : raw.Trim().ToLowerInvariant();
            switch (block)
            {
                case "4x4": return TextureImporterFormat.ASTC_4x4;
                case "5x5": return TextureImporterFormat.ASTC_5x5;
                case "6x6": return TextureImporterFormat.ASTC_6x6;
                case "8x8": return TextureImporterFormat.ASTC_8x8;
                case "10x10": return TextureImporterFormat.ASTC_10x10;
                case "12x12": return TextureImporterFormat.ASTC_12x12;
            }
            Debug.LogWarning($"[MobileConvertedModPolicy] {AstcVar}='{raw}' is not one of " +
                $"4x4/5x5/6x6/8x8/10x10/12x12; keeping {fallback}");
            return ParseAstcBlock(fallback, fallback == DefaultAstcBlock ? "6x6" : DefaultAstcBlock);
        }

        public static string[] ParseList(string raw, string[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            var parts = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    parts.Add(trimmed);
            }
            return parts.Count > 0 ? parts.ToArray() : fallback;
        }
    }

    /// <summary>Import policy for CONVERTED mods, and the reason a 1.7GB texture pack can be
    /// opened on an 8GB iPad at all. These are the opposite trade-offs from the pilot content's:
    /// there the assets are small and wanted on the CPU, here every default that keeps a second
    /// copy of a texture around is a copy the device cannot afford.
    ///
    /// isReadable false drops the CPU-side mirror of every texture, which is pure waste for
    /// content only the GPU ever samples and would otherwise double the cost. npotScale None
    /// keeps exact dimensions, since DFU's XML/uv metadata is written against the authored size
    /// and rescaling silently misaligns it. Normal maps are typed from the DFU *_Normal suffix so
    /// Unity compresses them as normal maps and shaders unpack them correctly; the other two
    /// linear maps are marked non-sRGB, matching DFU's own IsLinearTextureMap - note that this
    /// deliberately NORMALISES a map whose author left the sRGB default on, which is a change of
    /// behaviour relative to their desktop bundle and the reason the extraction side refuses to
    /// make that decision from the filename. Audio: songs stream, effects sit compressed in
    /// memory - a streamed sound effect would stutter, an in-memory song is megabytes resident.
    ///
    /// The three levers that actually move the memory number - size cap, mipmaps, ASTC block -
    /// live in MobileConvertedModPolicy, where they can be changed without a recompile.
    ///
    /// Scoped to the extraction root, so nothing else in the project is touched.</summary>
    class MobileConvertedModImporter : AssetPostprocessor
    {
        // These settings are applied at import time and nowhere else, so a policy change only
        // reaches assets already on disk if this number moves. Note that it invalidates the
        // import cache for every texture and audio clip in the PROJECT, not only the converted
        // ones - the scope check below runs per import, long after the version is compared - so
        // bumping it costs a full re-import. Change it when the policy changes, not otherwise.
        // An environment lever moving does NOT move this: reconvert the mod instead.
        public override uint GetVersion() { return 2; }

        static bool InScope(string assetPath)
        {
            return assetPath.Replace('\\', '/')
                .StartsWith(MobileConvertedModPolicy.Root, StringComparison.Ordinal);
        }

        void OnPreprocessTexture()
        {
            if (!InScope(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;   // exact sizes: DFU XML/uv metadata depends on them
            importer.isReadable = false;                          // no CPU copy - memory matters here
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = MobileConvertedModPolicy.MaxTextureSize();
            importer.mipmapEnabled = MobileConvertedModPolicy.MipmapsAllowed()
                && MobileConvertedModPolicy.ShouldMipmap(assetPath,
                    MobileConvertedModPolicy.NoMipMarkers());
            importer.streamingMipmaps = MobileConvertedModPolicy.StreamingMipmaps();

            // Same naming rule the extraction itself used, so what was written linear is read
            // back linear. NormalMap implies linear, hence the else.
            if (MobileModExtractor.IsNormalMapName(assetPath))
                importer.textureType = TextureImporterType.NormalMap;
            else if (MobileModExtractor.IsLinearMapName(assetPath))
                importer.sRGBTexture = false;                     // data, not colour

            // The default platform settings above are what the editor and a desktop build use;
            // the device is the point, and on iOS "Compressed" without an explicit format lets
            // the platform pick the block size - the single number that decides how many bytes
            // per pixel a 3.7GB pack costs. Name it.
            var ios = importer.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform);
            ios.overridden = true;
            ios.maxTextureSize = MobileConvertedModPolicy.MaxTextureSize();
            ios.textureCompression = TextureImporterCompression.Compressed;
            ios.format = MobileConvertedModPolicy.IosFormat();
            ios.compressionQuality = MobileConvertedModPolicy.CompressionQuality();
            importer.SetPlatformTextureSettings(ios);
        }

        void OnPreprocessAudio()
        {
            if (!InScope(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = MobileConvertedModPolicy.VorbisQuality;
            settings.loadType = MobileConvertedModPolicy.LoadTypeForSize(
                new FileInfo(assetPath).Length);
            importer.defaultSampleSettings = settings;
        }
    }
}
