// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: which fetched mods' textures must stay raw. Colour-key maps (read back with GetPixel and
// compared exactly) and point-filtered terrain tiles (DFU decompresses them into an ARGB32
// Texture2DArray anyway) gain nothing from ASTC and break under it.
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

        // Mod folder names under Assets/Game/Mods/ (the mods.json entry name).
        static readonly string[] rawDataMods = { "WorldOfDaggerfallBiomes" };

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

        /// <summary>Colour-key maps are sampled by pixel; a mip chain would only waste memory.</summary>
        public static bool NoMips(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            return p.EndsWith("/climate_map.png", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
