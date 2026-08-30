// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The classic-UI host for Mobile Settings. It draws nothing of its own; its job is to be
//   the top window while the UGUI panel is up. That does three things at once: the game
//   stays paused (any open window pauses), DFU's GUI pass paints only the top window so the
//   pause menu beneath cannot draw over the panel, and Escape / the touch BACK button pop
//   it - which closes the panel and lands the player back on the pause menu.
//
//   previousWindow is deliberately null: DaggerfallPopupWindow.Draw() paints the previous
//   window underneath itself, and IMGUI composites over UGUI, so the pause menu would sit
//   on top of the settings.
//

using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileSettingsWindow : DaggerfallPopupWindow
    {
        public MobileSettingsWindow(IUserInterfaceManager uiManager)
            : base(uiManager, null)
        {
        }

        public bool IsTop
        {
            get { return uiManager != null && uiManager.TopWindow == this; }
        }

        protected override void Setup()
        {
            base.Setup();
            // A dim over the frozen world, so the panel reads as a screen and not a widget.
            ParentPanel.BackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        }

        public override void OnPush()
        {
            base.OnPush();

            if (MobileSettingsPanel.Instance != null)
                MobileSettingsPanel.Instance.OpenFrom(this);
            else
                CloseWindow();   // no panel in this scene; nothing to host
        }

        public override void OnPop()
        {
            base.OnPop();

            if (MobileSettingsPanel.Instance != null)
                MobileSettingsPanel.Instance.OnHostClosed(this);
        }
    }
}
