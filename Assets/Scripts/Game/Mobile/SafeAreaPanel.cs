// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Insets a full-screen UI panel to Screen.safeArea so controls avoid the notch,
    /// Dynamic Island and home indicator. Attach to each HUD layer root.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafeArea;
        int lastWidth;
        int lastHeight;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastWidth || Screen.height != lastHeight)
                Apply();
        }

        void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            if (lastWidth <= 0 || lastHeight <= 0)
                return;

            Rect area = lastSafeArea;

            rect.anchorMin = new Vector2(area.xMin / lastWidth, area.yMin / lastHeight);
            rect.anchorMax = new Vector2(area.xMax / lastWidth, area.yMax / lastHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
