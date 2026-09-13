// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE pure: where every Unity TerrainData in this app is made.
//
// WHY THIS FILE EXISTS. A TerrainData built at runtime with `new TerrainData()` renders NO TERRAIN
// DETAILS in a player build - no grass, no detail meshes, nothing - while the identical code path
// renders them correctly in the Editor. It is Unity's own long-standing bug (issue tracker 10753,
// "Terrain Detail objects are not rendered in the build when the Terrain is generated at runtime";
// reported again for 2021.2+ and still present in 6000.3.23f1). The detail DATA is all there - the
// layers read back, Unity's own ComputeDetailInstanceTransforms returns thousands of correctly
// placed instances, the detail atlas is built and holds the prototype's pixels, the three engine
// detail shaders resolve and report isSupported - and the frame contains no billboards at all. A
// TerrainData that came from a SERIALIZED ASSET, instantiated at runtime, draws them.
//
// Measured in the iOS Simulator player on 2026-09-13, four terrains side by side in one frame:
//
//   new TerrainData(), runtime texture                          -> no grass
//   new TerrainData(), prototypes copied from the asset          -> no grass
//   Instantiate(TerrainData asset), prototypes untouched         -> GRASS
//   Instantiate(TerrainData asset), runtime texture swapped in   -> GRASS
//
// So the difference is the TerrainData object itself, not the prototypes, the textures or the
// layer data. DFU creates every world terrain with `new TerrainData()`
// (DaggerfallTerrain.PromoteTerrainData), which is why Real Grass drew nothing on device however
// its density, resolution, scatter mode or textures were set - and why every one of those was
// verified "correct" in a Player.log while the ground stayed bare.
//
// THE TEMPLATE. Assets/Resources/MobileTerrainDataTemplate.asset is a tiny TerrainData authored in
// the Editor (33-sample heightmap, one detail prototype). MobileBuildSetup.EnsureTerrainDataTemplate
// creates it if it is missing, so a clone cannot lose it silently. Everything DFU sets afterwards -
// heightmapResolution, size, SetDetailResolution, alphamapResolution, baseMapResolution, heights -
// is set on the instantiated copy exactly as it was on the constructed one.
//
// Place in Assets/Scripts/Game/Mobile/

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// MOBILE: creates the app's TerrainData objects from a serialized template, because a
    /// runtime-constructed one draws no terrain details in a player build. See the file header.
    /// </summary>
    public static class MobileTerrainData
    {
        /// <summary>Resources path of the template asset (no extension, no folder).</summary>
        public const string TemplateResourceName = "MobileTerrainDataTemplate";

        /// <summary>True once Create() has instantiated the template at least once.</summary>
        public static bool TemplateUsed { get; private set; }

        /// <summary>True once Create() has had to fall back to `new TerrainData()`.</summary>
        public static bool TemplateMissing { get; private set; }

        static bool logged;

        /// <summary>
        /// MOBILE: a TerrainData with Unity's detail rendering intact. Instantiated from the
        /// template asset; falls back to `new TerrainData()` (details will not draw in a player)
        /// only if the template is absent, and says so once.
        /// </summary>
        public static TerrainData Create()
        {
            TerrainData template = Resources.Load<TerrainData>(TemplateResourceName);
            if (template == null)
            {
                TemplateMissing = true;
                if (!logged)
                {
                    logged = true;
                    Debug.LogWarning("[MobileTerrainData] Resources/" + TemplateResourceName
                        + " is missing - terrains fall back to new TerrainData(), which draws no "
                        + "terrain details in a player build (Unity issue 10753).");
                }
                return new TerrainData();
            }

            TerrainData data = Object.Instantiate(template);
            if (!TemplateUsed)
            {
                TemplateUsed = true;
                Debug.Log("[MobileTerrainData] terrains are instantiated from Resources/"
                    + TemplateResourceName + " (a runtime-constructed TerrainData draws no details in a player)");
            }
            return data;
        }
    }
}
