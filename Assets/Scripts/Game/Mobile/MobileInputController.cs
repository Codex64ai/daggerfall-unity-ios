// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Central brain for touch input. Owns the mode state machine and pushes touch
//   state into InputManager using its existing public API:
//
//     ApplyHorizontalForce / ApplyVerticalForce  <- virtual joystick
//     SetMobileMouseAxes                         <- touch drag (drives BOTH camera
//                                                   look and weapon gestures, exactly
//                                                   like the PC mouse does)
//     AddAction                                  <- virtual buttons
//
//   Because mouseX/mouseY feed both PlayerMouseLook.ApplyLook() and
//   WeaponManager.TrackMouseAttack(), one injected channel reproduces PC behaviour
//   with no changes to either script. PlayerMouseLook.cs:247 already suppresses
//   camera look while Actions.SwingWeapon is held in WeaponSwingMode 0, so swipes
//   do not yank the camera mid-swing.
//

using UnityEngine;
using UnityEngine.UI;
using System.IO;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileInputController : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        // Low-level Input System capture. This is intentionally separate from
        // Mouse.current.delta: the latter may already be consumed by the time the
        // game's Update loop reaches PollHardwarePointer().
        Vector2 inputSystemEventDelta;
        // Unscaled-time stamp of the last real input-system mouse event. The sentinel
        // means "never saw one" and is treated as fresh, so a pointer is only declared
        // gone after it was demonstrably alive and then fell silent.
        float lastInputSystemEventAt = float.MinValue;
        bool directTouchInputActive;
        uint inputSystemMouseEvents;
        uint inputSystemNonZeroMouseEvents;

        void CaptureInputSystemMouseEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!(device is Mouse) || Mouse.current == null)
                return;

            // A live input-system mouse event means the hardware pointer is still
            // present; refresh the ownership timeout even while the touch mute is
            // eating the delta itself.
            lastInputSystemEventAt = Time.unscaledTime;

            if (directTouchInputActive)
                return;

            inputSystemMouseEvents++;
            Vector2 delta = Mouse.current.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f)
                return;

            inputSystemNonZeroMouseEvents++;
            inputSystemEventDelta += delta;
        }
#endif
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool DFMobilePointerRead(out float x, out float y, out float dx, out float dy, out bool buttonHeld, out bool atEdge, out bool secondaryButtonHeld, out bool directTouch);
        [DllImport("__Internal")] static extern void DFMobilePointerSetHidden(bool hidden);
        [DllImport("__Internal")] static extern void DFMobilePointerSetDirectTouchActive(bool active);
        [DllImport("__Internal")] static extern void DFMobilePointerLockWindowSize(bool locked);
        [DllImport("__Internal")] static extern void DFMobilePointerDiagnostics(
            out uint windowEvents, out uint indirectTouches, out uint nonZeroDeltas,
            out uint hoverEvents, out uint gameControllerDeltas,
            out int lastEventType, out bool locked, out uint lockRecoveries,
            out uint directTouches, out uint styleRequests,
            out uint unlocksWhileHeld, out uint unlocksWhileIdle);
#endif
        #region Singleton

        static MobileInputController instance;

        public static bool HasInstance { get { return instance != null; } }

        public static MobileInputController Instance { get { return instance; } }

        #endregion

        #region Fields

        [Header("Input Widgets")]
        public VirtualJoystick moveJoystick;

        [Tooltip("Right-side stick: deflection turns the camera at a steady rate, gamepad style.")]
        public VirtualJoystick lookJoystick;
        public TouchLookZone lookZone;
        public VirtualMouseCursor virtualMouse;

        [Header("HUD Layers")]
        [Tooltip("Joystick, action bank and combat toggle. Hidden while a classic menu is open.")]
        public GameObject gameplayLayer;
        [Tooltip("Menu-only helpers such as the UI back button.")]
        public GameObject menuLayer;

        [Header("Combat Toggle")]
        public Image combatToggleGraphic;

        [Tooltip("Optional sprites for the combat toggle. When both are set the sprite is " +
                 "swapped instead of tinting, which reads far better than a colour shift once " +
                 "real artwork is in place.")]
        public Sprite combatOnSprite;
        public Sprite combatOffSprite;
        public Color combatOnColor = new Color(0.80f, 0.16f, 0.16f, 0.90f);
        public Color combatOffColor = new Color(1f, 1f, 1f, 0.30f);

        [Header("Movement")]
        [Tooltip("Joystick magnitude at or above which the player auto-runs.")]
        [Range(0.5f, 1f)] public float runThreshold = 0.85f;

        [Tooltip("Disable Daggerfall's movement acceleration so the joystick gives analog speed. " +
                 "With acceleration on, any deflection ramps to full speed and the stick becomes direction-only.")]
        public bool disableMovementAcceleration = true;

        [Header("Look / Gesture Channel")]
        [Tooltip("Touch pixels -> mouse-axis units. Lower = slower camera. Also scales gesture travel.")]
        public float touchToMouseScale = 0.15f;

        [Tooltip("Look-stick turn rate at full deflection, in mouse-axis units per second. " +
                 "~270 matches a brisk drag across the screen.")]
        public float lookStickSpeed = 220f;

        [Tooltip("Extra camera turn rate while the hidden pointer is held against a gameplay edge.")]
        public float pointerEdgeLookSpeed = 480f;

        [Tooltip("Distance from the screen edge where pointer edge-look begins, in pixels.")]
        public float pointerEdgeMargin = 12f;

        [Tooltip("How long a screen touch may mute the Magic Keyboard trackpad before it is " +
                 "assumed to be a touch iPadOS claimed for a system gesture and never ended.")]
        public float maximumDirectTouchMute = 2f;

        [Tooltip("PHYSICAL swipe distance needed to trigger an attack, in inches. Physical rather " +
                 "than a screen fraction because 18% of an iPhone is ~1in while 18% of a 13in iPad " +
                 "is ~1.9in - the same setting would mean double the thumb travel on tablets.")]
        [Range(0.3f, 2.5f)] public float swipeDistanceInches = 0.9f;

        [Tooltip("Fall back to the old screen-fraction behaviour (not recommended across device sizes).")]
        public bool useScreenFractionInsteadOfInches = false;

        [Tooltip("Used only when useScreenFractionInsteadOfInches is on.")]
        [Range(0.05f, 0.6f)] public float swipeScreenFraction = 0.18f;

        [Tooltip("Keep Actions.SwingWeapon held this long after the finger lifts, so short flicks still resolve and bows release.")]
        public float swingHoldExtension = 0.12f;

        [Tooltip("Hold this long before a bow starts drawing, so an incidental tap on the " +
                 "view does not loose an arrow. Imperceptible for a deliberate hold.")]
        public float bowDrawMinHold = 0.10f;



        [Header("Virtual Cursor")]
        [Tooltip("Classic-style cursor drawn by InputManager.OnGUI while a menu is open. " +
                 "REQUIRED - without it the menu cursor is invisible.")]
        public Texture2D cursorTexture;
        public int cursorWidth = 32;
        public int cursorHeight = 32;

        [Header("Weapon Swing Mode")]
        [Tooltip("Force WeaponSwingMode 0 (hold-and-drag gestures). Required for swipe attacks: " +
                 "modes 1 and 2 are click-to-attack and pick a RANDOM direction, discarding the " +
                 "swipe. Mode 0 is also classic Daggerfall behaviour, so this does not disadvantage " +
                 "keyboard players - but it DOES overwrite the setting in settings.ini, so turn it " +
                 "off if you want to keep your own choice.")]
        public bool forceGestureSwingMode = true;

        [Header("Auto Combat")]
        [Tooltip("Treat a drawn weapon as combat mode, so swipes attack without needing the " +
                 "on-screen COMBAT button. Without this, drawing a weapon by any other means " +
                 "(keyboard, the WEAPON button) leaves drags look-only, which reads as broken.")]
        public bool autoCombatWhenWeaponDrawn = true;

        [Header("Physical Input")]
        [Tooltip("Show and process the touch controls. Turn this off when using only a Magic Keyboard or gamepad.")]
        public bool touchUIEnabled = true;

        [Tooltip("Hide the touch HUD when a hardware keyboard is used, the same way a gamepad " +
                 "does. Touching the screen brings it back.")]
        public bool autoHideOnKeyboard = true;

        [Tooltip("How long the hardware pointer may go completely silent before the touch layer " +
                 "takes ownership back. This is what makes a detached Magic Keyboard hand control " +
                 "back to the glass on its own: UIKit keeps reporting the pointer as present after " +
                 "the keyboard is gone, so without a timeout the ghost trackpad would keep the " +
                 "pointer's ownership forever and touch would never come back. Applied in " +
                 "PollHardwarePointer against Input System silence - there is no native twin " +
                 "in DFMobilePointer.mm to keep in step with.")]
        public float pointerIdleTimeout = 1f;

        [Header("Controller")]
        [Tooltip("Detect a connected gamepad, hide the touch HUD, and hand input back to " +
                 "Daggerfall's own controller support (which already exists and is complete).")]
        public bool autoDetectController = true;

        [Tooltip("Seconds between gamepad polls. Input.GetJoystickNames() allocates, so this must " +
                 "not run every frame.")]
        public float controllerPollInterval = 0.75f;

        [Header("Cutscenes")]
        [Tooltip("Holding a finger on the screen this long skips an intro/cutscene video. " +
                 "The video window already exits on the UI back button; touch just needed a " +
                 "way to press it.")]
        public float holdToSkipVideoSeconds = 0.6f;

        [Tooltip("Two taps inside this window skip an intro/cutscene video, as an " +
                 "alternative to holding to skip.")]
        public float doubleTapToSkipWindow = 0.45f;

        [Header("Debug")]
        [Tooltip("On-screen readout: live touch count, both sticks' state, gesture calibration. " +
                 "Toggle from TUNE > Show diagnostics.")]
        public bool showGestureDebug = false;

        bool combatModeWanted;
        bool menuLayerWasActive;
        bool controllerConnected;
        float nextControllerPoll;
        float nextTouchAssert;
        bool keyboardActive;
        bool pointerPositionInitialized;
        bool indirectPointerActive;
        bool touchCursorWasDriving;
        // Once a finger takes the pointer back, a detached or merely parked native
        // pointer must not reclaim it on the first zero-delta read after release.
        // Cleared only by demonstrable hardware-pointer activity or a keyboard key.
        bool touchOwnsPointer;

#if UNITY_IOS && !UNITY_EDITOR
        // The native bridge's verdict on whether a real finger is on the glass, kept
        // for the frame so PollKeyboard can consult it before the pointer poll runs.
        // Unity's own touch list cannot answer this: it reports the Magic Keyboard's
        // indirect pointer as a Direct touch.
        bool nativeDirectTouchPresent;
#endif
#if UNITY_IOS && !UNITY_EDITOR
        float directTouchSince = -1f;
#endif
        Vector2 previousPointerPosition;
        Vector2 lastPointerDirection;
        float vidHoldStart = -1f;
        bool vidSkipQueued;
        float vidLastTapTime = -1f;
        float swingHoldUntil;
        float bowHoldStart = -1f;
        bool thresholdApplied;
        int appliedScreenWidth;
        int appliedScreenHeight;

        // Virtual button taps held for 2 frames so both ActionStarted() and
        // ActionComplete() fire. PlayerActivate.cs:280 keys off ActionComplete, so a
        // single-frame injection would silently do nothing.
        readonly Dictionary<InputManager.Actions, int> tapActions = new Dictionary<InputManager.Actions, int>();
        readonly HashSet<InputManager.Actions> heldActions = new HashSet<InputManager.Actions>();
        readonly List<InputManager.Actions> scratch = new List<InputManager.Actions>();

        #endregion

        #region Properties

        public bool CombatModeWanted { get { return combatModeWanted; } }

        #endregion

        #region Unity

        void Awake()
        {
            // A second controller (e.g. one placed in both scenes) would clobber the
            // singleton and then reset globals when it was torn down.
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
#if ENABLE_INPUT_SYSTEM
            InputSystem.onEvent += CaptureInputSystemMouseEvent;
#endif
            MobileInput.Enabled = true;
            touchUIEnabled = PlayerPrefs.GetInt(MobileInput.TouchUIEnabledPrefKey,
                touchUIEnabled ? 1 : 0) == 1;
            MobileInput.TouchUIEnabled = touchUIEnabled;

            // Unity's legacy touch-to-mouse emulation reports a left click at the
            // fingertip on every touch. InputManager.GetMouseButtonDown() ORs against
            // Input.GetMouseButtonDown(), so leaving this on produces phantom clicks
            // that fight the virtual cursor.
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            MobileInput.CursorTexture = cursorTexture;
            MobileInput.CursorWidth = cursorWidth;
            MobileInput.CursorHeight = cursorHeight;
            MobileInput.CentreCursor();

            // Warm the Taptic generators so the first button press is not late.
            MobileHaptics.Prepare();

            // Set in Awake so it lands before InputManager.Start() caches the value.
            // If InputManager already started, this takes effect next session.
            if (disableMovementAcceleration)
                DaggerfallUnity.Settings.MovementAcceleration = false;
        }

        void Start()
        {
            // Gesture swings require WeaponSwingMode 0 (hold-and-drag). Modes 1 and 2
            // are click-to-attack, which picks a RANDOM direction in WeaponManager.cs:345
            // and would discard the swipe direction entirely.
            if (forceGestureSwingMode && DaggerfallUnity.Settings.WeaponSwingMode != 0)
            {
                Debug.Log("[MobileInput] forcing WeaponSwingMode 0 (was " +
                          DaggerfallUnity.Settings.WeaponSwingMode +
                          ") - required for directional swipe attacks");
                DaggerfallUnity.Settings.WeaponSwingMode = 0;
            }

            ApplyAttackThreshold();

            // DEVICE-PROVEN FIX: iPadOS (with a Magic Keyboard trackpad attached) reports
            // KeyCode.Mouse0 / GetMouseButton(0) as PERMANENTLY HELD - captured in the
            // idle probe with zero touches: "m0key=True m0btn=True". Since Mouse0 is
            // Daggerfall's ActivateCenterObject binding, the engine re-added the action
            // every frame, so the release edge PlayerActivate waits for never existed -
            // doors could not be opened by any means. On touch devices the mouse-button
            // bindings are no longer cleared here: Magic Keyboard users need the normal
            // mouse attack and interaction assignments. Joystick bindings are swept below.
            if (Input.touchSupported && !Application.isEditor && InputManager.HasInstance)
            {
                ClearPhantomProneBindings();
            }

            ApplyHudVisibility();
        }

        /// <summary>
        /// On touch devices, remove joystick-button keybinds, primary AND secondary, then
        /// force the private binding cache to rebuild. Mouse bindings remain available for
        /// hardware-pointer users.
        ///
        /// Two device-proven reasons. First, iPadOS (Magic Keyboard attached) reports a
        /// mouse button as permanently held. Second - the subtle one - severing only the
        /// Mouse0 primary PROMOTED our own persisted gamepad secondary
        /// (JoystickButton0 -> ActivateCenterObject, from MobileGamepadBindings) into the
        /// merged dict, and iPadOS pulses joystick-button state during touches: the action
        /// stayed pinned and doors remained unopenable. Pulled from the device's own
        /// KeyBinds.txt. Real gamepads are unaffected: on connect,
        /// MobileGamepadBindings.Apply() rebinds live; on disconnect we clear again.
        /// KeyBinds.txt on disk is never modified here.
        ///
        /// Scope note: this sweeps JoystickButton0-19 only. The gamepad layout also binds
        /// InputManager's synthetic axis keycodes (5000+) and combo keycodes
        /// (65537+) for the trigger/d-pad and LT-modifier layers, which are outside that
        /// range on purpose - they cannot resurrect from KeyBinds.txt because they are never
        /// written to it, and they resolve to false while EnableController is off. They are
        /// removed by MobileGamepadBindings.Clear() on disconnect instead.
        /// </summary>
        void ClearPhantomProneBindings()
        {
            if (!InputManager.HasInstance)
                return;

            InputManager im = InputManager.Instance;

            var prone = new List<KeyCode>();
            for (KeyCode k = KeyCode.JoystickButton0; k <= KeyCode.JoystickButton19; k++)
                prone.Add(k);

            foreach (KeyCode key in prone)
            {
                im.ClearBinding(key, true);
                im.ClearBinding(key, false);
            }

                // ClearBinding edits the source dictionaries, but the input loop reads a
                // CACHED merge (existingKeyDict) rebuilt only by the private
                // UpdateBindingCache(). Without this, the stale Mouse0->Activate entry
                // survives and the phantom-held button keeps the action pinned - the clear
                // only ever appeared to work. Invoke the rebuild via reflection (safe under
                // IL2CPP: link.xml preserves Assembly-CSharp wholesale).
                var rebuild = typeof(InputManager).GetMethod("UpdateBindingCache",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rebuild != null)
                    rebuild.Invoke(im, null);
                else
                    Debug.LogError("[MobileInput] UpdateBindingCache not found - mouse-binding clear will not stick!");

            // On-device verification: both slots must resolve to None.
            Debug.Log("[MobileInput] cleared mouse+joystick keybinds; Activate primary=" +
                      im.GetBinding(InputManager.Actions.ActivateCenterObject, true) +
                      " secondary=" + im.GetBinding(InputManager.Actions.ActivateCenterObject, false));
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onEvent -= CaptureInputSystemMouseEvent;
#endif
            // Hand the pointer back, otherwise VirtualCursorActive can stay true with
            // nothing driving it and the classic UI is left with a frozen cursor.
            MobileInput.Relinquish();
            RestoreEngineTouchDefaults();
        }

        void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onEvent -= CaptureInputSystemMouseEvent;
#endif
            if (instance == this)
            {
                instance = null;
                MobileInput.Relinquish();
                RestoreEngineTouchDefaults();
            }
        }

        /// <summary>
        /// Undo the global we changed in Awake. Input.simulateMouseWithTouches is engine
        /// wide and survives scene unload, so leaving it false after this controller goes
        /// away (returning to the startup scene, for instance) would strand the classic UI
        /// with no virtual cursor AND no touch-to-mouse fallback - completely dead input.
        /// </summary>
        static void RestoreEngineTouchDefaults()
        {
            Input.simulateMouseWithTouches = true;
        }

        void Update()
        {
            // Runs even while paused: Time.timeScale = 0 stops time, not Update calls.
            // GameManager.WeaponManager may not exist yet at Start(), so keep retrying
            // until the threshold actually lands.
            if (!thresholdApplied || Screen.width != appliedScreenWidth || Screen.height != appliedScreenHeight)
                ApplyAttackThreshold();

            // Belt and braces: nothing in DFU touches these, but if any plugin or OS
            // event ever resets them, sticks silently degrade to single-touch. Cheap.
            if (Time.unscaledTime >= nextTouchAssert)
            {
                nextTouchAssert = Time.unscaledTime + 1f;
                if (!Input.multiTouchEnabled)
                    Input.multiTouchEnabled = true;
                if (Input.simulateMouseWithTouches && !controllerConnected)
                    Input.simulateMouseWithTouches = false;
            }

            PollKeyboard();
            PollController();

            // Classic bottom-bar touch routing. Runs even with a controller connected -
            // the bar stays tappable when the rest of the touch overlay has stood down.
            MobileClassicHud.Poll();

            // Passive: records controller buttons the layout does not bind, so an unknown
            // button (Select/View) identifies itself during normal play.
            if (controllerConnected)
                MobileGamepadBindings.WatchUnknownButtons();

            MobileSessionLog.Poll();

            MobileControlMode desired = ResolveMode();
            if (desired != MobileInput.Mode)
                EnterMode(desired);
        }

        #region Physical Keyboard

        /// <summary>
        /// Mirrors the gamepad behaviour for a hardware keyboard: using one hides the touch
        /// HUD, touching the screen brings it back. Mouse movement deliberately does NOT
        /// count, because the editor drives the touch overlay with a mouse for testing.
        /// </summary>
        void PollKeyboard()
        {
            if (!autoHideOnKeyboard)
                return;

            // A REAL finger means the player is back on the glass.
            //
            // Not Input.touchCount: that includes the Magic Keyboard's own pointer,
            // which iPadOS delivers as an indirect-pointer touch and Unity reports as
            // Direct. Reading it raw let the trackpad cancel keyboard mode by itself
            // while it was merely being moved - the capture shows keyboard flipping
            // true/false almost every second through the whole session - and drop the
            // pointer below on the same beat. The native bridge sees the real UITouch
            // type; this is its verdict from the previous frame's poll.
            bool fingerOnGlass = Input.touchCount > 0;
#if UNITY_IOS && !UNITY_EDITOR
            fingerOnGlass = nativeDirectTouchPresent;
#endif

            if (fingerOnGlass)
            {
                // PollKeyboard runs before the pointer poll that normally performs
                // this handover. Clear the stale keyboard pointer here as well, so
                // ApplyHudVisibility can restore the gameplay layer before the
                // touch's first movement sample is dispatched.
                if (MobileInput.PointerActive)
                {
                    MobileInput.PointerActive = false;
                    MobileInput.PointerDelta = Vector2.zero;
                }

                if (keyboardActive)
                    SetKeyboardActive(false);
                else
                    ApplyHudVisibility();
                return;
            }

            // DEVICE-PROVEN FIX: on iOS, Input.anyKeyDown fires for TOUCHES, and at
            // frame boundaries between touches it latched keyboard mode with no keyboard
            // attached - force-releasing both joysticks mid-grab. That was the entire
            // "sticks are inconsistent" bug. Detect keyboards by the one signal only a
            // real keyboard produces: typed characters. Touches, trackpads and styluses
            // never populate Input.inputString.
            if (!keyboardActive && Input.inputString.Length > 0)
                SetKeyboardActive(true);
        }

        void SetKeyboardActive(bool value)
        {
            keyboardActive = value;
            MobileInput.KeyboardActive = value;

            if (value)
            {
                touchOwnsPointer = false;
                ReleaseGameplayInput();
                MobileInput.ResetButtons();
            }

            ApplyHudVisibility();

            Debug.Log(value
                ? "[MobileInput] hardware keyboard in use - touch HUD hidden"
                : "[MobileInput] touch resumed - touch HUD restored");
        }

        public bool KeyboardActive { get { return keyboardActive; } }

        #endregion

        #region Controller

        /// <summary>
        /// Daggerfall Unity already ships full controller support - axis bindings, a controller
        /// cursor drawn in InputManager.OnGUI, and DaggerfallJoystickControlsWindow. So this does
        /// not reimplement anything: it detects the gamepad, gets the touch layer out of the way,
        /// and lets the engine's own path take over.
        /// </summary>
        void PollController()
        {
            if (!autoDetectController || Time.unscaledTime < nextControllerPoll)
                return;

            nextControllerPoll = Time.unscaledTime + Mathf.Max(controllerPollInterval, 0.1f);

            bool found = false;
            string[] names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    found = true;
                    break;
                }
            }

            if (found == controllerConnected)
                return;

            controllerConnected = found;

            // MobileInput.VirtualCursorActive is forced false while this is true, so the classic
            // UI pointer reverts to InputManager.UsingController instead of our touch cursor.
            MobileInput.ControllerActive = found;

            if (found)
            {
                ReleaseGameplayInput();
                MobileInput.ResetButtons();
                Input.simulateMouseWithTouches = true;   // restore engine default while touch stands down
            }
            else
            {
                Input.simulateMouseWithTouches = false;  // touch layer resumes ownership
            }

            if (InputManager.HasInstance)
                InputManager.Instance.EnableController = found || DaggerfallUnity.Settings.EnableController;

            // DFU binds gamepad buttons for UI clicks only; gameplay actions default to
            // keyboard. Apply gamepad defaults while one is connected; strip them again on
            // disconnect - leftover joystick bindings are phantom fuel on iPadOS.
            if (found)
            {
                MobileGamepadBindings.Apply();
            }
            else
            {
                // Order matters: MobileGamepadBindings.Clear() knows the axis and combo
                // keycodes it applied, which live outside the Mouse0-6 / JoystickButton0-19
                // range ClearPhantomProneBindings() sweeps. Sweeping first would orphan them.
                MobileGamepadBindings.Clear();
                ClearPhantomProneBindings();
            }

            ApplyHudVisibility();

            Debug.Log(found
                ? "[MobileInput] gamepad connected - touch HUD hidden, controller cursor active"
                : "[MobileInput] gamepad disconnected - touch HUD restored");
        }

        /// <summary>Single place that decides which HUD layer is visible.</summary>
        void ApplyHudVisibility()
        {
            bool touchAllowed = touchUIEnabled && !controllerConnected && !keyboardActive && !MobileInput.PointerActive;
            bool menu = MobileInput.Mode == MobileControlMode.Menu;

            SetLayer(gameplayLayer, touchAllowed && !menu);
            SetLayer(menuLayer, touchAllowed && menu);

            MobileInput.TouchInputActive = touchAllowed;

            // NO CURSOR SPRITE WHILE TOUCH OWNS THE INPUT. RELATIVE MODE INCLUDED.
            //
            // In direct-touch mode the cursor sits exactly under the fingertip that put
            // it there, so it shows nothing the player cannot already see. Relative mode
            // has a better claim to one - the finger drives the cursor at a distance -
            // but a drawn pointer is troublesome enough on iOS to be worth losing there
            // too. Touch is the whole interface.
            MobileInput.VirtualCursorVisible = !touchAllowed;
        }

        public void SetTouchUIEnabled(bool value)
        {
            touchUIEnabled = value;
            MobileInput.TouchUIEnabled = value;
            if (!value)
            {
                ReleaseGameplayInput();
                MobileInput.ResetButtons();
                MobileInput.VirtualCursorActive = false;
            }
            ApplyHudVisibility();
        }

        public bool ControllerConnected { get { return controllerConnected; } }

        /// <summary>
        /// True while a bow is the drawn weapon. Bows are a completely different input
        /// shape from every other weapon: WeaponManager ignores swing tracking for them and
        /// keys off Actions.SwingWeapon being HELD, so the touch layer has to treat them
        /// separately or aiming itself becomes the trigger.
        /// </summary>
        public static bool BowEquipped
        {
            get
            {
                if (!GameManager.HasInstance)
                    return false;

                WeaponManager wm = GameManager.Instance.WeaponManager;
                if (wm == null || wm.ScreenWeapon == null || wm.Sheathed)
                    return false;

                return wm.ScreenWeapon.WeaponType == WeaponTypes.Bow;
            }
        }

        #endregion

        void OnGUI()
        {
            if (!showGestureDebug || MobileInput.MenuMode)
                return;

            float threshold = 0f;
            if (GameManager.HasInstance && GameManager.Instance.WeaponManager != null)
                threshold = GameManager.Instance.WeaponManager.AttackThreshold;

            string sticks = string.Format(
                "touches {0}  multi {1}  kb {6}\nmove {2} {3}\nlook {4} {5}",
                Input.touchCount, Input.multiTouchEnabled,
                moveJoystick != null && moveJoystick.IsHeld ? "HELD" : "idle",
                moveJoystick != null ? moveJoystick.Value.ToString("0.00") : "-",
                lookJoystick != null && lookJoystick.IsHeld ? "HELD" : "idle",
                lookJoystick != null ? lookJoystick.Value.ToString("0.00") : "-",
                keyboardActive ? "ACTIVE" : "off");

            string uiHit = "-";
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
            {
                uiHit = "NO EVENTSYSTEM";
            }
            else if (Input.touchCount > 0)
            {
                var pd = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.GetTouch(0).position };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pd, hits);
                uiHit = hits.Count > 0 ? hits[0].gameObject.name : "(nothing)";
            }

            string text = "ui hit: " + uiHit + "\n" + sticks + "\n" + string.Format(
                "mode {0}  gamepad {6}\nswipe {1:0.00}in x scale {2:0.000}  dpi {7:0}\nAttackThreshold {3:0.0000}\nswinging {4}\nrequired swipe ~{5:0} px",
                MobileInput.Mode, swipeDistanceInches, touchToMouseScale, threshold,
                Time.unscaledTime < swingHoldUntil,
                touchToMouseScale > 0f
                    ? threshold * Mathf.Max(Screen.width, Screen.height) / touchToMouseScale
                    : 0f,
                controllerConnected, MobileInput.Dpi);

            GUI.Label(new Rect(12f, 12f, 560f, 250f), text);
        }

        #endregion

        #region Mode

        MobileControlMode ResolveMode()
        {
            if (IsClassicMenuOpen())
                return MobileControlMode.Menu;

            bool combat = combatModeWanted;

            // A drawn weapon means the player wants to fight. Requiring a separate toggle
            // on top of that is a trap: draw fists with the keyboard or the WEAPON button,
            // swipe, and nothing happens because Combat mode was never entered.
            if (!combat && autoCombatWhenWeaponDrawn && GameManager.HasInstance)
            {
                WeaponManager weaponManager = GameManager.Instance.WeaponManager;
                if (weaponManager != null && !weaponManager.Sheathed)
                    combat = true;
            }

            return combat ? MobileControlMode.Combat : MobileControlMode.Gameplay;
        }

        /// <summary>
        /// True when a classic UI window owns the screen. UserInterfaceManager keeps a
        /// window stack, and StateManager uses the same WindowCount test to decide it is
        /// in a UI state, so this matches the engine's own notion of "a menu is open".
        ///
        /// Guarded with HasInstance because this runs during startup scenes where
        /// GameManager and DaggerfallUI may not exist yet.
        /// </summary>
        public static bool IsClassicMenuOpen()
        {
            if (DaggerfallUI.HasInstance)
            {
                IUserInterfaceManager uiManager = DaggerfallUI.UIManager;
                if (uiManager != null && uiManager.WindowCount > 0)
                    return true;
            }

            // Covers pause and anything that pauses without pushing a window.
            return GameManager.HasInstance && GameManager.IsGamePaused;
        }

        void EnterMode(MobileControlMode mode)
        {
            MobileInput.Mode = mode;

            if (mode == MobileControlMode.Menu)
            {
                // Release everything: a held joystick would otherwise keep the player
                // walking behind the inventory screen.
                ReleaseGameplayInput();

                ApplyHudVisibility();

                if (!menuLayerWasActive)
                {
                    MobileInput.ResetButtons();
                    MobileInput.CentreCursor();
                    if (virtualMouse != null)
                        virtualMouse.ResetGesture();
                    menuLayerWasActive = true;
                }

                MobileInput.VirtualCursorActive = true;
            }
            else
            {
                if (menuLayerWasActive)
                {
                    MobileInput.ResetButtons();
                    if (virtualMouse != null)
                        virtualMouse.ResetGesture();
                    menuLayerWasActive = false;
                }

                MobileInput.VirtualCursorActive = false;
                MobileKeyboard.Dismiss();

                ApplyHudVisibility();
            }

            if (lookZone != null)
                lookZone.SetCombatMode(mode == MobileControlMode.Combat);

            RefreshCombatToggleVisual();
        }

        static void SetLayer(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        void ReleaseGameplayInput()
        {
            if (moveJoystick != null)
                moveJoystick.ForceRelease();
            if (lookJoystick != null)
                lookJoystick.ForceRelease();
            if (lookZone != null)
                lookZone.ForceRelease();

            tapActions.Clear();
            heldActions.Clear();
            swingHoldUntil = 0f;
            bowHoldStart = -1f;
        }

        void RefreshCombatToggleVisual()
        {
            if (combatToggleGraphic == null)
                return;

            bool on = MobileInput.CombatMode;

            if (combatOnSprite != null && combatOffSprite != null)
            {
                // Real art: swap the sprite and stay fully opaque. Tinting artwork that
                // already depicts its own state just muddies it.
                combatToggleGraphic.sprite = on ? combatOnSprite : combatOffSprite;
                combatToggleGraphic.color = Color.white;
            }
            else
            {
                combatToggleGraphic.color = on ? combatOnColor : combatOffColor;
            }
        }

        #endregion

        #region Public UI Hooks

        /// <summary>Wire to the Combat Mode button's onClick.</summary>
        public void ToggleCombatMode()
        {
            combatModeWanted = !combatModeWanted;

            // Entering combat with a sheathed weapon is almost never what the player
            // meant, so draw it for them.
            if (combatModeWanted && GameManager.HasInstance)
            {
                WeaponManager weaponManager = GameManager.Instance.WeaponManager;
                if (weaponManager != null && weaponManager.Sheathed)
                    weaponManager.ToggleSheath();
            }

            EnterMode(ResolveMode());
        }

        /// <summary>Fire a one-shot action from a virtual button.</summary>
        public void QueueAction(InputManager.Actions action, int frames = 2)
        {
            tapActions[action] = Mathf.Max(frames, 2);
        }

        /// <summary>Hold or release an action for as long as a virtual button is pressed.</summary>
        public void SetActionHeld(InputManager.Actions action, bool value)
        {
            if (value)
                heldActions.Add(action);
            else
                heldActions.Remove(action);
        }

        #endregion

        #region Engine Poll Stages

        /// <summary>
        /// Called from InputManager.Update() before its paused early-return, mirroring
        /// where the engine updates its own controller cursor.
        /// </summary>
        public void PollCursorStage()
        {
            PollHardwarePointer();
            LogMenuDiagnostics();
            LogTouchDiagnostics();

#if UNITY_IOS && !UNITY_EDITOR
            bool gameplayPointer = MobileInput.PointerActive && !IsClassicMenuOpen();
            DFMobilePointerSetHidden(gameplayPointer);
            DFMobilePointerLockWindowSize(gameplayPointer);
#endif
            if (MobileInput.PointerActive)
                Cursor.visible = IsClassicMenuOpen();

            // iOS soft keyboard for classic text fields (player name entry, the travel map's
            // city search, and so on). Without this, TextBoxes are untypeable on device - the
            // soft keyboard only exists if something opens it.
            //
            // NOT part of the touch-only block below. This was previously skipped whenever a
            // controller was connected, which made every text field in the game unusable with
            // a pad: a gamepad cannot type, so suppressing the soft keyboard leaves no input
            // method at all. Device-reported by Ikram.
            //
            // Still suppressed for a HARDWARE keyboard, which genuinely can type - putting a
            // soft keyboard over the screen there would be an obstruction, not a feature.
            if (!keyboardActive && MobileInput.MenuMode)
                MobileKeyboard.Poll();

            // Touch-only from here. The virtual cursor must stand down for a controller
            // (InputManager drives its own cursor instead) and video-skip needs live touches.
            bool touchDrivesCursor = touchUIEnabled && !controllerConnected && !keyboardActive &&
                                     !MobileInput.PointerActive;

            // A GESTURE IN FLIGHT CANNOT BE ENDED BY A LAYER THAT IS NO LONGER POLLED.
            //
            // VirtualMouseCursor latches the left button down after a third of a second of
            // stationary contact, and only EndPrimary releases it - which runs from
            // PollTouches, below this return. So a hand still on the glass when a key is
            // typed, a gamepad appears, or the trackpad moves left the button latched with
            // no finger holding it, and TickButtons re-asserted it every frame afterwards.
            // With the button already down no press can produce a down edge, and a down
            // edge is the whole of a click: touch went dead and stayed dead. The capture
            // shows twenty-second stretches of held=True with touches=0.
            //
            // End the gesture on the way out instead. ResetGesture clears both sides -
            // MobileInput's latch and the cursor's own buttonLatched - which ResetButtons
            // alone does not, and that desync ate the tap after every handover.
            if (!touchDrivesCursor)
            {
                if (touchCursorWasDriving && virtualMouse != null)
                    virtualMouse.ResetGesture();

                touchCursorWasDriving = false;
                return;
            }

            touchCursorWasDriving = true;

            if (virtualMouse != null && MobileInput.MenuMode)
            {
                PollVideoSkip();
                virtualMouse.PollTouches();
            }
        }

        /// <summary>
        /// iPadOS exposes the Magic Keyboard trackpad through mousePosition, but on some
        /// iPadOS/Unity combinations it does not set Input.mousePresent or produce Mouse X/Y.
        /// Collect the position delta ourselves and use the real pointer for classic menus.
        /// A pointer event remains active until a finger takes ownership again.
        /// </summary>
        void PollHardwarePointer()
        {
            bool wasActive = MobileInput.PointerActive;

#if UNITY_IOS && !UNITY_EDITOR
            // WHICH TOUCHES ARE FINGERS IS NOT A QUESTION UNITY CAN ANSWER.
            //
            // UnityEngine.TouchType has only Direct/Indirect/Stylus, and iPadOS reports
            // the Magic Keyboard trackpad's own click as UITouchTypeIndirectPointer,
            // which Unity maps to Direct. Testing Input.GetTouch().type here therefore
            // called every trackpad press a finger on the screen - so the mute meant to
            // stop a finger fighting the trackpad fired on the trackpad itself, for the
            // whole of every weapon swing. The native bridge sees the real UITouch type
            // and answers this now.
            float nativeX, nativeY, nativeDeltaX, nativeDeltaY;
            bool nativeButtonHeld, nativeAtEdge, nativeSecondaryHeld, nativeDirectTouch;
            bool pointerRead = DFMobilePointerRead(out nativeX, out nativeY,
                                                   out nativeDeltaX, out nativeDeltaY,
                                                   out nativeButtonHeld, out nativeAtEdge,
                                                   out nativeSecondaryHeld, out nativeDirectTouch);

            bool directTouchPresent = nativeDirectTouch;
            nativeDirectTouchPresent = nativeDirectTouch;
            if (!directTouchPresent)
                directTouchSince = -1f;
            else if (directTouchSince < 0f)
                directTouchSince = Time.unscaledTime;

            // The touch UI is hidden while the pointer owns gameplay, so a finger held
            // down this long is not a control input - it is a touch iPadOS claimed for a
            // system gesture and never ended.
            bool directTouchActive = directTouchPresent &&
                Time.unscaledTime - directTouchSince <= maximumDirectTouchMute;
            DFMobilePointerSetDirectTouchActive(directTouchActive);
            directTouchInputActive = directTouchActive;
            if (directTouchActive)
                inputSystemEventDelta = Vector2.zero;

            // A real finger takes ownership immediately. Do not first publish the
            // still-live Magic Keyboard pointer: MobileInput.UpdatePointer preserves
            // an active pointer until it is explicitly relinquished, and publishing
            // it here leaves the HUD hidden while the finger is already on the glass.
            // The direct-touch path below will process the same touch and lets the
            // joystick/look-zone ownership rules decide where it belongs.
            if (directTouchPresent)
            {
                touchOwnsPointer = true;
                if (MobileInput.PointerActive)
                {
                    MobileInput.PointerActive = false;
                    MobileInput.PointerDelta = Vector2.zero;
                    ApplyHudVisibility();
                }
                pointerRead = false;
            }

            // The native bridge can still report the pointer as active after a
            // finger is lifted, but its position and delta are merely the last
            // parked sample. Keep touch ownership until real hardware activity
            // proves that the user intentionally returned to the trackpad.
            if (!directTouchPresent && touchOwnsPointer)
            {
                bool hardwareResumed = pointerRead &&
                    (new Vector2(nativeDeltaX, nativeDeltaY).sqrMagnitude > 0f ||
                     nativeButtonHeld || nativeSecondaryHeld);
                if (hardwareResumed)
                    touchOwnsPointer = false;
                else
                    pointerRead = false;
            }

            if (pointerRead)
            {
                MobileInput.PointerAtEdge = nativeAtEdge;
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    // UIKit owns the cursor lock, but Unity's Input System may be the
                    // component that receives the relative HID delta. The native hover
                    // bridge still supplies the stable position and button state; merge
                    // the Input System delta instead of returning before it is consumed.
                    Vector2 systemDelta = Mouse.current.delta.ReadValue();
                    systemDelta += inputSystemEventDelta;
                    inputSystemEventDelta = Vector2.zero;
                    if (systemDelta.sqrMagnitude > 0f)
                    {
                        nativeDeltaX += systemDelta.x;
                        nativeDeltaY += systemDelta.y;
                    }
                    nativeButtonHeld |= Mouse.current.leftButton.isPressed;
                    // Fallback only. GCMouse is the authoritative source for both
                    // buttons because it reports them while the pointer is locked,
                    // but GCMouse.current can be nil until the trackpad is first
                    // actuated - and on any setup where it never arrives, this keeps
                    // the swing button working the way it did before.
                    nativeSecondaryHeld |= Mouse.current.rightButton.isPressed;
                }
#else
                nativeButtonHeld |= Input.GetMouseButton(0);
                nativeSecondaryHeld |= Input.GetMouseButton(1);
#endif
                MobileInput.UpdatePointer(new Vector2(nativeX, nativeY),
                    EdgeLookDelta(new Vector2(nativeX, nativeY),
                    new Vector2(nativeDeltaX, nativeDeltaY)), true, nativeButtonHeld,
                    nativeSecondaryHeld);
                LogPointerDiagnostics(nativeDeltaX, nativeDeltaY, nativeButtonHeld);
                if (!wasActive)
                    ApplyHudVisibility();

                // Falling through to the Input System path below overwrites the pointer
                // with an absolute screen position, which is only correct while a finger
                // is driving it. Reuse the live-touch test from above rather than
                // repeating it: an ended touch counted here would hand the camera a
                // stale position for a frame.
                if (!directTouchPresent)
                    return;

            }
#endif

#if ENABLE_INPUT_SYSTEM
            Mouse systemMouse = Mouse.current;
#if UNITY_IOS && !UNITY_EDITOR
            // A detached Magic Keyboard keeps reporting Mouse.current as "added" -
            // UIKit never unmounts the phantom pointer - and this block used to claim
            // ownership every frame on that alone, returning before the direct-touch
            // handover below could ever run. A live finger on the glass is the
            // strongest signal: if the native bridge saw one, this Input System path
            // must not claim. Absent a finger, the pointer still has to show recent
            // activity (updated this frame, or an event within pointerIdleTimeout)
            // or it is treated as gone.
            bool inputSystemFresh = systemMouse != null && (systemMouse.wasUpdatedThisFrame ||
                (lastInputSystemEventAt != float.MinValue &&
                 Time.unscaledTime - lastInputSystemEventAt <= pointerIdleTimeout));
            if (systemMouse != null && systemMouse.added && inputSystemFresh &&
                !directTouchPresent && !touchOwnsPointer)
#else
            if (systemMouse != null && systemMouse.added)
#endif
            {
                Vector2 systemPosition = systemMouse.position.ReadValue();
                Vector2 systemDelta = pointerPositionInitialized
                    ? systemPosition - previousPointerPosition
                    : Vector2.zero;
                systemDelta += inputSystemEventDelta;
                inputSystemEventDelta = Vector2.zero;
                previousPointerPosition = systemPosition;
                pointerPositionInitialized = true;
                indirectPointerActive = false;
                MobileInput.UpdatePointer(systemPosition, systemDelta, true, systemMouse.leftButton.isPressed);
                LogPointerDiagnostics(systemDelta.x, systemDelta.y, systemMouse.leftButton.isPressed);
                if (!wasActive)
                    ApplyHudVisibility();
                return;
            }
#endif

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch indirect = Input.GetTouch(i);
                if (indirect.type != TouchType.Indirect)
                    continue;

                Vector2 pointerPosition = indirect.position;
                Vector2 indirectDelta = pointerPositionInitialized
                    ? pointerPosition - previousPointerPosition
                    : Vector2.zero;
                previousPointerPosition = pointerPosition;
                pointerPositionInitialized = true;
                indirectPointerActive = true;

                bool buttonHeld = indirect.phase == UnityEngine.TouchPhase.Began ||
                                  (indirect.phase == UnityEngine.TouchPhase.Moved && MobileInput.GetPointerButton()) ||
                                  (indirect.phase == UnityEngine.TouchPhase.Stationary && MobileInput.GetPointerButton());
                MobileInput.UpdatePointer(pointerPosition, indirectDelta, true, buttonHeld);
                if (!wasActive)
                    ApplyHudVisibility();
                return;
            }

            if (Input.touchCount > 0)
            {
                bool directTouchBegan = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.type == TouchType.Indirect)
                        continue;
#if UNITY_IOS && !UNITY_EDITOR
                    // Unity calls the trackpad's own click a direct touch, so without
                    // the native verdict a press of the trackpad button relinquished
                    // the pointer below - which drops the UIKit lock and puts the
                    // system cursor back on screen for the length of a weapon swing.
                    if (!nativeDirectTouchPresent)
                        continue;
#endif
                    directTouchBegan |= touch.phase == UnityEngine.TouchPhase.Began;
                    break;
                }

                // ONLY HAND THE POINTER TO A FINGER THAT SOMETHING WILL DRIVE.
                //
                // Relinquishing below is a handover to the touch layer, and the touch
                // layer stands down with the touch controls - PollCursorStage is what
                // polls the virtual cursor, and it returns early when they are off. So
                // with them off this handed the classic UI a cursor nothing moved:
                // frozen where CentreCursor left it, at screen centre, with buttons that
                // never latch. Brushing the glass while reading a menu was enough to stop
                // that menu responding until the trackpad was moved again.
                //
                // Also keeps ownership through the short gap after an indirect trackpad
                // click. iPadOS can leave the touch record alive for a frame after
                // release; handing it back immediately makes the next hover invisible
                // until another click occurs.
                if (wasActive && (!directTouchBegan || !touchUIEnabled))
                {
                    Vector2 fallbackPosition = Input.mousePosition;
                    Vector2 fallbackDelta = pointerPositionInitialized
                        ? fallbackPosition - previousPointerPosition
                        : Vector2.zero;
                    previousPointerPosition = fallbackPosition;
                    pointerPositionInitialized = true;
                    MobileInput.UpdatePointer(fallbackPosition, fallbackDelta, true, false);
                    return;
                }

                MobileInput.PointerActive = false;
                MobileInput.PointerDelta = Vector2.zero;
                MobileInput.UpdatePointer(Vector2.zero, Vector2.zero, false, false);
                pointerPositionInitialized = false;
                indirectPointerActive = false;
                if (wasActive)
                    ApplyHudVisibility();
                return;
            }

#if ENABLE_INPUT_SYSTEM && UNITY_IOS && !UNITY_EDITOR
            // The parked position of a phantom pointer (keyboard detached) freezes on
            // screen here; without this, menus keep a dead cursor and gameplay keeps
            // edge-drifting until a finger lands. Once the input system has been
            // silent past pointerIdleTimeout, treat the pointer as gone so the layer
            // can hand back to touch.
            if (systemMouse != null && !inputSystemFresh)
            {
                pointerPositionInitialized = false;
                indirectPointerActive = false;
                if (wasActive)
                {
                    MobileInput.PointerActive = false;
                    ApplyHudVisibility();
                }
                return;
            }
#endif
            Vector2 position = Input.mousePosition;
            Vector2 delta = pointerPositionInitialized ? position - previousPointerPosition : Vector2.zero;
            previousPointerPosition = position;
            pointerPositionInitialized = true;

            bool moved = delta.sqrMagnitude > 0.01f;
            bool buttonEvent = Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0) ||
                               Input.GetMouseButtonDown(1) || Input.GetMouseButtonUp(1);
            if (moved || buttonEvent)
                MobileInput.PointerActive = true;

            MobileInput.UpdatePointer(position, delta, MobileInput.PointerActive,
                MobileInput.PointerActive && !indirectPointerActive && Input.GetMouseButton(0));
            if (wasActive != MobileInput.PointerActive)
                ApplyHudVisibility();
        }

        int pointerDiagnosticFrames;
        string pointerDiagnosticPath;
        float nextMenuDiagnostic;
        float nextTouchDiagnostic;
        int menuFrames;
        int menuHeldFrames;
        int menuDownEdges;

        /// <summary>
        /// One line a second while a classic menu owns the screen, into the same file as
        /// the pointer diagnostics.
        ///
        /// A window that takes no input from touch OR from the trackpad is not an input
        /// problem in either device - both arrive through InputManager.MousePosition and
        /// GetMouseButton, so this records what those are actually reporting, and which
        /// window is on top to receive them. A top window that is not the one on screen,
        /// a frozen cursor position, or buttons that never read down each point somewhere
        /// different.
        /// </summary>
        /// <summary>
        /// Gameplay-side input state, once a second, into the pointer diagnostics file.
        ///
        /// SEPARATE FROM LogPointerDiagnostics ON PURPOSE. That one is called from inside
        /// the `if (pointerRead)` branch of PollHardwarePointer, and touchOwnsPointer
        /// forces pointerRead false for exactly as long as a finger owns the pointer. So
        /// it goes silent during the touch period - the capture has a forty-second hole
        /// where the touch handover happens, and every touch field it prints is sampled
        /// only while the trackpad is live, where they are all legitimately false. It
        /// cannot see a touch bug. This runs every frame regardless of who owns what.
        ///
        /// The fields are chosen to separate the candidates for a dead camera stick:
        /// whether the stick claimed a finger at all (stickFinger), whether the look zone
        /// took it instead (zoneFinger), whether the zone is reporting a drag it cannot
        /// deliver motion for (zonePointer set while zoneFinger is not), and whether a
        /// claim leaked across the handover (claimed outliving touches).
        /// </summary>
        void LogTouchDiagnostics()
        {
            if (IsClassicMenuOpen() || !MobileInput.Enabled)
            {
                nextTouchDiagnostic = 0f;
                return;
            }

            if (Time.unscaledTime < nextTouchDiagnostic)
                return;

            nextTouchDiagnostic = Time.unscaledTime + 1f;

            string phases = string.Empty;
            for (int i = 0; i < Input.touchCount && i < 4; i++)
            {
                UnityEngine.Touch t = Input.GetTouch(i);
                phases += string.Format("{0}{1}:{2}@{3:0},{4:0}", i > 0 ? "," : string.Empty,
                    t.fingerId, t.phase, t.position.x, t.position.y);
            }
            if (phases.Length == 0)
                phases = "-";

            AppendPointerDiagnostic(string.Format(
                "{0:O} TOUCH mode={1} touchUI={2} keyboard={3} controller={4} pointerActive={5} " +
                "touchOwns={6} hudGameplay={7} touches={8} [{9}] claimed={10} " +
                "lookHeld={11} lookValue=({12:0.##},{13:0.##}) lookFinger={14} lookPointer={15} " +
                "moveHeld={16} moveFinger={17} " +
                "zoneDragging={18} zoneFinger={19} zonePointer={20} combat={21}\n",
                System.DateTime.UtcNow, MobileInput.Mode, touchUIEnabled, keyboardActive,
                controllerConnected, MobileInput.PointerActive, touchOwnsPointer,
                gameplayLayer != null && gameplayLayer.activeSelf, Input.touchCount, phases,
                VirtualJoystick.ClaimedFingerCount,
                lookJoystick != null && lookJoystick.IsHeld,
                lookJoystick != null ? lookJoystick.Value.x : 0f,
                lookJoystick != null ? lookJoystick.Value.y : 0f,
                lookJoystick != null ? lookJoystick.DirectFingerId : -99,
                lookJoystick != null ? lookJoystick.PointerId : -99,
                moveJoystick != null && moveJoystick.IsHeld,
                moveJoystick != null ? moveJoystick.DirectFingerId : -99,
                lookZone != null && lookZone.IsDragging,
                lookZone != null ? lookZone.DirectFingerId : -99,
                lookZone != null ? lookZone.PointerId : -99,
                MobileInput.CombatMode));
        }

        void LogMenuDiagnostics()
        {
            if (!IsClassicMenuOpen())
            {
                nextMenuDiagnostic = 0f;
                menuFrames = 0;
                menuHeldFrames = 0;
                menuDownEdges = 0;
                return;
            }

            // COUNTED EVERY FRAME, NOT SAMPLED.
            //
            // A click is one frame of GetMouseButtonDown, and that is exactly what
            // BaseScreenComponent turns into OnMouseClick. Sampling once a second cannot
            // see an edge, so count them: the last capture showed the pointer sitting on
            // the exit button with the button held, which narrows the fault to whether
            // the down edge ever arrives. downEdges staying at 0 while heldFrames climbs
            // says it does not.
            menuFrames++;
            if (InputManager.HasInstance)
            {
                if (InputManager.Instance.GetMouseButton(0))
                    menuHeldFrames++;
                if (InputManager.Instance.GetMouseButtonDown(0))
                    menuDownEdges++;
            }

            if (Time.unscaledTime < nextMenuDiagnostic)
                return;

            nextMenuDiagnostic = Time.unscaledTime + 1f;

            string topWindowName = "none";
            int windowCount = 0;
            bool topSetup = false;
            int topComponents = -1;
            if (DaggerfallUI.HasInstance && DaggerfallUI.UIManager != null)
            {
                windowCount = DaggerfallUI.UIManager.WindowCount;
                IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;
                if (topWindow != null)
                    topWindowName = topWindow.GetType().Name;

                // The direct measure of a window rebuilding itself. Setup() runs once and
                // only once, so a component count that climbs frame over frame means it is
                // being called again - and a window that never finishes setup never runs
                // base.Update(), which is what feeds mouse events to its controls.
                DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallBaseWindow baseWindow =
                    topWindow as DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallBaseWindow;
                if (baseWindow != null)
                {
                    topSetup = baseWindow.IsSetup;
                    if (baseWindow.NativePanel != null)
                        topComponents = baseWindow.NativePanel.Components.Count;
                }
            }

            Vector3 mousePosition = Vector3.zero;
            bool mouseHeld = false, mouseDown = false;
            if (InputManager.HasInstance)
            {
                mousePosition = InputManager.Instance.MousePosition;
                mouseHeld = InputManager.Instance.GetMouseButton(0);
                mouseDown = InputManager.Instance.GetMouseButtonDown(0);
            }

            AppendPointerDiagnostic(string.Format(
                "{0:O} MENU top={1} windows={2} mode={3} virtualCursor={4} pointer={5} " +
                "touch={6} mouse=({7:0.#},{8:0.#}) held={9} down={10} cursor=({11:0.#},{12:0.#}) " +
                "screen=({13}x{14}) touches={15} paused={16} " +
                "frames={17} heldFrames={18} downEdges={19} setup={20} components={21}\n",
                System.DateTime.UtcNow, topWindowName, windowCount, MobileInput.Mode,
                MobileInput.VirtualCursorActive, MobileInput.PointerActive,
                MobileInput.TouchInputActive, mousePosition.x, mousePosition.y,
                mouseHeld, mouseDown, MobileInput.CursorPosition.x, MobileInput.CursorPosition.y,
                Screen.width, Screen.height, Input.touchCount,
                GameManager.HasInstance && GameManager.IsGamePaused,
                menuFrames, menuHeldFrames, menuDownEdges, topSetup, topComponents));
        }

        void LogPointerDiagnostics(float deltaX, float deltaY, bool buttonHeld)
        {
            if (++pointerDiagnosticFrames % 60 != 0)
                return;

#if UNITY_IOS && !UNITY_EDITOR
            uint windowEvents, indirectTouches, nonZeroDeltas, hoverEvents, gameControllerDeltas;
            uint lockRecoveries, directTouches, styleRequests, unlocksWhileHeld, unlocksWhileIdle;
            int lastEventType;
            bool locked;
            DFMobilePointerDiagnostics(out windowEvents, out indirectTouches, out nonZeroDeltas,
                                       out hoverEvents, out gameControllerDeltas,
                                       out lastEventType, out locked, out lockRecoveries,
                                       out directTouches, out styleRequests,
                                       out unlocksWhileHeld, out unlocksWhileIdle);
            Debug.Log(string.Format(
                "[DFPointerDiag] lock={0} native/systemDelta=({1:0.##},{2:0.##}) held={3} " +
                "windowEvents={4} indirectTouches={5} nonZero={6} hover={7} lastEventType={8} " +
                "rawAxes=({9:0.##},{10:0.##}) lockRecoveries={11}",
                locked, deltaX, deltaY, buttonHeld, windowEvents, indirectTouches,
                nonZeroDeltas, hoverEvents, lastEventType,
                Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), lockRecoveries));

            string line = string.Format(
                "{0:O} lock={1} delta=({2:0.##},{3:0.##}) held={4} windowEvents={5} " +
                "indirectTouches={6} nonZero={7} hover={8} gcDeltas={9} lastEventType={10} " +
                "rawAxes=({11:0.##},{12:0.##}) inputSystemEvents={13} inputSystemNonZero={14} " +
                "queuedDelta=({15:0.##},{16:0.##}) atEdge={17} directTouch={18} lockRecoveries={19} " +
                 "swing={20} directTouches={21} styleRequests={22} unlockHeld={23} unlockIdle={24} " +
                 "mode={25} touchOwnsPointer={26} lookHeld={27} lookValue=({28:0.##},{29:0.##}) " +
                 "lookZoneDragging={30}\n",
                System.DateTime.UtcNow, locked, deltaX, deltaY, buttonHeld, windowEvents,
                indirectTouches, nonZeroDeltas, hoverEvents, gameControllerDeltas, lastEventType,
                Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), inputSystemMouseEvents,
                inputSystemNonZeroMouseEvents, inputSystemEventDelta.x, inputSystemEventDelta.y,
                MobileInput.PointerAtEdge, directTouchInputActive, lockRecoveries,
                MobileInput.GetPointerSecondaryButton(), directTouches, styleRequests,
                 unlocksWhileHeld, unlocksWhileIdle, MobileInput.Mode, touchOwnsPointer,
                 lookJoystick != null && lookJoystick.IsHeld,
                 lookJoystick != null ? lookJoystick.Value.x : 0f,
                 lookJoystick != null ? lookJoystick.Value.y : 0f,
                 lookZone != null && lookZone.IsDragging);
            AppendPointerDiagnostic(line);
            Debug.Log(string.Format("[DFPointerDiag] inputSystemEvents={0} inputSystemNonZero={1} queuedDelta=({2:0.##},{3:0.##})",
                inputSystemMouseEvents, inputSystemNonZeroMouseEvents,
                inputSystemEventDelta.x, inputSystemEventDelta.y));
        #endif
        }

        void AppendPointerDiagnostic(string line)
        {
            try
            {
                if (string.IsNullOrEmpty(pointerDiagnosticPath))
                {
                    // UIFileSharingEnabled/LSSupportsOpeningDocumentsInPlace expose
                    // persistentDataPath's Documents directory in the Files app.
                    pointerDiagnosticPath = Path.Combine(Application.persistentDataPath,
                                                         "DaggerfallPointerDiagnostics.log");
                    File.WriteAllText(pointerDiagnosticPath,
                        "Daggerfall Unity pointer diagnostics\n");
                    Debug.Log("[DFPointerDiag] file=" + pointerDiagnosticPath);
                }

                File.AppendAllText(pointerDiagnosticPath, line);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[DFPointerDiag] file write failed: " + exception.Message);
            }
        }

        Vector2 EdgeLookDelta(Vector2 position, Vector2 delta)
        {
            // A lock/unlock transition can report the distance between the old
            // absolute hover position and the new scene position as one frame of
            // movement. It is not user input; passing it to PlayerMouseLook sends
            // pitch straight to the upper clamp and looks like a camera reset.
            const float maximumPointerDelta = 250f;
            if (delta.sqrMagnitude > maximumPointerDelta * maximumPointerDelta)
            {
                lastPointerDirection = Vector2.zero;
                return Vector2.zero;
            }

            if (delta.sqrMagnitude > 0.01f)
                lastPointerDirection = new Vector2(Mathf.Sign(delta.x), Mathf.Sign(delta.y));

            if (delta.sqrMagnitude > 0.01f || IsClassicMenuOpen())
                return delta;

            // Only turn the camera for a pointer UIKit is really holding against the
            // edge. While the pointer is locked its reported position is frozen
            // wherever the lock caught it, so trusting the position alone spun the
            // view on its own whenever that happened to be a border pixel.
            if (!MobileInput.PointerAtEdge)
                return delta;

            bool atHorizontalEdge = position.x <= pointerEdgeMargin ||
                                    position.x >= Screen.width - pointerEdgeMargin;
            bool atVerticalEdge = position.y <= pointerEdgeMargin ||
                                  position.y >= Screen.height - pointerEdgeMargin;
            if (!atHorizontalEdge && !atVerticalEdge)
                return delta;

            Vector2 edgeDelta = Vector2.zero;
            if (atHorizontalEdge)
                edgeDelta.x = lastPointerDirection.x * pointerEdgeLookSpeed * Time.unscaledDeltaTime;
            if (atVerticalEdge)
                edgeDelta.y = lastPointerDirection.y * pointerEdgeLookSpeed * Time.unscaledDeltaTime;
            return edgeDelta;
        }

        /// <summary>
        /// Hold-to-skip for intro and cutscene videos. DaggerfallVidPlayerWindow exits on
        /// InputManager.GetBackButtonDown(), which touch had no way to press - keyboard
        /// and gamepad players could skip, touch players sat through everything.
        /// </summary>
        void PollVideoSkip()
        {
            bool videoOnTop = DaggerfallUI.HasInstance && DaggerfallUI.UIManager != null &&
                DaggerfallUI.UIManager.TopWindow is DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallVidPlayerWindow;

            if (!videoOnTop)
            {
                vidHoldStart = -1f;
                vidSkipQueued = false;
                vidLastTapTime = -1f;
                return;
            }

            // Finger off the glass: stop the hold timer and allow a fresh skip decision,
            // but KEEP vidLastTapTime - a double-tap must survive the gap between taps.
            // Wiping it here is why the first double-tap implementation never landed on
            // device: the reset ran on the frame between the two taps, so the second tap
            // always saw the timestamp as -1.
            if (Input.touchCount == 0)
            {
                vidHoldStart = -1f;
                vidSkipQueued = false;
                return;
            }

            // Double-tap: two taps inside the window skip. Counted on the Began phase so
            // a finger held on the glass does not keep re-arming the window frame after
            // frame - only a new touch counts as a tap.
            if (Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                if (!vidSkipQueued && vidLastTapTime >= 0f &&
                    Time.unscaledTime - vidLastTapTime <= doubleTapToSkipWindow)
                {
                    vidSkipQueued = true;
                    MobileInput.QueueBack();
                    Debug.Log("[MobileInput] video skipped by touch double-tap");
                }
                vidLastTapTime = Time.unscaledTime;
            }

            if (vidHoldStart < 0f)
                vidHoldStart = Time.unscaledTime;

            if (!vidSkipQueued && Time.unscaledTime - vidHoldStart >= holdToSkipVideoSeconds)
            {
                vidSkipQueued = true;
                MobileInput.QueueBack();
                Debug.Log("[MobileInput] video skipped by touch hold");
            }
        }

        /// <summary>
        /// Called from InputManager.Update() after the vanilla mouse axes are collected
        /// and before FindKeyboardActions/UpdateLook/ApplyFriction consume them.
        /// </summary>
        public void PollGameplayStage(InputManager inputManager)
        {
            if (inputManager == null || MobileInput.MenuMode || controllerConnected)
                return;

            if (MobileInput.PointerActive)
            {
                Vector2 pointerDelta = MobileInput.PointerDelta;
                if (pointerDelta.sqrMagnitude > 0f)
                    inputManager.SetMobileMouseAxes(pointerDelta.x, pointerDelta.y);
                if (pointerDiagnosticFrames % 60 == 0)
                    Debug.Log(string.Format("[DFPointerDiag] gameplayDelta=({0:0.##},{1:0.##}) finalLook=({2:0.##},{3:0.##})",
                        pointerDelta.x, pointerDelta.y, inputManager.LookX, inputManager.LookY));
                return;
            }

            if (!touchUIEnabled || keyboardActive)
                return;

            PumpMovement(inputManager);
            PumpLookAndGesture(inputManager);
            PumpActions(inputManager);
        }

        void PumpMovement(InputManager inputManager)
        {
            Vector2 axis = (moveJoystick != null) ? moveJoystick.Value : Vector2.zero;

            // A journey drives movement itself, holding forward along its own bearing. The
            // move stick has to stand down for the same reason the look zone does: both end
            // up calling ApplyVerticalForce, and a thumb resting off-centre would walk the
            // player off course for the whole trip.
            //
            // Deliberately NOT wired as "push to cancel" here. Interrupting a journey is the
            // travel controller's decision, not the input layer's - it has to stop the clock,
            // release the camera and offer to resume, none of which belongs in a pump.
            if (MobileJourneyPilot.Active)
                return;

            // Use the engine's own force API so the impulse flags and ApplyFriction()
            // keep working. Skipping the call when an axis is zero is what lets friction
            // decelerate the player normally.
            if (!Mathf.Approximately(axis.x, 0f))
                inputManager.ApplyHorizontalForce(axis.x);
            if (!Mathf.Approximately(axis.y, 0f))
                inputManager.ApplyVerticalForce(axis.y);

            if (axis.magnitude >= runThreshold)
                inputManager.AddAction(InputManager.Actions.Run);

            // PlayerMotor and several systems test for discrete move actions rather
            // than reading the axes, so raise those too.
            const float actionDeadZone = 0.2f;
            if (axis.y > actionDeadZone) inputManager.AddAction(InputManager.Actions.MoveForwards);
            if (axis.y < -actionDeadZone) inputManager.AddAction(InputManager.Actions.MoveBackwards);
            if (axis.x < -actionDeadZone) inputManager.AddAction(InputManager.Actions.MoveLeft);
            if (axis.x > actionDeadZone) inputManager.AddAction(InputManager.Actions.MoveRight);
        }

        void PumpLookAndGesture(InputManager inputManager)
        {
            if (lookZone == null)
                return;

            // A JOURNEY OWNS THE CAMERA WHILE IT RUNS.
            //
            // MobileJourneyPilot sets the body's yaw outright to steer toward the
            // destination. If the look zone kept feeding mouseX/mouseY at the same time, the
            // two would fight every frame and the thumb would win - the player would drift
            // off course by resting a finger on the screen. The delta is still CONSUMED so
            // it does not pool up and fire as one large jerk the moment the journey ends.
            if (MobileJourneyPilot.Active)
            {
                lookZone.ConsumeDelta();
                return;
            }

            Vector2 delta = lookZone.ConsumeDelta() * touchToMouseScale;

            // The right stick is camera-only. Keep this explicit rather than relying
            // solely on the look zone winning its touch race: an active look stick
            // must never open the swipe/bow gesture channel, even if another update
            // path still reports the full-screen zone as dragging for one frame.
            bool lookStickHeld = lookJoystick != null && lookJoystick.IsHeld;

            // Swing state first, so the stick's look contribution can be gated on it.
            // SWIPES ARE THE ONLY ATTACK INPUT - the right stick never swings. (Flick-to-
            // swing existed briefly and device feedback was unanimous: the stick should
            // only ever be a camera.)
            bool combat = MobileInput.CombatMode;
            bool bow = BowEquipped;

            // A BOW IS DRAWN BY HOLDING THE VIEW, AND AIMED WITH THE SAME THUMB.
            //
            // Touch and hold anywhere on the view to pull the bow back, move that finger
            // (or the look stick) to aim while it is drawn, lift to loose the arrow. It is
            // how a bow actually works, and it needs no button.
            //
            // Bows are excluded from the swipe path entirely. WeaponManager keys a bow off
            // Actions.SwingWeapon being HELD, so under swipe rules the AIMING gesture was
            // the trigger - drag to line up, the 0.12s flick extension lapsed, an arrow
            // left, repeatedly (device report: "it just shoots non stop when trying to
            // aim"). The extension is what makes a flick resolve for a blade; for a bow it
            // is exactly wrong, because release must be immediate and deliberate.
            bool bowFingerDown = combat && bow && lookZone.IsDragging && !lookStickHeld;
            if (bowFingerDown)
            {
                if (bowHoldStart < 0f)
                    bowHoldStart = Time.unscaledTime;
            }
            else
            {
                bowHoldStart = -1f;
            }

            bool bowDrawing = bowFingerDown &&
                              Time.unscaledTime - bowHoldStart >= bowDrawMinHold;

            if (combat && !bow && lookZone.IsDragging && !lookStickHeld)
                swingHoldUntil = Time.unscaledTime + swingHoldExtension;

            // Swipe-swing (blades) and bow-draw are separate states: only the swipe one
            // may suppress the look stick.
            bool swipeWindow = combat && !bow && !lookStickHeld &&
                               Time.unscaledTime < swingHoldUntil;
            bool swingWindow = swipeWindow || bowDrawing;

            // Right stick: rate-based look, always and only. Excluded while a swipe-swing
            // is live so the aiming thumb cannot contaminate the attack direction.
            // Excluded during a swipe so the aiming thumb cannot contaminate the attack
            // direction - but a bow has no direction to contaminate, and aiming while drawn
            // is the whole point, so the stick stays live for it.
            if (lookStickHeld && !swipeWindow)
            {
                Vector2 stick = lookJoystick.Value;
                stick = new Vector2(stick.x * Mathf.Abs(stick.x), stick.y * Mathf.Abs(stick.y));

                if (lookZone != null && lookZone.invertY)
                    stick.y = -stick.y;

                delta += stick * lookStickSpeed * Time.unscaledDeltaTime;
            }

            // ALWAYS overwrite on touch devices - including with zero. InputManager reads
            // Input.GetAxisRaw("Mouse X/Y") every frame before this runs, and on iOS the
            // primary touch's movement feeds those axes. Overwriting only when our own
            // channel was non-zero let that raw delta survive, so dragging the MOVE stick
            // turned the camera. (Device log proved the stick claims were clean - this
            // was the only remaining feeder.) In the editor, keep the old conditional so
            // mouse-look still works for desktop testing.
            if (Input.touchSupported && !Application.isEditor)
                inputManager.SetMobileMouseAxes(delta.x, delta.y);
            else if (delta.sqrMagnitude > 0f)
                inputManager.SetMobileMouseAxes(delta.x, delta.y);

            // Holding SwingWeapon is what enables WeaponManager's gesture tracking.
            // Releasing it clears the gesture and, for bows, fires the arrow.
            if (swingWindow)
                inputManager.AddAction(InputManager.Actions.SwingWeapon);
        }

        void PumpActions(InputManager inputManager)
        {
            foreach (InputManager.Actions action in heldActions)
                inputManager.AddAction(action);

            if (tapActions.Count == 0)
                return;

            scratch.Clear();
            foreach (KeyValuePair<InputManager.Actions, int> kvp in tapActions)
            {
                inputManager.AddAction(kvp.Key);
                scratch.Add(kvp.Key);
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                InputManager.Actions action = scratch[i];
                int remaining = tapActions[action] - 1;
                if (remaining <= 0)
                    tapActions.Remove(action);
                else
                    tapActions[action] = remaining;
            }
        }

        #endregion

        #region Calibration

        /// <summary>
        /// WeaponManager compares accumulated gesture travel (in mouse-axis units)
        /// against AttackThreshold * longestScreenDimension. Touch deltas arrive in
        /// pixels scaled by touchToMouseScale, so:
        ///
        ///   requiredPixels = AttackThreshold * longestDim / touchToMouseScale
        ///
        /// Solving for a target swipe length as a fraction of the screen:
        ///
        ///   AttackThreshold = swipeScreenFraction * touchToMouseScale
        ///
        /// This keeps swipe length independent of look sensitivity - retune one without
        /// breaking the other.
        /// </summary>
        /// <summary>
        /// Pure form of the threshold maths, separated so it can be verified without a
        /// scene, a WeaponManager or a device.
        ///
        /// WeaponManager requires travelInMouseUnits >= AttackThreshold * longestDim, and
        /// travelInMouseUnits = pixels * scale. Solving for a target physical distance:
        ///     AttackThreshold = inches * dpi * scale / longestDim
        /// </summary>
        public static float ComputeAttackThreshold(float swipeInches, float scale, float dpi, float longestDim)
        {
            return (swipeInches * dpi * scale) / Mathf.Max(longestDim, 1f);
        }

        /// <summary>Inverse of ComputeAttackThreshold: how far the finger must actually travel.</summary>
        public static float RequiredSwipePixels(float threshold, float scale, float longestDim)
        {
            if (scale <= 0f)
                return 0f;
            return threshold * longestDim / scale;
        }

        /// <summary>
        /// Recompute the swipe threshold. Public because the tuning panel changes both
        /// swipeDistanceInches and touchToMouseScale live, and both feed the calculation.
        /// </summary>
        public void RefreshAttackThreshold()
        {
            ApplyAttackThreshold();
        }

        void ApplyAttackThreshold()
        {
            if (!GameManager.HasInstance)
                return;

            WeaponManager weaponManager = GameManager.Instance.WeaponManager;
            if (weaponManager == null)
                return;

            // WeaponManager needs: travelInMouseUnits >= AttackThreshold * longestScreenDim,
            // and travelInMouseUnits = pixels * touchToMouseScale. So for a target physical
            // distance:  AttackThreshold = inches * dpi * touchToMouseScale / longestDim
            weaponManager.AttackThreshold = useScreenFractionInsteadOfInches
                ? swipeScreenFraction * touchToMouseScale
                : ComputeAttackThreshold(swipeDistanceInches, touchToMouseScale,
                                         MobileInput.Dpi, Mathf.Max(Screen.width, Screen.height));

            thresholdApplied = true;
            appliedScreenWidth = Screen.width;
            appliedScreenHeight = Screen.height;
        }

        #endregion
    }
}
