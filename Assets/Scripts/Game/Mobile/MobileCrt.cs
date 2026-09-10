// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the CRT presentation filter's decisions, kept out of RetroPresentation.cs so that
// upstream file carries the smallest patch it can, and so the rules are testable headlessly -
// OnRenderImage is a MonoBehaviour callback no editor test can invoke.
//
// Place in Assets/Scripts/Game/Mobile/

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
        /// Scanlines to draw for a retro rendering mode. This is the count of the SOURCE raster,
        /// never a device-pixel figure: lines placed on device pixels beat against the panel's
        /// own grid and shimmer as the view moves. Mode 0 never reaches the shader (Active is
        /// false), but it still answers with a usable raster rather than zero, which would
        /// collapse the shader's scanline phase to a constant.
        /// </summary>
        public static int ScanlineCount(int retroMode)
        {
            return retroMode == 2 ? ScanlinesHighRes : ScanlinesLowRes;
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
