# Survival Mods Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Roleplay & Realism, Roleplay & Realism Items and Climates & Calories inside the iOS app as three launcher-switchable mods, with their data as in-app bundles and their C# compiled in.

**Architecture:** The mods' C# is copied under `Assets/Scripts/Game/Mobile/Ports/` with minimal edits; each mod's data is fetched by `tools/bundled-mods/fetch.py` (new `strip_code`/`builtin` flags) and built into `StreamingAssets/Mods` by `MobileBuildSetup.BuildBundledMods`; `MobilePortedMods` calls each mod's `Init` when its bundle entry is enabled; `MobileTravelOptionsBridge` answers the `TravelOptions` mod messages from the journey controller; `MobileTavernWindow` replaces the unlicensed tavern file.

**Tech Stack:** Unity 6000.3.23f1, C# (Assembly-CSharp, IL2CPP on iOS), Python 3 tooling, MobileSelfTest (`Unity -batchmode -quit -projectPath . -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll`).

**Spec:** `docs/superpowers/specs/2026-09-07-survival-mods-port-design.md`

## Global Constraints
- Pinned sources: `github.com/ajrb/dfunity-mods` @ `0af2ec99b5ad7e6722ddc9ce284987d4d6fa262d`; `github.com/Ralzar81/Climates-Calories` @ `c33a04f8d200615b01d09987262b1ed318e7615a`.
- Never ship `TavernWindow.cs`, `blb_tent.fbx`, `41606.prefab`, `RALZARTAVERN.PNG`, `BLANKMENU_TAVERN.PNG` (unlicensed).
- Every mod switch lives only in the launcher's MODS window; the in-game TUNE page has no Mods tab.
- All three switches default off. Titles stay exactly `Roleplay and Realism`, `RoleplayRealism-Items` (ModTitle from manifest), `Climates & Calories`.
- Commits authored as the repo's configured user with the Co-Authored-By and Claude-Session trailers.

---

### Task 1: Pipeline flags for built-in data bundles
**Files:** Modify `tools/bundled-mods/fetch.py:65-95, 300-354, 390-400`, `tools/bundled-mods/pack.py:24-64`, `tools/bundled-mods/mods.json`, `tools/bundled-mods/test_pack.py`.
**Produces:** mods.json entry keys `"builtin": true` (excluded from the pack zip) and `"strip_code": true` (drop `.cs` from `Files`, and `exclude_globs` still applies); `licence: "text:<MIT header>"` writes LICENSE.

- [ ] Step 1: test in `test_pack.py`: `check_bundles` ignores entries with `builtin: true` (a built bundle for them is not "unpinned", a missing one is not a problem) and `stems()` excludes them. Run `python3 -m pytest tools/bundled-mods/test_pack.py -q` → FAIL.
- [ ] Step 2: pack.py: `stems(cfg)` skips `m.get("builtin")`; `check_bundles` ignores built-in stems on both sides. fetch.py `validate_manifest(manifest, mod_dir, allow_code=False)`: when `allow_code` is false keep the refusal; `fetch_one`: after `exclude_globs`, `if entry.get("strip_code"): raw_files = [f for f in raw_files if not f.endswith(SCRIPT_EXTS)]`; `check_all` passes `allow_code=False` always (code is stripped before validation, so the check still holds).
- [ ] Step 3: mods.json entries:
```json
{"name": "RoleplayRealism", "repo": "https://github.com/ajrb/dfunity-mods.git", "commit": "0af2ec99b5ad7e6722ddc9ce284987d4d6fa262d", "subdir": "RoleplayRealism", "manifest": "RoleplayRealism.dfmod.json", "strip_code": true, "builtin": true, "licence": "text:MIT License. Copyright (C) 2020 Hazelnut. Source file headers: License: MIT License (http://www.opensource.org/licenses/mit-license.php). Data half shipped inside the iOS app; code compiled in, see THIRD-PARTY.md."},
{"name": "RoleplayRealismItems", "repo": "https://github.com/ajrb/dfunity-mods.git", "commit": "0af2ec99b5ad7e6722ddc9ce284987d4d6fa262d", "subdir": "RoleplayRealismItems", "manifest": "RoleplayRealism-Items.dfmod.json", "strip_code": true, "builtin": true, "licence": "text:MIT License. Copyright (C) 2020 Hazelnut and Ralzar. ..."},
{"name": "ClimatesCalories", "repo": "https://github.com/Ralzar81/Climates-Calories.git", "commit": "c33a04f8d200615b01d09987262b1ed318e7615a", "subdir": "Climates & Calories", "manifest": "Climates & Calories.dfmod.json", "strip_code": true, "builtin": true, "exclude_globs": ["blb_tent.fbx", "41606.prefab", "RALZARTAVERN.PNG", "BLANKMENU_TAVERN.PNG", "*.mat"], "licence": "text:MIT License. Copyright (C) 2020 Ralzar. ... TavernWindow.cs and the tent model are not shipped."}
```
  Run `python3 tools/bundled-mods/fetch.py --only RoleplayRealism` (and the other two), then `--check` → no problems. Verify the C&C manifest's dependency names (`roleplayrealism`, `roleplayrealism-items`, `traveloptions`) still validate: add `traveloptions` to the allowed-external list in `validate_set` (the bridge provides it) or drop the `traveloptions` dependency via `manifest_override` — choose manifest_override so the shipped manifest is honest about what exists (RR + RR-Items only).
- [ ] Step 4: run tests, commit "Pack pipeline: built-in data bundles (strip_code, builtin)".

### Task 2: Ported sources compile
**Files:** Create `Assets/Scripts/Game/Mobile/Ports/RoleplayRealism/*.cs` (7 from `dfunity-mods/RoleplayRealism/Scripts`), `Ports/RoleplayRealismItems/*.cs` (15 from `dfunity-mods/RoleplayRealismItems/Scripts`), `Ports/ClimatesCalories/*.cs` (8 from Climates-Calories minus TavernWindow.cs).
**Edits allowed:** remove `[Invoke(...)]` lines; in `ClimateCalories.cs` replace `typeof(TavernWindow)` with `typeof(Mobile.MobileTavernWindow)` (Task 5 provides it; add a stub first); fix compile errors against DFU 1.1.1 APIs only by the smallest change, each marked `// MOBILE:`.
- [ ] Step 1: copy files, strip `[Invoke`, add `// MOBILE PORT of <repo>@<commit>, file unchanged except lines marked MOBILE` header to each.
- [ ] Step 2: run the self-test (compiles the project) → fix errors until 0 `error CS`.
- [ ] Step 3: commit "Port sources: Roleplay & Realism, RR Items, Climates & Calories (code only, inert)".

### Task 3: Bootstrap and dependency gate
**Files:** Create `Assets/Scripts/Game/Mobile/MobilePortedMods.cs`; Test `Assets/Editor/MobileSelfTest.cs`.
**Produces:** `public static class MobilePortedMods { public const string RRTitle="Roleplay and Realism", RRItemsTitle="RoleplayRealism-Items", CCTitle="Climates & Calories"; public static bool[] Gate(bool rr, bool rrItems, bool cc) /* returns which run: cc requires rr && rrItems; rrItems requires rr */ ; static void Start() }`.
- [ ] Step 1: self-test `TestPortedModGate`: Gate(true,true,true)→{t,t,t}; Gate(false,true,true)→{f,f,f}; Gate(true,false,true)→{t,f,f}; Gate(true,true,false)→{t,t,f}. FAIL (no class).
- [ ] Step 2: implement. Runtime: subscribe `StateManager.OnStateChange` in a `[RuntimeInitializeOnLoadMethod]`; on `StateTypes.Start` (after ModManager's own handler: defer one frame with a driver MonoBehaviour), read `ModManager.Instance.GetMod(title)?.Enabled`, apply Gate, for a disabled-by-gate C&C set `Enabled=false`, append the gate note to `ModInfo.ModDescription`, `WriteModSettings()`; then in order call `RoleplayRealism.RoleplayRealism.Init(new InitParams(mod, idx, count))`, `RoleplayRealism.RoleplayRealismItemsMod.Init(...)`, `ClimatesCalories.ClimateCalories.Init(...)`. Log `[PortedMods] started: ...`.
- [ ] Step 3: self-test passes; commit.

### Task 4: Travel Options bridge
**Files:** Create `Assets/Scripts/Game/Mobile/MobileTravelOptionsBridge.cs`; Modify `MobileMods.cs` (register hidden built-in entry `TravelOptions`, description "Bridge for mods that talk to Travel Options; answered by Real travel. Always on."); Test in MobileSelfTest.
**Produces:** `public static class MobileTravelOptionsBridge { public static object Answer(string message, object data, bool pilotActive, bool followingRoad, Action stop, Action<string> hud) }` pure-ish; runtime `Receive(string, object, DFModMessageCallback)` wired to `mod.MessageReceiver`.
- [ ] Step 1: self-test `TestTravelOptionsBridge`: `isTravelActive` → callback gets pilotActive; `pauseTravel` with pilotActive calls stop once, without does not; `showMessage` forwards text; `isFollowingRoad`/`isPathFollowing` → followingRoad; unknown message → no callback, no throw.
- [ ] Step 2: implement; the entry's `Enabled` is forced true on register.
- [ ] Step 3: pass; commit.

### Task 5: Tavern window
**Files:** Create `Assets/Scripts/Game/Mobile/MobileTavernWindow.cs` (`: DaggerfallTavernWindow`): override `FoodAndDrink_OnItemPicked(int index, string name)`: call base; then `if (ClimatesCalories.ClimateCalories` is active) → drinks (`name` in the engine's drink set: contains "Ale", "Beer", "Mead", "Wine", "Water") call `ClimatesCalories.Hunger.RefillWater(50f, true)`; food leaves hunger to the engine's `LastTimePlayerAteOrDrankAtTavern` which C&C's Hunger already reads. Drunkenness is not ported (documented).
- [ ] Step 1: replace the Task-2 stub; compile via self-test; commit.

### Task 6: Launcher-only switches
**Files:** Modify `Assets/Scripts/Game/Mobile/MobileSettingsPanel.cs` (remove `Mods` from `enum Section`, the tab name, `BuildModsSection` and its call; arrays sized 3), `MobileMods.cs:62,77-78` (drop "Also switchable in play ..." sentences), and the three new entries get descriptions via their manifests.
- [ ] Step 1: edit; self-test compiles; commit "Mod switches live only in the launcher".

### Task 7: Docs
- [ ] `THIRD-PARTY.md`: new section "Survival mods (compiled in)" with the three rows (author, licence, commit, what is excluded and why). `README-iOS.md`: "Survival" section (what each switch does, launcher-only rule, drunkenness/tent notes). `UPSTREAM-PATCHES.md`: entry for the settings panel change and the bridge. Commit.

### Task 8: Build and verify
- [ ] `DFU_IOS_TESTAPP=1` ApplyAll (with bundles: do NOT set DFU_BUNDLED_MODS=0; the three built-in bundles must ship) → BuildIOS sim → run with `debug-newchar.txt` and the three entries enabled in the sim's `Mods.json` → Player.log shows `[PortedMods] started`, no exceptions; hunger message appears after game hours pass. Device ipa (unsigned) → draft. Note: the 44-pack bundles must NOT be in StreamingAssets for the ipa - ApplyAll builds only manifests under Assets/Game/Mods; move the pack sources aside or filter `BundledManifests()` to `builtin` entries (add that filter: `BundledManifests` reads mods.json and includes only entries with `builtin: true`).
