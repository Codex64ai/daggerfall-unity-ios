# World of Daggerfall - Biomes iOS Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WoD Biomes runs on iOS as a compiled-in port with a private data bundle and its own default-off launcher switch, gated on Daggerfall Expanded Textures, with the two colour-key/point-filter texture hazards and the per-update material leak fixed.

**Architecture:** Same shape as the WoD locations port: three upstream scripts copied under `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/`, started from `MobilePortedMods.StartEnabled` via `StartOne`; the data (224 terrain tiles + `climate_map`) becomes `world of daggerfall - biomes.dfmod` built by `MobileBuildSetup.ApplyAll` from the fetched repo, with a new importer rule that keeps that mod's textures readable and uncompressed; Location Loader gets carademono's `BiomesClimateSwap` back, reading the climate map from the compiled-in Biomes port.

**Tech Stack:** Unity 6000.3.23f1, DFU 1.1.1 fork, C# (IL2CPP iOS), Python 3 pack tooling (`tools/bundled-mods/`), MobileSelfTest (editor batch self-test), iOS Simulator + xcodebuild.

**Spec:** `docs/superpowers/specs/2026-09-09-wod-biomes-port-design.md` (research: `docs/superpowers/research/2026-09-09-wod-biomes.md`).

## Global Constraints

- Branch `unity6-upgrade`, repo `/Users/ikrammassabini/dev/daggerfall-unity`; work directly on it. Every commit ends with the two trailers:
  `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01XFLMiLMMZbzbjem8vgUjYE`. Add files by name; revert Unity reserialization churn (`Assets/Scenes/DaggerfallUnityGame.unity`, `ProjectSettings/ProjectSettings.asset`, `Assets/AddressableAssetsData/iOS/addressables_content_state.bin`) with `git checkout --` before committing.
- ONE Unity Editor at a time: `pgrep -f "Unity.app/Contents/MacOS/Unity"` must be empty before starting one; wait 60 s and retry (up to 10x); never kill processes. NEVER edit source while another agent's Unity run is in progress (a mid-edit broke a bundle build once).
- Self-test: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/ikrammassabini/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>` (full app path; the bare `Unity` on PATH is a different CLI; no `-nographics`). Baseline `=== 633 passed, 0 failed ===`, 0 `error CS`. Tests are `static void TestXxx()` in `Assets/Editor/MobileSelfTest.cs` using `Check(bool, name, detail)`, registered in `RunAll()`. TDD: add checks, one RED run, implement, one GREEN run.
- Tooling tests: `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` (35 OK today; pytest is not installed). `python3 tools/bundled-mods/fetch.py --check` must stay `0 problems`.
- Private only: mods.json entry has `"private_only": true` and a `"licence": "pending:..."` string (pack.py refuses a pending licence in the public pack). Never publish; upload only to the `testapp-unity6` draft of `Codex64ai/daggerfall-unity-ios` with `gh release upload --clobber`.
- Upstream pins: wod-biomes @ `40449fc5fc55c85c8089b068acefe1db4b61534c`; LL `rmb-object` @ `896a5741e5c9badd47fbb6c0f9926a95a79cb776` (for `BiomesClimateSwap.cs`). Biomes GUID `3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`, title `World of Daggerfall - Biomes`, manifest `World of Daggerfall - Biomes.dfmod.json` -> bundle `world of daggerfall - biomes.dfmod` (manifest file name lower-cased by Unity).
- Port headers on every copied file (source URL, commit, "no licence header upstream; private draft only"); every edit marked `// MOBILE:`.
- Staging mirror `~/daggerfall-mobile/Mobile/` (and `editor/`): project is authoritative; before copying a changed file there, diff staging against the pre-commit version - identical -> copy; different -> report NEEDS_CONTEXT.
- Workspace for briefs/reports/ledger: `.superpowers/sdd/2026-09-09-wod-biomes-port/` (gitignored).

---

### Task 1: Pack pipeline entry and fetch (no Unity)

**Files:**
- Modify: `tools/bundled-mods/mods.json` (append after the `WorldOfDaggerfall` entry)
- Test: `tools/bundled-mods/test_fetch.py` (existing `--check` coverage runs against the real file)

**Interfaces:**
- Produces: fetched folder `Assets/Game/Mods/WorldOfDaggerfallBiomes/` (gitignored) with the manifest and 225 PNGs + `.meta`; later tasks import from it. Entry `name` = `WorldOfDaggerfallBiomes` (this is the folder name the importer rule keys on in Task 2).

- [ ] **Step 1: Add the entry**

```json
{
  "name": "WorldOfDaggerfallBiomes",
  "repo": "https://github.com/drcarademono/wod-biomes.git",
  "commit": "40449fc5fc55c85c8089b068acefe1db4b61534c",
  "manifest": "World of Daggerfall - Biomes.dfmod.json",
  "strip_code": true,
  "private_only": true,
  "exclude_globs": ["**/*.7z", "**/*.xcf"],
  "archives_from": ["DaggerfallExpandedTextures"],
  "licence": "pending:No licence is declared upstream (github.com/drcarademono/wod-biomes, World of Daggerfall Team). Permission is being sought; until then this bundle is shipped only on the private test draft, never in the public mod pack. Its three scripts are compiled into the app under Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes (see THIRD-PARTY.md)."
}
```
Check how existing entries spell `exclude_globs` and `archives_from` (read `fetch.py` for the key names) and match them exactly. If the manifest declares dependencies other than `daggerfall expanded textures`, add `"drop_dependencies"` for the ones we do not ship (Vanilla Enhanced is optional; check the manifest's `Dependencies` array and its `IsOptional` flags).

- [ ] **Step 2: Fetch and check**

Run: `python3 tools/bundled-mods/fetch.py --only WorldOfDaggerfallBiomes` (read `fetch.py --help` for the real flag; if there is no per-mod flag, run the full fetch - it is idempotent) then `python3 tools/bundled-mods/fetch.py --check`.
Expected: `Assets/Game/Mods/WorldOfDaggerfallBiomes/World of Daggerfall - Biomes.dfmod.json` exists; `find Assets/Game/Mods/WorldOfDaggerfallBiomes -name "*.png" | wc -l` = 225; `find ... -name "*.cs" | wc -l` = 0; no `.7z`/`.xcf`; `--check` prints `49 mods, 0 problems`.

- [ ] **Step 3: Tooling tests**

Run: `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` -> `OK` (35). Run `python3 tools/bundled-mods/pack.py --check` (or whatever the public-pack dry run is) and confirm the Biomes stem is NOT a pack member.

- [ ] **Step 4: Commit**

```bash
git add tools/bundled-mods/mods.json
git commit -m "Pack pipeline: fetch World of Daggerfall - Biomes (private, no code)"
```

---

### Task 2: Importer rule - Biomes textures stay readable and uncompressed

**Files:**
- Modify: `Assets/Editor/MobileModBuilder.cs:141-172` (`MobileModPackTextureImporter.OnPreprocessTexture`)
- Create: `Assets/Editor/MobileModPackTextureRules.cs`
- Test: `Assets/Editor/MobileSelfTest.cs` (new `TestPackTextureRules`, registered in `RunAll`)

**Interfaces:**
- Produces: `public static class DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModPackTextureRules` with
  `public enum Rule { Default, RawData }` and `public static Rule For(string assetPath)`; `RawData` means: `isReadable = true`, iPhone format `RGBA32`, `mipmapEnabled = false` when the path ends with `/climate_map.png`, `filterMode` untouched (the meta already says Point).

- [ ] **Step 1: Write the failing checks** (append to `MobileSelfTest.cs`, register `TestPackTextureRules();` in `RunAll`)

```csharp
static void TestPackTextureRules()
{
    Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png") == MobileModPackTextureRules.Rule.RawData,
        "PackTextureRules: the Biomes climate map is raw data");
    Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Textures/Terrain/Subtropical/004_0-0.png") == MobileModPackTextureRules.Rule.RawData,
        "PackTextureRules: Biomes terrain tiles are raw data");
    Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfall/Textures/Rocks/rock_01.png") == MobileModPackTextureRules.Rule.Default,
        "PackTextureRules: other mods keep the default ASTC rule");
    Check(MobileModPackTextureRules.For("Assets\\Game\\Mods\\WorldOfDaggerfallBiomes\\Assets\\Maps\\climate_map.png") == MobileModPackTextureRules.Rule.RawData,
        "PackTextureRules: backslash paths are normalized");
    Check(MobileModPackTextureRules.NoMips("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png")
          && !MobileModPackTextureRules.NoMips("Assets/Game/Mods/WorldOfDaggerfallBiomes/Textures/Terrain/Subtropical/004_0-0.png"),
        "PackTextureRules: only the climate map drops mips");
}
```

- [ ] **Step 2: RED run** -> expect `error CS` (type missing). Record the log path.

- [ ] **Step 3: Implement** `Assets/Editor/MobileModPackTextureRules.cs`:

```csharp
// MOBILE: which fetched mods' textures must stay raw. Colour-key maps (read back with GetPixel and
// compared exactly) and point-filtered terrain tiles (DFU decompresses them into an ARGB32
// Texture2DArray anyway) gain nothing from ASTC and break under it.
namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileModPackTextureRules
    {
        public enum Rule { Default, RawData }

        // Mod folder names under Assets/Game/Mods/ (the mods.json entry name).
        static readonly string[] rawDataMods = { "WorldOfDaggerfallBiomes" };

        public static Rule For(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            foreach (string mod in rawDataMods)
                if (p.StartsWith("Assets/Game/Mods/" + mod + "/", System.StringComparison.Ordinal))
                    return Rule.RawData;
            return Rule.Default;
        }

        /// <summary>Colour-key maps are sampled by pixel; a mip chain would only waste memory.</summary>
        public static bool NoMips(string assetPath)
        {
            string p = (assetPath ?? "").Replace('\\', '/');
            return p.EndsWith("/climate_map.png", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
```
and in `MobileModPackTextureImporter.OnPreprocessTexture`, after `var importer = ...` and the trace log:

```csharp
            if (MobileModPackTextureRules.For(path) == MobileModPackTextureRules.Rule.RawData)
            {
                // MOBILE: see MobileModPackTextureRules.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true;
                importer.mipmapEnabled = !MobileModPackTextureRules.NoMips(path);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var raw = importer.GetPlatformTextureSettings("iPhone");
                raw.overridden = true;
                raw.format = TextureImporterFormat.RGBA32;
                raw.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(raw);
                return;
            }
```

- [ ] **Step 4: GREEN run** -> `=== 638 passed, 0 failed ===` (633 + 5), 0 `error CS`.

- [ ] **Step 5: Reimport proof.** Run `MobileBuildSetup.ReimportPacks` once (same Unity command shape, `-executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ReimportPacks`) and then read the two `.meta` files: `Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png.meta` must contain `isReadable: 1`, an `iPhone` platform block with `textureFormat: 4` (RGBA32) and `enableMipMap: 0`; a tile meta must have `isReadable: 1`, `textureFormat: 4`, `enableMipMap: 1`. Quote the lines in the report. (If `ReimportPacks` only touches manifest folders, use `AssetDatabase.ImportAsset` via a tiny throwaway `-executeMethod`, or run the importer through a full `ApplyAll` later in Task 7 and defer this proof there - say which.)

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/MobileModPackTextureRules.cs Assets/Editor/MobileModPackTextureRules.cs.meta Assets/Editor/MobileModBuilder.cs Assets/Editor/MobileSelfTest.cs
git commit -m "Pack importer: raw-data rule keeps the Biomes climate map and terrain tiles readable and uncompressed"
```
Staging: `~/daggerfall-mobile/editor/MobileBuildSetup.cs` is unrelated; copy `MobileSelfTest.cs` to `~/daggerfall-mobile/editor/` after the identical-diff check; add `MobileModPackTextureRules.cs` there too.

---

### Task 3: Port the three Biomes scripts (compile-only, inert) + engine hooks they need

**Files:**
- Create: `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/WODBiomes.cs`, `WODClimates.cs`, `WODTerrainMaterialProvider.cs` (+ `.meta`, fresh GUIDs are fine - no prefab binds them)
- Modify: `Assets/Scripts/Game/Mobile/MobileShaders.cs:20-30` (`names`), `Assets/Scripts/Terrain/DaggerfallBillboardBatch.cs:42,73` (`cachedMaterial`, `currentArchive` -> `internal`)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestMobileShaderNames` or extend the existing shader-names check)

**Interfaces:**
- Produces: `WorldOfDaggerfall.WODBiomes.Init(InitParams)` and `WorldOfDaggerfall.NatureBatchOverriderInstaller.Init(InitParams)` (both `public static`, `[Invoke]` removed); `public static Texture2D WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap` (set in `Init` from `mod.GetAsset<Texture2D>("climate_map")`; Task 6 reads it); `MobileShaders.names` contains `MaterialReader._DaggerfallBillboardBatchShaderName` and `MaterialReader._DaggerfallBillboardBatchNoShadowsShaderName` (check the exact constant names in `Assets/Scripts/MaterialReader.cs`).

- [ ] **Step 1: Copy** the three files from `Assets/Game/Mods/WorldOfDaggerfallBiomes/Scripts/` (they were fetched in Task 1 before `strip_code` runs at bundle time; if the fetch already stripped them, `git show` them from a clone of wod-biomes @ 40449fc in `/Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/wod-biomes`). Add the port header. Remove `[Invoke(...)]` attributes (leave a `// MOBILE: started by MobilePortedMods` comment). Replace every `Shader.Find(` with `DaggerfallWorkshop.Game.Mobile.MobileShaders.Find(`. Replace the three reflection field accesses in `CustomBillboardHelper` with direct assignments (`batch.currentArchive = ...`, `batch.cachedMaterial = ...`, `batch.TextureArchive = ...`) and delete the static constructor + `FieldInfo`s. Add `public static Texture2D ClimateMap;` to `NatureBatchOverriderInstaller` and assign it where the upstream code assigns its `climateMap` static.

- [ ] **Step 2: Engine hooks.** In `DaggerfallBillboardBatch.cs` change `CachedMaterial cachedMaterial;` and `int currentArchive` to `internal` with `// MOBILE: written directly by the compiled-in WoD Biomes atlas helper (was reflection)`. In `MobileShaders.names` add the two billboard-batch shader names (with a comment: DaggerfallBillboardBatch still uses raw Shader.Find for them - pre-existing gap now covered). Also change the two raw `Shader.Find` calls in `DaggerfallBillboardBatch.cs` (~:316-317, :380-381) to `MobileShaders.Find` marked `// MOBILE`.

- [ ] **Step 3: Failing check** (RED): in `MobileSelfTest.cs` find the existing check that enumerates `MobileShaders` names (grep `MobileShaders`); add
```csharp
Check(MobileShaders.Names.Contains(MaterialReader._DaggerfallBillboardBatchShaderName), "MobileShaders: billboard batch shader is captured");
```
If `names` is private, expose `public static IReadOnlyList<string> Names => names;`. RED run: expect the single FAIL (or `error CS` if `Names` is new - then it counts as the red).

- [ ] **Step 4: GREEN run** -> 639/0 (638 + 1), 0 `error CS`. Also confirm the log has no `warning CS` in the three ported files beyond upstream's own.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes Assets/Scripts/Game/Mobile/MobileShaders.cs Assets/Scripts/Terrain/DaggerfallBillboardBatch.cs Assets/Editor/MobileSelfTest.cs
git commit -m "Port sources: World of Daggerfall - Biomes (code only, inert); billboard batch shaders captured by MobileShaders"
```
Staging: copy `MobileShaders.cs` to `~/daggerfall-mobile/Mobile/` after the identical-diff check.

---

### Task 4: Harden the nature-batch overrider (no per-update leaks, safe climate map)

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/WODClimates.cs`
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestBiomesClimateKey`)

**Interfaces:**
- Produces: `public static bool WorldOfDaggerfall.NatureBatchOverrider.IsSubtropicalKey(Color32 c)` (pure: `c.r == 255 && c.g == 165 && c.b == 0`); `public static bool WorldOfDaggerfall.NatureBatchOverrider.MapReadable(Texture2D map)` (pure: `map != null && map.isReadable && map.width > 0`); `public const int NatureBatchOverrider.AtlasMaxSize = 1024`.

- [ ] **Step 1: Failing checks**
```csharp
static void TestBiomesClimateKey()
{
    Check(NatureBatchOverrider.IsSubtropicalKey(new Color32(255, 165, 0, 255)), "Biomes: #FFA500 is the subtropical key");
    Check(!NatureBatchOverrider.IsSubtropicalKey(new Color32(254, 165, 0, 255)), "Biomes: an off-by-one colour is not the key (exact match - this is why the map must not be ASTC)");
    Check(!NatureBatchOverrider.MapReadable(null), "Biomes: a missing climate map is not readable");
    Check(NatureBatchOverrider.AtlasMaxSize == 1024, "Biomes: atlas capped at 1024 (32 records of 64x64)");
}
```
RED run -> `error CS`.

- [ ] **Step 2: Implement** in `WODClimates.cs`, all `// MOBILE`:
  - `ApplyOverrides`: replace `FindObjectsOfType<DaggerfallBillboardBatch>()` with a walk of `GameManager.Instance.StreamingWorld.StreamingTarget` children (`GetComponentsInChildren<DaggerfallBillboardBatch>(true)`) - read `StreamingWorld.cs` for the right root (`StreamingTarget`) and confirm location batches are under it too; at the top: `if (!MapReadable(NatureBatchOverriderInstaller.ClimateMap)) { if (!warned) { Debug.LogWarning("[Biomes] climate map missing or not readable; nature swap disabled"); warned = true; } return; }`.
  - Keep a `static readonly HashSet<DaggerfallBillboardBatch> swapped` ; skip batches already in it; add after a successful swap; prune destroyed entries (`RemoveWhere(b => b == null)`) at the start of each update.
  - `CustomBillboardHelper`: cache the `Material` next to the atlas (`Dictionary<int, Material> materialCache`), reuse it; call `RevisedSetMaterial(batch, NEW_ARCHIVE, force: false)`.
  - `atlasMaxSize` (wherever `new Texture2D(4096, 4096 ...)`/`PackTextures(..., 4096)` appears) -> `AtlasMaxSize`.
  - Read pixels via `TextureReplacement.EnsureReadable(map).GetPixel(...)` (keep the same coordinates/flip).
  - Wrap the per-update body in `try { ... } catch (System.Exception ex) { if (loggedErrors.Add(ex.Message)) Debug.LogError("[Biomes] nature swap failed: " + ex); }`.
  - Add `Debug.Log("[Biomes] swapped " + n + " nature batches to archive 10030")` only when `n > 0` (Task 9 greps for it).

- [ ] **Step 3: GREEN run** -> 643/0, 0 `error CS`.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/WODClimates.cs Assets/Editor/MobileSelfTest.cs
git commit -m "Biomes: nature swap walks the streaming root, caches materials, swaps each batch once, tolerates an unreadable map"
```

---

### Task 5: Launcher entry, gate and start-up

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/MobilePortedMods.cs:34-68` (consts, `Titles`), `:236-268` (gate + start block)
- Test: `Assets/Editor/MobileSelfTest.cs:763-780` (`TestPortedModTitles`)

**Interfaces:**
- Produces: `public const string BiomesTitle = "World of Daggerfall - Biomes"`, `public const string BiomesDetNote`, `public static bool BiomesRuns(bool biomesOn, bool detOn) => biomesOn && detOn`, `public static bool BiomesRunning` (static flag set true only after both Inits returned; Task 6 reads it), `Titles` += `BiomesTitle`.

- [ ] **Step 1: Failing checks** (append inside `TestPortedModTitles`)
```csharp
Check(System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.BiomesTitle) >= 0, "PortedMods: Biomes is default-off");
Check(MobilePortedMods.BiomesRuns(true, true) && !MobilePortedMods.BiomesRuns(true, false) && !MobilePortedMods.BiomesRuns(false, true), "PortedMods: Biomes runs only with its switch and Daggerfall Expanded Textures on");
Check(MobilePortedMods.BiomesDetNote.Contains("Expanded Textures"), "PortedMods: Biomes note names the missing dependency");
```
RED run -> `error CS`.

- [ ] **Step 2: Implement.** Consts:
```csharp
public const string BiomesTitle = "World of Daggerfall - Biomes";
public const string BiomesDetNote = " Needs Daggerfall Expanded Textures switched on; it was switched off because that mod is off or not installed.";
public static bool BiomesRuns(bool biomesOn, bool detOn) => biomesOn && detOn;
/// <summary>True once both Biomes Inits returned this session; Location Loader's type-5 nature swap keys on it.</summary>
public static bool BiomesRunning;
```
`Titles = { RRTitle, RRItemsTitle, CCTitle, SkyTitle, LLTitle, WoDTitle, BiomesTitle }`. After the WoD start block and before `return SkyRuns(...)`:
```csharp
            Mod biomes = Entry(BiomesTitle);
            bool biomesChosen = biomes != null && biomes.Enabled;
            if (biomesChosen && !detOn)
            {
                biomes.Enabled = false;
                if (!(biomes.ModInfo.ModDescription ?? "").Contains(BiomesDetNote)) biomes.ModInfo.ModDescription += BiomesDetNote;
                ModManager.WriteModSettings();
                Debug.Log("[PortedMods] World of Daggerfall - Biomes off: Daggerfall Expanded Textures is not enabled");
            }
            if (BiomesRuns(biomes != null && biomes.Enabled, detOn))
            {
                int bi = ModManager.Instance.GetModIndex(BiomesTitle);
                bool a = StartOne(BiomesTitle + " (terrain)", () => WorldOfDaggerfall.WODBiomes.Init(new InitParams(biomes, bi, count)));
                bool b = a && StartOne(BiomesTitle + " (nature)", () => WorldOfDaggerfall.NatureBatchOverriderInstaller.Init(new InitParams(biomes, bi, count)));
                BiomesRunning = a && b;
                if (BiomesRunning) Debug.Log("[PortedMods] started " + BiomesTitle);
            }
```
Note `StartOne` already logs `started <title>` per call; keep those (they read `started World of Daggerfall - Biomes (terrain)` etc.) and add the summary line above. Confirm `DetOn` is computed before this block (it is, at `:238`).

- [ ] **Step 3: GREEN run** -> 646/0.

- [ ] **Step 4: Commit**
```bash
git add Assets/Scripts/Game/Mobile/MobilePortedMods.cs Assets/Editor/MobileSelfTest.cs
git commit -m "World of Daggerfall - Biomes: launcher entry off by default, gated on Daggerfall Expanded Textures, started after WoD"
```
Staging: copy `MobilePortedMods.cs` after the identical-diff check.

---

### Task 6: Location Loader type-5 nature swap (BiomesClimateSwap backport)

**Files:**
- Create: `Assets/Scripts/Game/Mobile/Ports/LocationLoader/BiomesClimateSwap.cs` (from LL `rmb-object` @ 896a574 `Scripts/BiomesClimateSwap.cs`, 101 lines)
- Modify: `Assets/Scripts/Game/Mobile/Ports/LocationLoader/LocationLoader.cs` (type-5 branch, after the transform assignment, where upstream had `if (LocationModLoader.WODBiomesModEnabled) BiomesClimateSwap.ApplySwaps(rmbBlock);`)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestBiomesClimateSwapGuard`)

**Interfaces:**
- Consumes: `WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap` (Task 3), `MobilePortedMods.BiomesRunning` (Task 5), `NatureBatchOverrider.IsSubtropicalKey` / `MapReadable` (Task 4).
- Produces: `public static bool LocationLoader.BiomesClimateSwap.ShouldSwap(bool biomesRunning, Texture2D map)` (pure gate: `biomesRunning && NatureBatchOverrider.MapReadable(map)`).

- [ ] **Step 1: Copy** `BiomesClimateSwap.cs` with a port header naming the fork + commit; replace its `LocationModLoader.climate_map` reads with `WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap`; reuse `NatureBatchOverrider.IsSubtropicalKey` for the colour test and `CustomBillboardHelper` for the archive-10030 material (read how upstream's version builds the material - if it duplicates the atlas builder, call the ported helper instead and say so). Keep its `pendingBlocks` retry on `StreamingWorld.OnUpdateTerrainsEnd` but make the subscription happen once (static flag).

- [ ] **Step 2: Failing check**
```csharp
static void TestBiomesClimateSwapGuard()
{
    Check(!LocationLoader.BiomesClimateSwap.ShouldSwap(false, null) && !LocationLoader.BiomesClimateSwap.ShouldSwap(true, null),
        "LL: type-5 nature swap needs Biomes running and a readable map");
}
```
RED -> `error CS`.

- [ ] **Step 3: Wire** in `LocationLoader.cs` type-5 branch, right after `rmbBlock.transform.localScale = obj.scale;`:
```csharp
                            // MOBILE: WoD Biomes' subtropical nature swap for freshly built RMB blocks
                            // (carademono's LL rmb-object branch; the map comes from the compiled-in Biomes port).
                            if (BiomesClimateSwap.ShouldSwap(DaggerfallWorkshop.Game.Mobile.MobilePortedMods.BiomesRunning,
                                                             WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap))
                                BiomesClimateSwap.ApplySwaps(rmbBlock);
```
(inside the existing try/catch).

- [ ] **Step 4: GREEN run** -> 647/0.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/Game/Mobile/Ports/LocationLoader/BiomesClimateSwap.cs Assets/Scripts/Game/Mobile/Ports/LocationLoader/BiomesClimateSwap.cs.meta Assets/Scripts/Game/Mobile/Ports/LocationLoader/LocationLoader.cs Assets/Editor/MobileSelfTest.cs
git commit -m "Location Loader: RMB blocks get the Biomes subtropical nature swap when Biomes is running"
```

---

### Task 7: Build the bundle, verify import settings, upload

**Files:** none committed (bundle + backup only)

- [ ] **Step 1:** Preconditions: tree clean; `pgrep` empty; `Assets/StreamingAssets/Mods/*.dfmod` = 48 (ASTC). Run the FULL `MobileBuildSetup.ApplyAll` (no env vars) to `.superpowers/sdd/2026-09-09-wod-biomes-port/applyall-biomes.log` (~25 min). Expected: `[MobileBuildSetup] bundled mods: 49`, `Assets/StreamingAssets/Mods/world of daggerfall - biomes.dfmod` present with `world of daggerfall - biomes-LICENSE.txt` (pending text) beside it, 0 `error CS`.
- [ ] **Step 2:** Import-settings proof (the load-bearing check): `Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png.meta` shows `isReadable: 1`, iPhone `textureFormat: 4`, `enableMipMap: 0`; a `004_0-0.png.meta` shows `isReadable: 1`, `textureFormat: 4`. Then `python3 tools/bundled-mods/dfmod_inspect.py "Assets/StreamingAssets/Mods/world of daggerfall - biomes.dfmod"`: textures 225, formats all RGBA32 (no ASTC), size ~5 MB. Quote the lines.
- [ ] **Step 3:** Refresh `~/dev/dfu-mods/astc-backup/` (49 bundles) and `~/dev/dfu-mods/astc-backup/Licenses/` (49 records). Copy the bundle to `~/dev/dfu-mods/world of daggerfall - biomes.dfmod`. `gh release upload testapp-unity6 "Assets/StreamingAssets/Mods/world of daggerfall - biomes.dfmod" --repo Codex64ai/daggerfall-unity-ios --clobber`; verify with `gh release view ... --json assets`.
- [ ] **Step 4:** Self-test -> 647/0. `git status --short` empty after reverting churn.

---

### Task 8: Docs

**Files:**
- Modify: `THIRD-PARTY.md` (new `## World of Daggerfall - Biomes` after the WoD section: table row, pins, no licence, private draft, what it does, DET requirement, the raw-texture rule and why, `BiomesClimateSwap` provenance, not device-verified), `UPSTREAM-PATCHES.md` (entry `### World of Daggerfall - Biomes support (2026-09-09)`: files + counts from `git diff --numstat`, `DaggerfallBillboardBatch` internal fields + `MobileShaders` names as the engine touch points, the importer rule, the gate; rebase risk LOW), `~/daggerfall-mobile/README-iOS.md` (`### World of Daggerfall - Biomes` after `### World of Daggerfall`: one switch, one bundle, needs Expanded Textures on, what changes visually: Subtropical/Dak'fron/Hammerfell-mountain/Haunted-Woodlands ground, Makija palms in the orange-keyed subtropics; off = simply off next launch, Player.log says why; not device-verified).
- Commit: `Docs: World of Daggerfall - Biomes`.

---

### Task 9: Simulator verification

Follow the recipe in `~/dev/dfu-mods/wod-evidence/sdd/task-5-report.md` / `task-5-brief.md` (RGBA32 sim bundle: the Biomes bundle is ALREADY RGBA32 by rule, so no rebuild is needed for it; the sim app is `DFU_IOS_TESTAPP=1 DFU_IOS_SIM=1 DFU_BUNDLED_MODS=builtin DFU_PACK_TEX_FORMAT=RGBA32` ApplyAll + BuildIOS + xcodebuild; restore 49 bundles + 49 licences after).
- Container: `world of daggerfall - biomes.dfmod` + `daggerfall expanded textures.dfmod` in `Documents/Mods`; `debug-newchar.txt` = a subtropical coastal pixel: find one by scanning the fetched `climate_map.png` for `#FFA500` with Python (`PIL` may not be installed; parse via `zlib`/`png` or use `sips`/`python3 -c` with the `png` chunks - or simply pick the Sentinel coast (region "Sentinel") and check the colour), print the chosen `pixel X Y`.
- Launches: A first run (entry default OFF, `Mods.json` shows `World of Daggerfall - Biomes` `Enabled:false`); B Biomes ON, DET present: expect `[PortedMods] started World of Daggerfall - Biomes (terrain)`, `... (nature)`, `[PortedMods] started World of Daggerfall - Biomes`, and after render `[Biomes] swapped N nature batches to archive 10030` with N > 0, no `[Biomes] ... failed`, no `Exception`; screenshot; C gate: DET moved out -> `[PortedMods] World of Daggerfall - Biomes off: Daggerfall Expanded Textures is not enabled`; D both LL+WoD+Biomes on at the same pixel: no new errors, `[LL]`/RMB lines unchanged.
- Report timings and the ground texture archive in use if the log says (grep `104`/`004` material lines if any).

---

### Task 10: Device build

Follow `~/dev/dfu-mods/wod-evidence/sdd/task-7-brief.md` verbatim with `DFU-Test-unity6-biomes.ipa`; restore 49 bundles + 49 licences; upload; install + push the Biomes bundle only if the iPad is available; hand-off notes for Ikram: enable `World of Daggerfall - Biomes` (Expanded Textures on), fast-travel to Sentinel or Alik'r Desert coast, look for the new palms and the desert/subtropical ground; Hammerfell mountains for the 104 tileset; diagnostics frame time; Player.log `[Biomes]` lines.

---

## Self-review

- Spec coverage: design 1 (Task 3+4), 2 (Task 5), 3 (Task 1+2+7), 4 (Task 6), 5 (Task 4+5 try/catch, StartOne), 6 (Task 2 step 5, Task 7 step 2, Task 9, Task 10), docs (Task 8). Gap: none.
- Placeholders: Task 1 asks the implementer to read `fetch.py` for exact key names (they exist: `exclude_globs`, `archives_from`, `drop_dependencies` are used by the WoD entry) - acceptable; Task 9 pixel selection is a documented procedure, not a TBD.
- Type consistency: `NatureBatchOverriderInstaller.ClimateMap` (Task 3) used by Task 6; `NatureBatchOverrider.IsSubtropicalKey/MapReadable/AtlasMaxSize` (Task 4) used by Task 6; `MobilePortedMods.BiomesRunning/BiomesTitle/BiomesRuns/BiomesDetNote` (Task 5) used by Tasks 6, 9; `MobileModPackTextureRules.For/NoMips/Rule` (Task 2) used in the importer.
- Self-test counts are cumulative expectations (633 -> 638 -> 639 -> 643 -> 646 -> 647); if a task adds a different number of checks, the next task's baseline is whatever the previous GREEN reported.
