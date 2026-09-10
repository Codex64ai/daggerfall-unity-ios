// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Headless .dfmod builder. The interactive Mod Builder window already knows the iOS
// BuildTarget but drives everything through GUI panels; this is the same packing logic
// as a plain -executeMethod entry so mod bundles can be produced from the command line:
//
//   env DFU_MOD_OUT=$HOME/dev/dfu-mods \
//   Unity -batchmode -quit -nographics -projectPath <proj> \
//     -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModBuilder.BuildFromEnv
//
// Script mods are refused: iOS runs IL2CPP with no JIT, so mod code can be neither
// compiled from source nor Assembly.Load-ed. Asset-only mods (DREAM-class) are the target.
//
// Place in Assets/Editor/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using UnityEditor;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileModBuilder
    {
        public static string[] BuildMod(string manifestPath, string outputRoot, BuildTarget[] targets, bool flatOutput = false)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Mod manifest not found", manifestPath);

            ModInfo modInfo = null;
            if (ModManager._serializer.TryDeserialize(
                    fsJsonParser.Parse(File.ReadAllText(manifestPath)), ref modInfo).Failed
                || modInfo == null || string.IsNullOrWhiteSpace(modInfo.ModTitle))
                throw new InvalidDataException("Failed to parse mod manifest: " + manifestPath);

            string script = modInfo.Files.FirstOrDefault(f =>
                f.EndsWith(".cs", StringComparison.Ordinal) ||
                f.EndsWith(".dll.bytes", StringComparison.Ordinal));
            if (script != null)
                throw new NotSupportedException(
                    modInfo.ModTitle + ": script mods are not supported by the iOS pipeline (" + script + ")");

            // Bundle = declared assets + the manifest itself (Mod.LoadModInfoFromBundle needs it).
            string manifestAssetPath = ToAssetPath(manifestPath);
            var assets = new List<string>(modInfo.Files);
            if (!assets.Contains(manifestAssetPath))
                assets.Add(manifestAssetPath);
            foreach (string asset in assets)
                if (!File.Exists(asset))
                    throw new FileNotFoundException("Mod asset not found in project", asset);

            string fileName = Path.GetFileName(manifestPath)
                .Replace(ModManager.MODINFOEXTENSION, ModManager.MODEXTENSION);
            var buildMap = new AssetBundleBuild[1];
            buildMap[0].assetBundleName = fileName;
            buildMap[0].assetNames = assets.ToArray();

            var built = new List<string>();
            foreach (BuildTarget target in targets)
            {
                // flatOutput: the bundle goes straight into outputRoot (the app's shipped Mods
                // folder - scanned recursively, but kept flat for tidiness). Otherwise the
                // per-target subfolder the DREAM workflow expects.
                string dir = flatOutput ? outputRoot : Path.Combine(outputRoot, target.ToString());
                Directory.CreateDirectory(dir);
                if (BuildPipeline.BuildAssetBundles(dir, buildMap,
                        BuildAssetBundleOptions.ChunkBasedCompression, target) == null)
                    throw new Exception("BuildAssetBundles failed for " + fileName + " (" + target + ")");
                built.Add(Path.Combine(dir, fileName));

                if (flatOutput)
                {
                    // BuildAssetBundles also drops a master bundle named after the folder and a
                    // .manifest per bundle. Neither is a mod; neither ships.
                    string master = Path.Combine(dir, Path.GetFileName(dir.TrimEnd('/', '\\')));
                    foreach (string junk in new[] { master, master + ".manifest", Path.Combine(dir, fileName + ".manifest") })
                        if (File.Exists(junk))
                            File.Delete(junk);
                }
            }
            return built.ToArray();
        }

        static string ToAssetPath(string path)
        {
            path = path.Replace('\\', '/');
            int i = path.IndexOf("Assets/", StringComparison.Ordinal);
            return i < 0 ? path : path.Substring(i);
        }

        public static void BuildFromEnv()
        {
            try
            {
                string outRoot = Environment.GetEnvironmentVariable("DFU_MOD_OUT");
                if (string.IsNullOrEmpty(outRoot))
                    outRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal), "dev/dfu-mods");

                string targetsVar = Environment.GetEnvironmentVariable("DFU_MOD_TARGETS");
                if (string.IsNullOrEmpty(targetsVar))
                    targetsVar = "iOS,StandaloneOSX";
                BuildTarget[] targets = targetsVar.Split(',')
                    .Select(t => (BuildTarget)Enum.Parse(typeof(BuildTarget), t.Trim(), true))
                    .ToArray();

                string manifestVar = Environment.GetEnvironmentVariable("DFU_MOD_MANIFEST");
                string[] manifests = !string.IsNullOrEmpty(manifestVar)
                    ? new[] { manifestVar }
                    : Directory.GetFiles("Assets/Game/Mods",
                        "*" + ModManager.MODINFOEXTENSION, SearchOption.AllDirectories);

                if (manifests.Length == 0)
                    throw new InvalidOperationException(
                        "No .dfmod.json manifests found under Assets/Game/Mods");

                foreach (string manifest in manifests)
                    foreach (string builtPath in BuildMod(manifest, outRoot, targets))
                        Debug.Log("[MobileModBuilder] built " + builtPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MobileModBuilder] " + ex);
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>
    /// Import settings for texture packs fetched from source (Vanilla Enhanced and the like) under
    /// Assets/Game/Mods/&lt;Pack&gt;/. DFU draws these 1:1 in place of arena2 art, so no NPOT scaling;
    /// ASTC 6x6 on iOS is what the DREAM conversion settled on (~9-16x smaller than the ARGB32
    /// a loose PNG becomes); 2D art (UI, inventory, paperdoll, portraits) gets no mipmaps. Not
    /// applied to IOSPilot (its own rules below) or Converted/ (the extractor already decided).
    /// MOBILE: two exceptions, both in MobileModPackTextureRules - RawData packs, whose textures are
    /// read back on the CPU or drawn as point-filtered terrain records, stay readable and
    /// uncompressed; LinearData packs, whose textures are numeric maps only a compute shader
    /// samples, additionally get sRGB off (and stay non-readable - the GPU is the only reader).
    /// </summary>
    class MobileModPackTextureImporter : AssetPostprocessor
    {
        static readonly string[] noMipFolders = { "/UI/", "/Img/", "/CifRci/", "/Inventory/", "/Paint/", "/Portraits/" };

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Game/Mods/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Game/Mods/IOSPilot/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Game/Mods/Converted/", StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            if (Environment.GetEnvironmentVariable("DFU_IMPORT_TRACE") == "1")
                Debug.Log("[MobileModPackTextureImporter] " + path);
            if (MobileModPackTextureRules.For(path) == MobileModPackTextureRules.Rule.RawData)
            {
                // MOBILE: see MobileModPackTextureRules.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.mipmapEnabled = !MobileModPackTextureRules.NoMips(path);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var raw = importer.GetPlatformTextureSettings("iPhone");
                raw.overridden = true;
                // MOBILE: one-channel data maps import as R8 (see MobileModPackTextureRules.SingleChannel):
                // Distant Terrain's deriv map is greyscale and its carve reads .r only, so RGBA32 would
                // cost 4x on both the GPU copy and the readable CPU copy this branch always keeps.
                raw.format = MobileModPackTextureRules.SingleChannel(path)
                    ? TextureImporterFormat.R8
                    : TextureImporterFormat.RGBA32;
                // MOBILE: 2048 is deliberate for the 5000x2500 deriv map too. The carve maps image
                // pixels onto the 1000x500 world grid by the texture's own width/height and takes the
                // DARKEST pixel in each cell's block, so a clamped map still resolves the coastline;
                // measured against a full-resolution carve, a 2048-clamped one loses 0.3% of water
                // cells and gains 0.2% (a 1250x625 one, 1.0%/0.4%). Full size would cost 50 MB of GPU
                // memory plus a 50 MB readable copy.
                raw.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(raw);
                return;
            }
            if (MobileModPackTextureRules.For(path) == MobileModPackTextureRules.Rule.LinearData)
            {
                // MOBILE: data maps read as numbers by a compute shader (heights, biome weights, port flags).
                // The project is Linear, so sRGB sampling would silently remap them; block compression
                // would quantise them. No mip chain either: every read of these maps in the shipped
                // compute shaders is a level-0 fetch, so the chain is ~11 MB of GPU memory nothing can
                // sample (see MobileModPackTextureRules.NoMipsForLinearData). Filter mode is left as
                // the upstream meta set it.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = false;
                importer.isReadable = false;
                importer.mipmapEnabled = !MobileModPackTextureRules.NoMipsForLinearData;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var lin = importer.GetPlatformTextureSettings("iPhone");
                lin.overridden = true;
                lin.format = TextureImporterFormat.RGBA32;
                lin.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(lin);
                return;
            }
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            bool twoD = false;
            foreach (string f in noMipFolders)
                if (path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) twoD = true;
            importer.mipmapEnabled = !twoD;
            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.format = TextureImporterFormat.ASTC_6x6;
            // DFU_PACK_TEX_FORMAT=RGBA32: uncompressed bundles for the iOS Simulator, whose Unity
            // player neither samples ASTC natively nor Graphics.CopyTexture()s it (terrain arrays
            // never build there). Diagnostics only; the shipped pack stays ASTC.
            if (Environment.GetEnvironmentVariable("DFU_PACK_TEX_FORMAT") == "RGBA32")
                ios.format = TextureImporterFormat.RGBA32;
            ios.maxTextureSize = 4096;
            importer.SetPlatformTextureSettings(ios);
        }

        // MOBILE: without this override Unity uses the default version 0 forever, so the import
        // result of every mod-pack texture is cached against a hash that does NOT change when the
        // rules above do. Every rule change since this class was written - RawData, LinearData,
        // R8 for single-channel maps, the 2048 clamp, NoMips - therefore needed someone to remember
        // to force a reimport by hand, and MobileModBuilder.ApplyAll does not force pack folders
        // (Task 4 recorded exactly that trap: "ApplyAll does not force-reimport pack folders -> run
        // unscoped ReimportPacks first"). A silently stale texture in a bundle is the worst class of
        // bug this port has: it looks right in the Editor and is wrong on the device.
        //
        // MobileModTextureImporter below has carried the same one-liner from the start; this is the
        // sibling that was missing it. Bump the number whenever OnPreprocessTexture above changes
        // behaviour. Going from the implicit 0 to 1 costs ONE reimport of the textures these
        // postprocessors apply to, on the next Editor run.
        public override uint GetVersion() { return 1; }
    }

    /// <summary>
    /// Import settings for the in-repo pilot mod's art only (Assets/Game/Mods/IOSPilot/).
    /// Classic-art replacements are odd sizes (320x200 IMGs, tiny CIF frames): Unity's
    /// default NPOT scaling would silently resize them and DFU draws them 1:1, so pin
    /// NPOT off, and Point filtering because the vanilla look is unfiltered pixels at 1:1.
    /// </summary>
    class MobileModTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Game/Mods/IOSPilot/", StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
        }

        // Bumping this makes Unity reimport affected textures when the rule above changes.
        public override uint GetVersion() { return 1; }
    }
}
