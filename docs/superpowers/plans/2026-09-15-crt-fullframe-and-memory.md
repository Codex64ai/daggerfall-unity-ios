# CRT full frame + terrain/sprite memory + residuals

Date 2026-09-15. Approved (Ikram: "Let's do 2, 3, 5. Also the CRT doesn't affect the UI or the player first person sprites so it looks off").
Global Constraints as docs/superpowers/plans/2026-09-14-atmosphere-round.md (one Unity via `ps -Ao comm | grep -c "MacOS/Unity$"`, kill nothing,
token-lean, self-test full app path no -nographics, restore 57x4 after builtin builds, revert churn, files by name, trailers, do not push).
Disk: keep >= 12 GiB before a device build (delete only ~/dev/dfu-ios-build, ~/dev/dfu-ios-sim, DerivedData, /private/tmp/ipa*).

## Task A: CRT covers the whole frame (weapon, HUD, menus), touch controls stay sharp
Today the CRT material is applied to the world presentation only (RetroPresentation blit / MobileCrtNative target); DFU's HUD, menus and the
first-person weapon are IMGUI (OnGUI) drawn after all cameras, so they escape the filter and look pasted on. Design: a new setting `CRTCoverage`
int 0..2 default 1: 0 = world only (today), 1 = whole frame except touch controls, 2 = everything. Mechanism for 1/2: an end-of-frame pass
(`WaitForEndOfFrame` coroutine on a persistent MonoBehaviour) that copies the back buffer into a screen-sized RT
(`ScreenCapture.CaptureScreenshotIntoRenderTexture` or `Graphics.Blit(null...)`-equivalent that works on Metal - verify which works in a player;
`CaptureScreenshotIntoRenderTexture` is GPU-side), blits it back through the CRT material, and for coverage 1 then re-renders the touch canvas on
top: give MobileCanvas a dedicated disabled UI camera (the unused patch in ~/dev/dfu-mods/grass-crt-evidence/touch-canvas/not-committed-ui-camera.patch
is a starting point) rendered manually with `camera.Render()` after the blit (ScreenSpaceCamera canvas, UI layer only, clear Nothing). When coverage
is 1/2 the world-only path must NOT also apply the filter (double filtering): MobileCrt.Active for the presenter returns false and the retro
presenter does its plain blit; scanline count = device rows / CRTScanlineCount as today. Retro mode on + coverage 1: the retro world is upscaled
first (plain), then the frame is filtered once. Panel: a "CRT coverage" row (World / Frame / Everything). Cost: one full-screen copy + one blit;
log `[CRT] frame pass WxH (coverage N)` once. Self-tests: settings clamp/default, `Active` truth table with coverage, source pins (end-of-frame
order: capture -> blit -> touch camera render), no double filter. Sim: screenshots coverage 0/1/2 with the weapon drawn (readied weapon via
`set`? the weapon shows when a weapon is equipped and drawn - use the debug character's dagger; `spawn` nothing) - the weapon sprite and HUD must
show scanlines/curvature in 1 and 2, joysticks sharp in 1 and curved in 2; retro-on + coverage 1 regression; frame-time line on/off.
Commit `CRT filter: whole-frame coverage option (weapon, HUD and menus filtered; touch controls stay sharp)`.

## Task B (same dispatch as A, after it): residuals
1. DistantTerrain.cs `new TerrainData()` -> `MobileTerrainData.Create()` (Unity bug 10753 trap), pin.
2. MobileCrtNative: keep the native target alive for 10 s after the filter turns off (grace) so retro/CRT toggling does not re-allocate per toggle;
   Release on scene change/low memory as today.
3. Bundle id pin: MobileBuildSetup writes `PRODUCT_BUNDLE_IDENTIFIER` for the app target into the generated Xcode project (post-build
   `IPostprocessBuildWithReport` / PBXProject API) from the same env/define that sets the test bundle id, so xcodebuild never picks the plain profile;
   self-test pin; README build recipe updated.
4. Staging: `~/daggerfall-mobile/Mobile/*.cs` and `editor/MobileSelfTest.cs` are behind the repo. RULING: the REPO is authoritative from now on; make
   staging a mirror (`rsync` the current repo files over staging, list what changed) and add a README note; update the memory file's claim.
Commit `Residuals: far terrain uses the terrain-data template; CRT target grace period; bundle id pinned by the build script; staging mirrors the repo`.

## Task C: DEX sprite compression + DREAM terrain tiles at 256 (second dispatch)
1. DEX: 7,341 sprites imported RGBA32 (Unity refused ASTC for NPOT with npotScale None - VERIFY: ASTC/ETC2 support NPOT in Unity; the refusal may be
   the mipmap setting or `TextureImporterFormat` vs platform override). Experiment with `DFU_REIMPORT_ONE` on a 126x120 DEX sprite: (a) ASTC_6x6 iOS
   override + mips off, (b) ASTC_4x4, (c) `npotScale` None + `TextureImporterCompression.Compressed` with format Automatic. Find the setting that yields a
   compressed NPOT import at 1:1 size; if none exists, pad to the next multiple of 4 (not POT) if that is what ASTC needs - DFU billboards size from
   the texture dimensions, so padding must be compensated (TextureReplacement scale from the record's classic size / a MOBILE size table) - prefer
   the importer fix. Apply to the DEX pack rule (and any other sprite-heavy pack: VE, WoD nature), rebuild the DEX bundle, inspect (formats, MB),
   upload --clobber, backup refresh. Report resident MB per archive before/after.
2. DREAM terrain tiles: `MobileModExtractor` `IsTerrainTileTexture` (~:2526-2560) + `MobileModBuilder` (~:224-231): `TerrainTileMaxTextureSize = 256`
   for terrain tile archives (002-004,102-104,202-204,302-304,402-404 + their _Normal if present) - reconvert the DREAM textures set (10 parts; find the
   converter recipe in ~/.claude/projects/-Users-ikrammassabini/memory/daggerfall-dream-conversion.md), inspect the terrain archives at 256, upload
   the changed parts --clobber, backup refresh; log the per-array memory in `[TextureArray]` (already sized) before/after. Self-tests for both rules.
Commit `Textures: DEX sprites compressed at 1:1; DREAM terrain tiles capped at 256 (26 MB -> <2 MB per tileset array)`.

## Task D: device ipa `DFU-Test-unity6-crtframe.ipa` (in the Task C dispatch) per the atmosphere recipe; upload; install + push changed bundles
if the iPad is available. Docs: README-iOS (CRT coverage row, known limitation updates), UPSTREAM-PATCHES, THIRD-PARTY unchanged.
