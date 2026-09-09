// MOBILE PORT - github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40, Assets/Shaders/noises.cginc; compiled into the app (Resources.Load), no licence header upstream, private draft only.
struct PerlinParams
{
    float2 pos;
    int octaves;
    float frequency;
    float amplitude;
    float lacunarity;
    float persistence;
    float2 offset;
    float maxHeight;
};

struct SwissParams
{
    float2 pos;
    int octaves;
    float frequency;
    float amplitude;
    float lacunarity;
    float persistence;
    float2 offset;
    float ridgeOffset;
    float warp;
    float maxHeight;
};

struct JordanParams {
    float2 pos;
    int octaves;
    float frequency;
    float amplitude;
    float lacunarity;
    float persistence;
    float persistence1;
    float2 offset;
    float warp0;
    float warp;
    float damp;
    float damp0;
    float damp_scale;
    float maxHeight;
};

float remap(float inMin, float inMax, float outMin, float outMax, float val, bool shouldClamp = true) {
    if (shouldClamp) return clamp((val - inMin) / (inMax - inMin) * (outMax - outMin) + outMin, outMin, outMax);
    else return (val - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
}

float remap(float inMin, float inMax, float outMin, float outMax, float2 val, bool shouldClamp = true) {
    return float2(
        remap(inMin, inMax, outMin, outMax, val.x, shouldClamp),
        remap(inMin, inMax, outMin, outMax, val.y, shouldClamp)
        );
}

float remap(float inMin, float inMax, float outMin, float outMax, float3 val, bool shouldClamp = true) {
    return float3(
        remap(inMin, inMax, outMin, outMax, val.x, shouldClamp),
        remap(inMin, inMax, outMin, outMax, val.y, shouldClamp),
        remap(inMin, inMax, outMin, outMax, val.z, shouldClamp)
        );
}

float2 remap2(float2 inMin, float2 inMax, float2 outMin, float2 outMax, float2 val, bool shouldClamp = true)
{
    return float2(
        remap(inMin.x, inMax.x, outMin.x, outMax.x, val.x, shouldClamp),
        remap(inMin.y, inMax.y, outMin.y, outMax.y, val.y, shouldClamp)
        );
}

float getDistance(float2 p1, float2 p2) {
    float a = abs(p1.x - p2.x);
    float b = abs(p1.y - p2.y);
    return sqrt((a * a) + (b * b));
}

float clamp01(float v) {
    return clamp(0.0, 1.0, v);
}

float2 clamp01(float2 v) {
    return float2(
        clamp01(v.x),
        clamp01(v.y)
        );
}

float3 clamp01(float3 v) {
    return float3(
        clamp01(v.x),
        clamp01(v.y),
        clamp01(v.z)
        );
}

float4 clamp01(float4 v) {
    return float4(
        clamp01(v.x),
        clamp01(v.y),
        clamp01(v.z),
        clamp01(v.w)
        );
}

float oneMinus(float v) {
    return 1.0 - v;
}

float2 oneMinus(float2 v) {
    return float2(
        oneMinus(v.x),
        oneMinus(v.y)
        );
}

float3 oneMinus(float3 v) {
    return float3(
        oneMinus(v.x),
        oneMinus(v.y),
        oneMinus(v.z)
        );
}

float4 oneMinus(float4 v) {
    return float4(
        oneMinus(v.x),
        oneMinus(v.y),
        oneMinus(v.z),
        oneMinus(v.w)
        );
}

/***********************************************
 * Noises and their constants + helper methods *
 ***********************************************/

float random(in float2 uv)
{
    float2 noise = (frac(sin(dot(uv, float2(12.9898, 78.233) * 2.0)) * 43758.5453));
    return abs(noise.x + noise.y) * 0.5;
}

float4 FAST32_hash_2D(float2 gridcell)	//	generates a random number for each of the 4 cell corners
{
			//	gridcell is assumed to be an integer coordinate
    const float2 OFFSET = float2(26.0, 161.0);
    const float DOMAIN = 71.0;
    const float SOMELARGEFLOAT = 951.135664;
    float4 P = float4(gridcell.xy, gridcell.xy + 1.0);
    P = P - floor(P * (1.0 / DOMAIN)) * DOMAIN; //	truncate the domain
    P += OFFSET.xyxy; //	offset to interesting part of the noise
    P *= P; //	calculate and return the hash
    return frac(P.xzxz * P.yyww * (1.0 / SOMELARGEFLOAT.x));
}
float4 FAST32_hash_2D_Cell(float2 gridcell)	//	generates 4 different random numbers for the single given cell point
{
			//	gridcell is assumed to be an integer coordinate
    const float2 OFFSET = float2(26.0, 161.0);
    const float DOMAIN = 71.0;
    const float4 SOMELARGEFLOATS = float4(951.135664, 642.949883, 803.202459, 986.973274);
    float2 P = gridcell - floor(gridcell * (1.0 / DOMAIN)) * DOMAIN;
    P += OFFSET.xy;
    P *= P;
    return frac((P.x * P.y) * (1.0 / SOMELARGEFLOATS.xyzw));
}

void FAST32_hash_2D(float2 gridcell, out float4 hash_0, out float4 hash_1)	//	generates 2 random numbers for each of the 4 cell corners
{
			//    gridcell is assumed to be an integer coordinate
    const float2 OFFSET = float2(26.0, 161.0);
    const float DOMAIN = 71.0;
    const float2 SOMELARGEFLOATS = float2(951.135664, 642.949883);
    float4 P = float4(gridcell.xy, gridcell.xy + 1.0);
    P = P - floor(P * (1.0 / DOMAIN)) * DOMAIN;
    P += OFFSET.xyxy;
    P *= P;
    P = P.xzxz * P.yyww;
    hash_0 = frac(P * (1.0 / SOMELARGEFLOATS.x));
    hash_1 = frac(P * (1.0 / SOMELARGEFLOATS.y));
}
float2 Interpolation_C2(float2 x)
{
    return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
}
float4 Interpolation_C2_InterpAndDeriv(float2 x)
{
    return x.xyxy * x.xyxy * (x.xyxy * (x.xyxy * (x.xyxy * float4(6.0, 6.0, 0.0, 0.0) + float4(-15.0, -15.0, 30.0, 30.0)) + float4(10.0, 10.0, -60.0, -60.0)) + float4(0.0, 0.0, 30.0, 30.0));
}
float Value2D(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float2 Pf = P - Pi;
		
			//	calculate the hash.
    float4 hash = FAST32_hash_2D(Pi);
		
			//	blend the results and return
    float2 blend = Interpolation_C2(Pf);
    float2 res0 = lerp(hash.xy, hash.zw, blend.y);
    return lerp(res0.x, res0.y, blend.x);
}
float Perlin2D(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float4 Pf_Pfmin1 = P.xyxy - float4(Pi, Pi + 1.0);
		
			//	calculate the hash.
    float4 hash_x, hash_y;
    FAST32_hash_2D(Pi, hash_x, hash_y);
		
			//	calculate the gradient results
    float4 grad_x = hash_x - 0.49999;
    float4 grad_y = hash_y - 0.49999;
    float4 grad_results = rsqrt(grad_x * grad_x + grad_y * grad_y) * (grad_x * Pf_Pfmin1.xzxz + grad_y * Pf_Pfmin1.yyww);
		
#if 1
			//	Classic Perlin Interpolation
    grad_results *= 1.4142135623730950488016887242097; //	(optionally) scale things to a strict -1.0->1.0 range    *= 1.0/sqrt(0.5)
    float2 blend = Interpolation_C2(Pf_Pfmin1.xy);
    float2 res0 = lerp(grad_results.xy, grad_results.zw, blend.y);
    return lerp(res0.x, res0.y, blend.x);
#else
			//	Classic Perlin Surflet
			//	http://briansharpe.wordpress.com/2012/03/09/modifications-to-classic-perlin-noise/
			grad_results *= 2.3703703703703703703703703703704;		//	(optionally) scale things to a strict -1.0->1.0 range    *= 1.0/cube(0.75)
			float4 vecs_len_sq = Pf_Pfmin1 * Pf_Pfmin1;
			vecs_len_sq = vecs_len_sq.xzxz + vecs_len_sq.yyww;
			return dot( Falloff_Xsq_C2( min( float4( 1.0, 1.0, 1.0, 1.0 ), vecs_len_sq ) ), grad_results );
#endif
}

float SimplexPerlin2D(float2 P)
{
			//	simplex math constants
    const float SKEWFACTOR = 0.36602540378443864676372317075294; // 0.5*(sqrt(3.0)-1.0)
    const float UNSKEWFACTOR = 0.21132486540518711774542560974902; // (3.0-sqrt(3.0))/6.0
    const float SIMPLEX_TRI_HEIGHT = 0.70710678118654752440084436210485; // sqrt( 0.5 )	height of simplex triangle
    const float3 SIMPLEX_POINTS = float3(1.0 - UNSKEWFACTOR, -UNSKEWFACTOR, 1.0 - 2.0 * UNSKEWFACTOR); //	vertex info for simplex triangle
		
			//	establish our grid cell.
    P *= SIMPLEX_TRI_HEIGHT; // scale space so we can have an approx feature size of 1.0  ( optional )
    float2 Pi = floor(P + dot(P, float2(SKEWFACTOR, SKEWFACTOR)));
		
			//	calculate the hash.
    float4 hash_x, hash_y;
    FAST32_hash_2D(Pi, hash_x, hash_y);
		
			//	establish vectors to the 3 corners of our simplex triangle
    float2 v0 = Pi - dot(Pi, float2(UNSKEWFACTOR, UNSKEWFACTOR)) - P;
    float4 v1pos_v1hash = (v0.x < v0.y) ? float4(SIMPLEX_POINTS.xy, hash_x.y, hash_y.y) : float4(SIMPLEX_POINTS.yx, hash_x.z, hash_y.z);
    float4 v12 = float4(v1pos_v1hash.xy, SIMPLEX_POINTS.zz) + v0.xyxy;
		
			//	calculate the dotproduct of our 3 corner vectors with 3 random normalized vectors
    float3 grad_x = float3(hash_x.x, v1pos_v1hash.z, hash_x.w) - 0.49999;
    float3 grad_y = float3(hash_y.x, v1pos_v1hash.w, hash_y.w) - 0.49999;
    float3 grad_results = rsqrt(grad_x * grad_x + grad_y * grad_y) * (grad_x * float3(v0.x, v12.xz) + grad_y * float3(v0.y, v12.yw));
		
			//	Normalization factor to scale the final result to a strict 1.0->-1.0 range
			//	x = ( sqrt( 0.5 )/sqrt( 0.75 ) ) * 0.5
			//	NF = 1.0 / ( x * ( ( 0.5 ? x*x ) ^ 4 ) * 2.0 )
			//	http://briansharpe.wordpress.com/2012/01/13/simplex-noise/#comment-36
    const float FINAL_NORMALIZATION = 99.204334582718712976990005025589;
		
			//	evaluate the surflet, sum and return
    float3 m = float3(v0.x, v12.xz) * float3(v0.x, v12.xz) + float3(v0.y, v12.yw) * float3(v0.y, v12.yw);
    m = max(0.5 - m, 0.0); //	The 0.5 here is SIMPLEX_TRI_HEIGHT^2
    m = m * m;
    m = m * m;
    return dot(m, grad_results) * FINAL_NORMALIZATION;
}

float Cellular2D(float2 xy, int cellType, int distanceFunction)
{
    int xi = int(floor(xy.x));
    int yi = int(floor(xy.y));
		 
    float xf = xy.x - float(xi);
    float yf = xy.y - float(yi);
		 
    float dist1 = 9999999.0;
    float dist2 = 9999999.0;
    float dist3 = 9999999.0;
    float dist4 = 9999999.0;
    float2 cell;
		 
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            cell = FAST32_hash_2D_Cell(float2(xi + x, yi + y)).xy;
            cell.x += (float(x) - xf);
            cell.y += (float(y) - yf);
            float dist = 0.0;
            if (distanceFunction <= 1)
            {
                dist = sqrt(dot(cell, cell));
            }
            else if (distanceFunction > 1 && distanceFunction <= 2)
            {
                dist = dot(cell, cell);
            }
            else if (distanceFunction > 2 && distanceFunction <= 3)
            {
                dist = abs(cell.x) + abs(cell.y);
                dist *= dist;
            }
            else if (distanceFunction > 3 && distanceFunction <= 4)
            {
                dist = max(abs(cell.x), abs(cell.y));
                dist *= dist;
            }
            else if (distanceFunction > 4 && distanceFunction <= 5)
            {
                dist = dot(cell, cell) + cell.x * cell.y;
            }
            else if (distanceFunction > 5 && distanceFunction <= 6)
            {
                dist = pow(abs(cell.x * cell.x * cell.x * cell.x + cell.y * cell.y * cell.y * cell.y), 0.25);
            }
            else if (distanceFunction > 6 && distanceFunction <= 7)
            {
                dist = sqrt(abs(cell.x)) + sqrt(abs(cell.y));
                dist *= dist;
            }
            if (dist < dist1)
            {
                dist4 = dist3;
                dist3 = dist2;
                dist2 = dist1;
                dist1 = dist;
            }
            else if (dist < dist2)
            {
                dist4 = dist3;
                dist3 = dist2;
                dist2 = dist;
            }
            else if (dist < dist3)
            {
                dist4 = dist3;
                dist3 = dist;
            }
            else if (dist < dist4)
            {
                dist4 = dist;
            }
        }
    }
		 
    if (cellType <= 1)	// F1
        return dist1; //	scale return value from 0.0->1.333333 to 0.0->1.0  	(2/3)^2 * 3  == (12/9) == 1.333333
    else if (cellType > 1 && cellType <= 2)	// F2
        return dist2;
    else if (cellType > 2 && cellType <= 3)	// F3
        return dist3;
    else if (cellType > 3 && cellType <= 4)	// F4
        return dist4;
    else if (cellType > 4 && cellType <= 5)	// F2 - F1 
        return dist2 - dist1;
    else if (cellType > 5 && cellType <= 6)	// F3 - F2 
        return dist3 - dist2;
    else if (cellType > 6 && cellType <= 7)	// F1 + F2/2
        return dist1 + dist2 / 2.0;
    else if (cellType > 7 && cellType <= 8)	// F1 * F2
        return dist1 * dist2;
    else if (cellType > 8 && cellType <= 9)	// Crackle
        return max(1.0, 10 * (dist2 - dist1));
    else
        return dist1;
}

float3 PerlinSurflet2D_Deriv(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float4 Pf_Pfmin1 = P.xyxy - float4(Pi, Pi + 1.0);
		
			//	calculate the hash.
    float4 hash_x, hash_y;
    FAST32_hash_2D(Pi, hash_x, hash_y);
		
			//	calculate the gradient results
    float4 grad_x = hash_x - 0.49999;
    float4 grad_y = hash_y - 0.49999;
    float4 norm = rsqrt(grad_x * grad_x + grad_y * grad_y);
    grad_x *= norm;
    grad_y *= norm;
    float4 grad_results = grad_x * Pf_Pfmin1.xzxz + grad_y * Pf_Pfmin1.yyww;
		
			//	eval the surflet
    float4 m = Pf_Pfmin1 * Pf_Pfmin1;
    m = m.xzxz + m.yyww;
    m = max(1.0 - m, 0.0);
    float4 m2 = m * m;
    float4 m3 = m * m2;
		
			//	calc the deriv
    float4 temp = -6.0 * m2 * grad_results;
    float xderiv = dot(temp, Pf_Pfmin1.xzxz) + dot(m3, grad_x);
    float yderiv = dot(temp, Pf_Pfmin1.yyww) + dot(m3, grad_y);
		
			//	sum the surflets and return all results combined in a float3
    const float FINAL_NORMALIZATION = 2.3703703703703703703703703703704; //	scales the final result to a strict 1.0->-1.0 range
    return float3(dot(m3, grad_results), xderiv, yderiv) * FINAL_NORMALIZATION;
}

float3 PerlinSurflet2D_Deriv_Vol(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float4 Pf_Pfmin1 = P.xyxy - float4(Pi, Pi + 1.0);
		
			//	calculate the hash.
    float4 hash_x, hash_y;
    FAST32_hash_2D(Pi, hash_x, hash_y);
		
			//	calculate the gradient results
    float4 grad_x = hash_x - 0.49999;
    float4 grad_y = hash_y - 0.49999;
    float4 norm = rsqrt(grad_x * grad_x + grad_y * grad_y);
    grad_x *= norm;
    grad_y *= norm;
    float4 grad_results = grad_x * Pf_Pfmin1.xzxz + grad_y * Pf_Pfmin1.yyww;

			//	eval the surflet
    float4 m = Pf_Pfmin1 * Pf_Pfmin1;
    m = m.xzxz + m.yyww;
    m = max(1.0 - m, 0.0);
    float4 m2 = m * m;
    float4 m3 = m * m2;
		
			//	calc the deriv
    float4 temp = -6.0 * m2 * grad_results;
    float xderiv = dot(temp, Pf_Pfmin1.xzxz) + dot(m3, grad_x);
    float yderiv = dot(temp, Pf_Pfmin1.yyww) + dot(m3, grad_y);
		
			//	sum the surflets and return all results combined in a float3
    const float FINAL_NORMALIZATION = 2.3703703703703703703703703703704; //	scales the final result to a strict 1.0->-1.0 range
    return float3(dot(m3, grad_results), xderiv, yderiv) * FINAL_NORMALIZATION;
}

float3 SimplexPerlin2D_Deriv(float2 P)
{
			//	simplex math constants
    const float SKEWFACTOR = 0.36602540378443864676372317075294; // 0.5*(sqrt(3.0)-1.0)
    const float UNSKEWFACTOR = 0.21132486540518711774542560974902; // (3.0-sqrt(3.0))/6.0
    const float SIMPLEX_TRI_HEIGHT = 0.70710678118654752440084436210485; // sqrt( 0.5 )	height of simplex triangle
    const float3 SIMPLEX_POINTS = float3(1.0 - UNSKEWFACTOR, -UNSKEWFACTOR, 1.0 - 2.0 * UNSKEWFACTOR); //	vertex info for simplex triangle
		
			//	establish our grid cell.
    P *= SIMPLEX_TRI_HEIGHT; // scale space so we can have an approx feature size of 1.0  ( optional )
    float2 Pi = floor(P + dot(P, float2(SKEWFACTOR, SKEWFACTOR)));
		
			//	calculate the hash.
    float4 hash_x, hash_y;
    FAST32_hash_2D(Pi, hash_x, hash_y);
		
			//	establish vectors to the 3 corners of our simplex triangle
    float2 v0 = Pi - dot(Pi, float2(UNSKEWFACTOR, UNSKEWFACTOR)) - P;
    float4 v1pos_v1hash = (v0.x < v0.y) ? float4(SIMPLEX_POINTS.xy, hash_x.y, hash_y.y) : float4(SIMPLEX_POINTS.yx, hash_x.z, hash_y.z);
    float4 v12 = float4(v1pos_v1hash.xy, SIMPLEX_POINTS.zz) + v0.xyxy;
		
			//	calculate the dotproduct of our 3 corner vectors with 3 random normalized vectors
    float3 grad_x = float3(hash_x.x, v1pos_v1hash.z, hash_x.w) - 0.49999;
    float3 grad_y = float3(hash_y.x, v1pos_v1hash.w, hash_y.w) - 0.49999;
    float3 norm = rsqrt(grad_x * grad_x + grad_y * grad_y);
    grad_x *= norm;
    grad_y *= norm;
    float3 grad_results = grad_x * float3(v0.x, v12.xz) + grad_y * float3(v0.y, v12.yw);
		
			//	evaluate the surflet
    float3 m = float3(v0.x, v12.xz) * float3(v0.x, v12.xz) + float3(v0.y, v12.yw) * float3(v0.y, v12.yw);
    m = max(0.5 - m, 0.0); //	The 0.5 here is SIMPLEX_TRI_HEIGHT^2
    float3 m2 = m * m;
    float3 m4 = m2 * m2;
		
			//	calc the deriv
    float3 temp = 8.0 * m2 * m * grad_results;
    float xderiv = dot(temp, float3(v0.x, v12.xz)) - dot(m4, grad_x);
    float yderiv = dot(temp, float3(v0.y, v12.yw)) - dot(m4, grad_y);
		
    const float FINAL_NORMALIZATION = 99.204334582718712976990005025589; //	scales the final result to a strict 1.0->-1.0 range
		
			//	sum the surflets and return all results combined in a float3
    return float3(dot(m4, grad_results), xderiv, yderiv) * FINAL_NORMALIZATION;
}

float3 Value2D_Deriv(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float2 Pf = P - Pi;
		
			//	calculate the hash.
    float4 hash = FAST32_hash_2D(Pi);
		
			//	blend the results and return
    float4 blend = Interpolation_C2_InterpAndDeriv(Pf);
    float4 res0 = lerp(hash.xyxz, hash.zwyw, blend.yyxx);
    return float3(res0.x, 0.0, 0.0) + (res0.yyw - res0.xxz) * blend.xzw;
}

float3 Value2DCell_Deriv(float2 P)
{
			//	establish our grid cell and unit position
    float2 Pi = floor(P);
    float2 Pf = P - Pi;
		
			//	calculate the hash.
    float4 hash = FAST32_hash_2D_Cell(Pi);
		
			//	blend the results and return
    float4 blend = Interpolation_C2_InterpAndDeriv(Pf);
    float4 res0 = lerp(hash.xyxz, hash.zwyw, blend.yyxx);
    return float3(res0.x, 0.0, 0.0) + (res0.yyw - res0.xxz) * blend.xzw;
}

float2 Hash(float2 pos) {
    float2 k = float2(0.3183099, 0.3678794);
    pos = pos * k + k.yx;
    return frac(16.0 * k * frac(pos.x * pos.y * (pos.x + pos.y)));
}

float hash1(float2 p)
{
    p = 50.0 * frac(p * 0.3183099);
    return frac(p.x * p.y * (p.x + p.y));
}
float3 noised(float2 x)
{
    float2 p = floor(x);
    float2 w = frac(x);

    float2 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);

    float a = hash1(p + float2(0, 0));
    float b = hash1(p + float2(1, 0));
    float c = hash1(p + float2(0, 1));
    float d = hash1(p + float2(1, 1));

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k4 = a - b - c + d;

    return float3(-1.0 + 2.0 * (k0 + k1 * u.x + k2 * u.y + k4 * u.x * u.y),
        2.0 * du * float2(k1 + k4 * u.y,
            k2 + k4 * u.x));
}

float Dunes(PerlinParams p)
{
    float sum = 0;
    for (int i = 0; i < p.octaves; i++)
    {
        float h = 0;
        h = abs(Value2D((p.pos + p.offset) * p.frequency));
        sum += h * p.amplitude;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }
    return saturate(sum) * (p.maxHeight / newHeight);
}

float BillowHills(float2 p)
{
    int octaves = 7;
    float frequency = 100.0;
    float amplitude = 0.8;
    float lacunarity = 0.55;
    float persistence = 1.03;
    float2 offset = float(14).xx;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = abs(SimplexPerlin2D((p + offset) * frequency));
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return saturate(sum * 0.04 - 0.05);
}
float BaseBumps(float2 p)
{
    int octaves = 19;
    float frequency = 100.0;
    float amplitude = 0.3;
    float lacunarity = 1.33;
    float persistence = 0.91;
    float2 offset = float(14).xx;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = Perlin2D((p + offset) * frequency);
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return sum * 0.002;
}
float SimplePerlin(PerlinParams p)
{
    float sum = 0;
    for (int i = 0; i < p.octaves; i++)
    {
        float h = 0;
        h = Perlin2D((p.pos + p.offset) * p.frequency);
        sum += h * p.amplitude;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }

    return clamp(sum, -1, 1) * (p.maxHeight / newHeight);
}

float PositivePerlin(PerlinParams p)
{
    float sum = 0;
    for (int i = 0; i < p.octaves; i++)
    {
        float h = 0;
        h = Perlin2D((p.pos + p.offset) * p.frequency);
        sum += h * p.amplitude;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }

    return saturate(sum * 0.5 + 0.5) * (p.maxHeight / newHeight);
}

float4 ColorPerlin(PerlinParams p)
{
    float r = 0;
    float g = 0;
    float b = 0;
    float a = 0;

    float2 offR = p.offset;
    float2 offG = offR + float2(483.58, 195.28);
    float2 offB = offG + float2(116.26, 994.28);
    float2 offA = offB + float2(472.89, 192.57);

    for (int i = 0; i < p.octaves; i++)
    {
        float hr = 0;
        hr = Perlin2D((p.pos + offR) * p.frequency);
        r += hr * p.amplitude;

        float hg = 0;
        hg = Perlin2D((p.pos + offG) * p.frequency * 0.2);
        g += hg * p.amplitude;

        float hb = 0;
        hb = Perlin2D((p.pos + offB) * p.frequency * 0.01);
        b += hb * p.amplitude;

        float ha = 0;
        ha = Perlin2D((p.pos + offA) * p.frequency * 0.002);
        a += ha * p.amplitude;

        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }

    r = saturate(r * 0.5 + 0.5);
    g = saturate(g * 0.5 + 0.5);
    b = saturate(b * 0.5 + 0.5);
    a = saturate(a * 0.5 + 0.5);

    float pf = saturate(p.maxHeight / newHeight);
    float power = lerp(1, 5, pf);

    return pow(float4(r, g, b, a), power);
}
float InterestingHills(float2 p)
{
    int octaves = 7;
    float frequency = 100.0;
    float amplitude = 0.11;
    float lacunarity = 1.6;
    float persistence = 1.0;
    float2 offset = float(14).xx;
    float ridgeOffset = 1.55;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = 0.5 * (ridgeOffset - abs(4 * Value2D((p + offset) * frequency)));
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return sum * 0.06;
}
float TurbulenceBase(float2 p, float freq)
{
    int octaves = 8;
    float frequency = 1.0;
    float amplitude = 0.5;
    float lacunarity = 2.13;
    float persistence = 1.05;
    float2 offset = float(14).xx;
    float ridgeOffset = 1.25;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = 0.5 * (ridgeOffset - abs(4 * Perlin2D((p + offset) * frequency)));
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return sum * 0.06;
}
float CellNoise(float2 p, float frequency)
{
    int octaves = 8;
    float amplitude = 0.73;
    float lacunarity = 0.7;
    float persistence = 0.5;
    float2 offset = float(14).xx;
    int cellType = 2;
    int distanceFunction = 2;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = Cellular2D((p + offset) * frequency, cellType, distanceFunction);
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return smoothstep(0, 0.3, clamp(remap(0.5, 1, 0, 0.5, saturate(sum)), 0, 0.3)) * 0.3;
}

float PlateauMountains(float2 p)
{
    int octaves = 2;
    float frequency = 40.0;
    float amplitude = 0.4;
    float lacunarity = 0.07;
    float persistence = 0.5;
    float2 offset = float(14).xx;
    int cellType = 3;
    int distanceFunction = 5;

    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = Cellular2D((p + offset) * frequency, cellType, distanceFunction);
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return 0.6 * (pow(smoothstep(0, 1, remap(0.4, 0.5, 0, 1, saturate(sum))), 3) + remap(0.3, 0.5, 0, 0.7, saturate(sum))) * 0.4 + BaseBumps(p / 2.0) * 4;
}

float IQNoises(PerlinParams p)
{
    float sum = 0;
    float2 dsum = float2(0.0, 0.0);
    for (int i = 0; i < p.octaves; i++)
    {
        float3 n = PerlinSurflet2D_Deriv((p.pos + p.offset) * p.frequency);
        dsum += n.yz;
        sum += p.amplitude * n.x / (1 + dot(dsum, dsum));
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }
    return saturate(abs(sum));
}

float IQMountains(SwissParams p)
{
    float sum = 0;
    float2 dsum = float2(0.0, 0.0);
    for (int i = 0; i < p.octaves; i++)
    {
        float3 n = PerlinSurflet2D_Deriv((p.pos + dsum * p.warp + p.offset) * p.frequency);
        dsum += n.yz * -n.x;
        sum += p.amplitude * n.x / (1 + dot(dsum, dsum));
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }
    return saturate(sum) * (p.maxHeight / newHeight);
}

float SwissMountainsGen(SwissParams p)
{
    float f = p.frequency;
    float a = lerp(p.amplitude * 0.125, p.amplitude, p.ridgeOffset);
    float t = 0.0;
    float2 dsum = 0;
    float warp = lerp(p.warp * 0.1, p.warp, p.ridgeOffset);
    float l = p.lacunarity;

    for (int i = 0; i < p.octaves; i++) {
        float3 n = SimplexPerlin2D_Deriv((p.pos + warp * dsum) * f + p.offset);
        t += a * (1 - abs(n.x));
        dsum += a * n.yz * -n.x;
        f *= l;
        a *= p.persistence * saturate(t);
    }

    return t * (p.maxHeight / newHeight);
}

float SwissMountains(SwissParams p) {
    float f1 = 0.5;
    float f2 = 0.25;

    SwissParams p1 = p;
    SwissParams p2 = p;

    p1.frequency *= f1;
    p2.frequency *= f2;
    p1.warp *= f1;
    p2.warp *= f2;

    float w1 = SwissMountainsGen(p1);
    float w2 = SwissMountainsGen(p2);

    w1 = saturate(w1 * f1 + (1.0 - f1));
    w2 = saturate(w2 * f2 + (1.0 - f2));

    return SwissMountainsGen(p) * w1 * w2;
}

float MountainBaseNoise(SwissParams p) {
    float h = SwissMountains(p);
    h = saturate(pow(h, 5));
    h = smoothstep(0.2, 0.8, h);

    return h;
}

float3 Canyonize(float3 n) {
    float3 rm = n;
    if (abs(rm.x) > 0.5) {
        rm = (1.0 - abs(rm)) * -1;
    }

    return abs(rm);
}

float SwissFaults2(SwissParams p)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    float heightVar = Perlin2D(p.pos * 1000.0) * 0.5 + 0.5;
    float t = 20.0;

    p.frequency = lerp(p.frequency, p.frequency * 0.99999, heightVar);

    for (int i = 0; i < p.octaves; i++)
    {
        float3 n = 0.5 * (0 + (p.ridgeOffset - abs(sin(PerlinSurflet2D_Deriv((p.pos + p.offset + p.warp * dsum) * p.frequency)))));
        sum = max(sum + p.amplitude * n.x, sum * p.amplitude);
        dsum += p.amplitude * n.yz * -n.x;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }

    float result = sum * (p.maxHeight / newHeight);

    return result;
}

float SwissFaultsNoise(SwissParams p)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    float heightVar = Perlin2D(p.pos * 1000.0) * 0.5 + 0.5;
    float t = 20.0;

    p.frequency = lerp(p.frequency, p.frequency * 0.99999, heightVar);

    float lowSum = 0;
    float baseSum;

    for (int i = 0; i < p.octaves; i++)
    {
        float3 n = 0.5 * (0 + (p.ridgeOffset - abs(sin(PerlinSurflet2D_Deriv((p.pos + p.offset + p.warp * dsum) * p.frequency)))));
        sum += p.amplitude * n.x;

        float per = Perlin2D(p.pos * p.frequency * 3);
        lowSum += per * p.amplitude * 2; 

        dsum += p.amplitude * n.yz * -n.x;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;

        if (i == 4) {
            baseSum = sum;
        }
    }

    float result = clamp(sum, 0, 1);
    return smoothstep(0, 1, 1.0 - result) * (p.maxHeight / newHeight);
}

float SwissTime(SwissParams p)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    for (int i = 0; i < p.octaves; i++)
    {
        float3 n = 0.5 * (0 + (p.ridgeOffset - abs(sin(SimplexPerlin2D_Deriv((p.pos + p.offset + p.warp * dsum) * p.frequency)))));
        n = pow(n, 2);
        sum += p.amplitude * n.x;
        dsum += p.amplitude * n.yz * -n.x;
        p.frequency *= p.lacunarity;
        p.amplitude *= p.persistence;
    }

    float result = (1.0 - sum);

    return pow(saturate(result * 2), 3) * (p.maxHeight / newHeight);
}

float SwissCellNoise(SwissParams p)
{
    float f = p.frequency;
    float a = lerp(p.amplitude * 0.125, p.amplitude, p.ridgeOffset);
    float t = 0.0;
    float2 dsum = 0;
    float warp = lerp(p.warp * 0.1, p.warp, p.ridgeOffset);
    float l = p.lacunarity;

    for (int i = 0; i < p.octaves; i++) {
        float3 n = PerlinSurflet2D_Deriv((p.pos + warp * dsum) * f);
        float h = p.ridgeOffset - abs(n.x);

        t += a * h;
        dsum += a * ((n.yz - (1.0 - p.ridgeOffset)) / p.ridgeOffset) * -n.x;
        f *= l;
        a *= p.persistence;
    }

    return t * (p.maxHeight / newHeight);
}

float VividHills(float2 p, int octaves = 8, float frequency = 88.3, float amplitude = 0.2, float lacunarity = 2.307, float persistence = 0.4, float2 offset = float(0).xx, float ridgeOffset = 1.25, float warp = 0.008)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    float heightVar = Perlin2D(p * 1000.0) * 0.5 + 0.5;
    float t = 1.0 / 200000.0;

    frequency = lerp(frequency, frequency * 0.99999, heightVar);

    for (int i = 0; i < octaves; i++)
    {
        float3 n = 0.5 * (ridgeOffset - PerlinSurflet2D_Deriv((p + offset + warp * dsum) * frequency));

        sum += amplitude * n.x;
        dsum += amplitude * n.yz * n.x;
        frequency *= lacunarity;
        amplitude *= persistence;
    }

    sum *= 0.5;
    sum += 0.5;
    
    sum *= 0.2;
    sum = saturate(sum);

    float result = sum;

    return result;
}

float SwissPlayground(float2 p, int octaves = 8, float frequency = 88.3, float amplitude = 0.2, float lacunarity = 2.307, float persistence = 0.4, float2 offset = float(0).xx, float ridgeOffset = 1.25, float warp = 0.008)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    float heightVar = Perlin2D(p * 1000.0) * 0.5 + 0.5;
    float t = 1.0 / 2000.0;

    frequency = lerp(frequency, frequency * 0.99999, heightVar);

    for (int i = 0; i < octaves; i++)
    {
        float3 n = 0.5 * (ridgeOffset - pow(abs(sin(PerlinSurflet2D_Deriv((p + offset + warp * dsum) * frequency))), 5));
        float3 n_n = 0.5 * (ridgeOffset - pow(abs(sin(PerlinSurflet2D_Deriv(((p + float2(0, t)) + offset + warp * dsum) * frequency))), 5));
        float3 n_s = 0.5 * (ridgeOffset - pow(abs(sin(PerlinSurflet2D_Deriv(((p + float2(0, -t)) + offset + warp * dsum) * frequency))), 5));
        float3 n_e = 0.5 * (ridgeOffset - pow(abs(sin(PerlinSurflet2D_Deriv(((p + float2(-t, 0)) + offset + warp * dsum) * frequency))), 5));
        float3 n_w = 0.5 * (ridgeOffset - pow(abs(sin(PerlinSurflet2D_Deriv(((p + float2(t, 0)) + offset + warp * dsum) * frequency))), 5));

        n = (n + n_n + n_e + n_w + n_s) / 5.0;

        sum += amplitude * n.x;
        dsum += amplitude * n.yz * -n.x;
        frequency *= lacunarity;
        amplitude *= persistence;
    }

    sum *= 0.5;
    sum += 0.5;
    
    
    
    sum = pow(sum, 5);
    sum = lerp(sum, smoothstep(0, 1, sum), -1.0);

    sum *= 0.05;
    sum = saturate(sum);
    sum *= smoothstep(0, 1, (((Perlin2D(p * 2000) + Perlin2D(p * 200)) / 2.0) * 0.5 + 0.5));

    float result = sum;

    return result;
}

float VolcanicMountains(float2 p, int octaves = 8, float frequency = 88.3, float amplitude = 0.2, float lacunarity = 2.307, float persistence = 0.4, float2 offset = float(0).xx, float ridgeOffset = 1.25, float warp = 0.008)
{
    float sum = 0.0;
    float2 dsum = float2(0.0, 0.0);

    float heightVar = Perlin2D(p * 1000.0) * 0.5 + 0.5;

    frequency = lerp(frequency, frequency * 0.99999, heightVar);

    for (int i = 0; i < octaves; i++)
    {
        float3 n = 0.5 * (0 + (ridgeOffset - abs(PerlinSurflet2D_Deriv((p + offset + warp * dsum) * frequency))));

        sum += amplitude * (1.0 - abs(n.x));
        dsum += amplitude * n.yz * -n.x;
        frequency *= lacunarity;
        amplitude *= persistence * saturate(sum);
    }
    sum = saturate(sum * lerp(1.0, 0.3, remap(0.5, 1.0, 0.0, 1.0, persistence, false)));

    sum *= 0.2;

    float result = sum;

    return saturate(result);
}

float JordanMountainsGen(JordanParams p)
{
    float3 n = PerlinSurflet2D_Deriv(p.pos * p.frequency);
    float3 n2 = n * n.x;
    float sum = n2.x;
    float2 dsum_warp = p.warp0 * n2.yz;
    float2 dsum_damp = p.damp0 * n2.yz;

    float amp = p.persistence1 * p.amplitude;
    float freq = p.lacunarity;
    float damped_amp = amp * p.persistence;

    for (int i = 1; i < p.octaves; i++)
    {
        n = PerlinSurflet2D_Deriv((p.pos * freq + dsum_warp.xy + p.offset) * p.frequency);
        n2 = n * n.x;
        sum += damped_amp * n2.x;
        dsum_warp += p.warp * n2.yz;
        dsum_damp += p.damp * n2.yz;
        freq *= p.lacunarity;
        amp *= p.persistence;
        damped_amp = amp * (1 - p.damp_scale / (1 + dot(dsum_damp, dsum_damp)));
    }

    float dampComp = p.damp_scale - 1.0;

    return (sum + dampComp) * (p.maxHeight / newHeight);
}

float JordanMountains(JordanParams p) {
    float f1 = 0.25;
    float f2 = 0.125;

    JordanParams p1 = p;
    JordanParams p2 = p;

    p1.frequency *= f1;
    p2.frequency *= f2;

    float w1 = JordanMountainsGen(p1);
    float w2 = JordanMountainsGen(p2);

    w1 = saturate(w1 * f1 + (1.0 - f1));
    w2 = saturate(w2 * f2 + (1.0 - f2));

    return JordanMountainsGen(p) * w1 * w2;
}

float CellNormal(float2 p, int octaves, float2 offset, float frequency, float amplitude, float lacunarity, float persistence, int cellType, int distanceFunction)
{
    float sum = 0;
    for (int i = 0; i < octaves; i++)
    {
        float h = 0;
        h = Cellular2D((p + offset) * frequency, cellType, distanceFunction);
        sum += h * amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }
    return sum;
}
