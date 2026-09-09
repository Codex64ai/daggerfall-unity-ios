// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationTerrainNature.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System.Collections.Generic;
using System.Linq;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using UnityEngine;

namespace LocationLoader
{
    public class LocationTerrainNature : DefaultTerrainNature
    {
        public override void LayoutNature(DaggerfallTerrain dfTerrain, DaggerfallBillboardBatch dfBillboardBatch, float terrainScale, int terrainDist)
        {
            var ll = LocationModLoader.modObject.GetComponent<LocationLoader>();
            List<Rect> llLocationRects = null;
            if (ll.TryGetTerrainExtraData(new Vector2Int(dfTerrain.MapPixelX, dfTerrain.MapPixelY),
                    out LocationLoader.LLTerrainData terrainExtraData))
            {
                llLocationRects = terrainExtraData.LocationInstanceRects;
            }

            // Location Rect is expanded slightly to give extra clearance around locations
            Rect rect = dfTerrain.MapData.locationRect;
            if (rect.x > 0 && rect.y > 0)
            {
                rect.xMin -= natureClearance;
                rect.xMax += natureClearance;
                rect.yMin -= natureClearance;
                rect.yMax += natureClearance;
            }

            // Chance scaled based on map pixel height
            // This tends to produce sparser lowlands and denser highlands
            // Adjust or remove clamp range to influence nature generation
            float elevationScale = (dfTerrain.MapData.worldHeight / 128f);
            elevationScale = Mathf.Clamp(elevationScale, 0.4f, 1.0f);

            // Chance scaled by base climate type
            float climateScale = 1.0f;
            DFLocation.ClimateSettings climate = MapsFile.GetWorldClimateSettings(dfTerrain.MapData.worldClimate);
            switch (climate.ClimateType)
            {
                case DFLocation.ClimateBaseType.Desert:         // Just lower desert for now
                    climateScale = 0.25f;
                    break;
            }
            float chanceOnDirt = baseChanceOnDirt * elevationScale * climateScale;
            float chanceOnGrass = baseChanceOnGrass * elevationScale * climateScale;
            float chanceOnStone = baseChanceOnStone * elevationScale * climateScale;

            // Get terrain
            Terrain terrain = dfTerrain.gameObject.GetComponent<Terrain>();
            if (!terrain)
                return;

            // Get terrain data
            TerrainData terrainData = terrain.terrainData;
            if (!terrainData)
                return;

            // Remove exiting billboards
            dfBillboardBatch.Clear();
            MeshReplacement.ClearNatureGameObjects(terrain);

            // Seed random with terrain key
            Random.InitState(TerrainHelper.MakeTerrainKey(dfTerrain.MapPixelX, dfTerrain.MapPixelY));

            // Just layout some random flats spread evenly across entire map pixel area
            // Flats are aligned with tiles, max 16129 billboards per batch
            Vector2 tilePos = Vector2.zero;
            int tDim = MapsFile.WorldMapTileDim;
            int hDim = DaggerfallUnity.Instance.TerrainSampler.HeightmapDimension;
            float scale = terrainData.heightmapScale.x * (float)(hDim - 1) / (float)tDim;
            float maxTerrainHeight = DaggerfallUnity.Instance.TerrainSampler.MaxTerrainHeight;
            float beachLine = DaggerfallUnity.Instance.TerrainSampler.BeachElevation;

            for (int y = 0; y < tDim; y++)
            {
                for (int x = 0; x < tDim; x++)
                {
                    // Reject based on steepness
                    float steepness = terrainData.GetSteepness((float)x / tDim, (float)y / tDim);
                    if (steepness > maxSteepness)
                        continue;

                    // Reject if inside location rect (expanded slightly to give extra clearance around locations)
                    tilePos.x = x;
                    tilePos.y = y;
                    if (rect.x > 0 && rect.y > 0 && rect.Contains(tilePos))
                        continue;

                    // Reject if inside LL location rect ;)
                    if (llLocationRects != null)
                    {
                        if (llLocationRects.Any(r => r.Contains(tilePos)))
                            continue;
                    }

                    // Chance also determined by tile type
                    int tile = dfTerrain.MapData.tilemapSamples[x, y] & 0x3F;
                    if (tile == 1)
                    {   // Dirt
                        if (Random.Range(0f, 1f) > chanceOnDirt)
                            continue;
                    }
                    else if (tile == 2)
                    {   // Grass
                        if (Random.Range(0f, 1f) > chanceOnGrass)
                            continue;
                    }
                    else if (tile == 3)
                    {   // Stone
                        if (Random.Range(0f, 1f) > chanceOnStone)
                            continue;
                    }
                    else
                    {   // Anything else
                        continue;
                    }

                    int hx = (int)Mathf.Clamp(hDim * ((float)x / (float)tDim), 0, hDim - 1);
                    int hy = (int)Mathf.Clamp(hDim * ((float)y / (float)tDim), 0, hDim - 1);
                    float height = dfTerrain.MapData.heightmapSamples[hy, hx] * maxTerrainHeight;  // x & y swapped in heightmap for TerrainData.SetHeights()

                    // Reject if too close to water
                    if (height < beachLine)
                        continue;

                    // Sample height and position billboard
                    Vector3 pos = new Vector3(x * scale, 0, y * scale);
                    float height2 = terrain.SampleHeight(pos + terrain.transform.position);
                    pos.y = height2 - (steepness / slopeSinkRatio);

                    // Add to batch unless a mesh replacement is found
                    int record = Random.Range(1, 32);
                    if (terrainDist > 1 || !MeshReplacement.ImportNatureGameObject(dfBillboardBatch.TextureArchive, record, terrain, x, y))
                        dfBillboardBatch.AddItem(record, pos);
                    else if (!NatureMeshUsed)
                        NatureMeshUsed = true;  // Signal that nature mesh has been used to initiate extra terrain updates
                }
            }

            // Apply new batch
            dfBillboardBatch.Apply();
        }
    }
}
