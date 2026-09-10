// MOBILE PORT - source: github.com/TheLacus/daggerfall-unity-mods @ 556ef6e1dd0f2da95aa34275a30861daf58fee86
// File RealGrass/Scripts/DetailPrototypesManager.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (RealGrass/LICENSE): MIT, Copyright (c) 2016-2019 Uncanny_Valley, TheLacus.
//
// MOBILE: two behaviour edits, both forced by what the iOS bundle contains. The Desert climate is
// pointed at BrownGrass_tex (upstream asks for a DesertGrass_tex that does not exist in the repo,
// in any style - see UpdateClimateDesert), and the water-plants colour reset is put behind its own
// option (with water plants off it was resetting the GRASS prototype's colours - see
// UpdateClimateSummer). Everything the Mixed and Full styles reach - the VMblast GrassDetails_*
// and Grass_tex textures, the FBX prototypes, the stones and the plants - is unreachable in this
// port's configuration and none of it is in the bundle.

// Project:         Real Grass for Daggerfall Unity
// Web Site:        http://forums.dfworkshop.net/viewtopic.php?f=14&t=17
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/TheLacus/realgrass-du-mod
// Original Author: TheLacus
// Contributors:    

using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RealGrass
{
    #region Structs

    public struct GrassColors
    {
        public Color
            SpringHealthy,
            SpringDry,
            SummerHealty,
            SummerDry,
            FallHealty,
            FallDry;

        public bool SeasonInterpolation;
    }

    public struct PrototypesProperties
    {
        public Range<float> GrassHeight;
        public Range<float> GrassWidth;
        public float NoiseSpread;
        public GrassColors GrassColors;
        public bool UseGrassShader;
        public bool TextureOverride;
    }

    public struct GrassDetail
    {
        public string Name;
        public float WidthModifier;
        public float HeightModifier;
    }

    #endregion

    /// <summary>
    /// Manages terrain detail prototypes.
    /// </summary>
    public class DetailPrototypesManager
    {
        private class SizeColor
        {
            private readonly GrassColors grassColors;
            private readonly Range<float> grassHeight;
            private readonly float noiseSpread;

            protected GrassStyle GrassStyle { get; }
            protected DetailPrototype Grass { get; }
            protected DetailPrototype GrassDetail { get; }
            protected DetailPrototype GrassAccent { get; }

            internal SizeColor(GrassStyle grassStyle, GrassColors grassColors, Range<float> grassHeight, float noiseSpread, DetailPrototype grass, DetailPrototype detail, DetailPrototype accent)
            {
                this.GrassStyle = grassStyle;
                this.grassColors = grassColors;
                this.grassHeight = grassHeight;
                this.noiseSpread = noiseSpread;
                this.Grass = grass;
                this.GrassDetail = detail;
                this.GrassAccent = accent;
            }

            internal void Refresh(int detail, int accent)
            {
                SetGrassColor(grassColors);
                SetGrassSize(grassHeight, detail, accent);
                Grass.noiseSpread = noiseSpread;
            }

            internal void RefreshDesert()
            {
                Grass.healthyColor = Color.white;
                Grass.dryColor = new Color(0.89f, 0.67f, 0.67f);
                Grass.minHeight = grassHeight.Min;
                Grass.maxHeight = grassHeight.Max;
                Grass.noiseSpread = 0.8f;
            }

            protected virtual void SetGrassColor(GrassColors grassColors)
            {
                DaggerfallDateTime daggerfallDateTime = DaggerfallUnity.Instance.WorldTime.Now;
                switch (daggerfallDateTime.SeasonValue)
                {
                    case DaggerfallDateTime.Seasons.Spring:
                        Grass.healthyColor = grassColors.SpringHealthy;
                        Grass.dryColor = grassColors.SpringDry;
                        break;
                    case DaggerfallDateTime.Seasons.Summer:
                        Grass.healthyColor = grassColors.SummerHealty;
                        Grass.dryColor = grassColors.SummerDry;
                        break;
                    case DaggerfallDateTime.Seasons.Fall:
                        Grass.healthyColor = grassColors.FallHealty;
                        Grass.dryColor = grassColors.FallDry;
                        break;
                    default:
                        Grass.healthyColor = Color.white;
                        Grass.dryColor = Color.white;
                        break;
                }
            }

            protected virtual void SetGrassSize(Range<float> grassHeight, int detail, int accent)
            {
                Grass.minHeight = grassHeight.Min;
                Grass.maxHeight = grassHeight.Max;

                if ((GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed)
                    SetDetailGrassSize(detail, accent);
            }

            protected void SetDetailGrassSize(int detail, int accent)
            {
                ScaleGrassDetail(Grass, GrassDetail, grassDetails[detail]);
                ScaleGrassDetail(Grass, GrassAccent, grassAccents[accent]);
            }
        }

        private class InterpolatedSizeColor : SizeColor
        {
            internal InterpolatedSizeColor(GrassStyle grassStyle, GrassColors grassColors, Range<float> grassHeight, float noiseSpread, DetailPrototype grass, DetailPrototype detail, DetailPrototype accent)
                : base(grassStyle, grassColors, grassHeight, noiseSpread, grass, detail, accent)
            {
            }

            protected override void SetGrassColor(GrassColors grassColors)
            {
                DaggerfallDateTime daggerfallDateTime = DaggerfallUnity.Instance.WorldTime.Now;
                int day = daggerfallDateTime.DayOfYear;
                if (day < DaysOfYear.Spring)
                {
                    float t = Mathf.InverseLerp(DaysOfYear.GrowDay, DaysOfYear.Spring, day);
                    Grass.healthyColor = Color.Lerp(Color.white, grassColors.SpringHealthy, t);
                    Grass.dryColor = Color.Lerp(Color.white, grassColors.SpringDry, t);
                }
                else if (day <= DaysOfYear.MidYear)
                {
                    float t = Mathf.InverseLerp(DaysOfYear.Spring, DaysOfYear.MidYear, day);
                    Grass.healthyColor = Color.Lerp(grassColors.SpringHealthy, grassColors.SummerHealty, t);
                    Grass.dryColor = Color.Lerp(grassColors.SpringDry, grassColors.SummerDry, t);
                }
                else if (day < DaysOfYear.Winter)
                {
                    float t = Mathf.InverseLerp(DaysOfYear.MidYear, DaysOfYear.Winter, day);
                    Grass.healthyColor = Color.Lerp(grassColors.SummerHealty, grassColors.FallHealty, t);
                    Grass.dryColor = Color.Lerp(grassColors.SummerDry, grassColors.FallDry, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(DaysOfYear.Winter, DaysOfYear.DieDay, day);
                    Grass.healthyColor = Color.Lerp(grassColors.FallHealty, Color.white, t);
                    Grass.dryColor = Color.Lerp(grassColors.FallDry, Color.white, t);
                }
            }

            protected override void SetGrassSize(Range<float> grassHeight, int detail, int accent)
            {
                const int seasonalModifier = 65;
                const float minScale = 1 - (float)seasonalModifier / 100;

                int day = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;

                if (day < DaysOfYear.Spring)
                {
                    Grass.minHeight = grassHeight.Min * minScale;
                    Grass.maxHeight = grassHeight.Max * minScale;
                }
                else if (day < DaysOfYear.Summer)
                {
                    float t = Mathf.InverseLerp(DaysOfYear.Spring, DaysOfYear.Summer, day);
                    float scale = Mathf.SmoothStep(minScale, 1, t);

                    Grass.minHeight = grassHeight.Min * scale;
                    Grass.maxHeight = grassHeight.Max * scale;
                }
                else if (day < DaysOfYear.Fall)
                {
                    Grass.minHeight = grassHeight.Min;
                    Grass.maxHeight = grassHeight.Max;
                }
                else if (day < DaysOfYear.Winter)
                {
                    float t = Mathf.InverseLerp(DaysOfYear.Fall, DaysOfYear.Winter, day);
                    float scale = Mathf.SmoothStep(minScale, 1, 1 - t);

                    Grass.minHeight = grassHeight.Min * scale;
                    Grass.maxHeight = grassHeight.Max * scale;
                }
                else
                {
                    Grass.minHeight = grassHeight.Min * minScale;
                    Grass.maxHeight = grassHeight.Max * minScale;
                }

                if ((GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed)
                    SetDetailGrassSize(detail, accent);
            }
        }

        #region Constants

        const string realisticGrass = "Grass";
        const string brownGrass = "BrownGrass";
        const string greenGrass = "GreenGrass";
        // MOBILE: kept for the record, unreferenced now. Both of upstream's uses were
        // SetGrass(desertGrass, desertGrass), i.e. mod.GetAsset<Texture2D>("DesertGrass_tex") in
        // billboard mode - and there is no DesertGrass_tex anywhere in the upstream repo (the
        // desert asset is the VMblast DesertGrass.psd, asset name "DesertGrass", which this port
        // does not ship and could not use as a billboard texture anyway).
        const string desertGrass = "DesertGrass";
        const string plantsTemperate = "PlantsTemperate";
        const string plantsSwamp = "PlantsSwamp";
        const string plantsMountain = "NearWaterGrass";
        const string plantsDesert = "PlantsDesert";
        const string rock = "Rock";
        const string rockWinter = "RockWinter";

        struct UpdateType { public const short Summer = 0, Winter = 1, Desert = 2; }

        #endregion

        #region Fields

        static GameObject grassDetailPrefab;
        static GameObject grassAccentPrefab;

        static readonly GrassDetail[] grassDetails = new GrassDetail[]
        {
            new GrassDetail()
            {
                Name = "GrassDetails_01",
                WidthModifier = 1.0f,
                HeightModifier = 2.0f
            },
            new GrassDetail()
            {
                Name = "GrassDetails_02",
                WidthModifier = 1.0f,
                HeightModifier = 2.0f
            },
            new GrassDetail()
            {
                Name = "GrassDetails_04",
                WidthModifier = 1.0f,
                HeightModifier = 2.0f
            },
            new GrassDetail()
            {
                Name = "GrassDetails_05",
                WidthModifier = 1.0f,
                HeightModifier = 2.0f
            }
        };

        static readonly GrassDetail[] grassAccents = new GrassDetail[]
        {
            new GrassDetail()
            {
                Name = "GrassDetails_03",
                WidthModifier = 0.65f,
                HeightModifier = 0.65f
            },
            new GrassDetail()
            {
                Name = "GrassDetails_06",
                WidthModifier = 0.65f,
                HeightModifier = 0.65f
            }
        };

        private readonly Mod mod;
        private readonly Transform parent;
        private readonly RealGrassOptions options;
        private readonly SizeColor sizeColor;
        private readonly bool useGrassShader;
        private readonly bool textureOverride;
        private int currentGrassDetail;
        private int currentGrassAccent;
        int currentkey = -1;

        #endregion

        #region Properties

        /// <summary>
        /// Detail prototypes used by the terrain.
        /// </summary>
        public DetailPrototype[] DetailPrototypes { get; }

        public int Grass { get; private set; }
        public int GrassDetails { get; private set; }
        public int GrassAccents { get; private set; }
        public int WaterPlants { get; private set; }
        public int Rocks { get; private set; }

        #endregion

        #region Constructor

        public DetailPrototypesManager(Mod mod, Transform parent, RealGrassOptions options, PrototypesProperties properties)
        {
            Color healthyColor = new Color(0.70f, 0.70f, 0.70f);
            Color dryColor = new Color(0.40f, 0.40f, 0.40f);

            this.mod = mod;
            this.parent = parent;
            this.options = options;
            textureOverride = properties.TextureOverride;
            float noiseSpread = properties.NoiseSpread;
            useGrassShader = properties.UseGrassShader;

            List<DetailPrototype> detailPrototypes = new List<DetailPrototype>();
            int index = 0;

            var grassPrototypes = new DetailPrototype()
            {
                minWidth = properties.GrassWidth.Min,
                maxWidth = properties.GrassWidth.Max,
                noiseSpread = properties.NoiseSpread,
                renderMode = useGrassShader ? DetailRenderMode.Grass : DetailRenderMode.GrassBillboard,
                usePrototypeMesh = useGrassShader
            };
            detailPrototypes.Add(grassPrototypes);
            Grass = index;

            if ((options.GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed)
            {
                detailPrototypes.Add(new DetailPrototype()
                {
                    minWidth = properties.GrassWidth.Min,
                    maxWidth = properties.GrassWidth.Max,
                    noiseSpread = properties.NoiseSpread,
                    healthyColor = healthyColor,
                    dryColor = dryColor,
                    renderMode = useGrassShader ? DetailRenderMode.Grass : DetailRenderMode.GrassBillboard,
                    usePrototypeMesh = useGrassShader
                });
                GrassDetails = ++index;

                detailPrototypes.Add(new DetailPrototype()
                {
                    minWidth = properties.GrassWidth.Min,
                    maxWidth = properties.GrassWidth.Max,
                    noiseSpread = properties.NoiseSpread,
                    healthyColor = healthyColor,
                    dryColor = dryColor,
                    renderMode = useGrassShader ? DetailRenderMode.Grass : DetailRenderMode.GrassBillboard,
                    usePrototypeMesh = useGrassShader
                });
                GrassAccents = ++index;
            }

            if (options.WaterPlants)
            {
                var waterPlantsNear = new DetailPrototype()
                {
                    usePrototypeMesh = true,
                    noiseSpread = 0.6f,
                    healthyColor = healthyColor,
                    dryColor = dryColor,
                    renderMode = DetailRenderMode.Grass
                };
                detailPrototypes.Add(waterPlantsNear);
                WaterPlants = ++index;
            }

            if (options.TerrainStones)
            {
                detailPrototypes.Add(new DetailPrototype()
                {
                    minWidth = 0.4f,
                    maxWidth = 1,
                    minHeight = 0.25f,
                    maxHeight = 1.5f,
                    usePrototypeMesh = true,
                    noiseSpread = 1,
                    renderMode = DetailRenderMode.VertexLit
                });
                Rocks = ++index;
            }

            DetailPrototypes = detailPrototypes.ToArray();

            GrassColors grassColors = properties.GrassColors;
            Range<float> grassHeight = properties.GrassHeight;
            // MOBILE: unchanged, and deliberately so - with Classic style GrassDetails and
            // GrassAccents are both 0, so the two prototypes handed over below are the grass
            // prototype itself. Nothing touches them: every path that does is behind a
            // (GrassStyle & Mixed) == Mixed test.
            sizeColor = grassColors.SeasonInterpolation ?
                new InterpolatedSizeColor(options.GrassStyle, grassColors, grassHeight, noiseSpread, detailPrototypes[Grass], detailPrototypes[GrassDetails], detailPrototypes[GrassAccents]) :
                new SizeColor(options.GrassStyle, grassColors, grassHeight, noiseSpread, detailPrototypes[Grass], detailPrototypes[GrassDetails], detailPrototypes[GrassAccents]);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load assets for Summer.
        /// </summary>
        public void UpdateClimateSummer(ClimateBases currentClimate)
        {
            RefreshGrassDetails();
            sizeColor.Refresh(currentGrassDetail, currentGrassAccent);

            if ((options.GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed)
            {
                SetGrassDetail(GrassDetails, grassDetails[currentGrassDetail], ref grassDetailPrefab);
                SetGrassDetail(GrassAccents, grassAccents[currentGrassAccent], ref grassAccentPrefab);
            }

            if (!NeedsUpdate(UpdateType.Summer, currentClimate))
                return;

            // MOBILE: guarded. WaterPlants is the layer INDEX, and it is 0 when water plants are
            // off - which is the index of the grass layer, so this line was resetting the grass
            // prototype's healthy/dry colours (to 0.70/0.40 grey) on every climate or season
            // change, undoing the seasonal colours sizeColor.Refresh had just set.
            if (options.WaterPlants)
                ResetColor(DetailPrototypes[WaterPlants]);
            switch (currentClimate)
            {
                case ClimateBases.Mountain:
                    SetGrass(brownGrass, realisticGrass);
                    if (options.WaterPlants)
                    {
                        DetailPrototypes[WaterPlants].healthyColor = new Color(0.66f, 0.88f, 0.20f);
                        DetailPrototypes[WaterPlants].dryColor = new Color(0.95f, 0.33f, 0.36f);
                        DetailPrototypes[WaterPlants].prototype = LoadGameObject(plantsMountain);
                    }
                    break;

                case ClimateBases.Swamp:
                    SetGrass(brownGrass, realisticGrass);
                    if (options.WaterPlants)
                        DetailPrototypes[WaterPlants].prototype = LoadGameObject(plantsSwamp);
                    break;

                case ClimateBases.Temperate:
                    SetGrass(greenGrass, realisticGrass);
                    if (options.WaterPlants)
                        DetailPrototypes[WaterPlants].prototype = LoadGameObject(plantsTemperate);
                    break;

                default:
                    throw new ArgumentException("Invalid climate", nameof(currentClimate));
            }

            if (options.TerrainStones)
            {
                DetailPrototypes[Rocks].prototype = LoadGameObject(rock);
                DetailPrototypes[Rocks].healthyColor = new Color(0.70f, 0.70f, 0.70f);
                DetailPrototypes[Rocks].dryColor = new Color(0.40f, 0.40f, 0.40f);
            }
        }

        /// <summary>
        /// Load assets for Winter.
        /// </summary>
        public void UpdateClimateWinter(ClimateBases currentClimate)
        {
            bool drawGrass = IsGrassTransitioning();
            if (drawGrass)
                sizeColor.Refresh(currentGrassDetail, currentGrassAccent);

            if (!NeedsUpdate(UpdateType.Winter, currentClimate))
                return;

            switch (currentClimate)
            {
                case ClimateBases.Mountain:
                    if (drawGrass)
                        SetGrass(brownGrass, realisticGrass);
                    break;

                case ClimateBases.Swamp:
                    if (drawGrass)
                        SetGrass(brownGrass, realisticGrass);
                    break;

                case ClimateBases.Temperate:
                    if (drawGrass)
                        SetGrass(greenGrass, realisticGrass);
                    break;

                default:
                    throw new ArgumentException("Invalid climate", nameof(currentClimate));
            }

            if (options.TerrainStones)
            {
                DetailPrototypes[Rocks].prototype = LoadGameObject(rockWinter);
                DetailPrototypes[Rocks].healthyColor = new Color(0.70f, 0.70f, 0.70f);
                DetailPrototypes[Rocks].dryColor = new Color(0.40f, 0.40f, 0.40f);
            }
        }

        /// <summary>
        /// Load assets for Desert, which doesn't support seasons.
        /// </summary>
        public void UpdateClimateDesert()
        {
            if (!NeedsUpdate(UpdateType.Desert, ClimateBases.Desert))
                return;

            // MOBILE: brownGrass, not desertGrass. Upstream's SetGrass(desertGrass, desertGrass)
            // resolves to mod.GetAsset<Texture2D>("DesertGrass_tex"), which exists in no version of
            // this mod: even a full upstream bundle logs "Failed to load asset: DesertGrass_tex" and
            // leaves prototypeTexture null, so desert terrains drew nothing in Billboard style and
            // every climate change cost a warning. The dry brown grass is what the sand wants
            // anyway, and RefreshDesert below recolours and re-sizes it for the desert regardless.
            SetGrass(brownGrass, brownGrass);
            sizeColor.RefreshDesert();
            DetailPrototype detailPrototype = DetailPrototypes[Grass];
            detailPrototype.healthyColor = Color.white;
            detailPrototype.dryColor = new Color(0.89f, 0.67f, 0.67f);
            detailPrototype.noiseSpread = 0.8f;

            if (options.WaterPlants)
            {
                ResetColor(DetailPrototypes[WaterPlants]);
                DetailPrototypes[WaterPlants].prototype = LoadGameObject(plantsDesert);
            }

            if (options.TerrainStones)
            {
                DetailPrototypes[Rocks].prototype = LoadGameObject(rock);
                DetailPrototypes[Rocks].healthyColor = Color.white;
                DetailPrototypes[Rocks].dryColor = new Color(0.85f, 0.85f, 0.85f);
            }
        }

        #endregion

        #region Private Methods

        private void SetGrass(string classic, string realistic)
        {
            string assetName = (options.GrassStyle & GrassStyle.Full) == GrassStyle.Full ? realistic : classic;

            if (!useGrassShader)
                DetailPrototypes[Grass].prototypeTexture = LoadTexture(assetName + "_tex");
            else
                DetailPrototypes[Grass].prototype = LoadGameObject(assetName);
        }

        private void SetGrassDetail(int layer, GrassDetail grassDetail, ref GameObject prefab)
        {
            Texture2D tex = LoadTexture(grassDetail.Name);

            if (!useGrassShader)
            {
                DetailPrototypes[layer].prototypeTexture = tex;
            }
            else
            {
                if (!prefab)
                {
                    prefab = UnityEngine.Object.Instantiate(LoadGameObject("GrassDetails"), parent);
                    prefab.SetActive(false);
                }

                GameObject go = prefab;
                go.GetComponent<Renderer>().material.mainTexture = tex;
                DetailPrototypes[layer].prototype = go;
            }
        }

        /// <summary>
        /// Import texture from loose files or from mod.
        /// </summary>
        /// <param name="name">Name of texture.</param>
        private Texture2D LoadTexture(string name)
        {
            if (TryImportTextureFromLooseFiles(name, out Texture2D tex))
                return tex;

            return mod.GetAsset<Texture2D>(name);
        }

        /// <summary>
        /// Import gameobject from mod and override material with texture from loose files.
        /// </summary>
        /// <param name="name">Name of gameobject.</param>
        private GameObject LoadGameObject(string name)
        {
            GameObject go = mod.GetAsset<GameObject>(name);

            if (TryImportTextureFromLooseFiles(name, out Texture2D tex))
                go.GetComponent<MeshRenderer>().material.mainTexture = tex;

            return go;
        }

        private bool TryImportTextureFromLooseFiles(string name, out Texture2D tex)
        {
            if (textureOverride)
                return TextureReplacement.TryImportTextureFromLooseFiles(Path.Combine(RealGrass.TexturesFolder, name), true, false, false, out tex);

            tex = null;
            return false;
        }

        /// <summary>
        /// True if season or climate changed and detail prototypes should be updated.
        /// </summary>
        private bool NeedsUpdate(short updateType, ClimateBases climate)
        {
            int key = (updateType << 16) + (short)climate;

            if (key == currentkey)
                return false;

            currentkey = key;
            return true;
        }

        private bool IsGrassTransitioning()
        {
            int day = DaggerfallUnity.Instance.WorldTime.Now.DayOfYear;
            return day > DaysOfYear.GrowDay || day < DaysOfYear.DieDay;
        }

        private void RefreshGrassDetails()
        {
            currentGrassDetail = UnityEngine.Random.Range(0, grassDetails.Length);
            currentGrassAccent = UnityEngine.Random.Range(0, grassAccents.Length);
        }

        private void ResetColor(DetailPrototype detailPrototype)
        {
            detailPrototype.healthyColor = new Color(0.70f, 0.70f, 0.70f);
            detailPrototype.dryColor = new Color(0.40f, 0.40f, 0.40f);
        }

        private static void ScaleGrassDetail(DetailPrototype reference, DetailPrototype prototype, GrassDetail detail)
        {
            prototype.minHeight = reference.minHeight * detail.HeightModifier;
            prototype.maxHeight = reference.maxHeight * detail.HeightModifier;
            prototype.minWidth = reference.minWidth * detail.WidthModifier;
            prototype.maxWidth = reference.maxWidth * detail.WidthModifier;
        }

        #endregion
    }
}
