// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Utility.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using System.Linq;
using UnityEngine;

namespace Monobelisk
{
    public static class Utility
    {
        public static Vector2 GetTerrainVertexPosition(int mapPixelX, int mapPixelY)
        {
            var max = GetWorldPixelSize();

            return new Vector2(mapPixelX, max.y - mapPixelY) * GetTerrainVertexSize();
        }

        public static Vector2 GetTerrainUnitPosition(int mapPixelX, int mapPixelY)
        {
            var max = GetWorldPixelSize();

            return new Vector2(mapPixelX, max.y - mapPixelY) * GetTerrainUnitSize();
        }

        public static float GetTerrainVertexSize()
        {
            return 129f;
        }

        public static float GetTerrainUnitSize()
        {
            return MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;
        }

        public static Vector2 GetWorldVertexSize()
        {
            var max = GetWorldPixelSize();

            return new Vector2(max.x, max.y) * GetTerrainVertexSize();
        }

        public static Vector2 GetWorldUnitSize()
        {
            var max = GetWorldPixelSize();

            return new Vector2(max.x, max.y) * GetTerrainUnitSize();
        }

        public static Vector2Int GetWorldPixelSize()
        {
            int maxX = 999;
            int maxY = 499;

            return new Vector2Int(maxX, maxY);
        }

        public static float OneMeterInPixel()
        {
            return 1f / GetTerrainUnitSize();
        }

        public static float GetOriginalTerrainHeight()
        {
            DaggerfallUnity dfUnity;
            DaggerfallUnity.FindDaggerfallUnity(out dfUnity);

            return dfUnity.TerrainSampler.MaxTerrainHeight;
        }

        /// <summary>
        /// MOBILE: (F2) upstream is `floats.Select(f =&gt; (byte)(uint)(f * 255f))`, which has no guard
        /// of any kind, and this is the ONE conversion the mod's whole start-up world heightmap goes
        /// through - the texture every location's flatten target is read from. Three inputs it could
        /// not survive:
        ///   NaN       (uint)float.NaN is unspecified in C# and 0 in practice, so a NaN map pixel
        ///             arrived as byte 0 - INDISTINGUISHABLE FROM OCEAN, and silently. That is the
        ///             mechanism behind the device's `holes 14519` against 459 for the identical code
        ///             on macOS Metal: iOS Metal compiles the generator with -ffast-math (now turned
        ///             off by #pragma disable_fastmath in both .compute files, which is the primary
        ///             fix - this is the net under it).
        ///   negative  (uint) of a negative float is undefined and wraps, so -0.004 became byte 254 -
        ///             a MOUNTAIN where the generator meant a hair below sea level.
        ///   above 1   (uint)(f * 255f) &gt; 255 truncates to a byte and wraps, so 1.004 became byte 0.
        ///             Unreachable while saturate() holds, and -ffast-math is exactly what stops
        ///             guaranteeing that it does.
        ///
        /// A non-finite or negative float takes the PREVIOUS VALID byte instead (WOODS.WLD's layout is
        /// index = x + y * MapWidth, so the previous element is the map pixel immediately to the west -
        /// the best available estimate, and one that keeps a bad pixel inside its own coastline instead
        /// of dropping it to the sea floor). The first element has no neighbour, so it falls back to the
        /// generator's ocean floor byte. Every substitution is COUNTED and the count is logged on the
        /// start-up line, because a silent repair of the very defect being hunted would be worse than
        /// the defect.
        ///
        /// The healthy path is byte-identical to upstream: for f in [0, 1], Mathf.Min(f * 255f, 255f)
        /// is f * 255f and (byte)(uint) truncates exactly as before.
        /// </summary>
        public static byte[] ToBytes(float[] floats)
        {
            int nonFinite;
            return ToBytes(floats, out nonFinite);
        }

        // MOBILE: (F2) the counting overload. Pure: no Unity state, no GPU - the self test pins it.
        public static byte[] ToBytes(float[] floats, out int nonFinite)
        {
            nonFinite = 0;

            if (floats == null)
                return new byte[0];

            var bytes = new byte[floats.Length];
            byte previous = (byte)TerrainComputer.OceanFloorByte;

            for (int i = 0; i < floats.Length; i++)
            {
                float f = floats[i];

                if (float.IsNaN(f) || float.IsInfinity(f) || f < 0f)
                {
                    nonFinite++;
                    bytes[i] = previous;
                    continue;
                }

                previous = (byte)(uint)Mathf.Min(f * 255f, 255f);
                bytes[i] = previous;
            }

            return bytes;
        }
    }
}