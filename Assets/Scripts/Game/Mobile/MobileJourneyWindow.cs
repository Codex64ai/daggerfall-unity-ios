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
        Rect panelRect = new Rect(0, 0, 320, 34);

        // 22 tall against the original's 10. At the scales this port runs on that is the
        // difference between a reliable tap and a fiddly one.
        Rect slowerRect = new Rect(4, 18, 26, 22);
        Rect speedRect = new Rect(32, 18, 34, 22);
        Rect fasterRect = new Rect(68, 18, 26, 22);
        Rect mapRect = new Rect(200, 18, 54, 22);
        Rect stopRect = new Rect(258, 18, 58, 22);

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

        readonly Color panelBackground = new Color(0f, 0f, 0f, 0.55f);
        readonly Color buttonBackground = new Color(0.15f, 0.12f, 0.08f, 0.85f);
        readonly Color stopBackground = new Color(0.45f, 0.08f, 0.05f, 0.85f);

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

            Button slower = DaggerfallUI.AddTextButton(slowerRect, "-", mainPanel);
            slower.BackgroundColor = buttonBackground;
            slower.OnMouseClick += (s, p) => ChangeCompression(-5);

            Panel speedPanel = DaggerfallUI.AddPanel(speedRect, mainPanel);
            speedPanel.BackgroundColor = buttonBackground;
            speedLabel = DaggerfallUI.AddTextLabel(
                DaggerfallUI.DefaultFont, new Vector2(4, 7), string.Empty, speedPanel);

            Button faster = DaggerfallUI.AddTextButton(fasterRect, "+", mainPanel);
            faster.BackgroundColor = buttonBackground;
            faster.OnMouseClick += (s, p) => ChangeCompression(5);

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
                speedLabel.Text = journey.TimeCompression + "x";
        }

        void ChangeCompression(int delta)
        {
            if (!MobileJourneyController.HasInstance)
                return;

            MobileJourneyController journey = MobileJourneyController.Instance;
            journey.SetTimeCompression(journey.TimeCompression + delta);
            RefreshLabels();
        }

        void ShowTravelMap()
        {
            // Opening the map interrupts, rather than travelling on behind the map. Picking a
            // new destination mid-journey would otherwise leave two journeys half-running, and
            // the controller keeps the old destination so the map can offer to resume it.
            CloseWindow();
            uiManager.PushWindow(DaggerfallUI.Instance.DfTravelMapWindow);
        }
    }
}
