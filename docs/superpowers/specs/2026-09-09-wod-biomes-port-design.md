# World of Daggerfall - Biomes on iOS: code compiled in, data as a private bundle

Date: 2026-09-09. Approved in conversation (Ikram, "Go"): the WoD family continues with Biomes first,
then WoD Terrain, then Distant Terrain, each as its own spec/plan. Fable plans and rules; Opus subagents
implement and verify. Research: `docs/superpowers/research/2026-09-09-wod-biomes.md`.
Licence: none declared upstream; same counterparty as WoD/LL; Ikram handles permission. Private draft only
(`private_only` + `pending:` licence, which `pack.py` now hard-refuses in the public pack).

## What the mod is

github.com/drcarademono/wod-biomes @ 40449fc5fc55c85c8089b068acefe1db4b61534c. Manifest
`World of Daggerfall - Biomes.dfmod.json`, title `World of Daggerfall - Biomes`, GUID
`3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`. Three scripts (559 lines, two `[Invoke(Start,0)]` entry points),
225 PNGs (224 terrain tiles 64x64 point-filtered for archives 003/004/104/304 + `Assets/Maps/climate_map.png`
1000x500 RGBA colour-key map), no shaders, no prefabs. It does two things:

1. `WODTerrainMaterialProvider` takes DFU's `ITerrainMaterialProvider` slot and re-routes four climates to
   different ground archives (Subtropical -> 4, Dak'fron desert -> 3, Hammerfell mountains -> 104/103,
   Haunted Woodlands -> 304/303; rainforest gets winter). Archives 4/104/304 exist in vanilla Arena2 with
   56 records, so no engine change is needed. It mirrors DFU's two providers (texture-array and atlas) and
   picks by the same `IsSupported` test.
2. `NatureBatchOverrider` (in `WODClimates.cs`): on `StreamingWorld.OnUpdateTerrainsEnd`, for every
   `DaggerfallBillboardBatch` with archive 501 whose map pixel is `#FFA500` in `climate_map`, swaps the
   batch to archive 10030 (Daggerfall Expanded Textures' "Makija" nature set, 32 records) at 2x scale via
   `CustomBillboardHelper` (an atlas builder for archives > 511 that writes the batch's private fields by
   reflection).

Dependencies: Daggerfall Expanded Textures (required, supplies archive 10030; already in the MIT pack and
already gated for WoD); Vanilla Enhanced (optional, probed by GUID). Independent of WoD Terrain, Distant
Terrain, Location Loader and Basic Roads (it never touches `TerrainTexturing`, `TerrainSampler` or
`TerrainNature`). `WODRocksMaterials` (already compiled in) probes the Biomes GUID and flips Hammerfell rock
materials when the entry exists and is enabled - no change needed there.

## Design

1. **Code compiled in**: `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/` (the three files;
   namespace `WorldOfDaggerfall` kept). Port header on each (source, commit, no licence upstream, private
   draft). Edits, each marked `// MOBILE`:
   - `[Invoke]` removed; `MobilePortedMods.StartEnabled` calls `WODBiomes.Init` then
     `NatureBatchOverriderInstaller.Init` (explicit order fixes the upstream `VEModEnabled` race) via
     `StartOne`, after the WoD Init.
   - `Shader.Find` -> `Game.Mobile.MobileShaders.Find`; add `_DaggerfallBillboardBatchShaderName` and its
     NoShadows variant to `MobileShaders.names` (pre-existing engine gap, fixes `DaggerfallBillboardBatch` too).
   - Reflection removed: `DaggerfallBillboardBatch.currentArchive` and `cachedMaterial` become `internal`
     (marked `// MOBILE`), assigned directly.
   - `NatureBatchOverrider.ApplyOverrides`: iterate `GameManager.Instance.StreamingWorld` terrains'
     batches (or hook per-terrain `DaggerfallTerrain.OnPromoteTerrainData` like the LL port) instead of
     `FindObjectsOfType`; cache the swapped `Material` beside the atlas; `force:false` plus a
     `HashSet<DaggerfallBillboardBatch>` of already-swapped batches so `Apply()` runs once per batch;
     guard `climateMap == null || !climateMap.isReadable` (log once, do nothing).
   - `atlasMaxSize` pinned to 1024, which avoids the transient 85 MB 4096^2. 1024 and not 512: archive
     10030's 32 records are NOT 64x64 - they run up to 130x153, 115x272 and 113x296, ~202 k px of content
     and ~225 k px once padded, so 1024^2 (1,048,576 px) leaves ~4.6x headroom while 512^2 (262,144 px)
     would be marginal. Do not "optimise" the pin down.
   - climate map read through `TextureReplacement.EnsureReadable` as belt-and-braces.
2. **Launcher entry**: the data bundle's own entry `World of Daggerfall - Biomes`, default OFF via
   `MobilePortedMods.Titles`; gated like WoD: switched off (log line; note is best-effort) when Daggerfall
   Expanded Textures is off/missing. It does NOT require Location Loader or WoD (the terrain re-skin and
   the nature swap work alone), so no LL gate. Pure `BiomesRuns(biomes, det) => biomes && det`.
3. **Data pipeline**: mods.json entry `WorldOfDaggerfallBiomes` (`strip_code`, `private_only`,
   `pending:` licence, `exclude_globs` for the `.7z`/`.xcf`; no `extra_dirs`). NEW builder rule in
   `MobileModPackTextureImporter` (Assets/Editor/MobileModBuilder.cs): a per-mod exemption list
   ("colour-key / point-filtered data textures stay raw") - for the Biomes folder: `isReadable = true`,
   uncompressed RGBA32 on iPhone, `mipmapEnabled = false` for `climate_map`, `filterMode = Point` kept, and
   the 224 tiles uncompressed too (DFU decompresses them into an ARGB32 `Texture2DArray` anyway; ASTC only
   costs quality). Expressed as a data-driven list in the importer (mod folder name -> rule), unit-tested
   where pure. Bundle name = lowercased manifest file name: `world of daggerfall - biomes.dfmod`
   (~2 MB on disk, ~7 MB uncompressed). The file name is not load-bearing - nothing resolves this mod by
   `FileName` - so the draft release asset is `wod-biomes.dfmod` (GitHub rewrites spaces in asset names).
4. **Location Loader side (type-5 nature swap)**: restore carademono's `BiomesClimateSwap` (101 lines,
   LL `rmb-object` @ 896a574) into `Ports/LocationLoader/`, with the climate map taken from the compiled-in
   Biomes port (a `public static Texture2D ClimateMap` set by `NatureBatchOverriderInstaller.Init`) instead
   of a bundle `GetAsset`; called from the type-5 branch only when the Biomes entry is running
   (`MobilePortedMods.BiomesRunning` static flag). Guarded null/unreadable -> no-op.
5. **Failure handling**: each Init in its own `StartOne`; the overrider's per-update work in try/catch
   logging once per exception message; texture-array unsupported -> the mod's own atlas provider fallback.
6. **Verification**: self-tests (gate, importer rule table, `MobileShaders.names` additions); tooling
   unittest; simulator run with the ASTC-path bundle characteristics reproduced (the importer rule is what
   the sim must prove: assert in the self-test that the fetched Biomes textures import readable + RGBA32 on
   the iPhone platform; runtime check in-game at a subtropical coastal pixel in Sentinel / Alik'r for the
   `[Biomes]` swap log line and a screenshot); then `DFU-Test-unity6-biomes.ipa` on the draft.
   Device is the visual check (ASTC vs RGBA32 cannot be judged in the sim).

## Out of scope
WoD Terrain, Distant Terrain (own specs next), Vanilla Enhanced, licence work.
