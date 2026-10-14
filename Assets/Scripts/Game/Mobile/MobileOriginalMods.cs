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
using System.Text;
using SharpCompress.Archives;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileOriginalMods
    {
        #region Recognizer table

        /// <summary>One Non-MIT mod: how to recognise the player's original, and which of our pack files it unlocks.</summary>
        public sealed class Row
        {
            public string Key, DisplayName, Url;
            public string[] ArchiveMarkers;     // entry names inside the original's archive (normalized compare)
            public int ArchiveThreshold;        // distinct marker hits needed
            public string[] DfmodMarkers;       // desktop .dfmod file names (normalized compare)
            public long DfmodMinBytes;          // stops an empty file renamed to a marker name
            public string[] Unlocks;            // our pack file names, all "<stem>-ios.dfmod"
            public string[] OldNames;           // our pre-rename file names; each maps to IosName(itself)
        }

        public static readonly byte[] UnityFsMagic = Encoding.ASCII.GetBytes("UnityFS");

        // PLACEHOLDER - verify against the Nexus download (Task 8)
        static readonly string[] DreamModules =
        {
            "DREAM - HUD & MENU", "DREAM - PAPERDOLL", "DREAM - SPRITES", "DREAM - MOBS", "DREAM - NPCS",
            "DREAM - COMMONERS", "DREAM - PORTRAITS", "DREAM - BACKGROUNDS", "DREAM - HANDHELD", "DREAM - SOUND",
            "DREAM - MUSIC", "DREAM - TEXTURES", "DREAM - MOVIES",
        };

        // Our DREAM pack file stems (the converter's names), each shipped as "<stem>-ios.dfmod".
        static readonly string[] DreamPackStems =
        {
            "dream-backgrounds", "dream-commoners_1_of_2", "dream-commoners_2_of_2", "dream-handheld", "dream-hud-menu",
            "dream-mobs_1_of_6", "dream-mobs_2_of_6", "dream-mobs_3_of_6", "dream-mobs_4_of_6", "dream-mobs_5_of_6",
            "dream-mobs_6_of_6", "dream-npcs_1_of_3", "dream-npcs_2_of_3", "dream-npcs_3_of_3",
            "dream-paperdoll_1_of_8", "dream-paperdoll_2_of_8", "dream-paperdoll_3_of_8", "dream-paperdoll_4_of_8",
            "dream-paperdoll_5_of_8", "dream-paperdoll_6_of_8", "dream-paperdoll_7_of_8", "dream-paperdoll_8_of_8",
            "dream-portraits", "dream-sprites_1_of_2", "dream-sprites_2_of_2",
            "dream-sound", "dream-textures",
        };

        static Row StemRow(string key, string displayName, string url, string manifestStem, long minBytes)
        {
            string old = manifestStem.ToLowerInvariant() + ".dfmod";
            return new Row
            {
                Key = key, DisplayName = displayName, Url = url,
                ArchiveMarkers = new[] { manifestStem }, ArchiveThreshold = 1,
                DfmodMarkers = new[] { manifestStem }, DfmodMinBytes = minBytes,
                Unlocks = new[] { IosName(manifestStem) },
                OldNames = new[] { old },
            };
        }

        static Row DreamRow()
        {
            var unlocks = new string[DreamPackStems.Length];
            var old = new string[DreamPackStems.Length];
            for (int i = 0; i < DreamPackStems.Length; i++)
            {
                unlocks[i] = IosName(DreamPackStems[i]);
                old[i] = DreamPackStems[i] + ".dfmod";
            }
            return new Row
            {
                Key = "DREAM", DisplayName = "DREAM", Url = "https://www.nexusmods.com/daggerfallunity/mods/5",
                ArchiveMarkers = DreamModules, ArchiveThreshold = 10,
                DfmodMarkers = DreamModules, DfmodMinBytes = 8L << 20,
                Unlocks = unlocks, OldNames = old,
            };
        }

        /// <summary>The shipped table: one row per private_only entry in tools/bundled-mods/mods.json, plus DREAM and DREAM SKY.</summary>
        public static readonly Row[] Rows =
        {
            DreamRow(),
            new Row
            {
                // PLACEHOLDER - verify against the Nexus download (Task 8)
                Key = "DREAMSKY", DisplayName = "DREAM SKY", Url = "https://www.nexusmods.com/daggerfallunity/mods/664",
                ArchiveMarkers = new[] { "DREAM - SKY" }, ArchiveThreshold = 1,
                DfmodMarkers = new[] { "DREAM - SKY" }, DfmodMinBytes = 1L << 20,
                Unlocks = new[] { "dream-sky-ios.dfmod" }, OldNames = new[] { "dream-sky.dfmod" },
            },
            StemRow("DEX", "Daggerfall Enemy Expansion", "https://www.nexusmods.com/daggerfallunity/mods/372",
                    "DaggerfallBestiaryProject", 1L << 20),
            StemRow("REALGRASS", "Real Grass", "https://www.nexusmods.com/daggerfallunity/mods/20",
                    "RealGrass", 1L << 20),
            StemRow("DYNAMICSKIES", "Dynamic Skies", "https://github.com/drcarademono/dynamic-skies",
                    "dynamic-skies", 64L << 10),
            StemRow("WOD", "World of Daggerfall", "https://github.com/drcarademono/world-of-daggerfall",
                    "WorldOfDaggerfall", 1L << 20),
            StemRow("WODBIOMES", "World of Daggerfall - Biomes", "https://github.com/drcarademono/wod-biomes",
                    "World of Daggerfall - Biomes", 1L << 20),
            StemRow("WODTERRAIN", "World of Daggerfall - Terrain", "https://github.com/drcarademono/wod-terrain",
                    "wod-terrain", 64L << 10),
            StemRow("DISTANTTERRAIN", "Distant Terrain of the World of Daggerfall", "https://www.nexusmods.com/daggerfallunity/mods/1284",
                    "distantterrain", 1L << 20),
        };

        #endregion

        #region Matching (pure)

        /// <summary>Leaf name only; lower-case; strip ".dfmod"; drop ' ', '-', '_'.</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            string leaf = slash >= 0 ? name.Substring(slash + 1) : name;
            leaf = leaf.ToLowerInvariant();
            if (leaf.EndsWith(".dfmod", StringComparison.Ordinal))
                leaf = leaf.Substring(0, leaf.Length - ".dfmod".Length);
            var sb = new StringBuilder(leaf.Length);
            foreach (char c in leaf)
                if (c != ' ' && c != '-' && c != '_')
                    sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Our pack file name for a bundle stem (mirrors tools/bundled-mods/pack.py ios_name):
        /// "dream - mobs (2 of 6)" -> "dream-mobs_2_of_6-ios.dfmod". Idempotent on an "-ios" name.
        /// </summary>
        public static string IosName(string stem)
        {
            string s = (stem ?? string.Empty).ToLowerInvariant();
            if (s.EndsWith(".dfmod", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - ".dfmod".Length);
            s = s.Replace(" & ", " ").Replace(" - ", "-").Replace(" (", "_").Replace(" of ", "_of_").Replace(")", "").Replace(' ', '-');
            if (!s.EndsWith("-ios", StringComparison.Ordinal))
                s += "-ios";
            return s + ".dfmod";
        }

        /// <summary>Distinct normalized marker hits over the entries' leaf names >= ArchiveThreshold.</summary>
        public static bool MatchArchive(Row row, IEnumerable<string> entryNames)
        {
            if (row == null || row.ArchiveMarkers == null || entryNames == null) return false;
            var markers = new HashSet<string>();
            foreach (string m in row.ArchiveMarkers) markers.Add(Normalize(m));
            var hits = new HashSet<string>();
            foreach (string e in entryNames)
            {
                string n = Normalize(e);
                if (markers.Contains(n)) hits.Add(n);
            }
            return hits.Count > 0 && hits.Count >= row.ArchiveThreshold;
        }

        /// <summary>A desktop original .dfmod: name is a marker, file starts "UnityFS", size >= DfmodMinBytes.</summary>
        public static bool MatchDfmod(Row row, string fileName, byte[] head, long sizeBytes)
        {
            if (row == null || row.DfmodMarkers == null || head == null || head.Length < UnityFsMagic.Length) return false;
            if (sizeBytes < row.DfmodMinBytes || sizeBytes <= 0) return false;
            for (int i = 0; i < UnityFsMagic.Length; i++)
                if (head[i] != UnityFsMagic[i]) return false;
            string n = Normalize(fileName);
            foreach (string m in row.DfmodMarkers)
                if (Normalize(m) == n) return true;
            return false;
        }

        static string Leaf(string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? string.Empty : Path.GetFileName(fileName.Replace('\\', '/'));
        }

        /// <summary>Row whose Unlocks contains the file's leaf name (case-insensitive), else null.</summary>
        public static Row RowForPackFile(string fileName, Row[] rows)
        {
            string leaf = Leaf(fileName);
            if (rows == null || leaf.Length == 0) return null;
            foreach (Row r in rows)
                if (r.Unlocks != null)
                    foreach (string u in r.Unlocks)
                        if (string.Equals(u, leaf, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>Row whose OldNames contains the file's leaf name (case-insensitive); newName = its "-ios" name.</summary>
        public static Row RowForOldName(string fileName, Row[] rows, out string newName)
        {
            newName = null;
            string leaf = Leaf(fileName);
            if (rows == null || leaf.Length == 0) return null;
            foreach (Row r in rows)
                if (r.OldNames != null)
                    foreach (string o in r.OldNames)
                        if (string.Equals(o, leaf, StringComparison.OrdinalIgnoreCase))
                        {
                            newName = IosName(o);
                            return r;
                        }
            return null;
        }

        #endregion

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
