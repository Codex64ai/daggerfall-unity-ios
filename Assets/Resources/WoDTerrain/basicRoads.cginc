// MOBILE PORT - github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40, Assets/Shaders/basicRoads.cginc; compiled into the app (Resources.Load), no licence header upstream, private draft only.
float4 NW_NE_SW_SE[9], N_E_S_W[9];
// MOBILE: deleted "#pragma exclude_renderers d3d11 gles" (with its auto-upgrade NOTE). It is a
// MOBILE: surface-shader pragma that has no meaning inside a .cginc included by a .compute - the
// MOBILE: arrays it complains about (locationPositions[1089]) are sized, and the mod demonstrably
// MOBILE: runs on D3D11 - but shipping a renderer exclusion on trust is not worth the risk on Metal.

struct RoadData {
    bool N[9], E[9], S[9], W[9];
    bool NW[9], NE[9], SW[9], SE[9];
};

RoadData GetRoadData() {
    RoadData d;

    for (int i = 0; i < 9; i++) {
        d.N[i] = N_E_S_W[i].x > 0.5;
        d.E[i] = N_E_S_W[i].y > 0.5;
        d.S[i] = N_E_S_W[i].z > 0.5;
        d.W[i] = N_E_S_W[i].w > 0.5;

        d.NW[i] = NW_NE_SW_SE[i].x > 0.5;
        d.NE[i] = NW_NE_SW_SE[i].y > 0.5;
        d.SW[i] = NW_NE_SW_SE[i].z > 0.5;
        d.SE[i] = NW_NE_SW_SE[i].w > 0.5;
    }

    /*d.N = N_E_S_W.x > 0.5;
    d.E = N_E_S_W.y > 0.5;
    d.S = N_E_S_W.z > 0.5;
    d.W = N_E_S_W.w > 0.5;

    d.NW = NW_NE_SW_SE.x > 0.5;
    d.NE = NW_NE_SW_SE.y > 0.5;
    d.SW = NW_NE_SW_SE.z > 0.5;
    d.SE = NW_NE_SW_SE.w > 0.5;*/

    return d;
}

float2 NearestPointInLine(float2 pos, float2 lineStart, float2 lineEnd) {
    float2 l = (lineEnd - lineStart);
    float len = length(l);
    l = normalize(l);

    float2 v = pos - lineStart;
    float2 d = dot(v, l);
    d = clamp(d, 0.0, len);

    return lineStart + l * d;
}

float GetRoadSegmentWeight(float2 pos, float2 roadStart, float2 roadEnd) {
    // Use NearestPointInLine as a base to find the nearest point on the road
    float2 roadPt = NearestPointInLine(pos, roadStart, roadEnd);

    // Calculate directional vector and sample position for noise
    // MOBILE: guarded - normalize(0) is NaN for a sample lying exactly on the road segment;
    // MOBILE: dir only offsets the noise sample point, so any unit vector will do.
    float2 toPos = pos - roadPt;
    float toPosLen = length(toPos);
    float2 dir = toPosLen > 1e-5 ? (toPos / toPosLen) : float2(1.0, 0.0);
    float2 samplePos = roadPt + dir * 15.0; // Adjust as needed
    samplePos -= (floor(samplePos / terrainSize) * terrainSize);
    samplePos /= terrainSize;

    // Sample noise to adjust fade distance dynamically
    float noise = tileableNoise.SampleLevel(bm_linear_clamp_sampler, samplePos, 0);

    // Determine the fade distance based on noise, similar to NearestRadialEdgePoint logic
    float baseFadeDist = 128.0; // Starting point for fade distance
    float fadeDistVariation = 1.0; // How much noise affects fade distance
    float fadeDist = baseFadeDist + baseFadeDist * fadeDistVariation * noise; // Apply noise influence

    // Calculate distance from the nearest road point to the position
    float dist = distance(roadPt, pos);

    // Adjust the weight calculation using a dynamic fade distance and smooth transitions
    float w = smoothstep(0.0, 1.0, 0.77 - clamp(dist / fadeDist, 0.0, 1.0));
    w = pow(w, 2.0); // Apply squared curve for smoother falloff
    //w = smoothstep(0.0, 1.0, w); // Use smoothstep for even smoother transition

    return w;
}


float GetRoadWeightForMapPixel(float2 pos, int2 mpOffset) {
    int i = (mpOffset.x + 1) + (mpOffset.y + 1) * 3;

    RoadData rd = GetRoadData();
    float f = terrainSize.x;
    float h = f * 0.5;
    float2 tp = terrainPosition + mpOffset * terrainSize * float2(1, -1);

    float2 n = tp + float2(h, f);
    float2 e = tp + float2(f, h);
    float2 s = tp + float2(h, 0);
    float2 w = tp + float2(0, h);

    float2 nw = tp + float2(0, f);
    float2 ne = tp + float2(f, f);
    float2 sw = tp + float2(0, 0);
    float2 se = tp + float2(f, 0);

    float2 c = tp + float2(h, h);

    float weight = 0.0;

    if (rd.N[i]) {
        weight += GetRoadSegmentWeight(pos, n, c);
    }
    if (rd.E[i]) {
        weight += GetRoadSegmentWeight(pos, e, c);
    }
    if (rd.S[i]) {
        weight += GetRoadSegmentWeight(pos, s, c);
    }
    if (rd.W[i]) {
        weight += GetRoadSegmentWeight(pos, w, c);
    }

    if (rd.NW[i]) {
        weight += GetRoadSegmentWeight(pos, nw, c);
    }
    if (rd.NE[i]) {
        weight += GetRoadSegmentWeight(pos, ne, c);
    }
    if (rd.SW[i]) {
        weight += GetRoadSegmentWeight(pos, sw, c);
    }
    if (rd.SE[i]) {
        weight += GetRoadSegmentWeight(pos, se, c);
    }

    return saturate(weight);
}

float GetRoadWeight(float2 pos) {
    float weight = 0;

    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            weight += GetRoadWeightForMapPixel(pos, int2(x, y));
        }
    }

    return saturate(weight);
}