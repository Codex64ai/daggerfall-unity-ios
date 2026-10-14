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
using System.Diagnostics;
using System.IO;
using System.Text;
using SharpCompress.Archives;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static partial class MobileOriginalMods
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

        // DREAM 2026 desktop originals: one "dream - <module>.dfmod" per module inside a Mods/ folder (13 modules).
        static readonly string[] DreamModules =
        {
            "dream - textures", "dream - paperdoll", "dream - mobs", "dream - npcs", "dream - cinematics",
            "dream - backgrounds", "dream - commoners", "dream - music", "dream - handheld", "dream - sprites",
            "dream - portraits", "dream - hud & menu", "dream - sound",
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
            "dream-sound",
            "dream-textures_1_of_10", "dream-textures_2_of_10", "dream-textures_3_of_10", "dream-textures_4_of_10",
            "dream-textures_5_of_10", "dream-textures_6_of_10", "dream-textures_7_of_10", "dream-textures_8_of_10",
            "dream-textures_9_of_10", "dream-textures_10_of_10",
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

        /// <summary>Extra old names for a row (tools/known_ours.py reads the string arguments).</summary>
        static Row WithOldNames(Row r, params string[] more)
        {
            r.OldNames = Concat(r.OldNames, more);
            return r;
        }

        static string[] Concat(string[] a, string[] b)
        {
            var r = new string[a.Length + b.Length];
            Array.Copy(a, r, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
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
                // "dream - sound.dfmod": our earlier iOS build under the exact desktop name (KnownOurs at its size).
                // "dream - textures (N of 10).dfmod": the same builds' texture parts (~/dev/dfu-mods/iOS names).
                Unlocks = unlocks, OldNames = Concat(old, new[] { "dream - sound.dfmod",
                    "dream - textures (1 of 10).dfmod", "dream - textures (2 of 10).dfmod", "dream - textures (3 of 10).dfmod",
                    "dream - textures (4 of 10).dfmod", "dream - textures (5 of 10).dfmod", "dream - textures (6 of 10).dfmod",
                    "dream - textures (7 of 10).dfmod", "dream - textures (8 of 10).dfmod", "dream - textures (9 of 10).dfmod",
                    "dream - textures (10 of 10).dfmod" }),
            };
        }

        /// <summary>The shipped table: one row per private_only entry in tools/bundled-mods/mods.json, plus DREAM and DREAM SKY.</summary>
        public static readonly Row[] Rows =
        {
            DreamRow(),
            new Row
            {
                // DREAM SKY original: "dream - sky.dfmod" (from DREAM - SKY-664-1-2-*.rar).
                Key = "DREAMSKY", DisplayName = "DREAM SKY", Url = "https://www.nexusmods.com/daggerfallunity/mods/664",
                ArchiveMarkers = new[] { "dream - sky" }, ArchiveThreshold = 1,
                DfmodMarkers = new[] { "dream - sky" }, DfmodMinBytes = 1L << 20,
                // "dream - sky.dfmod" is also our earlier iOS build under the exact desktop name (KnownOurs at its size).
                Unlocks = new[] { "dream-sky-ios.dfmod" }, OldNames = new[] { "dream-sky.dfmod", "dream - sky.dfmod" },
            },
            StemRow("DEX", "Daggerfall Enemy Expansion", "https://www.nexusmods.com/daggerfallunity/mods/372",
                    "DaggerfallBestiaryProject", 1L << 20),
            StemRow("REALGRASS", "Real Grass", "https://www.nexusmods.com/daggerfallunity/mods/20",
                    "RealGrass", 1L << 20),
            StemRow("DYNAMICSKIES", "Dynamic Skies", "https://github.com/drcarademono/dynamic-skies",
                    "dynamic-skies", 64L << 10),
            StemRow("WOD", "World of Daggerfall", "https://github.com/drcarademono/world-of-daggerfall",
                    "WorldOfDaggerfall", 1L << 20),
            // "wod-biomes.dfmod": our earlier build under another name (byte-identical, ~/dev/dfu-mods; KnownOurs at its size).
            WithOldNames(StemRow("WODBIOMES", "World of Daggerfall - Biomes", "https://github.com/drcarademono/wod-biomes",
                    "World of Daggerfall - Biomes", 1L << 20), "wod-biomes.dfmod"),
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

        /// <summary>Row whose OldNames contains the file's leaf name (case-insensitive); newName = its "-ios" name (or the
        /// row's only pack file when that name is not one of its Unlocks).</summary>
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
                            // An alias whose own -ios name is not shipped maps onto the row's single pack file.
                            newName = IosName(o);
                            if (r.Unlocks != null && r.Unlocks.Length == 1 && Array.IndexOf(r.Unlocks, newName) < 0) newName = r.Unlocks[0];
                            return r;
                        }
            return null;
        }

        #endregion

        #region Receipts and the scan

        /// <summary>One original seen on this device. MTime = DateTime.ToFileTimeUtc of the matched file.</summary>
        [Serializable]
        public class Receipt
        {
            public string Key, MatchedFile;
            public long Size, MTime;
            public string Date;
        }

        [Serializable]
        public class ReceiptFile
        {
            public List<Receipt> Receipts = new List<Receipt>();
        }

        /// <summary>A greyed row in the MODS window. Url is null for "Couldn't read" rows.</summary>
        public struct LockedEntry
        {
            public string Key, Label, Url;
        }

        public const string ReceiptFileName = "OriginalReceipts.json";

        /// <summary>Rebuilt by every scan (and the gate's NoteVerdict); read by the MODS window.</summary>
        public static readonly List<LockedEntry> Locked = new List<LockedEntry>();

        /// <summary>Full paths of .dfmod files recognised as desktop ORIGINALS by the last scan (never loaded on iOS).</summary>
        public static readonly HashSet<string> DesktopOriginals = new HashSet<string>(StringComparer.Ordinal);

        static readonly string[] ScanExtensions = { ".zip", ".7z", ".rar", ".dfmod" };

        /// <summary>Missing, truncated or garbage file -> an empty ReceiptFile. Never throws.</summary>
        public static ReceiptFile LoadReceipts(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new ReceiptFile();
                var f = JsonUtility.FromJson<ReceiptFile>(File.ReadAllText(path));
                if (f == null) return new ReceiptFile();
                if (f.Receipts == null) f.Receipts = new List<Receipt>();
                f.Receipts.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Key));
                return f;
            }
            catch (Exception)
            {
                return new ReceiptFile();
            }
        }

        /// <summary>Writes path+".tmp" then replaces path, so a kill mid-write never leaves half a file.</summary>
        public static void SaveReceipts(string path, ReceiptFile f)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(f ?? new ReceiptFile(), true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>True when a receipt names this exact file (leaf name + size + mtime).</summary>
        public static bool IsCovered(ReceiptFile f, string fileName, long size, long mtime)
        {
            if (f == null || f.Receipts == null) return false;
            string leaf = Leaf(fileName);
            foreach (Receipt r in f.Receipts)
                if (r != null && string.Equals(r.MatchedFile, leaf, StringComparison.OrdinalIgnoreCase) && r.Size == size && r.MTime == mtime)
                    return true;
            return false;
        }

        public static HashSet<string> ReceiptKeys(ReceiptFile f)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (f != null && f.Receipts != null)
                foreach (Receipt r in f.Receipts)
                    if (r != null && !string.IsNullOrEmpty(r.Key)) keys.Add(r.Key);
            return keys;
        }

        /// <summary>
        /// Looks at every zip/7z/rar/dfmod under modsDir (all subfolders) that no receipt covers, adds one receipt per
        /// newly matched Key (first match wins) and saves only when something was added. listEntries returns null for an
        /// unreadable archive. Rebuilds Locked ("Couldn't read" rows) and DesktopOriginals. Never throws.
        /// </summary>
        public static ReceiptFile ScanOnce(string modsDir, string receiptPath, Row[] rows,
                                           Func<string, List<string>> listEntries, Action<string> log,
                                           Dictionary<string, long> packIndex = null)
        {
            Locked.Clear();
            DesktopOriginals.Clear();
            ReceiptFile receipts = LoadReceipts(receiptPath);
            if (log == null) log = _ => { };
            if (rows == null || string.IsNullOrEmpty(modsDir)) return receipts;

            string[] files;
            try
            {
                if (!Directory.Exists(modsDir)) return receipts;
                files = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
            }
            catch (Exception e)
            {
                log("[OriginalMods] scan failed: " + e.Message);
                return receipts;
            }

            HashSet<string> have = ReceiptKeys(receipts);
            bool added = false;
            foreach (string path in files)
            {
                try
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (Array.IndexOf(ScanExtensions, ext) < 0) continue;
                    string leaf = Path.GetFileName(path);
                    var info = new FileInfo(path);
                    long size = info.Length, mtime = info.LastWriteTimeUtc.ToFileTimeUtc();
                    bool covered = IsCovered(receipts, leaf, size, mtime);
                    var sw = Stopwatch.StartNew();

                    if (ext == ".dfmod")
                    {
                        // Our own pack files (old names carry UnityFS and pass the size floor) never unlock anything.
                        if (IsKnownOurs(leaf, size, packIndex)) continue;
                        // Only files named like a desktop original are opened (8 bytes).
                        string n = Normalize(leaf);
                        List<Row> candidates = null;
                        foreach (Row r in rows)
                            if (r.DfmodMarkers != null)
                                foreach (string m in r.DfmodMarkers)
                                    if (Normalize(m) == n) { (candidates = candidates ?? new List<Row>()).Add(r); break; }
                        if (candidates == null) continue;
                        if (covered) { DesktopOriginals.Add(path); continue; }

                        byte[] head = new byte[8];
                        int read;
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            read = fs.Read(head, 0, head.Length);
                        if (read < head.Length) Array.Resize(ref head, read);
                        foreach (Row r in candidates)
                        {
                            if (!MatchDfmod(r, leaf, head, size)) continue;
                            DesktopOriginals.Add(path);
                            if (have.Add(r.Key))
                            {
                                AddReceipt(receipts, r.Key, leaf, size, mtime);
                                added = true;
                                log(string.Format("[OriginalMods] receipt {0} <- {1} ({2} ms)", r.Key, leaf, sw.ElapsedMilliseconds));
                            }
                        }
                        continue;
                    }

                    if (covered) continue;
                    List<string> entries = listEntries != null ? listEntries(path) : null;
                    if (entries == null)
                    {
                        Locked.Add(new LockedEntry { Key = null, Label = "Couldn't read " + leaf, Url = null });
                        log("[OriginalMods] couldn't read " + leaf);
                        continue;
                    }
                    foreach (Row r in rows)
                    {
                        if (have.Contains(r.Key) || !MatchArchive(r, entries)) continue;
                        have.Add(r.Key);
                        AddReceipt(receipts, r.Key, leaf, size, mtime);
                        added = true;
                        log(string.Format("[OriginalMods] receipt {0} <- {1} ({2} ms)", r.Key, leaf, sw.ElapsedMilliseconds));
                    }
                }
                catch (Exception e)
                {
                    log("[OriginalMods] skipped " + Path.GetFileName(path) + ": " + e.Message);
                }
            }

            if (added)
            {
                try { SaveReceipts(receiptPath, receipts); }
                catch (Exception e) { log("[OriginalMods] could not save receipts: " + e.Message); }
            }
            return receipts;
        }

        /// <summary>
        /// A pack file we built: (leaf name, exact size) is in KnownOurs (generated by tools/known_ours.py) or in the
        /// loaded pack-index.json. Such a file is never a desktop original.
        /// </summary>
        public static bool IsKnownOurs(string fileName, long size, Dictionary<string, long> packIndex)
        {
            string leaf = Leaf(fileName);
            foreach (KeyValuePair<string, long> k in KnownOurs)
                if (k.Value == size && string.Equals(k.Key, leaf, StringComparison.OrdinalIgnoreCase)) return true;
            long indexed;
            return packIndex != null && packIndex.TryGetValue(leaf, out indexed) && indexed == size;
        }

        static void AddReceipt(ReceiptFile f, string key, string leaf, long size, long mtime)
        {
            f.Receipts.Add(new Receipt
            {
                Key = key, MatchedFile = leaf, Size = size, MTime = mtime,
                Date = DateTime.UtcNow.ToString("o"),
            });
        }

        #endregion

        #region Gate

        public enum Verdict { Load, Locked, SkipDesktopOriginal, SkipOldName }

        public const string PackIndexFileName = "pack-index.json";

        [Serializable]
        class PackIndexEntry { public string file; public long size; public string key; }

        [Serializable]
        class PackIndexWrapper { public List<PackIndexEntry> items; }

        /// <summary>
        /// First pack-index.json under modsDir (all subfolders): a JSON array of {file, size, key} -> file leaf name -> size.
        /// Missing or malformed -> empty (the gate then decides by name alone and fails closed). Never throws.
        /// </summary>
        public static Dictionary<string, long> LoadPackIndex(string modsDir)
        {
            var index = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrEmpty(modsDir) || !Directory.Exists(modsDir)) return index;
                string[] hits = Directory.GetFiles(modsDir, PackIndexFileName, SearchOption.AllDirectories);
                if (hits.Length == 0) return index;
                Array.Sort(hits, StringComparer.Ordinal);
                string text = File.ReadAllText(hits[0]).Trim();
                if (!text.StartsWith("[", StringComparison.Ordinal)) return index;
                var w = JsonUtility.FromJson<PackIndexWrapper>("{\"items\":" + text + "}");
                if (w == null || w.items == null) return index;
                foreach (PackIndexEntry e in w.items)
                    if (e != null && !string.IsNullOrEmpty(e.file))
                        index[Leaf(e.file)] = e.size;
            }
            catch (Exception)
            {
                index.Clear();
            }
            return index;
        }

        /// <summary>
        /// Whether one .dfmod may load. Order: desktop original -> skip; old name with its new name present -> skip;
        /// not ours -> load; pack-index size mismatch -> load (not ours); receipt -> load; else locked.
        /// Old names are gated exactly like the -ios names (via the row's OldNames).
        /// </summary>
        public static Verdict Decide(string filePath, long sizeBytes, Row[] rows, HashSet<string> receiptKeys,
                                     Dictionary<string, long> packIndex, HashSet<string> siblingLeafNames, bool isDesktopOriginal)
        {
            if (isDesktopOriginal) return Verdict.SkipDesktopOriginal;
            string leaf = Leaf(filePath);

            string newName;
            Row row = RowForOldName(leaf, rows, out newName);
            if (row != null)
            {
                if (siblingLeafNames != null && ContainsIgnoreCase(siblingLeafNames, newName))
                    return Verdict.SkipOldName;
            }
            else
            {
                row = RowForPackFile(leaf, rows);
                if (row == null) return Verdict.Load;
                long indexed;
                if (packIndex != null && packIndex.TryGetValue(leaf, out indexed) && indexed != sizeBytes)
                    return Verdict.Load;
            }
            if (receiptKeys != null && receiptKeys.Contains(row.Key)) return Verdict.Load;
            return Verdict.Locked;
        }

        static bool ContainsIgnoreCase(HashSet<string> set, string name)
        {
            if (set.Contains(name)) return true;
            foreach (string s in set)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Documents/Mods/GameData/&lt;this&gt;: a test build that finds it enforces the lock exactly like a release build.</summary>
        public const string EnforceLockFileName = "enforce-original-lock";

        // Test-app builds (DFU_IOS_TESTAPP) do not enforce the lock. The define sets only this flag; the bypass is ApplyLockMode.
#if DFU_IOS_TESTAPP
        public static readonly bool IsTestBuild = true;
#else
        public static readonly bool IsTestBuild = false;
#endif

        /// <summary>Set by each ModManager scan: Documents/Mods/GameData/enforce-original-lock exists.</summary>
        public static bool EnforceFilePresent;

        /// <summary>Release builds always enforce; test builds only with the enforce file.</summary>
        public static bool LockEnforced(bool isTestBuild, bool enforceFileExists)
        {
            return !isTestBuild || enforceFileExists;
        }

        /// <summary>The gate verdict after the build's lock mode: an unenforced lock turns Locked into Load, nothing else changes
        /// (desktop originals and superseded old names are still skipped).</summary>
        public static Verdict ApplyLockMode(Verdict v, bool isTestBuild, bool enforceFileExists)
        {
            return v == Verdict.Locked && !LockEnforced(isTestBuild, enforceFileExists) ? Verdict.Load : v;
        }

        /// <summary>The one scan-time log line for a test build; null for a release build (no line).</summary>
        public static string LockModeLine(bool isTestBuild, bool enforceFileExists)
        {
            if (!isTestBuild) return null;
            return enforceFileExists ? "[OriginalMods] test build: lock enforced (" + EnforceLockFileName + " present)"
                                     : "[OriginalMods] test build: lock not enforced";
        }

        /// <summary>IO: the scan for ModManager.FindModsFromDirectory. Only when MobileContentPath.Active; never throws.</summary>
        public static void RunScanForModManager(string modsDir, string modDataDir)
        {
            if (!MobileContentPath.Active) return;
            try { EnforceFilePresent = File.Exists(Path.Combine(modDataDir, EnforceLockFileName)); }
            catch (Exception) { EnforceFilePresent = false; }
            string mode = LockModeLine(IsTestBuild, EnforceFilePresent);
            if (mode != null) UnityEngine.Debug.Log(mode);
            try
            {
                ScanOnce(modsDir, Path.Combine(modDataDir, ReceiptFileName), Rows, ListOrNull, UnityEngine.Debug.Log,
                         LoadPackIndex(modsDir));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("[OriginalMods] scan failed: " + e.Message);
            }
        }

        static List<string> ListOrNull(string path)
        {
            List<string> entries; string error;
            return TryListEntries(path, out entries, out error) ? entries : null;
        }

        /// <summary>Records a non-Load verdict: one "Needs ..." locked row per Key, one log line per skipped file.</summary>
        /// <summary>MODS window: rows at or after the real mods are locked rows (never index modSettings with one).</summary>
        public static bool IsLockedRow(int index, int realCount)
        {
            return index >= realCount;
        }

        /// <summary>MODS window: a tap on a locked row that was already selected opens its download page.</summary>
        public static bool TapOpensUrl(int selectedIndex, int previousSelection, int realCount)
        {
            return IsLockedRow(selectedIndex, realCount) && selectedIndex == previousSelection;
        }

        public static void NoteVerdict(Verdict v, string filePath)
        {
            string leaf = Leaf(filePath);
            switch (v)
            {
                case Verdict.Locked:
                {
                    string ignored;
                    Row row = RowForPackFile(leaf, Rows) ?? RowForOldName(leaf, Rows, out ignored);
                    string key = row != null ? row.Key : "?";
                    if (row != null && !Locked.Exists(l => l.Key == row.Key))
                        Locked.Add(new LockedEntry { Key = row.Key, Label = "Needs " + row.DisplayName + " - tap for the download page", Url = row.Url });
                    UnityEngine.Debug.Log(string.Format("[OriginalMods] locked {0} (needs {1})", leaf, key));
                    break;
                }
                case Verdict.SkipDesktopOriginal:
                    UnityEngine.Debug.Log("[OriginalMods] skipped desktop original " + leaf);
                    break;
                case Verdict.SkipOldName:
                {
                    string newName;
                    RowForOldName(leaf, Rows, out newName);
                    UnityEngine.Debug.Log(string.Format("[OriginalMods] skipped old name {0}, {1} present", leaf, newName));
                    break;
                }
            }
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
