# Real Grass iOS Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Real Grass runs on iOS as a compiled-in, default-off port in its cheap configuration (Classic style, billboards, 128/16 detail resolution, cached layer arrays), with Unity's terrain detail shaders pinned into the build and a small textures-only bundle.

**Architecture:** Same shape as the WoD-family ports: upstream C# under `Assets/Scripts/Game/Mobile/Ports/RealGrass/`, started from `MobilePortedMods.StartEnabled` via the 4-arg `StartOne`; the bundle `realgrass.dfmod` holds only the Classic textures; the three `Hidden/TerrainEngine/Details/*` shaders are pinned Always-Included; forced settings live in code constants (tests pin them).

**Tech Stack:** Unity 6000.3.23f1 built-in pipeline terrain detail renderer, DFU 1.1.1 fork, IL2CPP iOS/Metal, Python pack tooling, MobileSelfTest.

**Spec:** `docs/superpowers/specs/2026-09-10-real-grass-port-design.md` (research: `docs/superpowers/research/2026-09-10-grass-and-crt.md` Part A).

## Global Constraints
- Branch `unity6-upgrade`; commits end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01XFLMiLMMZbzbjem8vgUjYE`; files by name; revert scene/ProjectSettings.asset/addressables churn (GraphicsSettings.asset pin changes ARE committed).
- ONE Unity Editor at a time (`pgrep -f "Unity.app/Contents/MacOS/Unity"` empty; wait 60 s up to 10x; kill nothing). Never edit source while another agent's Unity run is in progress.
- Self-test: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/ikrammassabini/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>` (no -nographics). Baseline: read the latest count from the ledger (>= 893/0 after the follow-ups); after Task 1's fetch the `one bundle per fetched manifest` check fails by name until Task 4 - quote it.
- Tooling: `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` OK (41); `fetch.py --check` 0 problems.
- Upstream pin: github.com/TheLacus/daggerfall-unity-mods @ `556ef6e` (Task 1 resolves the full sha of the commit `556ef6e` ("Bump version", 2023-08-25) and records it; manifest `RealGrass.dfmod.json`, title `Real Grass`, GUID `2185b00e-bc5d-4758-81f5-7540817e2cbc`.
- Port headers: MIT (Uncanny_Valley, TheLacus) - keep the upstream MIT header text on each file; every edit `// MOBILE:`.
- Workspace: `.superpowers/sdd/2026-09-10-real-grass-port/`.

---

### Task 1: Pack pipeline entry and fetch (no Unity)
**Files:** `tools/bundled-mods/mods.json`
**Interfaces:** fetched folder `Assets/Game/Mods/RealGrass/` with the manifest and ONLY the textures the Classic+Billboard path loads (+ `.meta`); folder name `RealGrass`; licence record MIT from the repo `LICENSE`; a durable copy of `RealGrass/Scripts/*.cs`, `RealGrass/modsettings.json`, `modpresets.json` and the full manifest under `.superpowers/sdd/2026-09-10-real-grass-port/upstream-realgrass-<sha7>/`.
- [ ] Step 1: clone the monorepo (depth 200) into /Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/research-grass/ (reuse if present), resolve the pin sha; read `RealGrass/RealGrass.dfmod.json` `Files` (paths may point into `RealGrassAssets/`), and read `DetailPrototypesManager.cs` to list exactly which asset names the **Classic** style with **Billboard** prototypes loads (`mod.GetAsset<Texture2D>(...)` names; ignore Mixed/Full/Stones/WaterPlants/Fireflies assets). Record the list and the licence of each kept texture from `RealGrassAssets/credits.txt`/README (must be CC0/MIT; anything VMblast is excluded).
- [ ] Step 2: entry: `"name":"RealGrass", "repo":..., "commit":<sha>, "subdir":"RealGrass", "manifest":"RealGrass.dfmod.json", "strip_code":true, "exclude_globs":[<every non-kept asset basename or pattern: "*.psd","Rock*.png","*.fbx","*.prefab","*.mat","Fireflies*", ...>], "licence": "MIT ..." ` (if the manifest references files outside `RealGrass/` use `extra_roots`/the mechanism fetch.py offers - read it; if kept textures' licence is not provably permissive, use `private_only` + `pending:` and say so). Confirm the rewritten manifest lists exactly the kept files (the builder throws on missing files).
- [ ] Step 3: fetch, `--check` 0 problems (52 mods), unittest OK, pack dry-run membership as the licence dictates. Commit `Pack pipeline: fetch Real Grass (Classic textures only)`.

### Task 2: Port the code (inert) with the cheap configuration + shader pins
**Files:** `Assets/Scripts/Game/Mobile/Ports/RealGrass/{RealGrass.cs,DensityManager.cs,DetailPrototypesManager.cs,Range.cs}`, `Assets/Editor/MobileBuildSetup.cs` (`EnsureAlwaysIncludedShaders` += the three `Hidden/TerrainEngine/Details/*` names - verify the exact shader names in this Unity version via `Shader.Find` in the Editor and the built-in shader list), `Assets/Shaders/RequiredShaderVariants.shadervariants` (only if those shaders declare keywords), `ProjectSettings/GraphicsSettings.asset`, `Assets/Editor/MobileSelfTest.cs`
**Interfaces:** `public static void RealGrass.RealGrassPort.Init(InitParams)` (rename of `RealGrass.Init`, no `[Invoke]`), `public static bool RealGrassPort.Installed` (true after `OnPromoteTerrainData` subscribed), `public static bool RealGrassPort.Available(bool shadersFound, bool grassTexturePresent)`, constants `RealGrassPort.DetailResolution = 128`, `DetailResolutionPerPatch = 16`, `ForcedStyle = Classic`, `ForcedBillboard = true`, `DefaultDetailDistance = 40f`, `DefaultDetailDensity = 0.6f`, pure `static int DetailPatchesPerTerrain(int res, int perPatch) => (res/perPatch)*(res/perPatch)`; counter line `[RealGrass] details on N terrains` (every 25th promotion) and once `[RealGrass] detail data ~N MB (res R, layers L, terrains T, scatter <mode>/<max>)` (the scatter fields added by the Task 2 review's C1).
- [ ] Step 1: copy, MIT headers kept + port header; `[Invoke]` removed; console commands dropped; `SetDetailResolution(256, 8)` -> `(DetailResolution, DetailResolutionPerPatch)`; every `new int[256,256]`/`EmptyMap` -> a cached `int[DetailResolution,DetailResolution]` per layer cleared with `Array.Clear` (allocated once); style/billboard/stones/water plants/insects forced by constants (the settings loader still runs so the two dials `DetailDistance`/`DetailDensity` come from settings with the new defaults - if the settings API insists on the shipped modsettings.json defaults, clamp/override in code and say so); promotion handler in try/catch (log once per message); `Available` gate in Init (`Shader.Find` x3 non-null, grass texture from `mod.GetAsset` non-null) else `[RealGrass] not available: <reason>`.
- [ ] Step 2: pins + checks: `TestRealGrassPort` - constants, `DetailPatchesPerTerrain(128,16) == 64` and `(256,8) == 1024` (documents the win), `Available` truth table, no `[Invoke]`, the three shader names present in `GraphicsSettings.asset`'s Always Included list (read the asset text or `GraphicsSettings.GetGraphicsSettings()`), `Shader.Find` of each non-null in the Editor. RED/GREEN. Run `MobileBuildSetup.ApplyIOSSettings` to write the pin, commit the GraphicsSettings.asset change.
- [ ] Step 3: commit `Port sources: Real Grass (inert; Classic billboards, 128/16 detail resolution, cached layers); terrain detail shaders pinned`.

### Task 3: Launcher entry
**Files:** `MobilePortedMods.cs` (`GrassTitle = "Real Grass"`, `Titles` += it, block after Distant Terrain before `return SkyRuns`, 4-arg StartOne with `() => RealGrassPort.Installed`, hint `[RealGrass]`), `MobileSelfTest.cs` (Titles membership, literal == manifest title). RED/GREEN. Commit `Real Grass: launcher entry off by default`.

### Task 4: Bundle build + upload
Unscoped `ReimportPacks` (the importer now versions its rules), full `ApplyAll` -> 52 bundles; `tools/dfmod_inspect.py realgrass.dfmod`: only the kept textures (ASTC ok) + manifest; self-test flips to N/0; backups 52/52 (+ Licenses metas); copy to `~/dev/dfu-mods/realgrass.dfmod`; upload (asset name `realgrass.dfmod`, no spaces).

### Task 5: Docs
THIRD-PARTY.md (MIT; Classic textures CC0; what was dropped; the cheap configuration numbers; pins; not device-verified), UPSTREAM-PATCHES.md (no engine file expected; the GraphicsSettings pin), README-iOS.md `### Real Grass` (switch, bundle, the two dials, stacks with the terrain mods, grass appears on tiles promoted after the switch is on).

### Task 6: Simulator verification
Recipe as the WoD ones (52/52 restore). Launches at 207,213: default off; grass ON alone -> `started Real Grass`, `[RealGrass] details on N terrains`, `detail data ~N MB`, screenshot with grass billboards in the foreground (crop), no `not available`/`failed`; grass + WoD Terrain + Distant: no new errors, memory line; negative.

### Task 7: Device build
`DFU-Test-unity6-grass.ipa` (or combined with the CRT plan's build if both are ready - controller decides), restore 52/52, upload, install if connected. Hand-off: enable Real Grass, walk on grassland (Daggerfall/Wayrest), watch frame time on tile crossings and memory after fast travel with Terrain + Distant on.

## Self-review
Spec 1 (T2), 2 (T2), 3 (T1, T4), 4 (T3, T5), 5 (T6, T7). Interfaces: `RealGrassPort.Init/Installed/Available` (T2) used by T3; constants tested in T2; `GrassTitle` (T3) used by T6 greps. Counts are expectations; the next task's baseline is the previous GREEN.
