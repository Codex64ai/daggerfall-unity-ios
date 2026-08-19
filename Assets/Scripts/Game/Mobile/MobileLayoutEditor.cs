// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   In-game layout editor for the touch controls: drag any control where you want it,
//   resize it, hide the ones you never use, or reset the lot. Opened from TUNE.
//
//   Mechanism: a full-screen catcher panel sits on top of everything and owns every
//   touch while editing - so the controls underneath never fire their normal actions.
//   Drags move the element live; on release the position is stored in INCHES (via
//   MobileHudLayout's override store), so a layout arranged on an iPad still lands
//   sensibly on an iPhone.
//
//   MENU and TUNE are deliberately not hideable: hiding them would lock the player out
//   of the editor itself with no way back.
//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileLayoutEditor : MonoBehaviour
    {
        [Header("Wiring (auto-filled by MobileHudBuilder)")]
        public MobileHudLayout layout;
        public Canvas canvas;
        public GameObject gameplayLayer;

        static readonly HashSet<string> neverHide = new HashSet<string> { "MenuToggle", "SettingsGear" };

        RectTransform overlay;
        RectTransform highlight;
        Text selectionLabel;
        bool open;

        // current selection: either a layout element (movable) or a lone button (hide-only)
        MobileHudLayout.Element selectedElement;
        GameObject selectedButton;

        public bool IsOpen { get { return open; } }

        #region Enter / Exit

        public void Enter()
        {
            if (open)
                return;

            if (layout == null || canvas == null)
            {
                Debug.LogWarning("[MobileLayoutEditor] not wired; cannot open");
                return;
            }

            open = true;

            // Hidden controls must be visible (ghosted) so they can be un-hidden.
            layout.suppressHiding = true;
            foreach (var e in layout.elements)
                if (e != null && e.target != null)
                    e.target.gameObject.SetActive(true);
            foreach (var b in AllButtons(true))
                if (MobileHudLayout.GetHiddenOverride(b.name))
                    b.SetActive(true);

            if (overlay == null)
                Build();
            overlay.gameObject.SetActive(true);
            Select(null, null);

            // Raw-touch readers bypass the UGUI overlay, so silence them explicitly.
            VirtualJoystick.SuppressDirectTouch = true;
        }

        void Update()
        {
            // On device, drive editing from raw touches. UGUI pointer-DOWN works on this
            // hardware but OnDrag proved unreliable (the original joystick bug) - the
            // editor uses the same direct-read pattern that fixed the sticks.
            if (!open || !Input.touchSupported || Application.isEditor)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (editFingerId < 0 && t.phase == TouchPhase.Began)
                {
                    // Toolbar buttons are UGUI and sit on top - let them have their touch.
                    if (IsOverToolbar(t.position))
                        continue;
                    editFingerId = t.fingerId;
                    HitTest(t.position);
                    continue;
                }

                if (t.fingerId != editFingerId)
                    continue;

                if (t.phase == TouchPhase.Moved)
                    DragSelected(t.deltaPosition);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    PersistSelectedPosition();
                    editFingerId = -1;
                }
            }
        }

        int editFingerId = -1;

        bool IsOverToolbar(Vector2 screenPos)
        {
            if (overlay == null)
                return false;
            Transform bar = overlay.Find("Bar");
            if (bar == null)
                return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)bar, screenPos, cam);
        }

        public void Exit()
        {
            if (!open)
                return;

            open = false;
            overlay.gameObject.SetActive(false);
            editFingerId = -1;
            VirtualJoystick.SuppressDirectTouch = false;

            layout.suppressHiding = false;
            layout.Apply();                                 // re-applies element hiding

            foreach (var b in AllButtons(true))             // apply per-button hiding
                if (!neverHide.Contains(b.name))
                    b.SetActive(!MobileHudLayout.GetHiddenOverride(b.name));

            PlayerPrefs.Save();
        }

        #endregion

        #region Selection and hit testing

        void Select(MobileHudLayout.Element element, GameObject button)
        {
            selectedElement = element;
            selectedButton = button;
            RefreshChrome();
        }

        void RefreshChrome()
        {
            RectTransform target = SelectedRect();
            highlight.gameObject.SetActive(target != null);
            if (target != null)
            {
                highlight.position = target.position;
                highlight.sizeDelta = target.sizeDelta + new Vector2(16f, 16f);
                highlight.pivot = target.pivot;
            }

            string name = SelectedName();
            if (string.IsNullOrEmpty(name))
            {
                selectionLabel.text = "Tap a control to select it. Drag to move.";
            }
            else
            {
                bool hidden = MobileHudLayout.GetHiddenOverride(name);
                bool movable = selectedElement != null && selectedElement.applyPosition;
                selectionLabel.text = name
                    + (hidden ? "  (hidden in play)" : "")
                    + (movable ? "" : "  (tap Hide - moves with its group)");
            }
        }

        RectTransform SelectedRect()
        {
            if (selectedElement != null) return selectedElement.target;
            if (selectedButton != null) return (RectTransform)selectedButton.transform;
            return null;
        }

        string SelectedName()
        {
            if (selectedElement != null) return selectedElement.name;
            if (selectedButton != null) return selectedButton.name;
            return null;
        }

        void HitTest(Vector2 screenPos)
        {
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // Individual buttons first (smaller targets, hide-only when in a grid).
            GameObject bestButton = null;
            float bestArea = float.MaxValue;
            foreach (var b in AllButtons(false))
            {
                RectTransform rt = (RectTransform)b.transform;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                    continue;
                float area = rt.rect.width * rt.rect.height;
                if (area < bestArea) { bestArea = area; bestButton = b; }
            }

            // Then layout elements; prefer the smallest hit so knob-sized things win.
            MobileHudLayout.Element bestElement = null;
            foreach (var e in layout.elements)
            {
                if (e == null || e.target == null || !e.applyPosition)
                    continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(e.target, screenPos, cam))
                    continue;
                float area = e.target.rect.width * e.target.rect.height;
                if (area < bestArea) { bestArea = area; bestElement = e; bestButton = null; }
            }

            // A button inside a selected group: element wins only if strictly smaller,
            // so tapping WEAPONButton selects the button, tapping bank padding selects the bank.
            if (bestElement != null) Select(bestElement, null);
            else if (bestButton != null) Select(null, bestButton);
            else Select(null, null);
        }

        IEnumerable<GameObject> AllButtons(bool includeInactive)
        {
            if (gameplayLayer == null)
                yield break;

            foreach (var b in gameplayLayer.GetComponentsInChildren<MobileActionButton>(includeInactive))
                yield return b.gameObject;
        }

        #endregion

        #region Drag handling

        class Catcher : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public MobileLayoutEditor owner;
            public void OnPointerDown(PointerEventData e) { owner.OnDown(e); }
            public void OnDrag(PointerEventData e) { owner.OnDragged(e); }
            public void OnPointerUp(PointerEventData e) { owner.OnUp(e); }
        }

        void OnDown(PointerEventData e)
        {
            HitTest(e.position);
        }

        void OnDragged(PointerEventData e)
        {
            DragSelected(e.delta);
        }

        void DragSelected(Vector2 screenDelta)
        {
            if (selectedElement == null || selectedElement.target == null || !selectedElement.applyPosition)
                return;

            float scale = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            selectedElement.target.anchoredPosition += screenDelta / scale;
            RefreshChrome();
        }

        void OnUp(PointerEventData e)
        {
            PersistSelectedPosition();
        }

        void PersistSelectedPosition()
        {
            if (selectedElement == null || selectedElement.target == null || !selectedElement.applyPosition)
                return;

            // anchoredPosition = margin * unitsPerInch * sign  ->  invert to store inches.
            float upi = layout.UnitsPerInch * Mathf.Max(layout.hudScale, 0.0001f);
            Vector2 a = selectedElement.target.anchorMin;
            float sx = (a.x > 0.5f) ? -1f : 1f;
            float sy = (a.y > 0.5f) ? -1f : 1f;
            Vector2 pos = selectedElement.target.anchoredPosition;
            MobileHudLayout.SetMarginOverride(selectedElement.name,
                new Vector2(pos.x * sx / upi, pos.y * sy / upi));
        }

        #endregion

        #region Toolbar actions

        void NudgeScale(float delta)
        {
            string name = SelectedName();
            if (name == null || selectedElement == null)
                return;
            float s = Mathf.Clamp(MobileHudLayout.GetScaleOverride(name) + delta, 0.5f, 2f);
            MobileHudLayout.SetScaleOverride(name, s);
            layout.Apply();
            RefreshChrome();
        }

        void ToggleHidden()
        {
            string name = SelectedName();
            if (name == null || neverHide.Contains(name))
                return;
            MobileHudLayout.SetHiddenOverride(name, !MobileHudLayout.GetHiddenOverride(name));
            RefreshChrome();
        }

        void ResetAll()
        {
            layout.ClearAllOverrides();
            foreach (var b in AllButtons(true))
            {
                MobileHudLayout.ClearOverrides(b.name);
                b.SetActive(true);
            }
            PlayerPrefs.Save();
            layout.Apply();
            Select(null, null);
        }

        #endregion

        #region UI construction

        void Build()
        {
            overlay = NewRect("LayoutEditOverlay", (RectTransform)canvas.transform);
            Stretch(overlay);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.25f, 0.05f, 0.20f);       // faint green = edit mode
            dim.raycastTarget = true;
            overlay.gameObject.AddComponent<Catcher>().owner = this;

            highlight = NewRect("Highlight", overlay);
            Image hl = highlight.gameObject.AddComponent<Image>();
            hl.color = new Color(1f, 0.85f, 0.3f, 0.25f);
            hl.raycastTarget = false;

            // top toolbar
            RectTransform bar = NewRect("Bar", overlay);
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -110f); bar.offsetMax = Vector2.zero;
            Image barBg = bar.gameObject.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.05f, 0.04f, 0.92f);

            selectionLabel = AddText(bar, "", 24, TextAnchor.MiddleLeft);
            RectTransform lrt = selectionLabel.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.42f, 1f);
            lrt.offsetMin = new Vector2(20f, 0f); lrt.offsetMax = Vector2.zero;

            float x = 0.44f;
            AddBarButton(bar, "-", ref x, 0.06f, () => NudgeScale(-0.1f));
            AddBarButton(bar, "+", ref x, 0.06f, () => NudgeScale(+0.1f));
            AddBarButton(bar, "Hide/Show", ref x, 0.14f, ToggleHidden);
            AddBarButton(bar, "Reset all", ref x, 0.13f, ResetAll);
            AddBarButton(bar, "Done", ref x, 0.12f, Exit);
        }

        void AddBarButton(RectTransform bar, string label, ref float x, float width, System.Action onClick)
        {
            RectTransform rt = NewRect(label, bar);
            rt.anchorMin = new Vector2(x, 0.15f);
            rt.anchorMax = new Vector2(x + width, 0.85f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            x += width + 0.01f;

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.16f);
            Button b = rt.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => onClick());

            Text t = AddText(rt, label, 24, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }

        static RectTransform NewRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Text AddText(RectTransform parent, string text, int size, TextAnchor anchor)
        {
            RectTransform rt = NewRect("Text", parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = new Color(0.96f, 0.92f, 0.80f, 1f);
            t.raycastTarget = false;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        #endregion
    }
}
