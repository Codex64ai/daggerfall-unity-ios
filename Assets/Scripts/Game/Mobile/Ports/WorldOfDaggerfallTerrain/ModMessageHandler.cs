// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/ModMessageHandler.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace Monobelisk
{
    public static class ModMessageHandler
    {
        // Requests
        private const string GET_TILEDATA = "getTileData";

        // Responses
        private const string TILEDATA = "tileData";
        private const string ERROR = "error";

        public static void Init()
        {
            InterestingTerrains.Mod.MessageReceiver =
                (
                    string message,
                    object data,
                    DFModMessageCallback callBack
                ) =>
            {
                switch (message)
                {
                    case GET_TILEDATA:
                        GetTileData(data, callBack);
                        break;
                    default:
                        callBack(ERROR, "Message '" + message + "' is invalid.");
                        break;
                }
            };
        }

        private static void GetTileData(object data, DFModMessageCallback callBack)
        {
            var dataErrMsg = "Data for message '" + GET_TILEDATA + "' must be an int array, where [0] is MapPixelX and [1] is MapPixelY.";

            if (data.GetType() != typeof(int[]))
            {
                callBack(ERROR, dataErrMsg);
                return;
            }

            var mapPixelPos = (int[])data;

            if (mapPixelPos.Length < 2)
            {
                callBack(ERROR, dataErrMsg);
                return;
            }

            var mpx = Mathf.Clamp(mapPixelPos[0], 0, 999);
            var mpy = Mathf.Clamp(mapPixelPos[1], 0, 499);

            var tileData = InterestingTerrains.tileDataCache.Get(mpx, mpy);

            if (tileData == null)
            {
                callBack(ERROR, "tileData does not exist for map pixel " + mpx + "x" + mpy + ". Either it hasn't been generated yet, or it's been used by another process.");
                return;
            }

            callBack(TILEDATA, tileData);
        }
    }
}