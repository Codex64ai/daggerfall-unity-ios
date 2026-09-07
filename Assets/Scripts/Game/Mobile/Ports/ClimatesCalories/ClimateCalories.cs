// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File ClimateCalories.cs, copied unchanged for iOS except lines marked MOBILE. 1 [Invoke] attribute(s) removed: MobilePortedMods calls Init when the mod entry is enabled; TavernWindow replaced by MobileTavernWindow
// MOBILE: DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk / DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.Drunk() now resolve to MobileTavernWindow (the port's own).
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using DaggerfallConnect;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;
using System;
using Wenzil.Console;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Serialization;
using System.Collections.Generic;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Formulas;

namespace ClimatesCalories
{
    [FullSerializer.fsObject("v1")]
    public class ClimateCaloriesSaveData
    {
        public int WetCount;
        public int AttCount;
        public int Thirst;
        public uint Starvation;
        public bool Hungry;
        public bool Starving;
        public int HuntingTimer;
        public int RotDays;
        public DFPosition TentMapPixel;
        public bool TentPlaced;
        public Vector3 TentPosition;
        //public Vector3 FirePosition;
        public Quaternion TentRotation;
        public Matrix4x4 TentMatrix;
        public int DeployedTentCondition;
        public int SleepyCounter;
        public uint WakeOrSleepTime;
        public bool FirstModUse;
        public int Drunk;
        public bool StartRound;
        public bool DungeonTent;
    }

    public class ClimateCalories : MonoBehaviour, IHasModSaveData
    {
        public const int templateIndex_CampEquip = 530;
        public const int templateIndex_Rations = 531;
        public const int templateIndex_Waterskin = 539;
        public const int templateIndex_Skillet = 540;
        public const string CAMP_POPUP = "campPopup";
        public const string WATER_REFILL = "waterRefill";
        public const string EAT_FOOD = "eatFood";
        public const string DRY_PLAYER = "dryPlayer";
        public const string ADD_ITEM = "addITEM";

        static Mod mod;
        static ClimateCalories instance;

        public Type SaveDataType
        {
            get { return typeof(ClimateCaloriesSaveData); }
        }

        public object NewSaveData()
        {
            return new ClimateCaloriesSaveData
            {
                WetCount = 0,
                AttCount = 0,
                Thirst = 0,
                Starvation = 0,
                Hungry = false,
                Starving = false,
                HuntingTimer = 0,
                RotDays = 0,
                TentMapPixel = new DFPosition(),
                TentPlaced = false,
                TentPosition = new Vector3(),
                TentRotation = new Quaternion(),
                TentMatrix = new Matrix4x4(),
                DeployedTentCondition = 100,
                SleepyCounter = 0,
                WakeOrSleepTime = 0,
                FirstModUse = true,
                Drunk = 0,
                StartRound = false,
                DungeonTent = false
            };
        }

        public object GetSaveData()
        {
            return new ClimateCaloriesSaveData
            {
                WetCount = wetCount,
                AttCount = attCount,
                Thirst = thirst,
                Starvation = Hunger.starvDays,
                Hungry = Hunger.hungry,
                Starving = Hunger.starving,
                HuntingTimer = Hunting.huntingTimer,
                RotDays = Hunger.daysRot,
                TentMapPixel = Camping.CampMapPixel,
                TentPlaced = Camping.CampDeployed,
                TentPosition = Camping.TentPosition,
                TentRotation = Camping.TentRotation,
                TentMatrix = Camping.TentMatrix,
                DeployedTentCondition = Camping.CampDmg,
                SleepyCounter = Sleep.sleepyCounter,
                WakeOrSleepTime = Sleep.wakeOrSleepTime,
                Drunk = DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk,
                StartRound = startRound,
                DungeonTent = Camping.DungeonTent
            };
        }

        public void RestoreSaveData(object saveData)
        {
            playerEntity = GameManager.Instance.PlayerEntity;
            var climateCaloriesSaveData = (ClimateCaloriesSaveData)saveData;
            wetCount = climateCaloriesSaveData.WetCount;
            attCount = climateCaloriesSaveData.AttCount;
            thirst = climateCaloriesSaveData.Thirst;
            Hunger.starvDays = climateCaloriesSaveData.Starvation;
            Hunger.hungry = climateCaloriesSaveData.Hungry;
            Hunger.starving = climateCaloriesSaveData.Starving;
            Hunting.huntingTimer = climateCaloriesSaveData.HuntingTimer;
            Hunger.daysRot = climateCaloriesSaveData.RotDays;
            Camping.CampMapPixel = climateCaloriesSaveData.TentMapPixel;
            Camping.CampDeployed = climateCaloriesSaveData.TentPlaced;
            Camping.TentPosition = climateCaloriesSaveData.TentPosition;
            Camping.TentRotation = climateCaloriesSaveData.TentRotation;
            Camping.TentMatrix = climateCaloriesSaveData.TentMatrix;
            Camping.CampDmg = climateCaloriesSaveData.DeployedTentCondition;
            Sleep.sleepyCounter = climateCaloriesSaveData.SleepyCounter;
            Sleep.wakeOrSleepTime = climateCaloriesSaveData.WakeOrSleepTime;
            DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk = climateCaloriesSaveData.Drunk;
            startRound = climateCaloriesSaveData.StartRound;
            Camping.DungeonTent = climateCaloriesSaveData.DungeonTent;

            if (Sleep.sleepyCounter > 1000)
                Sleep.sleepyCounter = 1000;

            Camping.DestroyCamp();
            tentLoad = true;
            //if (Camping.CampDeployed)
            //{
            //    Camping.DeployTent(true);
            //}
            isVampire = GameManager.Instance.PlayerEffectManager.HasVampirism();
            restoreSaveRound = true;
            fastTravelTime = 0;
            firstModUse = climateCaloriesSaveData.FirstModUse;
            if(firstModUse || Sleep.wakeOrSleepTime == 0)
            {
                playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() - 10;
                Sleep.wakeOrSleepTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            }
        }

        public const int tentModelID = 41606;
        public const int templateIndex_Tent = 540;

        static bool statusLookUp = true;
        static bool statusInterval = true;
        static public int txtIntervals = 5;
        static bool txtSeverity = true;
        static bool clothDmg = true;
        static bool encumbranceRPR = false;
        static bool tediousTravel = false;
        static bool climatesCloaksOld = false;
        static bool fillingFoodOld = false;
        static bool restoreSaveRound = false;
        static bool firstModUse = false;
        static bool startRound = false;
        static bool startImmunity = false;
        static int absTempOld = 0;
        static bool tentLoad = false;
        static bool videoIsPlaying = false;
        static bool isExhausted = false;
        static bool rndmStrtDng = false;
        static bool resetValues = false;
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            instance = go.AddComponent<ClimateCalories>();
            mod.SaveDataInterface = instance;

            StartGameBehaviour.OnStartGame += ClimatesCalories_OnStartGame;
            EntityEffectBroker.OnNewMagicRound += ClimatesCaloriesEffects_OnNewMagicRound;
            EntityEffectBroker.OnNewMagicRound += Hunger.FoodEffects_OnNewMagicRound;
            EntityEffectBroker.OnNewMagicRound += Camping.OnNewMagicRound_PlaceCamp;
            PlayerEnterExit.OnTransitionInterior += Camping.Destroy_OnTransition;
            PlayerEnterExit.OnTransitionExterior += Camping.Destroy_OnTransition;
            PlayerEnterExit.OnTransitionDungeonInterior += Camping.Destroy_OnTransition;
            PlayerEnterExit.OnTransitionDungeonExterior += Camping.Destroy_OnTransition;
            playerEntity.OnExhausted += PassedOut_OnExhausted;
            DaggerfallVidPlayerWindow.OnVideoStart += BoolSet_OnVideoStart;
            DaggerfallVidPlayerWindow.OnVideoEnd += BoolSet_OnVideoEnd;

            GameManager.Instance.RegisterPreventRestCondition(() => { return TooExtremeToRest(); }, "The temperature is too extreme to rest.");
            UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Tavern, typeof(DaggerfallWorkshop.Game.Mobile.MobileTavernWindow) /* MOBILE: TavernWindow.cs is unlicensed; the port has its own */);

            ItemHelper itemHelper = DaggerfallUnity.Instance.ItemHelper;

            itemHelper.RegisterCustomItem(ItemApple.templateIndex, ItemGroups.UselessItems2, typeof(ItemApple));
            itemHelper.RegisterCustomItem(ItemOrange.templateIndex, ItemGroups.UselessItems2, typeof(ItemOrange));
            itemHelper.RegisterCustomItem(ItemBread.templateIndex, ItemGroups.UselessItems2, typeof(ItemBread));
            itemHelper.RegisterCustomItem(ItemRawFish.templateIndex, ItemGroups.UselessItems2, typeof(ItemRawFish));
            itemHelper.RegisterCustomItem(ItemCookedFish.templateIndex, ItemGroups.UselessItems2, typeof(ItemCookedFish));
            itemHelper.RegisterCustomItem(ItemMeat.templateIndex, ItemGroups.UselessItems2, typeof(ItemMeat));
            itemHelper.RegisterCustomItem(ItemRawMeat.templateIndex, ItemGroups.UselessItems2, typeof(ItemRawMeat));
            itemHelper.RegisterCustomItem(ItemRations.templateIndex, ItemGroups.UselessItems2, typeof(ItemRations));

            itemHelper.RegisterItemUseHandler(templateIndex_CampEquip, Camping.UseCampEquip);
            itemHelper.RegisterCustomItem(templateIndex_CampEquip, ItemGroups.UselessItems2);
            itemHelper.RegisterCustomItem(templateIndex_Waterskin, ItemGroups.UselessItems2);
            itemHelper.RegisterItemUseHandler(templateIndex_Waterskin, UseAutoItem);
            itemHelper.RegisterCustomItem(templateIndex_Skillet, ItemGroups.UselessItems2);
            itemHelper.RegisterItemUseHandler(templateIndex_Skillet, UseAutoItem);

            PlayerActivate.RegisterCustomActivation(mod, 101, 0, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 101, 5, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 210, 0, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 210, 1, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 41116, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 41117, Camping.RestOrPackFire);
            PlayerActivate.RegisterCustomActivation(mod, 41220, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 41221, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 41222, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 212, 0, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 212, 2, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 212, 8, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 212, 9, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 182, 1, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 182, 2, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 182, 3, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 182, 11, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 184, 16, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 186, 1, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 186, 2, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 186, 3, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 197, 0, NPCWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 085, 0, WaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 212, 3, DryWaterSourceActivation);
            PlayerActivate.RegisterCustomActivation(mod, 41606, Camping.RestOrPackTent);

            PlayerActivate.RegisterCustomActivation(mod, 41000, Camping.BedActivation);
            PlayerActivate.RegisterCustomActivation(mod, 41001, Camping.BedActivation);
            PlayerActivate.RegisterCustomActivation(mod, 41002, Camping.BedActivation);

            EnemyDeath.OnEnemyDeath += Hunting.EnemyDeath_OnEnemyDeath;
        }


        private static void BoolSet_OnVideoStart()
        {
            videoIsPlaying = true;
        }

        private static void BoolSet_OnVideoEnd()
        {
            videoIsPlaying = false;
        }

        private static void PassedOut_OnExhausted(DaggerfallEntity entity)
        {
            Sleep.sleepyCounter -= 50;
            isExhausted = true;
        }

        private static void WaterSourceActivation(RaycastHit hit)
        {
            RefillWater(100);
        }

        private static void NPCWaterSourceActivation(RaycastHit hit)
        {
            RefillWater(100, true);
        }

        private static void DryWaterSourceActivation(RaycastHit hit)
        {
            DaggerfallUI.AddHUDText("This fountain is dry as dust.");
        }

        public static bool UseAutoItem(DaggerfallUnityItem item, ItemCollection collection)
        {
            if (item.TemplateIndex == templateIndex_Waterskin)
            {
                if (item.weightInKg <= 0.1)
                {
                    DaggerfallUI.MessageBox(string.Format("You should find a tavern or another source of water to refill the skin."));
                }
                else if (item.weightInKg <= 1)
                {
                    DaggerfallUI.MessageBox(string.Format("Your waterskin is almost empty."));
                }
                else
                {
                    DaggerfallUI.MessageBox(string.Format("When too hot, you will drink some water."));

                }
            }                
            else if (item.TemplateIndex == templateIndex_Rations)
            {
                DaggerfallUI.MessageBox(string.Format("When too hungry, you will eat some rations."));
            }
            else if (item.TemplateIndex == templateIndex_Skillet)
            {
                if (item.currentCondition < 1)
                    DaggerfallUI.MessageBox(string.Format("This skillet is broken."));
                else
                    DaggerfallUI.MessageBox(string.Format("When cooking at a campfire, you will use this skillet."));
            }
            return false;
        }

        void Awake()
        {
            Debug.Log("[Climates&Calories] Checking for other mods.");
            Mod rr = ModManager.Instance.GetMod("RoleplayRealism");
            Mod tt = ModManager.Instance.GetMod("TediousTravel");
            Mod ff = ModManager.Instance.GetMod("Filling Food");
            Mod cc = ModManager.Instance.GetMod("Climates&Cloaks");
            Mod io = ModManager.Instance.GetMod("Ironman Options");
            Mod rsd = ModManager.Instance.GetMod("RandomStartingDungeon");
            if (rr != null)
            {
                ModSettings rrSettings = rr.GetSettings();
                encumbranceRPR = rrSettings.GetBool("Modules", "encumbranceEffects");
            }
            if (tt != null)
            {
                tediousTravel = true;
            }
            if (ff != null)
            {
                fillingFoodOld = true;
            }
            if (cc != null)
            {
                climatesCloaksOld = true;
            }
            if (tediousTravel || fillingFoodOld || climatesCloaksOld)
            {
                EntityEffectBroker.OnNewMagicRound += ClimateIncomp_OnNewMagicRound;
            }
            if (io != null)
            {
                Debug.Log("[Climates&Calories] Detected Ironman Options");
                ModSettings ioSettings = io.GetSettings();
                int ioDungeonSetting = ioSettings.GetValue<int>("DungeonPermanentSave", "WhenToSave");
                int ioOutsideSetting = ioSettings.GetValue<int>("OutsidePermanentSave", "WhenToSave");
                if (ioDungeonSetting == 3 || ioOutsideSetting == 3)
                    Camping.ironmanOptionsCamp = true;
                Debug.Log("[Climates&Calories] ioDungeonSetting = " + ioDungeonSetting.ToString());
                Debug.Log("[Climates&Calories] ioOutsideSetting = " + ioOutsideSetting.ToString());
                Debug.Log("[Climates&Calories] ironmanOptionsCamp = " + Camping.ironmanOptionsCamp.ToString());
            }
            if (rsd != null)
            {
                Debug.Log("Random Starting Dungeon present");
                rndmStrtDng = true;
            }

            mod.MessageReceiver = MessageReceiver;
            mod.IsReady = true;
        }

        static public DaggerfallUnityItem[] NewRandomLoot(LootChanceMatrix matrix, PlayerEntity playerEntity)
        {
            List<DaggerfallUnityItem> items = new List<DaggerfallUnityItem>();
            return items.ToArray();
        }



        static public int txtCount = 4;
        static public int wetWeather = 0;
        static public int wetEnvironment = 0;
        static public int wetCount = 0;
        static private int attCount = 0;
        static private int debuffValue = 0;

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEntity.EntityBehaviour.GetComponent<EntityEffectManager>();
        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

        static public int thirst = 0;
        static public bool camping = false;
        static public bool cooking = false;
        static private bool groundSleep = false;
        static private int sleepTemp = 0;
        static public bool playerIsWading = false;
        static public bool isVampire = GameManager.Instance.PlayerEffectManager.HasVampirism();
        static public bool inPrison = playerEntity.InPrison;
        static private bool noSpawns = false;

        static public bool roadFollow = false;
        static public bool pathFollow = false;

        const float stdInterval = 0.5f;
        static private bool lookingUp = false;
        static int fastTravelTime = 0;

        static uint currentTime;

        void Start()
        {
            DaggerfallUI.UIManager.OnWindowChange += UIManager_OnWindowChange;
            RegisterConsoleCommands();
        }

        void Update()
        {
            if (noSpawns)
                playerEntity.PreventEnemySpawns = true;

            if (GameManager.Instance.PlayerMouseLook.Pitch <= -70 && !playerEnterExit.IsPlayerInside && !GameManager.Instance.PlayerMotor.IsSwimming && !GameManager.Instance.PlayerMotor.IsClimbing)
            {
                lookingUp = true;
            }

            if (playerEntity.InPrison || playerEntity.Arrested)
                inPrison = true;

            if (!dfUnity.IsReady || !playerEnterExit || GameManager.IsGamePaused)
                return;

            if (Hunting.HuntingTime && GameManager.Instance.IsPlayerOnHUD)
                Hunting.HuntingTime = false;

            if (startRound)
            {
                startRound = false;
                NewCharEffects();
            }

            if (tentLoad)
            {
                tentLoad = false;
                if (Camping.CampDeployed)
                {
                    Camping.DeployTent(true);
                }
            }

            currentTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();

            if (fastTravelTime > 0 && !DaggerfallUI.Instance.FadeBehaviour.FadeInProgress)
            {
                if (fastTravelTime > 1 && !isExhausted)
                {
                    playerEntity.LastTimePlayerAteOrDrankAtTavern = Hunger.gameMinutes - 260;
                    Hunger.hungry = false;
                    Hunger.starving = false;
                    Hunger.starvDays = 0;
                    Hunger.FoodRot(fastTravelTime);
                    Sleep.wakeOrSleepTime = currentTime;
                    RefillWater(100, true);
                }
                fastTravelTime = 0;
            }

            if (isExhausted && DaggerfallUI.UIManager.WindowCount == 0)
                isExhausted = false;

            if (isVampire)
            {
                //Waiting for access to check when vampire last fed.
                //Hunger.ateTime = DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects.VampirismEffect.lastTimeFed;
                playerEntity.LastTimePlayerAteOrDrankAtTavern = Hunger.gameMinutes - 260;
                Hunger.hungry = false;
                Hunger.starving = false;
                Hunger.starvDays = 0;
                return;
            }
            else
            {
                Hunger.gameMinutes = currentTime;
                Hunger.ateTime = playerEntity.LastTimePlayerAteOrDrankAtTavern;
                Hunger.hunger = Hunger.gameMinutes - Hunger.ateTime;
            }
            if (Hunger.hunger <= 239 && Hunger.hungry)
            {
                Hunger.hungry = false;
                Hunger.starving = false;
                Hunger.starvDays = 0;
            }
            else if (Hunger.starvDays >= 1 && !Hunger.starving)
            {
                Hunger.starvDays = (Hunger.hunger / 1440);
                Hunger.starving = true;
                DaggerfallUI.AddHUDText("You are starving...");
            }
            else if (Hunger.starvDays < 1 && Hunger.starving)
            {
                Hunger.starving = false;
            }
        }

        private void UIManager_OnWindowChange(object sender, EventArgs e)
        {
            if (DaggerfallUI.UIManager.WindowCount == 0)
                AdviceText.statusClosed = true;

            if (DaggerfallUI.UIManager.WindowCount == 2 && AdviceText.statusClosed)
            {
                Climates.TemperatureCalculator();
                AdviceText.AdviceBuilder(encumbranceRPR);
            }
        }


        //// Alternative Textbox code.
        static DaggerfallMessageBox tempInfoBox;
        public static void TextPopup(string[] message)
        {
            if (tempInfoBox == null)
            {
                tempInfoBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                ModManager.Instance.SendModMessage("TravelOptions", "noStopForUIWindow", tempInfoBox);
                tempInfoBox.AllowCancel = true;
                tempInfoBox.ClickAnywhereToClose = true;
                tempInfoBox.ParentPanel.BackgroundColor = Color.clear;
            }

            tempInfoBox.SetText(message);
            DaggerfallUI.UIManager.PushWindow(tempInfoBox);
        }

        private static void ClimatesCalories_OnStartGame(object sender, EventArgs e)
        {
            startRound = true;
            startImmunity = true;
            DaggerfallUnityItem rations = ItemBuilder.CreateItem(ItemGroups.UselessItems2, 531);
            DaggerfallUnityItem waterSkin = ItemBuilder.CreateItem(ItemGroups.UselessItems2, 539);
            rations.stackCount = 2;
            playerEntity.Items.AddItem(rations);
            playerEntity.Items.AddItem(waterSkin);
            Hunger.hungry = false;
            attCount = 0;
            thirst = 0;
            DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk = 0;
            Sleep.sleepyCounter = 0;
            playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() - 10;
            Sleep.wakeOrSleepTime = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            if (rndmStrtDng)
            {
                resetValues = true;
                inPrison = true;
            }
            Debug.Log("[Climates & Calories] OnStartGame");
        }

        public static bool TooExtremeToRest()
        {
            if (!(playerEnterExit.IsPlayerInsideBuilding || camping) && Climates.absTemp > 30)
                return true;
            else
                return false;
        }

        private static void NewCharEffects()
        {
            Debug.Log("[Climates & Calories] NewCharEffects");
            playerEntity = GameManager.Instance.PlayerEntity;
            DaggerfallUnityItem campEquip = ItemBuilder.CreateItem(ItemGroups.UselessItems2, 530);
            DaggerfallUnityItem cloak;
            DaggerfallUnityItem boots;

            if (playerGPS.CurrentLocation.Name == "Privateer's Hold")
            {
                wetCount = 100;
                string[] messages = new string[] { "You are cold and wet from the shipwreck.", "You should use the campfire to get dry." };
                TextPopup(messages);
                Races race = playerEntity.Race;
                if (playerEntity.Gender == Genders.Male)
                {
                    boots = ItemBuilder.CreateMensClothing(MensClothing.Shoes, race);
                }
                else
                {
                    boots = ItemBuilder.CreateWomensClothing(WomensClothing.Shoes, race);
                }
                boots.currentCondition /= 4;
                campEquip.currentCondition = 3;
                playerEntity.Items.AddItem(boots);
            }
            else
            {
                wetCount = 0;
                
                Races race = playerEntity.Race;
                if (playerEntity.Gender == Genders.Male)
                {
                    cloak = ItemBuilder.CreateMensClothing(MensClothing.Formal_cloak, race);
                    boots = ItemBuilder.CreateMensClothing(MensClothing.Shoes, race);
                }
                else
                {
                    cloak = ItemBuilder.CreateWomensClothing(WomensClothing.Formal_cloak, race);
                    boots = ItemBuilder.CreateWomensClothing(WomensClothing.Shoes, race);
                }
                cloak.CurrentVariant = 1;
                cloak.currentCondition /= 4;
                boots.currentCondition /= 4;
                campEquip.currentCondition = 25;
                playerEntity.Items.AddItem(cloak);
                playerEntity.Items.AddItem(boots);
                
            }
            playerEntity.Items.AddItem(campEquip);
            inPrison = false;
        }

        private static void ClimateIncomp_OnNewMagicRound()
        {
            if (climatesCloaksOld)
            {
                DaggerfallUI.SetMidScreenText("Climates&Calories is not compatible with Climates&Cloaks.");
                return;
            }
            if (fillingFoodOld)
            {
                DaggerfallUI.SetMidScreenText("Climates&Calories is not compatible with FillingFood.");
                return;
            }
            if (tediousTravel)
            {
                DaggerfallUI.SetMidScreenText("Tedious Travel is no longer supported.");
                DaggerfallUI.SetMidScreenText("Please switch to the Travel Options mod.");
                return;
            }

            if (!tediousTravel && !fillingFoodOld && !climatesCloaksOld)
                EntityEffectBroker.OnNewMagicRound -= ClimateIncomp_OnNewMagicRound;
        }

        private static void ClimatesCaloriesEffects_OnNewMagicRound()
        {

            playerEntity = GameManager.Instance.PlayerEntity;
            if (restoreSaveRound && !SaveLoadManager.Instance.LoadInProgress)
            {
                restoreSaveRound = false;
                return;
            }

            if (!SaveLoadManager.Instance.LoadInProgress)
            {
                debuffValue = 0;
                int natTemp = Climates.natTemp;
                int absTemp = Climates.absTemp;
                int natCharTemp = Climates.natCharTemp;
                int baseNatTemp = Climates.baseNatTemp;
                int totalTemp = Climates.totalTemp;
                bool travelOptionsActive = false;
                noSpawns = false;

                ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                {
                    travelOptionsActive = (bool)data;
                });

                if (travelOptionsActive)
                {
                    ModManager.Instance.SendModMessage("TravelOptions", "isFollowingRoad", null, (string message, object data) =>
                    {
                        roadFollow = (bool)data;
                    });

                    ModManager.Instance.SendModMessage("TravelOptions", "isPathFollowing", null, (string message, object data) =>
                    {
                        pathFollow = (bool)data;
                    });
                }                

                //When inside or camping, the counters reset faster and no temp effects are applied.
                if (playerEnterExit.IsPlayerInsideBuilding || cooking || (playerEntity.IsResting && camping))
                {
                    if (camping || cooking)
                    {
                        if (Dice100.SuccessRoll(playerEntity.Stats.LiveLuck + 20))
                            noSpawns = true;
                    }
                    txtCount = txtIntervals;
                    wetCount = Mathf.Max(wetCount - 2, 0);
                    attCount = Mathf.Max(attCount - 2, 0);
                    DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.Drunk();
                    Hunger.FoodRotCounter();
                    Hunger.Starvation();
                    Sleep.SleepCheck();
                    debuffValue = (int)Hunger.starvDays * 2;
                    DebuffAtt(debuffValue);

                    if (isVampire && playerEnterExit.IsPlayerInSunlight)
                        playerEntity.IncreaseHealth(1);
                }
                //When fast traveling counters resets.
                else if ((DaggerfallUI.Instance.FadeBehaviour.FadeInProgress
                    && GameManager.Instance.IsPlayerOnHUD)
                    || (startImmunity && !GameManager.Instance.IsPlayingGame())
                    || inPrison
                    || (!playerEntity.IsResting
                        && !GameManager.Instance.IsPlayerOnHUD
                        && !GameManager.Instance.IsPlayingGame()
                        && !travelOptionsActive
                        && !(DaggerfallUI.UIManager.WindowCount > 1 && DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox)))
                {
                    if (startImmunity && GameManager.Instance.IsPlayingGame())
                        startImmunity = false;
                    fastTravelTime++;
                    if (fastTravelTime > 1)
                    {
                        txtCount = txtIntervals;
                        wetCount = 0;
                        attCount = 0;
                        thirst = 0;
                        DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.drunk = 0;
                        Sleep.sleepyCounter = 0;
                        Sleep.wakeOrSleepTime = currentTime;
                        if (inPrison && GameManager.Instance.IsPlayerOnHUD)
                            inPrison = false;                            
                    }
                }
                //Sleeping outside. I keep track of temp during sleep and apply effects when waking up.
                else if (playerEntity.IsResting && !playerEntity.IsLoitering)
                {
                    if (!playerEnterExit.IsPlayerInside && Dice100.SuccessRoll(playerEntity.Stats.LiveLuck))
                        noSpawns = true;

                    DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.Drunk();
                    Hunger.FoodRotCounter();
                    Hunger.Starvation();

                    txtCount = txtIntervals;
                    wetCount += wetWeather + wetEnvironment;
                    if (natTemp > 10)
                    {
                        wetCount -= (natTemp / 10);
                        wetCount = Mathf.Max(wetCount, 0);
                    }
                    if (wetCount >= 1 && wetWeather == 0 && wetEnvironment == 0)
                    {
                        wetCount--;
                    }

                    Climates.TemperatureCalculator();
                    if (absTemp > sleepTemp)
                    {
                        sleepTemp = absTemp;
                        if (sleepTemp > 10)
                        {
                            groundSleep = true;
                        }
                    }
                    Sleep.SleepCheck(sleepTemp);
                    debuffValue = (int)Hunger.starvDays * 2;
                    DebuffAtt(debuffValue);
                }
                //If not camping, bed sleeping or traveling, apply normal C&C effects.
                else
                {
                    isVampire = GameManager.Instance.PlayerEffectManager.HasVampirism();
                    DaggerfallWorkshop.Game.Mobile.MobileTavernWindow.Drunk();
                    Hunger.FoodRotCounter();
                    Hunger.FoodRotter();
                    Hunger.Starvation();
                    Sleep.SleepCheck();

                    playerIsWading = GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming;
                    int fatigueDmg = 0;
                    camping = false;

                    if (groundSleep && !playerEntity.IsInBeastForm)
                    {
                        groundSleep = false;
                        sleepTemp *= 3;
                        if (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle || playerEnterExit.IsPlayerInsideSpecialArea)
                        {
                            DaggerfallUI.AddHUDText("You should have rested by a fire...");
                        }
                        else if (!GameManager.Instance.PlayerGPS.IsPlayerInLocationRect)
                        {
                            DaggerfallUI.AddHUDText("Sleeping on the ground was rough...");
                        }
                        if (sleepTemp >= playerEntity.CurrentFatigue)
                        {
                            sleepTemp = playerEntity.CurrentFatigue - 1;
                        }
                        fatigueDmg += sleepTemp;
                        sleepTemp = 0;
                    }

                    Climates.TemperatureCalculator();
                    wetCount += wetWeather + wetEnvironment;
                    if (natTemp > 10)
                    {
                        wetCount -= (natTemp / 10);
                        wetCount = Mathf.Max(wetCount, 0);
                    }
                    if (wetCount >= 1 && wetWeather == 0 && wetEnvironment == 0)
                    {
                        wetCount--;
                    }
                    txtCount++;

                    if (!GameManager.IsGamePaused && !isVampire && !playerEntity.IsInBeastForm)
                    {
                        thirst += Math.Min(Math.Max(totalTemp - 10, 1), 30);
                        if (thirst > 100 && Climates.gotDrink)
                        {
                            thirst = 0;
                            DrinkWater();
                        }
                        else if (thirst > 100)
                        {
                            fatigueDmg += (thirst / 100);
                        }
                    }

                    //Basic mod effect starts here at +/- 10+ by decreasing fatigue.
                    if (absTemp > 10)
                    {
                        fatigueDmg += absTemp / 20;
                        if (playerEntity.RaceTemplate.ID != (int)Races.Argonian)
                        {
                            if (absTemp < 30) { fatigueDmg /= 2; }
                            else { fatigueDmg *= 2; }
                        }
                        //Temperature +/- 30+ and starts debuffing attributes.
                        if (absTemp > 30)
                        {
                            attCount++;
                        }
                        else { attCount = 0; }
                        //Temperature +/- 50+ and starts causing damage.
                        if (absTemp > 50)
                        {
                            { playerEntity.DecreaseHealth((absTemp - 40) / 10); }
                        }
                    }

                    //If hot or cold, clothing might get damaged
                    if ((baseNatTemp > 10 || baseNatTemp < -10) && clothDmg)
                    {
                        int dmgRoll = UnityEngine.Random.Range(0, 100);
                        if (dmgRoll <= 2) { ClothDmg(); }
                    }

                    //If wet, armor might get damaged
                    if (wetCount > 5)
                    {
                        int dmgRoll = UnityEngine.Random.Range(0, 100);
                        if (dmgRoll < 5) { ArmorDmg(); }
                    }

                    //If you look up, midtext displays how the weather is.
                    if (lookingUp)
                    {
                        UpText(natTemp);
                        lookingUp = false;
                        if (statusLookUp)
                        {
                            CharTxt(totalTemp);
                            txtCount = 0;
                        }
                    }

                    //Apply damage for being naked or walking on foot.
                    if (playerEntity.RaceTemplate.ID != (int)Races.Argonian && playerEntity.RaceTemplate.ID != (int)Races.Khajiit && !playerEntity.IsInBeastForm)
                    {
                        NakedDmg(natTemp);
                        if (!playerIsWading && !GameManager.IsGamePaused && !playerEnterExit.IsPlayerInside)
                        {
                            FeetDmg(natTemp);
                        }
                    }

                    //Displays toptext at intervals
                    if (statusInterval && !playerEntity.IsInBeastForm)
                    {
                        if (txtCount >= txtIntervals && GameManager.Instance.IsPlayerOnHUD)
                        {
                            CharTxt(totalTemp);
                        }
                    }

                    if (txtCount >= txtIntervals) { txtCount = 0; }

                    //To counter a bug where you have 0 Stamina with no averse effects.
                    if (playerEntity.CurrentFatigue <= 10)
                    {
                        playerEntity.DecreaseHealth(2);
                        if (!GameManager.IsGamePaused) { DaggerfallUI.AddHUDText("You are exhausted and need to rest..."); }
                    }

                    if (!roadFollow && !pathFollow && !playerGPS.IsPlayerInLocationRect && !Hunting.HuntingTime && playerEntity.CurrentFatigue > 10)
                        playerEntity.DecreaseFatigue(32);

                    if (!Hunting.HuntingTime)
                    {
                        playerEntity.DecreaseFatigue(fatigueDmg, true);
                        playerEntity.DecreaseMagicka(fatigueDmg);
                    }

                    if (resetValues)
                    {
                        playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() - 10;
                        Hunger.hungry = false;
                        Hunger.starving = false;
                        Hunger.starvDays = 0;
                        resetValues = false;
                        Debug.Log("starvDays" + Hunger.starvDays.ToString());
                    }
                    debuffValue = (int)Hunger.starvDays * 2;

                    if (attCount > 0)
                    {
                        int countOrTemp = Mathf.Min(absTemp - 30, attCount);
                        int tempAttDebuff = Mathf.Max(0, countOrTemp);
                        if (playerEntity.RaceTemplate.ID == (int)Races.Argonian)
                        {
                            if (absTemp > 50) { tempAttDebuff *= 2; }
                            else { tempAttDebuff /= 2; }
                        }
                        tempAttDebuff += (thirst / 100);
                        debuffValue += tempAttDebuff;
                    }

                    DebuffAtt(debuffValue);
                    Hunting.HuntingRound();
                    ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                    {
                        if ((bool)data && absTemp > absTempOld + 10)
                        {
                            ModManager.Instance.SendModMessage("TravelOptions", "showMessage", "The temperature shifted...");
                        }
                    });
                    
                    absTempOld = absTemp;
                }
            }
        }

        static void DrinkWater()
        {
            List<DaggerfallUnityItem> skins = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, templateIndex_Waterskin);
            const string sipWater = "You drink from your waterskin.";
            const string runningOut = "Your water skin is nearly empty.";
            foreach (DaggerfallUnityItem skin in skins)
            {
                if (skin.weightInKg > 0.1)
                {
                    skin.weightInKg -= 0.1f;
                    if (skin.weightInKg <= 0.1)
                    {
                        ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                        skin.shortName = "Empty Waterskin";
                        DaggerfallUI.AddHUDText("You drain your waterskin.");
                    }
                    else if (skin.weightInKg < 0.3)
                    {
                        ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                        {
                            if ((bool)data)
                                ModManager.Instance.SendModMessage("TravelOptions", "showMessage", runningOut);
                            else
                                DaggerfallUI.AddHUDText(runningOut);
                        });
                    }
                    else
                    {
                        ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                        {
                            if ((bool)data)
                                ModManager.Instance.SendModMessage("TravelOptions", "showMessage", sipWater);
                            else
                                DaggerfallUI.AddHUDText(sipWater);
                        });
                    }
                    break;
                }
            }
        }

        public static void RefillWater(float waterAmount, bool noFullMsg = false)
        {
            float wLeft = 0;
            float skinRoom = 0;
            float fill = 0;
            bool waterFull = false;
            List<DaggerfallUnityItem> skins = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, templateIndex_Waterskin);
            if (skins.Count == 0)
            {
                if (thirst > 50)
                    DaggerfallUI.AddHUDText("You quench your thirst.");
                else
                    DaggerfallUI.AddHUDText("You have no waterskins to fill.");
            }
            else
            {
                float waterWeigth = 0;
                foreach (DaggerfallUnityItem skin in skins)
                    waterWeigth += skin.weightInKg;

                waterFull = waterWeigth >= skins.Count;

                if (!waterFull)
                {
                    foreach (DaggerfallUnityItem skin in skins)
                    {
                        if (waterAmount <= 0)
                        {
                            break;
                        }
                        else if (skin.weightInKg < 2)
                        {
                            wLeft = waterAmount - skin.weightInKg;
                            skinRoom = 2 - skin.weightInKg;
                            fill = Mathf.Min(skinRoom, wLeft);
                            waterAmount -= fill;
                            skin.weightInKg += Mathf.Min(fill, 2f);
                            skin.shortName = "Waterskin";
                            DaggerfallUI.AddHUDText("You refill your water.");
                        }
                    }
                }
                else if (!noFullMsg)
                {
                    if (skins.Count == 1)
                        DaggerfallUI.AddHUDText("Your waterskin is already full.");
                    else
                        DaggerfallUI.AddHUDText("All your waterskins are already full.");
                }
            }
            
            
            thirst = 0;
        }





        //If naked, may take damage from temperatures.
        static void NakedDmg(int natTemp)
        {
            DaggerfallUnityItem chest = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestClothes);
            DaggerfallUnityItem legs = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsClothes);
            DaggerfallUnityItem aChest = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem aLegs = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsArmor);
            bool cTop = false;
            bool cBottom = false;

            if (chest != null)
            {
                switch (chest.TemplateIndex)
                {
                    case (int)MensClothing.Short_tunic:
                    case (int)MensClothing.Toga:
                    case (int)MensClothing.Short_shirt:
                    case (int)MensClothing.Short_shirt_with_belt:
                    case (int)MensClothing.Short_shirt_closed_top:
                    case (int)MensClothing.Short_shirt_closed_top2:
                    case (int)MensClothing.Short_shirt_unchangeable:
                    case (int)MensClothing.Long_shirt:
                    case (int)MensClothing.Long_shirt_with_belt:
                    case (int)MensClothing.Long_shirt_unchangeable:
                    case (int)MensClothing.Eodoric:
                    case (int)MensClothing.Kimono:
                    case (int)MensClothing.Open_Tunic:
                    case (int)MensClothing.Long_shirt_closed_top:
                    case (int)MensClothing.Long_shirt_closed_top2:
                    case (int)WomensClothing.Vest:
                    case (int)WomensClothing.Eodoric:
                    case (int)WomensClothing.Short_shirt:
                    case (int)WomensClothing.Short_shirt_belt:
                    case (int)WomensClothing.Short_shirt_closed:
                    case (int)WomensClothing.Short_shirt_closed_belt:
                    case (int)WomensClothing.Short_shirt_unchangeable:
                    case (int)WomensClothing.Long_shirt:
                    case (int)WomensClothing.Long_shirt_belt:
                    case (int)WomensClothing.Long_shirt_unchangeable:
                    case (int)WomensClothing.Peasant_blouse:
                    case (int)WomensClothing.Long_shirt_closed:
                    case (int)WomensClothing.Open_tunic:
                    case (int)MensClothing.Anticlere_Surcoat:
                    case (int)MensClothing.Formal_tunic:
                    case (int)MensClothing.Reversible_tunic:
                    case (int)MensClothing.Dwynnen_surcoat:
                    case (int)WomensClothing.Long_shirt_closed_belt:
                        cTop = true;
                        break;
                    case (int)MensClothing.Priest_robes:
                    case (int)MensClothing.Plain_robes:
                    case (int)WomensClothing.Evening_gown:
                    case (int)WomensClothing.Casual_dress:
                    case (int)WomensClothing.Strapless_dress:
                    case (int)WomensClothing.Formal_eodoric:
                    case (int)WomensClothing.Priestess_robes:
                    case (int)WomensClothing.Plain_robes:
                    case (int)WomensClothing.Day_gown:
                        cTop = true;
                        cBottom = true;
                        break;
                }
            }
            if (!cBottom && legs != null)
            {
                switch (legs.TemplateIndex)
                {
                    case (int)MensClothing.Khajiit_suit:
                    case (int)WomensClothing.Khajiit_suit:
                        cTop = true;
                        cBottom = true;
                        break;
                    case (int)WomensClothing.Wrap:
                    case (int)MensClothing.Wrap:
                    case (int)MensClothing.Short_skirt:
                    case (int)WomensClothing.Tights:
                    case (int)MensClothing.Long_Skirt:
                    case (int)WomensClothing.Long_skirt:
                    case (int)MensClothing.Casual_pants:
                    case (int)MensClothing.Breeches:
                    case (int)WomensClothing.Casual_pants:
                        cBottom = true;
                        break;
                }
            }
            if (Climates.cloak || aChest != null) { cTop = true; }
            if (Climates.cloak || aLegs != null) { cBottom = true; }
            if ((!cTop || !cBottom))
            {
                if (playerEnterExit.IsPlayerInSunlight && natTemp > 10 && !GameManager.Instance.WeatherManager.IsOvercast)
                {
                    SunBurnRace(natTemp, cTop, cBottom);
                }
                else if (natTemp < -10)
                {
                    playerEntity.DecreaseHealth(1);
                    if (txtCount >= txtIntervals && !isVampire)
                    { DaggerfallUI.AddHUDText("The cold air numbs your bare skin."); }
                }
            }
        }

        private static void SunBurnRace(int natTemp, bool cTop, bool cBottom)
        {
            if (playerEntity.RaceTemplate.ID == (int)Races.DarkElf || playerEntity.RaceTemplate.ID == (int)Races.Redguard)
            {
                if(natTemp > 30)
                    SunBurn(cTop, cBottom);
            }
            else
            {
                SunBurn(cTop, cBottom);
            }
        }

        private static void SunBurn(bool cTop, bool cBottom)
        {
            if (playerEntity.CurrentHealth > 5)
                playerEntity.DecreaseHealth(1);

            if (txtCount >= txtIntervals && !cTop)
            {
                DaggerfallUI.AddHUDText("The sun burns your bare skin.");
            }
            else if (txtCount >= txtIntervals && !cBottom)
            {
                DaggerfallUI.AddHUDText("The sun burns your bare legs.");
            }
        }

        //If bare feet, may take damage from temperatures.
        static void FeetDmg(int natTemp)
        {
            int endBonus = playerEntity.Stats.LiveEndurance / 2;
            if (playerEntity.ItemEquipTable.GetItem(EquipSlots.Feet) == null
               && (Mathf.Abs(natTemp) - endBonus > 0)
               && GameManager.Instance.TransportManager.TransportMode == TransportModes.Foot)
            {
                playerEntity.DecreaseHealth(1);
                if (natTemp > 0 && txtCount >= txtIntervals)
                {
                    DaggerfallUI.AddHUDText("Your bare feet are getting burned.");
                }
                else if (txtCount >= txtIntervals)
                {
                    DaggerfallUI.AddHUDText("Your bare feet are freezing.");
                }
            }
        }

        static void ClothDmg()
        {
            DaggerfallUnityItem chestCloth = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestClothes);
            DaggerfallUnityItem feetCloth = playerEntity.ItemEquipTable.GetItem(EquipSlots.Feet);
            DaggerfallUnityItem legsCloth = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsClothes);
            DaggerfallUnityItem cloak1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak1);
            DaggerfallUnityItem cloak2 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak2);
            DaggerfallUnityItem rArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightArm);
            DaggerfallUnityItem lArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftArm);
            DaggerfallUnityItem chest = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem legs = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsArmor);
            DaggerfallUnityItem head = playerEntity.ItemEquipTable.GetItem(EquipSlots.Head);
            DaggerfallUnityItem gloves = playerEntity.ItemEquipTable.GetItem(EquipSlots.Gloves);

            int roll = UnityEngine.Random.Range(1, 10);
            int lRoll = UnityEngine.Random.Range(1, 10);
            DaggerfallUnityItem cloth = cloak2;
            DaggerfallUnityItem lArmor = chest;

            if (Climates.cloak)
            {
                if (cloak2 == null) { cloth = cloak1; }
                switch (roll)
                {
                    case 1:
                    case 2:
                    case 3:
                        break;
                    case 4:
                    case 5:
                        if (cloak1 != null) { cloth = cloak1; }
                        break;
                    case 6:
                    case 7:
                        if (chestCloth != null) { cloth = chestCloth; }
                        break;
                    case 8:
                    case 9:
                        if (legsCloth != null) { cloth = legsCloth; }
                        break;
                    case 10:
                        if (gloves != null) { cloth = gloves; }
                        break;
                }
            }
            else
            {
                cloth = chestCloth;
                switch (roll)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        break;
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                        if (legsCloth != null) { cloth = legsCloth; }
                        break;
                    case 9:
                    case 10:
                        if (gloves != null) { cloth = gloves; }
                        break;
                }
            }

            if (cloth != null)
            {
                cloth.LowerCondition(1, playerEntity);
                if (GameManager.Instance.TransportManager.TransportMode == TransportModes.Foot && feetCloth != null)
                {
                    feetCloth.LowerCondition(1, playerEntity);
                    if (feetCloth.currentCondition < (feetCloth.maxCondition / 10))
                    {
                        DaggerfallUI.AddHUDText("Your " + feetCloth.ItemName.ToString() + " are getting worn out...");
                    }
                }
                if (cloth.currentCondition < (cloth.maxCondition / 10))
                {
                    if (cloth == legsCloth)
                        DaggerfallUI.AddHUDText("Your " + cloth.ItemName.ToString() + " are getting worn out...");
                    else
                        DaggerfallUI.AddHUDText("Your " + cloth.ItemName.ToString() + " is getting worn out...");
                }
                else if (cloth.currentCondition == 0)
                {
                    ModManager.Instance.SendModMessage("TravelOptions", "pauseTravel");
                    DaggerfallUI.AddHUDText("Your " + cloth.ItemName.ToString() + " is ruined...");
                }
            }

            switch (lRoll)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                    break;
                case 5:
                case 6:
                case 7:
                    if (legs != null) { lArmor = legs; }
                    break;
                case 8:
                    if (lArm != null) { lArmor = lArm; }
                    break;
                case 9:
                    if (rArm != null) { lArmor = rArm; }
                    break;
                case 10:
                    if (head != null) { lArmor = head; }
                    break;
            }

            if (lArmor != null)
            {
                if (Leather(lArmor))
                {
                    lArmor.LowerCondition(1, playerEntity);
                    if (lArmor.currentCondition < (cloth.maxCondition / 10))
                    {
                        DaggerfallUI.AddHUDText("Your " + lArmor.ItemName.ToString() + " is getting worn out...");
                    }
                }
            }
        }

        static void ArmorDmg()
        {
            DaggerfallUnityItem rArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightArm);
            DaggerfallUnityItem lArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftArm);
            DaggerfallUnityItem chest = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem legs = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsArmor);
            DaggerfallUnityItem head = playerEntity.ItemEquipTable.GetItem(EquipSlots.Head);

            int roll = UnityEngine.Random.Range(1, 10);
            DaggerfallUnityItem armor = chest;
            if (chest == null) { armor = legs; }
            switch (roll)
            {
                case 1:
                case 2:
                case 3:
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                    if (legs != null) { armor = legs; }
                    break;
                case 8:
                    if (head != null) { armor = head; }
                    break;
                case 9:
                    if (lArm != null) { armor = lArm; }
                    break;
                case 10:
                    if (rArm != null) { armor = rArm; }
                    break;
            }

            if (armor != null)
            {
                if (!Leather(armor))
                {
                    int armorDmg = Mathf.Max(armor.maxCondition / 100, 1);
                    armor.LowerCondition(armorDmg, playerEntity);
                    if (armor.currentCondition < (armor.maxCondition / 10))
                    {
                        DaggerfallUI.AddHUDText("Your " + armor.ItemName.ToString() + " is getting rusty...");
                    }
                }
            }

        }

        static bool Leather(DaggerfallUnityItem armor)
        {
            if (armor != null)
            {
                if (armor.nativeMaterialValue == (int)ArmorMaterialTypes.Leather)
                { return true; }
                else
                { return false; }
            }
            else
            { return false; }
        }

        static void DebuffAtt(int deBuff)
        {
            int currentEn = playerEntity.Stats.PermanentEndurance;
            int currentSt = playerEntity.Stats.PermanentStrength;
            int currentAg = playerEntity.Stats.PermanentAgility;
            int currentInt = playerEntity.Stats.PermanentIntelligence;
            int currentWill = playerEntity.Stats.PermanentWillpower;
            int currentPer = playerEntity.Stats.PermanentPersonality;
            int currentSpd = playerEntity.Stats.PermanentSpeed;
            int[] statMods = new int[DaggerfallStats.Count];
            statMods[(int)DFCareer.Stats.Endurance] = -Mathf.Min(deBuff, currentEn - 5);
            statMods[(int)DFCareer.Stats.Strength] = -Mathf.Min(deBuff, currentSt - 5);
            statMods[(int)DFCareer.Stats.Agility] = -Mathf.Min(deBuff, currentAg - 5);
            statMods[(int)DFCareer.Stats.Intelligence] = -Mathf.Min(deBuff, currentInt - 5);
            statMods[(int)DFCareer.Stats.Willpower] = -Mathf.Min(deBuff, currentWill - 5);
            statMods[(int)DFCareer.Stats.Personality] = -Mathf.Min(deBuff, currentPer - 5);
            statMods[(int)DFCareer.Stats.Speed] = -Mathf.Min(deBuff, currentSpd - 5);
            playerEffectManager.MergeDirectStatMods(statMods);
        }

        static private void UpText(int natTemp)
        {
            if (!playerEnterExit.IsPlayerInsideDungeon)
            { SkyTxt(natTemp); }
            else
            { DungTxt(natTemp); }
        }

        static private void CharTxt(int totalTemp)
        {
            if (wetCount > 0) { WetTxt(totalTemp); }
            string tempText = "";
            if (totalTemp > 10)
            {
                if (totalTemp > 50)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "Soon you will be... too warm... to move..."; }
                    else tempText = "You cannot go on much longer in this heat...";
                }
                else if (totalTemp > 30)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "The heat... is slowing you down..."; }
                    else tempText = "You are getting dizzy from the heat...";
                }
                else if (totalTemp > 20 && !txtSeverity)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Khajiit) { tempText = "The heat is making you pant..."; }
                    else if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "You are absorbing too much heat..."; }
                    else tempText = "You wipe the sweat from your brow...";
                }
                else if (totalTemp > 10 && !txtSeverity)
                {
                    if (GameManager.Instance.IsPlayerInsideDungeon)
                    { tempText = ""; }
                    else { tempText = "You are a bit warm..."; }
                }
            }
            else if (totalTemp < -10)
            {
                if (totalTemp < -50)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "Soon you will be... too cold... to move..."; }
                    else tempText = "Your teeth are chattering uncontrollably!";
                }
                else if (totalTemp < -30)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "The cold... is slowing... you down..."; }
                    else tempText = "The cold is seeping into your bones...";
                }
                else if (totalTemp < -20 && !txtSeverity)
                {
                    if (playerEntity.RaceTemplate.ID == (int)Races.Argonian) { tempText = "You are losing too much heat..."; }
                    else tempText = "You shiver from the cold...";
                }
                else if (totalTemp < -10 && !txtSeverity)
                {
                    if (GameManager.Instance.IsPlayerInsideDungeon)
                    { tempText = ""; }
                    else { tempText = "You are a bit chilly..."; }
                }
            }
            DaggerfallUI.AddHUDText(tempText);
        }

        static private void SkyTxt(int natTemp)
        {
            string tempText = "The weather is temperate.";

            if (natTemp > 2)
            {
                if (natTemp > 60) { tempText = "As hot as the Ashlands of Morrowind."; }
                else if (natTemp > 50) { tempText = "The heat is suffocating."; }
                else if (natTemp > 40) { tempText = "The heat is unrelenting."; }
                else if (natTemp > 30) { tempText = "The air is scorching."; }
                else if (natTemp > 20) { tempText = "The weather is hot."; }
                else if (natTemp > 10) { tempText = "The weather is nice and warm."; }
            }
            else if (natTemp < -3)
            {
                if (natTemp < -60) { tempText = "As cold as the peaks of Skyrim"; }
                else if (natTemp < -50) { tempText = "The cold weather is deadly."; }
                else if (natTemp < -40) { tempText = "The cold is unrelenting."; }
                else if (natTemp < -30) { tempText = "The weather is freezing."; }
                else if (natTemp < -20) { tempText = "The weather is cold."; }
                else if (natTemp < -10) { tempText = "The weather is nice and cool."; }
            }
            DaggerfallUI.SetMidScreenText(tempText);
        }

        static private void DungTxt(int natTemp)
        {
            natTemp = Climates.Dungeon(natTemp);
            string tempText = "The air is temperate.";
            if (natTemp > 2)
            {
                if (natTemp > 60) { tempText = "You feel you are trapped in an oven."; }
                else if (natTemp > 50) { tempText = "The air is so warm it is suffocating."; }
                else if (natTemp > 40) { tempText = "The heat in here is awful."; }
                else if (natTemp > 30) { tempText = "The air in here is swelteringly hot."; }
                else if (natTemp > 20) { tempText = "The air in here is very warm."; }
                else if (natTemp > 10) { tempText = "The air in this place is stuffy and warm."; }
                else { tempText = "The air is somewhat warm."; }
            }
            else if (natTemp < -3)
            {
                if (natTemp < -60) { tempText = "You feel you are trapped in a glacier."; }
                else if (natTemp < -50) { tempText = "This place is as cold as ice."; }
                else if (natTemp < -40) { tempText = "The cold is unrelenting."; }
                else if (natTemp < -30) { tempText = "The air in here is freezing."; }
                else if (natTemp < -20) { tempText = "The air in here is very cold."; }
                else if (natTemp < -10) { tempText = "The air in here is chilly."; }
                else { tempText = "The air is cool."; }
            }
            DaggerfallUI.SetMidScreenText(tempText);
        }

        static private void WetTxt(int totalTemp)
        {
            string wetString = "";
            if (wetCount > 200) { wetString = "You are completely drenched."; }
            else if (wetCount > 100) { wetString = "You are soaking wet."; }
            else if (wetCount > 50) { wetString = "You are quite wet."; }
            else if (wetCount > 20) { wetString = "You are somewhat wet."; }
            else if (wetCount > 10) { wetString = "You are a bit wet."; }
            DaggerfallUI.AddHUDText(wetString);
            if (totalTemp < -10 && !GameManager.Instance.PlayerMotor.IsSwimming && wetCount > 10) { DaggerfallUI.AddHUDText("You should make camp and dry off."); }
        }

        void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            Debug.Log("[Climates & Calories] mod message recieved");
            switch (message)
            {
                case CAMP_POPUP:
                    Camping.RestOrPackFire((RaycastHit)data);
                    break;
                case WATER_REFILL:
                    RefillWater((float)data);
                    break;
                case EAT_FOOD:
                    playerEntity.LastTimePlayerAteOrDrankAtTavern = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
                    break;
                case ADD_ITEM:
                    if ((int)data > 530 && (int)data <= 540)
                        playerEntity.Items.AddItem(ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)data));
                    break;
                case DRY_PLAYER:
                    wetCount -= (int)data;
                    if (wetCount < 0) wetCount = 0;
                    break;
                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        public static void RegisterConsoleCommands()
        {
            Debug.Log("[Realistic Wagon] Trying to register console commands.");
            try
            {
                ConsoleCommandsDatabase.RegisterCommand(GetTent.name, GetTent.description, GetTent.usage, GetTent.Execute);
                ConsoleCommandsDatabase.RegisterCommand(AddSkillet.name, AddSkillet.description, AddSkillet.usage, AddSkillet.Execute);
                ConsoleCommandsDatabase.RegisterCommand(AddCampEquip.name, AddCampEquip.description, AddCampEquip.usage, AddCampEquip.Execute);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("Error Registering Climates&Calories Console commands: {0}", e.Message));
            }
        }

        private static class GetTent
        {
            public static readonly string name = "tent_rescue";
            public static readonly string description = "Place tent in front of player.";
            public static readonly string usage = "tent_rescue";

            public static string Execute(params string[] args)
            {
                string result = "error";
                if (!Camping.CampDeployed)
                    result = "No tent to rescue";
                else if (!playerEnterExit.IsPlayerInside && GameManager.Instance.PlayerController.isGrounded)
                {
                    Camping.DestroyCamp();
                    Camping.DeployTent();
                    result = "Tent Rescued";
                }
                else
                    result = "Command only possible while on the ground outside.";
                return result;
            }
        }

        private static class AddSkillet
        {
            public static readonly string name = "add_skillet";
            public static readonly string description = "Add skillet to inventory.";
            public static readonly string usage = "add_skillet";

            public static string Execute(params string[] args)
            {
                DaggerfallUnityItem skillet = ItemBuilder.CreateItem(ItemGroups.UselessItems2, templateIndex_Skillet);
                playerEntity.Items.AddItem(skillet);
                return "skillet added";
            }
        }

        private static class AddCampEquip
        {
            public static readonly string name = "add_campequip";
            public static readonly string description = "Add camping equipment to inventory.";
            public static readonly string usage = "add_campequip";

            public static string Execute(params string[] args)
            {
                DaggerfallUnityItem campEquip = ItemBuilder.CreateItem(ItemGroups.UselessItems2, templateIndex_CampEquip);
                playerEntity.Items.AddItem(campEquip);
                return "camping equipment added";
            }
        }
    }    
}