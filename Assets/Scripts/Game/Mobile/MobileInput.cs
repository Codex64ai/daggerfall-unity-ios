// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Static facade that the engine talks to. InputManager pulls from here;
//   MobileInputController and the touch widgets push into here.
//
//   Kept static and MonoBehaviour-free so the InputManager patch never depends on
//   scene state or component lifetime. With Enabled == false every accessor is
//   inert and the engine behaves exactly as stock Daggerfall Unity.
//

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public enum MobileControlMode
    {
        /// <summary>Joystick moves, drag looks.</summary>
        Gameplay,
        /// <summary>Joystick moves, drag is tracked as a Daggerfall weapon gesture.</summary>
        Combat,
        /// <summary>Classic UI is open; touches drive the virtual mouse cursor.</summary>
        Menu,
    }

    /// <summary>
    /// The player's declared way of playing. Auto is the behaviour the port shipped with:
    /// detect a pad, keyboard or pointer and stand touch down while it is used. The other
    /// three are overrides for when detection gets it wrong - a keyboard case that reports
    /// as a joystick, a trackpad the player never touches, a pad they want to keep using
    /// while a mouse is plugged in. Persisted by MobileInputController.
    /// </summary>
    public enum MobileInputMode
    {
        Auto = 0,
        Touch = 1,
        KeyboardMouse = 2,
        Controller = 3,
    }

    /// <summary>What the input layer should act on, after the mode has had its say.</summary>
    public struct EffectiveInput
    {
        /// <summary>Touch HUD shown and the touch pumps drive the game.</summary>
        public bool TouchHud;
        public bool Controller;
        public bool Keyboard;
        public bool Mouse;
    }

    public static class MobileInput
    {
        #region State

#if UNITY_IOS || UNITY_ANDROID
        public static bool Enabled = true;
#else
        // Set true to exercise the overlay with a mouse in the editor.
        public static bool Enabled = false;
#endif

        public static MobileControlMode Mode = MobileControlMode.Gameplay;

        public static bool MenuMode { get { return Enabled && Mode == MobileControlMode.Menu; } }
        public static bool CombatMode { get { return Enabled && Mode == MobileControlMode.Combat; } }

        const int buttonCount = 3;

        // Virtual cursor, bottom-left origin to match Input.mousePosition.
        static Vector2 cursorPosition;

        static readonly bool[] held = new bool[buttonCount];
        static readonly bool[] heldPrevious = new bool[buttonCount];
        static readonly bool[] latched = new bool[buttonCount];
        static readonly int[] pressFrames = new int[buttonCount];

        // The classic UI closes windows through InputManager.GetBackButton*(), which
        // reads raw Input.GetKey(KeyCode.Escape) - injecting Actions.Escape does NOT
        // close a window. So the back button needs its own channel.
        static bool backHeld;
        static bool backHeldPrevious;
        static int backPressFrames;

        static float pendingScroll;

        /// <summary>
        /// Set by MobileInputController; InputManager reads it to decide whether to divert
        /// the mouse. Forced false while a gamepad is active so Daggerfall's own controller
        /// cursor (InputManager.UsingController) keeps the pointer - otherwise the touch
        /// layer would hijack it away from the gamepad. A hardware keyboard alone also
        /// stands it down.
        ///
        /// A real mouse/trackpad does NOT stand it down - it drives it. Unity's iOS player
        /// has no mouse support of its own (pointer clicks arrive as touches, and
        /// Input.GetMouseButton(0) reads as permanently held with a trackpad attached), so
        /// the classic UI must never fall back to the stock Input path while a pointer is in
        /// use. MobileInputController feeds the cursor from the plugin's hover position and
        /// real button state instead. The pointer wins over the keyboard because a Magic
        /// Keyboard is both at once.
        /// </summary>
        public static bool VirtualCursorActive
        {
            get { return virtualCursorActive && !ControllerActive && (MouseActive || !KeyboardActive); }
            set { virtualCursorActive = value; }
        }
        static bool virtualCursorActive;

        /// <summary>
        /// True when a gamepad OR a physical keyboard is driving the game. Touch stands
        /// down completely: HUD hidden, pointer handed back to the engine. Touching the
        /// screen again clears it.
        /// </summary>
        public static bool PhysicalInputActive
        {
            get { return ControllerActive || KeyboardActive || MouseActive; }
        }

        /// <summary>
        /// The single decision table for "who is driving". Pure, so the self-test can walk it.
        ///
        /// Auto            - detection rules; touch stands down while anything physical is used.
        /// Touch           - touch always; detection is ignored entirely. A phantom joystick
        ///                   or an idle trackpad can no longer hide the HUD or steal the pumps.
        /// KeyboardMouse   - touch HUD off; a connected pointer drives look and cursor;
        ///                   the keyboard counts as in use even between keystrokes (so a
        ///                   stray touch does not flip the HUD back on).
        /// Controller      - touch HUD off; the pad path is on whether or not Unity lists one.
        /// </summary>
        public static EffectiveInput ResolveInput(MobileInputMode mode,
            bool controllerDetected, bool keyboardDetected, bool mouseDetected)
        {
            EffectiveInput e = new EffectiveInput();
            switch (mode)
            {
                case MobileInputMode.Touch:
                    e.TouchHud = true;
                    break;

                case MobileInputMode.KeyboardMouse:
                    e.Keyboard = true;
                    e.Mouse = mouseDetected;
                    break;

                case MobileInputMode.Controller:
                    e.Controller = true;
                    break;

                default:
                    e.Controller = controllerDetected;
                    e.Keyboard = keyboardDetected;
                    e.Mouse = mouseDetected;
                    e.TouchHud = !(e.Controller || e.Keyboard || e.Mouse);
                    break;
            }
            return e;
        }

        /// <summary>
        /// Which WeaponSwingMode the engine should see. Mode 0 is hold-and-drag (the drag is
        /// the strike direction); mode 1 is click-to-attack (random direction on press).
        ///
        /// Touch swipes need 0, so touch imposes 0 - unless the player turned on tap-to-attack,
        /// in which case a tap is a click and touch runs in 1. A pointer or pad gets 1 when
        /// click-to-attack is on (the port's own setting, default on: right button or pad
        /// button attacks on press, no drag), otherwise whatever the launcher says.
        ///
        /// Never while a classic window is open: settings.ini is only ever written from
        /// windows (pause menu, controls screens), and the value on disk must be the player's,
        /// not our override. Nothing swings with a window open anyway.
        /// </summary>
        public static int ResolveSwingMode(int userMode, bool touchDrivesGameplay, bool menuOpen,
                                           bool clickToAttack, bool tapToAttack)
        {
            if (menuOpen)
                return userMode;
            if (touchDrivesGameplay)
                return tapToAttack ? 1 : 0;
            return clickToAttack ? 1 : userMode;
        }

        /// <summary>Launcher-following form: touch swipes, everyone else keeps their own mode.</summary>
        public static int ResolveSwingMode(int userMode, bool touchDrivesGameplay, bool menuOpen)
        {
            if (touchDrivesGameplay && !menuOpen)
                return 0;
            return userMode;
        }

        /// <summary>
        /// True while a physical mouse or trackpad is being used. With the pointer plugin
        /// this means a GCMouse is attached and has moved or clicked; without it (iOS 13,
        /// plugin absent) it falls back to movement on the raw mouse axes. Never Unity
        /// button state - iPadOS reports a phantom Mouse0 permanently held, which is exactly
        /// the trap that broke door activation once already. A finger on the screen clears
        /// it, the same as the keyboard; a pointer click does not count as a finger.
        /// </summary>
        public static bool MouseActive { get; set; }

        /// <summary>Set by MobileInputController when a hardware key is pressed.</summary>
        public static bool KeyboardActive { get; set; }

        /// <summary>
        /// True while a physical gamepad is connected. Touch input stands down entirely:
        /// the HUD hides and the pointer reverts to DFU's controller cursor.
        /// </summary>
        public static bool ControllerActive { get; set; }

        /// <summary>
        /// True when the classic UI must take Daggerfall Unity's corrected Metal draw
        /// path. Upstream diagnosed washed-out UI ("improper colour space adjustment...
        /// they appear bright") and fixed it - but gated the fix on MacOSX, when the
        /// actual trigger is the METAL graphics API. iOS is Metal too, so without this
        /// the intro videos, FPS weapon sprite and classic fonts all render washed out
        /// on device while the 3D world looks correct.
        /// </summary>
        public static bool UseMetalUIPath
        {
            get
            {
                if (metalUIPath < 0)
                {
                    metalUIPath =
                        (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX ||
                         SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
                        ? 1 : 0;
                }
                return metalUIPath == 1;
            }
        }
        static int metalUIPath = -1;

        /// <summary>Physical screen DPI, with a sane fallback - Screen.dpi returns 0 on some devices.</summary>
        public static float FallbackDpi = 264f;

        public static float Dpi
        {
            get { return (Screen.dpi > 1f) ? Screen.dpi : FallbackDpi; }
        }

        /// <summary>Convert inches to pixels on this device. The basis for size-independent thresholds.</summary>
        public static float InchesToPixels(float inches)
        {
            return inches * Dpi;
        }

        /// <summary>Cursor texture drawn by InputManager.OnGUI. Assigned by MobileInputController.</summary>
        public static Texture2D CursorTexture { get; set; }
        public static int CursorWidth { get; set; }
        public static int CursorHeight { get; set; }

        #endregion

        #region Cursor

        public static Vector2 CursorPosition { get { return cursorPosition; } }

        public static void SetCursorPosition(Vector2 screenPos)
        {
            cursorPosition = new Vector2(
                Mathf.Clamp(screenPos.x, 0f, Screen.width),
                Mathf.Clamp(screenPos.y, 0f, Screen.height));
        }

        public static void CentreCursor()
        {
            cursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        /// <summary>Rect in GUI space (top-left origin) for drawing the cursor.</summary>
        public static Rect CursorRect
        {
            get
            {
                int w = CursorWidth > 0 ? CursorWidth : 32;
                int h = CursorHeight > 0 ? CursorHeight : 32;
                return new Rect(cursorPosition.x, Screen.height - cursorPosition.y, w, h);
            }
        }

        #endregion

        #region Frame Tick

        /// <summary>
        /// Advance one frame of virtual button state. Called once per frame from
        /// InputManager.Update() BEFORE its paused early-return, so clicks keep
        /// working while the game is paused behind an open menu.
        ///
        /// Down/Up are derived from a held/heldPrevious transition rather than a frame
        /// stamp, so they stay correct regardless of whether a consumer reads before or
        /// after this runs - each edge is visible for exactly one frame either way.
        /// </summary>
        public static void TickButtons()
        {
            for (int i = 0; i < buttonCount; i++)
            {
                heldPrevious[i] = held[i];

                if (pressFrames[i] > 0)
                {
                    held[i] = true;
                    pressFrames[i]--;
                }
                else
                {
                    held[i] = latched[i];
                }
            }

            backHeldPrevious = backHeld;
            if (backPressFrames > 0)
            {
                backHeld = true;
                backPressFrames--;
            }
            else
            {
                backHeld = false;
            }

            // Publish at most one scroll step per frame.
            if (pendingScroll > 0f)
            {
                MouseScroll = 1f;
                pendingScroll = Mathf.Max(pendingScroll - 1f, 0f);
            }
            else if (pendingScroll < 0f)
            {
                MouseScroll = -1f;
                pendingScroll = Mathf.Min(pendingScroll + 1f, 0f);
            }
            else
            {
                MouseScroll = 0f;
            }
        }

        #endregion

        #region Mouse Buttons

        public static bool GetMouseButton(int button)
        {
            return Valid(button) && held[button];
        }

        public static bool GetMouseButtonDown(int button)
        {
            return Valid(button) && held[button] && !heldPrevious[button];
        }

        public static bool GetMouseButtonUp(int button)
        {
            return Valid(button) && !held[button] && heldPrevious[button];
        }

        /// <summary>
        /// Queue a synthetic click. Held for several frames because Daggerfall's
        /// BaseScreenComponent only completes a click on mouse-UP after a mouse-DOWN
        /// inside the same component, and those must land on different frames.
        /// </summary>
        public static void QueueClick(int button = 0, int frames = 3)
        {
            if (Valid(button))
                pressFrames[button] = Mathf.Max(frames, 2);
        }

        /// <summary>Latch a button down (long-press drag on scrollbars and sliders).</summary>
        public static void SetLatched(int button, bool value)
        {
            if (Valid(button))
                latched[button] = value;
        }

        public static bool IsLatched(int button)
        {
            return Valid(button) && latched[button];
        }

        static bool Valid(int button)
        {
            return button >= 0 && button < buttonCount;
        }

        #endregion

        #region UI Back Button

        /// <summary>
        /// Press the UI back button. Most windows test GetBackButtonUp(), so this must
        /// produce a down edge and an up edge on different frames.
        /// </summary>
        public static void QueueBack(int frames = 3)
        {
            backPressFrames = Mathf.Max(frames, 2);
        }

        public static bool GetBackButton() { return backHeld; }
        public static bool GetBackButtonDown() { return backHeld && !backHeldPrevious; }
        public static bool GetBackButtonUp() { return !backHeld && backHeldPrevious; }

        #endregion

        #region Scroll

        /// <summary>
        /// Scroll wheel emulation, consumed once per frame. BaseScreenComponent tests
        /// only the sign, so any non-zero magnitude scrolls by one step.
        /// </summary>
        public static float MouseScroll { get; private set; }

        public static void QueueScroll(float ticks)
        {
            pendingScroll += ticks;
        }

        #endregion

        #region Reset

        public static void ResetButtons()
        {
            for (int i = 0; i < buttonCount; i++)
            {
                held[i] = false;
                heldPrevious[i] = false;
                latched[i] = false;
                pressFrames[i] = 0;
            }

            backHeld = false;
            backHeldPrevious = false;
            backPressFrames = 0;

            pendingScroll = 0f;
            MouseScroll = 0f;
        }

        /// <summary>
        /// Hand the pointer back to the engine. Called when the controller is destroyed
        /// or disabled, otherwise VirtualCursorActive could stay true with nothing
        /// driving it and the classic UI would be left with a frozen cursor.
        /// </summary>
        public static void Relinquish()
        {
            VirtualCursorActive = false;
            KeyboardActive = false;
            Mode = MobileControlMode.Gameplay;
            ResetButtons();
        }

        #endregion

        #region Engine Hooks

        /// <summary>
        /// Called from InputManager.Update() before the paused early-return.
        /// Ticks button state and the virtual cursor, both of which must keep
        /// running while a menu has the game paused.
        /// </summary>
        public static void PollCursorStage()
        {
            if (!Enabled)
                return;

            TickButtons();

            if (MobileInputController.HasInstance)
                MobileInputController.Instance.PollCursorStage();
        }

        /// <summary>
        /// Called from InputManager.Update() after the vanilla mouse axes are read.
        /// Overwrites those axes with touch deltas and pushes movement/actions.
        /// </summary>
        public static void PollGameplayStage(InputManager inputManager)
        {
            if (!Enabled)
                return;

            if (MobileInputController.HasInstance)
                MobileInputController.Instance.PollGameplayStage(inputManager);
        }

        #endregion
    }
}
