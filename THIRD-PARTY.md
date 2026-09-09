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
| World of Daggerfall 0.4.0 | World of Daggerfall Team (KABoissonneault, Cliffworms, Kamer, carademono); NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/world-of-daggerfall @ 3bf8837 | WoD Terrain (a separate mod, out of scope - it needs compute shaders and synchronous readbacks), Distant Terrain, and the three optional dependencies Wilderness Overhaul, RMB Resource Pack and Beautiful Villages. WoD Biomes is a separate mod too, and is now shipped - see its own section below. Its one script, `WODRocksMaterials.cs`, is compiled in under `Ports/WorldOfDaggerfall/`; its data is the bundle |

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
exist in vanilla `arena2`, so no engine change was needed for them. `NatureBatchOverrider` (in
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
upstream licence. The bundle is `world of daggerfall - biomes.dfmod` (the lower-cased manifest file
name, `World of Daggerfall - Biomes.dfmod.json`; GUID `3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`). Without
that file there is no `World of Daggerfall - Biomes` entry, the compiled code never runs, and deleting
it removes every byte the feature added.

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
`[Biomes] swapped N type-5 nature flats to archive 10030`.

Not device-verified at the time of writing. Unlike WoD this mod places no objects - it changes which
textures existing ones use - so the performance question is narrower, but the nature swap does run on
every terrain-streaming update; it is bounded by the archive filter that skips any batch already
swapped, a per-archive material cache and a 1024 atlas cap (upstream allocated 4096, a transient
~85 MB spike), and its whole
body is wrapped so a failure logs once per distinct message rather than once per frame
(`[Biomes] nature swap failed: ...`). The lines a tester should look for are
`[PortedMods] started World of Daggerfall - Biomes` and, once terrain has streamed in,
`[Biomes] swapped N nature batches to archive 10030`.
