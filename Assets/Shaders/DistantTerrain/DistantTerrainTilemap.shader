// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File DistantTerrainTilemap.shader. MOBILE: rewritten to sample three Texture2DArrays instead of
// twelve 2048^2 tileset atlases (~270 MB -> ~15 MB, twelve samplers -> three). See FarTerrainCommon.cginc.
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce; WoD-flavour additions carry no licence; private draft only.
//Distant Terrain Mod for Daggerfall Tools For Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//Contributor: MaoDeVaca
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

Shader "Daggerfall/DistantTerrain/DistantTerrainTilemap" {
	Properties {
		[HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "white" {}
		[HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
		[HideInInspector] _SplatTex3("Layer 3 (A)", 2D) = "white" {}
		[HideInInspector] _SplatTex2("Layer 2 (B)", 2D) = "white" {}
		[HideInInspector] _SplatTex1("Layer 1 (G)", 2D) = "white" {}
		[HideInInspector] _SplatTex0("Layer 0 (R)", 2D) = "white" {}

		// MOBILE: three seasonal tile texture ARRAYS replace the twelve 2048^2 biome atlases
		// (seasonal + permanent summer copy + permanent winter copy, x four biomes) this shader
		// used to declare. Each array packs the four biome tilesets' 56 records as 64^2 slices:
		//     slice = biome * _SlicesPerBiome + record
		// biome 0 desert / 1 mountain / 2 woodland / 3 swamp; record 0 water, 1 dirt, 2 grass,
		// 3 stone. C# binds them from TextureReader.GetTerrainTextureArray for archives
		// 2/102/302/402 (summer), 3/103/303/403 (winter), 4/104/304/404 (rain). ~15 MB against
		// ~270 MB, and three samplers against twelve. See FarTerrainCommon.cginc.
		_TileArraySummer ("Tile Texture Array Summer", 2DArray) = "" {}
		_TileArrayWinter ("Tile Texture Array Winter", 2DArray) = "" {}
		_TileArrayRain ("Tile Texture Array Rain", 2DArray) = "" {}
		_SlicesPerBiome ("Slices per Biome Tileset", Int) = 56
	    _SkyTex("Sky Texture", 2D) = "white" {}
		_FarTerrainTilemapTex("Tilemap (R)", 2D) = "red" {}
		_FarTerrainTilesetDim("Tileset Dimension (in tiles)", Int) = 16
		_FarTerrainTilemapDim("Tilemap Dimension (in tiles)", Int) = 1000
		_MaxIndex("Max Tileset Index", Int) = 255
		
		_SeaReflectionTex("Reflection Texture Sea Reflection", 2D) = "black" {}
		_UseSeaReflectionTex("specifies if sea reflection texture is used", Int) = 0

		_PlayerPosX("Player Position in X-direction on world map", Int) = 0
		_PlayerPosY("Player Position in Y-direction on world map", Int) = 0
		_TerrainDistance("Terrain Distance", Int) = 3
		_WaterHeightTransformed("water level on y-axis in world coordinates", Float) = 58.9
		_TextureSetSeasonCode("specifies which seasonal/weather texture set is used", Int) = 0

		// Altitude-based distant snow caps (see FarTerrainCommon.cginc). _SnowCapsEnabled = 0 disables
		// the feature. _SnowCapStartY/_SnowCapFullY are the world-Y snow line and _SnowCapNoiseY its
		// per-pixel deviation, pushed each frame by DistantTerrain.PushSnowlineHeights (Y defaults are
		// huge so no snow shows until C# pushes them). The per-region colour tint + snow mode arrive
		// via the _RegionData[] array (set from C#), indexed by the tilemap's baked G channel.
		_SnowCapsEnabled("Enable Snow Caps", Int) = 1
		_SnowCapStartY("Snow Cap Start Y (world units)", Float) = 1000000
		_SnowCapFullY("Snow Cap Full Y (world units)", Float) = 1000000
		_SnowCapNoiseY("Snow Cap Noise Y (world units)", Float) = 0
		_EnableTreesAndDirt("Enable Trees And Dirt", Int) = 1
		_IPNMWater("IPNM Water Mode", Int) = 0
		_IPNMWaterBrightness("IPNM Water Brightness", Float) = 1.0
		_IPNMWaterOpacity("IPNM Water Opacity", Float) = 1.0
		_BlendStart("blend start distance", Float) = 15000.0
		_BlendEnd("blend end distance", Float) = 200000.0
		_FogMode("Fog Mode", Int) = 1
		_FogDensity("Fog Density", Float) = 0.01 
		_FogStartDistance("Fog Start Distance", Float) = 0 
		_FogEndDistance("Fog End Distance", Float) = 2000 
		_FogFromSkyTex("specifies if fog color should be derived from sky texture", Int) = 0

		// Width (in map tiles) of the fade band at the world boundary that hides the
		// ocean-textured padding when distance fog is disabled. Set to 0 to disable.
		_WorldEdgeFadeWidthTiles("World Edge Fade Width (tiles)", Float) = 25.0

		// Runtime kill-switch for the near-terrain cutout (e.g. during high-speed movement).
		_DisableCutout("Disable Terrain Cutout", Int) = 0

		// World-space depth the near-terrain boundary ring is pinned down to form the skirt
		// that seals the near/far seam (see FarTerrainCommon.cginc). 0 disables the skirt.
		_SkirtDepth("Boundary Skirt Depth (world units)", Float) = 600.0
	}
	SubShader {
		Tags { "RenderType"="Transparent" "Queue" = "Transparent-499"}
		LOD 200
		
		ZWrite Off
		Cull Back

		CGPROGRAM
		// MOBILE: 3.0 -> 3.5. Texture2DArray sampling needs shader model 3.5 / GLES3; Metal has
		// had it since day one. #pragma glsl (a no-op since Unity 5) deleted with it.
		#pragma target 3.5
		// Lambert (matte diffuse) instead of Standard (PBR): see matching comment on the
		// second pass. Removes the specular sheen on the distant terrain.
		#pragma surface surf Lambert vertex:vert noforwardadd finalcolor:fcolor alpha:fade keepalpha nolightmap
		#pragma multi_compile_local __ ENABLE_WATER_REFLECTIONS

		#include "FarTerrainCommon.cginc"

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Pin the near-terrain boundary ring down into a hidden skirt that seals the
            // see-through seam against the detailed near terrain. skirtDrop (0 elsewhere) lets
            // the surf stage recover the tile's true height for the water/biome test.
            o.skirtDrop = applyFarTerrainSkirt(v.vertex, v.texcoord.xy);
            COMPUTE_EYEDEPTH(o.eyeDepth);
        }

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 terrainTileInfo = tex2D(_FarTerrainTilemapTex, IN.uv_MainTex).rgba;
			int mapPixelX = IN.uv_MainTex.x*_FarTerrainTilemapDim;
			int mapPixelY = 499 - IN.uv_MainTex.y*_FarTerrainTilemapDim;

			// Near-terrain cutout + boundary skirt. Discard the far-terrain INTERIOR of the near
			// footprint, but KEEP the innermost ring (tileDist == _TerrainDistance): the vertex
			// program (applyFarTerrainSkirt) has pinned that ring down into a hidden curtain that
			// seals the see-through seam where the coarse far terrain meets the detailed near
			// terrain. The near terrain still also has its own downward edge skirt; this far-side
			// skirt fills the residual hairline gap the two leave between them. Ring math lives in
			// farTerrainCulled (FarTerrainCommon.cginc); mapPixelX/Y above are still used by the
			// location beacon below.
			if (farTerrainCulled(IN.uv_MainTex))
			{
				discard;
			}

			float dist = distance(IN.worldPos.xz, _WorldSpaceCameraPos.xz);
			if (dist>_BlendEnd)
			{
				discard;
			}

			int index = terrainTileInfo.r * _MaxIndex;
			half4 c = getColorFromTerrain(IN, IN.uv_MainTex, _FarTerrainTilemapDim, _FarTerrainTilesetDim, index);
			
			// G channel carries the baked per-region treatment index (read in getColorFromTerrain),
			// not tree coverage; pass 0 so the legacy tree-coverage tint stays the no-op it already
			// was (the tree look is the procedural specks in the cginc).
			int treeCoverage = 0;
			int locationRangeX = terrainTileInfo.b * _MaxIndex;
			int locationRangeY = terrainTileInfo.a * _MaxIndex;
			// Skip tree specks on water (rivers/lakes/coast) - mirrors getColorFromTerrain's water test
			// so trees/dirt don't appear on the distant rivers.
			bool isWater = (index == 223) || ((IN.worldPos.y + IN.skirtDrop) < _WaterHeightTransformed);
			c.rgb = updateColorWithInfoForTreeCoverageAndLocations(c.rgb, treeCoverage, locationRangeX, locationRangeY, mapPixelX, mapPixelY, IN.uv_MainTex, index, isWater);

			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG

		// Depth Pass. Mirrors the skirt pin-down and the near-terrain cutout of the colour
		// passes so depth stays consistent with the pinned geometry. (The old fixed-function
		// empty pass wrote depth at the UN-pinned heights and ignored the cutout, which would
		// make the pinned skirt fail the depth test and never render.) Self-contained so it
		// need not #include the surface-lighting machinery in FarTerrainCommon.cginc; the ring
		// math below must stay in sync with farTileDistToPlayer / applyFarTerrainSkirt /
		// farTerrainCulled there.
		Pass {
			ZWrite On
			Cull Back
			ColorMask 0

			CGPROGRAM
			#pragma vertex vertDepth
			#pragma fragment fragDepth
			#pragma target 3.0
			#include "UnityCG.cginc"

			int _FarTerrainTilemapDim;
			int _PlayerPosX;
			int _PlayerPosY;
			int _TerrainDistance;
			int _DisableCutout;
			float _SkirtDepth;

			struct appdataDepth { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2fDepth { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

			int dtTileDist(float2 uv)
			{
				int mapPixelX = uv.x * _FarTerrainTilemapDim;
				int mapPixelY = 499 - uv.y * _FarTerrainTilemapDim;
				int distX = abs(mapPixelX + 1 - _PlayerPosX);
				int distY = abs(mapPixelY + 1 - _PlayerPosY);
				return max(distX, distY);
			}

			v2fDepth vertDepth(appdataDepth v)
			{
				v2fDepth o;
				if (_DisableCutout == 0 && dtTileDist(v.uv) <= _TerrainDistance)
					v.vertex.y -= _SkirtDepth; // pin the boundary ring down (matches applyFarTerrainSkirt)
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 fragDepth(v2fDepth i) : SV_Target
			{
				if (_DisableCutout == 0 && dtTileDist(i.uv) < _TerrainDistance)
					discard; // cull the footprint interior; keep the skirt ring (matches farTerrainCulled)
				return 0;
			}
			ENDCG
		}

		ZWrite On
		Cull Back

		CGPROGRAM
		// MOBILE: 3.0 -> 3.5 for the texture arrays (see the first pass).
		#pragma target 3.5
        // Lambert (matte diffuse) instead of Standard (PBR): the distant terrain is a flat
        // LOD backdrop and the Standard model gave it an unwanted specular sheen (visible as
        // a moon/sky reflection on the far hills). Lambert has no specular term at all, so the
        // far terrain reads as a matte surface. The surf functions only write Albedo/Alpha,
        // which is exactly what SurfaceOutput (Lambert) needs.
        #pragma surface surf Lambert vertex:vert noforwardadd finalcolor:fcolor alpha:fade keepalpha nolightmap
		#pragma multi_compile_local __ ENABLE_WATER_REFLECTIONS

		#include "FarTerrainCommon.cginc"

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Pin the near-terrain boundary ring down into a hidden skirt that seals the
            // see-through seam against the detailed near terrain. skirtDrop (0 elsewhere) lets
            // the surf stage recover the tile's true height for the water/biome test.
            o.skirtDrop = applyFarTerrainSkirt(v.vertex, v.texcoord.xy);
            COMPUTE_EYEDEPTH(o.eyeDepth);
        }

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 terrainTileInfo = tex2D(_FarTerrainTilemapTex, IN.uv_MainTex).rgba;
			int mapPixelX = IN.uv_MainTex.x*_FarTerrainTilemapDim;
			int mapPixelY = 499 - IN.uv_MainTex.y*_FarTerrainTilemapDim;

			// Near-terrain cutout + boundary skirt (see matching comment in the first pass).
			if (farTerrainCulled(IN.uv_MainTex))
			{
				discard;
			}

			float dist = distance(IN.worldPos.xz, _WorldSpaceCameraPos.xz);
			if (dist>_BlendEnd)
			{
				discard;
			}

			int index = terrainTileInfo.r * _MaxIndex;
			half4 c = getColorFromTerrain(IN, IN.uv_MainTex, _FarTerrainTilemapDim, _FarTerrainTilesetDim, index);
			
			// G channel carries the baked per-region treatment index (read in getColorFromTerrain),
			// not tree coverage; pass 0 so the legacy tree-coverage tint stays the no-op it already
			// was (the tree look is the procedural specks in the cginc).
			int treeCoverage = 0;
			int locationRangeX = terrainTileInfo.b * _MaxIndex;
			int locationRangeY = terrainTileInfo.a * _MaxIndex;
			// Skip tree specks on water (rivers/lakes/coast) - mirrors getColorFromTerrain's water test
			// so trees/dirt don't appear on the distant rivers.
			bool isWater = (index == 223) || ((IN.worldPos.y + IN.skirtDrop) < _WaterHeightTransformed);
			c.rgb = updateColorWithInfoForTreeCoverageAndLocations(c.rgb, treeCoverage, locationRangeX, locationRangeY, mapPixelX, mapPixelY, IN.uv_MainTex, index, isWater);
			
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	} 
	FallBack "Diffuse"
}
