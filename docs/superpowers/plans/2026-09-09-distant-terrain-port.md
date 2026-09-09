# Distant Terrain (WoD flavour) iOS Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Distant Terrain's far-terrain renderer runs on iOS as a compiled-in port whose shader samples three texture arrays instead of twelve 2048² atlases, with the fly-map and spell dropped, an iOS low preset, a deterministic start order with Dynamic Skies, and its own default-off launcher switch; data (three CSVs + the river/coast map) ships as a private bundle.

**Architecture:** Same shape as the other WoD-family ports (C# under `Ports/DistantTerrain/`, shader under `Assets/Shaders/DistantTerrain/` compiled into the app and pinned Always-Included, data bundle built by `ApplyAll` from the fetched repo with a `RawData` importer rule for the deriv map). The one piece of new engineering is the texture-array rewrite of `FarTerrainCommon.cginc` + `DistantTerrainTilemap.shader`, fed by DFU's own `TextureReader.GetTerrainTextureArray(archive, TextureMap.Albedo)` for the twelve archives, packed into three 224-slice arrays (or twelve 56-slice arrays if packing costs more than it saves - Task 3 decides and records why).

**Tech Stack:** Unity 6000.3.23f1, DFU 1.1.1 fork, C# (IL2CPP iOS, Metal), ShaderLab/HLSL surface shaders, Python 3 pack tooling, MobileSelfTest, iOS Simulator + xcodebuild.

**Spec:** `docs/superpowers/specs/2026-09-09-distant-terrain-port-design.md` (research: `docs/superpowers/research/2026-09-09-distant-terrain-wod.md`).

## Global Constraints

- Branch `unity6-upgrade`, repo `/Users/ikrammassabini/dev/daggerfall-unity`; work directly on it. Every commit ends with the two trailers `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01XFLMiLMMZbzbjem8vgUjYE`. Add files by name; revert Unity reserialization churn (`Assets/Scenes/DaggerfallUnityGame.unity`, `ProjectSettings/ProjectSettings.asset`, `Assets/AddressableAssetsData/iOS/addressables_content_state.bin`) with `git checkout --` before committing. `ProjectSettings/GraphicsSettings.asset` changes from shader pinning ARE committed (precedent: Dynamic Skies).
- ONE Unity Editor at a time: `pgrep -f "Unity.app/Contents/MacOS/Unity"` must be empty before starting one; wait 60 s and retry (up to 10x); never kill processes. NEVER edit source while another agent's Unity run is in progress.
- Self-test: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/ikrammassabini/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>`. Baseline `=== 689 passed, 0 failed ===`, 0 `error CS`. After Task 1's fetch the check `one bundle per fetched manifest ...` fails until Task 7 builds the bundle - quote that FAIL by NAME in every GREEN until then. Tests are `static void TestXxx()` in `Assets/Editor/MobileSelfTest.cs` using `Check(bool, name, detail)`, registered in `RunAll()`. TDD: RED then GREEN.
- Tooling tests: `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` (35 OK); `python3 tools/bundled-mods/fetch.py --check` must stay `0 problems`.
- Private only: `"private_only": true` + `"licence": "pending:..."`. Upload only to the `testapp-unity6` draft (controller uploads if `gh` is classifier-blocked).
- Upstream pin: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ `d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f` (vendored upstream drop a722b33; her build scripts are not used). MIT base: Nystul-the-Magician/dfunity-mods `DistantTerrain/` @ `fc58546c3eae964babfdfeea51e97a47ba57cdce` (for provenance and for `TransitionRingTilemapTextureArray.shader` as a texture-array reference). Read the manifest in the clone for the exact `ModTitle`, GUID and manifest file name before writing constants (the research says title `Distant Terrain`; confirm).
- Port headers on every copied file (MIT base + WoD-flavour provenance, "WoD-flavour additions carry no licence; private draft only"); every edit marked `// MOBILE:`.
- Staging mirror: refreshed once by the controller at plan end.
- Workspace: `.superpowers/sdd/2026-09-09-distant-terrain-port/` (gitignored). Clone for reference: `/Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/research-distant/` (re-clone at the pins if missing; keep a durable copy of `Scripts/`, `Shaders/`, `modsettings.json` and the manifest under the workspace as `upstream-distant-d454b30/`).

---

### Task 1: Pack pipeline entry and fetch (no Unity)

**Files:** `tools/bundled-mods/mods.json`

**Interfaces:** Produces fetched folder `Assets/Game/Mods/DistantTerrainWoD/` with the manifest, 3 CSV TextAssets, `daggerfall_deriv_map.png` (+meta), `modsettings.json`; no `.cs`, no `.shader`/`.cginc`, no `.prefab`, no `.png~`, none of the three unused `*.bin.txt`. Folder name `DistantTerrainWoD` is the importer-rule key (Task 2). Save the durable upstream copy for Tasks 3-4.

- [ ] Step 1: entry (adapt to the manifest's real file name; `exclude_globs` are basename globs; `strip_code` strips only `.cs`/`.dll`):
```json
{
  "name": "DistantTerrainWoD",
  "repo": "https://github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall.git",
  "commit": "d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f",
  "manifest": "<manifest file name from the clone>",
  "strip_code": true,
  "private_only": true,
  "exclude_globs": ["*.shader", "*.cginc", "*.prefab", "*.png~", "mapLocationRangeX.bin.txt", "mapLocationRangeY.bin.txt", "mapTreeCoverage.bin.txt", "DaggerfallBillboardBatchFaded.shader"],
  "licence": "pending:Base is Nystul-the-Magician's Distant Terrain (MIT, 2017). The World of Daggerfall-flavour additions (MaoDeVaca, Nexus 1284; only versioned copy github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall) declare no licence. Permission is being sought; until then this bundle is shipped only on the private test draft, never in the public mod pack. Its C# is compiled into the app under Assets/Scripts/Game/Mobile/Ports/DistantTerrain and its shader (rewritten onto texture arrays) under Assets/Shaders/DistantTerrain (see THIRD-PARTY.md)."
}
```
The repo root may not be the mod root (the vendored drop may sit in a subfolder) - read `fetch.py` for a `subdir`/root option or how `extra_roots` works and use whichever the tooling supports; say which. If the manifest lists files the globs remove, confirm the rewritten manifest no longer names them (the builder throws on missing files).
- [ ] Step 2: fetch, `--check` -> `51 mods, 0 problems`; unittest OK; pack dry run excludes the stem.
- [ ] Step 3: commit `Pack pipeline: fetch Distant Terrain (WoD flavour; private, data only)`.

---

### Task 2: Spike - iOS compile of the ORIGINAL shader (informative) + importer rule for the deriv map

**Files:** `Assets/Shaders/DistantTerrain/DistantTerrainTilemap.shader`, `FarTerrainCommon.cginc` (verbatim upstream copies + provenance comment), `Assets/Editor/MobileModPackTextureRules.cs`, `MobileModBuilder.cs`, `MobileSelfTest.cs`

**Interfaces:** Produces the spike record `.superpowers/sdd/2026-09-09-distant-terrain-port/spike-metal-compile.md` (compiled yes/no for Metal iOS, sampler count reported, every warning/error verbatim); `Rule.RawData` extended to folder `DistantTerrainWoD` with a per-folder `maxTextureSize` hook: `public static int MaxSize(string assetPath)` returning 8192 for `DistantTerrainWoD` and 2048 otherwise (the deriv map is 5000x2500 and read by pixel - a 2048 clamp would quantise the coastlines), mips off (`NoMips` matches `/daggerfall_deriv_map.png` too).

- [ ] Step 1: copy the two shader files verbatim into `Assets/Shaders/DistantTerrain/`; add the shader name to the `EnsureAlwaysIncludedShaders` list in `MobileBuildSetup.cs` ONLY after the rewrite (Task 3) - for the spike, compile it directly: switch the Editor to iOS (`MobileBuildSetup.SwitchToIOS`) and run a throwaway `-executeMethod` that calls `ShaderUtil.CompilePass`/`ShaderUtil.GetShaderMessages` on the shader for `BuildTarget.iOS` (or the equivalent in `UnityEditor.ShaderUtil` for 6000.3 - read the API; if no per-target compile API exists, use `ShaderUtil.OpenCompiledShader` with Metal selected and grep the output), and record sampler count + messages. If Unity cannot give a per-target answer in batch mode, defer the spike verdict to Task 9's device build and say so - the rewrite in Task 3 proceeds regardless.
- [ ] Step 2: RED/GREEN for the importer rule checks (deriv map -> RawData, MaxSize 8192 for the folder, 2048 default, NoMips true for the deriv map; Biomes unchanged); one-asset reimport proof on `daggerfall_deriv_map.png.meta` (`isReadable: 1`, `textureFormat: 4`, `maxTextureSize: 8192`, `enableMipMap: 0`).
- [ ] Step 3: commit `Distant Terrain: original shader copied for the Metal spike; deriv map imports raw at full size` (record the spike verdict in the report and the ledger).

---

### Task 3: Texture-array rewrite of the far-terrain shader

**Files:** `Assets/Shaders/DistantTerrain/FarTerrainCommon.cginc`, `DistantTerrainTilemap.shader`, `Assets/Shaders/RequiredShaderVariants.shadervariants`, `Assets/Editor/MobileBuildSetup.cs` (`EnsureAlwaysIncludedShaders` list), `ProjectSettings/GraphicsSettings.asset` (pin result), `Assets/Editor/MobileSelfTest.cs`

**Interfaces:** Produces shader `Daggerfall/DistantTerrain/DistantTerrainTilemap` with array uniforms `_TileArraySummer`, `_TileArrayWinter`, `_TileArrayRain` (`UNITY_DECLARE_TEX2DARRAY`) and int uniform `_SlicesPerBiome` (56); a pure C# helper (in Task 4's `DistantTerrain.cs` but specified here) `public static int SliceIndex(int biome /*0 desert,1 mountain,2 woodland,3 swamp*/, int record /*0..55*/) => biome * 56 + record`; the C# side binds the three arrays built from `TextureReader.GetTerrainTextureArray(archive, TextureMap.Albedo)` for archives {2,102,302,402} (summer), {3,103,303,403} (winter), {4,104,304,404} (rain) - if `GetTerrainTextureArray` returns one 56-slice array per archive, either copy slices into a 224-slice array with `Graphics.CopyTexture` (same format/size required) or keep twelve arrays and twelve samplers (only if CopyTexture is impossible; then the sampler count is 12 arrays + tilemap + sky = 14, still under 16; record the decision).

- [ ] Step 1: read the original `getColorByTextureAtlasIndex` and every `tex2D*` in the cginc; write the replacement: slice = `SliceIndex(biome, record)`, sample with `UNITY_SAMPLE_TEX2DARRAY_GRAD(_TileArray<Season>, float3(uv, slice), ddx, ddy)` (or `_LOD` where the original used `tex2Dgrad` only for mip control); remove atlas gutter maths; delete the `_CameraDepthTexture` read, the three legacy samplers, `#pragma glsl`. Keep `alpha:fade`, the depth pass, the cutout `discard` and the skirt (upstream behaviour this round).
- [ ] Step 2: pin: add the shader to `EnsureAlwaysIncludedShaders`, add the keyword pair to `RequiredShaderVariants.shadervariants` (read how the BLB skybox entries look), run `ApplyIOSSettings` so `GraphicsSettings.asset` picks it up; self-test check: `MobileShaders.Find("Daggerfall/DistantTerrain/DistantTerrainTilemap") != null && .isSupported` in the Editor, and a source-text check that the cginc contains no `sampler2D _TileAtlas`/`tex2Dgrad(` of the old kind and does contain `UNITY_DECLARE_TEX2DARRAY`.
- [ ] Step 3: Editor compile evidence: 0 `Shader error`; compiled sampler count from the compiled-shader dump < 16.
- [ ] Step 4: commit `Distant Terrain: far-terrain shader samples three texture arrays (was twelve 2048^2 atlases); pinned Always-Included`.

---

### Task 4: Port the C# (inert) with the MOBILE edits

**Files:** `Assets/Scripts/Game/Mobile/Ports/DistantTerrain/{DistantTerrain.cs,_startupMod.cs,CloneCameraRotationFromMainCamera.cs,CloneCameraPositionFromMainCamera.cs,RenderSkyboxWithoutSun.cs}`, `Assets/Editor/MobileSelfTest.cs`

**Interfaces:** Produces `public static void DistantTerrainPort.Init(InitParams)` (rename of `_startupMod.InitStart`, `[Invoke]` removed), `public static bool DistantTerrainPort.Installed`, `public static bool DistantTerrainPort.Running` (set when Init succeeded; read by the sky start poll in Task 5), `public static int DistantTerrain.SliceIndex(int biome, int record)`, `public static bool DistantTerrain.Available(bool shaderOk, bool csvsPresent, bool derivPresent)`, settings-backed `blendEnd` (default 60000) and `mainCameraFarClipPlane` (15000), the timing lines `[DistantTerrain] far terrain built in N ms (heightmap A ms, carve B ms, lifts C ms, tilemap D ms)` and `[DistantTerrain] map-pixel update N ms` (first 10 then every 25th), `[DistantTerrain] arrays N MB` once.

- [ ] Step 1: copy the five files; drop `DistantTerrainFlyMap.cs`, `ThirteenthPassageEffect.cs`, the spell/console registration and the FlyMap creation + `KeyCode` settings in `_startupMod`; `mod.GetAsset<Shader>` -> `MobileShaders.Find(...)`; bind the three arrays (Task 3 contract) where the twelve `GetTerrainTilesetTexture(...).albedoMap` calls were; `TerrainData` minimal alphamap/basemap/detail resolutions (`alphamapResolution = 16`, `baseMapResolution = 16`, `SetDetailResolution(16, 8)` - the far terrain paints nothing through them); `blendEnd`/`mainCameraFarClipPlane` read from mod settings with the iOS defaults; `InitFarTerrain` wrapped in try/catch with teardown (destroy the far terrain object, the two cameras and the RT, restore `Camera.main.farClipPlane`/`clearFlags`, log `[DistantTerrain] far terrain failed: <ex>`); `Installed` true only after `InitFarTerrain` returned normally the first time (`Running` set at the end of `Init` when the gate passed).
- [ ] Step 2: checks: `SliceIndex(3,55) == 223`, `Available` truth table, `Installed/Running` false before Init, no `[Invoke]`, preset defaults (`blendEnd` 60000). RED/GREEN.
- [ ] Step 3: commit `Port sources: Distant Terrain (WoD flavour, inert) on texture arrays; fly-map and spell dropped`.

---

### Task 5: Launcher entry, start order with Dynamic Skies

**Files:** `Assets/Scripts/Game/Mobile/MobilePortedMods.cs` (`DistantTitle`, `Titles`, block BEFORE the sky's deferred start; `StartSkyWhenSceneReady` additionally waits for `GameObject.Find("stackedCamera") != null` while `DistantTerrainPort.Running`), `Assets/Editor/MobileSelfTest.cs`

**Interfaces:** `public const string DistantTitle = "<ModTitle>"`; `public static bool SkySceneReady(bool sunLight, bool mainCamera, bool distantRunning, bool stackedCamera) => sunLight && mainCamera && (!distantRunning || stackedCamera)` (replace the 2-arg version's callers; keep the 2-arg as `=> SkySceneReady(a, b, false, false)`); block uses the 4-arg `StartOne` with `() => DistantTerrainPort.Installed` and hint `[DistantTerrain]`. Note `Installed` becomes true only at `StreamingWorld.OnReady` (later than Init) - so the 4-arg StartOne cannot judge it at Init time: use `() => DistantTerrainPort.Running` (gate passed, hooks subscribed) for the launcher line, and log `[DistantTerrain] far terrain ready` from `InitFarTerrain`. Checks: Titles order (Distant before Sky? Titles order is DefaultOff only - assert membership), `SkySceneReady` truth table incl. the new pair. RED/GREEN. Commit `Distant Terrain: launcher entry off by default; the sky waits for the stacked camera when Distant Terrain runs`.

---

### Task 6: Fix wave placeholder (controller-driven from reviews) - no fixed content.

### Task 7: Build the bundle, verify, upload
Unscoped `ReimportPacks`, full `ApplyAll` -> 51 bundles; `tools/dfmod_inspect.py`: 1 texture RGBA32 5000x2500 (or whatever the source size is) readable, 3 TextAssets + manifest, no shader/script; metas quoted; self-test flips to N/0; backups 51/51; copy to `~/dev/dfu-mods/distant-terrain-wod.dfmod`; upload under that asset name.

### Task 8: Docs
THIRD-PARTY.md (MIT base + unlicensed additions, provenance both commits, the rewrite, the dropped files, the preset, memory, timing lines, not device-verified), UPSTREAM-PATCHES.md (engine touch points: none expected beyond MobilePortedMods/MobileShaders/RequiredShaderVariants/GraphicsSettings pin; rebase risk), README-iOS.md `### Distant Terrain` (one switch, one bundle, what you see, the dials, Dynamic Skies interplay, fog note, timing lines, not device-verified).

### Task 9: Simulator verification
Recipe as Terrain Task 8 (51/51 restore). Launches: default-off; ON at 207,213 -> `[PortedMods] started Distant Terrain`, `[DistantTerrain] far terrain built in ...`, `arrays N MB`, horizon shows distant hills where OFF shows fog/sky (crop), no Metal errors, no `far terrain failed`; + Dynamic Skies + DREAM SKY -> `[DynamicSkies]` takes the stacked-camera branch (grep for `stackedCamera`/its log), sky renders, sun visible (`_SunSize` restored); + WoD Terrain on -> far and near heights consistent at the seam (crop); coast 370,350 with everything on; negative (bundle removed). Timings + memory lines reported.

### Task 10: Device build
`DFU-Test-unity6-distant.ipa` per the Terrain Task 9 brief pattern (51/51); the iOS Metal compile of the rewritten shader is the gate (grep `Shader error`, sampler messages); hand-off: enable, look at the horizon, check the seam, thermals over 10 minutes of travel, send Player.log.

## Self-review
- Spec coverage: 1 (T2 spike), 2 (T3), 3 (T4), 4 (T5), 5 (T4 defaults + T8), 6 (T1, T2 rule, T7), 7 (T4), 8 (T9, T10). Gap: none.
- Placeholders: T1 manifest name and root-folder question are explicit reads of the clone/tooling; T3 leaves the 224-vs-12 array packing decision to the implementer with a recorded reason - acceptable for shader work of this kind.
- Type consistency: `SliceIndex` (T3 contract, T4 impl), `DistantTerrainPort.Running/Installed` (T4) used by T5; `SkySceneReady` 4-arg (T5); `MaxSize`/`NoMips` (T2).
