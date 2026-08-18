// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Daggerfall Unity already binds gamepad buttons for UI clicks
//   (JoystickButton0=LeftClick, 3=RightClick, 2=Middle, 1=Back) and gamepad axes for
//   movement and camera. What it does NOT do is bind gameplay actions - Jump, Activate,
//   ReadyWeapon and so on default to keyboard keys only. So out of the box a controller
//   can navigate menus but cannot actually play.
//
//   These are applied as SECONDARY bindings, so every keyboard primary is left intact
//   and the player can still rebind everything through DaggerfallJoystickControlsWindow.
//
//   Applied once, the first time a gamepad is seen. A flag in PlayerPrefs stops us
//   stomping the player's own choices on every launch.
//

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileGamepadBindings
    {
        const string appliedKey = "DFMobile.GamepadBindingsApplied";

        /// <summary>
        /// Conventional MFi / Xbox / DualSense face-button order as Unity's legacy Input
        /// reports it. NOTE: the numbering is not perfectly consistent across controllers
        /// and OS versions - this is a sane starting point, not gospel. Players can rebind
        /// in Settings > Controls > Joystick.
        /// </summary>
        static readonly (KeyCode code, InputManager.Actions action, string label)[] bindings =
        {
            (KeyCode.JoystickButton0, InputManager.Actions.ActivateCenterObject, "A      -> Activate"),
            (KeyCode.JoystickButton1, InputManager.Actions.Jump,                 "B      -> Jump"),
            (KeyCode.JoystickButton2, InputManager.Actions.ReadyWeapon,          "X      -> Ready Weapon"),
            (KeyCode.JoystickButton3, InputManager.Actions.CastSpell,            "Y      -> Cast Spell"),
            (KeyCode.JoystickButton4, InputManager.Actions.SwitchHand,           "LB     -> Switch Hand"),
            (KeyCode.JoystickButton5, InputManager.Actions.SwingWeapon,          "RB     -> Attack"),
            (KeyCode.JoystickButton6, InputManager.Actions.Inventory,            "Back   -> Inventory"),
            (KeyCode.JoystickButton7, InputManager.Actions.Escape,               "Start  -> Pause"),
            (KeyCode.JoystickButton8, InputManager.Actions.Crouch,              "LStick -> Crouch"),
            (KeyCode.JoystickButton9, InputManager.Actions.StealMode,            "RStick -> Sneak"),
        };

        public static bool AlreadyApplied
        {
            get { return PlayerPrefs.GetInt(appliedKey, 0) == 1; }
        }

        /// <summary>Apply the defaults unless they have been applied before.</summary>
        public static void ApplyOnce()
        {
            if (AlreadyApplied)
                return;

            Apply();
        }

        /// <summary>Force-apply, overwriting current secondary bindings. Exposed for a settings button.</summary>
        public static void Apply()
        {
            if (!InputManager.HasInstance)
            {
                Debug.LogWarning("[MobileGamepadBindings] InputManager not ready; skipping.");
                return;
            }

            InputManager input = InputManager.Instance;
            var log = new System.Text.StringBuilder();
            log.AppendLine("[MobileGamepadBindings] applied gamepad defaults as SECONDARY bindings:");

            for (int i = 0; i < bindings.Length; i++)
            {
                // primary: false - keyboard bindings are left exactly as they were.
                input.SetBinding(bindings[i].code, bindings[i].action, false);
                log.AppendLine("   " + bindings[i].label);
            }

            input.SaveKeyBinds();

            PlayerPrefs.SetInt(appliedKey, 1);
            PlayerPrefs.Save();

            log.AppendLine("Rebind any of these in Settings > Controls > Joystick.");
            Debug.Log(log.ToString());
        }

        /// <summary>Let the player opt back into having defaults reapplied.</summary>
        public static void ClearAppliedFlag()
        {
            PlayerPrefs.DeleteKey(appliedKey);
            PlayerPrefs.Save();
        }
    }
}
