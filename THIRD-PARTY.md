# Third-party notices

This repository is Daggerfall Unity (MIT, copyright (c) 2009-2023 Daggerfall Workshop - see
`LICENSE`) plus an iOS touch port (MIT, same terms). Two further MIT-licensed works are compiled
into the port rather than loaded as mods. Their original headers are preserved in the files named.

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

Thirty-one Daggerfall Unity mods are built into iOS `.dfmod` bundles and published as a mod pack
alongside each release (and may ship inside the app), each switchable in the launcher's MODS window.
Ten are by **Cliffworms** (MIT); twenty-one are by **Jay_H** (redistributed with his permission). All are MIT licensed (`Copyright (c) 2025
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

Not included yet: **Detailed Main Quest Dungeons**, **Main Quest Consequences** and **Detailed
Dungeon Exteriors** reference texture archives and models from Daggerfall Expanded Textures and
Decor & Miscellanea; DET's author has since given permission, so these return once DET itself is
converted and bundled; without them a block's flats throw during
layout and the whole dungeon fails to build (verified 2026-09-01). `tools/bundled-mods/fetch.py`
now rejects any block that references a non-vanilla texture archive or a required dependency the
pack does not ship.
