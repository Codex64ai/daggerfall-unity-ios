// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Collapses the secondary action buttons behind one MENU button.
//
//   Ten always-visible buttons is too many: they crowd the look/swipe area, and most of
//   them (map, status, rest) are used occasionally rather than moment to moment. Only the
//   buttons needed during play stay on screen - activate, weapon, jump, crouch, cast -
//   and the rest live one tap away.
//
//   The drawer closes itself after a selection, because every button inside it opens a
//   classic window anyway, which hides the whole gameplay HUD.
//

using UnityEngine;
using UnityEngine.UI;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileButtonDrawer : MonoBehaviour
    {
        [Header("Wiring (auto-filled by MobileHudBuilder)")]
        [Tooltip("Panel holding the secondary buttons. Hidden unless the drawer is open.")]
        public GameObject panel;

        [Tooltip("The MENU button graphic, tinted while open.")]
        public Image toggleGraphic;

        [Header("Behaviour")]
        [Tooltip("Close the drawer after any button inside it is pressed.")]
        public bool closeOnSelection = true;

        [Tooltip("Auto-close after this many seconds with no interaction. 0 disables.")]
        public float autoCloseSeconds = 6f;

        [Header("Appearance")]
        public Color openColor = new Color(0.85f, 0.70f, 0.35f, 0.95f);
        public Color closedColor = Color.white;

        bool open;
        float lastInteraction;

        // The layout editor forces the drawer open while arranging: its icons are layout
        // elements the player must be able to SEE to drag, and the auto-close timer would
        // otherwise shut them mid-edit (device report: "they're invisible when I'm editing
        // ... but I could move them still").
        [System.NonSerialized] public bool forceOpen;

        public bool IsOpen { get { return open || forceOpen; } }

        void Start()
        {
            Apply();
        }

        void OnDisable()
        {
            // The gameplay HUD hides when a menu opens or a gamepad connects; the drawer
            // must not be left open behind it.
            open = false;
            Apply();
        }

        void Update()
        {
            if (forceOpen && panel != null && !panel.activeSelf)
                Apply();

            if (!open || forceOpen || autoCloseSeconds <= 0f)
                return;

            if (Time.unscaledTime - lastInteraction >= autoCloseSeconds)
            {
                open = false;
                Apply();
            }
        }

        /// <summary>Wire to the MENU button's onClick.</summary>
        public void Toggle()
        {
            open = !open;
            lastInteraction = Time.unscaledTime;
            Apply();
        }

        public void Close()
        {
            if (!open)
                return;

            open = false;
            Apply();
        }

        /// <summary>Called by each button inside the drawer.</summary>
        public void NotifySelection()
        {
            lastInteraction = Time.unscaledTime;

            if (closeOnSelection)
                Close();
        }

        void Apply()
        {
            bool shown = open || forceOpen;

            if (panel != null && panel.activeSelf != shown)
                panel.SetActive(shown);

            if (toggleGraphic != null)
                toggleGraphic.color = shown ? openColor : closedColor;
        }
    }
}
