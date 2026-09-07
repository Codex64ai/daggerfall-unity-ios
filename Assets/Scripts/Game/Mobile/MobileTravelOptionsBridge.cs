// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Climates & Calories talks to Hazelnut's Travel Options mod through ModManager messages. On iOS
// the travel system is this port's Real travel, so a built-in mod entry titled "TravelOptions"
// answers those messages from the journey controller's state. Six messages are known (counted in
// the C&C source); anything else is ignored without a callback, which is what a missing mod would do.

using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>What the bridge needs to know about the journey; the real one wraps the pilot and controller.</summary>
    public interface IJourneyState
    {
        bool Active { get; }
        bool FollowingRoad { get; }
        void Pause();
        void Hud(string text);
    }

    public static class MobileTravelOptionsBridge
    {
        public const string Title = "TravelOptions";

        /// <summary>Handles one message. Returns true when the message was recognised.</summary>
        public static bool Handle(string message, object data, DFModMessageCallback callback, IJourneyState journey)
        {
            switch (message)
            {
                case "isTravelActive":
                    if (callback != null) callback(message, journey.Active);
                    return true;
                case "pauseTravel":
                    if (journey.Active) journey.Pause();
                    return true;
                case "noStopForUIWindow":
                    // Real travel already keeps walking under the mod's own message boxes; nothing to record.
                    return true;
                case "showMessage":
                    if (data is string && !string.IsNullOrEmpty((string)data)) journey.Hud((string)data);
                    return true;
                case "isPathFollowing":
                case "isFollowingRoad":
                    if (callback != null) callback(message, journey.Active && journey.FollowingRoad);
                    return true;
            }
            return false;
        }

        class LiveJourney : IJourneyState
        {
            public bool Active { get { return MobileJourneyPilot.Active; } }
            public bool FollowingRoad { get { return MobileJourneyController.Instance != null && MobileJourneyController.Instance.FollowingRoad; } }
            public void Pause()
            {
                if (MobileJourneyController.Instance != null)
                    MobileJourneyController.Instance.Stop(MobileJourneyController.JourneyEnd.Interrupted);
            }
            public void Hud(string text) { DaggerfallUI.AddHUDText(text, 3f); }
        }

        static readonly LiveJourney live = new LiveJourney();

        /// <summary>The receiver installed on the built-in TravelOptions entry.</summary>
        public static void Receive(string message, object data, DFModMessageCallback callback)
        {
            Handle(message, data, callback, live);
        }
    }
}
