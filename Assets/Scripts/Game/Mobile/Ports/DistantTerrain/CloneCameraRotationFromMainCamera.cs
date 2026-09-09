// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File Scripts/CloneCameraRotationFromMainCamera.cs, copied unchanged for iOS (no MOBILE edits).
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce.
// The World of Daggerfall-flavour additions carry no licence; private draft only.
//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

using UnityEngine;
using DaggerfallWorkshop.Game;

namespace DistantTerrain
{
    public class CloneCameraRotationFromMainCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            // Camera.main is null for a frame or two during state transitions
            // (load, fast travel, rest, dungeon enter/exit, death, quit to menu).
            // Skip the update on those frames to avoid a NullReferenceException.
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            this.transform.rotation = mainCamera.transform.rotation;
        }

    }
}