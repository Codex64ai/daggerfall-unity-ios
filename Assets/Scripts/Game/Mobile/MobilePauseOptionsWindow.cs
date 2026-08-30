// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The pause menu with one addition: a MOBILE SETTINGS button directly under the options
//   box. That is where the port's own settings live now - previously a gear on the touch
//   HUD, which was hidden exactly when a menu was open, and unreachable at all with a mouse,
//   keyboard or pad driving.
//
//   Installed through DFU's own extension point (UIWindowFactory.RegisterCustomUIWindow), so
//   no engine file changes. Registration has to happen once DaggerfallUI exists; the factory
//   then re-creates its persistent pause window instance from this type.
//

using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobilePauseOptionsWindow : DaggerfallPauseOptionsWindow
    {
        static bool registered;

        /// <summary>Idempotent. Called from MobileInputController.Start in the game scene.</summary>
        public static void Register()
        {
            if (registered || !DaggerfallUI.HasInstance)
                return;

            registered = true;
            UIWindowFactory.RegisterCustomUIWindow(UIWindowType.PauseOptions, typeof(MobilePauseOptionsWindow));
            Debug.Log("[MobileInput] pause menu carries Mobile Settings");
        }

        public MobilePauseOptionsWindow(IUserInterfaceManager uiManager, IUserInterfaceWindow previousWindow = null)
            : base(uiManager, previousWindow)
        {
        }

        protected override void Setup()
        {
            base.Setup();

            // The native options box sits at (0, 40) centred, 150 x 77 in the 320 x 200 native
            // space, so its bottom edge is y = 117. The button takes the box's width, just below.
            const float boxWidth = 150f;
            const float x = (320f - boxWidth) * 0.5f;
            Button settings = DaggerfallUI.AddTextButton(new Rect(x, 121f, boxWidth, 14f), "MOBILE SETTINGS", NativePanel);
            settings.BackgroundColor = new Color(0f, 0f, 0f, 0.65f);
            settings.Outline.Color = new Color(0.62f, 0.50f, 0.22f, 1f);
            settings.Label.TextColor = new Color(0.96f, 0.92f, 0.80f, 1f);
            settings.OnMouseClick += SettingsButton_OnMouseClick;
        }

        void SettingsButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            uiManager.PushWindow(new MobileSettingsWindow(uiManager));
        }
    }
}
