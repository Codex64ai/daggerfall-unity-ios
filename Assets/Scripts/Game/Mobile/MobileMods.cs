// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   The port's built-in "mods" as switches with one owner each. Roads & real travel is one
//   experience behind one switch (device decision): real travel walks the road network, so
//   roads without travel are scenery and travel without roads is a straight line.
//
//   Two places show the switch - the launcher's options page (where players look for mods,
//   and where a change lands before the game scene loads, so roads need no restart) and
//   Mobile Settings > Mods. Both read and write through here so they cannot disagree.
//
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileMods
    {
        // The travel half's pref name predates the combined switch; kept so saved choices carry over.
        const string travelPref = "DFMobile.journeymode";

        /// <summary>Roads drawn on the terrain and fast travel walked along them. Off by default.</summary>
        public static bool RoadsAndTravel
        {
            get { return PlayerPrefs.GetInt(travelPref, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(travelPref, value ? 1 : 0);
                PlayerPrefs.Save();
                MobileRoads.Enabled = value;
                MobileJourneyController.JourneyModeEnabled = value;
            }
        }

        /// <summary>
        /// Push the saved preference into the live flags. Called at startup by whoever runs
        /// first; harmless to repeat.
        /// </summary>
        public static void ApplySaved()
        {
            bool on = RoadsAndTravel;
            MobileJourneyController.JourneyModeEnabled = on;
            if (MobileRoads.Enabled != on)
                MobileRoads.Enabled = on;
        }
    }
}
