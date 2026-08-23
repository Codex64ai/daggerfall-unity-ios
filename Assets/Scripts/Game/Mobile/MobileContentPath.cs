// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Daggerfall Unity loads all of its user-replaceable content - loose textures, sounds,
//   books, world data, movies and quest scripts - from Application.streamingAssetsPath.
//   On desktop that folder sits next to the executable and the player can drop files into
//   it. On iOS it is inside the signed app bundle and is READ-ONLY, so none of it can ever
//   be added to. That single fact, not any deeper incompatibility, is why the port had no
//   mod support: the loaders all worked, they were just pointed somewhere unreachable.
//
//   This redirects those lookups to persistentDataPath - the Documents folder exposed by
//   file sharing, the same place arena2 goes.
//
//   ADDITIVE, never a swap. Textures/, Sound/ and especially Quests/ (265 shipped quest
//   scripts the game depends on) all contain content that ships with the build. A straight
//   redirect would leave the game unable to find its own data. Override() therefore returns
//   the user's copy only when one actually exists, and falls through to the shipped file
//   otherwise.
//
//   No-op on every other platform: Override() returns its argument unchanged, so desktop
//   behaviour is bit-for-bit what it was.
//

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileContentPath
    {
        /// <summary>Subfolders a player is expected to add content to.</summary>
        static readonly string[] userFolders =
        {
            "Mods", "Textures", "Textures/Img", "Textures/CifRci",
            "Sound", "Quests", "QuestPacks", "Books", "WorldData",
        };

        const string readmeName = "PUT-MODS-AND-LOOSE-FILES-HERE.txt";

        /// <summary>True only on an iOS device build, where streaming assets are read-only.</summary>
        public static bool Active
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Writable content root. Documents on iOS; the shipped root elsewhere.</summary>
        public static string UserRoot
        {
            get { return Active ? Application.persistentDataPath : Application.streamingAssetsPath; }
        }

        /// <summary>Read-only content root inside the build.</summary>
        public static string ShippedRoot
        {
            get { return Application.streamingAssetsPath; }
        }

        /// <summary>
        /// Given a path under the shipped content root, return the player's copy if they have
        /// one, otherwise the original path untouched.
        ///
        /// Checked per file rather than per folder on purpose: a player who adds one texture
        /// must not shadow the hundreds of shipped files sitting beside it.
        /// </summary>
        public static string Override(string shippedPath)
        {
            if (!Active)
                return shippedPath;

            return Remap(shippedPath, ShippedRoot, UserRoot, Exists);
        }

        static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        /// <summary>
        /// The path arithmetic behind <see cref="Override"/>, with the roots and the existence
        /// check injected.
        ///
        /// Split out so it is testable: on the Mac <see cref="Active"/> is false and Override
        /// is a deliberate no-op, which would leave the interesting logic - prefix matching,
        /// separator handling, the empty-relative case - permanently unexercised.
        /// </summary>
        public static string Remap(string shippedPath, string shippedRoot, string userRoot,
                                   Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(shippedPath) || string.IsNullOrEmpty(shippedRoot))
                return shippedPath;

            if (!shippedPath.StartsWith(shippedRoot, StringComparison.Ordinal))
                return shippedPath;

            string relative = shippedPath.Substring(shippedRoot.Length).TrimStart('/', '\\');
            if (relative.Length == 0)
                return shippedPath;

            string userPath = Path.Combine(userRoot, relative);
            return exists != null && exists(userPath) ? userPath : shippedPath;
        }

        /// <summary>
        /// Writable folder for a given subfolder, created if missing. Used where the engine
        /// enumerates a directory rather than opening a known file.
        /// </summary>
        public static string UserFolder(string subfolder)
        {
            string path = Path.Combine(UserRoot, subfolder);
            try
            {
                if (Active && !Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MobileContentPath] could not create " + path + ": " + ex.Message);
            }

            return path;
        }

        /// <summary>
        /// Files the player has added to a subfolder, for callers that MERGE content instead
        /// of picking one file - the shipped folder is enumerated by the caller as usual and
        /// these are appended. Returns empty when there is nothing to add.
        /// </summary>
        public static string[] UserFiles(string subfolder, string searchPattern = "*", bool recursive = false)
        {
            if (!Active)
                return new string[0];

            string path = Path.Combine(UserRoot, subfolder);
            if (!Directory.Exists(path))
                return new string[0];

            try
            {
                return Directory.GetFiles(path, searchPattern,
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MobileContentPath] could not list " + path + ": " + ex.Message);
                return new string[0];
            }
        }

        /// <summary>
        /// Create the folders a player is meant to drop content into, plus a note explaining
        /// what goes where. Without this the feature is invisible: an empty Documents folder
        /// gives no hint that loose files are supported at all.
        /// </summary>
        public static void EnsureUserFolders()
        {
            if (!Active)
                return;

            for (int i = 0; i < userFolders.Length; i++)
                UserFolder(userFolders[i]);

            try
            {
                string readme = Path.Combine(UserRoot, readmeName);
                if (File.Exists(readme))
                    return;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Daggerfall Unity for iOS - loose files and mods");
                sb.AppendLine();
                sb.AppendLine("Drop content into the folders beside this file. Anything you add here");
                sb.AppendLine("takes precedence over the copy inside the app; anything you do not add");
                sb.AppendLine("falls back to the app's own files, so partial packs are fine.");
                sb.AppendLine();
                sb.AppendLine("  Textures/      loose .png replacements, named like 180_0-0.png");
                sb.AppendLine("  Textures/Img/  loose .png replacements for UI images");
                sb.AppendLine("  Textures/CifRci/  loose .png for CIF/RCI images, e.g. paintings");
                sb.AppendLine("  Sound/         loose .wav sound effects");
                sb.AppendLine("  Quests/        quest scripts as plain .txt");
                sb.AppendLine("  QuestPacks/    quest packs (a folder each, with a QuestList-*.txt)");
                sb.AppendLine("  Books/         loose book text");
                sb.AppendLine("  WorldData/     loose location and block .json");
                sb.AppendLine("  Mods/          .dfmod packages BUILT FOR iOS");
                sb.AppendLine();
                sb.AppendLine("Two things will not work, and cannot be made to:");
                sb.AppendLine();
                sb.AppendLine("  * Mods containing C# code. iOS compiles ahead of time, so there is no");
                sb.AppendLine("    way to execute mod code that was not built into the app.");
                sb.AppendLine("  * .dfmod packages built for Windows, macOS or Linux. Asset bundles are");
                sb.AppendLine("    platform specific and iOS refuses them. They must be rebuilt for iOS.");
                sb.AppendLine();
                sb.AppendLine("Music replacement (.ogg) is not supported yet.");
                File.WriteAllText(readme, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MobileContentPath] could not write readme: " + ex.Message);
            }
        }
    }
}
