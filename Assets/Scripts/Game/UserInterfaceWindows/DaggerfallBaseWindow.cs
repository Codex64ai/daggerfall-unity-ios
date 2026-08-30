// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.UserInterface;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Implements a user interface window for native Daggerfall 320x200 screens.
    /// Also provides some control helpers common to UI windows.
    /// </summary>
    public abstract class DaggerfallBaseWindow : UserInterfaceWindow
    {
        bool isSetup;
        DaggerfallUnity dfUnity;
        Panel nativePanel = new Panel();

        protected ToolTip defaultToolTip = null;
        protected bool allowFreeScaling = true;

        public DaggerfallBaseWindow(IUserInterfaceManager uiManager, int screenWidth = 320, int screenHeight = 200)
            : base(uiManager)
        {
            // Parent panel
            parentPanel.Components.Add(nativePanel);
            parentPanel.BackgroundColor = Color.black;

            // Native panel
            nativePanel.HorizontalAlignment = HorizontalAlignment.Center;
            nativePanel.VerticalAlignment = VerticalAlignment.Middle;
            nativePanel.BackgroundTextureLayout = BackgroundLayout.StretchToFill;
            nativePanel.Size = new Vector2(screenWidth, screenHeight);
            nativePanel.AutoSize = AutoSizeModes.ScaleToFit;

            // Setup default tooltip
            if (DaggerfallUnity.Settings.EnableToolTips)
            {
                defaultToolTip = new ToolTip();
                defaultToolTip.ToolTipDelay = DaggerfallUnity.Settings.ToolTipDelayInSeconds;
                defaultToolTip.BackgroundColor = DaggerfallUnity.Settings.ToolTipBackgroundColor;
                defaultToolTip.TextColor = DaggerfallUnity.Settings.ToolTipTextColor;
                defaultToolTip.Parent = nativePanel;
            }
        }

        protected DaggerfallUnity DaggerfallUnity
        {
            get { return (dfUnity != null) ? dfUnity : dfUnity = DaggerfallUnity.Instance; }
        }

        public bool IsSetup
        {
            get { return isSetup; }
            protected set { isSetup = value; }
        }

        public Panel NativePanel
        {
            get { return nativePanel; }
        }

        public override void Update()
        {
            // DaggerfallUnity must be ready
            if (dfUnity == null)
                dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return;

            // Handle retro mode UI scaling
            if (DaggerfallUnity.Settings.RetroRenderingMode != 0 && DaggerfallUnity.Settings.RetroModeAspectCorrection != 0 && allowFreeScaling)
                nativePanel.AutoSize = AutoSizeModes.ScaleFreely;
            else
                nativePanel.AutoSize = AutoSizeModes.ScaleToFit;

            // Must be setup
            if (!isSetup)
            {
                // SET UP ONCE, EVEN IF SETUP THREW.
                //
                // isSetup used to be assigned only after Setup() returned, so an exception
                // inside it left the flag false and Update() retried every frame. Each
                // retry adds another full set of components to NativePanel, and none of
                // them ever update: this early return is before base.Update(), so no
                // component in the window sees a mouse event again. That is a window which
                // draws normally, ignores every click on every control, and loses frames
                // steadily as the duplicates pile up - the only way out being a key that
                // the window's own Update() handles past this point.
                //
                // Take the half-built window over that, and say what went wrong.
                try
                {
                    Setup();
                }
                catch (Exception ex)
                {
                    Debug.LogErrorFormat("{0}.Setup() failed - window will be incomplete: {1}",
                        GetType().Name, ex);
                }

                isSetup = true;
                return;
            }

            base.Update();

            // Update tooltip last
            if (defaultToolTip != null)
                defaultToolTip.Update();
        }

        public override void Draw()
        {
            base.Draw();

            // Draw tooltip last
            if (defaultToolTip != null)
                defaultToolTip.Draw();
        }

        protected abstract void Setup();
    }
}