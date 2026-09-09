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

        public static Rule For(string assetPath)
        {
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
