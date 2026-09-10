# Mobile CRT Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A licence-free single-pass CRT filter (curvature, scanlines, RGB grille, vignette) applied to DFU's retro presentation blit, off by default, with clamped retro settings and an in-game settings page plus mobile panel rows.

**Architecture:** One new shader `Daggerfall/Mobile/CRT` (Always-Included), one material argument added to the single `Graphics.Blit` in `RetroPresentation.OnRenderImage`, five clamped settings keys, a `CRTConfigPage` in `GameEffectsConfigWindow`, rows in `MobileSettingsPanel`. World only, retro mode only.

**Tech Stack:** Unity 6000.3.23f1 built-in pipeline, ShaderLab/HLSL (`#pragma target 3.0`), DFU settings + UI, MobileSelfTest.

**Spec:** `docs/superpowers/specs/2026-09-10-mobile-crt-design.md` (research Part B).

## Global Constraints
As the Real Grass plan (branch, trailers, one Unity, self-test command/baseline from the ledger, churn rules; GraphicsSettings pin committed). Workspace `.superpowers/sdd/2026-09-10-mobile-crt/`. Every file we write carries the port's MIT header; NO code copied from GPL CRT shaders (crt-pi, crt-geom, crt-easymode, crt-royale) - textbook formulas only.

---

### Task 1: Shader + hook + settings (one task, one review)
**Files:** `Assets/Shaders/Mobile/MobileCRT.shader` (new), `Assets/Scripts/Utility/RetroPresentation.cs`, `Assets/Scripts/Game/Mobile/MobileShaders.cs` (`names` += `Daggerfall/Mobile/CRT`), `Assets/Editor/MobileBuildSetup.cs` (Always-Included), `Assets/Shaders/RequiredShaderVariants.shadervariants` (the `CRT_HALATION` pair), `Assets/Scripts/SettingsManager.cs` + `Assets/Resources/defaults.ini.txt` (keys), `Assets/Editor/MobileSelfTest.cs`
**Interfaces:** settings `CRTFilter` (bool, False), `CRTCurvature` (float 0..0.3, 0.08), `CRTScanlines` (0..1, 0.35), `CRTMask` (0..1, 0.25), `CRTVignette` (0..1, 0.25); clamps `PostProcessingInRetroMode` 0..4, `PalettizationLUTShift` 1..3 with iOS default 2; pure `public static class MobileCrt { static float Clamp01(float), static int ScanlineCount(int retroMode) => retroMode == 2 ? 400 : 200, static bool Active(bool enabled, int retroMode, bool materialOk) => enabled && retroMode != 0 && materialOk }`; `RetroPresentation` gets `static Material crtMaterial` resolved lazily via `MobileShaders.Find("Daggerfall/Mobile/CRT")` and pushes the five uniforms + `_ScanlineCount` when `Active`.
- [ ] Step 1: failing checks: clamp/default table (read `DaggerfallUnity.Settings` after a `SettingsManager` load in the Editor, or test the pure clamp helpers), `ScanlineCount(1)==200`/`(2)==400`, `Active` truth table, `MobileShaders.Find("Daggerfall/Mobile/CRT") != null && isSupported`, the shader name in the Always-Included list, source-text: `RetroPresentation.cs` contains `Graphics.Blit(RetroPresentationSource, null as RenderTexture, ` (the material overload).
- [ ] Step 2: shader (fragment sketch, write it fully): `uv = i.uv*2-1; r2 = dot(uv,uv); uv *= 1 + _Curvature*r2; uv = uv*0.5+0.5; if (any(uv<0)||any(uv>1)) return 0; col = tex2D(_MainTex, uv).rgb; col *= col (fake gamma in); float sl = 0; for k in -1..1: sl += 0.5-0.5*cos((uv.y + k*(1.0/(3*_ScanlineCount)))*_ScanlineCount*6.2831853); sl /= 3; col *= 1 - _Scanlines*sl; float band = frac(i.screenPos.x*_ScreenParams.x/3.0); half3 mask = 1 - _Mask*(band<0.333 ? half3(0,1,1) : band<0.667 ? half3(1,0,1) : half3(1,1,0)); col *= mask; col *= 1 - _Vignette*r2; col = sqrt(col); return half4(col,1)`; `_MainTex` `FilterMode.Bilinear` is set on the source RT by the caller if it is Point? NO - the retro source is Point on purpose (crisp pixels); sample as is and document that the scanline math, not filtering, provides the CRT softness. Optional `CRT_HALATION` variant: +2 taps at +-1 px horizontal added at 0.15.
- [ ] Step 3: hook: in `OnRenderImage`, `if (MobileCrt.Active(DaggerfallUnity.Settings.CRTFilter, DaggerfallUnity.Settings.RetroRenderingMode, Material != null)) { set uniforms; Graphics.Blit(RetroPresentationSource, null as RenderTexture, Material); } else Graphics.Blit(RetroPresentationSource, null as RenderTexture);` marked `// MOBILE`. Settings: add the five keys (getter/setter + ini read/write like the neighbouring retro keys) and the two clamps; `defaults.ini.txt` `PalettizationLUTShift=2`.
- [ ] Step 4: GREEN; pin via `ApplyIOSSettings`; commit `Mobile CRT: single-pass CRT filter on the retro presentation blit; retro settings clamped`. Docs UPSTREAM-PATCHES entry (RetroPresentation.cs and SettingsManager.cs are upstream files - list lines).

### Task 2: UI - CRT page + mobile panel rows
**Files:** `Assets/Scripts/Game/UserInterfaceWindows/CRTConfigPage.cs` (new; model on `RetroModeConfigPage.cs`: toggle + four sliders, tip text "The HUD stays sharp on purpose"), `GameEffectsConfigWindow.cs` (`AddConfigPage(new CRTConfigPage())`), `Assets/Scripts/Game/Mobile/MobileSettingsPanel.cs` (rows: Retro mode 0/1/2, Aspect 0/1/2, CRT filter on/off - use the panel's existing row helpers; read the file first and add the minimum that fits its patterns), `MobileSelfTest.cs` (a check that `GameEffectsConfigWindow` registers the page: source-text or reflection over the pages list).
- [ ] RED/GREEN; commit `Mobile CRT: Game Effects page and settings-panel rows`.

### Task 3: Simulator verification
Sim app; `settings.ini` with `RetroRenderingMode=1`, `CRTFilter=True`; launch at 207,213: screenshot shows curvature (black corners), scanlines (crop at 4x: dark lines every device-pixel-multiple of the 200-line raster), grille; HUD unfiltered (crop of the compass); `RetroRenderingMode=0` + CRT on -> plain (no presenter); `CRTFilter=False` -> plain retro. Log has no shader errors. Report the overlay frame-time line on/off.

### Task 4: Docs
README-iOS.md `### CRT filter` (where the switch lives, the four sliders, HUD stays sharp, retro mode required, the `PalettizationLUTShift` default change), THIRD-PARTY.md (our MIT shader, no third-party code), UPSTREAM-PATCHES.md.

### Task 5: Device build
Combined with Real Grass if ready: `DFU-Test-unity6-grass-crt.ipa` (else `DFU-Test-unity6-crt.ipa`), restore 52/52, upload, install if connected.

## Self-review
Spec 1 (T1), 2 (T1), 3 (T1 keys, T2 UI), 4 (T3, T5). Interfaces: `MobileCrt.Active/ScanlineCount` (T1) used by the hook and tests; page name `CRTConfigPage` (T2). No GPL code by constraint.
