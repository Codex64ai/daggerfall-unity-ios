// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the launcher's way in to Daggerfall Enemy Expansion. Not upstream code - this file is the
// port's own, written for iOS, and it is the only thing MobilePortedMods knows about DEX.
//
// Why a façade rather than calling BestiaryMod.Init directly:
//
//   * BestiaryMod.Init is unconditional. It builds its GameObject, hangs the save interface off the
//     mod and reads the settings, and only a frame later, in Start, does it find out whether there
//     are any databases to parse. On a device where the dex.dfmod bundle was never installed that
//     leaves a live BestiaryMod with nothing in it, an EnemyBasics.Enemies array rebuilt from
//     nothing, and "started Daggerfall Enemy Expansion" in the log. Available() asks first.
//   * The launcher's four-argument StartOne wants one bool that means "this mod is running".
//     Installed is that bool: Init returned, the mod object exists.
//
// The mod is INERT until MobilePortedMods calls Init: nothing here runs off an attribute, a
// RuntimeInitializeOnLoadMethod or a scene object, and the launcher entry is off by default.
//
// Boot-time only. BestiaryMod resizes the static EnemyBasics.Enemies array and rewrites
// RandomEncounters.EncounterTables, neither of which can be undone in a running session, so the
// switch is read once, here, at the Start state - see MobilePortedMods.DEXNote for what the player
// is told about that and about saves.
//
// Place in Assets/Scripts/Game/Mobile/Ports/DEX/

using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallBestiaryProject
{
    public static class DEXPort
    {
        /// <summary>
        /// The ModTitle DaggerfallBestiaryProject.dfmod.json declares, verbatim. The launcher
        /// resolves the entry by title, so a wrong literal here does not fail - the mod is simply
        /// never there. MobileSelfTest compares it with the fetched manifest.
        /// </summary>
        public const string Title = "Daggerfall Enemy Expansion";

        /// <summary>The log prefix every line the mod writes carries; the launcher's hint.</summary>
        public const string LogPrefix = "[DEX]";

        /// <summary>
        /// The database extensions DEX reads. A bundle without at least one of them has no enemies
        /// in it, whatever else it holds.
        /// </summary>
        public static readonly string[] DatabaseExtensions = { ".mdb.csv", ".cdb.csv", ".tdb.csv" };

        static bool installed;

        /// <summary>True once Init built the mod. What MobilePortedMods' StartOne asks.</summary>
        public static bool Installed { get { return installed; } }

        /// <summary>
        /// True once the databases were parsed (a frame after Init, in BestiaryMod.Start). Not what
        /// the launcher asks - it has returned long before - but what a later reader should.
        /// </summary>
        public static bool Loaded { get { return BestiaryMod.DatabasesLoaded; } }

        /// <summary>
        /// Pure: may the mod run? It needs its bundle (the code alone defines no enemy - every
        /// stat, sprite archive and encounter table is a row in a CSV that ships in dex.dfmod) and
        /// that bundle must actually carry the databases.
        /// </summary>
        public static bool Available(bool bundleFound, bool csvsFound)
        {
            return bundleFound && csvsFound;
        }

        /// <summary>Does this mod's manifest list at least one DEX database?</summary>
        public static bool HasDatabases(Mod mod)
        {
            if (mod == null || mod.ModInfo == null || mod.ModInfo.Files == null)
                return false;
            foreach (string file in mod.ModInfo.Files)
            {
                if (file == null)
                    continue;
                foreach (string ext in DatabaseExtensions)
                    if (file.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Entry point. Called by MobilePortedMods when the launcher entry is on, never by an
        /// [Invoke] loader - there is no mod assembly for DFU to scan.
        /// </summary>
        public static void Init(InitParams initParams)
        {
            if (installed)
                return;

            Mod mod = initParams.Mod;
            if (!Available(mod != null, HasDatabases(mod)))
            {
                Debug.Log(LogPrefix + " not available: " + (mod == null
                    ? "no \"" + Title + "\" mod entry - the dex.dfmod bundle is not installed"
                    : "the bundle lists no .mdb/.cdb/.tdb.csv database"));
                return;
            }

            BestiaryMod.Init(initParams);
            installed = BestiaryMod.Instance != null;
            if (!installed)
                Debug.Log(LogPrefix + " not available: the mod object was not created");
        }
    }
}
