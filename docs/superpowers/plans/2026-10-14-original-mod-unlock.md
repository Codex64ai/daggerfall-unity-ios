# Original-Mod Unlock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Our iOS conversions of restricted mods (the Non-MIT pack) load only once the player's own copy of the original mod has been seen in `Documents/Mods`; everything is local, one-time, never blocks start-up.

**Architecture:** One new file `Assets/Scripts/Game/Mobile/MobileOriginalMods.cs` holds a static recognizer table, pure matching/receipt/gate functions (testable in the editor, where `MobileContentPath.Active` is false) and thin IO wrappers (SharpCompress entry listing, receipt file, scan). `ModManager.FindModsFromDirectory` calls the scan first and asks the gate per file. The MODS window appends greyed locked rows after the real ones. `tools/bundled-mods/pack.py --private` builds the Non-MIT zip with `-ios` names and `pack-index.json`.

**Tech Stack:** Unity 6000.3.23f1 (Assembly-CSharp, IL2CPP on iOS), SharpCompress 0.32.2 netstandard2.0 (MIT), JsonUtility for receipts, FullSerializer for Mods.json (unchanged), Python 3 unittest for tools.

**Spec:** `docs/superpowers/specs/2026-10-14-original-mod-unlock-design.md` (read it first; it carries the rules).

## Global Constraints
- Repo: `/Volumes/More data/AI projects/Daggerfall/daggerfall-unity` (the drive copy IS the working copy), branch `unity6-upgrade`. Never stage `ProjectSettings/ProjectSettings.asset` or `Assets/AddressableAssetsData/iOS/addressables_content_state.bin`; `git add` by path only. Do not push.
- Commits end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Self-test = the whole of `Assets/Editor/MobileSelfTest.cs`, run headless, NO `-nographics`, ONE Unity at a time (`ps -Ao comm | grep -c "MacOS/Unity$"` must print 0 first; kill nothing):
  ```
  LOG=/private/tmp/claude-501/-Users-ikrammassabini/selftest-$(date +%H%M).log
  "/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath "/Volumes/More data/AI projects/Daggerfall/daggerfall-unity" -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile "$LOG"; grep -n "error CS\|  FAIL\|passed, " "$LOG" | tail -20
  ```
  Baseline today: `=== 2358 passed, 0 failed ===`. Every task ends with 0 failed and a count >= the previous task's. If Unity cannot write `Library/` on the drive, stop and report; do not switch to `~/dev/daggerfall-unity` silently.
- New tests are `static void TestXxx()` methods registered by one call each after `TestBuildStamp();` at `Assets/Editor/MobileSelfTest.cs:173`, using the existing `Check(bool, string name, string detail = "")` (:6880). Fixtures: temp folder per test under `Path.Combine(Path.GetTempPath(), "dfu_origmod_<test>")`, deleted at the end. Pure functions take their data as parameters (rows, receipts, index) so no test needs `ModManager.Instance` or iOS.
- Token-lean: grep then ranged reads; no whole-file reads of `MobileSelfTest.cs` (10,969 lines), `ModManager.cs`, `MobileModExtractor.cs`. No builds except where a task says so (Tasks 10-11).
- Line anchors below are from HEAD `27e2033c4`; re-grep before editing.
- Rename rule (spec): every Non-MIT pack file is `<stem>-ios.dfmod`. The MIT pack never changes names. Keys are UPPERCASE ASCII: `DREAM`, `DREAMSKY`, `DEX`, `REALGRASS`, `DYNAMICSKIES`, `WOD`, `WODBIOMES`, `WODTERRAIN`, `DISTANTTERRAIN`.

## Review Focus
1. Archive whose entries sit in a top folder (`DREAM 1.2/DREAM - HUD & MENU.dfmod`) must still match: markers compare against the leaf name after normalization (Task 2 test).
2. A truncated or garbage `OriginalReceipts.json` must read as "no receipts" and be overwritten on the next match, never throw (Task 3 test).
3. Our `-ios` pack file must never be mistaken for a desktop original: no row's `Unlocks` name normalizes to one of its dfmod markers (Task 2 table self-consistency test).
4. A malformed `pack-index.json` is the same as a missing one: gate by name, fail closed (Task 4 test).
5. MODS window with 0 real mods and 1 locked row: `SelectedIndex` lands on the locked row; every handler must return without touching `modSettings[0]` (Task 6 source-pin test + pure rule test).

---

### Task 1: SharpCompress 0.32.2 DLL, link.xml, licence record, archive listing wrapper

**Files:**
- Create: `Assets/Plugins/SharpCompress/SharpCompress.dll` (+ Unity-generated `.meta` files, commit them), `Assets/Plugins/SharpCompress/LICENSE.txt`
- Create: `Assets/Scripts/Game/Mobile/MobileOriginalMods.cs` (skeleton: namespace `DaggerfallWorkshop.Game.Mobile`, `public static class MobileOriginalMods`)
- Modify: `Assets/link.xml:47` (before `</linker>`), `THIRD-PARTY.md` (new `## SharpCompress` section before `## Mobile CRT filter` at :1028)
- Test: `Assets/Editor/MobileSelfTest.cs` new `TestOriginalModArchiveListing()`

**Interfaces:**
- Produces: `public static bool TryListEntries(string archivePath, out List<string> entries, out string error)` - entry names (full paths inside the archive, directories excluded) via `SharpCompress.Archives.ArchiveFactory.Open(FileStream)` + `archive.Entries` (never `ReaderFactory`); every exception -> `false`, `error` = the exception message, nothing escapes.

- [ ] **Step 1: Get the DLL.** In a NEW empty scratch dir: `curl -L -o sc.nupkg https://www.nuget.org/api/v2/package/SharpCompress/0.32.2 && unzip -q sc.nupkg -d pkg`. Check `pkg/LICENSE.txt` (or, if the nupkg has none, `curl -L https://raw.githubusercontent.com/adamhathcock/sharpcompress/0.32.2/LICENSE.txt`) starts with `The MIT License (MIT)` / `Copyright (c) 2014 Adam Hathcock` (if not MIT: stop, report). Read `pkg/SharpCompress.nuspec`, group `targetFramework=".NETStandard2.0"`: for each `<dependency>` (expected: `System.Text.Encoding.CodePages`, `System.Memory`), check whether Unity provides it: `ls "/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/NetStandard/ref/2.1.0/" | grep -i <name>`. For any NOT provided, fetch its NuGet package the same way and copy its `lib/netstandard2.0/*.dll` beside SharpCompress.dll (each is MIT; add it to the THIRD-PARTY entry). Copy `pkg/lib/netstandard2.0/SharpCompress.dll` and `pkg/LICENSE.txt` to `Assets/Plugins/SharpCompress/`.
- [ ] **Step 2: link.xml.** Add `<assembly fullname="SharpCompress" preserve="all" />` (and one line per extra DLL) with a comment: used only to list zip/7z/rar entry names for the original-mod unlock.
- [ ] **Step 3: Failing test.** Add `TestOriginalModArchiveListing()`: in a temp dir write `fixture.zip` with SharpCompress's writer (`using (var za = SharpCompress.Archives.Zip.ZipArchive.Create()) { za.AddEntry("DREAM 1.2/DREAM - PAPERDOLL.dfmod", new MemoryStream(new byte[16]), true); za.AddEntry("readme.txt", new MemoryStream(new byte[4]), true); za.SaveTo(path, SharpCompress.Common.CompressionType.Deflate); }`), then `Check(MobileOriginalMods.TryListEntries(path, out e, out err) && e.Count == 2 && e.Contains("DREAM 1.2/DREAM - PAPERDOLL.dfmod"), "original mods: zip entries are listed")`; a file of 64 bytes of 0xFF named `bad.7z` -> `Check(!TryListEntries(...) && !string.IsNullOrEmpty(err), "original mods: a corrupt archive reports an error and throws nothing")`; a missing path -> false. Register the call at :173. Run the self-test: expect `error CS` (TryListEntries undefined).
- [ ] **Step 4: Implement** `TryListEntries` in the new file (`using SharpCompress.Archives;`): open `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)`, `using (var a = ArchiveFactory.Open(fs)) foreach (var en in a.Entries) if (!en.IsDirectory) entries.Add(en.Key);`. Wrap all in try/catch(Exception). Log nothing here (callers log once).
- [ ] **Step 5: THIRD-PARTY.md** section: name, version 0.32.2, copyright, MIT, NuGet URL, "used only to list archive entry names; nothing is extracted", DLL path, any extra DLLs.
- [ ] **Step 6: Run the self-test** -> 0 failed, count = 2358 + 3. Commit: `git add Assets/Plugins/SharpCompress Assets/link.xml THIRD-PARTY.md Assets/Scripts/Game/Mobile/MobileOriginalMods.cs Assets/Scripts/Game/Mobile/MobileOriginalMods.cs.meta Assets/Editor/MobileSelfTest.cs` ; message `Original-mod unlock: SharpCompress 0.32.2 (MIT) for archive listing`.

Done when: DLL + meta committed, self-test lists a zip and survives a corrupt file.

---

### Task 2: Recognizer table, name normalization, matching (pure)

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/MobileOriginalMods.cs`
- Test: `MobileSelfTest.cs` new `TestOriginalModRecognizer()`

**Interfaces (all in `MobileOriginalMods`):**
```csharp
public sealed class Row { public string Key, DisplayName, Url; public string[] ArchiveMarkers; public int ArchiveThreshold;
                          public string[] DfmodMarkers; public long DfmodMinBytes; public string[] Unlocks; public string[] OldNames; }
public static readonly Row[] Rows;                                   // the shipped table
public static string Normalize(string name);                         // leaf name only; lower-case; strip ".dfmod"; drop ' ', '-', '_'
public static bool MatchArchive(Row row, IEnumerable<string> entryNames);  // distinct normalized hits >= ArchiveThreshold
public static bool MatchDfmod(Row row, string fileName, byte[] head, long sizeBytes); // name in DfmodMarkers && head starts "UnityFS" && size >= DfmodMinBytes
public static Row RowForPackFile(string fileName, Row[] rows);       // row whose Unlocks contains fileName (case-insensitive), else null
public static Row RowForOldName(string fileName, Row[] rows, out string newName);
public static readonly byte[] UnityFsMagic = Encoding.ASCII.GetBytes("UnityFS");
```

- [ ] **Step 1: Failing tests** (fixture rows built inline, never `Rows`):
  - `Normalize("DREAM 1.2/DREAM - HUD & MENU.dfmod") == "dreamhud&menu"`; `Normalize("dream_hud-MENU") == "dreamhudmenu"`; `Normalize("x.DFMOD") == "x"`.
  - fixture row `ArchiveMarkers = {"DREAM - A","DREAM - B","DREAM - C"}, ArchiveThreshold = 2`: entries `{"top/dream-a.dfmod","DREAM_B.dfmod","readme.txt"}` -> true; `{"top/dream-a.dfmod","top/dream-a.dfmod"}` (duplicate) -> false; empty -> false.
  - `MatchDfmod`: `DfmodMarkers = {"DREAM - SPRITES"}, DfmodMinBytes = 1024`: name `dream - sprites.dfmod` + head `UnityFS\0` + 4096 -> true; fake head `XXXXXXX` -> false; size 0 -> false; name `other.dfmod` -> false; head shorter than 7 bytes -> false.
  - `RowForPackFile("DREAM-HUD-MENU-IOS.dfmod", rows)` returns the fixture row (case-insensitive); unknown -> null. `RowForOldName("distantterrain.dfmod", rows, out n)` -> row, `n == "distantterrain-ios.dfmod"`.
  - Shipped table checks: every `Rows[i].Key` unique and upper-case; every `Unlocks` entry ends with `-ios.dfmod`; every `OldNames` value is in `Unlocks`; **no `Unlocks` name normalizes to any of that row's `DfmodMarkers`** (Review Focus 3); the `DREAM` row has `ArchiveThreshold >= 10` and 25 `dream-*-ios.dfmod` unlocks (the list in Task 7 Step 1).
- [ ] **Step 2: Run** -> compile errors. **Step 3: Implement.** `Normalize`: take the substring after the last `/` or `\`, `ToLowerInvariant()`, remove a trailing `.dfmod`, then drop `' ', '-', '_'`. `MatchArchive`: `HashSet<string>` of normalized markers; count distinct marker hits over normalized entries. `Rows`: one row per `private_only` entry in `tools/bundled-mods/mods.json` plus DREAM and DREAMSKY. Dfmod markers = the desktop manifest stems (`DaggerfallBestiaryProject`, `RealGrass`, `dynamic-skies`, `WorldOfDaggerfall`, `World of Daggerfall - Biomes`, `wod-terrain`, `distantterrain`); archive markers = the same names, threshold 1; `DfmodMinBytes = 1 << 20` (1 MiB) except `DYNAMICSKIES` and `WODTERRAIN` (64 KiB - small data mods); `Unlocks` = `<manifest stem lower-case>-ios.dfmod`; `OldNames` = `<manifest stem lower-case>.dfmod` -> new. `Url` = the mod's Nexus/GitHub page as recorded in `THIRD-PARTY.md` for that mod (grep the section). DREAM row: `ArchiveMarkers` = the 13 module names below marked `// PLACEHOLDER - verify against the Nexus download (Task 8)`: `DREAM - HUD & MENU`, `DREAM - PAPERDOLL`, `DREAM - SPRITES`, `DREAM - MOBS`, `DREAM - NPCS`, `DREAM - COMMONERS`, `DREAM - PORTRAITS`, `DREAM - BACKGROUNDS`, `DREAM - HANDHELD`, `DREAM - SOUND`, `DREAM - MUSIC`, `DREAM - TEXTURES`, `DREAM - MOVIES`; threshold 10; `DfmodMarkers` = the same 13; `DfmodMinBytes = 8 << 20`; `Unlocks` = the 25 names + `dream-sound-ios.dfmod`, `dream-textures-ios.dfmod`; `Url = "https://www.nexusmods.com/daggerfallunity/mods/5"`. DREAMSKY row: markers `DREAM - SKY`, threshold 1, unlock `dream-sky-ios.dfmod` (placeholder too).
- [ ] **Step 4: Self-test** -> 0 failed. Commit `git add Assets/Scripts/Game/Mobile/MobileOriginalMods.cs Assets/Editor/MobileSelfTest.cs` ; `Original-mod unlock: recognizer table and matching`.

Done when: all fixture-row assertions pass and the table self-consistency checks pass with the placeholder DREAM rows.

---

### Task 3: Receipts and the scan (archive fixtures: zip generated, 7z/rar committed)

**Files:**
- Modify: `MobileOriginalMods.cs`
- Create: `Assets/Editor/TestFixtures/Archives/` + committed `.7z`/`.rar` fixtures (+ `.meta`), `Assets/Editor/MobileOriginalModsProbe.cs` (CLI lister, ~30 lines)
- Test: `TestOriginalModReceipts()`, `TestOriginalModScan()`

**Interfaces:**
```csharp
[Serializable] public class Receipt { public string Key, MatchedFile; public long Size, MTime; public string Date; }  // MTime = DateTime.ToFileTimeUtc
[Serializable] public class ReceiptFile { public List<Receipt> Receipts = new List<Receipt>(); }
public struct LockedEntry { public string Key, Label, Url; }        // Url null for "Couldn't read" rows
public static readonly List<LockedEntry> Locked;                     // rebuilt by every scan; read by the MODS window
public const string ReceiptFileName = "OriginalReceipts.json";
public static ReceiptFile LoadReceipts(string path);                 // missing/corrupt -> empty file, never throws
public static void SaveReceipts(string path, ReceiptFile f);         // JsonUtility.ToJson(f, true) to path+".tmp", then File.Delete(path)+File.Move
public static bool IsCovered(ReceiptFile f, string fileName, long size, long mtime);  // same MatchedFile+Size+MTime
public static HashSet<string> ReceiptKeys(ReceiptFile f);
public static ReceiptFile ScanOnce(string modsDir, string receiptPath, Row[] rows, Func<string, List<string>> listEntries, Action<string> log);
```
`ScanOnce`: clear `Locked`; load receipts; for every `*.zip|*.7z|*.rar|*.dfmod` under `modsDir` (all subfolders) not covered by a receipt (name+size+mtime): `.dfmod` -> read the first 8 bytes, `MatchDfmod` against every row without a receipt; archives -> `listEntries(path)` (null = unreadable -> add `Locked {Key=null, Label="Couldn't read " + leafName}` and log one line), else `MatchArchive` per row. A match adds one receipt per Key (first match wins), logs `[OriginalMods] receipt DREAM <- <file> (<ms> ms)`. Save only when something was added. Any exception inside the loop: one log line, continue. Also record which `.dfmod` paths matched as desktop originals in `public static readonly HashSet<string> DesktopOriginals` (full paths) for Task 4.

- [ ] **Step 1: Fixtures.** From the SharpCompress repo at tag 0.32.2 (`gh api "repos/adamhathcock/sharpcompress/contents/tests/TestArchives/Archives?ref=0.32.2" --jq '.[].name'`) download into a NEW scratch dir: `Zip.deflate.zip`, `7Zip.LZMA.7z`, `Rar.rar`, and the encrypted-header one (`Rar.encrypted_filesAndHeader.rar`; also a 7z `*.encrypted*.7z` if present). `unzip -l Zip.deflate.zip` gives the entry tree that all variants share (expected `Tests/jpg/test.jpg`, `Tests/exe/test.exe`, `Tests/txt/test.txt` - record what you see). Copy the 7z/rar files to `Assets/Editor/TestFixtures/Archives/`. The fixture row in tests uses those leaf names as markers (threshold 2).
- [ ] **Step 2: Failing tests.** `TestOriginalModReceipts`: temp dir; `LoadReceipts` on a missing path -> 0; `SaveReceipts` then `LoadReceipts` round-trips a receipt; write `{garbage` to the path -> `LoadReceipts` returns empty, no throw, and a following `SaveReceipts` overwrites it (Review Focus 2); after save no `.tmp` remains; `IsCovered` true only for the same name+size+mtime. `TestOriginalModScan` with the fixture row and a temp `Mods/` tree: (a) generated zip (Task 1 style, two matching entries in a `top/` folder) -> receipt written, `Locked` empty; (b) `7Zip.LZMA.7z` -> receipt; `Rar.rar` -> receipt (each in its own temp dir, keys differ); (c) zip with one matching entry -> no receipt; (d) corrupt `x.7z` -> no receipt, `Locked` has one `Couldn't read x.7z`, no exception; (e) the encrypted-header rar -> same as (d); (f) a zip written with `new ZipWriterOptions(CompressionType.Deflate) { ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(437) } }` and an entry name `tést.dfmod` -> scan completes, no exception (either outcome accepted; the IL2CPP "no I18N" case is device-only); (g) cache: second `ScanOnce` with a counting `listEntries` lambda does not call it for the receipted archive; (h) delete the archive -> receipt still loaded; delete the receipt file -> `ReceiptKeys` empty; (i) `.dfmod` fixture: `dream - sprites.dfmod` of 2 MiB starting `UnityFS` -> receipt + path in `DesktopOriginals`; same name with `XXXXXXX` -> nothing; 0-byte file renamed -> nothing.
- [ ] **Step 3: Run** -> compile errors. **Step 4: Implement** per the interface. `Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories)` filtered by extension (lower-case compare). Use `Stopwatch` for the ms in the log line.
- [ ] **Step 5 (probe, Task 8 needs it):** `MobileOriginalModsProbe.ListFromEnv()` with `[MenuItem]`-free static method, log prefix `[OriginalModsProbe]` (`DFU_ARCHIVE` env -> prints `TryListEntries` names to the log), used by Task 8 for the real DREAM download.
- [ ] **Step 6: Self-test** -> 0 failed. Commit `git add Assets/Scripts/Game/Mobile/MobileOriginalMods.cs Assets/Editor/MobileSelfTest.cs Assets/Editor/TestFixtures/Archives Assets/Editor/TestFixtures/Archives.meta Assets/Editor/MobileOriginalModsProbe.cs*` ; `Original-mod unlock: receipts, scan and archive fixtures`.

Done when: zip/7z/rar fixtures produce receipts, below-threshold/corrupt/encrypted do not, and the cache skips receipted files.

---

### Task 4: Gate decision (pure) and ModManager wiring

**Files:**
- Modify: `MobileOriginalMods.cs`; `Assets/Game/Addons/ModSupport/ModManager.cs:639-690` (`FindModsFromDirectory`)
- Test: `TestOriginalModGate()`

**Interfaces:**
```csharp
public enum Verdict { Load, Locked, SkipDesktopOriginal, SkipOldName }
public static Dictionary<string, long> LoadPackIndex(string modsDir);  // first pack-index.json under modsDir: [{file,size,key}] -> file->size; missing/malformed -> empty
public static Verdict Decide(string filePath, long sizeBytes, Row[] rows, HashSet<string> receiptKeys,
                             Dictionary<string, long> packIndex, HashSet<string> siblingLeafNames, bool isDesktopOriginal);
public static void RunScanForModManager(string modsDir, string modDataDir);   // IO: ScanOnce with TryListEntries, Debug.Log; only when MobileContentPath.Active
public static void NoteVerdict(Verdict v, string filePath);                   // appends the "Needs <mod>" LockedEntry once per Key; one Debug.Log line per skipped file
```
`Decide` order: `isDesktopOriginal` -> `SkipDesktopOriginal`; `RowForOldName` hit AND `siblingLeafNames` contains the new name -> `SkipOldName`; `RowForPackFile` null -> `Load`; pack index has the leaf name with a different size -> `Load` (treated as not ours); row.Key in `receiptKeys` -> `Load`; else `Locked` (and the caller appends `Locked {Key, Label="Needs " + DisplayName + " - tap for the download page", Url}` once per Key).

- [ ] **Step 1: Failing tests** with fixture rows: pack file + no receipt -> `Locked`; + receipt -> `Load`; desktop original flag -> `SkipDesktopOriginal` even with a receipt; MIT/unknown name -> `Load` with or without receipts; index size mismatch -> `Load`; index missing (empty dict) -> `Locked` (fails closed); malformed `pack-index.json` written to a temp dir -> `LoadPackIndex` returns empty, no throw (Review Focus 4); old name with the new name present in `siblingLeafNames` (names only, from different subfolders) -> `SkipOldName`; old name alone -> `Locked` unless its row has a receipt, then `Load` (RULING 2026-10-14, Opus main: old names are gated exactly like the -ios names - looked up via the row's `OldNames`; otherwise any pre-rename copy would bypass the gate forever). Source pins via `MethodBody(File.ReadAllText("Assets/Game/Addons/ModSupport/ModManager.cs"), "private void FindModsFromDirectory(bool refresh = false)")`: contains `MobileOriginalMods.RunScanForModManager(` before `Directory.GetFiles`, and `MobileOriginalMods.Decide(` before `loadedModNames.Contains`.
- [ ] **Step 2: Run** -> fails. **Step 3: Implement.** In `FindModsFromDirectory` (:639): first statement `if (MobileContentPath.Active) MobileOriginalMods.RunScanForModManager(ModDirectory, ModDataDirectory);`. After `modFileNames[i] = ...` (:668) and the null check, before the `loadedModNames` block (:679):
  ```csharp
  // MOBILE: Non-MIT pack files load only next to the player's own original (receipt); desktop originals never load on iOS.
  if (MobileContentPath.Active)
  {
      var verdict = MobileOriginalMods.Decide(modFilePath, new FileInfo(modFilePath).Length, MobileOriginalMods.Rows,
          MobileOriginalMods.ReceiptKeys(gateReceipts), gatePackIndex, gateSiblings, MobileOriginalMods.DesktopOriginals.Contains(modFilePath));
      if (verdict != MobileOriginalMods.Verdict.Load) { MobileOriginalMods.NoteVerdict(verdict, modFilePath); continue; }
  }
  ```
  where `gateReceipts = LoadReceipts(Path.Combine(ModDataDirectory, ReceiptFileName))`, `gatePackIndex = LoadPackIndex(ModDirectory)`, `gateSiblings` = leaf names of `modFiles`, all computed once before the loop (guarded by `MobileContentPath.Active`, else null). `NoteVerdict` appends the locked row once per Key and logs one line per skipped file (`[OriginalMods] locked <file> (needs DREAM)` / `skipped desktop original <file>` / `skipped old name <file>, <new> present`).
- [ ] **Step 4: Self-test** -> 0 failed. Commit `git add Assets/Scripts/Game/Mobile/MobileOriginalMods.cs Assets/Game/Addons/ModSupport/ModManager.cs Assets/Editor/MobileSelfTest.cs` ; `Original-mod unlock: gate in FindModsFromDirectory`.

Done when: every verdict case is pinned and ModManager compiles with the scan + gate in place (editor behaviour unchanged: `Active` is false there).

---

### Task 5: Keep saved choices for locked packs (MergeModSettings, three sources)

**Files:**
- Modify: `ModManager.cs:48` (field), `:907` (`MergeModSettings`), `:932` (`WriteModSettings`), `:983-1000` (`LoadModSettings`)
- Test: `TestModSettingsThreeSourceMerge()` (next to `TestModSettingsSerialization` :5311 for style)

**Interfaces:**
- Produces: `public static List<Mod> MergeModSettings(IList<Mod> current, IList<Mod> previous, IList<Mod> saved)`; the 2-arg overload stays and forwards `saved: null`. `readonly List<Mod> savedMods` on `ModManager` = the whole list deserialized by `LoadModSettings` (replaced on every load).

- [ ] **Step 1: Failing test.** Build `current = {A on}`, `previous = {B off}`, `saved` = deserialize `[{"Title":"A","Enabled":false,"LoadPriority":1},{"Title":"B","Enabled":true,"LoadPriority":2},{"Title":"dream - paperdoll","Enabled":true,"LoadPriority":9}]` with `fsSerializer` (the :5346 pattern). `MergeModSettings(current, previous, saved)`: 3 entries; A enabled (current wins), B off (pruned wins over saved), `dream - paperdoll` kept with priority 9 (saved only = the locked pack's entry). `MergeModSettings(current, previous)` still returns 2. Rename case (spec): a saved entry titled `Distant Terrain` with `FileName` `distantterrain` merges by Title alone, so the `-ios` file keeps it - assert the merged entry's Enabled/LoadPriority come from `saved` when `current` has the same Title from a Mod whose FileName differs (build it with `new Mod()` + `ModInfo.ModTitle`). Source pins: `MethodBody(src, "public static bool LoadModSettings()")` contains `savedMods.Clear()` and `savedMods.AddRange(temp)`; `MethodBody(src, "public static bool WriteModSettings()")` contains `ModManager.Instance.savedMods)`.
- [ ] **Step 2: Run** -> fails. **Step 3: Implement.** Third loop in `MergeModSettings` identical to the second over `saved`. `LoadModSettings`: after `TryDeserialize` succeeds and before the `foreach`, `ModManager.Instance.savedMods.Clear(); ModManager.Instance.savedMods.AddRange(temp);`. `WriteModSettings`: pass `ModManager.Instance.savedMods` as the third argument. Comment: locked Non-MIT packs are not loaded, so without this one launch would erase their entries.
- [ ] **Step 4: Self-test** -> 0 failed. Commit `git add Assets/Game/Addons/ModSupport/ModManager.cs Assets/Editor/MobileSelfTest.cs` ; `Mods.json: entries of mods that are not loaded survive a write (locked packs keep their choices)`.

Done when: the three-source merge is pinned and the old two-source call sites still pass.

---

### Task 6: Locked rows in the MODS window

**Files:**
- Modify: `Assets/Game/Addons/ModSupport/ModLoaderInterfaceWindow.cs:157` (subscribe), `:356-362` (`GetModSettings`), `:365-392` (`GetLoadedMods`), `:402-407` (`UpdateModPanel`), `:580-609` (priority swaps), `:644-648` (`ExtractFiles`), `:750-762` (`ShowModDescription`), `:768` (`ModSettingsButton`), `:776-786` (checkbox toggle); `MobileOriginalMods.cs` (one pure rule)
- Test: `TestLockedModRows()`

**Interfaces:**
- Produces: `public static bool IsLockedRow(int index, int realCount)` (= `index >= realCount`), `public static bool TapOpensUrl(int selectedIndex, int previousSelection, int realCount)` (= locked row AND `selectedIndex == previousSelection`).

- [ ] **Step 1: Failing tests.** Pure: `IsLockedRow(0, 0)` true (Review Focus 5), `IsLockedRow(2, 3)` false; `TapOpensUrl(3, 3, 3)` true, `TapOpensUrl(3, 1, 3)` false (first tap selects only), `TapOpensUrl(1, 1, 3)` false. Source pins on `ModLoaderInterfaceWindow.cs`: `GetLoadedMods` body contains `MobileOriginalMods.Locked` and `disabledModTextColor` after the real-rows loop; each of `GetModSettings`, `UpdateModPanel`, `DecreaseLoadOrderButton_OnMouseClick`, `IncreaseLoadOrderButton_OnMouseClick`, `ExtractFilesButton_OnMouseClick`, `ShowModDescriptionPopUp_OnMouseClick`, `ModSettingsButton_OnMouseClick`, `ModEnabledCheckBox_OnToggleState` (grep the real name at :776) contains `MobileOriginalMods.IsLockedRow(`; a `ModList_OnSelectItem` method exists, contains `MobileOriginalMods.TapOpensUrl(` and `Application.OpenURL(`; the file contains `modList.OnSelectItem += ModList_OnSelectItem;`; `SaveAndCloseButton_OnMouseClick` and `EnableAllButton_OnMouseClick` loop bounds remain `modSettings.Length`.
- [ ] **Step 2: Run** -> fails. **Step 3: Implement.** `GetLoadedMods`: after the loop, `foreach (var l in MobileOriginalMods.Locked) { modList.AddItem(l.Label, out ListBox.ListItem it); it.textColor = disabledModTextColor; }`. Guards: `GetModSettings` returns false when `IsLockedRow(modList.SelectedIndex, modSettings.Length)` (this also fixes the existing `>` off-by-one); `UpdateModPanel` clears the six labels' values and returns on a locked row; priority swaps return when either index is locked; the four button handlers and the toggle return on a locked row. `ModList_OnSelectItem`: `int real = modSettings == null ? 0 : modSettings.Length; if (MobileOriginalMods.TapOpensUrl(modList.SelectedIndex, currentSelection, real)) { var l = MobileOriginalMods.Locked[modList.SelectedIndex - real]; if (!string.IsNullOrEmpty(l.Url)) Application.OpenURL(l.Url); }` (`currentSelection` is updated in `Update()` only after the change, so on the first tap it still holds the previous row).
- [ ] **Step 4: Self-test** -> 0 failed. Commit `git add Assets/Game/Addons/ModSupport/ModLoaderInterfaceWindow.cs Assets/Scripts/Game/Mobile/MobileOriginalMods.cs Assets/Editor/MobileSelfTest.cs` ; `MODS window: greyed "Needs <mod>" rows for locked packs; tap the selected row for the download page`.

Done when: the window appends locked rows, no index handler can reach `modSettings` through one, and the tap rule is pure and pinned.

---

### Task 7: Non-MIT pack build: `-ios` names, `pack-index.json`, unlock keys

**Files:**
- Modify: `tools/bundled-mods/pack.py:146-170` (`main`), new helpers; `tools/bundled-mods/mods.json` (`unlock_key` on the 7 `private_only` entries: DEX->`DEX`, RealGrass->`REALGRASS`, DynamicSkies->`DYNAMICSKIES`, WorldOfDaggerfall->`WOD`, WorldOfDaggerfallBiomes->`WODBIOMES`, WorldOfDaggerfallTerrain->`WODTERRAIN`, DistantTerrainWoD->`DISTANTTERRAIN`)
- Test: `tools/bundled-mods/test_pack.py`; C# `TestNonMitPackNames()` (cross-check)

**Interfaces:**
```python
def ios_name(stem):  # "dream - mobs (2 of 6)" -> "dream-mobs_2_of_6-ios.dfmod"; "distantterrain" -> "distantterrain-ios.dfmod"
def private_stems(cfg):          # private_only entries that are not builtin: {stem: unlock_key}
def dream_key(stem):             # "dream-sky" -> "DREAMSKY", other "dream-*" -> "DREAM"
def pack_index(entries):         # [(file, size, key)] -> JSON text, sorted by file
# CLI: pack.py --private --dream-dir DIR [--out ~/Desktop/NonMIT-ModPack-ios.zip]
```
`ios_name` rule: lower-case; `" & "` -> `" "`; `" - "` -> `"-"`; `" ("` -> `"_"`; `" of "` -> `"_of_"`; drop `")"`; remaining spaces -> `"-"`; strip a trailing `.dfmod`; append `-ios.dfmod`. The 25 DREAM names (from the draft folder, each + `-ios`): `dream-backgrounds`, `dream-commoners_1_of_2`, `_2_of_2`, `dream-handheld`, `dream-hud-menu`, `dream-mobs_1_of_6` .. `_6_of_6`, `dream-npcs_1_of_3` .. `_3_of_3`, `dream-paperdoll_1_of_8` .. `_8_of_8`, `dream-portraits`, `dream-sprites_1_of_2`, `_2_of_2`.

- [ ] **Step 1: Failing tests (python).** `ios_name("dream - hud & menu") == "dream-hud-menu-ios.dfmod"`, `ios_name("dream - mobs (2 of 6)") == "dream-mobs_2_of_6-ios.dfmod"`, `ios_name("DistantTerrain") == "distantterrain-ios.dfmod"`, idempotent on an already `-ios` name. `private_stems(CFG)` with a `private_only` entry carrying `unlock_key` returns it and raises `ValueError` for a `private_only` entry without one. `pack_index` output parses back and has `file/size/key`. End-to-end: temp `MODS_DIR` with `dex.dfmod` + a temp dream dir with `dream - sky.dfmod` and `dream - mobs (1 of 2).dfmod` -> `main(["--private", "--dream-dir", d, "--out", z])` writes a zip whose names are `Mods/dex-ios.dfmod`, `Mods/dream-sky-ios.dfmod`, `Mods/dream-mobs_1_of_2-ios.dfmod`, `pack-index.json` (keys DEX/DREAMSKY/DREAM, sizes = file sizes), `README.txt`; MIT mode output unchanged (existing tests). Run `python3 -m unittest tools/bundled-mods/test_pack.py` -> fails.
- [ ] **Step 2: Implement** in pack.py; `--private` skips `check_bundles`' MIT list and instead requires every `private_stems` bundle to exist in `MODS_DIR` (or the entry's `bundle` path). README.txt for the private zip: one line per file with its key and "needs the original <DisplayName> from <url> in Documents/Mods" (urls as a dict in pack.py, same as the C# rows).
- [ ] **Step 3: C# cross-check test** `TestNonMitPackNames`: read `tools/bundled-mods/mods.json` (`Application.dataPath + "/../tools/bundled-mods/mods.json"`), for every entry with `private_only` and `unlock_key`: a row with that Key exists in `MobileOriginalMods.Rows` and its `Unlocks` contains `<manifest stem lower>-ios.dfmod`. Self-test -> 0 failed.
- [ ] **Step 4: Commit** `git add tools/bundled-mods/pack.py tools/bundled-mods/test_pack.py tools/bundled-mods/mods.json Assets/Editor/MobileSelfTest.cs` ; `Non-MIT pack: -ios file names, pack-index.json and unlock keys`.

Verify: `python3 -m unittest tools/bundled-mods/test_pack.py tools/bundled-mods/test_fetch.py` all pass.
Done when: the private zip carries only `-ios` names + index and the C# table agrees with mods.json.

---

### Task 8: Real DREAM / DREAM SKY markers (DONE 2026-10-14, no download needed)

Unblocked by the planner: real DREAM 2026 desktop originals are one file per module named `dream - <module>.dfmod` inside a `Mods/` folder; the 13 modules are textures, paperdoll, mobs, npcs, cinematics, backgrounds, commoners, music, handheld, sprites, portraits, hud & menu, sound. DREAM SKY original = `dream - sky.dfmod` (from `DREAM - SKY-664-1-2-*.rar`).

**Files:** `MobileOriginalMods.cs` (the two rows), `MobileSelfTest.cs` (`TestOriginalModRecognizer`)

- [x] **Step 1:** Module names taken from the planner ruling above (no archive listing needed). A real Mac-build original is read-only at `~/dev/dfu-mods/dream-src/dream - textures.dfmod` (1.8 GB).
- [x] **Step 2:** `ArchiveMarkers`/`DfmodMarkers` = the 13 `dream - <module>` names, threshold 10; DREAM SKY markers `dream - sky`; PLACEHOLDER comments removed. Tests: 13 markers + threshold, 10-of-13 archive match / 9 no match, DREAM SKY archive match, and MatchDfmod on the real file yields DREAM and the file is not in KnownOurs (skipped with a logged note when the file is absent).
- [x] **Step 3:** Committed `Original-mod unlock: real DREAM 2026 module markers (13) and DREAM SKY`.

Note for Tasks 10/11: the sim/device unlock tests use the real `~/dev/dfu-mods/dream-src/dream - textures.dfmod` (copied into `Documents/Mods`; it is a desktop original, matched by name + UnityFS + size) instead of a Nexus archive; the fake-zip prerequisite in Task 10 is no longer needed.

---

### Task 9: Docs and the Non-MIT pack on the private draft (DONE 2026-10-14)

Done: docs 11f5eb41e; pack.py `--mods-dir` + KnownOurs staleness check 212a314f1; split into `NonMIT-ModPack-ios-part1.zip` (1,891,822,092 B) / `-part2.zip` (1,106,355,783 B) d3a310c70 because the 3.0 GB pack passes GitHub's 2 GiB asset limit; both uploaded to the draft, release notes carry the DREAM 2026 line. The 25 DREAM files + 7 private bundles ship (dream-sky/-sound/-textures are not in the draft folder).

**Files:** `README-iOS.md:634` (`### The mod pack`: new sub-section "Non-MIT Mod Pack and the original-mod unlock": three downloads, how to drop the original, receipts file path, "Needs <mod>" rows, Refresh behaviour, release-notes template with original URL + version); `UPSTREAM-PATCHES.md` (ModManager scan/gate/savedMods, ModLoaderInterfaceWindow locked rows); `THIRD-PARTY.md` (Non-MIT pack paragraph: every file gated on the player's own original).

- [ ] **Step 1:** Write the docs; `grep -n "NonMIT-ModPack-ios\|OriginalReceipts.json\|Needs " README-iOS.md` shows the new text.
- [ ] **Step 2:** Build the zip: `python3 tools/bundled-mods/pack.py --private --dream-dir "/Volumes/More data/AI projects/Daggerfall/dfu-mods/dream-ios-draft" --out "/Volumes/More data/AI projects/Daggerfall/NonMIT-ModPack-ios.zip"` (private bundles must already be in `Assets/StreamingAssets/Mods` from a prior `BuildBundledMods`; if absent, say so and skip the upload). `unzip -l` shows only `-ios` names + `pack-index.json` + `README.txt`.
- [ ] **Step 3:** Upload: `gh release upload testapp-unity6 "<zip>" --clobber -R Codex64ai/daggerfall-unity-ios`. Commit docs: `git add README-iOS.md UPSTREAM-PATCHES.md THIRD-PARTY.md` ; `Docs: Non-MIT pack and the original-mod unlock`.

---

### Task 10: Simulator verification (DONE 2026-10-14)

Done with the real `dream - textures.dfmod` + a zip fixture: evidence and ledger in `/Volumes/More data/AI projects/Daggerfall/dfu-mods/origunlock-evidence/` (LEDGER.txt). Driven by the new test-app command `modswindow [seconds]` (2171bdffa). Safari double-tap not testable in the sim.

Prereq: Task 8 done (a real DREAM archive) OR, if still blocked, a zip built from the placeholder names (`cd $(mktemp -d) && for n in "DREAM - HUD & MENU" ... ; do head -c 16 /dev/zero > "$n.dfmod"; done; zip DREAM-fake.zip *.dfmod`) - say which was used in the ledger.

- [ ] **Step 1:** Simulator build per README-iOS.md:105-120 (`DFU_IOS_SIM=1`, `DFU_BUNDLED_MODS=0`); install on the booted iPad simulator; container = `xcrun simctl get_app_container booted net.codex64.daggerfall.test.K8RF7RDFB5 data`.
- [ ] **Step 2:** Copy the 25 `dream-*-ios.dfmod` (from the Task 9 zip) + `pack-index.json` into `Documents/Mods`, WITHOUT the archive. Launch -> Player.log has 25 `[OriginalMods] locked` lines, MODS window shows one greyed `Needs DREAM - tap for the download page` row after the real rows (screenshot). Tap it twice -> Safari opens the URL (log line).
- [ ] **Step 3:** Add the DREAM archive; launch -> `[OriginalMods] receipt DREAM <- ... (N ms)` (record N; must be well under 1000 ms), DREAM mods listed and loaded; `Documents/Mods/GameData/OriginalReceipts.json` exists; `Mods.json` keeps the `dream - *` entries (compare before/after).
- [ ] **Step 4:** Delete the archive; relaunch -> still loads, no scan of the archive. Delete the receipt; relaunch -> locked again. Drop the archive while on the MODS window and press Refresh -> receipt written, rows appear (unlocked packs default on, as the spec states).
- [ ] **Step 5:** Corrupt archive (`head -c 100 /dev/urandom > Documents/Mods/bad.7z`) -> one `Couldn't read bad.7z` row, game boots. Record everything in the ledger; no commit unless a bug was fixed (then its own commit + self-test).

---

### Task 11: Device verification (last)

- [ ] **Step 1:** Push Ikram's DREAM archive to the iPad `Documents/Mods` FIRST (the iPad already holds the 25 files under their OLD names; the archive writes the DREAM receipt on first launch, so the old-name files keep loading until the `-ios` files are copied in). Then install the device ipa built per the README recipe (`DFU-Test-unity6-origunlock.ipa`, bundle id pinned), upload it `--clobber` to the draft.
- [ ] **Step 2:** Swipe-away relaunch; read Player.log (`[OriginalMods] receipt DREAM`, ms); MODS window shows DREAM rows normal; repeat the Task 10 Step 4 cycle once. Report ms and any stall at the title.
- [ ] **Step 3:** Memory file update (daggerfall-ios-port.md HANDOFF): ipa name, receipt state on the iPad, blocked rows if Task 8 is still open.
