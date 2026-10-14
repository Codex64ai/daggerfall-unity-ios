# Original-mod unlock: our iOS conversions run only next to the player's own copy of the original

Date: 2026-10-14. Design approved in conversation by Ikram (both parts: release layout and in-app unlock). Opus wrote this draft
from the approved design; Fable reviews and rules; Opus implements.

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
   (`Assets/Game/Addons/ModSupport/ModManager.cs:639`) as its first statement, so both start-up (`Awake` :169) and the MODS window's
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
   - **unlocks**: the list of our pack filenames this original unlocks (e.g. all 25 `dream-*.dfmod`).
   Rows are added only for mods actually in the Non-MIT pack. Fill the DREAM row from the real Nexus download (Ikram re-downloads;
   the old 7z was deleted 2026-09) and the DREAM SKY row from `~/Downloads/DREAM - SKY-664-1-2-*.rar` if still present.
3. **Receipts** - `Documents/Mods/GameData/OriginalReceipts.json`: `[{Key, MatchedFile, Size, MTime, Date}]`. Written once per Key.
   Lives next to `Mods.json`, so it survives deleting the original. Deleting the receipt simply re-locks until an original is seen
   again.
4. **Gate** - in `FindModsFromDirectory`, before `LoadModAssetBundle` (:737) for each file:
   - our pack file whose Key has no receipt -> not loaded; added as a locked entry instead (below);
   - a file the Finder recognized as a desktop ORIGINAL `.dfmod` -> always skipped (a desktop bundle cannot load on iOS; today it
     just logs a Unity error - skipping it is quieter and saves the load attempt);
   - everything else (MIT pack, built-ins, unknown mods) -> unchanged behaviour.
   "Our pack file" = listed in `pack-index.json` (shipped inside the Non-MIT pack; `{file, size, key}`) by file name + size. Size
   tells our iOS file apart from a desktop original that happens to share its file name.
5. **Locked entries in the MODS window** (`ModLoaderInterfaceWindow.cs:382`, where each mod is added to the list): one greyed row per
   locked Key, using the existing `disabledModTextColor`, not toggleable. Text: "Needs DREAM - tap for the download page" (tap opens
   the URL with `Application.OpenURL`; that is the only outward action and it is the player's tap). An archive that matched nothing
   or could not be read shows "Couldn't read <file>" once, greyed, so the player knows the drop was seen.
6. **Archive listing** - SharpCompress (MIT) added as a managed DLL under `Assets/Plugins/SharpCompress/`, used ONLY to list entry
   names of zip/7z/rar. Check its IL2CPP/iOS behaviour first (no `System.Reflection.Emit`; add a `link.xml` entry if stripping
   removes readers). No archive code exists in the repo today.

### File-name collisions (decision for Fable)
Some of our pack files have the same file name as the desktop original (`distantterrain.dfmod`, `dynamic-skies.dfmod`, ...). If the
player copies a desktop `.dfmod` in through Files, Files offers "Replace" and our iOS file is gone. The size check in the Gate keeps
the game correct (receipt written, pack shows "Reinstall <file> from the Non-MIT Mod Pack"), but the player has to fetch our file
again. Recommendation: (a) release notes and the locked-row text say "drop the downloaded archive as-is" - archives never collide;
(b) from the next release, Non-MIT pack files carry an `-ios` suffix (DREAM already uses distinct `dream-*` names). (b) changes
`FileName` in `Mods.json`, so it needs a one-time migration mapping old name -> new name to keep the player's enabled/priority
choices. Fable to rule on (b).

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
   - name+size+mtime cache: second scan does not open the archive.
2. **Simulator**: real DREAM archive (Ikram's fresh download) in Documents/Mods with our 25 `dream-*` files: first launch writes the
   receipt and DREAM loads; delete the archive, relaunch, still loads; delete the receipt, relaunch, 25 files locked and one greyed
   "Needs DREAM" row; time the first-launch archive listing (log ms) - must not add a noticeable stall.
3. **Device**: same as the simulator on the iPad (swipe-away relaunch before reading Player.log - a suspended app never re-scans).
   The iPad already holds our DREAM files; it needs the receipt or the original before this build goes on, or DREAM locks. Ship the
   device build with Ikram's DREAM archive pushed first.
