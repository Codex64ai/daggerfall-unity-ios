// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Left-thumb movement stick. Attach to the joystick BACKGROUND image and assign
    /// the knob child as 'handle'. Value.y = forward/back, Value.x = strafe, both -1..1.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(-100)]
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

        [Tooltip("On touch devices, read Input.touches directly instead of relying on UGUI " +
                 "pointer events. Removes every UGUI variable from the equation - raycast " +
                 "order, drag thresholds, input-module quirks - which matters because the " +
                 "sticks are the two controls that must never miss a touch. The classic-menu " +
                 "cursor already works this way. Editor/mouse keeps the UGUI path.")]
        public bool directTouchOnDevice = true;

        [Tooltip("Normalized screen region this stick also claims touches from (zero = only its " +
                 "own rect). A grab landing anywhere in the region snaps the stick under the " +
                 "thumb - so an imprecise grab near the ring still moves the player instead of " +
                 "falling through to whatever is underneath.")]
        public Rect screenClaimRegion = new Rect(0f, 0f, 0f, 0f);

        public Vector2 Value { get; private set; }

        public bool IsHeld { get { return activePointerId != pointerIdNone || directFingerId >= 0; } }

        RectTransform rect;
        CanvasGroup canvasGroup;
        Canvas parentCanvas;
        Vector2 homeAnchoredPosition;
        int activePointerId = pointerIdNone;

        // Finger ids claimed across ALL sticks, so one finger can never drive two.
        static readonly HashSet<int> claimedFingers = new HashSet<int>();
        static readonly HashSet<VirtualJoystick> activeJoysticks = new HashSet<VirtualJoystick>();

        /// <summary>Set by the layout editor: sticks read raw touches and bypass UGUI, so
        /// without this they keep claiming fingers (and walking the player) behind the
        /// edit overlay.</summary>
        public static bool SuppressDirectTouch;

        /// <summary>True while ANY stick owns this finger. The look zone consults this so
        /// a stick-claimed finger can never also drag the camera.</summary>
        public static bool IsFingerClaimed(int fingerId)
        {
            return claimedFingers.Contains(fingerId);
        }
        int directFingerId = -1;
        bool recentered;
        Vector2 preGrabPosition;

        bool DirectTouchActive
        {
            get { return directTouchOnDevice && Input.touchSupported && !Application.isEditor; }
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            parentCanvas = GetComponentInParent<Canvas>();
            homeAnchoredPosition = rect.anchoredPosition;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = idleAlpha;
        }

        void OnEnable()
        {
            activeJoysticks.Add(this);
        }

        void OnDisable()
        {
            activeJoysticks.Remove(this);
            ForceRelease();
        }

        /// <summary>
        /// True when a screen point belongs to any active stick's visual rect or
        /// configured grab territory. The look zone uses this before claiming a new
        /// finger, because its Update can run before the stick's Update in the same
        /// frame. Stick territories must win that race.
        /// </summary>
        public static bool ClaimsScreenPoint(Vector2 screenPos)
        {
            foreach (VirtualJoystick joystick in activeJoysticks)
            {
                Camera cam = (joystick.parentCanvas != null &&
                              joystick.parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? joystick.parentCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(joystick.rect, screenPos, cam) ||
                    joystick.InClaimRegion(screenPos))
                    return true;
            }

            return false;
        }

        void Update()
        {
            if (!DirectTouchActive)
                return;

            if (SuppressDirectTouch)
            {
                if (directFingerId >= 0)
                    ForceRelease();
                return;
            }

            Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera : null;

            if (directFingerId < 0)
            {
                // Claim the first new touch that lands on this stick.
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    // Touches on the classic bottom bar belong to its icons, never to a stick.
                    if (t.phase == TouchPhase.Began && MobileClassicHud.ContainsScreenPoint(t.position))
                        continue;

                    // A touch may begin while the keyboard HUD is still hidden. Once
                    // the handover restores this object, adopt its first movement
                    // sample instead of losing the gesture forever.
                    if ((t.phase != TouchPhase.Began && t.phase != TouchPhase.Moved) ||
                        claimedFingers.Contains(t.fingerId))
                        continue;

                    bool onStick = RectTransformUtility.RectangleContainsScreenPoint(rect, t.position, cam);
                    bool inRegion = !onStick && InClaimRegion(t.position);
                    if (!onStick && !inRegion)
                        continue;

                    // Buttons beat territories. The claim regions are big on purpose, and
                    // action buttons live inside them (and the player can drag buttons
                    // anywhere with the layout editor) - a touch that lands on any
                    // interactive control belongs to that control, never to a stick.
                    if (IsOverInteractive(t.position))
                        continue;

                    directFingerId = t.fingerId;
                    claimedFingers.Add(t.fingerId);
                    canvasGroup.alpha = 1f;

                    if (inRegion)
                        RecenterUnder(t.position, cam);   // snap the stick to the thumb

                    Debug.Log(string.Format("[Stick:{0}] claimed finger {1} at {2} ({3})",
                        name, t.fingerId, t.position, inRegion ? "region" : "rect"));

                    UpdateFromScreenPoint(t.position, cam);
                    break;
                }
                return;
            }

            // Track our claimed finger by id, not index - indices shuffle as fingers lift.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != directFingerId)
                    continue;

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    ForceRelease();
                else
                    UpdateFromScreenPoint(t.position, cam);
                return;
            }

            ForceRelease();   // finger vanished without an Ended phase
        }

        static readonly List<UnityEngine.EventSystems.RaycastResult> raycastScratch =
            new List<UnityEngine.EventSystems.RaycastResult>();

        public static bool IsOverInteractive(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
                return false;

            var pointer = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos };
            raycastScratch.Clear();
            es.RaycastAll(pointer, raycastScratch);

            for (int i = 0; i < raycastScratch.Count; i++)
            {
                GameObject go = raycastScratch[i].gameObject;
                if (go == null)
                    continue;
                if (go.GetComponentInParent<UnityEngine.UI.Selectable>() != null ||
                    go.GetComponentInParent<MobileActionButton>() != null)
                    return true;
            }
            return false;
        }

        bool InClaimRegion(Vector2 screenPos)
        {
            if (screenClaimRegion.width <= 0f || screenClaimRegion.height <= 0f)
                return false;
            float nx = screenPos.x / Screen.width;
            float ny = screenPos.y / Screen.height;
            return screenClaimRegion.Contains(new Vector2(nx, ny));
        }

        void RecenterUnder(Vector2 screenPos, Camera cam)
        {
            RectTransform parent = rect.parent as RectTransform;
            Vector2 local;
            if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPos, cam, out local))
                return;

            // Restore point captured at grab time, NOT at Awake - the inch-based layout
            // (and the player's own layout edits) move the stick after Awake runs.
            preGrabPosition = rect.anchoredPosition;
            recentered = true;

            // ScreenPointToLocalPointInRectangle is relative to the PARENT'S PIVOT, but
            // anchoredPosition is relative to this rect's ANCHOR point. Assigning one to
            // the other teleports the stick by half the parent - which read as a constant
            // full-deflection input from wherever the finger actually was. Convert frames.
            Vector2 anchorOffset = new Vector2(
                (rect.anchorMin.x - parent.pivot.x) * parent.rect.width,
                (rect.anchorMin.y - parent.pivot.y) * parent.rect.height);
            rect.anchoredPosition = local - anchorOffset;
        }

        void UpdateFromScreenPoint(Vector2 screenPos, Camera cam)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, cam, out local))
                return;

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

        public void OnPointerDown(PointerEventData eventData)
        {
            if (DirectTouchActive)
                return;                                 // Update() owns touches on device

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
            if (directFingerId >= 0)
            {
                claimedFingers.Remove(directFingerId);
                Debug.Log("[Stick:" + name + "] released finger " + directFingerId);
            }
            directFingerId = -1;
            activePointerId = pointerIdNone;
            Value = Vector2.zero;

            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
            if (recentered && rect != null)
            {
                rect.anchoredPosition = preGrabPosition;
                recentered = false;
            }
            else if (floating && rect != null)
                rect.anchoredPosition = homeAnchoredPosition;
            if (canvasGroup != null)
                canvasGroup.alpha = idleAlpha;
        }
    }
}
