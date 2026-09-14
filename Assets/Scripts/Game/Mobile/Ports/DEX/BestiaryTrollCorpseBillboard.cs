// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiaryTrollCorpseBillboard.cs, copied unchanged for iOS. No [Invoke], no edits.
// MOBILE: this file's .meta keeps the UPSTREAM guid (4e2a5cd7ea09df248b37d80b8dd43550). The
// shipped prefab Prefabs/1618_20.prefab references it by that guid, and a strip_code bundle has
// no code of its own: without the guid the prefab builds with a missing script and the troll
// corpse is an inert billboard.
//
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using UnityEngine;
using UnityEngine.Rendering;

namespace DaggerfallBestiaryProject
{
    [ImportedComponent]
    public class BestiaryTrollCorpseBillboard : Billboard
    {
        public int Archive = 1618;
        public int Record = 20;
        public int AshArchive = 1618;
        public int AshRecord = 21;

        public float RespawnTime = 10.0f;

        public override int FramesPerSecond { get; set; }
        public override bool OneShot { get; set; }
        public override bool FaceY { get; set; }

        private bool aligned = false;

        Camera mainCamera = null;
        MeshRenderer meshRenderer;

        public ulong LoadID { get; private set; }

        bool postParentedSetup = false;
        BestiaryTrollProperties enemyProperties = new BestiaryTrollProperties();

        public override void AlignToBase()
        {
            if (aligned)
                return;

            Vector3 offset = Vector3.zero;

            // MeshReplacement.AlignToBase lowers custom billboard prefabs in dungeons for some reason
            // Just put them back up
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
            {
                int height = ImageReader.GetImageData(TextureFile.IndexToFileName(Archive), Record, createTexture: false).height;
                offset.y += height / 2 * MeshReader.GlobalScale;
            }

            // Calculate offset for correct positioning in scene
            offset.y += (summary.Size.y / 2);
            transform.position += offset;

            aligned = true;
        }

        public override Material SetMaterial(int archive, int record, int frame = 0)
        {
            // Get DaggerfallUnity
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return null;

            // Get references
            meshRenderer = GetComponent<MeshRenderer>();

            Vector2 size;
            Vector2 scale;
            Mesh mesh = null;
            Material material = null;
            if (material = TextureReplacement.GetStaticBillboardMaterial(gameObject, archive, record, ref summary, out scale))
            {
                mesh = dfUnity.MeshReader.GetBillboardMesh(summary.Rect, archive, record, out size);
                size *= scale;
                summary.AtlasedMaterial = false;
            }
            else if (dfUnity.MaterialReader.AtlasTextures)
            {
                material = dfUnity.MaterialReader.GetMaterialAtlas(
                    archive,
                    0,
                    4,
                    2048,
                    out summary.AtlasRects,
                    out summary.AtlasIndices,
                    4,
                    true,
                    0,
                    false,
                    true);
                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.AtlasRects[summary.AtlasIndices[record].startIndex],
                    archive,
                    record,
                    out size);
                summary.AtlasedMaterial = true;
            }
            else
            {
                material = dfUnity.MaterialReader.GetMaterial(
                    archive,
                    record,
                    0,
                    0,
                    out summary.Rect,
                    4,
                    true,
                    true);
                mesh = dfUnity.MeshReader.GetBillboardMesh(
                    summary.Rect,
                    archive,
                    record,
                    out size);
                summary.AtlasedMaterial = false;
            }

            // Set summary
            summary.FlatType = MaterialReader.GetFlatType(archive);
            summary.Archive = archive;
            summary.Record = record;
            summary.Size = size;

            // Assign mesh and material
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh oldMesh = meshFilter.sharedMesh;
            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }
            if (oldMesh)
            {
                // The old mesh is no longer required
#if UNITY_EDITOR
                DestroyImmediate(oldMesh);
#else
                Destroy(oldMesh);
#endif
            }

            // General billboard shadows if enabled
            bool isLightArchive = (archive == TextureReader.LightsTextureArchive);
            meshRenderer.shadowCastingMode = (DaggerfallUnity.Settings.GeneralBillboardShadows && !isLightArchive) ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;

            return material;
        }

        public override Material SetMaterial(Texture2D texture, Vector2 size, bool isLightArchive = false)
        {
            // Get DaggerfallUnity
            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
            if (!dfUnity.IsReady)
                return null;

            // Get references
            meshRenderer = GetComponent<MeshRenderer>();

            // Create material
            Material material = MaterialReader.CreateBillboardMaterial();
            material.mainTexture = texture;

            // Create mesh
            Mesh mesh = dfUnity.MeshReader.GetSimpleBillboardMesh(size);

            // Set summary
            summary.FlatType = FlatTypes.Decoration;
            summary.Size = size;

            // Assign mesh and material
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh oldMesh = meshFilter.sharedMesh;
            if (mesh)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
            }
            if (oldMesh)
            {
                // The old mesh is no longer required
#if UNITY_EDITOR
                DestroyImmediate(oldMesh);
#else
                Destroy(oldMesh);
#endif
            }

            // General billboard shadows if enabled
            meshRenderer.shadowCastingMode = (DaggerfallUnity.Settings.GeneralBillboardShadows && !isLightArchive) ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;

            return material;
        }

        public override void SetRDBResourceData(DFBlock.RdbFlatResource resource)
        {
            // Add common data
            summary.Flags = resource.Flags;
            summary.FactionOrMobileID = (int)resource.FactionOrMobileId;
            summary.FixedEnemyType = MobileTypes.None;

            // TEMP: Add name seed
            summary.NameSeed = (int)resource.Position;

            // Set data of fixed mobile types (e.g. non-random enemy spawn)
            if (resource.TextureArchive == 199)
            {
                if (resource.TextureRecord == 16)
                {
                    summary.IsMobile = true;
                    summary.EditorFlatType = EditorFlatTypes.FixedMobile;
                    summary.FixedEnemyType = (MobileTypes)(summary.FactionOrMobileID & 0xff);
                }
                else if (resource.TextureRecord == 10) // Start marker. Holds data for dungeon block water level and castle block status.
                {
                    if (resource.SoundIndex != 0)
                        summary.WaterLevel = (short)(-8 * resource.SoundIndex);
                    else
                        summary.WaterLevel = 10000; // no water

                    summary.CastleBlock = (resource.Magnitude != 0);
                }
            }
        }

        public override void SetRMBPeopleData(DFBlock.RmbBlockPeopleRecord person)
        {
            SetRMBPeopleData(person.FactionID, person.Flags, person.Position);
        }

        public override void SetRMBPeopleData(int factionID, int flags, long position = 0)
        {
            // Add common data
            summary.FactionOrMobileID = factionID;
            summary.FixedEnemyType = MobileTypes.None;
            summary.Flags = flags;

            // TEMP: Add name seed
            summary.NameSeed = (int)position;
        }

        public void SetEnemyProperties(DaggerfallEntityBehaviour enemyBehaviour, MobileUnit enemyMobile)
        {
            enemyProperties.MobileID = enemyMobile.Enemy.ID;
            enemyProperties.BillboardHeight = enemyMobile.Summary.RecordSizes[0].y;
            enemyProperties.AshCorpseArchive = AshArchive;
            enemyProperties.AshCorpseRecord = AshRecord;
        }

        public void PostParentedSetup()
        {
            if (postParentedSetup)
                return;

            // This troll is no loot
            var lootObject = transform.parent.gameObject;
            var lootComponent = lootObject.GetComponent<DaggerfallLoot>();
            if (lootComponent != null)
            {
                Destroy(lootComponent);

                foreach (var collider in lootObject.GetComponents<Collider>())
                {
                    Destroy(collider);
                }
            }

            var existingSerializer = GetComponentInParent<SerializableLootContainer>();
            if (existingSerializer != null)
            {
                LoadID = existingSerializer.LoadID;
                Destroy(existingSerializer);
            }
            else
            {
                LoadID = DaggerfallUnity.NextUID;
            }

            var entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            if (entityBehaviour)
            {
                var trollCorpseEntity = new BestiaryTrollCorpseEntity(entityBehaviour, enemyProperties);
                trollCorpseEntity.RespawnTime = RespawnTime;
                trollCorpseEntity.CorpseBillboardHeight = summary.Size.y;
                trollCorpseEntity.SetEntityDefaults();
                entityBehaviour.Entity = trollCorpseEntity;
            }

            postParentedSetup = true;
        }

        // Start is called before the first frame update
        void Start()
        {
            if (Application.isPlaying)
            {
                // Get component references
                mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
                meshRenderer = GetComponent<MeshRenderer>();

                // Hide editor marker from live scene
                bool showEditorFlats = GameManager.Instance.StartGameBehaviour.ShowEditorFlats;
                if (summary.FlatType == FlatTypes.Editor && meshRenderer && !showEditorFlats)
                {
                    // Just disable mesh renderer as actual object can be part of action chain
                    // Example is the treasury in Daggerfall castle, some action records flow through the quest item marker
                    meshRenderer.enabled = false;
                }

                SetMaterial(Archive, Record);
                AlignToBase();
            }

            PostParentedSetup();
        }

        // Update is called once per frame
        void Update()
        {
            // Rotate to face camera in game
            // Do not rotate if MeshRenderer disabled. The player can't see it anyway and this could be a hidden editor marker with child objects.
            // In the case of hidden editor markers with child treasure objects, we don't want a 3D replacement spinning around like a billboard.
            // Treasure objects are parented to editor marker in this way as the moving action data for treasure is actually on editor marker parent.
            // Visible child of treasure objects have their own MeshRenderer and DaggerfallBillboard to apply rotations.
            if (mainCamera && Application.isPlaying && meshRenderer.enabled)
            {
                Vector3 viewDirection = -new Vector3(mainCamera.transform.forward.x, 0, mainCamera.transform.forward.z);
                transform.LookAt(transform.position + viewDirection);
            }
        }
    }
}