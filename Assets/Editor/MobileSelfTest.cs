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

        #endregion

    }
}
