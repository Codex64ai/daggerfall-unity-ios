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
//   Approach: while a classic window is open, find every live writable TextBox and
//   subscribe to its click event. The keyboard opens when the player TAPS a field, seeded
//   with its current text, and keyboard.text is mirrored into TextBox.Text each frame.
//   TextBox.Text's setter maintains its own cursor position, so the classic UI stays
//   consistent.
//
//   AUTO-OPEN ONCE PER WINDOW. Three designs have now been tried against a real device:
//
//   v1 opened the keyboard whenever any writable TextBox existed and rescanned on a timer,
//   so dismissing it did not stick - the next scan reopened it over the player's choice.
//
//   v2 was click-driven: open only when the player taps the text field. MEASURED
//   IMPOSSIBLE: a classic TextBox sizes itself to its text, so an EMPTY box has a
//   zero-width hit rect - Size=(0, 7) on an empty box, and TextBox.WidthOverride only
//   affects popup layout, not the box's own rectangle. There is nothing to tap; the tap
//   falls through to whatever is behind (on the travel-map find popup, that cancels the
//   prompt entirely). Both text entry paths in the game were broken by this.
//
//   v3 (this): when a window with a writable TextBox comes to the top, open the keyboard
//   ONCE for that window instance. Dismissal sticks because reopening is keyed to window
//   identity, not to a timer - the same instance never auto-opens twice. On
//   DaggerfallInputMessageBox (a window that exists only to take text) a tap anywhere on
//   the popup reopens the keyboard after a dismissal; other windows do not reopen until
//   they are closed and shown again.
//

using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileKeyboard
    {
        static TouchScreenKeyboard keyboard;
        static TextBox target;
        static IUserInterfaceWindow targetWindow;
        static float nextScan;

        // The window instance whose keyboard the player dismissed. Auto-open is suppressed
        // for it until it stops being the top window; keying on identity rather than a timer
        // is what makes a dismissal actually stick.
        static IUserInterfaceWindow dismissedWindow;

        // Panel hooked for tap-to-reopen on input popups, so the handler can be detached
        // when the popup goes away.
        static Panel reopenPanel;
        static IUserInterfaceWindow reopenWindow;

        const float scanInterval = 0.25f;    // tree walk 4x/s, not per frame

        /// <summary>Called each frame from MobileInputController while a menu is open.</summary>
        public static void Poll()
        {
            if (!TouchScreenKeyboard.isSupported)
                return;

            // Re-validate the target cheaply every frame; rescan the tree on a timer.
            if (target != null && !TargetStillLive())
                Detach();

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + scanInterval;
                TryAutoOpen();
            }

            if (target == null || keyboard == null)
                return;

            if (keyboard.status == TouchScreenKeyboard.Status.Done ||
                keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                // Done means the player pressed the keyboard's return key - that is a
                // CONFIRMATION, not just a dismissal, and it has to be forwarded or the
                // window never acts on the text. Canceled is a plain dismissal.
                bool accepted = keyboard.status == TouchScreenKeyboard.Status.Done;
                TextBox finished = target;
                IUserInterfaceWindow window = targetWindow;

                // Either way the player is done with this window's keyboard; identity-keyed,
                // so it stays away for exactly as long as this window instance stays up.
                dismissedWindow = window;

                // Mirror the final text BEFORE letting go: the status flips on the same frame
                // as the last keystroke, and the old early-return here dropped it.
                if (accepted)
                    ApplyText(finished, keyboard.text);

                keyboard = null;
                target = null;
                targetWindow = null;

                if (accepted)
                    Confirm(window, finished);

                return;
            }

            ApplyText(target, keyboard.text);
        }

        /// <summary>Push the soft keyboard's text into the classic TextBox, clamped.</summary>
        static void ApplyText(TextBox textBox, string text)
        {
            if (textBox == null)
                return;

            string typed = text ?? string.Empty;

            int max = textBox.MaxCharacters;
            if (max > 0 && typed.Length > max)
            {
                typed = typed.Substring(0, max);
                if (keyboard != null)
                    keyboard.text = typed;
            }

            if (textBox.Text != typed)
                textBox.Text = typed;
        }

        /// <summary>
        /// Tell the window the input was accepted.
        ///
        /// Needed because DaggerfallInputMessageBox confirms on raw
        /// Input.GetKeyDown(KeyCode.Return), and the iOS soft keyboard produces no such
        /// event - so typing a location into the travel map's find box and pressing return
        /// closed the keyboard and did nothing at all. Device-reported by Ikram.
        ///
        /// Only DaggerfallInputMessageBox is handled: it is the window used for every
        /// "type something and press return" prompt (travel map find, and others), and it
        /// exposes a public accept method. Windows that confirm through their own OK button
        /// are left alone - the player taps that button as usual.
        /// </summary>
        static void Confirm(IUserInterfaceWindow window, TextBox textBox)
        {
            if (textBox == null)
                return;

            DaggerfallInputMessageBox inputBox = window as DaggerfallInputMessageBox;
            if (inputBox == null)
                return;

            inputBox.textBox_OnAcceptUserInputHandler(textBox, textBox.Text);
        }

        /// <summary>Close the keyboard, e.g. when the menu closes underneath it.</summary>
        public static void Dismiss()
        {
            if (keyboard != null)
                keyboard.active = false;
            keyboard = null;
            target = null;
            targetWindow = null;
            dismissedWindow = null;
            UnhookReopenPanel();
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

        /// <summary>
        /// Open the keyboard for the top window's writable text box, once per window
        /// instance. Runs on the scan timer, so a box that appears a few frames after the
        /// window (or becomes writable later) is still picked up.
        /// </summary>
        static void TryAutoOpen()
        {
            IUserInterfaceWindow top = TopWindow();

            // Window went away: drop per-window state so the NEXT window starts fresh.
            if (reopenWindow != null && top != reopenWindow)
                UnhookReopenPanel();
            if (dismissedWindow != null && top != dismissedWindow)
                dismissedWindow = null;

            if (top == null || target != null || top == dismissedWindow)
                return;

            TextBox box = FindActiveTextBox(top);
            if (box == null)
                return;

            target = box;
            targetWindow = top;
            OpenFor(box);

            // A DaggerfallInputMessageBox exists only to take a line of text, so after a
            // dismissal a tap anywhere on it brings the keyboard back. Panel-level on
            // purpose: the box itself has no tappable area (zero-width when empty).
            if (top is DaggerfallInputMessageBox && top.ParentPanel != null)
            {
                reopenPanel = top.ParentPanel;
                reopenWindow = top;
                reopenPanel.OnMouseClick += ReopenPanelClicked;
            }
        }

        static void ReopenPanelClicked(BaseScreenComponent sender, Vector2 position)
        {
            if (target != null || TopWindow() != reopenWindow)
                return;

            TextBox box = FindActiveTextBox(reopenWindow);
            if (box == null)
                return;

            dismissedWindow = null;
            target = box;
            targetWindow = reopenWindow;
            OpenFor(box);
        }

        static void UnhookReopenPanel()
        {
            if (reopenPanel != null)
                reopenPanel.OnMouseClick -= ReopenPanelClicked;
            reopenPanel = null;
            reopenWindow = null;
        }

        static TextBox FindActiveTextBox(IUserInterfaceWindow window)
        {
            if (window == null)
                return null;

            // Focused box wins (rename dialogs etc.)
            TextBox focused = window.FocusControl as TextBox;
            if (focused != null && IsWritable(focused))
                return focused;

            // Then focus-less boxes such as the character name entry and the travel map's
            // find prompt.
            return FindTextBox(window.ParentPanel);
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

    }
}
