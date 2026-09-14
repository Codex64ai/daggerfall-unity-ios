// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiaryTrollCorpseSerializer.cs, copied unchanged for iOS. No [Invoke], no edits.
// MOBILE: .meta keeps the upstream guid (3345490d17e842546b0a4e3879a5c0b3) for the same reason
// BestiaryTrollCorpseBillboard.cs does - the prefab names it.
//
using UnityEngine;
using DaggerfallWorkshop.Game.Serialization;
using FullSerializer;
using System;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallBestiaryProject
{
    [fsObject("v1")]
    public struct BestiaryTrollCorpseData_v1
    {
        public ulong LoadID;
        public Vector3 Position;
        public Vector3 WorldCompensation;
        public int TextureArchive;
        public int TextureRecord;
        public float RespawnBuffer;
        public int MobileID;
        public float BillboardHeight;
        public float CorpseBillboardHeight;
    }

    [ImportedComponent]
    public class BestiaryTrollCorpseSerializer : MonoBehaviour, ISerializableGameObject
    {
        BestiaryTrollCorpseBillboard corpseBillboard;

        public ulong LoadID { get { return corpseBillboard.LoadID; } }

        public bool ShouldSave => true;

        void Awake()
        {
            corpseBillboard = GetComponent<BestiaryTrollCorpseBillboard>();
            if (!corpseBillboard)
                throw new Exception("BestiaryTrollCorpseBillboard not found.");
        }

        void Start()
        {
            if (LoadID != 0)
                BestiaryMod.SaveInterface.RegisterActiveSerializer(this);
        }

        void OnDestroy()
        {
            if(LoadID != 0)
                BestiaryMod.SaveInterface.DeregisterActiveSerialier(this);
        }

        public object GetSaveData()
        {
            var entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            if (entityBehaviour == null)
                return null;

            var trollCorpseEntity = entityBehaviour.Entity as BestiaryTrollCorpseEntity;
            if (trollCorpseEntity == null)
                return null;

            BestiaryTrollCorpseData_v1 data = new BestiaryTrollCorpseData_v1();
            data.LoadID = LoadID;
            data.Position = transform.position - new Vector3(0, trollCorpseEntity.CorpseBillboardHeight / 2.0f, 0.0f); // Save pre-AlignToBase
            data.WorldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;
            data.TextureArchive = corpseBillboard.Archive;
            data.TextureRecord = corpseBillboard.Record;
            data.RespawnBuffer = trollCorpseEntity.RespawnBuffer;
            data.MobileID = trollCorpseEntity.EnemyProperties.MobileID;
            data.BillboardHeight = trollCorpseEntity.EnemyProperties.BillboardHeight;
            data.CorpseBillboardHeight = trollCorpseEntity.CorpseBillboardHeight;

            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            if (corpseBillboard == null)
                return;

            var entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            if (entityBehaviour == null)
                return;

            var trollCorpseEntity = entityBehaviour.Entity as BestiaryTrollCorpseEntity;
            if (trollCorpseEntity == null)
                return;

            BestiaryTrollCorpseData_v1 data = (BestiaryTrollCorpseData_v1)dataIn;
            if (data.LoadID != LoadID)
                return;

            float diffY = GameManager.Instance.StreamingWorld.WorldCompensation.y - data.WorldCompensation.y;
            transform.position = data.Position + new Vector3(0, diffY, 0);

            trollCorpseEntity.RestoreSaveData(data);
        }
    }
}
