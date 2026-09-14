// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the CRT presentation filter's decisions, kept out of RetroPresentation.cs so that
// upstream file carries the smallest patch it can, and so the rules are testable headlessly -
// OnRenderImage is a MonoBehaviour callback no editor test can invoke.
//
// Everything here is a pure function of its arguments except LiveRasterHeight and Material,
// which are the two things that have to ask the running project a question (which render
// texture is the main camera pointed at this frame; is the shader in this build). The pure
// halves - ScanlineCountFor, the clamps, NativeTargetSize, DockedViewportRect - are what the
// self-tests exercise, so the rules can be checked without a game.
//
// TWO PATHS, ONE SHADER. Retro mode on: the presenter blits RetroRenderer's 640x400
// presentation texture, and the scanline count is the SOURCE RASTER's own height, because
// lines drawn anywhere else beat against it (see the shader's MOIRE note). Retro mode off:
// MobileCrtNative points the main camera at a viewport-sized, native-resolution render texture
// and the same presenter blits THAT - there is no raster to lock to, so the count becomes a
// taste setting, CRTScanlineCount.
//
// Place in Assets/Scripts/Game/Mobile/

using UnityEngine;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Pure rules for the CRT filter (Assets/Shaders/Mobile/MobileCRT.shader).
    /// </summary>
    public static class MobileCrt
    {
        /// <summary>Name of the CRT shader, as registered with MobileShaders and pinned Always-Included.</summary>
        public const string ShaderName = "Daggerfall/Mobile/CRT";

        /// <summary>Upper bound on the barrel term. Held close to the eye, a strongly curved
        /// picture reads as a defect rather than as a tube, so the settings clamp matches this.</summary>
        public const float MaxCurvature = 0.3f;

        /// <summary>Scanlines in RetroRenderingMode 1 (320x200).</summary>
        public const int ScanlinesLowRes = 200;

        /// <summary>Scanlines in RetroRenderingMode 2 (640x400).</summary>
        public const int ScanlinesHighRes = 400;

        /// <summary>Lower bound on CRTScanlineCount. Below about a hundred lines the picture stops
        /// reading as a tube and starts reading as venetian blinds.</summary>
        public const int MinScanlineCount = 100;

        /// <summary>Upper bound on CRTScanlineCount. Past the panel's own row count the lines
        /// alias into a flicker instead of drawing, whatever the multisampling does.</summary>
        public const int MaxScanlineCount = 1200;

        /// <summary>Scanlines the non-retro path draws by default: a VGA 640x480 tube's count.
        /// On a 1640- to 2048-row iPad panel that is 3.4 to 4.3 device rows per line - fine
        /// enough to read as a monitor rather than as a television, coarse enough that the
        /// shader's three-tap analytic multisample kills the beat against the panel grid.</summary>
        public const int DefaultScanlineCount = 480;

        /// <summary>Clamps a 0..1 amplitude. The settings loader already clamps what comes out of
        /// the ini, but the in-game sliders write the properties directly, so the shader's
        /// uniforms are clamped again at the point of use.</summary>
        public static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        /// <summary>Clamps the barrel term to 0..MaxCurvature.</summary>
        public static float ClampCurvature(float value)
        {
            if (value < 0f)
                return 0f;
            return value > MaxCurvature ? MaxCurvature : value;
        }

        /// <summary>Clamps a scanline count to MinScanlineCount..MaxScanlineCount.</summary>
        public static int ClampScanlineCount(int value)
        {
            if (value < MinScanlineCount)
                return MinScanlineCount;
            return value > MaxScanlineCount ? MaxScanlineCount : value;
        }

        // ---- coverage --------------------------------------------------------------------
        // "the CRT doesn't affect the UI or the player first person sprites so it looks off"
        // (Ikram, 2026-09-15). The filter has always run on the WORLD's presentation blit, and
        // DFU draws its HUD, its menus and the first-person weapon afterwards in IMGUI (OnGUI),
        // which no camera and no blit can reach. Coverage is the answer: at 1 and 2 the world-only
        // filter is switched OFF and a single end-of-frame pass (MobileCrtFrame) filters the
        // finished frame instead - everything the player can see, drawn in whatever order it was
        // drawn. The only question left is the touch controls, and that is the difference between
        // 1 and 2: a joystick that curves away from where the finger lands is a joystick the player
        // misses, so 1 (the default) redraws them sharp on top of the filtered frame.

        /// <summary>Coverage 0: the world's presentation blit only - where the filter has always
        /// run. The IMGUI HUD, the menus and the first-person weapon stay flat and sharp.</summary>
        public const int CoverageWorld = 0;

        /// <summary>Coverage 1: the whole frame except the touch controls, which are redrawn sharp
        /// on top of it. The default.</summary>
        public const int CoverageFrame = 1;

        /// <summary>Coverage 2: everything, touch controls included.</summary>
        public const int CoverageEverything = 2;

        /// <summary>Default coverage: the whole frame with the touch controls left sharp.</summary>
        public const int DefaultCoverage = CoverageFrame;

        /// <summary>Clamps a coverage to 0..2. A hand-edited ini is the reason this exists, and an
        /// out-of-range value must land on a picture that works rather than on no picture: anything
        /// below 0 becomes the world-only path the filter shipped with, anything above 2 becomes
        /// "everything".</summary>
        public static int ClampCoverage(int value)
        {
            if (value < CoverageWorld)
                return CoverageWorld;
            return value > CoverageEverything ? CoverageEverything : value;
        }

        // ---- the material ----------------------------------------------------------------
        // Resolved once, and here rather than in RetroPresentation so the upstream file's patch
        // stays small and so MobileCrtNative can ask "is there a filter to run at all" without
        // reaching into a MonoBehaviour's private statics.

        static Material material;
        static bool materialResolved;

        /// <summary>
        /// The CRT material, built on first use from the shader MobileShaders captured at startup
        /// (Shader.Find alone can hand back a mod bundle's embedded copy of a shader once bundles
        /// have loaded). Resolved once: if the shader is missing - stripped from an iOS build
        /// despite the Always-Included pin - it says so once and stays null, and both call sites
        /// fall back to their plain blit rather than to a black screen.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (materialResolved)
                    return material;

                materialResolved = true;
                Shader shader = MobileShaders.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning("[CRT] shader " + ShaderName + " not found - CRT filter disabled");
                    return null;
                }
                material = new Material(shader);
                return material;
            }
        }

        // ---- which path runs -------------------------------------------------------------

        /// <summary>
        /// Whether the RETRO presentation blit runs the filter this frame. Retro mode 0 is
        /// excluded here on purpose: in that mode RetroRenderer has no presentation texture to
        /// hand over, and the filter arrives by the native path below instead. A missing
        /// material - a stripped shader on device - leaves the plain blit in place.
        /// </summary>
        public static bool Active(bool enabled, int retroMode, bool materialOk)
        {
            return enabled && retroMode != 0 && materialOk;
        }

        /// <summary>
        /// Whether the NATIVE path runs: the filter is on, retro mode is off (so there is no
        /// raster to lock to and no presentation texture), and the shader made it into the build.
        /// MobileCrtNative turns this into a render target; RetroPresentation only has to know
        /// that its source is not RetroRenderer's.
        /// </summary>
        public static bool NativeActive(bool enabled, int retroMode, bool materialOk)
        {
            return enabled && retroMode == 0 && materialOk;
        }

        /// <summary>
        /// The retro presentation blit runs the filter only at coverage 0. At 1 and 2 the frame
        /// pass owns the filter and this blit must present PLAINLY, or the picture is filtered
        /// twice - two sets of scanlines at two scales, two barrel warps, a vignette squared.
        /// The three-argument form above is left exactly as it was: it is the retro-versus-native
        /// partition, which coverage does not change, and the truth table pinned for it in the
        /// self test is the invariant this file has to keep.
        /// </summary>
        public static bool Active(bool enabled, int retroMode, bool materialOk, int coverage)
        {
            return Active(enabled, retroMode, materialOk) && ClampCoverage(coverage) == CoverageWorld;
        }

        /// <summary>
        /// The native path (a viewport-sized render target under Camera.main) exists only to give
        /// the presentation blit something to filter with retro mode off. At coverage 1 and 2 the
        /// frame pass filters the backbuffer instead, so the target is not merely redundant, it is
        /// 15-21 MB of iPad memory bought for nothing - the path is switched off and Camera.main
        /// draws straight to the backbuffer, exactly as it does with the filter off.
        /// </summary>
        public static bool NativeActive(bool enabled, int retroMode, bool materialOk, int coverage)
        {
            return NativeActive(enabled, retroMode, materialOk) && ClampCoverage(coverage) == CoverageWorld;
        }

        /// <summary>
        /// Whether the END-OF-FRAME pass runs: the filter is on, the shader made it into the build,
        /// and coverage is 1 or 2. Deliberately independent of retro mode - with retro mode on the
        /// retro picture is upscaled to the backbuffer plainly first and the finished frame is
        /// filtered once, which is the same single filtering the other two paths give.
        /// </summary>
        public static bool FrameActive(bool enabled, bool materialOk, int coverage)
        {
            return enabled && materialOk && ClampCoverage(coverage) != CoverageWorld;
        }

        /// <summary>
        /// Whether the touch controls must be kept OUT of the filtered frame and redrawn sharp over
        /// it. True only at coverage 1: at 0 the frame pass does not run at all, and at 2 the
        /// player has asked for everything.
        /// </summary>
        public static bool TouchControlsSharp(int coverage)
        {
            return ClampCoverage(coverage) == CoverageFrame;
        }

        /// <summary>
        /// Scanlines the frame pass draws. There is no raster here in any coverage: the source is
        /// the finished BACKBUFFER at panel resolution, and with retro mode on it holds the retro
        /// picture already upscaled and with an IMGUI HUD sitting over it at native resolution -
        /// so the retro mode's 200 or 400 lines would be locked to a raster that only occupies part
        /// of what is being filtered, and would beat against everything else. The count is the
        /// player's CRTScanlineCount, clamped, exactly as on the native path.
        /// </summary>
        public static int FrameScanlineCount(int nativeCount)
        {
            return ClampScanlineCount(nativeCount);
        }

        // ---- the touch canvas, at coverage 1 ----------------------------------------------
        // An overlay canvas is drawn "to the screen" from outside any camera, and it is in the
        // backbuffer before the frame pass ever sees it - so at coverage 1 the controls have to be
        // drawn by a CAMERA instead, one the frame pass renders BY HAND after the filtered frame is
        // on the backbuffer. A screen-space-camera canvas is real geometry a hundred units in front
        // of that camera, and DFU keeps the player near the world origin, so Camera.main would
        // otherwise be staring at a screen-sized quad: the subtree moves to Unity's built-in UI
        // layer and the world camera stops rendering it. `Camera.main.cullingMask &= ~(1 << layer)`
        // is DFU's own idiom for this - Automap, ExteriorAutomap and DaggerfallBankPurchasePopUp
        // all do it for their own preview layers.

        /// <summary>Unity's built-in "UI" layer (5). The touch canvas moves here while coverage 1
        /// is drawing it with its own camera, and that camera renders nothing else.</summary>
        public const int TouchUILayer = 5;

        /// <summary>The touch canvas's layer as a culling-mask bit.</summary>
        public static int TouchUILayerMask
        {
            get { return 1 << TouchUILayer; }
        }

        /// <summary>The mask a world camera must have while coverage 1 runs: anything but the
        /// touch HUD's layer.</summary>
        public static int WithoutTouchUILayer(int cullingMask)
        {
            return cullingMask & ~TouchUILayerMask;
        }

        /// <summary>
        /// Height in rows of the raster the main camera is rendering into this frame, or 0 when
        /// there is no running game or no retro texture yet (the Editor, or before RetroRenderer
        /// has picked a target). RetroRenderer.RetroTexture is 320x200 / 640x400 normally, but
        /// 320x154 / 640x308 when the large HUD is docked - Unity's viewport rect does not work
        /// with target textures, so DFU shortens the raster instead and stretches it into the
        /// fixed 640x400 presentation texture. It is null on the native path, which has no retro
        /// raster at all.
        /// </summary>
        public static int LiveRasterHeight
        {
            get
            {
                if (!GameManager.HasInstance)
                    return 0;

                RetroRenderer renderer = GameManager.Instance.RetroRenderer;
                if (renderer == null)
                    return 0;

                RenderTexture raster = renderer.RetroTexture;
                return raster != null ? raster.height : 0;
            }
        }

        /// <summary>
        /// Scanlines to draw over a raster of the given height.
        ///
        /// RETRO (retroMode != 0): the count of the SOURCE raster, never a device-pixel figure -
        /// lines placed on device pixels beat against the panel's own grid and shimmer as the view
        /// moves, and lines placed on the retro mode's NOMINAL count beat against a shortened
        /// raster (200 lines over the docked large HUD's 154 rows is 46 cycles of moire down the
        /// screen). A raster height of 0 (no live raster) falls back to the mode's nominal count.
        ///
        /// NATIVE (retroMode == 0): there is no raster - the source IS the screen, at whatever
        /// resolution the panel runs - so no count is "correct" and the number becomes the
        /// player's, CRTScanlineCount, clamped. A zero would collapse the shader's scanline phase
        /// to a constant, which is what the clamp's lower bound is really for.
        /// </summary>
        public static int ScanlineCountFor(int rasterHeight, int retroMode, int nativeCount)
        {
            if (retroMode == 0)
                return ClampScanlineCount(nativeCount);

            if (rasterHeight > 0)
                return rasterHeight;

            return retroMode == 2 ? ScanlinesHighRes : ScanlinesLowRes;
        }

        /// <summary>
        /// Scanlines to draw this frame: the live raster's own height in retro mode, the player's
        /// clamped CRTScanlineCount when retro mode is off.
        /// </summary>
        public static int ScanlineCount(int retroMode, int nativeCount)
        {
            return ScanlineCountFor(LiveRasterHeight, retroMode, nativeCount);
        }

        // ---- the native path's viewport maths ---------------------------------------------

        /// <summary>
        /// The normalised viewport the world occupies once a docked large HUD has taken the bottom
        /// of the screen - DFU's own rule (ViewportChanger.Update), repeated here so the native
        /// path's render-target size can be reasoned about and tested without a scene. Both
        /// arguments are in device pixels. A HUD taller than the screen, or a zero screen, yields
        /// the whole screen rather than an empty or inverted rect.
        /// </summary>
        public static Rect DockedViewportRect(float screenHeight, float hudScreenHeight)
        {
            if (screenHeight <= 0f || hudScreenHeight <= 0f || hudScreenHeight >= screenHeight)
                return new Rect(0f, 0f, 1f, 1f);

            float h = hudScreenHeight / screenHeight;
            return new Rect(0f, h, 1f, 1f - h);
        }

        /// <summary>
        /// The viewport rect Camera.main must carry, given who owns its render target this frame.
        ///
        /// A camera that renders into a render texture needs the WHOLE rect: "Camera viewport does
        /// not work with render textures" is DFU's own comment, and both texture paths - retro mode
        /// and this filter's native path - express the docked large HUD by shortening the target
        /// instead. A camera that renders straight to the backbuffer needs the docked rect, because
        /// there the viewport is the only thing keeping the world off the HUD.
        ///
        /// The 2026-09-10 device bug was this rule being broken for one configuration and then never
        /// re-checked: switching retro mode ON while the filter's native path was running let the
        /// path's teardown put the docked rect on a camera that retro mode had just pointed at the
        /// 320x200 retro texture. The world went into the top four fifths of that texture while
        /// Distant Terrain's stacked camera kept filling all of it, and the two pictures no longer
        /// agreed about where the horizon was.
        /// </summary>
        public static Rect MainCameraRect(int retroMode, bool nativeActive, float screenHeight, float hudScreenHeight)
        {
            if (retroMode != 0 || nativeActive)
                return new Rect(0f, 0f, 1f, 1f);

            return DockedViewportRect(screenHeight, hudScreenHeight);
        }

        /// <summary>
        /// The size of the native path's render target, given the presenting camera's own pixel
        /// rect. It is deliberately NOT derived from Screen.height and the HUD height a second
        /// time: the target has to match the rectangle it will be blitted into to the pixel, or
        /// the picture is resampled by a fraction of a pixel and the scanlines crawl. Clamped to
        /// at least 1x1 (a zero-sized RenderTexture throws) and to a sane ceiling, because
        /// Screen.width/height report 0 for a frame or two on an iOS resolution change and can
        /// report the full Retina buffer of a display the app has not yet been resized to.
        /// </summary>
        public static Vector2Int NativeTargetSize(int viewportPixelWidth, int viewportPixelHeight)
        {
            const int max = 8192;
            int w = viewportPixelWidth < 1 ? 1 : (viewportPixelWidth > max ? max : viewportPixelWidth);
            int h = viewportPixelHeight < 1 ? 1 : (viewportPixelHeight > max ? max : viewportPixelHeight);
            return new Vector2Int(w, h);
        }

        /// <summary>
        /// Bytes of memory the native path's render target costs at a given size: the 8-bit RGBA
        /// colour surface, four bytes a pixel, and nothing else. The 32-bit depth surface is
        /// declared RenderTextureMemoryless.Depth, so on iOS/Metal it is a tile-memory attachment
        /// that is never resolved to system memory - see MobileCrtNative.Recreate for why that is
        /// sound here (nothing samples depth, MSAA is off, both cameras clear depth on entry). On a
        /// graphics API with no tile memory the hint is ignored and depth costs another 4 bytes a
        /// pixel, which is not the platform this number is quoted for. Reported in the log when the
        /// target is created, because it is the whole price of the feature: a 12.9" iPad Pro's
        /// 2732x2048 comes to about 21 MB.
        /// </summary>
        public static long NativeTargetBytes(int width, int height)
        {
            return (long)width * height * 4L;
        }
    }
}
