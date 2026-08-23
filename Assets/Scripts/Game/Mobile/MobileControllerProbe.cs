// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Guided on-device probe that discovers what Unity's legacy Input actually reports for
//   a given controller on iPadOS.
//
//   Why this exists: Unity's joystick button numbering and - much worse - its axis
//   numbering for triggers and d-pads vary by controller model AND by OS. Published
//   tables for "Xbox on iOS" or "MFi" contradict each other and each other's OS
//   versions. The only trustworthy source is the device in the player's hands.
//
//   Why it draws on screen instead of logging: reading Debug.Log from a device needs the
//   iPad cabled to the Mac (xcrun devicectl --console). A sideloaded tester is usually
//   nowhere near the Mac. So the probe names a control, waits for the player to press it,
//   records whatever Unity reported, and finishes on a summary page that can be
//   screenshotted and sent back. Everything is ALSO written to Debug.Log, so a cabled
//   run loses nothing.
//
//   Why raw Input.touches for its own buttons: UGUI pointer events are dead on iPadOS in
//   this project (see REVIEW.md) and iPadOS reports a permanently-held phantom mouse
//   button, which makes GUI.Button fire on its own. Hit-testing Input.touches by hand is
//   the only reliable route, so that is what the SKIP/BACK/REDO controls use.
//
//   Axis-to-keycode maths mirrors InputManager exactly (startingAxisKeyCode = 5000, two
//   synthetic keycodes per axis, even = positive direction, odd = negative), so the
//   summary prints the literal keycode and KeyBinds.txt string a binding needs.
//

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileControllerProbe : MonoBehaviour
    {
        #region Fields

        public const int NumAxes = 16;                 // InputManager.numAxes is private; same value.
        const int numButtons = 20;                     // JoystickButton0..19
        const float axisTrigger = 0.5f;                // deviation from rest that counts as "pressed"
        const float axisIdle = 0.2f;                   // deviation below which we call it released
        const int calibrationFrames = 30;

        // A candidate must persist this many consecutive frames before it is recorded.
        // iPadOS pulses joystick-button state during touches, and on the first device run a
        // stray JoystickButton0 pulse consumed the Start and Select prompts before the real
        // buttons were pressed - Select came back as Btn0 with no way to tell whether that was
        // the button or the phantom. A real press lasts far longer than a pulse.
        const int holdFramesToRecord = 8;

        [Tooltip("Draw the probe overlay. MobileHudBuilder serializes this as true when the " +
                 "HUD is built with DFU_IOS_PROBE=1; otherwise toggle it from TUNE at runtime.")]
        public bool active;

        /// <summary>
        /// Controls to walk through, in the order the player is asked for them. Names match
        /// the mapping tables in README-iOS.md so the answers can be pasted straight in.
        /// </summary>
        static readonly string[] prompts =
        {
            "A  (bottom face button)",
            "B  (right face button)",
            "X  (left face button)",
            "Y  (top face button)",
            "LB (left shoulder)",
            "RB (right shoulder)",
            "LT (left trigger - press FULLY)",
            "RT (right trigger - press FULLY)",
            "L3 (click the LEFT stick in)",
            "R3 (click the RIGHT stick in)",
            "START / MENU",
            "SELECT / VIEW / BACK",
            "D-PAD UP",
            "D-PAD DOWN",
            "D-PAD LEFT",
            "D-PAD RIGHT",
        };

        static readonly string[] shortNames =
        {
            "A", "B", "X", "Y", "LB", "RB", "LT", "RT",
            "L3", "R3", "Start", "Select", "DUp", "DDown", "DLeft", "DRight",
        };

        enum Stage { Idle, Calibrating, Prompting, Summary }

        Stage stage = Stage.Idle;
        int index;
        int calibrationCounter;

        readonly float[] axisRest = new float[NumAxes + 1];
        readonly float[] axisPeak = new float[NumAxes + 1];
        readonly float[] axisMin = new float[NumAxes + 1];
        readonly float[] axisMax = new float[NumAxes + 1];

        // One result per prompt. A control can legitimately report more than one thing at
        // once (some d-pads fire a button AND an axis), so every hit is kept.
        readonly List<string>[] results = new List<string>[prompts.Length];

        bool waitingForIdle;
        string candidateKey = "";
        int candidateFrames;
        string liveHits = "";
        string joystickNames = "";

        Rect skipRect, backRect, redoRect, recalRect, hideRect;
        bool collapsed;

        /// <summary>
        /// True while a probe overlay is running anywhere. MobileGamepadBindings checks this
        /// and stands down: a guessed trigger axis that actually rests at -1 reads as
        /// permanently pressed, which would pin an action on and corrupt the very readings
        /// the probe exists to take.
        /// </summary>
        public static bool AnyActive { get; private set; }
        GUIStyle bodyStyle, titleStyle, buttonStyle;
        int lastTouchFrame = -10;

        #endregion

        #region Unity

        void Awake()
        {
            for (int i = 0; i < results.Length; i++)
                results[i] = new List<string>();
        }

        void OnEnable()
        {
            AnyActive = active;

            if (active && stage == Stage.Idle)
                BeginCalibration();
        }

        void OnDisable()
        {
            AnyActive = false;
        }

        void Update()
        {
            AnyActive = active;

            if (!active)
                return;

            if (stage == Stage.Idle)
                BeginCalibration();

            // The probe itself does not need EnableController - it reads Input.GetAxisRaw
            // directly, which is unconditional. But the bindings this probe exists to
            // produce DO need it (InputManager.GetAxisKey early-returns without it), so
            // match the real conditions while a controller is actually attached.
            //
            // Only while attached, deliberately. EnableController also gates the
            // joystick-button-to-mouse-click path (InputManager.GetMouseButtonDown and
            // friends), and iPadOS pulses joystick-button state during touches - forcing it
            // on with no controller present would put phantom clicks back into the classic
            // UI, which is the exact bug this port already fought once.
            if (InputManager.HasInstance && ControllerAttached() &&
                !InputManager.Instance.EnableController)
            {
                InputManager.Instance.EnableController = true;
            }

            TrackAxisExtremes();
            PollProbeTouchButtons();

            // Collapsed means the player has gone back to the game. Recording would burn
            // through the remaining prompts on whatever they pressed to play.
            if (collapsed)
                return;

            switch (stage)
            {
                case Stage.Calibrating: StepCalibration(); break;
                case Stage.Prompting:   StepPrompting();   break;
            }
        }

        #endregion

        #region Probe logic

        void BeginCalibration()
        {
            stage = Stage.Calibrating;
            calibrationCounter = 0;
            index = 0;
            waitingForIdle = false;

            for (int a = 1; a <= NumAxes; a++)
            {
                axisRest[a] = 0f;
                axisPeak[a] = 0f;
                axisMin[a] = 0f;
                axisMax[a] = 0f;
            }
            for (int i = 0; i < results.Length; i++)
                results[i].Clear();

            joystickNames = DescribeJoysticks();
            Debug.Log("[MobileProbe] calibrating. joysticks: " + joystickNames);
        }

        /// <summary>
        /// Records each axis' resting value. Triggers are the reason this exists: some
        /// controllers rest a trigger at 0 and travel to +1, others rest at -1 and travel
        /// to +1. Without the baseline, a trigger resting at -1 looks permanently pressed -
        /// and binding the negative direction of that axis would pin an action on forever.
        /// </summary>
        void StepCalibration()
        {
            for (int a = 1; a <= NumAxes; a++)
                axisRest[a] += Read(a);

            if (++calibrationCounter < calibrationFrames)
                return;

            var log = new StringBuilder("[MobileProbe] resting axis values:");
            for (int a = 1; a <= NumAxes; a++)
            {
                axisRest[a] /= calibrationFrames;
                axisMin[a] = axisRest[a];
                axisMax[a] = axisRest[a];
                if (Mathf.Abs(axisRest[a]) > 0.05f)
                    log.Append(string.Format("  Axis{0}={1:0.00}", a, axisRest[a]));
            }
            Debug.Log(log.ToString());

            stage = Stage.Prompting;
            index = 0;
            waitingForIdle = true;   // don't capture a press that was already down
        }

        void StepPrompting()
        {
            if (index >= prompts.Length)
            {
                Finish();
                return;
            }

            List<string> hits = CollectHits();
            liveHits = hits.Count == 0 ? "" : string.Join("  ", hits.ToArray());

            // Require a clean release between prompts, or one long press would blow through
            // the whole list and every control would report the same thing.
            if (waitingForIdle)
            {
                if (hits.Count == 0)
                {
                    waitingForIdle = false;
                    candidateKey = "";
                    candidateFrames = 0;
                }
                return;
            }

            if (hits.Count == 0)
            {
                candidateKey = "";
                candidateFrames = 0;
                return;
            }

            // Debounce: only record once the same set of inputs has been held steady.
            string key = string.Join(" ", hits.ToArray());
            if (key != candidateKey)
            {
                candidateKey = key;
                candidateFrames = 1;
                return;
            }

            if (++candidateFrames < holdFramesToRecord)
                return;

            results[index].Clear();
            results[index].AddRange(hits);
            Debug.Log(string.Format("[MobileProbe] {0} -> {1}", shortNames[index],
                                    string.Join(", ", hits.ToArray())));

            index++;
            waitingForIdle = true;
            candidateKey = "";
            candidateFrames = 0;

            if (index >= prompts.Length)
                Finish();
        }

        /// <summary>Every button and axis direction currently reading as pressed.</summary>
        List<string> CollectHits()
        {
            var hits = new List<string>();

            for (int b = 0; b < numButtons; b++)
            {
                if (Input.GetKey(KeyCode.JoystickButton0 + b))
                    hits.Add("Btn" + b);
            }

            for (int a = 1; a <= NumAxes; a++)
            {
                float dev = Read(a) - axisRest[a];
                if (dev > axisTrigger)
                    hits.Add("Axis" + a + "+");
                else if (dev < -axisTrigger)
                    hits.Add("Axis" + a + "-");
            }

            return hits;
        }

        void TrackAxisExtremes()
        {
            if (stage == Stage.Calibrating)
                return;

            for (int a = 1; a <= NumAxes; a++)
            {
                float v = Read(a);
                if (v < axisMin[a]) axisMin[a] = v;
                if (v > axisMax[a]) axisMax[a] = v;
                if (Mathf.Abs(v - axisRest[a]) > Mathf.Abs(axisPeak[a] - axisRest[a]))
                    axisPeak[a] = v;
            }
        }

        static float Read(int axis)
        {
            return Input.GetAxisRaw("Axis" + axis);
        }

        void Finish()
        {
            stage = Stage.Summary;
            Debug.Log(BuildReport());
        }

        #endregion

        #region Report

        /// <summary>
        /// The deliverable. Prints, per control, what Unity reported and - for axes - the
        /// synthetic keycode and KeyBinds.txt string InputManager needs to bind it.
        /// </summary>
        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DFU iOS CONTROLLER PROBE RESULT ===");
            sb.AppendLine("joysticks: " + joystickNames);
            sb.AppendLine("unity: " + Application.unityVersion + "  os: " + SystemInfo.operatingSystem);
            sb.AppendLine();

            for (int i = 0; i < prompts.Length; i++)
            {
                string hits = results[i].Count == 0 ? "(none)" : string.Join(" ", results[i].ToArray());
                sb.AppendLine(string.Format("{0,-7} {1}", shortNames[i], hits));
            }

            sb.AppendLine();
            sb.AppendLine("axis detail (rest / min / max -> keycode):");
            for (int a = 1; a <= NumAxes; a++)
            {
                bool moved = Mathf.Abs(axisMax[a] - axisMin[a]) > 0.25f;
                if (!moved && Mathf.Abs(axisRest[a]) < 0.05f)
                    continue;

                sb.AppendLine(string.Format(
                    "  Axis{0,-2} rest {1,5:0.00}  min {2,5:0.00}  max {3,5:0.00}   pos={4} ({5})  neg={6} ({7})",
                    a, axisRest[a], axisMin[a], axisMax[a],
                    AxisKeyCode(a, true), AxisKeyString(a, true),
                    AxisKeyCode(a, false), AxisKeyString(a, false)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Mirrors InputManager: startingAxisKeyCode + two codes per axis, even code =
        /// positive direction, odd = negative.
        /// </summary>
        public static int AxisKeyCode(int axis, bool positive)
        {
            return InputManager.startingAxisKeyCode + (axis - 1) * 2 + (positive ? 0 : 1);
        }

        /// <summary>The string form KeyBinds.txt uses, e.g. "JoystickAxis6Button0".</summary>
        public static string AxisKeyString(int axis, bool positive)
        {
            return string.Format("JoystickAxis{0}Button{1}", axis, positive ? 0 : 1);
        }

        static bool ControllerAttached()
        {
            string[] names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i]))
                    return true;

            return false;
        }

        static string DescribeJoysticks()
        {
            string[] names = Input.GetJoystickNames();
            var live = new List<string>();
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i]))
                    live.Add((i + 1) + ":" + names[i]);

            return live.Count == 0 ? "(NONE CONNECTED)" : string.Join("  ", live.ToArray());
        }

        #endregion

        #region Probe's own touch buttons

        /// <summary>
        /// Raw-touch hit testing. GUI.Button is unusable here: iPadOS reports a phantom
        /// held mouse button, so IMGUI buttons under the cursor latch on by themselves.
        /// Touch positions are bottom-up, GUI rects are top-down, hence the y flip.
        /// </summary>
        void PollProbeTouchButtons()
        {
            if (Input.touchCount == 0)
                return;

            // One action per tap, and only on the frame the finger lands.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began)
                    continue;
                if (Time.frameCount - lastTouchFrame < 10)
                    continue;

                Vector2 p = new Vector2(t.position.x, Screen.height - t.position.y);

                if (skipRect.Contains(p))
                {
                    lastTouchFrame = Time.frameCount;
                    if (stage == Stage.Prompting && index < prompts.Length)
                    {
                        results[index].Clear();
                        Debug.Log("[MobileProbe] " + shortNames[index] + " -> SKIPPED");
                        index++;
                        waitingForIdle = true;
                        if (index >= prompts.Length)
                            Finish();
                    }
                }
                else if (backRect.Contains(p))
                {
                    lastTouchFrame = Time.frameCount;
                    if (index > 0)
                    {
                        index--;
                        results[index].Clear();
                        stage = Stage.Prompting;
                        waitingForIdle = true;
                    }
                }
                else if (redoRect.Contains(p))
                {
                    lastTouchFrame = Time.frameCount;
                    BeginCalibration();
                }
                else if (recalRect.Contains(p))
                {
                    lastTouchFrame = Time.frameCount;
                    stage = Stage.Calibrating;
                    calibrationCounter = 0;
                    for (int a = 1; a <= NumAxes; a++)
                        axisRest[a] = 0f;
                }
                else if (hideRect.Contains(p))
                {
                    lastTouchFrame = Time.frameCount;
                    collapsed = !collapsed;
                }
            }
        }

        #endregion

        #region Drawing

        void EnsureStyles()
        {
            int baseSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 52f));

            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label);
                bodyStyle.wordWrap = false;
            }
            bodyStyle.fontSize = baseSize;
            bodyStyle.normal.textColor = Color.white;

            if (titleStyle == null)
                titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = Mathf.RoundToInt(baseSize * 1.9f);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            if (buttonStyle == null)
                buttonStyle = new GUIStyle(GUI.skin.label);
            buttonStyle.fontSize = Mathf.RoundToInt(baseSize * 1.2f);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (!active)
                return;

            EnsureStyles();

            float w = Screen.width;
            float h = Screen.height;
            float pad = w * 0.04f;

            // Collapsed leaves just a tab. Without a way back to the game this build would
            // be a dead end - the overlay is deliberately near-opaque, and it covers the
            // save/load menu too.
            if (collapsed)
            {
                hideRect = new Rect(w - pad - w * 0.16f, pad * 0.4f, w * 0.16f, Mathf.Max(52f, h * 0.07f));
                DrawTouchButton(hideRect, "PROBE");
                skipRect = backRect = redoRect = recalRect = new Rect();
                return;
            }

            // Opaque backdrop: this is a diagnostic, legibility beats subtlety.
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            GUI.color = prev;

            float y = pad;
            GUI.Label(new Rect(pad, y, w - pad * 2f, h), "DFU iOS - CONTROLLER PROBE", titleStyle);
            y += titleStyle.fontSize * 1.6f;

            GUI.Label(new Rect(pad, y, w - pad * 2f, h), "controllers: " + joystickNames, bodyStyle);
            y += bodyStyle.fontSize * 2f;

            switch (stage)
            {
                case Stage.Calibrating:
                    GUI.Label(new Rect(pad, y, w - pad * 2f, h),
                              "Calibrating - LET GO OF EVERYTHING for a moment...", bodyStyle);
                    break;

                case Stage.Prompting:
                    DrawPrompting(pad, ref y, w, h);
                    break;

                case Stage.Summary:
                    DrawSummary(pad, ref y, w, h);
                    break;
            }

            DrawTouchButtons(w, h, pad);
        }

        void DrawPrompting(float pad, ref float y, float w, float h)
        {
            GUI.Label(new Rect(pad, y, w - pad * 2f, h),
                      string.Format("Step {0} of {1}", index + 1, prompts.Length), bodyStyle);
            y += bodyStyle.fontSize * 1.6f;

            GUIStyle big = new GUIStyle(titleStyle);
            big.normal.textColor = new Color(0.5f, 1f, 0.6f);
            GUI.Label(new Rect(pad, y, w - pad * 2f, h), "PRESS:  " + prompts[index], big);
            y += big.fontSize * 1.8f;

            if (waitingForIdle && liveHits.Length > 0)
                GUI.Label(new Rect(pad, y, w - pad * 2f, h), "release to continue...", bodyStyle);
            else
                GUI.Label(new Rect(pad, y, w - pad * 2f, h),
                          liveHits.Length > 0
                              ? "reading: " + liveHits + "   (hold " +
                                Mathf.Max(0, holdFramesToRecord - candidateFrames) + ")"
                              : "waiting...", bodyStyle);
            y += bodyStyle.fontSize * 2.2f;

            // Everything captured so far, so a wrong entry is obvious immediately.
            for (int i = 0; i < index; i++)
            {
                string hits = results[i].Count == 0 ? "(skipped)" : string.Join(" ", results[i].ToArray());
                GUI.Label(new Rect(pad, y, w - pad * 2f, h),
                          string.Format("  {0,-7} {1}", shortNames[i], hits), bodyStyle);
                y += bodyStyle.fontSize * 1.25f;
            }
        }

        void DrawSummary(float pad, ref float y, float w, float h)
        {
            GUIStyle big = new GUIStyle(titleStyle);
            big.normal.textColor = new Color(0.5f, 1f, 0.6f);
            GUI.Label(new Rect(pad, y, w - pad * 2f, h), "DONE - screenshot this page", big);
            y += big.fontSize * 1.5f;

            // Two columns: 16 controls plus axis detail does not fit one column on a phone
            // and only just fits on a pad.
            float colW = (w - pad * 2f) * 0.5f;
            float leftY = y;
            for (int i = 0; i < prompts.Length; i++)
            {
                string hits = results[i].Count == 0 ? "(none)" : string.Join(" ", results[i].ToArray());
                GUI.Label(new Rect(pad, leftY, colW, h),
                          string.Format("{0,-7} {1}", shortNames[i], hits), bodyStyle);
                leftY += bodyStyle.fontSize * 1.3f;
            }

            float rightY = y;
            GUI.Label(new Rect(pad + colW, rightY, colW, h), "axis  rest   min    max", bodyStyle);
            rightY += bodyStyle.fontSize * 1.4f;
            for (int a = 1; a <= NumAxes; a++)
            {
                bool moved = Mathf.Abs(axisMax[a] - axisMin[a]) > 0.25f;
                if (!moved && Mathf.Abs(axisRest[a]) < 0.05f)
                    continue;

                GUI.Label(new Rect(pad + colW, rightY, colW, h), string.Format(
                    "{0,-5} {1,5:0.00} {2,6:0.00} {3,6:0.00}", "Axis" + a,
                    axisRest[a], axisMin[a], axisMax[a]), bodyStyle);
                rightY += bodyStyle.fontSize * 1.3f;
            }

            y = Mathf.Max(leftY, rightY);
        }

        void DrawTouchButtons(float w, float h, float pad)
        {
            const int columns = 5;
            float gap = pad * 0.33f;
            float bw = (w - pad * 2f - gap * (columns - 1)) / columns;
            float bh = Mathf.Max(64f, h * 0.09f);
            float by = h - bh - pad;

            skipRect  = new Rect(pad + (bw + gap) * 0f, by, bw, bh);
            backRect  = new Rect(pad + (bw + gap) * 1f, by, bw, bh);
            recalRect = new Rect(pad + (bw + gap) * 2f, by, bw, bh);
            redoRect  = new Rect(pad + (bw + gap) * 3f, by, bw, bh);
            hideRect  = new Rect(pad + (bw + gap) * 4f, by, bw, bh);

            DrawTouchButton(skipRect,  "SKIP");
            DrawTouchButton(backRect,  "BACK");
            DrawTouchButton(recalRect, "RE-ZERO");
            DrawTouchButton(redoRect,  "RESTART");
            DrawTouchButton(hideRect,  "HIDE");
        }

        void DrawTouchButton(Rect r, string label)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.22f, 0.24f, 0.30f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(r, label, buttonStyle);
        }

        #endregion
    }
}
