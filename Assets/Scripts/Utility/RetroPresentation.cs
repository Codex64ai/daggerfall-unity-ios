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
        static Material crtMaterial;
        static bool crtResolved;
        static readonly int crtCurvatureId = Shader.PropertyToID("_Curvature");
        static readonly int crtScanlinesId = Shader.PropertyToID("_Scanlines");
        static readonly int crtScanlineCountId = Shader.PropertyToID("_ScanlineCount");
        static readonly int crtMaskId = Shader.PropertyToID("_Mask");
        static readonly int crtVignetteId = Shader.PropertyToID("_Vignette");

        /// <summary>
        /// MOBILE: the CRT material, built on first use from the shader MobileShaders captured at
        /// startup (Shader.Find alone can hand back a mod bundle's embedded copy of a shader once
        /// bundles have loaded). Resolved once: if the shader is missing - stripped from an iOS
        /// build despite the Always-Included pin - it says so once and the filter stays off.
        /// </summary>
        static Material CrtMaterial
        {
            get
            {
                if (crtResolved)
                    return crtMaterial;

                crtResolved = true;
                Shader shader = MobileShaders.Find(MobileCrt.ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning("[CRT] shader " + MobileCrt.ShaderName + " not found - CRT filter disabled");
                    return null;
                }
                crtMaterial = new Material(shader);
                return crtMaterial;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (RetroPresentationSource && DaggerfallUnity.Settings.RetroRenderingMode != 0)
            {
                // MOBILE: present through the CRT filter when it is on and its material resolved.
                int retroMode = DaggerfallUnity.Settings.RetroRenderingMode;
                bool wanted = DaggerfallUnity.Settings.CRTFilter;
                Material crt = wanted ? CrtMaterial : null;
                if (MobileCrt.Active(wanted, retroMode, crt != null))
                {
                    crt.SetFloat(crtCurvatureId, MobileCrt.ClampCurvature(DaggerfallUnity.Settings.CRTCurvature));
                    crt.SetFloat(crtScanlinesId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTScanlines));
                    crt.SetFloat(crtScanlineCountId, MobileCrt.ScanlineCount(retroMode));
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
