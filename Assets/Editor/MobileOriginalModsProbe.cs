// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Command-line probe for the original-mod unlock: lists a real archive's entries with the same code the
//   device uses (MobileOriginalMods.TryListEntries) and says which recognizer rows it matches. Used to fill
//   the DREAM / DREAM SKY rows from the real Nexus downloads (plan Task 8):
//     DFU_ARCHIVE="/path/DREAM.7z" Unity -batchmode -quit -projectPath ... \
//       -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileOriginalModsProbe.ListFromEnv -logFile ...

using System.Collections.Generic;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileOriginalModsProbe
    {
        public static void ListFromEnv()
        {
            string path = System.Environment.GetEnvironmentVariable("DFU_ARCHIVE");
            if (string.IsNullOrEmpty(path)) { Debug.Log("[OriginalModsProbe] set DFU_ARCHIVE to an archive path"); return; }
            List<MobileOriginalMods.ArchiveEntry> full; string error;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!MobileOriginalMods.TryListEntries(path, out full, out error))
            {
                Debug.Log("[OriginalModsProbe] couldn't read " + path + ": " + error);
                return;
            }
            var entries = full.ConvertAll(x => x.Name);
            Debug.Log("[OriginalModsProbe] " + entries.Count + " entries in " + sw.ElapsedMilliseconds + " ms: " + path);
            foreach (var e in full)
                Debug.Log("[OriginalModsProbe] entry " + e.Name + " (" + e.Size + " B) -> " + MobileOriginalMods.Normalize(e.Name));
            foreach (var row in MobileOriginalMods.Rows)
                Debug.Log("[OriginalModsProbe] row " + row.Key + (MobileOriginalMods.MatchArchive(row, entries) ? " MATCHES" : " no match"));
        }
    }
}
