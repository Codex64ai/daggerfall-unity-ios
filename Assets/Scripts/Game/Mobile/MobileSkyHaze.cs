// Project:         Daggerfall Unity - iOS Touch Layer
// License:         MIT License
//
// MOBILE 2026-09-14: SKY HAZE. Two halves of one idea, both switched by [Enhancements] SkyHaze.
//
//   1. THE FOG COLOUR IS THE SKY'S OWN HORIZON COLOUR. Distance haze is the sky seen through air;
//      when RenderSettings.fogColor and the sky disagree, the far mountains fade into a colour that
//      is not the colour behind them and the horizon reads as a seam. Stock Daggerfall Unity takes
//      the fog colour from ONE pixel of the sky image (DaggerfallSky: skyColors.west[0]); Dynamic
//      Skies takes it from its weather preset's FogDayColor once a second. Both are replaced here
//      by the actual horizon: the average of the sky image's bottom row for the painted sky, and
//      the preset colour lerped by sun elevation every frame for the procedural one.
//
//   2. A HAZE BAND ALONG THE BOTTOM OF THE SKY. Air near the ground is the thick part, so the sky
//      itself is faded toward the fog colour over a band above the horizon. Thickness follows the
//      SAME dial as the distance haze - [Video] DistantFogStrength, the settings panel's "Distance
//      fog" row - because they are the same atmosphere: at 0% the far terrain is bare AND the sky
//      is clean, at 200% both are heavy. That is the whole reason the band is not its own slider.
//
// WHY THE RULES LIVE HERE AND NOT IN THE TWO SKIES. The two skies draw nothing alike - one is a
// procedural skybox shader, the other is two Graphics.DrawTexture quads of a 512x219 palettised
// image - but the band has to be the same band in both or the dial means two different things
// depending on which mod is on. So the arithmetic (how thick, how strong, what the horizon colour
// is) is pure and lives once, here, where the self test can read it; each sky only applies it.
//
// THE BAND IS MEASURED IN "FRACTION OF THE WAY UP", not degrees or pixels, because that is the one
// quantity both skies can honour: the skybox gets it as a normalised world-space Y (the shader's
// _HorizonHaze), and the painted sky gets it as a fraction of the image height. 10% of the way up
// per 1.0 of dial - so 100% is a tenth of the sky hazed, 200% a fifth, 0% none.

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileSkyHaze
    {
        /// <summary>
        /// MOBILE: how far up the sky the haze band reaches per 1.0 of Distance fog. 0.10 puts the
        /// top of the band a tenth of the way to the zenith at the dial's 100% - high enough to see
        /// against the mountains, low enough that the sun and clouds are never touched. The dial
        /// clamps at 2.0, so the band can never exceed a fifth of the sky.
        /// </summary>
        public const float BandPerStrength = 0.10f;

        /// <summary>
        /// MOBILE: how much of the fog colour the very bottom row of the band takes - 0.85, not 1.0.
        /// A band that reached the fog colour exactly would erase the horizon's own detail into a
        /// flat stripe with a hard top edge; leaving a sixth of the sky showing keeps the painted
        /// sky's banding faintly visible through the haze, which is what haze looks like.
        /// </summary>
        public const float MaxHazeAlpha = 0.85f;

        /// <summary>MOBILE: the switch, read live from settings.ini.</summary>
        public static bool Enabled
        {
            get { return DaggerfallUnity.Settings.SkyHaze; }
        }

        /// <summary>
        /// MOBILE: the Distance fog dial as the haze sees it - the shared 0..2 strength when the
        /// switch is on, and a hard zero when it is off, so "off" and "0%" are the same clean sky
        /// through one code path rather than two.
        /// </summary>
        public static float Strength
        {
            get { return Enabled ? global::DistantTerrain.DistantTerrainPort.FogStrength : 0f; }
        }

        /// <summary>
        /// MOBILE pure: band thickness as a fraction of the way from the horizon to the zenith.
        /// Clamped the way the dial is - a NaN out of a corrupt settings.ini must not become a NaN
        /// shader uniform, which would take the whole sky with it.
        /// </summary>
        public static float BandFraction(float strength)
        {
            if (float.IsNaN(strength))
                strength = 1f;
            return Mathf.Clamp(strength, 0f, 2f) * BandPerStrength;
        }

        /// <summary>MOBILE: the band the skies should be drawing right now.</summary>
        public static float ActiveBand()
        {
            return BandFraction(Strength);
        }

        /// <summary>
        /// MOBILE pure: how much fog colour is mixed in at height <paramref name="t"/> through the
        /// band, where t = 0 is the horizon and t = 1 the top of the band. Squared rather than
        /// linear so the band fades OUT gently and has no visible upper edge; the hard edge is at
        /// the horizon, where the ground hides it. Returns 0 for every t when the band is zero.
        /// </summary>
        public static float HazeAmount(float t, float strength)
        {
            if (BandFraction(strength) <= 0f)
                return 0f;
            float fade = 1f - Mathf.Clamp01(float.IsNaN(t) ? 1f : t);
            return fade * fade * MaxHazeAlpha;
        }

        /// <summary>
        /// MOBILE pure: how many rows off the bottom of a sky image the band covers. Rounded UP so
        /// that any band at all is at least one row - a dial nudged off zero must change something.
        /// </summary>
        public static int BandRows(int imageHeight, float strength)
        {
            if (imageHeight <= 0)
                return 0;
            float band = BandFraction(strength);
            if (band <= 0f)
                return 0;
            return Mathf.Min(imageHeight, Mathf.Max(1, Mathf.CeilToInt(imageHeight * band)));
        }

        /// <summary>
        /// MOBILE pure: the painted sky's horizon colour - the average of the BOTTOM row of the
        /// Color32 array, which is the horizon end of the image (BaseImageFile.GetColor32 writes
        /// Daggerfall's top-down rows bottom-up, so array index 0 is the image's last row, and
        /// DaggerfallSky draws that end at the horizon). Stock DFU uses a single pixel of that row
        /// as the fog colour; one pixel off a dithered 512-wide image is a lottery, and the whole
        /// row costs one pass over 512 bytes per sky frame.
        /// </summary>
        public static Color HorizonColour(Color32[] pixels, int width, int height, Color fallback)
        {
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length < width)
                return fallback;

            float r = 0f, g = 0f, b = 0f;
            for (int x = 0; x < width; x++)
            {
                Color32 c = pixels[x];
                r += c.r; g += c.g; b += c.b;
            }
            float inv = 1f / (255f * width);
            return new Color(r * inv, g * inv, b * inv, 1f);
        }

        /// <summary>
        /// MOBILE: bake the band into a copy of a sky image's pixels and return it - a copy, never
        /// in place, because DaggerfallSky keeps the loaded SkyColors and re-promotes from them when
        /// the dial moves; baking in place would haze an already-hazed image every time. Returns the
        /// original array when there is no band, so the no-haze path allocates nothing.
        /// </summary>
        public static Color32[] BakeHorizonBand(Color32[] pixels, int width, int height, Color horizon, float strength)
        {
            int rows = BandRows(height, strength);
            if (pixels == null || rows <= 0 || width <= 0 || pixels.Length < width * height)
                return pixels;

            Color32[] baked = new Color32[pixels.Length];
            System.Array.Copy(pixels, baked, pixels.Length);

            for (int row = 0; row < rows; row++)
            {
                // t is measured across the BAND, not the image: one dial position, one shape.
                float amount = HazeAmount((float)row / rows, strength);
                if (amount <= 0f)
                    continue;
                int at = row * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 c = baked[at + x];
                    baked[at + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.r, horizon.r * 255f, amount)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.g, horizon.g * 255f, amount)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.b, horizon.b * 255f, amount)),
                        c.a);
                }
            }
            return baked;
        }

        /// <summary>
        /// MOBILE: push the band to whichever sky is up, NOW. The painted sky re-promotes itself
        /// when the band changes (DaggerfallSky.Update watches it), so only the procedural skybox
        /// needs telling - its own push is on a once-a-second tick and a slider must not lag a
        /// second behind the finger. Called from DistantTerrain.ApplyLiveDials, which is where both
        /// the settings panel's row and the test app's `set` command land.
        /// </summary>
        public static void ApplyLiveDial()
        {
            if (global::BLBSkybox.Instance != null)
                global::BLBSkybox.PushHorizonHaze();
        }
    }
}
