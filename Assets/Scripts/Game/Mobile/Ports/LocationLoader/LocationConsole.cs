// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationConsole.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DaggerfallWorkshop.Game;
using UnityEngine;
using Wenzil.Console;
using static DaggerfallWorkshop.Utility.ContentReader;

namespace LocationLoader
{
    public static class LocationConsole
    {
        public static void RegisterCommands()
        {
#if UNITY_EDITOR
            ConsoleCommandsDatabase.RegisterCommand("LLPruneInvalidInstances",
                "Tests location instances for validity and removes invalid ones from their package (only CSV supported)"
                , "LLPruneInvalidInstances [flags...] --mod=<modname>\n\tFlags:\n\t\t--file=<file pattern>\n\t\t--region=<id>\n\t\t--type=<type>\n\t\t--prune-loc-overlap\n\t\t--nudge-oob\n\t\t--verbose",
                PruneInvalidInstances);

            ConsoleCommandsDatabase.RegisterCommand("LLDumpTerrainSamples",
                "Dumps all height samples for the specified terrain in a CSV"
                , "LLDumpTerrainSamples <worldX> <worldY> <filename>", DumpTerrainSamples);

            ConsoleCommandsDatabase.RegisterCommand("LLDumpDockLocations",
                "Dumps all the type 2 locations in the game, and what city they're close to",
                "LLDumpDockLocations --mod=<modname> --file=<modfile> --locationId=<id> --write-link",
                DumpDockLocations);

            ConsoleCommandsDatabase.RegisterCommand("LLDumpLocations",
                "Dumps all the locations in the game, and what city they're close to",
                "LLDumpLocations --mod=<modname> --file=<file pattern> --write-link --max-link=<num>", DumpLocations);

            ConsoleCommandsDatabase.RegisterCommand("LLNameLocations",
                "Writes a random name for all the specified locations",
                "LLNameDockLocations --mod=<modname> --file=<file pattern> dock|bandit", NameLocations);
#endif

            ConsoleCommandsDatabase.RegisterCommand("LL_Here", "Posts all the locations in the current map pixel",
                "LL_Here", Here);
        }

#if UNITY_EDITOR
        static Regex MakeFilePattern(string filePattern)
        {
            if (string.IsNullOrEmpty(filePattern))
                return null;

            var lowerPattern = filePattern.ToLower();

            var regexPattern = Regex.Escape(lowerPattern).Replace("\\?", ".").Replace("\\*", ".*");
            return new Regex(regexPattern, RegexOptions.Compiled);
        }

        static string PruneInvalidInstances(string[] Args)
        {
            int? regionId = null;
            int? type = null;
            string modName = null;
            string filePattern = null;
            bool pruneLocOverlap = false;
            bool nudgeOutOfBounds = false;
            bool verbose = false;

            bool parsingQuotedArg = false;
            StringBuilder quotedString = null;

            StringBuilder modNameBuilder = null;
            StringBuilder filePatternBuilder = null;

            foreach (string Arg in Args)
            {
                if (parsingQuotedArg)
                {
                    string ArgValue = Arg;
                    if (Arg.EndsWith("\""))
                    {
                        parsingQuotedArg = false;
                        ArgValue = Arg.Substring(0, Arg.Length - 1);
                    }

                    quotedString.Append(" ").Append(ArgValue);
                }
                else if (Arg.StartsWith("--region="))
                {
                    string regionIdStr = Arg.Replace("--region=", "");
                    if (!int.TryParse(regionIdStr, out int regionIdValue))
                    {
                        return $"Unknown region id '{regionIdStr}'";
                    }

                    regionId = regionIdValue;
                }
                else if (Arg.StartsWith("--type="))
                {
                    string typeStr = Arg.Replace("--type=", "");
                    if (!int.TryParse(typeStr, out int typeValue))
                    {
                        return $"Unknown type '{typeStr}'";
                    }

                    type = typeValue;
                }
                else if (Arg.StartsWith("--mod="))
                {
                    string value = Arg.Replace("--mod=", "");
                    if (value.StartsWith("\""))
                    {
                        if (value.EndsWith("\""))
                        {
                            modName = value.Substring(1, value.Length - 2);
                        }
                        else
                        {
                            quotedString = modNameBuilder = new StringBuilder(value.Substring(1));
                            parsingQuotedArg = true;
                        }
                    }
                    else
                    {
                        modName = value;
                    }
                }
                else if (Arg.StartsWith("--file="))
                {
                    string value = Arg.Replace("--file=", "");
                    if (value.StartsWith("\""))
                    {
                        quotedString = filePatternBuilder = new StringBuilder(value.Substring(1));
                        parsingQuotedArg = true;
                    }
                    else
                    {
                        filePattern = value;
                    }
                }
                else if (Arg == "--prune-loc-overlap")
                {
                    pruneLocOverlap = true;
                }
                else if (Arg == "--nudge-oob")
                {
                    nudgeOutOfBounds = true;
                }
                else if (Arg == "--verbose")
                {
                    verbose = true;
                }
                else
                {
                    return $"Unknown argument '{Arg}'";
                }
            }

            void Log(string msg)
            {
                if (verbose)
                {
                    Debug.Log(msg);
                }
            }

            if (modNameBuilder != null && modNameBuilder.Length > 0)
            {
                modName = modNameBuilder.ToString();
            }

            if (filePatternBuilder != null && filePatternBuilder.Length > 0)
            {
                filePattern = filePatternBuilder.ToString();
            }

            if (string.IsNullOrEmpty(modName))
            {
                return $"Loose files not yet supported. Specify a mod with --mod=<mod name>";
            }

            Dictionary<string, LocationPrefab> prefabCache = new Dictionary<string, LocationPrefab>();

            Mod basicRoads = ModManager.Instance.GetMod("BasicRoads");
            bool roadsEnabled = basicRoads != null && basicRoads.Enabled;

            Mod mod = ModManager.Instance.GetMod(modName);
            if (mod == null)
                return $"Mod '{modName}' not found";

            if (!mod.IsVirtual)
                return $"Mod '{modName} must be virtual (ie: have the sources in the editor)";

            if (mod.ModInfo.Files.Count == 0)
                return $"Mod '{modName} has no files in mod info (see .dfmod.json)";

            string dummyFilePath = mod.ModInfo.Files[0];
            string modFolderPrefix = dummyFilePath.Substring(17);
            modFolderPrefix = dummyFilePath.Substring(0, 17 + modFolderPrefix.IndexOf('/'));
            string modFolder = Path.Combine(Application.dataPath, modFolderPrefix.Substring(7));

            string locationsFolder = modFolderPrefix + "/Locations/";
            string locationPrefabsFolder = modFolderPrefix + "/Locations/LocationPrefab";

            var regex = MakeFilePattern(filePattern);

            int fileCount = 0;

            void ForEachModFile(Action<string> Func)
            {
                if (regionId.HasValue)
                {
                    string regionName =
                        DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(regionId.Value);

                    string regionIdFolder = locationsFolder + regionId.Value;
                    string regionNameFolder = locationsFolder + regionName;

                    foreach (string fileRelativePath in mod.ModInfo.Files
                                 .Where(file =>
                                     (file.StartsWith(regionIdFolder, StringComparison.InvariantCultureIgnoreCase) ||
                                      file.StartsWith(regionNameFolder, StringComparison.InvariantCultureIgnoreCase))
                                     && file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                                 .Select(file => file.Substring(locationsFolder.Length)))
                    {
                        var filename = Path.GetFileName(fileRelativePath).ToLower();

                        if (!string.IsNullOrEmpty(filePattern))
                        {
                            if (!regex.IsMatch(filename))
                                continue;
                        }

                        ++fileCount;

                        try
                        {
                            Log($"Pruning {filename}");
                            Func(fileRelativePath);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                        }
                    }
                }
                else
                {
                    foreach (string fileRelativePath in mod.ModInfo.Files
                                 .Where(file =>
                                     (file.StartsWith(locationsFolder, StringComparison.InvariantCultureIgnoreCase) &&
                                      !file.StartsWith(locationPrefabsFolder,
                                          StringComparison.InvariantCultureIgnoreCase))
                                     && file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                                 .Select(file => file.Substring(locationsFolder.Length)))
                    {
                        var filename = Path.GetFileName(fileRelativePath).ToLower();

                        if (!string.IsNullOrEmpty(filePattern))
                        {
                            if (!regex.IsMatch(filename))
                                continue;
                        }

                        ++fileCount;

                        try
                        {
                            Log($"Pruning {filename}");
                            Func(fileRelativePath);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                        }
                    }
                }
            }

            Dictionary<Vector2Int, List<LocationInstance>> occupyingInstancesPerTile = null;

            if (pruneLocOverlap)
            {
                occupyingInstancesPerTile = new Dictionary<Vector2Int, List<LocationInstance>>();
                ForEachModFile(
                    fileRelativePath =>
                    {
                        string modFilename = Path.GetFileName(fileRelativePath);

                        foreach (LocationInstance instance in LocationHelper.LoadLocationInstance(mod, modFilename))
                        {
                            if (instance.type != 0)
                                continue;

                            Vector2Int worldCoord = new Vector2Int(instance.worldX, instance.worldY);

                            List<LocationInstance> occupyingInstances;
                            if (!occupyingInstancesPerTile.TryGetValue(worldCoord, out occupyingInstances))
                            {
                                occupyingInstances = new List<LocationInstance>();
                                occupyingInstancesPerTile.Add(worldCoord, occupyingInstances);
                            }

                            occupyingInstances.Add(instance);
                        }
                    }
                );
            }

            bool HasLocOverlap(Vector2Int worldCoord, RectInt terrainArea)
            {
                if (occupyingInstancesPerTile == null)
                    return false;

                List<LocationInstance> occupyingInstances;
                if (!occupyingInstancesPerTile.TryGetValue(worldCoord, out occupyingInstances))
                {
                    return false;
                }

                foreach (LocationInstance occupyingInstance in occupyingInstances)
                {
                    LocationPrefab prefab;
                    if (!prefabCache.TryGetValue(occupyingInstance.prefab, out prefab))
                    {
                        prefab = LocationHelper.LoadLocationPrefab(mod, occupyingInstance.prefab);
                        prefabCache.Add(occupyingInstance.prefab, prefab);
                    }

                    var (halfWidth, halfHeight) = LocationHelper.GetHalfDimensions(occupyingInstance, prefab);

                    if (occupyingInstance.terrainX - halfWidth < 0
                        || occupyingInstance.terrainX + halfWidth > 128
                        || occupyingInstance.terrainY - halfHeight < 0
                        || occupyingInstance.terrainY + halfHeight > 128)
                    {
                        continue;
                    }

                    RectInt occupyingArea = new RectInt(occupyingInstance.terrainX - halfWidth,
                        occupyingInstance.terrainY - halfHeight, halfWidth * 2, halfHeight * 2);

                    if (terrainArea.Overlaps(occupyingArea))
                        return true;
                }

                return false;
            }

            bool LocationPasses(LocationInstance instance)
            {
                if (type.HasValue && instance.type != type.Value)
                {
                    return true;
                }

                LocationPrefab prefab;
                if (!prefabCache.TryGetValue(instance.prefab, out prefab))
                {
                    prefab = LocationHelper.LoadLocationPrefab(mod, instance.prefab);
                    if (prefab == null)
                    {
                        Log($"Prefab {instance.prefab} could not be found");
                        return false; // couldn't find prefab
                    }

                    prefabCache.Add(instance.prefab, prefab);
                }

                // Instance is out of bounds
                if (LocationHelper.IsOutOfBounds(instance, prefab))
                {
                    if (nudgeOutOfBounds)
                    {
                        if (prefab.width > LocationLoader.TERRAIN_SIZE || prefab.height > LocationLoader.TERRAIN_SIZE)
                        {
                            Log($"Prefab {instance.prefab} is too big to nudge (must be 128x128 max)");
                            return false; // No way to make this fit
                        }

                        var (halfWidth, halfHeight) = LocationHelper.GetHalfDimensions(instance, prefab);

                        if (instance.terrainX - halfWidth < 0)
                            instance.terrainX = halfWidth;

                        if (instance.terrainX + halfWidth > LocationLoader.TERRAIN_SIZE)
                            instance.terrainX = LocationLoader.TERRAIN_SIZE - halfWidth;

                        if (instance.terrainY - halfHeight < 0)
                            instance.terrainY = halfHeight;

                        if (instance.terrainY + halfHeight > LocationLoader.TERRAIN_SIZE)
                            instance.terrainY = LocationLoader.TERRAIN_SIZE - halfHeight;
                    }
                    else
                    {
                        Log($"Instance is out of map pixel bounds");
                        return false;
                    }
                }

                // Get all world locations it overlaps
                // Type 0 and type 2 instances only fit within their own map pixel, but type 1 can go out of bounds
                IEnumerable<LocationHelper.TerrainSection> overlappingCoordinates =
                    LocationHelper.GetOverlappingTerrainSections(instance, prefab, out bool instanceOverflow);
                if (instanceOverflow)
                {
                    Log($"Instance is out of world bounds");
                    return false;
                }

                foreach (var (coordinate, terrainArea) in overlappingCoordinates)
                {
                    var ContentReader = DaggerfallUnity.Instance.ContentReader;

                    // Instance is on existing Daggerfall location
                    if (ContentReader.HasLocation(coordinate.x, coordinate.y, out MapSummary mapSummary))
                    {
                        if (ContentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex,
                                out DFLocation location))
                        {
                            int locationWidth = location.Exterior.ExteriorData.Width;
                            int locationHeight = location.Exterior.ExteriorData.Height;

                            // If the location doesn't take the full terrain, check for specific overlap
                            const int blocksPerTerrain = 8;
                            if (locationWidth == blocksPerTerrain && locationHeight == blocksPerTerrain)
                            {
                                Log($"Map pixel ({coordinate.x}, {coordinate.y}) already has DF location");
                                return false;
                            }

                            int locationX =
                                (RMBLayout.RMBTilesPerTerrain - locationWidth * RMBLayout.RMBTilesPerBlock) / 2;
                            int locationY =
                                (RMBLayout.RMBTilesPerTerrain - locationHeight * RMBLayout.RMBTilesPerBlock) / 2;

                            RectInt locationArea = new RectInt(locationX, locationY,
                                locationWidth * RMBLayout.RMBTilesPerBlock,
                                locationHeight * RMBLayout.RMBTilesPerBlock);
                            if (locationArea.Overlaps(terrainArea))
                            {
                                Log($"Overlaps with map pixel ({coordinate.x}, {coordinate.y}) DF location");
                                return false;
                            }
                        }
                        else
                        {
                            Log($"Map pixel ({coordinate.x}, {coordinate.y}) already has DF location");
                            return false;
                        }
                    }

                    // Instance is on the ocean
                    if (ContentReader.MapFileReader.GetClimateIndex(coordinate.x, coordinate.y) ==
                        (int)MapsFile.Climates.Ocean)
                    {
                        Log($"Map pixel ({coordinate.x},{coordinate.y}) is ocean climate");
                        return false;
                    }

                    // Instance overlaps another loaded location
                    if (HasLocOverlap(coordinate, terrainArea))
                    {
                        Log($"Map pixel ({coordinate.x},{coordinate.y}) already has LL location");
                        return false;
                    }
                }

                // Instance is on road
                if (roadsEnabled)
                {
                    foreach (var (coordinate, rectangle) in overlappingCoordinates)
                    {
                        bool error = false;
                        byte pathsDataPoint = 0;
                        Vector2Int coords = new Vector2Int(coordinate.x, coordinate.y);
                        ModManager.Instance.SendModMessage("BasicRoads", "getPathsPoint", coords,
                            (string message, object data) =>
                            {
                                if (message == "getPathsPoint")
                                {
                                    pathsDataPoint = (byte)data;
                                }
                                else if (message == "error")
                                {
                                    error = true;
                                }
                            });

                        if (error)
                        {
                            Debug.LogError(
                                $"Error while checking road overlap (instance={instance.locationID}, coords={coordinate.x}, {coordinate.y})");
                            return false;
                        }

                        if (pathsDataPoint != 0)
                        {
                            if (LocationHelper.OverlapsRoad(rectangle, pathsDataPoint))
                            {
                                Log($"Instance overlaps road");
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            void PruneModFile(string fileRelativePath)
            {
                string modFilename = Path.GetFileName(fileRelativePath);

                TextAsset asset = mod.GetAsset<TextAsset>(modFilename);
                TextReader assetReader = new StringReader(asset.text);

                int line = 1;
                string header = assetReader.ReadLine();
                string[] fields = header.Split(',');

                string fullAssetPath = Path.Combine(modFolder, "Locations", fileRelativePath);

                using (StreamWriter streamWriter = new StreamWriter(fullAssetPath, append: false))
                {
                    streamWriter.WriteLine(header);

                    while (assetReader.Peek() > 0)
                    {
                        ++line;
                        string instanceLine = assetReader.ReadLine();

                        try
                        {
                            string context = $"mod={mod.ModInfo.ModTitle}, file={modFilename}, line={line}";
                            LocationInstance instance =
                                LocationHelper.LoadSingleLocationInstanceCsv(instanceLine, fields, context);
                            if (instance == null)
                            {
                                Debug.LogWarning($"({context}) Instance could not be parsed. Removing");
                                continue;
                            }

                            if (LocationPasses(instance))
                            {
                                string[] originalValues = instanceLine.Split(',');
                                string newLine =
                                    LocationHelper.SaveSingleLocationInstanceCsv(instance, fields, originalValues);
                                streamWriter.WriteLine(newLine);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                        }
                    }
                }
            }

            ForEachModFile(PruneModFile);

            return $"Success (visited {fileCount} files)";
        }

        static string DumpTerrainSamples(string[] Args)
        {
            if (Args.Length != 3)
                return "Invalid args. Expected 3";

            if (!int.TryParse(Args[0], out int worldX))
                return $"Error: First arg 'WorldX' was not an integer: {Args[0]}";

            if (!int.TryParse(Args[1], out int worldY))
                return $"Error: First arg 'WorldY' was not an integer: {Args[1]}";

            string filename = Args[2];

            if (string.IsNullOrEmpty(Path.GetExtension(filename)))
            {
                filename = filename + ".csv";
            }

            if (!LocationModLoader.modObject.GetComponent<LocationLoader>()
                    .TryGetTerrain(worldX, worldY, out DaggerfallTerrain daggerTerrain))
            {
                return $"Error: Could not find loaded terrain at ({worldX},{worldY})";
            }

            Directory.CreateDirectory(LocationModLoader.mod.PersistentDataDirectory);
            string path = Path.Combine(LocationModLoader.mod.PersistentDataDirectory, filename);
            using (StreamWriter outFile = new StreamWriter(path))
            {
                for (int y = 128; y >= 0; --y)
                {
                    string row = string.Join(";",
                        Enumerable.Range(0, 129)
                            .Select(x => daggerTerrain.MapData.heightmapSamples[y, x].ToString())
                    );
                    outFile.WriteLine(row);
                }
            }

            return "Success";
        }

        static string DumpDockLocations(string[] Args)
        {
            string modName = null;
            string fileName = null;
            ulong? locationId = null;
            bool writeLink = false;

            bool parsingQuotedArg = false;
            StringBuilder quotedString = null;

            StringBuilder modNameBuilder = null;
            StringBuilder fileNameBuilder = null;

            foreach (string Arg in Args)
            {
                if (parsingQuotedArg)
                {
                    string ArgValue = Arg;
                    if (Arg.EndsWith("\""))
                    {
                        parsingQuotedArg = false;
                        ArgValue = Arg.Substring(0, Arg.Length - 1);
                    }

                    quotedString.Append(" ").Append(ArgValue);
                }
                else if (Arg.StartsWith("--mod="))
                {
                    string value = Arg.Replace("--mod=", "");
                    if (value.StartsWith("\""))
                    {
                        quotedString = modNameBuilder = new StringBuilder(value.Substring(1));
                        parsingQuotedArg = true;
                    }
                    else
                    {
                        modName = value;
                    }
                }
                else if (Arg.StartsWith("--file="))
                {
                    string value = Arg.Replace("--file=", "");
                    if (value.StartsWith("\""))
                    {
                        quotedString = fileNameBuilder = new StringBuilder(value.Substring(1));
                        parsingQuotedArg = true;
                    }
                    else
                    {
                        fileName = value;
                    }
                }
                else if (Arg.StartsWith("--locationId="))
                {
                    string value = Arg.Replace("--locationId=", "");
                    if (ulong.TryParse(value, out ulong parsedValue))
                    {
                        locationId = parsedValue;
                    }
                    else
                    {
                        return $"Invalid value in '--locationId=' ({value})";
                    }
                }
                else if (Arg == "--write-link")
                {
                    writeLink = true;
                }
                else
                {
                    return $"Unknown argument '{Arg}'";
                }
            }

            if (modNameBuilder != null && modNameBuilder.Length > 0)
            {
                modName = modNameBuilder.ToString();
            }

            if (fileNameBuilder != null && fileNameBuilder.Length > 0)
            {
                fileName = fileNameBuilder.ToString();
            }

            if (string.IsNullOrEmpty(modName))
            {
                return $"Loose files not yet supported. Specify a mod with --mod=<mod name>";
            }

            Mod mod = ModManager.Instance.GetMod(modName);
            if (mod == null)
                return $"Mod '{modName}' not found";

            string dummyFilePath = mod.ModInfo.Files[0];
            string modFolderPrefix = dummyFilePath.Substring(17);
            modFolderPrefix = dummyFilePath.Substring(0, 17 + modFolderPrefix.IndexOf('/'));
            string modFolder = Path.Combine(Application.dataPath, modFolderPrefix.Substring(7));

            string locationsFolder = modFolderPrefix + "/Locations/";
            string locationPrefabsFolder = modFolderPrefix + "/Locations/LocationPrefab";

            void ForEachModFile(Action<string> Func)
            {
                foreach (string fileRelativePath in mod.ModInfo.Files
                             .Where(file =>
                                 (file.StartsWith(locationsFolder, StringComparison.InvariantCultureIgnoreCase) &&
                                  !file.StartsWith(locationPrefabsFolder, StringComparison.InvariantCultureIgnoreCase))
                                 && file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                             .Select(file => file.Substring(locationsFolder.Length)))
                {
                    if (!string.IsNullOrEmpty(fileName) && !Path.GetFileName(fileRelativePath)
                            .Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    try
                    {
                        Func(fileRelativePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                    }
                }
            }

            Directory.CreateDirectory(LocationModLoader.mod.PersistentDataDirectory);
            string path = Path.Combine(LocationModLoader.mod.PersistentDataDirectory, "DockLocations.csv");
            using (StreamWriter outFile = new StreamWriter(path))
            {
                outFile.WriteLine("Dock Type,LocId,Region,City,City MapId,City Type,Distance");

                bool TryDumpLocation(LocationInstance loc)
                {
                    bool foundCity = false;

                    if (locationId.HasValue && loc.locationID != locationId.Value)
                        return foundCity;

                    if (loc.type != 2)
                        return foundCity;

                    var contentReader = DaggerfallUnity.Instance.ContentReader;

                    MapSummary map;

                    void DumpMap(int distance)
                    {
                        foundCity = true;

                        if (writeLink)
                        {
                            loc.SetExtraDataField("LinkedMapId", map.ID);
                        }

                        var regionName = contentReader.MapFileReader.GetRegionName(map.RegionIndex);
                        DFLocation mapLocation = contentReader.MapFileReader.GetLocation(map.RegionIndex, map.MapIndex);
                        outFile.WriteLine(
                            $"{loc.prefab},{loc.locationID},{regionName},{mapLocation.Name},{map.ID},{map.LocationType},{distance}");
                    }

                    const int MaxManhatanDistance = 3;
                    for (int i = 1; i <= MaxManhatanDistance; ++i)
                    {
                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX + i - j, loc.worldY + j, out map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX - j, loc.worldY + i - j, out map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX - i + j, loc.worldY - j, out map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX + j, loc.worldY - i + j, out map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }
                    }

                    // Fallback dump
                    int politicValue =
                        DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(loc.worldX, loc.worldY);
                    string locRegionName;
                    if (politicValue != 64)
                    {
                        int regionNumber = politicValue & 0x7F;
                        locRegionName =
                            DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(regionNumber);
                    }
                    else
                    {
                        locRegionName = "Unknown";
                    }

                    outFile.WriteLine($"{loc.prefab},{loc.locationID},{locRegionName},,,,");

                    endloop:
                    return foundCity;
                }

                void OutputFile(string fileRelativePath)
                {
                    string modFilename = Path.GetFileName(fileRelativePath);

                    if (!writeLink)
                    {
                        foreach (LocationInstance loc in LocationHelper.LoadLocationInstance(mod, modFilename))
                        {
                            TryDumpLocation(loc);
                        }
                    }
                    else
                    {
                        TextAsset asset = mod.GetAsset<TextAsset>(modFilename);
                        TextReader assetReader = new StringReader(asset.text);

                        int line = 1;
                        string header = assetReader.ReadLine();
                        string[] originalFields = header.Split(',');

                        string[] newFields;
                        int extraDataIndex;
                        if (originalFields.Contains("extraData"))
                        {
                            newFields = originalFields;
                            extraDataIndex = Array.IndexOf(originalFields, "extraData");
                        }
                        else
                        {
                            newFields = (string[])originalFields.Clone();
                            UnityEditor.ArrayUtility.Add(ref newFields, "extraData");
                            extraDataIndex = newFields.Length - 1;
                        }

                        string fullAssetPath = Path.Combine(modFolder, "Locations", fileRelativePath);

                        using (StreamWriter locationFileWriter = new StreamWriter(fullAssetPath, append: false))
                        {
                            locationFileWriter.WriteLine(string.Join(",", newFields));
                            while (assetReader.Peek() > 0)
                            {
                                ++line;
                                string instanceLine = assetReader.ReadLine();

                                try
                                {
                                    string context = $"mod={mod.ModInfo.ModTitle}, file={modFilename}, line={line}";
                                    LocationInstance instance =
                                        LocationHelper.LoadSingleLocationInstanceCsv(instanceLine, originalFields,
                                            context);
                                    if (instance == null)
                                    {
                                        Debug.LogWarning($"({context}) Instance could not be parsed. Removing");
                                        continue;
                                    }

                                    if (TryDumpLocation(instance))
                                    {
                                        // Instance had a linked city written to it. Modify the line before dumping it back
                                        string[] instanceValues = instanceLine.Split(',');

                                        var serializedExtraData = instance.extraData.Replace("\"", "\\\"");
                                        serializedExtraData = $"\"{serializedExtraData}\"";

                                        if (newFields != originalFields)
                                        {
                                            UnityEditor.ArrayUtility.Add(ref instanceValues, serializedExtraData);
                                        }
                                        else
                                        {
                                            instanceValues[extraDataIndex] = serializedExtraData;
                                        }

                                        string modifiedInstanceLine = string.Join(",", instanceValues);
                                        locationFileWriter.WriteLine(modifiedInstanceLine);
                                    }
                                    else
                                    {
                                        if (newFields == originalFields)
                                        {
                                            locationFileWriter.WriteLine(instanceLine);
                                        }
                                        else
                                        {
                                            locationFileWriter.WriteLine(instanceLine + ",");
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError(e.Message);
                                }
                            }
                        }
                    }
                }

                ForEachModFile(OutputFile);
            }

            return "Success";
        }

        static bool IsCityMap(in MapSummary Map)
        {
            return Map.LocationType == DFRegion.LocationTypes.TownCity
                   || Map.LocationType == DFRegion.LocationTypes.TownHamlet
                   || Map.LocationType == DFRegion.LocationTypes.TownVillage;
        }

        static string DumpLocations(string[] Args)
        {
            string modName = null;
            string filePattern = null;
            int? manhattanDistance = null;

            bool writeLink = false;

            bool parsingQuotedArg = false;
            StringBuilder quotedString = null;

            StringBuilder modNameBuilder = null;
            StringBuilder filePatternBuilder = null;

            foreach (string Arg in Args)
            {
                if (parsingQuotedArg)
                {
                    string ArgValue = Arg;
                    if (Arg.EndsWith("\""))
                    {
                        parsingQuotedArg = false;
                        ArgValue = Arg.Substring(0, Arg.Length - 1);
                    }

                    quotedString.Append(" ").Append(ArgValue);
                }
                else if (Arg.StartsWith("--mod="))
                {
                    string value = Arg.Replace("--mod=", "");
                    if (value.StartsWith("\""))
                    {
                        if (value.EndsWith("\""))
                        {
                            modName = value.Substring(1, value.Length - 2);
                        }
                        else
                        {
                            quotedString = modNameBuilder = new StringBuilder(value.Substring(1));
                            parsingQuotedArg = true;
                        }
                    }
                    else
                    {
                        modName = value;
                    }
                }
                else if (Arg.StartsWith("--file="))
                {
                    string value = Arg.Replace("--file=", "");
                    if (value.StartsWith("\""))
                    {
                        if (value.EndsWith("\""))
                        {
                            filePattern = value.Substring(1, value.Length - 2);
                        }
                        else
                        {
                            quotedString = filePatternBuilder = new StringBuilder(value.Substring(1));
                            parsingQuotedArg = true;
                        }
                    }
                    else
                    {
                        filePattern = value;
                    }
                }
                else if (Arg == "--write-link")
                {
                    writeLink = true;
                }
                else if (Arg.StartsWith("--max-link="))
                {
                    string value = Arg.Replace("--max-link=", "");
                    if (int.TryParse(value, out int parsedValue))
                    {
                        manhattanDistance = parsedValue;
                    }
                    else
                    {
                        return $"Could not parse integer after '--max-link='";
                    }
                }
                else
                {
                    return $"Unknown argument '{Arg}'";
                }
            }

            if (modNameBuilder != null && modNameBuilder.Length > 0)
            {
                modName = modNameBuilder.ToString();
            }

            if (filePatternBuilder != null && filePatternBuilder.Length > 0)
            {
                filePattern = filePatternBuilder.ToString();
            }

            if (string.IsNullOrEmpty(modName))
            {
                return $"Loose files not yet supported. Specify a mod with --mod=<mod name>";
            }

            var regex = MakeFilePattern(filePattern);

            Mod mod = ModManager.Instance.GetMod(modName);
            if (mod == null)
                return $"Mod '{modName}' not found";

            string dummyFilePath = mod.ModInfo.Files[0];
            string modFolderPrefix = dummyFilePath.Substring(17);
            modFolderPrefix = dummyFilePath.Substring(0, 17 + modFolderPrefix.IndexOf('/'));
            string modFolder = Path.Combine(Application.dataPath, modFolderPrefix.Substring(7));

            string locationsFolder = modFolderPrefix + "/Locations/";
            string locationPrefabsFolder = modFolderPrefix + "/Locations/LocationPrefab";

            void ForEachModFile(Action<string> Func)
            {
                foreach (string fileRelativePath in mod.ModInfo.Files
                             .Where(file =>
                                 (file.StartsWith(locationsFolder, StringComparison.InvariantCultureIgnoreCase) &&
                                  !file.StartsWith(locationPrefabsFolder, StringComparison.InvariantCultureIgnoreCase))
                                 && file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                             .Select(file => file.Substring(locationsFolder.Length)))
                {
                    var filename = Path.GetFileName(fileRelativePath).ToLower();

                    if (!regex.IsMatch(filename))
                        continue;

                    try
                    {
                        Func(fileRelativePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                    }
                }
            }

            Directory.CreateDirectory(LocationModLoader.mod.PersistentDataDirectory);
            string path = Path.Combine(LocationModLoader.mod.PersistentDataDirectory, "Locations.csv");
            using (StreamWriter outFile = new StreamWriter(path))
            {
                outFile.WriteLine("Dock Type,LocId,Region,City,City MapId,City Type,Distance");

                bool TryDumpLocation(LocationInstance loc)
                {
                    bool foundCity = false;

                    var contentReader = DaggerfallUnity.Instance.ContentReader;

                    MapSummary map;

                    void DumpMap(int distance)
                    {
                        foundCity = true;

                        if (writeLink)
                        {
                            loc.SetExtraDataField("LinkedMapId", map.ID);
                        }

                        var regionName = contentReader.MapFileReader.GetRegionName(map.RegionIndex);
                        DFLocation mapLocation = contentReader.MapFileReader.GetLocation(map.RegionIndex, map.MapIndex);
                        outFile.WriteLine(
                            $"{loc.prefab},{loc.locationID},{regionName},{mapLocation.Name},{map.ID},{map.LocationType},{distance}");
                    }

                    int maxDistance = manhattanDistance.GetValueOrDefault(3);
                    for (int i = 1; i <= maxDistance; ++i)
                    {
                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX + i - j, loc.worldY + j, out map) &&
                                IsCityMap(map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX - j, loc.worldY + i - j, out map) &&
                                IsCityMap(map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX - i + j, loc.worldY - j, out map) &&
                                IsCityMap(map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }

                        for (int j = 0; j < i; ++j)
                        {
                            if (contentReader.HasLocation(loc.worldX + j, loc.worldY - i + j, out map) &&
                                IsCityMap(map))
                            {
                                DumpMap(i);
                                goto endloop;
                            }
                        }
                    }

                    // Fallback dump
                    int politicValue =
                        DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(loc.worldX, loc.worldY);
                    string locRegionName;
                    if (politicValue != 64)
                    {
                        int regionNumber = politicValue & 0x7F;
                        locRegionName =
                            DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(regionNumber);
                    }
                    else
                    {
                        locRegionName = "Unknown";
                    }

                    outFile.WriteLine($"{loc.prefab},{loc.locationID},{locRegionName},,,,");

                    endloop:
                    return foundCity;
                }

                void OutputFile(string fileRelativePath)
                {
                    string modFilename = Path.GetFileName(fileRelativePath);

                    if (!writeLink)
                    {
                        foreach (LocationInstance loc in LocationHelper.LoadLocationInstance(mod, modFilename))
                        {
                            TryDumpLocation(loc);
                        }
                    }
                    else
                    {
                        TextAsset asset = mod.GetAsset<TextAsset>(modFilename);
                        TextReader assetReader = new StringReader(asset.text);

                        int line = 1;
                        string header = assetReader.ReadLine();
                        string[] originalFields = header.Split(',');

                        string[] newFields;
                        int extraDataIndex;
                        if (originalFields.Contains("extraData"))
                        {
                            newFields = originalFields;
                            extraDataIndex = Array.IndexOf(originalFields, "extraData");
                        }
                        else
                        {
                            newFields = (string[])originalFields.Clone();
                            UnityEditor.ArrayUtility.Add(ref newFields, "extraData");
                            extraDataIndex = newFields.Length - 1;
                        }

                        string fullAssetPath = Path.Combine(modFolder, "Locations", fileRelativePath);

                        using (StreamWriter locationFileWriter = new StreamWriter(fullAssetPath, append: false))
                        {
                            locationFileWriter.WriteLine(string.Join(",", newFields));
                            while (assetReader.Peek() > 0)
                            {
                                ++line;
                                string instanceLine = assetReader.ReadLine();

                                try
                                {
                                    string context = $"mod={mod.ModInfo.ModTitle}, file={modFilename}, line={line}";
                                    LocationInstance instance =
                                        LocationHelper.LoadSingleLocationInstanceCsv(instanceLine, originalFields,
                                            context);
                                    if (instance == null)
                                    {
                                        Debug.LogWarning($"({context}) Instance could not be parsed. Removing");
                                        continue;
                                    }

                                    if (TryDumpLocation(instance))
                                    {
                                        // Instance had a linked city written to it. Modify the line before dumping it back
                                        string[] instanceValues = instanceLine.Split(',');

                                        var serializedExtraData = instance.extraData.Replace("\"", "\\\"");
                                        serializedExtraData = $"\"{serializedExtraData}\"";

                                        if (newFields != originalFields)
                                        {
                                            UnityEditor.ArrayUtility.Add(ref instanceValues, serializedExtraData);
                                        }
                                        else
                                        {
                                            instanceValues[extraDataIndex] = serializedExtraData;
                                        }

                                        string modifiedInstanceLine = string.Join(",", instanceValues);
                                        locationFileWriter.WriteLine(modifiedInstanceLine);
                                    }
                                    else
                                    {
                                        if (newFields == originalFields)
                                        {
                                            locationFileWriter.WriteLine(instanceLine);
                                        }
                                        else
                                        {
                                            locationFileWriter.WriteLine(instanceLine + ",");
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError(e.Message);
                                }
                            }
                        }
                    }
                }

                ForEachModFile(OutputFile);
            }

            return "Success";
        }

        static string NameLocations(string[] Args)
        {
            string modName = null;
            string filePattern = null;

            bool parsingQuotedArg = false;
            StringBuilder quotedString = null;

            StringBuilder modNameBuilder = null;
            StringBuilder filePatternBuilder = null;

            if (Args.Length == 0)
                return "Missing arguments. See usage";

            for (int i = 0; i < Args.Length - 1; ++i)
            {
                string Arg = Args[i];
                if (parsingQuotedArg)
                {
                    string ArgValue = Arg;
                    if (Arg.EndsWith("\""))
                    {
                        parsingQuotedArg = false;
                        ArgValue = Arg.Substring(0, Arg.Length - 1);
                    }

                    quotedString.Append(" ").Append(ArgValue);
                }
                else if (Arg.StartsWith("--mod="))
                {
                    string value = Arg.Replace("--mod=", "");
                    if (value.StartsWith("\""))
                    {
                        quotedString = modNameBuilder = new StringBuilder(value.Substring(1));
                        parsingQuotedArg = true;
                    }
                    else
                    {
                        modName = value;
                    }
                }
                else if (Arg.StartsWith("--file="))
                {
                    string value = Arg.Replace("--file=", "");
                    if (value.StartsWith("\""))
                    {
                        quotedString = filePatternBuilder = new StringBuilder(value.Substring(1));
                        parsingQuotedArg = true;
                    }
                    else
                    {
                        filePattern = value;
                    }
                }
                else
                {
                    return $"Unknown argument '{Arg}'";
                }
            }

            if (modNameBuilder != null && modNameBuilder.Length > 0)
            {
                modName = modNameBuilder.ToString();
            }

            if (filePatternBuilder != null && filePatternBuilder.Length > 0)
            {
                filePattern = filePatternBuilder.ToString();
            }

            if (Args[Args.Length - 1].StartsWith("--"))
            {
                return $"Name type not speficied. See usage";
            }

            if (string.IsNullOrEmpty(modName))
            {
                return $"Loose files not yet supported. Specify a mod with --mod=<mod name>";
            }

            if (string.IsNullOrEmpty(filePattern))
            {
                return $"Need a file pattern to name. Specify it with --file=<file pattern>";
            }

            var type = Args[Args.Length - 1];

            var regex = MakeFilePattern(filePattern);

            Mod mod = ModManager.Instance.GetMod(modName);
            if (mod == null)
                return $"Mod '{modName}' not found";

            string dummyFilePath = mod.ModInfo.Files[0];
            string modFolderPrefix = dummyFilePath.Substring(17);
            modFolderPrefix = dummyFilePath.Substring(0, 17 + modFolderPrefix.IndexOf('/'));
            string modFolder = Path.Combine(Application.dataPath, modFolderPrefix.Substring(7));

            string locationsFolder = modFolderPrefix + "/Locations/";
            string locationPrefabsFolder = modFolderPrefix + "/Locations/LocationPrefab";

            void ForEachModFile(Action<string> Func)
            {
                foreach (string fileRelativePath in mod.ModInfo.Files
                             .Where(file =>
                                 (file.StartsWith(locationsFolder, StringComparison.InvariantCultureIgnoreCase) &&
                                  !file.StartsWith(locationPrefabsFolder, StringComparison.InvariantCultureIgnoreCase))
                                 && file.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase))
                             .Select(file => file.Substring(locationsFolder.Length)))
                {
                    var filename = Path.GetFileName(fileRelativePath).ToLower();

                    if (!regex.IsMatch(filename))
                        continue;

                    try
                    {
                        Func(fileRelativePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                    }
                }
            }

            void RenameFile(string fileRelativePath)
            {
                string modFilename = Path.GetFileName(fileRelativePath);

                TextAsset asset = mod.GetAsset<TextAsset>(modFilename);
                TextReader assetReader = new StringReader(asset.text);

                int line = 1;
                string header = assetReader.ReadLine();
                string[] fields = header.Split(',');

                int nameIndex = Array.IndexOf(fields, "name");
                if (nameIndex == -1)
                    throw new Exception($"Field 'name' could not be found in file '{modFilename}'");

                string fullAssetPath = Path.Combine(modFolder, "Locations", fileRelativePath);

                using (StreamWriter locationFileWriter = new StreamWriter(fullAssetPath, append: false))
                {
                    locationFileWriter.WriteLine(header);
                    while (assetReader.Peek() > 0)
                    {
                        ++line;
                        string instanceLine = assetReader.ReadLine();

                        string[] values = instanceLine.Split(',');

                        try
                        {
                            string context = $"mod={mod.ModInfo.ModTitle}, file={modFilename}, line={line}";
                            LocationInstance instance =
                                LocationHelper.LoadSingleLocationInstanceCsv(instanceLine, fields, context);

                            switch (type.ToLower())
                            {
                                case "dock":
                                    values[nameIndex] =
                                        LocationNameGenerator.GenerateDockName(instance, context: context);
                                    break;

                                case "bandit":
                                    values[nameIndex] =
                                        LocationNameGenerator.GenerateBanditCampName(instance, context: context);
                                    break;

                                default:
                                    throw new Exception($"Invalid name type '{type}'");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.Message);
                        }

                        locationFileWriter.WriteLine(string.Join(",", values));
                    }
                }
            }

            ForEachModFile(RenameFile);

            return "Success";
        }
#endif

        static string Here(string[] Args)
        {
            var PlayerGPS = GameManager.Instance.PlayerGPS;
            var MapPixel = PlayerGPS.CurrentMapPixel;


            var LL = LocationModLoader.modObject.GetComponent<LocationLoader>();
            if (!LL.TryGetTerrainExtraData(new Vector2Int(MapPixel.X, MapPixel.Y),
                    out LocationLoader.LLTerrainData extraData))
            {
                return "No information";
            }

            if (extraData.LocationInstances == null || extraData.LocationInstances.Count == 0)
            {
                return $"No instances on map pixel [{MapPixel.X}, {MapPixel.Y}]";
            }

            StringBuilder result = new StringBuilder();
            result.AppendLine($"LL instances at [{MapPixel.X}, {MapPixel.Y}]");
            foreach (LocationData instance in extraData.LocationInstances)
            {
                result.Append($"{instance.Location.name}: prefab={instance.Location.prefab}");
                if (instance.LocationEnemies.Any())
                {
                    result.Append($" enemies={instance.LocationEnemies.Count()}");
                }
                if (instance.LocationLoots.Any())
                {
                    result.Append($" loot={instance.LocationLoots.Count()}");
                }

                result.AppendLine();
            }

            return result.ToString();
        }
    }
}