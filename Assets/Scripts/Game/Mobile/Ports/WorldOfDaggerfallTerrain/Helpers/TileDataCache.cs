// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Helpers/TileDataCache.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System.Collections.Generic;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;

namespace Monobelisk
{
    public class TileDataCache
    {
        private readonly Dictionary<string, byte[]> tileDataCache = new Dictionary<string, byte[]>();

        /// <summary>
        /// Get tileData for a map pixel during TerrainTexturer invokation.
        /// </summary>
        /// <param name="mapPixelX"></param>
        /// <param name="mapPixelY"></param>
        /// <returns></returns>
        public byte[] Get(int mapPixelX, int mapPixelY)
        {
            var pos = PositionKey(mapPixelX, mapPixelY);

            if (tileDataCache.ContainsKey(pos))
            {
                var td = tileDataCache[pos];
                tileDataCache.Remove(pos);

                return td;
            }

            Debug.LogWarning("==> Interesting Terrains: No tileData found for map pixel " + mapPixelX + "x" + mapPixelY);

            return null;
        }

        /// <summary>
        /// Store tileData for a map pixel for use in TerrainTexturer.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="tileData"></param>
        public void Add(DFPosition pos, byte[] tileData)
        {
            // MOBILE: (e) indexer assignment, not Dictionary.Add. Get() and UncacheTileData both remove
            // MOBILE: the entry, but nothing guarantees one of them ran before the same map pixel is
            // MOBILE: generated again (a tile regenerated before its texturer consumed it), and Add on a
            // MOBILE: key that is already present throws - which upstream-of-us (AsesinoBlade/wod-terrain
            // MOBILE: @ 76623bb2aebc66a7b25a8a9621b3e35d69d41917) patched for exactly this reason. The
            // MOBILE: newest tileData is the correct one, so overwrite.
            tileDataCache[PositionKey(pos.X, pos.Y)] = tileData;
        }

        /// <summary>
        /// Remove cached tileData, if no TerrainTexturer has used it, to avoid memory buildup.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="terrainData"></param>
        public void UncacheTileData(DaggerfallTerrain sender, TerrainData terrainData)
        {
            var pos = PositionKey(sender.MapPixelX, sender.MapPixelY);
            if (tileDataCache.ContainsKey(pos))
            {
                tileDataCache.Remove(pos);
            }
        }

        private static string PositionKey(int mapPixelX, int mapPixelY)
        {
            return new DFPosition(mapPixelX, mapPixelY).ToString().Trim();
        }
    }
}