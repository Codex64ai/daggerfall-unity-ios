// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// C# wrapper over Assets/Plugins/iOS/DFMobilePointer.mm, plus the pure decisions the
// pointer pump relies on, kept here so the self-test can exercise them headlessly.
//
// Why this exists: Unity's iOS player has no real mouse support. A pointer reaches the
// game only as click-touches, hover is invisible and Cursor.lockState does nothing. The
// native side supplies GCMouse deltas/buttons/scroll, the hover position, and a working
// pointer lock; this side scales them into the engine's own mouse-axis units and decides
// when the pointer should be locked.
//
// Everything degrades to "no pointer" off-device, in the editor, and below iOS 14, so
// callers never need to guard.
//

using System.Runtime.InteropServices;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobilePointer
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool DFPointerSupported();
        [DllImport("__Internal")] static extern void DFPointerInit();
        [DllImport("__Internal")] static extern bool DFPointerConnected();
        [DllImport("__Internal")] static extern void DFPointerConsumeDelta(out float dx, out float dy);
        [DllImport("__Internal")] static extern int DFPointerButtons();
        [DllImport("__Internal")] static extern float DFPointerConsumeScroll();
        [DllImport("__Internal")] static extern bool DFPointerHover(out float nx, out float ny);
        [DllImport("__Internal")] static extern void DFPointerSetLocked(bool locked);
        [DllImport("__Internal")] static extern bool DFPointerIsLocked();
#endif

        public const int LeftMask = 1;
        public const int RightMask = 2;
        public const int MiddleMask = 4;
        /// <summary>Any auxiliary button (side buttons, a wheel-click the mouse reports as aux).
        /// Never bound to an action; it exists so a click on one is not mistaken for a finger.</summary>
        public const int AuxMask = 8;

        /// <summary>
        /// Unity's "Mouse X/Y" axes are raw counts x 0.1 (ProjectSettings/InputManager.asset
        /// sensitivity). GCMouse gives the raw counts, so this is what makes DFU's own mouse
        /// sensitivity setting mean the same thing it does on PC.
        /// </summary>
        public const float UnityMouseAxisScale = 0.1f;

        static int supportedCache = -1;   // -1 unknown, 0 no, 1 yes
        static bool initialised;
        static bool lockRequested;

        #region Native

        /// <summary>False in the editor, on desktop, and below iOS 14.</summary>
        public static bool Supported
        {
            get
            {
                if (supportedCache >= 0)
                    return supportedCache == 1;

#if UNITY_IOS && !UNITY_EDITOR
                bool ok;
                try { ok = DFPointerSupported(); }
                catch (System.Exception) { ok = false; }   // plugin missing from the build
#else
                bool ok = false;
#endif
                supportedCache = ok ? 1 : 0;
                return ok;
            }
        }

        /// <summary>Register for mice, install the hover recogniser and the lock override. Idempotent.</summary>
        public static void Init()
        {
            if (initialised || !Supported)
                return;
            initialised = true;
#if UNITY_IOS && !UNITY_EDITOR
            try { DFPointerInit(); } catch (System.Exception) { supportedCache = 0; }
#endif
        }

        /// <summary>True while a mouse or trackpad is attached.</summary>
        public static bool Connected
        {
            get
            {
                if (!Supported)
                    return false;
#if UNITY_IOS && !UNITY_EDITOR
                try { return DFPointerConnected(); } catch (System.Exception) { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Raw movement since the last call (positive Y = up), then zeroed.</summary>
        public static Vector2 ConsumeDelta()
        {
            if (!Supported)
                return Vector2.zero;
#if UNITY_IOS && !UNITY_EDITOR
            float dx, dy;
            try { DFPointerConsumeDelta(out dx, out dy); } catch (System.Exception) { return Vector2.zero; }
            return new Vector2(dx, dy);
#else
            return Vector2.zero;
#endif
        }

        /// <summary>Bitmask of held buttons: LeftMask | RightMask | MiddleMask.</summary>
        public static int Buttons
        {
            get
            {
                if (!Supported)
                    return 0;
#if UNITY_IOS && !UNITY_EDITOR
                try { return DFPointerButtons(); } catch (System.Exception) { return 0; }
#else
                return 0;
#endif
            }
        }

        public static bool AnyButton { get { return Buttons != 0; } }
        public static bool Left { get { return (Buttons & LeftMask) != 0; } }
        public static bool Right { get { return (Buttons & RightMask) != 0; } }
        public static bool Middle { get { return (Buttons & MiddleMask) != 0; } }

        /// <summary>Scroll accumulated since the last call, then zeroed. Undefined units.</summary>
        public static float ConsumeScroll()
        {
            if (!Supported)
                return 0f;
#if UNITY_IOS && !UNITY_EDITOR
            try { return DFPointerConsumeScroll(); } catch (System.Exception) { return 0f; }
#else
            return 0f;
#endif
        }

        /// <summary>Pointer position in Unity screen pixels while unlocked. False until seen once.</summary>
        public static bool TryGetHover(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (!Supported)
                return false;
#if UNITY_IOS && !UNITY_EDITOR
            float nx, ny;
            bool valid;
            try { valid = DFPointerHover(out nx, out ny); } catch (System.Exception) { return false; }
            if (!valid)
                return false;
            screenPos = HoverToScreen(nx, ny, Screen.width, Screen.height);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Request or release pointer lock. Only crosses into native on a change.</summary>
        public static void SetLocked(bool locked)
        {
            if (!Supported)
                return;
            if (locked == lockRequested && initialised)
                return;
            lockRequested = locked;
#if UNITY_IOS && !UNITY_EDITOR
            try { DFPointerSetLocked(locked); } catch (System.Exception) { supportedCache = 0; }
#endif
        }

        public static bool LockRequested { get { return lockRequested; } }

        /// <summary>The system's answer, which may differ from the request (e.g. not full screen).</summary>
        public static bool IsLocked
        {
            get
            {
                if (!Supported)
                    return false;
#if UNITY_IOS && !UNITY_EDITOR
                try { return DFPointerIsLocked(); } catch (System.Exception) { return false; }
#else
                return false;
#endif
            }
        }

        #endregion

        #region Pure Decisions

        /// <summary>Raw counts to Unity mouse-axis units, with an optional Y flip.</summary>
        public static Vector2 ScaleDelta(Vector2 raw, float scale, bool flipY)
        {
            Vector2 d = raw * scale;
            if (flipY)
                d.y = -d.y;
            return d;
        }

        /// <summary>
        /// Lock exactly when PlayerMouseLook would have on PC: a pointer is in use, no classic
        /// window is open, the game is not paused, and the engine has hidden its cursor.
        /// </summary>
        public static bool ShouldLock(bool mouseActive, bool menuOpen, bool gamePaused, bool engineCursorVisible)
        {
            return mouseActive && !menuOpen && !gamePaused && !engineCursorVisible;
        }

        /// <summary>
        /// The cursor-stage pump may swallow movement only while the game is paused with no
        /// classic window open - the single state in which the gameplay pump never runs. In
        /// menus hover owns the position (the menu pump drains itself), and in live play the
        /// deltas belong to the camera. Draining there is the bug that made the first mouse
        /// build lock the pointer and then never move.
        /// </summary>
        public static bool ShouldDrainInCursorStage(bool menuOpen, bool gamePaused)
        {
            return !menuOpen && gamePaused;
        }

        /// <summary>Normalised hover (0..1, bottom-left origin) to screen pixels, clamped.</summary>
        public static Vector2 HoverToScreen(float nx, float ny, int width, int height)
        {
            return new Vector2(
                Mathf.Clamp01(nx) * width,
                Mathf.Clamp01(ny) * height);
        }

        /// <summary>
        /// At most one classic-UI scroll step per call once the accumulator crosses the
        /// threshold, and the accumulator is emptied so a hard flick cannot carry over.
        /// </summary>
        public static int ScrollTicks(ref float accumulator, float threshold)
        {
            if (Mathf.Abs(accumulator) < threshold)
                return 0;
            int tick = accumulator > 0f ? 1 : -1;
            accumulator = 0f;
            return tick;
        }

        /// <summary>
        /// A touch hands control back to the touch layer only if it is a finger (or pencil):
        /// not an indirect device, and not arriving while a pointer button is held - iPadOS
        /// delivers pointer clicks as touches.
        /// </summary>
        public static bool IsFingerTouch(TouchType type, bool anyPointerButtonHeld)
        {
            return type != TouchType.Indirect && !anyPointerButtonHeld;
        }

        /// <summary>
        /// Daggerfall's stock mouse layout, used when KeyBinds.txt had no mouse bindings to
        /// capture: left = activate, right = swing.
        /// </summary>
        public static bool TryDefaultAction(int button, out InputManager.Actions action)
        {
            switch (button)
            {
                case 0: action = InputManager.Actions.ActivateCenterObject; return true;
                case 1: action = InputManager.Actions.SwingWeapon; return true;
                default: action = InputManager.Actions.Unknown; return false;
            }
        }

        #endregion
    }
}
