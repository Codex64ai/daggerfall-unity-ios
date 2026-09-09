// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationSaveDataInterface.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Utility;
using FullSerializer;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;

namespace LocationLoader
{
    [fsObject("v1")]
    public class LocationLootData_v1
    {
        public ulong loadID;
        public LootContainerTypes containerType;
        public InventoryContainerImages containerImage;
        public Vector3 currentPosition;
        public Vector3 localPosition;
        public Vector3 worldCompensation;
        public float heightScale;
        public int textureArchive;
        public int textureRecord;
        public string lootTableKey;
        public string entityName;
        public int stockedDate;
        public bool playerOwned;
        public bool customDrop;
        public bool isEnemyClass;
        public ItemData_v1[] items;
    }

    [fsObject("v1")]
    public struct LocationSaveData_v1
    {
        public ulong[] clearedEnemies;
        public EnemyData_v1[] activeEnemies;
        public LocationLootData_v1[] lootContainers;
    }

    public class LocationLootSerializer : MonoBehaviour, ISerializableGameObject
    {
        public

            #region Fields

            DaggerfallLoot loot;

        Vector3 worldCompensation;
        private LocationSaveDataInterface saveDataInterface;

        public bool Activated { get; set; }

        #endregion

        #region Unity

        void Awake()
        {
            loot = GetComponent<DaggerfallLoot>();
            if (!loot)
                throw new Exception("DaggerfallLoot not found.");
            saveDataInterface = LocationModLoader.modObject.GetComponent<LocationSaveDataInterface>();
        }

        void OnEnable()
        {
            if (LoadID != 0)
            {
                RefreshWorldCompensation();
                saveDataInterface.RegisterActiveSerializer(this);
            }
        }

        void OnDisable()
        {
            if (LoadID != 0)
            {
                if (saveDataInterface)
                {
                    if (Activated)
                        saveDataInterface.SerializeLoot(this);
                    saveDataInterface.DeregisterActiveSerializer(this);
                }
            }
        }

        #endregion

        public bool TryLoadSavedData()
        {
            if (LoadID == 0)
                return false;

            return saveDataInterface.TryDeserializeLoot(this);
        }

        public void RefreshWorldCompensation()
        {
            worldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;
        }

        public void InvalidateSave()
        {
            if (LoadID != 0)
            {
                if (saveDataInterface)
                {
                    // Don't serialize, this is used for when invalid loot is loaded on reused terrain
                    saveDataInterface.DeregisterActiveSerializer(this);
                }

                loot = null;
                Activated = false;
            }
        }

        #region ISerializableGameObject

        public ulong LoadID => loot ? loot.LoadID : 0;
        public bool ShouldSave => loot && Activated;

        public object GetSaveData()
        {
            if (!loot || !Activated)
                return null;

            // Create save data
            LocationLootData_v1 data = new LocationLootData_v1
            {
                loadID = LoadID,
                containerType = loot.ContainerType,
                containerImage = loot.ContainerImage,
                currentPosition = loot.transform.position,
                localPosition = loot.transform.localPosition,
                worldCompensation = worldCompensation,
                heightScale = loot.transform.localScale.y,
                textureArchive = loot.TextureArchive,
                textureRecord = loot.TextureRecord,
                stockedDate = loot.stockedDate,
                playerOwned = loot.playerOwned,
                customDrop = loot.customDrop,
                items = loot.Items.SerializeItems(),
                entityName = loot.entityName,
                isEnemyClass = loot.isEnemyClass
            };

            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            if (!loot)
                return;

            LocationLootData_v1 data = (LocationLootData_v1)dataIn;
            if (data.loadID != LoadID)
                return;

            // Restore billboard only if this is a billboard-based loot container
            if (loot.ContainerType == LootContainerTypes.RandomTreasure ||
                loot.ContainerType == LootContainerTypes.CorpseMarker ||
                loot.ContainerType == LootContainerTypes.DroppedLoot)
            {
                float diffY = GameManager.Instance.StreamingWorld.WorldCompensation.y - data.worldCompensation.y;
                loot.transform.position = data.currentPosition + new Vector3(0, diffY, 0);

                DaggerfallBillboard billboard = loot.GetComponent<DaggerfallBillboard>();
                // Restore appearance
                if (MeshReplacement.SwapCustomFlatGameobject(data.textureArchive, data.textureRecord, loot.transform,
                        Vector3.zero, inDungeon: false))
                {
                    // Use imported model instead of billboard
                    if (billboard) Destroy(billboard);
                    Destroy(GetComponent<MeshRenderer>());
                }
                else
                {
                    // Restore billboard if previously replaced by custom model
                    // This happens when the record is changed and new model is not provided by mods
                    if (!billboard)
                        billboard = loot.transform.gameObject.AddComponent<DaggerfallBillboard>();

                    // Restore billboard appearance
                    billboard.SetMaterial(data.textureArchive, data.textureRecord);

                    // Fix position if custom scale changed
                    if (data.heightScale == 0)
                        data.heightScale = 1;
                    if (data.heightScale != billboard.transform.localScale.y)
                    {
                        float height = billboard.Summary.Size.y * (data.heightScale / billboard.transform.localScale.y);
                        billboard.transform.Translate(0, (billboard.Summary.Size.y - height) / 2f, 0);
                    }
                }
            }

            // Restore items
            loot.Items.DeserializeItems(data.items);

            // Restore other data
            loot.ContainerType = data.containerType;
            loot.ContainerImage = data.containerImage;
            loot.TextureArchive = data.textureArchive;
            loot.TextureRecord = data.textureRecord;
            loot.stockedDate = data.stockedDate;
            loot.playerOwned = data.playerOwned;
            loot.customDrop = data.customDrop;
            loot.entityName = data.entityName;
            loot.isEnemyClass = data.isEnemyClass;

            // Remove loot container if empty
            if (loot.Items.Count == 0)
                GameObjectHelper.RemoveLootContainer(loot);

            Activated = true;
        }

        #endregion
    }

    public class LocationEnemySerializer : MonoBehaviour, ISerializableGameObject
    {
        #region Fields

        DaggerfallEnemy enemy;
        private DaggerfallEntityBehaviour entityBehaviour;
        Vector3 worldCompensation;
        private LocationSaveDataInterface saveDataInterface;

        #endregion

        #region Unity

        void Awake()
        {
            enemy = GetComponent<DaggerfallEnemy>();
            if (!enemy)
                throw new Exception("DaggerfallEnemy not found.");
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            saveDataInterface = LocationModLoader.modObject.GetComponent<LocationSaveDataInterface>();
        }

        void OnEnable()
        {
            if (LoadID != 0)
            {
                RefreshWorldCompensation();
                saveDataInterface.RegisterActiveSerializer(this);
            }
        }

        void OnDisable()
        {
            if (LoadID != 0)
            {
                if (LocationModLoader.modObject != null)
                {
                    if (IsDead())
                        saveDataInterface.AddDeadEnemy(this);
                    saveDataInterface.DeregisterActiveSerializer(this);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up, "LoadID=" + LoadID);
        }
#endif

        #endregion

        public bool IsDead()
        {
            if (!entityBehaviour)
                return false;

            EnemyEntity entity = (EnemyEntity)entityBehaviour.Entity;
            return entity.CurrentHealth <= 0;
        }

        public void RefreshWorldCompensation()
        {
            worldCompensation = GameManager.Instance.StreamingWorld.WorldCompensation;
        }

        public void InvalidateSave()
        {
            if (LoadID != 0)
            {
                if (saveDataInterface)
                {
                    // Don't serialize, this is used for when invalid loot is loaded on reused terrain
                    saveDataInterface.DeregisterActiveSerializer(this);
                }

                enemy = null;
            }
        }

        #region ISerializableGameObject

        public ulong LoadID => GetLoadID();
        public bool ShouldSave => HasChanged();

        public object GetSaveData()
        {
            if (!enemy)
                return null;

            // Get entity behaviour
            DaggerfallEntityBehaviour entityBehaviour = enemy.GetComponent<DaggerfallEntityBehaviour>();
            if (!entityBehaviour)
                return null;

            // Create save data
            EnemyEntity entity = (EnemyEntity)entityBehaviour.Entity;
            EnemyMotor motor = enemy.GetComponent<EnemyMotor>();
            EnemySenses senses = enemy.GetComponent<EnemySenses>();
            var mobileEnemy = enemy.GetComponentInChildren<MobileUnit>();
            EnemyData_v1 data = new EnemyData_v1
            {
                loadID = LoadID,
                gameObjectName = entityBehaviour.gameObject.name,
                currentPosition = enemy.transform.position,
                localPosition = enemy.transform.localPosition,
                currentRotation = enemy.transform.rotation,
                worldContext = entity.WorldContext,
                worldCompensation = worldCompensation,
                entityType = entity.EntityType,
                careerName = entity.Career.Name,
                careerIndex = entity.CareerIndex,
                startingHealth = entity.MaxHealth,
                currentHealth = entity.CurrentHealth,
                currentFatigue = entity.CurrentFatigue,
                currentMagicka = entity.CurrentMagicka,
                isHostile = motor.IsHostile,
                hasEncounteredPlayer = senses.HasEncounteredPlayer,
                isDead = entity.CurrentHealth <= 0,
                questSpawn = enemy.QuestSpawn,
                mobileGender = mobileEnemy.Enemy.Gender,
                items = entity.Items.SerializeItems(),
                equipTable = entity.ItemEquipTable.SerializeEquipTable(),
                instancedEffectBundles = GetComponent<EntityEffectManager>().GetInstancedBundlesSaveData(),
                alliedToPlayer = mobileEnemy.Enemy.Team == MobileTeams.PlayerAlly,
                questFoeSpellQueueIndex = entity.QuestFoeSpellQueueIndex,
                questFoeItemQueueIndex = entity.QuestFoeItemQueueIndex,
                wabbajackActive = entity.WabbajackActive,
                team = (int)entity.Team + 1,
                specialTransformationCompleted = mobileEnemy.SpecialTransformationCompleted
            };

            // Add quest resource data if present
            QuestResourceBehaviour questResourceBehaviour = GetComponent<QuestResourceBehaviour>();
            if (questResourceBehaviour)
            {
                data.questResource = questResourceBehaviour.GetSaveData();
            }

            return data;
        }

        public void RestoreSaveData(object dataIn)
        {
            if (!enemy)
                return;

            EnemyData_v1 data = (EnemyData_v1)dataIn;
            if (data.loadID != LoadID)
                return;

            DaggerfallEntityBehaviour entityBehaviour = enemy.GetComponent<DaggerfallEntityBehaviour>();
            EnemySenses senses = enemy.GetComponent<EnemySenses>();
            EnemyMotor motor = enemy.GetComponent<EnemyMotor>();
            EnemyEntity entity = entityBehaviour.Entity as EnemyEntity;
            MobileUnit mobileEnemy = enemy.GetComponentInChildren<MobileUnit>();

            bool genderChanged = false;
            if (data.mobileGender != MobileGender.Unspecified)
            {
                if (entity.Gender == Genders.Male && data.mobileGender == MobileGender.Female)
                    genderChanged = true;
                else if (entity.Gender == Genders.Female && data.mobileGender == MobileGender.Male)
                    genderChanged = true;
            }

            // Restore enemy career or class if different
            if (entity == null || entity.EntityType != data.entityType || entity.CareerIndex != data.careerIndex ||
                genderChanged)
            {
                SetupDemoEnemy setupEnemy = enemy.GetComponent<SetupDemoEnemy>();
                setupEnemy.ApplyEnemySettings(data.entityType, data.careerIndex, data.mobileGender, data.isHostile,
                    alliedToPlayer: data.alliedToPlayer);
                setupEnemy.AlignToGround();

                if (entity == null)
                    entity = entityBehaviour.Entity as EnemyEntity;
            }

            // Quiesce entity during state restore
            entity.Quiesce = true;

            // Restore enemy data
            entityBehaviour.gameObject.name = data.gameObjectName;
            enemy.transform.rotation = data.currentRotation;
            entity.QuestFoeSpellQueueIndex = data.questFoeSpellQueueIndex;
            entity.QuestFoeItemQueueIndex = data.questFoeItemQueueIndex;
            entity.WabbajackActive = data.wabbajackActive;
            entity.Items.DeserializeItems(data.items);
            entity.ItemEquipTable.DeserializeEquipTable(data.equipTable, entity.Items);
            entity.MaxHealth = data.startingHealth;
            entity.SetHealth(data.currentHealth, true);
            entity.SetFatigue(data.currentFatigue, true);
            entity.SetMagicka(data.currentMagicka, true);
            int team = data.team;
            if (team > 0) // Added 1 to made backwards compatible. 0 = no team saved
                entity.Team = (MobileTeams)(team - 1);
            motor.IsHostile = data.isHostile;
            senses.HasEncounteredPlayer = data.hasEncounteredPlayer;

            // Restore enemy position and migrate to floating y support for exteriors
            // Need to get relative difference between current and serialized world compensation to get actual y position
            float diffY = GameManager.Instance.StreamingWorld.WorldCompensation.y - data.worldCompensation.y;
            enemy.transform.position = data.currentPosition + new Vector3(0, diffY, 0);

            // Disable dead enemies
            if (data.isDead)
            {
                entityBehaviour.gameObject.SetActive(false);
            }

            // Restore quest resource link
            enemy.QuestSpawn = data.questSpawn;
            if (enemy.QuestSpawn)
            {
                // Add QuestResourceBehaviour to GameObject
                QuestResourceBehaviour questResourceBehaviour =
                    entityBehaviour.gameObject.AddComponent<QuestResourceBehaviour>();
                questResourceBehaviour.RestoreSaveData(data.questResource);

                // Destroy QuestResourceBehaviour if no actual quest properties are restored from save
                if (questResourceBehaviour.QuestUID == 0 || questResourceBehaviour.TargetSymbol == null)
                {
                    enemy.QuestSpawn = false;
                    Destroy(questResourceBehaviour);
                }
            }

            // Restore instanced effect bundles
            GetComponent<EntityEffectManager>().RestoreInstancedBundleSaveData(data.instancedEffectBundles);

            // Restore special transformation state if completed
            if (data.specialTransformationCompleted && mobileEnemy)
            {
                mobileEnemy.SetSpecialTransformationCompleted();
            }

            // Resume entity
            entity.Quiesce = false;
        }

        #endregion

        #region Private Methods

        bool HasChanged()
        {
            if (!enemy)
                return false;

            // Always serialize enemy
            return true;
        }

        ulong GetLoadID()
        {
            if (!enemy)
                return 0;

            return enemy.LoadID;
        }

        #endregion
    }

    public class LocationSaveDataInterface : MonoBehaviour, IHasModSaveData
    {
        public static ulong ToObjectLoadId(ulong locationId, int objectId)
        {
            return (locationId << 16) | (uint)objectId;
        }

        public static ulong LocationIdFromObjectLoadId(ulong objectLoadId)
        {
            return objectLoadId >> 16;
        }

        Dictionary<ulong, LocationLootData_v1> savedLoot = new Dictionary<ulong, LocationLootData_v1>();

        Dictionary<ulong, LocationLootSerializer> activeLootSerializers =
            new Dictionary<ulong, LocationLootSerializer>();

        Dictionary<ulong, LocationEnemySerializer> activeEnemySerializers =
            new Dictionary<ulong, LocationEnemySerializer>();

        private HashSet<ulong> clearedEnemies = new HashSet<ulong>();

        #region Unity

        void OnEnable()
        {
            StreamingWorld.OnFloatingOriginChange += StreamingWorld_OnFloatingOriginChange;
            SaveLoadManager.OnStartLoad += SaveLoadManager_OnStartLoad;
        }

        void OnDisable()
        {
            StreamingWorld.OnFloatingOriginChange -= StreamingWorld_OnFloatingOriginChange;
            SaveLoadManager.OnStartLoad -= SaveLoadManager_OnStartLoad;
        }

        private void SaveLoadManager_OnStartLoad(SaveData_v1 saveData)
        {
            clearedEnemies.Clear();
        }

        private void StreamingWorld_OnFloatingOriginChange()
        {
            foreach (var loot in activeLootSerializers.Values)
            {
                loot.RefreshWorldCompensation();
            }

            foreach (var enemy in activeEnemySerializers.Values)
            {
                enemy.RefreshWorldCompensation();
            }
        }

        #endregion

        public void SerializeLoot(LocationLootSerializer serializer)
        {
            savedLoot[serializer.LoadID] = (LocationLootData_v1)serializer.GetSaveData();
        }

        public bool TryDeserializeLoot(LocationLootSerializer serializer)
        {
            if (!savedLoot.TryGetValue(serializer.LoadID, out LocationLootData_v1 value))
                return false;

            serializer.RestoreSaveData(value);
            return true;
        }

        public void RegisterActiveSerializer(LocationLootSerializer serializer)
        {
            activeLootSerializers.Add(serializer.LoadID, serializer);
        }

        public void DeregisterActiveSerializer(LocationLootSerializer serializer)
        {
            activeLootSerializers.Remove(serializer.LoadID);
        }

        public void AddDeadEnemy(LocationEnemySerializer serializer)
        {
            clearedEnemies.Add(serializer.LoadID);
        }

        public bool IsEnemyDead(ulong loadID)
        {
            return clearedEnemies.Contains(loadID);
        }

        public void RegisterActiveSerializer(LocationEnemySerializer serializer)
        {
            activeEnemySerializers.Add(serializer.LoadID, serializer);
        }

        public void DeregisterActiveSerializer(LocationEnemySerializer serializer)
        {
            activeEnemySerializers.Remove(serializer.LoadID);
        }


        void FlushActiveInstances()
        {
            // We always serialize active loot instances, even if expired
            foreach (var loot in activeLootSerializers.Values)
            {
                if (loot.Activated)
                    SerializeLoot(loot);
            }

            foreach (var enemy in activeEnemySerializers.Values)
            {
                if (enemy.IsDead())
                    AddDeadEnemy(enemy);
            }
        }

        void ReloadActiveInstances(EnemyData_v1[] enemies)
        {
            // Copy collection since empty loot might get deregistered in the process
            var currentSerializers = activeLootSerializers.Values.ToArray();
            foreach (var loot in currentSerializers)
            {
                TryDeserializeLoot(loot);
            }

            // Copy collection
            var enemySerializers = activeEnemySerializers.Values.ToArray();
            foreach (LocationEnemySerializer activeSerializer in enemySerializers)
            {
                if (clearedEnemies.Contains(activeSerializer.LoadID))
                {
                    Destroy(activeSerializer.gameObject);
                }
                else
                {
                    EnemyData_v1 enemyData = enemies.FirstOrDefault(e => e.loadID == activeSerializer.LoadID);
                    if (enemyData != null)
                    {
                        activeSerializer.RestoreSaveData(enemyData);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"Location loader: Enemy data not found for loadId '{activeSerializer.LoadID}'");
                    }
                }
            }
        }

        // Returns the oldest date that we preserve in save
        public static int MakeLootThresholdExpirationValue()
        {
            DaggerfallDateTime time = DaggerfallUnity.Instance.WorldTime.Now;
            var thresholdYear = time.Year;
            var thresholdDay = time.DayOfYear; // 1-indexed, 1-360
            if (thresholdDay <= LocationLoader.LootExpirationDays)
            {
                thresholdYear -= 1;
                thresholdDay = 360 + thresholdDay - LocationLoader.LootExpirationDays;
            }
            else
            {
                thresholdDay -= LocationLoader.LootExpirationDays;
            }

            return thresholdYear * 1000 + thresholdDay;
        }

        /// <summary>
        /// The type of a custom class that holds save data and optionally use <see cref="FullSerializer.fsObjectAttribute"/> for versioning.
        /// </summary>
        public Type SaveDataType => typeof(LocationSaveData_v1);

        /// <summary>
        /// Makes a new instance of <see cref="SaveDataType"/> with default values.
        /// </summary>
        public object NewSaveData()
        {
            LocationSaveData_v1 defaultData = new LocationSaveData_v1
            {
                lootContainers = Array.Empty<LocationLootData_v1>(),
                activeEnemies = Array.Empty<EnemyData_v1>(),
                clearedEnemies = Array.Empty<ulong>()
            };
            return defaultData;
        }

        /// <summary>
        /// Makes a new instance of <see cref="SaveDataType"/> for the current state or null if there is nothing to save.
        /// </summary>
        public object GetSaveData()
        {
            FlushActiveInstances();

            // We save active loot and preserve inactive loot for some time
            var thresholdValue = MakeLootThresholdExpirationValue();
            var lootToSave = savedLoot.Values.Where(
                loot => activeLootSerializers.ContainsKey(loot.loadID) || loot.stockedDate >= thresholdValue
                );

            LocationSaveData_v1 data = new LocationSaveData_v1
            {
                lootContainers = lootToSave.ToArray(),
                activeEnemies = activeEnemySerializers.Values
                    .Select(serializer => (EnemyData_v1)serializer.GetSaveData()).ToArray(),
                clearedEnemies = clearedEnemies.ToArray()
            };

            return data;
        }

        /// <summary>
        /// Restores retrieved data when a save is loaded.
        /// </summary>
        /// <param name="saveData">An instance of <see cref="SaveDataType"/>.</param>
        public void RestoreSaveData(object saveData)
        {
            LocationSaveData_v1 data = (LocationSaveData_v1)saveData;

            savedLoot = data.lootContainers.ToDictionary(loot => loot.loadID);
            foreach (ulong clearedEnemy in data.clearedEnemies)
                clearedEnemies.Add(clearedEnemy);

            ReloadActiveInstances(data.activeEnemies);
        }
    }
}