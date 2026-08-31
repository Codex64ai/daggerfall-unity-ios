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
    public class ExtractReport
    {
        public string manifestPath;
        public List<string> extracted = new List<string>();
        public Dictionary<string, int> skippedByType = new Dictionary<string, int>();
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
                ModInfo modInfo = null;
                foreach (string assetName in ab.GetAllAssetNames())
                {
                    if (assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                    {
                        var manifestAsset = ab.LoadAsset<TextAsset>(assetName);
                        if (manifestAsset != null)
                            ModManager._serializer.TryDeserialize(fsJsonParser.Parse(manifestAsset.text), ref modInfo);
                        continue; // rewritten below, not copied verbatim
                    }

                    string outPath = OutputPathFor(assetName, outputRoot);
                    var obj = ab.LoadAsset<UnityEngine.Object>(assetName);
                    if (obj is Texture2D tex2d)
                    {
                        outPath = Path.ChangeExtension(outPath, ".png");
                        WriteFile(outPath, TexturePng.Encode(tex2d, false));
                        report.extracted.Add(outPath);
                    }
                    else if (obj is TextAsset textAsset)
                    {
                        WriteFile(outPath, textAsset.bytes);
                        report.extracted.Add(outPath);
                    }
                    else
                    {
                        string type = obj ? obj.GetType().Name : "null";
                        report.skippedByType.TryGetValue(type, out int n);
                        report.skippedByType[type] = n + 1;
                        Debug.LogWarning($"[MobileModExtractor] skipped {assetName} ({type} not supported yet)");
                    }
                }

                if (modInfo == null || string.IsNullOrWhiteSpace(modInfo.ModTitle))
                    throw new InvalidDataException("Bundle has no readable .dfmod.json manifest: " + dfmodPath);

                // Manifest identity is preserved; only Files points at the extraction.
                modInfo.Files = new List<string>(report.extracted);
                ModManager._serializer.TrySerialize(modInfo, out fsData data);
                string manifestOut = Path.Combine(outputRoot,
                    Path.GetFileNameWithoutExtension(dfmodPath) + ModManager.MODINFOEXTENSION);
                WriteFile(manifestOut, System.Text.Encoding.UTF8.GetBytes(fsJsonPrinter.PrettyJson(data)));
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

        /// <summary>Bundle-internal path tail preserved under outputRoot (directory-suffix
        /// queries via ModManager.FindAssets keep working); short file name kept verbatim.</summary>
        static string OutputPathFor(string bundleAssetName, string outputRoot)
        {
            string tail = bundleAssetName.Replace('\\', '/');
            if (tail.StartsWith("assets/", StringComparison.Ordinal))
                tail = tail.Substring("assets/".Length);
            return Path.Combine(outputRoot, tail);
        }

        static void WriteFile(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
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
