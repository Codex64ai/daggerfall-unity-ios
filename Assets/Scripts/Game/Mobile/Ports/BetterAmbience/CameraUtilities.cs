// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/CameraShake/CameraUtilities.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;

namespace SpellcastStudios.CameraShake
{
    ///ORIGINAL SOURCE: https://github.com/andersonaddo/EZ-Camera-Shake-Unity/
    
    public class CameraUtilities
    {
        /// <summary>
        /// Smoothes a Vector3 that represents euler angles.
        /// </summary>
        /// <param name="current">The current Vector3 value.</param>
        /// <param name="target">The target Vector3 value.</param>
        /// <param name="velocity">A refernce Vector3 used internally.</param>
        /// <param name="smoothTime">The time to smooth, in seconds.</param>
        /// <returns>The smoothed Vector3 value.</returns>
        public static Vector3 SmoothDampEuler(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
        {
            Vector3 v;

            v.x = Mathf.SmoothDampAngle(current.x, target.x, ref velocity.x, smoothTime);
            v.y = Mathf.SmoothDampAngle(current.y, target.y, ref velocity.y, smoothTime);
            v.z = Mathf.SmoothDampAngle(current.z, target.z, ref velocity.z, smoothTime);

            return v;
        }

        /// <summary>
        /// Multiplies each element in Vector3 v by the corresponding element of w.
        /// </summary>
        public static Vector3 MultiplyVectors(Vector3 v, Vector3 w)
        {
            v.x *= w.x;
            v.y *= w.y;
            v.z *= w.z;

            return v;
        }

    }
}