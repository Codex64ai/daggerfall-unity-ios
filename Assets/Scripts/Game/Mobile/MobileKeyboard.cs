// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Bridges iOS's on-screen keyboard into Daggerfall's classic TextBox controls.
//
//   TextBox pulls typed characters from DaggerfallUI.Instance.LastCharacterTyped, which
//   ultimately comes from hardware key events - and on iOS the soft keyboard produces
//   NO such events unless a TouchScreenKeyboard is explicitly opened, and even then its
//   text arrives via keyboard.text, not the event stream. So without this bridge the
//   player-name entry (and every other text field) is simply untypeable on device.
//
//   Approach: while a classic window is open, watch for a live writable TextBox - the
//   window's FocusControl first, then a tree walk for focus-less boxes like the name
//   entry dialog. Open the iOS keyboard seeded with its current text and mirror
//   keyboard.text into TextBox.Text each frame. TextBox.Text's setter maintains its own
//   cursor position, so the classic UI stays consistent.
//

using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileKeyboard
    {
        static TouchScreenKeyboard keyboard;
        static TextBox target;
        static IUserInterfaceWindow targetWindow;
        static float nextScan;

        const float scanInterval = 0.25f;    // tree walk 4x/s, not per frame

        /// <summary>Called each frame from MobileInputController while a menu is open.</summary>
        public static void Poll()
        {
            if (!TouchScreenKeyboard.isSupported)
                return;

            // Re-validate the target cheaply every frame; rescan the tree on a timer.
            if (target != null && !TargetStillLive())
                Detach();

            if (target == null && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + scanInterval;
                target = FindActiveTextBox();
                if (target != null)
                {
                    targetWindow = TopWindow();
                    OpenFor(target);
                }
            }

            if (target == null || keyboard == null)
                return;

            if (keyboard.status == TouchScreenKeyboard.Status.Done ||
                keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                // Player dismissed the keyboard; leave the text as-is and stop tracking
                // this box so we do not instantly reopen over their choice.
                keyboard = null;
                target = null;
                targetWindow = null;
                nextScan = Time.unscaledTime + 1.5f;
                return;
            }

            string typed = keyboard.text ?? string.Empty;

            int max = target.MaxCharacters;
            if (max > 0 && typed.Length > max)
            {
                typed = typed.Substring(0, max);
                keyboard.text = typed;
            }

            if (target.Text != typed)
                target.Text = typed;
        }

        /// <summary>Close the keyboard, e.g. when the menu closes underneath it.</summary>
        public static void Dismiss()
        {
            if (keyboard != null)
                keyboard.active = false;
            keyboard = null;
            target = null;
            targetWindow = null;
        }

        static void OpenFor(TextBox textBox)
        {
            // The game draws its own text box; iOS's floating input bar would duplicate it.
            TouchScreenKeyboard.hideInput = true;

            keyboard = TouchScreenKeyboard.Open(
                textBox.Text ?? string.Empty,
                TouchScreenKeyboardType.Default,
                false,   // autocorrect: fantasy names are exactly what autocorrect ruins
                false,   // multiline
                false,   // secure
                false);  // alert
        }

        static void Detach()
        {
            if (keyboard != null)
                keyboard.active = false;
            keyboard = null;
            target = null;
            targetWindow = null;
        }

        static bool TargetStillLive()
        {
            // O(1): same window still on top and the box still writable. A full tree walk
            // here would run every frame while typing - the exact cost the scan timer
            // exists to avoid. If the window changed, the target died with it.
            return TopWindow() == targetWindow && IsWritable(target);
        }

        static TextBox FindActiveTextBox()
        {
            IUserInterfaceWindow top = TopWindow();
            if (top == null)
                return null;

            // Focused box wins (rename dialogs etc.)
            TextBox focused = top.FocusControl as TextBox;
            if (focused != null && IsWritable(focused))
                return focused;

            // Then focus-less boxes such as the character name entry.
            return FindTextBox(top.ParentPanel);
        }

        static IUserInterfaceWindow TopWindow()
        {
            if (!DaggerfallUI.HasInstance || DaggerfallUI.UIManager == null)
                return null;
            return DaggerfallUI.UIManager.TopWindow;
        }

        static bool IsWritable(TextBox textBox)
        {
            return textBox != null && !textBox.ReadOnly && textBox.Enabled &&
                   (!textBox.UseFocus || textBox.HasFocus());
        }

        static TextBox FindTextBox(Panel panel)
        {
            if (panel == null || !panel.Enabled)
                return null;

            ScreenComponentCollection components = panel.Components;
            for (int i = 0; i < components.Count; i++)
            {
                TextBox textBox = components[i] as TextBox;
                if (textBox != null && IsWritable(textBox))
                    return textBox;

                Panel child = components[i] as Panel;
                if (child != null)
                {
                    TextBox found = FindTextBox(child);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}
