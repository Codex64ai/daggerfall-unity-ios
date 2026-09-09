// MOBILE PORT - source: github.com/drcarademono/wod-biomes @ 40449fc5fc55c85c8089b068acefe1db4b61534c
// File WODBiomes.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections;
using UnityEngine;

namespace WorldOfDaggerfall
{
    public class WODBiomes : MonoBehaviour
    {
        public static WODBiomes instance;
        public static Mod Mod { get; private set; }

	    private static Mod VEMod;
	    public static bool VEModEnabled;

        #region Invoke
        // MOBILE: started by MobilePortedMods when the launcher entry is on
        // (upstream: [Invoke(StateManager.StateTypes.Start, 0)])
        public static void Init(InitParams initParams)
        {
            Mod = initParams.Mod;
            var go = new GameObject(Mod.Title);
            instance = go.AddComponent<WODBiomes>();

		    VEMod = ModManager.Instance.GetModFromGUID("1f124f8c-dd01-48ad-a5b9-0b4a0e4702d2");
		    if (VEMod != null && VEMod.Enabled)
		    {
			    VEModEnabled = true;
		    }
        }
        #endregion

        private void Start()
        {
            // Set WOD custom terrain material provider
            // MOBILE: which of the two providers is installed decides what the ground looks like, and
            // Task 9 could only infer it from the re-skinned terrain itself. One line, once per session
            // (Init builds exactly one WODBiomes), so the device log says it outright.
            bool textureArray = WODTilemapTextureArrayTerrainMaterialProvider.IsSupported;
            if(textureArray)
                DaggerfallUnity.Instance.TerrainMaterialProvider = new WODTilemapTextureArrayTerrainMaterialProvider();
            else                 
                DaggerfallUnity.Instance.TerrainMaterialProvider = new WODTilemapTerrainMaterialProvider();
            Debug.Log("[Biomes] terrain material provider installed: " + (textureArray ? "TextureArray" : "Atlas"));
        }
    }
}

