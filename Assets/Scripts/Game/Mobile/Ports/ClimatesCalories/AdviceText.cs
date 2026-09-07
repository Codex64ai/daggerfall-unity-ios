// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File AdviceText.cs, copied unchanged for iOS except lines marked MOBILE.
// MOBILE: DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk / DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.Drunk() now resolve to MobileTavernWindow (the port's own).
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace ClimatesCalories
{
    public class AdviceText
    {
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static RaceTemplate playerRace = playerEntity.RaceTemplate;

        static private int wetCount = ClimateCalories.wetCount;
        static private int baseNatTemp = Climates.baseNatTemp;
        static private int natTemp = Climates.natTemp;
        static private int armorTemp = Climates.armorTemp;
        static private int pureClothTemp = Climates.pureClothTemp;
        static private int totalTemp = Climates.totalTemp;
        static private bool cloak = Climates.cloak;
        static private bool hood = Climates.hood;
        static private bool drink = Climates.gotDrink;
        static private uint hunger = Hunger.hunger;
        static private bool starving = Hunger.starving;
        static private bool rations = Hunger.rations;
        static private int sleepyCounter = Sleep.sleepyCounter;
        static private int awakeOrAsleepHours = (int)Sleep.awakeOrAsleepHours;

        static public bool statusClosed = true;


        public static void AdviceBuilder(bool encumbranceRPR)
        {
            DaggerfallMessageBox msgBox = DaggerfallUI.UIManager.TopWindow as DaggerfallMessageBox;
            if (msgBox != null && msgBox.ExtraProceedBinding == InputManager.Instance.GetBinding(InputManager.Actions.Status))
            {
                // Setup next status info box.
                DaggerfallMessageBox newBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, msgBox);
                List<string> messages = new List<string>();
                messages.Add(TxtClimate());
                if (!string.IsNullOrEmpty(TxtAdvice()))
                {
                    messages.Add(TxtAdvice());
                }
                messages.Add(string.Empty);
                if (encumbranceRPR)
                {
                    messages.Add(TxtEncumbrance());
                    if (!string.IsNullOrEmpty(TxtEncAdvice()))
                    {
                        messages.Add(TxtEncAdvice());
                    }
                    messages.Add(string.Empty);
                }
                if (!ClimateCalories.isVampire)
                {
                    messages.Add(TxtWater());
                    messages.Add(TxtFood());
                    messages.Add(string.Empty);
                    messages.Add(TxtSleep());
                }
                else
                {
                    messages.Add("You have no need for food or sleep.");
                }
                if (DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk > playerEntity.Stats.LiveEndurance / 2)
                {
                    messages.Add(string.Empty);
                    if (DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk > playerEntity.Stats.LiveEndurance - 10)
                    {
                        messages.Add("You are very drunk.");
                    }
                    else
                    {
                        messages.Add("You are drunk.");
                    }
                }
                
                newBox.SetText(messages.ToArray());

                newBox.ExtraProceedBinding = InputManager.Instance.GetBinding(InputManager.Actions.Status); // set proceed binding
                newBox.ClickAnywhereToClose = true;
                msgBox.AddNextMessageBox(newBox);
                statusClosed = false;
            }
        }

        public static void AdviceDataUpdate()
        {
            wetCount = ClimateCalories.wetCount;
            baseNatTemp = Climates.baseNatTemp;
            natTemp = Climates.natTemp;
            armorTemp = Climates.armorTemp;
            pureClothTemp = Climates.pureClothTemp;
            totalTemp = Climates.totalTemp;
            cloak = Climates.cloak;
            hood = Climates.hood;
            drink = Climates.gotDrink;
            hunger = Hunger.hunger;
            starving = Hunger.starving;
            rations = Hunger.RationsToEat();
            sleepyCounter = Sleep.sleepyCounter;
        }

        public static string TxtClimate()
        {
            string temperatureTxt = "mild ";
            string weatherTxt = "";
            string seasonTxt = " summer";
            string timeTxt = " in ";
            string climateTxt = "";
            string suitabilityTxt = " is suitable for you.";

            int climate = playerGPS.CurrentClimateIndex;

            bool isRaining = GameManager.Instance.WeatherManager.IsRaining;
            bool isOvercast = GameManager.Instance.WeatherManager.IsOvercast;
            bool isStorming = GameManager.Instance.WeatherManager.IsStorming;
            bool isSnowing = GameManager.Instance.WeatherManager.IsSnowing;

            if (baseNatTemp >= 10)
            {
                if (baseNatTemp >= 50)
                {
                    temperatureTxt = "scorching";
                }
                else if (baseNatTemp >= 30)
                {
                    temperatureTxt = "hot";
                }
                else
                {
                    temperatureTxt = "warm";
                }
            }
            else if (baseNatTemp <= -10)
            {
                if (baseNatTemp <= -50)
                {
                    temperatureTxt = "freezing";
                }
                else if (baseNatTemp <= -30)
                {
                    temperatureTxt = "cold";
                }
                else
                {
                    temperatureTxt = "cool";
                }
            }
            if (!GameManager.Instance.IsPlayerInsideDungeon)
            {
                if (isRaining)
                {
                    weatherTxt = " and rainy";
                }
                else if (isStorming)
                {
                    weatherTxt = " and stormy";
                }
                else if (isOvercast)
                {
                    weatherTxt = " and foggy";
                }
                else if (isSnowing)
                {
                    weatherTxt = " and snowy";
                }
                else if (playerEnterExit.IsPlayerInSunlight)
                {
                    weatherTxt = " and sunny";
                }
            }

            switch (DaggerfallUnity.Instance.WorldTime.Now.SeasonValue)
            {
                //Spring
                case DaggerfallDateTime.Seasons.Fall:
                    seasonTxt = " fall";
                    break;
                case DaggerfallDateTime.Seasons.Spring:
                    seasonTxt = " spring";
                    break;
                case DaggerfallDateTime.Seasons.Winter:
                    seasonTxt = " winter";
                    break;
            }

            if (!GameManager.Instance.IsPlayerInsideDungeon)
            {
                int clock = DaggerfallUnity.Instance.WorldTime.Now.Hour;

                if (clock >= 4 && clock <= 7)
                {
                    timeTxt = " morning in ";
                }
                else if (clock >= 16 && clock <= 19)
                {
                    timeTxt = " evening in ";
                }
                else if (DaggerfallUnity.Instance.WorldTime.Now.IsNight)
                {
                    timeTxt = " night in ";
                }
                else
                {
                    timeTxt = " day in ";
                }
            }

            if (GameManager.Instance.IsPlayerInsideDungeon)
            {
                switch (climate)
                {
                    case (int)MapsFile.Climates.Desert2:
                    case (int)MapsFile.Climates.Desert:
                        climateTxt = "desert dungeon";
                        break;
                    case (int)MapsFile.Climates.Rainforest:
                    case (int)MapsFile.Climates.Subtropical:
                        climateTxt = "tropical dungeon";
                        break;
                    case (int)MapsFile.Climates.Swamp:
                        climateTxt = "swampy dungeon";
                        break;
                    case (int)MapsFile.Climates.Woodlands:
                    case (int)MapsFile.Climates.HauntedWoodlands:
                        climateTxt = "woodlands dungeon";
                        break;
                    case (int)MapsFile.Climates.MountainWoods:
                    case (int)MapsFile.Climates.Mountain:
                        climateTxt = "mountain dungeon";
                        break;
                }
            }
            else
            {
                switch (climate)
                {
                    case (int)MapsFile.Climates.Desert2:
                    case (int)MapsFile.Climates.Desert:
                        climateTxt = "the desert";
                        break;
                    case (int)MapsFile.Climates.Rainforest:
                    case (int)MapsFile.Climates.Subtropical:
                        climateTxt = "the tropics";
                        break;
                    case (int)MapsFile.Climates.Swamp:
                        climateTxt = "the swamps";
                        break;
                    case (int)MapsFile.Climates.Woodlands:
                    case (int)MapsFile.Climates.HauntedWoodlands:
                        climateTxt = "the woodlands";
                        break;
                    case (int)MapsFile.Climates.MountainWoods:
                    case (int)MapsFile.Climates.Mountain:
                        climateTxt = "the mountains";
                        break;
                }
            }


            if (ClimateCalories.isVampire && playerEnterExit.IsPlayerInSunlight)
            {
                if (natTemp > 0 && DaggerfallUnity.Instance.WorldTime.Now.IsDay && !hood)
                {
                    suitabilityTxt = " will burn you!";
                }
            }
            else if (natTemp < -60 || baseNatTemp > 50)
            {
                suitabilityTxt = " will be the death of you.";
            }
            else if (natTemp < -40 || baseNatTemp > 30)
            {
                suitabilityTxt = " will wear you down.";
            }
            else if (natTemp < -20)
            {
                suitabilityTxt = " makes you shiver.";
            }
            else if (natTemp > 10)
            {
                suitabilityTxt = " makes you sweat.";
            }

            if (GameManager.Instance.IsPlayerInsideDungeon)
            {
                return "The " + temperatureTxt.ToString() + " air in this " + climateTxt.ToString() + suitabilityTxt.ToString();
            }
            else
            {
                return "It is a " + temperatureTxt.ToString() + weatherTxt.ToString() + seasonTxt.ToString() + timeTxt.ToString() + climateTxt.ToString() + ".";
            }
        }

        public static string TxtClothing()
        {
            string clothTxt = "The way you are dressed provides no warmth";
            string wetTxt = ". ";
            string armorTxt = "";


            if (wetCount > 10)
            {
                if (wetCount > 200) { wetTxt = " and you are completely drenched."; }
                else if (wetCount > 100) { wetTxt = " and you are soaking wet."; }
                else if (wetCount > 50) { wetTxt = " and you are quite wet."; }
                else if (wetCount > 20) { wetTxt = " and you are somewhat wet."; }
                else { wetTxt = " and you are a bit wet."; }
            }

            if (pureClothTemp > 40)
            {
                clothTxt = "You are very warmly dressed";
                if (wetCount > 39)
                {
                    wetTxt = " but your clothes are soaked.";
                }
                else if (wetCount > 20)
                {
                    wetTxt = " but your clothes are damp.";
                }
            }
            else if (pureClothTemp > 20)
            {
                clothTxt = "You are warmly dressed";
                if (wetCount > 19)
                {
                    wetTxt = " but your clothes are soaked.";
                }
                else if (wetCount > 10)
                {
                    wetTxt = " but your clothes are damp.";
                }
            }
            else if (pureClothTemp > 10)
            {
                clothTxt = "You are moderately dressed";
                if (wetCount > 9)
                {
                    wetTxt = " but your clothes are soaked.";
                }
                else if (wetCount > 5)
                {
                    wetTxt = " but your clothes are damp.";
                }
            }
            else if (pureClothTemp > 5)
            {
                clothTxt = "You are lightly dressed";
                if (wetCount > 4)
                {
                    wetTxt = " and your clothes are wet.";
                }
                else if (wetCount > 2)
                {
                    wetTxt = " and your clothes are damp.";
                }
            }




            if (armorTemp > 20)
            {
                armorTxt = " Your armor is scorchingly hot.";
            }
            else if (armorTemp > 15)
            {
                armorTxt = " Your armor is very hot.";
            }
            else if (armorTemp > 11)
            {
                armorTxt = " Your armor is hot.";
            }
            else if (armorTemp > 5)
            {
                armorTxt = " Your armor is warm.";
            }
            else if (armorTemp > 0)
            {
                armorTxt = " Your armor is a bit stuffy.";
            }
            else if (armorTemp < -5)
            {
                armorTxt = " The metal of your armor is cold.";
            }
            else if (armorTemp < 0)
            {
                armorTxt = " The metal of your armor is cool.";
            }
            return clothTxt.ToString() + wetTxt.ToString() + armorTxt.ToString();
        }

        public static string TxtAdvice()
        {
            bool isDungeon = GameManager.Instance.IsPlayerInsideDungeon;
            bool isRaining = GameManager.Instance.WeatherManager.IsRaining;
            bool isStorming = GameManager.Instance.WeatherManager.IsStorming;
            bool isSnowing = GameManager.Instance.WeatherManager.IsSnowing;
            bool isWeather = isRaining || isStorming || isSnowing;
            bool isNight = DaggerfallUnity.Instance.WorldTime.Now.IsNight;
            bool isDesert = playerGPS.CurrentClimateIndex == (int)MapsFile.Climates.Desert || playerGPS.CurrentClimateIndex == (int)MapsFile.Climates.Desert2 || playerGPS.CurrentClimateIndex == (int)MapsFile.Climates.Subtropical;
            bool isMountain = playerGPS.CurrentClimateIndex == (int)MapsFile.Climates.Mountain || playerGPS.CurrentClimateIndex == (int)MapsFile.Climates.MountainWoods;
            DaggerfallUnityItem cloak1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak1);
            DaggerfallUnityItem cloak2 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak2);
            DaggerfallUnityItem shoes = playerEntity.ItemEquipTable.GetItem(EquipSlots.Feet);

            string adviceTxt = "";

            if (playerEntity.IsInBeastForm)
                return adviceTxt;

            if (totalTemp < -10)
            {
                if (!cloak && isWeather && !isDungeon)
                {
                    adviceTxt = "A cloak would protect you from getting wet.";
                }
                else if ((isRaining || isStorming || isSnowing) && !hood && !isDungeon)
                {
                    adviceTxt = "Your head and neck is getting soaked.";
                }
                else if (wetCount > 19)
                {
                    if (isDungeon)
                    {
                        adviceTxt = "Find a fire or make camp to help you get dry.";
                    }
                    else
                    {
                        adviceTxt = "Find a tavern or make camp to get dry.";
                    }
                }
                else if (pureClothTemp < 30)
                {
                    adviceTxt = "It is important to dress warm enough.";

                    if (cloak1 != null)
                    {
                        switch (cloak1.TemplateIndex)
                        {
                            case (int)MensClothing.Casual_cloak:
                            case (int)WomensClothing.Casual_cloak:
                                adviceTxt = "Your casual cloak offers little warmth.";
                                break;
                        }
                        if (cloak2 == null)
                        {
                            adviceTxt = "You should wear thicker clothes or a second cloak.";
                        }
                    }
                    if (cloak2 != null)
                    {
                        switch (cloak2.TemplateIndex)
                        {
                            case (int)MensClothing.Casual_cloak:
                            case (int)WomensClothing.Casual_cloak:
                                adviceTxt = "Your casual cloak offers little warmth.";
                                break;
                        }
                        if (cloak1 == null)
                        {
                            adviceTxt = "You should wear thicker clothes or a second cloak.";
                        }
                    }
                }
                else if (armorTemp < 0)
                {
                    adviceTxt = "The metal of your armor has gone icy cold.";
                }
                else if (isNight && !isDungeon)
                {
                    adviceTxt = "Most adventurers know the dangers of traveling at night.";
                }
                else if (isDesert && isNight && !isDungeon)
                {
                    adviceTxt = "The desert nights might be preferable to this heat.";
                }
                else if (shoes == null)
                {
                    adviceTxt = "Any footwear would be preferrable to leaving your feet bare.";
                }
                else if (Hunger.hungry)
                {
                    adviceTxt = "Eating some fresh food would keep your strength up.";
                }
                else if (isMountain && DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
                {
                    adviceTxt = "Crossing mountains in the winter is only for the most hardy.";
                }                
            }
            else if (totalTemp > 10)
            {
                if (armorTemp > 11 && playerEnterExit.IsPlayerInSunlight && !Climates.ArmorCovered())
                {
                    adviceTxt = "The sun is heating up your uncovered armor.";
                }
                else if (!cloak && baseNatTemp > 30 && playerEnterExit.IsPlayerInSunlight)
                {
                    adviceTxt = "The people of the deserts dress lightly and cover up.";
                }
                else if (cloak && !hood && baseNatTemp > 30 && playerEnterExit.IsPlayerInSunlight)
                {
                    adviceTxt = "The hood of your cloak will protect your head.";
                }
                else if (pureClothTemp > 8 && baseNatTemp > 10)
                {
                    adviceTxt = "It is best to dress as lightly as possible.";
                }
                else if (pureClothTemp > 10)
                {
                    adviceTxt = "You should dress lighter.";
                }
                else if (isMountain && !isNight && !isDungeon)
                {
                    adviceTxt = "These mountains will be icy cold once night falls.";
                }
                else if (totalTemp > 10 && !drink)
                {
                    adviceTxt = "You wish you had a water skin to drink from.";
                }
                else if (totalTemp > 30 && playerGPS.IsPlayerInLocationRect)
                {
                    adviceTxt = "Perhaps there is a pool of water here you could cool off in.";
                }
                else if (isDesert && !isNight)
                {
                    adviceTxt = "Though monsters roam the night, it might be preferable.";
                }
                else if (Hunger.hungry)
                {
                    adviceTxt = "Eating some fresh food would keep your strength up.";
                }
                else if (isDesert && DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Summer)
                {
                    adviceTxt = "Crossing deserts in the summer is only for the most hardy.";
                }
            }

            if (ClimateCalories.isVampire && playerEnterExit.IsPlayerInSunlight)
            {
                if (natTemp > 0 && DaggerfallUnity.Instance.WorldTime.Now.IsDay && !hood)
                {
                    if (cloak && !hood)
                    {
                        adviceTxt = "The rays of the sun burns your face and neck!";
                    }
                    else
                    {
                        adviceTxt = "Your exposed skin sizzles in the deadly sunlight!";
                    }
                }
            }

            return adviceTxt;
        }

        public static string TxtFood()
        {
            hunger = Hunger.hunger;
            string foodString = "You could do with a decent meal.";

            if (starving)
            {
                if (Hunger.starvDays > 7)
                {
                    foodString = string.Format("You have not eaten properly in over a week.");
                }
                else if (Hunger.starvDays == 1)
                {
                    foodString = string.Format("You have not eaten properly in a day.");
                }
                else
                {
                    foodString = string.Format("You have not eaten properly in {0} days.", Hunger.starvDays.ToString());
                }
            }
            else if (playerGPS.IsPlayerInLocationRect && playerGPS.IsPlayerInTown() && !rations)
            {
                foodString = "You should buy some sacks of rations while in town.";
            }
            else if (hunger < 180)
            {
                foodString = "You are invigorated from your last meal.";
            }
            else if (hunger < 220)
            {
                foodString = "You are not hungry.";
            }
            else if (rations)
            {
                foodString = "Your rations keep you from starving.";
            }
            
            return foodString;
        }

        public static string TxtWater()
        {
            int thirst = ClimateCalories.thirst;
            string drinkString = "You have water to drink.";

            if (!drink)
            {
                if (thirst > 500)
                {
                    drinkString = "You are very thirsty.";
                }
                else if (thirst > 100)
                {
                    drinkString = "You are getting thirsty.";
                }
                else if (playerGPS.IsPlayerInLocationRect && playerGPS.IsPlayerInTown())
                {
                    drinkString = "You should stock up on water while in town.";
                }
                else
                {
                    drinkString = "You have no water.";
                }
            }
            return drinkString;
        }

        public static string TxtSleep()
        {
            if (sleepyCounter > 200)
                return "You are exhausted from lack of sleep.";
            else if (sleepyCounter > 100)
                return "You are drowsy from lack of sleep.";
            else if (sleepyCounter > 50)
                return "You are tired from lack of sleep.";
            else if (sleepyCounter > 0)
                return "You are a bit sleepy.";

            return "You are well rested.";
        }

        public static string TxtEncumbrance()
        {
            float encPc = playerEntity.CarriedWeight / playerEntity.MaxEncumbrance;
            float encOver = Mathf.Max(encPc - 0.75f, 0f) * 2f;
            if (encOver > 0)
            {
                return "Your burden slows and exhausts you.";
            }
            else if (encPc > 0.6)
            {
                return "Your burden is quite heavy.";
            }
            return "You are not overburdened.";
        }

        public static string TxtEncAdvice()
        {
            int goldWeight = playerEntity.GoldPieces / 400;
            int thirdMaxEnc = playerEntity.MaxEncumbrance / 3;
            float encPc = playerEntity.CarriedWeight / playerEntity.MaxEncumbrance;
            if (playerEntity.Stats.LiveStrength < playerEntity.Stats.PermanentStrength)
            {
                return "Your strength is reduced, making you unable to carry as much.";
            }
            else if (goldWeight > thirdMaxEnc)
            {
                return "You are carrying " + goldWeight.ToString() + " kg in gold pieces.";
            }
            else if (encPc >= 0.75)
            {
                return "Perhaps you should leave some items behind?";
            }
            return "";
        }

    }
}