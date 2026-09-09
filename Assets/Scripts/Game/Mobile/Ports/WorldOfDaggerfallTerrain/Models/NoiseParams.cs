// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/NoiseParams.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;

namespace Monobelisk
{
    public interface NoiseParams : IniSerializable
    {
        void ApplyToCS(ComputeShader cs);
        void ApplyToMaterial(Material mat);
    }
}