# Mobile CRT filter: our own single-pass shader on the retro presentation blit

Date: 2026-09-10. Approved in conversation (Ikram: "Do both go!"). Fable plans; Opus builds. Research:
`docs/superpowers/research/2026-09-10-grass-and-crt.md` Part B. No mod is ported: no CRT mod exists for DFU; DFU's retro
mode (320x200 / 640x400, VGA palette, 4:3 stretch) already ships and is reachable on iOS via pause > options > Game Effects.

## Design
1. **Shader** `Assets/Shaders/Mobile/MobileCRT.shader`, name `Daggerfall/Mobile/CRT`, MIT header (our code; techniques are
   textbook: barrel UV, cosine scanlines, procedural RGB grille, radial vignette; NO code copied from GPL shaders), `#pragma target 3.0`,
   ONE `tex2D` (hardware bilinear does the horizontal filtering), `half` maths. Uniforms: `_Curvature` (0..0.3), `_Scanlines`
   (0..1 amplitude), `_ScanlineCount` (200 or 400, tied to `RetroRenderingMode` so lines land on the source raster, never on device
   pixels - the moire rule), `_Mask` (0..1 grille strength, `frac(screenPos.x/3)` bands), `_Vignette` (0..1), `_Gamma` fake gamma
   (`c*c` / `sqrt`). Analytic scanline multisampling (3 evaluations, 0 extra fetches). Outside the curved screen: black.
   One `multi_compile __ CRT_HALATION` for an optional 2-tap halation tier (default off). Pinned Always-Included + shadervariants.
2. **Hook**: `Assets/Scripts/Utility/RetroPresentation.cs` `OnRenderImage`: `Graphics.Blit(source, null, crtMaterial)` when the
   filter is enabled, else the existing plain blit. World only (IMGUI HUD/menus drawn after, unfiltered - deliberate: touch
   controls must not curve away from fingers); retro mode only (no presenter when `RetroRenderingMode == 0`). Material resolved via
   `MobileShaders.Find("Daggerfall/Mobile/CRT")`; if null the filter silently stays off (log once).
3. **Settings**: `SettingsManager` + `defaults.ini.txt` keys `CRTFilter` (bool, default False), `CRTCurvature` (0.08),
   `CRTScanlines` (0.35), `CRTMask` (0.25), `CRTVignette` (0.25) - clamped. ALSO clamp the two unclamped retro keys the research
   found: `PostProcessingInRetroMode` to 0..4 and `PalettizationLUTShift` to 1..3 (shift 0 = 64 MB Texture3D + ~7 s build; iOS
   default changed to 2 = 1 MB); the LUT build stays where DFU does it (documented). UI: a fourth page `CRTConfigPage` in
   `GameEffectsConfigWindow` next to `RetroModeConfigPage` (toggle + four sliders, tip text says the HUD stays sharp on purpose),
   PLUS three rows in `MobileSettingsPanel` (Retro mode, CRT filter, aspect) so it is findable by thumb - Task decides the
   minimum that the panel's existing row types support.
4. **Verification**: self-tests (clamps, defaults, shader pin, settings round-trip); simulator: retro 320x200 + CRT on -> screenshot
   shows scanlines/curvature (crop) and the HUD unfiltered; retro off -> plain; frame-time overlay line unchanged within noise;
   device ipa `DFU-Test-unity6-crt.ipa` (installed if connected) - the 5.6 MP iPad fill-rate is the device question.

## Out of scope
Whole-screen (UI-inclusive) filtering, Retro Frame bezel, PPv2 route, halation on by default.
