// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the in-game page for the CRT presentation filter (Assets/Shaders/Mobile/MobileCRT.shader),
// a fourth Game Effects page sitting next to Retro Mode - which is where a player will look for
// it, because the filter does nothing at all while retro mode is off (RetroPresentation has no
// presenter to hook then, so MobileCrt.Active is false).
//
// This file lives with DFU's other config pages rather than in Assets/Scripts/Game/Mobile/ because
// GameEffectsConfigWindow discovers nothing: a page is a class in this namespace that the window
// constructs by name. It is still ours, hence the port's header.
//
// There is nothing to deploy. RetroPresentation.OnRenderImage reads all five settings every frame
// and clamps them again at the point of use, so a slider moves the picture while it is dragged;
// GameEffectsConfigWindow.OnPop is what writes settings.ini.
//
// The slider ranges ARE the settings loader's clamps (0..MobileCrt.MaxCurvature and three 0..1),
// so the UI cannot ask for a value the loader would refuse on the next launch. DFU's float slider
// indicator carries one decimal digit, which makes curvature a four-step 0.0 / 0.1 / 0.2 / 0.3
// control - coarser than the 0.08 default, so the default is reachable only through the page's own
// "set page defaults" button. That is the widget, not a choice.
//
// Place in Assets/Scripts/Game/UserInterfaceWindows/

using UnityEngine;
using DaggerfallWorkshop.Game.Mobile;
using DaggerfallWorkshop.Game.UserInterface;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class CRTConfigPage : GameEffectConfigPage
    {
        const string key = "crtFilter";

        // Localisation: these keys are ours and are not in Internal_Strings.csv, so every lookup
        // carries the English reversion it should fall back to. GetLocalizedText would return the
        // lookup-error string instead.
        const string titleReversion = "CRT Filter";
        const string tipReversion = "Curved tube, scanlines and phosphor grille over the world. " +
                                    "Needs retro mode on. The HUD stays sharp on purpose, and " +
                                    "curvature crops the edges of the view.";

        Checkbox enableCheckbox;
        HorizontalSlider curvatureSlider;
        HorizontalSlider scanlinesSlider;
        HorizontalSlider maskSlider;
        HorizontalSlider vignetteSlider;

        // ReadSettings drives the sliders, and setting a slider's value raises OnScroll - which
        // would write the rounded slider value straight back over the setting it just read
        // (0.08 curvature becomes 0.1 on the first time the window is opened). The other pages
        // live with that; this one does not.
        bool reading;

        public override string Key => key;

        public override string Title => TextManager.Instance.GetLocalizedTextWithReversion(key, reversion: titleReversion);

        public override void Setup(Panel parent)
        {
            // SetIndicator raises OnScroll as it positions the thumb, so the guard has to be up for
            // the whole of Setup or building the page writes the rounded slider values over the
            // settings it was built from. ReadSettings drops it again at the end.
            reading = true;

            // Taller than the other pages' tip panel, and wrapped: this page has two things to say
            // that a player cannot discover by looking at the picture.
            aboutPanelRect = new Rect(0, 0, 145, 22);
            Panel tipPanel = AddTipPanel(parent, TextManager.Instance.GetLocalizedTextWithReversion(key + "Tip", reversion: tipReversion));
            foreach (BaseScreenComponent component in tipPanel.Components)
            {
                TextLabel label = component as TextLabel;
                if (label != null)
                {
                    label.MaxWidth = (int)aboutPanelRect.width - 4;
                    label.WrapText = true;
                    label.WrapWords = true;
                }
            }

            Vector2 pos = settingsStartPos;
            pos.y += aboutPanelRect.height - 6;     // clear of the taller tip panel

            // Enable toggle
            enableCheckbox = AddCheckbox(parent, TextManager.Instance.GetLocalizedText("enable"), ref pos);
            enableCheckbox.OnToggleState += EnableCheckbox_OnToggleState;

            // Curvature: 0 .. MobileCrt.MaxCurvature, the loader's clamp.
            curvatureSlider = AddSlider(parent, TextManager.Instance.GetLocalizedTextWithReversion(key + "Curvature", reversion: "Curvature"), 3, ref pos);
            curvatureSlider.OnScroll += CurvatureSlider_OnScroll;
            curvatureSlider.SetIndicator(0f, MobileCrt.MaxCurvature, DaggerfallUnity.Settings.CRTCurvature);
            StyleIndicator(curvatureSlider);

            // Scanline depth
            scanlinesSlider = AddSlider(parent, TextManager.Instance.GetLocalizedTextWithReversion(key + "Scanlines", reversion: "Scanlines"), 10, ref pos);
            scanlinesSlider.OnScroll += ScanlinesSlider_OnScroll;
            scanlinesSlider.SetIndicator(0f, 1f, DaggerfallUnity.Settings.CRTScanlines);
            StyleIndicator(scanlinesSlider);

            // Aperture grille depth
            maskSlider = AddSlider(parent, TextManager.Instance.GetLocalizedTextWithReversion(key + "Mask", reversion: "Grille"), 10, ref pos);
            maskSlider.OnScroll += MaskSlider_OnScroll;
            maskSlider.SetIndicator(0f, 1f, DaggerfallUnity.Settings.CRTMask);
            StyleIndicator(maskSlider);

            // Vignette depth
            vignetteSlider = AddSlider(parent, TextManager.Instance.GetLocalizedTextWithReversion(key + "Vignette", reversion: "Vignette"), 10, ref pos);
            vignetteSlider.OnScroll += VignetteSlider_OnScroll;
            vignetteSlider.SetIndicator(0f, 1f, DaggerfallUnity.Settings.CRTVignette);
            StyleIndicator(vignetteSlider);

            ReadSettings();
        }

        public override void ReadSettings()
        {
            reading = true;
            enableCheckbox.IsChecked = DaggerfallUnity.Settings.CRTFilter;
            curvatureSlider.Value = Mathf.RoundToInt(DaggerfallUnity.Settings.CRTCurvature * 10);
            scanlinesSlider.Value = Mathf.RoundToInt(DaggerfallUnity.Settings.CRTScanlines * 10);
            maskSlider.Value = Mathf.RoundToInt(DaggerfallUnity.Settings.CRTMask * 10);
            vignetteSlider.Value = Mathf.RoundToInt(DaggerfallUnity.Settings.CRTVignette * 10);
            reading = false;
        }

        /// <summary>
        /// Nothing to deploy: the filter is not a PPv2 effect and has no CoreGameEffectSettingsGroups
        /// entry. RetroPresentation reads the five settings on every frame it presents, so the
        /// picture already followed the slider.
        /// </summary>
        public override void DeploySettings()
        {
        }

        public override void SetDefaults()
        {
            DaggerfallUnity.Settings.CRTFilter = false;
            DaggerfallUnity.Settings.CRTCurvature = 0.08f;
            DaggerfallUnity.Settings.CRTScanlines = 0.35f;
            DaggerfallUnity.Settings.CRTMask = 0.25f;
            DaggerfallUnity.Settings.CRTVignette = 0.25f;
        }

        private void EnableCheckbox_OnToggleState()
        {
            if (reading)
                return;

            DaggerfallUnity.Settings.CRTFilter = enableCheckbox.IsChecked;
        }

        private void CurvatureSlider_OnScroll()
        {
            if (reading)
                return;

            DaggerfallUnity.Settings.CRTCurvature = MobileCrt.ClampCurvature(curvatureSlider.ScrollIndex / 10f);
        }

        private void ScanlinesSlider_OnScroll()
        {
            if (reading)
                return;

            DaggerfallUnity.Settings.CRTScanlines = MobileCrt.Clamp01(scanlinesSlider.ScrollIndex / 10f);
        }

        private void MaskSlider_OnScroll()
        {
            if (reading)
                return;

            DaggerfallUnity.Settings.CRTMask = MobileCrt.Clamp01(maskSlider.ScrollIndex / 10f);
        }

        private void VignetteSlider_OnScroll()
        {
            if (reading)
                return;

            DaggerfallUnity.Settings.CRTVignette = MobileCrt.Clamp01(vignetteSlider.ScrollIndex / 10f);
        }
    }
}
