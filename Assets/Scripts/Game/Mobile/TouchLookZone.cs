// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   This zone deliberately does NOT classify swipe direction. WeaponManager owns that
//   in TrackMouseAttack(), including its radial segment table - which only ever yields
//   Right, Up, Left, DownLeft, Down and DownRight. UpLeft and UpRight are unreachable
//   because the "up" cone is widened to 150 degrees (WeaponManager.cs:833-844).
//   Reimplementing the mapping here would produce attacks that do not match the game's
//   own animations, so we forward raw deltas and let the engine decide.
//

using UnityEngine;
using UnityEngine.EventSystems;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Invisible drag surface. Accumulates touch deltas in pixels for the camera-look
    /// and weapon-gesture channel. Place FIRST in the HUD hierarchy so the joystick and
    /// action buttons, drawn later, win the raycast and their touches never leak here.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TouchLookZone : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int pointerIdNone = -999;

        [Header("Palm Rejection")]
        [Tooltip("Ignore touches that begin in the bottom corners, where thumbs rest while " +
                 "gripping a tablet. Measured in INCHES so it is the same physical size on " +
                 "an iPad Pro and an iPhone. 0 disables.")]
        [Range(0f, 1.5f)] public float cornerDeadMarginInches = 0.45f;

        [Tooltip("Also reject the top corners (relevant when gripping an iPad in landscape).")]
        public bool rejectTopCorners = false;

        [Tooltip("Invert vertical drag.")]
        public bool invertY = false;

        [Tooltip("Extra damping on the vertical axis. Pitch reads twitchier than yaw on touch.")]
        [Range(0.2f, 1.5f)] public float verticalScale = 0.75f;

        [Tooltip("Discard a single frame's delta larger than this many pixels. Filters the jump when a second finger becomes the primary pointer.")]
        public float deltaSpikeLimit = 300f;

        Vector2 accumulated;
        int activePointerId = pointerIdNone;
        bool combatMode;

        public bool IsDragging { get { return activePointerId != pointerIdNone; } }

        public bool CombatMode { get { return combatMode; } }

        public void SetCombatMode(bool value)
        {
            combatMode = value;
        }

        void OnDisable()
        {
            ForceRelease();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != pointerIdNone)
                return;                                 // second finger ignored

            if (IsInGripCorner(eventData.position))
                return;                                 // resting thumb, not a look drag

            activePointerId = eventData.pointerId;
        }

        /// <summary>
        /// A touch starting in a grip corner is almost certainly a hand holding the device.
        /// Without this, a resting thumb claims the single look pointer and every real drag
        /// is silently ignored for as long as the thumb stays down.
        /// </summary>
        bool IsInGripCorner(Vector2 screenPos)
        {
            if (cornerDeadMarginInches <= 0f)
                return false;

            float m = MobileInput.InchesToPixels(cornerDeadMarginInches);

            bool nearSide = screenPos.x <= m || screenPos.x >= Screen.width - m;
            if (!nearSide)
                return false;

            if (screenPos.y <= m)
                return true;

            return rejectTopCorners && screenPos.y >= Screen.height - m;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;

            Vector2 delta = eventData.delta;

            if (delta.magnitude > deltaSpikeLimit)
                return;

            accumulated.x += delta.x;
            accumulated.y += delta.y * verticalScale * (invertY ? -1f : 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;

            activePointerId = pointerIdNone;
        }

        public void ForceRelease()
        {
            activePointerId = pointerIdNone;
            accumulated = Vector2.zero;
        }

        /// <summary>Read and clear this frame's accumulated pixel delta.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 delta = accumulated;
            accumulated = Vector2.zero;
            return delta;
        }
    }
}
