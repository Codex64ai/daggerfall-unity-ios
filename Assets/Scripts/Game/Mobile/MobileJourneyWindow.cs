// Project:         Daggerfall Unity iOS touch port
// Copyright:       Copyright (c) 2009-2023 Daggerfall Workshop
// License:         MIT License (LICENSE file)
//
// Derived from Tedious Travel by TheNewBob / Jedidia, used under the MIT License:
//     MIT License, Copyright (c) 2018 TheNewBob
//     https://github.com/TheNewBob/TediousTravel
//
// Adapted for this port: touch-sized controls rather than the original's 20x10 classic
// pixels, arrival estimate shown, and the window's own lifetime is the journey's - closing
// it interrupts travel, so there is no way to leave a journey running with no way to stop it.

using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// The overlay shown while walking to a destination: where you are going, when you will
    /// get there, and the controls to speed up, slow down or stop.
    ///
    /// WHY THIS IS A CLASSIC WINDOW AND NOT A UGUI OVERLAY
    /// It has to look like Daggerfall, and it has to be reachable by both a thumb and a
    /// controller. A DaggerfallPopupWindow gets the classic look, the UI stack, and this
    /// port's existing virtual-cursor and controller UI-click routing for free. A hand-rolled
    /// UGUI panel would need all three rebuilt and would still look foreign.
    ///
    /// The controls are deliberately much larger than the original mod's. Its buttons were
    /// 20x10 in classic 320x200 space, which is a fine mouse target and a poor thumb one.
    /// </summary>
    public class MobileJourneyWindow : DaggerfallPopupWindow
    {
        // Classic 320x200 space. The bar sits at the top so it does not cover the horizon,
        // which is the part of the view worth watching while travelling.
        Rect panelRect = new Rect(0, 0, 320, 50);   // 20 text row + 26 buttons + padding

        // 26 tall, against this window's first pass at 22 and the original mod's 10. The speed
        // controls are also wider and pushed apart from the MAP/STOP pair: in the device
        // screenshot the two groups floated with a large dead gap between them, which read as
        // two unrelated sets of controls rather than one bar.
        Rect slowerRect = new Rect(6, 20, 40, 26);
        Rect speedRect = new Rect(48, 20, 44, 26);
        Rect fasterRect = new Rect(94, 20, 40, 26);
        Rect mapRect = new Rect(186, 20, 60, 26);
        Rect stopRect = new Rect(250, 20, 64, 26);

        /// <summary>
        /// On the UI stack right now. The base window type exposes no such flag, so it is
        /// tracked from the push/pop pair - the controller needs it to avoid closing a window
        /// that has already gone.
        /// </summary>
        public bool IsShowing { get; private set; }

        Panel mainPanel;
        TextLabel destinationLabel;
        TextLabel clockLabel;
        TextLabel speedLabel;
        TextLabel diagLabel;

        // Near-opaque on device evidence. At 0.55 / 0.85 the snowy landscape read straight
        // through the bar and the buttons looked like windows onto the scene rather than
        // controls - the MAP button in particular appeared to have trees inside it. A HUD
        // element the player has to hit in a hurry needs to be legible against ANY biome, so
        // it stops trying to be subtle.
        readonly Color panelBackground = new Color(0.03f, 0.025f, 0.02f, 0.92f);
        readonly Color buttonBackground = new Color(0.16f, 0.13f, 0.09f, 1f);
        readonly Color stopBackground = new Color(0.42f, 0.07f, 0.05f, 1f);

        public MobileJourneyWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
            // The world must stay visible and running - the point of the feature is watching
            // the journey happen. A pausing, screen-filling window would defeat it.
            ParentPanel.BackgroundColor = Color.clear;
            pauseWhileOpened = false;
        }

        protected override void Setup()
        {
            base.Setup();

            mainPanel = DaggerfallUI.AddPanel(panelRect, NativePanel);
            mainPanel.BackgroundColor = panelBackground;

            destinationLabel = DaggerfallUI.AddTextLabel(
                DaggerfallUI.DefaultFont, new Vector2(4, 3), string.Empty, mainPanel);
            clockLabel = DaggerfallUI.AddTextLabel(
                DaggerfallUI.DefaultFont, new Vector2(170, 3), string.Empty, mainPanel);

            // What the journey is actually doing: speed in effect, whether terrain is still
            // building, measured ground speed, and road progress. Shown only when TUNE's
            // "Show diagnostics" is on - it is a developer readout, and leaving it permanently
            // across a player-facing bar is how a debug string ends up in a release.
            diagLabel = DaggerfallUI.AddTextLabel(
                DaggerfallUI.DefaultFont, new Vector2(140, 27), string.Empty, mainPanel);

            Button slower = DaggerfallUI.AddTextButton(slowerRect, "-", mainPanel);
            slower.BackgroundColor = buttonBackground;
            slower.OnMouseClick += (s, p) => StepCompression(-1);

            Panel speedPanel = DaggerfallUI.AddPanel(speedRect, mainPanel);
            speedPanel.BackgroundColor = buttonBackground;
            speedLabel = DaggerfallUI.AddTextLabel(
                DaggerfallUI.DefaultFont, new Vector2(4, 7), string.Empty, speedPanel);

            Button faster = DaggerfallUI.AddTextButton(fasterRect, "+", mainPanel);
            faster.BackgroundColor = buttonBackground;
            faster.OnMouseClick += (s, p) => StepCompression(1);

            Button map = DaggerfallUI.AddTextButton(mapRect, "MAP", mainPanel);
            map.BackgroundColor = buttonBackground;
            map.OnMouseClick += (s, p) => ShowTravelMap();

            // Red, and the widest target on the bar. Stopping is the one control a player
            // reaches for in a hurry, usually because something is attacking them.
            Button stop = DaggerfallUI.AddTextButton(stopRect, "STOP", mainPanel);
            stop.BackgroundColor = stopBackground;
            stop.OnMouseClick += (s, p) => CloseWindow();

            RefreshLabels();
        }

        public override void OnPush()
        {
            base.OnPush();
            IsShowing = true;
            RefreshLabels();
        }

        public override void OnPop()
        {
            base.OnPop();
            IsShowing = false;

            // THE WINDOW'S LIFETIME IS THE JOURNEY'S.
            // Any route out of this window - the STOP button, a controller Back press, or the
            // controller stopping travel itself - ends up here. Treating a closed window as an
            // interrupt means there is no state where the player is being walked across the
            // Iliac Bay with no visible way to stop.
            if (MobileJourneyController.HasInstance && MobileJourneyController.Instance.IsTravelling)
                MobileJourneyController.Instance.Stop(MobileJourneyController.JourneyEnd.Interrupted);
        }

        public override void Update()
        {
            base.Update();

            // Travel ended for a reason of its own - arrival, an encounter, a disease. Close
            // so the bar does not linger over a game that is no longer travelling.
            if (!MobileJourneyController.HasInstance || !MobileJourneyController.Instance.IsTravelling)
            {
                CloseWindow();
                return;
            }

            RefreshLabels();
        }

        void RefreshLabels()
        {
            if (destinationLabel == null)
                return;

            MobileJourneyController journey =
                MobileJourneyController.HasInstance ? MobileJourneyController.Instance : null;

            string name = (journey != null && !string.IsNullOrEmpty(journey.DestinationName))
                ? journey.DestinationName : "your destination";
            destinationLabel.Text = "Travelling to " + name;

            if (DaggerfallUnity.Instance.WorldTime != null)
                clockLabel.Text = DaggerfallUnity.Instance.WorldTime.Now.MidDateTimeString();

            if (journey != null)
            {
                speedLabel.Text = journey.TimeCompression + "x";

                bool diagnostics = MobileInputController.Instance != null &&
                                   MobileInputController.Instance.showGestureDebug;

                diagLabel.Text = diagnostics
                    ? string.Format("run {0}x  {1}  {2:0} u/s  {3}",
                        journey.ActiveCompression,
                        journey.TerrainBuilding ? "TERRAIN" : "ready",
                        journey.MeasuredSpeed,
                        journey.FollowingRoad ? "road " + journey.RouteRemaining : "direct")

                    // Not nothing: following a road is worth telling the player about, because
                    // it explains why a journey is not heading straight at its destination.
                    : (journey.FollowingRoad ? "Following the road" : string.Empty);
            }
        }

        // Named speeds rather than a fixed increment. A +5 step needed six taps to get from
        // the default to the maximum, on a control the player uses while travelling; these are
        // three taps end to end and every stop is a round number they can reason about.
        // The ceiling depends on the transport (50x foot, 150x mount, 200x ship);
        // SetTimeCompression clamps to it, so the button simply stops climbing.
        static readonly int[] speedSteps = { 1, 5, 10, 20, 30, 50, 100, 200 };

        void StepCompression(int direction)
        {
            if (!MobileJourneyController.HasInstance)
                return;

            MobileJourneyController journey = MobileJourneyController.Instance;
            int current = journey.TimeCompression;

            // Nearest step to where we are, then move one along. Starting from the nearest
            // rather than an index keeps this correct even if the speed was set elsewhere.
            int nearest = 0;
            for (int i = 1; i < speedSteps.Length; i++)
            {
                if (Mathf.Abs(speedSteps[i] - current) < Mathf.Abs(speedSteps[nearest] - current))
                    nearest = i;
            }

            int target = Mathf.Clamp(nearest + direction, 0, speedSteps.Length - 1);
            journey.SetTimeCompression(speedSteps[target]);
            RefreshLabels();
        }

        void ShowTravelMap()
        {
            // MAP NO LONGER ENDS THE JOURNEY.
            //
            // It used to close this bar first and then push the map, which stopped travel and
            // frequently showed no map at all. Glancing at where you are should not cancel a
            // three-day trip anyway - so the map is pushed ON TOP of this bar, which stays on
            // the stack underneath. The map pauses the game while it is open, so travel holds
            // still, and resumes when it closes.
            //
            // Pushing rather than closing also keeps the controller's resume prompt quiet: that
            // only offers itself when no journey is running, and this one still is.
            uiManager.PushWindow(DaggerfallUI.Instance.DfTravelMapWindow);
        }
    }
}
