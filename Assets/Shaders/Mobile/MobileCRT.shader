// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: single-pass CRT filter for the retro presentation path. Used as the material
// argument of the one Graphics.Blit in RetroPresentation.OnRenderImage, so it runs once per
// frame at native backbuffer resolution over the WORLD only - DaggerfallUI draws the HUD and
// menus afterwards in OnGUI, so they stay pin-sharp and flat. That is deliberate on a touch
// port: curving the touch controls away from where fingers land is worse than the purist
// inconsistency of a sharp HUD over a curved world.
//
// PROVENANCE. Every technique here is textbook and was written from the formula, not adapted
// from an existing shader: a barrel UV warp, a raised-cosine scanline, an RGB stripe taken
// from the destination pixel column, and a radial vignette. No code from
// crt-pi, crt-geom, crt-easymode, crt-royale, crt-lottes, zfast_crt or any other CRT shader
// is present here, and none was copied while writing it. The port's MIT licence therefore
// covers this file outright.
//
// COST. The cheap tier is ONE tex2D with no dependent read, plus ~30 ALU: at 5.6 MP (12.9"
// iPad Pro) that is one full-screen pass with no new render target and no new texture, since
// the grille is procedural rather than a mask texture (a mask texture also aliases badly on a
// high-DPI panel). The scanline is antialiased analytically - its closed form is evaluated
// three times across the destination pixel's own footprint, which costs ALU and zero
// bandwidth. CRT_HALATION adds two horizontal taps and is off by default; both variants are
// pinned in RequiredShaderVariants so a later quality tier can turn it on without a rebuild.
//
// MOIRE. _ScanlineCount is the SOURCE raster's own line count, taken from the height of the
// texture the main camera is actually rendering into (MobileCrt.ScanlineCount ->
// RetroRenderer.RetroTexture.height): 200 or 400 normally, 154 or 308 when the large HUD is
// docked, because DFU points the camera at a shortened raster and stretches it into the
// 640x400 presentation texture. Never a device-pixel figure, and never the retro mode's
// nominal count when the live raster disagrees: 200 lines drawn over a 154-line raster beat
// against it at 46 cycles down the screen. Lines placed on the raster ARE the raster, and
// hold still.
//
// LINEAR LIGHT. There is no gamma round trip here, and there must not be one. The project is
// Linear (ProjectSettings m_ActiveColorSpace: 1) and Assets/Resources/RetroPresentation.
// renderTexture is not sRGB (m_SRGB: 0), so what tex2D hands back is already linear light.
// The first cut squared the sample on the way in and took its square root on the way out,
// which is an identity on the picture but applies every modulation as sqrt(m) - the
// scanlines, grille and vignette all landed about half as deep as the numbers ask for. The
// modulations are applied directly instead: correct for this project, and two ALU cheaper.
//
// The retro presentation source is a Point-filtered render texture on purpose (crisp pixels),
// and it is sampled as-is: the softness of a CRT comes from the scanline and grille
// modulation here, not from bilinear filtering of the source.
//
// Place in Assets/Shaders/Mobile/

Shader "Daggerfall/Mobile/CRT"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Curvature ("Barrel curvature", Range(0, 0.3)) = 0.08
        _Scanlines ("Scanline depth", Range(0, 1)) = 0.35
        _ScanlineCount ("Scanlines in the source raster", Float) = 200
        _Mask ("Aperture grille depth", Range(0, 1)) = 0.25
        _Vignette ("Vignette depth", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Lighting Off
        Cull Off ZWrite Off ZTest Always
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ CRT_HALATION

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            // VPOS carries the destination pixel's own coordinate, which is what the grille needs:
            // deriving it from uv * _ScreenParams would be wrong whenever ViewportChanger has set
            // a partial camera.rect (4:3 stretch, 16:10 pillarbox, docked large HUD), and a grille
            // whose period is off by that scale factor is exactly a moire generator.
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_VPOS_TYPE vpos : VPOS;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half _Curvature;
            half _Scanlines;
            float _ScanlineCount;
            half _Mask;
            half _Vignette;

            v2f vert (appdata v, out float4 outpos : SV_POSITION)
            {
                v2f o;
                outpos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.vpos = 0;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Barrel warp. Centre the coordinate, push it outward by k*r^2, put it back.
                // r2 is kept for the vignette, which wants the undistorted radius.
                float2 centred = i.uv * 2.0 - 1.0;
                float r2 = dot(centred, centred);
                float2 uv = centred * (1.0 + _Curvature * r2) * 0.5 + 0.5;

                // The sampled coordinate's footprint in one destination pixel, in source-uv units.
                float footprint = abs(ddy(uv.y));

                // Outside the curved screen there is no tube, so there is no light. A mask rather
                // than an early return, and deliberately: a derivative is only well defined in
                // uniform control flow, and an early-out here puts the ddy above inside a branch
                // that the pixels just off the curved edge have already left (the Metal compiler
                // sinks it there, which was measured, not assumed). One multiply also beats a
                // divergent branch in a full-screen pass.
                half inside = all(abs(uv - 0.5) <= 0.5) ? 1.0 : 0.0;

                half3 col = tex2D(_MainTex, uv).rgb;

#ifdef CRT_HALATION
                // Phosphor bleed: one source texel either side, added back weakly. The only part
                // of this shader that costs bandwidth, hence the keyword. Linear light, so the
                // taps add straight in.
                half3 left  = tex2D(_MainTex, float2(uv.x - _MainTex_TexelSize.x, uv.y)).rgb;
                half3 right = tex2D(_MainTex, float2(uv.x + _MainTex_TexelSize.x, uv.y)).rgb;
                col += 0.075 * (left + right);
#endif

                // Raised-cosine scanlines on the source raster. Three evaluations spread across the
                // destination pixel's footprint average the closed form over the pixel instead of
                // point-sampling it - the offsets are thirds of a PIXEL, not of a scanline period:
                // three samples a third of a period apart sit 120 degrees apart on the cosine and
                // sum to exactly zero, which would erase the scanlines altogether.
                // frac() keeps the cosine's argument inside one turn, so a 400-line raster does not
                // spend float mantissa on the integer part.
                //
                // PHASE. raster counts source rows, so an INTEGER raster is the seam between two
                // rows and a half-integer is a row's own centre. A CRT is brightest along the
                // centre of each line and dark in the gap between lines, so the darkening has to
                // peak on the integer: 0.5 + 0.5*cos, which is 1 there and 0 at the row centre.
                // The other way round (0.5 - 0.5*cos) dims the middle of every source row and
                // leaves the seam at full brightness - each row reads as a bright-edged donut and
                // the bright line straddles two differently coloured rows.
                float raster = uv.y * _ScanlineCount;
                float spread = footprint * _ScanlineCount * 0.33333333;
                const float tau = 6.2831853;
                half wave = (0.5 + 0.5 * cos(frac(raster - spread) * tau))
                          + (0.5 + 0.5 * cos(frac(raster        ) * tau))
                          + (0.5 + 0.5 * cos(frac(raster + spread) * tau));
                col *= 1.0 - _Scanlines * (wave * 0.33333333);

                // Aperture grille: every third destination pixel column keeps one primary and has
                // the other two pulled down. Procedural, so nothing to fetch and nothing to alias.
                half band = frac(i.vpos.x * 0.33333333);
                half3 notch = band < 0.33333333 ? half3(0.0, 1.0, 1.0)
                            : (band < 0.66666667 ? half3(1.0, 0.0, 1.0) : half3(1.0, 1.0, 0.0));
                col *= 1.0 - _Mask * notch;

                // Radial falloff of the tube's brightness. Saturated, and not decoratively: r2 is
                // dot(centred, centred), which reaches ~1.58 along the diagonal while still INSIDE
                // the curved screen, so an unsaturated factor goes negative in a lens-shaped band
                // along the edges for any _Vignette above ~0.63 - and the slider's range is 0..1.
                col *= saturate(1.0 - _Vignette * r2);

                return half4(col * inside, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
