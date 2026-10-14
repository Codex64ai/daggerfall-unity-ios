// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Original-mod unlock. Our iOS conversions of restricted mods (the Non-MIT pack) load only once the
//   player's own copy of the ORIGINAL mod has been seen in Documents/Mods. Everything is local and
//   one-time, and nothing here may block start-up. Spec:
//   docs/superpowers/specs/2026-10-14-original-mod-unlock-design.md
//
//   Pure functions take their data as parameters so the editor self-test can run them
//   (MobileContentPath.Active is false in the editor). SharpCompress is used ONLY to list archive
//   entry names; nothing is ever extracted.

using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileOriginalMods
    {
        /// <summary>
        /// Lists the entry names (full paths inside the archive, directories excluded) of a zip/7z/rar.
        /// Header-only (ArchiveFactory + Entries, never ReaderFactory). Any exception -> false with
        /// error = the message; nothing escapes. Logs nothing (callers log once).
        /// </summary>
        public static bool TryListEntries(string archivePath, out List<string> entries, out string error)
        {
            entries = new List<string>();
            error = null;
            try
            {
                using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var a = ArchiveFactory.Open(fs))
                {
                    foreach (var en in a.Entries)
                        if (!en.IsDirectory)
                            entries.Add(en.Key);
                }
                return true;
            }
            catch (Exception e)
            {
                entries = new List<string>();
                error = string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message;
                return false;
            }
        }
    }
}
