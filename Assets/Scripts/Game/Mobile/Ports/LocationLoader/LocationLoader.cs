// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// MOBILE PORT - also: github.com/drcarademono/DFU-LocationLoader @ 896a5741e5c9badd47fbb6c0f9926a95a79cb776
//   (branch rmb-object) - object type 5 (RMB block) backport only.
// File LocationLoader.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using System;
using System.Runtime.CompilerServices;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Entity;
using static DaggerfallWorkshop.Utility.ContentReader;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallConnect;
using DaggerfallConnect.Arena2; // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)

namespace LocationLoader
{
    public class LocationLoader : MonoBehaviour
    {
        Dictionary<Vector2Int, WeakReference<DaggerfallTerrain>> loadedTerrain = new Dictionary<Vector2Int, WeakReference<DaggerfallTerrain>>();
        Dictionary<Vector2Int, List<LocationData>> pendingIncompleteLocations = new Dictionary<Vector2Int, List<LocationData>>();
        Dictionary<ulong, List<Vector2Int>> instancePendingTerrains = new Dictionary<ulong, List<Vector2Int>>();

        public class LLTerrainData
        {
            public List<Rect> LocationInstanceRects = new List<Rect>();
            public List<LocationData> LocationInstances = new List<LocationData>();
        }

        private ConditionalWeakTable<DaggerfallTerrain, LLTerrainData> terrainExtraData =
            new ConditionalWeakTable<DaggerfallTerrain, LLTerrainData>();

        LocationResourceManager resourceManager;

        // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)
        // Block names whose RMB layout already threw once. Farms repeat tens of thousands of times, so a
        // broken block must not fill the log on every terrain-streaming step.
        static readonly HashSet<string> failedRmbBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // MOBILE: end backport

        public const int TERRAIN_SIZE = 128;
        public const int ROAD_WIDTH = 4; // Actually 2, but let's leave a bit of a gap
        public const float TERRAINPIXELSIZE = 819.2f;
        public const float TERRAIN_SIZE_MULTI = TERRAINPIXELSIZE / TERRAIN_SIZE;

        bool sceneLoading = false;
        private ulong lastLocationId = 0;

        public static int LootExpirationDays => 7;

        void Start()
        {
            Debug.Log("Begin mod init: Location Loader");

            LocationConsole.RegisterCommands();
            LocationRMBVariant.RegisterCommands();
            resourceManager = GetComponent<LocationResourceManager>();

            Debug.Log("Finished mod init: Location Loader");
        }

        private void OnEnable()
        {
            DaggerfallTerrain.OnPromoteTerrainData += OnTerrainPromoted;
            StreamingWorld.OnInitWorld += StreamingWorld_OnInitWorld;
            StreamingWorld.OnUpdateTerrainsEnd += StreamingWorld_OnUpdateTerrainsEnd;
            LocationData.OnLocationEnabled += LocationData_OnLocationEnabled;
        }

        private void OnDisable()
        {
            DaggerfallTerrain.OnPromoteTerrainData -= OnTerrainPromoted;
            StreamingWorld.OnInitWorld -= StreamingWorld_OnInitWorld;
            StreamingWorld.OnUpdateTerrainsEnd -= StreamingWorld_OnUpdateTerrainsEnd;
            LocationData.OnLocationEnabled -= LocationData_OnLocationEnabled;
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (!game.StateManager.GameInProgress)
                return;

            CheckCurrentLocation();
        }

        void CheckCurrentLocation()
        {
            var game = GameManager.Instance;

            if (game.IsPlayerInside)
                return;

            // When near an instance, activate it
            var playerGps = game.PlayerGPS;
            var mapPixel = playerGps.CurrentMapPixel;
            if (!TryGetTerrainExtraData(new Vector2Int(mapPixel.X, mapPixel.Y), out LLTerrainData terrainData))
                return;

            if (terrainData.LocationInstances.Count == 0)
                return;

            // World coords are 256 values per terrain tile, or 32768 per map pixel (256*128)
            var playerTerrainX = (playerGps.WorldX % 32768) / 256;
            var playerTerrainY = (playerGps.WorldZ % 32768) / 256;

            const int extraRect = 1; // Add one terrain tile around each instance
            foreach (var instance in terrainData.LocationInstances)
            {
                var loc = instance.Location;
                var prefab = instance.Prefab;

                var instanceMinX = loc.terrainX - prefab.HalfWidth - extraRect;
                var instanceMinY = loc.terrainY - prefab.HalfHeight - extraRect;
                var instanceMaxX = loc.terrainX + prefab.HalfWidth + extraRect;
                var instanceMaxY = loc.terrainY + prefab.HalfHeight + extraRect;

                if (playerTerrainX >= instanceMinX
                    && playerTerrainX <= instanceMaxX
                    && playerTerrainY >= instanceMinY
                    && playerTerrainY <= instanceMaxY)
                {
                    if (loc.locationID != lastLocationId)
                    {
                        // Activate location

                        foreach (var lootSerializer in instance.LocationLoots)
                        {
                            if (!lootSerializer.Activated)
                            {
                                LocationHelper.GenerateLoot(lootSerializer.loot);
                                lootSerializer.Activated = true;
                            }
                        }

                        lastLocationId = loc.locationID;
                    }

                    return;
                }
            }
        }

        private void StreamingWorld_OnInitWorld()
        {
            sceneLoading = true;
        }

        private void StreamingWorld_OnUpdateTerrainsEnd()
        {
            if(sceneLoading)
            {
                StartCoroutine(InstantiateAllDynamicObjectsNextFrame());
            }
        }

        private void LocationData_OnLocationEnabled(object sender, EventArgs _)
        {
            if(!sceneLoading)
            {
                LocationData instance = sender as LocationData;
                // Ignore embedded instances (where location instance is null)
                if(instance != null && !instance.IsEmbeddedLocation && !instance.HasSpawnedDynamicObjects)
                {
                    InstantiateInstanceDynamicObjects(instance);
                }
            }
        }

        System.Collections.IEnumerator InstantiateAllDynamicObjectsNextFrame()
        {
            yield return new WaitForEndOfFrame();

            var instances = FindObjectsOfType<LocationData>();
            foreach (var instance in instances)
            {
                // Ignore embedded prefabs
                // Or instances which already have dynamic objects spawned
                if (instance.IsEmbeddedLocation || instance.HasSpawnedDynamicObjects)
                    continue;

                InstantiateInstanceDynamicObjects(instance);
            }

            sceneLoading = false;

            yield break;
        }

        public bool TryGetTerrain(int worldX, int worldY, out DaggerfallTerrain terrain)
        {
            var worldCoord = new Vector2Int(worldX, worldY);
            return TryGetTerrain(worldCoord, out terrain);
        }

        public bool TryGetTerrain(Vector2Int worldCoord, out DaggerfallTerrain terrain)
        {
            if (loadedTerrain.TryGetValue(worldCoord, out WeakReference<DaggerfallTerrain> terrainReference))
            {
                if (terrainReference.TryGetTarget(out terrain))
                {
                    // Terrain has been pooled and placed somewhere else
                    // Happens with Distant Terrain
                    if (terrain.MapPixelX != worldCoord.x || terrain.MapPixelY != worldCoord.y)
                    {
                        loadedTerrain.Remove(worldCoord);
                        return false;
                    }
                    return true;
                }
                else
                {
                    loadedTerrain.Remove(worldCoord);
                }
            }

            terrain = null;
            return false;
        }

        public bool TryGetTerrainExtraData(Vector2Int worldCoord, out LLTerrainData extraData)
        {
            if(TryGetTerrain(worldCoord, out DaggerfallTerrain terrain))
            {
                return terrainExtraData.TryGetValue(terrain, out extraData);
            }

            extraData = null;
            return false;
        }

        void InstantiateInstanceDynamicObjects(LocationData locationData)
        {
            if(!locationData)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects: location data was null");
                return;
            }

            if(locationData.IsEmbeddedLocation)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects: location was embedded");
                return;
            }

            LocationPrefab locationPrefab = locationData.Prefab;
            if (locationPrefab == null)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects: prefab was null");
                return;
            }

            LocationInstance loc = locationData.Location;
            if (loc == null || string.IsNullOrEmpty(loc.prefab))
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects: instance was null or invalid");
                return;
            }

            GameObject instance = locationData.gameObject;
            if(!instance)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects at ({loc.worldX}, {loc.worldY}): GameObject was null");
                return;
            }

            if(!LocationModLoader.modObject)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects at ({loc.worldX}, {loc.worldY}): mod object was null");
                return;
            }

            var saveInterface = LocationModLoader.modObject.GetComponent<LocationSaveDataInterface>();
            if (!saveInterface)
            {
                Debug.LogError($"[LL] Failed to spawn dynamic objects at ({loc.worldX}, {loc.worldY}): save interface was null");
                return;
            }

            foreach (LocationObject obj in locationPrefab.obj)
            {
                if (obj == null)
                {
                    Debug.LogError($"[LL] Failed to spawn dynamic object at ({loc.worldX}, {loc.worldY}) on prefab '{loc.prefab}': obj was null");
                    continue;
                }

                if(string.IsNullOrEmpty(obj.name))
                {
                    Debug.LogError($"[LL] Failed to spawn dynamic object at ({loc.worldX}, {loc.worldY}) on prefab '{loc.prefab}': obj had null name");
                    continue;
                }

                GameObject go = null;

                if (obj.type == 1)
                {
                    go = LocationHelper.LoadStaticObject(
                        obj.type,
                        obj.name,
                        instance.transform,
                        obj.pos,
                        obj.rot,
                        obj.scale
                        );
                }
                else if (obj.type == 2)
                {
                    string[] arg = obj.name.Split('.');

                    if (arg == null || arg.Length != 2)
                    {
                        Debug.LogError($"[LL] Invalid type 2 obj name '{obj.name}' in prefab '{loc.prefab}'");
                        continue;
                    }

                    if (arg[0] == "199")
                    {
                        switch (arg[1])
                        {
                            case "16":
                                object result = SaveLoadManager.Deserialize(typeof(EnemyMarkerExtraData), obj.extraData);
                                if(result == null)
                                {
                                    Debug.LogError($"[LL] Could not spawn enemy in prefab '{loc.prefab}': invalid extra data");
                                    continue;
                                }

                                var extraData = (EnemyMarkerExtraData)result;
                                if (!Enum.IsDefined(typeof(MobileTypes), extraData.EnemyId) && DaggerfallEntity.GetCustomCareerTemplate(extraData.EnemyId) == null)
                                {
                                    Debug.LogError($"[LL] Could not spawn enemy in prefab '{loc.prefab}', unknown mobile type '{extraData.EnemyId}'");
                                    continue;
                                }

                                ulong v = (uint)obj.objectID;
                                ulong loadId = LocationSaveDataInterface.ToObjectLoadId(loc.locationID, obj.objectID);

                                // Enemy is dead, don't spawn anything

                                if (saveInterface.IsEnemyDead(loadId))
                                {
                                    break;
                                }

                                MobileTypes mobileType = (MobileTypes)extraData.EnemyId;

                                go = GameObjectHelper.CreateEnemy(TextManager.Instance.GetLocalizedEnemyName((int)mobileType), mobileType, obj.pos, MobileGender.Unspecified, instance.transform);
                                if(!go)
                                {
                                    Debug.LogError($"[LL] Could not spawn enemy in prefab '{loc.prefab}': GameObject.CreateEnemy returned null");
                                    continue;
                                }

                                SerializableEnemy serializable = go.GetComponent<SerializableEnemy>();
                                if (serializable)
                                {
                                    Destroy(serializable);
                                }

                                DaggerfallEntityBehaviour behaviour = go.GetComponent<DaggerfallEntityBehaviour>();
                                if(!behaviour)
                                {
                                    Debug.LogError($"[LL] Failed to spawn enemy at ({loc.worldX}, {loc.worldY}) on prefab '{loc.prefab}': behaviour was null");
                                    continue;
                                }

                                EnemyEntity entity = (EnemyEntity)behaviour.Entity;
                                if (entity == null)
                                {
                                    Debug.LogError($"[LL] Failed to spawn enemy at ({loc.worldX}, {loc.worldY}) on prefab '{loc.prefab}': entity was null");
                                    continue;
                                }

                                if (entity.MobileEnemy.Gender == MobileGender.Male)
                                {
                                    entity.Gender = Genders.Male;
                                }
                                else if (entity.MobileEnemy.Gender == MobileGender.Female)
                                {
                                    entity.Gender = Genders.Female;
                                }

                                if (extraData.TeamOverride != 0 && Enum.IsDefined(typeof(MobileTeams), extraData.TeamOverride))
                                {
                                    entity.Team = (MobileTeams)extraData.TeamOverride;
                                }

                                DaggerfallEnemy enemy = go.GetComponent<DaggerfallEnemy>();
                                if (!enemy)
                                {
                                    Debug.LogError($"[LL] Failed to spawn enemy at ({loc.worldX}, {loc.worldY}) on prefab '{loc.prefab}': no enemy component");
                                    continue;
                                }

                                enemy.LoadID = loadId;
                                var serializer = go.AddComponent<LocationEnemySerializer>();

                                locationData.AddEnemy(serializer);

                                break;

                            case "19":
                                {
                                    int iconIndex = UnityEngine.Random.Range(0, DaggerfallLootDataTables.randomTreasureIconIndices.Length);
                                    int iconRecord = DaggerfallLootDataTables.randomTreasureIconIndices[iconIndex];
                                    go = LocationHelper.CreateLootContainer(loc.locationID, obj.objectID, 216, iconRecord, instance.transform);
                                    if (!go)
                                    {
                                        Debug.LogError($"[LL] Could not spawn treasure in prefab '{loc.prefab}': LocationHelper.CreateLootContainer returned null");
                                        continue;
                                    }

                                    go.transform.localPosition = obj.pos;

                                    locationData.AddLoot(go.GetComponent<LocationLootSerializer>());

                                    break;
                                }
                        }
                    }
                }
                // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)
                else if (obj.type == LocationObject.TypeRMB)
                {
                    // MOBILE: try/catch is an iOS-side safety net. Upstream lets an RMB layout failure
                    // throw out of the whole spawn loop, which on a streaming terrain would drop every
                    // later object in the prefab too. Log once per block name and carry on.
                    // MOBILE: hoisted out of the try so the catch can destroy a half-built block.
                    GameObject rmbBlock = null;
                    try
                    {
                        //if (blocksFile.GetBlockIndex(obj.name) == -1)
                            //WorldDataReplacement.AssignNextIndex(obj.name); Commented out for until PR accepted

                        // MOBILE: a name with no block index can never resolve - the WorldData override is
                        // only reachable through BlocksFile.GetBlock(int), and indices for non-BSA names are
                        // minted only by WorldDataReplacement.AssignBlockIndices. GetBlockData would still
                        // return true with a zeroed DFBlock, so CreateBaseGameObject builds a GameObject and
                        // then throws inside AddModels. Skip by name instead, with a message that says why.
                        if (DaggerfallUnity.Instance.ContentReader.BlockFileReader.GetBlockIndex(obj.name) == -1)
                        {
                            if (failedRmbBlocks.Add(obj.name))
                                Debug.LogWarning($"[LL] RMB block {obj.name} skipped: not in BLOCKS.BSA and no block index (WorldData override unreachable)");
                            continue;
                        }

                        // Step 1: Get the player's current climate index
                        int currentClimateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;

                        // Step 2: Use MapsFile to get the climate settings based on the current climate index
                        var climateSettings = MapsFile.GetWorldClimateSettings(currentClimateIndex);

                        // Step 3: Convert DFLocation.ClimateBaseType to ClimateBases using ClimateSwaps
                        ClimateBases climateBase = ClimateSwaps.FromAPIClimateBase(climateSettings.ClimateType);

                        // Step 4: Convert DFLocation.ClimateTextureSet to ClimateNatureSets using ClimateSwaps
                        ClimateNatureSets climateNature = ClimateSwaps.FromAPITextureSet(climateSettings.NatureSet);

                        // Step 5: Determine the season (set to Winter if it's currently Winter; otherwise, use Summer)
                        ClimateSeason climateSeason = (DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
                            ? ClimateSeason.Winter
                            : ClimateSeason.Summer;

                        // Create the RMB block game object
                        DFBlock blockData;
                        rmbBlock = RMBLayout.CreateBaseGameObject(obj.name, layoutX: 0, layoutY: 0, out blockData); // MOBILE: assigns the hoisted local

                        // Add the ground plane with the determined climate base and season
                        //if (obj.groundPlane == true) RMBLayout.AddGroundPlane(ref blockData, rmbBlock.transform, climateBase, climateSeason);

                        // Add nature flats with the determined nature set and season
                        RMBLayout.AddNatureFlats(ref blockData, rmbBlock.transform, null, climateNature, climateSeason);

                        // Add Lights with billboard batching or custom lighting
                        RMBLayout.AddLights(ref blockData, rmbBlock.transform, rmbBlock.transform, null);

                        // Add other block flats, like animals and NPCs
                        RMBLayout.AddMiscBlockFlats(ref blockData, rmbBlock.transform, mapId: 0, locationIndex: 0);

                        // Add Exterior Block Flats
                        RMBLayout.AddExteriorBlockFlats(ref blockData, rmbBlock.transform, rmbBlock.transform, mapId: 0, locationIndex: 0, climateNature, climateSeason);

                        // Place and set up the final RMB block in the scene
                        rmbBlock.transform.parent = instance.transform;
                        rmbBlock.transform.localPosition = obj.pos;
                        rmbBlock.transform.localRotation = obj.rot;
                        rmbBlock.transform.localScale = obj.scale;

                        // MOBILE: WoD Biomes' subtropical nature swap for freshly built RMB blocks
                        // (carademono's LL rmb-object branch; the map comes from the compiled-in Biomes
                        // port). ApplySwaps swallows its own exceptions, so it cannot cost this block
                        // the destroy-and-skip in the catch below.
                        if (BiomesClimateSwap.ShouldSwap(DaggerfallWorkshop.Game.Mobile.MobilePortedMods.BiomesRunning,
                                                         WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap))
                            BiomesClimateSwap.ApplySwaps(rmbBlock);
                    }
                    catch (Exception ex)
                    {
                        // MOBILE: CreateBaseGameObject builds the block and its "Models" child before any
                        // of the layout calls can throw, and the reparent is the last statement in the try.
                        // Without this a throw leaves a fully-built, unparented block at the scene root that
                        // terrain unload never reclaims, repeating on every stream-in.
                        if (rmbBlock != null)
                            UnityEngine.Object.Destroy(rmbBlock);
                        if (failedRmbBlocks.Add(obj.name))
                            Debug.LogWarning($"[LL] RMB block {obj.name} failed: {ex.Message}");
                        continue;
                    }
                }
                // MOBILE: end backport

                if (go)
                {
                    var billboard = go.GetComponent<Billboard>();
                    if (billboard)
                    {
                        float tempY = go.transform.position.y;
                        billboard.AlignToBase();
                        var objectTransform = go.transform;
                        var position = objectTransform.position;
                        objectTransform.position = new Vector3(position.x, tempY + ((position.y - tempY) * objectTransform.localScale.y), position.z);
                    }
                }
            }

            locationData.HasSpawnedDynamicObjects = true;
        }

        Vector3 GetLocationPosition(LocationData locationData, DaggerfallTerrain daggerTerrain)
        {
            var loc = locationData.Location;

            if (loc.type == 2 || loc.type == 3)
            {
                return new Vector3(loc.terrainX * TERRAIN_SIZE_MULTI, DaggerfallUnity.Instance.TerrainSampler.OceanElevation * daggerTerrain.TerrainScale, loc.terrainY * TERRAIN_SIZE_MULTI);
            }
            else
            {
                float terrainHeightMax = DaggerfallUnity.Instance.TerrainSampler.MaxTerrainHeight * daggerTerrain.TerrainScale;
                float sinkOffset = Mathf.Lerp(0, locationData.HeightOffset, loc.sink);
                return new Vector3(loc.terrainX * TERRAIN_SIZE_MULTI, locationData.OverlapAverageHeight * terrainHeightMax + sinkOffset, loc.terrainY * TERRAIN_SIZE_MULTI);
            }
        }

        void SetActiveRecursively(GameObject go)
        {
            go.SetActive(true);
            foreach(Transform child in go.transform)
            {
                if (child.TryGetComponent(out LocationData _))
                {
                    child.gameObject.SetActive(true);
                    SetActiveRecursively(child.gameObject);
                }
            }
        }

        LocationData InstantiateTopLocationPrefab(string prefabName, float overlapAverageHeight, LocationPrefab locationPrefab, LocationInstance loc, DaggerfallTerrain daggerTerrain)
        {
            GameObject instance = resourceManager.InstantiateLocationPrefab(prefabName, locationPrefab, daggerTerrain.transform);

            LocationData data = instance.AddComponent<LocationData>();
            data.Location = loc;
            data.Prefab = locationPrefab;
            data.OverlapAverageHeight = overlapAverageHeight;

            if(loc.type == 1 && loc.sink > 0.0f)
            {
                FindAdjustedHeightOffset(data);
            }

            Vector3 terrainOffset = GetLocationPosition(data, daggerTerrain);
            instance.transform.localPosition = terrainOffset;
            instance.transform.localRotation = loc.rot;
            instance.transform.localScale = new Vector3(loc.scale, loc.scale, loc.scale);

            // Now that we have the LocationData, add it to "pending instances" if needed
            if(instancePendingTerrains.TryGetValue(loc.locationID, out List<Vector2Int> pendingTerrains))
            {
                foreach (Vector2Int terrainCoord in pendingTerrains)
                {
                    if(!pendingIncompleteLocations.TryGetValue(terrainCoord, out List<LocationData> terrainPendingLocations))
                    {
                        terrainPendingLocations = new List<LocationData>();
                        pendingIncompleteLocations.Add(terrainCoord, terrainPendingLocations);
                    }

                    terrainPendingLocations.Add(data);
                }
            }

            SetActiveRecursively(instance);

            // The "LocationData.OnEnabled" callback might or might not spawn dynamic objects before this point
            if (!sceneLoading && !data.HasSpawnedDynamicObjects)
            {
                InstantiateInstanceDynamicObjects(data);
            }

            return data;
        }

        bool IsInSnowFreeClimate(DaggerfallTerrain daggerTerrain)
        {
            int climateIndex = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY);
            return WeatherManager.IsSnowFreeClimate(climateIndex);
        }

        void OnTerrainPromoted(DaggerfallTerrain daggerTerrain, TerrainData terrainData)
        {
            Vector2Int worldLocation = new Vector2Int(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY);
            loadedTerrain[worldLocation] = new WeakReference<DaggerfallTerrain>(daggerTerrain);

            List<LocationData> terrainLocations = new List<LocationData>();

            // Terrain can be reused in terrain mods (ex: Distant Terrain)
            // Delete existing locations left on reused terrain
            foreach(var existingLoot in daggerTerrain.GetComponentsInChildren<LocationLootSerializer>())
            {
                existingLoot.InvalidateSave();
            }

            foreach (var existingEnemy in daggerTerrain.GetComponentsInChildren<LocationEnemySerializer>())
            {
                existingEnemy.InvalidateSave();
            }

            foreach (var existingLocation in daggerTerrain.GetComponentsInChildren<LocationData>())
            {
                Destroy(existingLocation.gameObject);
            }

            // Spawn the terrain's instances
            foreach (LocationInstance loc in resourceManager.GetTerrainInstances(daggerTerrain))
            {
                string context = $"location=\"{loc.name}\"";

                LocationPrefab locationPrefab = resourceManager.GetPrefabInfo(loc.prefab);
                if (locationPrefab == null)
                    continue;

                if (DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter
                    && !IsInSnowFreeClimate(daggerTerrain)
                    && !string.IsNullOrEmpty(locationPrefab.winterPrefab))
                {
                    var winterPrefab = resourceManager.GetPrefabInfo(locationPrefab.winterPrefab);
                    if (winterPrefab == null)
                        Debug.LogError($"Winter prefab '{locationPrefab.winterPrefab}' could not be loaded");
                    else
                        locationPrefab = winterPrefab;
                }

                if (LocationHelper.IsOutOfBounds(loc, locationPrefab))
                {
                    Debug.LogWarning($"Out-of-bounds location at ({daggerTerrain.MapPixelX}, {daggerTerrain.MapPixelY}) ({context})");
                    continue;
                }

                if (PruneInstance(loc, locationPrefab))
                {
                    continue;
                }

                if (loc.type == 2)
                {
                    // We find and adjust the type 2 instance position here
                    // So that terrain can be flattened in consequence
                    // If the current tile has no coast and adjacent terrain are not loaded,
                    // then we don't care about flattening, since it means the instance
                    // is gonna be on water at the edge of this tile anyway
                    if (FindNearestCoast(loc, daggerTerrain, out Vector2Int coastTileCoord))
                    {
                        loc.terrainX = coastTileCoord.x;
                        loc.terrainY = coastTileCoord.y;
                    }
                }

                int count = 0;
                float averageHeight = 0;

                var (halfWidth, halfHeight) = LocationHelper.GetHalfDimensions(loc, locationPrefab);

                int minX = Math.Max(loc.terrainX - halfWidth, 0);
                int minY = Math.Max(loc.terrainY - halfHeight, 0);
                int maxX = Math.Min(loc.terrainX + halfWidth, 128);
                int maxY = Math.Min(loc.terrainY + halfHeight, 128);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        averageHeight += daggerTerrain.MapData.heightmapSamples[y, x];
                        count++;
                    }
                }

                averageHeight /= count;

                var instantiatedLocation = InstantiateTopLocationPrefab(loc.prefab, averageHeight, locationPrefab, loc, daggerTerrain);
                if (instantiatedLocation != null)
                {
                    terrainLocations.Add(instantiatedLocation);
                }
            }

            // Check for pending instances waiting on this terrain
            if(pendingIncompleteLocations.TryGetValue(worldLocation, out List<LocationData> pendingLocations))
            {
                for(int i = 0; i < pendingLocations.Count; ++i)
                {
                    LocationData pendingLoc = pendingLocations[i];

                    if(pendingLoc == null)
                        // We got no info left on this instance
                        continue;

                    if(pendingLoc.IsEmbeddedLocation)
                    {
                        Debug.LogError($"[LL] Embedded location in pending incomplete locations at ({worldLocation.x}, {worldLocation.y})");
                        continue;
                    }

                        // Invalid locations?
                    if (!instancePendingTerrains.TryGetValue(pendingLoc.Location.locationID, out List<Vector2Int> pendingTerrains))
                        continue;

                    // Removes the instance from all "pending terrains"
                    void ClearPendingInstance()
                    {
                        foreach(Vector2Int pendingTerrainCoord in pendingTerrains)
                        {
                            if (pendingTerrainCoord == worldLocation)
                                continue;

                            if(pendingIncompleteLocations.TryGetValue(pendingTerrainCoord, out List<LocationData> pendingTerrainPendingInstances))
                            {
                                pendingTerrainPendingInstances.Remove(pendingLoc);
                                if (pendingTerrainPendingInstances.Count == 0)
                                    pendingIncompleteLocations.Remove(pendingTerrainCoord);
                            }
                        }

                        instancePendingTerrains.Remove(pendingLoc.Location.locationID);
                    }

                    if(!TryGetTerrain(pendingLoc.Location.worldX, pendingLoc.Location.worldY, out DaggerfallTerrain pendingLocTerrain))
                    {
                        // Terrain the location was on has expired
                        ClearPendingInstance();
                        continue;
                    }

                    // Type 2 location try to see if they found a coast to snap to
                    if (pendingLoc.Location.type == 2)
                    {
                        if (FindNearestCoast(pendingLoc.Location, pendingLocTerrain, out Vector2Int coastCoord))
                        {
                            pendingLoc.Location.terrainX = coastCoord.x;
                            pendingLoc.Location.terrainY = coastCoord.y;

                            pendingLoc.gameObject.transform.localPosition = GetLocationPosition(pendingLoc, pendingLocTerrain);

                            // Instance is not pending anymore
                            ClearPendingInstance();
                            continue;
                        }
                    }
                    // Adjust type 1 location height sink
                    else if(pendingLoc.Location.type == 1)
                    {
                        if (FindAdjustedHeightOffset(pendingLoc))
                        {
                            pendingLoc.gameObject.transform.localPosition = GetLocationPosition(pendingLoc, pendingLocTerrain);
                            // Instance is not pending anymore
                            ClearPendingInstance();
                            continue;
                        }
                        else
                        {
                            pendingLoc.gameObject.transform.localPosition = GetLocationPosition(pendingLoc, pendingLocTerrain);
                        }
                    }

                    // Remove this terrain from the location's pending terrains
                    pendingTerrains.Remove(worldLocation);
                }

                pendingIncompleteLocations.Remove(worldLocation);
            }

            if (BlendTerrain(daggerTerrain, terrainLocations))
            {
                terrainData.SetHeights(0, 0,
                    daggerTerrain.MapData.heightmapSamples); // Reset terrain data after heightmap samples change
            }

            LLTerrainData extraData = terrainExtraData.GetOrCreateValue(daggerTerrain);
            extraData.LocationInstanceRects.Clear();
            foreach (var location in terrainLocations)
            {
                if (location.Location.type == 0)
                {
                    extraData.LocationInstanceRects.Add(new Rect(location.Location.terrainX - location.Prefab.HalfWidth
                        , location.Location.terrainY - location.Prefab.HalfHeight
                        , location.Prefab.TerrainWidth
                        , location.Prefab.TerrainHeight));

                    extraData.LocationInstances.Add(location);
                }
            }
        }

        struct LocationRectData
        {
            public Rect rect;
            public float averageHeight;
        }

        bool BlendTerrain(DaggerfallTerrain daggerTerrain, List<LocationData> terrainLocations)
        {
            float transitionWidth = 10.0f;

            List<LocationRectData> locationRects = new List<LocationRectData>();

            foreach (LocationData loc in terrainLocations)
            {
                if (loc.Location.type == 0)
                {
                    LocationRectData locationRectData = new LocationRectData();
                    locationRectData.rect = new Rect(
                        loc.Location.terrainX - loc.Prefab.HalfWidth,
                        loc.Location.terrainY - loc.Prefab.HalfHeight,
                        loc.Prefab.TerrainWidth,
                        loc.Prefab.TerrainHeight
                    );
                    locationRectData.averageHeight = loc.OverlapAverageHeight;
                    locationRects.Add(locationRectData);
                }
            }

            // Ignore if we have no type 0 instances
            if (locationRects.Count == 0)
                return false;

            var locationRect = daggerTerrain.MapData.locationRect;
            bool hasDFLocation = locationRect.x > 0 && locationRect.y > 0;
            if (hasDFLocation)
            {
                LocationRectData locationRectData = new LocationRectData();
                locationRectData.rect = locationRect;

                float averageHeight = 0.0f;
                int count = 0;
                int minX = Mathf.FloorToInt(locationRect.xMin);
                int minY = Mathf.FloorToInt(locationRect.yMin);
                int maxX = Mathf.CeilToInt(locationRect.xMax);
                int maxY = Mathf.CeilToInt(locationRect.yMax);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        averageHeight += daggerTerrain.MapData.heightmapSamples[y, x];
                        count++;
                    }
                }

                averageHeight /= count;

                locationRectData.averageHeight = averageHeight;

                locationRects.Add(locationRectData);
            }

            for (int y = 1; y < TERRAIN_SIZE - 1; y++)
            {
                for (int x = 1; x < TERRAIN_SIZE - 1; x++)
                {
                    Vector2 point = new Vector2(x, y);

                    float averageHeight = 0.0f;
                    float currentDistance = 128.0f;
                    foreach (LocationRectData rect in locationRects)
                    {
                        float distance = GetDistanceFromRect(rect.rect, point);
                        if (distance == 0.0f)
                        {
                            currentDistance = 0.0f;
                            averageHeight = rect.averageHeight;
                            break;
                        }
                        else if (distance < currentDistance)
                        {
                            currentDistance = distance;
                            averageHeight = rect.averageHeight;
                        }
                    }

                    if (currentDistance < transitionWidth)
                    {
                        float factor = currentDistance / transitionWidth;
                        float originalHeight = daggerTerrain.MapData.heightmapSamples[y, x];
                        float blendedHeight = Mathf.Lerp(averageHeight, originalHeight, Mathf.SmoothStep(0.0f, 1.0f, factor));
                        daggerTerrain.MapData.heightmapSamples[y, x] = blendedHeight;
                    }
                }
            }

            return true;
        }

        bool PruneInstance(LocationInstance loc, LocationPrefab prefab)
        {
            foreach(var terrainSection in LocationHelper.GetOverlappingTerrainSections(loc, prefab))
            {
                Vector2Int worldCoord = terrainSection.WorldCoord;

                if(DaggerfallUnity.Instance.ContentReader.HasLocation(worldCoord.x, worldCoord.y, out MapSummary summary))
                {
                    // Check World Data locations only
                    if(WorldDataReplacement.GetDFLocationReplacementData(summary.RegionIndex, summary.MapIndex, out DFLocation wdLoc))
                    {
                        int locationWidth = wdLoc.Exterior.ExteriorData.Width;
                        int locationHeight = wdLoc.Exterior.ExteriorData.Height;
                        int locationX = (RMBLayout.RMBTilesPerTerrain - locationWidth * RMBLayout.RMBTilesPerBlock) / 2;
                        int locationY = (RMBLayout.RMBTilesPerTerrain - locationHeight * RMBLayout.RMBTilesPerBlock) / 2;
                        RectInt locationArea = new RectInt(locationX, locationY, locationWidth * RMBLayout.RMBTilesPerBlock, locationHeight * RMBLayout.RMBTilesPerBlock);

                        // Instance is on a World Data location. Prune it
                        if (locationArea.Overlaps(terrainSection.Section))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private float GetDistanceFromRect(Rect rect, Vector2 point)
        {
            float squared_dist = 0.0f;

            if (point.x > rect.xMax)
                squared_dist += (point.x - rect.xMax) * (point.x - rect.xMax);
            else if (point.x < rect.xMin)
                squared_dist += (rect.xMin - point.x) * (rect.xMin - point.x);

            if (point.y > rect.yMax)
                squared_dist += (point.y - rect.yMax) * (point.y - rect.yMax);
            else if (point.y < rect.yMin)
                squared_dist += (rect.yMin - point.y) * (rect.yMin - point.y);

            if (squared_dist == 0.0f)
                return 0.0f;

            return Mathf.Sqrt(squared_dist);
        }

        // Takes a type 2 Location instance and searches for the nearest coast it can snap to
        // Returns true if an answer was found
        // If false, and any surrounding terrains weren't loaded, it will be added to the list of instances waiting on terrain
        bool FindNearestCoast(LocationInstance loc, DaggerfallTerrain daggerTerrain, out Vector2Int tileCoord)
        {
            byte GetTerrainSample(DaggerfallTerrain terrain, int x, int y)
            {
                return terrain.MapData.tilemapSamples[x, y];
            }

            byte GetSample(int x, int y)
            {
                return GetTerrainSample(daggerTerrain, x, y);
            }

            byte initial = GetSample(loc.terrainX, loc.terrainY);

            // If we start in water
            if(initial == 0)
            {
                // Find non-water in any direction
                for(int i = 1;;++i)
                {
                    bool anyValid = false;

                    // North
                    if(loc.terrainY + i < 128)
                    {
                        if(GetSample(loc.terrainX, loc.terrainY + i) != 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX, loc.terrainY + i - 1);
                            return true;
                        }
                        anyValid = true;
                    }

                    // East
                    if (loc.terrainX + i < 128)
                    {
                        if (GetSample(loc.terrainX + i, loc.terrainY) != 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX + i - 1, loc.terrainY);
                            return true;
                        }
                        anyValid = true;
                    }

                    // South
                    if (loc.terrainY - i >= 0)
                    {
                        if (GetSample(loc.terrainX, loc.terrainY - i) != 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX, loc.terrainY - i + 1);
                            return true;
                        }
                        anyValid = true;
                    }

                    // West
                    if (loc.terrainX - i >= 0)
                    {
                        if (GetSample(loc.terrainX - i, loc.terrainY) != 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX - i + 1, loc.terrainY);
                            return true;
                        }
                        anyValid = true;
                    }

                    if (!anyValid)
                        break;
                }

                // Look the edges of adjacent terrain
                if (GetNorthNeighbor(daggerTerrain, out DaggerfallTerrain northNeighbor))
                {
                    if(GetTerrainSample(northNeighbor, loc.terrainX, 0) != 0)
                    {
                        tileCoord = new Vector2Int(loc.terrainX, 127);
                        return true;
                    }
                }

                if (GetEastNeighbor(daggerTerrain, out DaggerfallTerrain eastNeighbor))
                {
                    if (GetTerrainSample(eastNeighbor, 0, loc.terrainY) != 0)
                    {
                        tileCoord = new Vector2Int(127, loc.terrainY);
                        return true;
                    }
                }

                if (GetSouthNeighbor(daggerTerrain, out DaggerfallTerrain southNeighbor))
                {
                    if (GetTerrainSample(southNeighbor, loc.terrainX, 127) != 0)
                    {
                        tileCoord = new Vector2Int(loc.terrainX, 0);
                        return true;
                    }
                }

                if (GetWestNeighbor(daggerTerrain, out DaggerfallTerrain westNeighbor))
                {
                    if (GetTerrainSample(westNeighbor, 127, loc.terrainY) != 0)
                    {
                        tileCoord = new Vector2Int(0, loc.terrainY);
                        return true;
                    }
                }

                List<Vector2Int> pendingTerrain = new List<Vector2Int>();
                if(northNeighbor == null && loc.worldY != 0)
                {
                    pendingTerrain.Add(new Vector2Int(loc.worldX, loc.worldY - 1));
                }

                if (eastNeighbor == null && loc.worldX != 1000)
                {
                    pendingTerrain.Add(new Vector2Int(loc.worldX + 1, loc.worldY));
                }

                if (southNeighbor == null && loc.worldY != 500)
                {
                    pendingTerrain.Add(new Vector2Int(loc.worldX, loc.worldY + 1));
                }

                if (westNeighbor == null && loc.worldX != 0)
                {
                    pendingTerrain.Add(new Vector2Int(loc.worldX - 1, loc.worldY));
                }

                if (pendingTerrain.Count != 0)
                {
                    instancePendingTerrains[loc.locationID] = pendingTerrain;
                }
            }
            else
            {
                // Find water in any direction
                for (int i = 1; ; ++i)
                {
                    bool anyValid = false;

                    // North
                    if (loc.terrainY + i < 128)
                    {
                        if (GetSample(loc.terrainX, loc.terrainY + i) == 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX, loc.terrainY + i);
                            return true;
                        }
                        anyValid = true;
                    }

                    // East
                    if (loc.terrainX + i < 128)
                    {
                        if (GetSample(loc.terrainX + i, loc.terrainY) == 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX + i, loc.terrainY);
                            return true;
                        }
                        anyValid = true;
                    }

                    // South
                    if (loc.terrainY - i >= 0)
                    {
                        if (GetSample(loc.terrainX, loc.terrainY - i) == 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX, loc.terrainY - i);
                            return true;
                        }
                        anyValid = true;
                    }

                    // West
                    if (loc.terrainX - i >= 0)
                    {
                        if (GetSample(loc.terrainX - i, loc.terrainY) == 0)
                        {
                            tileCoord = new Vector2Int(loc.terrainX - i, loc.terrainY);
                            return true;
                        }
                        anyValid = true;
                    }

                    if (!anyValid)
                        break;
                }
            }

            tileCoord = new Vector2Int(loc.terrainX, loc.terrainY);
            return false;
        }

        bool GetNorthNeighbor(DaggerfallTerrain daggerTerrain, out DaggerfallTerrain northNeighbor)
        {
            if(daggerTerrain.MapPixelY == 0)
            {
                northNeighbor = null;
                return false;
            }

            return TryGetTerrain(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY - 1, out northNeighbor);
        }

        bool GetEastNeighbor(DaggerfallTerrain daggerTerrain, out DaggerfallTerrain eastNeighbor)
        {
            if (daggerTerrain.MapPixelX == 1000)
            {
                eastNeighbor = null;
                return false;
            }

            return TryGetTerrain(daggerTerrain.MapPixelX + 1, daggerTerrain.MapPixelY, out eastNeighbor);
        }

        bool GetSouthNeighbor(DaggerfallTerrain daggerTerrain, out DaggerfallTerrain southNeighbor)
        {
            if (daggerTerrain.MapPixelY == 500)
            {
                southNeighbor = null;
                return false;
            }

            return TryGetTerrain(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY + 1, out southNeighbor);
        }

        bool GetWestNeighbor(DaggerfallTerrain daggerTerrain, out DaggerfallTerrain westNeighbor)
        {
            if (daggerTerrain.MapPixelX == 0)
            {
                westNeighbor = null;
                return false;
            }

            return TryGetTerrain(daggerTerrain.MapPixelX - 1, daggerTerrain.MapPixelY, out westNeighbor);
        }

        // Returns true if the loc is done adjusting
        bool FindAdjustedHeightOffset(LocationData locationData)
        {
            var loc = locationData.Location;
            var locationPrefab = locationData.Prefab;

            if (!TryGetTerrain(loc.worldX, loc.worldY, out DaggerfallTerrain locBaseTerrain))
            {
                return true;
            }

            float baseHeightMax = DaggerfallUnity.Instance.TerrainSampler.MaxTerrainHeight * locBaseTerrain.TerrainScale;
            float baseHeightAverage = locationData.OverlapAverageHeight * baseHeightMax;

            List<Vector2Int> pendingTerrain = new List<Vector2Int>();

            foreach (LocationHelper.TerrainSection terrainSection in LocationHelper.GetOverlappingTerrainSections(loc, locationPrefab))
            {
                if(!TryGetTerrain(terrainSection.WorldCoord.x, terrainSection.WorldCoord.y, out DaggerfallTerrain sectionTerrain))
                {
                    pendingTerrain.Add(terrainSection.WorldCoord);
                    continue;
                }

                float terrainHeightMax = DaggerfallUnity.Instance.TerrainSampler.MaxTerrainHeight * sectionTerrain.TerrainScale;

                for (int i = terrainSection.Section.min.x; i <= terrainSection.Section.max.x; i++)
                {
                    for(int j = terrainSection.Section.min.y; j <= terrainSection.Section.max.y; j++)
                    {
                        float sampleHeight = sectionTerrain.MapData.heightmapSamples[j, i];
                        float unitHeight = sampleHeight * terrainHeightMax;

                        float currentHeight = baseHeightAverage + locationData.HeightOffset;

                        if (unitHeight < currentHeight)
                        {
                            locationData.HeightOffset = unitHeight - baseHeightAverage;
                        }
                    }
                }
            }

            if(pendingTerrain.Count > 0)
            {
                instancePendingTerrains[loc.locationID] = pendingTerrain;

                return false;
            }

            return true;
        }
    }
}
