// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Sizes and positions the touch HUD in PHYSICAL INCHES rather than pixels or a
//   reference-resolution fraction.
//
//   A CanvasScaler keeps a layout proportional, which is not the same as usable. 150 px
//   at a 1920 reference is a comfortable button on a phone and a small target on a 13in
//   iPad, while corner-anchored controls that are an easy reach on a 6in phone are a
//   genuine stretch across 11in of tablet. Thumbs do not scale with screen size.
//
//   Conversion, accounting for the CanvasScaler:
//       canvasUnits = inches * dpi / canvas.scaleFactor
//
//   Apple's minimum touch target is 44pt (~0.29in); these defaults sit well above it.
//

using UnityEngine;
using UnityEngine.UI;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileHudLayout : MonoBehaviour
    {
        [System.Serializable]
        public class Element
        {
            public string name;
            public RectTransform target;

            [Tooltip("Width in inches. 0 leaves width alone.")]
            public float widthInches;

            [Tooltip("Height in inches. 0 means square (uses width).")]
            public float heightInches;

            [Tooltip("Offset from its own anchor, in inches. Sign follows the anchor corner.")]
            public Vector2 marginInches;

            public bool applySize = true;
            public bool applyPosition = true;

            [Tooltip("If the target has a GridLayoutGroup, size its cells/spacing in inches too.")]
            public float gridCellInches;
            public float gridSpacingInches;

            [Header("Classic docked mode")]
            [Tooltip("Margin used while the classic bar is docked. The two modes have " +
                     "genuinely different geometry, so each gets its own authored default.")]
            public Vector2 classicMarginInches;

            [Tooltip("Width in classic docked mode. 0 inherits the fullscreen width.")]
            public float classicWidthInches;

            [Tooltip("Hidden by default in classic docked mode (until the player chooses " +
                     "otherwise - an explicit Hide/Show always wins).")]
            public bool classicHidden;

        }

        [Header("Canvas")]
        public Canvas canvas;

        [Header("User Scale")]
        [Tooltip("Player-facing multiplier over every inch value. Exposed in the tuning panel.")]
        [Range(0.6f, 1.8f)] public float hudScale = 1f;

        [Header("Elements")]
        public Element[] elements = new Element[0];

        int lastWidth;
        int lastHeight;
        float lastScale;
        float lastCanvasScale;
        float lastBottomInset;

        void Start()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight ||
                !Mathf.Approximately(hudScale, lastScale) ||
                (canvas != null && !Mathf.Approximately(canvas.scaleFactor, lastCanvasScale)) ||
                !Mathf.Approximately(MobileClassicHud.BottomInsetInches, lastBottomInset))
                Apply();
        }

        /// <summary>Canvas units per inch on this device, including CanvasScaler scaling.</summary>
        public float UnitsPerInch
        {
            get
            {
                float scaleFactor = (canvas != null && canvas.scaleFactor > 0.0001f) ? canvas.scaleFactor : 1f;
                return MobileInput.Dpi / scaleFactor;
            }
        }

        #region Player Overrides

        // Player customisations from the layout editor. Stored per element name in
        // PlayerPrefs, in INCHES (position) and a scale multiplier (size), so a layout
        // arranged on an iPad still lands sensibly on an iPhone. Never mutates the
        // serialized element values - reset is just deleting the overrides.

        const string prefPrefix = "DFMobile.layout.";

        /// <summary>
        /// Two SEPARATE saved layouts: one for the fullscreen HUD, one for classic docked
        /// mode. The classic bar changes the geometry of the whole bottom band, so an
        /// arrangement tuned for one mode is wrong in the other - and with a single
        /// namespace, edits made in classic mode corrupted the fullscreen layout (device
        /// report: "if i make changes then switch to full screen it doesn't save
        /// correctly"). The classic profile keys carry a suffix; the fullscreen profile
        /// keeps the legacy unsuffixed keys, so existing fullscreen customisations survive.
        /// </summary>
        static string ProfileSuffix
        {
            get { return MobileClassicHud.DockedBarVisible ? ".classic" : ""; }
        }

        static string Key(string name, string field)
        {
            return prefPrefix + name + ProfileSuffix + field;
        }

        public static void SetMarginOverride(string name, Vector2 marginInches)
        {
            PlayerPrefs.SetFloat(Key(name, ".mx"), marginInches.x);
            PlayerPrefs.SetFloat(Key(name, ".my"), marginInches.y);
        }

        public static void SetScaleOverride(string name, float scale)
        {
            PlayerPrefs.SetFloat(Key(name, ".scale"), scale);
        }

        public static float GetScaleOverride(string name)
        {
            return PlayerPrefs.GetFloat(Key(name, ".scale"), 1f);
        }

        public static void SetHiddenOverride(string name, bool hidden)
        {
            PlayerPrefs.SetInt(Key(name, ".hidden"), hidden ? 1 : 0);
        }

        public static bool GetHiddenOverride(string name)
        {
            return PlayerPrefs.GetInt(Key(name, ".hidden"), 0) == 1;
        }

        /// <summary>
        /// The hidden state Apply() actually uses: the player's explicit choice in the
        /// current profile if one exists, else the element's authored default for the
        /// mode. Defaults are per-element data (classicHidden), not a hardcoded list -
        /// and they are only ever DEFAULTS: an explicit Hide/Show always wins.
        /// </summary>
        public bool EffectiveHidden(Element e)
        {
            string key = Key(e.name, ".hidden");
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetInt(key, 0) == 1;

            return MobileClassicHud.DockedBarVisible && e.classicHidden;
        }

        /// <summary>
        /// Elements that may never be hidden. MenuToggle is the only way into the drawer,
        /// so hiding it in fullscreen mode would strand every button inside it. Classic
        /// docked mode is the exception: there the drawer stands permanently open
        /// (MobileButtonDrawer.PanelShown) and the bar duplicates all of its buttons but
        /// the travel map, which takes MENU's slot - so MENU opens nothing and may hide
        /// like any other duplicate. Pure static so the self test can pin it.
        /// </summary>
        public static bool ExemptFromHiding(string name, bool classicDocked)
        {
            return name == "MenuToggle" && !classicDocked;
        }

        /// <summary>Name-based lookup for the layout editor.</summary>
        public bool EffectiveHiddenByName(string name)
        {
            Element e = Find(name);
            return e != null ? EffectiveHidden(e) : GetHiddenOverride(name);
        }

        /// <summary>Clears the CURRENT profile's customisations only - resetting the classic
        /// layout must not touch the fullscreen one, and vice versa.</summary>
        public static void ClearOverrides(string name)
        {
            foreach (string k in new[] { ".mx", ".my", ".scale", ".hidden" })
                PlayerPrefs.DeleteKey(Key(name, k));
        }

        /// <summary>Reset every stored customisation for this layout's elements.</summary>
        public void ClearAllOverrides()
        {
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] != null)
                    ClearOverrides(elements[i].name);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>While the layout editor is open, hidden elements stay visible (ghosted).</summary>
        [System.NonSerialized] public bool suppressHiding;

        /// <summary>
        /// Screen space reserved at the top, in inches. The layout editor sets this to keep
        /// elements clear of its toolbar - the classic-bar inset pushed the top of the
        /// drawer column (the TUNE gear) underneath it, where it could not be seen or
        /// grabbed (device report).
        /// </summary>
        [System.NonSerialized] public float topReserveInches;

        /// <summary>
        /// Convert an element's current anchoredPosition back to margin inches - the exact
        /// inverse of Apply(), INCLUDING the classic-bar inset. The layout editor must save
        /// through this: inverting the raw position baked the bar height into the stored
        /// margin, which then double-applied on the next mode switch and walked the element
        /// up the screen every fullscreen/classic round trip.
        /// </summary>
        public Vector2 MarginInchesFromCurrentPosition(Element e)
        {
            float upi = UnitsPerInch * Mathf.Max(hudScale, 0.0001f);
            Vector2 a = e.target.anchorMin;
            float sx = (a.x > 0.5f) ? -1f : 1f;
            float sy = (a.y > 0.5f) ? -1f : 1f;

            float inset = (a.y <= 0.5f) ? lastBottomInset * UnitsPerInch : 0f;

            Vector2 pos = e.target.anchoredPosition;
            return new Vector2(pos.x * sx / upi, (pos.y * sy - inset) / upi);
        }

        public Element Find(string name)
        {
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] != null && elements[i].name == name)
                    return elements[i];
            return null;
        }

        #endregion

        public void Apply()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastScale = hudScale;
            lastCanvasScale = (canvas != null) ? canvas.scaleFactor : 1f;

            // When the classic bottom bar is docked, it owns the bottom band of the screen -
            // exactly where the default layout puts everything. Lift every bottom-anchored
            // control clear of it. Deliberately NOT multiplied by hudScale: the bar's height
            // is a fact about the screen, not a control size preference.
            lastBottomInset = MobileClassicHud.BottomInsetInches;

            float upi = UnitsPerInch * hudScale;
            if (upi <= 0.0001f)
                return;

            for (int i = 0; i < elements.Length; i++)
            {
                Element e = elements[i];
                if (e == null || e.target == null)
                    continue;

                float userScale = GetScaleOverride(e.name);

                bool classic = MobileClassicHud.DockedBarVisible;

                float widthIn = (classic && e.classicWidthInches > 0f)
                    ? e.classicWidthInches : e.widthInches;

                if (e.applySize && widthIn > 0f)
                {
                    float w = widthIn * userScale * upi;
                    float h = (e.heightInches > 0f ? e.heightInches : widthIn) * userScale * upi;
                    e.target.sizeDelta = new Vector2(w, h);
                }

                if (!suppressHiding)
                {
                    // MenuToggle is exempt from hiding in fullscreen mode - it is the way
                    // back into the drawer - but not in classic mode, where the drawer is
                    // already open. Everything else follows EffectiveHidden: hidden by
                    // default when the classic bar duplicates it, but the player's own
                    // choice in this profile always wins.
                    bool hidden = EffectiveHidden(e) && !ExemptFromHiding(e.name, classic);
                    e.target.gameObject.SetActive(!hidden);
                }

                if (e.applyPosition)
                {
                    // Sign follows the anchor so a bottom-right control moves inward
                    // with a positive margin, same as a bottom-left one.
                    Vector2 margin = classic ? e.classicMarginInches : e.marginInches;
                    string kx = Key(e.name, ".mx");
                    if (PlayerPrefs.HasKey(kx))
                        margin = new Vector2(PlayerPrefs.GetFloat(kx),
                                             PlayerPrefs.GetFloat(Key(e.name, ".my")));

                    Vector2 a = e.target.anchorMin;
                    float sx = (a.x > 0.5f) ? -1f : 1f;
                    float sy = (a.y > 0.5f) ? -1f : 1f;

                    // Classic-bar inset applies to bottom-anchored controls only; a
                    // top-anchored element is nowhere near the bar.
                    float inset = (a.y <= 0.5f) ? lastBottomInset * UnitsPerInch : 0f;

                    float y = margin.y * upi * sy + inset * sy;

                    // Keep the element on screen. The inset can shove a tall column's top
                    // element past the screen edge (or under the editor toolbar), and on
                    // short phone screens the default column would overflow even without
                    // the bar. Pivot-aware: clamp the element's TOP edge, wherever its
                    // pivot sits.
                    if (a.y <= 0.5f && canvas != null && canvas.scaleFactor > 0.0001f)
                    {
                        float screenTop = Screen.height / canvas.scaleFactor
                                          - topReserveInches * UnitsPerInch;
                        float h = e.target.sizeDelta.y;
                        float maxY = screenTop - h * (1f - e.target.pivot.y);
                        if (y > maxY)
                            y = maxY;
                    }

                    e.target.anchoredPosition = new Vector2(margin.x * upi * sx, y);
                }

                if (e.gridCellInches > 0f)
                {
                    GridLayoutGroup grid = e.target.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        float cell = e.gridCellInches * userScale * upi;
                        float gap = e.gridSpacingInches * userScale * upi;
                        grid.cellSize = new Vector2(cell, cell);
                        grid.spacing = new Vector2(gap, gap);
                    }
                }
            }
        }

        /// <summary>Diagonal screen size in inches. Useful for logging / device classing.</summary>
        public static float ScreenDiagonalInches
        {
            get
            {
                float dpi = MobileInput.Dpi;
                if (dpi <= 1f)
                    return 0f;

                float w = Screen.width / dpi;
                float h = Screen.height / dpi;
                return Mathf.Sqrt(w * w + h * h);
            }
        }
    }
}
