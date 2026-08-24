// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Makes DFU's Large HUD - the classic Daggerfall bottom interface bar - a first-class
//   touch citizen. Three jobs:
//
//   1. TAPS. A finger tap on any bar icon (inventory, map, rest, spellbook, options,
//      interaction mode, portrait, ...) triggers that icon, controller connected or not.
//      Routed by hit-testing raw Input.touches against the bar and calling
//      HUDLarge.TriggerTap - the classic UI's own click pump runs off the real mouse,
//      which touch gameplay deliberately does not feed.
//
//   2. NO DOUBLE-CLAIMING. The bar owns the bottom of the screen in docked mode - the
//      exact band where the sticks' claim regions and the look zone live. Touches that
//      begin on the bar must never grab a stick or turn the camera, so both consult
//      ContainsScreenPoint before claiming.
//
//   3. DEDUPLICATION. The bar already shows Inventory, Map, Rest and Options as proper
//      game art, and Sheathe covers the WEAPON toggle - so while it is visible the touch
//      overlay hides its copies (drawer PAUSE/INVENTORY/MAP/REST, bank WEAPON) and shifts
//      every bottom-anchored control up by the bar's height. STATUS stays: the bar has no
//      status icon (classic used the portrait for the character sheet).
//
//   Static class polled from MobileInputController: the bar can be toggled in DFU's own
//   settings mid-session, so everything here reacts to state rather than configuring once.
//

using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileClassicHud
    {
        const float tapMaxSeconds = 0.40f;
        const float tapMaxMoveInches = 0.25f;

        struct PendingTap
        {
            public Vector2 startPos;
            public float startTime;
        }

        static readonly Dictionary<int, PendingTap> pending = new Dictionary<int, PendingTap>();

        /// <summary>The classic bar, if the HUD exists. Cheap property; no caching needed.</summary>
        static HUDLarge Bar
        {
            get
            {
                if (!DaggerfallUI.HasInstance || DaggerfallUI.Instance.DaggerfallHUD == null)
                    return null;
                return DaggerfallUI.Instance.DaggerfallHUD.LargeHUD;
            }
        }

        /// <summary>True while the classic bar is switched on and drawn.</summary>
        public static bool BarVisible
        {
            get
            {
                HUDLarge bar = Bar;
                return bar != null && bar.Enabled && DaggerfallUnity.Settings.LargeHUD;
            }
        }

        /// <summary>True when the classic bar is on AND docked - the mode that owns the
        /// bottom band of the screen. This is also the layout-profile switch.</summary>
        public static bool DockedBarVisible
        {
            get { return BarVisible && DaggerfallUnity.Settings.LargeHUDDocked; }
        }

        /// <summary>
        /// Bar height as a fraction usable for layout, in inches. Zero when the bar is off
        /// or undocked - undocked floats over the view at user scale and position, so
        /// shoving the whole overlay up for it would be wrong.
        /// </summary>
        public static float BottomInsetInches
        {
            get
            {
                if (!DockedBarVisible)
                    return 0f;

                return Bar.Rectangle.height / MobileInput.Dpi;
            }
        }

        /// <summary>
        /// Does this touch position (Unity screen coords, BOTTOM-left origin) land on the
        /// bar? Sticks and the look zone use this to leave bar touches alone.
        /// </summary>
        public static bool ContainsScreenPoint(Vector2 screenPos)
        {
            if (!BarVisible)
                return false;

            return Bar.Rectangle.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
        }

        /// <summary>
        /// Called every frame from MobileInputController - INCLUDING while a controller is
        /// connected, which is the whole point: the bar stays tappable when the rest of the
        /// touch overlay has stood down.
        /// </summary>
        public static void Poll()
        {
            if (!BarVisible || MobileInput.MenuMode)
            {
                if (pending.Count > 0)
                    pending.Clear();
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    if (ContainsScreenPoint(t.position))
                        pending[t.fingerId] = new PendingTap
                        {
                            startPos = t.position,
                            startTime = Time.unscaledTime,
                        };
                }
                else if (t.phase == TouchPhase.Ended)
                {
                    PendingTap tap;
                    if (!pending.TryGetValue(t.fingerId, out tap))
                        continue;
                    pending.Remove(t.fingerId);

                    bool quick = Time.unscaledTime - tap.startTime <= tapMaxSeconds;
                    bool still = (t.position - tap.startPos).magnitude
                                     <= tapMaxMoveInches * MobileInput.Dpi;
                    if (!quick || !still)
                        continue;

                    HUDLarge bar = Bar;
                    if (bar != null)
                        bar.TriggerTap(new Vector2(t.position.x, Screen.height - t.position.y));
                }
                else if (t.phase == TouchPhase.Canceled)
                {
                    pending.Remove(t.fingerId);
                }
            }
        }

    }
}
