// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Daggerfall's classic UI reads the pointer through exactly three engine surfaces:
//     InputManager.Instance.MousePosition          (BaseScreenComponent:573, automaps)
//     InputManager.Instance.GetMouseButton*(int)   (every clickable component)
//     InputManager.Instance.GetBackButton*()       (how every window actually closes)
//   All three already divert to a synthetic cursor for gamepad support, so no UI
//   window needed rebuilding or patching.
//
//   Movement is relative (trackpad model) rather than absolute, because the classic UI
//   is authored at 320x200 and scaled up: hotspots such as the inventory scroll arrows
//   are a few virtual pixels wide and a fingertip covers them entirely. Relative drag
//   lets the player see the cursor before committing to a click.
//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class VirtualMouseCursor : MonoBehaviour
    {
        [Header("Feel")]
        [Tooltip("Cursor pixels per finger pixel at low speed.")]
        [Range(0.4f, 3f)] public float baseGain = 1.15f;

        [Tooltip("Extra gain applied to fast flicks (pointer acceleration).")]
        [Range(0f, 2f)] public float accelGain = 0.9f;

        [Tooltip("Finger speed, in screen-heights per second, at which accelGain is fully applied.")]
        public float accelReferenceSpeed = 1.5f;

        [Tooltip("Finger position IS the cursor position: tap a button to click it, exactly as " +
                 "every other iOS app works. This is the right default - device testing showed the " +
                 "trackpad model reads as broken ('I have to drag the mouse over the button'). " +
                 "Turn off for trackpad-style relative movement with acceleration, which trades " +
                 "familiarity for precision on very small hotspots.")]
        public bool absoluteMode = true;

        [Header("Tap / Hold")]
        [Tooltip("Largest travel for a tap, as a fraction of screen height. A tap is " +
                 "decided by how far the finger moved, not how long it was down - " +
                 "holdToDragDelay below is what separates a tap from a drag.")]
        [Range(0.004f, 0.08f)] public float tapMaxTravel = 0.02f;

        [Tooltip("Stationary hold that latches the left button down, for scrollbars and sliders.")]
        public float holdToDragDelay = 0.32f;

        [Header("Two Finger Gestures")]
        [Tooltip("Two-finger tap sends a right click.")]
        public bool twoFingerRightClick = true;

        [Tooltip("Two-finger vertical drag emits scroll wheel ticks.")]
        public bool twoFingerScroll = true;

        public float scrollPixelsPerTick = 60f;

        int primaryFingerId = -1;
        Vector2 primaryLastPosition;
        float primaryStartTime;
        float primaryTravel;
        bool buttonLatched;

        bool multiTouchActive;
        bool rightClickArmed;
        float scrollAccumulator;

        // Fingers that started on a UGUI widget (the MenuLayer back button). Those must
        // not also drag the cursor, or tapping BACK would slide the pointer.
        readonly HashSet<int> uiOwnedFingers = new HashSet<int>();

        /// <summary>
        /// Called once per frame from MobileInputController.PollCursorStage(), which is
        /// called by InputManager.Update() before its paused early-return. An explicit
        /// poll rather than Update() keeps a deterministic order relative to the code
        /// that reads the cursor.
        /// </summary>
        public void PollTouches()
        {
            int touchCount = Input.touchCount;

            if (touchCount == 0)
            {
                if (multiTouchActive)
                    EndMultiTouch();
                else if (primaryFingerId != -1)
                    EndPrimary();

                uiOwnedFingers.Clear();

                // Desktop / editor fallback. With no touches there is nothing to drive
                // the virtual cursor, yet InputManager.MousePosition is already being
                // diverted to it - so the classic UI would be stuck with a cursor frozen
                // at screen centre. Mirror the real mouse instead.
                //
                // Input.mousePresent is false on iOS, so this is inert on device and
                // needs no compile guard.
                if (Input.mousePresent)
                    PollMouseFallback();

                return;
            }

            PruneUiFingers();

            if (touchCount >= 2)
            {
                HandleMultiTouch();
                return;
            }

            if (multiTouchActive)
            {
                EndMultiTouch();
                return;
            }

            Touch touch = Input.GetTouch(0);

            // Claim-on-begin: if this finger landed on a UGUI widget, that widget owns it.
            if (touch.phase == TouchPhase.Began && IsOverUi(touch.fingerId))
            {
                uiOwnedFingers.Add(touch.fingerId);
                return;
            }

            if (uiOwnedFingers.Contains(touch.fingerId))
                return;

            HandleSingleTouch(touch);
        }

        public void ResetGesture()
        {
            primaryFingerId = -1;
            primaryTravel = 0f;
            buttonLatched = false;
            multiTouchActive = false;
            rightClickArmed = false;
            scrollAccumulator = 0f;
            uiOwnedFingers.Clear();
            MobileInput.SetLatched(0, false);
            MobileInput.SetLatched(1, false);
        }

        #region Desktop Fallback

        /// <summary>
        /// Drives the virtual cursor from a real mouse so the touch layer can be
        /// exercised in the editor. Absolute (cursor follows the pointer exactly)
        /// because there is no precision problem to solve with a mouse.
        /// </summary>
        void PollMouseFallback()
        {
            MobileInput.SetCursorPosition(Input.mousePosition);

            // Latching from the physical button state lets TickButtons() derive the
            // down/up edges, so clicks behave identically to the touch path.
            MobileInput.SetLatched(0, Input.GetMouseButton(0));
            MobileInput.SetLatched(1, Input.GetMouseButton(1));

            float scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0f))
                MobileInput.QueueScroll(scroll);
        }

        #endregion

        #region UI Ownership

        static bool IsOverUi(int fingerId)
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(fingerId);
        }

        /// <summary>Drop finger ids that are no longer touching the screen.</summary>
        void PruneUiFingers()
        {
            if (uiOwnedFingers.Count == 0)
                return;

            uiOwnedFingers.RemoveWhere(IsFingerGone);
        }

        static bool IsFingerGone(int fingerId)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != fingerId)
                    continue;

                return t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            return true;
        }

        #endregion

        #region Single Touch

        void HandleSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginPrimary(touch);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (primaryFingerId != touch.fingerId)
                        BeginPrimary(touch);
                    else
                        MovePrimary(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (primaryFingerId == touch.fingerId)
                        EndPrimary();
                    break;
            }
        }

        void BeginPrimary(Touch touch)
        {
            primaryFingerId = touch.fingerId;
            primaryLastPosition = touch.position;
            primaryStartTime = Time.unscaledTime;
            primaryTravel = 0f;
            buttonLatched = false;

            if (absoluteMode)
                MobileInput.SetCursorPosition(touch.position);
        }

        void MovePrimary(Touch touch)
        {
            Vector2 delta = touch.position - primaryLastPosition;
            primaryLastPosition = touch.position;
            primaryTravel += delta.magnitude;

            if (absoluteMode)
            {
                MobileInput.SetCursorPosition(touch.position);
            }
            else if (delta.sqrMagnitude > 0f)
            {
                // Unscaled: an open menu sets Time.timeScale to 0.
                float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                float speedFraction = (delta.magnitude / Mathf.Max(Screen.height, 1)) / dt;
                float accel = Mathf.Clamp01(speedFraction / Mathf.Max(accelReferenceSpeed, 0.01f));
                float gain = baseGain + accelGain * accel;

                MobileInput.SetCursorPosition(MobileInput.CursorPosition + delta * gain);
            }

            if (!buttonLatched &&
                Time.unscaledTime - primaryStartTime >= holdToDragDelay &&
                primaryTravel < tapMaxTravel * Screen.height)
            {
                buttonLatched = true;
                MobileInput.SetLatched(0, true);
            }
        }

        void EndPrimary()
        {
            if (buttonLatched)
            {
                MobileInput.SetLatched(0, false);
            }
            else
            {
                // Travel decides a tap, not duration.
                //
                // There used to be a tapMaxDuration of 0.22s as well, which left a dead
                // band up to holdToDragDelay at 0.32s: a touch lifted in that window was
                // too slow to count as a tap and too quick to have latched, so it emitted
                // nothing at all. A press that lands on a province and lifts a quarter of
                // a second later is an ordinary tap, and it did nothing.
                //
                // Duration is not needed to tell the two apart: a stationary hold long
                // enough to matter has already latched by holdToDragDelay and taken the
                // branch above, and a touch that wandered fails the travel test whatever
                // its duration.
                if (primaryTravel <= tapMaxTravel * Screen.height)
                    MobileInput.QueueClick(0);
            }

            primaryFingerId = -1;
            primaryTravel = 0f;
            buttonLatched = false;
        }

        #endregion

        #region Multi Touch

        void HandleMultiTouch()
        {
            if (!multiTouchActive)
            {
                multiTouchActive = true;
                rightClickArmed = twoFingerRightClick;
                scrollAccumulator = 0f;

                // Demote the first finger without emitting a left click.
                if (buttonLatched)
                    MobileInput.SetLatched(0, false);
                primaryFingerId = -1;
                primaryTravel = 0f;
                buttonLatched = false;
            }

            if (!twoFingerScroll)
                return;

            float averageDeltaY = (Input.GetTouch(0).deltaPosition.y + Input.GetTouch(1).deltaPosition.y) * 0.5f;

            if (Mathf.Abs(averageDeltaY) > 1f)
                rightClickArmed = false;            // moving, so it is a scroll not a tap

            scrollAccumulator += averageDeltaY;

            int ticks = (int)(scrollAccumulator / scrollPixelsPerTick);
            if (ticks != 0)
            {
                scrollAccumulator -= ticks * scrollPixelsPerTick;
                MobileInput.QueueScroll(ticks);
            }
        }

        void EndMultiTouch()
        {
            if (rightClickArmed)
                MobileInput.QueueClick(1);

            multiTouchActive = false;
            rightClickArmed = false;
            scrollAccumulator = 0f;
        }

        #endregion
    }
}
