// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationResourceManager.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Unity;
using UnityEngine;

using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;

namespace LocationLoader
{
    class LocationResourceManager : MonoBehaviour
    {
        Dictionary<Vector2Int, List<LocationInstance>> worldPixelInstances = new Dictionary<Vector2Int, List<LocationInstance>>();
        HashSet<int> visitedRegions = new HashSet<int>();
        Dictionary<int, Dictionary<string, Mod>> modRegionFiles = new Dictionary<int, Dictionary<string, Mod>>();

        Dictionary<string, Mod> modLocationPrefabs = null;
        Dictionary<string, LocationPrefab> prefabInfos = new Dictionary<string, LocationPrefab>();
        Dictionary<string, GameObject> prefabTemplates = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> unityPrefabAssets = new Dictionary<string, GameObject>();

        void Start()
        {
            CacheGlobalInstances();
        }

        public IEnumerable<LocationInstance> GetTerrainInstances(DaggerfallTerrain daggerTerrain)
        {
            var regionIndex = GetRegionIndex(daggerTerrain);
            if (regionIndex != -1)
            {
                CacheRegionInstances(regionIndex);
            }

            if(worldPixelInstances.TryGetValue(GetCoord(daggerTerrain), out List<LocationInstance> instances))
            {
                return instances;
            }

            return Enumerable.Empty<LocationInstance>();
        }

        public LocationPrefab GetPrefabInfo(string prefabName)
        {
            CacheLocationPrefabs();

            string assetName = prefabName + ".txt";

            LocationPrefab prefabInfo;
            if (!prefabInfos.TryGetValue(assetName, out prefabInfo))
            {
                Mod mod;
                if (!modLocationPrefabs.TryGetValue(assetName, out mod))
                {
                    Debug.LogWarning($"Can't find location prefab '{prefabName}'");
                    return null;
                }

                prefabInfo = mod != null
                    ? LocationHelper.LoadLocationPrefab(mod, assetName)
                    : LocationHelper.LoadLocationPrefab(Path.Combine(Application.dataPath, LocationHelper.locationPrefabFolder, assetName));

                if (prefabInfo == null)
                {
                    Debug.LogWarning($"Location prefab '{prefabName}' could not be parsed");
                    return null;
                }

                prefabInfos.Add(assetName, prefabInfo);
            }

            return prefabInfo;
        }

        public GameObject InstantiateLocationPrefab(string prefabName, LocationPrefab locationPrefab, Transform prefabParent)
        {
            GameObject prefabObject = GetPrefabTemplate(prefabName, locationPrefab);

            GameObject instance = UnityEngine.Object.Instantiate(prefabObject, new Vector3(), Quaternion.identity, prefabParent);
            instance.name = prefabName;
            return instance;
        }
        
        Vector2Int GetCoord(DaggerfallTerrain daggerTerrain)
        {
            return new Vector2Int(daggerTerrain.MapData.mapPixelX, daggerTerrain.MapData.mapPixelY);
        }

        Dictionary<string, Mod> GetModGlobalFiles()
        {
            Dictionary<string, Mod> modGlobalFiles = new Dictionary<string, Mod>(StringComparer.InvariantCultureIgnoreCase);

            foreach (Mod mod in ModManager.Instance.Mods)
            {
                if (!mod.Enabled)
                    continue;

                if (mod.AssetBundle && mod.AssetBundle.GetAllAssetNames().Length > 0)
                {
                    string dummyFilePath = mod.AssetBundle.GetAllAssetNames()[0];
                    string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                    if (modFolderPrefix == null)                               // MOBILE
                        continue;                                              // MOBILE

                    string globalFolder = modFolderPrefix + "/locations";

                    foreach (string filename in mod.AssetBundle.GetAllAssetNames())
                    {
                        string directoryName = Path.GetDirectoryName(filename).Replace('\\', '/');
                        if (!string.Equals(directoryName, globalFolder, StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) && !filename.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        string file = Path.GetFileName(filename);
                        modGlobalFiles[file] = mod;
                    }
                }
#if UNITY_EDITOR
                else if (mod.IsVirtual && mod.ModInfo.Files.Count > 0)
                {
                    string dummyFilePath = mod.ModInfo.Files[0];
                    string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                    if (modFolderPrefix == null)                               // MOBILE
                        continue;                                              // MOBILE

                    string globalFolder = modFolderPrefix + "/Locations";

                    foreach (string filename in mod.ModInfo.Files)
                    {
                        string directoryName = Path.GetDirectoryName(filename).Replace('\\', '/');
                        if (!string.Equals(directoryName, globalFolder, StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (!filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) && !filename.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        string file = Path.GetFileName(filename);
                        modGlobalFiles[file] = mod;
                    }
                }
#endif
            }

            string looseLocationFolder = Path.Combine(Application.dataPath, LocationHelper.locationInstanceFolder);
            if (Directory.Exists(looseLocationFolder))
            {
                foreach (string filename in Directory.GetFiles(looseLocationFolder)
                    .Where(file => file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                    .Select(file => Path.GetFileName(file)))
                {
                    modGlobalFiles[filename] = null;
                }
            }

            return modGlobalFiles;
        }

        void CacheGlobalInstances()
        {
            Dictionary<string, Mod> modGlobalFiles = GetModGlobalFiles();

            foreach (var kvp in modGlobalFiles)
            {
                string filename = kvp.Key;
                Mod mod = kvp.Value;

                if (mod == null)
                {
                    string looseLocationFolder = Path.Combine(Application.dataPath, LocationHelper.locationInstanceFolder);
                    string looseFileLocation = Path.Combine(looseLocationFolder, filename);

                    foreach (LocationInstance instance in LocationHelper.LoadLocationInstance(looseFileLocation))
                    {
                        Vector2Int location = new Vector2Int(instance.worldX, instance.worldY);
                        List<LocationInstance> instances;
                        if (!worldPixelInstances.TryGetValue(location, out instances))
                        {
                            instances = new List<LocationInstance>();
                            worldPixelInstances.Add(location, instances);
                        }

                        instances.Add(instance);
                    }
                }
                else
                {
                    foreach (LocationInstance instance in LocationHelper.LoadLocationInstance(mod, filename))
                    {
                        Vector2Int location = new Vector2Int(instance.worldX, instance.worldY);
                        List<LocationInstance> instances;
                        if (!worldPixelInstances.TryGetValue(location, out instances))
                        {
                            instances = new List<LocationInstance>();
                            worldPixelInstances.Add(location, instances);
                        }

                        instances.Add(instance);
                    }
                }
            }
        }

        void CacheRegionInstances(int regionIndex)
        {
            CacheRegionFileNames(regionIndex);

            if (visitedRegions.Contains(regionIndex))
                return;

            Dictionary<string, Mod> regionFiles = modRegionFiles[regionIndex];
            foreach (var kvp in regionFiles)
            {
                string filename = kvp.Key;
                Mod mod = kvp.Value;

                if (mod == null)
                {
                    string looseLocationRegionFolder = Path.Combine(Application.dataPath, LocationHelper.locationInstanceFolder, regionIndex.ToString());
                    string looseFileLocation = Path.Combine(looseLocationRegionFolder, filename);

                    foreach (LocationInstance instance in LocationHelper.LoadLocationInstance(looseFileLocation))
                    {
                        Vector2Int location = new Vector2Int(instance.worldX, instance.worldY);
                        List<LocationInstance> instances;
                        if (!worldPixelInstances.TryGetValue(location, out instances))
                        {
                            instances = new List<LocationInstance>();
                            worldPixelInstances.Add(location, instances);
                        }

                        instances.Add(instance);
                    }
                }
                else
                {
                    foreach (LocationInstance instance in LocationHelper.LoadLocationInstance(mod, filename))
                    {
                        Vector2Int location = new Vector2Int(instance.worldX, instance.worldY);
                        List<LocationInstance> instances;
                        if (!worldPixelInstances.TryGetValue(location, out instances))
                        {
                            instances = new List<LocationInstance>();
                            worldPixelInstances.Add(location, instances);
                        }

                        instances.Add(instance);
                    }
                }
            }

            visitedRegions.Add(regionIndex);
        }

        void CacheRegionFileNames(int regionIndex)
        {
            if (!modRegionFiles.ContainsKey(regionIndex))
            {
                Dictionary<string, Mod> regionFiles = new Dictionary<string, Mod>(StringComparer.InvariantCultureIgnoreCase);
                modRegionFiles.Add(regionIndex, regionFiles);

                string regionName = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(regionIndex);

                foreach (Mod mod in ModManager.Instance.Mods)
                {
                    if (!mod.Enabled)
                        continue;

                    if (mod.AssetBundle && mod.AssetBundle.GetAllAssetNames().Length > 0)
                    {
                        string dummyFilePath = mod.AssetBundle.GetAllAssetNames()[0];
                        string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                        if (modFolderPrefix == null)                               // MOBILE
                            continue;                                              // MOBILE

                        string regionIndexFolder = modFolderPrefix + "/Locations/" + regionIndex.ToString();
                        string regionNameFolder = modFolderPrefix + "/Locations/" + regionName;


                        foreach (string filename in mod.AssetBundle.GetAllAssetNames()
                            .Where(file => (file.StartsWith(regionIndexFolder, StringComparison.InvariantCultureIgnoreCase) || file.StartsWith(regionNameFolder, StringComparison.InvariantCultureIgnoreCase))
                                && (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase)))
                            .Select(file => Path.GetFileName(file)))
                        {
                            regionFiles[filename] = mod;
                        }
                    }
#if UNITY_EDITOR
                    else if (mod.IsVirtual && mod.ModInfo.Files.Count > 0)
                    {
                        string dummyFilePath = mod.ModInfo.Files[0];
                        string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                        if (modFolderPrefix == null)                               // MOBILE
                            continue;                                              // MOBILE

                        string regionIndexFolder = modFolderPrefix + "/Locations/" + regionIndex.ToString();
                        string regionNameFolder = modFolderPrefix + "/Locations/" + regionName;

                        foreach (string filename in mod.ModInfo.Files
                            .Where(file => (file.StartsWith(regionIndexFolder, StringComparison.InvariantCultureIgnoreCase) || file.StartsWith(regionNameFolder, StringComparison.InvariantCultureIgnoreCase))
                                && (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase)))
                            .Select(file => Path.GetFileName(file)))
                        {
                            regionFiles[filename] = mod;
                        }
                    }
#endif                   
                }

                string looseLocationFolder = Path.Combine(Application.dataPath, LocationHelper.locationInstanceFolder);
                string looseLocationRegionIndexFolder = Path.Combine(looseLocationFolder, regionIndex.ToString());
                string looseLocationRegionNameFolder = Path.Combine(looseLocationFolder, regionName);
                if (Directory.Exists(looseLocationFolder))
                {
                    if (Directory.Exists(looseLocationRegionIndexFolder))
                    {
                        foreach (string filename in Directory.GetFiles(looseLocationRegionIndexFolder)
                            .Where(file => file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                            .Select(file => Path.GetFileName(file)))
                        {
                            regionFiles[filename] = null;
                        }
                    }

                    if (Directory.Exists(looseLocationRegionNameFolder))
                    {
                        foreach (string filename in Directory.GetFiles(looseLocationRegionNameFolder)
                            .Where(file => file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                            .Select(file => Path.GetFileName(file)))
                        {
                            regionFiles[filename] = null;
                        }
                    }
                }
            }
        }

        int GetRegionIndex(DaggerfallTerrain daggerfallTerrain)
        {
            if (daggerfallTerrain.MapData.mapRegionIndex != -1)
                return daggerfallTerrain.MapData.mapRegionIndex;

            int region = daggerfallTerrain.MapData.worldPolitic & 0x7F;
            // Region 64 is an "all water" terrain tile, according to UESP
            if (region < 0 || region >= DaggerfallUnity.Instance.ContentReader.MapFileReader.RegionCount || region == 64)
            {
                if (region != 64)
                    Debug.LogWarning($"Invalid region found at map location [{daggerfallTerrain.MapPixelX}, {daggerfallTerrain.MapPixelY}]");
                return -1;
            }

            return region;
        }

        void CacheLocationPrefabs()
        {
            if (modLocationPrefabs != null)
                return;

            modLocationPrefabs = new Dictionary<string, Mod>(StringComparer.InvariantCultureIgnoreCase);

            foreach (Mod mod in ModManager.Instance.Mods)
            {
                if (!mod.Enabled)
                    continue;

                if (mod.AssetBundle && mod.AssetBundle.GetAllAssetNames().Length > 0)
                {
                    string dummyFilePath = mod.AssetBundle.GetAllAssetNames()[0];
                    string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                    if (modFolderPrefix == null)                               // MOBILE
                        continue;                                              // MOBILE

                    string prefabFolder = modFolderPrefix + "/Locations/LocationPrefab/";

                    foreach (string filename in mod.AssetBundle.GetAllAssetNames()
                        .Where(file => file.StartsWith(prefabFolder, StringComparison.InvariantCultureIgnoreCase)
                        && (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase)))
                        .Select(file => Path.GetFileName(file)))
                    {
                        modLocationPrefabs[filename] = mod;
                    }
                }
#if UNITY_EDITOR
                else if (mod.IsVirtual && mod.ModInfo.Files.Count > 0)
                {
                    string dummyFilePath = mod.ModInfo.Files[0];
                    string modFolderPrefix = LocationHelper.ModFolderPrefix(dummyFilePath);   // MOBILE
                    if (modFolderPrefix == null)                               // MOBILE
                        continue;                                              // MOBILE

                    string prefabFolder = modFolderPrefix + "/Locations/LocationPrefab/";

                    foreach (string filename in mod.ModInfo.Files
                        .Where(file => file.StartsWith(prefabFolder, StringComparison.InvariantCultureIgnoreCase)
                        && (file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase)))
                        .Select(file => Path.GetFileName(file)))
                    {
                        modLocationPrefabs[filename] = mod;
                    }
                }
#endif                
            }

            string looseLocationFolder = Path.Combine(Application.dataPath, LocationHelper.locationInstanceFolder);

            if(!Directory.Exists(looseLocationFolder))
            {
                Debug.Log($"Loose locations folder '{looseLocationFolder}' did not exist");
                return;
            }

            string looseLocationPrefabFolder = Path.Combine(looseLocationFolder, "LocationPrefab");
            if(!Directory.Exists(looseLocationPrefabFolder))
            {
                Debug.Log($"Loose location prefabs folder '{looseLocationFolder}' did not exist");
                return;
            }

            foreach (string filename in Directory.GetFiles(looseLocationPrefabFolder)
                .Where(file => file.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase))
                .Select(file => Path.GetFileName(file)))
            {
                Debug.Log($"Found loose location prefab '{filename}'");
                modLocationPrefabs[filename] = null;
            }
        }

        public GameObject CreatePrefabTemplate(string prefabName, LocationPrefab locationPrefab, Transform prefabParent, ModelCombiner combiner = null)
        {
            GameObject prefabObject = new GameObject($"{prefabName}_Template");
            prefabObject.SetActive(false);
            Transform templateTransform = prefabObject.transform;
            templateTransform.parent = prefabParent; // Put them under this mod for Hierarchy organization

            bool topCombiner = false;
            if (combiner == null)
            {
                combiner = new ModelCombiner();
                topCombiner = true;
            }

            foreach (LocationObject obj in locationPrefab.obj)
            {
                // Only instantiate the types for now
                if (obj.type == 0)
                {
                    GameObject go = LocationHelper.LoadStaticObject(
                        obj.type,
                        obj.name,
                        templateTransform,
                        obj.pos,
                        obj.rot,
                        obj.scale,
                        combiner
                        );

                    if (go != null)
                    {
                        if (go.GetComponent<DaggerfallBillboard>())
                        {
                            float tempY = go.transform.position.y;
                            go.GetComponent<DaggerfallBillboard>().AlignToBase();
                            go.transform.position = new Vector3(go.transform.position.x, tempY + ((go.transform.position.y - tempY) * go.transform.localScale.y), go.transform.position.z);
                        }

                        if (!go.GetComponent<DaggerfallLoot>())
                            go.isStatic = true;
                    }
                }
                else if (obj.type == 3)
                {
                    LocationPrefab objPrefabInfo = GetPrefabInfo(obj.name);
                    if (objPrefabInfo == null)
                    {
                        Debug.LogError($"Could not find prefab '{obj.name}' while instanciating prefab '{prefabName}'");
                        continue;
                    }

                    GameObject subPrefab = InstantiateLocationPrefab(obj.name, objPrefabInfo, prefabObject.transform);
                    subPrefab.transform.localPosition = obj.pos;
                    subPrefab.transform.localRotation = obj.rot;
                    subPrefab.transform.localScale = obj.scale;
                    LocationData data = subPrefab.AddComponent<LocationData>();
                    data.IsEmbeddedLocation = true;
                    data.Prefab = objPrefabInfo;
                }
                else if (obj.type == 4)
                {
                    GameObject subPrefab = InstantiateUnityPrefab(obj.name, prefabObject.transform);
                    if(subPrefab == null)
                    {
                        Debug.LogError($"Could not find Unity prefab '{obj.name}' while instanciating prefab '{prefabName}'");
                        continue;
                    }
                    subPrefab.transform.localPosition = obj.pos;
                    subPrefab.transform.localRotation = obj.rot;
                    subPrefab.transform.localScale = Vector3.Scale(subPrefab.transform.localScale, obj.scale);
                }
            }

            if (topCombiner && combiner.VertexCount > 0)
            {
                combiner.Apply();
                var go = GameObjectHelper.CreateCombinedMeshGameObject(combiner, $"{prefabName}_CombinedModels", templateTransform, makeStatic: true);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            return prefabObject;
        }

        GameObject InstantiateUnityPrefab(string prefabName, Transform prefabParent)
        {
            GameObject prefabTemplate;

            if (!unityPrefabAssets.TryGetValue(prefabName, out prefabTemplate))
            {
                ModManager.Instance.TryGetAsset(prefabName, clone: false, out prefabTemplate);
                unityPrefabAssets.Add(prefabName, prefabTemplate);
            }

            if (prefabTemplate == null)
                return null;

            GameObject instance = Instantiate(prefabTemplate, new Vector3(), Quaternion.identity, prefabParent);
            instance.name = prefabName;
            return instance;
        }

        GameObject GetPrefabTemplate(string prefabName, LocationPrefab locationPrefab)
        {
            // If it's the first time loading this prefab, load the non-dynamic objects into a template
            GameObject prefabObject;
            if (!prefabTemplates.TryGetValue(prefabName, out prefabObject))
            {
                prefabObject = CreatePrefabTemplate(prefabName, locationPrefab, transform);
                prefabTemplates.Add(prefabName, prefabObject);
            }
            return prefabObject;
        }
    }
}
