// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationModLoader.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System.Collections.Generic;
using DaggerfallWorkshop;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;   //required for modding features

namespace LocationLoader
{
    public static class LocationModLoader
    {
        public static Mod mod { get; private set; }
        public static GameObject modObject { get; private set; }

        // MOBILE: started by MobilePortedMods when the launcher entry is on
        public static void Init(InitParams initParams)
        {
            // Get mod
            mod = initParams.Mod;

            modObject = new GameObject("LocationLoader");
            modObject.AddComponent<LocationLoader>();
            mod.SaveDataInterface = modObject.AddComponent<LocationSaveDataInterface>();
            modObject.AddComponent<LocationResourceManager>();
            mod.MessageReceiver = MessageReceiver;
            mod.IsReady = true;

            // It's okay if other mods override us, they better provide a compatibility patch though
            DaggerfallUnity.Instance.TerrainNature = new LocationTerrainNature();

            const int ladderModelId = 41409;
            PlayerActivate.RegisterCustomActivation(mod, ladderModelId, OnLadderActivated);
        }

        private static void MessageReceiver(string message, object data, DFModMessageCallback callback)
        {
            var ll = modObject.GetComponent<LocationLoader>();
            switch (message)
            {
                case "getTerrainLocationInstanceRects":
                    var mapPixelCoord = (Vector2Int)data;
                    if (!ll.TryGetTerrainExtraData(mapPixelCoord, out LocationLoader.LLTerrainData extraData))
                    {
                        Debug.LogError($"[LL] Call to 'getTerrainLocationInstanceRects' failed: terrain at ({mapPixelCoord.x}, {mapPixelCoord.y}) is not loaded");
                        callback(message, null);
                    }

                    callback(message, new List<Rect>(extraData.LocationInstanceRects));

                    break;
            }
        }

        static void OnLadderActivated(RaycastHit hit)
        {
            // Player must not be inside a building (already handled by DFU)
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
            if (playerEnterExit.IsPlayerInsideBuilding)
                return;

            Transform ladderTransform = hit.transform;
            Transform prefabTransform = ladderTransform.parent;
            GameObject prefabObject = prefabTransform.gameObject;
            LocationData data = prefabObject.GetComponent<LocationData>();

            PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
            bool foundBottom = data.FindClosestMarker(EditorMarkerTypes.LadderBottom, playerMotor.transform.position, out Vector3 bottomMarker);
            bool foundTop = data.FindClosestMarker(EditorMarkerTypes.LadderTop, playerMotor.transform.position, out Vector3 topMarker);

            Vector2 ladderPlanarPos = new Vector2(ladderTransform.position.x, ladderTransform.position.z);
            Vector2 bottomMarkerPlanarPos = new Vector2(bottomMarker.x, bottomMarker.z);
            Vector2 topMarkerPlanarPos = new Vector2(topMarker.x, topMarker.z);

            float bottomPlanarDistance = Vector2.Distance(ladderPlanarPos, bottomMarkerPlanarPos);
            float topPlanarDistance = Vector2.Distance(ladderPlanarPos, topMarkerPlanarPos);

            const float MaxMarkerDistance = PlayerActivate.DefaultActivationDistance * 2;
            foundBottom = foundBottom && bottomPlanarDistance < MaxMarkerDistance;
            foundTop = foundTop && topPlanarDistance < MaxMarkerDistance;

            float bottomDistance = Vector3.Distance(playerMotor.transform.position, bottomMarker);
            float topDistance = Vector3.Distance(playerMotor.transform.position, topMarker);

            // Teleport to top marker
            if (foundTop && (!foundBottom || topDistance > bottomDistance))
            {
                playerMotor.transform.position = topMarker;
                playerMotor.FixStanding();
            }
            else if (foundBottom)
            {
                playerMotor.transform.position = bottomMarker;
                playerMotor.FixStanding();
            }
        }
    }
}