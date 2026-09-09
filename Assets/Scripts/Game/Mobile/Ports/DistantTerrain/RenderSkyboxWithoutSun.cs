// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File Scripts/RenderSkyboxWithoutSun.cs, copied unchanged for iOS (no MOBILE edits).
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce.
// The World of Daggerfall-flavour additions carry no licence; private draft only.
//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

using UnityEngine;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using DaggerfallConnect;
//using DaggerfallConnect.Arena2;
//using DaggerfallConnect.Utility;
//using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
//using DaggerfallWorkshop.Utility;

namespace DistantTerrain
{
    public class RenderSkyboxWithoutSun : MonoBehaviour
    {
        float revertSunSize;

        void OnPreRender()
        {
            if (RenderSettings.skybox)
            {
                revertSunSize = RenderSettings.skybox.GetFloat("_SunSize");
                RenderSettings.skybox.SetFloat("_SunSize", 0.0f);
            }
        }

        void OnPostRender()
        {
            if (RenderSettings.skybox)
            {
                RenderSettings.skybox.SetFloat("_SunSize", revertSunSize);
            }
        }

    }
}