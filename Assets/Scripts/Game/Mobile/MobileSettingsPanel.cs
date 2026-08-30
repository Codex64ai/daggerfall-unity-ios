// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Mobile Settings: the port's own options screen. Builds its own UI at runtime so there
//   is nothing to wire in the editor and nothing to keep in sync with a prefab. Values
//   persist in PlayerPrefs and apply live.
//
//   Reached from the pause menu (MobilePauseOptionsWindow adds the button), which pushes a
//   MobileSettingsWindow on top of the pause window. That host window is what keeps the game
//   paused and, being the top classic window, is the only thing DFU's own GUI pass draws -
//   so this UGUI panel is not painted over. Closing lands back on the pause menu.
//
//   Deliberately NOT part of Daggerfall's classic UI: it must remain reachable even when the
//   classic UI is misbehaving, and it must not be diverted through the virtual cursor. It is
//   plain UGUI on the mobile canvas, parented to the canvas root so it survives the HUD
//   layers hiding.
//
//   Layout: header, a row of section buttons (Input / HUD / Mods / Advanced), a scrolling
//   viewport for the active section, and a pinned Close. The panel is sized to the canvas,
//   never to its content - the previous fixed 930-unit box let the last rows fall off the
//   bottom of iPhone-shaped screens with no way to reach them.
//

using System.Collections;
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
        [Tooltip("Upper bound; the panel shrinks to fit the canvas with a margin.")]
        public Vector2 maxPanelSize = new Vector2(760f, 1000f);
        public float canvasMargin = 30f;

        public static MobileSettingsPanel Instance { get; private set; }

        RectTransform panel;
        Vector2 panelSize;
        Text header;
        bool built;
        bool open;
        MobileSettingsWindow hostWindow;

        // Sections. One content transform each; the scroll view shows the active one.
        enum Section { Input, HUD, Mods, Advanced }
        readonly RectTransform[] sectionContent = new RectTransform[4];
        readonly Image[] sectionTabs = new Image[4];
        Section activeSection = Section.Input;
        ScrollRect scroll;

        // Rows that show state owned elsewhere and need repainting on open / after a change.
        System.Action refreshDynamic;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

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

        #region Open / close

        /// <summary>Called by the host window when it is pushed over the pause menu.</summary>
        public void OpenFrom(MobileSettingsWindow host)
        {
            if (!built)
                Build();

            hostWindow = host;
            open = true;
            panel.gameObject.SetActive(true);

            // The classic virtual cursor has nothing to point at on this screen and would
            // float over the panel. Menu mode stays; the pause window gets it back on Close.
            MobileInput.VirtualCursorActive = false;

            ShowSection(activeSection);
            RefreshHeader();
            if (refreshDynamic != null)
                refreshDynamic();
        }

        /// <summary>Close row and Escape: hide the panel and pop the host, landing on the pause menu.</summary>
        public void Close()
        {
            if (!open)
                return;

            HidePanel();

            MobileSettingsWindow host = hostWindow;
            hostWindow = null;
            if (host != null && host.IsTop)
                host.CloseWindow();
        }

        /// <summary>The host window was popped by someone else (Escape, back button).</summary>
        public void OnHostClosed(MobileSettingsWindow host)
        {
            if (hostWindow != host)
                return;

            hostWindow = null;
            if (open)
                HidePanel();
        }

        void HidePanel()
        {
            open = false;
            if (panel != null)
                panel.gameObject.SetActive(false);

            if (MobileInput.MenuMode)
                MobileInput.VirtualCursorActive = true;
        }

        /// <summary>
        /// The layout editor needs the live gameplay HUD, and this panel is reached from the
        /// pause menu where that HUD is hidden. So: close ourselves, close the pause menu,
        /// let one frame resolve back to gameplay, then enter the editor.
        /// </summary>
        void EnterLayoutEditor()
        {
            if (layoutEditor == null)
                return;

            Close();
            StartCoroutine(EnterLayoutEditorWhenLive());
        }

        IEnumerator EnterLayoutEditorWhenLive()
        {
            // Pop whatever classic windows remain (the host is closing via message; the pause
            // menu is beneath it). Bounded so a misbehaving stack cannot spin this forever.
            for (int guard = 0; guard < 8 && DaggerfallUI.HasInstance &&
                 DaggerfallUI.UIManager != null && DaggerfallUI.UIManager.WindowCount > 0; guard++)
            {
                DaggerfallUI.UIManager.PopWindow();
                yield return null;
            }

            // One more frame for the controller to resolve Gameplay mode and show the HUD.
            yield return null;
            layoutEditor.Enter();
        }

        #endregion

        #region Build

        void Build()
        {
            built = true;

            // Size to the canvas, never to the content.
            RectTransform host = (RectTransform)transform;
            Rect canvasRect = host.rect;
            float availW = canvasRect.width > 0f ? canvasRect.width - 2f * canvasMargin : maxPanelSize.x;
            float availH = canvasRect.height > 0f ? canvasRect.height - 2f * canvasMargin : maxPanelSize.y;
            panelSize = new Vector2(Mathf.Min(maxPanelSize.x, availW), Mathf.Min(maxPanelSize.y, availH));

            panel = NewRect("SettingsPanel", host);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = panelSize;

            Image bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);
            bg.raycastTarget = true;

            const float headerH = 76f;
            const float rowH = 62f;
            const float pad = 12f;

            header = AddLabel(panel, "Mobile Settings", 30, TextAnchor.UpperCenter,
                              new Vector2(0f, -14f), new Vector2(panelSize.x - 40f, headerH));

            // --- section tabs -----------------------------------------------------------
            float tabsTop = -(14f + headerH + pad);
            BuildTabs(tabsTop, rowH);

            // --- pinned close ----------------------------------------------------------
            RectTransform closeRow = NewRect("CloseRow", panel);
            closeRow.anchorMin = closeRow.anchorMax = new Vector2(0.5f, 0f);
            closeRow.pivot = new Vector2(0.5f, 0f);
            closeRow.anchoredPosition = new Vector2(0f, pad);
            closeRow.sizeDelta = new Vector2(panelSize.x - 48f, rowH - 8f);
            MakeButton(closeRow, "Close", 24, Close);

            // --- scrolling viewport ----------------------------------------------------
            float viewportTop = tabsTop - rowH - pad;
            float viewportBottom = pad + rowH;          // above the close row
            RectTransform viewport = NewRect("Viewport", panel);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(24f, viewportBottom);
            viewport.offsetMax = new Vector2(-24f, viewportTop);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.02f);   // raycast surface for drag-scroll

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;

            // Scrollbar: thin, right edge of the viewport.
            RectTransform sbRect = NewRect("Scrollbar", panel);
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            sbRect.offsetMin = new Vector2(-20f, viewportBottom);
            sbRect.offsetMax = new Vector2(-8f, viewportTop);
            Image sbBg = sbRect.gameObject.AddComponent<Image>();
            sbBg.color = new Color(1f, 1f, 1f, 0.08f);
            Scrollbar sb = sbRect.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            RectTransform sliding = NewRect("Sliding", sbRect);
            sliding.anchorMin = Vector2.zero;
            sliding.anchorMax = Vector2.one;
            sliding.offsetMin = Vector2.zero;
            sliding.offsetMax = Vector2.zero;
            RectTransform handle = NewRect("Handle", sliding);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            Image handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.70f, 0.35f, 0.7f);
            sb.handleRect = handle;
            sb.targetGraphic = handleImg;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // --- sections ---------------------------------------------------------------
            float rowW = panelSize.x - 48f - 24f;
            for (int i = 0; i < sectionContent.Length; i++)
            {
                RectTransform content = NewRect("Section_" + (Section)i, viewport);
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(rowW, 0f);
                sectionContent[i] = content;
            }

            BuildInputSection(sectionContent[(int)Section.Input], rowW, rowH);
            BuildHudSection(sectionContent[(int)Section.HUD], rowW, rowH);
            BuildModsSection(sectionContent[(int)Section.Mods], rowW, rowH);
            BuildAdvancedSection(sectionContent[(int)Section.Advanced], rowW, rowH);

            ShowSection(Section.Input);
            panel.gameObject.SetActive(false);
        }

        void BuildTabs(float y, float rowH)
        {
            RectTransform bar = NewRect("Tabs", panel);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, y);
            bar.sizeDelta = new Vector2(panelSize.x - 48f, rowH - 8f);

            string[] names = { "Input", "HUD", "Mods", "Advanced" };
            float gap = 8f;
            float w = (bar.sizeDelta.x - gap * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform t = NewRect("Tab_" + names[i], bar);
                t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
                t.pivot = new Vector2(0f, 0.5f);
                t.anchoredPosition = new Vector2(i * (w + gap), 0f);
                t.sizeDelta = new Vector2(w, bar.sizeDelta.y);
                int idx = i;
                sectionTabs[i] = MakeButton(t, names[i], 24, () => ShowSection((Section)idx));
            }
        }

        void ShowSection(Section s)
        {
            activeSection = s;
            for (int i = 0; i < sectionContent.Length; i++)
            {
                bool on = i == (int)s;
                if (sectionContent[i] != null)
                    sectionContent[i].gameObject.SetActive(on);
                if (sectionTabs[i] != null)
                    sectionTabs[i].color = on ? new Color(0.55f, 0.45f, 0.18f, 0.95f) : new Color(1f, 1f, 1f, 0.16f);
            }
            if (scroll != null)
            {
                scroll.content = sectionContent[(int)s];
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        // ------------------------------------------------------------------ sections

        void BuildInputSection(RectTransform c, float rowW, float rowH)
        {
            float y = 0f;

            AddNote(c, ref y, rowW,
                "How you play. Auto detects a keyboard, mouse or controller and stands touch " +
                "down while one is used. Pick one to make it stick.");

            AddChoice(c, ref y, rowW, rowH, "Input",
                new[] { "Auto", "Touch", "Keyboard & mouse", "Controller" },
                () => controller != null ? (int)controller.InputMode : 0,
                v => { if (controller != null) controller.InputMode = (MobileInputMode)v; RefreshHeader(); });

            AddNote(c, ref y, rowW, "With touch off, a four-finger tap brings it back.");

            AddToggle(c, ref y, rowW, rowH, "Click to attack (mouse & controller)",
                () => controller != null && controller.clickToAttack,
                v => { if (controller != null) { controller.clickToAttack = v; controller.RefreshSwingMode(); } },
                "clickattack");
            AddNote(c, ref y, rowW,
                "Right button or pad button attacks on the press, no drag. Off: follow the " +
                "launcher's Weapon swing mode.");

            AddToggle(c, ref y, rowW, rowH, "Tap to attack (touch)",
                () => controller != null && controller.tapToAttack,
                v => { if (controller != null) { controller.tapToAttack = v; controller.RefreshSwingMode(); } },
                "tapattack");
            AddNote(c, ref y, rowW,
                "A quick tap in combat attacks and swipes only look. Off: swipe to attack, " +
                "the direction of the swipe picks the strike.");

            // Daggerfall's cursor mode (Return on a keyboard): arrow shown, camera parked, click
            // on world objects. Touch clears it automatically when it takes over, so this is
            // the rare deliberate use. Live state, not a preference.
            AddToggle(c, ref y, rowW, rowH, "Cursor mode (arrow, camera parked)",
                () => GameManager.HasInstance && GameManager.Instance.PlayerMouseLook != null &&
                      GameManager.Instance.PlayerMouseLook.cursorActive,
                v => { if (GameManager.HasInstance && GameManager.Instance.PlayerMouseLook != null)
                           GameManager.Instance.PlayerMouseLook.cursorActive = v; },
                null);

            AddSlider(c, ref y, rowW, rowH, "Look sensitivity", 0.04f, 0.45f,
                () => controller != null ? controller.touchToMouseScale : 0.15f,
                v => { if (controller != null) controller.touchToMouseScale = v; ReapplyThreshold(); },
                "look", "0.000");

            AddSlider(c, ref y, rowW, rowH, "Look stick speed", 60f, 480f,
                () => controller != null ? controller.lookStickSpeed : 220f,
                v => { if (controller != null) controller.lookStickSpeed = v; },
                "stickspeed", "0");

            AddSlider(c, ref y, rowW, rowH, "Swipe to attack (inches)", 0.3f, 2.5f,
                () => controller != null ? controller.swipeDistanceInches : 0.9f,
                v => { if (controller != null) controller.swipeDistanceInches = v; ReapplyThreshold(); },
                "swipe", "0.00");

            AddSlider(c, ref y, rowW, rowH, "Palm rejection (inches)", 0f, 1.0f,
                () => lookZone != null ? lookZone.cornerDeadMarginInches : 0.45f,
                v => { if (lookZone != null) lookZone.cornerDeadMarginInches = v; },
                "palm", "0.00");

            AddToggle(c, ref y, rowW, rowH, "Invert look Y",
                () => lookZone != null && lookZone.invertY,
                v => { if (lookZone != null) lookZone.invertY = v; },
                "inverty");

            // Mouse/trackpad only. GameController reports Y positive-up like Unity, so this
            // should stay off - it exists so a wrong sign on some device is a toggle, not a
            // rebuild (the pointer plugin cannot be exercised in the editor).
            AddToggle(c, ref y, rowW, rowH, "Invert pointer Y (mouse/trackpad)",
                () => controller != null && controller.pointerFlipY,
                v => { if (controller != null) controller.pointerFlipY = v; },
                "pointerflipy");

            AddToggle(c, ref y, rowW, rowH, "Direct touch in menus",
                () => controller != null && controller.virtualMouse != null && controller.virtualMouse.absoluteMode,
                v => { if (controller != null && controller.virtualMouse != null) controller.virtualMouse.absoluteMode = v; },
                "directtouch");

            AddToggle(c, ref y, rowW, rowH, "Floating joystick",
                () => joystick != null && joystick.floating,
                v => { if (joystick != null) joystick.floating = v; },
                "floatstick");

            AddButton(c, ref y, rowW, rowH, "Apply gamepad defaults", () =>
            {
                MobileGamepadBindings.Apply();
                RefreshHeader();
            });

            FinishSection(c, y);
        }

        void BuildHudSection(RectTransform c, float rowW, float rowH)
        {
            float y = 0f;

            AddSlider(c, ref y, rowW, rowH, "Control size", 0.6f, 1.8f,
                () => layout != null ? layout.hudScale : 1f,
                v => { if (layout != null) { layout.hudScale = v; layout.Apply(); } },
                "hudscale", "0.00");

            AddSlider(c, ref y, rowW, rowH, "Control opacity", 0.15f, 1f,
                () => hudGroup != null ? hudGroup.alpha : 1f,
                v => { if (hudGroup != null) hudGroup.alpha = v; },
                "hudalpha", "0.00");

            AddNote(c, ref y, rowW,
                "Edit layout closes the menus and lets you drag, resize or hide each touch control.");

            AddButton(c, ref y, rowW, rowH, "Edit layout (drag / resize / hide)", EnterLayoutEditor);

            FinishSection(c, y);
        }

        void BuildModsSection(RectTransform c, float rowW, float rowH)
        {
            float y = 0f;

            AddNote(c, ref y, rowW,
                "Built into this port. Each can be switched on or off here; none is needed to play.");

            // ONE switch for roads and real travel, by request: they are one experience. Real
            // travel walks the player along the road network, so roads without travel are
            // scenery and travel without roads is a straight line across country.
            //
            // The travel half applies live (it is consulted when the player next travels).
            // The roads half is read once before the first scene loads - terrain already
            // built this session cannot be repainted - so it records an intent for the next
            // launch, and the note below says so while the two disagree.
            Text roadsNote = null;
            AddToggle(c, ref y, rowW, rowH, "Roads & real travel",
                () => MobileJourneyController.JourneyModeEnabled,
                v =>
                {
                    MobileJourneyController.JourneyModeEnabled = v;
                    MobileRoads.Enabled = v;
                    if (roadsNote != null)
                        roadsNote.text = RoadsStatusText();
                },
                "journeymode");

            roadsNote = AddNote(c, ref y, rowW, RoadsStatusText());
            refreshDynamic += () => { if (roadsNote != null) roadsNote.text = RoadsStatusText(); };

            AddNote(c, ref y, rowW,
                "Fast travel becomes a journey: you walk to your destination along Daggerfall's " +
                "roads and tracks, at a time compression you control, and can stop anywhere. " +
                "Roads and tracks are drawn on the terrain (Hazelnut's Basic Roads, MIT), and " +
                "routes follow them.");

            FinishSection(c, y);
        }

        static string RoadsStatusText()
        {
            if (MobileRoads.RestartRequired)
                return MobileRoads.Enabled
                    ? "Roads: restart the app to draw them (travel walks already)."
                    : "Roads: restart the app to remove them from the terrain.";
            return MobileRoads.Active ? "Roads: active." : "Roads: off.";
        }

        void BuildAdvancedSection(RectTransform c, float rowW, float rowH)
        {
            float y = 0f;

            AddToggle(c, ref y, rowW, rowH, "Show diagnostics",
                () => controller != null && controller.showGestureDebug,
                v => { if (controller != null) controller.showGestureDebug = v; },
                "debug");

            AddNote(c, ref y, rowW,
                "On-screen readout of touches, sticks, input mode and the pointer plugin. " +
                "Applies from the next launch too.");

            // Escape hatch for a controller whose buttons report unexpected numbers: the
            // player can run the probe from a shipping build and send back the summary,
            // instead of needing a special build.
            AddToggle(c, ref y, rowW, rowH, "Controller probe overlay",
                () => Probe != null && Probe.active,
                v => { if (Probe != null) Probe.active = v; },
                "ctrlprobe");

            FinishSection(c, y);
        }

        static void FinishSection(RectTransform c, float y)
        {
            // y ran negative as rows were added; the content height is what the scroll view
            // measures against the viewport.
            c.sizeDelta = new Vector2(c.sizeDelta.x, -y + 8f);
        }

        void RefreshHeader()
        {
            if (header == null)
                return;

            header.text = string.Format("Mobile Settings\n{0:0} dpi   {1:0.0}in screen   input: {2}   gamepad: {3}   mouse: {4}",
                MobileInput.Dpi,
                MobileHudLayout.ScreenDiagonalInches,
                controller != null ? controller.InputMode.ToString() : "-",
                (controller != null && controller.ControllerConnected) ? "yes" : "no",
                MobilePointer.Connected ? (MobilePointer.IsLocked ? "locked" : "yes") : "no");
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>A button filling the given rect; returns its background so callers can tint it.</summary>
        static Image MakeButton(RectTransform rt, string label, int fontSize, System.Action onClick)
        {
            Image bgi = rt.gameObject.AddComponent<Image>();
            bgi.color = new Color(1f, 1f, 1f, 0.16f);
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            Text caption = AddLabel(rt, label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            Stretch(caption.rectTransform);

            btn.onClick.AddListener(() => onClick());
            return bgi;
        }

        static RectTransform AddRow(RectTransform parent, ref float y, float rowW, float rowH)
        {
            RectTransform row = NewRect("Row", parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(rowW, rowH - 8f);
            y -= rowH;
            return row;
        }

        /// <summary>Explanatory text between rows. Height follows the text.</summary>
        static Text AddNote(RectTransform parent, ref float y, float rowW, string text)
        {
            const int fontSize = 19;
            RectTransform row = NewRect("Note", parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);

            Text t = AddLabel(row, text, fontSize, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
            t.color = new Color(0.80f, 0.76f, 0.66f, 1f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(t.rectTransform);

            // Estimate the wrapped height without a layout pass: characters per line at this
            // size are roughly rowW / (0.48 * fontSize). Generous, so text never clips.
            float charsPerLine = Mathf.Max(20f, rowW / (0.48f * fontSize));
            int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length / charsPerLine) + text.Split('\n').Length - 1);
            float h = lines * (fontSize * 1.25f) + 10f;
            row.sizeDelta = new Vector2(rowW - 16f, h);
            y -= h + 6f;
            return t;
        }

        void AddSlider(RectTransform parent, ref float y, float rowW, float rowH, string label,
                       float min, float max,
                       System.Func<float> get, System.Action<float> set,
                       string key, string format)
        {
            RectTransform row = AddRow(parent, ref y, rowW, rowH);

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
            Stretch(fillArea);

            RectTransform fill = NewRect("Fill", fillArea);
            Stretch(fill);
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.85f, 0.70f, 0.35f, 0.95f);

            RectTransform handleArea = NewRect("HandleArea", srt);
            Stretch(handleArea);

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

        void AddToggle(RectTransform parent, ref float y, float rowW, float rowH, string label,
                       System.Func<bool> get, System.Action<bool> set, string key)
        {
            RectTransform row = AddRow(parent, ref y, rowW, rowH);

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
            btn.transition = Selectable.Transition.None;
            Text state = AddLabel(brt, "", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            Stretch(state.rectTransform);

            // A null key means the caller owns its own persistence. Without this, prefix + null
            // collapses to the bare prefix and every such toggle would share one pref.
            bool current = (key != null)
                ? PlayerPrefs.GetInt(prefix + key, get() ? 1 : 0) == 1
                : get();
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
                if (key != null)
                {
                    PlayerPrefs.SetInt(prefix + key, current ? 1 : 0);
                    PlayerPrefs.Save();
                }
                paint();
            });
        }

        /// <summary>
        /// One-of-N selector: a caption line with the options as buttons beneath it. The
        /// caller owns persistence (the value belongs to whoever exposes the getter).
        /// </summary>
        void AddChoice(RectTransform parent, ref float y, float rowW, float rowH, string label,
                       string[] options, System.Func<int> get, System.Action<int> set)
        {
            const float captionH = 34f;
            RectTransform row = NewRect("Choice", parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(rowW, captionH + rowH - 8f);
            y -= captionH + rowH;

            Text caption = AddLabel(row, label, 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(rowW, captionH));
            caption.rectTransform.anchorMin = new Vector2(0f, 1f);
            caption.rectTransform.anchorMax = new Vector2(1f, 1f);
            caption.rectTransform.offsetMin = new Vector2(0f, -captionH);
            caption.rectTransform.offsetMax = Vector2.zero;

            float gap = 8f;
            float w = (rowW - gap * (options.Length - 1)) / options.Length;
            Image[] bgs = new Image[options.Length];
            System.Action paint = () =>
            {
                int cur = get();
                for (int i = 0; i < bgs.Length; i++)
                    bgs[i].color = i == cur ? new Color(0.55f, 0.45f, 0.18f, 0.95f) : new Color(1f, 1f, 1f, 0.14f);
            };

            for (int i = 0; i < options.Length; i++)
            {
                RectTransform b = NewRect("Option_" + options[i], row);
                b.anchorMin = b.anchorMax = new Vector2(0f, 0f);
                b.pivot = new Vector2(0f, 0f);
                b.anchoredPosition = new Vector2(i * (w + gap), 0f);
                b.sizeDelta = new Vector2(w, rowH - 8f);
                int idx = i;
                bgs[i] = MakeButton(b, options[i], 20, () => { set(idx); paint(); });
            }
            paint();
            refreshDynamic += paint;
        }

        void AddButton(RectTransform parent, ref float y, float rowW, float rowH, string label, System.Action onClick)
        {
            RectTransform row = AddRow(parent, ref y, rowW, rowH);
            MakeButton(row, label, 24, onClick);
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
                // Toggles normally read their pref when the panel is first built, so a saved
                // "Show diagnostics" did nothing until the panel was opened.
                controller.showGestureDebug = PlayerPrefs.GetInt(prefix + "debug", controller.showGestureDebug ? 1 : 0) == 1;
                controller.clickToAttack = PlayerPrefs.GetInt(prefix + "clickattack", controller.clickToAttack ? 1 : 0) == 1;
                controller.tapToAttack = PlayerPrefs.GetInt(prefix + "tapattack", controller.tapToAttack ? 1 : 0) == 1;
                controller.pointerFlipY = PlayerPrefs.GetInt(prefix + "pointerflipy", controller.pointerFlipY ? 1 : 0) == 1;
                controller.lookStickSpeed = PlayerPrefs.GetFloat(prefix + "stickspeed", controller.lookStickSpeed);
                if (controller.virtualMouse != null)
                    controller.virtualMouse.absoluteMode =
                        PlayerPrefs.GetInt(prefix + "directtouch", controller.virtualMouse.absoluteMode ? 1 : 0) == 1;
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

            // Needed before the panel is ever opened, and more so than the rest: the travel
            // popup asks whether journey mode is on the first time the player travels, which
            // is usually long before they go looking through settings.
            MobileJourneyController.JourneyModeEnabled =
                PlayerPrefs.GetInt(prefix + "journeymode",
                                   MobileJourneyController.JourneyModeEnabled ? 1 : 0) == 1;

            // Roads and real travel are one switch now. A player from a build where they were
            // two follows their travel choice; the Mods section says if a restart is due.
            if (MobileRoads.Enabled != MobileJourneyController.JourneyModeEnabled)
                MobileRoads.Enabled = MobileJourneyController.JourneyModeEnabled;
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
