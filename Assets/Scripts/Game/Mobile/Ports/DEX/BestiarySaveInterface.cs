// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiarySaveInterface.cs, copied unchanged for iOS. No [Invoke], no edits: the
// bundle mod is a real Mod object, so mod.SaveDataInterface works exactly as it does on desktop
// and the troll-corpse state is saved and restored (the Climates & Calories port does the same).
//
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using FullSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DaggerfallBestiaryProject
{    
    [fsObject("v1")]
    struct BestiarySaveData_v1
    {
        public BestiaryTrollCorpseData_v1[] ActiveCorpses;
    }

    public class BestiarySaveInterface : MonoBehaviour, IHasModSaveData
    {
        Dictionary<ulong, BestiaryTrollCorpseSerializer> activeCorpseSerializers = new Dictionary<ulong, BestiaryTrollCorpseSerializer>();

        #region Unity
        void OnEnable()
        {
            SaveLoadManager.OnStartLoad += SaveLoadManager_OnStartLoad;
        }
                
        void OnDisable()
        {
            SaveLoadManager.OnStartLoad -= SaveLoadManager_OnStartLoad;
        }
        #endregion

        private void SaveLoadManager_OnStartLoad(SaveData_v1 saveData)
        {

        }

        public Type SaveDataType { get { return typeof(BestiarySaveData_v1); } }

        public object NewSaveData()
        {
            BestiarySaveData_v1 data = new BestiarySaveData_v1();
            data.ActiveCorpses = new BestiaryTrollCorpseData_v1[0] { };
            return data;
        }

        public object GetSaveData()
        {
            BestiarySaveData_v1 data = new BestiarySaveData_v1();
            data.ActiveCorpses = activeCorpseSerializers.Values.Select(serializer => (BestiaryTrollCorpseData_v1)serializer.GetSaveData()).ToArray();
            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            BestiarySaveData_v1 data = (BestiarySaveData_v1)saveData;

            foreach(BestiaryTrollCorpseData_v1 corpseData in data.ActiveCorpses)
            {
                if (corpseData.RespawnBuffer <= 0.0f)
                    continue;

                if(!activeCorpseSerializers.TryGetValue(corpseData.LoadID, out BestiaryTrollCorpseSerializer serializer))
                {
                    DaggerfallLoot loot = GameObjectHelper.CreateDroppedLootContainer(GameManager.Instance.PlayerObject, corpseData.LoadID, corpseData.TextureArchive, corpseData.TextureRecord);
                    serializer = loot.GetComponentInChildren<BestiaryTrollCorpseSerializer>();
                    var corpseBillboard = loot.GetComponentInChildren<BestiaryTrollCorpseBillboard>();
                    if (corpseBillboard != null)
                        corpseBillboard.PostParentedSetup(); // Ensures the BestiaryTrollCorpseEntity exists before we RestoreSaveData
                }

                if (serializer == null)
                    continue;

                serializer.RestoreSaveData(corpseData);
            }
        }

        public void RegisterActiveSerializer(BestiaryTrollCorpseSerializer serializer)
        {
            activeCorpseSerializers.Add(serializer.LoadID, serializer);
        }

        public void DeregisterActiveSerialier(BestiaryTrollCorpseSerializer serializer)
        {
            activeCorpseSerializers.Remove(serializer.LoadID);
        }
    }
}
