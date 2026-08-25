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
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileInputController : MonoBehaviour
    {
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
        [Tooltip("Hide the touch HUD when a hardware keyboard is used, the same way a gamepad " +
                 "does. Touching the screen brings it back.")]
        public bool autoHideOnKeyboard = true;

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
        float vidHoldStart = -1f;
        bool vidSkipQueued;
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
            MobileInput.Enabled = true;

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
            // bindings serve no purpose (the touch layer injects actions directly), so
            // clear them for this session. KeyBinds.txt on disk is not modified.
            if (Input.touchSupported && !Application.isEditor && InputManager.HasInstance)
            {
                ClearPhantomProneBindings();
            }
        }

        /// <summary>
        /// On touch devices, remove every mouse and joystick-button keybind, primary AND
        /// secondary, then force the private binding cache to rebuild.
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
        /// Scope note: this sweeps Mouse0-6 and JoystickButton0-19 only. The gamepad layout
        /// also binds InputManager's synthetic axis keycodes (5000+) and combo keycodes
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

            var prone = new List<KeyCode>
            {
                KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2,
                KeyCode.Mouse3, KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6,
            };
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
            // Hand the pointer back, otherwise VirtualCursorActive can stay true with
            // nothing driving it and the classic UI is left with a frozen cursor.
            MobileInput.Relinquish();
            RestoreEngineTouchDefaults();
        }

        void OnDestroy()
        {
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

            // Any touch means the player is back on the glass.
            if (Input.touchCount > 0)
            {
                if (keyboardActive)
                    SetKeyboardActive(false);
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

            // SELF-HEALING BINDING GUARD. Each scene carries an InputManager whose Start()
            // loads KeyBinds.txt, and Unity does not order it against our Start() - so the
            // phantom-prone mouse binding can resurrect AFTER our clear, nondeterministically
            // per launch (which is why the door worked exactly once). Instead of winning an
            // ordering race, detect any resurrection on this cadence and re-clear.
            if (Input.touchSupported && !Application.isEditor && !controllerConnected &&
                InputManager.HasInstance)
            {
                InputManager imGuard = InputManager.Instance;
                if (imGuard.GetBinding(InputManager.Actions.ActivateCenterObject, true) != KeyCode.None ||
                    imGuard.GetBinding(InputManager.Actions.ActivateCenterObject, false) != KeyCode.None ||
                    imGuard.GetBinding(InputManager.Actions.SwingWeapon, true) == KeyCode.Mouse1)
                {
                    Debug.Log("[MobileInput] phantom-prone bindings RESURRECTED (scene InputManager reloaded keybinds) - re-clearing");
                    ClearPhantomProneBindings();
                }
            }

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
            bool touchAllowed = !controllerConnected && !keyboardActive;
            bool menu = MobileInput.Mode == MobileControlMode.Menu;

            SetLayer(gameplayLayer, touchAllowed && !menu);
            SetLayer(menuLayer, touchAllowed && menu);
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
            if (controllerConnected || keyboardActive)
                return;

            if (virtualMouse != null && MobileInput.MenuMode)
            {
                PollVideoSkip();
                virtualMouse.PollTouches();
            }
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

            if (!videoOnTop || Input.touchCount == 0)
            {
                vidHoldStart = -1f;
                vidSkipQueued = false;
                return;
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
            if (inputManager == null || MobileInput.MenuMode || controllerConnected || keyboardActive)
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
            bool bowFingerDown = combat && bow && lookZone.IsDragging;
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

            if (combat && !bow && lookZone.IsDragging)
                swingHoldUntil = Time.unscaledTime + swingHoldExtension;

            // Swipe-swing (blades) and bow-draw are separate states: only the swipe one
            // may suppress the look stick.
            bool swipeWindow = combat && !bow && Time.unscaledTime < swingHoldUntil;
            bool swingWindow = swipeWindow || bowDrawing;

            // Right stick: rate-based look, always and only. Excluded while a swipe-swing
            // is live so the aiming thumb cannot contaminate the attack direction.
            // Excluded during a swipe so the aiming thumb cannot contaminate the attack
            // direction - but a bow has no direction to contaminate, and aiming while drawn
            // is the whole point, so the stick stays live for it.
            if (lookJoystick != null && lookJoystick.IsHeld && !swipeWindow)
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
