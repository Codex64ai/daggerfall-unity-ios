# Dynamic Skies Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the Dynamic Skies procedural sky on iOS as an off-by-default launcher switch, with the converted DREAM SKY bundle as its preset, without growing the app beyond code plus one shader.

**Architecture:** The mod's C# is compiled in under `Assets/Scripts/Game/Mobile/Ports/DynamicSkies/` (four marked edits); its skybox shader is our copy under `Assets/Shaders/BLB/` with the keyword pragmas pinned to the one variant set the material uses, force-included in the build; its DATA (material, JSON, textures) is fetched by `tools/bundled-mods/fetch.py` as a NON-builtin, private-only bundle that ships as a downloadable `dynamic-skies.dfmod`; `MobilePortedMods` starts it when its launcher entry is on; DREAM SKY converts with the existing extractor into `dream-sky.dfmod`, which the mod discovers as its preset by itself.

**Tech Stack:** Unity 6000.3.23f1, C# (Assembly-CSharp, IL2CPP on iOS), ShaderLab/CG for Metal, Python 3 tooling (`fetch.py`, `pack.py`, pytest), MobileSelfTest (graphics-enabled run: `Unity -batchmode -quit -projectPath ~/dev/daggerfall-unity -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll -logFile <log>` - NO `-nographics`).

**Spec:** `docs/superpowers/specs/2026-09-08-dynamic-skies-port-design.md`

## Global Constraints
- Pinned source: `https://github.com/drcarademono/dynamic-skies.git` @ `04506e2ef65aff27e4882ec07ed29a6fc7a4be6b` (repo root is the mod; manifest `dynamic-skies.dfmod.json`, `ModTitle` = `Dynamic Skies`, GUID `53a9b8f5-6271-4f74-9b8b-9220dd105a04`). A local clone is at `/private/tmp/claude-501/-Users-ikrammassabini/3d73a6ab-7f57-4287-bf2c-0ab417b7493b/scratchpad/dynamic-skies` this session; re-clone with `git clone --depth 1` + `git fetch origin 04506e2...` if gone.
- Never ship: `Scripts/SunShafts.cs`, `Scripts/PostEffectsBase.cs`, `Shaders/SunShaftsComposite.shader`, `Shaders/SimpleClear.shader`, `Shaders/BLBProceduralSkyboxOriginal.shader` (dead code upstream, Standard Assets terms).
- The switch defaults OFF. Title exactly `Dynamic Skies`. Launcher MODS window only (TUNE has no Mods tab).
- The app gains ONLY code and the shader: the Dynamic Skies data bundle is NOT `builtin` and must not land in an app build (`DFU_BUNDLED_MODS=builtin`).
- The Dynamic Skies bundle and `dream-sky.dfmod` go ONLY to the private `testapp-unity6` draft (gh: `-R Codex64ai/daggerfall-unity-ios`), never into `MIT-ModPack-ios.zip`, never published. Licence: none declared upstream; Ikram is handling permission.
- Every ported file carries the header: `// MOBILE PORT - source: github.com/drcarademono/dynamic-skies @ 04506e2ef65aff27e4882ec07ed29a6fc7a4be6b` / `// File <name>, copied unchanged for iOS except lines marked MOBILE.` (upstream files have no licence header; say so on line 3: `// Upstream carries no licence header; shipped on the private draft only.`).
- Commits authored as the repo's configured user (`Codex64ai <ikrammassabini@gmail.com>`), each ending with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Keep >8 GB free before any Unity build; delete `~/dev/dfu-ios-{sim,build}/DerivedData/Build/Intermediates.noindex` after each build.
- Staging rule: `~/daggerfall-mobile` mirrors `Mobile/*.cs`; before copying staging -> project diff against `git show HEAD:<path>` (a blind copy once deleted 261 lines). This plan edits the PROJECT and copies to staging at the end of each code task.

---

### Task 1: Pipeline flag for private-only bundles and the Dynamic Skies data entry

**Files:**
- Modify: `tools/bundled-mods/fetch.py:143-152` (`licence_problems`), `:223-233` (`licence_text_for`), `:369-404` (`check_all`)
- Modify: `tools/bundled-mods/pack.py` (wherever `stems()`/`check_bundles` skip `builtin` - grep `builtin`)
- Modify: `tools/bundled-mods/mods.json` (new entry)
- Test: `tools/bundled-mods/test_fetch.py`, `tools/bundled-mods/test_pack.py`

**Interfaces:**
- Produces: mods.json keys `"private_only": true` (bundle is built by `BuildBundledMods` but skipped by `pack.py`, exactly as `builtin` is) and `"licence": "pending:<text>"` (LICENSE written as `Pending\n\n<text>`; accepted by `licence_problems` only when the entry is `private_only`).
- Later tasks rely on the fetched folder `Assets/Game/Mods/DynamicSkies/` containing `dynamic-skies.dfmod.json`, `Materials/BLBSkyboxMaterial.mat`, `Materials/BLBSnowMaterial.mat`, `SkyboxSettings/*.json`, `Resources/*.json`, `FogSettings/*.json`, `LightCurveSettings/LightCurve.json`, `Textures/*`, `modsettings.json`, `LICENSE` - and NO `.cs`, `.shader`, `.cginc`.

- [ ] **Step 1: Failing tests.** In `test_fetch.py` add:
```python
def test_pending_licence_only_for_private_only():
    assert fetch.licence_problems("Pending\n\nno licence upstream", allow_pending=True) == []
    assert fetch.licence_problems("Pending\n\nno licence upstream") != []
    assert fetch.licence_text_for({"licence": "pending:no licence upstream; draft only"}) == "Pending\n\nno licence upstream; draft only\n"
```
In `test_pack.py` add a test mirroring the existing `builtin` one: an entry with `"private_only": True` is absent from `stems(cfg)` and ignored by `check_bundles` on both sides (copy the existing builtin test body, swap the key). Run `python3 -m pytest tools/bundled-mods -q` -> the new tests FAIL (`unexpected keyword allow_pending`, stems contains the entry).

- [ ] **Step 2: Implement.** fetch.py:
```python
def licence_problems(text, allow_permission=False, accept_substring=None, allow_pending=False):
    stripped = (text or "").strip()
    first = stripped.splitlines()[0].strip() if stripped else ""
    if first == "MIT License":
        return []
    if accept_substring and accept_substring in stripped:
        return []
    if allow_permission and first == "Permission" and len(stripped.splitlines()) > 1:
        return []
    if allow_pending and first == "Pending" and len(stripped.splitlines()) > 1:
        return []          # private_only entry: no licence upstream, permission being sought
    return ["LICENSE first line is %r, expected 'MIT License'%s" % (first, " or 'Permission'" if allow_permission else "")]
```
`licence_text_for`: add `if lic.startswith("pending:"): return "Pending\n\n" + lic[len("pending:"):].strip() + "\n"`. In `check_all`, pass `allow_pending=bool(entry.get("private_only")) and str(entry.get("licence", "")).startswith("pending:")`. Also in `check_all`, append a problem `"<name>: pending licence requires private_only"` when licence starts with `pending:` but `private_only` is not true. pack.py: every place that tests `m.get("builtin")` becomes `m.get("builtin") or m.get("private_only")` (write a helper `def excluded_from_pack(m): return bool(m.get("builtin") or m.get("private_only"))`).

- [ ] **Step 3: mods.json entry** (append to the mods list, before the three builtin survival entries):
```json
{
  "name": "DynamicSkies",
  "repo": "https://github.com/drcarademono/dynamic-skies.git",
  "commit": "04506e2ef65aff27e4882ec07ed29a6fc7a4be6b",
  "manifest": "dynamic-skies.dfmod.json",
  "strip_code": true,
  "private_only": true,
  "exclude_globs": ["*.shader", "*.cginc"],
  "licence": "pending:No licence is declared upstream (github.com/drcarademono/dynamic-skies, authors BadLuckBurt and carademono). Permission is being sought; until then this bundle is shipped only on the private test draft, never in the public mod pack. Code compiled in under Assets/Scripts/Game/Mobile/Ports/DynamicSkies with the shader under Assets/Shaders/BLB (see THIRD-PARTY.md)."
}
```
No `subdir` key (the repo root is the mod). `strip_code` already drops the `.cs` files (including SunShafts/PostEffectsBase, which the manifest does not list anyway).

- [ ] **Step 4: Fetch and check.** `python3 tools/bundled-mods/fetch.py --only DynamicSkies && python3 tools/bundled-mods/fetch.py --check` -> no problems. `ls Assets/Game/Mods/DynamicSkies` shows the folders listed under Interfaces; `find Assets/Game/Mods/DynamicSkies -name "*.cs" -o -name "*.shader" -o -name "*.cginc"` prints nothing. `python3 -m pytest tools/bundled-mods -q` -> all pass.

- [ ] **Step 5: Commit** `git add tools/bundled-mods Assets/Game/Mods/DynamicSkies` (metas included) with message `Pack pipeline: private-only bundles; fetch Dynamic Skies data (no code, no shader)`.

---

### Task 2: The skybox shader in the app

**Files:**
- Create: `Assets/Shaders/BLB/BLBProceduralSkybox.shader`, `Assets/Shaders/BLB/Includes/Scattering.cginc`, `Assets/Shaders/BLB/Includes/MoonFunctions.cginc` (copied from the clone's `Shaders/`)
- Modify: `Assets/Editor/MobileBuildSetup.cs:237-247` (`EnsureAlwaysIncludedShaders` names array)
- Test: `Assets/Editor/MobileSelfTest.cs` (new `TestDynamicSkiesShader`, registered in `RunAll` after `TestMobileShadersFind`)

**Interfaces:**
- Produces: shader named `BLB/SkyBox/BLBProceduralSkybox` (unchanged name), findable via `MobileShaders.Find("BLB/SkyBox/BLBProceduralSkybox")` at runtime; `public const string DynamicSkiesShaderName = "BLB/SkyBox/BLBProceduralSkybox"` on `MobilePortedMods` (added in Task 4; Task 2's test uses the literal).

- [ ] **Step 1: Failing test.** Add to MobileSelfTest:
```csharp
static void TestDynamicSkiesShader()
{
    Shader sky = Shader.Find("BLB/SkyBox/BLBProceduralSkybox");
    Check(sky != null, "DynamicSkies: skybox shader is in the project");
    if (sky == null) return;
    Check(sky.isSupported, "DynamicSkies: skybox shader compiles for this editor's graphics API");
    // The material arrives from a bundle with its keywords but no shader; swapping the shader in must keep them.
    var mat = new Material(Shader.Find("Daggerfall/Default"));
    mat.EnableKeyword("REDUCE_COLOR"); mat.EnableKeyword("_SUNDISK_HIGH_QUALITY");
    mat.shader = sky;
    Check(mat.IsKeywordEnabled("REDUCE_COLOR") && mat.IsKeywordEnabled("_SUNDISK_HIGH_QUALITY"), "DynamicSkies: material keywords survive the shader swap");
    string src = System.IO.File.ReadAllText("Assets/Shaders/BLB/BLBProceduralSkybox.shader");
    Check(!src.Contains("#pragma multi_compile _") && !src.Contains("multi_compile_local"), "DynamicSkies: keyword pragmas are pinned (fog variants only)");
    Check(src.Contains("#pragma target 3.5"), "DynamicSkies: shader target pinned");
    Check(!src.Contains("sampler3D"), "DynamicSkies: unused 3D LUT sampler removed");
    UnityEngine.Object.DestroyImmediate(mat);
}
```
Register `TestDynamicSkiesShader();` in `RunAll` next to `TestMobileShadersFind();`. Run the self-test -> FAIL (`shader is in the project`).

- [ ] **Step 2: Copy and edit the shader.** `mkdir -p Assets/Shaders/BLB/Includes`; copy the three files. In `BLBProceduralSkybox.shader`: (a) add the three-line MOBILE PORT header as `//` comments above `Shader "BLB/SkyBox/BLBProceduralSkybox"`; (b) delete property line 6 (`[NoScaleOffset]_Lut ("LUT Texture", 3D) = "white" {}`) and line 426 (`sampler3D _Lut;`); (c) replace lines 134-138 with:
```hlsl
            // MOBILE: the material pins one keyword set and the C# never toggles keywords, so the
            // 108-combination multi_compile set is fixed here. Only the fog variants remain.
            #pragma target 3.5
            #define _MOONSPINOPTION_TIDAL_LOCK 1
            #define _SECUNDASPINOPTION_TIDAL_LOCK 1
            #define REDUCE_COLOR 1
            #define _SUNDISK_HIGH_QUALITY 1
```
Check the SUNDISK mapping block just below (grep `SKYBOX_SUNDISK_HQ` near line 140-160) still resolves: it is `#if defined(_SUNDISK_HIGH_QUALITY) ... #define SKYBOX_SUNDISK SKYBOX_SUNDISK_HQ`. `PHASE_LIGHT` and `SECUNDA_PHASE_LIGHT` stay undefined (the material does not enable them). Leave the `[Toggle]`/`[KeywordEnum]` property lines: they are harmless material properties.

- [ ] **Step 3: Pin in the build.** In `EnsureAlwaysIncludedShaders` add `"BLB/SkyBox/BLBProceduralSkybox",` to `names` and change the log line to `"... (UIBlit/UIBlend/fonts/BLB skybox, anti-stripping)"`.

- [ ] **Step 4: Run the self-test** -> the five DynamicSkies checks pass, total green. Also `grep -c "Shader error\|error CS" <log>` = 0.

- [ ] **Step 5: Commit** `Dynamic Skies shader compiled into the app, variants pinned, force-included`.

---

### Task 3: Ported sources compile (inert)

**Files:**
- Create: `Assets/Scripts/Game/Mobile/Ports/DynamicSkies/BLBSkybox.cs`, `BLBCloudsSetting.cs`, `BLBFastPalette.cs`, `BLBFogSetting.cs`, `BLBLightCurve.cs`, `BLBMoonSetting.cs`, `BLBSkyboxSetting.cs`, `BLBStarsSetting.cs`, `LightningFlash.cs`, `LightingFlashListener.cs` (from the clone: root `BLBSkybox.cs`, the rest from `Scripts/`; NOT `SunShafts.cs`, NOT `PostEffectsBase.cs`)

**Interfaces:**
- Produces: `public static void BLBSkybox.Init(InitParams initParams)` (unchanged signature, class `BLBSkybox` in the global namespace as upstream); `public static Texture2D BLBSkybox.LoadPresetTexture(Mod preset, Mod fallback, string name)` (pure-ish helper, Task 5 tests it).

- [ ] **Step 1: Copy** the ten files, add the three-line header to each. Remove the `[Invoke(StateManager.StateTypes.Start, 0)]` line above `Init` (mark with `// MOBILE: started by MobilePortedMods when the launcher entry is on`).

- [ ] **Step 2: Shader swap and bail-out.** In `Init`, right after line `Instance.skyboxMat = Mod.GetAsset<Material>(...)` insert:
```csharp
        // MOBILE: the bundle carries the material without its shader (excluded on purpose); the app
        // compiles the shader itself. No shader or no material -> leave the vanilla sky alone.
        Shader appShader = DaggerfallWorkshop.Game.Mobile.MobileShaders.Find("BLB/SkyBox/BLBProceduralSkybox");
        if (Instance.skyboxMat == null || appShader == null)
        {
            Debug.LogError("[DynamicSkies] " + (Instance.skyboxMat == null ? "material BLBSkyboxMaterial missing from the bundle" : "shader BLB/SkyBox/BLBProceduralSkybox missing from the app") + " - keeping the vanilla sky");
            return;
        }
        Instance.skyboxMat.shader = appShader;
```
This runs BEFORE `Instance.dfSky.SetActive(false)` and `ToggleSkybox(true)`.

- [ ] **Step 3: Texture fallback.** Add to `BLBSkybox`:
```csharp
    // MOBILE: a converted preset bundle may lack a texture its JSON names; fall back to this mod's own
    // copy and say so, instead of binding null and drawing a black sky.
    public static Texture2D LoadPresetTexture(Mod preset, Mod fallback, string name)   // public: the editor self-test assembly calls it
    {
        Texture2D tex = preset != null ? preset.GetAsset<Texture2D>(name) : null;
        if (tex == null && fallback != null && fallback != preset)
        {
            tex = fallback.GetAsset<Texture2D>(name);
            Debug.LogWarning("[DynamicSkies] preset texture missing: " + name + (tex != null ? " (using the default)" : " (no default either)"));
        }
        return tex;
    }
```
In `ProcessSkyboxSetting`'s `#else` branch replace each `Instance.presetMod.GetAsset<Texture2D>(X)` with `LoadPresetTexture(Instance.presetMod, Mod, X)` (nine lines, each ending `// MOBILE`).

- [ ] **Step 4: Compile.** Run the self-test (it compiles the project). Fix `error CS` lines with the smallest change, each marked `// MOBILE:`; expected: none (the code targets DFU 1.0.0 APIs that exist in 1.1.1). Confirm nothing outside `#if UNITY_EDITOR` references `UnityEditor` (`grep -n "AssetDatabase\|MenuItem\|EditorUtility" BLBSkybox.cs` all inside guarded regions - lines 1494-1550 and 1567-1790 upstream).

- [ ] **Step 5: Commit** `Port sources: Dynamic Skies (code only, inert until switched on)`.

---

### Task 4: Fourth launcher switch in MobilePortedMods

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/MobilePortedMods.cs` (Titles, DefaultOff coverage, StartEnabled)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestPortedModGate` extended; new `TestPortedModTitles`)

**Interfaces:**
- Produces: `public const string SkyTitle = "Dynamic Skies";` `public const string DynamicSkiesShaderName = "BLB/SkyBox/BLBProceduralSkybox";` `Titles` now `{ RRTitle, RRItemsTitle, CCTitle, SkyTitle }`; `public static bool SkyRuns(bool entryPresent, bool enabled) => entryPresent && enabled;` (pure).

- [ ] **Step 1: Failing tests.**
```csharp
static void TestPortedModTitles()
{
    Check(System.Array.IndexOf(MobilePortedMods.Titles, "Dynamic Skies") >= 0, "PortedMods: Dynamic Skies is a default-off title");
    Check(MobilePortedMods.SkyRuns(true, true) && !MobilePortedMods.SkyRuns(true, false) && !MobilePortedMods.SkyRuns(false, true), "PortedMods: sky runs only when its entry exists and is on");
    Check(MobilePortedMods.Gate(true, true, true).Length == 3, "PortedMods: survival gate unchanged by the sky entry");
}
```
Register in `RunAll` after `TestPortedModOrder();`. Run -> FAIL (`SkyRuns` undefined).

- [ ] **Step 2: Implement.** Add the constants and `SkyRuns`; extend `Titles`. In `StartEnabled`, after the C&C line:
```csharp
            Mod sky = Entry(SkyTitle);
            if (SkyRuns(sky != null, sky != null && sky.Enabled))
            {
                BLBSkybox.Init(new InitParams(sky, ModManager.Instance.GetModIndex(SkyTitle), count));
                Debug.Log("[PortedMods] started " + SkyTitle);
            }
```
`DefaultOff` iterates `Titles`, so it covers the new entry without change. Update the class summary comment to name the fourth mod and note its data is NOT built in. Keep `EnsureOrder(rr, items, cc)` untouched (`Entry(SkyTitle)` may be null when the bundle is not installed - that is the dormant case).

- [ ] **Step 3: Self-test green; commit** `Dynamic Skies: launcher switch, off by default, starts only with its bundle installed`. Copy `MobilePortedMods.cs` to `~/daggerfall-mobile/Mobile/` after diffing against HEAD.

---

### Task 5: Texture fallback and preset-name checks are tested

**Files:**
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestDynamicSkiesPresetTextures`)
- Modify (only if the rule misses): `Assets/Editor/MobileModExtractor.cs:1157-1160` (`IsNormalMapName` / `HasMapSuffix`)

- [ ] **Step 1: Failing test.**
```csharp
static void TestDynamicSkiesPresetTextures()
{
    // Names DREAM SKY and the default presets use for normal maps must be recognised as normals by the converter.
    Check(MobileModExtractor.IsNormalMapName("CdMCloudsNormal"), "DynamicSkies: CdMCloudsNormal is a normal map by name");
    Check(MobileModExtractor.IsNormalMapName("2k_sky_3_Normal"), "DynamicSkies: 2k_sky_3_Normal is a normal map by name");
    Check(!MobileModExtractor.IsNormalMapName("DefaultStars"), "DynamicSkies: star map is not a normal map");
    // The default preset JSON names only textures the fetched bundle actually has.
    string root = "Assets/Game/Mods/DynamicSkies";
    var have = new HashSet<string>(System.IO.Directory.GetFiles(root + "/Textures").Where(f => !f.EndsWith(".meta")).Select(f => System.IO.Path.GetFileNameWithoutExtension(f)));
    var wanted = new HashSet<string>();
    foreach (string json in System.IO.Directory.GetFiles(root + "/SkyboxSettings", "*.json").Concat(System.IO.Directory.GetFiles(root + "/Resources", "*.json")))
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(json), "TextureFile\\\\?\":\\s*\\\\?\"([^\"\\\\]+)"))
            wanted.Add(m.Groups[1].Value);
    Check(wanted.Count >= 9 && wanted.All(have.Contains), "DynamicSkies: every texture named by the default presets is in the bundle (" + string.Join(",", wanted.Where(w => !have.Contains(w)).ToArray()) + ")");
    Check(BLBSkybox.LoadPresetTexture(null, null, "nothing") == null, "DynamicSkies: fallback with no mods returns null without throwing");
}
```
Register in `RunAll`. Run -> observe which checks fail. `HasMapSuffix(name, "Normal")` upstream matches `_Normal`; `CdMCloudsNormal` has no underscore.

- [ ] **Step 2: Extend the rule if needed.** If `CdMCloudsNormal` fails: in `MobileModExtractor.IsNormalMapName` return `HasMapSuffix(assetName, "Normal") || assetName.EndsWith("Normal", System.StringComparison.OrdinalIgnoreCase);` with a comment naming Dynamic Skies' cloud normals as the reason. Do NOT loosen the other map rules.

- [ ] **Step 3: Self-test green; commit** `Dynamic Skies: converter recognises cloud normal maps; preset texture names verified`.

---

### Task 6: Convert DREAM SKY

**Files:**
- Input: `~/Downloads/DREAM - SKY-664-1-2-1748213265.rar` (extract with `bsdtar -xf` into the scratchpad -> `dream - sky.dfmod`, 3.4 MB, macOS bundle)
- Output: `~/dev/dfu-mods/dream-sky.dfmod` (iOS)
- Create: `tools/bundled-mods/check_sky_preset.py` (small verifier)

- [ ] **Step 1: Convert.**
```bash
mkdir -p ~/dev/dfu-mods && cd ~/dev/daggerfall-unity && \
env DFU_MOD_IN="/private/tmp/claude-501/-Users-ikrammassabini/3d73a6ab-7f57-4287-bf2c-0ab417b7493b/scratchpad/sky/dream - sky.dfmod" \
    DFU_MOD_OUT=$HOME/dev/dfu-mods DFU_MOD_KEEP_EXTRACTION=1 \
/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath ~/dev/daggerfall-unity \
  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModExtractor.ConvertFromEnv -logFile ~/dev/dfu-mods/dream-sky-convert.log
```
(no `-nographics`: texture decode needs a GPU; no `-quit`: ConvertFromEnv refuses it and exits itself). Expected in the log: 13 textures, 16 TextAssets, an iOS bundle written as `~/dev/dfu-mods/iOS/dream - sky.dfmod` (source basename); copy it to `~/dev/dfu-mods/dream-sky.dfmod` for upload. Note the kept extraction folder path from the log.

- [ ] **Step 2: Verify names.** Write `tools/bundled-mods/check_sky_preset.py`:
```python
#!/usr/bin/env python3
"""Check a Dynamic Skies preset extraction: every *TextureFile name in its JSON has a texture, and
there are the seven SkyboxSettings files the mod's FindPresetMod requires."""
import json, os, re, sys
root = sys.argv[1]
texts = {os.path.splitext(f)[0] for _, _, fs in os.walk(root) for f in fs if f.lower().endswith((".png", ".jpg", ".tga"))}
jsons = [os.path.join(d, f) for d, _, fs in os.walk(root) for f in fs if f.endswith(".json")]
wanted = set()
for j in jsons:
    wanted |= set(re.findall(r'TextureFile\\?":\s*\\?"([^"\\]+)', open(j, encoding="utf-8").read()))
settings = [os.path.basename(j) for j in jsons if os.path.basename(j).startswith("Skybox") and "Night" not in j]
missing = sorted(w for w in wanted if w not in texts)
print("presets:", len(settings), sorted(settings)); print("textures referenced:", len(wanted), "present:", len(wanted) - len(missing))
if missing: print("MISSING:", missing)
sys.exit(1 if missing or len(settings) < 7 else 0)
```
Run it on the kept extraction folder. Expected exit 0; if it lists MISSING names, they are what `LoadPresetTexture` will fall back on - record them in the upload note, do not rename anything. Also confirm in the convert log that `CdMCloudsNormal` and the `2k_sky_*_Normal` textures were written as normals (the extractor's "unswizzle" note).

- [ ] **Step 3: Upload** `gh release upload testapp-unity6 ~/dev/dfu-mods/dream-sky.dfmod -R Codex64ai/daggerfall-unity-ios --clobber`. Commit the checker script: `DREAM SKY preset checker`.

---

### Task 7: Build the Dynamic Skies bundle and ship it to the draft

**Files:**
- Output: `Assets/StreamingAssets/Mods/dynamic-skies.dfmod` (and every other pack bundle - this also does the long-queued full rebuild so `pack.py` works again)

- [ ] **Step 1: Full bundle rebuild** (DFU_BUNDLED_MODS unset, ASTC): `git checkout -- ProjectSettings/AudioManager.asset` first (a past crash leaves audio disabled), then
```bash
cd ~/dev/daggerfall-unity && /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . \
  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ApplyAll -logFile ~/dev/dfu-mods/bundles.log
```
Expected: `Assets/StreamingAssets/Mods/dynamic-skies.dfmod` exists (a few MB), plus the 46 pack bundles; log has no `error`. Run `python3 tools/bundled-mods/fetch.py --check` and `python3 tools/bundled-mods/pack.py --check` (or its equivalent listing) -> `dynamic-skies` is NOT in the pack stems.

- [ ] **Step 2: Inspect** with `python3 ~/daggerfall-mobile/tools/dfmod_inspect.py Assets/StreamingAssets/Mods/dynamic-skies.dfmod` -> textures ~22, `code assets 0`, and the report must NOT list a shader (the `.shader` was excluded; a material with a missing shader is expected).

- [ ] **Step 3: Upload** `gh release upload testapp-unity6 Assets/StreamingAssets/Mods/dynamic-skies.dfmod -R Codex64ai/daggerfall-unity-ios --clobber`. Do not commit `StreamingAssets/Mods` bundles if the repo ignores them (check `git status`); commit nothing else here.

---

### Task 8: Diagnostics overlay shows the sky state and GPU frame time

**Files:**
- Modify: `Assets/Scripts/Game/Mobile/MobileAssetStats.cs` (the diagnostics overlay text builder - grep `modmaps` to find the line that prints the material-map counters and add beside it)
- Test: `Assets/Editor/MobileSelfTest.cs` (`TestAssetStatsCounters` extended)

**Interfaces:**
- Produces: `public static string SkyLine(bool dynamicSky, double gpuMs)` on `MobileAssetStats` returning e.g. `sky dynamic gpu 4.2 ms` / `sky vanilla gpu n/a`.

- [ ] **Step 1: Failing test.** In `TestAssetStatsCounters` add `Check(MobileAssetStats.SkyLine(true, 4.25) == "sky dynamic gpu 4.3 ms" && MobileAssetStats.SkyLine(false, -1) == "sky vanilla gpu n/a", "diagnostics: sky line");` -> FAIL.

- [ ] **Step 2: Implement.** `SkyLine` formats as above (`gpuMs < 0` -> `n/a`, else `F1`). In the overlay's per-second refresh: `FrameTimingManager.CaptureFrameTimings(); var t = new FrameTiming[1]; uint n = FrameTimingManager.GetLatestTimings(1, t); double gpu = n > 0 ? t[0].gpuFrameTime : -1;` and `bool dyn = RenderSettings.skybox != null && RenderSettings.skybox.shader != null && RenderSettings.skybox.shader.name == MobilePortedMods.DynamicSkiesShaderName;` then append `SkyLine(dyn, gpu)`. `FrameTimingManager` needs `PlayerSettings.enableFrameTimingStats`; set it in `MobileBuildSetup.ApplyAll` next to the other player settings (`PlayerSettings.enableFrameTimingStats = true;`) - it is cheap and off-device harmless.

- [ ] **Step 3: Self-test green; commit** `Diagnostics: sky mode and GPU frame time`. Copy to staging after diffing.

---

### Task 9: Docs

**Files:**
- Modify: `THIRD-PARTY.md` (after the "Survival mods (compiled in)" section), `~/daggerfall-mobile/README-iOS.md` (a "Sky" subsection under "Mods and loose files" or after "Survival"), `UPSTREAM-PATCHES.md` (entry for `EnsureAlwaysIncludedShaders` + `MobileModExtractor.IsNormalMapName` if changed), `docs/superpowers/plans/2026-09-08-dynamic-skies-port.md` (tick boxes)

- [ ] **Step 1: THIRD-PARTY.md** section `## Dynamic Skies (compiled in, private draft only)`: one table row - `Dynamic Skies 2.3.4 | BadLuckBurt & carademono, NO LICENCE DECLARED (permission being sought by Ikram; not in any public release) | github.com/drcarademono/dynamic-skies @ 04506e2 | SunShafts.cs, PostEffectsBase.cs, SunShaftsComposite.shader, SimpleClear.shader, BLBProceduralSkyboxOriginal.shader (Unity Standard Assets terms / dead code); the skybox shader is compiled into the app with its keyword set pinned` - and a paragraph: DREAM SKY 1.2 (King of Worms, Nexus 664) is a preset for it, converted to `dream-sky.dfmod`, private draft only.
- [ ] **Step 2: README-iOS.md** `### Sky` : what the switch does, that it needs `dynamic-skies.dfmod` in `Mods/`, that `dream-sky.dfmod` gives DREAM's sky, that deleting both removes every byte, where the diagnostics line is, and the vanilla-sky fallback on any error.
- [ ] **Step 3: UPSTREAM-PATCHES.md** entry: `MobileBuildSetup.EnsureAlwaysIncludedShaders` now pins `BLB/SkyBox/BLBProceduralSkybox`; the extractor's normal-map rule change if Task 5 made one.
- [ ] **Step 4: Commit** `Docs: Dynamic Skies and DREAM SKY`.

---

### Task 10: Simulator and device verification

- [ ] **Step 1: Simulator build.** `DFU_IOS_TESTAPP=1 DFU_IOS_SIM=1 DFU_BUNDLED_MODS=builtin` -> `ApplyAll` then `BuildIOS` (see memory recipe; sim bundles need `DFU_PACK_TEX_FORMAT=RGBA32` for a sim-only `dynamic-skies.dfmod` and `dream-sky` rebuild via `ReimportPacks`+`BuildBundledMods`, then RESTORE ASTC bundles afterwards). Recreate an iPad simulator, copy arena2, seed `settings.ini` `ShowOptionsAtStart = False`, park `ANIM0001.VID` out of arena2, put `dynamic-skies.dfmod` (RGBA32 build) in the container's `Documents/Mods`, enable `Dynamic Skies` in the container's `Mods.json`, launch with `debug-newchar.txt` = `pixel 207 213`.
  Expected in Player.log: `[PortedMods] started Dynamic Skies`, `Dynamic Skies has been set up`, no `[DynamicSkies]` errors, no `Shader error`. Screenshot via `xcrun simctl io booted screenshot sky-day.png` shows a procedural sky (clouds, not the classic sky strip). Repeat with `dream-sky.dfmod` (RGBA32 build) added: log shows `Dynamic Skies - Found preset mod: DREAM - SKY`, screenshot differs (DREAM clouds). Advance to night with `set_time` in the console if reachable, else rest; screenshot shows stars and moons.
- [ ] **Step 2: Negative check.** Remove `dynamic-skies.dfmod`: no launcher entry, no `[PortedMods] started Dynamic Skies`, vanilla sky. Temporarily rename the shader in a scratch build? No - instead unit-covered by Task 2/3; skip.
- [ ] **Step 3: Device build.** `DFU_IOS_TESTAPP=1 DFU_BUNDLED_MODS=builtin` ApplyAll -> BuildIOS -> pbxproj bundle id `net.codex64.daggerfall.test.K8RF7RDFB5` (app target only) -> xcodebuild signed -> `xcrun devicectl device install app --device 528EE413-05E3-5A39-87AC-9639CF1C6AD2 <DFUTest.app>`. Push ASTC `dynamic-skies.dfmod` + `dream-sky.dfmod` into the app's `Documents/Mods` with `devicectl copy to`. Also export the unsigned ipa as `DFU-Test-unity6-sky.ipa` to the draft.
- [ ] **Step 4: Hand to Ikram** with the checklist: enable `Dynamic Skies` in MODS; sky at title/outdoors day and night; weather change (rain/fog) changes the sky; interiors unaffected; Real travel journey shows clouds moving faster and no black sky after arrival; TUNE > Advanced > Show diagnostics: read the `sky dynamic gpu N ms` line with the switch on and off and report both. Save+load outdoors keeps the sky. If anything is off: pull Player.log over the cable, look for `[DynamicSkies]` and `Dynamic Skies` lines.
- [ ] **Step 5: Record** results in memory (`daggerfall-dream-conversion.md`, `daggerfall-ios-port.md` HANDOFF) and tick this plan.
