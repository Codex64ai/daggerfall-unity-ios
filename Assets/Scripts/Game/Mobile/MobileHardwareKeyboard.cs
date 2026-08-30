// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Real key state for a hardware keyboard on iPadOS, from GCKeyboard via DFMobilePointer.mm.
//
//   WHY. Unity's iOS player has no key-state input for hardware keyboards. It registers a
//   UIKeyCommand per character (UnityView+Keyboard.mm) - a mechanism meant for menu
//   shortcuts, which reports a press with the system's auto-repeat timing and never a
//   release. Unity then fakes "held" with timers. On device that is a long pause before a
//   walk starts and a stutter while the key is down; touch and pad, which do not go through
//   it, have no such lag (device report). GCKeyboard (iOS 14) reports true down and up per
//   key, and InputManager.GetPollKey reads it through here when a keyboard is attached.
//
//   Codes are USB HID keyboard usages (GCKeyCode); the table maps the ones Daggerfall can
//   bind. TryGetKey says whether it KNOWS a key, so an unmapped key falls back to Unity's
//   own reading rather than going dead.
//
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileHardwareKeyboard
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool DFKeyboardConnected();
        [DllImport("__Internal")] static extern int DFKeyboardHeldCount();
        [DllImport("__Internal")] static extern int DFKeyboardSnapshot(int[] codes, int max);
#endif

        const int maxHeld = 32;
        static readonly int[] snapshot = new int[maxHeld];
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static int snapshotFrame = -1;
        static bool failed;

        /// <summary>True while a hardware keyboard is attached. False in the editor and below iOS 14.</summary>
        public static bool Connected
        {
            get
            {
                if (failed || !MobilePointer.Supported)
                    return false;
#if UNITY_IOS && !UNITY_EDITOR
                try { return DFKeyboardConnected(); }
                catch (System.Exception) { failed = true; return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Any key down right now - "the keyboard is in use", which Input.inputString misses for arrows and modifiers.</summary>
        public static bool AnyHeld
        {
            get
            {
                if (failed || !MobilePointer.Supported)
                    return false;
#if UNITY_IOS && !UNITY_EDITOR
                try { return DFKeyboardHeldCount() > 0; }
                catch (System.Exception) { failed = true; return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// The real state of a key, if this layer knows the key and a keyboard is attached.
        /// Returns false (with held = false) when the caller should fall back to Unity's own
        /// reading: no keyboard, plugin missing, or a KeyCode the HID table does not cover.
        /// </summary>
        public static bool TryGetKey(KeyCode key, out bool isHeld)
        {
            isHeld = false;
            if (!Connected || !Mappable(key))
                return false;

            Refresh();
            isHeld = held.Contains(key);
            return true;
        }

        static void Refresh()
        {
            if (snapshotFrame == Time.frameCount)
                return;
            snapshotFrame = Time.frameCount;
            held.Clear();

#if UNITY_IOS && !UNITY_EDITOR
            int n;
            try { n = DFKeyboardSnapshot(snapshot, maxHeld); }
            catch (System.Exception) { failed = true; return; }
            for (int i = 0; i < n; i++)
            {
                KeyCode k = FromHid(snapshot[i]);
                if (k != KeyCode.None)
                    held.Add(k);
            }
#endif
        }

        static bool Mappable(KeyCode key)
        {
            return ToHid(key) >= 0;
        }

        // ---------------------------------------------------------------- HID <-> KeyCode

        /// <summary>USB HID keyboard usage -> Unity KeyCode. None for anything not in the table.</summary>
        public static KeyCode FromHid(int code)
        {
            if (code >= 4 && code <= 29) return (KeyCode)((int)KeyCode.A + (code - 4));
            if (code >= 30 && code <= 38) return (KeyCode)((int)KeyCode.Alpha1 + (code - 30));
            if (code >= 58 && code <= 69) return (KeyCode)((int)KeyCode.F1 + (code - 58));
            if (code >= 89 && code <= 97) return (KeyCode)((int)KeyCode.Keypad1 + (code - 89));
            switch (code)
            {
                case 39: return KeyCode.Alpha0;
                case 40: return KeyCode.Return;
                case 41: return KeyCode.Escape;
                case 42: return KeyCode.Backspace;
                case 43: return KeyCode.Tab;
                case 44: return KeyCode.Space;
                case 45: return KeyCode.Minus;
                case 46: return KeyCode.Equals;
                case 47: return KeyCode.LeftBracket;
                case 48: return KeyCode.RightBracket;
                case 49: return KeyCode.Backslash;
                case 51: return KeyCode.Semicolon;
                case 52: return KeyCode.Quote;
                case 53: return KeyCode.BackQuote;
                case 54: return KeyCode.Comma;
                case 55: return KeyCode.Period;
                case 56: return KeyCode.Slash;
                case 57: return KeyCode.CapsLock;
                case 73: return KeyCode.Insert;
                case 74: return KeyCode.Home;
                case 75: return KeyCode.PageUp;
                case 76: return KeyCode.Delete;
                case 77: return KeyCode.End;
                case 78: return KeyCode.PageDown;
                case 79: return KeyCode.RightArrow;
                case 80: return KeyCode.LeftArrow;
                case 81: return KeyCode.DownArrow;
                case 82: return KeyCode.UpArrow;
                case 83: return KeyCode.Numlock;
                case 84: return KeyCode.KeypadDivide;
                case 85: return KeyCode.KeypadMultiply;
                case 86: return KeyCode.KeypadMinus;
                case 87: return KeyCode.KeypadPlus;
                case 88: return KeyCode.KeypadEnter;
                case 98: return KeyCode.Keypad0;
                case 99: return KeyCode.KeypadPeriod;
                case 224: return KeyCode.LeftControl;
                case 225: return KeyCode.LeftShift;
                case 226: return KeyCode.LeftAlt;
                case 227: return KeyCode.LeftCommand;
                case 228: return KeyCode.RightControl;
                case 229: return KeyCode.RightShift;
                case 230: return KeyCode.RightAlt;
                case 231: return KeyCode.RightCommand;
            }
            return KeyCode.None;
        }

        /// <summary>Unity KeyCode -> USB HID usage, or -1 when the table has no entry.</summary>
        public static int ToHid(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return 4 + (key - KeyCode.A);
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9) return 30 + (key - KeyCode.Alpha1);
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return 58 + (key - KeyCode.F1);
            if (key >= KeyCode.Keypad1 && key <= KeyCode.Keypad9) return 89 + (key - KeyCode.Keypad1);
            switch (key)
            {
                case KeyCode.Alpha0: return 39;
                case KeyCode.Return: return 40;
                case KeyCode.Escape: return 41;
                case KeyCode.Backspace: return 42;
                case KeyCode.Tab: return 43;
                case KeyCode.Space: return 44;
                case KeyCode.Minus: return 45;
                case KeyCode.Equals: return 46;
                case KeyCode.LeftBracket: return 47;
                case KeyCode.RightBracket: return 48;
                case KeyCode.Backslash: return 49;
                case KeyCode.Semicolon: return 51;
                case KeyCode.Quote: return 52;
                case KeyCode.BackQuote: return 53;
                case KeyCode.Comma: return 54;
                case KeyCode.Period: return 55;
                case KeyCode.Slash: return 56;
                case KeyCode.CapsLock: return 57;
                case KeyCode.Insert: return 73;
                case KeyCode.Home: return 74;
                case KeyCode.PageUp: return 75;
                case KeyCode.Delete: return 76;
                case KeyCode.End: return 77;
                case KeyCode.PageDown: return 78;
                case KeyCode.RightArrow: return 79;
                case KeyCode.LeftArrow: return 80;
                case KeyCode.DownArrow: return 81;
                case KeyCode.UpArrow: return 82;
                case KeyCode.Numlock: return 83;
                case KeyCode.KeypadDivide: return 84;
                case KeyCode.KeypadMultiply: return 85;
                case KeyCode.KeypadMinus: return 86;
                case KeyCode.KeypadPlus: return 87;
                case KeyCode.KeypadEnter: return 88;
                case KeyCode.Keypad0: return 98;
                case KeyCode.KeypadPeriod: return 99;
                case KeyCode.LeftControl: return 224;
                case KeyCode.LeftShift: return 225;
                case KeyCode.LeftAlt: return 226;
                case KeyCode.LeftCommand: return 227;
                case KeyCode.RightControl: return 228;
                case KeyCode.RightShift: return 229;
                case KeyCode.RightAlt: return 230;
                case KeyCode.RightCommand: return 231;
            }
            return -1;
        }
    }
}
