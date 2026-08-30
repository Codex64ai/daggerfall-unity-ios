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
        // MEASURED on device 2026-08-20 with an Xbox Wireless Controller on iPadOS
        // (iPad Pro 11-inch M4), using MobileControllerProbe. These are the ONLY values
        // that should need changing for a different controller; re-run the probe
        // (TUNE -> "Controller probe overlay") and edit this block.
        //
        // Do not copy numbers from the internet, and do not assume the old 0-9 face-button
        // convention: on this controller NOTHING useful lives below Btn4. That is precisely
        // why the pre-0.1.2 layout felt scrambled - it bound Btn0-Btn9, which here lands on
        // the d-pad and shoulders instead of the face buttons.
        //
        // Two things the probe established that shape the choices below:
        //
        // 1. Every control except L3/R3 reports BOTH a button and a duplicate axis
        //    (Btn4 = Axis5, Btn5 = Axis6, ... Btn15 = Axis16 - a consistent +1 offset;
        //    L3/R3 have no axis because theirs would be Axis18/19, past InputManager's
        //    16-axis limit). Buttons are bound rather than axes: they need no
        //    EnableController gate, they are polled before axis keycodes so they win the
        //    heldKeys budget, and they sidestep resting-value ambiguity entirely.
        //
        // 2. Every axis rests at 0.00 on this controller - no trigger resting at -1 - so
        //    there is no permanently-held phantom axis to design around here. Do not
        //    assume that holds for the next controller; that is what the probe is for.
        //

        static KeyCode A       { get { return Btn(14); } }
        static KeyCode B       { get { return Btn(13); } }
        static KeyCode X       { get { return Btn(15); } }
        static KeyCode Y       { get { return Btn(12); } }
        static KeyCode LB      { get { return Btn(8);  } }
        static KeyCode RB      { get { return Btn(9);  } }
        static KeyCode LT      { get { return Btn(10); } }
        static KeyCode RT      { get { return Btn(11); } }
        static KeyCode Start   { get { return Btn(16); } }
        static KeyCode L3      { get { return Btn(17); } }
        static KeyCode R3      { get { return Btn(18); } }
        static KeyCode DUp     { get { return Btn(4);  } }
        static KeyCode DRight  { get { return Btn(5);  } }
        static KeyCode DDown   { get { return Btn(6);  } }
        static KeyCode DLeft   { get { return Btn(7);  } }

        // SELECT / VIEW IS DELIBERATELY UNBOUND.
        //
        // The probe recorded Select as Btn0 and nothing else, and recorded Start as
        // Btn0 + Btn16. Both readings point away from binding Btn0, whichever explains it:
        //
        //   - If Btn0 really is Select, then Start reports it too, so binding Btn0 to Rest
        //     would fire Rest every time the player pauses.
        //   - If Btn0 is the documented iPadOS phantom (this port has been bitten by
        //     JoystickButton0 pulsing during touches before - see REVIEW.md and
        //     MobileInputController.ClearPhantomProneBindings), then it is not a button at
        //     all and binding it would pin an action on.
        //
        // Either way Btn0 is unusable, so Rest and QuickLoad live on LT combos instead
        // (see BaseLayer/ModifierLayer). Select's real keycode is still unknown - the probe
        // most likely advanced on a stray Btn0 before the button was pressed, so it may
        // yet turn out to be Btn1, Btn2 or Btn3. If a re-probe pins it down, add it here
        // and move Rest/QuickLoad back onto Select.

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

                // Rest and QuickLoad were specified on Select and LT+Select. Select reports
                // as Btn0, which is unusable on this controller (see the CONTROLLER MAP
                // block), so they take the two free LT combos instead. RT and R3 are the
                // only base-layer inputs with no LT variant of their own.
                new Bind(Combo(LT, RT),     InputManager.Actions.Rest,         "LT + RT      -> Rest"),
                new Bind(Combo(LT, R3),     InputManager.Actions.QuickLoad,    "LT + R3      -> Quickload"),
            };
        }

        /// <summary>
        /// Menu / classic-UI pointer bindings. Separate table from the gameplay actions:
        /// InputManager routes UI clicks through joystickUIDict (GetMouseButtonDown and
        /// friends), not through the action system.
        ///
        /// DFU's stock defaults are JoystickButton0-3 - the conventional face-button
        /// numbers. On the probed controller NOTHING useful lives below Btn4: Btn0 is the
        /// Start co-fire (and the iPadOS phantom), Btn1-3 map to no physical control at
        /// all. Device-reported result: Start "clicked" in menus, A did nothing, and
        /// right-click/back were unreachable. Same bug as the old gameplay layout, one
        /// table over.
        /// </summary>
        static (KeyCode code, InputManager.JoystickUIActions action, string label)[] UILayer()
        {
            return new (KeyCode, InputManager.JoystickUIActions, string)[]
            {
                (A, InputManager.JoystickUIActions.LeftClick,   "A       -> UI select (left click)"),
                (X, InputManager.JoystickUIActions.RightClick,  "X       -> UI right click"),
                (Y, InputManager.JoystickUIActions.MiddleClick, "Y       -> UI middle click"),
                (B, InputManager.JoystickUIActions.Back,        "B       -> UI back / close window"),
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

            var ui = UILayer();
            for (int i = 0; i < ui.Length; i++)
            {
                input.SetJoystickUIBinding(ui[i].code, ui[i].action);
                log.AppendLine("   " + ui[i].label);
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

            // Put the UI pointer table back to DFU's stock numbers. Live-only like
            // everything else here; nothing is written to KeyBinds.txt, so a fresh launch
            // is untouched either way - this just keeps the in-memory state honest for the
            // rest of the session.
            input.SetJoystickUIBinding(KeyCode.JoystickButton0, InputManager.JoystickUIActions.LeftClick);
            input.SetJoystickUIBinding(KeyCode.JoystickButton3, InputManager.JoystickUIActions.RightClick);
            input.SetJoystickUIBinding(KeyCode.JoystickButton2, InputManager.JoystickUIActions.MiddleClick);
            input.SetJoystickUIBinding(KeyCode.JoystickButton1, InputManager.JoystickUIActions.Back);

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

        #region Passive unknown-button watch

        // Buttons this layout binds. Anything else that fires is worth recording: on the
        // probed controller Btn0-3 map to no known control, and Select/View is still
        // unidentified because a phantom Btn0 pulse consumed its probe prompt.
        static readonly System.Collections.Generic.HashSet<int> mappedButtons =
            new System.Collections.Generic.HashSet<int>
            { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

        static readonly System.Collections.Generic.HashSet<int> alreadyLogged =
            new System.Collections.Generic.HashSet<int>();

        const string watchFileName = "controller-unknown-buttons.txt";
        const int maxWatchLines = 60;
        static int watchLinesWritten;

        /// <summary>
        /// Record any joystick button that fires but is not part of the layout, along with
        /// the context needed to interpret it. Passive: the player just plays, and the file
        /// answers the question - no dedicated probe session, which is why Select/View has
        /// stayed unknown through several releases.
        ///
        /// The two context flags are the whole point. iPadOS pulses JoystickButton0 during
        /// touches, so a press logged with touches active is probably that phantom; and
        /// Start co-fires Btn0 alongside its own Btn16, so a Btn0 seen WITHOUT Btn16 and
        /// WITHOUT touches is very likely the real Select/View.
        /// </summary>
        public static void WatchUnknownButtons()
        {
            // Development builds only: this writes a file into the player's Documents.
            if (!Debug.isDebugBuild)
                return;
            if (!MobileInput.Enabled || watchLinesWritten >= maxWatchLines)
                return;

            for (int n = 0; n <= 19; n++)
            {
                if (mappedButtons.Contains(n) || !Input.GetKeyDown(Btn(n)))
                    continue;

                bool touching = Input.touchCount > 0;
                bool startHeld = Input.GetKey(Start);

                // One line per distinct (button, context) combination - enough to answer
                // the question without filling the file during normal play.
                int signature = n * 4 + (touching ? 2 : 0) + (startHeld ? 1 : 0);
                if (!alreadyLogged.Add(signature))
                    continue;

                Append(string.Format(
                    "Btn{0,-2} touchesActive={1,-5} startHeld={2,-5} t={3:0.0}s{4}",
                    n, touching, startHeld, Time.unscaledTime,
                    (n == 0 && !touching && !startHeld)
                        ? "   <== LIKELY Select/View (no touch, no Start)" : ""));
            }
        }

        static void Append(string line)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, watchFileName);

                if (watchLinesWritten == 0 && !System.IO.File.Exists(path))
                {
                    System.IO.File.WriteAllText(path,
                        "Unmapped controller buttons seen during play.\n" +
                        "Btn0-3 are unmapped on the reference controller; Select/View is still\n" +
                        "unidentified. touchesActive=True suggests the iPadOS phantom pulse;\n" +
                        "startHeld=True means Start co-fired it. A Btn0 with both False is\n" +
                        "very likely the real Select/View button.\n\n");
                }

                System.IO.File.AppendAllText(path, line + "\n");
                watchLinesWritten++;
                Debug.Log("[MobileGamepadBindings] unmapped button: " + line);
            }
            catch (System.Exception ex)
            {
                watchLinesWritten = maxWatchLines;   // stop trying
                Debug.LogWarning("[MobileGamepadBindings] could not write button watch: " + ex.Message);
            }
        }

        #endregion

        #region Reporting

        #endregion
    }
}
