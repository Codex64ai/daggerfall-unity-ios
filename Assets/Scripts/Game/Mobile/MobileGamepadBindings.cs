// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Daggerfall Unity already binds gamepad buttons for UI clicks (JoystickButton0 =
//   LeftClick, 3 = RightClick, 2 = Middle, 1 = Back) and gamepad axes for movement and
//   camera. What it does NOT do is bind gameplay actions - Jump, Activate, ReadyWeapon
//   and so on default to keyboard keys only. Out of the box a controller can navigate
//   menus but cannot actually play.
//
//   Two layers are applied here:
//
//     BASE      face buttons, shoulders, RT, stick clicks, d-pad, start/select
//     LT + ...   a second full layer, with left trigger held as a modifier
//
//   The modifier layer is NOT hand-rolled. InputManager has a combo-keycode system
//   (startingComboKeyCode = 65537; two keycodes packed as `a << 16 | b`) that already
//   does everything needed, including the part that is easy to get wrong: while the
//   modifier is held, GetUnaryKey suppresses the BASE action of any key that has a combo
//   variant (InputManager.cs, the checkModHeldFirst branch). So holding LT and pressing Y
//   opens the inventory WITHOUT also casting a spell, with no work from us. Combos are
//   allowed to use axis keycodes as the modifier, so a trigger works as the modifier
//   whether the controller reports it as an axis or as a button.
//
//   Everything is applied as SECONDARY bindings, so every keyboard primary is left
//   intact and the player can still rebind through DaggerfallJoystickControlsWindow.
//   The base-action suppression above also depends on the paired primary existing, since
//   the engine keys it off primarySecondaryKeybindDict.
//
//   LIVE ONLY - see Apply(). Nothing here is written to KeyBinds.txt.
//

using System.Collections.Generic;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileGamepadBindings
    {
        #region Controller map

        //
        // ================= PROBE-DISCOVERED CONTROLLER MAP =======================
        //
        // These are the ONLY values that should need changing for a new controller.
        // Capture them with MobileControllerProbe (build with DFU_IOS_PROBE=1): it names
        // each control, records what Unity reported, and prints a summary on device.
        //
        // Do not copy numbers from the internet. Unity's legacy-input joystick button
        // numbering, and especially its trigger/d-pad AXIS numbering, differ per
        // controller model AND per iOS version.
        //
        // Btn(n)          = KeyCode.JoystickButton0 + n
        // Axis(n, true)   = axis n pushed POSITIVE   (InputManager synthetic keycode)
        // Axis(n, false)  = axis n pushed NEGATIVE
        //
        // A control reported as a button uses Btn(); one reported as an axis uses Axis().
        // Both are plain KeyCodes to the engine, so either works anywhere below,
        // including as the modifier.
        //

        static KeyCode A       { get { return Btn(0); } }
        static KeyCode B       { get { return Btn(1); } }
        static KeyCode X       { get { return Btn(2); } }
        static KeyCode Y       { get { return Btn(3); } }
        static KeyCode LB      { get { return Btn(4); } }
        static KeyCode RB      { get { return Btn(5); } }
        static KeyCode Select  { get { return Btn(6); } }
        static KeyCode Start   { get { return Btn(7); } }
        static KeyCode L3      { get { return Btn(8); } }
        static KeyCode R3      { get { return Btn(9); } }

        // Triggers. PROBE THESE - they are the single most controller-dependent inputs.
        // MFi/Xbox/DualSense controllers on iPadOS variously report triggers as buttons,
        // as an axis resting at 0 travelling to +1, or as an axis resting at -1.
        static KeyCode LT      { get { return Axis(5, true); } }
        static KeyCode RT      { get { return Axis(6, true); } }

        // D-pad. Usually a pair of axes (one horizontal, one vertical) rather than four
        // buttons, but not always, and the vertical sign is not consistent either.
        static KeyCode DUp     { get { return Axis(7, true); } }
        static KeyCode DDown   { get { return Axis(7, false); } }
        static KeyCode DLeft   { get { return Axis(8, false); } }
        static KeyCode DRight  { get { return Axis(8, true); } }

        // ================= END CONTROLLER MAP ====================================

        static KeyCode Btn(int n)
        {
            return KeyCode.JoystickButton0 + n;
        }

        /// <summary>
        /// InputManager's synthetic axis-as-button keycode. Two per axis: the even code is
        /// the positive direction, the odd code the negative (InputManager.GetAxisKey).
        /// </summary>
        static KeyCode Axis(int axis, bool positive)
        {
            return (KeyCode)(InputManager.startingAxisKeyCode + (axis - 1) * 2 + (positive ? 0 : 1));
        }

        #endregion

        #region Binding tables

        struct Bind
        {
            public KeyCode code;
            public InputManager.Actions action;
            public string label;

            public Bind(KeyCode code, InputManager.Actions action, string label)
            {
                this.code = code;
                this.action = action;
                this.label = label;
            }
        }

        /// <summary>Base layer - no modifier held.</summary>
        static List<Bind> BaseLayer()
        {
            return new List<Bind>
            {
                new Bind(A,      InputManager.Actions.ActivateCenterObject, "A       -> Activate"),
                new Bind(X,      InputManager.Actions.ReadyWeapon,          "X       -> Ready weapon"),
                new Bind(RT,     InputManager.Actions.SwingWeapon,          "RT      -> Swing weapon"),
                new Bind(B,      InputManager.Actions.CastSpell,            "B       -> Cast spell"),
                new Bind(Y,      InputManager.Actions.Jump,                 "Y       -> Jump"),
                new Bind(RB,     InputManager.Actions.SwitchHand,           "RB      -> Switch hand"),
                new Bind(LB,     InputManager.Actions.AutoRun,              "LB      -> Autorun"),
                new Bind(L3,     InputManager.Actions.Crouch,               "L3      -> Crouch"),
                new Bind(R3,     InputManager.Actions.Transport,            "R3      -> Transport"),
                new Bind(DUp,    InputManager.Actions.CharacterSheet,       "D-Up    -> Character sheet"),
                new Bind(DDown,  InputManager.Actions.Status,               "D-Down  -> Status"),
                new Bind(DLeft,  InputManager.Actions.AutoMap,              "D-Left  -> Automap"),
                new Bind(DRight, InputManager.Actions.TravelMap,            "D-Right -> Travel map"),
                new Bind(Start,  InputManager.Actions.Escape,               "Start   -> Pause"),
                new Bind(Select, InputManager.Actions.Rest,                 "Select  -> Rest"),
            };
        }

        /// <summary>
        /// LT-held layer. Every entry is a combo keycode, so the engine both fires these
        /// and suppresses the base action of the same button while LT is down.
        /// </summary>
        static List<Bind> ModifierLayer()
        {
            return new List<Bind>
            {
                new Bind(Combo(LT, Y),      InputManager.Actions.Inventory,    "LT + Y       -> Inventory"),
                new Bind(Combo(LT, A),      InputManager.Actions.RecastSpell,  "LT + A       -> Recast spell"),
                new Bind(Combo(LT, B),      InputManager.Actions.UseMagicItem, "LT + B       -> Use magic item"),
                new Bind(Combo(LT, X),      InputManager.Actions.NoteBook,     "LT + X       -> Notebook"),
                new Bind(Combo(LT, RB),     InputManager.Actions.LogBook,      "LT + RB      -> Logbook"),
                new Bind(Combo(LT, LB),     InputManager.Actions.Run,          "LT + LB      -> Run"),
                new Bind(Combo(LT, L3),     InputManager.Actions.Sneak,        "LT + L3      -> Sneak"),
                new Bind(Combo(LT, DUp),    InputManager.Actions.StealMode,    "LT + D-Up    -> Steal mode"),
                new Bind(Combo(LT, DDown),  InputManager.Actions.GrabMode,     "LT + D-Down  -> Grab mode"),
                new Bind(Combo(LT, DLeft),  InputManager.Actions.InfoMode,     "LT + D-Left  -> Info mode"),
                new Bind(Combo(LT, DRight), InputManager.Actions.TalkMode,     "LT + D-Right -> Talk mode"),
                new Bind(Combo(LT, Start),  InputManager.Actions.QuickSave,    "LT + Start   -> Quicksave"),
                new Bind(Combo(LT, Select), InputManager.Actions.QuickLoad,    "LT + Select  -> Quickload"),
            };
        }

        static KeyCode Combo(KeyCode modifier, KeyCode key)
        {
            if (!InputManager.HasInstance)
                return KeyCode.None;

            return InputManager.Instance.GetComboCode(modifier, key);
        }

        #endregion

        #region Apply / Clear

        // Every code this class put into the binding dictionaries, so Clear() can take out
        // exactly what it put in - including axis and combo keycodes, which are outside the
        // Mouse0-6 / JoystickButton0-19 range that MobileInputController's phantom guard
        // sweeps.
        static readonly List<KeyCode> appliedCodes = new List<KeyCode>();

        public static bool IsApplied { get { return appliedCodes.Count > 0; } }

        /// <summary>The left trigger keycode, exposed so the HUD can show a modifier hint.</summary>
        public static KeyCode ModifierKey { get { return LT; } }

        /// <summary>
        /// Apply both layers as secondary bindings, for this session only.
        ///
        /// Deliberately does NOT call SaveKeyBinds(). Persisting joystick bindings to
        /// KeyBinds.txt is what created the original phantom-input bug: each scene's
        /// InputManager.Start() reloads the file, and iPadOS pulses joystick-button state
        /// during touches, so a persisted JoystickButton0 -> ActivateCenterObject binding
        /// left doors permanently un-openable (see REVIEW.md). Applying live and clearing on
        /// disconnect keeps the file clean; the cost is that the bindings are re-applied on
        /// every connect, which is free.
        /// </summary>
        public static void Apply()
        {
            if (!InputManager.HasInstance)
            {
                Debug.LogWarning("[MobileGamepadBindings] InputManager not ready; skipping.");
                return;
            }

            // The probe reports what the hardware ACTUALLY sends. Applying a layout built
            // on not-yet-verified axis numbers on top of it would both fire spurious
            // actions and make the probe's own readings suspect.
            if (MobileControllerProbe.AnyActive)
            {
                Debug.Log("[MobileGamepadBindings] controller probe active - not applying bindings.");
                return;
            }

            InputManager input = InputManager.Instance;

            // Axis-as-button keycodes resolve to false unless the controller path is live
            // (InputManager.GetAxisKey early-returns on !EnableController), which would
            // silently drop RT, the d-pad and the whole LT layer.
            input.EnableController = true;

            Clear();

            var log = new System.Text.StringBuilder();
            log.AppendLine("[MobileGamepadBindings] applied gamepad layout as SECONDARY bindings (live, not saved):");

            List<Bind> all = BaseLayer();
            all.AddRange(ModifierLayer());

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].code == KeyCode.None)
                {
                    Debug.LogWarning("[MobileGamepadBindings] skipped " + all[i].label +
                                     " - could not build keycode");
                    continue;
                }

                input.SetBinding(all[i].code, all[i].action, false);
                appliedCodes.Add(all[i].code);
                log.AppendLine("   " + all[i].label);
            }

            RebuildBindingCache(input);

            log.AppendLine("Hold LT for the second layer. Rebind in Settings > Controls > Joystick.");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Remove every binding this class applied. Called on controller disconnect, where
        /// leftover joystick bindings are phantom fuel on iPadOS.
        /// </summary>
        public static void Clear()
        {
            if (!InputManager.HasInstance || appliedCodes.Count == 0)
            {
                appliedCodes.Clear();
                return;
            }

            InputManager input = InputManager.Instance;
            for (int i = 0; i < appliedCodes.Count; i++)
            {
                input.ClearBinding(appliedCodes[i], false);
                input.ClearBinding(appliedCodes[i], true);
            }

            int count = appliedCodes.Count;
            appliedCodes.Clear();
            RebuildBindingCache(input);
            Debug.Log("[MobileGamepadBindings] cleared " + count + " gamepad bindings");
        }

        /// <summary>
        /// The input loop reads a CACHED merge of the binding dictionaries, and the combo
        /// system's modifier table (modifierHeldFirstDict) is built there too - so without
        /// this rebuild, SetBinding appears to do nothing and the LT layer never arms.
        /// SaveKeyBinds() would call it, but it also writes KeyBinds.txt, which is exactly
        /// what we are avoiding. Reflection is safe under IL2CPP: link.xml preserves
        /// Assembly-CSharp wholesale.
        /// </summary>
        static void RebuildBindingCache(InputManager input)
        {
            var rebuild = typeof(InputManager).GetMethod("UpdateBindingCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (rebuild != null)
                rebuild.Invoke(input, null);
            else
                Debug.LogError("[MobileGamepadBindings] UpdateBindingCache not found - " +
                               "gamepad bindings will not take effect!");
        }

        #endregion

        #region Reporting

        /// <summary>Human-readable dump of the layout, for the settings panel and logs.</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            List<Bind> all = BaseLayer();
            all.AddRange(ModifierLayer());
            for (int i = 0; i < all.Count; i++)
                sb.AppendLine(all[i].label);

            return sb.ToString();
        }

        #endregion
    }
}
