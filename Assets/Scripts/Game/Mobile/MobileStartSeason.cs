// Project:         Daggerfall Unity iOS touch port
// License:         MIT License (LICENSE file)
//
// Starts a NEW character in summer instead of the canonical winter date.

using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// The opt-in "Summer start" built-in mod.
    ///
    /// WHAT IT DOES
    /// Daggerfall begins every new game at 13:30 on the 4th of Morning Star, 3E405 - the
    /// morning after the shipwreck. Morning Star is month 0, and DaggerfallDateTime treats
    /// months 11, 0 and 1 as Winter, so a brand-new character spends roughly two in-game
    /// months in snow before the world thaws. On this port that is the player's whole first
    /// impression: someone installs several gigabytes of HD texture mods, launches, and looks
    /// out at a grey snowfield. When this switch is on, a new character starts on the 4th of
    /// Midyear instead.
    ///
    /// WHY THE 4th OF MIDYEAR
    /// Only the MONTH moves - 0 (Morning Star) to 5 (Midyear). The day of the month (4th),
    /// the year (3E405) and the time of day (13:30) are all left exactly as classic sets
    /// them. Midyear is the middle of the three Summer months (5, 6, 7), so the player gets
    /// the longest possible run of summer before autumn, and it is far enough from the
    /// season boundaries that a few days of in-game travel cannot tip it back into spring.
    /// Keeping 13:30 matters more than it looks: the start time decides where the sun is,
    /// whether shops and guilds are open, and whether the first NPCs are out on the street.
    /// Shifting it would change far more than the weather.
    ///
    /// WHY IT IS OFF BY DEFAULT
    /// The winter start is lore - it is the date of the shipwreck that opens the main quest,
    /// and it is what a returning player expects to see. Vanilla behaviour is untouched
    /// unless the player asks for something else.
    ///
    /// WHY IT IS SAFE
    /// Nothing in the game is anchored to the absolute start date. Quest deadlines are set
    /// from the CURRENT world time when the quest is offered, the main quest is triggered by
    /// the player entering the tavern rather than by a calendar date, and the character's
    /// birth date and age are derived from the birth sign chosen at creation, not from the
    /// clock. Only the season, the weather and the moon phases move with the month.
    /// </summary>
    public static class MobileStartSeason
    {
        /// <summary>Midyear - the middle Summer month. See the note on the class.</summary>
        public const int SummerMonth = (int)DaggerfallDateTime.Months.Midyear;

        /// <summary>
        /// Player preference, owned by MobileMods (the Mods window entry when there is one, a
        /// pref mirror before then). Read once, when a new character is created.
        /// </summary>
        public static bool Enabled
        {
            get { return MobileMods.SummerStart; }
        }

        /// <summary>
        /// Sets the start date for a NEW character. Called instead of
        /// SetClassicGameStartTime() from the new-game path only - loading a save never comes
        /// through here, so an existing character keeps whatever date it was saved with.
        /// </summary>
        public static void ApplyNewGameStartTime(DaggerfallDateTime now)
        {
            ApplyNewGameStartTime(now, Enabled);
        }

        /// <summary>
        /// The decision on its own, with the preference passed in so it can be tested without
        /// PlayerPrefs or a ModManager. Always lays down the classic start time first, so the
        /// off case is byte-for-byte the vanilla date and only the month is ever touched.
        /// </summary>
        public static void ApplyNewGameStartTime(DaggerfallDateTime now, bool summer)
        {
            if (now == null)
                return;

            now.SetClassicGameStartTime();

            if (summer)
                now.Month = SummerMonth;
        }
    }
}
