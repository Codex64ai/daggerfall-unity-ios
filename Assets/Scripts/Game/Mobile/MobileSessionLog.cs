// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Periodic sampling of how the app is actually holding up, appended as CSV to
//   Documents/session-log.csv so it can be pulled off the device afterwards.
//
//   Why this exists: this port has had exactly one performance incident - a kernel
//   watchdog panic traced to a Unity development build's debug transport (see
//   HANDOFF/REVIEW) - and it was diagnosed from a crash report after the fact. Nobody has
//   ever watched a long RELEASE-build session, so "does it stay healthy for three hours"
//   is unanswered. Impressions are not data; this makes the next long session produce
//   evidence for free.
//
//   iOS does not expose thermal state to Unity, so throttling has to be inferred: sustained
//   frame-time growth at a steady battery drain is what thermal limiting looks like from
//   inside the process. Frame time, battery, and managed memory together are enough to tell
//   "we are leaking", "we are being throttled", and "we are fine" apart.
//
//   Deliberately cheap: one sample per 30s, ~90 bytes each, and the file is capped. A
//   three-hour session costs about 32KB and 360 samples.
//

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileSessionLog
    {
        const string fileName = "session-log.csv";
        const float sampleSeconds = 30f;
        const int maxSamples = 1200;              // ~10 hours, then it stops growing

        static float nextSample = -1f;
        static int samples;
        static bool failed;

        // Frame-time stats accumulated between samples, so each row summarises a window
        // rather than catching one arbitrary frame.
        static float windowWorst;
        static float windowTotal;
        static int windowFrames;

        static float sessionStart;

        public static void Poll()
        {
            if (failed || !MobileInput.Enabled || samples >= maxSamples)
                return;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                windowTotal += dt;
                windowFrames++;
                if (dt > windowWorst)
                    windowWorst = dt;
            }

            if (nextSample < 0f)
            {
                // First call: start the clock, and skip the launch spike - the first frames
                // include scene load and shader warm-up and would libel the whole session.
                sessionStart = Time.unscaledTime;
                nextSample = Time.unscaledTime + sampleSeconds;
                ResetWindow();
                return;
            }

            if (Time.unscaledTime < nextSample)
                return;

            nextSample = Time.unscaledTime + sampleSeconds;
            Write();
            ResetWindow();
        }

        static void ResetWindow()
        {
            windowWorst = 0f;
            windowTotal = 0f;
            windowFrames = 0;
        }

        static void Write()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);

                if (samples == 0 && !File.Exists(path))
                {
                    File.WriteAllText(path,
                        "# Daggerfall Unity iOS session log. One row per 30s.\n" +
                        "# avg_ms/worst_ms: frame time over the window. Sustained growth at a\n" +
                        "# steady battery drain is what thermal throttling looks like from\n" +
                        "# inside the process - iOS does not expose thermal state to Unity.\n" +
                        "# managed_mb climbing without bound across hours suggests a leak.\n" +
                        "minutes,avg_ms,worst_ms,fps,battery,managed_mb,scene\n");
                }

                float avgMs = windowFrames > 0 ? (windowTotal / windowFrames) * 1000f : 0f;
                float fps = windowTotal > 0f ? windowFrames / windowTotal : 0f;
                float managedMb = GC.GetTotalMemory(false) / (1024f * 1024f);

                var sb = new StringBuilder();
                sb.Append(((Time.unscaledTime - sessionStart) / 60f).ToString("0.0")).Append(',');
                sb.Append(avgMs.ToString("0.0")).Append(',');
                sb.Append((windowWorst * 1000f).ToString("0.0")).Append(',');
                sb.Append(fps.ToString("0.0")).Append(',');
                sb.Append(BatteryText()).Append(',');
                sb.Append(managedMb.ToString("0.0")).Append(',');
                sb.Append(SceneText());

                File.AppendAllText(path, sb.ToString() + "\n");
                samples++;
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.LogWarning("[MobileSessionLog] disabled: " + ex.Message);
            }
        }

        static string BatteryText()
        {
            float level = SystemInfo.batteryLevel;      // -1 when unavailable
            if (level < 0f)
                return "n/a";

            return (level * 100f).ToString("0") + (SystemInfo.batteryStatus == BatteryStatus.Charging
                ? "+" : "");
        }

        /// <summary>Coarse context, so a bad window can be attributed to something.</summary>
        static string SceneText()
        {
            if (MobileInput.MenuMode)
                return "menu";
            if (GameManager.HasInstance && GameManager.Instance.IsPlayerInside)
                return GameManager.Instance.IsPlayerInsideDungeon ? "dungeon" : "interior";

            return "exterior";
        }
    }
}
