// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net), Pango
// Contributors:    
// 
// Notes:
//

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace DaggerfallWorkshop.Utility
{
    /// <summary>
    /// Changes camera viewport based on large HUD configuration.
    /// </summary>
    public class ViewportChanger : MonoBehaviour
    {
        public bool isRetroPresenter = false;
        public Camera retroClearerCamera;

        Rect standardViewportRect = new Rect(0, 0, 1, 1);
        Rect lastViewportRect;
        new Camera camera;

        private void Start()
        {
            camera = GetComponent<Camera>();
        }

        private void Update()
        {
            // HUD must be created
            if (DaggerfallUI.Instance.DaggerfallHUD == null)
                return;

            // Offload when using retro aspect correction
            // MOBILE: `&& RetroRenderingMode != 0`. Aspect correction is a RETRO setting - it
            // pillarboxes or stretches a 320x200 raster into a 4:3 or 16:10 shape - and until the
            // CRT filter learned to run with retro mode off, this branch could only ever be reached
            // with retro mode on, because the presenter's GameObject was switched off otherwise. It
            // is reachable now, and squeezing a native 16:9 render into a 4:3 box because a retro
            // setting was left switched on is not what anyone asked for.
            if (isRetroPresenter && DaggerfallUnity.Settings.RetroModeAspectCorrection != 0
                && DaggerfallUnity.Settings.RetroRenderingMode != 0)
            {
                SetRetroAspectViewport();
                return;
            }
            else
            {
                DaggerfallUI.Instance.CustomScreenRect = null;
            }

            // Change viewport when large HUD is docked
            // When not using docked the large HUD is just an overlay of variable size and main viewport does not change
            if (DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDDocked)
            {
                // Shrink viewport to area not occupied by docked large HUD
                // Check size every frame as HUD height can change (e.g. resizing window, changing resolution)
                HUDLarge largeHUD = DaggerfallUI.Instance.DaggerfallHUD.LargeHUD;
                float hudHeight = largeHUD.ScreenHeight / Screen.height;
                Rect rect = new Rect(0, hudHeight, 1, 1 - hudHeight);
                SetViewport(rect);
            }
            else
            {
                // Set standard viewport area
                SetViewport(standardViewportRect);
            }
        }

        void SetViewport(Rect rect)
        {
            if (!camera)
                return;

            // Handle retro rendering mode
            // Camera viewport does not work with render textures so need to adjust output to appropriately size render target instead
            // Then retro presentation needs to use correct screen viewport area, not main camera
            // MOBILE: `|| MobileCrtNative.Active` - the CRT filter's non-retro path puts the
            // main camera on a render target too, so the same rule applies to it: full rect on
            // the camera, and the viewport expressed as the size of the target instead.
            // UpdateRenderTarget is what re-sizes that target, and this is the call that tells
            // it the docked large HUD has changed the viewport.
            bool ontoRenderTarget = (DaggerfallUnity.Settings.RetroRenderingMode != 0 || Game.Mobile.MobileCrtNative.Active) && !isRetroPresenter;
            Rect wanted = ontoRenderTarget ? standardViewportRect : rect;

            // Do nothing if viewport rect hasn't changed
            // MOBILE: ...and the camera is in fact showing the rect this mode calls for. Upstream's
            // early-out remembers the rect it was ASKED for, never the one it applied, so once the
            // docked large HUD's rect had been recorded the camera was never corrected again unless
            // the HUD's height changed. Switching the render target on or off does not change the
            // requested rect at all - it changes which of the two branches below is right - so the
            // camera could be left on a full rect with nothing to render into (the world drawn over
            // the docked HUD when retro mode is switched off), or on a partial rect with a render
            // texture attached, which is the combination the comment above says does not work.
            // Comparing the camera's actual rect makes this self-correcting on any mode change,
            // whoever caused it, and still costs nothing per frame once the two agree.
            if (rect == lastViewportRect && camera.rect == wanted)
                return;

            if (ontoRenderTarget)
            {
                camera.rect = standardViewportRect;
                GameManager.Instance.RetroRenderer.UpdateRenderTarget();
            }
            else
            {
                camera.rect = rect;
            }

            lastViewportRect = rect;
        }

        void SetRetroAspectViewport()
        {
            float heightRatio = 0;
            int viewWidth = 0;
            RetroModeAspects aspect = (RetroModeAspects)DaggerfallUnity.Settings.RetroModeAspectCorrection;
            if (aspect == RetroModeAspects.FourThree)
            {
                // Classic rendered at 320x200 (Mode13h/16:10) but was typically displayed on 4:3 monitors (e.g. 320x240)
                // In this environment display output signal was stretched 20% higher in vertical dimension
                // This setting scales output viewport to simulate resulting aspect ratio in this environment
                // Works from ideal 16:10 > 4:3 upscale (1600x1200 or 5x width, 6x height, 20% higher) and ratios into actual screen area

                // Start with screen height at 6x classic to get a ratio
                heightRatio = Screen.height / 6f / 200f;

                // Then determine 5x classic width at this ratio
                viewWidth = (int)(320f * 5f * heightRatio);
            }
            else if (aspect == RetroModeAspects.SixteenTen)
            {
                // Upscale 320x200 6x in both dimensions to 1920x1200, a very common 16:10 resolution, then ratio into actual screen area

                // Start with screen height at 6x classic to get a ratio
                heightRatio = Screen.height / 6f / 200f;

                // Then determine 6x classic width at this ratio
                viewWidth = (int)(320f * 6f * heightRatio);
            }

            // Get pillarbox width offset to centre viewport horizontally
            int pillarWidth = (Screen.width - viewWidth) / 2;

            // Handle docked large HUD
            float hudHeight = DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDDocked
                                ? DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight / Screen.height
                                : 0;

            // Set final viewport area
            float x = (float)pillarWidth / Screen.width;
            float h = 1 - x * 2;
            Rect rect = new Rect(x, hudHeight, h, 1.0f - hudHeight);

            // Get screen rect and pass over to UI so it can treat this viewport as entire screen space
            Rect adjustedScreenRect = new Rect(pillarWidth, 0, Screen.width - pillarWidth * 2, Screen.height);
            DaggerfallUI.Instance.CustomScreenRect = adjustedScreenRect;

            // After adjusting output viewport pillarbox bars won't be cleared automatically
            // Use camera with -1 depth covering entire viewport to clear black first
            // This camera renders nothing, just clears screen black before custom viewport drawn centred in screen
            if (retroClearerCamera && !retroClearerCamera.gameObject.activeSelf)
                retroClearerCamera.gameObject.SetActive(true);

            SetViewport(rect);
        }
    }
}