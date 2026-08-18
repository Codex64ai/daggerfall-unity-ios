// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// C# wrapper over Assets/Plugins/iOS/DFMobileHaptics.mm
//
// Everything degrades to a no-op off-device and on iPad (no Taptic Engine), so callers
// never need to guard. Supported is cached because the native call crosses the managed
// boundary and the answer cannot change at runtime.
//

using System.Runtime.InteropServices;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public enum HapticStyle
    {
        None = -1,
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Selection = 3,
    }

    public static class MobileHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool DFMobileHapticsSupported();
        [DllImport("__Internal")] static extern void DFMobileHapticsPrepare();
        [DllImport("__Internal")] static extern void DFMobileHapticsImpact(int style);
        [DllImport("__Internal")] static extern void DFMobileHapticsSelection();
#endif

        static int supportedCache = -1;   // -1 unknown, 0 no, 1 yes

        /// <summary>False in the editor, on desktop, and on every iPad.</summary>
        public static bool Supported
        {
            get
            {
                if (supportedCache >= 0)
                    return supportedCache == 1;

#if UNITY_IOS && !UNITY_EDITOR
                bool ok;
                try { ok = DFMobileHapticsSupported(); }
                catch (System.Exception) { ok = false; }   // plugin missing from the build
#else
                bool ok = false;
#endif
                supportedCache = ok ? 1 : 0;
                return ok;
            }
        }

        /// <summary>Warm the generators so the first tap is not late.</summary>
        public static void Prepare()
        {
            if (!Supported)
                return;
#if UNITY_IOS && !UNITY_EDITOR
            try { DFMobileHapticsPrepare(); } catch (System.Exception) { supportedCache = 0; }
#endif
        }

        public static void Play(HapticStyle style)
        {
            if (style == HapticStyle.None || !Supported)
                return;

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (style == HapticStyle.Selection)
                    DFMobileHapticsSelection();
                else
                    DFMobileHapticsImpact((int)style);
            }
            catch (System.Exception)
            {
                supportedCache = 0;
            }
#endif
        }
    }
}
