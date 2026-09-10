# Third-party notices

This repository is Daggerfall Unity (MIT, copyright (c) 2009-2023 Daggerfall Workshop - see
`LICENSE`) plus an iOS touch port (MIT, same terms). Several further works are compiled into the port
rather than loaded as mods - see each section below for its licence status, which is not MIT in every
case and is undeclared for three of them - and for half of a fourth, whose base is MIT and whose
modifications are not. Their original headers are preserved in the files named.

## Basic Roads

Copyright (c) 2020 Hazelnut. MIT License. https://github.com/ajrb/dfunity-mods
Contributors (per the original header): Hazelnut, and others credited there.

Used for: road, track, river and stream terrain texturing and the authored path data
(`Assets/Scripts/Game/Mobile/BasicRoadsTexturing.cs`, `Assets/Resources/BasicRoads/*.bytes`).
Modifications are listed in the file header. The author confirmed to the port's maintainer that
this use is welcome.

## Tedious Travel

Copyright (c) 2018 TheNewBob (Jedidia). MIT License. https://github.com/Jedidia/TediousTravel

Used for: the design and portions of the implementation of real travel
(`Assets/Scripts/Game/Mobile/MobileJourneyController.cs`, `MobileJourneyPilot.cs`,
`MobileJourneyWindow.cs`), reworked for touch and for this port's road routing.

## Daggerfall

The Elder Scrolls II: Daggerfall is copyright Bethesda Softworks. No game data is distributed
with this repository; players supply their own `arena2` folder.

## Bundled mods

Forty-four Daggerfall Unity mods are built into iOS `.dfmod` bundles and published as a mod pack
alongside each release (and may ship inside the app), each switchable in the launcher's MODS window.
Eleven are by **Cliffworms** (MIT); twenty-one are by **Jay_H** (redistributed with his permission);
five are Vanilla Enhanced modules by **drcarademono and Kokey** (permission pending); six are UBLaMF
modules by **XJDHDR** (CC BY-NC-SA 4.0); one is **Daggerfall Expanded Textures** by Ninelan
(converted from the macOS release with the author's permission). All are MIT licensed (`Copyright (c) 2025
Cliffworms`); the licence text ships in the app at `StreamingAssets/Mods/Licenses/`. They are
fetched at the pinned commits by `tools/bundled-mods/fetch.py` and are not part of this
repository's history.

| Mod | Repository | Commit | Manifest |
|---|---|---|---|
| Fixed Dungeon Exteriors | https://github.com/Cliffworms/FixedDungeonExteriors | f384bb3f | upstream |
| Varied Wealthy Homes | https://github.com/Cliffworms/VariedWealthyHomes | 085a9f2a | upstream |
|||| Aquatic Sprites | https://github.com/Cliffworms/AquaticSprites | ea195e77 | upstream |
| Smaller Main Quest Dungeons | https://github.com/Cliffworms/SmallerMQDungeons | 51dc8db3 | upstream |
| Leveling Inspiration | https://github.com/Cliffworms/LevelingInspiration | 37aefbbe | upstream |
| Skyrim's Adventures | https://github.com/Cliffworms/SkyrimsAdventures | e5083f29 | upstream |
| Jobs of the Thieves Guild | https://github.com/Cliffworms/JOTG | 701440f3 | upstream |
| Arena's Adventures | https://github.com/Cliffworms/ArenasAdventures | 9352a928 | upstream |
| Town Greetings of the Iliac Bay | https://github.com/Cliffworms/TownGreetingsIliacBay | 203f9d2a | upstream |
| Rumors of the Iliac Bay | https://github.com/Cliffworms/RumorsOfTheIliacBay | b5641cd1 | upstream |

Every manifest is the author's own. The data in every bundle is Cliffworms' work, unmodified.


### Jay_H quest packs

Jay_H (JayH2971) publishes his quest packs as loose quest files with no licence file. He granted
Ikram Massabini permission to redistribute them with the iOS port in September 2026; each bundle
carries a `Permission` record in place of a LICENSE. The packs ship no manifest upstream, so
`tools/bundled-mods/fetch.py` generates one per pack from its files: every `QuestList-*.txt`
becomes a `Contributes.QuestLists` entry and every quest script a `LooseQuestsList` entry. The two
Ironman Madness variants both ship `QuestList-IronmanMadness`; the engine silently drops a second
list of the same name, so theirs are renamed `IronmanMadnessInfighting` / `IronmanMadnessNoInfighting`.

Known limitation: Quest Pack 1 (16 quests), Random Little Quests (6) and Reputation Consequences
(1) use the `reduce player health` quest action, which vanilla Daggerfall Unity does not have.
Those quests load and do nothing until the port provides that action.

| Mod | Repository | Commit | Manifest |
|---|---|---|---|
| Quest Pack 1 | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `QP1/` |
| Random Little Quests | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Random Little Quests/` |
| Immersion Roles | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Immersion Roles/` |
| Reputation Consequences | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `ReputationConsequences/` |
| Chronicle of the Great Knight | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `chronicle-great-knight/` |
| The Tale Continues | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `thetalecontinues/` |
| Battle Creatures | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Battle Creatures/` |
| Mundane Jobs | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Mundane Jobs/` |
| Medical Emergency | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `medicalemergency/` |
| Become a Dark Brotherhood Member | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `becomedarkb/` |
| Become a Thief | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `becomethieves/` |
| Become a Vampire | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `becomevampire/` |
| Become a Wereboar | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `becomewereboar/` |
| Become a Werewolf | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `becomewerewolf/` |
| Cheat Armory | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `cheatarmory/` |
| Weather Items | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Weather-Items/` |
| Random Monster Noises | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `Random Monster Noises/` |
| Main Quest Reputation Fix | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `MQrepfix/` |
| Ironman Madness (infighting) | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `JH Ironman Madness INFIGHTING/` |
| Ironman Madness (no infighting) | https://github.com/JayH2971/dfunity-questpacks | 0dd7c6fb | generated from `JH Ironman Madness NO INFIGHTING/` |
| Starting Dungeon Randomizer | https://github.com/JayH2971/dfu-starting-dungeon-randomizer | 72276305 | generated from `repo root/` |


### Vanilla Enhanced (drcarademono, Kokey)

Five modules built from https://github.com/drcarademono/vanilla-enhanced at `c0c9041c` using the
authors' own manifests: Base (1,246 textures at 256 px plus materials), Masked Roads, Snowless
Swamps and Jungles, Winter Tracks, and Kokey's Temperate. **No licence is declared and the authors
have not yet been asked**; included at the port owner's request with a "permission pending" record
in each bundle. Remove on request.

### UBLaMF (XJDHDR)

Six of the seven modules of *Unofficial Block, Location and Model Fixes*, built from
https://github.com/XJDHDR/DFU_UBLaMF at `21ed9c87` with the author's manifests: Building
Overrides, Dungeon Blocks, Locations, Map Blocks, Models (105 prefabs with their OBJ meshes and
materials) and Textures. The Scripting module (a GitHub update checker) is not included - iOS cannot
run mod code. Licence: **CC BY-NC-SA 4.0** (`License.md`), which permits redistribution with
attribution for a free, non-commercial port; ShareAlike applies to derivatives.

### Daggerfall Expanded Textures (Ninelan)

The Standard edition, converted for iOS from the author's macOS `.dfmod` (Nexus 307) with the
port's converter, with the author's permission granted to Ikram Massabini in September 2026. Its
prefabs are not converted (the converter handles textures, audio, text and materials); the texture
archives are, which is what the Cliffworms dungeon mods need. Detailed Dungeon Exteriors returns to
the pack on the strength of it; Detailed Main Quest Dungeons and Main Quest Consequences still wait on
Decor & Miscellanea.

Not included yet: **Detailed Main Quest Dungeons** and **Main Quest Consequences** reference texture archives and models from Daggerfall Expanded Textures and
Decor & Miscellanea; DET's author has since given permission, so these return once DET itself is
converted and bundled; without them a block's flats throw during
layout and the whole dungeon fails to build (verified 2026-09-01). `tools/bundled-mods/fetch.py`
now rejects any block that references a non-vanilla texture archive or a required dependency the
pack does not ship.

## Survival mods (compiled in)

Three desktop mods ship inside the app: their C# is compiled in under `Assets/Scripts/Game/Mobile/Ports/`
(iOS cannot load mod code from a `.dfmod`), their data is a bundle in `StreamingAssets/Mods`, and each is
an ordinary, off-by-default entry in the launcher's MODS window. Copied unchanged except lines marked
`MOBILE` (the `[Invoke]` loaders removed, the tavern references redirected).

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| RoleplayRealism 1.8 | Hazelnut, MIT (file headers) | github.com/ajrb/dfunity-mods @ 0af2ec99 (RoleplayRealism/) | - |
| RoleplayRealism-Items 1.3 | Hazelnut & Ralzar, MIT (file headers) | github.com/ajrb/dfunity-mods @ 0af2ec99 (RoleplayRealismItems/) | - |
| Climates & Calories 1.7.0 | Ralzar, MIT (file headers) | github.com/Ralzar81/Climates-Calories @ c33a04f8 | `TavernWindow.cs` (no licence header) - replaced by the port's own `MobileTavernWindow`; `blb_tent.fbx`, `41606.prefab`, the three `.mat` files and the two tavern PNGs (author unstated) - the engine's own tent model is used |

Climates & Calories talks to Hazelnut's Travel Options; here a built-in entry titled `TravelOptions`
answers those messages from this port's Real travel (`MobileTravelOptionsBridge`). No Travel Options code
is included.

## Dynamic Skies (compiled in, private draft only)

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| Dynamic Skies 2.3.4 | BadLuckBurt & carademono, NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/dynamic-skies @ 04506e2 | `SunShafts.cs`, `PostEffectsBase.cs`, `SunShaftsComposite.shader`, `SimpleClear.shader`, `BLBProceduralSkyboxOriginal.shader` (Unity Standard Assets terms / dead code); the skybox shader is compiled into the app with its keyword set pinned |

Because no licence has ever been declared upstream, this mod ships on the private test draft only -
never in a public release, and `private_only` in `tools/bundled-mods/mods.json` keeps it out of the
public MIT mod pack build. DREAM SKY 1.2 (King of Worms, Nexus mods/664) is a preset for it - textures
and weather JSON, no code - converted to `dream-sky.dfmod` for the same private draft.

## World of Daggerfall (compiled in, private draft only)

Two mods, one feature. Location Loader is pure code - it has no content of its own and does nothing
alone - and World of Daggerfall is the location mod it reads. Both follow the Dynamic Skies pattern:
the C# is compiled in under `Assets/Scripts/Game/Mobile/Ports/` (iOS cannot load mod code from a
`.dfmod`), and each is an ordinary, off-by-default entry in the launcher's MODS window. WoD's data is
a bundle the player installs; Location Loader has no data at all, so its entry is registered in code.
Copied unchanged except lines marked `MOBILE` (the `[Invoke]` loaders removed); every ported file
carries a header naming its source repo and commit, and `WODRocksMaterials.cs.meta` keeps upstream's
own script GUID so WoD's rock prefabs still bind it. Location Loader has one exception to "unchanged":
three of its files also carry object type 5, backported from carademono's `rmb-object` fork because
WoD 0.4.0 was authored against it and upstream has no such commit (see UPSTREAM-PATCHES.md). Those
three files name both commits in their header; the backport is the type-5 core only, nothing else off
that branch.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| Location Loader 0.3 plus the type-5 backport | KABoissonneault, a fork of Uncanny_Valley's loader; NO LICENCE DECLARED (permission being sought by Ikram; not in any public release). The type-5 backport below is carademono's, from a fork which declares no licence either | github.com/KABoissonneault/DFU-LocationLoader @ a5e7a18, plus object type 5 (RMB blocks) backported from github.com/drcarademono/DFU-LocationLoader @ 896a574 (branch `rmb-object`) | the 3 editor scripts (`Scripts/Editor/`) - authoring tools, useless on a device. The 11 runtime files are compiled in under `Ports/LocationLoader/` (12 since WoD Biomes added `BiomesClimateSwap.cs`, off the same fork - see that section); upstream's manifest carries no data, so there is no bundle at all - the launcher entry is registered in code |
| World of Daggerfall 0.4.0 | World of Daggerfall Team (KABoissonneault, Cliffworms, Kamer, carademono); NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/world-of-daggerfall @ 3bf8837 | WoD Terrain and Distant Terrain (separate mods, each shipped in its own right since 2026-09-09 - see their sections below), and the three optional dependencies Wilderness Overhaul, RMB Resource Pack and Beautiful Villages. WoD Biomes is a separate mod too, and is now shipped - see its own section below. Its one script, `WODRocksMaterials.cs`, is compiled in under `Ports/WorldOfDaggerfall/`; its data is the bundle |

Because no licence has ever been declared upstream for either repo, this ships on the private test
draft only - never in a public release. Location Loader adds only code, so it is in every build: a
built-in launcher entry titled `Location Loader` (upstream's own GUID `fc5c0fa6-...`), off by default,
inert until switched on. World of Daggerfall's data is the downloadable bundle
`worldofdaggerfall.dfmod`, marked `private_only` in `tools/bundled-mods/mods.json` so `pack.py` keeps
it out of the public MIT mod pack; without that file there is no `World of Daggerfall` entry and the
compiled code never runs, and deleting it removes every byte the feature added. That entry is also off
by default and is switched off automatically whenever Location Loader is off - a location mod is nothing
without the loader reading it. The switch is simply off the next time you open MODS; `Player.log` records
why (`[PortedMods] World of Daggerfall switched off: Location Loader must be on`).

WoD requires Daggerfall Expanded Textures (Ninelan; already in the pack, see above). If that mod is
off or missing, World of Daggerfall is switched off at start-up: again the switch is simply off the next
time you open MODS, and `Player.log` records why
(`[PortedMods] World of Daggerfall off: Daggerfall Expanded Textures is not enabled`). Nothing else is
needed. Performance is the acknowledged unknown: WoD places about 329,000
instances across the world, and the cost on older iPads is unmeasured - the off-by-default switch is
the mitigation until it is, and `TUNE > Advanced > Show diagnostics` shows the frame time. Neither mod
was device-verified at the time of writing.

`tools/bundled-mods/fetch.py` gained an `extra_dirs` flag for this mod: WoD's manifest lists the
prefabs but not the `Meshes/` folder they point at by GUID, so the flag copies those unlisted asset
folders wholesale (files and their `.meta`) into the mod folder, leaving the manifest untouched, and
Unity resolves the references when it builds the bundle. Without it every WoD rock ships as a prefab
with no model.

## World of Daggerfall - Biomes (compiled in, private draft only)

A third mod from the same team, and independent of the two above: it re-skins terrain and swaps
nature billboards by itself, so it needs neither Location Loader nor World of Daggerfall - only
Daggerfall Expanded Textures. Same pattern again: the three C# files are compiled in under
`Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/`, and the data is an ordinary,
off-by-default entry in the launcher's MODS window fed by a bundle the player installs. No prefab
binds these scripts, so their `.meta` files carry fresh GUIDs rather than upstream's.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| World of Daggerfall - Biomes 0.4.0 | carademono and Kab the Bird Ranger (World of Daggerfall Team); NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/wod-biomes @ 40449fc5fc55c85c8089b068acefe1db4b61534c | the repo's `.7z` and `.xcf` authoring files (`exclude_globs`), and nothing else - the manifest names every asset, and there are no prefabs, meshes, materials or shaders, so no `extra_dirs` flag was needed. The three scripts (`WODBiomes.cs`, `WODClimates.cs`, `WODTerrainMaterialProvider.cs`, 712 lines as ported) are compiled in under `Ports/WorldOfDaggerfallBiomes/`; the 225 PNGs are the bundle |

What it does, in two parts. `WODTerrainMaterialProvider` takes DFU's `ITerrainMaterialProvider` slot
and re-routes four climates to different ground archives - Subtropical to 4, the Dak'fron desert to 3,
the Hammerfell mountain regions (Alik'r Desert, Dragontail Mountains, Dak'fron, Lainlyn, Tigonus,
Ephesus, Santaki) to 104 or 103 in winter, and Haunted Woodlands to 304 or 303 - all archives that
exist in vanilla `arena2`, so no engine change was needed for them. The Hammerfell branch is the one
that had to be null-guarded, and the guard is narrower than it sounds: it runs inside
`DaggerfallTerrain.PromoteTerrainData`, which has no try/catch of its own, so a throw there breaks
terrain promotion repeatedly and silently - and what it actually protects against is a **missing
region name**, which falls through to the unmodified ground archive. It is not evidence that the
port runs without a `PlayerGPS`, and nothing here should be read as a "no GPS" case. `NatureBatchOverrider` (in
`WODClimates.cs`) then runs on `StreamingWorld.OnUpdateTerrainsEnd` and swaps every nature billboard
batch on archive 501 whose pixel in `climate_map.png` is exactly `#FFA500` over to archive 10030,
Daggerfall Expanded Textures' 32-record "Makija" palm set.

One optional interaction, and the only one: `WODBiomes.Init` probes Vanilla Enhanced - Base by GUID
(`1f124f8c-dd01-48ad-a5b9-0b4a0e4702d2`), which is in this pack (see "Vanilla Enhanced" above). With
that module enabled the swapped nature records are used at their own size; with it off or absent they
are scaled 2x, which is the mod's own no-VE path. It is not a dependency and the manifest does not
declare one - either way the swap happens.

Because no licence has ever been declared upstream, this ships on the private test draft only - never
in a public release. The `tools/bundled-mods/mods.json` entry `WorldOfDaggerfallBiomes` is
`private_only` with a `pending:` licence, the combination `fetch.py` requires and `pack.py` hard-refuses
in the public MIT mod pack; the fetched `LICENSE` record beside the mod is that pending text, not an
upstream licence. The builder writes the bundle as `world of daggerfall - biomes.dfmod` (the lower-cased
manifest file name, `World of Daggerfall - Biomes.dfmod.json`; GUID
`3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`), but the draft release asset is named **`wod-biomes.dfmod`**:
GitHub rewrites spaces in release-asset names to dots, so the built name was not used. That is safe
because **nothing resolves this mod by `FileName`** - the launcher entry keys on the manifest `ModTitle`
`World of Daggerfall - Biomes`, and the feature's only `FileName` match is the Daggerfall Expanded
Textures dependency below. So any file name works here, and a tester who downloads `wod-biomes.dfmod`
should install it under that name rather than renaming it. The contrast matters: Expanded Textures
**must** keep its exact file name, because both this mod and World of Daggerfall find it by that name.
Without some Biomes bundle in `Mods` there is no `World of Daggerfall - Biomes` entry, the compiled code
never runs, and deleting it removes every byte the feature added.

Its one requirement is Daggerfall Expanded Textures (Ninelan; already in the pack, see above), which
supplies archive 10030 - the manifest declares it as a non-optional peer at 1.2.0, and it is detected
the same way WoD's is: DFU's own ordinal, case-SENSITIVE match against the bundle file name
`daggerfall expanded textures.dfmod` (`ModManager.FileNameMatches`, in which '-' and ' ' are the same
character since 2026-09-10 - see UPSTREAM-PATCHES.md - so a hyphenated copy of that name resolves too,
and nothing else about the comparison moved). With that mod off or missing, `World of Daggerfall - Biomes` is
switched off at start-up: the switch is simply off the next time MODS is opened and `Player.log`
records why (`[PortedMods] World of Daggerfall - Biomes off: Daggerfall Expanded Textures is not
enabled`). `BiomesDetNote` is appended to the entry's description as well, but that is best-effort and
the current launcher flow never shows it, for the reason UPSTREAM-PATCHES.md gives for WoD's own notes:
the log line is the signal, so do not tell a tester to look for a note.

**This mod's textures are the one exception to the pack's ASTC rule**, and it is load-bearing. The
climate map is a colour key read back on the CPU and compared *exactly* (`r == 255 && g == 165 && b == 0`),
so a lossy format silently turns every orange pixel into a near-miss and the nature swap simply never
happens; the 224 terrain tiles are 64x64 point-filtered records DFU decompresses into an ARGB32
`Texture2DArray` anyway, so ASTC would only cost them quality. `Assets/Editor/MobileModPackTextureRules.cs`
holds that exemption as a data-driven list read by `MobileModPackTextureImporter`: textures under
`Assets/Game/Mods/WorldOfDaggerfallBiomes/` import `isReadable = true`, uncompressed, RGBA32 on the
iPhone platform, with mipmaps off for `climate_map.png` only, and the meta's Point filter left alone.
**The rule keys on the mod folder name, which is the `mods.json` entry `name`** - rename that entry and
the textures silently revert to ASTC, breaking the colour key with no error anywhere. The cost is
memory: 224 tiles of 64x64 RGBA32 with mips plus a 1000x500 RGBA32 map is about 6.6 MB of texture data,
and `isReadable` keeps a CPU-side copy of each, so about 13 MB in all, against under 1 MB had they gone
through ASTC 6x6 unreadable. Belt and braces on top of the rule, `NatureBatchOverriderInstaller.ClimateMap`
runs a bundle built before it (or hand-installed) through `TextureReplacement.EnsureReadable` once per
session, so `GetPixel` cannot throw.

Location Loader gets one more file for this mod: `Ports/LocationLoader/BiomesClimateSwap.cs`, carademono's,
from the same unlicensed `rmb-object` fork the type-5 backport came from
(github.com/drcarademono/DFU-LocationLoader @ 896a574). It applies the same subtropical nature swap to
the RMB blocks Location Loader builds for object type 5 - whose nature is loose `Billboard` components
rather than batches, so the streaming-world pass above never sees them - and it reads the climate map
from the compiled-in Biomes port rather than `GetAsset`-ing its own copy out of a bundle, which is why
it was left out of the first Location Loader port. It is a no-op unless the Biomes entry actually
started this session (`MobilePortedMods.BiomesRunning`) and the map is readable; its own log line is
`[Biomes] swapped N type-5 nature flats to archive 10030 in <block name>`. The block name is on that
line for a reason worth knowing when reading a log: identical counts repeating (22 four times, 7 four
times, in the run that prompted it) are innocent when the block names differ - that is one WoD prefab
placed at several map pixels. One block name repeated with *different* counts is innocent too: a block
whose terrain was not ready part-way down its flat list is deferred whole (`[Biomes] terrain not ready
for ...; type-5 nature swap deferred to the next terrain update`), logs the flats it did swap, and logs
the remainder when the retry re-walks it. The signature of a genuine double swap, which would double a
flat's scale a second time, is **the same block name repeated with the same count**.

Not device-verified at the time of writing. Unlike WoD this mod places no objects - it changes which
textures existing ones use - so the performance question is narrower, but the nature swap does run on
every terrain-streaming update; it is bounded by the archive filter that skips any batch already
swapped, a per-archive material cache and a 1024 atlas cap (upstream allocated 4096, a transient
~85 MB spike), and its whole
body is wrapped so a failure logs once per distinct message rather than once per frame
(`[Biomes] nature swap failed: ...`). The lines a tester should look for are
`[PortedMods] started World of Daggerfall - Biomes` and, once terrain has streamed in,
`[Biomes] swapped N nature batches to archive 10030`.

## World of Daggerfall - Terrain (compiled in, private draft only)

The fourth mod of the set and much the most invasive: it does not decorate the world, it *replaces*
it. `Monobelisk.InterestingTerrainSampler` takes DFU's `DaggerfallUnity.TerrainSampler` slot and
every terrain tile's 129x129 heightmap is computed by a Metal compute shader instead of by the
engine. It replaces **heights only**. Both of upstream's `TerrainTexturing` assignments are commented
out - verbatim upstream, which expects Wilderness Overhaul to pull the tilemap through the
`getTileData` mod message, and Wilderness Overhaul is not in scope here - so DFU's own texturing
(`MobileRoads.cs` -> `BasicRoadsTexturing`) still assigns every tile, exactly as it does with the
switch off. Which is also why the `LinearData` maps' road and port channels reach the ground only
through the heightmap: nothing in this build paints with them.

The lineage is long - monobelisk's Interesting Terrains, by way of
Freak2121, carademono and Ninelan - and nowhere in it is a licence declared, so the same rule as the
three sections above applies: the C# is compiled in under
`Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/`, the data is an off-by-default entry in
the launcher's MODS window fed by a bundle, and the bundle ships only on the private test draft.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| World of Daggerfall - Terrain 1.5.0 | monobelisk, Freak2121, carademono, Ninelan (from monobelisk's Interesting Terrains); NO LICENCE DECLARED anywhere in the lineage (permission being sought by Ikram; not in any public release) | github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40 | the repo's 212 MB of `.xcf` and `WOODS.WLD` authoring files (they are not in the manifest, and the fetch is manifest-only); the editor scripts and `Scripts/Models/Editor/`; `Helpers/ConsoleHandler.cs` (dev console commands, useless on a device) and the `ClearNoonRoutine` that existed only for its `clearnoon` command; the dead `MainHeightmapSmoother.compute`. The 20 remaining runtime files (+2,530 lines as ported) are compiled in under `Ports/WorldOfDaggerfallTerrain/`; the two live compute shaders and their four `.cginc` (6 files, +2,557 lines) are compiled into the app under `Assets/Resources/WoDTerrain/`; the five PNG world maps and the noise-parameter INI are the bundle |

What it does, in two passes. At start-up `MainHeightmapComputer` is dispatched over the whole
1000x500 world and the result replaces `ContentReader.WoodsFileReader.Buffer` - the small world
heightmap the travel map, the region maps and Distant Terrain all read - so the shape of the world
changes on the map as well as under your feet. Then, per terrain tile, `TerrainComputer.compute`
runs its `TerrainComputer` and `TilemapComputer` kernels (13x13 groups of 10x10) and both results
are read back **synchronously on the main thread**; that synchronous readback is deliberately kept
for version one, and whether it hitches is exactly what the device test is for. Terrain around every
location is flattened and blended on the GPU across a 33x33 map-pixel window
(`IsLocationTerrainBlended() => true`), `StreamingWorld.TerrainScale` becomes 1 and
`Camera.main.farClipPlane` 10000.

**The compute shaders ship inside the app, not in the bundle.** iOS cannot load code from a
`.dfmod`, and a `.compute` is code; so `TerrainComputer.compute`, `MainHeightmapComputer.compute`
and `noises.cginc` / `noiseParams.cginc` / `heightSampling.cginc` / `basicRoads.cginc` live in
`Assets/Resources/WoDTerrain/` and every `mod.GetAsset<ComputeShader>(name)` became
`Resources.Load<ComputeShader>("WoDTerrain/" + name)`. The `mods.json` entry excludes `*.compute`
and `*.cginc` from the fetch (`strip_code` only strips `.cs` and `.dll`), so the bundle carries data
and nothing else. One line was deleted from `basicRoads.cginc`: `#pragma exclude_renderers d3d11
gles`, a surface-shader pragma with no meaning inside a `.cginc` included by a `.compute` - shipping
a renderer exclusion on trust was not worth the risk on Metal. Everything else in all six files is
upstream's, with a provenance line added at the top.

The five maps are the one import in the pack that goes through the new **`LinearData`** rule in
`Assets/Editor/MobileModPackTextureRules.cs` (Biomes' `RawData` rule is the other exception; see
above). They are numbers, not pictures - heights, derivatives, biome weights, port and road flags -
and only a compute shader ever reads them, so the rule turns **sRGB sampling off** (the project is
Linear, and sRGB would silently remap every value), keeps them **uncompressed** RGBA32 on iPhone at
`maxTextureSize` 2048, leaves `isReadable` false (the GPU is the only reader) and the upstream
filter modes alone, and switches **mipmaps off**. The mip chain matters twice over: every read of
these maps in the shipped shaders is a level-0 fetch (`SampleLevel(..., 0)`, and
`float sampleLevel = 0` in `heightSampling.cginc`), so a chain is memory nothing can ever sample,
and a mip level of numeric data would be an average of unrelated numbers rather than a smaller
picture. That is about 11 MB saved, and it puts the four 2048x1024 maps plus the 128x128 noise tile
at roughly **34 MB of GPU memory**, not the ~43 MB the design estimated with mips on. As with the
Biomes rule, **the rule keys on the mod folder name, which is the `mods.json` entry `name`**
(`WorldOfDaggerfallTerrain`): rename that entry and the maps silently go back through sRGB and block
compression, which shows up as wrong ground heights with nothing in any log to say so. The self-test
checks every name in both lists against `mods.json`, and that the two lists are disjoint - a name in
both would be treated as raw data, which is the opposite of what a compute-only map needs.

Seven fixes were made in place, each marked `// MOBILE` and each with the smallest test that could
hold it. **(a)** the start-up dispatch was one submit of 500,000 threads followed by one `GetData`;
iOS's watchdog is free to kill a Metal command buffer that runs too long and does not care that the
work is legitimate, so it is now ten row bands (`TerrainComputer.Bands`, `StartupBands = 10`) with a
readback between them, a new `int yOffset` uniform in the kernel and the index formula otherwise
untouched. The readback range is computed from the *end* of the band (`MapHeight - yStart - rows`),
because the kernel writes world row `y` to buffer row `499 - y`: reading back `yStart * MapWidth`
would hand back a region belonging to the band at the other end of the map, and the world would come
out striped. `Bands` splits into whole 5-row thread groups (the kernel is `numthreads(10,5,1)`) with
the last band taking the remainder, and because the last band is the one place a row could still be
lost to `Dispatch`'s integer group count, a height that is not a whole number of groups is refused
with a named exception rather than quietly producing a striped world (`WoodsFile.MapHeight` is a
const 500, so that guard is against a future source change, not a live path). The mirrored offset is
a pure `ReadbackStartRow(mapHeight, yStart, rows)` so the self-test can prove the ten ranges tile
`[0, 500000)` exactly once, rather than leaving the deviation to a comment. The self-test also pins
that every band is contiguous, whole-group and covers every row for every band count.
**(b)** `locationHeightData` was a 289-element `ComputeBuffer`, but the shader writes
`locationHeightData[i]` for every `i < locationCount` and `locationCount` counts the locations found
in a **33x33 = 1089** map-pixel window (which is also what its `locationPositions[1089]` /
`locationSizes[1089]` arrays are sized for) - so any tile with more than 289 nearby locations wrote
out of bounds on the GPU. D3D11 silently discards out-of-range UAV writes, which is why it was never
noticed on desktop; on Metal an out-of-bounds buffer write is undefined and can fault the command
buffer. The buffer is now `LocationBufferSize = 1089` and the shader is untouched. **(c)** the shader was
`Object.Instantiate`d **once per terrain tile** and never destroyed, so a session leaked one managed
object and one Metal pipeline state per tile; there is now one held clone per kernel asset, released
by `Cleanup()`. **(d)** `SetVectorArray` with a zero-length array throws, and open water in the
Iliac Bay produces exactly that; a one-element zero array with `locationCount = 0` is passed
instead. **(e)** `TileDataCache.Add` threw on a key already present (a tile regenerated before its
texturer consumed it) and is now an indexer assignment - the same guard AsesinoBlade's fork made,
for the same reason. **(f)** the location cache remembered only hits, so the ~1,000 map pixels in
each 33x33 window that hold no location were re-read from MAPS.BSA through `GetMapPixelData` for
every tile forever; misses are cached too (`Dictionary<DoubleInt, Rect?>`, a null meaning "checked,
nothing here"), and its key type gained `IEquatable` and `GetHashCode` because 1,089 lookups per tile
were going through reflection-based `ValueType.Equals` and boxing. **(g)** capability and
containment: the mod has no CPU generator of any kind, so `Init` refuses rather than half-installs -
`Available(SystemInfo.supportsComputeShaders, shadersLoaded)` must hold, both shaders must carry
their three kernels, and all five maps and the INI must be in the bundle, and any failure logs
`[WoDTerrain] not available: <reason>` and returns with DFU's own sampler untouched. Per tile,
`GenerateSamples` is wrapped in try/catch/finally with `TerrainComputer.Create` **inside** the try
(it allocates three `ComputeBuffer`s, the one thing here likely to throw under the memory pressure
this port adds); a failure logs `[WoDTerrain] tile failed: ...` once per distinct message and falls
back to `DefaultTerrainSampler`'s job path, pointed at this sampler's dimension and height so a
fallen-back tile is lower relief but continuous ground rather than a 3x-too-tall spike. The buffers
are disposed in `finally`, idempotently.

Three more hardenings came out of review. The INI parse and the whole banded world-heightmap
dispatch now run **before** the `GameObject` exists, because `AddComponent` runs `Awake`
synchronously and `Awake` is what swaps the sampler - upstream did both afterwards, so a malformed
INI left the GPU sampler installed with a null or half-parsed parameter set, which is garbage terrain
instead of vanilla terrain: the exact failure the gate exists to prevent, one step later. A failure
in that window puts `WOODS.WLD`'s buffer back the way it was found (`ShouldRestoreWoodsBuffer` is the
pure half, and is pinned by the self-test) and drops the generated basemap, the shader clones and the
location buffer. And a refused start nulls the five maps and both shader references so they are
collectable rather than pinned by statics for the session - on the one device where the gate actually
fires, which is the device that could least afford it. **A limit, honestly:** DFU's own
`Mod.loadedAssets` cache still holds its entries for those textures (private, stamped `-1` so it is
never pruned, with no public clear), so full reclamation would need an engine change and this port
deliberately makes none.

One more, found by reading the shader rather than by running it. `heightSampling.cginc`'s
`GetBiomeWeights` calls `SampleBaseHeight` unconditionally, and that function reads two
`StructuredBuffer`s (`shm`, `lhm`) and four uniforms (`hDim`, `div`, `sd`, `ld`) that only the
*per-tile* path ever bound - so the start-up world-heightmap dispatch was reading unbound buffers
through an undefined divisor. D3D11 hides that; on Metal an unbound or out-of-range structured read
is undefined behaviour and is exactly the shape of a command-buffer fault at launch. The start-up
dispatch now binds two small zero-filled dummies (4x4 and 9x9, the same shapes the per-tile path
allocates) and sets the four uniforms to the values *this* path's index arithmetic implies - `hDim`
is `MapWidth`, not the sampler's `HeightmapDimension`, because here the flattened index was built
with `MapWidth` as its stride, and passing the tile value would drive the reads hundreds of elements
out of bounds. The *values* are provably dead - `CSMain` asks for `detailedHeights: false` and the
result is overwritten by the DerivMap-derived height a few lines later - so zeroes change no output
pixel; what had to be true is that every read is in bounds, and the pure
`StartupSampleMaxIndices` plus its self-test check prove that for all 500,000 sample indices the
dispatch can produce. The dummies are released after the last blocking readback.

The start-up stopwatch spans the whole method rather than only the dispatch loop, because the 500 KB
buffer copy, the ten submits, `ToBytes` over 500,000 floats and the 1000x500 `SetPixels32`/`Apply`
are one uninterruptible pause from the player's point of view. The number in
`[WoDTerrain] world heightmap <ms> ms (10 bands)` is therefore the pause it names, not a fraction
of it.

**The generated world map is repaired before either reader sees it** (2026-09-10). 459 of its
500,000 map pixels came out at or under the generator's own ocean floor byte while being ringed by
clear land on five or more of their eight sides - inland Tamriel rendered as sea. They are not
heights the generator meant: they are pixels its 2048-clamped control maps averaged from land into
water, and both readers of that byte suffer for it. The travel and region maps draw `WOODS.WLD`
directly, so each one is a hole in the coastline; and the tile shader samples the same map as a
LOCATION'S FLATTEN TARGET with a texel offset that blends up to four neighbouring map pixels, so a
location whose blend touched one lost its height (the 20-unit sink cap stops that becoming a pit,
but the wrong target remains). `TerrainComputer.RepairWorldHeightmapHoles` replaces each with the
MEDIAN of its land neighbours - median so a two-pixel hole's own floor byte cannot drag the repair
back towards the sea, and so the result can never lie outside the range the neighbours already hold
- in at most two passes, each judging the map as it stood when the pass began. Open sea and
coastlines are excluded by the same three constants the census uses. It runs between `ToBytes` and
the `WoodsFile` buffer swap, so the buffer and the `baseHeightmap` texture both see the repaired
bytes, and it reports itself in the same line:
`[WoDTerrain] world heightmap 42 ms (10 bands) holes 459 repaired 542 nonfinite 0`. Repaired (542)
exceeds holes (459) because the second pass re-censuses; 40 remain, all in shapes two passes cannot
reach without starting to eat real inlets. Measured on the Mac's Metal by the Editor probe and
confirmed byte for byte on the iOS Simulator.

**Read the per-tile number knowing that part of it is dead work.** Each tile dispatches the
`TilemapComputer` kernel as well as `TerrainComputer`, reads its 16,641-int result back
synchronously on the main thread (~66 KB), turns it into a `byte[16641]` and files it in
`TileDataCache` under a string key - and then nothing reads it. The only consumer is
`ModMessageHandler.GetTileData`, which no mod in this build calls, and `UncacheTileData` deletes each
entry at promote. It is kept for this round because it is upstream's shape and because it is what the
`getTileData` contract is made of; a later round may drop it. So a measurable slice of every
`[WoDTerrain] tile <x>,<y> <ms> ms (locations <n>)` line is a second dispatch and a second blocking
readback whose output is thrown away, and if the tile timings come back too high, that slice is the
first thing to remove.

**Road smoothing works, over the compiled-in road network** (2026-09-10; it was inert in the first
two builds and that is worth knowing when reading an older log). Upstream's `BasicRoadsUtils` asks
`ModManager` for a *mod titled* `"BasicRoads"` and then sends it a `getPathData` message. This port
has no such mod - Basic Roads is compiled into the app as the "Roads & tracks" feature, with no
message receiver - so `CompatibilityUtils.BasicRoadsLoaded` was always false, `GetRoadData` returned
its nine zeroed vectors, and the shader's `saturate(1.3 - roadWeight * smoothRoads)` never left
1.3: the terrain was generated as though there were no roads, underneath roads the player can see
painted across it.

`BasicRoadsUtils.Init` now runs unconditionally and chooses its own source. When the roads switch is
on for drawing it reads `MobileRoadNetwork` - the same `roadData`/`trackData` byte arrays Basic Roads
authored, at the same 1000x500 map-pixel layout with the same direction bitmask, so this is that data
by another door and not a second implementation. It says which source it took, once:
`[WoDTerrain] roads: smoothing on, over the compiled-in Basic Roads network`, followed by
`[WoDTerrain] roads: N map pixels with paths in the current tile (x,y, source compiled-in)` for the
first tile - the one line that distinguishes "the flags reached the shader" from the old silent zero
state. Upstream's message path is still there for a real bundle, and its answer is now checked for
null and for length before anything indexes it (it fed a null-array dereference per direction per
tile). The gate is the DRAWING switch, not merely the data: the network ships with the code either
way (Real travel routes on it with roads switched off), and flattening a corridor the player cannot
see would be an unexplained flat strip on a hillside. It is read once, which is correct rather than
merely cheap - drawing roads is restart-required by design.

Measured in the iOS Simulator with only that switch changed: 23 of the 49 tiles both runs built
moved, `base=` identical on every one of them (so the world heightmap is untouched and this really
is the per-tile pass), largest single change 0.0103 of `newHeight` - about 51 units of bump taken
out of a road corridor. The other 26 tiles had no road in their 3x3 window and are byte-identical.
The `daggerfall_road_map.png` mask still decides *where* smoothing is allowed at all.

Two things change the moment the switch goes on, and both are by design rather than by accident.
Ground height: `MaxTerrainHeight` goes from DFU's 1539 to 5000, `OceanElevation` from 27.2 to
100.01, `BeachElevation` from 40 to 103.9 and the sampler `Version` from 1 to 7, so a character
saved standing on ground computed the old way is standing on ground that has moved. And the travel
map: the world heightmap the map draws from is rewritten at start-up, so the coastlines and relief
on the travel and region maps change too. Turning the switch back off puts both back, so nothing is
destroyed - but a save made while it was on was made in a world of a different shape, which is why
this is a per-save decision rather than a per-session one. `MobilePortedMods` says so once per launch
as a plain log line
rather than a warning:
`[PortedMods] World of Daggerfall - Terrain: this changes ground height under existing saves and the
travel map (by design)`. The entry has no dependency gate at all: Basic Roads is optional to it and
Daggerfall Expanded Textures is not its dependency, so its own switch is the whole condition. It is
started **last** of everything in `StartEnabled`, because replacing the terrain sampler is the most
invasive thing started there and an `Init` that threw at that point cannot cost any mod before it
its start.

Not device-verified at the time of writing, and the port carries its own measurement for when it is:
`[WoDTerrain] world heightmap <ms> ms (10 bands)` once at start-up, and
`[WoDTerrain] tile <x>,<y> <ms> ms (locations <n>)` for the first ten tiles and then every
twenty-fifth - steady on a walk, and still sampled on a fast-travel arrival, which generates 49
tiles in one non-yielding call. `Player.log` is therefore the performance report. One thing to watch
there beyond the timings: `TerrainComputer.LocationRectCache` has no eviction, so it grows by one
entry per distinct map pixel visited and is bounded only by the 1000x500 world; a long session is
the case that would show it.

A note for anyone diffing the port against upstream: five files were rewritten with LF line endings
where upstream had CRLF - `InterestingTerrains.cs`, `Models/TerrainComputer.cs`,
`Models/HeightmapBufferCollection.cs`, `Helpers/BufferIO.cs` and `Helpers/TileDataCache.cs`, which
are exactly the five CRLF files that carry substantive `MOBILE` edits. A plain diff shows every line
of them as changed; `diff <(tr -d '\r' < upstream/file) ported/file` shows what actually changed.

## Distant Terrain of the World of Daggerfall (compiled in, private draft only)

The fifth of the set, and the only one you look *at* rather than walk on. It draws a second, coarse
Unity `Terrain` covering the whole province on a camera stacked behind the main one, so the horizon
has hills and coastline on it instead of fog - the World of Daggerfall flavour of it lifts 32,928
mountains out of that far ground at the map pixels where WoD's own mountain prefabs stand, so what
you see in the distance is the world you would arrive in.

Its lineage forks: **the base is MIT and the additions are not**. Nystul-the-Magician published
Distant Terrain in 2017 under the MIT licence and that copyright header is preserved in every file.
MaoDeVaca's World of Daggerfall-flavour modifications - the mountain lifts, the river and coast
carve, the location beacons, the per-region treatment - declare no licence anywhere, and the only
versioned copy of them in existence is a vendored drop inside somestupidgirl's build-script repo.
So the same rule as the three sections above applies to the whole of it: the C# is compiled in under
`Assets/Scripts/Game/Mobile/Ports/DistantTerrain/`, the rewritten shader under
`Assets/Shaders/DistantTerrain/`, the data is an off-by-default entry in the launcher's MODS window
fed by a bundle, and the bundle ships only on the private test draft.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| Distant Terrain of the World of Daggerfall | Base: Nystul-the-Magician, **MIT, 2017** (header preserved). WoD-flavour additions: MaoDeVaca (Nexus 1284), **NO LICENCE DECLARED** (permission being sought by Ikram; not in any public release) | Additions: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ `d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f` - the **only** versioned copy, and it is a vendored drop of upstream `a722b337935dd8a57a913eb413c918c351e68e67`; her own commits add build scripts, which are not used. MIT base: github.com/Nystul-the-Magician/dfunity-mods `DistantTerrain/` @ `fc58546c3eae964babfdfeea51e97a47ba57cdce` | `DistantTerrainFlyMap.cs` (930 lines - a keyboard/mouse/IMGUI free-camera teleporter, and a phone has neither) and `ThirteenthPassageEffect.cs` (144 lines - the "13th Passage" Mysticism teleport spell and its `passage` console command), with the spell registration, the console registration, the fly-map creation and the `KeyCode` settings that drove them. Excluded from the fetch as well: `*.shader`, `*.cginc` (the shader is compiled into the app, rewritten - see below), `*.prefab`, `*.png~`, `DaggerfallBillboardBatchFaded.shader`, and the three `*.bin.txt` blobs (`mapLocationRangeX`, `mapLocationRangeY`, `mapTreeCoverage`) nothing in the runtime reads. The five remaining runtime files (+3,918 lines as ported) are compiled in; the two shader files (+1,081) are compiled into the app; the three `Mountains*.csv` and `daggerfall_deriv_map.png` are the bundle, fetched into `ModResources/` rather than upstream's `Resources/` (a folder of that name is baked into every player build by Unity, which would ship this pending-licence data in a public IPA) |

The manifest is `distantterrain.dfmod.json`, so the bundle builds as `distantterrain.dfmod`, and the
launcher entry keys on the manifest `ModTitle`, which is the full
**`Distant Terrain of the World of Daggerfall`** - not the bare `Distant Terrain` the research
predicted. Its GUID is `9632a2ad-2ea0-46b6-b9b1-a4dafcca8a9a`, **the same GUID as Nystul's original**,
which makes the two mutually exclusive by construction; Nystul's is not in this pack, so nothing
collides here, but it is why a future MIT-only fallback would be a replacement rather than a
companion.

What it does, once the world exists. At `StreamingWorld.OnReady` it builds a 1025² `TerrainData`
from the small world heightmap (`WoodsFileReader`, 1000x500), carves rivers and coastline into it
from a hand-painted greyscale mask, lifts the WoD mountains out of it from three CSVs (un-lifted
inside the near-terrain footprint, so the seam stays continuous), bakes a 1024² RGBA32 "terrain
info" tilemap (climate, per-region treatment, location beacon) and hangs the whole thing on a
stacked camera whose far clip plane is `blendEnd`, with the main camera forced to 15,000 and clear
flags `Depth`. A third camera renders the skybox only into a 256² render texture, which is where the
fog colour and the world-edge fade come from. Every map-pixel crossing re-lifts the mountains and
pushes a `SetHeights` dirty rect.

**The one piece of real engineering here is the shader.** Upstream's `FarTerrainCommon.cginc`
sampled **twelve 2048² ARGB32 tileset atlases** - four biomes (desert, mountain, woodland, swamp) by
three seasons (summer, winter, rain) - which the C# built at runtime, and which this build's terrain
path never builds at all. That is roughly **270 MB of texture memory** before anything else in the
game is loaded, and it is the reason the port could not simply copy the file. The rewrite samples
**three `UNITY_DECLARE_TEX2DARRAY` arrays** instead, one per season, each packed with all four biome
tilesets' 56 records as slices (`slice = biome * 56 + record`, 224 slices of 64², built from DFU's
own `TextureReader.GetTerrainTextureArray` so texture-replacement packs are still honoured, packed
with 224 `Graphics.CopyTexture` slice blits at world entry, or resampling blits through a render
target when a source's size, mip chain or format is not the destination's - see below). **~15 MB**
instead of ~270, and **~15 MB whatever the player has installed**: the destination slice size is a
constant of the port (`TileSliceSize` 64, `TileSliceMipCount` 7), not the sources' own. The atlas
cell origin, the 32-texel gutter offset and the atlas-normalised gradients are gone; the tiling
maths, the slope blend, the snow caps, the woodland dirt, the tree specks, the beacons, the skirt,
the near-terrain cutout `discard` and the `alpha:fade` transparent queue are upstream's, unchanged.
The mip selection is algebraically identical to upstream's `tex2Dgrad` - a 2048 atlas of 16x16 cells
holds a 64-texel tile core, so `_AtlasSize / _GutterSize` is 64 and divides straight out - and it is
computed with an explicit `_LOD` sample because `HLSLSupport.cginc` has no
`UNITY_SAMPLE_TEX2DARRAY_GRAD` and both DFU's own array terrain shader and Nystul's own
`TransitionRingTilemapTextureArray` select the level by hand for the same reason. Also deleted: the
never-read `_CameraDepthTexture` sampler pair, three legacy samplers (`_TileAtlasTex`, `_TilemapTex`,
`_BumpMap`) and with them the orphaned `uv_BumpMap` interpolator, and `#pragma glsl`. `#pragma target`
goes 3.0 -> 3.5 on the two surface programs (a `Texture2DArray` needs SM3.5); the depth-only pass
samples nothing and stays at 3.0.

**The sampler count was never the problem, and it is worth recording that we checked rather than
assumed.** A spike compiled the *original* atlas shader for Metal/iOS before a line of it was
rewritten (`Assets/Editor/MobileShaderSpike.cs`, kept for reuse): it compiled, all three passes, 102
Metal programs, `ShaderHasError` false, **zero messages** - no errors and no warnings, including
none about the `float4 pos : SV_POSITION` in the surface `Input` struct or the long-dead
`#pragma glsl` that the design had flagged as risks. The worst single fragment program bound **12 of
Metal's 16 sampler slots**, with four to spare. So the original could have shipped as far as the
compiler is concerned; it could not ship because of *what* those twelve bindings were. After the
rewrite the same tool measures **6 samplers in the worst program** (`_TileArraySummer/Winter/Rain`,
`_FarTerrainTilemapTex`, `_SkyTex`, `_SeaReflectionTex`), ten slots spare, still zero messages, and
the same 102 programs. The useful corollary the spike bought: any Metal complaint that appears from
here on belongs to the rewrite, not to an inherited incompatibility.

**The river/coast mask imports at 2048x1024 R8, and that is a deliberate 25x memory decision.**
`daggerfall_deriv_map.png` is a 5,000x2,500 8-bit greyscale water mask (139 distinct values - it is
antialiased, not binary), read once on the CPU with `GetPixels32` and never sampled by any shader.
The design said to keep it at full resolution; reading `ApplyDerivativeHeightmap` showed that wrong:
the carve scales the image onto the 1000x500 world grid by the texture's **own** `width`/`height` and
takes the darkest pixel of each cell's block, hard-coding no resolution at all. So it goes through
the pack's `RawData` rule at Unity's ordinary `maxTextureSize` 2048 - Unity clamps proportionally,
giving 2048x1024, still twice the grid it is sampled onto - readable, uncompressed, **no mips**, and
single-channel `R8` (a new `SingleChannel(path)` hook, because the carve reads `.r` only and the
other three channels would be duplicate bytes paid for twice, once on the GPU and once in the
readable copy). **4 MB instead of 100.** The cost was measured offline against a full-resolution
carve rather than argued: of 186,942 water cells, 2048x1024 loses 565 (0.30%) and gains 3,038 - a
handful of thin river cells move by one cell - where a 1250x625 import would have lost 1.03%. That
`GetPixels32` returns sensible bytes from an `R8` texture is a self-test check with a real round
trip, not an assumption, though it is an *Editor* check; if the far terrain ever comes back
all-ocean on a device, that is the first thing to look at. As with the Biomes and Terrain rules,
**the rule keys on mod folder + file name, never the bare name**: `WorldOfDaggerfallTerrain` ships a
*different* `daggerfall_deriv_map.png` (RGBA, under the `LinearData` rule, read by a compute shader),
and a bare-name match would have taken three channels and the mip chain off that one.

**The reach dials, and why one of them is derived rather than configured.** `blendEnd` is both the
stacked camera's far clip plane and the distance at which the far terrain has faded to nothing;
upstream ships 120,000 and this port defaults to **60,000**, half the reach, because half the world
is a lot to draw on a phone. That halving is also the reason a tester can look straight at a working
far terrain and see nothing: **at a flat vantage the far terrain's whole skyline sits below the near
world's tree line.** Measured in the simulator at map pixel 300,210, its highest point is +1.3° above
eye level - 41 pixels of a 3,870,400-pixel frame, which reads as zero and once cost a whole
diagnostic round on a "the far terrain never draws" finding that was false. It draws. **Look for it
from a hilltop, or across water or a coast**, where the near ground falls away and there is sky for
it to occupy; at 300,210 there is not. Lowering `blendEnd` further shortens that skyline again, and
raising it towards upstream's 120,000 lengthens it - so the dial that costs frames is also the dial
that decides whether the feature is visible from anywhere but a summit. `mainCameraFarClipPlane` is **15,000**, upstream's own value, exposed as
a dial rather than changed. The trap is `blendStart`: the shader computes
`fadeRange = _BlendEnd - _BlendStart + 1`, so halving `blendEnd` alone would leave upstream's
`blendStart` of 100,000 *above* it, invert the band and fade every far-terrain fragment to alpha
zero - an invisible far terrain produced by a setting that looks entirely reasonable. So `blendStart`
is not a setting at all: `DistantTerrainPort.BlendStartFor(blendEnd)` keeps upstream's 5:6
proportion (50,000 at the default) and a self-test pins it. Both are read from a `Rendering` section
in `modsettings.json` if one is present and clamped (main far clip 1,000-30,000; `blendEnd` from the
main far clip to 145,000), and **the iOS preset lives in the code defaults, not in
`modsettings.json`** - that file is fetched data this port does not patch. The two feature dials do
still come from the bundle's own settings: `EnableTreesAndDirt` (the procedural distant tree specks
and woodland dirt) is already **off** in upstream's shipped settings, and
`HighlightDistantLocations` (the colour-coded location beacons) is **on** there - and the port
overrides that. Upstream's second, *live* beacon gate (`RuntimeVisible`, flipped in game by an End
key that only the deleted fly-map polled) has to start **true** here, because with the hotkey gone
leaving it false would mean the beacons could never appear at all - which makes the master switch
the whole gate, so the master switch carries the preset: `HighlightLocations` defaults to **false**
in code, and `DistantTerrainPort.HighlightLocationsFrom` treats the bundle's `true` as *unset*.
DFU merges a player's own settings file over the bundle's `modsettings.json` and offers no way to
ask which of the two a value came from, so the file's existence is the test: **only a settings file
the player actually has on disk can turn the beacons on.** Upstream shipped them baked-but-hidden
(`true`/`false`); this port ships them not baked at all unless asked for (`false`/`true`).

Two behavioural differences from upstream are worth naming because they are the largest ones.
Upstream's `Awake` called `Application.Quit()` when `StreamingWorld`, `PlayerGPS` or `WeatherManager`
was missing - reasonable in a mod that starts inside a running game, fatal here, where `Init` runs at
the **title screen** and would have closed the app on every launch with the entry on. Those lookups
moved into a retry that reports only at world entry, `SetupGameObjects` declines until the world
exists, and the consequence is that **the stacked camera comes into existence at world entry, not at
start-up** - which is exactly what makes the sky's wait for it meaningful (below). And
`InitFarTerrain` is now a try/catch wrapper around the build: upstream let a throw escape into
`StreamingWorld.OnReady`'s invocation list, where it stops every later subscriber for the session.
On a throw the port logs `[DistantTerrain] far terrain failed: <ex>` and tears down - the far terrain
object, its `TerrainData`, the runtime material, the 1024² tilemap texture and the managed `Color32`
copy behind it, the stacked camera, the skybox camera and its render texture, the heightmap arrays,
the tile arrays - and puts the main camera's saved `farClipPlane` and `clearFlags` back, and clears
`Running`/`Installed` so nothing downstream keeps waiting on a far terrain that removed itself. The
first four of those are objects that outlive the GameObject holding them: destroying a `Terrain`
does not destroy its `TerrainData`, and dropping a reference to a runtime `Material` or a readable
1024² `Texture2D` leaks it for the session (~12-16 MB) inside the handler whose purpose is to
undo the half-built world. Smaller ones in the
same spirit: the `TerrainData` alphamap and basemap resolutions drop from 1,000 to 16 and the detail
resolution to 16/8, because the far terrain paints nothing through splat, basemap or detail maps and
upstream sized all three at world resolution; the driver object is `DontDestroyOnLoad`; source tile
arrays are destroyed after the pack **only if they are nameless**, so a replacement pack's own
`Texture2DArray` asset is left to its owner rather than corrupted for the session; and all nine
upstream log lines were re-prefixed to a single `[DistantTerrain]` so one grep finds everything the
port writes.

**Start order with Dynamic Skies.** The launcher starts Distant Terrain *before* the sky's deferred
start, and the sky's 1 Hz readiness poll additionally waits for `GameObject.Find("stackedCamera")`
while Distant Terrain is running, so `BLBSkybox.Init` takes its stacked-camera branch
deterministically instead of racing for it. That extra wait is bounded at fifteen passes: after
that the sky starts anyway with one line
(`[PortedMods] Dynamic Skies starting without Distant Terrain's stacked camera`), so no reason for
an absent stacked camera can strand it for a session. The sky's own branch is keyed on that camera
rather than on the `DistantTerrain` object upstream looked for - the object is `DontDestroyOnLoad`
and survives a teardown that destroyed the camera, and taking the stacked branch without a stacked
camera skipped the `cameraClearExterior = Skybox` line that stops `CameraClearManager` unsetting
the skybox after an exterior transition. It says which way it went:
`[DynamicSkies] clear flags on: player camera` or `[DynamicSkies] clear flags on: stackedCamera`. Because the stacked camera now only exists from world
entry, the practical consequence is that **with Distant Terrain on, the sky starts at world entry
rather than at the title screen** - accepted, since the title screen needs no sky. Fog is the loose
end: Distant Terrain's `Start()` overwrites five `WeatherManager` fog settings, which is upstream's
behaviour and is kept, but it means this mod owns those values rather than the weather system or
Dynamic Skies. It is now null-safe, idempotent and logged once
(`[DistantTerrain] fog settings overwritten -- sunny ..., overcast ..., rainy ..., snowy ...,
heavy ...`) so a log reader can see who wrote them, and reconciling the two visually is a **device
tuning item for a later round, not a code item in this one**.

Not device-verified at the time of writing, but no longer unrendered: the far terrain has been built
and drawn repeatedly in the **iOS simulator**, including an isolation frame with the near world's
layers culled away, which is what retired the earlier "it never draws" reading (see the reach dials
above). What remains unproven is the device - frame time, memory at world entry, and the look of the
near/far seam on a real screen. The port carries its own measurement for that:

```
[DistantTerrain] far terrain built in N ms (heightmap A ms, carve B ms, lifts C ms, tilemap D ms, arrays E ms)
[DistantTerrain] map-pixel update N ms
[DistantTerrain] arrays N MB
[DistantTerrain] far terrain ready
```

once per world entry, then per map-pixel crossing for the first ten and every twenty-fifth after,
plus the array total once per session (analytic, from slice geometry rather than from the profiler,
and it deliberately over-reports a compressed replacement set). Of the five stages, `arrays E ms` -
the twelve tileset builds and the 672 GPU slice copies behind them - is the only one that is not
paid on every entry: the arrays are packed once and kept, so it reads a real number on the first
world entry of a session and 0 on every one after it. The refusal and failure lines are
`[DistantTerrain] not available: <reason>` (the gate: shader unresolved or unsupported, a missing
CSV, a missing deriv map, or no scene at world entry), `[DistantTerrain] far terrain failed: <ex>`
(the build threw and was torn down) and `[DistantTerrain] tileset arrays mismatch: ...` - a source
array that did not decode into a usable surface at all (a zero dimension, or no mip level), or a
device that reports **no GPU texture copy support whatsoever**, where neither pack path can land a
slice because both end in a `CopyTexture`. Neither the SIZE nor the FORMAT of a source is a refusal
any more.

**Size stopped being a refusal on 2026-09-10, and that is a fix rather than a relaxation.** The
twelve `GetTerrainTextureArray` results have per-archive slice sizes as soon as a texture pack
replaces some terrain archives and not others, which is the ordinary state of a DREAM install:
Ikram's device log has `tileset 3 is 256x256, tileset 0 is 1024x1024 (summer)` and `tileset 1 is
1024x1024, tileset 0 is 64x64 (winter)`. The old rule refused the season, `BuildTileArrays` then
refused the far terrain, and `TearDownFarTerrain` ran - so his Distant Terrain had quietly been doing
nothing at all for the whole of every session. The destination is now **always** `TileSliceSize`
(64) square with `TileSliceMipCount` (7) mip levels, and any source that is not already exactly that
- in size, in mip count, or in format - is **resampled** into it by the same blit that converts
formats. The far terrain is the horizon, drawn on one 1000x500 world `Terrain` whose tiles are a few
pixels across on screen, so vanilla resolution is already more than it can show; and a fixed
destination is what makes the ~15 MB figure a promise rather than a hope, since a 1024² pack under
the old "adopt the sources' size" rule would have been 256 times that. One line per season says what
happened: `[DistantTerrain] tileset arrays packed: 56 copied, 168 resampled from 1024x1024 ARGB32,
256x256 RGBA32 (summer)`.

FORMAT was already not a refusal, for the same kind of reason: World of Daggerfall - Biomes installs
a `TextureArray` terrain material provider and `GetTerrainTextureArray` then hands back RGBA32 for
one climate variant of a season and ARGB32 for another, which is legitimate and which the pack
converts as it copies (a GPU blit through a destination-format `RenderTexture`, destination RGBA32).
Refusing it instead stopped the far terrain building at all whenever Biomes was on, which is how the
Task 9 simulator run found it. The sources' own format is still kept when all four agree - vanilla's
ARGB32, which makes the whole pack plain slice copies - with one exception: a **compressed** agreed
format that also has to be resampled falls back to RGBA32, because the scratch surface a resample
goes through is a `RenderTexture` of the destination format and no driver renders into ASTC or DXT.
That is exactly what an iOS-converted DREAM pack ships, so the alternative would be a surface that
cannot be created and a refused season.

The cross-season check remains, over the three PACKED arrays: the shader derives one mip dimension
from the summer array and applies it to all three, so they have to agree in size and mip count with
each other. All three are now built at the same constants, which makes it an invariant assertion
rather than a gate - it is kept so that a later edit making the destination shape per-season is
caught rather than shipped. `BuildTileArrays` also checks that the packed chain really is the
64-slice chain (`tileset arrays mip chain: packed N levels, a 64x64 slice is 7`), non-fatally,
because the uniform is pushed from the array itself and a short chain still samples inside it.

**And `Graphics.ConvertTexture` does not work on array slices at all.** It returns `false` for a
genuine ARGB32 -> RGBA32 slice conversion even while `SystemInfo.copyTextureSupport` reports
`Basic, Copy3D, DifferentTypes, TextureToRT, RTToTexture` - so the capability flags are not the
question, and gating on them left the Biomes mix refused anyway. The simulator run caught Unity's own
reason for it in the player console, which is worth writing down because it retires the "Metal quirk"
reading: **`Graphics.ConvertTexture does not support a Texture2DArray as source.`** That is an API
precondition, not a driver answer, which is exactly why no `copyTextureSupport` bit predicts it and
why the same call also returned `false` for the same-format round trip Task 9 fixed.
`ConvertTexture` is therefore not part of this path at all. The method is chosen per source archive
by a pure `SlicePackMethod(srcW, srcH, srcMips, srcFormat, dstW, dstH, dstMips, dstFormat)` - two
rows, decided by **shape and format**, with nothing learned at runtime: **Copy** when the source
slice already *is* a destination slice - same size, same mip chain, same format, which is what
vanilla's 64² ARGB32 tilesets take, all 672 of them (`Graphics.CopyTexture`, slice to slice) -
**Blit** for anything else -
`Graphics.Blit(src, scratch, sourceDepthSlice, 0)` into a `RenderTexture` of the destination
array's exact size and graphics format, `GenerateMips()`, then
`Graphics.CopyTexture(scratch, 0, dst, slice)`.
A blit is a *sampler fetch* over a fullscreen quad, which is why it does three jobs in one: the
channel order is decoded on read and re-encoded on write (so the driver has nothing to refuse), the
quad **rescales** whatever the source's resolution is into the destination's 64x64 viewport - with
the sampler's own mip selection doing the box filtering, because the quad's implicit LOD is exactly
`log2(srcDim / 64)` - and the regenerated chain gives the slice the destination's mip count, which is
also why mip count is in the `Copy` row: `Graphics.CopyTexture` of a whole element demands the two
agree on how many levels there are, and the destination's is now a constant. An earlier revision still probed `ConvertTexture` once per session and
latched the answer, on the theory that a Unity which grew an array-source path should get the cheaper
call for free; the probe could not succeed on any shipping runtime and it printed a red
`Graphics.ConvertTexture does not support a Texture2DArray as source` in the console at the first
world entry of every Biomes session, so it is gone and the `SlicePack` enum no longer has a `Convert`
member to fall into. The pack says what it did once per season, and that line is the one to grep for
on a Biomes or DREAM install: `[DistantTerrain] tileset arrays packed: 56 copied, 168 resampled from
1024x1024 ARGB32, 256x256 RGBA32 (summer)`. The mip
chain is regenerated from the converted level 0 rather than carried across, which for a same-size
source is the same image - the sources' own chains are Unity-generated box filters of the same pixels
- and for a downsampled one is the only option, since their chains are chains for the wrong
dimension. It matters because
the shader picks its tile mip *explicitly* (`UNITY_SAMPLE_TEX2DARRAY_LOD`; array `GRAD` sampling has
a long-standing seam bug), and an explicit lod past an array's last level is undefined in HLSL rather
than clamped. That is also why `_TileArrayMipCount` exists: the C# pushes the packed arrays' real mip
count and the shader clamps every lod to `_TileArrayMipCount - 1`, so an array with a short chain -
a replacement pack that ships no mips, or a future fallback that had to drop them - samples level 0
instead of black. The whole fallback is exercised on a real GPU by the self-test, which blits an
ARGB32 source slice into an RGBA32 array and reads the result back: slice index, orientation, channel
order and mip generation are all things only a driver can get wrong.

Memory, added up: the three tile arrays ~15 MB, the 1024² RGBA32 terrain-info tilemap 4 MB on the GPU
(the CPU copy is released at the upload and the staging `Color32[]` dropped with it), the deriv map
2 + 2, the 256² skybox render texture and its depth buffer under half a megabyte - about **27 MB of
texture memory**. Against the ~270 MB the twelve atlases alone would have cost, which is the whole
argument for the rewrite.

**But 27 MB is the texture memory only, and the measured total is roughly double it: ~50 MB steady,
~80 MB peak at world entry.** The difference is managed and just as real - three 1000² float
heightmaps (`worldHeights`, `baseWorldHeights`, `preDerivWorldHeights`, 12 MB), the 1000² ocean mask,
Unity's own 1025² TerrainData heightmap and its LOD pyramid - plus two transients on the world-entry
frame: the 3.5 MB CSV string split across 32,928 rows, and the 8 MB `GetPixels32` copy of the deriv
map. Quoting the texture figure alone was how a reader came away with half the number. On a 4 GB
iPad, beside World of Daggerfall's ~211 MB of textures and WoD Terrain's ~34 MB, all three together
are about **300 MB of mod memory** on top of DFU's own baseline: comfortable in steady state, with
the world-entry peak the thing to watch, because Distant Terrain's build lands on the same frame as
World of Daggerfall's location loading. **Watch memory at world entry with all three WoD mods on** is
therefore a named item of the device hand-off, not a general caution.

## Real Grass (compiled in, private draft only)

The only mod in this build that uses Unity's terrain **detail** renderer - the grass billboards
themselves, rather than the ground they stand on. It takes none of DFU's four terrain slots, so it is
additive with Basic Roads, Biomes, WoD Terrain, Location Loader and Distant Terrain rather than
competing with them.

Its licence forks along the same seam as the repository does. **The code is MIT and the art folder is
not.** Upstream is an archived monorepo with two roots: `RealGrass/` (the five C# files, the manifest,
`modsettings.json`, `LICENSE`, `credits.txt`) and `RealGrassAssets/` (28 MB of source art). The
`LICENSE` in the code folder is a plain MIT grant, reproduced verbatim below; `README.md` then takes
the whole of `RealGrassAssets/` *out* of that grant and points at `credits.txt`, which licenses
positively only (a) VMblast's textures, "authorized for this project only", and (b) *some* meshes and
textures drawn from three CC0 packs, without mapping any file to any pack.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| Real Grass 2.11 | Uncanny_Valley & TheLacus. Code: **MIT** (`RealGrass/LICENSE`, header preserved on all four ported files). Art: **NO LICENCE DECLARED for the two textures shipped** - they are outside VMblast's named list, but nothing states positively whose they are (permission being sought by Ikram) | github.com/TheLacus/daggerfall-unity-mods @ `556ef6e1dd0f2da95aa34275a30861daf58fee86` ("Bump version", 2023-08-25); manifest `RealGrass.dfmod.json`, GUID `2185b00e-bc5d-4758-81f5-7540817e2cbc` | Every VMblast `.psd` (`Grass_tex`, all `GrassDetails_*`, `DesertGrass`); every `.fbx`, `.prefab` and `.mat` - the Classic billboard path wants textures and nothing else; the stone, water-plant and firefly art with the features that read it; `External/RealGrassConsoleCommands.cs` (a desktop console this port has no way to reach); `modsettings.json` and `modpresets.json`, which live in the code root and are unreachable from the asset root the fetch reads |

**What ships is two 256² PNGs**: `BrownGrass_tex.png` (25,073 bytes) and `GreenGrass_tex.png`
(40,306 bytes), the Classic set. Their `.meta` carries a default-platform `maxTextureSize` of 128,
which is upstream's own setting and is easy to misread as the shipped size - but the iOS entry is
`overridden: 1` (ASTC 6x6, `maxTextureSize` 4096), written by `MobileModPackTextureImporter`
(`Assets/Editor/MobileModBuilder.cs`), and **a platform override wins over the default rule**. So on
the only platform this project builds they import at their full **256²**; the 128 would apply only to
a desktop player, which is not shipped. Neither appears in VMblast's list, and the history says they predate
his contribution - both were added in 2017 and last touched in 2018, where the changelog dates
VMblast's textures to 2.3 and 2.11, and the green one has a changelog line in the original author's
own voice ("Improved the green grass texture (I'm no artist but I try)"). That is a strong lead and
it is not a licence, so the same rule as the WoD family applies: `private_only: true` with a
`pending:` record in `tools/bundled-mods/mods.json`, the bundle rides the private test draft only,
and `pack.py` keeps `realgrass.dfmod` out of the public MIT mod pack. One line from TheLacus or
Uncanny_Valley would flip it; so would replacing the two textures with our own, which at 256² is
cheap. The evidence is in the `licence` string so a future reader need not redo the archaeology.

The MIT text below is the one that matters even on a build with no bundle at all, because **the code
is compiled into the app**: the app is a distribution of it, and MIT requires this notice to travel
with it. The bundle's own licence record in `tools/bundled-mods/mods.json` quotes the copyright line
and summarises the grant in prose, and every ported file keeps upstream's header - but a summary is
not the permission notice, which is why the verbatim text lives here.

```
MIT License

Copyright (c) 2016-2019 Uncanny_Valley, TheLacus

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**The port runs one configuration, and it is the cheap one.** Upstream's shipped defaults are the
`Full` style with prototype meshes, water plants, stones and a 120 m detail distance; this build
forces **Classic** style with **billboards** (no FBX prototypes, no Standard-shader meshes, so
nothing the bundle does not contain is ever asked for), stones off, water plants off, fireflies off.
The detail store is `SetDetailResolution(128, 16)` where upstream forces `(256, 8)`: **64 detail
patches per terrain instead of 1,024, and a quarter of the resident detail data**. The number to hold
on to is that 128 at 16 per patch is not finer than DFU - it is **exactly DFU's own store**, which
`DaggerfallTerrain` creates as `SetDetailResolution(heightmapDimension, 16)` with a heightmap
dimension of 129, i.e. a 128² store in 64 patches. So the win is 16x against upstream, and parity
with the terrain the game already builds; the guard that would resize the store normally finds it
already the right shape and does nothing. Upstream's `new int[256,256]` per layer per promotion is
gone too: the layers are allocated once and cleared with `System.Array.Clear`, so a map-pixel
crossing that promotes a ring of terrains allocates nothing.

Folding a 256-space mod onto a 128-space store is the one place where the port's arithmetic differs
from upstream's rather than merely being cheaper, and it is why the scatter mode is now **set
explicitly**. Unity has two: `CoverageMode`, where a sample is how much ground the detail covers
(ceiling 255, resolution-independent), and the legacy `InstanceCountMode`, where it is an instance
count with the legacy per-cell ceiling of 16 - the ceiling upstream itself ran under, so its authored
thick range of 6..20 was effectively 6..16 for every player it ever had. DFU builds its terrain
data with a bare `new TerrainData()` and never sets the mode, so it was whatever the engine
defaulted to - and the two modes are 16x apart in what a value MEANS. `CoverageMode` is chosen
because it is resolution-independent, because nothing at Classic densities gets near its ceiling,
and because an explicit mode beats an inherited default; the port calls
`SetDetailScatterMode(CoverageMode)` once per `TerrainData`, before any `SetDetailLayer`, and
**averages** the four upstream sub-cell writes into the one cell they now share - rounded to nearest
rather than floored, so a thin single write does not vanish. Under coverage semantics a value is how
much of the cell's ground the grass covers, so the mean is parity **across the resolution change**:
the same coverage on the same ground as upstream's four cells put there. Summing them, which an
earlier revision did, would have been four times that on the platform whose whole reason for this
port is cost.

What the fold does *not* settle is absolute density, and the two are easy to run together. Upstream
authored those numbers as counts ("number of grass patches per terrain tile") and they now reach
Unity as coverage fractions of 255, which Unity converts to billboards natively - a re-denomination
no amount of reading can verify. It is a device question, and the dial for it is `DetailDensity`
(0.6, which becomes Unity's `detailObjectDensity`), one constant that moves density without touching
the fold or any write site. The mode and the ceiling are named in the memory line, so a log settles
that half rather than a document.

Two upstream bugs are fixed in passing, both forced by what the bundle holds. `UpdateClimateDesert`
asked for a `DesertGrass_tex` asset that **does not exist in any version of the mod** (the desert art
is a `.psd` named `DesertGrass`), so upstream drew no desert grass at all in billboard style and
logged a failed load on every climate change; the port points desert at the brown grass, which
`RefreshDesert` recolours anyway. And `ResetColor(DetailPrototypes[WaterPlants])` ran unconditionally
in `UpdateClimateSummer` - with water plants off, `WaterPlants` is layer index **0**, the grass layer,
so that line was resetting the grass prototype to grey on every season change, undoing the seasonal
colours set moments earlier. It is now behind `if (options.WaterPlants)`.

Three **engine** shaders had to be pinned, and this is the part that is invisible until it fails: the
detail renderer draws through `Hidden/TerrainEngine/Details/Vertexlit`, `.../WavingDoublePass` and
`.../BillboardWavingDoublePass` (note the lower-case `l` in `Vertexlit`), no scene or prefab in this
project references them, and an IL2CPP player build is therefore free to strip them - after which
grass renders as nothing at all, with no error. They are in `EnsureAlwaysIncludedShaders` and in
`ProjectSettings/GraphicsSettings.asset`, and an Always-Included entry pins every variant, so no
`.shadervariants` entry is needed (nor easily written: a built-in shader has no project GUID and
could only be named there by a fileID into `unity_builtin_extra`). `MobileShaders.names` is
deliberately unchanged - these are engine shaders, so there is no captured copy to prefer and no mod
bundle that could shadow them by name.

The two dials are `RealGrassPort.DetailDistance` (40 m, upstream 120) and `RealGrassPort.DetailDensity`
(0.6, upstream 1.0), public static fields rather than constants because there is no settings file in
the bundle to read them from - `modsettings.json` lives in the code root the fetch cannot reach, so
nothing in the port calls `mod.GetSettings()` and nothing can throw on its absence. Every other value
upstream's settings file supplied is hard-coded from that file's own shipped defaults.

Not device-verified at the time of writing. The gate declines with
`[RealGrass] not available: <reason>` when any of the three shaders is unresolved or the grass
textures are missing (which is exactly what a public build with no bundle looks like), the promotion
handler is wrapped so a throw leaves DFU's empty detail layers rather than escaping into world
streaming, and the cost is logged rather than asserted: `[RealGrass] detail data ~N MB (...)` once and
`[RealGrass] details on N terrains` every twenty-fifth promotion. Memory grows with the number of
live terrains, so the measurement to take is after fast travel with Distant Terrain and WoD Terrain
also on.

## Mobile CRT filter (ours, no third-party code)

There is no CRT mod for Daggerfall Unity, and nothing was ported to make one. The filter is
`Assets/Shaders/Mobile/MobileCRT.shader` (181 lines, shader name `Daggerfall/Mobile/CRT`),
`Assets/Scripts/Game/Mobile/MobileCrt.cs` and
`Assets/Scripts/Game/UserInterfaceWindows/CRTConfigPage.cs` - all three written for this port and MIT
licensed on the same terms as the rest of it. The retro presentation it hangs on
(`RetroPresentation.cs`, 320x200 / 640x400, the VGA palette, the 4:3 stretch) is Daggerfall Unity's
own, MIT, copyright (c) 2009-2023 Daggerfall Workshop.

This section exists to record a *negative*: **no code was copied from any existing CRT shader.** The
well-known ones - crt-pi, crt-geom, crt-easymode, crt-royale - are GPL, and a GPL shader compiled
into an MIT app relicenses the app. The four techniques here are textbook and were written from the
formulas: a barrel warp of the sampled UV (`uv *= 1 + k * dot(uv, uv)` about the centre, with
everything outside the warped rectangle black), raised-cosine scanlines evaluated against the source
raster, a three-band phosphor grille taken from the destination pixel's own x coordinate, and a
radial vignette. One texture fetch, one sampler, no dependent read.
