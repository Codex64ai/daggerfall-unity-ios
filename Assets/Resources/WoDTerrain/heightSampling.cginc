// MOBILE PORT - github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40, Assets/Shaders/heightSampling.cginc; compiled into the app (Resources.Load), no licence header upstream, private draft only.
#define TERRAIN_X 999.0
#define TERRAIN_Y 499.0
#define COASTLINE_VAR 40.0
#define COASTLINE_THRES 0.03
#define COASTLINE_MAX 0.005
#define BASEHEIGHT_MIN 100.0
#define BASEHEIGHT_MAX 800.0
#define BASEHEIGHT_HILL 1000.0
#define BASEHEIGHT_MNT 1200.0

// Basemap sampling params
StructuredBuffer<float> shm;
StructuredBuffer<float> lhm;
int sd;
int ld;
int hDim;
float div;
int mapPixelX;
int mapPixelY;
float maxTerrainHeight;
float baseHeightScale;
float noiseMapScale;
float extraNoiseScale;
float scaledOceanElevation;

struct BiomeWeights
{
    float mountain;
    float mountainBase;
    float desert;
    float hills;
    float land;
    float2 deriv;
};

float CubicInterpolation(float v0, float v1, float v2, float v3, float fracy) {
    float A = (v3 - v2) - (v0 - v1);
    float B = (v0 - v1) - A;
    float C = v2 - v0;
    float D = v1;

    return A * (fracy * fracy * fracy) + B * (fracy * fracy) + C * fracy + D;
}

float GetBasemapNoise(int x, int y, float frequency, float amplitude, float persistence, int octaves) {
    PerlinParams p;
    p.pos = float2(x, y);
    p.octaves = octaves;
    p.amplitude = amplitude;
    p.frequency = frequency * 0.1;
    p.persistence = persistence;
    p.lacunarity = 2;
    p.maxHeight = newHeight;
    p.offset = 0;

    return saturate(SimplePerlin(p) * 0.5 + 0.5);
}

int Idx(int r, int c, int dim) {
    return r + c * dim;
}

int Row(int index, int dim) {
    return index % dim;
}
int Col(int index, int dim) {
    return index / dim;
}

float SampleBaseHeight(int index) {
    float baseHeight, noiseHeight, x1, x2, x3, x4;

    int x = Row(index, hDim);
    int y = Col(index, hDim);

    float rx = float(x) / div;
    float ry = float(y) / div;
    int ix = int(rx);
    int iy = int(ry);
    float sfracx = float(x) / float(hDim - 1);
    float sfracy = float(y) / float(hDim - 1);
    float fracx = float(x - ix * div) / div;
    float fracy = float(y - iy * div) / div;
    float scaledHeight = 0;

    // Bicubic sample small height map for base terrain elevation
    x1 = CubicInterpolation(shm[Idx(0, 3, sd)], shm[Idx(1, 3, sd)], shm[Idx(2, 3, sd)], shm[Idx(3, 3, sd)], sfracx);
    x2 = CubicInterpolation(shm[Idx(0, 2, sd)], shm[Idx(1, 2, sd)], shm[Idx(2, 2, sd)], shm[Idx(3, 2, sd)], sfracx);
    x3 = CubicInterpolation(shm[Idx(0, 1, sd)], shm[Idx(1, 1, sd)], shm[Idx(2, 1, sd)], shm[Idx(3, 1, sd)], sfracx);
    x4 = CubicInterpolation(shm[Idx(0, 0, sd)], shm[Idx(1, 0, sd)], shm[Idx(2, 0, sd)], shm[Idx(3, 0, sd)], sfracx);
    baseHeight = CubicInterpolation(x1, x2, x3, x4, sfracy);
    scaledHeight += baseHeight * baseHeightScale;

    // Bicubic sample large height map for noise mask over terrain features
    x1 = CubicInterpolation(lhm[Idx(ix, iy + 0, ld)], lhm[Idx(ix + 1, iy + 0, ld)], lhm[Idx(ix + 2, iy + 0, ld)], lhm[Idx(ix + 3, iy + 0, ld)], fracx);
    x2 = CubicInterpolation(lhm[Idx(ix, iy + 1, ld)], lhm[Idx(ix + 1, iy + 1, ld)], lhm[Idx(ix + 2, iy + 1, ld)], lhm[Idx(ix + 3, iy + 1, ld)], fracx);
    x3 = CubicInterpolation(lhm[Idx(ix, iy + 2, ld)], lhm[Idx(ix + 1, iy + 2, ld)], lhm[Idx(ix + 2, iy + 2, ld)], lhm[Idx(ix + 3, iy + 2, ld)], fracx);
    x4 = CubicInterpolation(lhm[Idx(ix, iy + 3, ld)], lhm[Idx(ix + 1, iy + 3, ld)], lhm[Idx(ix + 2, iy + 3, ld)], lhm[Idx(ix + 3, iy + 3, ld)], fracx);
    noiseHeight = CubicInterpolation(x1, x2, x3, x4, fracy);
    scaledHeight += noiseHeight * noiseMapScale;

    // Additional noise mask for small terrain features at ground level
    int noisex = mapPixelX * (hDim - 1) + x;
    int noisey = (499 - mapPixelY) * (hDim - 1) + y;
    float lowFreq = GetBasemapNoise(noisex, noisey, 0.3f, 0.5f, 0.5f, 1);
    float highFreq = GetBasemapNoise(noisex, noisey, 0.9f, 0.5f, 0.5f, 1);
    scaledHeight += (lowFreq * highFreq) * extraNoiseScale;

    float maxScale = (baseHeightScale + noiseMapScale + extraNoiseScale) - scaledOceanElevation;

    // Set sample
    float height = saturate((scaledHeight - scaledOceanElevation) / maxTerrainHeight);
    return height;
}

BiomeWeights GetBiomeWeights(float2 worldUv, int2 id = 0, bool detailedHeights = true, bool smallerBase = false) {
    //float heightmapMaxIndex = terrainSize - 1;
    //float2 tileUv = id.xy / heightmapMaxIndex;
    float2 pos = worldUv * (terrainSize * float2(TERRAIN_X, TERRAIN_Y));

    //worldUv.x -= 3.0 / TERRAIN_X;
    //worldUv.y -= 1.0 / TERRAIN_Y;

    float sampleLevel = 0;
    SamplerState ss = bm_linear_clamp_sampler;

    if (!detailedHeights) {
        //worldUv += float(0.5).xx / float2(TERRAIN_X, TERRAIN_Y);
        pos = float2(id) * 129.0;
        //ss = bm_point_clamp_sampler; 
    }

    float4 tex = BiomeMap.SampleLevel(ss, worldUv, sampleLevel);

    id.x = min(id.x, terrainSize - 1);

    int index = Idx(id.x, id.y, terrainSize);
    float baseHeight = SampleBaseHeight(index);

    float3 dTex = DerivMap.SampleLevel(ss, worldUv, sampleLevel).rgb;
    tex.r = smoothstep(0, 1, tex.r);

    float loResBaseHeight = (dTex.b * 255.0) * (baseHeightScale + noiseMapScale);
    loResBaseHeight = saturate((loResBaseHeight - scaledOceanElevation) / maxTerrainHeight);

    float loHiFade = 100.0 / maxTerrainHeight;
    float loHiThres = 1.5 / maxTerrainHeight;
    baseHeight = lerp(baseHeight, loResBaseHeight, saturate((baseHeight - loHiThres) / loHiFade));

    //PerlinParams hillBaseParams = HillBase(pos);
    //float hillBase = SimplePerlin(hillBaseParams);
    //hillBase = saturate(hillBase * 0.5 + 0.5);
    //hillBase = pow(hillBase, 3);

    //SwissParams mountainBaseParams = MountainBase(pos);
    //float mountainBase = MountainBaseNoise(mountainBaseParams);

    BiomeWeights w;

    w.mountain = tex.r;// saturate(max(tex.r, mountainBase * 0.25));
    w.mountainBase = tex.r;
    w.desert = tex.g;
    w.hills = tex.b;// max(tex.b, hillBase * 0.5);
    w.land = baseHeight;

    if (!detailedHeights) {
        w.land = loResBaseHeight;
    }

    w.deriv = normalize(float3(dTex.r, dTex.g, 1)).rg;
    w.deriv = w.deriv * 2 - 1;

    return w;
}

float GetBaseHeight(float2 id, out float extraHeight, out float bumps, bool detailedHeights = true, bool detailedBasemap = true, bool idIsPos = false) {
    // Init biome weights and variation masks
    float heightmapMaxIndex = terrainSize - 1;
    float2 tileUv = id.xy / heightmapMaxIndex;
    float2 pos = terrainPosition + tileUv * (terrainSize);

    if (idIsPos) {
        pos = id;
    }

    float2 worldUv = (pos) / (terrainSize * float2(TERRAIN_X, TERRAIN_Y));

    if (!detailedHeights) {
        pos = id * 129.0;
        worldUv = float2(id) / float2(TERRAIN_X, TERRAIN_Y);
    }

    BiomeWeights w = GetBiomeWeights(worldUv, int2(id), detailedHeights && detailedBasemap, !detailedBasemap);

    PerlinParams colorVarParams = ColorVar(pos);
    float4 colorVar = ColorPerlin(colorVarParams);

    SwissParams landVarParams = SwissCell(pos);
    landVarParams.amplitude = 1;
    float landVar = abs(SwissCellNoise(landVarParams)) * (newHeight / swissCell_maxHeight);

    float coastlineThres = lerp(3.4 / 255.0, 0.00001, landVar);
    float coastlineMax = lerp(5.0 / 255.0, 0.0001, landVar);
    coastlineMax = lerp(15.0 / 255.0, 45.0 / 255.0, landVar);

    float beachWeight = smoothstep(0, 1, saturate(w.land / coastlineMax));

    // Init landscape generator weights
    float iqMntWeight = saturate(1.0 - (w.mountain / 0.25));
    float swissMntWeight = saturate(w.mountain * (1.0 / 0.4) - 0.6);
    float jordanMntWeight = saturate((1.0 - iqMntWeight) * (1.0 - swissMntWeight));

    w.mountain = saturate(w.mountain / 0.25);
    w.mountain = smoothstep(0.4, 0.6, w.mountain);
    w.hills *= (1.0 - w.mountain) * (1.0 - w.desert);
    float hillNoiseFactor = saturate(w.hills / 0.1);

    float plateauWeight = max(colorVar.a, w.desert) * (1.0 - w.mountainBase);
    float duneWeight = w.desert * (1.0 - w.mountain);

    float bhm = BASEHEIGHT_MAX;

    float baseHeightMax = lerp(bhm, BASEHEIGHT_HILL, w.hills);
    baseHeightMax = lerp(baseHeightMax, BASEHEIGHT_MNT, w.mountain);

    float baseHeightMin = BASEHEIGHT_MIN - 1;

    float baseHeight = lerp(baseHeightMin / newHeight, baseHeightMax / newHeight, w.land);

    float coastlineVar = lerp(COASTLINE_VAR, COASTLINE_VAR * 2.0, landVar);

    float beachMin = saturate(w.land - lerp(5.0, 15.0, landVar) / newHeight);

    float landWeight = saturate(saturate(beachMin) / ((coastlineVar * 2) / newHeight));

    landWeight = saturate(beachMin / (lerp(coastlineVar * 10.0, coastlineVar * 20.0, landVar) / newHeight));

    float landFade = saturate(beachMin / ((coastlineVar * 8) / newHeight));

    PerlinParams bumpParams = PerlinDune(pos);
    PerlinParams bumpsParams = bumpParams;
    float bump = PositivePerlin(bumpParams) * landWeight * (1.0 - w.mountain);

    bumpsParams.octaves = 8;
    bumpsParams.frequency *= 64.0;
    bumpsParams.maxHeight = 6.5;
    bumps = SimplePerlin(bumpsParams) * landWeight;

    SwissParams rockyBaseParams = SwissCell(pos);
    float rockyBase = SwissCellNoise(rockyBaseParams) * landWeight * w.mountain * 0.25;

    SwissParams duneParams = SwissDune(pos);
    float dunes = SwissMountainsGen(duneParams);

    SwissParams swissParams = SwissFolded(pos);
    float3 swissMntD = 0;
    float sm = saturate(SwissMountains(swissParams));
    float swissMnt = sm * landWeight;

    JordanParams jordanParams = JordanFolded(pos);

    // Alternate between fold and fault for Jordan
    float defaultWarp = jordanParams.warp;
    float defaultDampScale = jordanParams.damp_scale;
    jordanParams.warp = lerp(defaultWarp, 200, plateauWeight);
    jordanParams.damp_scale = lerp(defaultDampScale, 1, plateauWeight);

    // Alternate between mountain and hill for Jordan
    jordanParams.warp = lerp(jordanParams.warp, -defaultWarp, hillNoiseFactor);
    jordanParams.persistence = lerp(jordanParams.persistence, 0.2, hillNoiseFactor);
    jordanParams.damp = lerp(jordanParams.damp, 1, hillNoiseFactor);
    jordanParams.damp_scale = lerp(jordanParams.damp_scale, 1, hillNoiseFactor);
    jordanParams.maxHeight = lerp(jordanParams.maxHeight, 800, hillNoiseFactor);

    float jordanMnt = JordanMountains(jordanParams) * landWeight;
    float jmw = 1.0 - saturate(sm * (newHeight / swissFolded_maxHeight));

    SwissParams iqParams = IQMountain(pos);
    float iqMnt = IQMountains(iqParams) * landWeight * w.mountain;
    // Fade-replace iq with jordan for fault type
    iqMnt = lerp(iqMnt, jordanMnt, plateauWeight);

    float mnt = swissMnt * swissMntWeight + jordanMnt * jordanMntWeight + iqMnt * iqMntWeight;
    float hill = jordanMnt * w.hills + dunes * w.desert;

    baseHeight += bump + rockyBase;

    float eh = saturate(mnt + hill) - landVar * (10.0 / newHeight);

    // Faults
    SwissParams faultParams = SwissFaults(pos);
    float subtraction = 1.0 - SwissTime(faultParams);
    subtraction *= saturate((colorVar.g * colorVar.b) * 2 - 1) * w.mountain;

    extraHeight = saturate(mnt + hill) - landVar * (10.0 / newHeight);
    extraHeight -= subtraction * min((extraHeight + (baseHeight - 115.0 / newHeight)), 500.0 / newHeight);

    return baseHeight;
}

