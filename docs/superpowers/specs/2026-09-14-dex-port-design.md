# Daggerfall Enemy Expansion (DEX) on iOS: compiled in, full content, private draft only

Date: 2026-09-14. Approved in conversation (Ikram: "next is this mod ... use opus to build"; "ignore licenses we will figure it out when we
distribute"). Fable plans and rules; Opus implements. Research: `~/dev/dfu-mods/dex-research.md`.

## What the mod is
DEX 1.3.4 by Kab (code) and Kamer (art), source at github.com/SquidKamer/DaggerfallBestiaryProject (manifest
`DaggerfallBestiaryProject.dfmod.json`, title `Daggerfall Enemy Expansion`, DFU floor 1.0). 10 C# files (~176 KB; `Scripts/Editor/` is
editor-only), six CSV databases in DEX's own `.mdb/.cdb/.tdb.csv` formats (spec: KABoissonneault/DEX-sans-spiders README, Unlicense),
`SpellRecords.json`, one prefab (troll corpse), 8,031 PNG sprite frames (26 MB) in 80 texture archives. 50 new enemies (35 monsters ids
256-383, 15 class enemies 384-511), 43 careers, 39 rewritten encounter tables, 16 re-statted vanilla monsters. No custom shaders or audio,
no mod dependencies. Every DFU API it uses exists in our 1.1.1 (EnemyBasics.Enemies resize, RegisterCustomCareerTemplate,
FormulaHelper.RegisterOverride, RandomEncounters.EncounterTables, QuestMachine.FoesTable, TextureReplacement lazy sprite import).
Licence: none declared; much of the art is derived from other Bethesda titles -> `private_only` + `pending:` (never the public pack).

## Design
1. **Code compiled in** under `Assets/Scripts/Game/Mobile/Ports/DEX/` (all Scripts except `Editor/`), `[Invoke]` removed, started by
   `MobilePortedMods.StartEnabled` via the 4-arg `StartOne`. Data (CSVs, SpellRecords.json, prefab, textures) comes from the bundle mod
   `Daggerfall Enemy Expansion` resolved through `ModManager` exactly as the other strip_code ports do; the addon scan over other mods'
   `.mdb/.cdb/.tdb.csv` is kept (it is how DEX-sans-spiders style overrides work). Save data (`BestiarySaveInterface`, troll corpses) uses
   the bundle mod's `SaveDataInterface` the way the Climates & Calories port does; if that path is not available to compiled-in code,
   the troll-corpse persistence is dropped for pass 1 and the corpse billboard falls back to a plain corpse (documented).
2. **Full content**: monsters, classes, careers, encounter-table replacement and the classic re-stats all ON (that is the mod; without
   the tables no new enemy ever appears outside quests). `DisableTrollRespawn` stays the one mod setting.
3. **Boot-time gate, default OFF**: launcher entry `Daggerfall Enemy Expansion`; the flag is read once at start-up because the mod
   resizes `EnemyBasics.Enemies` and rewrites the encounter tables. Changing it shows "takes effect after restart". Saves that contain
   DEX enemies need DEX on: the launcher hint says so, and loading such a save with DEX off logs one clear warning (DFU already
   tolerates unknown enemy ids by skipping them - verify and state).
4. **RR-Items compatibility**: `DEX_RRICompat.cs` kept, re-pointed at our `Ports/RoleplayRealismItems` module flags instead of
   `ModManager` settings; its template indices 513-525 must match what our RR-Items port registers (assert in a self-test).
5. **Data pipeline**: mods.json entry `DEX` (repo above, `strip_code`, `private_only`, `pending:` text stating no licence + the
   third-party-art provenance + permission being sought from Kab and Kamer, `exclude_globs` for `Editor/*`, `*.meta`); textures at ASTC
   6x6, no mips unless the DFU sprite path expects them (match how DFU imports loose enemy PNGs: point filter is applied at runtime by
   `TextureReplacement`, so import settings only need correct alpha and no sRGB surprises); one bundle `dex.dfmod`; the importer rule
   must not make 8,031 sprites readable.
6. **Verification**: self-tests (gate, CSV parse of all six databases against the shipped copies -> 50 enemies / 43 careers / 39
   tables, enemy id ranges, RRI index match, no `[Invoke]`, launcher title == manifest title); test-app debug command `spawn <id|name>`
   (gated `#if DFU_IOS_TESTAPP`) to place an enemy in front of the player; simulator run: DEX on, spawn three monsters and one class
   enemy, screenshot with their sprites, log shows `[DEX] loaded 50 enemies, 43 careers, 39 tables` and no red errors; a dungeon visit
   shows DEX names in the encounter log; first-spawn stall measured (log the ms of the first sprite import per archive). Device ipa.

## Out of scope
Bestiary DEX Compat (GPL), DEX-DREAM patch, MUDEX/RUDEX rebalances, Horrible Hordes, pre-warming archives (measure first).
