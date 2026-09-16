// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Denies the existence of a mouse to UGUI on touch devices.
//
//   iPadOS (with a Magic Keyboard attached) presents a system pointer as a real mouse
//   with a PERMANENTLY HELD button, and Unity's StandaloneInputModule processes mouse
//   events whenever no touch is active - at Input.mousePosition, which on iOS parks at
//   the LAST TOUCH POSITION. Net effect: after every tap, a phantom pointer re-pressed
//   whatever was just tapped (device log: 22 phantom Escape presses, extra Activate
//   queues, and a re-pinned action that made doors unopenable AGAIN after the keybind
//   fix). Overriding mousePresent to false starves the module's mouse path entirely;
//   touch processing is unaffected. The editor keeps its real mouse.
//
//   CRT, 2026-09-16: this class is already the StandaloneInputModule's inputOverride (wired on
//   the EventSystem in DaggerfallUnityGame.unity), so it is also the one place every UGUI pointer
//   position passes through - which is what CRTCoverage 2 needs. At coverage 2 the touch controls
//   are drawn INSIDE the CRT frame pass's filtered picture, pulled toward the middle of the tube
//   by the barrel warp, and an action button hit-tested at the raw finger position is a button the
//   player misses. MobileCrtFrame.ToControlPoint moves the position to the pixel of the source
//   picture under the finger, and is the identity at coverage 1 (where the pass redraws the
//   controls sharp through its own camera) and with the filter off - so nothing below changes a
//   single event in the default configuration.
//

using UnityEngine;
using UnityEngine.EventSystems;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileUGUIInput : BaseInput
    {
        public override bool mousePresent
        {
            get
            {
                if (Input.touchSupported && !Application.isEditor)
                    return false;
                return base.mousePresent;
            }
        }

        public override bool GetMouseButtonDown(int button)
        {
            if (Input.touchSupported && !Application.isEditor)
                return false;
            return base.GetMouseButtonDown(button);
        }

        public override bool GetMouseButton(int button)
        {
            if (Input.touchSupported && !Application.isEditor)
                return false;
            return base.GetMouseButton(button);
        }

        public override bool GetMouseButtonUp(int button)
        {
            if (Input.touchSupported && !Application.isEditor)
                return false;
            return base.GetMouseButtonUp(button);
        }

        /// <summary>Where the module thinks the pointer is, moved through the tube's warp at
        /// coverage 2. Starved on device by mousePresent above, but the editor's real mouse
        /// drives UGUI through here and gets the same picture the player would.</summary>
        public override Vector2 mousePosition
        {
            get { return MobileCrtFrame.ToControlPoint(base.mousePosition); }
        }

        /// <summary>
        /// The touch the module builds its PointerEventData from, with its position moved to the
        /// pixel of the source picture the finger is over. Only the position is touched: the
        /// finger id, the phase and the tap count are what they were, so every pressed/released
        /// bookkeeping the module does still matches finger for finger.
        /// </summary>
        public override Touch GetTouch(int index)
        {
            Touch t = base.GetTouch(index);
            Vector2 warped = MobileCrtFrame.ToControlPoint(t.position);
            if (warped != t.position)
                t.position = warped;
            return t;
        }
    }
}
