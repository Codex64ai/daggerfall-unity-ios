// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the CRT presentation filter's decisions, kept out of RetroPresentation.cs so that
// upstream file carries the smallest patch it can, and so the rules are testable headlessly -
// OnRenderImage is a MonoBehaviour callback no editor test can invoke.
//
// Everything here is a pure function of its arguments except LiveRasterHeight, which is the one
// thing that has to ask the running game a question (which render texture is the main camera
// pointed at this frame). ScanlineCountFor is the pure half of that, so the rule can be tested
// without a game.
//
// Place in Assets/Scripts/Game/Mobile/

using UnityEngine;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Pure rules for the retro-mode CRT filter (Assets/Shaders/Mobile/MobileCRT.shader).
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

        /// <summary>
        /// Height in rows of the raster the main camera is rendering into this frame, or 0 when
        /// there is no running game or no retro texture yet (the Editor, or before RetroRenderer
        /// has picked a target). RetroRenderer.RetroTexture is 320x200 / 640x400 normally, but
        /// 320x154 / 640x308 when the large HUD is docked - Unity's viewport rect does not work
        /// with target textures, so DFU shortens the raster instead and stretches it into the
        /// fixed 640x400 presentation texture.
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
        /// Scanlines to draw over a raster of the given height. This is the count of the SOURCE
        /// raster, never a device-pixel figure: lines placed on device pixels beat against the
        /// panel's own grid and shimmer as the view moves, and lines placed on the retro mode's
        /// NOMINAL count beat against a shortened raster - 200 lines over the docked large HUD's
        /// 154 rows is 46 cycles of moire down the screen. A raster height of 0 (no live raster)
        /// falls back to the mode's nominal count; mode 0 never reaches the shader (Active is
        /// false there), but it still answers with a usable raster rather than zero, which would
        /// collapse the shader's scanline phase to a constant.
        /// </summary>
        public static int ScanlineCountFor(int rasterHeight, int retroMode)
        {
            if (rasterHeight > 0)
                return rasterHeight;

            return retroMode == 2 ? ScanlinesHighRes : ScanlinesLowRes;
        }

        /// <summary>
        /// Scanlines to draw this frame: the live raster's own height when the game is running,
        /// otherwise the retro mode's nominal count.
        /// </summary>
        public static int ScanlineCount(int retroMode)
        {
            return ScanlineCountFor(LiveRasterHeight, retroMode);
        }

        /// <summary>
        /// Whether the CRT filter runs this frame. Retro mode 0 has no presenter at all
        /// (RetroPresentation.OnRenderImage early-outs and Camera.main renders straight to the
        /// backbuffer), so the filter is retro-only by construction; a missing material - a
        /// stripped shader on device - leaves the plain blit in place rather than a black screen.
        /// </summary>
        public static bool Active(bool enabled, int retroMode, bool materialOk)
        {
            return enabled && retroMode != 0 && materialOk;
        }
    }
}
