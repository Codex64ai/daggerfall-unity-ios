// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Counts the extra material maps - normal, height, metallic/gloss, emission - that
//   actually reach a material, split by where they came from: a mod's asset bundle, or
//   loose files on disk.
//
//   Why this exists: two fixes made bundled maps work (CustomizeMaterial now falls through
//   to mods, and MaterialReader no longer discards a bundle-sourced normal map). Neither is
//   verifiable from a screenshot - painted-in shading and real normal mapping look alike in
//   a still, and the only way to A/B was to reinstall a different ipa. A non-zero mod count
//   is unambiguous proof the maps are being found and applied from a bundle; a zero says
//   just as clearly that something is still gating them.
//
//   The loose/mod split is the whole point. Loose files always worked, so a loose count
//   proves nothing; it is the MOD column that distinguishes the fix from the behaviour that
//   was already there.
//
//   Counting is gated on the diagnostics overlay being on, because the normal-map site has
//   to ask whether a loose file exists to know which column to credit, and that is a file
//   system probe. With the overlay off, every call here is a static bool read.
//
//   Cumulative for the session: nothing resets them, matching the rest of the overlay,
//   which reads live state and keeps no history. Note they count APPLICATIONS, not distinct
//   textures - MaterialReader purges old entries from its material cache, so a surface
//   revisited after a long detour can be rebuilt and counted again. Numbers only climb.
//

using DaggerfallWorkshop.Utility.AssetInjection;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileAssetStats
    {
        /// <summary>
        /// Mirrors MobileInputController.showGestureDebug once per frame. False in a normal
        /// release session, which is what keeps the counting free.
        /// </summary>
        public static bool Enabled;

        public static int ModNormal;
        public static int ModHeight;
        public static int ModMetallicGloss;
        public static int ModEmission;

        public static int LooseNormal;
        public static int LooseHeight;
        public static int LooseMetallicGloss;
        public static int LooseEmission;

        /// <summary>True once anything at all has been applied from a mod bundle.</summary>
        public static bool AnyFromMods
        {
            get { return ModNormal + ModHeight + ModMetallicGloss + ModEmission > 0; }
        }

        /// <summary>
        /// Record one map applied to a material. Call at the point of APPLICATION, not import:
        /// a map that is imported and then discarded is exactly the bug this is here to detect,
        /// so counting it would hide the thing being measured.
        ///
        /// Plain integer increments, no allocation and no logging - this runs while the world
        /// streams. Silently ignores map types it has no column for rather than growing one.
        /// </summary>
        public static void CountApplied(TextureMap textureMap, bool fromMod)
        {
            if (!Enabled)
                return;

            switch (textureMap)
            {
                case TextureMap.Normal:
                    if (fromMod) ModNormal++; else LooseNormal++;
                    break;
                case TextureMap.Height:
                    if (fromMod) ModHeight++; else LooseHeight++;
                    break;
                case TextureMap.MetallicGloss:
                    if (fromMod) ModMetallicGloss++; else LooseMetallicGloss++;
                    break;
                case TextureMap.Emission:
                    if (fromMod) ModEmission++; else LooseEmission++;
                    break;
            }
        }

        /// <summary>Back to zero. Only the self test uses this; no session resets them.</summary>
        public static void Reset()
        {
            ModNormal = ModHeight = ModMetallicGloss = ModEmission = 0;
            LooseNormal = LooseHeight = LooseMetallicGloss = LooseEmission = 0;
        }

        /// <summary>
        /// The overlay line. Allocates, so it is only ever called from OnGUI while the overlay
        /// is being drawn - never from the counting path.
        /// </summary>
        public static string Summary()
        {
            return string.Format(
                "modmaps  normal {0}  height {1}  metallic {2}  emission {3}\n" +
                "  (loose: normal {4}  height {5}  metallic {6}  emission {7})",
                ModNormal, ModHeight, ModMetallicGloss, ModEmission,
                LooseNormal, LooseHeight, LooseMetallicGloss, LooseEmission);
        }
    }
}
