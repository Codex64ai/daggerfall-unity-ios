// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DaggerfallWorkshop;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Data-driven virtual button. Drop onto any UI Image, pick an InputManager.Actions
    /// value and a press mode. Covers the whole action bank with one script: Jump,
    /// Crouch, Rest, ReadyWeapon, Status, ActivateCenterObject, Inventory, TravelMap,
    /// CastSpell, Transport, SwitchHand and so on.
    ///
    /// Handles pointer events directly rather than through Button.onClick, because
    /// onClick only fires on release and cannot express Hold.
    /// </summary>
    public class MobileActionButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        public enum PressMode
        {
            /// <summary>One shot. Held two frames so both ActionStarted and ActionComplete fire.</summary>
            Tap,
            /// <summary>Held for as long as the finger is down.</summary>
            Hold,
            /// <summary>Latching visual state, but still sends a discrete press.
            /// Correct for Crouch: PlayerHeightChanger.cs:131 uses ActionStarted().</summary>
            Toggle,
        }

        [Header("Action")]
        public InputManager.Actions action = InputManager.Actions.ActivateCenterObject;
        public PressMode pressMode = PressMode.Tap;

        [Header("Interaction Mode Cycle")]
        [Tooltip("Cycle the interaction mode instead of sending a fixed action. Steal is " +
                 "how locks are picked and Info is how things are examined, and touch had " +
                 "no way to reach either - the mode switch only existed on the classic bar " +
                 "and the controller d-pad. Cycles in the same order the classic bar does.")]
        public bool cyclesInteractionMode = false;

        [Header("UI Back Button")]
        [Tooltip("Send the UI back button instead of an action. REQUIRED for a menu close button: " +
                 "windows close via InputManager.GetBackButtonUp(), which reads raw KeyCode.Escape - " +
                 "injecting Actions.Escape does NOT close a window.")]
        public bool sendsUiBackButton = false;

        [Header("Feedback")]
        [Tooltip("Tinted while pressed or latched. Usually this button's own Image.")]
        public Graphic tintTarget;
        public Color pressedColor = new Color(1f, 0.85f, 0.40f, 1f);

        [Tooltip("Taptic Engine feedback on press. Silently does nothing on iPad (no motor) " +
                 "and off-device. Set None to disable.")]
        public HapticStyle haptic = HapticStyle.Light;

        [Header("Drawer")]
        [Tooltip("Set when this button lives inside the MENU drawer, so pressing it closes it.")]
        public MobileButtonDrawer ownerDrawer;

        [Header("Rate Limit")]
        [Tooltip("Minimum seconds between Tap fires. 0 disables the limit.")]
        public float tapCooldown = 0.12f;

        Color idleColor = Color.white;
        bool toggledOn;
        bool held;
        float lastFireTime = float.NegativeInfinity;

        void Awake()
        {
            if (tintTarget != null)
                idleColor = tintTarget.color;
        }

        void Start()
        {
            // Player-hidden via the layout editor. MENU and TUNE are exempt - hiding
            // them would lock the player out of the editor with no way back.
            if (gameObject.name != "SettingsGear" &&
                MobileHudLayout.GetHiddenOverride(gameObject.name))
                gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // Never leave an action stuck down because the HUD layer got hidden.
            if (held && MobileInputController.HasInstance)
                MobileInputController.Instance.SetActionHeld(action, false);

            held = false;
            toggledOn = false;
            ApplyTint(false);
        }

        /// <summary>
        /// The action that advances the interaction mode one step, matching the order the
        /// classic bar cycles in (Steal -> Talk -> Grab -> Info -> Steal).
        ///
        /// Routed as an ACTION rather than by calling ChangeInteractionMode directly, so it
        /// goes through PlayerActivate's own handling - the same path the keyboard, the
        /// controller d-pad and the classic bar all use.
        /// </summary>
        InputManager.Actions NextInteractionModeAction()
        {
            if (!GameManager.HasInstance || GameManager.Instance.PlayerActivate == null)
                return InputManager.Actions.GrabMode;

            switch (GameManager.Instance.PlayerActivate.CurrentMode)
            {
                case PlayerActivateModes.Steal: return InputManager.Actions.TalkMode;
                case PlayerActivateModes.Talk: return InputManager.Actions.GrabMode;
                case PlayerActivateModes.Grab: return InputManager.Actions.InfoMode;
                case PlayerActivateModes.Info: return InputManager.Actions.StealMode;
            }

            return InputManager.Actions.GrabMode;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (sendsUiBackButton)
            {
                if (Time.unscaledTime - lastFireTime < tapCooldown)
                    return;
                lastFireTime = Time.unscaledTime;
                MobileInput.QueueBack();
                ApplyTint(true);
                PlayHaptic();
                return;
            }

            if (!MobileInputController.HasInstance)
                return;

            MobileInputController controller = MobileInputController.Instance;

            switch (pressMode)
            {
                case PressMode.Tap:
                    if (Time.unscaledTime - lastFireTime < tapCooldown)
                        return;
                    lastFireTime = Time.unscaledTime;
                    controller.QueueAction(cyclesInteractionMode ? NextInteractionModeAction()
                                                                : action);
                    ApplyTint(true);
                    break;

                case PressMode.Hold:
                    held = true;
                    controller.SetActionHeld(action, true);
                    ApplyTint(true);
                    break;

                case PressMode.Toggle:
                    toggledOn = !toggledOn;
                    controller.QueueAction(action);
                    ApplyTint(toggledOn);
                    break;
            }

            PlayHaptic();

            if (ownerDrawer != null)
                ownerDrawer.NotifySelection();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressMode == PressMode.Hold && !sendsUiBackButton && MobileInputController.HasInstance)
            {
                held = false;
                MobileInputController.Instance.SetActionHeld(action, false);
            }

            if (pressMode != PressMode.Toggle || sendsUiBackButton)
                ApplyTint(false);
        }

        void ApplyTint(bool active)
        {
            if (tintTarget != null)
                tintTarget.color = active ? pressedColor : idleColor;
        }

        void PlayHaptic()
        {
            MobileHaptics.Play(haptic);
        }

        // Historical note, kept because it is a trap worth remembering:
        //
        // Handheld.Vibrate() is a no-op on iPad (no vibration motor) and on iPhone it fires the
        // harsh legacy full-device buzz rather than the crisp taps users expect. Real feedback
        // needs a native plugin wrapping UIImpactFeedbackGenerator / UISelectionFeedbackGenerator.
        // An honest no-op beats a setting that pretends to work.
    }
}
