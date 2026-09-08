// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// The tavern window Climates & Calories registers. Written from the engine's DaggerfallTavernWindow
// (food and drink are already sold there in DFU 1.1) so that the mod's unlicensed TavernWindow.cs
// never ships. Eating at a tavern already satisfies C&C's hunger, because both read the engine's
// LastTimePlayerAteOrDrankAtTavern. This window adds what the engine lacks for C&C: a drink refills
// the mod's water and raises this port's own drunkenness (MobileDrunkenness), which C&C's advice
// text and per-tick call read through the two members below.

using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace DaggerfallWorkshop.Game.Mobile
{
    public class MobileTavernWindow : DaggerfallTavernWindow
    {
        /// <summary>Climates & Calories reads, saves and resets this; it is MobileDrunkenness.Level.</summary>
        public static int drunk
        {
            get { return MobileDrunkenness.Level; }
            set { MobileDrunkenness.Level = value; }
        }

        const float waterPerDrink = 25f;

        public MobileTavernWindow(IUserInterfaceManager uiManager, StaticNPC npc)
            : base(uiManager, npc)
        {
        }

        /// <summary>Called by Climates & Calories once per effect tick.</summary>
        public static void Drunk()
        {
            MobileDrunkenness.Tick(GameManager.Instance.PlayerEntity);
        }

        protected override void FoodAndDrink_OnItemPicked(int index, string foodOrDrinkName)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            uint before = player.LastTimePlayerAteOrDrankAtTavern;
            base.FoodAndDrink_OnItemPicked(index, foodOrDrinkName);
            if (player.LastTimePlayerAteOrDrankAtTavern == before)
                return;     // not enough gold: the engine declined the sale
            int alcohol = MobileDrunkenness.AlcoholFor(index, player.Stats.LiveEndurance);
            if (alcohol > 0)
            {
                MobileDrunkenness.Level += alcohol;
                ClimatesCalories.ClimateCalories.RefillWater(waterPerDrink, true);
            }
        }
    }
}
