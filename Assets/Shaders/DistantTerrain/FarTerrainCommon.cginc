// MOBILE PORT - source: github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall @ d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f
// File FarTerrainCommon.cginc. MOBILE: rewritten to sample three Texture2DArrays instead of twelve
// 2048^2 tileset atlases (~270 MB -> ~15 MB, twelve samplers -> three). Everything else is upstream.
// MIT base: Nystul-the-Magician/dfunity-mods DistantTerrain @ fc58546c3eae964babfdfeea51e97a47ba57cdce; WoD-flavour additions carry no licence; private draft only.
//Distant Terrain Mod for Daggerfall Tools For Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//Contributor: MaoDeVaca
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)
//based on the DaggerfallTilemap shader by Gavin Clayton (interkarma@dfworkshop.net)

#ifndef FAR_TERRAIN_COMMON_CG_INCLUDED
#define FAR_TERRAIN_COMMON_CG_INCLUDED

struct Input
{
	float4 pos : SV_POSITION;
	float2 uv_MainTex;
	float3 worldPos; // interpolated vertex positions used for correct coast line texturing
	float3 worldNormal; // interpolated vertex normals used for texturing terrain based on terrain slope
	float4 screenPos;
    float eyeDepth;
	float skirtDrop; // world-units this fragment's vertex was pinned down for the boundary skirt (0 elsewhere)
	INTERNAL_DATA
};

#define PI 3.1416f

// MOBILE: _TileAtlasTex / _TilemapTex / _BumpMap were declared but never sampled by this shader
// (leftovers from the near-terrain shader it was derived from) and are deleted, as are _AtlasSize
// and _GutterSize - atlas geometry that a texture array does not have.
int _TilesetDim;
int _TilemapDim;
int _MaxIndex;

float _blendWeightFarTerrainTop;
float _blendWeightFarTerrainBottom;
float _blendWeightFarTerrainLeft;
float _blendWeightFarTerrainRight;

// === Terrain tile texture ARRAYS (MOBILE: replaces the twelve 2048^2 atlases) ==============
// Upstream declared TWELVE sampler2D atlases here - four biomes (desert/mountain/woodland/swamp)
// x three sets (the current season, a permanent summer copy for the per-climate winter-snow
// opt-out, and a permanent winter copy for the snow caps). DistantTerrain.cs built each one at
// runtime with TextureReader.GetTerrainTilesetTexture: 2048x2048 ARGB32 + mips, ~22 MB apiece,
// ~270 MB resident. That is the single reason this file was rewritten.
//
// The same twelve tilesets now arrive as THREE Texture2DArrays, one per SEASON, each packing the
// four biome tilesets' 56 records as slices (4 x 56 = 224 slices of 64^2, ~5 MB per array), built
// from DFU's own TextureReader.GetTerrainTextureArray(archive, TextureMap.Albedo) - the same call
// the near terrain uses, so texture replacement (DET and friends) is honoured exactly as before.
// The "summer copy" and "winter copy" sampler sets are no longer separate textures at all: they
// are just the summer and winter ARRAYS, selected per fragment (see dtArrayForSet below).
//
//   slice = biome * _SlicesPerBiome + record          (_SlicesPerBiome == 56)
//
// with the biome and record constants below. C# side: DistantTerrain.SliceIndex(biome, record).
// Arrays, in _TextureSetSeasonCode order, so the season code indexes them directly:
//   _TileArraySummer  archives   2 / 102 / 302 / 402
//   _TileArrayWinter  archives   3 / 103 / 303 / 403
//   _TileArrayRain    archives   4 / 104 / 304 / 404
UNITY_DECLARE_TEX2DARRAY(_TileArraySummer);
UNITY_DECLARE_TEX2DARRAY(_TileArrayWinter);
UNITY_DECLARE_TEX2DARRAY(_TileArrayRain);
float4 _TileArraySummer_TexelSize; // .zw = slice width/height in texels; drives mip selection
int _SlicesPerBiome;               // 56 - records per biome tileset, the slice-block stride
// MOBILE: mip levels the three arrays carry (they are guaranteed to agree - BuildTileArrays checks
// it across the seasons). The tile sample below picks its mip level EXPLICITLY, and an explicit lod
// past an array's last level is undefined - black on some drivers - so this is the ceiling every
// lod is clamped to. 1 means there is no mip chain at all and only level 0 may be sampled. Pushed
// from DistantTerrain.TileArrayMipCount; the 1 default is the safe answer for an unbound material.
int _TileArrayMipCount;

// Array selector. Deliberately the same numbering as _TextureSetSeasonCode.
#define DT_ARRAY_SUMMER 0
#define DT_ARRAY_WINTER 1
#define DT_ARRAY_RAIN   2

// Biome -> slice block. MUST match DistantTerrain.SliceIndex's biome argument on the C# side.
#define DT_BIOME_DESERT   0
#define DT_BIOME_MOUNTAIN 1
#define DT_BIOME_WOODLAND 2
#define DT_BIOME_SWAMP    3

// The four tileset records the far terrain uses. Upstream addressed these as atlas CELLS
// 0 / 4 / 8 / 12, because GetTerrainTilesetTexture lays out four rotation/flip variants per record
// (cell = record * 4 + variant) and the far terrain always took variant 0. GetTerrainTextureArray
// has one slice per record, so the * 4 disappears.
#define DT_TILE_WATER 0
#define DT_TILE_DIRT  1
#define DT_TILE_GRASS 2
#define DT_TILE_STONE 3
// ===========================================================================================

sampler2D _FarTerrainTilemapTex;
int _FarTerrainTilesetDim; // used by FarTerrainTilemap shader, but not by TransitionRingTilemap shader
int _FarTerrainTilemapDim;

#if defined(ENABLE_WATER_REFLECTIONS)
	sampler2D _SeaReflectionTex;
	int _UseSeaReflectionTex;
#endif

float _WaterHeightTransformed;
int _TerrainDistance;
// Runtime kill-switch for the near-terrain cutout + boundary skirt. Held OFF during teleport /
// high-speed streaming so the far terrain covers the player's own tile until the near terrain
// lands. Declared here (it used to be declared per-pass) so the shared cutout/skirt helpers
// below can reference it.
int _DisableCutout;
// World-space depth the near-terrain boundary ring is pinned straight down to form the
// seam-sealing skirt (see the helper block below). Pushed by DistantTerrain.cs
// (farTerrainSkirtDepth). 0 disables the skirt (falls back to a hard cutout edge).
float _SkirtDepth;
int _MapPixelX;
int _MapPixelY;
int _PlayerPosX;
int _PlayerPosY;
int _TextureSetSeasonCode;
sampler2D _SkyTex;
float _BlendStart;
float _BlendEnd;
int _FogMode;
int _FogFromSkyTex;
float _FogDensity;
float _FogStartDistance;
float _FogEndDistance;

// World-edge fog: width (in map tiles) of the fade band that hides the
// ocean-textured padding at the world boundary. Position-based, not
// distance-based — applies even when distance fog is disabled. Set to 0
// to disable entirely. Width is the distance from the world edge (in
// tiles) over which the fragment is blended toward the sky color.
float _WorldEdgeFadeWidthTiles;

// Master kill-switch for the distant-location beacon overlay. Pushed by
// DistantTerrain.cs from DistantTerrainLocationConfig.HighlightLocations.
// When 0, updateColorWithInfoForTreeCoverageAndLocations skips the beacon
// branch entirely and the LOD mesh renders the same way it did before the
// feature was added, even if non-zero markers happen to be present in the
// tilemap texture's B/A channels.
int _HighlightLocations;

// Per-climate "keep summer look in winter" flags (1 = disable snow for that climate), indexed by
// (climate - 223): [0]=Ocean 223 ... [10]=Maquis 233. Pushed from DistantTerrainWinterSnowConfig.
// Only consulted when _TextureSetSeasonCode == 1 (winter); see snowFreeForClimate below.
float _DisableSnow[11];

// === Per-region treatment ==================================================================
// The WOD mountains are placed by REGION, not biome, so the distant terrain's colour and snow
// behaviour are configured per Daggerfall region. DistantTerrain bakes a per-pixel treatment
// INDEX into the tilemap GREEN channel and pushes this table (from DistantTerrainRegionConfig).
// Each entry packs an RGB colour tint (xyz, multiplies the region's distant terrain - brown vs
// stone vs neutral, replacing the old per-biome brightness) and a snow MODE (w):
//   0 = original season atlas, no caps (the pre-feature look; used for the "rest of map")
//   1 = snow-capped in winter, bald in summer
//   2 = completely snow-covered in winter, snow-capped in summer
float4 _RegionData[8];
// ===========================================================================================

// User-facing toggle (modsettings, default on) for the procedural tree specks + woodland dirt.
// 0 = off (distant ground reverts to the plain tile atlas). Pushed once by DistantTerrain.cs.
int _EnableTreesAndDirt;

// === Iliac Puddle No More water mode ======================================================
// Optional water brightness and opacity adjustments to match IPNM's near-water appearance.
// _IPNMWater: 1 = apply adjustments, 0 = normal water rendering.
// _IPNMWaterBrightness: multiplier on water color (1.0 = normal, >1.0 brightens).
// _IPNMWaterOpacity: alpha modifier for water (1.0 = fully opaque, <1.0 fades to transparent).
// All pushed once by DistantTerrain.cs.
int _IPNMWater;
float _IPNMWaterBrightness;
float _IPNMWaterOpacity;

// === Altitude-based distant snow caps ======================================================
// For the two "capped" modes above, snow is added above a world-Y snow line, drawn with the real
// winter snow tileset, giving a clean horizontal line (no run-down-one-face artefacts). The snow
// is always sampled from the MOUNTAIN winter atlas (white snowy rock) so a cap reads as snow on
// any region's rock colour, while the rock below is tinted per region. _SnowCapStartY/_SnowCapFullY
// (plus a per-pixel _SnowCapNoiseY deviation for variety) are pushed each frame by
// DistantTerrain.PushSnowlineHeights (world-Y, tracking the floating origin). _SnowCapsEnabled == 0
// reproduces the original season-atlas-only behaviour.
int _SnowCapsEnabled;
float _SnowCapStartY;       // world-Y where snow begins
float _SnowCapFullY;        // world-Y at/above which snow is solid
float _SnowCapNoiseY;       // world-Y magnitude of the random per-pixel snow-line deviation

// MOBILE: the four "winter atlas" samplers that used to be declared here are gone - applySnowCaps
// now reads the MOUNTAIN block of _TileArrayWinter in every season, which is the same texture it
// always sampled, so the snow still matches the terrain's own snow tileset rather than a
// procedural white.
// ===========================================================================================

// === Near-terrain cutout + boundary skirt (vertex "pin-down") ============================
// The far terrain is a coarse LOD mesh (one height sample per in-game map pixel); the near
// terrain is the detailed streamed terrain. They meet at the cutout boundary, where far
// fragments inside the near-terrain footprint are discarded. Because the two meshes are
// tessellated at wildly different resolutions their shared edge does not coincide exactly, so
// a hairline strip at the boundary is covered by NEITHER mesh and reads as a see-through seam.
// It is worst at low Terrain Distance (1-2) over uneven ground, where the coarse far edge
// diverges most from the detailed near edge.
//
// Fix (cheap, no extra texture fetch): keep the innermost ring of far tiles that would
// otherwise be culled - the ring at tileDist == _TerrainDistance, which sits UNDER the
// outermost near-terrain tile - and, in the vertex program, push that ring straight down by
// _SkirtDepth. This forms a short vertical curtain ("skirt") hidden beneath the near terrain
// everywhere except in the hairline gap, where it backs the seam with terrain instead of sky.
// Pinning DOWN (rather than merely extending coverage) guarantees the overlap ring can never
// poke up through the detailed near terrain.
//
// These three helpers are the single source of truth for the ring math. The lightweight depth
// pass in DistantTerrainTilemap.shader mirrors them inline (it can't #include this file without
// dragging in the surface-lighting code) and MUST be kept in sync.
int farTileDistToPlayer(float2 uv)
{
	int mapPixelX = uv.x * _FarTerrainTilemapDim;
	int mapPixelY = 499 - uv.y * _FarTerrainTilemapDim;
	int distX = abs(mapPixelX + 1 - _PlayerPosX);
	int distY = abs(mapPixelY + 1 - _PlayerPosY);
	return max(distX, distY);
}

// Pin the boundary ring down to form the skirt. Returns the drop applied (0 when none) so the
// fragment stage can recover the tile's true height for the water/biome test. No-op when the
// cutout is disabled (then the far terrain must render everywhere at its true height) or when
// _SkirtDepth is 0.
float applyFarTerrainSkirt(inout float4 vertex, float2 uv)
{
	if (_DisableCutout != 0)
		return 0.0f;
	if (farTileDistToPlayer(uv) <= _TerrainDistance)
	{
		vertex.y -= _SkirtDepth; // object space == world space (the far-terrain object's scale is 1)
		return _SkirtDepth;
	}
	return 0.0f;
}

// True for far fragments to discard: the near-terrain footprint INTERIOR (everything strictly
// inside the kept skirt ring). The kept ring (tileDist == _TerrainDistance) is the skirt and is
// intentionally NOT culled so the curtain renders.
bool farTerrainCulled(float2 uv)
{
	return (_DisableCutout == 0) && (farTileDistToPlayer(uv) < _TerrainDistance);
}
// =========================================================================================

void fcolor (Input IN, SurfaceOutput o, inout fixed4 color) 
{
    // MOBILE: upstream sampled _CameraDepthTexture into rawZ/sceneZ here and never used either
    // value; nothing sets Camera.depthTextureMode, so on Metal that was a read of an unbound
    // texture every fragment. Deleted with its sampler.
    float partZ = IN.eyeDepth;
    float dist = partZ;
	float blendFacTerrain = 1.0f;
				
	if (_FogMode == 1) 
	{
		blendFacTerrain = max(0.0f, min(1.0f, (_FogEndDistance - dist) / (_FogEndDistance - _FogStartDistance + 1.0)));
	}
	if (_FogMode == 2) 
	{
		float fogFac = 0.0;
		fogFac = _FogDensity * dist;
		blendFacTerrain = exp2(-fogFac);

	}
	if (_FogMode == 3) 
	{
		float fogFac = 0.0;
		fogFac = _FogDensity * dist;
		blendFacTerrain = exp2(-fogFac*fogFac);
	}

    blendFacTerrain = saturate(blendFacTerrain);
	
	const float fadeRange = _BlendEnd - _BlendStart + 1.0f;
	float alphaFadeAmount = max(0.0f, min(1.0f, (_BlendEnd - dist) / fadeRange));

	if (_FogFromSkyTex == 1)
	{
		float2 screenUV = IN.screenPos.xy / IN.screenPos.w;		
        color.rgb = lerp(color, tex2D(_SkyTex, screenUV).rgb, 1.0f - blendFacTerrain);
	}
	else
	{		
        color.rgb = lerp(color, unity_FogColor.rgb, 1.0f - blendFacTerrain);
	}

	if (_WorldEdgeFadeWidthTiles > 0.0f)
	{
		float mapPixelXf = IN.uv_MainTex.x * (float)_FarTerrainTilemapDim;
		float mapPixelYf = 499.0f - IN.uv_MainTex.y * (float)_FarTerrainTilemapDim;
		float edgeDist = min(
			min(mapPixelXf, 999.0f - mapPixelXf),
			min(mapPixelYf, 499.0f - mapPixelYf));
		float edgeFogFactor = saturate(1.0f - edgeDist / _WorldEdgeFadeWidthTiles);
		if (edgeFogFactor > 0.0f)
		{
			float2 edgeScreenUV = IN.screenPos.xy / IN.screenPos.w;
			half3 edgeSky = tex2D(_SkyTex, edgeScreenUV).rgb;
			color.rgb = lerp(color.rgb, edgeSky, edgeFogFactor);
		}
	}

	color.a = alphaFadeAmount * o.Alpha;

	o.Albedo = half3(1.0f, 0.0f, 0.0f);
}


// --- Cheap procedural "hyper texture" detail overlay --------------------------------------
// Goal: break up the obviously repeating tiled atlas textures on the distant terrain WITHOUT
// adding a texture fetch (no extra bandwidth / no new asset) and without reintroducing the
// shimmer/"crawling" that a high-frequency detail map or coarse mip would cause.
//
// Strategy: a couple of octaves of smooth value noise evaluated in MAP-PIXEL space (not world
// space) used only to modulate brightness by a few percent. Map-pixel space is used because it
// is stable under the floating-origin shifts (worldPos jumps around as the origin re-bases,
// which would make a worldPos-keyed pattern visibly slide). The frequencies are deliberately
// low - features span whole map pixels, each of which is large on screen - so the result is
// smooth in screen space and cannot alias. Cost is a handful of frac/mul/lerp ops per fragment.

float dt_hash21(float2 p)
{
	p = frac(p * float2(123.34f, 345.45f));
	p += dot(p, p + 34.345f);
	return frac(p.x * p.y);
}

float dt_valueNoise(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	f = f * f * (3.0f - 2.0f * f); // smoothstep interpolation -> C1 continuity, no faceting
	float a = dt_hash21(i);
	float b = dt_hash21(i + float2(1.0f, 0.0f));
	float c = dt_hash21(i + float2(0.0f, 1.0f));
	float d = dt_hash21(i + float2(1.0f, 1.0f));
	return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Returns a brightness multiplier centred on 1.0 for the given fractional map-pixel position.
half detailOverlayFactor(float2 mapPixelPos)
{
	// Two octaves: a broad mottle (~4 map-pixel features) plus a subtler finer band.
	float n = dt_valueNoise(mapPixelPos * 0.25f) * 0.7f
			+ dt_valueNoise(mapPixelPos * 0.6f) * 0.3f;
	// Map [0,1] noise to a gentle +/-12% brightness swing.
	return (half)lerp(0.88f, 1.12f, n);
}
// ------------------------------------------------------------------------------------------

// --- Procedural "tree" speckle ------------------------------------------------------------
// Scatters small dark dots across the vegetated biomes so the distant LOD ground reads as
// individual trees rather than a flat repeating tile, WITHOUT a texture fetch or new asset. Like
// the detail overlay it is evaluated in MAP-PIXEL space (stable under floating-origin shifts, so
// the dots don't crawl when the origin re-bases). Returns dot COVERAGE in [0,1] (1 = dot centre);
// the caller blends toward a tree COLOUR by this - a hue shift that stays visible on dark biomes
// (gray mountains), where the old pure-darkening wash vanished. `density` (0..1) sets how many
// dots appear. High, non-harmonic frequencies keep the dots small and separated ("individual
// trees, not clumps").
half treeSpeckMask(float2 mapPixelPos, float density)
{
	density = saturate(density);
	if (density <= 0.004f) return (half)0.0f; // no trees here

	float n = dt_valueNoise(mapPixelPos * 13.0f) * 0.6f
			+ dt_valueNoise(mapPixelPos * 23.0f + 7.3f) * 0.4f;

	// Denser forest -> lower threshold -> more dots. The narrow smoothstep band keeps each dot
	// small and crisp; widen it (or lower the thresholds) for bigger/more dots.
	float thresh = lerp(0.86f, 0.52f, density);
	return (half)smoothstep(thresh, thresh + 0.04f, n);
}

// Per-climate "tree density" (0..1) feeding treeSpeckMask. The distant tilemap's G channel now
// carries the baked mountain snow-cap proportion, and never held real per-tile tree coverage
// anyway, so speck density is keyed off the climate - the best available proxy: forests dense,
// open mountains sparse. Tune freely. Ocean (223) and the desert-atlas group (224/225/229) return
// 0 (and are also gated out by the caller), so they never speckle. Mountain climates keep a
// modest density so trees still register on the darker rock (the caller's colour blend makes even
// sparse dots visible there).
half speckDensityForClimate(int index)
{
	if (index==231) return (half)0.80f; // Woodlands
	if (index==232) return (half)0.70f; // HauntedWoodlands
	if (index==233) return (half)0.45f; // Maquis (shrubland)
	if (index==227) return (half)0.85f; // Rainforest (jungle)
	if (index==228) return (half)0.55f; // Swamp
	if (index==226) return (half)0.34f; // Mountain (sparse trees, but still visible on dark rock)
	if (index==230) return (half)0.55f; // MountainWoods
	return (half)0.0f;                  // ocean / desert / subtropical / unknown -> no trees
}
// ------------------------------------------------------------------------------------------

// MOBILE: the texture-array replacement for upstream's getColorByTextureAtlasIndex. The tiling /
// crispness maths and the mip-selection gradient are carried over unchanged; what is gone is the
// atlas cell arithmetic - cell origin from index, gutter offset, atlas-normalised gradients and
// the atlas-bleed clamp's reason for existing - because a slice IS the tile.
//   arraySel  DT_ARRAY_SUMMER / _WINTER / _RAIN  (see dtArrayForSet)
//   biome     DT_BIOME_*                          (slice block)
//   record    DT_TILE_*                           (record within the block)
half4 getColorFromTileArray(Input IN, int arraySel, int biome, int record, float2 uvTex, int texDim, int tilesetDim)
{
	// Lowered from 3.5 to 2.0: the previous value tiled the per-tile texture so finely that
	// the repeating pattern read as "crawling" ground in motion (high-frequency tiles, too
	// small on the mesh). A larger value enlarges each texture instance on the mesh and
	// reduces the visible repetition, per playtest feedback.
	const float textureCrispness = 2.0f; // defines how crisp textures of extended terrain are (higher values result in more crispness)
	const float textureCrispnessDiminishingFactor = 0.075f; // defines how fast crispness of textures diminishes with more distance from the player (the camera)
	const float distanceAttenuation = 0.001; // used to attenuated computed distance

	float dist = max(abs(IN.worldPos.x - _WorldSpaceCameraPos.x), abs(IN.worldPos.z - _WorldSpaceCameraPos.z));
	dist = floor(dist*distanceAttenuation);

	// Frequency multiplier applied to the within-tile texture coordinate.
	float crispnessFreq = textureCrispness / max(1, dist * textureCrispnessDiminishingFactor);

	// Offset to fragment position inside tile - the tile texture repeats over the tile.
	// Upstream wrote this as frac(...) / _GutterSize plus a _GutterSize / _AtlasSize origin shift,
	// which placed the sample inside the 64x64 CORE of the 128-texel gutter-padded atlas cell
	// (the middle half of the cell, in atlas UV). A slice has no gutter, so that whole mapping
	// collapses to the frac() itself, in slice UV.
	float2 uv = frac(uvTex * _FarTerrainTilemapDim * crispnessFreq);

	// Mip selection, unchanged in effect. Upstream handed tex2Dgrad the gradient of
	// uvTex * (texDim / _GutterSize) in ATLAS-normalised units; with a 2048 atlas of 128-texel
	// cells holding 64-texel tiles, the _AtlasSize / _GutterSize factor of 64 is exactly the tile
	// size, so the same mip comes out of the gradient of uvTex * texDim (map-pixel space) measured
	// in TILE texels. Deliberately NOT scaled by crispnessFreq (upstream: keeps the sampler near
	// mip 0 - a little aliasing traded for crisp distant ground, with the repetition tamed by
	// textureCrispness above instead).
	float2 uvr = uvTex * (float)texDim;
	float2 gradX = ddx(uvr);
	float2 gradY = ddy(uvr);

	// Upstream's "atlas-bleed clamp", converted to these units: 0.25 / tilesetDim of an atlas UV is
	// 0.25 * (_AtlasSize / _GutterSize) / tilesetDim = 8 / tilesetDim here. A texture array has no
	// neighbouring cell to bleed in, so all it still does is cap the mip level (5 of 6 for a 64^2
	// slice) - kept so the distant ground keeps exactly the sharpness it had upstream.
	float maxGrad = 8.0f / (float)tilesetDim;
	float gx = min(length(gradX), maxGrad);
	float gy = min(length(gradY), maxGrad);

	// There is no UNITY_SAMPLE_TEX2DARRAY_GRAD in HLSLSupport.cginc, and DFU's own
	// DaggerfallTilemapTextureArray shader selects the level explicitly for the same reason (array
	// GRAD sampling has a long-standing seam bug - Nystul's TransitionRingTilemapTextureArray says
	// so in as many words). So turn the clamped gradient into a LOD: lod = log2(texels per pixel).
	float sliceDim = max(_TileArraySummer_TexelSize.z, _TileArraySummer_TexelSize.w);
	float lod = log2(max(max(gx, gy) * sliceDim, 1e-6f));

	// MOBILE: and the ceiling. The gradient clamp above caps this at mip 5 of a 64^2 slice's six
	// levels, which is right for the arrays the pack normally builds - but an array with a shorter
	// chain (a replacement pack that ships no mips) would be sampled past its last level, which is
	// undefined rather than clamped in HLSL. _TileArrayMipCount is what the arrays actually have.
	lod = clamp(lod, 0.0f, (float)max(_TileArrayMipCount - 1, 0));

	float3 uv3 = float3(uv, (float)(biome * _SlicesPerBiome + record));

	// A texture array cannot be selected through a variable, so this is a three-way branch on a
	// uniform-driven int rather than the twelve-sampler macro maze it replaces. Written as one
	// initialised local with a single return: early returns out of a branch make the HLSL compiler
	// emit "use of potentially uninitialized variable" for the inlined return temp.
	half4 c = half4(0.0f, 0.0f, 0.0f, 0.0f);
	if (arraySel == DT_ARRAY_WINTER)
		c = UNITY_SAMPLE_TEX2DARRAY_LOD(_TileArrayWinter, uv3, lod);
	else if (arraySel == DT_ARRAY_RAIN)
		c = UNITY_SAMPLE_TEX2DARRAY_LOD(_TileArrayRain, uv3, lod);
	else
		c = UNITY_SAMPLE_TEX2DARRAY_LOD(_TileArraySummer, uv3, lod);
	return c;
}

// True when this climate should keep its summer look despite winter (its per-climate DisableSnow
// flag is set). Only land climates 224..233 are eligible; ocean/unknown and any non-winter season
// return false, so the default (all flags 0) reproduces the original "everything snows in winter".
bool snowFreeForClimate(int index)
{
	if (_TextureSetSeasonCode != 1) return false; // snow disable only matters in winter
	int i = index - 223;                          // 1..10 == climates 224..233
	if (i < 1 || i > 10) return false;
	return _DisableSnow[i] > 0.5f;
}

// Per-pixel region treatment index, baked into the tilemap GREEN channel by DistantTerrain.cs and
// indexing _RegionData[] (rgb = colour tint, w = snow mode). The tilemap is point-sampled, so the
// stored byte round-trips exactly; clamp to the array bounds for safety.
int regionTreatmentIndex(float2 uvTex)
{
	int t = (int)(tex2D(_FarTerrainTilemapTex, uvTex).g * 255.0f + 0.5f);
	return clamp(t, 0, 7);
}

// Atlas-set selector, unchanged in meaning from upstream (it named three SAMPLER sets; here it
// names three ARRAYS).
#define ATLAS_SEASONAL 0   // current season's set, auto-swapping to summer for snow-free climates
#define ATLAS_SUMMER   1   // always the summer ("bald") set
#define ATLAS_SNOW     2   // always the winter ("snow") set

// MOBILE: replaces upstream's SAMPLE_BIOME_ATLAS / SAMPLE_BIOME_3 macros, which existed only
// because a sampler2D cannot be stored in a variable and so had to be pasted into every branch.
// With one array per season the choice is a plain int, so the twelve-way macro expansion becomes
// this: which of the three seasonal arrays does this request resolve to?
//   ATLAS_SNOW     -> winter  (upstream's permanently-bound _TileAtlasTex*Snow set)
//   ATLAS_SUMMER   -> summer  (upstream's permanently-bound _TileAtlasTex*SnowFree set)
//   ATLAS_SEASONAL -> the current season, or summer when this climate has opted out of winter snow
int dtArrayForSet(int atlasSet, bool snowFree)
{
	int arr = clamp(_TextureSetSeasonCode, DT_ARRAY_SUMMER, DT_ARRAY_RAIN);
	if (atlasSet == ATLAS_SNOW)      arr = DT_ARRAY_WINTER;
	else if (atlasSet == ATLAS_SUMMER) arr = DT_ARRAY_SUMMER;
	else if (snowFree)                 arr = DT_ARRAY_SUMMER;
	return arr;
}

// Samples a LAND biome's three-way (grass/dirt/stone) slope blend from the chosen atlas set
// (ATLAS_SEASONAL / ATLAS_SUMMER / ATLAS_SNOW). Pulling the per-biome switch into a function lets
// the snow-cap code request the summer ("bald") and winter ("snow") copies of the SAME fragment
// without duplicating the switch. Only non-zero slope channels are fetched, as before. Water
// (index 223 / below sea level) is handled by the caller.
half4 sampleLandBiome(Input IN, float2 uvTex, int texDim, int tilesetDim, int index,
                      float weightGrass, float weightDirt, float weightStone, int atlasSet)
{
	bool snowFree = snowFreeForClimate(index);   // only consulted for ATLAS_SEASONAL
	int arr = dtArrayForSet(atlasSet, snowFree);

	// MOBILE: upstream had four near-identical blocks here, one per biome, each pasting a different
	// pair/triple of samplers into the SAMPLE_BIOME_3 macro. The biome is now just a slice-block
	// index, so the four blocks collapse to picking that index.
	int biome = -1;
	if ((index==224)||(index==225)||(index==229))       biome = DT_BIOME_DESERT;   // desert
	else if ((index==227)||(index==228))                biome = DT_BIOME_SWAMP;    // swamp
	else if ((index==226)||(index==230))                biome = DT_BIOME_MOUNTAIN; // mountain
	else if ((index==231)||(index==232)||(index==233))  biome = DT_BIOME_WOODLAND; // woodland

	half4 c = half4(0.0f, 0.0f, 0.0f, 1.0f);   // ocean / unknown - handled by the caller
	if (biome >= 0)
	{
		half4 c_g = half4(0,0,0,0);
		half4 c_d = half4(0,0,0,0);
		half4 c_s = half4(0,0,0,0);

		// Only non-zero slope channels are fetched, as before.
		if (weightGrass > 0.0f) c_g = getColorFromTileArray(IN, arr, biome, DT_TILE_GRASS, uvTex, texDim, tilesetDim);
		if (weightDirt  > 0.0f) c_d = getColorFromTileArray(IN, arr, biome, DT_TILE_DIRT,  uvTex, texDim, tilesetDim);
		if (weightStone > 0.0f) c_s = getColorFromTileArray(IN, arr, biome, DT_TILE_STONE, uvTex, texDim, tilesetDim);

		c = c_g * weightGrass + c_d * weightDirt + c_s * weightStone;
	}

	return c;
}

// Altitude-based snow cap, driven by the per-region snow MODE (0 none, 1 cap-winter/bare-summer,
// 2 full-winter/cap-summer). Snows terrain above the world-Y snow line (with a per-pixel deviation
// for variety), giving a clean horizontal line. The snow itself is always the MOUNTAIN winter
// tileset (white) and UNtinted, so caps read as snow on any region's rock; the rock below carries
// the region tint (already applied to seasonalRGB by the caller; re-applied to the summer swap).
half3 applySnowCaps(Input IN, float2 uvTex, int texDim, int tilesetDim, int index,
                    float weightGrass, float weightDirt, float weightStone,
                    float trueWorldY, int snowMode, half3 regionTint, half3 seasonalRGB)
{
	// Mode 0 (default / "rest of map") = original season atlas, no caps. Desert/ocean regions also
	// use mode 0 unless explicitly configured, so they fall through here untouched.
	if (snowMode == 0)
		return seasonalRGB;

	bool fullSnowBiome = (snowMode == 2);
	// A snow-free climate (winter opt-out) behaves like a non-winter season here.
	bool effWinter = (_TextureSetSeasonCode == 1) && !snowFreeForClimate(index);

	// The two "original-look" quadrants are exactly the seasonal atlas, so nothing to add:
	//   mode 1 + summer -> bald;  mode 2 + winter -> fully snow-covered (seasonal IS the snowy set)
	if (!effWinter && !fullSnowBiome) return seasonalRGB;
	if ( effWinter &&  fullSnowBiome) return seasonalRGB;

	// Remaining: mode 2 + summer, or mode 1 + winter -> altitude snow cap. A low-frequency noise
	// deviation nudges the line up/down per area so neighbouring peaks don't all snow at one height.
	float2 mapPixelPos = uvTex * (float)texDim;
	float dev = (dt_valueNoise(mapPixelPos * 0.7f) - 0.5f) * 2.0f * _SnowCapNoiseY; // +/- _SnowCapNoiseY
	float cap = smoothstep(_SnowCapStartY + dev, _SnowCapFullY + dev, trueWorldY);  // 0 below -> 1 above

	// Snow colour: the FLAT-snow tile (slot 8) of the mountain winter atlas, NOT the slope-weighted
	// blend - the dirt/stone slots of the winter atlas are snowy ROCK (grey/brown) and made steep
	// caps read brown. Always untinted, and whitened a touch so caps stay convincingly white on any
	// region's rock (raise the 0.4 toward 1.0 for purer white, lower it to keep more snow texture).
	half3 snowRGB = getColorFromTileArray(IN, DT_ARRAY_WINTER, DT_BIOME_MOUNTAIN, DT_TILE_GRASS, uvTex, texDim, tilesetDim).rgb;
	snowRGB = lerp(snowRGB, half3(1.0f, 1.0f, 1.0f), 0.4f);

	// Base (rock) below the line. In WINTER the seasonal atlas is the snowy GROUND, which would make
	// the capped mountain read as fully white; so on the sloped (stone/dirt) part swap in the
	// region-tinted SUMMER ("stone"/"brown") look, leaving the flat winter ground white. In SUMMER
	// the seasonal atlas already IS that look (and is region-tinted).
	half3 baseRGB = seasonalRGB;
	if (effWinter)
	{
		half3 summerRGB = sampleLandBiome(IN, uvTex, texDim, tilesetDim, index,
		                                  weightGrass, weightDirt, weightStone, ATLAS_SUMMER).rgb * regionTint;
		baseRGB = lerp(seasonalRGB, summerRGB, saturate(weightStone + weightDirt)); // tinted slopes, white flat
	}

	return lerp(baseRGB, snowRGB, cap);
}

// Lighter-brown dirt patches for the woodland biomes (231/232/233): blends the biome's own DIRT
// atlas slot in under a broad, low-frequency noise mask, so the distant ground shows the bare-earth
// blotches you see in the near terrain instead of a flat green sheet. No-op outside woodland and in
// winter (the ground is snow then). Map-pixel space, so the patches are stable under floating origin.
half3 applyWoodlandDirt(Input IN, float2 uvTex, int texDim, int tilesetDim, int index, half3 c)
{
	if (!((index==231)||(index==232)||(index==233))) return c;
	if (_TextureSetSeasonCode == 1) return c; // winter ground is snow, not dirt

	float2 mapPixelPos = uvTex * (float)texDim;
	// Small, irregular blotches (two non-harmonic octaves at a higher frequency than before, so the
	// dirt reads as scattered patches rather than big swatches). Raise the frequencies further to
	// shrink them more, or the threshold to make them sparser.
	float n = dt_valueNoise(mapPixelPos * 1.7f) * 0.6f
			+ dt_valueNoise(mapPixelPos * 3.7f + 3.1f) * 0.4f;
	float dirtMask = smoothstep(0.62f, 0.82f, n) * 0.55f; // up to 55% dirt at patch centres

	// The woodland DIRT record of the SEASONAL array - the actual bare-earth colour for this biome.
	// Upstream named the raw seasonal sampler here (no snow-free swap), and this is winter-gated
	// above anyway, so the season code selects the array directly.
	half3 dirt = getColorFromTileArray(IN, clamp(_TextureSetSeasonCode, DT_ARRAY_SUMMER, DT_ARRAY_RAIN),
	                                   DT_BIOME_WOODLAND, DT_TILE_DIRT, uvTex, texDim, tilesetDim).rgb;
	return lerp(c, dirt, dirtMask);
}

half4 getColorFromTerrain(Input IN, float2 uvTex, int texDim, int tilesetDim, int index)
{
	const float cosLimitDirt  = 0.86162916f; 
	const float cosLimitStone = 0.70090926f; 

	half4 c;

	bool snowFree = snowFreeForClimate(index); 

	float weightGrass = 1.0f;
	float weightDirt = 0.0f;
	float weightStone = 0.0f;

	float3 surfaceWorldPos = float3(IN.worldPos.x, IN.worldPos.y + IN.skirtDrop, IN.worldPos.z);
	float3 surfaceNormal = normalize(cross(ddx(surfaceWorldPos), -ddy(surfaceWorldPos))); 

	const float3 upVec = float3(0.0f, 1.0f, 0.0f);
	float dotResult = dot(surfaceNormal, upVec); 

	if (dotResult > cosLimitDirt) 
	{
		weightGrass = saturate((dotResult - cosLimitDirt) / (1.0f - cosLimitDirt));
		weightDirt = 1.0f - weightGrass;
		weightStone = 0.0f;
	}
	else if (dotResult > cosLimitStone) 
	{
		weightGrass = 0.0f;
		weightDirt = saturate((dotResult - cosLimitStone) / (cosLimitDirt - cosLimitStone));
		weightStone = 1.0f - weightDirt;
	}
	else 
	{
		weightGrass = 0.0f;
		weightDirt = 0.0f;
		weightStone = 1.0f;
	}

	float trueWorldY = IN.worldPos.y + IN.skirtDrop;

	int regionT = regionTreatmentIndex(uvTex);
	half3 regionTint = (half3)_RegionData[regionT].rgb;
	int snowMode = (int)(_RegionData[regionT].w + 0.5f);

	if ((index==223) || (trueWorldY < _WaterHeightTransformed)) 
	{
		// Water is the woodland tileset's record 0 (upstream: atlas cell 0 of the woodland set),
		// on the seasonal array with the per-climate winter-snow opt-out applied.
		c = getColorFromTileArray(IN, dtArrayForSet(ATLAS_SEASONAL, snowFree),
		                          DT_BIOME_WOODLAND, DT_TILE_WATER, uvTex, texDim, tilesetDim);
		#if defined(ENABLE_WATER_REFLECTIONS)
			if (_UseSeaReflectionTex)
			{
				float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
				c.rgb = 0.5f * c.rgb + 0.5f * tex2D(_SeaReflectionTex, screenUV).rgb;
			}
		#endif
		c.rgb *= regionTint; 

		// Apply IPNM water mode brightness and opacity adjustment
		if (_IPNMWater != 0)
		{
			c.rgb *= _IPNMWaterBrightness;  
			c.a = _IPNMWaterOpacity;        
		}
		else
		{
			c.a = 1.0f;
		}
	}
	else
	{
		c = sampleLandBiome(IN, uvTex, texDim, tilesetDim, index, weightGrass, weightDirt, weightStone, ATLAS_SEASONAL);
		c.a = 1.0f; // ENSURE LAND IS OPAQUE

		if (_EnableTreesAndDirt != 0)
			c.rgb = applyWoodlandDirt(IN, uvTex, texDim, tilesetDim, index, c.rgb);

		c.rgb *= regionTint;

		if (_SnowCapsEnabled != 0)
			c.rgb = applySnowCaps(IN, uvTex, texDim, tilesetDim, index,
			                      weightGrass, weightDirt, weightStone, trueWorldY, snowMode, regionTint, c.rgb);
	}

	float2 mapPixelPos = uvTex * (float)texDim;
	c.rgb *= detailOverlayFactor(mapPixelPos);

	return c;
}

half3 updateColorWithInfoForTreeCoverageAndLocations(half3 colorIn, int treeCoverage, int locationRangeX, int locationRangeY, int mapPixelX, int mapPixelY, float2 uvTex, int index, bool isWater)
{
	half3 c = colorIn;
	half3 treeColor;
	float treeCoverage2 = (float)treeCoverage / 255.0f;

	// Per-climate snow opt-out: a snow-free climate in winter uses the summer (green) tree tint
	// instead of winter white, so its trees match the summer atlas it now samples. Non-winter or
	// snowing climates fall through to the real season code, preserving the original behaviour.
	int effSeasonCode = snowFreeForClimate(index) ? 0 : _TextureSetSeasonCode;

	if (effSeasonCode == 0)
		treeColor = half3(0.125f, 0.165f, 0.061f);
	else if (effSeasonCode == 1)
		treeColor = half3(0.96f, 0.98f, 0.94f);
	else if (effSeasonCode == 2)
		treeColor = half3(0.10f, 0.14f, 0.04f);

	// Apply tree-coverage tint to every pixel using the SAME blend the original code used
	// for the "non-location" branch (0.3/0.7, then *0.9). The original code had a slightly
	// brighter blend (0.4/0.6, then *1.0) for location pixels using a per-tile rectangle
	// test driven by RMB-block dimensions. We've replaced that rectangle test with a
	// type-coded beacon overlay below, which is far more visible at LOD distance - so the
	// base tile color uses the original "non-location" blend uniformly.
	c.rgb = min(1.0f, 0.3f * c.rgb + 0.7f * ((1.0f - treeCoverage2) * c.rgb + treeCoverage2 * treeColor));
	c.rgb = 0.9f * c.rgb;

	// Procedural tree specks: small dots blended toward a tree COLOUR (not just a darken, so they
	// stay visible on dark biomes like mountains, where the old darkening wash vanished). Excludes
	// ocean (223) and the desert-atlas group (224/225/229), which carry no forest. Density comes
	// from the climate (see speckDensityForClimate). Map-pixel space, so dots stay put under the
	// floating origin.
	// Gated by the modsettings toggle. isWater skips rivers/lakes/coast (their land climate index
	// would otherwise speckle the water).
	if (_EnableTreesAndDirt != 0 && !isWater && index != 223 && index != 224 && index != 225 && index != 229)
	{
		float2 speckMapPixelPos = uvTex * (float)_FarTerrainTilemapDim;
		float speck = treeSpeckMask(speckMapPixelPos, speckDensityForClimate(index));
		// Dark evergreen in summer/fall; a darker frosted grey-green in winter so dots read on snow.
		half3 speckColor = (effSeasonCode == 1) ? half3(0.26f, 0.30f, 0.29f)
		                                        : half3(0.045f, 0.075f, 0.025f);
		c.rgb = lerp(c.rgb, speckColor, speck * 0.78f);
	}

	// Beacon overlay. The C# tilemap-builder writes a non-zero locationRangeX (the marker
	// value, currently a constant 4) into the B channel when a tile has any location, and
	// the LocationType bucket (1..6) into the A channel as locationRangeY. Both reads pass
	// through the same "* _MaxIndex" unscaling that R uses for the climate index, so the
	// integer values seen here match the bytes written by C# exactly.
	//
	// Gated by _HighlightLocations so a user toggling the feature off (in mod settings on the
	// next world load, OR live with the in-game hotkey) sees the markers disappear without a
	// per-pixel rebuild.
	if (_HighlightLocations != 0 && locationRangeX > 0)
	{
		// Category -> color palette. Defaults to the "city gold" marker when the category
		// byte is 1 OR unrecognized; explicit if-branches override for the other buckets.
		// Keep this in sync with MapLocationTypeToCategory in DistantTerrain.cs.
		half3 beacon = half3(1.0f, 0.85f, 0.35f);                       // 1 - TownCity (warm gold)
		if (locationRangeY == 2) beacon = half3(1.0f, 0.70f, 0.30f);    // 2 - Hamlet/Village (amber)
		if (locationRangeY == 3) beacon = half3(0.55f, 0.95f, 0.45f);   // 3 - Farms/Tavern (green)
		if (locationRangeY == 4) beacon = half3(0.55f, 0.85f, 1.00f);   // 4 - Temple/Cult/Coven (pale blue)
		if (locationRangeY == 5) beacon = half3(0.80f, 0.55f, 1.00f);   // 5 - Graveyard (violet)
		if (locationRangeY == 6) beacon = half3(1.00f, 0.35f, 0.30f);   // 6 - Dungeon (red)

		// Soft glowing dot instead of the old flat, full-tile, ~1 Hz pulsing square. Position
		// within this location's map-pixel tile (0..1, centre at 0.5): uvTex*_FarTerrainTilemapDim
		// is the same map-pixel space mapPixelX/Y use (and the tree specks above), so frac() is the
		// sub-tile coordinate. The marker bytes are constant across the tile (point-sampled), so
		// only this geometry varies here, letting us shape a round, feathered marker.
		float2 tileUV = frac(uvTex * (float)_FarTerrainTilemapDim);
		float rad = length(tileUV - 0.5f) * 2.0f;       // 0 at centre, ~1 at the tile-edge midpoints

		// Wide bright plateau feathered to nothing by the tile edge (smoothstep = cheap
		// anti-aliased falloff), so it reads as a glowing point at LOD distance rather than a
		// hard flickering pixel - and neighbouring non-location tiles stay untouched.
		float halo = 1.0f - smoothstep(0.30f, 1.05f, rad);

		// Luminous core: lift the very centre toward white so the marker looks lit from within
		// rather than like a flat decal, while the rim keeps the category colour.
		float core = 1.0f - smoothstep(0.0f, 0.45f, rad);
		half3 glowColor = lerp(beacon, half3(1.0f, 1.0f, 1.0f), core * 0.5f);

		// Gentle, slow "breathing" (~0.25 Hz, shallow) keeps a hint of life so the eye still
		// catches the marker, without the old alarm-bell full-range blink. _Time.y is continuous
		// across frames, so it stays smooth under frame-time jitter.
		float breathe = 0.92f + 0.08f * sin(_Time.y * 1.6f);

		// Composite the glow over the underlying biome tile by the soft mask, so a desert city
		// still reads subtly different from a forest one at the feathered edge. saturate() guards
		// against overshoot when downstream post-processing (bloom etc.) is stacked.
		float a = saturate(halo * breathe);
		c.rgb = saturate(lerp(c.rgb, glowColor, a));
	}

	return c;
}

#endif // FAR_TERRAIN_COMMON_CG_INCLUDED