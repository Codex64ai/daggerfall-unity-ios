// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Compatibility/BasicRoadsUtils.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System;
using System.Collections;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Mobile;
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

        // MOBILE: (R1) this whole feature was inert on iOS, and it is the reason roads climbed hills
        // instead of cutting through them. Basic Roads is not a mod bundle here - it is compiled into
        // the port: MobileRoads installs its terrain texturing (so the roads are DRAWN) and
        // MobileRoadNetwork owns its path bytes (so Real travel can route on them). Nothing therefore
        // registers a mod titled "BasicRoads" with ModManager, CompatibilityUtils.BasicRoadsLoaded was
        // always false, Init copied nothing, GetRoadData returned nine zero vectors and the shader's
        // `saturate(1.3 - roadWeight * smoothRoads)` never left 1.3 - the terrain was generated as if
        // there were no roads at all, under roads the player can see.
        //
        // Reading MobileRoadNetwork here is not a re-implementation of the data. Those are the same
        // roadData/trackData byte arrays Basic Roads authored, at the same 1000x500 map-pixel layout
        // (index x + y * 1000) with the same direction bitmask (N = 128 down to NW = 1) - the two
        // copies of those constants are pinned against each other by the self test.
        static bool ready;              // MOBILE: (R1) a usable byte source was found
        static bool fromMobileNetwork;  // MOBILE: (R1) ... and it is the compiled-in one
        static bool loggedTile;         // MOBILE: (R1) the once-per-session proof line

        /// <summary>
        /// MOBILE: (R1) resolved once per session, from InterestingTerrains.Start, which now calls this
        /// unconditionally and lets it make its own decision. Reading the preference once is correct
        /// rather than merely cheap: drawing roads is a restart-required switch by design (see the
        /// class note on MobileRoads - a mid-session change leaves already-built tiles painted the old
        /// way), so the answer cannot change under a running session.
        /// </summary>
        public static void Init()
        {
            ready = false;
            fromMobileNetwork = false;
            loggedTile = false;
            pathsData[roads] = null;
            pathsData[tracks] = null;

            // MOBILE: (R1) gated on the DRAWING switch and not merely on the data being present. The
            // road network ships with the code either way (Real travel routes on it with the roads
            // switched off), and flattening a corridor the player cannot see would be an unexplained
            // flat strip across a hillside. The bug being fixed is roads that ARE drawn climbing.
            if (MobileRoads.Enabled && MobileRoadNetwork.Available)
            {
                ready = true;
                fromMobileNetwork = true;
                Debug.Log("[WoDTerrain] roads: smoothing on, over the compiled-in Basic Roads network");
                return;
            }

            if (!CompatibilityUtils.BasicRoadsLoaded)
            {
                // MOBILE: (R1) says which of the two reasons it was, because "no roads in the terrain"
                // looks identical from a screenshot either way.
                Debug.Log("[WoDTerrain] roads: smoothing off - "
                          + (MobileRoads.Enabled ? "no path data" : "Roads & tracks is off"));
                return;
            }

            pathsData[roads] = GetPathData(roads);
            pathsData[tracks] = GetPathData(tracks);
            //pathsData[rivers] = GetPathData(rivers);
            //pathsData[streams] = GetPathData(streams);

            // MOBILE: (R1) upstream trusted SendModMessage to answer. It hands the callback whatever the
            // mod chooses to pass and the `as byte[]` can come back null, after which HasRoadPoint
            // dereferenced a null array once per direction per road test on every terrain tile - i.e.
            // the terrain sampler throwing for the rest of the session.
            ready = UsablePathData(pathsData[roads]) && UsablePathData(pathsData[tracks]);
            if (!ready)
                Debug.LogWarning("[WoDTerrain] roads: " + CompatibilityUtils.BASIC_ROADS + " answered "
                                 + GET_PATH_DATA + " with no usable path data; smoothing off");
        }

        // MOBILE: (R1) a short array is as fatal as a null one - the 3x3 gather indexes up to
        // (x+1) + (y+1) * 1000 without a length test, exactly as upstream wrote it.
        static bool UsablePathData(byte[] data)
        {
            return data != null && data.Length >= MapsFile.MaxMapPixelX * MapsFile.MaxMapPixelY;
        }

        public static RoadData GetRoadData(int mapPixelX, int mapPixelY)
        {
            if (!ready)
                return EmptyRoadData();

            int pathPixels;
            var roadData = BuildRoadData(mapPixelX, mapPixelY, PathBitsAt, out pathPixels);

            // MOBILE: (R1) once per session. The two arrays go straight into the compute shader, so
            // there is otherwise nothing in a log to distinguish "roads reached the shader" from the
            // silent nine-zero-vectors state this fix removes.
            if (!loggedTile)
            {
                loggedTile = true;
                Debug.Log("[WoDTerrain] roads: " + pathPixels + " map pixels with paths in the current tile ("
                          + mapPixelX + "," + mapPixelY + ", source "
                          + (fromMobileNetwork ? "compiled-in" : CompatibilityUtils.BASIC_ROADS) + ")");
            }

            return roadData;
        }

        // MOBILE: (R1) public, not internal, only so the editor self test can reach it from the
        // other assembly - the two below are called from nowhere else in the port.
        // The two nine-slot arrays the shader declares, all zero - "no roads anywhere near
        // this tile", which is what every call returned before this fix.
        public static RoadData EmptyRoadData()
        {
            return new RoadData
            {
                NW_NE_SW_SE = new Vector4[9],
                N_E_S_W = new Vector4[9]
            };
        }

        /// <summary>
        /// MOBILE: (R1) upstream's 3x3 gather, with the byte source passed in so the self test can drive
        /// it without a world, a mod or a GPU. The index arithmetic is upstream's and has to stay:
        /// si = (x + 1) + (y + 1) * 3 is the same slot basicRoads.cginc reads back as
        /// i = (mpOffset.x + 1) + (mpOffset.y + 1) * 3, and getting it wrong would smooth the wrong
        /// neighbour rather than fail visibly. <paramref name="pathPixels"/> counts how many of the
        /// nine map pixels carry any path at all, for the log line above.
        /// </summary>
        public static RoadData BuildRoadData(int mapPixelX, int mapPixelY,
                                             Func<int, int, byte> pathBitsAt, out int pathPixels)
        {
            var roadData = EmptyRoadData();
            pathPixels = 0;

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

                    var si = (x + 1) + (y + 1) * 3;
                    var bits = pathBitsAt(mpx, mpy);
                    if (bits != 0)
                        pathPixels++;

                    roadData.NW_NE_SW_SE[si] = new Vector4()
                    {
                        x = (bits & NW) != 0 ? 1 : 0,
                        y = (bits & NE) != 0 ? 1 : 0,
                        z = (bits & SW) != 0 ? 1 : 0,
                        w = (bits & SE) != 0 ? 1 : 0,
                    };

                    roadData.N_E_S_W[si] = new Vector4()
                    {
                        x = (bits & N) != 0 ? 1 : 0,
                        y = (bits & E) != 0 ? 1 : 0,
                        z = (bits & S) != 0 ? 1 : 0,
                        w = (bits & W) != 0 ? 1 : 0,
                    };
                }
            }

            return roadData;
        }

        // MOBILE: (R1) replaces HasRoadPoint, which took an already-computed flat index and tested one
        // direction bit at a time against the two arrays. Roads and tracks are ORed together here
        // instead of per direction - the same answer, one array read per pixel rather than eight.
        static byte PathBitsAt(int mapPixelX, int mapPixelY)
        {
            if (fromMobileNetwork)
                return MobileRoadNetwork.PathsAt(mapPixelX, mapPixelY);

            var i = mapPixelX + mapPixelY * MapsFile.MaxMapPixelX;
            return (byte)(pathsData[roads][i] | pathsData[tracks][i]);
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
