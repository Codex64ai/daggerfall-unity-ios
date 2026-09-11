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
