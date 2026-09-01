# Bundled MIT mods (v0.2.0 phase 1) — design

Date: 2026-09-01. Status: approved in discussion, awaiting spec review.

## Goal

Ship a curated set of MIT-licensed, data-only Daggerfall Unity mods **inside the iOS app**, so a
player gets them with no files to install, can switch each one off in the launcher's MODS window
or Pause > Mobile Settings > Mods, and every author is credited with their licence text intact.

Phase 1 is thirteen Cliffworms mods. Phase 3 (a separate spec) adds mods whose value needs code
compiled in (Atlas of the Iliac Bay's 56-line loader, Flat Replacer, Lively Cities ...).

## Why `.dfmod` bundles and not loose files

Both work. The bundle route wins because the engine already gives it everything the loose route
would need us to write:

- Every one of the thirteen repos ships (or can trivially be given) a `.dfmod.json` manifest, and
  `MobileModBuilder` already turns such a manifest into an iOS AssetBundle — the DREAM pipeline.
- Per-mod on/off, title, author, version, description, dependency checks and persistence in
  `Mods.json` all come from the existing mod system. New mods start enabled.
- Memory is not the DREAM problem: nothing calls `LoadAllAssetsFromBundle`; text assets load on
  demand through `Mod.loadedAssets` and are swept. 55 MB of JSON costs almost nothing resident.

Known costs, accepted: bundles are tied to the Unity version (rebuild on upgrade — one command);
bundles are opaque (diff the source repos instead); one engine change is needed (below).

## The thirteen mods

All MIT with a LICENSE file (`Copyright (c) 2025 Cliffworms`). Pinned to the commit inspected on
2026-09-01. Sizes are the payload on disk.

| Mod | Repo (github.com/Cliffworms/…) | Pinned commit | Payload | Manifest |
|---|---|---|---|---|
| Fixed Dungeon Exteriors | FixedDungeonExteriors | f384bb3f | 1,783 WorldData JSON, 24 MB | upstream |
| Varied Wealthy Homes | VariedWealthyHomes | 085a9f2a | 730 WorldData JSON, 12 MB | upstream |
| Main Quest Consequences | MainQuestConsequences | 1cca6534 | 64 WorldData JSON + 1 quest + QuestList, 11 MB | **ours** (see below) |
| Detailed Dungeon Exteriors | DetailedDungeonExteriors | 6d886ad9 | 10 WorldData JSON, 2.5 MB | **ours** (none upstream) |
| Detailed Main Quest Dungeons | DetailedMainQuestDungeons | 045022ef | 4 WorldData JSON + 1 texture, 1 MB | upstream |
| Aquatic Sprites | AquaticSprites | ea195e77 | 3 WorldData JSON, 0.7 MB | upstream (`UnderwaterSprites.dfmod.json`) |
| Smaller MQ Dungeons | SmallerMQDungeons | 51dc8db3 | 6 WorldData JSON, 0.1 MB | upstream |
| Leveling Inspiration | LevelingInspiration | 37aefbbe | 32 quests + QuestList | upstream |
| Skyrim's Adventures | SkyrimsAdventures | e5083f29 | 18 quests + QuestList | upstream |
| Jobs of the Thieves Guild | JOTG | 701440f3 | 13 quests + QuestList | upstream |
| Arena's Adventures | ArenasAdventures | 9352a928 | 10 quests + QuestList | upstream |
| Town Greetings of the Iliac Bay | TownGreetingsIliacBay | 203f9d2a | 5 quests + QuestList | upstream |
| Rumors of the Iliac Bay | RumorsOfTheIliacBay | b5641cd1 | 2 quests + QuestList | upstream |

Deliberately excluded from phase 1 and why: Atlas of the Iliac Bay (needs its loader — phase 3);
Famous Faces, Desert Architecture, Detailed City Walls, Finding My Religion, Lively Cities, Betony
Restored (declare Daggerfall Expanded Textures / RMB Resource Pack as REQUIRED, or lose real
function without their C#); everything by Jay_H, BadLuckBurt, drcarademono (no licence — ask
first, §4 of MOD-MASTER-LIST.md); theJF Quest Pack (non-commercial term); Arena-Style Flavor Text
(no licence declared).

The seven QuestList names (ARENA, JOTG, LVLUP, MQCControl, RIB, SKYRIM, TGIB) are unique, so the
engine's silent-drop on duplicate list names is not triggered.

### Manifest corrections we own

Kept in `tools/bundled-mods/manifests/`, copied over the repo's own manifest at fetch time.

- **MainQuestConsequences.dfmod.json** — upstream lists the 64 WorldData files but not
  `QuestPacks/Cliff/MQC/MQCControl.txt` and `QuestList-MQCControl.txt`, and has no `Contributes`.
  Without the control quest the "cleared" variants are never triggered. Ours adds both files and
  `Contributes: { QuestLists: ["MQCControl"], LooseQuestsList: ["MQCControl"] }`.
- **DetailedDungeonExteriors.dfmod.json** — upstream has no manifest (the repo is loose
  WorldData plus `ObjectGroups/` authoring fragments, which are **not** shipped). Ours lists the
  10 `WorldData/*.RMB.json`, credits Cliffworms, and declares a non-optional dependency on Fixed
  Dungeon Exteriors, whose block names it assumes.

Every manifest gets `DFUnity_Version` left as upstream wrote it (1.0.0 / 1.1.1 — both at or below
our 1.1.1) and its GUID untouched, so a player who later installs the author's own desktop-to-iOS
conversion of the same mod is recognised as the same mod.

## Architecture

```
tools/bundled-mods/
  mods.json                     the 13 entries: repo, commit, manifest name, licence path
  manifests/                    our two corrected manifests
  fetch.py                      clone at pinned commit -> Assets/Game/Mods/<Name>/ (+ LICENSE)
  test_fetch.py                 unit tests for the selection/validation logic

Assets/Game/Mods/<Name>/        gitignored already (Assets/Game/Mods/.gitignore ignores *)
  <Name>.dfmod.json             upstream or ours
  WorldData/, QuestPacks/, Textures/
  LICENSE

Assets/StreamingAssets/Mods/    gitignored already; build product
  <Name>.dfmod                  iOS AssetBundle from MobileModBuilder
  Licenses/<Name>-LICENSE.txt   the MIT text, shipped inside the app

Assets/Game/Addons/ModSupport/ModManager.cs
  scans BOTH Documents/Mods (player) and StreamingAssets/Mods (shipped) on iOS
```

### fetch.py

`python3 tools/bundled-mods/fetch.py [--only NAME] [--check]`

1. For each entry in `mods.json`: shallow-clone the repo at the pinned commit into a temp dir.
2. Copy the manifest (ours if listed, else the repo's) and only the payload folders the manifest
   references — never `ObjectGroups/`, `Scripts/`, `.meta` files from the repo, or the README.
   Destination is exactly `Assets/Game/Mods/<Name>/` because the manifest's `Files` entries are
   absolute asset paths of that shape.
3. Copy `LICENSE` beside the manifest.
4. Validate, and fail loudly on: a manifest `Files` entry that does not exist after the copy; a
   `.cs` / `.dll.bytes` in `Files`; a `QuestList-*.txt` in `Files` whose name is missing from
   `Contributes.QuestLists`; a quest `.txt` in `Files` missing from `LooseQuestsList`; a missing
   LICENSE; a LICENSE whose first line is not "MIT License"; a duplicate QuestList name across
   the whole set; an uppercase image extension (iOS is case-sensitive — the file and its `Files`
   entry are both lowercased, see Detailed Main Quest Dungeons' `1210_2-0.PNG`).
5. `--check` runs step 4 against what is already on disk without cloning.

### Build step

`MobileBuildSetup.ApplyAll` gains a call that builds every manifest under `Assets/Game/Mods/`
except `IOSPilot/` for the iOS target into `Assets/StreamingAssets/Mods/` (flat, not the builder's
per-target subfolder), and copies each mod's LICENSE to `Licenses/<Name>-LICENSE.txt`. It is also
exposed as its own `-executeMethod` (`MobileBuildSetup.BuildBundledMods`) for iteration. If
`Assets/Game/Mods/` holds no fetched mods the step logs a warning and continues, so a clone that
never ran `fetch.py` still builds an app — just without the bundle. The RELEASE.md recipe gains
"run fetch.py" before ApplyAll.

`IOSPilot` is excluded on purpose: it is a build-path test fixture with placeholder art, and it
already loads as a virtual mod in the editor.

### Engine change: two mod directories

Today on iOS `ModManager.ModDirectory` is a straight redirect to `Documents/Mods`, so the shipped
folder is never read. Change `FindModsFromDirectory` to enumerate a list of roots:

1. `Documents/Mods` (player) first,
2. `StreamingAssets/Mods` (shipped) second,

and feed the merged file list through the existing loop. The existing `GetModIndex(mod.Title) < 0`
guard already skips a second mod with the same title, so **the player's copy wins** purely by
ordering. `ModDirectory` itself stays `Documents/Mods`, because it is also where `Mods.json` and
per-mod settings are written and the shipped folder is read-only. The refresh path (`refresh:
true`, used by the MODS window) compares against the same merged list. Off iOS the list has one
entry and behaviour is unchanged.

The merge and precedence rule is factored into a pure static helper
(`ModManager.MergeModFiles(string[] playerFiles, string[] shippedFiles)` returning the ordered
list with shipped duplicates-by-file-name removed) so it is testable on the Mac.

### Attribution

- `Licenses/<Name>-LICENSE.txt` ships inside the app for each bundle (MIT's "include the notice
  in all copies").
- `THIRD-PARTY.md` gains a "Bundled mods" section: one row per mod with author, licence, repo,
  pinned commit, and whether the manifest is upstream's or ours.
- `README-iOS.md` gains a "Bundled mods" section: what ships, that each can be switched off, and
  that a player's own copy of the same mod takes precedence.
- Each release's notes list the bundled mods and authors.

## Testing

Self-test (`MobileSelfTest`, runs in the editor on the Mac):

- `MergeModFiles`: player-only, shipped-only, both disjoint, same file name in both → player's
  path kept and shipped dropped, order = player then shipped.
- If `Assets/StreamingAssets/Mods/*.dfmod` exist: every one has a matching
  `Licenses/<Name>-LICENSE.txt`; the count equals the number of manifests under
  `Assets/Game/Mods/` minus IOSPilot. Skipped with a note when no bundles are present.
- Every fetched manifest under `Assets/Game/Mods/` (excluding IOSPilot) parses, names no `.cs`,
  and every `Files` entry exists — the same rule as fetch.py's check, enforced from the editor
  side so a stale fetch cannot ship.

Python (`tools/bundled-mods/test_fetch.py`): the validation rules above against small fixtures,
including the QuestList/Contributes cross-check, duplicate list detection, and the uppercase
extension rule.

On-device (Ikram): after a test build, the MODS window lists 16 entries (13 + Roads & tracks,
Real travel, Summer start); switching Skyrim's Adventures off and on persists across a restart;
a dungeon exterior visibly differs from vanilla; Player.log shows no "QuestList already
registered" and no manifest errors.

## Out of scope

Anything needing code compiled in (phase 3). Nexus-only or unlicensed content. An in-game
"about this mod / licence" viewer — the MODS window already shows author and description.
Changing DFU's mod-settings UI.
