// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using DaggerfallWorkshop.Game.Mobile;

namespace DaggerfallWorkshop.Utility
{
    /// <summary>
    /// Presents final retro rendering to viewport.
    /// </summary>
    public class RetroPresentation : MonoBehaviour
    {
        public RenderTexture RetroPresentationSource;

        // MOBILE: the CRT filter. This one blit is the whole insertion point - it runs at native
        // backbuffer resolution (which is what scanlines and a phosphor grille need; the same
        // effect applied to the 640x400 intermediate would alias into mush), it respects
        // camera.rect so aspect correction and the docked large HUD keep working, and it sits
        // downstream of every camera target, so nothing that re-points Camera.main.targetTexture
        // (RetroRenderer.UpdateRenderTarget, Distant Terrain's stacked camera) interacts with it.
        // The IMGUI HUD is drawn after all cameras and so stays unfiltered, deliberately: curving
        // the touch controls away from where fingers land would be worse than a sharp HUD.
        //
        // It runs in BOTH pictures. With retro mode on the source is RetroRenderer's 640x400
        // presentation texture, as upstream intended. With retro mode off MobileCrtNative rebuilds
        // this last stage of the chain at native resolution - it hands Camera.main a viewport-sized
        // render texture, switches this presenter's own GameObject back on and points
        // RetroPresentationSource at that texture - so the only thing this file has to know is that
        // "retro mode is off" is no longer the same statement as "there is nothing to present".
        static readonly int crtCurvatureId = Shader.PropertyToID("_Curvature");
        static readonly int crtScanlinesId = Shader.PropertyToID("_Scanlines");
        static readonly int crtScanlineCountId = Shader.PropertyToID("_ScanlineCount");
        static readonly int crtMaskId = Shader.PropertyToID("_Mask");
        static readonly int crtVignetteId = Shader.PropertyToID("_Vignette");

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // MOBILE: `|| MobileCrtNative.Active` is the whole of the retro-off case. Upstream's
            // condition stays first so that with the filter off this file behaves exactly as it did.
            if (RetroPresentationSource && (DaggerfallUnity.Settings.RetroRenderingMode != 0 || MobileCrtNative.Active))
            {
                // MOBILE: present through the CRT filter when it is on and its material resolved.
                int retroMode = DaggerfallUnity.Settings.RetroRenderingMode;
                bool wanted = DaggerfallUnity.Settings.CRTFilter;
                Material crt = wanted ? MobileCrt.Material : null;
                if (crt != null && (MobileCrt.Active(wanted, retroMode, true) || MobileCrtNative.Active))
                {
                    crt.SetFloat(crtCurvatureId, MobileCrt.ClampCurvature(DaggerfallUnity.Settings.CRTCurvature));
                    crt.SetFloat(crtScanlinesId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTScanlines));
                    crt.SetFloat(crtScanlineCountId, MobileCrt.ScanlineCount(retroMode, DaggerfallUnity.Settings.CRTScanlineCount));
                    crt.SetFloat(crtMaskId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTMask));
                    crt.SetFloat(crtVignetteId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTVignette));
                    Graphics.Blit(RetroPresentationSource, null as RenderTexture, crt);
                    return;
                }

                // Present retro render
                Graphics.Blit(RetroPresentationSource, null as RenderTexture);
            }
        }
    }
}
