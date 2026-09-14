// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiaryTrollCorpseEntity.cs, copied unchanged for iOS. No [Invoke], no edits.
//
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace DaggerfallBestiaryProject
{
    public struct BestiaryTrollProperties
    {
        public int MobileID;
        public float BillboardHeight;
        public int AshCorpseArchive;
        public int AshCorpseRecord;
    }

    public class BestiaryTrollCorpseEntity : DaggerfallEntity
    {
        BestiaryTrollProperties enemyProperties;

        public float RespawnTime = 5.0f;
        public float CorpseBillboardHeight = 1.0f;
        public float RespawnBuffer { get { return respawnBuffer; } }
        public BestiaryTrollProperties EnemyProperties { get { return enemyProperties; } }


        float respawnBuffer;
        bool entityStarted = false;

        public BestiaryTrollCorpseEntity(DaggerfallEntityBehaviour entityBehaviour, BestiaryTrollProperties trollProperties)
            : base(entityBehaviour)
        {
            enemyProperties = trollProperties;
        }

        public override void SetEntityDefaults()
        {
            MaxHealth = 1;
            CurrentHealth = 1;

            if(BestiaryMod.Instance.CustomEnemies.TryGetValue(enemyProperties.MobileID, out BestiaryMod.CustomEnemyProperties customEnemy))
            {
                if(BestiaryMod.Instance.CustomCareers.TryGetValue(customEnemy.career, out BestiaryMod.CustomCareer customCareer))
                {
                    career = customCareer.dfCareer;
                }
            }

            respawnBuffer = RespawnTime;
            entityStarted = true;
        }

        public override int SetHealth(int amount, bool restoreMode = false)
        {
            var currentHealth = base.SetHealth(amount, restoreMode);
            if(currentHealth <= 0)
            {
                // Show death message
                string deathMessage = TextManager.Instance.GetLocalizedText("thingJustDied");
                deathMessage = deathMessage.Replace("%s", TextManager.Instance.GetLocalizedEnemyName(enemyProperties.MobileID));
                if (!DaggerfallUnity.Settings.DisableEnemyDeathAlert)
                    DaggerfallUI.Instance.PopupMessage(deathMessage);

                // Generate corpse
                GameObject corpse = GameObjectHelper.CreateDaggerfallBillboardGameObject(enemyProperties.AshCorpseArchive, enemyProperties.AshCorpseRecord, GameObjectHelper.GetBestParent());
                corpse.transform.position = EntityBehaviour.transform.position + new Vector3(0.0f, -CorpseBillboardHeight / 2.0f + 0.1f, 0.0f);

                Object.Destroy(EntityBehaviour.transform.parent.gameObject);
            }

            return currentHealth;
        }

        public override void Update(DaggerfallEntityBehaviour sender)
        {
            if (!entityStarted)
                return;

            if (BestiaryMod.DisableTrollRespawn)
                return;

            respawnBuffer -= Time.deltaTime;

            if(respawnBuffer <= 0.0f)
            {
                var enemyParent = GameObjectHelper.GetBestParent();

                string enemyName = TextManager.Instance.GetLocalizedEnemyName(enemyProperties.MobileID);

                var go = GameObjectHelper.CreateEnemy(
                    enemyName,
                    (MobileTypes)enemyProperties.MobileID,
                    Vector3.zero,
                    MobileGender.Unspecified, enemyParent);

                // Spawn at the same position, adjusting for billboard height differences
                go.transform.position = EntityBehaviour.transform.position + new Vector3(0, enemyProperties.BillboardHeight - CorpseBillboardHeight, 0);

                Object.Destroy(EntityBehaviour.transform.parent.gameObject);

                DaggerfallUI.AddHUDText($"The {enemyName} has recovered from its injuries.");
            }
        }

        public void RestoreSaveData(in BestiaryTrollCorpseData_v1 data)
        {
            respawnBuffer = data.RespawnBuffer;
            enemyProperties.MobileID = data.MobileID;
            enemyProperties.BillboardHeight = data.BillboardHeight;
            CorpseBillboardHeight = data.CorpseBillboardHeight;
        }
    }
}
