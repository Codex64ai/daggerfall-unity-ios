// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   On-device tuning overlay. Builds its own UI at runtime so there is nothing to wire
//   in the editor and nothing to keep in sync with a prefab.
//
//   This exists because touch feel cannot be calibrated without a real finger, and
//   without it every adjustment costs a full IL2CPP rebuild. Values persist in
//   PlayerPrefs and apply live.
//
//   Deliberately NOT part of Daggerfall's classic UI: this must remain reachable even
//   when the classic UI is misbehaving, and it must not be diverted through the virtual
//   cursor. It is plain UGUI on the mobile canvas.
//

using UnityEngine;
using UnityEngine.UI;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileSettingsPanel : MonoBehaviour
    {
        const string prefix = "DFMobile.";

        [Header("Wiring (auto-filled by MobileHudBuilder)")]
        public MobileInputController controller;
        public MobileHudLayout layout;
        public TouchLookZone lookZone;
        public VirtualJoystick joystick;
        public CanvasGroup hudGroup;
        public MobileLayoutEditor layoutEditor;

        [Header("Panel")]
        public Vector2 panelSize = new Vector2(760f, 930f);

        RectTransform panel;
        Text header;
        bool built;
        bool open;

        void Start()
        {
            LoadPrefs();
            ApplyAll();
        }

        public bool IsOpen { get { return open; } }

        /// <summary>
        /// MobileHudBuilder puts MobileControllerProbe on the same object as the controller,
        /// so this needs no separate wiring field.
        /// </summary>
        MobileControllerProbe Probe
        {
            get { return controller != null ? controller.GetComponent<MobileControllerProbe>() : null; }
        }

        /// <summary>Wire to the gear button.</summary>
        public void Toggle()
        {
            if (!built)
                Build();

            open = !open;
            panel.gameObject.SetActive(open);

            if (open)
                RefreshHeader();
        }

        public void Close()
        {
            if (built && open)
            {
                open = false;
                panel.gameObject.SetActive(false);
            }
        }

        #region Build

        void Build()
        {
            built = true;

            panel = NewRect("SettingsPanel", (RectTransform)transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = panelSize;

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.04f, 0.94f);
            bg.raycastTarget = true;

            header = AddLabel(panel, "Touch Controls", 30, TextAnchor.UpperCenter,
                              new Vector2(0f, -14f), new Vector2(panelSize.x - 40f, 76f));

            float y = -100f;
            const float rowH = 62f;

            AddSlider(panel, ref y, rowH, "Look sensitivity", 0.04f, 0.45f,
                () => controller != null ? controller.touchToMouseScale : 0.15f,
                v => { if (controller != null) controller.touchToMouseScale = v; ReapplyThreshold(); },
                "look", "0.000");

            AddSlider(panel, ref y, rowH, "Look stick speed", 60f, 480f,
                () => controller != null ? controller.lookStickSpeed : 220f,
                v => { if (controller != null) controller.lookStickSpeed = v; },
                "stickspeed", "0");

            AddSlider(panel, ref y, rowH, "Swipe to attack (inches)", 0.3f, 2.5f,
                () => controller != null ? controller.swipeDistanceInches : 0.9f,
                v => { if (controller != null) controller.swipeDistanceInches = v; ReapplyThreshold(); },
                "swipe", "0.00");

            AddSlider(panel, ref y, rowH, "Control size", 0.6f, 1.8f,
                () => layout != null ? layout.hudScale : 1f,
                v => { if (layout != null) { layout.hudScale = v; layout.Apply(); } },
                "hudscale", "0.00");

            AddSlider(panel, ref y, rowH, "Control opacity", 0.15f, 1f,
                () => hudGroup != null ? hudGroup.alpha : 1f,
                v => { if (hudGroup != null) hudGroup.alpha = v; },
                "hudalpha", "0.00");

            AddSlider(panel, ref y, rowH, "Palm rejection (inches)", 0f, 1.0f,
                () => lookZone != null ? lookZone.cornerDeadMarginInches : 0.45f,
                v => { if (lookZone != null) lookZone.cornerDeadMarginInches = v; },
                "palm", "0.00");

            AddToggle(panel, ref y, rowH, "Direct touch in menus",
                () => controller != null && controller.virtualMouse != null && controller.virtualMouse.absoluteMode,
                v => { if (controller != null && controller.virtualMouse != null) controller.virtualMouse.absoluteMode = v; },
                "directtouch");

            AddToggle(panel, ref y, rowH, "Invert look Y",
                () => lookZone != null && lookZone.invertY,
                v => { if (lookZone != null) lookZone.invertY = v; },
                "inverty");

            AddToggle(panel, ref y, rowH, "Show diagnostics",
                () => controller != null && controller.showGestureDebug,
                v => { if (controller != null) controller.showGestureDebug = v; },
                "debug");

            // Escape hatch for a controller whose buttons report unexpected numbers: the
            // player can run the probe from a shipping build and send back the summary,
            // instead of needing a special build. Enable it BEFORE connecting the
            // controller - this panel lives on the touch HUD, which hides itself as soon
            // as a gamepad appears.
            AddToggle(panel, ref y, rowH, "Controller probe overlay",
                () => Probe != null && Probe.active,
                v => { if (Probe != null) Probe.active = v; },
                "ctrlprobe");

            AddToggle(panel, ref y, rowH, "Floating joystick",
                () => joystick != null && joystick.floating,
                v => { if (joystick != null) joystick.floating = v; },
                "floatstick");

            AddButton(panel, ref y, rowH, "Edit layout (drag / resize / hide)", () =>
            {
                Close();
                if (layoutEditor != null)
                    layoutEditor.Enter();
            });

            AddButton(panel, ref y, rowH, "Apply gamepad defaults", () =>
            {
                MobileGamepadBindings.Apply();
                RefreshHeader();
            });

            AddButton(panel, ref y, rowH, "Close", Close);

            panel.gameObject.SetActive(false);
        }

        void RefreshHeader()
        {
            if (header == null)
                return;

            header.text = string.Format("Touch Controls\n{0:0} dpi   {1:0.0}in screen   gamepad: {2}",
                MobileInput.Dpi,
                MobileHudLayout.ScreenDiagonalInches,
                (controller != null && controller.ControllerConnected) ? "yes" : "no");
        }

        void ReapplyThreshold()
        {
            // Swipe distance and look scale both feed WeaponManager.AttackThreshold, so it
            // has to be recomputed whenever either moves or attacks silently mistune.
            if (controller != null)
                controller.RefreshAttackThreshold();
        }

        #endregion

        #region UI helpers

        static RectTransform NewRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Text AddLabel(RectTransform parent, string text, int size, TextAnchor anchor,
                             Vector2 pos, Vector2 sizeDelta)
        {
            RectTransform rt = NewRect("Label", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Text t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = new Color(0.96f, 0.92f, 0.80f, 1f);
            t.raycastTarget = false;
            t.font = BuiltinFont();
            return t;
        }

        static Font BuiltinFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null)
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        RectTransform AddRow(RectTransform parent, ref float y, float rowH, out RectTransform right)
        {
            RectTransform row = NewRect("Row", parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(panelSize.x - 48f, rowH - 8f);
            y -= rowH;

            right = row;
            return row;
        }

        void AddSlider(RectTransform parent, ref float y, float rowH, string label,
                       float min, float max,
                       System.Func<float> get, System.Action<float> set,
                       string key, string format)
        {
            RectTransform right;
            RectTransform row = AddRow(parent, ref y, rowH, out right);

            Text caption = AddLabel(row, label, 22, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
            RectTransform crt = caption.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0.44f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            Text value = AddLabel(row, "", 22, TextAnchor.MiddleRight, Vector2.zero, Vector2.zero);
            RectTransform vrt = value.rectTransform;
            vrt.anchorMin = new Vector2(0.86f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;

            // --- slider ---
            RectTransform srt = NewRect("Slider", row);
            srt.anchorMin = new Vector2(0.46f, 0.22f);
            srt.anchorMax = new Vector2(0.84f, 0.78f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            Image track = srt.gameObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.18f);

            Slider slider = srt.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.transition = Selectable.Transition.None;

            RectTransform fillArea = NewRect("FillArea", srt);
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;

            RectTransform fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.85f, 0.70f, 0.35f, 0.95f);

            RectTransform handleArea = NewRect("HandleArea", srt);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = Vector2.zero;
            handleArea.offsetMax = Vector2.zero;

            RectTransform handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(34f, 44f);
            Image handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.98f, 0.95f, 0.86f, 1f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            float initial = Mathf.Clamp(PlayerPrefs.GetFloat(prefix + key, get()), min, max);
            slider.value = initial;
            set(initial);
            value.text = initial.ToString(format);

            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                value.text = v.ToString(format);
                PlayerPrefs.SetFloat(prefix + key, v);
                PlayerPrefs.Save();
            });
        }

        void AddToggle(RectTransform parent, ref float y, float rowH, string label,
                       System.Func<bool> get, System.Action<bool> set, string key)
        {
            RectTransform right;
            RectTransform row = AddRow(parent, ref y, rowH, out right);

            Text caption = AddLabel(row, label, 22, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
            RectTransform crt = caption.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0.7f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            RectTransform brt = NewRect("Toggle", row);
            brt.anchorMin = new Vector2(0.74f, 0.15f);
            brt.anchorMax = new Vector2(0.98f, 0.85f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            Image bgi = brt.gameObject.AddComponent<Image>();
            Button btn = brt.gameObject.AddComponent<Button>();
            Text state = AddLabel(brt, "", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            RectTransform strt = state.rectTransform;
            strt.anchorMin = Vector2.zero;
            strt.anchorMax = Vector2.one;
            strt.offsetMin = Vector2.zero;
            strt.offsetMax = Vector2.zero;

            bool current = PlayerPrefs.GetInt(prefix + key, get() ? 1 : 0) == 1;
            set(current);

            System.Action paint = () =>
            {
                bgi.color = current ? new Color(0.55f, 0.45f, 0.18f, 0.95f) : new Color(1f, 1f, 1f, 0.14f);
                state.text = current ? "ON" : "OFF";
            };
            paint();

            btn.onClick.AddListener(() =>
            {
                current = !current;
                set(current);
                PlayerPrefs.SetInt(prefix + key, current ? 1 : 0);
                PlayerPrefs.Save();
                paint();
            });
        }

        void AddButton(RectTransform parent, ref float y, float rowH, string label, System.Action onClick)
        {
            RectTransform right;
            RectTransform row = AddRow(parent, ref y, rowH, out right);

            Image bgi = row.gameObject.AddComponent<Image>();
            bgi.color = new Color(1f, 1f, 1f, 0.16f);
            Button btn = row.gameObject.AddComponent<Button>();

            Text caption = AddLabel(row, label, 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            RectTransform crt = caption.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(() => onClick());
        }

        #endregion

        #region Prefs

        void LoadPrefs()
        {
            // Sliders read their own pref on build; this covers values needed before the
            // panel has ever been opened.
            if (controller != null)
            {
                controller.touchToMouseScale = PlayerPrefs.GetFloat(prefix + "look", controller.touchToMouseScale);
                controller.swipeDistanceInches = PlayerPrefs.GetFloat(prefix + "swipe", controller.swipeDistanceInches);
            }
            if (layout != null)
                layout.hudScale = PlayerPrefs.GetFloat(prefix + "hudscale", layout.hudScale);
            if (hudGroup != null)
                hudGroup.alpha = PlayerPrefs.GetFloat(prefix + "hudalpha", hudGroup.alpha);
            if (lookZone != null)
            {
                lookZone.cornerDeadMarginInches = PlayerPrefs.GetFloat(prefix + "palm", lookZone.cornerDeadMarginInches);
                lookZone.invertY = PlayerPrefs.GetInt(prefix + "inverty", lookZone.invertY ? 1 : 0) == 1;
            }
            if (joystick != null)
                joystick.floating = PlayerPrefs.GetInt(prefix + "floatstick", joystick.floating ? 1 : 0) == 1;
        }

        void ApplyAll()
        {
            if (layout != null)
                layout.Apply();
            ReapplyThreshold();
        }

        #endregion
    }
}
