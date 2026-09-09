// MOBILE PORT - source: github.com/drcarademono/wod-biomes @ 40449fc5fc55c85c8089b068acefe1db4b61534c
// File WODTerrainMaterialProvider.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
// MOBILE: using System.Linq removed - the Hammerfell region test is a HashSet lookup now.
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop;

namespace WorldOfDaggerfall
{
    public abstract class WODTerrainMaterialProvider : ITerrainMaterialProvider
    {

        public enum Climates
        {
            Ocean = 223,
            Desert = 224,
            Desert2 = 225, // seen in dak'fron
            Mountain = 226,
            Rainforest = 227,
            Swamp = 228,
            Subtropical = 229,
            MountainWoods = 230,
            Woodlands = 231,
            HauntedWoodlands = 232 // not sure where this is?
        }

        /// <summary>
        /// MOBILE: hoisted to class scope. Upstream built this seven-element string[] inside
        /// GetClimateInfo, which DaggerfallTerrain.PromoteTerrainData calls for every Mountain-climate
        /// terrain on the streaming path - an allocation plus a LINQ enumerator per terrain promote.
        /// </summary>
        static readonly HashSet<string> HammerfellRegions = new HashSet<string>
        {
            "Alik'r Desert", "Dragontail Mountains", "Dak'fron", "Lainlyn", "Tigonus", "Ephesus", "Santaki"
        };

        /// <summary>
        /// MOBILE: pure, so the self-test can pin it - and null-tolerant, because the caller reads the
        /// region name through a null-conditional GameManager chain (see GetClimateInfo).
        /// </summary>
        public static bool IsHammerfellRegion(string name)
        {
            return name != null && HammerfellRegions.Contains(name);
        }

        public abstract Material CreateMaterial();

        public abstract void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData);

        /// <summary>
        /// Parses climate informations and retrieves ground archive index.
        /// </summary>
        /// <param name="worldClimate">Index of world climate.</param>
        /// <returns>Texture archive index.</returns>
        protected int GetGroundArchive(int worldClimate)
        {
            return GetClimateInfo(worldClimate).GroundArchive;
        }

        /// <summary>
        /// Parses climate informations.
        /// </summary>
        /// <param name="worldClimate">Index of world climate.</param>
        /// <returns>Parsed climate informations.</returns>
        protected virtual (int GroundArchive, DFLocation.ClimateSettings Settings, bool IsWinter) GetClimateInfo(int worldClimate)
        {
            // Get current climate and ground archive using the provided method
            DFLocation.ClimateSettings climate = MapsFile.GetWorldClimateSettings(worldClimate);
            int groundArchive = climate.GroundArchive;
            bool isWinter = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter;

            // Handle winter season for climates that change during winter, excluding specific ones
            if (isWinter && climate.ClimateType != DFLocation.ClimateBaseType.Desert &&
                //worldClimate != (int)Climates.Rainforest && 
                worldClimate != (int)Climates.Subtropical && 
                worldClimate != (int)Climates.Desert &&
                worldClimate != (int)Climates.Desert2)
            {
                // Default winter behavior (increment groundArchive), will be overridden for specific cases below
                groundArchive++;
            }

            // Adjust groundArchive based on specific climates
            switch (worldClimate)
            {
                case (int)Climates.Subtropical:
                    groundArchive = 4; // No change in winter
                    break;
                case (int)Climates.Desert2:
                    groundArchive = 3; // No change in winter
                    break;
                case (int)Climates.Mountain:
                    // MOBILE: null-guarded. This is the only unguarded dereference the port had, and it
                    // runs inside DaggerfallTerrain.PromoteTerrainData with no try/catch of its own, so a
                    // throw here breaks terrain promotion repeatedly and silently. No GPS (or no region
                    // name) now falls through to the unmodified groundArchive instead.
                    if (IsHammerfellRegion(GameManager.Instance?.PlayerGPS?.CurrentRegionName))
                    {
                        groundArchive = isWinter ? 103 : 104; // Special winter handling for Hammerfell Mountains
                    }
                    break;
                case (int)Climates.HauntedWoodlands:
                    groundArchive = isWinter ? 303 : 304; // Special winter handling for Haunted Woodlands
                    break;
            }

            return (groundArchive, climate, isWinter);
        }
    }

    public class WODTilemapTerrainMaterialProvider : WODTerrainMaterialProvider
    {
        // MOBILE: MobileShaders.Find, so this is the player's own shader and never the copy a mod
        // bundle embeds alongside its materials.
        private readonly Shader shader = DaggerfallWorkshop.Game.Mobile.MobileShaders.Find(MaterialReader._DaggerfallTilemapShaderName);

        public override Material CreateMaterial()
        {
            return new Material(shader);
        }

        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData)
        {
            Material tileSetMaterial = DaggerfallUnity.Instance.MaterialReader.GetTerrainTilesetMaterial(GetGroundArchive(terrainMaterialData.WorldClimate));
            terrainMaterialData.Material.SetTexture(TileUniforms.TileAtlasTex, tileSetMaterial.GetTexture(TileUniforms.TileAtlasTex));
            terrainMaterialData.Material.SetTexture(TileUniforms.TilemapTex, terrainMaterialData.TileMapTexture);
            terrainMaterialData.Material.SetInt(TileUniforms.TilemapDim, MapsFile.WorldMapTileDim);
        }
    }

    public class WODTilemapTextureArrayTerrainMaterialProvider : WODTerrainMaterialProvider
    {
        // MOBILE: MobileShaders.Find (see above).
        private readonly Shader shader = DaggerfallWorkshop.Game.Mobile.MobileShaders.Find(MaterialReader._DaggerfallTilemapTextureArrayShaderName);

        internal static bool IsSupported => SystemInfo.supports2DArrayTextures && DaggerfallUnity.Settings.EnableTextureArrays;

        public override Material CreateMaterial()
        {
            return new Material(shader);
        }

        public override void PromoteMaterial(DaggerfallTerrain daggerfallTerrain, TerrainMaterialData terrainMaterialData)
        {
            Material tileMaterial = DaggerfallUnity.Instance.MaterialReader.GetTerrainTextureArrayMaterial(GetGroundArchive(terrainMaterialData.WorldClimate));

            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileNormalMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileNormalMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileParallaxMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileParallaxMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TileMetallicGlossMapTexArr, tileMaterial.GetTexture(TileTexArrUniforms.TileMetallicGlossMapTexArr));
            terrainMaterialData.Material.SetTexture(TileTexArrUniforms.TilemapTex, terrainMaterialData.TileMapTexture);

            AssignKeyword("NORMAL_MAP", tileMaterial, terrainMaterialData.Material);
            AssignKeyword("HEIGHT_MAP", tileMaterial, terrainMaterialData.Material);
            AssignKeyword("METALLIC_GLOSS_MAP", tileMaterial, terrainMaterialData.Material);
        }

        private void AssignKeyword(string keyword, Material src, Material dst)
        {
            if (src.IsKeywordEnabled(keyword)) dst.EnableKeyword(keyword);
            else dst.DisableKeyword(keyword);
        }
    }
}

