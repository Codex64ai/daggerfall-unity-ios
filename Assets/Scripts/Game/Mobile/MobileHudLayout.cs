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

                if (e.applySize && e.widthInches > 0f)
                {
                    float w = e.widthInches * upi;
                    float h = (e.heightInches > 0f ? e.heightInches : e.widthInches) * upi;
                    e.target.sizeDelta = new Vector2(w, h);
                }

                if (e.applyPosition)
                {
                    // Sign follows the anchor so a bottom-right control moves inward
                    // with a positive margin, same as a bottom-left one.
                    Vector2 a = e.target.anchorMin;
                    float sx = (a.x > 0.5f) ? -1f : 1f;
                    float sy = (a.y > 0.5f) ? -1f : 1f;
                    e.target.anchoredPosition = new Vector2(
                        e.marginInches.x * upi * sx,
                        e.marginInches.y * upi * sy);
                }

                if (e.gridCellInches > 0f)
                {
                    GridLayoutGroup grid = e.target.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        float cell = e.gridCellInches * upi;
                        float gap = e.gridSpacingInches * upi;
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
