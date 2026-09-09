// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/TerrainComputer.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static DaggerfallWorkshop.StreamingWorld;

namespace Monobelisk
{
    // MOBILE: (f) IEquatable + GetHashCode added. This is the key type of a dictionary looked up 1,089
    // MOBILE: times per terrain tile; without them every lookup went through ValueType.Equals, which is
    // MOBILE: reflection-based and boxes. Field-by-field equality is exactly what the default gave, so
    // MOBILE: behaviour is unchanged - only the cost is.
    public struct DoubleInt : IEquatable<DoubleInt>
    {
        public int Item1;
        public int Item2;

        // MOBILE: (f)
        public bool Equals(DoubleInt other)
        {
            return Item1 == other.Item1 && Item2 == other.Item2;
        }

        // MOBILE: (f)
        public override bool Equals(object obj)
        {
            return obj is DoubleInt && Equals((DoubleInt)obj);
        }

        // MOBILE: (f)
        public override int GetHashCode()
        {
            return (Item1 * 397) ^ Item2;
        }
    }

    public struct TerrainComputer
    {
        /// <summary>
        /// MOBILE: (b) the shader declares "float4 locationPositions[1089], locationSizes[1089]" and both
        /// LocationWeight and PortLocationWeight write locationHeightData[i] for every i &lt; locationCount,
        /// where locationCount is the number of locations found in the 33x33 = 1089 map-pixel search
        /// window. Upstream sized the buffer 289. D3D11 silently discards out-of-range UAV writes, which
        /// is why this was never noticed on desktop; on Metal an out-of-bounds buffer write is undefined
        /// and can fault the command buffer. Size it to what the shader can address.
        /// </summary>
        public const int LocationBufferSize = 1089;

        /// <summary>
        /// MOBILE: (a) the y dimension of MainHeightmapComputer's CSMain thread group, [numthreads(10,5,1)].
        /// Dispatch takes a count of GROUPS, so every band handed to it must be a whole multiple of this
        /// or the integer division silently drops the remainder rows.
        /// </summary>
        public const int GroupRowsY = 5;

        /// <summary>
        /// MOBILE: (a) how many row bands the start-up world heightmap is generated in. Upstream ran the
        /// whole 1000x500 map as one 500,000-thread dispatch followed by one GetData - a single Metal
        /// command buffer that iOS's execution-time limit can kill outright (the watchdog does not care
        /// that the work is legitimate). Ten bands of 50 rows is ten small submits with a readback
        /// between them; the arithmetic and the output layout are identical.
        /// </summary>
        public const int StartupBands = 10;

        public static byte[] originalHeightmapBuffer;
        public static byte[] alteredHeightmapBuffer;

        // MOBILE: (b)(g) created on first use rather than in a static field initialiser, so merely
        // MOBILE: touching this type (a pure helper, a self-test) does not allocate a GPU buffer, and
        // MOBILE: Cleanup() can put it back to a state where the next Init starts clean.
        private static ComputeBuffer locationHeightDataBuffer;
        public static ComputeBuffer LocationHeightData
        {
            get
            {
                if (locationHeightDataBuffer == null)
                    locationHeightDataBuffer = new ComputeBuffer(LocationBufferSize, sizeof(float) * 3);
                return locationHeightDataBuffer;
            }
        }

        // MOBILE: (c) one ComputeShader clone per kernel asset, held for the life of the session.
        // MOBILE: Upstream called Object.Instantiate(csPrototype) once per terrain tile and never
        // MOBILE: destroyed the clone; on Metal each clone can carry its own MTLComputePipelineState, so
        // MOBILE: a session leaked one managed object and one pipeline state per tile generated. The
        // MOBILE: clone (rather than the Resources asset itself) is kept so that setting uniforms does
        // MOBILE: not mutate the asset that Resources.Load hands back.
        private static ComputeShader terrainCS;
        private static ComputeShader mainCS;

        public static Texture2D baseHeightmap;

        public Vector2 terrainPosition;
        public Vector2 terrainSize;
        public int heightmapResolution;
        public Rect locationRect;
        public HeightmapBufferCollection heightmapBuffers;
        private InterestingTerrainSampler sampler;

        // MOBILE: (h) how many locations the last DispatchAndProcess fed to the shader, for the timing
        // MOBILE: log line - the dominant term in a tile's GPU cost.
        public int locationCount;

        /// <summary>
        /// MOBILE: (f) the 33x33 sweep now caches MISSES as well as hits (a null value). Upstream cached
        /// only the hits, so the ~1,000 map pixels in the window that hold no location were re-read from
        /// MAPS.BSA through TerrainHelper.GetMapPixelData for every tile, forever.
        /// </summary>
        public static readonly Dictionary<DoubleInt, Rect?> LocationRectCache = new Dictionary<DoubleInt, Rect?>();

        /// <summary>
        /// MOBILE: (a) splits a height into contiguous row bands, the last taking the remainder, so the
        /// start-up world heightmap can be dispatched as several small Metal command buffers instead of
        /// one 500,000-thread submit that iOS's command-buffer execution limit can kill. Pure: no GPU,
        /// no Unity state, so the self-test can pin it.
        ///
        /// Every band but the last is rounded DOWN to a multiple of GroupRowsY, because the caller feeds
        /// rows / GroupRowsY to Dispatch, which counts thread GROUPS. A band of 71 rows would dispatch 14
        /// groups = 70 rows and lose the 71st; the next band would start where this one claimed to end,
        /// so the lost row would never be written by anyone and the world heightmap would carry a stripe
        /// of whatever the buffer held. The last band absorbs the remainder, which for the only size that
        /// matters (500, a multiple of 5) is itself always a whole number of groups.
        /// </summary>
        public static (int yStart, int rows)[] Bands(int height, int bands)
        {
            if (height <= 0)
                return new (int yStart, int rows)[0];
            if (bands < 1)
                bands = 1;

            // MOBILE: (a) more bands than there are whole thread groups would force a band under one
            // group, so cap the count instead of emitting bands the dispatch cannot express.
            int maxBands = height / GroupRowsY;
            if (maxBands < 1)
                maxBands = 1;
            if (bands > maxBands)
                bands = maxBands;

            int per = height / bands;
            per -= per % GroupRowsY;          // MOBILE: (a) whole thread groups only
            if (per < GroupRowsY)
                per = GroupRowsY;             // MOBILE: (a) only reachable when height < GroupRowsY, where bands == 1

            var result = new (int yStart, int rows)[bands];
            int y = 0;

            for (int i = 0; i < bands; i++)
            {
                int rows = (i == bands - 1) ? height - y : per;
                result[i] = (y, rows);
                y += rows;
            }

            return result;
        }

        public static TerrainComputer Create(MapPixelData mapPixelData, InterestingTerrainSampler sampler)
        {
            var tSize = Utility.GetTerrainVertexSize();

            return new TerrainComputer()
            {
                sampler = sampler,
                heightmapBuffers = BufferIO.CreateHeightmapBuffers(),
                heightmapResolution = (int)InterestingTerrains.settings.heightmapResolution,
                locationRect = mapPixelData.hasLocation
                    ? mapPixelData.locationRect
                    : new Rect(-10, -10, 1, 1),
                terrainPosition = Utility.GetTerrainVertexPosition(mapPixelData.mapPixelX, mapPixelData.mapPixelY),
                terrainSize = new Vector2(tSize, tSize)
            };
        }

        // MOBILE: (M2) the parameters arrive as an argument rather than through
        // MOBILE: InterestingTerrains.instance.csParams, because this now runs BEFORE the
        // MOBILE: GameObject (and therefore `instance`) exists - see InterestingTerrains.TryPrepareWorld.
        public static void InitializeWoodsFileHeightmap(TerrainComputerParams csParams)
        {
            var woodsFile = DaggerfallUnity.Instance.ContentReader.WoodsFileReader;
            var original = woodsFile.Buffer;
            originalHeightmapBuffer = new byte[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                originalHeightmapBuffer[i] = original[i];
            }

            var alteredHeights = new ComputeBuffer(original.Length, sizeof(float));

            // MOBILE: (c) one held clone instead of a fresh Instantiate every call.
            var cs = MainComputer();
            var k = cs.FindKernel("CSMain");

            cs.SetFloat("newHeight", Constants.TERRAIN_HEIGHT);
            cs.SetFloat("maxTerrainHeight", 2308.5f);
            cs.SetFloat("scaledOceanElevation", 27.2f);
            cs.SetFloat("baseHeightScale", 8f);
            cs.SetFloat("noiseMapScale", 4f);
            cs.SetFloat("extraNoiseScale", 10f);
            cs.SetVector("terrainSize", new Vector2(WoodsFile.MapWidth, WoodsFile.MapHeight));
            cs.SetVector("terrainPosition", Vector2.zero);
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetBuffer(k, "Result", alteredHeights);
            csParams.ApplyToCS(cs);     // MOBILE: (M2)

            // MOBILE: (a) upstream ran the whole 1000x500 map as ONE dispatch of 500,000 threads
            // followed by one GetData - a single Metal command buffer whose execution time iOS's
            // watchdog is free to kill, and it does not care that the work is legitimate. The same
            // work is now submitted as StartupBands row bands with a readback between them, so no
            // single command buffer carries more than a tenth of it.
            //
            // The kernel writes world row y to buffer row (MapHeight - 1 - y) - the "(499 - id.y)"
            // in the index formula, which is deliberately left untouched so the output array layout
            // is identical to upstream's. That flip is why the readback range below is computed from
            // the END of the band: the rows a band dispatches land in the MIRRORED region of the
            // buffer, and reading back band.yStart * MapWidth would hand back a region that has not
            // been written yet (it belongs to the band at the other end of the map).
            var floatHeights = new float[original.Length];
            var bands = Bands(WoodsFile.MapHeight, StartupBands);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var band in bands)
            {
                cs.SetInt("yOffset", band.yStart);
                cs.Dispatch(k, WoodsFile.MapWidth / 10, band.rows / GroupRowsY, 1);

                int flippedStart = WoodsFile.MapHeight - band.yStart - band.rows;
                int offset = flippedStart * WoodsFile.MapWidth;
                alteredHeights.GetData(floatHeights, offset, offset, band.rows * WoodsFile.MapWidth);
            }

            watch.Stop();
            // MOBILE: (h) the one-off cost of the whole start-up pass, in Ikram's Player.log.
            Debug.Log(string.Format("[WoDTerrain] world heightmap {0} ms ({1} bands)",
                watch.ElapsedMilliseconds, bands.Length));

            alteredHeightmapBuffer = Utility.ToBytes(floatHeights);
            woodsFile.Buffer = alteredHeightmapBuffer;

            baseHeightmap = new Texture2D(WoodsFile.MapWidth, WoodsFile.MapHeight, TextureFormat.ARGB32, false, true);
            baseHeightmap.SetPixels32(ToBasemap(alteredHeightmapBuffer));
            baseHeightmap.Apply();

            alteredHeights.Release();
            alteredHeights.Dispose();
        }

        // MOBILE: (c)
        private static ComputeShader MainComputer()
        {
            if (mainCS == null)
                mainCS = UnityEngine.Object.Instantiate(InterestingTerrains.mainHeightComputer);
            return mainCS;
        }

        // MOBILE: (c)
        private static ComputeShader TerrainComputerShader(ComputeShader csPrototype)
        {
            if (terrainCS == null)
                terrainCS = UnityEngine.Object.Instantiate(csPrototype);
            return terrainCS;
        }

        public static void Cleanup()
        {
            // MOBILE: (b)(c) release the location buffer only if it was ever created, and destroy the two
            // MOBILE: held ComputeShader clones - they are the leak the per-tile Instantiate used to be.
            if (locationHeightDataBuffer != null)
            {
                locationHeightDataBuffer.Release();
                locationHeightDataBuffer.Dispose();
                locationHeightDataBuffer = null;
            }

            if (terrainCS != null)
            {
                UnityEngine.Object.Destroy(terrainCS);
                terrainCS = null;
            }

            if (mainCS != null)
            {
                UnityEngine.Object.Destroy(mainCS);
                mainCS = null;
            }
        }

        public void DispatchAndProcess(ComputeShader csPrototype, ref MapPixelData mapData, TerrainComputerParams csParams)
        {
            var woodsFile = DaggerfallUnity.Instance.ContentReader.WoodsFileReader;
            // MOBILE: (c) one held clone instead of a fresh Instantiate every tile.
            var cs = TerrainComputerShader(csPrototype);
            var k = cs.FindKernel("TerrainComputer");
            uint _x, _y, _z;
            cs.GetKernelThreadGroupSizes(k, out _x, out _y, out _z);

            int res = heightmapResolution + 1;

            DaggerfallUnity dfUnity;
            DaggerfallUnity.FindDaggerfallUnity(out dfUnity);
            int searchSize = 16;
            var locations = new List<Rect>();

            int x, y;

            for (x = -searchSize; x <= searchSize; x++)
            {
                for (y = -searchSize; y <= searchSize; y++)
                {
                    var mpx = mapData.mapPixelX + x;
                    var mpy = mapData.mapPixelY + y;
                    var key = new DoubleInt() { Item1 = mpx, Item2 = mpy };

                    // MOBILE: (f) a cached null means "checked, no location here" - the miss is
                    // MOBILE: remembered, so GetMapPixelData runs once per map pixel per session.
                    Rect? cached;
                    if (LocationRectCache.TryGetValue(key, out cached))
                    {
                        if (cached.HasValue)
                            locations.Add(cached.Value);
                        continue;
                    }

                    var mapPixelPos = new DFPosition(mpx, mpy);
                    var mapPixelData = TerrainHelper.GetMapPixelData(dfUnity.ContentReader, mpx, mpy);

                    if (!mapPixelData.hasLocation)
                    {
                        LocationRectCache[key] = null;      // MOBILE: (f)
                        continue;
                    }

                    var location = dfUnity.ContentReader.MapFileReader.GetLocation(mapPixelData.mapRegionIndex, mapPixelData.mapLocationIndex);

                    var locationRect = GetLocationRect(location);
                    if (locationRect.width == 0 || locationRect.height == 0)
                    {
                        LocationRectCache[key] = null;      // MOBILE: (f)
                        continue;
                    }
                    locationRect = ExpandInEachDirection(locationRect, 1);

                    LocationRectCache[key] = locationRect;  // MOBILE: (f) indexer, not Add
                    locations.Add(locationRect);
                }
            }

            x = (int)_x;
            y = (int)_y;

            // MOBILE: (d) the sweep visits 33x33 = 1089 map pixels and adds at most one Rect each, so
            // MOBILE: locations.Count is bounded by the shader's [1089] arrays. It can also be ZERO -
            // MOBILE: open water in the Iliac Bay - and Unity rejects a zero-length SetVectorArray. Pass
            // MOBILE: a one-element zero array in that case; locationCount 0 makes the shader ignore it.
            locationCount = locations.Count;
            var locationPositions = locationCount > 0
                ? locations.Select(r => new Vector4(r.min.x, r.min.y)).ToArray()
                : new Vector4[1];
            var locationSizes = locationCount > 0
                ? locations.Select(r => new Vector4(r.size.x, r.size.y)).ToArray()
                : new Vector4[1];

            cs.SetVector("terrainPosition", terrainPosition);
            cs.SetVector("terrainSize", terrainSize);
            cs.SetInt("heightmapResolution", heightmapResolution);
            cs.SetVector("locationPosition", locationRect.min);
            cs.SetVector("locationSize", locationRect.size);
            cs.SetVectorArray("locationPositions", locationPositions);   // MOBILE: (d)
            cs.SetVectorArray("locationSizes", locationSizes);           // MOBILE: (d)
            cs.SetInt("locationCount", locationCount);                   // MOBILE: (d)
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetTexture(k, "PortMap", InterestingTerrains.portMap);
            cs.SetTexture(k, "RoadMap", InterestingTerrains.roadMap);
            cs.SetTexture(k, "tileableNoise", InterestingTerrains.tileableNoise);
            cs.SetFloat("newHeight", Constants.TERRAIN_HEIGHT);
            cs.SetTexture(k, "mapPixelHeights", baseHeightmap);
            cs.SetBuffer(k, "heightmapBuffer", heightmapBuffers.heightmapBuffer);
            cs.SetBuffer(k, "rawNoise", heightmapBuffers.rawNoise);
            cs.SetBuffer(k, "locationHeightData", LocationHeightData);   // MOBILE: (b)
            cs.SetVector("worldSize", Utility.GetWorldVertexSize());

            var rd = Compatibility.BasicRoadsUtils.GetRoadData(mapData.mapPixelX, mapData.mapPixelY);
            cs.SetVectorArray("NW_NE_SW_SE", rd.NW_NE_SW_SE);
            cs.SetVectorArray("N_E_S_W", rd.N_E_S_W);

            csParams.ApplyToCS(cs);

            woodsFile.Buffer = originalHeightmapBuffer;
            HandleBaseMapSampleParams(ref mapData, ref cs, k);
            woodsFile.Buffer = alteredHeightmapBuffer;

            cs.Dispatch(k, res / x, res / y, 1);

            k = cs.FindKernel("TilemapComputer");
            cs.SetTexture(k, "BiomeMap", InterestingTerrains.biomeMap);
            cs.SetTexture(k, "DerivMap", InterestingTerrains.derivMap);
            cs.SetBuffer(k, "heightmapBuffer", heightmapBuffers.heightmapBuffer);
            cs.SetBuffer(k, "tilemapData", heightmapBuffers.tilemapData);
            cs.SetBuffer(k, "rawNoise", heightmapBuffers.rawNoise);

            cs.Dispatch(k, res / x, res / y, 1);

            // MOBILE: (g) passed by ref so the buffers this struct owns really are nulled out here and
            // MOBILE: the sampler's finally does not double-release them.
            BufferIO.ProcessBufferValuesAndDispose(ref heightmapBuffers, ref mapData);
        }

        private static Color32[] ToBasemap(byte[] heightBuffer)
        {
            var basemap = new Color32[heightBuffer.Length];

            for (int x = 0; x < WoodsFile.MapWidth; x++)
            {
                for(int y = 0; y < WoodsFile.MapHeight; y++)
                {
                    var idx = x + y * WoodsFile.MapWidth;
                    var sampleIdx = x + (WoodsFile.MapHeight - 1 - y) * WoodsFile.MapWidth;

                    var b = heightBuffer[sampleIdx];
                    basemap[idx] = new Color32(b, b, b, 255);
                }
            }

            return basemap;
        }

        void HandleBaseMapSampleParams(ref MapPixelData mapPixel, ref ComputeShader cs, int k)
        {
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

            // Divisor ensures continuous 0-1 range of height samples
            float div = (sampler.HeightmapDimension - 1) / 3f;

            // Read neighbouring height samples for this map pixel
            int mx = mapPixel.mapPixelX;
            int my = mapPixel.mapPixelY;
            int sDim = 4;

            // Replace with all zero values
            Byte[] GetHeightMapValuesRange1Dim(int mapPixelX, int mapPixelY, int dim)
            {
                Byte[] dstData = new Byte[dim * dim];
                for (int y = 0; y < dim; y++)
                {
                    for (int x = 0; x < dim; x++)
                    {
                        dstData[x + (y * dim)] = 255;
                    }
                }
                return dstData;
            }

            Byte[,] GetLargeHeightMapValuesRange(int mapPixelX, int mapPixelY, int dim)
            {
                const int len = 3;
                Byte[,] dstData = new Byte[dim * len, dim * len];
                for (int y = 0; y < dim; y++)
                {
                    for (int x = 0; x < dim; x++)
                    {
                        int startX = x * len;
                        int startY = y * len;
                        for (int iy = 0; iy < len; iy++)
                        {
                            for (int ix = 0; ix < len; ix++)
                            {
                                dstData[startX + ix, startY + iy] = 255;
                            }
                        }
                    }
                }
                return dstData;
            }

            // Use adapted methods
            var shmByte = GetHeightMapValuesRange1Dim(mx - 2, my - 2, sDim);
            var shm = new float[shmByte.Length];
            for (int i = 0; i < shm.Length; i++)
            {
                shm[i] = Convert.ToSingle(shmByte[i]);
            }

            byte[,] lhm2 = GetLargeHeightMapValuesRange(mx - 1, my, 3);
            float[] lhm = new float[lhm2.Length];
            int lDim = lhm2.GetLength(0);
            int idx = 0;
            for (int y = 0; y < lDim; y++)
            {
                for (int x = 0; x < lDim; x++)
                {
                    lhm[idx++] = Convert.ToSingle(lhm2[x, y]);
                }
            }

            // Extract height samples for all chunks
            int hDim = sampler.HeightmapDimension;

            // Create buffers with extracted heightmap data
            heightmapBuffers.shm = new ComputeBuffer(shm.Length, sizeof(float));
            heightmapBuffers.shm.SetData(shm);

            heightmapBuffers.lhm = new ComputeBuffer(lhm.Length, sizeof(float));
            heightmapBuffers.lhm.SetData(lhm);

            // Assign properties to CS
            cs.SetBuffer(k, "shm", heightmapBuffers.shm);
            cs.SetBuffer(k, "lhm", heightmapBuffers.lhm);
            cs.SetInt("sd", sDim);
            cs.SetInt("ld", lDim);
            cs.SetInt("hDim", hDim);
            cs.SetFloat("div", div);
            cs.SetInt("mapPixelX", mapPixel.mapPixelX);
            cs.SetInt("mapPixelY", mapPixel.mapPixelY);
            cs.SetFloat("maxTerrainHeight", 2308.5f);
            cs.SetFloat("baseHeightScale", 8f);
            cs.SetFloat("noiseMapScale", 4f);
            cs.SetFloat("extraNoiseScale", 10f);
            cs.SetFloat("scaledOceanElevation", 27.2f);
        }

        static Rect ExpandInEachDirection(Rect src, int amount)
        {
            var amt = new Vector2(amount, amount);
            var pos = src.position;
            var size = src.size;

            pos -= amt;
            size += amt * 2;

            return new Rect(pos, size);
        }

        static Rect GetLocationRect(DFLocation currentLocation)
        {
            var locationRect = DaggerfallLocation.GetLocationRect(currentLocation);

            var min = WorldCoordToTerrainPosition(locationRect.min);
            var max = WorldCoordToTerrainPosition(locationRect.max);

            return new Rect()
            {
                xMin = min.x,
                yMin = min.y,
                xMax = max.x,
                yMax = max.y
            };
        }

        static Vector2 WorldCoordToTerrainPosition(Vector2 worldCoords)
        {
            var posX = worldCoords.x / 32768 * Utility.GetTerrainVertexSize();
            var posY = worldCoords.y / 32768 * Utility.GetTerrainVertexSize();

            return new Vector2(posX, posY);
        }
    }
}
