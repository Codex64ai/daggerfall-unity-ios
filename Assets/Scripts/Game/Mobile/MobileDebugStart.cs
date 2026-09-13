// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Device-side diagnostic for the "every building is a flat grey box" report (2026-09-03). The
// editor cannot reproduce it: the same bundles import every UBLaMF model fully textured there,
// so the evidence has to come from a player build. When Documents/debug-newchar.txt exists the
// app skips the title menu, starts a new character outdoors, and writes one line per custom
// model (and the first vanilla meshes) describing its materials - shader, texture, format - to
// the log MobileLog mirrors into Documents/Player.log. Inert without the file.
//
// THE COMMAND FILE. debug-newchar.txt is read line by line; an empty file still does the
// hands-free start. Commands:
//
//   pixel <X> <Y>                     teleport to that map pixel once the world is up (207 213 =
//                                     Daggerfall city). Unchanged; still recognised anywhere in
//                                     the file.
//   set [<seconds>] <Name> <value>    write DaggerfallUnity.Settings.<Name> by reflection <seconds>
//                                     after the world finishes loading, then deploy it exactly the
//                                     way the in-game settings panel does. <seconds> may be left
//                                     out, in which case the command fires five seconds after the
//                                     previous one (the first, five seconds after the world).
//
// WHY `set` EXISTS. Some bugs only happen on the TRANSITION between two settings - the CRT
// filter's native render target handing Camera.main back to retro mode is the one this was
// written for (device report, 2026-09-10: "retro mode is broke when I switch the resolution ...
// I can switch it back but world breaks"). A build launched with the setting already in place
// never executes that transition, and there is no way to tap the settings panel from a headless
// simulator run. This drives the same code path the panel's own callbacks drive - the property
// setter, SaveSettings, and DeployCoreGameEffectSettings for the retro group - on a timer, so a
// simulator launch can reproduce a live switch and screenshot both sides of it.
//
// Every `set` logs a camera/presenter snapshot before and after, and again on each of the next
// three frames, because the failures in this area are one-frame ordering failures between two
// components that both want to own Camera.main.targetTexture.
//
// TEST APP ONLY. Everything below that touches the device - the hook, the driver, the `set`
// schedule and the reflection write - is inside #if DFU_IOS_TESTAPP, a define MobileBuildSetup
// adds only when DFU_IOS_TESTAPP=1 built the app (and removes otherwise). MobileContentPath.Active
// is `UNITY_IOS && !UNITY_EDITOR`, i.e. TRUE in a release build too, and debug-newchar.txt lives in
// the same Documents folder the port tells players to drop arena2 into - so the runtime gate alone
// would ship a surface that writes any SettingsManager property by reflection and saves it. The two
// members left outside the gate are the pure parser (which the editor self-test exercises) and
// Audit (which other files call, and which is inert while Active is false).

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileDebugStart
    {
        public const string FileName = "debug-newchar.txt";
        public static bool Active { get; private set; }

        /// <summary>`journeyfix 0|1` from the command file: -1 means the line was not present.</summary>
        public static int journeyFix = -1;
        static readonly HashSet<string> audited = new HashSet<string>();
#if DFU_IOS_TESTAPP
        static int targetX = -1, targetY = -1;   // "pixel X Y" in the command file teleports there once in the world
        static int journeyX = -1, journeyY = -1; // "journey X Y" starts the real-travel autopilot to that map pixel
        static bool journeyStarted;
        static bool boxLoggerInstalled;
        static readonly HashSet<object> loggedBoxes = new HashSet<object>();
        static DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox pendingBox;
        static float pendingBoxAt;
        static int boxesSeen;

        /// <summary>One scheduled `set` line: a settings property written <see cref="at"/> seconds
        /// after the world finished loading.</summary>
        class SetCommand
        {
            public float at;
            public string name;
            public string value;
            public bool fired;
        }

        static readonly List<SetCommand> setCommands = new List<SetCommand>();

        /// <summary>Time.realtimeSinceStartup at which the world finished loading; the `set`
        /// schedule is measured from here. Negative until then.</summary>
        static float worldReadyAt = -1f;

        /// <summary>Frames still owed a camera snapshot after the last `set`.</summary>
        static int snapshotsOwed;
#endif

        /// <summary>The gap used for a `set` line that did not name its own time.</summary>
        public const float DefaultSetSpacing = 5f;

        /// <summary>
        /// Parses the command file's text into the teleport target and the `set` schedule. Split out
        /// from <see cref="Hook"/> so the self-test can exercise it without a Documents folder: the
        /// schedule is the part with arithmetic in it, and a mis-parsed delay silently turns a
        /// reproduction run into a run that never switches anything.
        /// </summary>
        public static List<string> ParseCommands(string text, out int pixelX, out int pixelY,
                                                 out List<KeyValuePair<float, string>> sets)
        {
            int jx, jy;
            return ParseCommands(text, out pixelX, out pixelY, out sets, out jx, out jy);
        }

        /// <summary>
        /// As above, plus the `journey X Y` destination. Separate overload rather than a fifth out
        /// parameter on the original so existing callers (and their tests) are untouched.
        /// </summary>
        public static List<string> ParseCommands(string text, out int pixelX, out int pixelY,
                                                 out List<KeyValuePair<float, string>> sets,
                                                 out int toX, out int toY)
        {
            pixelX = -1;
            pixelY = -1;
            toX = -1;
            toY = -1;
            journeyFix = -1;
            sets = new List<KeyValuePair<float, string>>();
            List<string> errors = new List<string>();
            float previous = 0f;

            if (text == null)
                return errors;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] w = line.Split(new[] { ' ', '\t', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (w.Length == 0)
                    continue;

                if (w[0] == "pixel" && w.Length >= 3)
                {
                    int x, y;
                    if (int.TryParse(w[1], out x) && int.TryParse(w[2], out y)) { pixelX = x; pixelY = y; }
                    else errors.Add(line);
                    continue;
                }

                // `journeyfix 0|1` - turn the journey/quest-popup fixes off or on for this run, so
                // the before and after box counts come off the SAME binary on the same route.
                if (w[0] == "journeyfix" && w.Length >= 2)
                {
                    if (w[1] == "0" || w[1] == "1") journeyFix = w[1] == "1" ? 1 : 0;
                    else errors.Add(line);
                    continue;
                }

                // `journey X Y` - start Real travel's autopilot to that map pixel once the world is
                // up. There is no other scripted way into a journey (it begins from a tap on the
                // travel popup), and the pass-through rules cannot be driven from the self test.
                if (w[0] == "journey" && w.Length >= 3)
                {
                    int x, y;
                    if (int.TryParse(w[1], out x) && int.TryParse(w[2], out y)) { toX = x; toY = y; }
                    else errors.Add(line);
                    continue;
                }

                if (w[0] == "set")
                {
                    // `set <seconds> <Name> <value>` or `set <Name> <value>`. The time is optional
                    // because a two-step reproduction reads better without arithmetic in it, and it
                    // is recognised by being a number - a settings property never is.
                    int i = 1;
                    float at;
                    if (w.Length >= 4 && float.TryParse(w[1], System.Globalization.NumberStyles.Float,
                                                       System.Globalization.CultureInfo.InvariantCulture, out at))
                        i = 2;
                    else
                        at = previous + DefaultSetSpacing;

                    if (w.Length < i + 2) { errors.Add(line); continue; }

                    // NIT-4 (review 2026-09-11): a three-word `set` whose second word is a number
                    // is a time with its value missing, not a settings property called "1.5". Report
                    // it rather than letting it reach ApplySetting as a name nothing can resolve.
                    float unused;
                    if (i == 1 && float.TryParse(w[1], System.Globalization.NumberStyles.Float,
                                                 System.Globalization.CultureInfo.InvariantCulture, out unused))
                    { errors.Add(line); continue; }

                    previous = at;
                    sets.Add(new KeyValuePair<float, string>(at, w[i] + " " + w[i + 1]));
                    continue;
                }

                errors.Add(line);
            }

            return errors;
        }

#if DFU_IOS_TESTAPP
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (!MobileContentPath.Active)
                return;
            string file = Path.Combine(Application.persistentDataPath, FileName);
            if (!File.Exists(file))
                return;
            Active = true;
            try
            {
                List<KeyValuePair<float, string>> sets;
                List<string> errors = ParseCommands(File.ReadAllText(file), out targetX, out targetY, out sets,
                                                    out journeyX, out journeyY);
                if (journeyX >= 0)
                    Debug.Log(string.Format("[DebugStart] scheduled: journey to map pixel {0},{1}", journeyX, journeyY));
                if (journeyFix >= 0)
                {
                    MobileJourneyController.DebugJourneyFixes = journeyFix == 1;
                    Debug.Log("[DebugStart] journey/quest-popup fixes " + (journeyFix == 1 ? "ON" : "OFF (measuring the old behaviour)"));
                }
                foreach (KeyValuePair<float, string> s in sets)
                {
                    string[] nv = s.Value.Split(' ');
                    setCommands.Add(new SetCommand { at = s.Key, name = nv[0], value = nv[1] });
                    Debug.Log(string.Format("[DebugStart] scheduled: set {0} = {1} at world+{2:0.0}s", nv[0], nv[1], s.Key));
                }
                foreach (string bad in errors)
                    Debug.LogWarning("[DebugStart] unrecognised command line: " + bad);
            }
            catch (System.Exception ex) { Debug.LogWarning("[DebugStart] command file: " + ex.Message); }
            Debug.Log(string.Format("[DebugStart] {0} present: new character outdoors + material audit. gfx={1} astc6x6={2} copyTexture={3}",
                FileName, SystemInfo.graphicsDeviceType, SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_6x6), SystemInfo.copyTextureSupport));
            var go = new GameObject("MobileDebugStart");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
            // Who restarted the game? Log the events with a stack so a reset after spawn can be traced.
            StartGameBehaviour.OnNewGame += () => Debug.Log("[DebugStart] OnNewGame\n" + System.Environment.StackTrace);
            Serialization.SaveLoadManager.OnStartLoad += (saveData) => Debug.Log("[DebugStart] OnStartLoad\n" + System.Environment.StackTrace);
        }

        /// <summary>
        /// Fires the scheduled `set` commands. Called every frame once the world is up.
        /// </summary>
        static void RunSetCommands()
        {
            LogMessageBoxes();
            AnswerPendingBox();
            CloseRestWindow();
            StartScheduledJourney();
            ReportJourneyEnd();

            if (snapshotsOwed > 0)
            {
                snapshotsOwed--;
                LogCameras("after+" + (3 - snapshotsOwed));
            }

            if (worldReadyAt < 0f)
                return;

            float t = Time.realtimeSinceStartup - worldReadyAt;
            foreach (SetCommand c in setCommands)
            {
                if (c.fired || t < c.at)
                    continue;
                // NIT-3 (review 2026-09-11): fired is set AFTER the work, and the work is wrapped -
                // LogCameras sits outside ApplySetting's own try, so a throw there used to consume
                // the command and leave the run silently doing nothing. Set after the catch rather
                // than in it, so a persistent throw cannot retry every frame either.
                try
                {
                    LogCameras("before set " + c.name + "=" + c.value);
                    ApplySetting(c.name, c.value);
                    LogCameras("after set " + c.name + "=" + c.value);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[DebugStart] set " + c.name + "=" + c.value + " threw: " + ex.Message);
                }
                c.fired = true;
                snapshotsOwed = 3;
                return;     // one per frame: two settings changing in the same frame is not what a player does
            }
        }

        /// <summary>
        /// Writes one DaggerfallUnity.Settings property by reflection and then deploys it the way
        /// the in-game panel does. The deploy half matters as much as the write: RetroRenderingMode
        /// is not read per frame by anything, it is pushed into RetroRenderer by
        /// DeployCoreGameEffectSettings, and MobileSettingsPanel's own callback is exactly
        /// "set the property, SaveSettings, deploy the RetroMode group".
        /// </summary>
        static void ApplySetting(string name, string value)
        {
            try
            {
                SettingsManager settings = DaggerfallUnity.Settings;
                PropertyInfo prop = typeof(SettingsManager).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite)
                {
                    Debug.LogWarning("[DebugStart] set: no writable setting named '" + name + "'");
                    return;
                }

                object parsed;
                if (prop.PropertyType == typeof(bool))
                {
                    // "1"/"0" as well as "True"/"false": a settings file writes the words, a person
                    // driving a reproduction writes the digits.
                    bool b;
                    if (value == "1") b = true;
                    else if (value == "0") b = false;
                    else if (!bool.TryParse(value, out b)) { Debug.LogWarning("[DebugStart] set: '" + value + "' is not a bool"); return; }
                    parsed = b;
                }
                else
                {
                    parsed = System.Convert.ChangeType(value, prop.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
                }

                object before = prop.GetValue(settings);
                prop.SetValue(settings, parsed);
                Debug.Log(string.Format("[DebugStart] set {0}: {1} -> {2}", name, before, prop.GetValue(settings)));

                settings.SaveSettings();

                // The two retro properties own the main camera's render target and the aspect
                // viewport, so they have to be deployed, not merely stored - MobileSettingsPanel
                // and RetroModeConfigPage both do this and nothing else. Everything else in the
                // panel is read where it is used, so storing it is the whole of the change.
                if (name == "RetroRenderingMode" || name == "RetroModeAspectCorrection")
                {
                    if (GameManager.HasInstance && GameManager.Instance.StartGameBehaviour != null)
                        GameManager.Instance.StartGameBehaviour.DeployCoreGameEffectSettings(CoreGameEffectSettingsGroups.RetroMode);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[DebugStart] set " + name + " = " + value + " threw: " + ex.Message);
            }
        }

        /// <summary>
        /// One line per message box the moment it reaches the top of the UI stack, with its first
        /// text row. Counting boxes is the whole measurement for "quest pop-ups appear nonstop
        /// during a journey", and a count read off a screen recording is not a count.
        /// </summary>
        static void LogMessageBoxes()
        {
            // DaggerfallUI does not exist at AfterSceneLoad, so this is retried every frame from
            // the driver until it takes. Installed once, for the life of the run.
            if (boxLoggerInstalled || !DaggerfallUI.HasInstance || DaggerfallUI.UIManager == null)
                return;
            boxLoggerInstalled = true;

            DaggerfallUI.UIManager.OnWindowChange += (sender, e) =>
            {
                var box = DaggerfallUI.UIManager.TopWindow as DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox;
                if (box == null || !loggedBoxes.Add(box))
                    return;

                string first = "?";
                try
                {
                    FieldInfo f = typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox)
                        .GetField("label", BindingFlags.NonPublic | BindingFlags.Instance);
                    var mftl = f != null ? f.GetValue(box) as DaggerfallWorkshop.Game.UserInterface.MultiFormatTextLabel : null;
                    if (mftl != null && mftl.TextLabels != null && mftl.TextLabels.Count > 0)
                        foreach (var tl in mftl.TextLabels)
                            if (tl != null && !string.IsNullOrEmpty(tl.Text)) { first = tl.Text; break; }
                }
                catch (System.Exception ex) { first = "<" + ex.GetType().Name + ">"; }

                var gm = GameManager.HasInstance ? GameManager.Instance : null;
                var gps = gm != null ? gm.PlayerGPS : null;
                boxesSeen++;
                Debug.Log(string.Format("[DebugStart] MSGBOX #{0} travelling={1} pixel={2},{3} loc='{4}' inRect={5} text=\"{6}\"",
                    boxesSeen, MobileJourneyPilot.Active,
                    gps != null ? gps.CurrentMapPixel.X : -1, gps != null ? gps.CurrentMapPixel.Y : -1,
                    gps != null && gps.HasCurrentLocation ? gps.CurrentLocation.Name : "-",
                    gps != null && gps.IsPlayerInLocationRect, first));

                // A scripted journey has no finger to dismiss boxes with, and one unanswered
                // prompt stops the run dead (measured: the journey never left its first map pixel).
                // Only while a `journey` line is driving the run.
                if (journeyX >= 0)
                {
                    pendingBox = box;
                    pendingBoxAt = Time.realtimeSinceStartup;
                }
            };
        }

        /// <summary>
        /// Answers the logged box a moment later: its default button if it has one (that is what
        /// Return does), otherwise the last button added, otherwise just closes it. Unscaled time,
        /// because the game is paused under the box.
        /// </summary>
        static void AnswerPendingBox()
        {
            if (pendingBox == null || Time.realtimeSinceStartup - pendingBoxAt < 1f)
                return;

            var box = pendingBox;
            pendingBox = null;
            try
            {
                if (DaggerfallUI.UIManager.TopWindow != box)
                    return;

                var def = box.GetDefaultButton();
                if (def == null)
                {
                    FieldInfo bf = typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox)
                        .GetField("buttons", BindingFlags.NonPublic | BindingFlags.Instance);
                    var list = bf != null ? bf.GetValue(box) as List<DaggerfallWorkshop.Game.UserInterface.Button> : null;
                    if (list != null && list.Count > 0)
                        def = list[list.Count - 1];
                }

                if (def != null)
                {
                    def.TriggerMouseClick();
                    Debug.Log("[DebugStart] MSGBOX answered with its default button");
                }
                else
                {
                    box.CloseWindow();
                    Debug.Log("[DebugStart] MSGBOX closed");
                }
            }
            catch (System.Exception ex) { Debug.Log("[DebugStart] MSGBOX answer threw: " + ex.Message); }
        }

        static float restWindowSeenAt = -1f;

        /// <summary>
        /// Closes the camp rest screen a moment after it appears. A journey that meets nightfall in
        /// the open stops and puts Daggerfall's Rest screen up, and a scripted run has no one to
        /// answer it - measured as 15 restarts all spent camping on the same map pixel. Closing it
        /// resumes the journey; the night is already marked handled, so it travels on rather than
        /// camping again on the next frame.
        /// </summary>
        static void CloseRestWindow()
        {
            if (journeyX < 0 || !DaggerfallUI.HasInstance || DaggerfallUI.UIManager == null)
                return;

            var top = DaggerfallUI.UIManager.TopWindow as DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallRestWindow;
            if (top == null) { restWindowSeenAt = -1f; return; }
            if (restWindowSeenAt < 0f) { restWindowSeenAt = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - restWindowSeenAt < 1.5f) return;

            restWindowSeenAt = -1f;
            RefillPlayer();
            top.CloseWindow();
            Debug.Log("[DebugStart] closed the camp rest screen; the journey travels on");
        }

        /// <summary>
        /// Puts the player back on their feet. A scripted journey never eats, drinks or sleeps, so
        /// vitals run down and the journey camps - and closing the camp screen without resting just
        /// camps again on the next frame. Test scaffolding: applied identically before and after,
        /// and only where the run would otherwise deadlock.
        /// </summary>
        static void RefillPlayer()
        {
            var p = GameManager.HasInstance ? GameManager.Instance.PlayerEntity : null;
            if (p == null)
                return;
            p.CurrentHealth = p.MaxHealth;
            p.CurrentFatigue = p.MaxFatigue;
            p.CurrentMagicka = p.MaxMagicka;
            if (DaggerfallUnity.Instance.WorldTime != null)
                p.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime();
        }

        /// <summary>Starts the `journey X Y` autopilot once, a few seconds after the world settles.</summary>
        static void StartScheduledJourney()
        {
            if (journeyStarted || journeyX < 0 || worldReadyAt < 0f)
                return;
            if (Time.realtimeSinceStartup - worldReadyAt < 5f)
                return;

            journeyStarted = true;
            bool began = MobileJourneyController.DebugBeginJourneyTo(journeyX, journeyY);
            Debug.Log(string.Format("[DebugStart] journey to {0},{1}: {2}", journeyX, journeyY,
                began ? "under way" : "REFUSED"));
        }

        static bool journeyEndReported;
        static int journeyRestarts;
        static float journeyStoppedAt;
        const int maxJourneyRestarts = 40;

        /// <summary>
        /// A journey stops for encounters, vitals and quest foes, and on this route a
        /// Random Little Quests foe stopped it in the first minute. A player would tap resume, so
        /// the run does too - up to a cap - and only reports the journey over when it reaches the
        /// destination or runs out of restarts. Each stop and restart is a log line.
        /// </summary>
        static void ReportJourneyEnd()
        {
            if (!journeyStarted || journeyEndReported || MobileJourneyPilot.Active)
            {
                journeyStoppedAt = -1f;
                return;
            }
            if (Time.realtimeSinceStartup - worldReadyAt < 8f)
                return;

            var gps = GameManager.HasInstance ? GameManager.Instance.PlayerGPS : null;
            int px = gps != null ? gps.CurrentMapPixel.X : -1;
            int py = gps != null ? gps.CurrentMapPixel.Y : -1;
            string loc = gps != null && gps.HasCurrentLocation ? gps.CurrentLocation.Name : "-";

            if (journeyStoppedAt < 0f)
            {
                journeyStoppedAt = Time.realtimeSinceStartup;
                Debug.Log(string.Format("[DebugStart] journey stopped at pixel {0},{1} loc='{2}' boxes={3}",
                    px, py, loc, boxesSeen));
                return;
            }

            bool arrived = px == journeyX && py == journeyY;
            if (arrived || journeyRestarts >= maxJourneyRestarts)
            {
                journeyEndReported = true;
                Debug.Log(string.Format("[DebugStart] JOURNEY ENDED {0} at pixel {1},{2} loc='{3}' boxes={4} restarts={5}",
                    arrived ? "ARRIVED" : "GAVE UP", px, py, loc, boxesSeen, journeyRestarts));
                return;
            }

            // Give the world a moment to settle (and any foe to wander off) before resuming.
            if (Time.realtimeSinceStartup - journeyStoppedAt < 6f)
                return;

            // Clear whatever stopped it. A quest foe standing where the journey begins stops it on
            // the frame it starts, forever (measured: 15 restarts without leaving Daggerfall), and
            // a scripted run has no sword. Test scaffolding, applied identically before and after.
            int killed = 0;
            foreach (DaggerfallEnemy e in Object.FindObjectsByType<DaggerfallEnemy>(FindObjectsSortMode.None))
                if (e != null) { Object.Destroy(e.gameObject); killed++; }
            if (killed > 0)
                Debug.Log("[DebugStart] cleared " + killed + " nearby enemies before resuming");

            RefillPlayer();
            journeyRestarts++;
            bool again = MobileJourneyController.DebugBeginJourneyTo(journeyX, journeyY);
            Debug.Log(string.Format("[DebugStart] journey restart {0}/{1} from {2},{3}: {4}",
                journeyRestarts, maxJourneyRestarts, px, py, again ? "under way" : "REFUSED"));
            journeyStoppedAt = again ? -1f : Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Who owns what, this frame: every enabled camera's render target, viewport rect and depth,
        /// plus the retro presenter's active state and source. This is the readout that tells a
        /// screenshot of a broken world apart from a screenshot of a broken transition.
        /// </summary>
        static void LogCameras(string when)
        {
            var sb = new StringBuilder();
            sb.Append("[DebugStart] cams ").Append(when).Append(": retro=").Append(DaggerfallUnity.Settings.RetroRenderingMode)
              .Append(" crt=").Append(DaggerfallUnity.Settings.CRTFilter)
              .Append(" native=").Append(MobileCrtNative.Active)
              .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height);

            Camera main = Camera.main;
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam == null)
                    continue;
                sb.Append(" [").Append(cam.name).Append(cam == main ? "*" : "")
                  .Append(" d=").Append(cam.depth)
                  .Append(" rect=").Append(Fmt(cam.rect))
                  .Append(" px=").Append((int)cam.pixelWidth).Append('x').Append((int)cam.pixelHeight)
                  .Append(" tgt=").Append(Name(cam.targetTexture))
                  .Append(" clear=").Append(cam.clearFlags)
                  .Append(']');
            }

            RetroPresentation presenter = GameManager.HasInstance ? GameManager.Instance.RetroPresenter : null;
            if (presenter == null)
                foreach (RetroPresentation p in Resources.FindObjectsOfTypeAll<RetroPresentation>())
                    if (p != null && p.gameObject.scene.IsValid()) { presenter = p; break; }

            sb.Append(" presenter=").Append(presenter == null ? "none"
                : (presenter.gameObject.activeInHierarchy ? "on" : "off") + " src=" + Name(presenter.RetroPresentationSource));

            RetroRenderer rr = GameManager.HasInstance ? GameManager.Instance.RetroRenderer : null;
            if (rr != null)
                sb.Append(" retroTex=").Append(Name(rr.RetroTexture)).Append(" presTarget=").Append(Name(rr.RetroPresentationTarget));
            sb.Append(" crtTarget=").Append(Name(MobileCrtNative.Target));

            Debug.Log(sb.ToString());
        }

        static string Fmt(Rect r)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###},{1:0.###},{2:0.###},{3:0.###})", r.x, r.y, r.width, r.height);
        }

        static string Name(Texture t)
        {
            return t == null ? "null" : t.name + " " + t.width + "x" + t.height;
        }

        class Driver : MonoBehaviour
        {
            float readyAt = -1f, teleportedAt = -1f;
            bool fired, popped, teleported, done;

            void Update()
            {
                if (done) { RunSetCommands(); return; }
                if (popped) { AfterStart(); return; }
                var dfu = DaggerfallUnity.Instance;
                if (dfu == null || !dfu.IsReady) return;
                var sgb = FindFirstObjectByType<StartGameBehaviour>();
                if (sgb == null) return;
                if (readyAt < 0f) { readyAt = Time.realtimeSinceStartup; return; }
                float t = Time.realtimeSinceStartup - readyAt;
                if (!fired && t > 3f)
                {
                    fired = true;
                    if (GameManager.Instance.PlayerEntity != null)
                        GameManager.Instance.PlayerEntity.OnDeath += (e) => Debug.Log("[DebugStart] player OnDeath\n" + System.Environment.StackTrace);
                    DaggerfallUnity.Settings.StartInDungeon = false;
                    sgb.StartMethod = StartGameBehaviour.StartMethods.NewCharacter;
                    Debug.Log("[DebugStart] StartMethod -> NewCharacter (outdoors)");
                }
                else if (fired && t > 6f)
                {
                    popped = true;
                    try { DaggerfallUI.Instance.PopToHUD(); } catch (System.Exception) { }
                    // A new character starts barefoot (vanilla gear is shirt and trousers). Outdoors in
                    // winter that kills within a minute once Climates & Calories is on, so wear whatever
                    // footwear the inventory holds - the survival systems can then be watched, not the death.
                    try
                    {
                        var player = GameManager.Instance.PlayerEntity;
                        if (player != null && player.ItemEquipTable.GetItem(Items.EquipSlots.Feet) == null)
                            foreach (var item in player.Items.CloneAll())
                                if (item.EquipSlot == Items.EquipSlots.Feet) { player.ItemEquipTable.EquipItem(item, true, false); Debug.Log("[DebugStart] equipped " + item.LongName); break; }
                    }
                    catch (System.Exception ex) { Debug.Log("[DebugStart] footwear: " + ex.Message); }
                    Shader sh = Shader.Find("Daggerfall/Default");
                    Debug.Log("[DebugStart] popped UI to HUD; Daggerfall/Default " + (sh ? "found supported=" + sh.isSupported + " id=" + sh.GetInstanceID() : "MISSING")
                        + " captured id=" + (MobileShaders.Find("Daggerfall/Default") ? MobileShaders.Find("Daggerfall/Default").GetInstanceID() : 0));
                    LogRenderState("after start");
                }
            }

            float lastWaitLog;

            void AfterStart()
            {
                var gm = GameManager.Instance;
                var sw = gm.StreamingWorld;
                if (gm.PlayerEntity == null || gm.PlayerEnterExit == null || gm.PlayerEnterExit.IsPlayerInside || sw == null || sw.IsInit || !sw.IsReady)   // IsInit means "init still pending"
                {
                    // Quest mods greet a new character with message boxes; the world does not init while paused.
                    var top = DaggerfallUI.UIManager.TopWindow as DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallMessageBox;
                    if (top != null && Time.realtimeSinceStartup - lastWaitLog > 1f) { lastWaitLog = Time.realtimeSinceStartup - 9f; top.CloseWindow(); Debug.Log("[DebugStart] dismissed a message box"); return; }
                    if (Time.realtimeSinceStartup - lastWaitLog > 10f)
                    {
                        lastWaitLog = Time.realtimeSinceStartup;
                        Debug.Log("[DebugStart] waiting: entity=" + (gm.PlayerEntity != null) + " enterExit=" + (gm.PlayerEnterExit != null)
                            + " inside=" + (gm.PlayerEnterExit != null && gm.PlayerEnterExit.IsPlayerInside) + " sw=" + (sw != null)
                            + " init=" + (sw != null && sw.IsInit) + " ready=" + (sw != null && sw.IsReady)
                            + " pixel=" + gm.PlayerGPS.CurrentMapPixel.X + "," + gm.PlayerGPS.CurrentMapPixel.Y
                            + " paused=" + GameManager.IsGamePaused + " top=" + (DaggerfallUI.UIManager.TopWindow != null ? DaggerfallUI.UIManager.TopWindow.GetType().Name : "none"));
                    }
                    return;
                }
                if (!teleported)
                {
                    teleported = true;
                    teleportedAt = Time.realtimeSinceStartup;
                    if (targetX >= 0)
                    {
                        Debug.Log("[DebugStart] teleporting to map pixel " + targetX + "," + targetY);
                        sw.TeleportToCoordinates(targetX, targetY, StreamingWorld.RepositionMethods.DirectionFromStartMarker);
                    }
                    return;
                }
                if (sw.IsRepositioningPlayer || Time.realtimeSinceStartup - teleportedAt < 10f) return;
                var loc = sw.CurrentPlayerLocationObject;
                if (loc == null)
                {
                    if (Time.realtimeSinceStartup - teleportedAt > 60f) { done = true; worldReadyAt = Time.realtimeSinceStartup; Debug.Log("[DebugStart] no location object after 60 s at pixel " + gm.PlayerGPS.CurrentMapPixel.X + "," + gm.PlayerGPS.CurrentMapPixel.Y); }
                    return;
                }
                done = true;
                worldReadyAt = Time.realtimeSinceStartup;
                LogRenderState("at location");
                try { AuditLocation(loc.gameObject, gm.PlayerGPS.HasCurrentLocation ? gm.PlayerGPS.CurrentLocation.Name : "?"); }
                catch (System.Exception ex) { Debug.LogError("[DebugStart] location audit threw: " + ex); }
            }
        }

        static void LogRenderState(string when)
        {
            var sb = new StringBuilder();
            sb.Append("[DebugStart] render ").Append(when).Append(": fog=").Append(RenderSettings.fog).Append(' ').Append(RenderSettings.fogMode)
              .Append(" color=").Append(RenderSettings.fogColor).Append(" density=").Append(RenderSettings.fogDensity)
              .Append(" start/end=").Append(RenderSettings.fogStartDistance).Append('/').Append(RenderSettings.fogEndDistance)
              .Append(" ambient=").Append(RenderSettings.ambientMode).Append(' ').Append(RenderSettings.ambientLight).Append(" x").Append(RenderSettings.ambientIntensity);
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) sb.Append(" sun[").Append(l.name).Append(" on=").Append(l.enabled).Append(" i=").Append(l.intensity).Append(' ').Append(l.color).Append(']');
            Camera cam = Camera.main;
            if (cam) sb.Append(" cam clear=").Append(cam.clearFlags).Append(" bg=").Append(cam.backgroundColor).Append(" far=").Append(cam.farClipPlane);
            var mm = DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance;
            if (mm != null) { sb.Append(" mods=").Append(mm.LoadedModCount).Append(':'); foreach (var m in mm.GetAllMods()) sb.Append(' ').Append(m.Title).Append(m.Enabled ? "" : "(off)"); }
            Debug.Log(sb.ToString());
        }

        /// <summary>Renderer-level summary of a built location, the same numbers the editor town probe reports.</summary>
        static void AuditLocation(GameObject loc, string locName)
        {
            int renderers = 0, replacements = 0, nullSlots = 0, noTexture = 0, fallbackMat = 0, missingScripts = 0, rtm = 0, rtmNotApplied = 0;
            var formats = new Dictionary<string, int>();
            var untextured = new Dictionary<string, int>();
            var applied = typeof(DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials).GetField("hasAppliedMaterials",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (MeshRenderer mr in loc.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                if (mr.gameObject.name.Contains("[Replacement]")) replacements++;
                foreach (Component c in mr.gameObject.GetComponents<Component>()) if (c == null) missingScripts++;
                var r = mr.GetComponent<DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials>();
                if (r != null) { rtm++; if (applied != null && !(bool)applied.GetValue(r)) rtmNotApplied++; }
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string key;
                    if (mats[i] == null) { nullSlots++; key = mr.gameObject.name + " slot" + i + " NULL"; }
                    else
                    {
                        if (mats[i].name.StartsWith("Default-Material")) fallbackMat++;
                        Texture t = mats[i].mainTexture;
                        if (t != null)
                        {
                            string fmt = t is Texture2D ? ((Texture2D)t).format.ToString() : t.GetType().Name;
                            int n; formats.TryGetValue(fmt, out n); formats[fmt] = n + 1;
                            continue;
                        }
                        noTexture++;
                        key = mr.gameObject.name + " mat '" + mats[i].name + "' sh=" + (mats[i].shader ? mats[i].shader.name : "null") + " NO-TEX";
                    }
                    int c; untextured.TryGetValue(key, out c); untextured[key] = c + 1;
                }
            }
            var sb = new StringBuilder();
            foreach (var kv in formats) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log(string.Format("[DebugStart] LOCATION '{0}' renderers={1} replacements={2} nullSlots={3} noTexture={4} fallbackMaterial={5} missingScripts={6} runtimeMaterials={7} notApplied={8} formats: {9}",
                locName, renderers, replacements, nullSlots, noTexture, fallbackMat, missingScripts, rtm, rtmNotApplied, sb));
            int shown = 0;
            foreach (var kv in untextured) { if (shown++ >= 25) break; Debug.Log("[DebugStart]   untextured x" + kv.Value + " " + kv.Key); }
        }

#endif

        /// <summary>One log line per distinct model describing every material slot.</summary>
        public static void Audit(string label, GameObject go, int cap)
        {
            if (!Active || go == null || audited.Count > cap * 4 || !audited.Add(label)) return;
            var sb = new StringBuilder();
            sb.Append("[DebugStart] ").Append(label).Append(':');
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = mr.sharedMaterials;
                sb.Append(" [").Append(mr.gameObject.name).Append(" slots=").Append(mats.Length);
                sb.Append(mr.GetComponent<DaggerfallWorkshop.Utility.AssetInjection.RuntimeMaterials>() ? " rtm" : "");
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) { sb.Append(" NULL"); continue; }
                    sb.Append(" {").Append(m.name).Append(" sh=").Append(m.shader ? m.shader.name + (m.shader.isSupported ? "" : "!unsupported") : "null");
                    Texture tex = m.mainTexture;
                    if (tex == null) sb.Append(" NO-TEX");
                    else sb.Append(" tex=").Append(tex.name).Append(' ').Append(tex.width).Append('x').Append(tex.height).Append(tex is Texture2D ? " " + ((Texture2D)tex).format : "");
                    if (m.shaderKeywords.Length > 0) sb.Append(" kw=").Append(string.Join(",", m.shaderKeywords));
                    sb.Append('}');
                }
                sb.Append(']');
            }
            Debug.Log(sb.ToString());
        }
    }
}
