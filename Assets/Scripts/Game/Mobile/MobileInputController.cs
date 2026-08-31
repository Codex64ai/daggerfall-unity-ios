// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The port's input hub. One MonoBehaviour in the game scene that decides, every frame, WHO
//   IS DRIVING and feeds Daggerfall Unity's InputManager accordingly:
//
//     touch      - virtual sticks, swipe attacks, action buttons and the menu cursor
//                  (PollCursorStage / PollGameplayStage, called from InputManager.Update)
//     pointer    - a real mouse or trackpad through MobilePointer (native GCMouse plugin):
//                  look deltas, buttons, hover cursor, pointer lock
//     keyboard   - detection via typed characters and MobileHardwareKeyboard (GCKeyboard)
//     controller - detection via Input.GetJoystickNames, then DFU's own pad support
//
//   The player's declared input mode (MobileInputMode: Auto / Touch / Keyboard & mouse /
//   Controller) is resolved with the raw detection through MobileInput.ResolveInput into the
//   effective flags everything else reads. It also owns: the weapon swing mode the engine
//   sees (hold-and-drag for touch, click for pointer/pad - ApplySwingMode), the attack
//   threshold calibration in inches (Calibration region), hold-to-skip for videos, the
//   four-finger touch-restore gesture, and the optional diagnostics overlay (OnGUI).
//
//   Registration side effects at Start: the pause menu gains Mobile Settings
//   (MobilePauseOptionsWindow), the roads/travel switch is applied (MobileMods).
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
        [Tooltip("Impose WeaponSwingMode 0 (hold-and-drag gestures) WHILE TOUCH IS SWINGING. " +
                 "Swipe attacks need it: modes 1 and 2 are click-to-attack and pick a RANDOM " +
                 "direction, discarding the swipe. The moment a mouse, keyboard or pad is driving, " +
                 "the player's own launcher choice comes back - so click-to-attack works with a " +
                 "mouse. Off = never touch the setting.")]
        public bool forceGestureSwingMode = true;

        [Tooltip("Mouse and controller attack on the button PRESS (WeaponSwingMode 1), whatever the " +
                 "launcher says. Off = follow the launcher's Weapon swing mode. Mobile Settings > Input.")]
        public bool clickToAttack = true;

        [Tooltip("Touch: a quick tap in combat attacks, swipes only look. Off = swipes attack " +
                 "(the touch default). Mobile Settings > Input.")]
        public bool tapToAttack = false;

        [Header("Auto Combat")]
        [Tooltip("Treat a drawn weapon as combat mode, so swipes attack without needing the " +
                 "on-screen COMBAT button. Without this, drawing a weapon by any other means " +
                 "(keyboard, the WEAPON button) leaves drags look-only, which reads as broken.")]
        public bool autoCombatWhenWeaponDrawn = true;

        [Header("Physical Input")]
        [Tooltip("Hide the touch HUD when a hardware keyboard is used, the same way a gamepad " +
                 "does. Touching the screen brings it back.")]
        public bool autoHideOnKeyboard = true;

        [Header("Pointer (mouse / trackpad)")]
        [Tooltip("GCMouse raw counts -> mouse-axis units. 0.1 matches Unity's own Mouse X/Y axes " +
                 "(ProjectSettings/InputManager.asset), so DFU's mouse sensitivity setting behaves as on PC.")]
        public float pointerToMouseScale = MobilePointer.UnityMouseAxisScale;

        [Tooltip("Invert pointer Y. GameController reports positive-up like Unity, so this should " +
                 "stay off; it exists so a wrong sign on some device is a toggle, not a rebuild. " +
                 "TUNE > Invert pointer Y.")]
        public bool pointerFlipY = false;

        [Tooltip("Scroll accumulator threshold for one classic-UI step. GCMouse scroll has no " +
                 "defined range, so this is tuned by feel.")]
        public float pointerScrollThreshold = 0.5f;

        [Tooltip("Seconds after any pointer button or movement during which a touch is treated as " +
                 "the pointer's own click rather than a finger. Stops the touch HUD flashing on " +
                 "every right-click attack.")]
        public float pointerTouchGrace = 0.4f;

        [Tooltip("Largest pointer movement accepted in one frame, in raw counts. A pointer-lock " +
                 "transition can report one enormous delta; this keeps it from throwing the camera.")]
        public float maxPointerDeltaPerFrame = 250f;

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
        bool mouseActive;

        // Pointer plugin state. pointerBindings is the player's own Mouse0-2 keybinds, captured
        // from InputManager BEFORE ClearPhantomProneBindings() removes them, so the pointer
        // pump can inject the same actions the bindings would have produced on PC.
        float pointerScrollAccum;
        readonly Dictionary<int, InputManager.Actions> pointerBindings = new Dictionary<int, InputManager.Actions>();
        Vector2 lastPointerDelta;
        string lastTouchTypes = "-";
        // When the pointer last did anything (button or movement). A touch that lands inside
        // pointerTouchGrace of it is the pointer's own click, not a finger.
        float lastPointerActivity = -10f;
        bool touchWasDriving;

        // The player's declared input mode. Persisted here rather than through the settings
        // panel's own pref plumbing because the HUD must honour it before the panel has ever
        // been built. The legacy "touch controls off" toggle migrates to KeyboardMouse.
        const string inputModePref = "DFMobile.inputmode";
        const string legacyTouchControlsPref = "DFMobile.touchcontrols";
        MobileInputMode inputMode = MobileInputMode.Auto;

        // RAW detection, before the mode has its say. controllerConnected, keyboardActive and
        // mouseActive above are the EFFECTIVE values the rest of the layer acts on - the
        // polls detect into these, then MobileInput.ResolveInput decides.
        bool controllerDetected;
        bool keyboardDetected;
        bool mouseDetected;

        // The swing mode the player chose (launcher / controls window). Touch imposes 0 only
        // while it is the thing swinging; see ApplySwingMode.
        int userSwingMode;
        int appliedSwingMode = -1;
        bool spellWasReady;         // edge for the tap-to-cast HUD hint
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

        #endregion

        #region Unity

        void Awake()
        {
            inputMode = LoadInputMode();
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
            // The swing mode the player chose in the launcher (or the controls window). It
            // used to be overwritten with 0 here for everyone, which is why "click to attack"
            // never worked with a mouse: the setting was gone before the first swing. Now
            // touch imposes 0 only while it is the thing swinging - see ApplySwingMode.
            userSwingMode = DaggerfallUnity.Settings.WeaponSwingMode;
            ApplySwingMode();

            // Mobile Settings lives in the pause menu; the window subclass adds the button.
            MobilePauseOptionsWindow.Register();

            // The Mods window's choice for roads & real travel, into the live flags, before the
            // first travel popup or terrain tile can ask.
            MobileMods.ApplySaved();

            ApplyAttackThreshold();

            // Real mouse/trackpad support. Unity's iOS player has none (see MobilePointer);
            // this registers for GCMouse, the hover recogniser and the pointer-lock override.
            // Inert below iOS 14 and in the editor.
            MobilePointer.Init();

            // DEVICE-PROVEN FIX: iPadOS (with a Magic Keyboard trackpad attached) reports
            // KeyCode.Mouse0 / GetMouseButton(0) as PERMANENTLY HELD - captured in the
            // idle probe with zero touches: "m0key=True m0btn=True". Since Mouse0 is
            // Daggerfall's ActivateCenterObject binding, the engine re-added the action
            // every frame, so the release edge PlayerActivate waits for never existed -
            // doors could not be opened by any means. On touch devices the mouse-button
            // bindings serve no purpose (the touch layer injects actions directly, and a
            // real pointer injects the CAPTURED bindings via MobilePointer), so clear them
            // for this session. KeyBinds.txt on disk is not modified.
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

            // Remember what the mouse buttons MEANT before they go, so a real pointer can
            // still honour the player's layout (see PumpPointerGameplay).
            CapturePointerBindings(im);

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
            // nothing driving it and the classic UI is left with a frozen cursor. And never
            // leave the OS pointer locked with nothing to unlock it.
            MobileInput.Relinquish();
            MobilePointer.SetLocked(false);
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
            // Asset-map counting is gated on the same toggle as this overlay, and the counting
            // sites are engine code with no view of a MonoBehaviour, so mirror it into the static
            // they can see. One assignment; the counters read it and do nothing when it is false.
            MobileAssetStats.Enabled = showGestureDebug;

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
            PollMouse();
            PollPointerLock();
            PollTouchRestoreGesture();
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
            if (autoHideOnKeyboard)
            {
                // Any touch means the player is back on the glass.
                if (Input.touchCount > 0)
                {
                    keyboardDetected = false;
                }
                // DEVICE-PROVEN FIX: on iOS, Input.anyKeyDown fires for TOUCHES, and at
                // frame boundaries between touches it latched keyboard mode with no keyboard
                // attached - force-releasing both joysticks mid-grab. That was the entire
                // "sticks are inconsistent" bug. Detect keyboards by the one signal only a
                // real keyboard produces: typed characters. Touches, trackpads and styluses
                // never populate Input.inputString.
                else if (!keyboardDetected && (Input.inputString.Length > 0 || MobileHardwareKeyboard.AnyHeld))
                {
                    // inputString misses arrows, modifiers and held keys; the plugin's key
                    // state does not.
                    keyboardDetected = true;
                }
            }

            bool want = Effective.Keyboard;
            if (want != keyboardActive)
                SetKeyboardActive(want);
        }

        /// <summary>
        /// A physical mouse or trackpad stands the touch layer down, the same as a keyboard.
        /// This regressed when touch was added - the touch layer overwrote the mouse axes
        /// every frame and its virtual cursor fought the real pointer, so a mouse that worked
        /// before the port stopped working after it.
        ///
        /// Detection is a connected GCMouse that has moved or clicked (MobilePointer). Unity's
        /// own view of the pointer is useless here: its iOS player has no mouse support, so
        /// hover never reaches the raw axes and Input.GetMouseButton(0) reads as permanently
        /// held with a trackpad attached - the trap that broke doors once already. Without the
        /// plugin (iOS 13) the old axis-movement check remains as a fallback. Not in the
        /// editor, where the mouse legitimately drives the touch overlay for testing.
        ///
        /// A FINGER on the glass hands control back to touch. iPadOS delivers pointer clicks
        /// as touches too, so only touches that pass MobilePointer.IsFingerTouch count -
        /// otherwise every click would flip the touch HUD back on.
        /// </summary>
        void PollMouse()
        {
            if (Application.isEditor)
                return;

            DetectMouse();

            bool want = Effective.Mouse;
            if (want != mouseActive)
                SetMouseActive(want);

            // A pointer nobody is listening to (Touch or Controller mode) must not bank its
            // movement: switching back to Auto later would release it all as one jerk.
            if (!want && MobilePointer.Supported)
                MobilePointer.ConsumeDelta();
        }

        /// <summary>Raw pointer detection into mouseDetected; the mode decides what to do with it.</summary>
        void DetectMouse()
        {
            bool plugin = MobilePointer.Supported;
            bool anyButton = plugin && MobilePointer.AnyButton;
            if (anyButton)
                lastPointerActivity = Time.unscaledTime;

            if (Input.touchCount > 0)
            {
                bool finger = false;
                var types = new System.Text.StringBuilder();
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    types.Append(t.type).Append(' ');
                    if (MobilePointer.IsFingerTouch(t.type, anyButton,
                                                    Time.unscaledTime - lastPointerActivity, pointerTouchGrace))
                        finger = true;
                }
                lastTouchTypes = types.ToString();

                if (finger)
                {
                    mouseDetected = false;
                    return;
                }
            }

            if (mouseDetected)
                return;

            if (plugin)
            {
                if (!MobilePointer.Connected)
                    return;

                // Nobody else consumes while the pointer is idle, so consuming here is what
                // keeps the first real movement from arriving as one accumulated jerk.
                bool moved = MobilePointer.ConsumeDelta().sqrMagnitude > 0f;
                if (moved || anyButton)
                    mouseDetected = true;
                return;
            }

            // Fallback without the plugin: movement on the raw axes and nothing else. With
            // simulateMouseWithTouches forced off, touches never reach those axes.
            if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.01f ||
                Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.01f)
            {
                mouseDetected = true;
            }
        }

        /// <summary>
        /// Lock the pointer exactly when PlayerMouseLook would have on PC. Cursor.lockState is
        /// a no-op on iOS, so the engine's request never reaches the OS; this mirrors the
        /// engine's own cursor state (PlayerMouseLook hides it during play and shows it for
        /// menus, pause and the ActivateCursor toggle) into the plugin instead. Never in the
        /// startup scene - there is no game to look around in.
        /// </summary>
        void PollPointerLock()
        {
            if (!MobilePointer.Supported)
                return;

            bool inGame = GameManager.HasInstance;
            bool paused = inGame && GameManager.IsGamePaused;
            bool cursorVisible = !InputManager.HasInstance || InputManager.Instance.CursorVisible;

            MobilePointer.SetLocked(inGame &&
                MobilePointer.ShouldLock(mouseActive, MobileInput.MenuMode, paused, cursorVisible));
        }

        /// <summary>
        /// Menu-side pointer pump, run from PollCursorStage (so it works while the game is
        /// paused behind a window). The real pointer drives the existing virtual cursor: hover
        /// gives the position, GCMouse buttons are latched so TickButtons() derives the same
        /// down/up edges the touch path produces, scroll becomes classic-UI steps. The cursor
        /// TEXTURE is not drawn while a pointer is active (InputManager.OnGUI) because the
        /// system arrow is already there.
        /// </summary>
        void PumpPointerCursor()
        {
            if (!MobileInput.MenuMode)
            {
                // DEVICE-PROVEN BUG (first mouse build): this ran every frame of normal play,
                // and PollCursorStage precedes PollGameplayStage in InputManager.Update - so
                // it drained every delta before the gameplay pump could read one. The pointer
                // locked, then nothing moved and nothing swung. Drain ONLY while the game is
                // paused with no classic window (the one case the gameplay pump never runs),
                // so movement during a pause cannot burst into the camera on resume.
                bool paused = GameManager.HasInstance && GameManager.IsGamePaused;
                if (MobilePointer.ShouldDrainInCursorStage(MobileInput.MenuMode, paused))
                    MobilePointer.ConsumeDelta();
                return;
            }

            Vector2 hover;
            if (MobilePointer.TryGetHover(out hover))
                MobileInput.SetCursorPosition(hover);

            MobileInput.SetLatched(0, MobilePointer.Left);
            MobileInput.SetLatched(1, MobilePointer.Right);

            pointerScrollAccum += MobilePointer.ConsumeScroll();
            int tick = MobilePointer.ScrollTicks(ref pointerScrollAccum, pointerScrollThreshold);
            if (tick != 0)
                MobileInput.QueueScroll(tick);

            // Unlocked: hover owns the position. Drop movement so it cannot pool up.
            MobilePointer.ConsumeDelta();
        }

        /// <summary>
        /// Gameplay-side pointer pump, run from PollGameplayStage in place of the touch pumps.
        /// Movement goes into the same mouseX/mouseY channel the touch drag uses, so
        /// PlayerMouseLook and WeaponManager.TrackMouseAttack() both see it exactly as they
        /// would a PC mouse - hold the swing button and drag to attack, release to reset,
        /// bows draw while held and loose on release. Buttons inject the actions the player's
        /// own Mouse0-2 keybinds named, falling back to Daggerfall's defaults.
        /// </summary>
        void PumpPointerGameplay(InputManager inputManager)
        {
            // One frame of pointer-lock transition can report a huge delta; unclamped it
            // slams pitch to its limit.
            Vector2 raw = MobilePointer.ClampDelta(MobilePointer.ConsumeDelta(), maxPointerDeltaPerFrame);
            lastPointerDelta = raw;
            if (raw.sqrMagnitude > 0f || MobilePointer.Buttons != 0)
                lastPointerActivity = Time.unscaledTime;

            // ALWAYS overwrite, including with zero. A pointer click arrives as a touch too,
            // and on iOS the primary touch's movement feeds Unity's raw Mouse X/Y - left alone
            // it would add to ours and double the look during a click-drag. A journey owns the
            // camera outright (see PumpLookAndGesture), so it gets zero.
            Vector2 d = MobileJourneyPilot.Active
                ? Vector2.zero
                : MobilePointer.ScaleDelta(raw, pointerToMouseScale, pointerFlipY);
            inputManager.SetMobileMouseAxes(d.x, d.y);

            int buttons = MobilePointer.Buttons;
            for (int b = 0; b < 3; b++)
            {
                if ((buttons & (1 << b)) == 0)
                    continue;

                InputManager.Actions action;
                if (TryGetPointerAction(b, out action))
                    inputManager.AddAction(action);
            }
        }

        /// <summary>
        /// Record every action bound to Mouse0-2 (primary or secondary). Called before each
        /// ClearPhantomProneBindings() sweep - including the self-healing re-clears, which is
        /// what makes this robust to the InputManager.Start() ordering race: if keybinds had
        /// not loaded yet the first time, the resurrection pass captures them.
        /// </summary>
        void CapturePointerBindings(InputManager im)
        {
            foreach (InputManager.Actions action in System.Enum.GetValues(typeof(InputManager.Actions)))
            {
                if (action == InputManager.Actions.Unknown)
                    continue;
                RecordPointerBinding(im.GetBinding(action, true), action);
                RecordPointerBinding(im.GetBinding(action, false), action);
            }
        }

        void RecordPointerBinding(KeyCode key, InputManager.Actions action)
        {
            if (key < KeyCode.Mouse0 || key > KeyCode.Mouse2)
                return;

            int button = key - KeyCode.Mouse0;
            if (pointerBindings.ContainsKey(button))
                return;

            pointerBindings[button] = action;
            Debug.Log("[MobileInput] pointer binding captured: " + key + " -> " + action);
        }

        bool TryGetPointerAction(int button, out InputManager.Actions action)
        {
            if (pointerBindings.TryGetValue(button, out action))
                return true;

            // Nothing captured at all (KeyBinds.txt with no mouse entries, or not yet
            // loaded): use the stock layout. If the player HAS mouse bindings, an unbound
            // button stays unbound - that is their choice.
            if (pointerBindings.Count == 0)
                return MobilePointer.TryDefaultAction(button, out action);

            action = InputManager.Actions.Unknown;
            return false;
        }

        void SetMouseActive(bool value)
        {
            mouseActive = value;
            MobileInput.MouseActive = value;

            // Same hand-back as the keyboard: touch buttons and gestures must not linger over
            // a session being driven by a real pointer. Relinquish() also drops the virtual
            // cursor and resets Mode; ResolveMode() re-enters Menu on the next Update if a
            // window is open, which re-claims the cursor for the pointer pump.
            if (value)
                MobileInput.Relinquish();
            else
                MobilePointer.SetLocked(false);

            Debug.Log(value ? "[MobileInput] pointer active - touch HUD hidden, pointer drives look/cursor"
                            : "[MobileInput] pointer idle - finger on screen, touch HUD restored");

            ApplyHudVisibility();
        }

        public bool MouseActive { get { return mouseActive; } }

        /// <summary>
        /// What the mode makes of the raw detection. Recomputed on demand - it is a handful of
        /// booleans - so every decision in the layer reads the same table.
        /// </summary>
        EffectiveInput Effective
        {
            get { return MobileInput.ResolveInput(inputMode, controllerDetected, keyboardDetected, mouseDetected); }
        }

        /// <summary>
        /// The player's declared input mode (Mobile Settings > Input). Setting it re-runs every
        /// decision immediately rather than on the next poll tick, so the HUD and pumps flip
        /// the frame the row is tapped.
        ///
        /// THE ESCAPE HATCH MATTERS: with touch stood down there is nothing on screen that can
        /// bring it back, and the pause menu that holds Mobile Settings needs a pad or keyboard
        /// to open. A four-finger tap - a gesture nothing else in the port uses - returns to
        /// Touch. Without it, KeyboardMouse or Controller is a soft brick on an iPad whose
        /// accessory has just been unplugged.
        /// </summary>
        public MobileInputMode InputMode
        {
            get { return inputMode; }
            set
            {
                if (inputMode == value)
                    return;

                inputMode = value;
                PlayerPrefs.SetInt(inputModePref, (int)value);
                PlayerPrefs.Save();
                Debug.Log("[MobileInput] input mode -> " + value);

                nextControllerPoll = 0f;
                PollController();

                bool wantKeyboard = Effective.Keyboard;
                if (wantKeyboard != keyboardActive)
                    SetKeyboardActive(wantKeyboard);

                bool wantMouse = Effective.Mouse;
                if (wantMouse != mouseActive)
                    SetMouseActive(wantMouse);

                ApplyHudVisibility();
            }
        }

        static MobileInputMode LoadInputMode()
        {
            if (PlayerPrefs.HasKey(inputModePref))
            {
                int raw = PlayerPrefs.GetInt(inputModePref, 0);
                if (raw >= (int)MobileInputMode.Auto && raw <= (int)MobileInputMode.Controller)
                    return (MobileInputMode)raw;
                return MobileInputMode.Auto;
            }

            // Pre-mode builds had a single "touch controls" switch; off meant mouse and keyboard.
            if (PlayerPrefs.GetInt(legacyTouchControlsPref, 1) == 0)
                return MobileInputMode.KeyboardMouse;

            return MobileInputMode.Auto;
        }

        void PollTouchRestoreGesture()
        {
            if (Effective.TouchHud)
                return;

            if (Input.touchCount >= 4)
            {
                InputMode = MobileInputMode.Touch;
                DaggerfallUI.AddHUDText("Touch controls restored.", 2f);
            }
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
            if (Time.unscaledTime < nextControllerPoll)
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

            // The launcher's Enable Controller is the player's word on pads (device report: it
            // was being overridden by detection). Off there = no pad is ever detected here;
            // choosing Controller in Mobile Settings still forces it on, as an explicit choice.
            controllerDetected = false;
            bool padsAllowed = autoDetectController && DaggerfallUnity.Settings.EnableController;
            string[] names = padsAllowed ? Input.GetJoystickNames() : new string[0];
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    controllerDetected = true;
                    break;
                }
            }

            // Raw detection in, the mode's verdict out: Touch ignores a phantom pad,
            // Controller is on with nothing listed.
            bool found = Effective.Controller;
            // Kept for the log line below: on iPadOS Unity can list things that are not
            // gamepads (the iOS 26 Simulator reports one with nothing attached), and a
            // phantom "controller" hides the touch HUD. Only computed on a state change.
            string joystickNames = null;

            if (found == controllerConnected)
                return;

            controllerConnected = found;
            joystickNames = "[" + string.Join(", ", names) + "]";

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

            Debug.Log((found
                ? "[MobileInput] gamepad connected - touch HUD hidden, controller cursor active"
                : "[MobileInput] gamepad disconnected - touch HUD restored") +
                " joysticks=" + joystickNames);
        }

        /// <summary>Single place that decides which HUD layer is visible.</summary>
        void ApplyHudVisibility()
        {
            bool touchAllowed = Effective.TouchHud;
            bool menu = MobileInput.Mode == MobileControlMode.Menu;

            SetLayer(gameplayLayer, touchAllowed && !menu);
            SetLayer(menuLayer, touchAllowed && menu);

            // Touch standing down mid-gesture must not leave a latched button or a half-drag
            // behind for the keyboard, pad or pointer that took over.
            if (!touchAllowed && virtualMouse != null)
                virtualMouse.ResetGesture();

            // Whoever now drives gameplay decides how a swing is made.
            ApplySwingMode();
        }

        /// <summary>Re-run the swing-mode decision now (Mobile Settings changed a switch).</summary>
        public void RefreshSwingMode()
        {
            ApplySwingMode();
        }

        /// <summary>
        /// Hold-and-drag (0) while touch swings, the player's own choice otherwise, and always
        /// the player's own choice while a classic window is open so settings.ini can only
        /// ever be written with their value. See MobileInput.ResolveSwingMode.
        /// </summary>
        void ApplySwingMode()
        {
            if (!forceGestureSwingMode)
                return;

            int want = MobileInput.ResolveSwingMode(userSwingMode, Effective.TouchHud, MobileInput.MenuMode,
                                                    clickToAttack, tapToAttack);
            if (DaggerfallUnity.Settings.WeaponSwingMode != want)
                DaggerfallUnity.Settings.WeaponSwingMode = want;

            if (appliedSwingMode != want)
            {
                appliedSwingMode = want;
                Debug.Log("[MobileInput] WeaponSwingMode -> " + want +
                          (want == 0 && userSwingMode != 0 ? " (touch swipes; player's choice " + userSwingMode + " returns with a pointer or pad)" : ""));
            }
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
                "mode {0}  input {8}  gamepad {6}  swing {9}\nswipe {1:0.00}in x scale {2:0.000}  dpi {7:0}\nAttackThreshold {3:0.0000}\nswinging {4}\nrequired swipe ~{5:0} px",
                MobileInput.Mode, swipeDistanceInches, touchToMouseScale, threshold,
                Time.unscaledTime < swingHoldUntil,
                touchToMouseScale > 0f
                    ? threshold * Mathf.Max(Screen.width, Screen.height) / touchToMouseScale
                    : 0f,
                controllerConnected, MobileInput.Dpi, inputMode, DaggerfallUnity.Settings.WeaponSwingMode);

            text += string.Format(
                "\npointer: plugin {0}  connected {1}  active {2}  lock req {3} / actual {4}\n" +
                "buttons {5}  last delta {6}  touch types: {7}",
                MobilePointer.Supported ? "yes" : "no", MobilePointer.Connected, mouseActive,
                MobilePointer.LockRequested, MobilePointer.IsLocked,
                MobilePointer.Buttons, lastPointerDelta.ToString("0.0"), lastTouchTypes);

            // Material maps applied from mod bundles vs loose files. The mod column is the only
            // proof available in-game that bundled normal/height maps are being found and used -
            // it cannot be told from a screenshot of the world itself.
            text += "\n" + MobileAssetStats.Summary();

            GUI.Label(new Rect(12f, 12f, 560f, 340f), text);
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

                // A window was open, so the setting held the player's value the whole time -
                // including any change they just made in the controls screen. Take it before
                // touch imposes its own again.
                userSwingMode = DaggerfallUnity.Settings.WeaponSwingMode;

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

            // A real pointer drives the virtual cursor itself (hover + GCMouse buttons). The
            // touch cursor must NOT also run: a pointer click arrives as a touch and would
            // fire a second click.
            if (mouseActive && !controllerConnected)
            {
                PumpPointerCursor();
                return;
            }

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
            if (inputManager == null || MobileInput.MenuMode || controllerConnected)
                return;

            // A pointer replaces the touch pumps entirely, and runs regardless of the touch
            // master toggle or a hardware keyboard (a Magic Keyboard is both at once).
            if (mouseActive)
            {
                PumpPointerGameplay(inputManager);
                return;
            }

            if (!Effective.TouchHud)
            {
                touchWasDriving = false;
                return;
            }

            // Daggerfall's cursor mode (Return on a keyboard) parks the camera and shows the
            // arrow. Touch has no key to toggle it back, so a player who pressed Return with a
            // keyboard and then went back to touch found the look stick dead (device report).
            // Cleared once, at the moment touch takes over - not every frame, so the toggle in
            // Mobile Settings > Input can still turn it on deliberately from touch.
            if (!touchWasDriving)
            {
                touchWasDriving = true;
                if (GameManager.HasInstance && GameManager.Instance.PlayerMouseLook != null &&
                    GameManager.Instance.PlayerMouseLook.cursorActive)
                {
                    GameManager.Instance.PlayerMouseLook.cursorActive = false;
                    Debug.Log("[MobileInput] cursor mode cleared: touch took over");
                }
            }

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

            // A READY SPELL IS CAST BY ACTIVATE, NOT BY A SWING. EntityEffectManager fires
            // CastReadySpell on Actions.ActivateCenterObject, and WeaponManager deliberately
            // ignores SwingWeapon while a spell is ready - so the swipe gesture could never
            // cast. Mouse left-click and the pad's A button both ARE Activate, which is why
            // only touch was broken. With a mouse the cast is a click, so the touch analog
            // is a tap on the view; drags still only look, in or out of combat mode.
            bool spellReady = GameManager.HasInstance &&
                              GameManager.Instance.PlayerEffectManager != null &&
                              GameManager.Instance.PlayerEffectManager.HasReadySpell;
            if (spellReady && !spellWasReady)
                DaggerfallUI.AddHUDText("Tap the view to cast", 2.5f);
            spellWasReady = spellReady;

            // TAP TO ATTACK (Mobile Settings > Input, off by default): the engine runs in
            // click mode for touch, so a quick tap is the click and a drag only looks. The
            // swipe hold below must not arm, or every look-drag would be an attack.
            if (spellReady)
            {
                if (lookZone.ConsumeTap())
                    QueueAction(InputManager.Actions.ActivateCenterObject);
            }
            else if (tapToAttack)
            {
                if (combat && !bow && lookZone.ConsumeTap())
                    QueueAction(InputManager.Actions.SwingWeapon);
            }
            else
            {
                lookZone.ConsumeTap();      // never let a stale tap fire later
                if (combat && !bow && lookZone.IsDragging)
                    swingHoldUntil = Time.unscaledTime + swingHoldExtension;
            }

            // Swipe-swing (blades) and bow-draw are separate states: only the swipe one
            // may suppress the look stick.
            bool swipeWindow = !tapToAttack && combat && !bow && Time.unscaledTime < swingHoldUntil;
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
