# Distant Terrain (World of Daggerfall flavour) on iOS: far terrain on texture arrays, compiled in

Date: 2026-09-09. Third of the three approved cycles (Ikram: "World of Daggerfall biomes, terrain, and distant terrain
should be working as well. Go ahead and build those"; design decisions confirmed: drop the fly-map and the teleport spell,
rewrite the far-terrain texturing onto texture arrays, shader-compile spike first). Fable plans and rules; Opus subagents
implement and verify. Research: `docs/superpowers/research/2026-09-09-distant-terrain-wod.md`.
Licence: Nystul's base is MIT (2017); the WoD-flavour additions by MaoDeVaca (Nexus 1284) declare no licence; Ikram
handles permission. Private draft only (`private_only` + `pending:`), exactly as the other WoD-family ports.

## What the mod is

"Distant Terrain of the World of Daggerfall": Nystul's Distant Terrain (MIT) modified by MaoDeVaca. The only versioned
copy is the vendored drop inside github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @
d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f (upstream drop a722b337935dd8a57a913eb413c918c351e68e67; her own commits add build
scripts only - not used). Manifest title `Distant Terrain`, same GUID as Nystul's (mutually exclusive with it; Nystul's is
not in our pack). It draws a second, coarse Unity `Terrain` covering the whole province on a stacked camera behind the
main one (far clip 120,000; main camera forced to 15,000 and clear flags Depth), built at `StreamingWorld.OnReady` from
the small world heightmap (`WoodsFileReader`, 1000x500 -> 1025^2 terrain data), with: 32,928 WoD mountain-prefab lifts
from three CSVs (un-lifted while inside the near-terrain footprint); a river/coast carve from a hand-painted 5000x2500
`daggerfall_deriv_map.png` read with `GetPixels32`; a 1024^2 RGBA32 "terrain info" tilemap (climate, per-region treatment,
location beacon); a skybox-only third camera into a 256^2 RenderTexture used for fog colour and the world-edge fade; a
near/far seam skirt with `discard` inside the near footprint; per-frame uniform pushes and a `SetHeights` dirty-rect on
every map-pixel cross. Runtime C#: 4,141 lines, of which `DistantTerrainFlyMap.cs` (930, keyboard/mouse/IMGUI teleporter)
and `ThirteenthPassageEffect.cs` (144, a spell + console command) are dropped -> ~3,050 lines in `DistantTerrain.cs`,
`_startupMod.cs`, two camera-clone scripts, `RenderSkyboxWithoutSun.cs`. One `[Invoke(Start,0)]`. No reflection, threads,
file IO or `Resources.Load`. Shaders: `DistantTerrainTilemap.shader` (two surface programs + a depth pass, `target 3.0`, one
keyword) + `FarTerrainCommon.cginc` (16 sampled `sampler2D`s = at Metal's portable ceiling); it samples TWELVE 2048^2
ARGB32 tileset atlases (desert/mountain/woodland/swamp x summer/winter/rain) that our texture-array terrain path never
builds -> ~268 MB that a straight port would add. Data: three CSVs (3.54 MB), the deriv map, `modsettings.json`; 1.5 MB of
manifest files nobody reads. Dependencies: none declared; works with DFU's sampler and with WoD Terrain; WoD locations make
its mountains real (its `BasicMode` setting skips the lifts and the carve for a no-WoD setup). Our Dynamic Skies port is
already Distant-Terrain-aware (`GameObject.Find("DistantTerrain")` / `"stackedCamera"` at its Init).

## Design

1. **Spike first (go/no-go on the ORIGINAL shader, informative only)**: copy `DistantTerrainTilemap.shader` +
   `FarTerrainCommon.cginc` into `Assets/Shaders/DistantTerrain/` and run an iOS shader compile (the same batch path as
   `EnsureAlwaysIncludedShaders` + a `ShaderUtil`/build-for-iOS compile in the Editor, or a throwaway `BuildIOS`). Record:
   does Metal accept 16 samplers + the surface generator's own; the `float4 pos : SV_POSITION` in the surface `Input`
   struct; warnings. The texture-array rewrite (2) happens regardless (memory); the spike tells us whether the original
   could ever be a fallback and surfaces Metal complaints early.
2. **Texture-array rewrite** (the real work): `FarTerrainCommon.cginc` + `DistantTerrainTilemap.shader` sample
   `UNITY_DECLARE_TEX2DARRAY` slices instead of 12 atlases: three arrays (summer / winter / rain), each holding the four
   biome tilesets' 56 records as slices (4 x 56 = 224 slices of 64^2, ~3.7 MB per array uncompressed; ~11 MB total vs
   268 MB), built with DFU's own `TextureReader.GetTerrainTextureArray(archive)` (already used by the near terrain), so
   texture replacement (DET) is honoured. Atlas UV/gutter maths (`getColorByTextureAtlasIndex`, `tex2Dgrad`) becomes
   slice index + `UNITY_SAMPLE_TEX2DARRAY_GRAD`/`LOD`; references: DFU `DaggerfallTilemapTextureArray.shader` and Nystul's
   deleted `TransitionRingTilemapTextureArray.shader`. Sampler count drops from 16 to ~7. Delete the dead
   `_CameraDepthTexture` sampling, the three legacy samplers, `#pragma glsl`. Keep the one keyword; register the
   variants in `RequiredShaderVariants.shadervariants` and pin the shader Always-Included (`MobileBuildSetup`).
   Visual regression is judged against the simulator screenshots of the atlas version if the spike compiles on the Mac
   Editor (Metal desktop), else against desktop reference screenshots from the research.
3. **Code compiled in**: `Assets/Scripts/Game/Mobile/Ports/DistantTerrain/` - `DistantTerrain.cs`, `_startupMod.cs`
   (trimmed: no FlyMap, no spell, no console command, no `KeyCode` settings), `CloneCamera*.cs`, `RenderSkyboxWithoutSun.cs`.
   `[Invoke]` removed; `mod.GetAsset<Shader>` -> `MobileShaders.Find("Daggerfall/DistantTerrain/DistantTerrainTilemap")`;
   `mod.GetAsset<TextAsset>` (CSVs) and `<Texture2D>` (deriv map) stay bundle loads. Port headers name both provenance
   commits and the MIT base. `MOBILE` edits beyond the list: `TerrainData` over-allocation trimmed (alphamap/basemap/detail
   resolutions to their minimum - the far terrain uses none of them); `blendEnd` (far camera far clip) and
   `mainCameraFarClipPlane` become mod settings (defaults: 60,000 and 15,000 - half the upstream reach as the iOS default;
   documented as the perf dial); `heightmapPixelError` left 0 (upstream); the `Update()` per-frame `SyncStackedCameraToMain`
   kept; CSV parse kept (3.5 MB string split once at world entry - measured, pre-bake to binary only if the timing line says so).
   Timing lines: `[DistantTerrain] far terrain built in N ms (heightmap A ms, carve B ms, lifts C ms, tilemap D ms)` and
   `[DistantTerrain] map-pixel update N ms` for the first 10 then every 25th.
4. **Start order and sky/fog contract**: `MobilePortedMods.StartEnabled` starts Distant Terrain BEFORE the deferred sky
   start, and the sky's 1 Hz `StartSkyWhenSceneReady` poll additionally waits for `GameObject.Find("stackedCamera")` when
   the Distant Terrain entry is running (a static `DistantTerrainPort.Running` flag), so `BLBSkybox.Init` takes its
   stacked-camera branch deterministically. Fog: Distant Terrain's `Start()` overwrite of the five `WeatherManager`
   fog settings is kept (upstream behaviour) but logged once; the visual reconciliation with Dynamic Skies is a device
   tuning item, not a code item in this cycle. `RenderSkyboxWithoutSun`'s `_SunSize` pre/post-render write is kept
   (the BLB skybox has `_SunSize`).
5. **Launcher entry + preset**: `Distant Terrain` default OFF via `Titles`; no dependency gate (BasicMode covers no-WoD);
   ships with an iOS low preset in `modsettings.json` defaults: `EnableTreesAndDirt=false`,
   `HighlightDistantLocations=false`, `BasicMode=false` (WoD is the point), `blendEnd=60000`. Docs say what the dials do.
6. **Data pipeline**: mods.json entry `DistantTerrainWoD` (`strip_code`; `private_only`; `pending:` licence naming the MIT
   base + MaoDeVaca; `exclude_globs` for `*.shader`, `*.cginc`, `*.prefab`, `*.png~`, the three unused `*.bin.txt`,
   `DaggerfallBillboardBatchFaded.shader`); importer rule `RawData` extended to folder `DistantTerrainWoD` (the deriv map
   is `GetPixels32`-read: readable, uncompressed, no mips, 2048 max size is WRONG here - it must stay 5000x2500 or the carve
   is quantised: set `maxTextureSize = 8192` for this folder's rule variant; note ~50 MB RGBA32 - OR downsample to 2500x1250
   at fetch time with a documented tool step if the device budget bites; v1 ships full size). Bundle `distant terrain.dfmod`
   -> GitHub asset name `distant-terrain-wod.dfmod` (spaces).
7. **Failure handling**: Init gated on the shader resolving (`MobileShaders.Find` non-null + `isSupported`) and the three
   CSVs + deriv map present, else `[DistantTerrain] not available: <reason>`; `InitFarTerrain` in try/catch that tears the
   far terrain and the two cameras down and restores `Camera.main.farClipPlane`/`clearFlags` on failure; `Installed` flag
   for the launcher's `did not start` line (the 4-arg `StartOne`).
8. **Verification**: self-tests (gate, preset defaults, slice-index maths of the rewrite as a pure function, start-order
   flag); simulator (Metal): far terrain visible on the horizon at 207,213 and at the coast 370,350, with Dynamic Skies on
   (sky clears on the stacked camera - grep the `[DynamicSkies]` lines), WoD Terrain on and off (heights differ), timing
   lines, no Metal errors, memory line (`[DistantTerrain] arrays N MB`); negative run; device ipa `DFU-Test-unity6-distant.ipa`.
   Device judges thermals and the near/far seam.

## Out of scope
Nystul's original as a publishable MIT fallback (priced separately if permission is refused), RealtimeReflections hooks,
the fly-map/spell, per-region treatment tuning, licence work.
