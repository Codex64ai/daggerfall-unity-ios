// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        void OnDisable()
        {
            // Never leave an action stuck down because the HUD layer got hidden.
            if (held && MobileInputController.HasInstance)
                MobileInputController.Instance.SetActionHeld(action, false);

            held = false;
            toggledOn = false;
            ApplyTint(false);
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
                    controller.QueueAction(action);
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
