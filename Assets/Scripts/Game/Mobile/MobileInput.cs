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

    public static class MobileInput
    {
        public const string TouchUIEnabledPrefKey = "DFMobile.touchui";
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
        /// layer would hijack it away from the gamepad.
        /// </summary>
        public static bool VirtualCursorActive
        {
            get { return virtualCursorActive && !PhysicalInputActive; }
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
            get { return ControllerActive || KeyboardActive || PointerActive; }
        }

        /// <summary>Set by MobileInputController when a hardware key is pressed.</summary>
        public static bool KeyboardActive { get; set; }

        /// <summary>True after iPadOS has delivered a hardware-pointer event.</summary>
        public static bool PointerActive { get; set; }

        /// <summary>True while UIKit has placed the indirect pointer on the view edge.</summary>
        public static bool PointerAtEdge { get; set; }

        /// <summary>Relative hardware-pointer movement collected before gameplay input.</summary>
        public static Vector2 PointerDelta { get; set; }
        public static Vector2 PointerPosition { get; private set; }

        static bool pointerButtonHeld;
        static bool pointerButtonPrevious;
        static bool pointerSecondaryHeld;
        static bool pointerSecondaryPrevious;

        public static bool GetPointerButtonDown()
        {
            return pointerButtonHeld && !pointerButtonPrevious;
        }

        public static bool GetPointerButtonUp()
        {
            return !pointerButtonHeld && pointerButtonPrevious;
        }

        public static bool GetPointerButton()
        {
            return pointerButtonHeld;
        }

        /// <summary>
        /// Secondary (right) pointer button - the classic SwingWeapon binding.
        ///
        /// Read from GCMouse through the native bridge rather than from Unity. A
        /// locked pointer has no screen position, so iPadOS delivers no located
        /// touch for the click and Unity's own mouse buttons stay down-less for the
        /// whole of a swing. GCMouse reports the button either way, which is what
        /// lets the pointer stay locked - and the cursor stay hidden - while the
        /// player swings.
        /// </summary>
        public static bool GetPointerSecondaryButtonDown()
        {
            return pointerSecondaryHeld && !pointerSecondaryPrevious;
        }

        public static bool GetPointerSecondaryButtonUp()
        {
            return !pointerSecondaryHeld && pointerSecondaryPrevious;
        }

        public static bool GetPointerSecondaryButton()
        {
            return pointerSecondaryHeld;
        }

        public static void UpdatePointer(Vector2 position, Vector2 delta, bool active, bool buttonHeld,
                                         bool secondaryHeld = false)
        {
            pointerButtonPrevious = pointerButtonHeld;
            pointerButtonHeld = active && buttonHeld;
            pointerSecondaryPrevious = pointerSecondaryHeld;
            pointerSecondaryHeld = active && secondaryHeld;
            PointerPosition = position;
            PointerDelta = active ? delta : Vector2.zero;
            PointerActive = active || PointerActive;
        }

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
            PointerActive = false;
            PointerAtEdge = false;
            PointerDelta = Vector2.zero;
            PointerPosition = Vector2.zero;
            pointerButtonHeld = false;
            pointerButtonPrevious = false;
            pointerSecondaryHeld = false;
            pointerSecondaryPrevious = false;
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
