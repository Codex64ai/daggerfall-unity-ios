# World of Daggerfall – Terrain: iOS port research

Date: 2026-09-09. Read-only research, no code written. Companion to
`docs/superpowers/specs/2026-09-09-world-of-daggerfall-port-design.md`, which declared this mod
out of scope ("compute shaders + synchronous readbacks"). This document tests that judgement.

## 0. Source and pin

| | |
|---|---|
| Repo | `github.com/drcarademono/wod-terrain` |
| Pinned commit | **`9aeb1bcc5de5343ccb7a6b09062559ad871e9d40`** ("Merge pull request #4 from drcarademono/PortCoastlines", Thu 15 May 2025) — tip of `main`, only branch, 63 commits, all authored `drcarademono` |
| Manifest | `wod-terrain.dfmod.json` — `ModTitle` "World of Daggerfall - Terrain", `ModVersion` 1.5.0, `GUID` `a9091dd7-e07a-4171-b16d-d13d67a5f221`, `ModAuthor` "monobelisk, Freak2121, carademono, & Ninelan", `DFUnity_Version` 1.0.0 |
| Upstream lineage | A modification of **Interesting Terrains** by monobelisk (`github.com/monobelisk/DFUnity-InterestingTerrains`, last pushed 2020-11-21, no licence), "intended to work in tandem with Enhanced and Eroded Terrains" (`github.com/Freak2121/Enhanced-ErodedTerrains`). Namespace is still `Monobelisk`. |
| Forks checked | `AsesinoBlade/wod-terrain` @ `76623bb2aebc66a7b25a8a9621b3e35d69d41917` (2026-07-09, "removed all editor scripts and set up project as a patch" — strips all 7 editor scripts and `NoisePreview.compute`, sets `ModPreCompile`/`ModPatch`, adds per-platform texture overrides incl. `iPhone`, guards `TileDataCache.Add` against a duplicate-key throw); `jet082/wod-terrain-for-baking`; `SquidKamer/wod-terrain` (= upstream tip); `KABoissonneault/wod-terrain` (2024, behind). **No `somestupidgirl` fork of this mod exists** — that account has one Daggerfall repo, `Distant-Terrain-of-the-World-of-Daggerfall` (2026-09-01, no licence), which is a fork of the separate *Distant Terrain* mod, not of wod-terrain. |
| Clone used | `/Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/research-terrain/wod-terrain` |

Two upstream fixes worth taking if we ever port this: AsesinoBlade's `TileDataCache.Add` guard (upstream
`Dictionary.Add` throws on a duplicate map-pixel key, which can happen when a tile is regenerated
before its tileData is consumed) and his editor-script strip (which is the shape our port needs anyway).

## 1. What it does

A **replacement `ITerrainSampler`** that generates the whole exterior heightmap on the GPU with a
compute shader, plus a coarse 4-value tilemap that it hands to *other* mods by mod message.

- `InterestingTerrains.Awake` sets `DaggerfallUnity.Instance.TerrainSampler = new InterestingTerrainSampler()`
  (`MaxTerrainHeight` 5000, `MeanTerrainHeightScale` 5000/255, `OceanElevation` 100.01,
  `BeachElevation` 103.9, `HeightmapDimension` 129, `Version` 7, `IsLocationTerrainBlended() => true`).
- It does **not** replace `TerrainTexturing`. The line
  `DaggerfallUnity.Instance.TerrainTexturing = new WildernessOverhaul.WOTerrainTexturing(true, true)`
  is commented out in both `Awake` and `Start`. So the tilemap the second kernel computes is dead
  unless another mod pulls it: `ModMessageHandler` answers `"getTileData"` with the cached
  `byte[]` (water/dirt/grass/stone per tile) for a map pixel. Wilderness Overhaul is the intended
  consumer (its GUID `2beb90e5-de58-43cf-b61c-46652f5ecbe3` is looked up in the sampler constructor
  into `WOModEnabled` — a field that is then **never read**; dead code).
- It **rewrites DFU's global small heightmap**. At Start it dispatches `MainHeightmapComputer` over the
  whole 1000×500 world, reads it back, and assigns the result to
  `DaggerfallUnity.Instance.ContentReader.WoodsFileReader.Buffer` (keeping the original in
  `originalHeightmapBuffer` and swapping the two around each per-tile basemap sample). That buffer
  is what the travel map, region maps, Distant Terrain and anything else reading `WOODS.WLD` see.
- It bumps `GameManager.Instance.StreamingWorld.TerrainScale = 1f` and
  `Camera.main.farClipPlane = 10000f`.
- Heightmap generation is authored-data-driven, not purely procedural: four world maps (biome,
  derivative, port, road) steer ~13 configurable fractal-noise generators (Swiss/Jordan turbulence,
  IQ mountains, Perlin dunes and bumps) whose parameters come from an INI text asset.
- It flattens and blends terrain around **every** location within ±16 map pixels of the tile, using a
  per-pixel loop over those location rects — that is the "locations sit right" mechanism, and it is
  also the mod's single most expensive piece of work.
- `basicroads` is an optional dependency: if Basic Roads is loaded, the mod pulls its path bitfields by
  mod message and passes 9 map pixels' worth of road flags to the shader so roads get smoothed.

## 2. Code inventory

21 runtime `.cs` (manifest) + 7 editor `.cs` + 1 testing `.cs`. **1,960 lines** of `.cs` counting
everything the repo has under `Scripts/`, `Misc/` and `Testing/`; **2,519** including the "Noise Params"
subfolder files (the shell glob in the first count missed them — the true totals are below).

### Runtime (in the manifest) — 1,634 lines

| File | Lines | Notes |
|---|---|---|
| `Scripts/InterestingTerrains.cs` | 114 | the mod entry; the only `[Invoke]` |
| `Scripts/InterestingTerrainSampler.cs` | 52 | the `TerrainSampler` subclass |
| `Scripts/Models/TerrainComputer.cs` | 356 | dispatch, location search, readback orchestration, `WOODS.WLD` rewrite |
| `Scripts/Models/TerrainComputerParams.cs` | 174 | 13 noise-param blocks, INI (de)serialisation, `ApplyToCS` |
| `Scripts/Models/Noise Params/JordanTurbulence.cs` | 158 | 13 `cs.Set*` calls |
| `Scripts/Models/Noise Params/SwissTurbulence.cs` | 130 | 9 `cs.Set*` |
| `Scripts/Models/Noise Params/Perlin.cs` | 116 | 7 `cs.Set*` |
| `Scripts/Helpers/BufferIO.cs` | 133 | buffer create + **synchronous `GetData`** + copy into `MapPixelData` |
| `Scripts/Compatibility/BasicRoadsUtils.cs` | 123 | Basic Roads mod message |
| `Scripts/Utility.cs` | 76 | world/vertex/unit conversions |
| `Scripts/ModMessageHandler.cs` | 68 | `getTileData` responder |
| `Scripts/Helpers/TileDataCache.cs` | 63 | `Dictionary<string,byte[]>`; hooks `DaggerfallTerrain.OnPromoteTerrainData` |
| `Scripts/Helpers/ConsoleHandler.cs` | 62 | two dev console commands (`clearnoon`, `speedy`) |
| `Scripts/IniParser/IniUtils.cs` | 54 | |
| `Scripts/Models/HeightmapBufferCollection.cs` | 34 | 5 `ComputeBuffer`s + `Dispose` |
| `Scripts/Compatibility/CompatibilityUtils.cs` | 28 | `ModManager.GetAllModTitles()` |
| `Scripts/Enums/HeightmapResolution.cs` | 9 | 129 / 513 / 1025 / 4097 |
| `Scripts/Models/NoiseParams.cs` | 9 | interface |
| `Scripts/Models/Settings.cs` | 8 | `heightmapResolution = low` (129) |
| `Scripts/Constants/Constants.cs` | 7 | `TERRAIN_HEIGHT = 5000`, `HEIGHTMAP_RESOLUTION = 129` |
| `Scripts/IniParser/IniSerializable.cs` | 7 | interface |

### Editor / testing — excluded from a port (885 lines)

`Scripts/Models/Editor/EditorTerrainComputerParams.cs` (381; `Resources.Load<ComputeShader>("NoisePreview")`,
`File.WriteAllText`), `Testing/Scripts/TestTerrain.cs` (147, `[ExecuteInEditMode]`),
`Scripts/Models/Noise Params/Editor/{JordanTurbulenceEditor 48, SwissTurbulenceEditor 39, PerlinEditor 35, Utility 33}`,
`Misc/Editor/HeightmapExtractorTool.cs` (39; `File.WriteAllBytes`), `Scripts/Editor/EditorPrefsNames.cs` (16).
Plus `Testing/Editor/Shaders/TerrainTestShader.shader` (186) and
`Scripts/Models/Noise Params/Editor/Resources/NoisePreview.compute` (208, 13 kernels).
`InterestingTerrains.cs` and `TerrainComputerParams.cs` each carry `#if UNITY_EDITOR` branches
(`TerrainComputerParams` is a `ScriptableObject` only in the editor) — both compile fine with the
non-editor branch, which is what the mod already ships.

### Entry points and DFU surface

- **`[Invoke]`: exactly one** — `InterestingTerrains.Init(InitParams)` at `StateManager.StateTypes.Start, 0`.
- **Replaces**: `DaggerfallUnity.Instance.TerrainSampler`.
- **Overwrites**: `ContentReader.WoodsFileReader.Buffer` (global; swapped back and forth per tile),
  `StreamingWorld.TerrainScale`, `Camera.main.farClipPlane`.
- **Subscribes**: `DaggerfallTerrain.OnPromoteTerrainData` (cache eviction).
- **Sets**: `Mod.MessageReceiver`, `Mod.IsReady`.
- **Reads**: `TerrainHelper.GetMapPixelData`, `ContentReader.MapFileReader.GetLocation`,
  `DaggerfallLocation.GetLocationRect`, `MapsFile.WorldMapTileDim/WorldMapTerrainDim`,
  `MeshReader.GlobalScale`, `WoodsFile.MapWidth/MapHeight`.
- **Registers**: two `Wenzil.Console` commands, which also touch `PlayerEntity.GodMode`,
  `AcrobatMotor.airControl`, `GameManager.SpeedChanger`, `WorldTime.DaggerfallDateTime`.
- **External library**: `IniParser.Parser.IniDataParser` / `IniParser.Model`. Already in the project
  (`Assets/Resources/INIFileParser.dll`, used by `SettingsManager`) — **no new dependency**.

### Threading / unsafe / reflection / IO

- **Threading**: none of its own. `ScheduleGenerateSamplesJob` does **not** schedule anything — it calls
  `GenerateSamples` inline and returns `new JobHandle()` (a default, already-complete handle).
  No `IJob`, no Burst, no `Task`, no `Thread`. `using Unity.Jobs` / `Unity.Collections` only for the
  `JobHandle` return type and `NativeArray<float>` copy.
- **`unsafe`**: none. **Reflection**: none. **`Resources.Load`**: editor file only.
- **File IO**: editor files only (`File.WriteAllBytes`, `File.WriteAllText`). Runtime does none.
- **GPU**: `ComputeBuffer.SetData` ×2, **`ComputeBuffer.GetData` ×3** (1 at start-up, 2 per terrain tile).
  **No `AsyncGPUReadback` anywhere.**
- **No capability check and no CPU fallback**: `SystemInfo.supportsComputeShaders` is never queried;
  there is no non-GPU code path of any kind. If compute is unavailable or a dispatch fails, the mod
  produces garbage terrain rather than degrading.

## 3. Data inventory

Manifest `Files`: **35 entries, 6.46 MB** of source bytes (1 `.json`, 1 `.txt`, 5 `.png`,
3 `.compute`, 4 `.cginc`, 21 `.cs`). Non-code payload is 5 PNGs, 6.62 MB on disk:

| File | Source px | Bytes | Role in code | Import (upstream `.meta`) |
|---|---|---|---|---|
| `Assets/Maps/daggerfall_deriv_map.png` | 5000×2500 RGBA8 | 4,767,936 | `derivMap` — `.rgb` = mountain/desert deriv, `.b` = lo-res base height read as `dTex.b * 255.0` | `maxTextureSize 2048`, `textureCompression 0` (uncompressed), mips on, **`sRGBTexture 1`**, point filter |
| `Assets/Maps/daggerfall_road_map.png` | 5000×2500 RGBA8 | 1,606,511 | `roadMap` — `.r` gates road smoothing | 2048, `textureCompression 1`, mips, **sRGB 1**, bilinear |
| `Assets/Maps/daggerfall_port_map.png` | 5000×2500 RGBA8 | 151,564 | `portMap` — `.r` port, `.g` alt height, `.b` sea-level flag | 2048, uncompressed, mips, **sRGB 1**, point |
| `Assets/Maps/daggerfall_heightmap.png` | 5000×2500 RGBA8 | 60,794 | `biomeMap` (misleading name) — biome weights | 2048, `textureCompression 1`, mips, **sRGB 1**, bilinear |
| `Assets/Maps/tileable_noise.png` | 128×128 RGB8 | 34,091 | `tileableNoise` — road noise | 2048, uncompressed, mips, **sRGB 1**, point |
| `Assets/interesting_terrains.txt` | — | 2,125 | 13 INI noise-parameter sections | `TextAsset` |

Not in the manifest but in the repo: `daggerfall_location_map.png` (1000×500, 371 KB — unused at
runtime), five GIMP `.xcf` working files and one `.asset` totalling **~212 MB** (`daggerfall_deriv_map.xcf`
79.7 MB, `daggerfall_deriv_map (copy).xcf` 80.4 MB, `daggerfall_road_map.xcf` 25.4 MB,
`daggerfall_port_map.xcf` 21.3 MB, `daggerfall_road_network.xcf` 7.3 MB) plus **`WOODS.WLD` 25.4 MB**
(a copy of the vanilla world file, not shipped and not read at runtime). A fetch would need to take
the manifest's 35 files only, not the tree.

**No heightmap or biome binaries** — every "distant heightmap" the mod uses is either one of the four
PNGs or generated at start-up from `WOODS.WLD` on the GPU.

At runtime on iOS the four world maps land at 2048×1024. With mips: deriv and port uncompressed
RGBA32 ≈ 10.7 MB each, heightmap and road ASTC 4×4 ≈ 2.7 MB each → **≈ 27 MB GPU**, plus a runtime
1000×500 `ARGB32` `baseHeightmap` (2 MB) and two 500 KB managed byte buffers.

## 4. Compute shaders and shaders

Three `.compute` ship; **two are used**. `#pragma target` appears nowhere. No `multi_compile`, no
`shader_feature`, no keywords at all — nothing to pin the way Dynamic Skies' skybox needed.

### `Assets/Shaders/TerrainComputer.compute` (699 lines) — the per-tile workhorse

- Kernels: `#pragma kernel TerrainComputer` and `#pragma kernel TilemapComputer`, both
  **`[numthreads(10,10,1)]`**. Dispatched `(130/10, 130/10, 1)` = 13×13 groups = 16,900 threads each.
- Buffers: `RWStructuredBuffer<float> heightmapBuffer` (129²), `RWStructuredBuffer<float> rawNoise`
  (130²), `RWStructuredBuffer<int> tilemapData` (129²),
  **`RWStructuredBuffer<float3> locationHeightData`** (a `static` 289-element, stride-12 buffer),
  `StructuredBuffer<float> shm` (16), `StructuredBuffer<float> lhm` (81).
  `StructuredBuffer<int> lookupTable` and `StructuredBuffer<int> prototypes` are **declared, never
  bound, never used** — dead declarations that rely on the compiler stripping them.
- Textures: `Texture2D<float4> BiomeMap / DerivMap / PortMap / RoadMap / mapPixelHeights`,
  `Texture2D<float> tileableNoise`, and five named inline `SamplerState`s
  (`bm_linear_clamp_sampler`, `bm_point_clamp_sampler`, `dm_linear_clamp_sampler`,
  `pm_point_clamp_sampler`, `bh_linear_clamp_sampler`). Reads are `SampleLevel(..., 0)` plus typed
  loads (`mapPixelHeights[int2]`).
- **Uniform arrays: `float4 locationPositions[1089], locationSizes[1089]`** — 17,424 bytes each,
  ~35 KB of the kernel's constant buffer (Metal's 64 KB per-buffer limit is not exceeded, but there
  is little headroom left for the ~120 noise scalars).
- Includes `noises.cginc` (1,119 lines, 24 octave loops — Perlin/simplex/cell/Swiss/Jordan/IQ variants),
  `noiseParams.cginc` (250), `heightSampling.cginc` (300), `basicRoads.cginc` (140).
- Per pixel `GetHeightSample` calls, unconditionally, **both** `LocationWeight` **and**
  `PortLocationWeight` — each a `for (i < locationCount)` loop doing a texture sample plus an
  8-octave and a 16-octave Perlin per location — then `GetBaseHeight`, which evaluates roughly ten
  more fractal generators (`swissFolded` 16 oct, `iqMountain` 16, `swissCell` 15 ×2, `swissFaults` 12,
  `jordanFolded` 12, `perlinBump` 8 ×2, `colorVar` 4, `perlinDune` 4, `swissDune` 3) ≈ 100 octaves,
  plus two bicubic basemap interpolations.
- `basicRoads.cginc` line 3 carries **`#pragma exclude_renderers d3d11 gles`** (an auto-upgrade note:
  "excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays"). Metal is not in the
  exclusion list, and the mod demonstrably works on desktop D3D11, so the pragma is evidently inert
  inside a `.cginc` included by a `.compute` — but it should be deleted rather than trusted.

### `Assets/Shaders/MainHeightmapComputer.compute` (30 lines) — start-up, once

`#pragma kernel CSMain`, **`[numthreads(10,5,1)]`**, dispatched `(1000/10, 500/5, 1)` = 100×100 groups
= **500,000 threads in one dispatch**, each running the full ~100-octave `GetBaseHeight`
(`detailedHeights = false`). Writes `RWStructuredBuffer<float> Result` (500,000 floats, 2 MB), then
`Result.GetData(float[500000])` **synchronously on the main thread**, converts to bytes and replaces
`WoodsFileReader.Buffer`.

### `Assets/Shaders/MainHeightmapSmoother.compute` (46 lines) — shipped, never loaded

`#pragma kernel CSMain`, `[numthreads(10,5,1)]`, `Texture2D<float4> BaseHeightmap` →
`RWTexture2D<float4> SmoothedHeightmap`. No C# ever calls `Mod.GetAsset<ComputeShader>("MainHeightmapSmoother")`,
and the kernel body computes a blur weight and then writes `centerVal` — a no-op. Dead weight.

### Editor-only

`Scripts/Models/Noise Params/Editor/Resources/NoisePreview.compute` (208 lines, 13 kernels, all
`[numthreads(8,8,1)]`) and `Testing/Editor/Shaders/TerrainTestShader.shader` (186 lines). Neither ships.

### Readback style

Fully synchronous, blocking, on the main thread, immediately after `Dispatch`:

1. **Start-up, once**: `alteredHeights.GetData(float[500_000])` — 2 MB, after a 500,000-thread dispatch.
2. **Per terrain tile, twice**: `heightmapBuffer.GetData(float[16_641])` (66.6 KB) then
   `tilemapData.GetData(int[16_641])` (66.6 KB). The first flushes and waits on both kernels.

No `AsyncGPUReadback`, no double-buffering, no fencing. **Rate while walking**: one DFU map pixel is
`MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale` = 32768 × 0.025 = **819.2 Unity units** on a
side, and `StreamingWorld.TerrainDistance` is 3, so a 7×7 grid of 49 tiles is live and crossing a map
pixel boundary queues **7 new tiles**. On foot that is roughly one tile every few seconds — well
under 1 tile/s. The dangerous cases are not walking: the initial world build and every fast-travel
arrival go through `StreamingWorld.UpdateTerrainData` (the non-coroutine path used when `init` is
set), which does all 49 tiles without yielding.

### Metal compatibility assessment

Compute is fully supported on iOS Metal, and nothing here needs a feature Metal lacks — no
`Texture2DArray` sampling in compute, no `GetDimensions` on a RW texture, no atomics, no wave
intrinsics, no unordered typed UAV loads (the one `RWTexture2D` is in the dead shader), no 64-bit
types, no indirect dispatch. Named inline `SamplerState`s translate fine. Four concerns, in order:

1. **`locationHeightData` is written out of bounds.** The buffer is `new ComputeBuffer(289, 12)`, but
   both `LocationWeight` and `PortLocationWeight` write `locationHeightData[i]` for every
   `i < locationCount`, and `locationCount` is the number of locations found in a **33×33 = 1089**
   map-pixel window (the shader's array is sized 1089 for exactly that reason). On land, Daggerfall's
   location density puts a typical count near 100 and a dense-province count plausibly above 289.
   D3D11 silently discards out-of-range UAV writes, which is why this has never been noticed on
   desktop; **Metal leaves out-of-bounds buffer writes undefined** — corruption or a GPU fault that
   iOS reports as a command-buffer error and the app treats as a crash. The value written is never
   read back by anything, so the fix is trivial (size the buffer 1089, or delete the writes), but it
   must be fixed before the first device run.
2. **`float3` in a structured buffer.** `RWStructuredBuffer<float3>` against a stride-12 buffer is a
   known Metal packing hazard (`float3` is 16-byte aligned; `packed_float3` is 12). Moot if item 1 is
   fixed by deleting the writes.
3. **`SetVectorArray` with a short or empty array.** `cs.SetVectorArray("locationPositions", ...)` is
   handed exactly `locations.Count` elements against a `[1089]` declaration. Deep in the Iliac Bay,
   `locations.Count` can be **0**, and Unity rejects a zero-length array. Needs a guard.
4. **Colour space.** Every world map is imported with **`sRGBTexture: 1`**, and this project is
   **Linear** (`ProjectSettings.asset` `m_ActiveColorSpace: 1`; iOS graphics API is Metal only,
   `m_APIs: 10000000`). These are data maps whose bytes are read as exact numbers
   (`dTex.b * 255.0`, `portHeight`, biome weights). In a Gamma project `sRGBTexture` is inert, which is
   presumably where the mod was authored; in our Linear project it silently linearises them and the
   terrain comes out wrong. Every one needs `sRGBTexture: 0` — cheap, but it means our terrain is
   only bit-comparable to desktop after that change, and `daggerfall_heightmap`/`daggerfall_road_map`
   also arrive block-compressed (`textureCompression: 1` → ASTC on iOS), which will visibly quantise
   biome weights unless overridden to uncompressed as well.

## 5. Interaction with `StreamingWorld` / `DaggerfallTerrain`

**The main-thread question resolves in the port's favour.** The premise that DFU 1.1 generates tiles
on a background `Task` does not hold for this fork: `StreamingWorld.UpdateTerrainData` /
`UpdateTerrainDataCoroutine` (`Assets/Scripts/Terrain/StreamingWorld.cs:1211` and `:1245`) call
`dfTerrain.BeginMapPixelDataUpdate(...)` from a Unity coroutine — i.e. **on the main thread** — and
`DaggerfallTerrain.BeginMapPixelDataUpdate` (`:148`) calls
`dfUnity.TerrainSampler.ScheduleGenerateSamplesJob(ref MapData)` (`:169`) as the first thing it does,
before any `JobHandle` chaining. The *scheduling* call is on the main thread; only the returned jobs
run on workers. So the mod's GPU calls are legal, and no thread marshalling is needed. That removes
what looked like the blocking problem.

What replaces it is a **main-thread stall**, by design:

- `InterestingTerrainSampler.ScheduleGenerateSamplesJob` ignores the jobs system entirely: it runs
  `GenerateSamples` inline (dispatch → `GetData` → copy → `GetData`) and returns an already-complete
  `new JobHandle()`.
- The coroutine's `yield return new WaitUntil(() => updateTerrainDataJobHandle.IsCompleted)` therefore
  yields *after* the sampler has already blocked. The frame-spreading DFU built for terrain streaming
  buys nothing; every tile's full GPU cost lands inside one frame.
- Downstream jobs (`ScheduleCalcAvgMaxHeightJob`, `ScheduleBlendLocationTerrainJob`,
  `ITerrainTexturing.ScheduleAssignTilesJob`, `ScheduleUpdateTileMapDataJob`) still run as Burst jobs
  on `MapData.heightmapData` — that part is untouched and fine, because the mod writes the
  `NativeArray` before returning.
- `IsLocationTerrainBlended() => true` makes DFU skip `ScheduleBlendLocationTerrainJob`: the mod
  claims it has already blended locations itself (which it has, in `LocationWeight`).
- Per tile the mod also does a **CPU** sweep of the 33×33 map-pixel neighbourhood calling
  `TerrainHelper.GetMapPixelData` and `MapFileReader.GetLocation`. `locationRectCache` caches only
  the **hits**; misses are never recorded, so ~1,000 `GetMapPixelData` calls (each touching MAPS.BSA
  region/politic/climate data) are repeated for *every* tile, forever.
- `UnityEngine.Object.Instantiate(csPrototype)` clones the `ComputeShader` **once per tile** and never
  destroys the clone. On iOS each clone can mean a fresh `MTLComputePipelineState`; over a session
  this is an unbounded leak of both managed objects and GPU pipeline state. Trivially fixable
  (instantiate once, or set uniforms on the shared asset), but it is there today.
- `Cleanup()` releases only the static `locationHeightData`; the per-tile buffers are released in
  `BufferIO`, but a throw between `Create` and `ProcessBufferValuesAndDispose` (e.g. the
  `ValidateLength` exception, or the `TileDataCache.Add` duplicate-key throw AsesinoBlade patched)
  leaks five `ComputeBuffer`s.
- `woodsFile.Buffer` is reassigned three times per tile (original → sample → altered). Harmless on
  the main thread; it would be a data race the moment anything moved off it.

## 6. Dependencies, and whether ported WoD locations need this mod

**Declared dependencies: one, optional** — `basicroads` ≥ 1.0.0, `IsOptional: true`, `IsPeer: false`.
Everything else is soft: the sampler looks up Wilderness Overhaul's GUID and never uses the answer,
and `CompatibilityUtils` only checks whether Basic Roads is in `ModManager.GetAllModTitles()`.
The port already compiles Basic Roads' texturing in (`Assets/Scripts/Game/Mobile/BasicRoadsTexturing.cs`,
MIT, author's blessing), so the road-smoothing path is reachable if we want it.

**It does not depend on WoD Biomes.** Checked `drcarademono/wod-biomes` (GUID
`3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`, 229 manifest files, 3 scripts / 559 lines): its only declared
dependency is `daggerfall expanded textures` (peer, required), and none of its three scripts mentions
wod-terrain's GUID, `getTileData`, `tileData`, `InterestingTerrain`, `Monobelisk` or `TerrainSampler`.
The two mods are independent. **Distant Terrain is not a dependency either** — it is a separate mod
(and the recently-touched `somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall` fork of it) that
would *consume* the rewritten `WOODS.WLD`, not supply anything.

**WoD locations place correctly without WoD Terrain.** Three independent reasons:

1. `world-of-daggerfall`'s own manifest declares `location loader`, `daggerfall expanded textures`,
   `wilderness overhaul`, `rmb resource pack` and `beautiful villages` — **wod-terrain is not among
   them**, optional or otherwise.
2. Location Loader does its own terrain flattening, against whatever sampler is active. In our ported
   `Assets/Scripts/Game/Mobile/Ports/LocationLoader/LocationLoader.cs` the coupling is all *relative*:
   it hooks `DaggerfallTerrain.OnPromoteTerrainData` (`:68`), averages
   `daggerTerrain.MapData.heightmapSamples` over the instance footprint (`:688`, `:858`), blends and
   writes them back (`:897`–`:899`), then resets terrain data (`:790`), and derives world height from
   `DaggerfallUnity.Instance.TerrainSampler.MaxTerrainHeight` and `.OceanElevation` (`:532`, `:536`,
   `:1207`, `:1220`). Nothing reads an absolute elevation or a wod-terrain-specific value.
3. The port has been running WoD locations on the default sampler since the WoD design was approved,
   and `THIRD-PARTY.md` already records WoD Terrain as deliberately excluded.

The honest caveat: WoD Terrain's `daggerfall_port_map` exists **because** WoD adds docks, lighthouses
and port districts, and the pinned commit is literally the `PortCoastlines` merge. Ports and coastal
WoD locations were tuned against a coastline this mod reshapes, and its `LocationWeight` fade is much
wider (`fadeDist` 64 vs upstream's 32) than DFU's own blend. So without it WoD's coastal and dock
locations sit on vanilla coastline — they *place*, they are reachable and not floating, but they will
not look the way their authors framed them, and the 382 dock/lighthouse instances are the ones most
likely to read as off. That is a fidelity gap, not a correctness one.

## 7. Licence status

**No licence, at any level of the lineage.**

- `drcarademono/wod-terrain`: no `LICENSE` file, no `license` field on the GitHub repo, no licence
  header in any `.cs`, `.compute` or `.cginc`. `README.md` is a short note plus monobelisk's original
  readme; neither mentions terms.
- Upstream `monobelisk/DFUnity-InterestingTerrains`: also `license: none`.
- `Freak2121/Enhanced-ErodedTerrains`, credited in the readme, is a third unlicensed work in the chain.
- Four named authors on the manifest (monobelisk, Freak2121, carademono, Ninelan) and one committer
  (drcarademono), so permission would have to come from more parties than Dynamic Skies or WoD did.

Same posture as Dynamic Skies, Location Loader and World of Daggerfall: it could only ever ship
`private_only`, on the private test draft, with a "permission pending" record — and the permission
conversation here is a four-way one through two upstream projects.

## 8. iOS risk list, cost and memory

### Estimated cost per tile

Per `TerrainComputer` dispatch: 16,900 threads. Per thread, roughly 100 fractal-noise octaves for
`GetBaseHeight`, plus `2 × locationCount × 24` octaves for the two location loops (24 = the 8- and
16-octave Perlins inside each iteration), plus two bicubic interpolations and ~8 texture samples.
At a typical inland `locationCount ≈ 30` that is ~1,540 octaves per thread ≈ **26 M octave
evaluations per tile**; near a dense province (`locationCount ≈ 150`) it is ~7,300 octaves per thread
≈ **123 M**. At a rough 40–60 FLOP per 2D gradient octave: **1–1.5 GFLOP typical, 5–7 GFLOP worst
case, per tile**. `TilemapComputer` adds ~25 octaves per thread — negligible by comparison.

Translated to devices, and to be treated as an order-of-magnitude estimate to be replaced by a
measurement, not a number to plan against:

| Device class | Typical tile | Dense-province tile |
|---|---|---|
| A17/M-class iPad (~2 TFLOP FP32) | ~2–5 ms | ~15–30 ms |
| A14/A15 iPhone (~1.2 TFLOP) | ~4–10 ms | ~30–60 ms |
| A10/A12 iPad, 6th/7th gen (~0.3–0.5 TFLOP) | ~15–40 ms | ~100–250 ms |

Because the readback is synchronous on the main thread, **the whole of that lands in one frame**, plus
a full pipeline flush, plus the per-tile CPU cost of ~1,000 uncached `TerrainHelper.GetMapPixelData`
calls (likely another 2–10 ms). Walking is survivable — 7 tiles spread over tens of seconds, each a
visible hitch. **Fast-travel arrival and the initial world build are not**: 49 tiles through the
non-yielding `UpdateTerrainData` path is on the order of **0.5–2 s on a modern device and 2–10 s on
an old iPad**, on top of the start-up dispatch below.

### Start-up cost

`InitializeWoodsFileHeightmap` is a single dispatch of **500,000 threads**, each running the same
~100-octave `GetBaseHeight` — **50 M octaves, ~2–3 GFLOP** — followed by a 2 MB synchronous readback,
a 500,000-iteration LINQ `Select` to bytes, and a 500,000-pixel `SetPixels32`/`Apply`. Call it **1–3 s
on a modern iPhone and 5–20 s on an A10-class iPad**, blocking, at the `Start` state. That single
dispatch is the top iOS risk: iOS enforces a command-buffer execution limit (a few seconds), and a
kernel that long in one submit can be killed, which surfaces as a Metal command-buffer error and, in
practice, a crash on exactly the devices the port most wants to support. It would have to be split
into tiles or moved off the critical path.

### Memory

| Item | Cost |
|---|---|
| 4 world maps at 2048×1024 with mips (2 uncompressed RGBA32, 2 ASTC) | ≈ 27 MB GPU |
| `baseHeightmap` `Texture2D` 1000×500 `ARGB32` | 2 MB |
| `originalHeightmapBuffer` + `alteredHeightmapBuffer` | 1 MB managed |
| transient start-up `ComputeBuffer` + `float[500_000]` | 4 MB, freed |
| per-tile `ComputeBuffer`s (heightmap 66.6 KB + rawNoise 67.6 KB + tilemap 66.6 KB + shm/lhm) | ≈ 200 KB, released per tile |
| `tileDataCache` | 16.6 KB per live tile, evicted on promote |
| **leaked `ComputeShader` clones + pipeline state** | **unbounded, one per tile generated** |

**≈ 30–35 MB steady**, plus a leak that grows for the life of the session. On a 2 GB iPad that
steady figure is affordable; the leak is not.

### Risk list

1. **The 500,000-thread start-up dispatch** may exceed iOS's command-buffer execution limit on
   A10–A12 devices and be killed → crash at launch, on the oldest supported hardware. Must be tiled.
2. **Out-of-bounds `RWStructuredBuffer<float3>` writes** (289-element buffer, up to 1,089 indices
   written). Undefined on Metal; silently discarded on D3D11, which is why upstream never saw it.
   Cheap fix, but a device run before fixing it is not worth doing.
3. **Synchronous `GetData` on the main thread, twice per tile, with the jobs system bypassed.** Every
   tile's full GPU cost is a main-thread stall. Fast-travel arrival and world init put 49 of them in
   one non-yielding call. Fixing this properly means restructuring the sampler around
   `AsyncGPUReadback` and DFU's coroutine, which is real design work, not a `// MOBILE` line.
4. **A `ComputeShader` clone leaked per tile**, with its Metal pipeline state.
5. **`sRGBTexture: 1` on five data maps in a Linear project**, plus ASTC on two of them. Wrong terrain
   until every `.meta` is overridden; and once overridden, output diverges from the desktop reference,
   so "does it look right" has to be judged rather than diffed.
6. **~1,000 uncached `GetMapPixelData` calls per tile** — a pure-CPU cost the mod never needed to pay
   (miss-caching is a two-line fix).
7. **It rewrites `WoodsFileReader.Buffer` globally.** The travel map, region maps and anything else
   reading `WOODS.WLD` change with it, and the port's own map and travel UI would need checking.
8. **Save compatibility.** `MaxTerrainHeight` goes 2308.5 → 5000 and `OceanElevation` 27.2 → 100.01,
   and `Version` is 7 rather than the default sampler's. Toggling the mod on or off moves the ground
   under an existing save — the launcher switch is a per-save decision, not a free toggle, which is a
   different UX shape from every mod the port ships today.
9. **No CPU fallback and no capability check.** `SystemInfo.supportsComputeShaders` is never read;
   there is no non-GPU path to fall back to. On iOS Metal compute is always available, so this is not
   a blocker — but it means any dispatch failure yields silently wrong terrain rather than vanilla
   terrain, and it removes the "switch it off if it misbehaves" safety the other ported mods have
   (the launcher switch still works, but only between launches).
10. **`#pragma exclude_renderers d3d11 gles` in a shipped `.cginc`.** Inert in practice, but it should
    not be shipped on trust.
11. **`SetVectorArray` with a zero-length array** in open water.
12. **`TileDataCache.Add` duplicate-key throw** (AsesinoBlade already patched this upstream-of-us).
13. **Licence**: four authors, two unlicensed upstream projects, nothing declared anywhere.
    `private_only` at best.

### Is there a CPU fallback path in the code?

**No.** There is no software heightmap generator, no `#if` branch, no `supportsComputeShaders` guard
and no way to run the mod without a working compute dispatch. If we wanted a fallback we would be
porting ~100 octaves of `noises.cginc` to C#/Burst — which is, in effect, writing a different mod.

## 9. Feasibility verdict

**Hard.** Not because Metal cannot run it — it can, and the main-thread worry turned out to be a
non-issue in this fork — but because a faithful port is not a copy-in job. Three of the items above
(the start-up dispatch, the synchronous per-tile readback, the out-of-bounds buffer) are changes to
how the mod works, not lines marked `// MOBILE`, and the honest version of item 3 is a rewrite of the
sampler around `AsyncGPUReadback` plus a way to hold a terrain tile back for a frame or two — which
DFU's terrain pipeline does not currently offer. Set against that: the mod is small (1,634 runtime
lines, 6.5 MB of data, one `[Invoke]`, no reflection, no `unsafe`, no threading, no new libraries,
one optional dependency we already compile in), it needs no shader keywords pinned, and the fidelity
it buys is optional — **WoD's locations place correctly without it**, so nothing already shipped is
blocked. Keeping it out of scope, as the WoD design decided, remains the right call; if it is ever
revisited, the sequence is (a) fix items 1, 2, 4, 5, 11, 12 — each small and well-understood —
(b) measure a single tile and the start-up dispatch on the oldest target device before writing any
integration code, and (c) only then decide whether item 3 is worth the redesign.
