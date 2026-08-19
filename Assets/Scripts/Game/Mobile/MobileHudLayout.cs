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

        void Start()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight ||
                !Mathf.Approximately(hudScale, lastScale) ||
                (canvas != null && !Mathf.Approximately(canvas.scaleFactor, lastCanvasScale)))
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

        public static bool HasOverride(string name)
        {
            return PlayerPrefs.HasKey(prefPrefix + name + ".mx") ||
                   PlayerPrefs.HasKey(prefPrefix + name + ".scale") ||
                   PlayerPrefs.HasKey(prefPrefix + name + ".hidden");
        }

        public static void SetMarginOverride(string name, Vector2 marginInches)
        {
            PlayerPrefs.SetFloat(prefPrefix + name + ".mx", marginInches.x);
            PlayerPrefs.SetFloat(prefPrefix + name + ".my", marginInches.y);
        }

        public static void SetScaleOverride(string name, float scale)
        {
            PlayerPrefs.SetFloat(prefPrefix + name + ".scale", scale);
        }

        public static float GetScaleOverride(string name)
        {
            return PlayerPrefs.GetFloat(prefPrefix + name + ".scale", 1f);
        }

        public static void SetHiddenOverride(string name, bool hidden)
        {
            PlayerPrefs.SetInt(prefPrefix + name + ".hidden", hidden ? 1 : 0);
        }

        public static bool GetHiddenOverride(string name)
        {
            return PlayerPrefs.GetInt(prefPrefix + name + ".hidden", 0) == 1;
        }

        public static void ClearOverrides(string name)
        {
            foreach (string k in new[] { ".mx", ".my", ".scale", ".hidden" })
                PlayerPrefs.DeleteKey(prefPrefix + name + k);
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

            float upi = UnitsPerInch * hudScale;
            if (upi <= 0.0001f)
                return;

            for (int i = 0; i < elements.Length; i++)
            {
                Element e = elements[i];
                if (e == null || e.target == null)
                    continue;

                float userScale = GetScaleOverride(e.name);

                if (e.applySize && e.widthInches > 0f)
                {
                    float w = e.widthInches * userScale * upi;
                    float h = (e.heightInches > 0f ? e.heightInches : e.widthInches) * userScale * upi;
                    e.target.sizeDelta = new Vector2(w, h);
                }

                if (!suppressHiding)
                    e.target.gameObject.SetActive(!GetHiddenOverride(e.name) ||
                        e.name == "SecondaryBank" || e.name == "MenuToggle");

                if (e.applyPosition)
                {
                    // Sign follows the anchor so a bottom-right control moves inward
                    // with a positive margin, same as a bottom-left one.
                    Vector2 margin = e.marginInches;
                    string kx = prefPrefix + e.name + ".mx";
                    if (PlayerPrefs.HasKey(kx))
                        margin = new Vector2(PlayerPrefs.GetFloat(kx),
                                             PlayerPrefs.GetFloat(prefPrefix + e.name + ".my"));

                    Vector2 a = e.target.anchorMin;
                    float sx = (a.x > 0.5f) ? -1f : 1f;
                    float sy = (a.y > 0.5f) ? -1f : 1f;
                    e.target.anchoredPosition = new Vector2(
                        margin.x * upi * sx,
                        margin.y * upi * sy);
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
