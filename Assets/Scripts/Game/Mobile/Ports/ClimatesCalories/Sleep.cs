// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File Sleep.cs, copied unchanged for iOS except lines marked MOBILE.
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Banking;

namespace ClimatesCalories
{
    public class Sleep
    {
        DaggerfallUnity dfUnity;

        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static private bool sleepy = false;
        static private bool exhausted = false;
        static public int sleepyCounter = 0;
        static private uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
        static public uint wakeOrSleepTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
        static public uint awakeOrAsleepHours = 0;
        static private bool awake = true;
        static private uint currentTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
        static private bool campSleep = false;

        static public void SleepCheck(int sleepTemp = 0)
        {
            if (sleepyCounter < 0)
                sleepyCounter = 0;
            //Debug.Log("[Climates & Calories] sleepyCounter = " + sleepyCounter.ToString());
            currentTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            if (ClimateCalories.isVampire)
            {
                sleepyCounter = 0;
                return;
            }
            if (playerEntity.IsResting && !playerEntity.IsLoitering && (playerEnterExit.IsPlayerInsideBuilding || campSleep))
            {
                campSleep = ClimateCalories.camping;
                Sleeping(sleepTemp);
            }
            else if (playerEntity.IsResting && !playerEntity.IsLoitering)
            {
                campSleep = ClimateCalories.camping;
                Sleeping(sleepTemp+20);
            }
            else
                NotResting();
        }

        static private void NotResting()
        {
            if (!awake)
            {
                awake = true;
                wakeOrSleepTime = currentTime;
                if (sleepyCounter > 0)
                    DaggerfallUI.AddHUDText("You need more rest...");
                int qualityPenalty = 10;
                if (playerEnterExit.IsPlayerInsideBuilding)
                {
                    int quality = (int)playerEnterExit.Interior.BuildingData.Quality;
                    qualityPenalty -= quality;
                    if (quality < 6)
                        DaggerfallUI.AddHUDText("You slept poorly.");
                    if (quality > 14)
                        DaggerfallUI.AddHUDText("Your rest was excellent.");
                }
                else if (!campSleep)
                {
                    qualityPenalty = 20;
                    DaggerfallUI.AddHUDText("You slept very poorly.");
                }
                campSleep = false;    
                sleepyCounter += qualityPenalty;
            }
           // Debug.Log("[Climates & Calories] NotResting()");
            gameMinutes = currentTime;
            awakeOrAsleepHours = (gameMinutes - wakeOrSleepTime) / 60;
            sleepyCounter += Mathf.Max((int)(awakeOrAsleepHours - 6) / 6, 0);
            //Debug.Log("[Climates & Calories] awakeOrAsleepHours = " + awakeOrAsleepHours.ToString());
            if (sleepyCounter > 0 && !sleepy)
            {
                sleepy = true;
                DaggerfallUI.AddHUDText("You stiffle a yawn...");
                sleepyCounter++;
            }

            if (sleepyCounter > 200 && !exhausted)
            {
                ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                DaggerfallUI.AddHUDText("You really need some sleep...");
                sleepyCounter++;
                exhausted = true;
            }

            if (sleepyCounter > 0)
            {
                int fatigueDmg = sleepyCounter / 20;
                playerEntity.DecreaseFatigue(fatigueDmg);
                //Debug.Log("[Climates & Calories] fatigueDmg = " + fatigueDmg.ToString());
            }
            else
            {
                sleepy = false;
                exhausted = false;
            }
        }

        static private void Sleeping(int sleepTemp = 0)
        {
            if (awake)
            {
                awake = false;
                wakeOrSleepTime = currentTime;
                sleepyCounter -= 5;
            }
            //Debug.Log("[Climates & Calories] Sleeping()");
            gameMinutes = currentTime;
            awakeOrAsleepHours = (gameMinutes - wakeOrSleepTime) / 60;
            //Debug.Log("[Climates & Calories] awakeOrAsleepHours = " + awakeOrAsleepHours.ToString());
            if (sleepyCounter > 200)
                sleepyCounter -= 100;
            else if (sleepyCounter > 50)
                sleepyCounter -= 10;

            if (awakeOrAsleepHours >= 1)
            {
                wakeOrSleepTime = currentTime - (uint)sleepTemp;
                sleepyCounter -= 5;
                if (playerEnterExit.IsPlayerInsideBuilding || ClimateCalories.camping)
                    sleepyCounter--;
            }
        }
    }
}