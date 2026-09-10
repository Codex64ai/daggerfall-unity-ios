# Real Grass on iOS: compiled in, Classic billboards only, reduced density, off by default

Date: 2026-09-10. Approved in conversation (Ikram: "Do both go! use opus as the builder fable as the planner"). Fable plans and
rules; Opus subagents implement and verify. Research: `docs/superpowers/research/2026-09-10-grass-and-crt.md` Part A.

## What the mod is
Real Grass 2.11 by Uncanny_Valley & TheLacus - github.com/TheLacus/daggerfall-unity-mods (archived), folders `RealGrass/`
(code, 5 files, 83 KB, one `[Invoke(Start,0)]` = `RealGrass.Init`) and `RealGrassAssets/` (28 MB of source art, of which the
shipping subset for the Classic style is a few textures). Manifest `RealGrass.dfmod.json`, title `Real Grass`, GUID
`2185b00e-bc5d-4758-81f5-7540817e2cbc`. Licence: code MIT (repo LICENSE); the "realistic" textures are a third-party pack
(VMblast) with unclear terms -> NOT shipped; the Classic textures are CC0 (repo credits.txt) -> shipped. Publishable in the
MIT pack once the licence records are verified in Task 1 (else `private_only` like the WoD family - Task 1 decides from the files).
It hooks `DaggerfallTerrain.OnPromoteTerrainData` and fills Unity Terrain DETAIL layers (`SetDetailResolution`,
`SetDetailLayer`, `detailPrototypes`) - the only DFU mod using Unity's terrain detail renderer. No shaders of its own; it relies
on Unity's built-in `Hidden/TerrainEngine/Details/{Vertexlit,WavingDoublePass,BillboardWavingDoublePass}` which an iOS player
build STRIPS unless pinned. Takes none of DFU's four terrain slots: additive with Basic Roads, Biomes, WoD Terrain, Location Loader.
Zero API gaps against our DFU 1.1.1 (checked symbol by symbol).

## Design
1. **Code compiled in**: `Assets/Scripts/Game/Mobile/Ports/RealGrass/` (RealGrass.cs, DensityManager.cs,
   DetailPrototypesManager.cs, Range.cs; `External/RealGrassConsoleCommands.cs` dropped). `[Invoke]` removed; started by
   `MobilePortedMods.StartEnabled` via the 4-arg `StartOne` (`RealGrassPort.Installed` true after the terrain hook subscribed).
   MOBILE edits: `SetDetailResolution(256, 8)` -> `(128, 16)` (DFU's own patch density, 4x its detail resolution; quarter the
   resident data, 1/16 the patches); the `int[256,256]` per-layer allocations become cached `int[128,128]` arrays cleared with
   `Array.Clear` (no per-promotion allocation); style forced to **Classic** with **Billboard = true** (no FBX prototypes, no
   Standard-shader meshes), Stones off, WaterPlants off, FlyingInsects off, `DetailDistance` default 40 m (dial), `DetailDensity`
   default 0.6 (dial) - both readable from the mod's settings so the player can raise them; `wavingGrassTint` kept.
   Failure containment: the promotion handler wrapped (log once per message `[RealGrass] terrain details failed: <ex>`, terrain
   keeps DFU's empty detail layers); `Installed` false and `[RealGrass] not available: <reason>` when the three detail shaders are
   not found at runtime (`Shader.Find` of all three must be non-null) or the grass texture is missing from the bundle.
2. **Shaders pinned**: the three `Hidden/TerrainEngine/Details/*` shaders added to `EnsureAlwaysIncludedShaders` (GraphicsSettings
   pin, committed) and `RequiredShaderVariants.shadervariants` where they carry keywords; self-test asserts the pin.
   `MobileShaders.names` unchanged (these are engine shaders, not mod shaders).
3. **Data**: mods.json entry `RealGrass` with `subdir: RealGrass` (monorepo), `strip_code`, `exclude_globs` for `*.psd`, `Rock*.png`,
   `*.fbx`, `*.prefab`, `*.mat` (Classic billboards need textures only - Task 1 confirms from DetailPrototypesManager which asset
   names the Classic+Billboard path loads and keeps exactly those + the manifest); licence record from the repo LICENSE (MIT) +
   credits.txt; RawData importer rule NOT needed (grass textures are sampled, ASTC is fine), but `filterMode` and alpha must import
   as upstream metas say. Bundle `realgrass.dfmod` (~< 1 MB).
4. **Launcher**: `Real Grass` default OFF via `Titles`; no dependency gate. Docs: what it costs, the two dials, that it stacks with
   Distant Terrain and WoD Terrain (memory grows with live terrains; watch after fast travel), that grass appears only on terrain
   tiles promoted after the switch is on.
5. **Verification**: self-tests (gate, forced style constants, detail resolution/patch maths, pins); simulator at 207,213 with
   Terrain + Distant + grass on: `[PortedMods] started Real Grass`, `[RealGrass] details on N terrains` counter line, screenshot with
   visible grass billboards in the near ground; memory line `[RealGrass] detail data ~N MB`; negative run; device ipa
   `DFU-Test-unity6-grass.ipa` and install if the iPad is connected. Device judges the frame time on tile crossings.

## Out of scope
Mixed/Full styles, stones, water plants, fireflies, VMblast textures, Vibrant Wind, Wilderness Overhaul.
