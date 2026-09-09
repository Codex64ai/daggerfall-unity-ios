// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Lypyl (Lypyl@dfworkshop.net), Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Numidium
// 
// Notes:
//

using UnityEngine;
using System;
using System.IO;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Mobile;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.UserInterface
{
    public class FLCPlayer : Panel
    {
        FlcFile flcFile = new FlcFile();
        Texture2D flcTexture = null;

        float nextFrameTime;
        bool isPlaying = false;
        byte tRed = 0;
        byte tGreen = 0;
        byte tBlue = 0;

        public bool Loop { get; set; }

        public bool TransparencyEnabled { get; set; }
        
        public FlcFile FLCFile { get { return flcFile; } }

        public FLCPlayer()
        {
            Loop = true;
            BackgroundTextureLayout = BackgroundLayout.ScaleToFit;
            BackgroundColor = Color.black;
            TransparencyEnabled = false;
        }

        public void Load(string filename)
        {
            flcFile.Transparency = TransparencyEnabled;
            flcFile.TransparentRed = tRed;
            flcFile.TransparentGreen = tGreen;
            flcFile.TransparentBlue = tBlue;

            // Seek from loose files Movies or Arena2 path
            // MOBILE: user content from the app's Documents folder (see MobileContentPath).
            string moviePath = MobileContentPath.Override(Path.Combine(Application.streamingAssetsPath, "Movies"));
            string path = ResolvePath(filename, moviePath, DaggerfallUnity.Instance.Arena2Path, File.Exists);
            if (!flcFile.Load(path))
                return;

            flcTexture = TextureReader.CreateFromSolidColor(flcFile.Header.Width, flcFile.Header.Height, (TransparencyEnabled ? Color.clear : Color.black), false, false);
            flcTexture.filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
        }

        /// <summary>
        /// Where an .FLC comes from: the movies folder if it holds one, otherwise arena2.
        ///
        /// Split out with the folder and the existence check passed in so it can be tested
        /// headlessly - on desktop the movies folder is inside the build and MobileContentPath
        /// is a no-op, so the interesting case (a replacement in the player's Documents folder,
        /// which is where DREAM's HD Daedra summoning animations land) is unreachable there.
        /// </summary>
        public static string ResolvePath(string filename, string moviesDir, string arena2Dir, Func<string, bool> exists)
        {
            string moviePath = Path.Combine(moviesDir, filename);
            if (exists != null && exists(moviePath))
                return moviePath;

            return Path.Combine(arena2Dir, filename);
        }

        public void Start()
        {
            if (flcFile != null && flcFile.ReadyToPlay)
            {
                flcFile.CurrentFrame = 0;
                nextFrameTime = Time.realtimeSinceStartup + flcFile.FrameDelay;
                BackgroundTexture = flcTexture;
                isPlaying = true;
            }
        }

        public void Stop()
        {
            if (flcFile != null)
            {
                isPlaying = false;
                BackgroundTexture = null;
            }
        }

        public void SetTransparentColor(byte r, byte g, byte b)
        {
            tRed = r;
            tGreen = g;
            tBlue = b;
        }

        public override void Update()
        {
            base.Update();

            // Stop playing if we reach final frame and not looping
            if (isPlaying && !Loop && flcFile.CurrentFrame >= flcFile.Header.NumOfFrames)
            {
                isPlaying = false;
                RaiseAnimEnd();
            }

            if (flcFile == null || !flcTexture || !flcFile.ReadyToPlay || !isPlaying)
                return;

            if (Time.realtimeSinceStartup < nextFrameTime)
                return;
            else
                nextFrameTime = Time.realtimeSinceStartup + flcFile.FrameDelay;

            flcTexture.SetPixels32(flcFile.FrameBuffer);
            flcTexture.Apply(false);
            flcFile.BufferNextFrame();
        }

        public delegate void OnAnimEndHandler(FLCPlayer player);
        public event OnAnimEndHandler OnAnimEnd;
        void RaiseAnimEnd()
        {
            if (OnAnimEnd != null)
                OnAnimEnd(this);
        }
    }
}
