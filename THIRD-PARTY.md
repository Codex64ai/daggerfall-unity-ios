# Third-party notices

This repository is Daggerfall Unity (MIT, copyright (c) 2009-2023 Daggerfall Workshop - see
`LICENSE`) plus an iOS touch port (MIT, same terms). Several further works are compiled into the port
rather than loaded as mods - see each section below for its licence status, which is not MIT in every
case and is undeclared for three of them. Their original headers are preserved in the files named.

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
| World of Daggerfall 0.4.0 | World of Daggerfall Team (KABoissonneault, Cliffworms, Kamer, carademono); NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/world-of-daggerfall @ 3bf8837 | WoD Terrain (a separate mod, shipped in its own right since 2026-09-09 - see its section below), Distant Terrain, and the three optional dependencies Wilderness Overhaul, RMB Resource Pack and Beautiful Villages. WoD Biomes is a separate mod too, and is now shipped - see its own section below. Its one script, `WODRocksMaterials.cs`, is compiled in under `Ports/WorldOfDaggerfall/`; its data is the bundle |

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
`daggerfall expanded textures.dfmod`. With that mod off or missing, `World of Daggerfall - Biomes` is
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
placed at several map pixels. The signature of a genuine double swap, which would double a flat's
scale a second time, is **the same block name repeated with the same count**.

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
every terrain tile's 129x129 heightmap and its tilemap are computed by a Metal compute shader
instead of by the engine. The lineage is long - monobelisk's Interesting Terrains, by way of
Freak2121, carademono and Ninelan - and nowhere in it is a licence declared, so the same rule as the
three sections above applies: the C# is compiled in under
`Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/`, the data is an off-by-default entry in
the launcher's MODS window fed by a bundle, and the bundle ships only on the private test draft.

| Mod | Author, licence | Source | What is NOT shipped |
|---|---|---|---|
| World of Daggerfall - Terrain 1.5.0 | monobelisk, Freak2121, carademono, Ninelan (from monobelisk's Interesting Terrains); NO LICENCE DECLARED anywhere in the lineage (permission being sought by Ikram; not in any public release) | github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40 | the repo's 212 MB of `.xcf` and `WOODS.WLD` authoring files (they are not in the manifest, and the fetch is manifest-only); the editor scripts and `Scripts/Models/Editor/`; `Helpers/ConsoleHandler.cs` (dev console commands, useless on a device) and the `ClearNoonRoutine` that existed only for its `clearnoon` command; the dead `MainHeightmapSmoother.compute`. The 20 remaining runtime files (+2,384 lines as ported) are compiled in under `Ports/WorldOfDaggerfallTerrain/`; the two live compute shaders and their four `.cginc` (6 files, +2,557 lines) are compiled into the app under `Assets/Resources/WoDTerrain/`; the five PNG world maps and the noise-parameter INI are the bundle |

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

**Road smoothing is inert in this build.** `BasicRoadsUtils` asks `ModManager` for a *mod titled*
`"BasicRoads"` and then sends it a `getPathData` message; this port has no such mod - Basic Roads is
compiled into the app as the "Roads & tracks" feature, with no message receiver - so
`CompatibilityUtils.BasicRoadsLoaded` is always false, `GetRoadData` returns its zeroed arrays and
the shader's road-flattening pass has nothing to flatten. The `daggerfall_road_map.png` mask that
says *where* roads may be smoothed is still bound and still sampled; it just never has any direction
data to work with. The visible cost is that roads on WoD Terrain's steeper hills are not levelled
into the slope. The follow-up is to wire `BasicRoadsUtils` to the compiled-in road network directly
rather than through the mod-message protocol; it is not done, and nothing about it is guessed at
runtime.

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
