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
    /// preference is applied as each DaggerfallUnity starts (see Hook), and a change needs a
    /// restart.
    ///
    /// WHY IT IS OFF BY DEFAULT
    /// It adds work to the most performance-sensitive path in the port. Terrain texturing runs
    /// per tile as the world streams, and streaming is already the thing that struggles when a
    /// journey compresses time. Until that has been measured on a device, roads are opt-in.
    /// </summary>
    public static class MobileRoads
    {
        const string enabledPref = "dfumobile.roads";

        // One texturing instance for the whole process. It holds the path data (four large
        // byte arrays), so it is built once and re-attached to each DaggerfallUnity that comes
        // along rather than rebuilt per scene.
        static BasicRoads.BasicRoadsTexturing texturing;
        static bool hooked;

        /// <summary>
        /// Whether roads are installed on the DaggerfallUnity that is running the game right
        /// now - read from the live object, not from a flag. The old flag stayed true after the
        /// instance it described had been replaced, and the Mods section said "active" over a
        /// world with no roads in it.
        /// </summary>
        public static bool Active
        {
            get
            {
                return DaggerfallUnity.HasInstance &&
                       DaggerfallUnity.Instance.TerrainTexturing is BasicRoads.BasicRoadsTexturing;
            }
        }

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

        /// <summary>
        /// WHY AN EVENT AND NOT A ONE-SHOT INSTALL. This used to assign TerrainTexturing once,
        /// before the first scene loaded. But DaggerfallUnity.Instance at that point is a
        /// throwaway object the getter creates because no scene has one yet - and every
        /// DaggerfallUnity component's Awake() does `instance = null; SetupSingleton()`, so
        /// the game scene's own component takes the singleton over with a fresh
        /// DefaultTerrainTexturing from its field initialiser. The roads were installed on an
        /// object nothing ever looked at, and no road was ever drawn.
        ///
        /// OnSetTerrainSampler is raised from DaggerfallUnity.Start() - it exists so mods can
        /// swap terrain interfaces on the instance that will actually build the world - so
        /// each DaggerfallUnity gets the roads as it comes up, scene swaps included.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            DaggerfallUnity.OnSetTerrainSampler += InstallOnLiveInstance;
        }

        /// <summary>
        /// Put the roads texturing on the DaggerfallUnity that is live now, if the player has
        /// roads on and it is not already there. Public so the self-test can drive the same
        /// path the event does.
        /// </summary>
        public static void InstallOnLiveInstance()
        {
            if (!Enabled || !DaggerfallUnity.HasInstance)
                return;

            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (dfUnity.TerrainTexturing is BasicRoads.BasicRoadsTexturing)
                return;

            if (texturing == null)
            {
                // Deliberately guarded. A missing or short data file makes BasicRoadsTexturing
                // fall back to a blank network, which would cost the extra per-tile work and
                // draw no roads at all - the worst of both. Better to leave the default
                // texturing alone and say why.
                if (!PathDataPresent())
                {
                    Debug.LogWarning("[MobileRoads] path data missing from Resources - " +
                                     "roads not installed, default terrain texturing kept.");
                    return;
                }

                try
                {
                    texturing = new BasicRoads.BasicRoadsTexturing(true, true, null, false);
                }
                catch (System.Exception e)
                {
                    // Terrain texturing is load-bearing: if this throws, the world does not
                    // generate. Falling back to the default is always better than no terrain.
                    Debug.LogError("[MobileRoads] could not build roads texturing, keeping default: " + e);
                    return;
                }
            }

            dfUnity.TerrainTexturing = texturing;
            Debug.Log("[MobileRoads] roads and tracks installed on " + dfUnity.name +
                      " (" + dfUnity.GetInstanceID() + ").");
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
