// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// The tavern window Climates & Calories registers. Written from the engine's DaggerfallTavernWindow
// (food and drink are already sold there in DFU 1.1) so that the mod's unlicensed TavernWindow.cs
// never ships. Eating at a tavern already satisfies C&C's hunger, because both read the engine's
// LastTimePlayerAteOrDrankAtTavern. This window adds what the engine lacks for C&C: a drink refills
// the mod's water and raises a small drunkenness value that the mod's advice text and its per-tick
// call to Drunk() read. The drunkenness model here is this port's own, deliberately mild.

using System;
using UnityEngine;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileTavernWindow : DaggerfallTavernWindow
    {
        /// <summary>0 = sober. Compared by Climates & Calories against the player's endurance.</summary>
        public static int drunk;

        // Engine menu order: ale, beer, mead, wine, then seven foods (DaggerfallTavernWindow.tavernMenu).
        static readonly int[] alcoholByMenuIndex = { 8, 10, 15, 20 };
        const float waterPerDrink = 25f;

        public MobileTavernWindow(IUserInterfaceManager uiManager, StaticNPC npc)
            : base(uiManager, npc)
        {
        }

        /// <summary>Called by Climates & Calories once per effect tick: sobering up, and the cost of excess.</summary>
        public static void Drunk()
        {
            if (drunk <= 0) { drunk = 0; return; }
            drunk = Math.Max(0, drunk - 2);
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (player == null) return;
            int endurance = player.Stats.LiveEndurance;
            if (drunk > endurance)
            {
                // Past what the body can take: fatigue drains and the room turns.
                player.DecreaseFatigue(2, true);
                if (UnityEngine.Random.Range(0, 4) == 0)
                    DaggerfallUI.AddHUDText("The room will not stay still.", 2f);
            }
        }

        /// <summary>Pure: what one drink from menu row <paramref name="index"/> adds to drunkenness (0 for food).</summary>
        public static int AlcoholFor(int index)
        {
            return index >= 0 && index < alcoholByMenuIndex.Length ? alcoholByMenuIndex[index] : 0;
        }

        protected override void FoodAndDrink_OnItemPicked(int index, string foodOrDrinkName)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            uint before = player.LastTimePlayerAteOrDrankAtTavern;
            base.FoodAndDrink_OnItemPicked(index, foodOrDrinkName);
            if (player.LastTimePlayerAteOrDrankAtTavern == before)
                return;     // not enough gold: the engine declined the sale
            int alcohol = AlcoholFor(index);
            if (alcohol > 0)
            {
                drunk += alcohol;
                ClimatesCalories.ClimateCalories.RefillWater(waterPerDrink, true);
            }
        }
    }
}
