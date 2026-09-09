// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Compatibility/BasicRoadsUtils.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallConnect.Arena2;

namespace Monobelisk.Compatibility
{
    public struct RoadData
    {
        public Vector4[] NW_NE_SW_SE;
        public Vector4[] N_E_S_W;
    }
    public static class BasicRoadsUtils
    {
        const byte N = 128;//0b_1000_0000;
        const byte NE = 64; //0b_0100_0000;
        const byte E = 32; //0b_0010_0000;
        const byte SE = 16; //0b_0001_0000;
        const byte S = 8;  //0b_0000_1000;
        const byte SW = 4;  //0b_0000_0100;
        const byte W = 2;  //0b_0000_0010;
        const byte NW = 1;  //0b_0000_0001;

        const int roads = 0;
        const int tracks = 1;
        const int rivers = 2;
        const int streams = 3;
        const string GET_PATH_DATA = "getPathData";
        static byte[][] pathsData = new byte[4][];

        public static void Init()
        {
            if (!CompatibilityUtils.BasicRoadsLoaded)
                return;

            pathsData[roads] = GetPathData(roads);
            pathsData[tracks] = GetPathData(tracks);
            //pathsData[rivers] = GetPathData(rivers);
            //pathsData[streams] = GetPathData(streams);
        }

 public static RoadData GetRoadData(int mapPixelX, int mapPixelY)
{
    var roadData = new RoadData
    {
        NW_NE_SW_SE = new Vector4[9],
        N_E_S_W = new Vector4[9]
    };

    if (!CompatibilityUtils.BasicRoadsLoaded)
        return roadData;

    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            var mpx = mapPixelX + x;
            var mpy = mapPixelY + y;

            if (mpx < 0 || mpx >= MapsFile.MaxMapPixelX)
                continue;
            if (mpy < 0 || mpy >= MapsFile.MaxMapPixelY)
                continue;

            var i = mpx + mpy * MapsFile.MaxMapPixelX;
            var si = (x + 1) + (y + 1) * 3;

            roadData.NW_NE_SW_SE[si] = new Vector4()
            {
                x = HasRoadPoint(i, NW) ? 1 : 0,
                y = HasRoadPoint(i, NE) ? 1 : 0,
                z = HasRoadPoint(i, SW) ? 1 : 0,
                w = HasRoadPoint(i, SE) ? 1 : 0,
            };

            roadData.N_E_S_W[si] = new Vector4()
            {
                x = HasRoadPoint(i, N) ? 1 : 0,
                y = HasRoadPoint(i, E) ? 1 : 0,
                z = HasRoadPoint(i, S) ? 1 : 0,
                w = HasRoadPoint(i, W) ? 1 : 0,
            };

            // Add debug messages here to log the computed vectors
            //Debug.Log($"NW_NE_SW_SE[{si}]: {roadData.NW_NE_SW_SE[si]}");
            //Debug.Log($"N_E_S_W[{si}]: {roadData.N_E_S_W[si]}");
        }
    }

    return roadData;
}


        private static bool HasRoadPoint(int i, byte direction)
        {
            var hasRoadPoint = false;

            if ((pathsData[roads][i] & direction) != 0)
                hasRoadPoint = true;
            if ((pathsData[tracks][i] & direction) != 0)
                hasRoadPoint = true;
            /*if (pathsData[rivers][i] % direction != 0)
                hasRoadPoint = true;
            if (pathsData[streams][i] % direction != 0)
                hasRoadPoint = true;*/

            return hasRoadPoint;
        }

        private static byte[] GetPathData(int type)
        {
            var modName = CompatibilityUtils.BASIC_ROADS;
            byte[] pathData = null;

            ModManager.Instance.SendModMessage(modName, GET_PATH_DATA, type, (string message, object data) =>
            {
                pathData = data as byte[];
            });

            return pathData;
        }
    }
}
