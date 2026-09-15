// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: which fetched mods' textures must stay raw. Colour-key maps (read back with GetPixel and
// compared exactly), point-filtered terrain tiles (DFU decompresses them into an ARGB32
// Texture2DArray anyway) and Unity terrain DETAIL PROTOTYPE textures gain nothing from ASTC and
// break under it. The last class is the least obvious: Unity does not sample
// DetailPrototype.prototypeTexture directly for a non-instanced prototype - DetailDatabase packs
// every non-instanced prototype into one "Terrain Detail Atlas", and a TerrainData built at
// RUNTIME (DaggerfallTerrain.PromoteTerrainData does exactly that) has no serialized atlas, so the
// engine builds it from the prototypes' CPU pixels. Unreadable inputs give an empty atlas, no
// managed exception and only native "Texture of width N and height N is not accessible." errors
// that never reach Player.log. In the Editor the CPU read succeeds through the importer, so this
// is invisible until the player build.
//
// A second exception class: LinearData, for maps only a compute shader ever reads. Those need
// sRGB sampling off as well as no compression, but not a CPU copy (isReadable stays false).
//
// Read by MobileModPackTextureImporter in MobileModBuilder.cs; pinned as a pure rule by
// MobileSelfTest.TestPackTextureRules, because the importer itself only runs inside an import.
//
// Place in Assets/Editor/

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileModPackTextureRules
    {
        public enum Rule { Default, RawData, LinearData }

        // MOBILE: Distant Terrain (WoD flavour). Named as a constant because three things key off it:
        // the raw-data list below, and the NoMips/SingleChannel path tests, which must match this mod's
        // deriv map and NOT the identically named file in WorldOfDaggerfallTerrain.
        public const string DistantTerrainMod = "DistantTerrainWoD";

        // MOBILE: Real Grass. Named as a constant for the same reason as DistantTerrainMod - the
        // raw-data list and the self-test's import assertions both key off it, and the folder name
        // is the only thing tying either to the fetched pack.
        public const string RealGrassMod = "RealGrass";

        // Mod folder names under Assets/Game/Mods/ (the mods.json entry name).
        static readonly string[] rawDataMods = { "WorldOfDaggerfallBiomes", DistantTerrainMod, RealGrassMod };

        // MOBILE: mods whose textures are numeric data sampled only by a compute shader.
        static readonly string[] linearDataMods = { "WorldOfDaggerfallTerrain" };

        /// <summary>The raw-data mod names, for verification.</summary>
        // MOBILE: AsReadOnly, not the array itself (same reason as MobileShaders.Names) - an
        // IReadOnlyList<string> that IS the live array can be cast back to string[] and mutated.
        // Exposed so MobileSelfTest.TestPackTextureRules can check every name against
        // tools/bundled-mods/mods.json: nothing else ties this literal to the fetched folder name,
        // and a rename there reverts 225 textures to ASTC with nothing in any log to say so.
        public static System.Collections.Generic.IReadOnlyList<string> RawDataMods => System.Array.AsReadOnly(rawDataMods);

        /// <summary>The linear-data mod names, for verification.</summary>
        // MOBILE: same AsReadOnly reasoning and the same reason for existing as RawDataMods - the
        // folder name is the only thing tying this literal to the fetched pack, and a rename would
        // silently put the Terrain world maps back through sRGB and block compression, which shows
        // up as wrong ground heights and nothing in any log.
        public static System.Collections.Generic.IReadOnlyList<string> LinearDataMods => System.Array.AsReadOnly(linearDataMods);

        /// <summary>
        /// MOBILE: linear-data maps get no mip chain. Every read of these textures in the shipped
        /// compute shaders is a level-0 fetch - SampleLevel(..., 0) in TerrainComputer.compute and
        /// basicRoads.cginc, "float sampleLevel = 0" in heightSampling.cginc - so the chain is memory
        /// that nothing can ever sample: about 11 MB of GPU residency across the five world maps.
        /// They are also numeric data, so a mip level would be an average of unrelated numbers rather
        /// than a lower-resolution picture, which is what makes trimming this safe as well as free.
        /// A constant rather than a literal in the importer so MobileSelfTest can pin it without
        /// running an import.
        /// </summary>
        public const bool NoMipsForLinearData = true;

        // MOBILE: the two lists must not overlap. For() walks rawDataMods first, so a name in both
        // would silently be treated as raw data - readable, mipped, sRGB left alone - which is the
        // opposite of what a compute-only data map needs, and no log anywhere would say which rule
        // won. Computed once; exposed as a pure hook so the self-test can assert it, and thrown from
        // For() so a misconfiguration stops the import rather than quietly changing 225 textures.
        static readonly bool listsDisjointValue = ComputeListsDisjoint();

        /// <summary>True when no mod name appears in both rule lists (a configuration error).</summary>
        public static bool ListsDisjoint => listsDisjointValue;

        static bool ComputeListsDisjoint()
        {
            foreach (string raw in rawDataMods)
                foreach (string lin in linearDataMods)
                    if (string.Equals(raw, lin, System.StringComparison.OrdinalIgnoreCase))
                        return false;
            return true;
        }

        public static Rule For(string assetPath)
        {
            // MOBILE: a name in both lists is a configuration error, not a question of precedence.
            if (!ListsDisjoint)
                throw new System.InvalidOperationException(
                    "MobileModPackTextureRules: a mod name is in both rawDataMods and linearDataMods; " +
                    "the two rules contradict each other (readable + mips vs sRGB-off, GPU-only).");

            string p = (assetPath ?? "").Replace('\\', '/');
            foreach (string mod in rawDataMods)
                if (p.StartsWith("Assets/Game/Mods/" + mod + "/", System.StringComparison.Ordinal))
                    return Rule.RawData;
            foreach (string mod in linearDataMods)
                if (p.StartsWith("Assets/Game/Mods/" + mod + "/", System.StringComparison.Ordinal))
                    return Rule.LinearData;
            return Rule.Default;
        }

        /// <summary>
        /// Colour-key maps are sampled by pixel; a mip chain would only waste memory.
        /// MOBILE: this is per-FILE, not per-mod, precisely so the raw-data class can hold textures
        /// that ARE drawn. Real Grass's two 256x256 billboards are drawn at every distance out to
        /// detailObjectDistance, so they keep their mip chain (about 85 KB each on top of the
        /// 256 KB level 0); only the CPU-read-once maps below drop theirs.
        /// </summary>
        public static bool NoMips(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            if (p.EndsWith("/climate_map.png", System.StringComparison.OrdinalIgnoreCase))
                return true;
            // MOBILE: Distant Terrain's river/coast mask is read once on the CPU (GetPixels32) and
            // never sampled by a shader, so its mip chain is memory nothing can reach.
            return IsDistantDerivMap(p);
        }

        /// <summary>
        /// MOBILE: the iOS import ceiling for a pack texture's longest side. 2048 everywhere except
        /// Real Grass, whose textures are Unity terrain DETAIL PROTOTYPES: every non-instanced
        /// prototype is packed into one "Terrain Detail Atlas" and the whole detail pass is drawn
        /// from that atlas, so these sizes decide a resident GPU texture as well as their own.
        ///
        /// Measured on 6000.3.23f1 with this port's three-layer Full configuration (grass +
        /// flowers + tufts), one atlas per distinct prototype-texture set:
        ///
        ///   sources 256 / 128x256 / 256   -> atlas  512x512  =  2.7 MB
        ///   sources 512 / 256x512 / 256   -> atlas 1024x512  =  5.5 MB
        ///   sources 512 / 256x512 / 512   -> atlas 1024x1024 = 10.9 MB
        ///   upstream's own sizes          -> atlas 2048x1024 = 21.8 MB
        ///
        /// 256 is shipped: it is the size upstream's own Classic billboards are, it keeps the atlas
        /// at 2.7 MB, and it also quarters the readable CPU copy every raw-data texture keeps (the
        /// atlas is built from those pixels - see the class header). Upstream's Grass_tex is
        /// 1024x1024 and GrassDetails_01 is 512x1024; both are alpha-cut vegetation drawn at 0.65-2.1
        /// m, so the texel density at the 100 m draw radius is not what limits this look.
        /// </summary>
        public const int RealGrassMaxTextureSize = 256;

        /// <summary>MOBILE: the default iOS import ceiling for a raw-data pack texture.</summary>
        public const int DefaultMaxTextureSize = 2048;

        /// <summary>
        /// MOBILE: the iOS maxTextureSize a raw-data texture imports at. See
        /// <see cref="RealGrassMaxTextureSize"/> for why Real Grass is the exception.
        /// </summary>
        public static int MaxTextureSize(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            if (p.StartsWith("Assets/Game/Mods/" + RealGrassMod + "/", System.StringComparison.Ordinal))
                return RealGrassMaxTextureSize;
            return DefaultMaxTextureSize;
        }

        /// <summary>
        /// MOBILE: whether a DEFAULT-rule pack texture keeps a mip chain.
        ///
        /// Unity silently falls back to RGBA32 when it is asked to compress a NON-POWER-OF-TWO
        /// texture that HAS MIPMAPS - no error, no warning, just an uncompressed result (the same
        /// trap the converter documents at MobileModExtractor.cs "NON-POWER-OF-TWO IS WHY THE
        /// COMPRESSION SILENTLY DID NOT HAPPEN"). Daggerfall Expanded Textures is 7,341 sprites and
        /// NOT ONE of them is power-of-two, so every one of them imported RGBA32: 636 MB resident if
        /// every archive were live, against 71 MB as ASTC_6x6.
        ///
        /// The cure has to keep the art 1:1 - npotScale ToNearest (the converter's cure, which works
        /// because DREAM only replaces CLASSIC archives) would RESIZE DEX's enemies: billboard world
        /// size comes from the classic record (MaterialReader.GetSize) and falls back to the
        /// replacement's own pixels when there is no classic file, which is DEX's case for 66 of its
        /// 73 archives. So the mip chain is what goes: dropping it is the only setting that is both
        /// compressed and 1:1. Distant enemies alias slightly more (they are 75-300 px sprites DFU
        /// draws point-filtered anyway) and the 33% mip tax is not paid either, so the saving is
        /// nearer 12x than 9x.
        ///
        /// POT textures are unaffected - they compress fine WITH mips, and mips are what keeps a
        /// 1024x1024 wall texture from shimmering at distance. 2D art (UI, inventory, paperdoll)
        /// never had mips.
        /// </summary>
        public static bool MipsFor(bool isPowerOfTwo, bool twoD)
        {
            return !twoD && isPowerOfTwo;
        }

        /// <summary>MOBILE: true for a positive power of two. Both sides must pass for MipsFor.</summary>
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>
        /// MOBILE: textures that carry data in ONE channel and should import as R8 rather than RGBA32.
        /// Only Distant Terrain's daggerfall_deriv_map.png qualifies: it is an 8-bit greyscale PNG and
        /// DistantTerrain.ApplyDerivativeHeightmap reads `pixels[i].r` alone, comparing it against a
        /// 0-255 threshold ("Image is grayscale; R == G == B, so sampling R is sufficient"). RGBA32
        /// would store three copies of the same byte plus a constant alpha, and this texture must keep
        /// a readable CPU copy as well as its GPU one, so the waste is paid twice: at the imported
        /// 2048-clamped size that is 8 MB + 8 MB as RGBA32 against 2 MB + 2 MB as R8.
        /// </summary>
        public static bool SingleChannel(string assetPath)
        {
            return IsDistantDerivMap((assetPath ?? "").Replace('\\', '/'));
        }

        // MOBILE: keyed on mod folder AND file name, never the file name alone. World of Daggerfall -
        // Terrain ships a DIFFERENT daggerfall_deriv_map.png (RGBA, a compute-shader input, LinearData)
        // at Assets/Game/Mods/WorldOfDaggerfallTerrain/Assets/Maps/. A bare-name match would drop three
        // of its channels and its mip chain with nothing in any log to say why the ground went wrong.
        static bool IsDistantDerivMap(string normalizedPath)
        {
            return normalizedPath.StartsWith("Assets/Game/Mods/" + DistantTerrainMod + "/", System.StringComparison.Ordinal)
                && normalizedPath.EndsWith("/daggerfall_deriv_map.png", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
