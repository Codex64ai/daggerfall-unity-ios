// Project:         Daggerfall Unity iOS touch port
// License:         MIT License (LICENSE file)
//
// Turns on the ported Basic Roads terrain texturing.
//
// Basic Roads is Copyright (C) 2020 Hazelnut, MIT licensed - see BasicRoadsTexturing.cs.
// Hazelnut also confirmed directly to the port author that this use is welcome.

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Installs road and track texturing over Daggerfall's default terrain painting.
    ///
    /// WHY THIS IS A STARTUP DECISION AND NOT A LIVE TOGGLE
    /// DaggerfallUnity.TerrainTexturing is consulted while a terrain tile is being built, so
    /// switching it mid-session leaves every already-generated tile painted the old way. The
    /// result is a world where roads stop at an invisible line - worse than having none. So the
    /// preference is read once, before the first scene loads, and a change needs a restart.
    ///
    /// WHY IT IS OFF BY DEFAULT
    /// It adds work to the most performance-sensitive path in the port. Terrain texturing runs
    /// per tile as the world streams, and streaming is already the thing that struggles when a
    /// journey compresses time. Until that has been measured on a device, roads are opt-in.
    /// </summary>
    public static class MobileRoads
    {
        const string enabledPref = "dfumobile.roads";

        /// <summary>Whether roads were installed for this session.</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Player preference. Setting it does NOT affect the running session - see the note on
        /// the class about partially painted worlds.
        /// </summary>
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(enabledPref, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(enabledPref, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True when the preference no longer matches what is installed.</summary>
        public static bool RestartRequired { get { return Enabled != Active; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Enabled)
                return;

            // Deliberately guarded. A missing or short data file makes BasicRoadsTexturing fall
            // back to a blank network, which would cost the extra per-tile work and draw no
            // roads at all - the worst of both. Better to leave the default texturing alone and
            // say why.
            if (!PathDataPresent())
            {
                Debug.LogWarning("[MobileRoads] path data missing from Resources - " +
                                 "roads not installed, default terrain texturing kept.");
                return;
            }

            try
            {
                DaggerfallUnity.Instance.TerrainTexturing =
                    new BasicRoads.BasicRoadsTexturing(true, true, null, false);
                Active = true;
                Debug.Log("[MobileRoads] roads and tracks installed over default texturing.");
            }
            catch (System.Exception e)
            {
                // Terrain texturing is load-bearing: if this throws, the world does not
                // generate. Falling back to the default is always better than no terrain.
                Debug.LogError("[MobileRoads] install failed, keeping default texturing: " + e);
                Active = false;
            }
        }

        static bool PathDataPresent()
        {
            string[] names = { "roadData", "trackData", "riverData", "streamData" };
            foreach (string name in names)
            {
                TextAsset asset = Resources.Load<TextAsset>(
                    BasicRoads.BasicRoadsTexturing.ResourceFolder + name);
                if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                    return false;
            }
            return true;
        }
    }
}
