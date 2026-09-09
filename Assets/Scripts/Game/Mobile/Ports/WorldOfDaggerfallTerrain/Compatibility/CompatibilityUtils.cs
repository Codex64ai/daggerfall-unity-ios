// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Compatibility/CompatibilityUtils.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System.Collections;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Linq;

namespace Monobelisk.Compatibility
{
    public static class CompatibilityUtils
    {
        internal const string BASIC_ROADS = "BasicRoads";

        private static string[] _loadedMods;
        private static string[] LoadedMods
        {
            get
            {
                if (_loadedMods == null)
                {
                    _loadedMods = ModManager.Instance.GetAllModTitles();
                }

                return _loadedMods;
            }
        }

        public static bool BasicRoadsLoaded =>
            LoadedMods.Contains(BASIC_ROADS);
    }
}