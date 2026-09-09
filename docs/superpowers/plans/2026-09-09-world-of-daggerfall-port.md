# World of Daggerfall Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run World of Daggerfall's locations on the iPad: Location Loader compiled into the app behind an off-by-default launcher entry, WoD's data as a private downloadable bundle gated on it.

**Architecture:** Location Loader's 11 runtime C# files and WoD's one script are compiled in under `Assets/Scripts/Game/Mobile/Ports/` with the Invoke loaders removed; `MobileMods` registers a built-in `Location Loader` entry; `MobilePortedMods` starts LL at the Start state and WoD's rock-materials script after it, gated LL -> WoD; `tools/bundled-mods` gains an `extra_dirs` flag so WoD's unlisted `Meshes/` folder ships inside its bundle; the bundle is `private_only`.

**Tech Stack:** Unity 6000.3.23f1 (full editor path `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity`; the bare `Unity` on PATH is a different CLI), C# IL2CPP, Python 3 tooling with `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"`, MobileSelfTest (graphics-enabled: `... -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>`, NO -nographics; baseline `=== 612 passed, 0 failed ===`).

**Spec:** `docs/superpowers/specs/2026-09-09-world-of-daggerfall-port-design.md`

## Global Constraints
- Pins: LL `https://github.com/KABoissonneault/DFU-LocationLoader.git` @ `a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0`; WoD `https://github.com/drcarademono/world-of-daggerfall.git` @ `3bf8837402cce41421dcb36c64a3a2c4f27d0448`.
- Titles exactly `Location Loader` (built-in, GUID `fc5c0fa6-e80d-4cb1-89fa-c10be8e35bf3`) and `World of Daggerfall` (bundle). Both default OFF. WoD requires LL; the survival trio's `Gate`/`OrderedPriorities`/`EnsureOrder` stay unchanged.
- Port header on every copied file: `// MOBILE PORT - source: <repo> @ <sha>` / `// File <name>, copied unchanged for iOS except lines marked MOBILE.` / `// Upstream carries no licence header; shipped on the private draft only.`
- Never ship LL's `Scripts/Editor/*.cs`. The WoD bundle is `private_only` (never in `MIT-ModPack-ios.zip`), uploaded only to the `testapp-unity6` draft of `Codex64ai/daggerfall-unity-ios`.
- The app gains only code: `DFU_BUNDLED_MODS=builtin` app builds must not contain the WoD bundle.
- Commits authored by the repo's configured user; each message ends with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01XFLMiLMMZbzbjem8vgUjYE`. Add files by name; revert `Assets/Scenes/DaggerfallUnityGame.unity` / `ProjectSettings/ProjectSettings.asset` reserialization before committing; never commit `Assets/StreamingAssets/Mods` or `Assets/Game/Mods/*` fetched folders (gitignored).
- Unity: one instance at a time; wait 60 s and retry if it refuses to start; never kill. Before any `DFU_BUNDLED_MODS=builtin` ApplyAll, back up the 47 ASTC bundles (`~/dev/dfu-mods/astc-backup`) and restore after; keep > 8 GB free; delete `~/dev/dfu-ios-{sim,build}/DerivedData/Build/Intermediates.noindex` after builds.
- Staging: after committing a file under `Assets/Scripts/Game/Mobile/*.cs` (not Ports), diff `~/daggerfall-mobile/Mobile/<file>` against the pre-commit project version; if identical copy the new file over, if it differs STOP and report.
- Work on `unity6-upgrade` directly (HEAD 6fed9c7be).

---

### Task 1: Pipeline: `extra_dirs` flag and the WoD entry

**Files:** Modify `tools/bundled-mods/fetch.py` (`fetch_one`, after the per-file copy loop ~line 340), `tools/bundled-mods/test_fetch.py`, `tools/bundled-mods/mods.json`.

**Interfaces:** Produces mods.json key `"extra_dirs": ["Meshes"]` = repo folders copied wholesale (files + `.meta`) into the mod folder in addition to the manifest's `Files`; they are NOT added to the manifest. Produces `Assets/Game/Mods/WorldOfDaggerfall/` on disk (gitignored) with `WorldOfDaggerfall.dfmod.json`, `Locations/`, `Prefabs/`, `Materials/`, `Textures/`, `CustomRuntimeMaterials/`, `WorldData/`, `Meshes/`, `LICENSE`, and no `.cs`.

- [ ] Step 1 (RED): in `test_fetch.py` add a test that builds a fake repo dir with `Meshes/a.fbx`, `Meshes/a.fbx.meta`, `Other/x.txt`, calls the new pure helper `fetch.copy_extra_dirs(src_root, dest, ["Meshes"])` and asserts `dest/Meshes/a.fbx` and its `.meta` exist and `dest/Other` does not; and that a missing dir raises `SystemExit` naming it. Run `python3 -m unittest discover -s tools/bundled-mods -p "test_*.py"` -> FAIL (no attribute).
- [ ] Step 2: implement `copy_extra_dirs(src_root, dest, dirs)` (shutil.copytree per dir, `dirs_exist_ok=True`, skipping `.git`), call it from `fetch_one` right after the manifest files are copied: `copy_extra_dirs(src_root, dest, entry.get("extra_dirs") or [])`, printing `  copied extra dir Meshes (N files)`. Do not touch `validate_manifest`.
- [ ] Step 3: mods.json entry (before the builtin survival entries):
```json
{
  "name": "WorldOfDaggerfall",
  "repo": "https://github.com/drcarademono/world-of-daggerfall.git",
  "commit": "3bf8837402cce41421dcb36c64a3a2c4f27d0448",
  "manifest": "WorldOfDaggerfall.dfmod.json",
  "strip_code": true,
  "private_only": true,
  "extra_dirs": ["Meshes"],
  "archives_from": ["DaggerfallExpandedTextures"],
  "drop_dependencies": ["location loader", "wilderness overhaul", "rmb resource pack", "beautiful villages"],
  "licence": "pending:No licence is declared upstream (github.com/drcarademono/world-of-daggerfall, World of Daggerfall Team: KABoissonneault, Cliffworms, Kamer, carademono). Permission is being sought; until then this bundle is shipped only on the private test draft, never in the public mod pack. Its one script (WODRocksMaterials.cs) is compiled into the app under Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfall; Location Loader is compiled in under Ports/LocationLoader (see THIRD-PARTY.md)."
}
```
- [ ] Step 4: `python3 tools/bundled-mods/fetch.py --only WorldOfDaggerfall` (large repo, ~270 MB clone; the fetch does a depth-1 fetch of the commit) then `--check` -> 0 problems. If `validate_manifest`/`worlddata_archive_problems` rejects a WorldData block for a texture archive not covered by `archives_from`, record the exact message and add the missing provider ONLY if that provider is in the pack; otherwise add the offending WorldData file(s) to `exclude_globs` and note it. Verify: `find Assets/Game/Mods/WorldOfDaggerfall -name "*.cs" | wc -l` = 0; `ls Assets/Game/Mods/WorldOfDaggerfall/Meshes | wc -l` ~ 301 (incl. metas); the shipped manifest's `Dependencies` contains only `daggerfall expanded textures`.
- [ ] Step 5: unit tests green; commit `Pack pipeline: extra_dirs; fetch World of Daggerfall data (private, no code)`.

### Task 2: Ported sources compile (inert)

**Files:** Create `Assets/Scripts/Game/Mobile/Ports/LocationLoader/{LocationConsole,LocationData,LocationHelper,LocationLoader,LocationModLoader,LocationNameGenerator,LocationObjectExtraData,LocationRMBVariant,LocationResourceManager,LocationSaveDataInterface,LocationTerrainNature}.cs` (from the LL repo `Scripts/`, NOT `Scripts/Editor/`), `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfall/WODRocksMaterials.cs` (from the WoD repo `Scripts/`).

**Interfaces:** Produces `LocationModLoader.Init(InitParams)` (namespace as upstream: check the file; likely `LocationLoader`) and `WODRocksMaterials.Init(InitParams)` (namespace as upstream), both callable from `DaggerfallWorkshop.Game.Mobile`.

- [ ] Step 1: clone both repos at the pins into the scratch dir (`git clone --depth 1 <repo> <dir> && git -C <dir> fetch --depth 1 origin <sha> && git -C <dir> checkout FETCH_HEAD`), copy the 12 files, add the header, remove the two `[Invoke(StateManager.StateTypes.Start, 0)]` lines (each replaced by `// MOBILE: started by MobilePortedMods when the launcher entry is on`).
- [ ] Step 2: run the self-test (compiles). Expected: 0 `error CS` and `=== 612 passed, 0 failed ===`. Fix compile errors with the smallest `// MOBILE:` change; list each. Known things to check, not change unless they fail: `LocationResourceManager` uses `Application.dataPath` loose-file fallbacks (fine on device: folder absent), `LocationConsole` registers console commands (fine), `LocationHelper` may reference `UnityEditor` under `#if UNITY_EDITOR` only (verify no unguarded editor references).
- [ ] Step 3: `diff -u` each ported file against the clone: only header + MOBILE hunks. Commit `Port sources: Location Loader and WoD rock materials (code only, inert)`.

### Task 3: Launcher entries and start-up

**Files:** Modify `Assets/Scripts/Game/Mobile/MobileMods.cs` (register the built-in `Location Loader` entry), `Assets/Scripts/Game/Mobile/MobilePortedMods.cs` (titles, gate, start), `Assets/Editor/MobileSelfTest.cs` (tests).

**Interfaces:** `MobilePortedMods.LLTitle = "Location Loader"`, `WoDTitle = "World of Daggerfall"`, `Titles` += both (so `DefaultOff` covers them), `public static bool WodRuns(bool locationLoaderOn, bool wodOn) => locationLoaderOn && wodOn;`, `WoDGateNote = " Needs Location Loader switched on; it was switched off because Location Loader is not."`.

- [ ] Step 1 (RED): `TestPortedModTitles` gains: both titles in `Titles`; `WodRuns` truth table (only true/true runs); `Gate(true,true,true).Length == 3` unchanged. Run -> FAIL.
- [ ] Step 2: `MobileMods.Register`: after the bridge, `Mod ll = new Mod(); ll.ModInfo.ModTitle = MobilePortedMods.LLTitle; ModVersion "0.3"; ModAuthor "KABoissonneault (fork of Uncanny_Valley), ported by Codex64ai"; ContactInfo "github.com/KABoissonneault/DFU-LocationLoader"; DFUnity_Version = VersionInfo.DaggerfallUnityVersion; GUID = "fc5c0fa6-e80d-4cb1-89fa-c10be8e35bf3"; ModDescription = "Loads location mods such as World of Daggerfall. Does nothing on its own; switch it on together with a location mod. Compiled into this port."; ll.Enabled = false; manager.RegisterBuiltInMod(ll);`. `MobilePortedMods.StartEnabled`: after the survival Inits, `Mod ll = Entry(LLTitle), wod = Entry(WoDTitle); bool llOn = ll != null && ll.Enabled; if (wod != null && wod.Enabled && !WodRuns(llOn, true)) { wod.Enabled = false; append WoDGateNote to its description if absent; WriteModSettings(); log "[PortedMods] World of Daggerfall switched off: Location Loader must be on"; } if (llOn) { LocationModLoader.Init(new InitParams(ll, index, count)); log started; } if (WodRuns(llOn, wod != null && wod.Enabled)) { WODRocksMaterials.Init(new InitParams(wod, index, count)); log started; }`. Keep the sky's deferred start and the survival code untouched. Update the class summary comment.
- [ ] Step 3: self-test GREEN (612 + new checks, 0 failed, 0 error CS). Commit `Location Loader and World of Daggerfall: launcher entries, off by default, WoD gated on Location Loader`. Staging copy for `MobileMods.cs` and `MobilePortedMods.cs` per the rule.

### Task 4: Build the WoD bundle and ship it to the draft

- [ ] Step 1: back up ASTC bundles; `git checkout -- ProjectSettings/AudioManager.asset`; full `ApplyAll` with `DFU_BUNDLED_MODS` UNSET (log to the workspace). Expected: `Assets/StreamingAssets/Mods/` gains the WoD bundle (name from the manifest: `worldofdaggerfall.dfmod` or `world of daggerfall.dfmod` - report the exact name) alongside the 47 others; no `error`; `fetch.py --check` clean; `pack.stems()` excludes it.
- [ ] Step 2: `python3 ~/daggerfall-mobile/tools/dfmod_inspect.py <bundle>`: code assets 0; textures ~44; report size. If the bundle is missing meshes (materials/prefabs with null meshes), the `extra_dirs` copy did not land in the project before the build - re-run `fetch.py --only WorldOfDaggerfall` and rebuild.
- [ ] Step 3: upload as `worldofdaggerfall.dfmod` (rename if the emitted name has spaces) to the draft; verify size via `gh release view ... --json assets`. Commit nothing.

### Task 5: Simulator verification

- [ ] Step 1: RGBA32 sim copy of the WoD bundle (`DFU_PACK_TEX_FORMAT=RGBA32` + the builder/reimport path used for Dynamic Skies in `~/dev/dfu-mods/dynamic-skies-evidence/task-10a-run3-report.md`); sim test app build (`DFU_IOS_TESTAPP=1 DFU_IOS_SIM=1 DFU_BUNDLED_MODS=builtin`, ApplyAll + BuildIOS + xcodebuild for the simulator, as in that report); restore ASTC bundles.
- [ ] Step 2: container setup as in that report; put `worldofdaggerfall.dfmod` (RGBA32) AND the pack's `daggerfall expanded textures.dfmod` (ASTC is fine for presence) into `Documents/Mods`; first launch writes `Mods/GameData/Mods.json` with `Location Loader`, `World of Daggerfall`, `daggerfall expanded textures` entries - set all three `Enabled: true`; `debug-newchar.txt` = `pixel 204 213` (open country west of Daggerfall city). Relaunch.
- [ ] Step 3: Player.log must show `[PortedMods] started Location Loader`, `[PortedMods] started World of Daggerfall`, no `[Error]` from `LocationLoader`/`[LL]`/`WODRocks`, and evidence of instances: grep `[LL]` lines and any `WOD_` prefab names; after `[DebugStart] render at location` wait 15 s and screenshot `wod-sim.png`; also grep the `[DebugStart] render` line's renderer count vs a run with WoD disabled (negative launch) to show extra objects exist. Report what rendered.
- [ ] Step 4: negative check (both entries off): no LL/WoD lines. Clean up as before.

### Task 6: Docs

- [ ] `THIRD-PARTY.md`: section `## World of Daggerfall (compiled in + private bundle)` with two rows (Location Loader 0.3, WoD 0.4.0; authors; NO LICENCE DECLARED, permission being sought; sources @ pins; what is not shipped: LL's editor scripts, WoD Terrain/Biomes/optional deps). `UPSTREAM-PATCHES.md`: entry for the new `extra_dirs` flag, the built-in LL entry and the MobilePortedMods gate. `~/daggerfall-mobile/README-iOS.md`: `### World of Daggerfall` subsection next to `### Sky` (needs `worldofdaggerfall.dfmod` + DET in Mods, both switches, performance caveat, deletion removes all). Commit.

### Task 7: Device build

- [ ] `DFU_IOS_TESTAPP=1 DFU_BUNDLED_MODS=builtin` ApplyAll + BuildIOS + xcodebuild (unsigned ipa `DFU-Test-unity6-wod.ipa` + signed app) per `~/dev/dfu-mods/dynamic-skies-evidence/task-10b-report.md`; sanity: no WoD bundle inside the app; upload the ipa; if the iPad is available, install and push `worldofdaggerfall.dfmod` into `Documents/Mods`; restore ASTC bundles; clean Intermediates; commit nothing.
- [ ] Hand-off checklist for Ikram: enable Location Loader + World of Daggerfall (DET already on), travel a road out of Daggerfall, look for rocks/camps/towers, read `TUNE > Advanced > Show diagnostics` frame time while walking and during Real travel, report hitches; Player.log `[LL]` lines on any problem.
