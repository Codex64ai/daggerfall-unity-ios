# World of Daggerfall - Terrain iOS Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WoD Terrain's GPU terrain sampler runs on iOS as a compiled-in port with its compute shaders in the app, its five world maps + INI as a private bundle, its own default-off launcher switch, and the seven correctness/containment fixes the research names, keeping the synchronous per-tile readback for version one.

**Architecture:** Same shape as Biomes: upstream C# copied under `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/`, started from `MobilePortedMods.StartEnabled` via `StartOne` last; the two compute shaders + four cginc live in `Assets/Resources/WoDTerrain/` and are loaded with `Resources.Load`; the data bundle `wod-terrain.dfmod` is built by `ApplyAll` from the fetched repo with a new `LinearData` importer rule (sRGB off, uncompressed); start-up dispatch chunked into row bands; buffer sizes, clone reuse, caches and try/finally containment fixed in place.

**Tech Stack:** Unity 6000.3.23f1, DFU 1.1.1 fork, C# (IL2CPP iOS, Metal compute), Python 3 pack tooling, MobileSelfTest, iOS Simulator + xcodebuild.

**Spec:** `docs/superpowers/specs/2026-09-09-wod-terrain-port-design.md` (research: `docs/superpowers/research/2026-09-09-wod-terrain.md`).

## Global Constraints

- Branch `unity6-upgrade`, repo `/Users/ikrammassabini/dev/daggerfall-unity`; work directly on it. Every commit ends with the two trailers `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01XFLMiLMMZbzbjem8vgUjYE`. Add files by name; revert Unity reserialization churn (`Assets/Scenes/DaggerfallUnityGame.unity`, `ProjectSettings/ProjectSettings.asset`, `Assets/AddressableAssetsData/iOS/addressables_content_state.bin`) with `git checkout --` before committing.
- ONE Unity Editor at a time: `pgrep -f "Unity.app/Contents/MacOS/Unity"` must be empty before starting one; wait 60 s and retry (up to 10x); never kill processes. NEVER edit source while another agent's Unity run is in progress.
- Self-test: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/ikrammassabini/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>`. Baseline `=== 663 passed, 0 failed ===`, 0 `error CS`. After Task 1 fetches a 50th manifest the check `one bundle per fetched manifest ...` fails until Task 6 builds the bundle - quote that FAIL by NAME in every GREEN until then. Tests are `static void TestXxx()` in `Assets/Editor/MobileSelfTest.cs` using `Check(bool, name, detail)`, registered in `RunAll()`. TDD: add checks, one RED run, implement, one GREEN run.
- Tooling tests: `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` (35 OK); `python3 tools/bundled-mods/fetch.py --check` must stay `0 problems`.
- Private only: mods.json entry has `"private_only": true` and a `"licence": "pending:..."` string. Never publish; upload only to the `testapp-unity6` draft of `Codex64ai/daggerfall-unity-ios` (the controller uploads if `gh` is blocked for a subagent).
- Upstream pin: wod-terrain @ `9aeb1bcc5de5343ccb7a6b09062559ad871e9d40`. GUID `a9091dd7-e07a-4171-b16d-d13d67a5f221`, title `World of Daggerfall - Terrain`, manifest `wod-terrain.dfmod.json` -> bundle `wod-terrain.dfmod`. AsesinoBlade/wod-terrain @ `76623bb2aebc66a7b25a8a9621b3e35d69d41917` is the reference for the `TileDataCache` guard only.
- Port headers on every copied file (source URL, commit, "no licence header upstream; private draft only"); every edit marked `// MOBILE:`. Namespace `Monobelisk` kept.
- Staging mirror `~/daggerfall-mobile/`: refreshed once by the controller at plan end - implementers do not copy.
- Workspace: `.superpowers/sdd/2026-09-09-wod-terrain-port/` (gitignored). Upstream clone for reference: `/Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/research-terrain/wod-terrain` (re-clone at the pin if missing).

---

### Task 1: Pack pipeline entry and fetch (no Unity)

**Files:**
- Modify: `tools/bundled-mods/mods.json` (append after `WorldOfDaggerfallBiomes`)

**Interfaces:**
- Produces: fetched folder `Assets/Game/Mods/WorldOfDaggerfallTerrain/` with the manifest, 5 PNGs, `interesting_terrains.txt`, and (strip_code) no `.cs`; the `.compute`/`.cginc` files ARE manifest files - check whether `strip_code` also strips them (read `fetch.py`); if it does not, add `"exclude_globs": ["*.compute", "*.cginc"]` so the bundle carries data only (the shaders ship in the app, Task 3). Folder name `WorldOfDaggerfallTerrain` is the importer-rule key (Task 2).

- [ ] **Step 1: Add the entry**
```json
{
  "name": "WorldOfDaggerfallTerrain",
  "repo": "https://github.com/drcarademono/wod-terrain.git",
  "commit": "9aeb1bcc5de5343ccb7a6b09062559ad871e9d40",
  "manifest": "wod-terrain.dfmod.json",
  "strip_code": true,
  "private_only": true,
  "exclude_globs": ["*.compute", "*.cginc"],
  "drop_dependencies": ["basicroads"],
  "licence": "pending:No licence is declared anywhere in the lineage (github.com/drcarademono/wod-terrain, from monobelisk's Interesting Terrains; authors monobelisk, Freak2121, carademono, Ninelan). Permission is being sought; until then this bundle is shipped only on the private test draft, never in the public mod pack. Its C# is compiled into the app under Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain and its compute shaders under Assets/Resources/WoDTerrain (see THIRD-PARTY.md)."
}
```
Confirm `drop_dependencies` matches the manifest's dependency `Name` (`basicroads`) exactly; if the fetch validator complains that a dropped dependency is optional-only, keep it dropped anyway (Basic Roads has no bundle FileName in our pack).

- [ ] **Step 2: Fetch and check.** Run the fetch (see `fetch.py --help`), then `--check` -> `50 mods, 0 problems`. Expected in the folder: `wod-terrain.dfmod.json`, 5 `.png` (+ `.meta`), `interesting_terrains.txt`, 0 `.cs`, 0 `.compute`, 0 `.cginc`, and NO `.xcf`/`WOODS.WLD` (manifest-only copy). Report `du -sh` of the folder (expect ~7 MB).
- [ ] **Step 3: Tooling tests** -> `OK` (35); pack dry run shows `wod-terrain` NOT a pack member.
- [ ] **Step 4: Commit** `Pack pipeline: fetch World of Daggerfall - Terrain (private, data only)`.

---

### Task 2: Importer rule `LinearData` (sRGB off, uncompressed) for the Terrain maps

**Files:**
- Modify: `Assets/Editor/MobileModPackTextureRules.cs`, `Assets/Editor/MobileModBuilder.cs` (`MobileModPackTextureImporter.OnPreprocessTexture`)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestPackTextureRules`)

**Interfaces:**
- Produces: `Rule.LinearData`; `MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallTerrain/...")` returns it; `public static IReadOnlyList<string> LinearDataMods`; the importer branch sets `importer.sRGBTexture = false`, `textureCompression = Uncompressed`, iPhone `RGBA32`, `maxTextureSize = 2048`, `npotScale = None`, leaves `isReadable` false and mips/filter as the upstream meta.

- [ ] **Step 1: Failing checks** (append to `TestPackTextureRules`):
```csharp
Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallTerrain/Assets/Maps/daggerfall_deriv_map.png") == MobileModPackTextureRules.Rule.LinearData,
    "PackTextureRules: the Terrain world maps are linear data (sRGB off, uncompressed)");
Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png") == MobileModPackTextureRules.Rule.RawData,
    "PackTextureRules: Biomes stays raw data after adding the linear rule");
Check(MobileModPackTextureRules.LinearDataMods.Contains("WorldOfDaggerfallTerrain"), "PackTextureRules: Terrain is in the linear-data list");
```
and extend the existing mods.json cross-check loop to cover `LinearDataMods` too. RED run -> `error CS`.
- [ ] **Step 2: Implement.** In the rules class add `static readonly string[] linearDataMods = { "WorldOfDaggerfallTerrain" };`, `LinearDataMods` (AsReadOnly), and a second loop in `For` returning `Rule.LinearData`. In the importer, after the RawData branch:
```csharp
            if (MobileModPackTextureRules.For(path) == MobileModPackTextureRules.Rule.LinearData)
            {
                // MOBILE: data maps read as numbers by a compute shader (heights, biome weights, port flags).
                // The project is Linear, so sRGB sampling would silently remap them; block compression
                // would quantise them. Keep the upstream mip/filter settings.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = false;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var lin = importer.GetPlatformTextureSettings("iPhone");
                lin.overridden = true;
                lin.format = TextureImporterFormat.RGBA32;
                lin.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(lin);
                return;
            }
```
- [ ] **Step 3: GREEN run** -> 666 passed, 1 failed (FAIL = the bundle-count check by name). Reimport proof as Biomes Task 2 did (`DFU_REIMPORT_ONE` scoped `ReimportPacks`): quote `daggerfall_deriv_map.png.meta` lines `sRGBTexture: 0`, iOS `textureFormat: 4`, `maxTextureSize: 2048`, and `daggerfall_heightmap.png.meta` (`textureCompression` upstream was 1 -> now uncompressed via override).
- [ ] **Step 4: Commit** `Pack importer: linear-data rule keeps the WoD Terrain world maps sRGB-off and uncompressed`.

---

### Task 3: Port the sources and compute shaders (inert), with the in-place fixes

**Files:**
- Create: `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/**` (20 files: the 21 runtime files minus `Helpers/ConsoleHandler.cs`; keep upstream subfolders `Models/`, `Models/Noise Params/`, `Helpers/`, `Compatibility/`, `IniParser/`, `Enums/`, `Constants/`), `Assets/Resources/WoDTerrain/{TerrainComputer.compute, MainHeightmapComputer.compute, noises.cginc, noiseParams.cginc, heightSampling.cginc, basicRoads.cginc}` (+ `.meta`)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestWoDTerrainPort`)

**Interfaces:**
- Produces: `public static void Monobelisk.InterestingTerrains.Init(InitParams)` (no `[Invoke]`); `public static bool Monobelisk.InterestingTerrains.Available(bool supportsCompute, bool shadersLoaded) => supportsCompute && shadersLoaded` (pure gate); `public static class Monobelisk.TerrainComputer` constants `LocationBufferSize = 1089` (matches the shader's `[1089]` arrays); `public static (int yStart, int rows)[] Monobelisk.TerrainComputer.Bands(int height, int bands)` (Task 4 wires it; define it here so the test can pin it); `Monobelisk.TerrainComputer.LocationRectCache` caches misses.

- [ ] **Step 1: Copy** files from the clone at the pin; port headers; remove `[Invoke]`; drop `ConsoleHandler.cs` and its registration call in `InterestingTerrains` (mark `// MOBILE: dev console commands not shipped`). Compute shaders + cginc to `Assets/Resources/WoDTerrain/` with a one-line provenance comment at the top of each; delete the `#pragma exclude_renderers d3d11 gles` line in `basicRoads.cginc` (comment why). Replace every `mod.GetAsset<ComputeShader>("X")` with `Resources.Load<ComputeShader>("WoDTerrain/X")`; `mod.GetAsset<Texture2D>(...)`/`<TextAsset>` for the maps and INI stay (they come from the bundle). Do NOT change behaviour beyond the list below.
- [ ] **Step 2: In-place fixes** (`// MOBILE` each): (b) `locationHeightData = new ComputeBuffer(LocationBufferSize, 12)` with `LocationBufferSize = 1089`; (c) one `ComputeShader` instance per kernel asset held in a static, `Cleanup()` destroys it - remove the per-tile `Instantiate`; (d) `SetVectorArray` guard: if `locations.Count == 0` pass `new Vector4[1]` and `locationCount = 0`; (e) `TileDataCache`: `dict[key] = data` instead of `Add`; (f) `locationRectCache` becomes `Dictionary<int, LocationRect?>` (or a parallel `HashSet<int> misses`) so a miss is remembered; (g) capability: `Init` computes `bool ok = Available(SystemInfo.supportsComputeShaders, tcs != null && mhcs != null)`; if false log `[WoDTerrain] not available: <reason>` and return before touching `TerrainSampler`; `InterestingTerrainSampler.GenerateSamples` wrapped in `try { ... } catch (Exception ex) { LogOnce("[WoDTerrain] tile failed: " + ex); fallback.GenerateSamples(ref mapPixel); } finally { buffers.Dispose(); }` where `fallback = new DefaultTerrainSampler()` held by the sampler (check the class name of DFU's default sampler in `Assets/Scripts/Terrain/`); (h) timing: `System.Diagnostics.Stopwatch` around `GenerateSamples`, log `[WoDTerrain] tile <x>,<y> <ms> ms (locations <n>)` for tiles 1-10 then every 25th (a static counter).
- [ ] **Step 3: Failing checks**:
```csharp
static void TestWoDTerrainPort()
{
    Check(Monobelisk.TerrainComputer.LocationBufferSize == 1089, "WoDTerrain: location buffer matches the shader's [1089] arrays (OOB write fix)");
    Check(Monobelisk.InterestingTerrains.Available(true, true) && !Monobelisk.InterestingTerrains.Available(false, true) && !Monobelisk.InterestingTerrains.Available(true, false),
        "WoDTerrain: needs compute support and both compute shaders");
    var bands = Monobelisk.TerrainComputer.Bands(500, 10);
    int rows = 0; foreach (var b in bands) rows += b.rows;
    Check(bands.Length == 10 && rows == 500 && bands[0].yStart == 0 && bands[9].yStart == 450, "WoDTerrain: start-up dispatch splits 500 rows into 10 bands");
    var odd = Monobelisk.TerrainComputer.Bands(500, 7); int r2 = 0; foreach (var b in odd) r2 += b.rows;
    Check(r2 == 500, "WoDTerrain: uneven band split still covers every row");
    Check(Resources.Load<ComputeShader>("WoDTerrain/TerrainComputer") != null && Resources.Load<ComputeShader>("WoDTerrain/MainHeightmapComputer") != null,
        "WoDTerrain: compute shaders are in Resources");
    Check(System.Attribute.GetCustomAttributes(typeof(Monobelisk.InterestingTerrains).GetMethod("Init"), typeof(DaggerfallWorkshop.Game.Utility.ModSupport.Invoke), false).Length == 0,
        "WoDTerrain: no [Invoke] survives");
}
```
RED -> `error CS`. Implement `Bands` (last band takes the remainder). GREEN -> 672 passed, 1 failed (bundle count). Also confirm the compute shaders compile for iOS: the Editor log must show no `Shader error in 'WoDTerrain/...'`; additionally run `MobileBuildSetup.SwitchToIOS` if needed so the log covers the Metal compile - or note that the iOS shader compile is exercised by Task 6's build; say which.
- [ ] **Step 4: Commit** `Port sources: World of Daggerfall - Terrain (inert) with buffer, clone, cache and containment fixes; compute shaders in Resources`.

---

### Task 4: Start-up world heightmap in row bands

**Files:**
- Modify: `Ports/WorldOfDaggerfallTerrain/Models/TerrainComputer.cs` (`InitializeWoodsFileHeightmap` or wherever `MainHeightmapComputer` is dispatched)
- Test: existing `TestWoDTerrainPort` (bands already pinned) + a check that the band count constant is 10

**Interfaces:**
- Consumes: `Bands(height, bands)` from Task 3. Produces: `public const int StartupBands = 10`; log line `[WoDTerrain] world heightmap <ms> ms (<bands> bands)`.

- [ ] **Step 1:** Replace the single `Dispatch(k, 1000/10, 500/5, 1)` + one `GetData` with a loop over `Bands(500, StartupBands)`: set an `int yOffset` uniform (add `int yOffset;` to `MainHeightmapComputer.compute` and use `id.y + yOffset` for the row - keep the buffer index formula identical so the output array layout is unchanged), `Dispatch(k, 1000/10, rows/5, 1)` then `buffer.GetData(result, yStart*1000, yStart*1000, rows*1000)` (managed offset, buffer offset, count). Time the whole loop and log the line. Rows per band must be a multiple of 5 (the kernel's y group size): `Bands` should round each band to a multiple of 5 with the last taking the remainder - update `Bands` and its test accordingly (500/10 = 50 rows -> 10 groups of 5).
- [ ] **Step 2:** RED (new check `StartupBands == 10` + `Bands` multiple-of-5 property) -> GREEN 674/1.
- [ ] **Step 3: Commit** `WoD Terrain: world heightmap generated in ten row bands (no single Metal command buffer can hit the iOS execution limit)`.

---

### Task 5: Launcher entry and start-up

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/MobilePortedMods.cs` (consts, `Titles`, block after Biomes, before `return SkyRuns`)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestPortedModTitles`)

**Interfaces:**
- Produces: `public const string TerrainTitle = "World of Daggerfall - Terrain"`, `Titles` += it (last), start block:
```csharp
            Mod terrain = Entry(TerrainTitle);
            if (terrain != null && terrain.Enabled)
            {
                Debug.Log("[PortedMods] " + TerrainTitle + ": this changes ground height under existing saves and the travel map (by design)");
                StartOne(TerrainTitle, () => Monobelisk.InterestingTerrains.Init(new InitParams(terrain, ModManager.Instance.GetModIndex(TerrainTitle), count)));
            }
```
No dependency gate. Checks: `Titles` contains `TerrainTitle`; `TerrainTitle == "World of Daggerfall - Terrain"`. RED/GREEN (676/1). Commit `World of Daggerfall - Terrain: launcher entry off by default, started last`.

---

### Task 6: Build the bundle, verify import settings, upload

Full `ApplyAll` (no env) -> `bundled mods: 50`, `Assets/StreamingAssets/Mods/wod-terrain.dfmod` + licence; `tools/dfmod_inspect.py` shows 5 textures, all RGBA32, sizes 2048x1024 x4 + 128x128, plus 1 TextAsset; metas: `sRGBTexture: 0` on all five; the Editor log shows the two compute shaders compiled for iOS with no `Shader error`; self-test 676/0 (bundle-count clears). Backups -> 50/50. Copy to `~/dev/dfu-mods/wod-terrain.dfmod`; upload to the draft (controller if blocked). No commit.

---

### Task 7: Docs

`THIRD-PARTY.md` (`## World of Daggerfall - Terrain`: lineage + four authors, no licence, private draft, what it does, compute shaders in the app, the seven fixes, save-height caveat, travel-map change, memory ~43 MB GPU, the timing log lines, not device-verified), `UPSTREAM-PATCHES.md` (entry: no upstream engine file touched unless Task 3 needed one; importer rule; `Resources/WoDTerrain`; rebase risk NONE/LOW), `~/daggerfall-mobile/README-iOS.md` (`### World of Daggerfall - Terrain`: one switch, one bundle `wod-terrain.dfmod`, decide per save, first launch pause while the world heightmap is generated, the `[WoDTerrain]` lines to look for, hitching expectations on foot vs fast travel, not device-verified). Commit `Docs: World of Daggerfall - Terrain`.

---

### Task 8: Simulator verification

Recipe: `~/dev/dfu-mods/biomes-evidence/sdd/task-9-brief.md` + report (driver, container, restore 50/50). Bundle: `wod-terrain.dfmod` is already RGBA32 by rule. Launches: A default-off; B Terrain ON at 207,213 (Daggerfall): expect `[PortedMods] started World of Daggerfall - Terrain`, `[WoDTerrain] world heightmap N ms (10 bands)`, `[WoDTerrain] tile x,y N ms` lines, no `[WoDTerrain] tile failed` / `not available`, no Metal errors (`grep -i "command buffer\|MTL\|GPU"`), screenshot; C OFF same pixel, screenshot (terrain silhouette should differ); D Terrain + Dynamic Skies + DREAM SKY on: no new errors, sky still renders (far plane 10000); E Terrain + LL + WoD + Biomes at 370,350 (Naresa coast): no new errors, dock present, note `[LL]` lines; report the world-heightmap ms and the tile ms distribution (min/median/max) as the first performance datum. Restore 50/50, tree clean.

---

### Task 9: Device build

`~/dev/dfu-mods/biomes-evidence/sdd/task-10-brief.md` pattern with `DFU-Test-unity6-terrain.ipa`, restore 50/50, push `wod-terrain.dfmod` if the iPad is available. Hand-off: enable the switch on a NEW game (ground height changes), watch the first-launch pause and the diagnostics frame time while walking and on fast-travel arrival, send Player.log (`[WoDTerrain]` timing lines are the measurement).

---

## Self-review
- Spec coverage: design 1 (T3), 2a (T4), 2b-2h (T3), 3 (T1, T2, T6), 4 (T5, T7), 5 (T8, T9). Gap: none.
- Placeholders: T3 asks the implementer to find DFU's default sampler class name (it exists: `DefaultTerrainSampler` in `Assets/Scripts/Terrain/DefaultTerrainSampler.cs` - confirm) - acceptable.
- Type consistency: `Bands` (T3) used by T4; `LocationBufferSize`, `Available` (T3) tested in T3; `TerrainTitle` (T5) used by T8 greps; `Rule.LinearData`/`LinearDataMods` (T2) used in the importer.
- Self-test counts are expectations; the next task's baseline is whatever the previous GREEN reported.
