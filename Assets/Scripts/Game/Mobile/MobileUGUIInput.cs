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
    }
}
