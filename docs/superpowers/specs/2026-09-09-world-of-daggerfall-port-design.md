# World of Daggerfall on iOS: Location Loader compiled in, WoD data as a private bundle

Date: 2026-09-09. Approved in conversation (Ikram): scope = Location Loader + World of Daggerfall
locations only, both off by default; WoD Biomes, WoD Terrain and Distant Terrain are out of scope.
Licences: neither repo declares one; Ikram is handling permission. Private draft only, never the
public pack (enforced by `private_only` in the pack tooling), exactly as DREAM and Dynamic Skies.

## What the two mods are

- Location Loader (LL), github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
  (title `Location Loader`, GUID `fc5c0fa6-e80d-4cb1-89fa-c10be8e35bf3`, version 0.3). Pure code: 11 runtime
  C# files (~420 KB) plus 3 editor files; its manifest ships NO data. `LocationModLoader.Init(InitParams)`
  (`[Invoke(Start,0)]`) creates a `LocationLoader` GameObject with `LocationLoader`, `LocationSaveDataInterface`
  (= `mod.SaveDataInterface`) and `LocationResourceManager`, sets `mod.MessageReceiver`, replaces
  `DaggerfallUnity.Instance.TerrainNature` with `LocationTerrainNature`, registers a ladder activation.
  It discovers location data by scanning EVERY enabled mod's `AssetBundle.GetAllAssetNames()` for
  `assets/game/mods/<folder>/locations/**` `.txt`/`.csv` (case-insensitive; folder name arbitrary) and
  loads prefab definitions and Unity prefabs from that mod. Hooks `DaggerfallTerrain.OnPromoteTerrainData`,
  `StreamingWorld.OnInitWorld/OnUpdateTerrainsEnd`.
- World of Daggerfall (WoD), github.com/drcarademono/world-of-daggerfall @ 3bf8837402cce41421dcb36c64a3a2c4f27d0448
  (title `World of Daggerfall`, version 0.4.0, GUID `ed0c8c07-a6ce-4d7f-8072-f5a88916c830`). Data: 866
  manifest files (185 csv, 314 txt prefab definitions, 137 json runtime materials, 121 Unity prefabs,
  63 materials, 44 png ~35 MB, 23 WorldData) + ONE script `Scripts/WODRocksMaterials.cs`
  (`[Invoke(Start,0)] Init(InitParams)`; climate/season rock materials; looks up optional mods by GUID
  and tolerates their absence). The prefabs reference `Meshes/*.fbx|.dae` (301 files, 3 MB) by GUID and
  those meshes are NOT in the manifest - Unity pulls them into the bundle as dependencies when building
  from source, so they must be present in the project folder with their .meta files. Dependencies:
  `location loader` (required), `daggerfall expanded textures` (required; already in our pack as the
  pre-built bundle `daggerfall expanded textures.dfmod`), `wilderness overhaul`, `rmb resource pack`,
  `beautiful villages` (optional; not shipped).

## Design

1. Code compiled in, like the survival mods: `Assets/Scripts/Game/Mobile/Ports/LocationLoader/` (the 11
   runtime files; editor files excluded) and `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfall/WODRocksMaterials.cs`.
   Edits: `[Invoke]` removed; anything else only to compile, marked `// MOBILE`. Port header on every file
   (source, commit, "no licence header upstream; private draft only").
2. Launcher entries. `Location Loader` is a BUILT-IN entry registered by `MobileMods.Register` (like the
   TravelOptions bridge) with LL's upstream GUID, default OFF, description saying it does nothing without a
   location mod. `World of Daggerfall` is the data bundle's own entry, default OFF (via `MobilePortedMods.Titles`),
   gated on `Location Loader` being on and on Daggerfall Expanded Textures being installed and on (pure
   `WodRuns(locationLoaderStarted, wodOn, detOn) = locationLoaderStarted && wodOn && detOn`; auto-off +
   description note when LL is off, and a second gate with its own note when DET is off or missing, both
   mirroring the C&C gate). `MobilePortedMods.StartEnabled` calls `LocationModLoader.Init` at the Start
   state (it needs no scene objects) and `WODRocksMaterials.Init` after it only when that `Init` returned
   and both dependencies are satisfied.
3. Data pipeline. mods.json entry `WorldOfDaggerfall`: `strip_code` (drops WODRocksMaterials.cs), `private_only`,
   `pending:` licence, `archives_from: ["DaggerfallExpandedTextures"]` (WorldData blocks may reference DET
   archives), `drop_dependencies: ["location loader", "wilderness overhaul", "rmb resource pack", "beautiful villages"]`
   (LL is built in and has no FileName; the others are not shipped) - the DET dependency stays. NEW fetch flag
   `extra_dirs: ["Meshes"]`: copy those repo folders (with .meta) into the mod folder even though the manifest
   does not list them, so prefab GUID references resolve at bundle build. The bundle is `worldofdaggerfall.dfmod` -
   `MobileModBuilder` derives the name from the manifest file name (`WorldOfDaggerfall.dfmod.json`) and
   Unity lower-cases it - shipped on the private draft only.
4. Failure handling: each compiled-in mod's Init runs through `MobilePortedMods.StartOne(title, init)`,
   which holds its own try/catch, so one mod throwing costs only that mod (the Dynamic Skies entry is
   resolved before any Init runs, so it keeps its deferred start). WoD without LL is switched off with a
   note; WoD needs Daggerfall Expanded Textures, and if that mod is off or missing WoD is switched off at
   start-up too and its description in MODS says why (DFU's own dependency check only warns, so this port
   gates it) - both documented.
5. Verification: self-tests (gate, fetch flag, compile); simulator run with LL+WoD+DET installed (RGBA32
   rebuild of the WoD bundle for the sim; DET stays ASTC since only its presence matters) at a wilderness
   pixel near Daggerfall, expecting `[LL]` log lines and WoD instances, screenshot; then a device ipa
   (`DFU-Test-unity6-wod.ipa`) for Ikram, who reads the diagnostics frame time and looks for hitching while
   travelling. Performance is the acknowledged unknown (~200,000 placed instances world-wide); the switch is
   the mitigation.

## Out of scope
WoD Biomes (separate code mod), WoD Terrain (compute shaders + synchronous readbacks), Distant Terrain,
the optional dependency mods, any licence work.
