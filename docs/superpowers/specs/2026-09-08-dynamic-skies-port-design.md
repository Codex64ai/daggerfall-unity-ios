# Dynamic Skies port for iOS, with DREAM SKY as its preset

Date: 2026-09-08. Status: approved in conversation (Ikram), option "launcher switch, off by default"
and approach B (shader compiled into the app).

## Goal

DREAM's sky on the iPad. DREAM SKY 1.2 (Nexus mods/664) is not a sky texture pack: it is a preset
bundle for Dynamic Skies (Nexus mods/376, github.com/drcarademono/dynamic-skies) - 13 textures plus
16 JSON files. It draws nothing on its own. So Dynamic Skies must run on iOS first, the way the
survival mods do (code compiled in), and DREAM SKY is converted like the other DREAM modules.

Licensing is being handled separately by Ikram. Nothing here is published; the DREAM pieces stay on
the private draft release.

## Constraints (from Ikram)

- Off by default. A launcher switch in the MODS window, like the survival mods.
- The app must not grow. Only code and one shader go into the app; every texture stays in
  downloadable bundles that the player can delete.

## What Dynamic Skies is (upstream commit 04506e2, 2025-05-18)

- `BLBSkybox.cs` (110 KB): replaces `GameManager.SkyRig` with a Unity skybox material set in
  `RenderSettings.skybox`, drives it from `WorldTime` and `WeatherManager` (day parts, dawn/dusk
  atmosphere lerp, lunar phases, fog colours and distances, lightning flashes, optional pixel snow).
  Reads the material with `Mod.GetAsset<Material>("Materials/BLBSkyboxMaterial")`.
- Seven weather presets (`SkyboxSettings/Skybox*.json`) plus night variants (`Resources/*Night.json`),
  fog settings, a light curve, 21 MB of cloud/star/moon textures, and the material.
- `Shaders/BLBProceduralSkybox.shader` (52 KB) + `Includes/Scattering.cginc`, `MoonFunctions.cginc`.
  Five keyword pragmas (3 x 3 x 2 x 2 x 3 = 108 combinations) times fog variants. The material pins
  `REDUCE_COLOR`, `_MOONSPINOPTION_TIDAL_LOCK`, `_SECUNDASPINOPTION_TIDAL_LOCK`, `_SUNDISK_HIGH_QUALITY`
  and the C# only ever sets floats, never keywords - so exactly one keyword set is ever used.
- `SunShafts.cs`, `PostEffectsBase.cs`, `SunShaftsComposite.shader`, `SimpleClear.shader`,
  `BLBProceduralSkyboxOriginal.shader` are in the repo but NOT in the manifest's Files list and are
  not referenced by `BLBSkybox.cs`. They are dead code and are not shipped.
- Preset discovery (`FindPresetMod`): walks `ModManager.EnumerateModsReverse()`, skips itself and
  disabled mods, takes the first mod whose `FindAssetNames(.., "SkyboxSettings", ".json")` returns at
  least seven names. Textures are then loaded ONLY from that preset mod
  (`presetMod.GetAsset<Texture2D>(name)`).
- Engine hooks it needs all exist in our fork (DFU 1.1.1): `GameManager.SkyRig`, `CameraClearManager`,
  `WeatherManager.FogSettings.excludeSkybox`, the `SunLight` object, `PlayerAdvanced/SmoothFollower/
  Snow_Particles`.

## Design

### 1. Where things live

| Piece | Location | In the app? |
|---|---|---|
| Mod C# (BLBSkybox.cs, Scripts/BLB*.cs, LightningFlash*.cs) | `Assets/Scripts/Game/Mobile/Ports/DynamicSkies/` | yes (code only) |
| Skybox shader + 2 includes | `Assets/Shaders/BLB/` | yes, Always Included |
| Data bundle `dynamic-skies.dfmod` (material, JSON, textures, modsettings) | `Assets/Game/Mods/DynamicSkies/` via `tools/bundled-mods/mods.json` | NO - pack zip / draft |
| `dream-sky.dfmod` (converted DREAM SKY) | draft release only | NO |

The data bundle is a normal downloadable mod (NOT `builtin: true`), so the pack script ships it and
`DFU_BUNDLED_MODS=builtin` app builds leave it out. If the bundle is not installed there is no
launcher entry and the compiled code never runs.

### 2. The shader (approach B)

Our copy of `BLBProceduralSkybox.shader`, header marked with the upstream commit, three edits:
1. The five keyword pragmas (lines 134-138) are replaced by fixed `#define`s matching the material:
   `_MOONSPINOPTION_TIDAL_LOCK`, `_SECUNDASPINOPTION_TIDAL_LOCK`, `REDUCE_COLOR`,
   `_SUNDISK_HIGH_QUALITY`; `PHASE_LIGHT` stays undefined. `multi_compile_fog` stays. Result: the
   fog variants only.
2. `#pragma target 3.5` added explicitly.
3. `sampler3D _Lut` and the `_Lut` property are removed; the only use (`tex3D(_Lut, ...)`) is already
   commented out upstream.

`MobileBuildSetup.EnsureAlwaysIncludedShaders` gains `"BLB/SkyBox/BLBProceduralSkybox"`.

The data bundle EXCLUDES the shader and include files (`exclude_globs`), so its material arrives with
property values and keywords but a missing shader reference. At Init the port assigns
`MobileShaders.Find("BLB/SkyBox/BLBProceduralSkybox")` to that material. Keywords survive the swap
because they are stored on the material.

### 3. Switching on

`MobilePortedMods` gets a fourth title, `"Dynamic Skies"` (the manifest's `ModTitle`):
- `DefaultOff` covers it (first discovery -> off; a saved choice wins).
- `Gate`: independent, no dependencies, so it is not part of the RR/Items/C&C triple. Add it as its
  own boolean path, keeping the existing pure `Gate(rr, items, cc)` unchanged.
- `StartEnabled` calls `BLBSkybox.Init(new InitParams(mod, index, count))` when the entry is enabled,
  after the survival mods (order between them does not matter).
- `EnsureOrder` is not extended: the sky mod has no activation conflicts.

Preset choice stays upstream: `dream-sky.dfmod` installed and enabled -> DREAM's sky; otherwise the
mod's own default sky.

Settings (`modsettings.json`: FogDensity, pixel snow) show in the launcher's settings window like the
survival mods'. The DREAM 2026 preset for them is already in `DREAM-extras-ios.zip`.

### 4. Edits to the mod code (keep the diff against upstream tiny, like the other ports)

1. Remove `[Invoke(StateManager.StateTypes.Start, 0)]`.
2. After `skyboxMat` is loaded: `skyboxMat.shader = MobileShaders.Find(...)`. If the shader is null or
   the material is null: `Debug.LogError("[DynamicSkies] ...")` and RETURN before `dfSky.SetActive(false)`,
   so the vanilla sky stays.
3. In `ProcessSkyboxSetting` (player branch): a helper `LoadPresetTexture(name)` that tries the preset
   mod, then falls back to the Dynamic Skies mod's own asset of that name, and logs
   `"[DynamicSkies] preset texture missing: <name>"` when the preset lacks it. Never leaves a null when
   the default has it.
4. Editor-only code (`ImportSkyboxSettings` etc.) stays under its existing `#if UNITY_EDITOR`.
   Verify nothing outside those guards references `UnityEditor`.

Every file carries the port header used by the other Ports (source repo, commit, "copied unchanged
except lines marked MOBILE").

### 5. The two bundles

- `mods.json` entry `DynamicSkies`: repo `https://github.com/drcarademono/dynamic-skies.git`, commit
  `04506e2ef65aff27e4882ec07ed29a6fc7a4be6b`, subdir `.` (repo root is the mod), manifest
  `dynamic-skies.dfmod.json`, `strip_code: true`, `exclude_globs` for `*.shader`, `*.cginc`,
  `Scripts/SunShafts.cs`, `Scripts/PostEffectsBase.cs`. NOT builtin. Licence text: "none declared
  upstream; private draft only pending permission (Ikram)". `fetch.py`'s licence check must accept
  that wording or gain an explicit allow for this entry - decide in the plan, do not weaken the check
  for other entries.
- DREAM SKY: `~/Downloads/DREAM - SKY-664-1-2-1748213265.rar` -> `dream - sky.dfmod` (macOS bundle,
  3.4 MB). Extract with `MobileModExtractor`, rebuild with `MobileModBuilder` as `dream-sky.dfmod`
  (ASTC; 2048 star maps are power-of-two so no rescale question). Before upload, check the extractor
  report: (a) every texture name referenced by the JSON presets (`*TextureFile` fields, including the
  flattened TopCloudsFlat/BottomCloudsFlat/StarsFlat/MasserFlat/SecundaFlat strings) exists in the
  bundle; (b) the cloud normal maps (`CdMCloudsNormal`, `2k_sky_*_Normal`) are recognised by
  `IsNormalMapName` so they are re-encoded as normals. If a name misses the rule, extend the rule.
  Upload to the `testapp-unity6` draft.

### 6. Failure handling

- Shader missing from the build -> Init logs and leaves the vanilla sky. Never a pink sky.
- Data bundle missing -> no entry, nothing runs.
- Preset texture missing -> fallback to default texture + log line.
- Init exception -> caught by MobilePortedMods' existing try/catch around StartEnabled; logged; the
  other built-ins still start.

### 7. Testing

Self-tests (MobileSelfTest, graphics-enabled run):
- shader `BLB/SkyBox/BLBProceduralSkybox` is found and `isSupported`, and its keyword set compiles to
  the fog variants only (assert `Shader.Find` non-null; variant count check in the editor via
  `ShaderUtil` where available).
- assigning the app shader to a material that carries the four keywords keeps the keywords.
- the fourth entry defaults off and starts only when enabled (pure gate test).
- `LoadPresetTexture` falls back to the default mod's texture and logs when the preset lacks a name.

Editor: iOS-target shader compile check; play-mode run with the mod enabled proving `SkyRig` is
inactive, `RenderSettings.skybox` is the material, and a weather change updates `_CloudDiffuse`.

Simulator: sky visible at title and in Daggerfall city, day and night, with and without dream-sky.

Device (Ikram): visual check; the diagnostics overlay gains the sky's frame cost (skybox draw timing
via a simple `Time` sample around `Camera.onPreRender/onPostRender` is NOT reliable - use Unity's
`FrameTimingManager` GPU time when available, else the existing frame-time readout before/after
toggling the switch). If too heavy, the switch is the mitigation.

## Out of scope

Sun shafts (dead upstream code), pixel snow beyond the upstream setting, Transparent Windows
integration (that mod is not shipped; the check returns null harmlessly), Distant Terrain's stacked
camera path (not shipped).
