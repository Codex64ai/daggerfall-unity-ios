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
    /// show as "N assets skipped". notesByType counts assets that WERE extracted but needed
    /// something said about them: a texture re-encoded to .png under a new runtime lookup name,
    /// or a path whose capitalisation the source manifest did not record.</summary>
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

                    string outPath = OutputPathFor(assetName, outputRoot, originalCase, report);
                    var obj = ab.LoadAsset<UnityEngine.Object>(assetName);
                    if (obj is Texture2D tex2d)
                    {
                        // Textures are always re-encoded as .png, which moves the runtime lookup
                        // key with them (it is the short name WITH extension). Report it.
                        if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            Noted(report, "extension-rewritten");
                            Debug.LogWarning($"[MobileModExtractor] {assetName} is re-encoded as .png, " +
                                "so its runtime lookup name changes with it");
                            outPath = Path.ChangeExtension(outPath, ".png");
                        }
                        if (!Claim(claimed, outPath, assetName, report))
                            continue;
                        if (!TryWriteFile(outPath, TexturePng.Encode(tex2d, false), outputRoot, assetName, report))
                            continue;
                        report.extracted.Add(outPath);
                    }
                    else if (obj is TextAsset textAsset)
                    {
                        if (!Claim(claimed, outPath, assetName, report))
                            continue;
                        if (!TryWriteFile(outPath, textAsset.bytes, outputRoot, assetName, report))
                            continue;
                        report.extracted.Add(outPath);
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
            Dictionary<string, string> originalCase, ExtractReport report)
        {
            string tail = bundleAssetName.Replace('\\', '/');
            string original;
            if (originalCase.TryGetValue(tail.ToLowerInvariant(), out original))
                return Path.Combine(outputRoot, original);

            Noted(report, "unlisted-in-manifest");
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

        /// <summary>The asset did not make it into the extraction.</summary>
        static void Skipped(ExtractReport report, string key) { Bump(report.skippedByType, key); }

        /// <summary>The asset was extracted, but something about it is worth reporting.</summary>
        static void Noted(ExtractReport report, string key) { Bump(report.notesByType, key); }

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
        public static byte[] Encode(Texture2D src, bool linear)
        {
            if (src.isReadable)
            {
                try { return src.EncodeToPNG(); }
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
    }
}
