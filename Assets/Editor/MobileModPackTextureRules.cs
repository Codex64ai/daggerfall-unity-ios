// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: which fetched mods' textures must stay raw. Colour-key maps (read back with GetPixel and
// compared exactly) and point-filtered terrain tiles (DFU decompresses them into an ARGB32
// Texture2DArray anyway) gain nothing from ASTC and break under it.
//
// Read by MobileModPackTextureImporter in MobileModBuilder.cs; pinned as a pure rule by
// MobileSelfTest.TestPackTextureRules, because the importer itself only runs inside an import.
//
// Place in Assets/Editor/

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileModPackTextureRules
    {
        public enum Rule { Default, RawData }

        // Mod folder names under Assets/Game/Mods/ (the mods.json entry name).
        static readonly string[] rawDataMods = { "WorldOfDaggerfallBiomes" };

        /// <summary>The raw-data mod names, for verification.</summary>
        // MOBILE: AsReadOnly, not the array itself (same reason as MobileShaders.Names) - an
        // IReadOnlyList<string> that IS the live array can be cast back to string[] and mutated.
        // Exposed so MobileSelfTest.TestPackTextureRules can check every name against
        // tools/bundled-mods/mods.json: nothing else ties this literal to the fetched folder name,
        // and a rename there reverts 225 textures to ASTC with nothing in any log to say so.
        public static System.Collections.Generic.IReadOnlyList<string> RawDataMods => System.Array.AsReadOnly(rawDataMods);

        public static Rule For(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            foreach (string mod in rawDataMods)
                if (p.StartsWith("Assets/Game/Mods/" + mod + "/", System.StringComparison.Ordinal))
                    return Rule.RawData;
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
