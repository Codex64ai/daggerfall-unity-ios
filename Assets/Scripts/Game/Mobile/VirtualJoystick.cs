// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License

using UnityEngine;
using UnityEngine.EventSystems;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Left-thumb movement stick. Attach to the joystick BACKGROUND image and assign
    /// the knob child as 'handle'. Value.y = forward/back, Value.x = strafe, both -1..1.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int pointerIdNone = -999;

        [Tooltip("Knob that visually follows the thumb. Its Raycast Target must be OFF.")]
        public RectTransform handle;

        [Tooltip("Knob travel as a fraction of the background's half extents.")]
        [Range(0.3f, 1f)] public float handleRange = 0.75f;

        [Tooltip("Ignore deflection below this magnitude.")]
        [Range(0f, 0.4f)] public float deadZone = 0.15f;

        [Tooltip("Recentre the stick under the thumb on touch-down.")]
        public bool floating = false;

        [Tooltip("Alpha when untouched.")]
        [Range(0f, 1f)] public float idleAlpha = 0.35f;

        public Vector2 Value { get; private set; }

        public bool IsHeld { get { return activePointerId != pointerIdNone; } }

        RectTransform rect;
        CanvasGroup canvasGroup;
        Vector2 homeAnchoredPosition;
        int activePointerId = pointerIdNone;

        void Awake()
        {
            rect = (RectTransform)transform;
            homeAnchoredPosition = rect.anchoredPosition;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = idleAlpha;
        }

        void OnDisable()
        {
            ForceRelease();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != pointerIdNone)
                return;                                 // one thumb owns the stick

            activePointerId = eventData.pointerId;
            canvasGroup.alpha = 1f;

            if (floating)
            {
                RectTransform parent = rect.parent as RectTransform;
                Vector2 local;
                if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent, eventData.position, eventData.pressEventCamera, out local))
                    rect.anchoredPosition = local;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out local))
                return;

            // ScreenPointToLocalPointInRectangle is relative to the pivot, so normalise
            // against half extents. Assumes a centred pivot (0.5, 0.5).
            Vector2 half = rect.rect.size * 0.5f;
            if (half.x <= 0f || half.y <= 0f)
                return;

            Vector2 normalized = new Vector2(local.x / half.x, local.y / half.y);
            if (normalized.magnitude > 1f)
                normalized = normalized.normalized;

            Value = (normalized.magnitude < deadZone) ? Vector2.zero : normalized;

            if (handle != null)
                handle.anchoredPosition = normalized * half * handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;

            ForceRelease();
        }

        public void ForceRelease()
        {
            activePointerId = pointerIdNone;
            Value = Vector2.zero;

            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
            if (floating && rect != null)
                rect.anchoredPosition = homeAnchoredPosition;
            if (canvasGroup != null)
                canvasGroup.alpha = idleAlpha;
        }
    }
}
