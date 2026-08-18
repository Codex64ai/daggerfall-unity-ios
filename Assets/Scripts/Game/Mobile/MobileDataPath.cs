// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Resolves the Daggerfall game data location on iOS, the way upstream expects:
//   the app ships WITHOUT game data and the player supplies their own copy.
//
//   Daggerfall was released as freeware by Bethesda in 2009 - free to download, but
//   still their copyright. Upstream Daggerfall Unity never redistributes arena2, and
//   neither should any public build of this port. Bundling it into StreamingAssets
//   would be redistributing someone else's copyrighted assets.
//
//   On iOS, Application.persistentDataPath IS the app's Documents directory. With
//   UIFileSharingEnabled and LSSupportsOpeningDocumentsInPlace set (see
//   MobileIOSPostProcess), that folder is visible in Finder and the Files app, so the
//   player can drop their own arena2 folder straight into it.
//
//   Hooks DaggerfallUnity.OnSetArena2Source, which exists precisely for this:
//   "Allow implementor to set own Arena2 path (e.g. from custom settings file)".
//

using System.IO;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileDataPath
    {
        public const string ReadmeName = "PUT-ARENA2-FOLDER-HERE.txt";

        /// <summary>Documents directory on iOS; the file-sharing drop target.</summary>
        public static string DataRoot { get { return Application.persistentDataPath; } }

        public static string Arena2Path { get { return Path.Combine(DataRoot, "arena2"); } }

        public static bool DataPresent
        {
            get
            {
                // A dataless placeholder or half-copied folder is worse than none, so
                // require a real, non-trivial file to be readable.
                if (!Directory.Exists(Arena2Path))
                    return false;

                string probe = Path.Combine(Arena2Path, "ARCH3D.BSA");
                if (!File.Exists(probe))
                    return false;

                try
                {
                    return new FileInfo(probe).Length > 1024;
                }
                catch
                {
                    return false;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
#if UNITY_IOS && !UNITY_EDITOR
            DaggerfallUnity.OnSetArena2Source += OnSetArena2Source;
            EnsureDropTarget();
#endif
        }

        static void OnSetArena2Source()
        {
            if (!DataPresent)
            {
                Debug.LogWarning(
                    "[MobileDataPath] No usable game data at " + Arena2Path + "\n" +
                    "Copy your own Daggerfall 'arena2' folder into this app's Documents folder " +
                    "using Finder (device > Files > this app) or the Files app on iOS.\n" +
                    "Daggerfall is a free download from Bethesda; this app does not include it.");
                return;
            }

            DaggerfallUnity.Settings.MyDaggerfallPath = DataRoot;

            if (DaggerfallUnity.HasInstance)
                DaggerfallUnity.Instance.Arena2Path = Arena2Path;

            Debug.Log("[MobileDataPath] Using game data at " + Arena2Path);
        }

        /// <summary>
        /// Create a visible marker file so the Documents folder is not empty. An empty
        /// share folder gives the player no clue what belongs there.
        /// </summary>
        static void EnsureDropTarget()
        {
            try
            {
                Directory.CreateDirectory(DataRoot);

                string readme = Path.Combine(DataRoot, ReadmeName);
                if (File.Exists(readme))
                    return;

                File.WriteAllText(readme,
                    "Daggerfall Unity - game data required\n" +
                    "=====================================\n\n" +
                    "This app does not include Daggerfall's game data, because that data is\n" +
                    "still Bethesda's copyright even though the game is a free download.\n\n" +
                    "To play:\n" +
                    "  1. Download Daggerfall for free from Bethesda (or use a GOG/Steam copy).\n" +
                    "  2. Find the 'arena2' folder inside the Daggerfall install.\n" +
                    "  3. Copy that whole folder into THIS folder, so you end up with:\n" +
                    "         <this folder>/arena2/ARCH3D.BSA\n" +
                    "         <this folder>/arena2/BLOCKS.BSA\n" +
                    "         <this folder>/arena2/MAPS.BSA\n" +
                    "         ...and the rest (about 512 MB, ~1560 files)\n" +
                    "  4. Relaunch the app.\n\n" +
                    "Copy via Finder (connect device > Files > this app) or the Files app.\n\n" +
                    "IMPORTANT: if your copy lives in iCloud Drive, make sure the files are\n" +
                    "actually downloaded first. iCloud placeholders report the right size but\n" +
                    "contain no data, and the game will fail when loading the world.\n");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MobileDataPath] Could not create drop-target readme: " + ex.Message);
            }
        }
    }
}
