// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Headless verification of the touch layer's pure logic.
//
//   Menu: Tools > Daggerfall Mobile > Run Self Test
//   CLI:  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll
//
// Run with "-batchmode -quit" but NOT with -nographics: the mod extractor tests decode
// compressed bundle textures through a GPU blit, which needs a real graphics device.
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

using System;
using DaggerfallWorkshop.Game.Mobile;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using DaggerfallWorkshop.Utility.AssetInjection;
using System.Collections.Generic;
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
            TestPassThroughGeometry();
            TestRoadData();
            TestRoadsInstallSurvivesSceneSwap();
            TestModsSwitchOwnsBothPrefs();
            TestModBundleRoundTrip();
            TestModScriptSkipRule();
            TestNormalReconstructRule();
            TestWavEncoderRule();
            TestConvertedModImportPolicy();
            TestModExtractorRoundTrip();
            TestModExtractorPathContainment();
            TestModExtractorSurvivesBadPaths();
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
            Check(MobileInput.ResolveSwingMode(1, true, false, false, false) == 0, "touch play imposes hold-and-drag");
            Check(MobileInput.ResolveSwingMode(1, false, false, false, false) == 1, "click-to-attack off: mouse/pad keep the launcher's click mode");
            Check(MobileInput.ResolveSwingMode(1, true, true, false, false) == 1, "window open -> player's own value, so saves keep it");
            Check(MobileInput.ResolveSwingMode(0, false, false, false, false) == 0, "vanilla stays vanilla");

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
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false, float.MaxValue, 0f), "direct touch, no button -> finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Stylus, false, float.MaxValue, 0f), "pencil counts as a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Indirect, false, float.MaxValue, 0f), "indirect touch -> not a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, true, float.MaxValue, 0f), "touch while a pointer button is held -> pointer click, not a finger");
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
            Check(MobileJourneyController.LoadPreferredCompression(TransportModes.Foot) >= 1 &&
                  MobileJourneyController.LoadPreferredCompression(TransportModes.Foot) <= 50,
                  "tiers: the remembered foot speed is within 1x..50x");
            Check(MobileJourneyController.LoadPreferredCompression(TransportModes.Horse) <= 150,
                  "tiers: the remembered horse speed never exceeds 150x");
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

        /// <summary>
        /// Crossing a settlement: the exit point must be on the far side of its footprint along
        /// the bearing, plus the margin - and never behind the player.
        /// </summary>
        static void TestPassThroughGeometry()
        {
            Rect town = new Rect(1000f, 1000f, 2000f, 2000f);      // x 1000..3000, y 1000..3000

            // Heading north (yaw 0) from the south edge: leave through y = 3000.
            Vector2 e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(2000f, 1000f), 0f, 100f);
            Near(e.x, 2000f, 0.5f, "pass-through: north exit keeps x");
            Near(e.y, 3100f, 0.5f, "pass-through: north exit is the far edge plus margin");

            // Heading east (yaw 90) from inside: leave through x = 3000.
            e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(1500f, 2000f), 90f, 50f);
            Near(e.x, 3050f, 0.5f, "pass-through: east exit is the far edge plus margin");
            Near(e.y, 2000f, 0.5f, "pass-through: east exit keeps y");

            // Already past it, heading away: just the margin ahead.
            e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(2000f, 3500f), 0f, 100f);
            Near(e.y, 3600f, 0.5f, "pass-through: beyond the town, a short hop forward");

            Check(Mathf.Abs(Mathf.DeltaAngle(MobileJourneyPilot.TurnToward(10f, 350f, 5f), 5f)) < 0.01f,
                  "steering: turns the short way round and no faster than the step");
            Check(Mathf.Abs(Mathf.DeltaAngle(MobileJourneyPilot.TurnToward(10f, 20f, 90f), 20f)) < 0.01f,
                  "steering: a big step reaches the target");
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
            bool savedPref = MobileMods.Roads;
            try
            {
                MobileMods.Roads = true;
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

                MobileMods.Roads = false;
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                MobileRoads.InstallOnLiveInstance();
                Check(!MobileRoads.Active, "roads: not installed while the preference is off");
            }
            finally
            {
                MobileMods.Roads = savedPref;
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

            Vector2 big = MobilePointer.ClampDelta(new Vector2(3000f, -4000f), 250f);
            Check(Mathf.Abs(big.magnitude - 250f) < 0.01f, "delta clamp: a lock-transition spike is capped");
            Check(Mathf.Abs(big.x / big.y - 3000f / -4000f) < 0.001f, "delta clamp: direction preserved");
            Check(MobilePointer.ClampDelta(new Vector2(3f, 4f), 250f) == new Vector2(3f, 4f),
                  "delta clamp: ordinary movement untouched");

            Check(MobileInput.SecondTapConfirms(true, true, 4, 4, 0.5f, 0.3f),
                  "second tap: slow re-tap on the same row confirms");
            Check(!MobileInput.SecondTapConfirms(true, true, 4, 4, 0.2f, 0.3f),
                  "second tap: a fast pair is the engine's double-click, not ours");
            Check(!MobileInput.SecondTapConfirms(true, true, 5, 4, 0.5f, 0.3f),
                  "second tap: a different row only selects");
            Check(!MobileInput.SecondTapConfirms(false, true, 4, 4, 0.5f, 0.3f),
                  "second tap: keyboard/programmatic selection never confirms");
            Check(!MobileInput.SecondTapConfirms(true, true, -1, -1, 0.5f, 0.3f),
                  "second tap: empty selection never confirms");

            int open = 0;
            for (uint h = 0; h < 1000u; h++)
                if (MobileJourneyController.CautiousEncounterGateOpen(h, 25)) open++;
            Check(open > 150 && open < 350,
                  "encounter gate: ~25% of hours are open (got " + open + "/1000)");
            Check(MobileJourneyController.CautiousEncounterGateOpen(7u, 25) ==
                  MobileJourneyController.CautiousEncounterGateOpen(7u, 25),
                  "encounter gate: deterministic for the same hour");
            bool anyOpen0 = false, allOpen100 = true;
            for (uint h = 0; h < 200u; h++)
            {
                anyOpen0 |= MobileJourneyController.CautiousEncounterGateOpen(h, 0);
                allOpen100 &= MobileJourneyController.CautiousEncounterGateOpen(h, 100);
            }
            Check(!anyOpen0, "encounter gate: 0% never opens");
            Check(allOpen100, "encounter gate: 100% always open");

            // Fresh install: both built-in mods must start OFF (release requirement,
            // 2026-08-31). With no pref keys and no ModManager, the flags fall back to
            // their shipped defaults - which must be false.
            bool hadRoads = PlayerPrefs.HasKey("DFMobile.mod.roads");
            bool hadTravel = PlayerPrefs.HasKey("DFMobile.journeymode");
            int savedRoadsPref = PlayerPrefs.GetInt("DFMobile.mod.roads", 0);
            int savedTravelPref = PlayerPrefs.GetInt("DFMobile.journeymode", 0);
            try
            {
                PlayerPrefs.DeleteKey("DFMobile.mod.roads");
                PlayerPrefs.DeleteKey("DFMobile.journeymode");
                Check(!MobileMods.Roads, "fresh install: Roads & tracks starts off");
                Check(!MobileMods.RealTravel, "fresh install: Real travel starts off");
            }
            finally
            {
                if (hadRoads) PlayerPrefs.SetInt("DFMobile.mod.roads", savedRoadsPref);
                if (hadTravel) PlayerPrefs.SetInt("DFMobile.journeymode", savedTravelPref);
            }
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

        /// <summary>Two switches since the 2026-08-30 split: each drives only its own flag.</summary>
        static void TestModsSwitchOwnsBothPrefs()
        {
            bool savedRoads = MobileMods.Roads;
            bool savedTravel = MobileMods.RealTravel;
            try
            {
                MobileMods.Roads = true;
                MobileMods.RealTravel = false;
                Check(MobileRoads.Enabled && !MobileJourneyController.JourneyModeEnabled,
                      "mods: roads alone - scenery without the journey system");
                MobileMods.Roads = false;
                MobileMods.RealTravel = true;
                Check(!MobileRoads.Enabled && MobileJourneyController.JourneyModeEnabled,
                      "mods: travel alone - journeys follow road data invisibly");
                MobileJourneyController.JourneyModeEnabled = false;      // a stale flag
                MobileMods.ApplySaved();
                Check(MobileJourneyController.JourneyModeEnabled, "mods: ApplySaved re-asserts the saved choice");
            }
            finally
            {
                MobileMods.Roads = savedRoads;
                MobileMods.RealTravel = savedTravel;
            }
        }

        /// <summary>
        /// The whole iOS mod pipeline in one pass: pack the pilot manifest into a .dfmod,
        /// load it back, and look up the replacement exactly the way ModManager does at
        /// runtime. Also pins the refusal of script mods - iOS is IL2CPP, no JIT.
        /// </summary>
        static void TestModBundleRoundTrip()
        {
            const string manifest = "Assets/Game/Mods/IOSPilot/ios-pilot.dfmod.json";
            const string outRoot = "Temp/MobileModBuilderTest";
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, true);

            // Import settings must be normalized (NPOT scaling would silently resize).
            AssetDatabase.ImportAsset("Assets/Game/Mods/IOSPilot/PICK03I0.IMG.png",
                ImportAssetOptions.ForceUpdate);

            string[] built = MobileModBuilder.BuildMod(manifest, outRoot,
                new[] { BuildTarget.StandaloneOSX });
            Check(built.Length == 1 && File.Exists(built[0]),
                  "builder produces a .dfmod", built.Length > 0 ? built[0] : "no output");

            AssetBundle ab = AssetBundle.LoadFromFile(built[0]);
            Check(ab != null, "built bundle loads in the editor");
            if (ab != null)
            {
                bool hasManifest = false;
                foreach (string n in ab.GetAllAssetNames())
                    if (n.EndsWith(".dfmod.json")) hasManifest = true;
                Check(hasManifest, "bundle carries its manifest (Mod ctor requires it)");

                // Exactly the lookup ModManager.TryGetAsset does at runtime.
                Check(ab.Contains("PICK03I0.IMG"), "bundle answers to the runtime texture name");
                var tex = ab.LoadAsset<Texture2D>("PICK03I0.IMG");
                Check(tex != null && tex.width == 320 && tex.height == 200,
                      "replacement texture loads at 320x200",
                      tex ? tex.width + "x" + tex.height : "null");
                ab.Unload(true);
            }

            // Script mods must be refused loudly, not built silently.
            Directory.CreateDirectory(outRoot);
            string scriptManifest = Path.Combine(outRoot, "script-mod.dfmod.json");
            File.WriteAllText(scriptManifest,
                "{\"ModTitle\":\"Script Mod\",\"GUID\":\"test-script-mod\"," +
                "\"Files\":[\"Assets/Fake/Thing.cs\"]}");
            bool refused = false;
            try { MobileModBuilder.BuildMod(scriptManifest, outRoot, new[] { BuildTarget.StandaloneOSX }); }
            catch (System.NotSupportedException) { refused = true; }
            Check(refused, "builder refuses script mods (no JIT on iOS)");

            Directory.Delete(outRoot, true);
        }

        /// <summary>
        /// The engine-side half of the same rule: iOS runs IL2CPP, so mod scripts can be
        /// neither compiled from source nor Assembly.Load-ed. The guard must fire only for
        /// mods that actually carry sources, leaving asset-only mods completely alone.
        /// </summary>
        static void TestModScriptSkipRule()
        {
            // iOS runs IL2CPP: no JIT, so mod scripts can be neither compiled nor loaded.
            // Asset-only mods (sources == 0) must be untouched by the guard.
            Check(!Mod.ShouldSkipScriptCompilation(0, true), "asset-only mod, JIT: no skip");
            Check(!Mod.ShouldSkipScriptCompilation(0, false), "asset-only mod, no JIT: no skip");
            Check(!Mod.ShouldSkipScriptCompilation(2, true), "script mod, JIT: compiles");
            Check(Mod.ShouldSkipScriptCompilation(2, false), "script mod, no JIT: skips");
            Check(Mod.RuntimeScriptsSupported, "editor/desktop supports mod scripts");
        }

        /// <summary>
        /// Normal maps do not survive a naive extraction. A compressed normal map does not
        /// store its blue channel at all: DXT5nm keeps x in alpha and y in green (RGB are
        /// thrown away), BC5 keeps x and y in red and green. Writing those bytes straight
        /// out yields a PNG that looks like a normal map to no one - the same family of
        /// silent corruption as a blank texture, but harder to see. z has to be rebuilt from
        /// x and y, which is possible because a tangent-space normal is a unit vector.
        /// </summary>
        static void TestNormalReconstructRule()
        {
            // Flat up-normal (0,0,1): x=y=0 -> encoded 128,128,255.
            var flat = MobileModExtractor.ReconstructNormalPixel(new Color32(255, 128, 0, 128), true);
            Check(flat.r == 128 && flat.g == 128 && flat.b >= 254, "DXTnm flat normal reconstructs (x from alpha)");
            var flatBc5 = MobileModExtractor.ReconstructNormalPixel(new Color32(128, 128, 0, 255), false);
            Check(flatBc5.r == 128 && flatBc5.g == 128 && flatBc5.b >= 254, "BC5 flat normal reconstructs (x from red)");
            // Fully tilted +x: x=1, y=0 -> z=0. Zero is the MIDDLE of the encoding, not the
            // bottom of it: every channel of a tangent-space normal map is stored as
            // (n * 0.5 + 0.5), which is what Unity's UnpackNormal undoes, so a collapsed z
            // encodes to 128 and not to 0.
            var tilt = MobileModExtractor.ReconstructNormalPixel(new Color32(0, 128, 0, 255), true);
            Check(tilt.r == 255 && tilt.b == 128, "tilted normal keeps x, z collapses to encoded zero",
                  "r=" + tilt.r + " b=" + tilt.b);
            // A vector that is not unit length must not produce NaN or wrap around: x=y=1
            // gives 1-x*x-y*y = -1, and Mathf.Sqrt of a negative is NaN, which casts to a
            // garbage byte. The max(0,..) clamp is what stops a corrupt source pixel from
            // becoming a corrupt output pixel.
            var over = MobileModExtractor.ReconstructNormalPixel(new Color32(255, 255, 0, 255), false);
            Check(over.r == 255 && over.g == 255 && over.b == 128 && over.a == 255,
                  "over-unit x,y clamps to z=0 instead of NaN", "b=" + over.b);
            // Alpha is always opaque: the extracted png is a data texture, and a 0 alpha
            // would let a later importer treat it as transparent.
            Check(flat.a == 255 && flatBc5.a == 255 && tilt.a == 255, "reconstructed normal is opaque");

            // Which textures get this treatment is decided by name alone - a bundle texture
            // records nothing about the importer settings it was built with - so the naming
            // rule is the whole of the classification and both the extractor and the converted
            // -mod import policy read it from here. DFU appends "_" + the TextureMap enum name
            // (TextureReplacement.GetName), and its own IsLinearTextureMap calls exactly
            // Normal, Height and MetallicGloss linear: Emission and Mask are colour, and
            // forcing those linear would regrade them as badly as leaving a normal in sRGB.
            const string dfuName = "Assets/Textures/004_0-0";
            Check(MobileModExtractor.IsNormalMapName(dfuName + "_Normal.png"), "DFU _Normal suffix is a normal map");
            Check(MobileModExtractor.IsNormalMapName(dfuName + "_normal.PNG"), "suffix match ignores case");
            Check(!MobileModExtractor.IsNormalMapName(dfuName + ".png"), "an albedo is not a normal map");
            Check(!MobileModExtractor.IsNormalMapName("Assets/Textures/wallNormal.png"),
                  "the underscore is required: 'wallNormal' is not a map suffix");
            Check(!MobileModExtractor.IsNormalMapName(dfuName + "_Height.png"), "a height map is not a normal map");
            Check(MobileModExtractor.IsLinearMapName(dfuName + "_Normal.png")
                  && MobileModExtractor.IsLinearMapName(dfuName + "_Height.png")
                  && MobileModExtractor.IsLinearMapName(dfuName + "_MetallicGloss.png"),
                  "normal, height and metallic/gloss are linear (as in DFU's IsLinearTextureMap)");
            Check(!MobileModExtractor.IsLinearMapName(dfuName + ".png")
                  && !MobileModExtractor.IsLinearMapName(dfuName + "_Emission.png")
                  && !MobileModExtractor.IsLinearMapName(dfuName + "_Mask.png"),
                  "albedo, emission and mask stay sRGB colour");
        }

        /// <summary>
        /// The WAV container the extractor writes, as a pure function. A bundle's AudioClip is
        /// float samples in memory and nothing else - whatever the author imported is gone - so
        /// the extraction has to build a file format from scratch, and a header that is wrong by
        /// one field produces a file every tool refuses or, worse, one that decodes at the wrong
        /// rate. The layout is the standard 44-byte canonical RIFF/WAVE: "RIFF", size-8, "WAVE",
        /// "fmt " with 16 bytes of PCM fields, then "data" with the payload size.
        /// </summary>
        static void TestWavEncoderRule()
        {
            // The brief's shape check: four mono samples at 8kHz is 44 header bytes + 8 payload,
            // and +1.0 is the top of the 16-bit range.
            byte[] wav = MobileModExtractor.EncodeWav(new float[] { 0f, 1f, -1f, 0f }, 1, 8000);
            Check(wav.Length == 44 + 8 && wav[0] == (byte)'R'
                  && BitConverter.ToInt16(wav, 46) == short.MaxValue,
                  "EncodeWav writes 16-bit PCM with RIFF header", "len=" + wav.Length);

            // Every field of the header, by offset. These are what a decoder actually reads;
            // a plausible-looking file with byteRate or blockAlign wrong plays at the wrong
            // speed rather than failing loudly, which is the failure mode worth pinning.
            Check(System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF"
                  && System.Text.Encoding.ASCII.GetString(wav, 8, 4) == "WAVE"
                  && System.Text.Encoding.ASCII.GetString(wav, 12, 4) == "fmt "
                  && System.Text.Encoding.ASCII.GetString(wav, 36, 4) == "data",
                  "canonical chunk ids at the canonical offsets");
            Check(BitConverter.ToInt32(wav, 4) == wav.Length - 8
                  && BitConverter.ToInt32(wav, 40) == 8,
                  "RIFF size is the file minus 8; data size is the payload",
                  "riff=" + BitConverter.ToInt32(wav, 4) + " data=" + BitConverter.ToInt32(wav, 40));
            Check(BitConverter.ToInt32(wav, 16) == 16 && BitConverter.ToInt16(wav, 20) == 1
                  && BitConverter.ToInt16(wav, 34) == 16,
                  "fmt chunk is 16 bytes, format tag 1 (PCM), 16 bits per sample");
            Check(BitConverter.ToInt16(wav, 22) == 1 && BitConverter.ToInt32(wav, 24) == 8000
                  && BitConverter.ToInt32(wav, 28) == 8000 * 1 * 2
                  && BitConverter.ToInt16(wav, 32) == 1 * 2,
                  "mono 8kHz: byteRate = freq*channels*2, blockAlign = channels*2",
                  "byteRate=" + BitConverter.ToInt32(wav, 28) + " align=" + BitConverter.ToInt16(wav, 32));

            // Stereo changes two derived fields and nothing else; getting them from the channel
            // count rather than assuming mono is the difference between a stereo song and a
            // stereo song played at half speed.
            byte[] st = MobileModExtractor.EncodeWav(new float[] { 0f, 0f, 0f, 0f }, 2, 44100);
            Check(BitConverter.ToInt16(st, 22) == 2 && BitConverter.ToInt32(st, 28) == 44100 * 2 * 2
                  && BitConverter.ToInt16(st, 32) == 4 && st.Length == 44 + 8,
                  "stereo derives byteRate and blockAlign from the channel count",
                  "byteRate=" + BitConverter.ToInt32(st, 28) + " align=" + BitConverter.ToInt16(st, 32));

            // Clamping is not decoration. AudioClip.GetData can hand back samples outside
            // [-1,1] - a mod mastered hot, or any DSP that overshot - and the naive cast wraps:
            // 1.5*32767 is 49150, which truncates to -16386 and turns a loud peak into a loud
            // click of the opposite sign. Clamp, do not wrap.
            byte[] hot = MobileModExtractor.EncodeWav(new float[] { 1.5f, -1.5f, float.NaN }, 1, 8000);
            Check(BitConverter.ToInt16(hot, 44) == short.MaxValue
                  && BitConverter.ToInt16(hot, 46) == -short.MaxValue,
                  "samples past full scale clamp instead of wrapping round",
                  "hi=" + BitConverter.ToInt16(hot, 44) + " lo=" + BitConverter.ToInt16(hot, 46));
            Check(BitConverter.ToInt16(hot, 48) == 0, "a NaN sample becomes silence, not a garbage byte",
                  "nan=" + BitConverter.ToInt16(hot, 48));

            // An empty clip is a legal one: header only, and no decoder is asked to read past it.
            byte[] empty = MobileModExtractor.EncodeWav(new float[0], 1, 22050);
            Check(empty.Length == 44 && BitConverter.ToInt32(empty, 40) == 0,
                  "an empty clip still produces a valid header-only file", "len=" + empty.Length);
        }

        /// <summary>
        /// The converted-mod import policy, as pure rules. This is the memory-critical part of
        /// the pipeline: against 1.72GB of DREAM textures plus ~3.7GB of sprite modules on an
        /// 8GB iPad, the size cap, the mipmap decision and the ASTC block size are the three
        /// numbers that decide whether the pack loads or iOS kills the app. They are read from
        /// environment variables so they can be tuned against a device without a recompile,
        /// which means the PARSING is now part of the policy: an operator typo that silently
        /// fell back to "whatever the platform picks" would undo the whole point of naming them.
        /// </summary>
        static void TestConvertedModImportPolicy()
        {
            // Defaults, stated as assertions so a change to one is a deliberate act.
            Check(MobileConvertedModPolicy.DefaultMaxTextureSize == 1024,
                  "default cap is 1024, not Unity's never-downscale 2048",
                  "" + MobileConvertedModPolicy.DefaultMaxTextureSize);
            Check(MobileConvertedModPolicy.ParseAstcBlock(
                      MobileConvertedModPolicy.DefaultAstcBlock, "6x6")
                  == TextureImporterFormat.ASTC_6x6, "default iOS block is ASTC 6x6 (3.56 bpp)");

            // Sizes. Unity accepts powers of two from 32 to 16384 and nothing else, so a typo
            // must fall back loudly rather than become the policy.
            Check(MobileConvertedModPolicy.ParseSize("2048", 1024) == 2048, "a valid cap is honoured");
            Check(MobileConvertedModPolicy.ParseSize(" 512 ", 1024) == 512, "whitespace is tolerated");
            Check(MobileConvertedModPolicy.ParseSize(null, 1024) == 1024, "unset keeps the default");
            Check(MobileConvertedModPolicy.ParseSize("", 1024) == 1024, "empty keeps the default");
            Check(MobileConvertedModPolicy.ParseSize("1000", 1024) == 1024, "a non-power-of-two is refused");
            Check(MobileConvertedModPolicy.ParseSize("16", 1024) == 1024, "an absurdly small cap is refused");
            Check(MobileConvertedModPolicy.ParseSize("banana", 1024) == 1024, "garbage is refused");

            // Booleans, in the spellings a shell user actually types.
            Check(MobileConvertedModPolicy.ParseBool("1", false)
                  && MobileConvertedModPolicy.ParseBool("true", false)
                  && MobileConvertedModPolicy.ParseBool("ON", false), "1/true/on are true");
            Check(!MobileConvertedModPolicy.ParseBool("0", true)
                  && !MobileConvertedModPolicy.ParseBool("no", true)
                  && !MobileConvertedModPolicy.ParseBool("Off", true), "0/no/off are false");
            Check(MobileConvertedModPolicy.ParseBool(null, true)
                  && !MobileConvertedModPolicy.ParseBool("maybe", false), "unset and garbage keep the default");

            // ASTC block sizes: the bytes-per-pixel lever.
            Check(MobileConvertedModPolicy.ParseAstcBlock("4x4", "6x6") == TextureImporterFormat.ASTC_4x4
                  && MobileConvertedModPolicy.ParseAstcBlock("8x8", "6x6") == TextureImporterFormat.ASTC_8x8
                  && MobileConvertedModPolicy.ParseAstcBlock("12x12", "6x6") == TextureImporterFormat.ASTC_12x12,
                  "every block size Unity defines is reachable");
            Check(MobileConvertedModPolicy.ParseAstcBlock("7x7", "6x6") == TextureImporterFormat.ASTC_6x6,
                  "a block size Unity does not define falls back rather than guessing");
            Check(MobileConvertedModPolicy.ParseQuality("100", 50) == 100
                  && MobileConvertedModPolicy.ParseQuality("-1", 50) == 50
                  && MobileConvertedModPolicy.ParseQuality("101", 50) == 50,
                  "compressor quality is clamped to 0-100 or refused");

            // The mipmap rule. Mipmaps cost 33% resident across the whole pack, and 2D art
            // drawn at 1:1 never samples them. Which assets those are is derived from DFU's own
            // conventions, not invented: TextureReplacement serves IMG images and CIF/RCI
            // images (paperdolls, portraits, weapon animations, UI) - and a MOD can only serve
            // them under a short name carrying the original .IMG/.CIF/.RCI filename, because
            // that name is the runtime lookup key (TryImportImage/TryImportCifRci ->
            // ModManager.TryGetAsset). So the name is a real signal even though a bundled mod's
            // internal directory layout is the author's own business.
            string[] markers = MobileConvertedModPolicy.DefaultNoMipMarkers;
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/004_0-0.png", markers),
                  "a world texture is minified in use and keeps mipmaps");
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/210_1-0_Normal.png", markers),
                  "a billboard's normal map keeps mipmaps too");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/UI/BOOK00I0.IMG.png", markers),
                  "an IMG image is drawn 1:1 and gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Art/TFAC00I0.RCI_0-0.png", markers),
                  "a paperdoll/portrait RCI record gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Art/WEAPON01.CIF_3-2.png", markers),
                  "a CIF weapon frame gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/CifRci/anything.png", markers),
                  "DFU's own CifRci directory is recognised as well as the name");
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/Images/004_0-0.png", markers),
                  "a folder merely called Images is not the .img marker");
            // The rule is overridable wholesale, because the real pack's internal paths have not
            // been inspected and a silently wrong guess is exactly what must not ship.
            string[] custom = MobileConvertedModPolicy.ParseList(" /paperdoll/ , .ui ", markers);
            Check(custom.Length == 2 && custom[0] == "/paperdoll/" && custom[1] == ".ui",
                  "the no-mipmap list is overridable and trimmed");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/x/Paperdoll/a.png", custom)
                  && MobileConvertedModPolicy.ShouldMipmap("Assets/UI/BOOK00I0.IMG.png", custom),
                  "an override replaces the defaults rather than adding to them");
            Check(MobileConvertedModPolicy.ParseList(null, markers) == markers
                  && MobileConvertedModPolicy.ParseList("  ", markers) == markers,
                  "unset or blank keeps DFU's derived defaults");

            // The audio half of the policy: songs stream, effects sit compressed in memory.
            // Both directions cost something real if they are got wrong - a resident song is
            // megabytes the device never gets back, a streamed effect misses the frame it was
            // triggered on - and the streaming side is the only part of this policy that is
            // NOT what Unity would have done anyway, which makes it the part worth a test. It
            // is checked here rather than through a fixture because reaching it needs a file
            // over 2MB, and committing 2MB of silence to prove a comparison is not a trade
            // this repo should make.
            const long mb = 1024 * 1024;
            Check(MobileConvertedModPolicy.LoadTypeForSize(64 * 1024)
                      == AudioClipLoadType.CompressedInMemory,
                  "a sound effect stays compressed in memory, never streamed");
            Check(MobileConvertedModPolicy.LoadTypeForSize(30 * mb) == AudioClipLoadType.Streaming,
                  "a song streams instead of sitting resident");
            // The threshold is read against the extraction's own output, which is always
            // uncompressed 16-bit PCM, so it is a duration rule wearing a size: 2MB is ~12s of
            // mono 22kHz. Both sides of the boundary are pinned so a later "just round it up"
            // cannot quietly move songs into memory.
            Check(MobileConvertedModPolicy.LoadTypeForSize(MobileConvertedModPolicy.StreamingThresholdBytes)
                      == AudioClipLoadType.CompressedInMemory
                  && MobileConvertedModPolicy.LoadTypeForSize(
                      MobileConvertedModPolicy.StreamingThresholdBytes + 1)
                      == AudioClipLoadType.Streaming,
                  "the boundary itself is an effect; one byte past it is a song");
            Check(MobileConvertedModPolicy.StreamingThresholdBytes == 2 * mb
                  && Mathf.Abs(MobileConvertedModPolicy.VorbisQuality - 0.7f) < 0.001f,
                  "the audio policy's two constants are the argued-for ones",
                  MobileConvertedModPolicy.StreamingThresholdBytes + "B q"
                      + MobileConvertedModPolicy.VorbisQuality);
        }

        /// <summary>
        /// The reverse direction of the mod pipeline. Third-party mods (DREAM-class) ship
        /// only as desktop AssetBundles, so iOS support means unpacking one back into loose
        /// project assets and repacking it. This packs a synthetic desktop .dfmod, extracts
        /// it, and rebuilds - what survives the full circle is what a converted mod gets.
        ///
        /// NEEDS A REAL GRAPHICS DEVICE: the bundle texture is compressed and non-readable, so
        /// extracting it goes through a GPU blit. Do not run this suite with -nographics.
        /// </summary>
        static void TestModExtractorRoundTrip()
        {
            const string fixtureManifest = "Assets/Editor/TestFixtures/ExtractorFixture/fixture-mod.dfmod.json";
            const string bundleDir = "Temp/MobileModExtractorTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__test__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }

            // 1. Make a "desktop mod" the way the outside world does: build for StandaloneOSX.
            string[] built = MobileModBuilder.BuildMod(fixtureManifest, bundleDir,
                new[] { BuildTarget.StandaloneOSX });

            // 2. Extract it back.
            var report = MobileModExtractor.Extract(built[0], extractRoot);
            Check(File.Exists(report.manifestPath), "extractor writes a manifest", report.manifestPath);
            Check(report.extracted.Count == 6, "extractor writes four textures + textasset + audio clip",
                  "extracted=" + report.extracted.Count);

            // 3. Path tail and short names preserved.
            string tex = report.extracted.Find(p => p.EndsWith("fixture_tex.png"));
            string txt = report.extracted.Find(p => p.EndsWith("fixture_data.json"));
            string nrm = report.extracted.Find(p => p.EndsWith("fixture_wall_Normal.png"));
            string hgt = report.extracted.Find(p => p.EndsWith("fixture_wall_Height.png"));
            string wav = report.extracted.Find(p => p.EndsWith("fixture_beep.wav"));
            // Mod.FindAssetNames accepts an asset whose directory ENDS WITH the requested one
            // and compares with a case-sensitive CompareOrdinal, while callers pass literal
            // capitalised paths ("Assets/Textures"). AssetBundle.GetAllAssetNames hands back
            // everything lowercased, so the extraction has to recover the manifest's own casing
            // - and keep the leading "Assets/" - or a converted mod silently loses loose-file
            // injection while every other check here still passes.
            Check(tex != null && tex.Replace('\\', '/').Contains(
                      "/Assets/Editor/TestFixtures/ExtractorFixture/fixture_tex.png"),
                  "manifest path casing and Assets/ prefix preserved", tex);
            Check(!report.notesByType.ContainsKey("unlisted-in-manifest"),
                  "every bundle asset matched a manifest entry (casing recoverable)");
            Check(txt != null && File.ReadAllText(txt).Contains("\"value\":42"),
                  "textasset bytes preserved");

            // fixture_tex.tga and fixture_tex.png collapse onto one output path once the texture
            // extension is rewritten. Overwriting would lose an asset and list the survivor twice
            // in the rebuilt manifest, so the clash must be reported instead. Both fixtures carry
            // the same pixels, so which one wins does not change anything else in this test.
            int collisions;
            report.skippedByType.TryGetValue("collision", out collisions);
            Check(collisions == 1, "colliding output path reported, not overwritten",
                  "collision=" + collisions);
            // The two counters answer different questions and the boundary between them is
            // exactly here. fixture_lone.tga has no .png twin: it really is extracted, under a
            // changed runtime lookup name, so it earns the note. fixture_tex.tga is ALSO an
            // extension rewrite, but it loses the collision and never reaches disk - and a note
            // is a claim about a survivor, so it must not be counted. One rewrite, not two: a
            // note banked before the write would report an asset as both rewritten-and-extracted
            // and skipped, in the same run.
            string lone = report.extracted.Find(p => p.EndsWith("fixture_lone.png"));
            Check(lone != null && File.Exists(lone),
                  "a .tga with no .png twin is extracted as .png", lone ?? "missing");
            int rewritten;
            report.notesByType.TryGetValue("extension-rewritten", out rewritten);
            Check(rewritten == 1, "only the rewrite that actually reached disk is noted",
                  "extension-rewritten=" + rewritten);

            // 3b. THE CHECK THAT MATTERS for textures. Everything above passes on a blank
            // image: the name, the path, the size and the manifest are all still right when
            // the pixels are gone. The bundle texture is DXT1 and non-readable, so extraction
            // must go through the GPU blit, and a blit with no graphics device is a silent
            // no-op that yields a uniform grey. Compare against the fixture's generator
            // pattern - pixel (x,y) = (4x, 4y, (x^y)*4) - which only real decoded data matches.
            var decoded = new Texture2D(2, 2);
            bool loaded = tex != null && decoded.LoadImage(File.ReadAllBytes(tex));
            Check(loaded && decoded.width == 64 && decoded.height == 64,
                  "extracted png decodes at 64x64",
                  loaded ? decoded.width + "x" + decoded.height : "did not decode");

            Color32[] px = loaded ? decoded.GetPixels32() : new Color32[0];
            var seen = new HashSet<int>();
            foreach (Color32 c in px)
                seen.Add((c.r << 16) | (c.g << 8) | c.b);
            Check(seen.Count > 100, "extracted texture is not a solid fill",
                  "distinct colours=" + seen.Count + " (1 means the blit produced a flat fill)");

            // DXT1 is lossy, so allow a margin - but one far tighter than a grey wash.
            const int dxtTolerance = 16;
            int[,] samples = { { 0, 0 }, { 17, 42 }, { 32, 32 }, { 63, 63 }, { 20, 40 }, { 5, 58 } };
            int worst = 0;
            string worstAt = "none";
            for (int i = 0; loaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                // GetPixels32 runs bottom-up; the fixture pattern is written top-down.
                Color32 got = px[(63 - y) * 64 + x];
                int dr = Mathf.Abs(got.r - 4 * x % 256);
                int dg = Mathf.Abs(got.g - 4 * y % 256);
                int db = Mathf.Abs(got.b - (x ^ y) * 4 % 256);
                int d = Mathf.Max(dr, Mathf.Max(dg, db));
                if (d > worst) { worst = d; worstAt = string.Format("({0},{1}) got {2},{3},{4} want {5},{6},{7}",
                    x, y, got.r, got.g, got.b, 4 * x % 256, 4 * y % 256, (x ^ y) * 4 % 256); }
            }
            Check(loaded && worst <= dxtTolerance, "extracted pixels match the fixture pattern",
                  "worst channel delta=" + worst + " at " + worstAt);
            UnityEngine.Object.DestroyImmediate(decoded);

            // 3c. THE SAME CHECK FOR NORMAL MAPS, where "the bytes came out" is even further
            // from "the asset survived". Unity does not store a normal map as an image of one:
            // it throws the blue channel away and swizzles what is left into the two channels
            // its block format codes best, so a byte-for-byte extraction produces a white image
            // (DXT5nm) or a blue-less one (BC5) that re-imports as an ordinary colour texture
            // and lights nothing. The fixture is generated from real unit normals, so a correct
            // extraction reproduces all three channels of the source pattern; a wrong one misses
            // blue by ~100. Which swizzle Unity actually used is recorded rather than assumed.
            int unswizzled = 0;
            string layout = "none";
            foreach (var kv in report.notesByType)
                if (kv.Key.StartsWith("normal-")) { layout = kv.Key; unswizzled += kv.Value; }
            // The layout is named in the check itself rather than asserted: which of the two
            // swizzles Unity picks is a build-target and Unity-version decision, and pinning it
            // would make this a test of Unity. What must hold is that exactly one normal map was
            // recognised and classified - the pixel comparison below is what proves the branch
            // chosen was the right one, since the wrong one misses blue by about 100.
            Check(unswizzled == 1, "normal map recognised, layout recorded: " + layout,
                  "notes=" + string.Join(",", new List<string>(report.notesByType.Keys).ToArray()));

            var dn = new Texture2D(2, 2);
            bool nLoaded = nrm != null && dn.LoadImage(File.ReadAllBytes(nrm));
            Check(nLoaded && dn.width == 64 && dn.height == 64, "extracted normal png decodes at 64x64",
                  nLoaded ? dn.width + "x" + dn.height : "did not decode");
            Color32[] npx = nLoaded ? dn.GetPixels32() : new Color32[0];
            int nWorst = 0;
            string nWorstAt = "none";
            for (int i = 0; nLoaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                Color32 got = npx[(63 - y) * 64 + x];       // GetPixels32 is bottom-up
                // The fixture's generator: a unit normal fanning out across the square.
                float fx = ((x / 63f) * 2f - 1f) * 0.5f;
                float fy = ((y / 63f) * 2f - 1f) * 0.5f;
                float fz = Mathf.Sqrt(Mathf.Max(0f, 1f - fx * fx - fy * fy));
                int wr = Mathf.RoundToInt((fx * 0.5f + 0.5f) * 255f);
                int wg = Mathf.RoundToInt((fy * 0.5f + 0.5f) * 255f);
                int wb = Mathf.RoundToInt((fz * 0.5f + 0.5f) * 255f);
                int d = Mathf.Max(Mathf.Abs(got.r - wr),
                        Mathf.Max(Mathf.Abs(got.g - wg), Mathf.Abs(got.b - wb)));
                if (d > nWorst) { nWorst = d; nWorstAt = string.Format("({0},{1}) got {2},{3},{4} want {5},{6},{7}",
                    x, y, got.r, got.g, got.b, wr, wg, wb); }
            }
            Check(nLoaded && nWorst <= 16, "extracted normal map reconstructs x, y AND z",
                  "worst channel delta=" + nWorst + " at " + nWorstAt);
            UnityEngine.Object.DestroyImmediate(dn);

            // 3c-bis. THE DEGAMMA PIN. The blit's gamma behaviour is decided by the SOURCE
            // texture's graphics format, never by what the file is called. fixture_wall_Height
            // is deliberately left sRGB - which is what a mod author who never touched the
            // importer ships, and the real DREAM texture pack does contain *_Height assets - so
            // a converter that picked "linear" from the "_Height" suffix would sample it
            // degamma'd with nothing re-encoding on write. That is not a rounding error: a
            // mid-tone 128 comes out as 55. The fixture is a ramp through every mid-tone, so
            // any sRGB/linear mix-up in either direction lands far outside this tolerance.
            var dh = new Texture2D(2, 2);
            bool hLoaded = hgt != null && dh.LoadImage(File.ReadAllBytes(hgt));
            Check(hLoaded && dh.width == 64 && dh.height == 64, "extracted height png decodes at 64x64",
                  hLoaded ? dh.width + "x" + dh.height : "did not decode");
            Color32[] hpx = hLoaded ? dh.GetPixels32() : new Color32[0];
            int hWorst = 0;
            string hWorstAt = "none";
            for (int i = 0; hLoaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                Color32 got = hpx[(63 - y) * 64 + x];       // GetPixels32 is bottom-up
                int want = ((x + y) * 2) % 256;            // the fixture's generator
                int d = Mathf.Max(Mathf.Abs(got.r - want),
                        Mathf.Max(Mathf.Abs(got.g - want), Mathf.Abs(got.b - want)));
                if (d > hWorst) { hWorst = d; hWorstAt = string.Format("({0},{1}) got {2},{3},{4} want {5}",
                    x, y, got.r, got.g, got.b, want); }
            }
            Check(hLoaded && hWorst <= 16, "sRGB-flagged height map survives without a degamma",
                  "worst channel delta=" + hWorst + " at " + hWorstAt);
            UnityEngine.Object.DestroyImmediate(dh);

            // 3d. The import policy that makes a multi-gigabyte pack fit on the device. The
            // extraction lands under Assets/Game/Mods/Converted/, which MobileConvertedModImporter
            // owns, so the settings below are the postprocessor's doing and not Unity's defaults.
            // A normal map imported as a colour texture is silently wrong in exactly the way this
            // whole test exists to catch, and npotScale is the one asserted setting Unity does NOT
            // default to - it pins that the postprocessor actually ran on the colour texture too.
            var nrmImp = AssetImporter.GetAtPath(nrm) as TextureImporter;
            Check(nrmImp != null && nrmImp.textureType == TextureImporterType.NormalMap,
                  "extracted *_Normal re-imports as a normal map",
                  nrmImp == null ? "no importer" : nrmImp.textureType.ToString());
            var hgtImp = AssetImporter.GetAtPath(hgt) as TextureImporter;
            Check(hgtImp != null && hgtImp.textureType == TextureImporterType.Default
                  && !hgtImp.sRGBTexture,
                  "extracted *_Height re-imports as linear data, not colour",
                  hgtImp == null ? "no importer" : "sRGB=" + hgtImp.sRGBTexture);
            var texImp = AssetImporter.GetAtPath(tex) as TextureImporter;
            Check(texImp != null && texImp.textureType == TextureImporterType.Default
                  && texImp.sRGBTexture,
                  "extracted colour texture stays sRGB colour",
                  texImp == null ? "no importer" : texImp.textureType.ToString());
            // Unity already defaults isReadable false, Compressed and mipmapEnabled true, so
            // those three would pass with the postprocessor deleted - they are regression pins,
            // not proof it ran. The four below are not Unity defaults and cannot pass by
            // accident: npotScale defaults to ToNearest, maxTextureSize to 2048, and a texture
            // has no iOS platform override at all until something writes one.
            Check(texImp != null && !texImp.isReadable
                  && texImp.textureCompression == TextureImporterCompression.Compressed,
                  "converted textures are compressed and keep no CPU-side copy");
            Check(texImp != null && texImp.npotScale == TextureImporterNPOTScale.None,
                  "converted textures keep their exact dimensions (DFU uv metadata depends on it)",
                  texImp == null ? "no importer" : texImp.npotScale.ToString());
            // Against the policy's value, not a literal: this must keep passing when an operator
            // is tuning the cap against a device, which is the whole reason it is an env var.
            // The default itself (1024, below Unity's never-downscale 2048) is pinned in
            // TestConvertedModImportPolicy.
            Check(texImp != null && texImp.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize(),
                  "converted textures take their size cap from the policy",
                  texImp == null ? "no importer" : "max=" + texImp.maxTextureSize
                      + " policy=" + MobileConvertedModPolicy.MaxTextureSize());
            var ios = texImp != null
                ? texImp.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform) : null;
            Check(ios != null && ios.overridden,
                  "converted textures carry an explicit iOS override",
                  ios == null ? "no settings" : "overridden=" + ios.overridden);
            Check(ios != null && ios.format == MobileConvertedModPolicy.IosFormat()
                  && ios.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize()
                  && ios.compressionQuality == MobileConvertedModPolicy.CompressionQuality(),
                  "iOS override names the ASTC block, the cap and the compressor quality",
                  ios == null ? "no settings"
                    : ios.format + " " + ios.maxTextureSize + " q" + ios.compressionQuality);
            // World textures ARE minified, so this one keeps its mipmaps; the 2D-art rule is
            // exercised as a pure function in TestConvertedModImportPolicy, because no fixture
            // path here can stand in for a real paperdoll's.
            Check(texImp != null && texImp.mipmapEnabled,
                  "a world texture keeps its mipmaps");
            Check(texImp != null && !texImp.streamingMipmaps,
                  "mipmap streaming stays off (QualitySettings has it disabled project-wide)");
            // The extraction root is deleted at the end of this test, so its .meta files never
            // survive to be inspected by hand. Record what the policy actually produced.
            if (texImp != null && nrmImp != null && hgtImp != null && ios != null)
                Debug.Log(string.Format("[MobileSelfTest] converted-mod import policy produced: " +
                    "colour type={0} readable={1} compression={2} mips={3} stream={4} npot={5} " +
                    "sRGB={6} max={7}; iOS override={8} fmt={9} max={10} q={11}; " +
                    "normal type={12} sRGB={13}; height type={14} sRGB={15}",
                    texImp.textureType, texImp.isReadable, texImp.textureCompression,
                    texImp.mipmapEnabled, texImp.streamingMipmaps, texImp.npotScale,
                    texImp.sRGBTexture, texImp.maxTextureSize,
                    ios.overridden, ios.format, ios.maxTextureSize, ios.compressionQuality,
                    nrmImp.textureType, nrmImp.sRGBTexture,
                    hgtImp.textureType, hgtImp.sRGBTexture));

            // 3e. AUDIO. A bundle holds an AudioClip as decoded float samples and nothing
            // else - the author's .wav/.ogg source is not in there - so extraction means
            // re-authoring a container around the samples. The header checks below are the
            // cheap half; the tone check after them is the half that matters, because a WAV of
            // pure silence has a perfectly correct header, the right length and the right name.
            Check(wav != null && File.Exists(wav), "audio clip extracted as .wav", wav ?? "missing");
            byte[] wavBytes = wav != null ? File.ReadAllBytes(wav) : new byte[0];
            Check(wavBytes.Length > 44
                  && wavBytes[0] == (byte)'R' && wavBytes[1] == (byte)'I'
                  && wavBytes[2] == (byte)'F' && wavBytes[3] == (byte)'F',
                  "extracted audio is a RIFF file with a payload", "bytes=" + wavBytes.Length);

            var clip = wav != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(wav) : null;
            Check(clip != null && clip.frequency == 22050 && clip.channels == 1,
                  "extracted clip re-imports at the fixture's rate and channel count",
                  clip == null ? "no clip" : clip.frequency + "Hz x" + clip.channels);
            Check(clip != null && clip.length > 0.24f && clip.length < 0.26f,
                  "extracted clip is the fixture's 0.25s",
                  clip == null ? "no clip" : clip.length.ToString("F4") + "s");

            // THE CHECK THAT MATTERS for audio. Correlate the written PCM against the fixture's
            // own 440Hz generator (sin and cos, so phase does not matter) and against a decoy
            // frequency that is not in the fixture at all. Silence, a DC offset, a half-rate
            // header or samples that wrapped instead of clamping all leave the 440Hz magnitude
            // far from the fixture's 0.8 amplitude; only real, correctly-rated audio lands on it.
            double sin440 = 0, cos440 = 0, sinDecoy = 0, cosDecoy = 0, peak = 0;
            int frames = Mathf.Max(0, (wavBytes.Length - 44) / 2);
            for (int i = 0; i < frames; i++)
            {
                double v = BitConverter.ToInt16(wavBytes, 44 + i * 2) / 32767.0;
                if (Math.Abs(v) > peak) peak = Math.Abs(v);
                double t = i / 22050.0;
                sin440 += v * Math.Sin(2 * Math.PI * 440 * t);
                cos440 += v * Math.Cos(2 * Math.PI * 440 * t);
                sinDecoy += v * Math.Sin(2 * Math.PI * 1300 * t);
                cosDecoy += v * Math.Cos(2 * Math.PI * 1300 * t);
            }
            double mag440 = frames > 0 ? 2 * Math.Sqrt(sin440 * sin440 + cos440 * cos440) / frames : 0;
            double magDecoy = frames > 0 ? 2 * Math.Sqrt(sinDecoy * sinDecoy + cosDecoy * cosDecoy) / frames : 0;
            Check(frames > 5000 && frames < 6000, "extracted PCM holds ~0.25s of 22050Hz mono frames",
                  "frames=" + frames);
            Check(mag440 > 0.5 && mag440 < 1.0 && magDecoy < 0.1,
                  "extracted audio still IS the fixture's 440Hz tone",
                  "440Hz=" + mag440.ToString("F3") + " decoy1300Hz=" + magDecoy.ToString("F3")
                      + " peak=" + peak.ToString("F3"));

            // THE LOUD SKIPS, and the reason this test carries three audio fixtures that are
            // byte-for-byte the same sound. AudioClip.GetData reads DECODED PCM, so it serves
            // only a clip the author imported as DecompressOnLoad; Unity says so itself
            // ("Cannot get data on compressed samples for audio clip ... Changing the load type
            // to DecompressOnLoad on the audio clip will fix this"). The other two load types
            // are therefore not extractable AT ALL by this route, and the only difference
            // between these three fixtures is the load type in their .meta - so a skip here can
            // have no other cause.
            //
            // Unity's default is DecompressOnLoad, so the clip an author never configured does
            // convert - fixture_beep.wav carries Unity's own generated .meta and is the proof.
            // But music is the part of a mod an author DOES configure, and both of the settings
            // they would reach for are unreadable. If DREAM's 273MB music module turns out to be
            // streamed, the whole module is unconvertible and this report is the only place
            // anyone would find that out - so it is counted per load type and warned about per
            // clip, never silently totalled.
            int streamSkipped, packedSkipped, noData;
            report.skippedByType.TryGetValue("AudioClip(streaming)", out streamSkipped);
            report.skippedByType.TryGetValue("AudioClip(compressed)", out packedSkipped);
            report.skippedByType.TryGetValue("AudioClip(nodata)", out noData);
            Check(streamSkipped == 1, "a Streaming clip is skipped loudly, not silently dropped",
                  "AudioClip(streaming)=" + streamSkipped);
            Check(packedSkipped == 1,
                  "a CompressedInMemory clip is skipped loudly too, for the same reason",
                  "AudioClip(compressed)=" + packedSkipped);
            Check(noData == 0, "no clip reached the GetData backstop: the load type caught both",
                  "AudioClip(nodata)=" + noData);
            Check(report.extracted.Find(p => p.EndsWith("fixture_stream.wav")) == null
                  && report.extracted.Find(p => p.EndsWith("fixture_packed.wav")) == null,
                  "the unreadable clips really are absent from the extraction");
            Check(!report.notesByType.ContainsKey("AudioClip(streaming)")
                  && !report.notesByType.ContainsKey("AudioClip(compressed)"),
                  "a skip is a loss, so it is never filed as a note about a survivor");

            // 3f. The audio half of the import policy - MobileConvertedModImporter.OnPreprocessAudio -
            // which nothing in the suite could reach until audio was extracted, because the
            // postprocessor is scoped to the extraction root and nothing had ever landed an
            // AudioClip there. Songs must stream (a megabyte-per-minute resident song is what
            // the memory budget cannot afford) and sound effects must not (a streamed effect
            // stutters on its first frame), and that split is decided by file size here.
            var clipImp = wav != null ? AssetImporter.GetAtPath(wav) as AudioImporter : null;
            var sampleSettings = clipImp != null ? clipImp.defaultSampleSettings
                                                 : default(AudioImporterSampleSettings);
            // Two of the three are proof rather than pins: measured against the live control
            // below, Unity 6 defaults a .wav to DecompressOnLoad at quality 1.0, so the load
            // type and the quality here are both the postprocessor's doing. Vorbis happens to
            // coincide with Unity's default and is a regression pin only. The Streaming branch
            // for songs cannot be reached by any fixture small enough to commit, so it is
            // pinned as a pure rule in TestConvertedModImportPolicy instead.
            Check(clipImp != null && sampleSettings.compressionFormat == AudioCompressionFormat.Vorbis,
                  "converted audio is Vorbis, not raw PCM",
                  clipImp == null ? "no importer" : sampleSettings.compressionFormat.ToString());
            Check(clipImp != null && sampleSettings.loadType == AudioClipLoadType.CompressedInMemory,
                  "a small clip is a sound effect: compressed in memory, never streamed",
                  clipImp == null ? "no importer" : sampleSettings.loadType.ToString());
            Check(clipImp != null && Mathf.Abs(sampleSettings.quality - 0.7f) < 0.001f,
                  "converted audio carries the policy's Vorbis quality",
                  clipImp == null ? "no importer" : sampleSettings.quality.ToString("F3"));

            // Non-defaultness, PROVEN rather than claimed - the trap the texture block above
            // documents in its own words ("those three would pass with the postprocessor
            // deleted"). The source fixture is the same bytes as the extracted file but lives
            // outside the extraction root, so the postprocessor never sees it, and its .meta is
            // deliberately left at guid-only - which is exactly how Unity 6 records "every
            // importer setting is at its default". Reading both importers and requiring them to
            // differ is what turns the three checks above into evidence that the policy ran,
            // without this test having to hard-code Unity's defaults and rot when they move.
            // NON-DEFAULTNESS, PROVEN AGAINST A LIVE CONTROL rather than against a remembered
            // value. fixture_beep.wav is the same bytes as the extracted file, sits outside the
            // extraction root so the postprocessor never sees it, and carries the .meta UNITY
            // ITSELF generated - so its importer is Unity's defaults, read at run time. Reading
            // them instead of hard-coding them is what keeps this from rotting when Unity's
            // defaults move, and it is what corrected this task's own first guess about what
            // they were.
            const string defaultFixture =
                "Assets/Editor/TestFixtures/ExtractorFixture/fixture_beep.wav";
            var srcImp = AssetImporter.GetAtPath(defaultFixture) as AudioImporter;
            var srcSettings = srcImp != null ? srcImp.defaultSampleSettings
                                             : default(AudioImporterSampleSettings);
            string audioSettings = srcImp == null || clipImp == null ? "no importer"
                : "default=" + srcSettings.compressionFormat + "/" + srcSettings.loadType
                  + "/q" + srcSettings.quality.ToString("F2")
                  + " converted=" + sampleSettings.compressionFormat + "/"
                  + sampleSettings.loadType + "/q" + sampleSettings.quality.ToString("F2");
            Check(srcImp != null && clipImp != null
                  && Mathf.Abs(srcSettings.quality - sampleSettings.quality) > 0.001f,
                  "the converted clip's Vorbis quality is not the importer default",
                  audioSettings);
            Check(srcImp != null && clipImp != null
                  && srcSettings.loadType != sampleSettings.loadType,
                  "the converted clip's load type is not the importer default either",
                  audioSettings);
            // And the source really is readable, which is what makes it a control AND what
            // makes the three skips above attributable to the load type and nothing else.
            Check(srcImp != null && srcSettings.loadType == AudioClipLoadType.DecompressOnLoad,
                  "Unity's default load type is DecompressOnLoad, so an unconfigured clip converts",
                  audioSettings);

            if (clipImp != null)
                Debug.Log(string.Format("[MobileSelfTest] converted-mod audio policy produced: " +
                    "format={0} loadType={1} quality={2} -> clip {3}Hz x{4} {5}s " +
                    "(Unity's defaults for the same file: format={6} loadType={7} quality={8})",
                    sampleSettings.compressionFormat, sampleSettings.loadType, sampleSettings.quality,
                    clip == null ? 0 : clip.frequency, clip == null ? 0 : clip.channels,
                    clip == null ? 0f : clip.length,
                    srcImp == null ? "?" : srcSettings.compressionFormat.ToString(),
                    srcImp == null ? "?" : srcSettings.loadType.ToString(),
                    srcImp == null ? "?" : srcSettings.quality.ToString("F2")));

            // 4. Rewritten manifest points at extracted files, keeps identity.
            ModInfo info = null;
            ModManager._serializer.TryDeserialize(
                fsJsonParser.Parse(File.ReadAllText(report.manifestPath)), ref info);
            Check(info != null && info.ModTitle == "Extractor Fixture"
                  && info.GUID == "0d2c4a68-9e1f-4b7a-8c35-6d0e2f4a6b8c",
                  "manifest identity preserved");
            Check(info != null && info.Files.Count == 6
                  && info.Files.TrueForAll(f => File.Exists(f)),
                  "manifest Files rewritten to extracted paths");

            // 5. Full circle: rebuild from the extraction, short-name lookup still answers.
            string[] rebuilt = MobileModBuilder.BuildMod(report.manifestPath, bundleDir,
                new[] { BuildTarget.StandaloneOSX });
            AssetBundle ab = AssetBundle.LoadFromFile(rebuilt[0]);
            Check(ab != null && ab.Contains("fixture_tex"), "rebuilt bundle answers to short name");
            if (ab != null)
            {
                var t = ab.LoadAsset<Texture2D>("fixture_tex");
                Check(t != null && t.width == 64 && t.height == 64, "rebuilt texture is 64x64",
                      t ? t.width + "x" + t.height : "null");
                ab.Unload(true);
            }

            // Cleanup.
            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// A .dfmod is untrusted input - a file a stranger hands us - and the converter is meant
        /// to be exposed publicly. The manifest inside the bundle is the part an attacker fully
        /// controls, so an unconstrained Files entry ("../../.ssh/authorized_keys", or an absolute
        /// path, which Path.Combine would let win outright) is an arbitrary file write, not a
        /// theoretical one. Every output path must therefore be proven inside the extraction root
        /// before any byte is written.
        /// </summary>
        static void TestModExtractorPathContainment()
        {
            // The decision itself, as a pure function. Normalisation, not string matching.
            const string root = "Assets/Game/Mods/Converted/probe";
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "tex.png"), root),
                  "containment: a plain path inside the root is allowed");
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "Assets/Textures/water.png"), root),
                  "containment: a nested path inside the root is allowed");
            Check(!MobileModExtractor.IsInsideRoot(Path.Combine(root, "../escape.png"), root),
                  "containment: .. climbing out of the root is refused");
            Check(!MobileModExtractor.IsInsideRoot("/tmp/dfu-extractor-evil.png", root),
                  "containment: an absolute path is refused");
            // Path.Combine would happily build this, and a naive StartsWith would accept it.
            Check(!MobileModExtractor.IsInsideRoot(root + "-evil/x.png", root),
                  "containment: a sibling sharing a name prefix is refused");
            // .. that resolves back inside is legitimate; refusing it would be a false positive.
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "sub/../ok.png"), root),
                  "containment: .. that resolves back inside is allowed");

            // End to end, through Extract, with a genuinely hostile bundle. MobileModBuilder
            // cannot produce one - it validates every manifest entry - so pack it the way an
            // attacker would, giving one asset an addressable name that climbs out of the root.
            const string hostileManifest = "Assets/Editor/TestFixtures/ExtractorFixture/hostile-mod.dfmod.json";
            const string payload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_payload.json";
            // A distinct asset: Unity refuses to pack the same one into a bundle twice.
            const string escapePayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_escape_payload.json";
            const string escapeName = "../dfu-extractor-escape.json";
            const string bundleDir = "Temp/MobileModExtractorEscapeTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__escape__";
            const string escapeTarget = "Assets/Game/Mods/Converted/dfu-extractor-escape.json";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }
            File.Delete(escapeTarget);

            // hostile-mod.dfmod.json's Files lists the escaping name, and one bundle asset is
            // addressed by it, so the extractor resolves that entry exactly as it would a real
            // attacker's - through the manifest lookup, straight into an output path.
            var build = new AssetBundleBuild[1];
            build[0].assetBundleName = "hostile-mod.dfmod";
            build[0].assetNames = new[] { payload, escapePayload, hostileManifest };
            build[0].addressableNames = new[] {
                "assets/editor/testfixtures/extractorfixture/hostile_payload.json",
                escapeName,
                "assets/editor/testfixtures/extractorfixture/hostile-mod.dfmod.json" };

            Directory.CreateDirectory(bundleDir);
            BuildPipeline.BuildAssetBundles(bundleDir, build,
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneOSX);
            string hostileBundle = Path.Combine(bundleDir, "hostile-mod.dfmod");

            var report = MobileModExtractor.Extract(hostileBundle, extractRoot);

            int escapes;
            report.skippedByType.TryGetValue("path-escape", out escapes);
            Check(escapes == 1, "hostile manifest entry is refused", "path-escape=" + escapes);
            Check(!File.Exists(escapeTarget) && !File.Exists("Assets/Game/Mods/Converted/" + Path.GetFileName(escapeName)),
                  "nothing was written outside the extraction root");
            Check(report.extracted.Count == 1 && report.extracted[0].EndsWith("hostile_payload.json"),
                  "the legitimate asset still extracts alongside the refused one",
                  "extracted=" + report.extracted.Count);

            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Containment is not the only way a manifest path fails to become a file. A mod listing
        /// both "clash" (a TextAsset) and "clash/inner.json" is fully contained and fully legal,
        /// but one of the two must lose - a name cannot be a file and a directory at once - and an
        /// unguarded write would throw straight out of Extract, costing the operator every other
        /// asset in the mod. The same manifest also spells one file two ways, which must count as
        /// a collision rather than two assets: keyed on the raw string they look distinct, so the
        /// second would quietly overwrite the first and the rebuilt manifest would list it twice,
        /// which Unity then refuses to pack at all.
        /// </summary>
        static void TestModExtractorSurvivesBadPaths()
        {
            const string clashManifest = "Assets/Editor/TestFixtures/ExtractorFixture/clash-mod.dfmod.json";
            const string okPayload = "Assets/Editor/TestFixtures/ExtractorFixture/fixture_data.json";
            const string filePayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_payload.json";
            const string innerPayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_escape_payload.json";
            const string dupePayload = "Assets/Editor/TestFixtures/ExtractorFixture/clash_dupe_payload.json";
            const string bundleDir = "Temp/MobileModExtractorClashTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__clash__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }

            const string dir = "assets/editor/testfixtures/extractorfixture/";
            var build = new AssetBundleBuild[1];
            build[0].assetBundleName = "clash-mod.dfmod";
            build[0].assetNames = new[] { okPayload, filePayload, innerPayload, dupePayload, clashManifest };
            build[0].addressableNames = new[] {
                dir + "clash_ok.json",
                dir + "clash",                      // a file...
                dir + "clash/inner.json",           // ...and the same name as a directory
                dir + "sub/../clash_ok.json",       // a second spelling of clash_ok.json
                dir + "clash-mod.dfmod.json" };

            Directory.CreateDirectory(bundleDir);
            BuildPipeline.BuildAssetBundles(bundleDir, build,
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneOSX);

            var report = MobileModExtractor.Extract(Path.Combine(bundleDir, "clash-mod.dfmod"), extractRoot);

            // Whichever of the file/directory pair the bundle happens to enumerate first, exactly
            // one of them is unwritable - so these hold without depending on that order.
            int writeFailed;
            report.skippedByType.TryGetValue("write-failed", out writeFailed);
            Check(writeFailed == 1, "an unwritable path costs only its own asset",
                  "write-failed=" + writeFailed);

            int collisions;
            report.skippedByType.TryGetValue("collision", out collisions);
            Check(collisions == 1, "two spellings of one file are one collision, not two assets",
                  "collision=" + collisions);

            Check(report.extracted.Count == 2, "the rest of the mod still extracts",
                  "extracted=" + report.extracted.Count);

            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
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
