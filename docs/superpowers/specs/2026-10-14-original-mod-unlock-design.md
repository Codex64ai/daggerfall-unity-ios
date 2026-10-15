# Original-mod unlock: our iOS conversions run only next to the player's own copy of the original

Date: 2026-10-14. Design approved in conversation by Ikram (both parts: release layout and in-app unlock). Opus wrote this draft
from the approved design; Fable reviewed it 2026-10-14 (approve with changes - all folded in below); Opus implements.

## Why
Several mods we convert for iOS (DREAM, DEX, Real Grass art, Dynamic Skies data, the other `private_only` entries in
`tools/bundled-mods/mods.json`) have no licence or forbid redistribution. We still want players to have them. The answer: we ship
our iOS conversion, but it only switches on when the player has put the ORIGINAL mod, downloaded by them from its real page, on the
device. The original is the proof they got it from the author; our pack is the iOS-ready form of it.

## Part 1: releases
Each release has three downloads:
1. **The app** (ipa).
2. **MIT Mod Pack**: the openly licensed conversions (as today). No gate, ever.
3. **Non-MIT Mod Pack**: our iOS conversions of the restricted mods, plus `pack-index.json` (see below).

The release notes list every Non-MIT mod with: a link to its original download page, and the exact original version our pack was
built from ("built from DREAM 1.2, Nexus mods/5"). This is information only; the app accepts any version (below).

## Part 2: in the app
The player drops the original into `Documents/Mods` (Files app) in whichever form they have:
- the downloaded archive as-is: `.zip`, `.7z` or `.rar`, or
- the extracted desktop `.dfmod` file(s).

Rules (all approved):
- **Any version** of the original is accepted. Matching is by identity (name/structure), never by hash.
- **No version warning** in the app.
- **One-time check.** A match writes a receipt; after that the player may delete the original and the pack stays unlocked.
- **No server, no network.** Everything is local (the code base has no network code today; keep it that way).
- **Never blocks start-up.** Any failure (unreadable archive, exception) logs one line and leaves that pack locked; the game boots.

### Components
1. **Original Finder** - new `Assets/Scripts/Game/Mobile/MobileOriginalMods.cs`. Called from `ModManager.FindModsFromDirectory`
   (`Assets/Game/Addons/ModSupport/ModManager.cs:639`) as its first statement (scan + receipts only; the per-file Gate is separate, below), so both start-up (`Awake` :169) and the MODS window's
   Refresh button (`Refresh` :629) re-run it. Scans `Documents/Mods` (all subfolders) for `*.zip|*.7z|*.rar|*.dfmod`. Skips files
   already covered by a receipt with the same name+size+mtime (so a 3 GB archive is read once, not every launch). Only runs when
   `MobileContentPath.Active`.
2. **Recognizer table** - built-in, static C# table (a data file the player could edit would defeat the point). One row per Non-MIT
   mod:
   - `Key` (e.g. `DREAM`), display name, original download URL;
   - **archive markers**: entry names that must appear inside the archive (listing only, nothing extracted). Threshold per row,
     e.g. DREAM: at least 10 of its 13 module names (`DREAM - HUD & MENU`, `DREAM - PAPERDOLL`, ...). Matching is
     case-insensitive and ignores spaces, `-`, `_` and the `.dfmod` extension, so renamed versions still match;
   - **dfmod markers**: normalized file-name patterns + the file must start with the `UnityFS` header + a minimum size (stops an
     empty file renamed to `DREAM - SPRITES.dfmod`);
   - **unlocks**: the list of our pack filenames this original unlocks (e.g. all 25 `dream-*-ios.dfmod`). This list, not
     `pack-index.json`, decides what is "ours", so the Gate fails CLOSED if the index is missing;
   - **old names**: our pre-rename file names (old -> new), used by the both-files rule in the collisions section.
   Rows are added only for mods actually in the Non-MIT pack. Fill the DREAM row from the real Nexus download (Ikram re-downloads;
   the old 7z was deleted 2026-09) and the DREAM SKY row from `~/Downloads/DREAM - SKY-664-1-2-*.rar` if still present.
3. **Receipts** - `Documents/Mods/GameData/OriginalReceipts.json`: `[{Key, MatchedFile, Size, MTime, Date}]`. Written once per Key.
   Lives next to `Mods.json` (= `ModDataDirectory`), so it survives deleting the original. Written to a temp file + rename so a kill
   mid-write cannot leave half a file. Deleting the receipt simply re-locks until an original is seen
   again.
4. **Gate** - in `FindModsFromDirectory`'s per-file loop, after `modFileNames[i]` is set (:668) and before the `loadedModNames`
   check (:679), so before `LoadModAssetBundle` (:737):
   - our pack file whose Key has no receipt -> not loaded; added as a locked entry instead (below);
   - a file the Finder recognized as a desktop ORIGINAL `.dfmod` -> always skipped (a desktop bundle cannot load on iOS; today it
     just logs a Unity error - skipping it is quieter and saves the load attempt);
   - everything else (MIT pack, built-ins, unknown mods) -> unchanged behaviour.
   "Our pack file" = named in a recognizer row's `unlocks` list. `pack-index.json` (shipped in the Non-MIT pack, `{file, size, key}`,
   first hit anywhere under `Documents/Mods`) only adds a size check, to tell our file from a desktop original of the same name; if it
   is missing the name alone decides.
   Refresh behaviour (accepted, stated so nobody files it as a bug): a loaded pack whose receipt is deleted stays loaded until relaunch;
   a pack unlocked by Refresh loads with DFU's discovery defaults (enabled, no saved priority) because Refresh does not re-run
   `LoadModSettings`/`DefaultOff` - the same as any mod dropped in mid-session today. Relaunch restores saved choices.
4b. **Keep saved choices for locked packs** - today `WriteModSettings` (:926) writes only `mods` + `prunedMods`, and `LoadModSettings`
   (:983) throws away entries for titles that are not loaded, so one launch with DREAM locked would erase its 25 enabled/priority
   entries. Fix: `LoadModSettings` keeps the whole deserialized list (`savedMods`); `MergeModSettings` (:907) takes it as a third source,
   priority current > pruned > saved (matched by Title, as today). Self-test it.
5. **Locked entries in the MODS window** (`ModLoaderInterfaceWindow.cs:382`): locked Keys live in `MobileOriginalMods.Locked`, a static
   list of a small struct, never a `Mod`, so Mods.json never sees them. The window appends one greyed row per locked Key AFTER the real
   rows, in `disabledModTextColor`. The window is index-coupled (`modSettings[modList.SelectedIndex]` at :361/:407, priority swap
   :587-609, Enable/Disable All :702-725, SaveAndClose :631), so every index handler guards `i < modSettings.Length`, and locked rows
   cannot be enabled, re-prioritised or saved. Text: "Needs DREAM - tap for the download page"; the URL opens (`Application.OpenURL`)
   only on a tap of the row that is ALREADY selected, so scrolling past it never launches Safari. That is the only outward action. An archive that matched nothing
   or could not be read shows "Couldn't read <file>" once, greyed, so the player knows the drop was seen.
6. **Archive listing** - SharpCompress (MIT) added as a managed DLL under `Assets/Plugins/SharpCompress/`, used ONLY to list entry
   names of zip/7z/rar. Pin 0.32.2 (0.33+ pulls in a `ZstdSharp` DLL), netstandard2.0 build (no Reflection.Emit; fine on IL2CPP). Add
   `<assembly fullname="SharpCompress" preserve="all"/>` to `Assets/link.xml`. Use `ArchiveFactory.Open(FileStream)` and enumerate
   `Entries` only - header-only for zip/7z/rar, cheap even for a multi-GB solid 7z. Never `ReaderFactory` (forward-only, decompresses).
   Everything wrapped in try/catch: non-UTF8 zip names (`Encoding.GetEncoding`, missing I18N on IL2CPP) and encrypted 7z headers throw
   -> "Couldn't read". No archive code exists in the repo today.

### File-name collisions: rename ours (decided by Ikram 2026-10-14)
Some of our pack files have the same file name as the desktop original (`distantterrain.dfmod`, `dynamic-skies.dfmod`, ...). Copying a
desktop `.dfmod` in through Files would offer "Replace" and wipe our iOS file. Decision: **every Non-MIT pack file gets an `-ios`
suffix** (`distantterrain-ios.dfmod`; DREAM's `dream-*` names are already distinct but get the suffix too, for one rule). The pack
build (`tools/bundled-mods` + the DREAM converter) emits the new names; `pack-index.json` and the recognizer `unlocks` lists use them.
**No Mods.json migration needed** (Fable, verified in code): Mods.json is matched by mod **Title** (`LoadModSettings` :983,
`MergeModSettings` :907) and per-mod settings by GUID; `FileName` is written but never read back. Renaming a file keeps enabled,
load order and settings as-is. **Both files present** (old name left over next to the new one): the Gate skips the old name whenever
the new-name file exists (recognizer `old names` table, logged once), so the mod loads once and no AssetBundle is leaked by the
duplicate-title drop at :683. Self-tests: old-name Mods.json + new-name file -> choices kept; both files present, both scan orders ->
loads once.

### Test-app builds do not enforce the lock (ruling 2026-10-14, Ikram: "just fix it")
The first device build locked 8 mods on Ikram's real game (no originals on his iPad). In `DFU_IOS_TESTAPP` builds the Gate treats
every Key as unlocked: pack files load and no `Needs ...` rows appear. Desktop originals are still skipped and the scan/receipts run
and log as before, so the lock stays observable. Escape hatch: if `Documents/Mods/GameData/enforce-original-lock` exists, a test
build enforces the lock exactly like a release build. One scan line says which: `[OriginalMods] test build: lock not enforced` /
`... lock enforced (enforce-original-lock present)`. Release builds (no define) are unchanged and log no such line. The decision is
the pure `ApplyLockMode(verdict, isTestBuild, enforceFileExists)`; the define sets only `IsTestBuild` (source-pinned).

## Loose files follow the receipt (approved by Ikram 2026-10-14, "Let's do your recommendations")
DREAM's music, cinematics and extras are not `.dfmod` files, so the Gate above never saw them: they ship as
`DREAM-music-ios.zip` (81 `.ogg` -> `Documents/Sound`), `DREAM-movies-ios.zip` (17 `.mp4` + 16 Daedra `.FLC` ->
`Documents/Movies`) and `DREAM-extras-ios.zip` (4 spell icon atlases + metadata -> `Documents/SpellIcons`, 2 settings
presets -> `Documents/Presets/<mod>/`). Rule: in a build that enforces the lock, without a DREAM receipt those files are
treated as **absent** (vanilla music/movies/icons); with the receipt they work as before.

- **Table** - `KnownOursLoose` (`MobileOriginalModsKnownOursLoose.cs`, generated by `tools/known_ours_loose.py` from the three
  zips' directories; README notes skipped): `(leaf name lower-case, exact size, unlock key)`. A name may appear at several sizes
  (the two `DREAM 2026.json` presets). Identity is name AND size, never a hash, so the player's own files are never blocked:
  other names, other loose mods, or a DREAM name at another size (their own re-encode) are used.
- **Decision** - pure `LooseLocked(leaf, size, table, receiptKeys, isTestBuild, enforceFileExists)`: ours AND key without a
  receipt AND `ApplyLockMode(Locked, ...)` still Locked. So test-app builds use the files unless `GameData/enforce-original-lock`
  exists, exactly like the `.dfmod` gate.
- **IO wrapper** - `IsLooseLocked(path)`: only when `MobileContentPath.Active`. The table is built once; the hot path is a
  case-insensitive dictionary lookup on the leaf name (no stat, no allocation beyond `Path.GetFileName`); a name hit costs one
  `FileInfo`. Receipt keys and the enforce file are read once and re-read only after each `RunScanForModManager` (which may
  have written a receipt, e.g. on Refresh). Any exception -> not locked (the file is used, as before) + one log line. One log
  line per key, decision and file type: `[OriginalMods] loose DREAM files ignored (needs DREAM), first .ogg: <file>` /
  `... used, first .ogg: <file>`.
- **Lookup points** (every place a loose file is resolved):
  1. `MobileContentPath.Override` - a found user file that is locked returns the shipped path instead. Covers SoundReplacement
     (music `.ogg` incl. the iOS preload, `.wav`, MIDI bytes), the Better Ambience port's SoundList, VideoReplacement (`.mp4`),
     TextAssetReader.TryRead, BookReplacement, WorldDataReplacement, TextureReplacement, quests.
  2. `MobileContentPath.UserFiles` - locked files are left out of the listing. Covers SpellIconCollection (atlases),
     TextAssetReader.ReadAll (mod settings Presets), WorldData, quest lists.
  3. `FLCPlayer.Load` - `ResolvePath` uses `LooseFileExists` (locked -> the arena2 original FLC).
  4. Dynamic Music port - both playlist-folder listings (`Sound/DynMusic/...`) go through `WithoutLockedLoose`.
- **Not gated**: anything outside the table. Self-tests: `TestOriginalModLooseFiles` (locked / receipt / same name other size /
  test build with and without the enforce file / unrelated file / editor no-op / generated-table shape / source pins of all four
  lookup points and the scan reset).

## Out of scope
Checking the original is a "real" download (signatures, hashes) - identity is enough by design. Extracting anything from the
original. Any online check. Desktop/Android builds (Finder and Gate are `MobileContentPath.Active` only).

## Verification
1. **Self-tests** (`Assets/Editor/MobileSelfTest.cs`, fixtures generated in a temp folder by the test, not committed binaries where
   avoidable):
   - zip, 7z and rar fixtures whose entry names match the DREAM row -> receipt; one below the threshold -> no receipt;
   - name normalization (spaces/dashes/underscores/case/extension);
   - desktop `.dfmod` fixture: right name + `UnityFS` header + size -> match; same name with a fake header -> no match; empty file
     renamed -> no match;
   - Gate: locked pack file skipped and listed locked; receipt present -> loaded; desktop original always skipped; MIT and unknown
     files untouched; pack-index size mismatch -> treated as not-ours;
   - receipt survives the original being deleted; deleting the receipt re-locks;
   - corrupt archive -> "Couldn't read", no exception escapes, start-up continues;
   - name+size+mtime cache: second scan does not open the archive;
   - pack-index.json missing -> pack still gated by name (fails closed);
   - locked pack across a launch + SaveAndClose -> its Mods.json entries kept (4b);
   - MODS window with 0 real mods and 1 locked row (SelectedIndex 0 path); index handlers never touch a locked row;
   - 7z with encrypted header and zip with cp437 names -> "Couldn't read", no exception;
   - Refresh with a freshly dropped archive writes the receipt; ported titles still default off after relaunch.
2. **Simulator**: real DREAM archive (Ikram's fresh download) in Documents/Mods with our 25 `dream-*` files: first launch writes the
   receipt and DREAM loads; delete the archive, relaunch, still loads; delete the receipt, relaunch, 25 files locked and one greyed
   "Needs DREAM" row; time the first-launch archive listing (log ms) - must not add a noticeable stall.
3. **Device**: same as the simulator on the iPad (swipe-away relaunch before reading Player.log - a suspended app never re-scans).
   The iPad already holds our DREAM files; it needs the receipt or the original before this build goes on, or DREAM locks. Ship the
   device build with Ikram's DREAM archive pushed first.

## 2026-10-15: the 33 non-MIT mods join the gate

Approved by Ikram 2026-10-15 ("only true-MIT mods stay in the open MIT pack"). Plan:
`docs/superpowers/plans/2026-10-15-nonmit-gate-33.md`. Research (identity, links, download forms, markers):
`docs/superpowers/specs/2026-10-15-nonmit-originals-research.md`.

**The rule.** The MIT pack keeps only the 15 true-MIT mods (11 Cliffworms + Better Ambience, Immersive Footsteps,
Dynamic Music, First-Person Lighting). The other 33 (21 Jay_H quest packs, 6 UBLaMF modules, Vanilla Enhanced Base /
Masked Roads / Snowless Swamps and Jungles / Winter Tracks, Kokey's Temperate, Daggerfall Expanded Textures) move to the
Non-MIT pack and load only next to the player's own original. Builtins (Roleplay & Realism, RR Items, Climates & Calories)
are MIT and stay ungated. Every Non-MIT pack file gets the `-ios` name (`IosName`), not only the 10 that collide with a
desktop name; the old names become `OldNames` of their row (gated the same way, skipped when the `-ios` sibling exists).
No Mods.json migration (keyed by Title). Consequence: old-name files from the earlier MIT pack lock until the original is seen.

**23 new rows** (`PackRow`, research section B; 32 rows in total): `JH_QP1 JH_RLQ JH_ROLES JH_REPCON JH_CGK JH_TTC JH_BC
JH_MUNDANE JH_MEDEM JH_INVITE_DB JH_INVITE_TG JH_VAMPIRE JH_WEREBOAR JH_WEREWOLF JH_CHEAT JH_WEATHER JH_NOISES JH_MQREP
JH_IRONMAN JH_SDR UBLAMF VE DET`. `JH_IRONMAN` unlocks both Ironman files, `UBLAMF` the 6 modules, `VE` 5 files.
Rulings on research section D: extracted loose quest folders count as the original; any one Immersion Roles download
unlocks the 8-role file (threshold 1); the Nexus Vanilla Enhanced download also unlocks Winter Tracks and Kokey's
Temperate; Android `.dfmod` downloads count; DET 1.3's inner name is unconfirmed, so the row uses
`daggerfall expanded textures`. UBLAMF and VE archive markers deliberately exclude the names we ship (a second line of
defence behind the size rule below). DET cannot: its real archive's only entry is `daggerfall expanded textures.dfmod`,
our v0.2.0 name, so that name is a DET archive marker and the size rule alone tells our copy from the original.

**Archive self-exclusion is (name, size).** Before marker matching, archive entries whose leaf name and uncompressed
size match `KnownOurs` or `pack-index.json` are dropped, so our own v0.2.0 MIT zip (which holds the 10 desktop-named
files) can never unlock anything. An entry with the same name at another size is kept: a real desktop
`daggerfall expanded textures.dfmod` unlocks DET.

**Loose quest folders.** The scan also collects every non-empty `*.txt` under `Documents/Mods` (recursive) and
`Documents/QuestPacks` (recursive) and matches their leaf names against each receipt-less row's `ArchiveMarkers`
(`MatchLooseQuests`). Names only, nothing opened, never blocks start-up. Empty files, our `README.txt` /
`README-DREAM-extras.txt` and `*-LICENSE.txt` never match (pinned by test). `Documents/QuestPacks` is included because
DFU's own instructions extract quest packs there (open for Ikram; trivial to drop).

**KnownOurs sources.** `tools/known_ours.py` reads the `PackRow(...)` stems, emits the `-ios` alias of every file it
finds (same size), and reads the frozen `release/v0.2.0/mitpack-dl/Mods/` (the v0.2.0 MIT zip's exact bytes).
`Assets/StreamingAssets/Mods` is not a source (its contents change during builtin app builds).

**DfmodMinBytes floors.** Jay_H rows 1 KB; UBLAMF 64 KB; VE 64 KB (masked roads is about 300 KB on desktop); DET 1 MB.

**Private pack README.** "Not under an open licence: some are redistributed with the author's permission, some are
non-commercial (CC BY-NC-SA), some are pending the author's answer."

**Verification.** Editor self-test pins the table (32 rows, distinct keys, no shipped name normalizes to a marker, the
10 colliding names are `OldNames`/`DfmodMarkers`, archive markers only for DET), the (name, size) filter with the real `Rows` and `KnownOurs`, and
the loose scan. Simulator (plan Task 7, scenarios S1-S7, lock enforced) checks: locked rows for the moved mods; our v0.2.0 MIT
zip does not unlock; a Jay_H zip, loose quest folders in `Documents/Mods/QuestPacks` and `Documents/QuestPacks`, and
UBLaMF / VE / DET fixtures with the real inner names do; an old-name file is gated. Results and log lines go in
`dfu-mods/origunlock-evidence/gate33/LEDGER.txt`. Run 2026-10-15 on the iPad Pro 11 (M5) simulator: S1-S6 passed
(6 "Needs ..." rows; the v0.2.0 MIT zip wrote no receipt; `receipt JH_RLQ <- Random Little Quests-204-1-15a.zip`;
`receipt JH_WEATHER` / `JH_NOISES ... (loose quest files)`; UBLAMF / VE / DET receipts and all 9 files loading; the DET
desktop `.dfmod` path; the old name `jh-questpack1.dfmod` locked, then skipped beside its `-ios` file). S7: no
TypeLoad / MissingMethod; the in-world runs with the unlocked texture mods log a terrain `Texture2DArray.SetPixels32`
ArgumentException (a texture-mod content issue, not the gate; open). Real UBLaMF / VE / DET downloads and the device
are not yet verified.
