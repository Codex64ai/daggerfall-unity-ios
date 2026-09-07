// MOBILE PORT - source: github.com/Ralzar81/Climates-Calories @ c33a04f8d200615b01d09987262b1ed318e7615a
// File Camping.cs, copied unchanged for iOS except lines marked MOBILE.
// The original author's licence header follows.
// Project:         Climates & Calories mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;

namespace ClimatesCalories
{
    public class Camping
    {
        public static DFPosition CampMapPixel = null;
        public static bool CampDeployed = false;
        public static bool DungeonTent = false;
        public static Vector3 TentPosition;
        public static Quaternion TentRotation;
        public static GameObject Tent = null;
        public static Matrix4x4 TentMatrix;
        public static GameObject Fire = null;
        public static Vector3 FirePosition;
        public static bool FireLit = false;
        public static int CampDmg;
        public const int tentModelID = 41606;
        public const int templateIndex_Tent = 515;
        protected const DaggerfallMessageBox.MessageBoxButtons cancelButton = (DaggerfallMessageBox.MessageBoxButtons)2;
        protected const DaggerfallMessageBox.MessageBoxButtons cookButton = (DaggerfallMessageBox.MessageBoxButtons)37;
        protected const DaggerfallMessageBox.MessageBoxButtons restButton = (DaggerfallMessageBox.MessageBoxButtons)35;
        protected const DaggerfallMessageBox.MessageBoxButtons packButton = (DaggerfallMessageBox.MessageBoxButtons)36;
        public static bool ironmanOptionsCamp = false;
        static UserInterfaceManager uiManager = DaggerfallUI.Instance.UserInterfaceManager;
        private static List<DaggerfallUnityItem> rawFood;
        private static List<DaggerfallUnityItem> rawFish;
        private static DaggerfallUnityItem usedSkillet;

        public static void BedActivation(RaycastHit hit)
        {
            ClimateCalories.camping = true;
            DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenRestWindow);
        }

        public static bool UseCampEquip(DaggerfallUnityItem item, ItemCollection collection)
        {
            if (GameManager.Instance.AreEnemiesNearby(true))
            {
                DaggerfallUI.MessageBox("There are enemies nearby.");
                return false;
            }
            else if (CampDeployed)
            {
                DestroyCamp();
            }
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside && !GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
            {
                    DaggerfallUI.MessageBox("You can not set up your tent indoors.");
                    return false;
            }
            else if (GameManager.Instance.PlayerGPS.IsPlayerInTown(true, true))
            {
                DaggerfallUI.MessageBox("It is illegal to camp in town.");
                return false;
            }
            else
            {
                item.LowerCondition(1, GameManager.Instance.PlayerEntity, collection);
                if (item.currentCondition > 0)
                {
                    CampDmg = item.maxCondition - item.currentCondition;
                    collection.RemoveItem(item);
                    DeployTent();
                    return true;
                }
                else
                {
                    DaggerfallUI.MessageBox("Your camping equipment broke.");
                    collection.RemoveItem(item);
                    return false;
                }
            }
        }

        public static void DeployTent(bool fromSave = false)
        {
            if (fromSave == false)
            {
                CampMapPixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
                SetTentPositionAndRotation();
                DaggerfallUI.MessageBox("You set up camp");
            }
            else
            {
                PlaceTentOnGround();
            }
            //Attempt to load a model replacement
            Tent = MeshReplacement.ImportCustomGameobject(tentModelID, null, TentMatrix);
            Fire = MeshReplacement.ImportCustomFlatGameobject(210, 1, FirePosition, null);
            //Fire = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, null);
            if (Tent == null)
            {
                Tent = GameObjectHelper.CreateDaggerfallMeshGameObject(tentModelID, null);
            }
            if (Fire == null)
            {
                Fire = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, null);
            }
            //Set the model's position in the world

            Tent.transform.SetPositionAndRotation(TentPosition, TentRotation);
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
            {
                FirePosition = Tent.transform.position + (Tent.transform.up * 0.9f);
                Tent.SetActive(false);
            }
            else
            {
                FirePosition = Tent.transform.position + (Tent.transform.forward * 3) + (Tent.transform.up * 0.8f);
                Tent.SetActive(true);
            }

            Fire.transform.SetPositionAndRotation(FirePosition, TentRotation);
            Fire.SetActive(true);
            AddTorchAudioSource(Fire);
            GameObject lightsNode = new GameObject("Lights");
            lightsNode.transform.parent = Fire.transform;
            AddLight(DaggerfallUnity.Instance, Fire, lightsNode.transform);
            CampDeployed = true;
            FireLit = true;
        }        

        public static void RestOrPackTent(RaycastHit hit)
        {
            DaggerfallMessageBox campPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
            if (hit.transform.gameObject.GetInstanceID() == Tent.GetInstanceID())
            {

                string[] message = { "Do you wish to rest, cook or pack?" };
                campPopUp.SetText(message);
                campPopUp.OnButtonClick += CampPopUp_OnButtonClick;
                campPopUp.AddButton(restButton);
                campPopUp.AddButton(cookButton);
                campPopUp.AddButton(packButton);
                campPopUp.AddButton(cancelButton);
                campPopUp.Show();
            }
            else
            {
                DaggerfallUI.MessageBox("This is not your camp.");
            }
        }

        public static void RestOrPackFire(RaycastHit hit)
        {
            if (GameManager.Instance.AreEnemiesNearby(true))
            {
                DaggerfallUI.MessageBox("There are enemies nearby.");
            }
            else
            {
                if (!GameManager.Instance.AreEnemiesNearby(true) && ironmanOptionsCamp)
                {
                    Debug.Log("[Climates&Calories] Sending mod message to Ironman Options.");

                    ModManager.Instance.SendModMessage("Ironman Options", "campSave");
                }
                DaggerfallMessageBox campPopUp = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);
                if (Fire != null)
                {
                    if (hit.transform.gameObject.GetInstanceID() == Fire.GetInstanceID())
                    {
                        string[] message = { "Do you wish to rest, cook or pack?" };
                        campPopUp.SetText(message);
                        campPopUp.OnButtonClick += CampPopUp_OnButtonClick;
                        campPopUp.AddButton(restButton);
                        campPopUp.AddButton(cookButton);
                        campPopUp.AddButton(packButton);
                        campPopUp.AddButton(cancelButton);
                        campPopUp.Show();
                    }
                    else
                    {
                        ClimateCalories.camping = true;
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenRestWindow);
                    }
                }
                else
                {
                    if (!GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding || GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
                    {
                        string[] message = { "Do you wish to rest or cook?" };
                        campPopUp.SetText(message);
                        campPopUp.OnButtonClick += CampPopUp_OnButtonClick;
                        campPopUp.AddButton(restButton);
                        campPopUp.AddButton(cookButton);
                        campPopUp.AddButton(cancelButton);
                        campPopUp.Show();
                    }
                    else
                    {
                        ClimateCalories.camping = true;
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenRestWindow);
                    }

                }
            }           
        }

        private static void CampPopUp_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {

            if (messageBoxButton == restButton)
            {
                sender.CloseWindow();
                IUserInterfaceManager uiManager = DaggerfallUI.UIManager;
                ClimateCalories.camping = true;
                ClimateCalories.cooking = false;
                uiManager.PushWindow(new DaggerfallRestWindow(uiManager, true));
            }
            else if (messageBoxButton == cookButton)
            {
                List<DaggerfallUnityItem> skillets = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.UselessItems2, ClimateCalories.templateIndex_Skillet);
                if (skillets.Count >= 1)
                    usedSkillet = skillets[0];

                rawFood = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.UselessItems2, ItemRawMeat.templateIndex);
                rawFish = GameManager.Instance.PlayerEntity.Items.SearchItems(ItemGroups.UselessItems2, ItemRawFish.templateIndex);
                foreach (DaggerfallUnityItem rawFishItem in rawFish)
                {
                    rawFood.Add(rawFishItem);
                }

                DaggerfallListPickerWindow validItemPicker = new DaggerfallListPickerWindow(uiManager, uiManager.TopWindow);
                validItemPicker.OnItemPicked += Cook_OnItemPicked;
                foreach (DaggerfallUnityItem rawFoodItem in rawFood)
                {
                    validItemPicker.ListBox.AddItem(rawFoodItem.shortName);
                }

                if (validItemPicker.ListBox.Count > 0)
                    uiManager.PushWindow(validItemPicker);
                else
                    DaggerfallUI.MessageBox("You have nothing to cook.");
 
            }
            else if (messageBoxButton == packButton)
            {
                ClimateCalories.cooking = false;
                DaggerfallUnityItem CampEquip = ItemBuilder.CreateItem(ItemGroups.UselessItems2, ClimateCalories.templateIndex_CampEquip);
                CampEquip.LowerCondition(CampDmg, GameManager.Instance.PlayerEntity);
                DestroyCamp();
                GameManager.Instance.PlayerEntity.Items.AddItem(CampEquip);
                CampDeployed = false;
                FireLit = false;
                TentMatrix = new Matrix4x4();
                sender.CloseWindow();
            }
            else
            {
                ClimateCalories.cooking = false;
                sender.CloseWindow();
            }
        }

        private static void Cook_OnItemPicked(int index, string itemName)
        {
            ClimateCalories.cooking = true;
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            DaggerfallUI.UIManager.PopWindow();
            int cookTime = 30;
            int cookedTemplateIndex = ItemMeat.templateIndex;
            if (usedSkillet != null)
            {
                cookTime /= 2;
                usedSkillet.currentCondition -= 1;
            }

            DaggerfallUnityItem rawItem = rawFood[index];
            if (rawItem.TemplateIndex == ItemRawFish.templateIndex)
                cookedTemplateIndex = ItemCookedFish.templateIndex;
            DaggerfallUnityItem cookedItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, cookedTemplateIndex);
            DaggerfallUnity.Instance.WorldTime.Now.RaiseTime(DaggerfallDateTime.SecondsPerMinute * cookTime);

            GameManager.Instance.PlayerEntity.Items.RemoveItem(rawItem);
            switch ((rawItem as AbstractItemFood).FoodStatus)
            {
                case AbstractItemFood.StatusFresh:
                case AbstractItemFood.StatusStale:
                default:
                    break;
                case AbstractItemFood.StatusMouldy:
                    (cookedItem as AbstractItemFood).RotFood();
                    break;
                case AbstractItemFood.StatusRotten:
                    (cookedItem as AbstractItemFood).RotFood();
                    (cookedItem as AbstractItemFood).RotFood();
                    break;
                case AbstractItemFood.StatusPutrid:
                    (cookedItem as AbstractItemFood).RotFood();
                    (cookedItem as AbstractItemFood).RotFood();
                    (cookedItem as AbstractItemFood).RotFood();
                    break;
            }
            GameManager.Instance.PlayerEntity.Items.AddItem(cookedItem);
            string cookingTool = (usedSkillet != null) ? "Using your skillet" : "Having no skillet";
            DaggerfallUI.MessageBox(cookingTool + ", you spend " + cookTime.ToString() + " minutes cooking a piece of " + cookedItem.shortName + "." );
            if (usedSkillet != null && usedSkillet.currentCondition <= 0)
            {
                GameManager.Instance.PlayerEntity.Items.RemoveItem(usedSkillet);
                DaggerfallUI.MessageBox("Your skillet broke.");
            }
        }

        public static void DestroyCamp()
        {
            if (Tent != null)
            {
                Object.Destroy(Tent);
                Object.Destroy(Fire);
                Tent = null;
                Fire = null;
            }
        }

        public static void Destroy_OnTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            DestroyCamp();
        }

        private static void SetTentPositionAndRotation()
        {
            GameObject player = GameManager.Instance.PlayerObject;
            TentPosition = player.transform.position + (player.transform.forward * 2);
            TentMatrix = player.transform.localToWorldMatrix;

            RaycastHit hit;
            Ray ray = new Ray(TentPosition, Vector3.down);
            if (Physics.Raycast(ray, out hit, 10))
            {
                Debug.Log("Setting tent position and rotation");
                TentPosition = hit.point + (Vector3.down * 0.2f);
                TentRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                Debug.Log("Setting tent position and rotation failed");
            }
        }

        private static void PlaceTentOnGround()
        {
            if (!GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && !GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                RaycastHit hit;
                Ray rayDown = new Ray(TentPosition, Vector3.down);
                if (Physics.Raycast(rayDown, out hit, 1000))
                {
                    TentPosition = hit.point + (Vector3.down * 0.2f);
                }
                else
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    Vector3 newTentPos = TentPosition;
                    newTentPos.y = player.transform.position.y;
                    Ray rayFromPlayer = new Ray(newTentPos + Vector3.up, Vector3.down);
                    if (Physics.Raycast(rayFromPlayer, out hit, 1000))
                    {
                        TentPosition = hit.point + (Vector3.down * 0.2f);
                        //FirePosition = Tent.transform.position + (Tent.transform.forward * 3) + (Tent.transform.up * 0.6f);
                    }
                    else
                    {
                        Ray rayUp = new Ray(newTentPos + (Vector3.up * 500f), Vector3.down);
                        if (Physics.Raycast(rayUp, out hit, 1000))
                        {
                            TentPosition = hit.point + (Vector3.down * 0.2f);
                            //FirePosition = Tent.transform.position + (Tent.transform.forward * 3) + (Tent.transform.up * 0.6f);
                        }
                    }
                }                
                TentRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
        }

        public static void OnNewMagicRound_PlaceCamp()
        {
            if (CampDeployed && Tent != null && !GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                //GameObject player = GameObject.FindGameObjectWithTag("Player");
                //float distance = Vector3.Distance(player.transform.position, Tent.transform.position);
                DFPosition player = GameManager.Instance.PlayerGPS.CurrentMapPixel;
                int campX = CampMapPixel.X;
                int campY = CampMapPixel.Y;
                bool sameMapPixel = true;
                if (campX != player.X || campY != player.Y)
                    sameMapPixel = false;
                Debug.Log("[Climates & Calories] OnNewMagicRound_PlaceCamp CampMapPixel = " + Camping.CampMapPixel.ToString());
                Debug.Log("[Climates & Calories] OnNewMagicRound_PlaceCamp player = " + player.ToString());
                Debug.Log("[Climates & Calories] sameMapPixel = " + sameMapPixel.ToString());
                Debug.Log("[Climates & Calories] Tent active = " + Tent.activeSelf.ToString());
                if (!sameMapPixel)
                {
                    Tent.SetActive(false);
                    Fire.SetActive(false);
                }
                else
                {
                    Tent.SetActive(true);
                    Fire.SetActive(true);
                    PlaceTentOnGround();
                }
            }
        }

        private static GameObject AddLight(DaggerfallUnity dfUnity, GameObject obj, Transform parent)
        {
            Vector3 position = FirePosition;
            GameObject go = GameObjectHelper.InstantiatePrefab(dfUnity.Option_DungeonLightPrefab.gameObject, string.Empty, parent, position);
            Light light = go.GetComponent<Light>();
            Color32 fireColor = new Color32(255, 147, 41, 255);
            if (light != null)
            {
                light.color = fireColor;
                light.intensity = 1;
                light.range = 20;
                light.type = LightType.Point;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 1f;
                light.spotAngle = 140;
            }

            return go;
        }

        private static void AddTorchAudioSource(GameObject go)
        {
            DaggerfallAudioSource c = go.AddComponent<DaggerfallAudioSource>();
            c.AudioSource.dopplerLevel = 0;
            c.AudioSource.rolloffMode = AudioRolloffMode.Linear;
            c.AudioSource.maxDistance = 5f;
            c.AudioSource.volume = 0.7f;
            c.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);
        }
    }
}