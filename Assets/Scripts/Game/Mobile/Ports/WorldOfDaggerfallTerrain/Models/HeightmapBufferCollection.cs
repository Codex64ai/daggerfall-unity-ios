// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/HeightmapBufferCollection.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;

namespace Monobelisk
{
    public struct HeightmapBufferCollection
    {
        public ComputeBuffer heightmapBuffer;
        public ComputeBuffer rawNoise;
        public ComputeBuffer tilemapData;

        public ComputeBuffer shm;
        public ComputeBuffer lhm;

        public void ApplyToCS(ComputeShader cs, int kernel)
        {
            cs.SetBuffer(kernel, "heightmapBuffer", heightmapBuffer);
            cs.SetBuffer(kernel, "rawNoise", rawNoise);
            cs.SetBuffer(kernel, "tilemapData", tilemapData);
        }

        /// <summary>
        /// MOBILE: (g) null-safe and idempotent. Upstream released all five unconditionally, so a throw
        /// before HandleBaseMapSampleParams ran (shm/lhm still null) turned one failure into a
        /// NullReferenceException inside the cleanup path, and the other three buffers leaked. Now the
        /// sampler's finally can call this unconditionally, whether or not the readback already did.
        /// </summary>
        public void Dispose()
        {
            Release(ref heightmapBuffer);
            Release(ref rawNoise);
            Release(ref tilemapData);
            Release(ref shm);
            Release(ref lhm);
        }

        // MOBILE: (g)
        private static void Release(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer.Dispose();
            buffer = null;
        }
    }
}
