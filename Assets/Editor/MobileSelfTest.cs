// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Headless verification of the touch layer's pure logic.
//
//   Menu: Tools > Daggerfall Mobile > Run Self Test
//   CLI:  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll
//
// Deliberately not NUnit: this project has no asmdefs, so everything lands in the
// predefined assemblies and test discovery there is unreliable. A plain -executeMethod
// entry point always works and exits non-zero on failure, which is what CI needs.
//
// Covers only logic that is genuinely device-independent - button edge derivation,
// unit conversion, threshold maths, state teardown. It cannot cover touch feel; that
// needs a finger.
//
// Place in Assets/Editor/

using DaggerfallWorkshop.Game.Mobile;
using DaggerfallWorkshop.Utility.AssetInjection;
using System.IO;
using System.Text;
using UnityEngine;
using DaggerfallConnect.Utility;
using UnityEditor;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileSelfTest
    {
        static int passed;
        static int failed;
        static StringBuilder log;

        [MenuItem("Tools/Daggerfall Mobile/Run Self Test")]
        public static void RunAll()
        {
            passed = 0;
            failed = 0;
            log = new StringBuilder();
            log.AppendLine("=== Mobile touch layer self test ===");

            TestButtonEdges();
            TestLatchedButton();
            TestBackButtonEdges();
            TestScrollOneStepPerFrame();
            TestControllerForcesCursorOff();
            TestKeyboardForcesCursorOff();
            TestInputModeResolution();
            TestSwingModeDecision();
            TestPointerKeepsCursorOverKeyboard();
            TestPointerDeltaScale();
            TestPointerLockDecision();
            TestPointerDrainDecision();
            TestPointerHoverToScreen();
            TestPointerScrollTicks();
            TestPointerFingerRule();
            TestPointerClickGrace();
            TestHardwareKeyboardTable();
            TestPointerDefaultActions();
            TestDpiFallback();
            TestThresholdMaths();
            TestThresholdRoundTrip();
            TestDeviceIndependence();
            TestRelinquish();
            TestContentPathRemap();
            TestWavDecoder();
            TestJourneyBearing();
            TestJourneyArrivalRect();
            TestJourneyCompressionClamp();
            TestJourneySpeedTiers();
            TestRouteRule();
            TestNightDecision();
            TestRoadData();
            TestRoadsInstallSurvivesSceneSwap();
            TestRoadDirectionReciprocity();
            TestRoadRouting();
            TestWaypointOvershoot();

            log.AppendLine();
            log.AppendLine(string.Format("=== {0} passed, {1} failed ===", passed, failed));

            if (failed > 0)
            {
                Debug.LogError(log.ToString());
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log(log.ToString());
            }
        }

        /// <summary>
        /// The user-content path arithmetic. Exercised with injected roots because on desktop
        /// MobileContentPath.Active is false and Override() is a deliberate no-op - otherwise
        /// the prefix matching and separator handling would never be tested anywhere.
        /// </summary>
        static void TestContentPathRemap()
        {
            const string shipped = "/app/Data/Raw";
            const string user = "/docs";

            // Player has the file: the user copy wins.
            Check(MobileContentPath.Remap(shipped + "/Textures/180_0-0.png", shipped, user,
                      p => p == "/docs/Textures/180_0-0.png") == "/docs/Textures/180_0-0.png",
                  "remap prefers an existing user file");

            // Player does not have it: falls back to the shipped file. This is the case that
            // matters most - 265 shipped quests must stay reachable.
            Check(MobileContentPath.Remap(shipped + "/Quests/S0000977.txt", shipped, user,
                      p => false) == shipped + "/Quests/S0000977.txt",
                  "remap falls back to the shipped file");

            // Paths outside the shipped root are left alone.
            Check(MobileContentPath.Remap("/somewhere/else/x.png", shipped, user, p => true)
                      == "/somewhere/else/x.png",
                  "remap ignores paths outside the shipped root");

            // The root itself must not remap to the user root wholesale.
            Check(MobileContentPath.Remap(shipped, shipped, user, p => true) == shipped,
                  "remap leaves the root itself alone");

            Check(MobileContentPath.Remap(null, shipped, user, p => true) == null,
                  "remap tolerates null");

            // A leading separator must not defeat Path.Combine.
            Check(MobileContentPath.Remap(shipped + "/Sound/a.wav", shipped, user,
                      p => p == "/docs/Sound/a.wav") == "/docs/Sound/a.wav",
                  "remap strips the leading separator");
        }

        /// <summary>
        /// The hand-rolled RIFF/WAVE decoder that replaces the legacy WWW("file://") path on
        /// iOS. Written by hand, so it gets tested rather than trusted: a real file is built
        /// on disk with known sample values and decoded back.
        /// </summary>
        static void TestWavDecoder()
        {
            string path = Path.Combine(Path.GetTempPath(), "dfu_selftest.wav");

            const int rate = 22050;
            const int channels = 1;
            const int frames = 512;

            // 16-bit PCM mono, with a deliberate LIST chunk before 'data' so the decoder has
            // to walk the chunk list instead of assuming a 44-byte header.
            var pcm = new byte[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                short v = (short)(i == 0 ? 0 : (i == 1 ? 32767 : (i == 2 ? -32768 : 1000)));
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            byte[] junk = System.Text.Encoding.ASCII.GetBytes("INFOhello!!!");

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(0);                                       // patched below
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);                                 // PCM
                w.Write((short)channels);
                w.Write(rate);
                w.Write(rate * channels * 2);                      // byte rate
                w.Write((short)(channels * 2));                    // block align
                w.Write((short)16);                                // bits

                w.Write(System.Text.Encoding.ASCII.GetBytes("LIST"));
                w.Write(junk.Length);
                w.Write(junk);

                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(pcm.Length);
                w.Write(pcm);

                w.Flush();
                byte[] all = ms.ToArray();
                int riffSize = all.Length - 8;
                all[4] = (byte)(riffSize & 0xff);
                all[5] = (byte)((riffSize >> 8) & 0xff);
                all[6] = (byte)((riffSize >> 16) & 0xff);
                all[7] = (byte)((riffSize >> 24) & 0xff);
                File.WriteAllBytes(path, all);
            }

            AudioClip clip;
            bool ok = SoundReplacement.TryDecodeWavFromDisk(path, "selftest", out clip);

            Check(ok && clip != null, "wav decodes to a clip");
            if (ok && clip != null)
            {
                Check(clip.channels == channels, "wav channel count", "got " + clip.channels);
                Check(clip.frequency == rate, "wav sample rate", "got " + clip.frequency);
                Check(clip.samples == frames, "wav sample count (chunk walk found data)",
                      "got " + clip.samples);

                var got = new float[frames];
                clip.GetData(got, 0);
                Near(got[0], 0f, 0.001f, "wav sample 0 is silence");
                Near(got[1], 1f, 0.001f, "wav sample 1 is full positive");
                Near(got[2], -1f, 0.001f, "wav sample 2 is full negative");
            }

            // Malformed input must be refused, not throw - a bad file in a mod folder should
            // fall back to the original sound, not take the game down.
            string bad = Path.Combine(Path.GetTempPath(), "dfu_selftest_bad.wav");
            File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            AudioClip badClip;
            bool badOk;
            try
            {
                badOk = SoundReplacement.TryDecodeWavFromDisk(bad, "bad", out badClip);
                Check(!badOk, "malformed wav is refused without throwing");
            }
            catch (System.Exception ex)
            {
                Check(false, "malformed wav is refused without throwing", ex.GetType().Name);
            }

            try { File.Delete(path); File.Delete(bad); } catch { }
        }

        #region Assertions

        static void Check(bool condition, string name, string detail = "")
        {
            if (condition)
            {
                passed++;
                log.AppendLine("  PASS  " + name);
            }
            else
            {
                failed++;
                log.AppendLine("  FAIL  " + name + (string.IsNullOrEmpty(detail) ? "" : "   -> " + detail));
            }
        }

        static void Near(float actual, float expected, float tol, string name)
        {
            bool ok = Mathf.Abs(actual - expected) <= tol;
            Check(ok, name, string.Format("expected ~{0}, got {1}", expected, actual));
        }

        #endregion

        #region Tests

        /// <summary>
        /// The input-mode table. Auto must reproduce the shipped detection behaviour exactly;
        /// the three overrides must ignore detection in the directions that matter - a phantom
        /// joystick (the iOS 26 Simulator lists one) must not be able to hide the touch HUD in
        /// Touch mode, and Controller mode must work with nothing listed at all.
        /// </summary>
        static void TestInputModeResolution()
        {
            EffectiveInput e = MobileInput.ResolveInput(MobileInputMode.Auto, false, false, false);
            Check(e.TouchHud && !e.Controller && !e.Keyboard && !e.Mouse, "auto: nothing physical -> touch HUD");

            e = MobileInput.ResolveInput(MobileInputMode.Auto, true, false, false);
            Check(!e.TouchHud && e.Controller, "auto: pad detected -> pad drives, touch stands down");

            e = MobileInput.ResolveInput(MobileInputMode.Auto, false, true, true);
            Check(!e.TouchHud && e.Keyboard && e.Mouse && !e.Controller, "auto: keyboard + pointer detected -> both drive");

            e = MobileInput.ResolveInput(MobileInputMode.Touch, true, true, true);
            Check(e.TouchHud && !e.Controller && !e.Keyboard && !e.Mouse, "touch: phantom pad, keyboard and pointer all ignored");

            e = MobileInput.ResolveInput(MobileInputMode.KeyboardMouse, true, false, false);
            Check(!e.TouchHud && e.Keyboard && !e.Mouse && !e.Controller,
                  "kb+mouse: keyboard counts without a keystroke, pad ignored, no pointer until one connects");

            e = MobileInput.ResolveInput(MobileInputMode.KeyboardMouse, false, false, true);
            Check(e.Mouse && e.Keyboard && !e.TouchHud, "kb+mouse: connected pointer drives look and cursor");

            e = MobileInput.ResolveInput(MobileInputMode.Controller, false, true, true);
            Check(!e.TouchHud && e.Controller && !e.Keyboard && !e.Mouse,
                  "controller: pad path on with nothing listed; keyboard and pointer stand down");
        }

        /// <summary>
        /// WeaponSwingMode: touch swipes need hold-and-drag (0); everyone else keeps what they
        /// chose in the launcher - which is where "click to attack" was being lost. With a
        /// classic window open the player's value must be the one in memory, because that is
        /// the only time settings.ini gets written.
        /// </summary>
        static void TestSwingModeDecision()
        {
            Check(MobileInput.ResolveSwingMode(1, true, false) == 0, "touch play imposes hold-and-drag");
            Check(MobileInput.ResolveSwingMode(1, false, false) == 1, "mouse/pad play keeps click-to-attack");
            Check(MobileInput.ResolveSwingMode(2, false, false) == 2, "hold-to-attack kept too");
            Check(MobileInput.ResolveSwingMode(1, true, true) == 1, "window open -> player's own value, so saves keep it");
            Check(MobileInput.ResolveSwingMode(0, false, false) == 0, "vanilla stays vanilla");

            // The port's own switches.
            Check(MobileInput.ResolveSwingMode(0, false, false, true, false) == 1,
                  "click-to-attack on: pointer/pad get click mode even when the launcher says vanilla");
            Check(MobileInput.ResolveSwingMode(2, false, false, false, false) == 2,
                  "click-to-attack off: pointer/pad keep the launcher's choice");
            Check(MobileInput.ResolveSwingMode(1, true, false, true, false) == 0,
                  "touch without tap-to-attack still swipes");
            Check(MobileInput.ResolveSwingMode(0, true, false, true, true) == 1,
                  "tap-to-attack on: touch runs click mode");
            Check(MobileInput.ResolveSwingMode(0, true, true, true, true) == 0,
                  "window open -> launcher value regardless of switches");
        }

        /// <summary>A queued click must produce exactly one Down frame and one Up frame.</summary>
        static void TestButtonEdges()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueClick(0, 3);

            int downs = 0, ups = 0, heldFrames = 0;
            for (int i = 0; i < 8; i++)
            {
                MobileInput.TickButtons();
                if (MobileInput.GetMouseButtonDown(0)) downs++;
                if (MobileInput.GetMouseButtonUp(0)) ups++;
                if (MobileInput.GetMouseButton(0)) heldFrames++;
            }

            Check(downs == 1, "click yields exactly one Down", "downs=" + downs);
            Check(ups == 1, "click yields exactly one Up", "ups=" + ups);
            Check(heldFrames == 3, "click held for 3 frames", "held=" + heldFrames);
            MobileInput.ResetButtons();
        }

        /// <summary>Long-press latch stays down until explicitly released.</summary>
        static void TestLatchedButton()
        {
            MobileInput.ResetButtons();
            MobileInput.SetLatched(0, true);

            for (int i = 0; i < 4; i++)
                MobileInput.TickButtons();

            Check(MobileInput.GetMouseButton(0), "latched button stays held");
            Check(!MobileInput.GetMouseButtonDown(0), "latched button does not re-fire Down");

            MobileInput.SetLatched(0, false);
            MobileInput.TickButtons();
            Check(MobileInput.GetMouseButtonUp(0), "releasing latch yields Up");
            MobileInput.ResetButtons();
        }

        /// <summary>
        /// The back channel matters: every classic window closes on GetBackButtonUp(),
        /// so a press with no release edge would never close anything.
        /// </summary>
        static void TestBackButtonEdges()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueBack(3);

            int downs = 0, ups = 0;
            for (int i = 0; i < 8; i++)
            {
                MobileInput.TickButtons();
                if (MobileInput.GetBackButtonDown()) downs++;
                if (MobileInput.GetBackButtonUp()) ups++;
            }

            Check(downs == 1, "back yields exactly one Down", "downs=" + downs);
            Check(ups == 1, "back yields exactly one Up (windows close on Up)", "ups=" + ups);
            MobileInput.ResetButtons();
        }

        /// <summary>BaseScreenComponent only reads the sign, so emit one step per frame.</summary>
        static void TestScrollOneStepPerFrame()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueScroll(3f);

            int steps = 0;
            for (int i = 0; i < 6; i++)
            {
                MobileInput.TickButtons();
                if (!Mathf.Approximately(MobileInput.MouseScroll, 0f))
                {
                    steps++;
                    Check(Mathf.Abs(MobileInput.MouseScroll) <= 1.0001f,
                          "scroll step magnitude <= 1 (frame " + i + ")");
                }
            }
            Check(steps == 3, "3 queued ticks emit 3 frames of scroll", "steps=" + steps);
            MobileInput.ResetButtons();
        }

        /// <summary>
        /// Critical integration rule: with a gamepad connected the touch cursor must stand
        /// down so DFU's own controller cursor keeps the pointer.
        /// </summary>
        static void TestControllerForcesCursorOff()
        {
            bool savedController = MobileInput.ControllerActive;

            MobileInput.ControllerActive = false;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "cursor active with no gamepad");

            MobileInput.ControllerActive = true;
            Check(!MobileInput.VirtualCursorActive, "gamepad forces virtual cursor OFF");

            MobileInput.ControllerActive = false;
            Check(MobileInput.VirtualCursorActive, "cursor restored when gamepad disconnects");

            MobileInput.VirtualCursorActive = false;
            MobileInput.ControllerActive = savedController;
        }

        /// <summary>
        /// A hardware keyboard must stand the touch layer down exactly like a gamepad,
        /// otherwise the classic UI gets the virtual cursor while the player is typing.
        /// </summary>
        static void TestKeyboardForcesCursorOff()
        {
            bool savedKeyboard = MobileInput.KeyboardActive;
            bool savedController = MobileInput.ControllerActive;

            MobileInput.ControllerActive = false;
            MobileInput.KeyboardActive = false;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "cursor active with no physical input");

            MobileInput.KeyboardActive = true;
            Check(!MobileInput.VirtualCursorActive, "keyboard forces virtual cursor OFF");
            Check(MobileInput.PhysicalInputActive, "PhysicalInputActive true for keyboard");

            MobileInput.KeyboardActive = false;
            Check(MobileInput.VirtualCursorActive, "cursor restored when keyboard idles");

            MobileInput.VirtualCursorActive = false;
            MobileInput.KeyboardActive = savedKeyboard;
            MobileInput.ControllerActive = savedController;
        }

        /// <summary>
        /// A real pointer DRIVES the virtual cursor rather than standing it down: hover feeds
        /// the position and GCMouse buttons feed the clicks, so the classic UI never consults
        /// Unity's phantom-held Input.GetMouseButton(0). That must hold even with a hardware
        /// keyboard active (Magic Keyboard = keyboard + trackpad together), where the keyboard
        /// alone would have switched the cursor off. A gamepad still wins outright.
        /// </summary>
        static void TestPointerKeepsCursorOverKeyboard()
        {
            bool savedKeyboard = MobileInput.KeyboardActive;
            bool savedController = MobileInput.ControllerActive;
            bool savedMouse = MobileInput.MouseActive;

            MobileInput.ControllerActive = false;
            MobileInput.KeyboardActive = false;
            MobileInput.MouseActive = true;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "pointer alone keeps the virtual cursor");
            Check(MobileInput.PhysicalInputActive, "PhysicalInputActive true for pointer");

            MobileInput.KeyboardActive = true;
            Check(MobileInput.VirtualCursorActive, "pointer + keyboard keeps the virtual cursor");

            MobileInput.MouseActive = false;
            Check(!MobileInput.VirtualCursorActive, "keyboard alone still forces cursor OFF");

            MobileInput.MouseActive = true;
            MobileInput.ControllerActive = true;
            Check(!MobileInput.VirtualCursorActive, "gamepad beats pointer for the cursor");

            MobileInput.VirtualCursorActive = false;
            MobileInput.KeyboardActive = savedKeyboard;
            MobileInput.ControllerActive = savedController;
            MobileInput.MouseActive = savedMouse;
        }

        /// <summary>
        /// GCMouse reports raw counts; Unity's "Mouse X/Y" axes are counts x 0.1 (the project's
        /// InputManager.asset sensitivity). Matching that keeps DFU's own mouse-sensitivity
        /// setting meaning the same thing it does on PC. Y is positive-up in both systems, so
        /// the flip is OFF by default and only exists as a device-verification escape hatch.
        /// </summary>
        static void TestPointerDeltaScale()
        {
            Vector2 d = MobilePointer.ScaleDelta(new Vector2(40f, -20f), 0.1f, false);
            Near(d.x, 4f, 0.0001f, "delta X scaled by 0.1");
            Near(d.y, -2f, 0.0001f, "delta Y scaled by 0.1, sign kept");

            Vector2 f = MobilePointer.ScaleDelta(new Vector2(40f, -20f), 0.1f, true);
            Near(f.y, 2f, 0.0001f, "flipY inverts Y only");
            Near(f.x, 4f, 0.0001f, "flipY leaves X alone");

            Vector2 z = MobilePointer.ScaleDelta(Vector2.zero, 0.1f, true);
            Check(z == Vector2.zero, "zero delta stays zero");
        }

        /// <summary>
        /// The pointer is locked exactly when PlayerMouseLook would have locked it on PC:
        /// a pointer is in use, no classic window is open, the game is not paused, and the
        /// engine has hidden its cursor. Any one of those failing releases the pointer, so
        /// menus, the pause screen and the ActivateCursor toggle all get the arrow back.
        /// </summary>
        static void TestPointerLockDecision()
        {
            Check(MobilePointer.ShouldLock(true, false, false, false), "locks in plain gameplay");
            Check(!MobilePointer.ShouldLock(false, false, false, false), "no pointer -> no lock");
            Check(!MobilePointer.ShouldLock(true, true, false, false), "menu open -> unlocked");
            Check(!MobilePointer.ShouldLock(true, false, true, false), "paused -> unlocked");
            Check(!MobilePointer.ShouldLock(true, false, false, true), "engine cursor visible -> unlocked");
        }

        /// <summary>
        /// Regression for the first device build: the cursor-stage pump ran before the
        /// gameplay pump every frame and drained the deltas in live play, so the pointer
        /// locked and then never moved. Draining is legal in exactly one state - paused with
        /// no classic window open.
        /// </summary>
        static void TestPointerDrainDecision()
        {
            Check(!MobilePointer.ShouldDrainInCursorStage(false, false), "live play -> never drain (the camera owns the deltas)");
            Check(MobilePointer.ShouldDrainInCursorStage(false, true), "paused, no window -> drain");
            Check(!MobilePointer.ShouldDrainInCursorStage(true, true), "menu open -> menu pump owns it, no drain here");
            Check(!MobilePointer.ShouldDrainInCursorStage(true, false), "menu open unpaused -> no drain here");
        }

        /// <summary>
        /// Hover arrives normalised (0..1, bottom-left origin) so the plugin never has to
        /// agree with Unity about contentScaleFactor. Corners must land on the pixel edges.
        /// </summary>
        static void TestPointerHoverToScreen()
        {
            Vector2 c = MobilePointer.HoverToScreen(0.5f, 0.5f, 2000, 1000);
            Near(c.x, 1000f, 0.001f, "hover centre X");
            Near(c.y, 500f, 0.001f, "hover centre Y");

            Vector2 tl = MobilePointer.HoverToScreen(0f, 1f, 2000, 1000);
            Near(tl.x, 0f, 0.001f, "hover left edge");
            Near(tl.y, 1000f, 0.001f, "hover top edge (bottom-left origin)");

            Vector2 over = MobilePointer.HoverToScreen(1.5f, -0.5f, 2000, 1000);
            Near(over.x, 2000f, 0.001f, "hover clamps X into the screen");
            Near(over.y, 0f, 0.001f, "hover clamps Y into the screen");
        }

        /// <summary>
        /// Scroll wheel/trackpad values have no defined range, so the accumulator emits at
        /// most one classic-UI step per frame once it crosses the threshold, and carries
        /// nothing over - a hard flick must not keep a list scrolling for seconds.
        /// </summary>
        static void TestPointerScrollTicks()
        {
            float acc = 0.2f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == 0, "below threshold -> no tick");
            Near(acc, 0.2f, 0.0001f, "sub-threshold scroll is kept");

            acc = 0.7f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == 1, "above threshold -> one tick up");
            Near(acc, 0f, 0.0001f, "tick consumes the accumulator");

            acc = -30f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == -1, "large flick -> still exactly one tick down");
            Near(acc, 0f, 0.0001f, "large flick does not carry over");
        }

        /// <summary>
        /// A touch counts as a FINGER (and so hands control back to the touch layer) only if it
        /// is not an indirect device and no pointer button is down. iPadOS delivers pointer
        /// clicks as touches - without this rule every click would flip the touch HUD back on.
        /// </summary>
        static void TestPointerFingerRule()
        {
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false), "direct touch, no button -> finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Stylus, false), "pencil counts as a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Indirect, false), "indirect touch -> not a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, true), "touch while a pointer button is held -> pointer click, not a finger");
        }

        /// <summary>
        /// Fallback layout when KeyBinds.txt has no mouse bindings to capture: Daggerfall's
        /// own defaults, left = activate, right = swing. Anything else is unbound.
        /// </summary>
        static void TestPointerDefaultActions()
        {
            InputManager.Actions a;
            Check(MobilePointer.TryDefaultAction(0, out a) && a == InputManager.Actions.ActivateCenterObject, "left button -> ActivateCenterObject");
            Check(MobilePointer.TryDefaultAction(1, out a) && a == InputManager.Actions.SwingWeapon, "right button -> SwingWeapon");
            Check(!MobilePointer.TryDefaultAction(2, out a), "middle button unbound by default");
            Check(!MobilePointer.TryDefaultAction(-1, out a), "invalid button unbound");
        }

        /// <summary>Screen.dpi returns 0 on some devices; the fallback must hold.</summary>
        static void TestDpiFallback()
        {
            Check(MobileInput.Dpi > 1f, "Dpi is usable (fallback works)", "dpi=" + MobileInput.Dpi);
            Near(MobileInput.InchesToPixels(1f), MobileInput.Dpi, 0.01f, "1 inch == dpi pixels");
            Near(MobileInput.InchesToPixels(0f), 0f, 0.001f, "0 inches == 0 pixels");
        }

        static void TestThresholdMaths()
        {
            // 0.9in at 264dpi on a 2752px longest edge, scale 0.15
            float t = MobileInputController.ComputeAttackThreshold(0.9f, 0.15f, 264f, 2752f);
            Near(t, (0.9f * 264f * 0.15f) / 2752f, 1e-6f, "threshold formula matches derivation");
            Check(t > 0f && t < 1f, "threshold in sane range", "t=" + t);
        }

        static void TestThresholdRoundTrip()
        {
            const float inches = 0.9f, scale = 0.15f, dpi = 264f, dim = 2752f;
            float t = MobileInputController.ComputeAttackThreshold(inches, scale, dpi, dim);
            float px = MobileInputController.RequiredSwipePixels(t, scale, dim);
            Near(px, inches * dpi, 0.5f, "round trip recovers the physical distance");
        }

        /// <summary>
        /// The whole point of DPI normalisation: the same setting must mean the same
        /// PHYSICAL swipe on a dense phone and a large tablet, even though the old
        /// screen-fraction approach differed by ~2x.
        /// </summary>
        static void TestDeviceIndependence()
        {
            const float inches = 0.9f, scale = 0.15f;

            // iPhone 17 Pro class: ~460dpi, 2622px longest edge
            float tPhone = MobileInputController.ComputeAttackThreshold(inches, scale, 460f, 2622f);
            float pxPhone = MobileInputController.RequiredSwipePixels(tPhone, scale, 2622f);

            // 13in iPad Pro class: ~264dpi, 2752px longest edge
            float tPad = MobileInputController.ComputeAttackThreshold(inches, scale, 264f, 2752f);
            float pxPad = MobileInputController.RequiredSwipePixels(tPad, scale, 2752f);

            Near(pxPhone / 460f, inches, 0.02f, "phone requires 0.9in of travel");
            Near(pxPad / 264f, inches, 0.02f, "tablet requires 0.9in of travel");
            Check(!Mathf.Approximately(pxPhone, pxPad),
                  "pixel counts differ while physical distance matches");
        }

        /// <summary>
        /// Teardown must hand the pointer back, or the classic UI is left with a frozen
        /// cursor and no fallback.
        /// </summary>
        static void TestRelinquish()
        {
            MobileInput.VirtualCursorActive = true;
            MobileInput.QueueClick(0);
            MobileInput.TickButtons();

            MobileInput.Relinquish();

            Check(!MobileInput.VirtualCursorActive, "Relinquish clears VirtualCursorActive");
            Check(!MobileInput.GetMouseButton(0), "Relinquish clears button state");
            Check(MobileInput.Mode == MobileControlMode.Gameplay, "Relinquish resets mode");
        }

        /// <summary>
        /// A journey steers by bearing alone, so a wrong bearing walks the player away from
        /// the destination for the entire trip. Unity yaw: 0 faces +Z, 90 faces +X.
        /// </summary>
        static void TestJourneyBearing()
        {
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 0f, 100f), 0f, 0.01f,
                 "bearing: due north is 0");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 100f, 0f), 90f, 0.01f,
                 "bearing: due east is 90");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 0f, -100f), 180f, 0.01f,
                 "bearing: due south is 180");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, -100f, 0f), 270f, 0.01f,
                 "bearing: due west is 270");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 100f, 100f), 45f, 0.01f,
                 "bearing: north-east is 45");

            // Never negative - the value is compared and logged, so a stable range matters.
            bool allInRange = true;
            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                float b = MobileJourneyPilot.BearingDegrees(
                    0f, 0f, Mathf.Sin(rad) * 500f, Mathf.Cos(rad) * 500f);
                if (b < 0f || b >= 360.01f)
                    allInRange = false;
            }
            Check(allInRange, "bearing: always normalised to 0-360");

            // Offset start position must not change the bearing - only the delta matters.
            Near(MobileJourneyPilot.BearingDegrees(5000f, -3000f, 5000f, -2900f), 0f, 0.01f,
                 "bearing: independent of absolute position");
        }

        /// <summary>
        /// The arrival rect is the location's rect grown on all four sides, so a journey stops
        /// outside the gates rather than walking itself into the location.
        /// </summary>
        static void TestJourneyArrivalRect()
        {
            Rect location = new Rect(10000f, 20000f, 400f, 600f);
            Rect arrival = MobileJourneyPilot.ArrivalRect(location);

            Check(arrival.Contains(new Vector2(location.center.x, location.center.y)),
                  "arrival rect: contains the location centre");

            // Grown, not shrunk, on every side.
            Check(arrival.xMin < location.xMin && arrival.xMax > location.xMax &&
                  arrival.yMin < location.yMin && arrival.yMax > location.yMax,
                  "arrival rect: grown on all four sides");

            // A point just outside the location but inside the margin must count as arrived,
            // which is the whole point of widening it.
            Check(arrival.Contains(new Vector2(location.xMin - 500f, location.center.y)),
                  "arrival rect: a point in the margin counts as arrived");

            // Far outside must not.
            Check(!arrival.Contains(new Vector2(location.xMin - 5000f, location.center.y)),
                  "arrival rect: a distant point does not count as arrived");

            Near(arrival.width - location.width, (arrival.height - location.height), 0.01f,
                 "arrival rect: margin applied equally to both axes");
        }

        static void TestJourneyCompressionClamp()
        {
            Check(MobileJourneyController.ClampCompression(0) >= 1,
                  "compression: zero clamps to at least 1x (time cannot stop)");
            Check(MobileJourneyController.ClampCompression(-50) >= 1,
                  "compression: negative clamps to at least 1x (time cannot reverse)");
            Check(MobileJourneyController.ClampCompression(9999) <=
                  MobileJourneyController.MaxTimeCompression,
                  "compression: absurd values clamp to the maximum");
            Check(MobileJourneyController.ClampCompression(20) == 20,
                  "compression: a legal value passes through unchanged");
            Check(MobileJourneyController.ClampCompression(
                      MobileJourneyController.DefaultTimeCompression) ==
                  MobileJourneyController.DefaultTimeCompression,
                  "compression: the default is itself legal");
        }

        /// <summary>
        /// The ceiling follows the transport (device decision): 50x on foot, 150x mounted,
        /// 200x by ship. Cautious vs reckless no longer changes speed.
        /// </summary>
        static void TestJourneySpeedTiers()
        {
            Check(MobileJourneyController.CapForTransport(TransportModes.Foot) == 50, "tiers: foot caps at 50x");
            Check(MobileJourneyController.CapForTransport(TransportModes.Horse) == 150, "tiers: horse caps at 150x");
            Check(MobileJourneyController.CapForTransport(TransportModes.Cart) == 150, "tiers: cart rides like a horse");
            Check(MobileJourneyController.CapForTransport(TransportModes.Ship) == 200, "tiers: ship caps at 200x");
            Check(MobileJourneyController.ClampCompression(200, TransportModes.Foot) == 50,
                  "tiers: 200x on foot clamps down to 50x");
            Check(MobileJourneyController.ClampCompression(150, TransportModes.Horse) == 150,
                  "tiers: a horse keeps 150x");
            Check(MobileJourneyController.ClampCompression(-5, TransportModes.Ship) >= 1,
                  "tiers: a ship still cannot reverse time");
        }

        /// <summary>
        /// The road rule that replaced "the road must be longer than the off-road ends" - which
        /// binned most medium trips. Plus the reset that used to wipe the planned route is a
        /// code-shape bug the tests cannot see; it is documented in Resume().
        /// </summary>
        static void TestRouteRule()
        {
            Check(MobileJourneyController.RouteWorthTaking(30, 10, 35), "route: a road with short off-road ends is taken");
            Check(MobileJourneyController.RouteWorthTaking(3, 10, 12), "route: a short road is still taken if reaching it is cheap");
            Check(!MobileJourneyController.RouteWorthTaking(30, 40, 20), "route: refused when the detour outweighs the trip");
            Check(!MobileJourneyController.RouteWorthTaking(1, 0, 5), "route: a one-pixel route is not a route");
        }

        /// <summary>Nightfall decision table: what the travel popup's option means at dusk.</summary>
        static void TestNightDecision()
        {
            var N = MobileJourneyController.NightAction.None;
            Check(MobileJourneyController.DecideNight(false, false, false, false, 100, 5) == N, "night: daytime does nothing");
            Check(MobileJourneyController.DecideNight(true, true, false, false, 100, 5) == N, "night: decided once per night");
            Check(MobileJourneyController.DecideNight(true, false, false, true, 100, 5) == MobileJourneyController.NightAction.Camp,
                  "night: camp out camps, even in a town");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 100, 5) == MobileJourneyController.NightAction.Inn,
                  "night: inns mode in a town takes a room");
            Check(MobileJourneyController.DecideNight(true, false, true, false, 100, 5) == MobileJourneyController.NightAction.TravelOn,
                  "night: inns mode in the wild walks on to the next town");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 3, 5) == MobileJourneyController.NightAction.CampNoGold,
                  "night: inns mode without the gold camps outside the walls");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 0, 0) == MobileJourneyController.NightAction.Inn,
                  "night: free rooms (knightly order) cost nothing");
            Check(MobileJourneyController.HoursUntilDawn(18) == 12 && MobileJourneyController.HoursUntilDawn(2) == 4 &&
                  MobileJourneyController.HoursUntilDawn(23) == 7,
                  "night: hours to dawn wrap past midnight");
        }

        /// <summary>The ported path data is present and looks like a road network.</summary>
        static void TestRoadData()
        {
            Check(MobileRoadNetwork.Available, "roads: path data loaded from Resources");
            if (!MobileRoadNetwork.Available)
                return;

            int withPath = 0;
            for (int y = 0; y < MobileRoadNetwork.Height; y += 3)
                for (int x = 0; x < MobileRoadNetwork.Width; x += 3)
                    if (MobileRoadNetwork.HasAnyPath(x, y))
                        withPath++;

            // Sampled every third pixel, so this is a shape check rather than a census: a
            // network covers a small but non-trivial slice of the world. Zero means the data
            // did not really load; a huge number means it is not a network at all.
            Check(withPath > 200, "roads: network is not empty",
                  "sampled pixels carrying a path: " + withPath);
            Check(withPath < MobileRoadNetwork.Width * MobileRoadNetwork.Height / 9 / 2,
                  "roads: network is sparse, as a road network should be",
                  "sampled pixels carrying a path: " + withPath);

            Check(!MobileRoadNetwork.InBounds(-1, 0) &&
                  !MobileRoadNetwork.InBounds(0, MobileRoadNetwork.Height),
                  "roads: bounds reject out-of-world pixels");
        }

        /// <summary>
        /// The bug that hid the roads: the texturing was assigned once, before any scene, to a
        /// DaggerfallUnity the game scene then replaced - whose fresh DefaultTerrainTexturing
        /// nobody overrode. Model exactly that: install, swap in a default (what a new
        /// DaggerfallUnity's field initialiser does), and require the install to come back.
        /// </summary>
        static void TestRoadsInstallSurvivesSceneSwap()
        {
            bool savedPref = MobileRoads.Enabled;
            try
            {
                MobileRoads.Enabled = true;
                DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                Check(!MobileRoads.Active, "roads: default texturing reads as not active");

                MobileRoads.InstallOnLiveInstance();
                Check(dfUnity.TerrainTexturing is BasicRoads.BasicRoadsTexturing,
                      "roads: install lands on the live DaggerfallUnity");
                Check(MobileRoads.Active && !MobileRoads.RestartRequired,
                      "roads: Active reflects the live instance");

                // A scene swap: the new DaggerfallUnity arrives with a default texturing.
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                Check(!MobileRoads.Active && MobileRoads.RestartRequired,
                      "roads: a replaced texturing is reported honestly");

                MobileRoads.InstallOnLiveInstance();
                Check(MobileRoads.Active, "roads: re-installed after the swap");

                MobileRoads.Enabled = false;
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                MobileRoads.InstallOnLiveInstance();
                Check(!MobileRoads.Active, "roads: not installed while the preference is off");
            }
            finally
            {
                MobileRoads.Enabled = savedPref;
                if (DaggerfallUnity.HasInstance)
                    DaggerfallUnity.Instance.TerrainTexturing = new DefaultTerrainTexturing();
            }
        }

        /// <summary>
        /// A right-click's touch can be seen a frame before GameController reports the button.
        /// Inside the grace window it must not count as a finger, or the touch HUD flashes on
        /// every attack. Outside it, with no button held, a direct touch is a finger.
        /// </summary>
        static void TestPointerClickGrace()
        {
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, false, 0.05f, 0.4f),
                  "grace: touch right after pointer activity is the click, not a finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false, 1.0f, 0.4f),
                  "grace: a touch well after pointer activity is a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, true, 5f, 0.4f),
                  "grace: button held is never a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Indirect, false, 5f, 0.4f),
                  "grace: indirect touch is never a finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false),
                  "grace: two-argument rule unchanged for callers without timing");
        }

        /// <summary>The HID table must round-trip and cover what Daggerfall binds by default.</summary>
        static void TestHardwareKeyboardTable()
        {
            KeyCode[] must = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.Return,
                               KeyCode.Escape, KeyCode.LeftShift, KeyCode.UpArrow, KeyCode.F5, KeyCode.Alpha0,
                               KeyCode.Keypad0, KeyCode.Tab, KeyCode.BackQuote };
            bool ok = true;
            foreach (KeyCode k in must)
            {
                int hid = MobileHardwareKeyboard.ToHid(k);
                if (hid < 0 || MobileHardwareKeyboard.FromHid(hid) != k)
                    ok = false;
            }
            Check(ok, "keyboard: HID table round-trips the default bindings");
            Check(MobileHardwareKeyboard.FromHid(4) == KeyCode.A && MobileHardwareKeyboard.FromHid(29) == KeyCode.Z,
                  "keyboard: letters follow HID usage order");
            Check(MobileHardwareKeyboard.FromHid(0) == KeyCode.None && MobileHardwareKeyboard.ToHid(KeyCode.Mouse0) < 0,
                  "keyboard: unknown codes are None / -1, so callers fall back");
            bool held;
            Check(!MobileHardwareKeyboard.TryGetKey(KeyCode.W, out held) && !held,
                  "keyboard: no plugin in the editor -> fall back to Unity");
        }

        /// <summary>
        /// THE CHECK THAT MATTERS. Each direction bit must pair with the opposite bit on the
        /// neighbour it points at. If the direction-to-offset mapping had a sign error - most
        /// easily on north, since Daggerfall map pixel Y grows southward - reciprocity would
        /// collapse and every route would run the wrong way. Verified against the real data
        /// rather than assumed from reading it.
        /// </summary>
        static void TestRoadDirectionReciprocity()
        {
            if (!MobileRoadNetwork.Available)
                return;

            byte[] bits = { MobileRoadNetwork.N, MobileRoadNetwork.NE, MobileRoadNetwork.E,
                            MobileRoadNetwork.SE, MobileRoadNetwork.S, MobileRoadNetwork.SW,
                            MobileRoadNetwork.W, MobileRoadNetwork.NW };
            byte[] opposite = { MobileRoadNetwork.S, MobileRoadNetwork.SW, MobileRoadNetwork.W,
                                MobileRoadNetwork.NW, MobileRoadNetwork.N, MobileRoadNetwork.NE,
                                MobileRoadNetwork.E, MobileRoadNetwork.SE };
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

            int checked_ = 0, reciprocal = 0;

            for (int y = 1; y < MobileRoadNetwork.Height - 1 && checked_ < 4000; y++)
            {
                for (int x = 1; x < MobileRoadNetwork.Width - 1 && checked_ < 4000; x++)
                {
                    byte here = MobileRoadNetwork.PathsAt(x, y);
                    if (here == 0)
                        continue;

                    for (int d = 0; d < 8; d++)
                    {
                        if ((here & bits[d]) == 0)
                            continue;

                        checked_++;
                        byte there = MobileRoadNetwork.PathsAt(x + dx[d], y + dy[d]);
                        if ((there & opposite[d]) != 0)
                            reciprocal++;
                    }
                }
            }

            Check(checked_ > 500, "roads: found enough connections to test",
                  "connections examined: " + checked_);

            float ratio = checked_ > 0 ? (float)reciprocal / checked_ : 0f;
            Check(ratio > 0.9f, "roads: direction offsets agree with the data (reciprocity)",
                  string.Format("{0:P1} of {1} connections were reciprocal - a low value means " +
                                "the direction-to-offset mapping is wrong", ratio, checked_));
        }

        /// <summary>
        /// A route must be walkable: every step adjacent to the last, and every step actually
        /// carrying the path bit that permits it. A route that teleports or crosses open
        /// country would walk the player through terrain with no road under them.
        /// </summary>
        static void TestRoadRouting()
        {
            if (!MobileRoadNetwork.Available)
                return;

            // Find a start on the network, and a target far enough to be a real search.
            DFPosition start = null, target = null;
            for (int y = 20; y < MobileRoadNetwork.Height - 20 && start == null; y += 7)
                for (int x = 20; x < MobileRoadNetwork.Width - 20 && start == null; x += 7)
                    if (MobileRoadNetwork.HasAnyPath(x, y))
                        start = new DFPosition(x, y);

            if (start == null)
            {
                Check(false, "roads: found a starting pixel on the network");
                return;
            }

            for (int r = 6; r <= 40 && target == null; r += 2)
            {
                for (int d = 0; d < 8 && target == null; d++)
                {
                    int[] ox = { 0, 1, 1, 1, 0, -1, -1, -1 };
                    int[] oy = { -1, -1, 0, 1, 1, 1, 0, -1 };
                    int tx = start.X + ox[d] * r, ty = start.Y + oy[d] * r;
                    if (MobileRoadNetwork.InBounds(tx, ty) && MobileRoadNetwork.HasAnyPath(tx, ty))
                        target = new DFPosition(tx, ty);
                }
            }

            Check(target != null, "roads: found a distant pixel on the network to route to");
            if (target == null)
                return;

            System.Collections.Generic.List<DFPosition> route =
                MobileRoadNetwork.FindRoute(start.X, start.Y, target.X, target.Y);

            // No route between two arbitrary network pixels is a legitimate outcome - the
            // network is not fully connected - so absence is not a failure. What must never
            // happen is a route that is not walkable.
            if (route == null)
            {
                Check(true, "roads: unconnected pair correctly reports no route");
                return;
            }

            Check(route.Count > 0, "roads: route is non-empty");
            Check(route[route.Count - 1].X == target.X && route[route.Count - 1].Y == target.Y,
                  "roads: route ends at the destination");

            bool contiguous = true, onNetwork = true;
            DFPosition prev = start;
            foreach (DFPosition step in route)
            {
                int sx = step.X - prev.X, sy = step.Y - prev.Y;
                if (Mathf.Abs(sx) > 1 || Mathf.Abs(sy) > 1 || (sx == 0 && sy == 0))
                    contiguous = false;
                if (!MobileRoadNetwork.HasAnyPath(step.X, step.Y))
                    onNetwork = false;
                prev = step;
            }

            Check(contiguous, "roads: every step is adjacent to the last (no teleports)");
            Check(onNetwork, "roads: every step is on the network (no open country)");

            Check(MobileRoadNetwork.FindRoute(start.X, start.Y, start.X, start.Y).Count == 0,
                  "roads: routing to where you already are is an empty route");
        }


        /// <summary>
        /// A waypoint must not be steppable-over. Its own rect is 512 world units where a map
        /// pixel is 32768, so at high time compression a single frame covers far more than the
        /// rect - and a fixed arrival radius would be passed straight through, leaving the
        /// journey steering at a waypoint behind it indefinitely.
        /// </summary>
        static void TestWaypointOvershoot()
        {
            // Standing still or walking: the waypoint's own size governs.
            float still = MobileJourneyPilot.WaypointRadius(0f);
            Check(still > 0f, "waypoint: radius is positive when stationary");
            Near(MobileJourneyPilot.WaypointRadius(10f), still, 0.01f,
                 "waypoint: slow movement does not shrink the radius");

            // Fast: the radius must exceed the distance covered, or the waypoint is skipped.
            float[] speeds = { 500f, 2000f, 20000f, 200000f };
            bool alwaysCatchable = true;
            foreach (float perFrame in speeds)
            {
                if (MobileJourneyPilot.WaypointRadius(perFrame) <= perFrame)
                    alwaysCatchable = false;
            }
            Check(alwaysCatchable,
                  "waypoint: radius always exceeds one frame of travel, at any speed");

            // Monotonic - faster must never mean a smaller catch radius.
            bool monotonic = MobileJourneyPilot.WaypointRadius(100f) <=
                             MobileJourneyPilot.WaypointRadius(1000f) &&
                             MobileJourneyPilot.WaypointRadius(1000f) <=
                             MobileJourneyPilot.WaypointRadius(10000f);
            Check(monotonic, "waypoint: radius grows with speed");
        }

        #endregion




    }
}
