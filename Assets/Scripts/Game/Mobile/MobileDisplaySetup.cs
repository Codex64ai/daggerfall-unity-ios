// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Stops Daggerfall Unity forcing a desktop resolution on a mobile display.
//
//   SceneControl.Start() unconditionally does:
//       Screen.SetResolution(Settings.ResolutionWidth, Settings.ResolutionHeight, Fullscreen)
//   with defaults of 1920x1080. On an iPad that requests a 16:9 backbuffer on a ~4:3
//   panel, so the image is letterboxed, and it renders well below the device's native
//   pixel count and is then upscaled - which reads as "the game is too pixelated".
//
//   Rather than patch the engine, this rewrites the setting BEFORE the first scene loads,
//   so SceneControl's own call becomes a request for the native resolution.
//

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileDisplaySetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            ApplyNativeResolution();
#endif
        }

        /// <summary>
        /// Point the stored resolution at the panel's real pixel dimensions.
        /// Screen.currentResolution is the display; Screen.width/height are the current
        /// backbuffer. Prefer the former, fall back to the latter if it looks bogus.
        /// </summary>
        public static void ApplyNativeResolution()
        {
            int w = Screen.currentResolution.width;
            int h = Screen.currentResolution.height;

            if (w < 480 || h < 320)
            {
                w = Screen.width;
                h = Screen.height;
            }

            if (w < 480 || h < 320)
            {
                Debug.LogWarning("[MobileDisplaySetup] could not determine a sane native resolution; leaving settings alone");
                return;
            }

            SettingsManager settings = DaggerfallUnity.Settings;

            int oldW = settings.ResolutionWidth;
            int oldH = settings.ResolutionHeight;

            settings.ResolutionWidth = w;
            settings.ResolutionHeight = h;
            settings.Fullscreen = true;

            // Exclusive fullscreen is a desktop concept and takes a different SetResolution
            // overload; on mobile it must stay off.
            settings.ExclusiveFullscreen = false;

            Debug.Log(string.Format(
                "[MobileDisplaySetup] resolution {0}x{1} -> native {2}x{3} (dpi {4:0})",
                oldW, oldH, w, h, Screen.dpi));
        }
    }
}
