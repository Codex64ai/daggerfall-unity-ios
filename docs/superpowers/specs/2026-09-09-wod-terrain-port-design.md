# World of Daggerfall - Terrain on iOS: GPU terrain sampler compiled in, maps as a private bundle

Date: 2026-09-09. Approved in conversation (Ikram: "World of Daggerfall biomes, terrain, and distant terrain should be
working as well. Go ahead and build those"; design decision confirmed: keep the mod's synchronous per-tile readback for
version one and let the device test judge hitching). Second of three cycles (Biomes done, Distant Terrain next).
Fable plans and rules; Opus subagents implement and verify. Research: `docs/superpowers/research/2026-09-09-wod-terrain.md`.
Licence: none anywhere in the lineage (monobelisk, Freak2121, carademono, Ninelan); Ikram handles permission. Private
draft only (`private_only` + `pending:` licence; `pack.py` refuses a pending licence in the public pack).

## What the mod is

github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40 (tip of main, "PortCoastlines").
Manifest `wod-terrain.dfmod.json`, title `World of Daggerfall - Terrain`, version 1.5.0, GUID
`a9091dd7-e07a-4171-b16d-d13d67a5f221`. A replacement `ITerrainSampler` (namespace `Monobelisk`, from Interesting
Terrains): 21 runtime C# files (1,634 lines, one `[Invoke(Start,0)]` = `InterestingTerrains.Init`), two used compute
shaders (`TerrainComputer.compute` with kernels `TerrainComputer`/`TilemapComputer`, `MainHeightmapComputer.compute`) plus
four `.cginc`, five PNG world maps (deriv/road/port/biome 5000x2500 clamped to 2048 on import, tileable noise 128x128) and
one INI text asset of noise parameters. No keywords, no `#pragma target`, no reflection, no threads, no `unsafe`, no file IO
at runtime; `IniParser` is already in the project. Optional dependency `basicroads` (compiled in already). Independent of
WoD locations, Biomes and Distant Terrain (WoD locations place correctly without it; its `PortCoastlines` heightmap is
what WoD's 382 docks/lighthouses were tuned against, so coasts look "as authored" only with it on).

At start it dispatches `MainHeightmapComputer` over the whole 1000x500 world and replaces
`ContentReader.WoodsFileReader.Buffer` (the small world heightmap the travel map and Distant Terrain read); per terrain
tile it dispatches both kernels (13x13 groups of 10x10) and reads the 129^2 heightmap and tilemap back synchronously on the
main thread; it flattens/blends terrain around every location in a 33x33 map-pixel window on the GPU
(`IsLocationTerrainBlended() => true`); sets `StreamingWorld.TerrainScale = 1`, `Camera.main.farClipPlane = 10000`,
`MaxTerrainHeight` 5000, `OceanElevation` 100.01, sampler `Version` 7.

## Design

1. **Code compiled in**: `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/` - the 21 runtime files minus
   `ConsoleHandler.cs` (dev console commands; dropped) and `HeightmapResolution`/dead `WOModEnabled` left as is; editor and
   testing files excluded. `[Invoke]` removed; started by `MobilePortedMods.StartEnabled` via `StartOne`, AFTER Location
   Loader/WoD/Biomes (order is irrelevant to them; keep it last so a failure here cannot precede theirs). Port headers; every
   edit `// MOBILE`. Compute shaders compiled into the app: `Assets/Resources/WoDTerrain/{TerrainComputer,MainHeightmapComputer}.compute`
   + the four `.cginc` beside them (`MainHeightmapSmoother.compute` is dead - not shipped); `mod.GetAsset<ComputeShader>(name)`
   -> `Resources.Load<ComputeShader>("WoDTerrain/" + name)`; delete `#pragma exclude_renderers d3d11 gles` from `basicRoads.cginc`.
2. **Mandatory correctness fixes** (each a `// MOBILE` edit, each with the smallest pure test possible):
   a. Start-up dispatch chunked: `MainHeightmapComputer` runs as R row-bands (e.g. 10 bands of 50 rows = 50,000 threads
      each) with a `GetData` per band into the same `float[500000]` (one Metal command buffer per band - none can hit
      iOS's execution limit); pure helper `static (int yStart, int rows)[] Bands(int height, int bands)`.
   b. `locationHeightData` buffer sized to the shader's array (1089) - or the writes deleted; ruling: size to 1089 and keep
      the writes verbatim (no shader edit). Pure check on the constant.
   c. `ComputeShader` clone: instantiate ONCE (a `static` per kernel asset) and reuse; `Cleanup()` releases it.
   d. `SetVectorArray` guards: when `locations.Count == 0`, pass a 1-element zero array and `locationCount = 0`.
   e. `TileDataCache.Add` -> indexer assignment (AsesinoBlade's guard).
   f. `locationRectCache` also caches misses (`Dictionary<int, LocationRect?>`), so the 33x33 sweep costs ~1,000
      dictionary hits per tile, not ~1,000 `GetMapPixelData` calls.
   g. Capability + failure containment: if `!SystemInfo.supportsComputeShaders` or any compute asset fails to load, Init
      logs `[WoDTerrain] not available: <reason>` and returns WITHOUT replacing the sampler (vanilla terrain). The per-tile
      path is wrapped: an exception during `GenerateSamples` logs once per message and falls back to the default sampler's
      output for that tile (call `base`/a held `DefaultTerrainSampler` instance) so one bad tile is a hole in fidelity, not
      a crash. Buffers released in `finally`.
   h. Timing evidence: `[WoDTerrain] world heightmap <ms> ms (<bands> bands)` once, and `[WoDTerrain] tile <x>,<y> <ms> ms
      (locations <n>)` for the first 10 tiles then every 25th - Ikram's Player.log becomes the perf measurement.
3. **Data pipeline**: mods.json entry `WorldOfDaggerfallTerrain` (`strip_code`, `private_only`, `pending:` licence,
   manifest files only - the repo's 212 MB of `.xcf`/`WOODS.WLD` are not in the manifest; `drop_dependencies: ["basicroads"]`
   because Basic Roads is compiled in and has no bundle FileName). Bundle `wod-terrain.dfmod` (manifest name has no
   spaces - GitHub-safe) holding the 5 PNGs + INI (~7 MB). NEW importer rule kind `LinearData` in
   `MobileModPackTextureRules` for folder `WorldOfDaggerfallTerrain`: `sRGBTexture = false` (data maps read as numbers; the
   project is Linear), uncompressed RGBA32 on iPhone, `maxTextureSize 2048`, mips and filter as upstream metas, not readable.
   Memory ~43 MB GPU for the four maps (accepted for v1; ASTC for the two bilinear maps is a later trade).
4. **Launcher entry**: `World of Daggerfall - Terrain`, default OFF via `Titles`; no dependency gate (Basic Roads optional).
   Docs and the entry's log line say plainly: switching it changes the ground height under existing saves
   (`MaxTerrainHeight` 2308.5 -> 5000, `OceanElevation` 27.2 -> 100.01) - decide per save, start a new game or accept
   repositioning; and that the travel/region maps change too (the small world heightmap is rewritten by design).
5. **Verification**: self-tests (bands, buffer size, caches, gate, importer rule); simulator run (Metal compute works in the
   iOS Simulator on Apple silicon): with the entry on, expect `[WoDTerrain] world heightmap N ms`, tile lines, no
   `[WoDTerrain] ... failed`, visibly different terrain vs off at pixel 207,213 (Daggerfall) and a coastal WoD dock pixel
   (370,350 Naresa), plus Dynamic Skies on alongside (far plane interaction); negative run; then
   `DFU-Test-unity6-terrain.ipa`. Device judges hitching (walk, fast travel) and start-up pause.

## Out of scope
`AsyncGPUReadback` restructuring (v2 if the device shows stutter), a CPU fallback generator, Wilderness Overhaul's
tilemap consumer, Distant Terrain (next spec), licence work.
