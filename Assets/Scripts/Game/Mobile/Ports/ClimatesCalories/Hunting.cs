// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File Hunting.cs, copied unchanged for iOS except lines marked MOBILE.
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using DaggerfallWorkshop.Game.Utility;

namespace ClimatesCalories
{

    public class Hunting
    {
        static int climate;
        static int luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
        public static int huntingTimer = 0;
        static bool lucky = false;
        static bool vLucky = false;
        static bool vUnLucky = false;
        public static bool HuntingTime = false;
        public static bool hunting = false;
        static bool isWinter = false;
        static GameObject carcass;

        public static void EnemyDeath_OnEnemyDeath(object sender, EventArgs e)
        {
            luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
            EnemyDeath enemyDeath = sender as EnemyDeath;
            if (enemyDeath != null)
            {
                DaggerfallEntityBehaviour entityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();
                if (entityBehaviour != null)
                {
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;
                    if (enemyEntity != null)
                    {
                        bool humanoid = HumanoidCheck(enemyEntity.MobileEnemy.ID);
                        if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Animal || enemyEntity.MobileEnemy.ID == (int)MobileTypes.Slaughterfish)
                        {
                            int meatAmount = GetMeatAmount(enemyEntity.MobileEnemy.ID);
                            for (int i = 0; i < meatAmount; i++)
                            {
                                DaggerfallUnityItem rawMeat = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemRawMeat.templateIndex);
                                if (enemyEntity.MobileEnemy.ID != (int)MobileTypes.GrizzlyBear && enemyEntity.MobileEnemy.ID != (int)MobileTypes.SabertoothTiger)
                                {
                                    AbstractItemFood food = rawMeat as AbstractItemFood;
                                    food.RotFood();
                                    if (Dice100.SuccessRoll(50))
                                        food.RotFood();
                                }
                                entityBehaviour.CorpseLootContainer.Items.AddItem(rawMeat);
                            }
                        }
                        else if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Human || humanoid)
                        {
                            int luckRoll = UnityEngine.Random.Range(1, 20) + (luckMod - 5);
                            if (luckRoll > 17)
                            {
                                DaggerfallUnityItem foodItem = FoodLoot();
                                entityBehaviour.CorpseLootContainer.Items.AddItem(foodItem);
                            }
                            if (luckRoll > 19)
                            {
                                DaggerfallUnityItem foodItem2 = FoodLoot();
                                entityBehaviour.CorpseLootContainer.Items.AddItem(foodItem2);
                            }      
                        }
                    }
                }
            }
        }

        private static int GetMeatAmount(int enemyID)
        {
            int meatAmount = 0;
            int luck = Mathf.Max(1, luckMod - 5);
            switch (enemyID)
            {
                case (int)MobileTypes.GrizzlyBear:
                    meatAmount = UnityEngine.Random.Range(6, (10 + luck));
                    break;
                case (int)MobileTypes.SabertoothTiger:
                case 263: //Snow Wolf
                    meatAmount = UnityEngine.Random.Range(4, (8 + luck));
                    break;
                case (int)MobileTypes.GiantScorpion:
                case (int)MobileTypes.Slaughterfish:
                case 262: //Wolf
                case 278: //Boar
                case 280: //Mountain Lion
                    meatAmount = UnityEngine.Random.Range(2, (3 +luck));
                    break;
                case (int)MobileTypes.Spider:
                case 267: //Dog
                case 271: //Blood Spider
                case 281: //Mudcrab
                    meatAmount = 2;
                    break;
                case (int)MobileTypes.Rat:
                    meatAmount = 1;
                    break;
                case (int)MobileTypes.GiantBat:
                case 260:
                    meatAmount = 1;
                    break;

            }
            return meatAmount;
        }

        private static bool HumanoidCheck(int enemyID)
        {
            switch (enemyID)
            {
                case (int)MobileTypes.Orc:
                case (int)MobileTypes.Centaur:
                case (int)MobileTypes.OrcSergeant:
                case (int)MobileTypes.Giant:
                case (int)MobileTypes.OrcShaman:
                case (int)MobileTypes.OrcWarlord:
                    return true;
            }
            return false;
        }

        private static DaggerfallUnityItem FoodLoot()
        {
            DaggerfallUnityItem food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemRations.templateIndex);
            int roll = UnityEngine.Random.Range(1, 11);

            switch (roll)
            {
                case 10:
                    food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemMeat.templateIndex);
                    break;
                case 9:
                    food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemRawFish.templateIndex);
                    break;
                case 8:
                case 7:
                    food.stackCount = UnityEngine.Random.Range(1, 4);
                    break;
                case 6:
                    food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemBread.templateIndex);
                    break;
                case 5:
                    food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemCookedFish.templateIndex);
                    break;
                case 4:
                case 3:
                case 2:
                case 1:
                    if (climate == (int)MapsFile.Climates.Subtropical || climate == (int)MapsFile.Climates.Desert || climate == (int)MapsFile.Climates.Desert2)
                    {
                        if (roll > 2)
                        {
                            food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, 539); //waterskin
                            food.weightInKg = 1f;
                        }
                        else
                            food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemOrange.templateIndex);
                    }
                    else
                        food = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemApple.templateIndex);
                    break;
            }

            if (food.TemplateIndex != 531 && food.TemplateIndex != 539)
            {
                int rotChance = UnityEngine.Random.Range(1, 110);
                AbstractItemFood absFood = food as AbstractItemFood;
                if (rotChance > food.maxCondition && !absFood.RotFood())
                {
                    absFood.RotFood();
                }
            }
            return food;
        }


        private static bool PlayerHasBow()
        {
            bool hasBow = false;
            List<DaggerfallUnityItem> sBow = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.Weapons, (int)Weapons.Short_Bow);
            List<DaggerfallUnityItem> lBow = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.Weapons, (int)Weapons.Long_Bow);
            List<DaggerfallUnityItem> cBow = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.Weapons, 289);
            hasBow = sBow.Count > 0 || lBow.Count > 0 || cBow.Count > 0 ? true : false;

            return hasBow;
        }

        //Uses OnNewMagicRound to check for animals to hunt.
        public static void HuntingRound()
        {
            if (!GameManager.Instance.AreEnemiesNearby() &&
                !DaggerfallUnity.Instance.WorldTime.Now.IsNight &&
                !GameManager.IsGamePaused &&
                !GameManager.Instance.PlayerGPS.IsPlayerInLocationRect &&
                !GameManager.Instance.PlayerEnterExit.IsPlayerInside &&
                huntingTimer <= 0)
            {
                isWinter = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
                luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
                int roll;
                if (isWinter)
                {
                    roll = UnityEngine.Random.Range(1, 300) - luckMod;
                }
                else
                    roll = UnityEngine.Random.Range(1, 200) - luckMod;
                
                if (roll < 1)
                {
                    if (!ClimateCalories.pathFollow && !ClimateCalories.roadFollow && Dice100.SuccessRoll(70))
                        HuntCheck();
                    else if (ClimateCalories.pathFollow && !ClimateCalories.roadFollow && Dice100.SuccessRoll(50))
                        HuntCheck();
                }
            }
            else if (huntingTimer > 10)
                huntingTimer--;
            else if (!GameManager.Instance.PlayerGPS.IsPlayerInLocationRect && huntingTimer > 0)
                huntingTimer--;
        }        

        public static void HuntCheck()
        {
            int lckRoll = UnityEngine.Random.Range(1, 110);
            lucky = lckRoll < GameManager.Instance.PlayerEntity.Stats.LiveLuck ? true : false;
            vLucky = lckRoll < GameManager.Instance.PlayerEntity.Stats.LiveLuck / 2 ? true : false;
            vUnLucky = lckRoll > GameManager.Instance.PlayerEntity.Stats.LiveLuck + 30 ? true : false;

            huntingTimer = UnityEngine.Random.Range(100, 500);

            climate = GameManager.Instance.PlayerGPS.CurrentClimateIndex;

            if ((climate == (int)MapsFile.Climates.Desert || climate == (int)MapsFile.Climates.Desert2))
            {
                DesertHuntingRoll();
            }
            else if (climate == (int)MapsFile.Climates.Subtropical)
            {
                SubtropicalHuntingRoll();
            }
            else if ((climate == (int)MapsFile.Climates.Swamp || climate == (int)MapsFile.Climates.Rainforest))
            {
                SwampHuntingRoll();
            }
            else if ((climate == (int)MapsFile.Climates.Woodlands || climate == (int)MapsFile.Climates.HauntedWoodlands))
            {
                WoodsHuntingRoll();
            }
            else if ((climate == (int)MapsFile.Climates.Mountain || climate == (int)MapsFile.Climates.MountainWoods))
            {
                MountainHuntingRoll();
            }
        }


        //Method for checking hunting in desert. Going to either DesertHunting_OnButtonClick or DesertWater_OnButtonClick.
        private static void DesertHuntingRoll()
        {
            int roll = UnityEngine.Random.Range(1, 11);
            DaggerfallMessageBox huntingPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", huntingPopUp);
            if (roll > 7 && Climates.gotDrink)
            {
                string[] message = {
                            "You spot a cluster of greener vegetation off in the distance.",
                            " ",
                            "There might be a source of water here where you could refill",
                            "your waterskin.",
                            "",
                            "Do you wish to spend some time searching for water?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += DesertWater_OnButtonClick;
            }
            else
            {
                string[] message = {
                            "You spot a cluster of rocks in the distance..",
                            " ",
                            "There might be animals seeking shelter between",
                            "them to avoid the harsh sun..",
                            "",
                            "Do you wish to spend some time checking the rocks?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += DesertHunting_OnButtonClick;
            }
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);

            huntingPopUp.Show();
        }
        //When clicking yes, do a DesertHuntingCheck
        private static void DesertHunting_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                DesertHuntingCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to see if and how much meat you find in the desert.
        private static void DesertHuntingCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            bool playerHasBow = PlayerHasBow();
            int skillSum = 0;
            int huntingRoll = UnityEngine.Random.Range(1, 101);
            int genRoll = UnityEngine.Random.Range(1, 101);
            Poisons poisonType = (Poisons)UnityEngine.Random.Range(128, 140);

            if (lucky)
            {
                //Lucky. Has bow
                if (playerHasBow)
                {
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery);
                    if (huntingRoll < skillSum)
                    {
                        string[] messages = new string[] { "You spot a snake among the rocks. You take careful aim and nail it with an arrow.", "", "You spend some time butchering the snake." };
                        ClimateCalories.TextPopup(messages);
                        GiveRawMeat(1);
                        playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                        playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                    }
                    else
                    {
                        string[] messages = new string[] { "You spot a snake among the rocks and take aim with your bow.", "You miss and the snake slithers away.", "", "You spend some more time searching, but no luck." };
                        ClimateCalories.TextPopup(messages);
                    }
                }
                //Lucky. No bow
                else
                {
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike);
                    if (huntingRoll < skillSum)
                    {
                        string[] messages = new string[] { "While searching the rocks, you come upon a sleeping snake.", "Your hand shoots out, grabbing the snakes tail.", "You whip it around and smack it into a rock.", "", "You spend some time butchering the snake." };
                        ClimateCalories.TextPopup(messages);
                        GiveRawMeat(1);
                        playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                        playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                    }
                    else
                    {
                        string[] messages = new string[] { "While searching the rocks, you come upon a sleeping snake.", "You attempt to get within striking distance, but the snake wakes and slither underneath a large rock.", "", "You spend some more time searching, but no luck." };
                        ClimateCalories.TextPopup(messages);
                    }
                }
            }
            else
            {
                //Very unlucky
                if (vUnLucky)
                {
                    string[] messages = new string[] { "You spend some time searching among the", "rocks, when you suddenly hear a sound...", "", "It seems you are about to become another hunters meal!" };
                    ClimateCalories.TextPopup(messages);
                    SpawnBeast();
                }
                //Unlucky. Has bow
                else if (playerHasBow)
                {
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery);
                    if (huntingRoll < skillSum && genRoll < GameManager.Instance.PlayerEntity.Stats.LiveIntelligence)
                    {
                        string[] messages = new string[] { "You spot a snake among the rocks. You take careful aim and nail it with an arrow.", "", "You poke the snake to make sure it is dead before picking it up.", "", "You spend some time butchering the snake." };
                        ClimateCalories.TextPopup(messages);
                        GiveRawMeat(1);
                        playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    }
                    else if (huntingRoll < skillSum)
                    {
                        string[] messages = new string[] { "You spot a snake among the rocks. You take careful aim and nail it with an arrow.", "", "As you pick up the dead snake, it suddenly twitches and sinks its fangs into your hand.", "You spend some time butchering the snake.", "", "You hope the snake was not poisonous..." };
                        ClimateCalories.TextPopup(messages);
                        DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                        GiveRawMeat(1);
                        playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    }
                    else
                    {
                        string[] messages = new string[] { "You miss the snake and it slithers away.", "", "As you search among the rocks you suddenly feel a sharp pain on your leg.", "", "You hope whatever bit you was not poisonous..." };
                        ClimateCalories.TextPopup(messages);
                        DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                    }
                }
                //Unlucky. No bow
                else if (!playerHasBow)
                {
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                    skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike);
                    if (huntingRoll < skillSum && genRoll + 30 < GameManager.Instance.PlayerEntity.Stats.LiveSpeed)
                    {
                        string[] messages = new string[] { "While searching the rocks, you come upon a snake.", "Before the snake has time to lunge, your grab it.", "You whip the snake around and smack it into a rock.", "", "You spend some time butchering the snake." };
                        ClimateCalories.TextPopup(messages);
                        GiveRawMeat(1);
                    }
                    else if (huntingRoll < skillSum)
                    {
                        string[] messages = new string[] { "While searching the rocks, you come upon a snake.", "Its head shoots out, sinking its fangs into your hand.", "You whip it around and smack it into a rock.", "", "You spend some time butchering the snake.", "", "You hope the snake was not poisonous..." };
                        ClimateCalories.TextPopup(messages);
                        DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                        GiveRawMeat(1);
                    }
                    else
                    {
                        string[] messages = new string[] { "While searching the rocks, you come upon a snake.", "Its head shoots out, sinking its fangs into your hand.", "You let out a yelp as the snake dislodges and slithers under a rock.", "", "You hope the snake was not poisonous..." };
                        ClimateCalories.TextPopup(messages);
                        DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                    }
                }
            }
        }

        //Method for checking hunting in tropics. Going to either SubtropicalHunting_OnButtonClick or DesertWater_OnButtonClick.
        private static void SubtropicalHuntingRoll()
        {
            int roll = UnityEngine.Random.Range(1, 11);
            DaggerfallMessageBox huntingPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", huntingPopUp);
            if (roll > 7 && Climates.gotDrink)
            {
                string[] message = {
                            "You spot a cluster of greener vegetation off in the distance.",
                            " ",
                            "There might be a source of water here where you could refill",
                            "your waterskin.",
                            "",
                            "Do you wish to spend some time searching for water?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += DesertWater_OnButtonClick;
            }
            else
            {
                string[] message = {
                            "You spot a gathering of trees in the distance..",
                            " ",
                            "There might be some ripe fruits to pick from them.",
                            "",
                            "Do you wish to spend some time checking the trees?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += SubtropicalHunting_OnButtonClick;
            }
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            huntingPopUp.Show();
        }
        //When clicking yes, skip 1 hour and do a SubtropicalHuntingCheck
        private static void SubtropicalHunting_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {

                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                SubtropicalHuntingCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to see if and how many oranges you find in the desert.
        private static void SubtropicalHuntingCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            int genRoll = UnityEngine.Random.Range(1, 90);
            Poisons poisonType = (Poisons)UnityEngine.Random.Range(128, 140);

            if (lucky)
            {
                //Very Lucky
                if (vLucky)
                {
                    string[] messages = new string[] { "You spot some fruits on a small tree and easily pick them." };
                    ClimateCalories.TextPopup(messages);
                    int fruit = UnityEngine.Random.Range(1, 10);
                    GiveOranges(fruit);
                }
                //Lucky
                else
                {
                    if (genRoll < playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Climbing))
                    {
                        string[] messages = new string[] { "You spot some fruits left on the highest branches of a tree.", "", "You climb up between the branches and pick some fruit." };
                        ClimateCalories.TextPopup(messages);
                        int fruit = UnityEngine.Random.Range(1, 5);
                        GiveOranges(fruit);
                        playerEntity.TallySkill(DFCareer.Skills.Climbing, 1);
                    }
                    else
                    {
                        string[] messages = new string[] { "You spot some fruits left on the highest branches of a tree.", "", "You attempt to climb the tree but are unable to get up there.", "", "Frustrated, you give up and continue your journey." };
                        ClimateCalories.TextPopup(messages);
                    }
                }
            }
            else
            {
                //Very UnLucky.
                if (vUnLucky)
                {
                    string[] messages = new string[] { "You pick a strange fruit from the tree and take a tentative bite.", "It seems edible at first, but then you feel your stomach cramp.", "", "You hope it was not poisonous and continue your journey." };
                    DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                    ClimateCalories.TextPopup(messages);
                }
                //UnLucky.
                else
                {
                    string[] messages = new string[] { "All edible fruits seem to have allready been picked.", "", "Disappointed, you continue your journey." };
                    ClimateCalories.TextPopup(messages);
                }
            }
        }

        //When clicking yes, do a DeserWaterCheck
        private static void DesertWater_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                DesertWaterCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to see if and how much water you find in the desert.
        private static void DesertWaterCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            int genRoll = UnityEngine.Random.Range(1, 101);
            Diseases diseaseType = (Diseases)UnityEngine.Random.Range(0, 17);

            if (lucky)
            {
                //Very Lucky
                if (vLucky)
                {
                    string[] messages = new string[] { "After some searching you find a pool of water.", "", "The water seems safe to drink." };
                    ClimateCalories.TextPopup(messages);
                    RefillWater(5);
                }
                //Lucky
                else
                {
                    string[] messages = new string[] { "After some searching you find a small pool of water.", "", "The water seems safe to drink." };
                    ClimateCalories.TextPopup(messages);
                    RefillWater(2);

                }
            }
            else
            {
                //Very Unlucky
                if (vUnLucky)
                {

                    if (genRoll < playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Medical - 10))
                    {
                        string[] messages = new string[] { "After some searching you find a small pool of water.", "You smell the water and decide it is unsafe to drink." };
                        ClimateCalories.TextPopup(messages);
                        playerEntity.TallySkill(DFCareer.Skills.Medical, 1);
                    }
                    else
                    {
                        string[] messages = new string[] { "After some searching you find a small pool of water.", "The water tastes somewhat foul, but you fill your waterskin with what you can scoop up.", "", "You are sure it is drinkable..." };
                        ClimateCalories.TextPopup(messages);
                        RefillWater(1);
                        EntityEffectBundle bundle = GameManager.Instance.PlayerEffectManager.CreateDisease(diseaseType);
                        GameManager.Instance.PlayerEffectManager.AssignBundle(bundle, AssignBundleFlags.BypassSavingThrows);
                    }
                }
                //Unlucky
                else
                {
                    string[] messages = new string[] { "No matter how much you search, you find nothing but dusty rocks." };
                    ClimateCalories.TextPopup(messages);
                }
            }
        }




        //Method for checking hunting in swamps. Going to either BirdHunting_OnButtonClick or SwampHunt_OnButtonClick.
        private static void SwampHuntingRoll()
        {
            int roll = UnityEngine.Random.Range(1, 11);
            DaggerfallMessageBox huntingPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", huntingPopUp);
            if (roll > 7)
            {
                string[] message = {
                            "You spot a flock of birds settling down in the tall grass.",
                            " ",
                            "Do you wish to spend some time attempting to hunt them?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += BirdHunting_OnButtonClick;
            }
            else
            {
                string[] message = {
                            "You see something slither under the surface of the murky water.",
                            "",
                            "Do you wish to spend some time attempting to hunt it?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += SwampHunt_OnButtonClick;
            }
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            huntingPopUp.Show();
        }
        //When clicking yes, skip 1 hour and do a BirdHuntingCheck
        private static void BirdHunting_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                BirdHuntingCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to hunt birds.
        private static void BirdHuntingCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            bool playerHasBow = PlayerHasBow();
            int skillSum = 0;
            int skillRoll = UnityEngine.Random.Range(1, 90);

            //Very unlucky
            if (vUnLucky)
            {
                string[] messages = new string[] { "You slowly and quietly sneak towards the birds.", "", "They all flee into the air as a deep roar is heard nearby!" };
                ClimateCalories.TextPopup(messages);
                SpawnBeast();
            }
            else if (playerHasBow)
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 2;
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery) / 2;
                if (skillRoll < skillSum - 30)
                {
                    int meat = UnityEngine.Random.Range(2, 3);
                    string[] messages = new string[] { "You slowly and quietly sneak towards the birds, readying your bow and arrow.", "You loose the arrow, piercing one of the birds. The rest take flight", "but you manage to loose several more arrows before they are out of range.", "", "You pick up the " + meat.ToString() + " dead birds and spend some time preparing them." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else if (skillRoll < skillSum)
                {
                    string[] messages = new string[] { "You slowly and quietly sneak towards the birds, readying your bow and arrow.", "You loose the arrow, piercing one of the birds. The rest take flight", " and your next shot goes wide of your prey. They are soon out of range.", "", "You collect the dead bird and spend some time preparing it." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(1);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else
                {
                    string[] messages = new string[] { "You slowly and quietly sneak towards the birds, readying your bow and arrow.", "Before you are in position, something spooks the birds and they suddenly take to the air." };
                    ClimateCalories.TextPopup(messages);
                }
            }
            else
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 4;
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 4;
                if (skillRoll < skillSum)
                {
                    string[] messages = new string[] { "You slowly and quietly sneak towards the birds, preparing to strike.", "You leap forward, attempting to reach your mark before it takes off.", "Your strike connect with a satisfying sound, the rest of the flock quickly flies away.", "", "You collect the dead bird and spend some time preparing it." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(1);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else
                {
                    string[] messages = new string[] { "You slowly and quietly sneak towards the birds, preparing to strike.", "Before you are in position, something spooks the birds and they suddenly take to the air." };
                    ClimateCalories.TextPopup(messages);
                }
            }
        }

        //When clicking yes, skip 1 hour and do a SwampHuntCheck
        private static void SwampHunt_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                SwampHuntCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to hunt in the swamp.
        private static void SwampHuntCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            bool playerHasBow = PlayerHasBow();
            int skillSum = 0;
            int skillRoll = UnityEngine.Random.Range(1, 110);

            //Very unlucky
            if (vUnLucky)
            {
                string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "", "Suddenly you hear a sound behind you.", "You are not the hunter, but the hunted!" };
                ClimateCalories.TextPopup(messages);
                SpawnBeast();
            }
            else if (playerHasBow)
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery) / 2;
                if (skillRoll < skillSum)
                {
                    int meat = UnityEngine.Random.Range(1, 2);
                    string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "", "Another ripple in the water appear and you release an arrow.", "", "You pull your scaly prey out of the swamp and butcher it." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else if (skillRoll > skillSum)
                {
                    string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "Another ripple in the water appear and you release an arrow.", "", "You miss the animal and the ripple does not reappear." };
                    ClimateCalories.TextPopup(messages);
                }
            }
            else
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 2;
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 2;
                if (skillRoll < skillSum)
                {
                    int meat = UnityEngine.Random.Range(1, 2);
                    string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "Your strike connect with a satisfying sound, and leverage the struggling lizard out of the water.", "", "You spend some time butchering the animal." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else
                {
                    Poisons poisonType = (Poisons)UnityEngine.Random.Range(128, 140);
                    if (lucky)
                    {
                        string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "", "The ripples never appear again. Finally, you give up." };
                        ClimateCalories.TextPopup(messages);
                    }
                    else
                    {
                        string[] messages = new string[] { "You sneak up to the waters edge and keep completely still.", "Time goes by while you stare intently at the water.", "", "You strike the water and some kind of fanged lizard", "explodes out of the water, sinking its teeth into your arm.", "You manage to shake it off and it disappears back into the water.", "", "You hope it was not poisonous..." };
                        ClimateCalories.TextPopup(messages);
                        DaggerfallWorkshop.Game.Formulas.FormulaHelper.InflictPoison(playerEntity, playerEntity, poisonType, false);
                    }
                }
            }
        }



        //Method for checking hunting in woods. Going to either BirdHunting_OnButtonClick or WoodHunt_OnButtonClick.
        private static void WoodsHuntingRoll()
        {
            int roll = UnityEngine.Random.Range(1, 11);
            DaggerfallMessageBox huntingPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", huntingPopUp);
            if (roll > 7)
            {
                string birds = "You spot a flock of birds settling down in the tall grass.";
                if (isWinter)
                {
                    birds = "You spot a flock of birds settling down in some snow-covered bushes.";
                }

                string[] message = {
                            birds,
                            " ",
                            "Do you wish to spend some time attempting to hunt them?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += BirdHunting_OnButtonClick;
            }
            else
            {
                string[] message = {
                            "You spot a set of animal tracks. They seem fresh.",
                            "",
                            "Do you wish to spend some time on a hunt?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += WoodsHunt_OnButtonClick;
            }
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            huntingPopUp.Show();
        }
        //When clicking yes, skip 1 hour and do a WoodsHuntCheck
        private static void WoodsHunt_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                WoodsHuntCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to hunt in the woods.
        private static void WoodsHuntCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            bool playerHasBow = PlayerHasBow();
            int skillSum = 0;
            int skillRoll = UnityEngine.Random.Range(1, 101);

            //Very unlucky
            if (vUnLucky)
            {
                string[] messages = new string[] { "You track your prey for some time. As you suspect you are", "getting near, you hear a sudden roar behind you.", "", "You are not the hunter, but the hunted!" };
                ClimateCalories.TextPopup(messages);
                SpawnBeast();
            }
            else if (playerHasBow)
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery) / 2;

                //Success
                if (skillRoll + 30 < skillSum)
                {
                    int meat = UnityEngine.Random.Range(2, 3);
                    string[] messages = new string[] { "You track a set of deer prints for some time.", "As you get within range, you knock an arrow and wait for the right moment.", "", "Your arrow flies true. The deer takes a few steps and collapses." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else if (skillRoll < skillSum)
                {
                    string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and stay perfectly still.", "", "After some time, you get a clear shot and your arrow pierces the animal." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(1);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                //Fail
                else if (skillRoll >= skillSum)
                {
                    if (skillRoll < 50)
                    {
                        string[] messages = new string[] { "You track a set of deer prints for some time.", "As you get within range, you knock an arrow and wait for the right moment.", "", "The deer suddenly leaps away and disappears, your arrow going wide of the mark." };
                        ClimateCalories.TextPopup(messages);
                    }
                    else
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and stay perfectly still.", "", "Your arrow goes wide of the mark, the rabbit scampers off." };
                        ClimateCalories.TextPopup(messages);
                    }
                }
            }
            else
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 2;
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 2;
                //Success
                if (skillRoll < skillSum)
                {
                    int meat = UnityEngine.Random.Range(1, 2);
                    string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "After some time, you have the animal within range and you lunge!", "", "You kill the rabbit in a single strike." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                //Fail
                else
                {
                    if (lucky)
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "After some time, you have the animal within range and you lunge!", "", "The rabbit is too quick and scampers away." };
                        ClimateCalories.TextPopup(messages);
                    }
                    else
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "Suddenly, a wild boar charges at you!", "", "After a furious struggle you manage to chase it off." };
                        ClimateCalories.TextPopup(messages);
                        playerEntity.DecreaseHealth(10);
                    }
                }
            }
        }



        //Method for checking hunting in mountains. Going to either BirdHunting_OnButtonClick or MountainHunt_OnButtonClick.
        private static void MountainHuntingRoll()
        {
            int roll = UnityEngine.Random.Range(1, 11);
            DaggerfallMessageBox huntingPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", huntingPopUp);
            if (roll > 7)
            {
                string[] message = {
                            "You spot a flock of birds settling down between some rocks.",
                            " ",
                            "Do you wish to spend some time attempting to hunt them?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += BirdHunting_OnButtonClick;
            }
            else
            {
                string[] message = {
                            "You cross a set of animal tracks. They seem fresh.",
                            "",
                            "Do you wish to spend some time on a hunt?"
                        };
                huntingPopUp.SetText(message);
                huntingPopUp.OnButtonClick += MountainHunt_OnButtonClick;
            }
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            huntingPopUp.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            huntingPopUp.Show();
        }
        //When clicking yes, skip 1 hour and do a MountainHuntCheck
        private static void MountainHunt_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                sender.CloseWindow();
                MovePlayer();
                TimeSkip();
                MountainHuntCheck();
            }
            else { sender.CloseWindow(); }
        }
        //Rolls for luck and skill checks to hunt in the mountains.
        private static void MountainHuntCheck()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            bool playerHasBow = PlayerHasBow();
            int skillSum = 0;
            int skillRoll = UnityEngine.Random.Range(1, 101);

            //Very unlucky
            if (vUnLucky)
            {
                string[] messages = new string[] { "You track your prey for some time. As you suspect you are", "getting near, you hear a sudden roar behind you.", "", "You are not the hunter, but the hunted!" };
                ClimateCalories.TextPopup(messages);
                SpawnBeast();
            }
            else if (playerHasBow)
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Archery) / 2;

                //Success
                if (skillRoll + 30 < skillSum)
                {
                    int meat = UnityEngine.Random.Range(2, 3);
                    string[] messages = new string[] { "You follow the trail of a mountain goat for some time.", "As you get within range, you knock an arrow and wait for the right moment.", "", "Your arrow flies true. The goat takes a few steps and collapses." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                else if (skillRoll < skillSum)
                {
                    string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and stay perfectly still.", "", "After some time, you get a clear shot and your arrow pierces the animal." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(1);
                    playerEntity.TallySkill(DFCareer.Skills.Archery, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                //Fail
                else if (skillRoll >= skillSum)
                {
                    if (skillRoll < 50)
                    {
                        string[] messages = new string[] { "You follow the trail of a mountain goat for some time.", "As you get within range, you knock an arrow and wait for the right moment.", "", "You miss, the arrow bouncing off the rocks. The goat escapes unscathed." };
                        ClimateCalories.TextPopup(messages);
                    }
                    else
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and stay perfectly still.", "", "Your arrow goes wide of the mark, the rabbit scampers off." };
                        ClimateCalories.TextPopup(messages);
                    }
                }
            }
            else
            {
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth) / 2;
                skillSum += playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike) / 2;
                //Success
                if (skillRoll < skillSum)
                {
                    int meat = UnityEngine.Random.Range(1, 2);
                    string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "After some time, you have the animal within range and you lunge!", "", "You kill the rabbit in a single strike." };
                    ClimateCalories.TextPopup(messages);
                    GiveRawMeat(meat);
                    playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                //Fail
                else
                {
                    if (lucky)
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "After some time, you have the animal within range and you lunge!", "", "The rabbit is too quick and scampers away." };
                        ClimateCalories.TextPopup(messages);
                    }
                    else
                    {
                        string[] messages = new string[] { "You find traces of rabbits in the area.", "You spot movement in the underbrush and attempt to get closer.", "", "Suddenly the rocks beneath your foot give way and you take a hard fall.", "", "The rabbit scampers off and you are left nursing your bruises." };
                        ClimateCalories.TextPopup(messages);
                        playerEntity.DecreaseHealth(10);
                    }
                }
            }
        }


        private static void GiveRawMeat(int meatAmount)
        {
            for (int i = 0; i < meatAmount; i++)
            {
                GameManager.Instance.PlayerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemRawMeat.templateIndex));
            }
            ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
            DaggerfallUI.AddHUDText("You gain " + meatAmount.ToString() + " pieces of Raw Meat.");
        }

        private static void GiveMeat(int meatAmount)
        {            
            for (int i = 0; i < meatAmount; i++)
            {
                GameManager.Instance.PlayerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemMeat.templateIndex));
            }
            ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
            DaggerfallUI.AddHUDText("You gain " + meatAmount.ToString() + " pieces of Meat.");
        }

        private static void GiveOranges(int fruitAmount)
        {
            for (int i = 0; i < fruitAmount; i++)
            {
                GameManager.Instance.PlayerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemOrange.templateIndex));
            }
            ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
            DaggerfallUI.AddHUDText("You gain " + fruitAmount.ToString() + " Oranges.");
        }

        private static void GiveApples(int fruitAmount)
        {
            for (int i = 0; i < fruitAmount; i++)
            {
                GameManager.Instance.PlayerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, ItemApple.templateIndex));
            }
            ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
            DaggerfallUI.AddHUDText("You gain "+fruitAmount.ToString()+" Apples.");
        }

        static void RefillWater(float waterAmount)
        {
            ClimateCalories.RefillWater(waterAmount);
        }

        private static void MovePlayer()
        {
            //int rollX = UnityEngine.Random.Range(-50, 51);
            //int rollY = UnityEngine.Random.Range(-50, 51);
            //int destinationPosX = (int)GameManager.Instance.PlayerObject.transform.position.x + rollX;
            //int destinationPosY = (int)GameManager.Instance.PlayerObject.transform.position.y + rollY;
            //GameManager.Instance.StreamingWorld.TeleportToCoordinates(destinationPosX, destinationPosY, StreamingWorld.RepositionMethods.DirectionFromStartMarker);
        }

        private static void TimeSkip(bool hunting = true)
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            HuntingTime = true;
            int skipAmount;
            if (hunting)
                skipAmount = Mathf.Max(UnityEngine.Random.Range(30, 60) - (playerEntity.Stats.LiveSpeed / 10), 5);
            else
                skipAmount = UnityEngine.Random.Range(10, 30);

            DaggerfallUnity.Instance.WorldTime.Now.RaiseTime(DaggerfallDateTime.SecondsPerMinute * skipAmount);
            playerEntity.DecreaseFatigue(PlayerEntity.DefaultFatigueLoss * skipAmount);
        }

        private static void SpawnBeast()
        {

            int roll = UnityEngine.Random.Range(0, 11);
            GameObject player = GameManager.Instance.PlayerObject;
            MobileTypes beast = MobileTypes.None;
            int count = 1;

            //Desert monster
            if ((climate == (int)MapsFile.Climates.Desert || climate == (int)MapsFile.Climates.Desert2) || climate == (int)MapsFile.Climates.Subtropical)
            {
                if (roll < 2)
                {
                    beast = MobileTypes.GiantScorpion;
                    count = 3;
                }
                else if (roll < 4)
                {
                    beast = MobileTypes.GiantScorpion;
                    count = 2;
                }
                else if (roll < 9)
                {
                    beast = MobileTypes.GiantScorpion;
                }
                else
                {
                    beast = MobileTypes.Dragonling_Alternate;
                }
            }
            //Swamp Monster
            else if ((climate == (int)MapsFile.Climates.Swamp || climate == (int)MapsFile.Climates.Rainforest))
            {
                if (roll < 2)
                {
                    beast = MobileTypes.GrizzlyBear;
                    count = 3;
                }
                else if (roll < 4)
                {
                    beast = MobileTypes.Spider;
                    count = 2;
                }
                else if (roll < 9)
                {
                    beast = MobileTypes.Spider;
                }
                else
                {
                    beast = MobileTypes.Dragonling_Alternate;
                }
            }
            //Forest Monster
            else if (climate == (int)MapsFile.Climates.Woodlands || climate == (int)MapsFile.Climates.HauntedWoodlands)
            {

                if (roll < 2)
                {
                    beast = MobileTypes.GrizzlyBear;
                    count = 3;
                }
                else if (roll < 4)
                {
                    beast = MobileTypes.GrizzlyBear;
                    count = 2;
                }
                else if (roll < 9)
                {
                    beast = MobileTypes.Spriggan;
                }
                else
                {
                    beast = MobileTypes.Dragonling_Alternate;
                }

            }
            //Mountain Monster
            else if (climate == (int)MapsFile.Climates.Mountain || climate == (int)MapsFile.Climates.MountainWoods)
            {
                if (roll < 2)
                {
                    beast = MobileTypes.SabertoothTiger;
                    count = 3;
                }
                else if (roll < 4)
                {
                    beast = MobileTypes.SabertoothTiger;
                    count = 2;
                }
                else if (roll < 9)
                {
                    beast = MobileTypes.SabertoothTiger;
                }
                else
                {
                    beast = MobileTypes.Dragonling_Alternate;
                }
            }

            //GameObjectHelper.CreateFoeSpawner(true, beast, count, 8, 20);
            ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
            int range = UnityEngine.Random.Range(2,8);
            GameObject[] mobiles = GameObjectHelper.CreateFoeGameObjects(player.transform.position, beast, count);
            mobiles[0].transform.position = player.transform.position - player.transform.forward * range;
            mobiles[0].transform.LookAt(player.transform.position);
            mobiles[0].SetActive(true);
            if (count > 1)
            {
                mobiles[1].transform.position = player.transform.position - player.transform.forward * (range + 2);
                mobiles[1].transform.LookAt(player.transform.position);
                mobiles[1].SetActive(true);
            }
            if (count == 3)
            {
                mobiles[2].transform.position = player.transform.position + player.transform.forward * (range + 2);
                mobiles[2].transform.LookAt(player.transform.position);
                mobiles[2].SetActive(true);
            }
        }
    }
}
