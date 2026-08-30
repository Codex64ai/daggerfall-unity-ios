// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The port's built-in "mods" as entries in Daggerfall Unity's own Mods window. Players look
//   for mods there (device report), so Roads & real travel registers itself with ModManager as
//   a mod with no .dfmod file: it is listed, has a description, and its Enabled checkbox is
//   saved to Mods.json with everyone else's. Mobile Settings > Mods drives the same flag.
//
//   Roads & real travel is one experience behind one switch (device decision): real travel
//   walks the road network, so roads without travel are scenery and travel without roads is a
//   straight line.
//
//   A PlayerPrefs mirror keeps the choice readable before ModManager exists (and in the editor
//   self-test, which has no ModManager at all). When the mod entry is present it is the truth.
//
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileMods
    {
        public const string RoadsAndTravelTitle = "Roads & real travel";

        // The travel half's pref name predates the combined switch; kept so saved choices carry over.
        const string travelPref = "DFMobile.journeymode";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            ModManager.OnRegisterBuiltInMods -= Register;
            ModManager.OnRegisterBuiltInMods += Register;
        }

        static void Register(ModManager manager)
        {
            Mod mod = new Mod();
            mod.ModInfo.ModTitle = RoadsAndTravelTitle;
            mod.ModInfo.ModVersion = "1.0";
            mod.ModInfo.ModAuthor = "Codex64ai - roads by Hazelnut (Basic Roads), travel after Jedidia (Tedious Travel)";
            mod.ModInfo.ContactInfo = "github.com/Codex64ai/daggerfall-unity-ios";
            mod.ModInfo.DFUnity_Version = VersionInfo.DaggerfallUnityVersion;
            mod.ModInfo.GUID = "dfumobile-roads-real-travel";
            mod.ModInfo.ModDescription =
                "Fast travel becomes a journey: you walk to your destination at a time compression " +
                "you control, can stop anywhere, and cautious travel follows Daggerfall's roads and " +
                "tracks, which are drawn on the terrain. Built into this port; no files to install. " +
                "Also switchable in play from Pause > Mobile Settings > Mods.";

            // Start from the saved choice; Mods.json, applied right after registration, wins if
            // the player has since changed it in this window.
            mod.Enabled = PlayerPrefs.GetInt(travelPref, 0) == 1;
            manager.RegisterBuiltInMod(mod);
        }

        static Mod Entry
        {
            get
            {
                if (ModManager.Instance == null)
                    return null;
                try { return ModManager.Instance.GetMod(RoadsAndTravelTitle); }
                catch (System.Exception) { return null; }
            }
        }

        /// <summary>Roads drawn on the terrain and fast travel walked along them. Off by default.</summary>
        public static bool RoadsAndTravel
        {
            get
            {
                Mod entry = Entry;
                if (entry != null)
                    return entry.Enabled;
                return PlayerPrefs.GetInt(travelPref, 0) == 1;
            }
            set
            {
                Mod entry = Entry;
                if (entry != null && entry.Enabled != value)
                {
                    entry.Enabled = value;
                    ModManager.WriteModSettings();
                }
                PlayerPrefs.SetInt(travelPref, value ? 1 : 0);
                PlayerPrefs.Save();
                MobileJourneyController.JourneyModeEnabled = value;
            }
        }

        /// <summary>
        /// Push the effective choice (the Mods window's, if there is one) into the live flags and
        /// the pref mirror. Called at game-scene startup; harmless to repeat.
        /// </summary>
        public static void ApplySaved()
        {
            bool on = RoadsAndTravel;
            MobileJourneyController.JourneyModeEnabled = on;
            if ((PlayerPrefs.GetInt(travelPref, 0) == 1) != on)
            {
                PlayerPrefs.SetInt(travelPref, on ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
