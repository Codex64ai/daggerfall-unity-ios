// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationObjectExtraData.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
namespace LocationLoader
{
    public struct EnemyMarkerExtraData
    {
        public int EnemyId;
        public int TeamOverride;

        public static string DefaultData = "{\"EnemyId\": 0, \"TeamOverride\": 0}";
    };
}
