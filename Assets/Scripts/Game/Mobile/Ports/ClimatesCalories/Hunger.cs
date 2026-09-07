// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File Hunger.cs, copied unchanged for iOS except lines marked MOBILE.
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace ClimatesCalories
{
    public class Hunger
    {
        DaggerfallUnity dfUnity;
        PlayerEnterExit playerEnterExit;

        //Hunting code WIP
        static float lastTickTime;
        static float tickTimeInterval;

        //Hunting Quest test
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static public bool hungry = true;
        static public bool starving = false;
        static public uint starvDays = 0;
        static private int starvCounter = 0;
        static public bool rations = RationsToEat();
        static private int foodCount = 0;
        static public uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
        static public uint ateTime = GameManager.Instance.PlayerEntity.LastTimePlayerAteOrDrankAtTavern;
        static public uint hunger = gameMinutes - ateTime;

        private static void GiveMeat(int meatAmount)
        {
            for (int i = 0; i < meatAmount; i++)
            {
                GameManager.Instance.PlayerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemMeat.templateIndex));
            }
        }

        static public void Starvation()
        {
            gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            if (playerEntity.IsInBeastForm)
                ateTime = gameMinutes;
            else if (ClimateCalories.isVampire)
                ateTime = gameMinutes - 250;
            else
                ateTime = GameManager.Instance.PlayerEntity.LastTimePlayerAteOrDrankAtTavern;
            hunger = gameMinutes - ateTime;
            starvDays = (hunger / 1440);
            starvCounter += (int)starvDays;
            rations = RationsToEat();
            if (hunger > 240)
            {
                hungry = true;
            }
            if (starvDays >= 1)
            {
                starving = true;
            }
            else
            {
                starving = false;
            }
            if (starving)
            {
                if (rations && starvCounter > 24)
                {
                    EatRation();
                    starving = false;
                }
                else if (!rations && starvCounter > 5)
                {
                    playerEntity.DecreaseFatigue(1);
                }
            }
            else if (!starving)
            {
                starvDays = 0;
            }
        }

        static private void EatRation()
        {
            ItemCollection playerItems = GameManager.Instance.PlayerEntity.Items;
            List<DaggerfallUnityItem> sacks = playerItems.SearchItems(ItemGroups.UselessItems2, ClimateCalories.templateIndex_Rations);
            if (sacks.Count >= 1)
            {
                DaggerfallUnityItem sack = sacks[0];
                DaggerfallUI.AddHUDText("You eat some rations.");
                playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() - 250;
                if (sack.stackCount == 1)
                {
                    DaggerfallUI.MessageBox(string.Format("You empty your sack of rations."));
                    playerItems.RemoveItem(sack);
                }
                else
                    sack.stackCount -= 1;
            }
            //foreach (DaggerfallUnityItem sack in sacks)
            //{
            //    if (sack.weightInKg > 0.1)
            //    {
            //        sack.weightInKg -= 1.0f;
            //        playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() - 250;
            //        if (!GameManager.IsGamePaused)
            //        {
            //            DaggerfallUI.AddHUDText("You eat some rations.");
            //        }
            //        if (sack.weightInKg <= 0.1)
            //        {
            //            GameManager.Instance.PlayerEntity.Items.RemoveItem(sack);
            //            DaggerfallUI.MessageBox(string.Format("You empty your sack of rations."));
            //        }
            //        break;
            //    }
            //}
        }

        static public bool RationsToEat()
        {
            List<DaggerfallUnityItem> sacks = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.UselessItems2, ClimateCalories.templateIndex_Rations);
            if (sacks.Count >= 1)
            {
                if (sacks.Count > 1)
                {
                    int stackSum = 0;
                    //Check ration items to clean up buggy items
                    foreach (DaggerfallUnityItem sack in sacks)
                    {
                        stackSum += sack.stackCount;
                        playerEntity.Items.RemoveItem(sack);
                    }
                    DaggerfallUnityItem newSack = ItemBuilder.CreateItem(ItemGroups.UselessItems2, 531);
                    newSack.stackCount = stackSum;
                    playerEntity.Items.AddItem(newSack);
                }
                
                return true;
            }
                

            return false;
        }

        static public void FoodRot(int rotBonus = 0)
        {
            bool rotted = false;
            int rotChance = 0;
            foreach (ItemCollection playerItems in new ItemCollection[] { GameManager.Instance.PlayerEntity.Items, GameManager.Instance.PlayerEntity.WagonItems, GameManager.Instance.PlayerEntity.OtherItems })
            {
                for (int i = 0; i < playerItems.Count; i++)
                {
                    DaggerfallUnityItem item = playerItems.GetItem(i);
                    if (item is AbstractItemFood)
                    {
                        rotChance = UnityEngine.Random.Range(1, 100) + (rotBonus / 2);
                        AbstractItemFood food = item as AbstractItemFood;
                        if (rotChance > food.maxCondition && !food.RotFood())
                        {
                            food.RotFood();
                            rotted = true;
                        }
                        rotChance = 0;
                    }
                }
            }
            if (rotted)
            {
                daysRot = 0;
                rotted = false;
                DaggerfallUI.AddHUDText("Your food is getting a bit ripe...");
            }
        }

        private static int rotCounter = 0;
        public static int daysRot = 0;

        public static void FoodRotter()
        {
            if (daysRot >= 1)
            {
                for (int i = 0; i < daysRot; i++)
                {
                    FoodRot();
                }
                daysRot = 0;
            }
        }

        public static void FoodRotCounter()
        {
            if (Climates.baseNatTemp > -30)
                rotCounter++;
            if (Climates.baseNatTemp > 20)
            {
                rotCounter++;
                if (Climates.baseNatTemp > 50)
                    rotCounter++;
            }
                
            if (rotCounter > 720)
            {
                rotCounter = 0;
                daysRot++;
            }
        }

        public static void FoodEffects_OnNewMagicRound()
        {
            if (hunger < 240)
            {
                foodCount += (240 - (int)hunger);
                if (foodCount >= 500)
                {
                    playerEntity.IncreaseFatigue(1, true);
                    foodCount = 0;
                }
            }
            else if (!hungry && !ClimateCalories.isVampire)
            {
                hungry = true;
                DaggerfallUI.AddHUDText("Your stomach rumbles...");
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                Debug.Log("FoodEffects_OnNewMagicRound() stomache message displayed. hunger = " + hunger.ToString());
            }
        }
    }
}