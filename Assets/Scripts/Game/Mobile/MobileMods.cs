// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The port's built-in "mods" as entries in Daggerfall Unity's own Mods window. Players look
//   for mods there (device report), so each registers itself with ModManager as a mod with no
//   .dfmod file: it is listed, has a description, and its Enabled checkbox is saved to
//   Mods.json with everyone else's. Mobile Settings > Mods drives the same flags.
//
//   TWO switches since 2026-08-30 (device decision, reversing the earlier combined one):
//   "someone may want the roads but not the travel method". They really are independent -
//   the road NETWORK DATA ships with the code either way, so Real travel's cautious routing
//   follows roads even when they are not drawn, and Roads & tracks without travel is honest
//   scenery for players who keep vanilla fast travel.
//
//   A PlayerPrefs mirror keeps each choice readable before ModManager exists (and in the
//   editor self-test, which has no ModManager at all). When a mod entry is present it is the
//   truth. The roads pref defaults to the travel pref so the old combined switch carries
//   over to both halves on first run after the split.
//
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileMods
    {
        public const string RoadsTitle = "Roads & tracks";
        public const string TravelTitle = "Real travel";

        // The travel pref name predates the (now reversed) combined switch; kept so saved
        // choices carry over. The roads pref is new with the split and inherits from it.
        const string travelPref = "DFMobile.journeymode";
        const string roadsPref = "DFMobile.mod.roads";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            ModManager.OnRegisterBuiltInMods -= Register;
            ModManager.OnRegisterBuiltInMods += Register;
        }

        static void Register(ModManager manager)
        {
            Mod roads = new Mod();
            roads.ModInfo.ModTitle = RoadsTitle;
            roads.ModInfo.ModVersion = "1.0";
            roads.ModInfo.ModAuthor = "Hazelnut (Basic Roads), ported by Codex64ai";
            roads.ModInfo.ContactInfo = "github.com/Codex64ai/daggerfall-unity-ios";
            roads.ModInfo.DFUnity_Version = VersionInfo.DaggerfallUnityVersion;
            roads.ModInfo.GUID = "dfumobile-roads";
            roads.ModInfo.ModDescription =
                "Daggerfall's roads and tracks are drawn on the terrain (Hazelnut's Basic Roads, " +
                "MIT). Works with or without Real travel. Built into this port; no files to " +
                "install. Also switchable in play from Pause > Mobile Settings > Mods.";
            roads.Enabled = Roads;
            manager.RegisterBuiltInMod(roads);

            Mod travel = new Mod();
            travel.ModInfo.ModTitle = TravelTitle;
            travel.ModInfo.ModVersion = "1.0";
            travel.ModInfo.ModAuthor = "Codex64ai, after Jedidia's Tedious Travel";
            travel.ModInfo.ContactInfo = "github.com/Codex64ai/daggerfall-unity-ios";
            travel.ModInfo.DFUnity_Version = VersionInfo.DaggerfallUnityVersion;
            travel.ModInfo.GUID = "dfumobile-real-travel";
            travel.ModInfo.ModDescription =
                "Fast travel becomes a journey: you walk to your destination at a time " +
                "compression you control, can stop anywhere, and cautious travel follows " +
                "Daggerfall's roads and tracks - even when Roads & tracks is off and they are " +
                "not drawn. Built into this port; no files to install. Also switchable in play " +
                "from Pause > Mobile Settings > Mods.";
            travel.Enabled = RealTravel;
            manager.RegisterBuiltInMod(travel);
        }

        static Mod Entry(string title)
        {
            if (ModManager.Instance == null)
                return null;
            try { return ModManager.Instance.GetMod(title); }
            catch (System.Exception) { return null; }
        }

        static bool GetFlag(string title, string pref, int fallback)
        {
            Mod entry = Entry(title);
            if (entry != null)
                return entry.Enabled;
            return PlayerPrefs.GetInt(pref, fallback) == 1;
        }

        static void SetFlag(string title, string pref, bool value)
        {
            Mod entry = Entry(title);
            if (entry != null && entry.Enabled != value)
            {
                entry.Enabled = value;
                ModManager.WriteModSettings();
            }
            PlayerPrefs.SetInt(pref, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Roads and tracks drawn on the terrain. Off by default.</summary>
        public static bool Roads
        {
            // Migration: inherit the combined switch's value the first time.
            get { return GetFlag(RoadsTitle, roadsPref, PlayerPrefs.GetInt(travelPref, 0)); }
            set { SetFlag(RoadsTitle, roadsPref, value); }
        }

        /// <summary>The journey system: walked fast travel, camps, inns. Off by default.</summary>
        public static bool RealTravel
        {
            get { return GetFlag(TravelTitle, travelPref, 0); }
            set
            {
                SetFlag(TravelTitle, travelPref, value);
                MobileJourneyController.JourneyModeEnabled = value;
            }
        }

        /// <summary>
        /// Push the effective choices (the Mods window's, if there is one) into the live flags
        /// and the pref mirrors. Called at game-scene startup; harmless to repeat.
        /// </summary>
        public static void ApplySaved()
        {
            bool travelOn = RealTravel;
            MobileJourneyController.JourneyModeEnabled = travelOn;
            if ((PlayerPrefs.GetInt(travelPref, 0) == 1) != travelOn)
                PlayerPrefs.SetInt(travelPref, travelOn ? 1 : 0);
            bool roadsOn = Roads;
            if ((PlayerPrefs.GetInt(roadsPref, 0) == 1) != roadsOn)
                PlayerPrefs.SetInt(roadsPref, roadsOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
