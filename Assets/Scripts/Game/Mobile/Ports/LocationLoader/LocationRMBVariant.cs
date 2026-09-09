// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationRMBVariant.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Wenzil.Console;
using System.Collections.Generic;
using static DaggerfallConnect.DFRegion;
using System.Globalization;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace LocationLoader
{
    [Flags]
    enum EdgeCriteria
    {
        None, // Item is not on any edge
        N = 1,
        W = 2,
        S = 4,
        E = 8,
    }

    /*
        Concept: An RMB variant system with rules-based filtering and replacement

        Replacement rules should be stored in a CSV, with one rule per each row.
        Rules lower in the CSV should overwrite higher rules in case of conflict.
        A rule may consist of one or multiple conditions, and all conditions must be met for a block to be replaced.

        The CSV should contain the following fields:

        - OLD_BLOCK [string]: The block to be replaced. Note that all block fields should ideally work with block prefixes (RESIAM*) as well as specific blocks (RESIAM07).
        - NEW_BLOCK [string]: The replacement block.
        - LOC_ID [int]: Replace all OLD_BLOCK in specified location id with NEW_BLOCK.
        - LOC_TYPE [string]: Replace all OLD_BLOCK in specified location type with NEW_BLOCK.
        - REGION [string]: Replace all OLD_BLOCK in specified region with NEW_BLOCK.
        - CLIMATE [string]: Replace all OLD_BLOCK in specified climate with NEW_BLOCK.
        - DUNGEON_TYPE : Replace all OLD_BLOCK in specified dungeon type with NEW_BLOCK.
        - IS_PORT [int]: Replace OLD_BLOCK if location is a port.
        - N_EDGE [int]: Replace OLD_BLOCK if it is on the north edge of location. Used to replace wall blocks or place farm blocks.
        - E_EDGE [int]: Replace OLD_BLOCK if it is on the east edge of location.
        - S_EDGE [int]: Replace OLD_BLOCK if it is on the south edge of location.
        - W_EDGE [int]: Replace OLD_BLOCK if it is on the west edge of location.
        - NOT_EDGE [int]: Replace OLD_BLOCK if it is not on any edge of location.
        - BLOCK_PRESENT [string]: Replace OLD_BLOCK if another block (BLOCK_PRESENT) is found at location.
            For example, you could use this to place a barracks if a certain type of castle is present.
        - BLOCK_ABSENT [string]: Replace OLD_BLOCK if another block (BLOCK_ABSENT) is *not* found at location.
            You could use this to avoid placing a second stables at the south exit if one has already been placed at the east exit.
        - QUEST [string]: Replace OLD_BLOCK if a specific quest is active. (Note: may require an extra check to preserve changes after finishing a quest)
        - QUEST_STATE [vector]: Replace OLD_BLOCK if QUEST is at specific state(s).
        - LOCID_LASTNUM [vector]: This field takes a vector of numbers: [1,3,5,7,9].
            Replace OLD_BLOCK only in location ids that end in one of the given numbers.
            This allows for replacing half (or 20% or 70%) of a specific OLD_BLOCK, but not all, in a random-deterministic manner.
    */

    internal static class LocationRMBVariant
    {

        class Rule
        {
            public string TargetRMB { get; set; }
            public string ReplacementRMB { get; set; }

            public LocationTypes? LocType { get; set; }
            public string Region { get; set; }
            public MapsFile.Climates? Climate { get; set; }
            public DungeonTypes? DungeonType { get; set; }
            public bool? IsPort { get; set; }
            public string RequiredRMB { get; set; }
            public string ForbiddenRMB { get; set; }
            public int[] LocationIdParity { get; set; }
        }

        public static void RegisterCommands()
        {
#if UNITY_EDITOR
            ConsoleCommandsDatabase.RegisterCommand("LLGenerateRMBVariants", "Generate RMB variants from .rmb.csv rules lists"
                , "LLGenerateRMBVariants --mod=<modname> <variant name>", GenerateRMBVariants);

#endif
        }

#if UNITY_EDITOR
        public static string GenerateRMBVariants(string[] Args)
        {
            string modName = null;

            bool parsingQuotedArg = false;
            StringBuilder quotedString = null;
            StringBuilder modNameBuilder = null;

            if (Args.Length < 1)
                return "Missing <variant name> argument";

            int ArgsCount = Args.Length;
            foreach (string Arg in Args.Take(ArgsCount - 1))
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
                else
                {
                    return $"Unknown argument '{Arg}'";
                }
            }

            string variantName = Args[ArgsCount - 1];

            if (modNameBuilder != null && modNameBuilder.Length > 0)
            {
                modName = modNameBuilder.ToString();
            }

            if (string.IsNullOrEmpty(modName))
            {
                return $"Loose files not yet supported. Specify a mod with --mod=<mod name>";
            }

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

            const string TRAVELOPTIONS_MODNAME = "TravelOptions";
            Mod travelOptionsMod = ModManager.Instance.GetMod(TRAVELOPTIONS_MODNAME);
            bool travelOptionsEnabled = travelOptionsMod != null && travelOptionsMod.Enabled;

            bool IsPort(in DFLocation location)
            {
                if (travelOptionsEnabled)
                {
                    bool hasPort = false;
                    ModManager.Instance.SendModMessage(TRAVELOPTIONS_MODNAME, "hasPort", location.MapTableData.MapId,
                        (string message, object data) => { hasPort = (bool)data; });
                    return hasPort;
                }
                else
                {
                    return location.Exterior.ExteriorData.PortTownAndUnknown != 0 && DaggerfallBankManager.OwnsShip;
                }
            }

            void ForEachModFile(Action<string> Func)
            {
                foreach (string filePath in mod.ModInfo.Files
                    .Where(file => file.EndsWith(".rmb.csv", StringComparison.InvariantCultureIgnoreCase))
                    )
                {
                    try
                    {
                        Func(filePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                    }
                }
            }

            List<Rule> rules = new List<Rule>();
            Dictionary<int, List<Rule>> locationRules = new Dictionary<int, List<Rule>>();

            void ParseRules(string fileApplicationPath)
            {
                var filePath = Path.Combine(Application.dataPath, fileApplicationPath.Substring(7));
                var filename = Path.GetFileName(filePath);

                using(StreamReader ruleFile = new StreamReader(filePath))
                {
                    string header = ruleFile.ReadLine();

                    string[] fields = header.Split(';', ',').Select(field => field.Trim('\"')).ToArray();

                    bool GetIndex(string fieldName, out int index)
                    {
                        index = -1;
                        for (int i = 0; i < fields.Length; ++i)
                        {
                            if (fields[i].Equals(fieldName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                index = i;
                                break;
                            }
                        }
                        if (index == -1)
                        {
                            Debug.LogError($"RMB Rules file '{filename}': could not find field '{fieldName}' in header");
                            return false;
                        }
                        return true;
                    }

                    int? GetIndexOpt(string fieldName)
                    {
                        int index = -1;
                        for (int i = 0; i < fields.Length; ++i)
                        {
                            if (fields[i].Equals(fieldName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                index = i;
                                break;
                            }
                        }
                        if (index == -1)
                        {
                            return null;
                        }
                        return index;
                    }
                    if (!GetIndex("OLD_BLOCK", out int OldBlockIndex)) return; 
                    if (!GetIndex("NEW_BLOCK", out int NewBlockIndex)) return;

                    int? LocIdIndex = GetIndexOpt("LOC_ID");
                    int? LocTypeIndex = GetIndexOpt("LOC_TYPE");
                    int? RegionIndex = GetIndexOpt("REGION");
                    int? ClimateIndex = GetIndexOpt("CLIMATE");
                    int? DungeonTypeIndex = GetIndexOpt("DUNGEON_TYPE");
                    int? IsPortIndex = GetIndexOpt("IS_PORT");
                    /*
                    int? NorthEdgeIndex = GetIndexOpt("N_EDGE");
                    int? EastEdgeIndex = GetIndexOpt("E_EDGE");
                    int? SouthEdgeIndex = GetIndexOpt("S_EDGE");
                    int? WestEdgeIndex = GetIndexOpt("W_EDGE");
                    int? NotEdgeIndex = GetIndexOpt("NOT_EDGE");
                    */
                    int? BlockPresentIndex = GetIndexOpt("BLOCK_PRESENT");
                    int? BlockAbsentIndex = GetIndexOpt("BLOCK_ABSENT");
                    int? LocIdParityIndex = GetIndexOpt("LOCID_LASTNUM");

                    CultureInfo cultureInfo = new CultureInfo("en-US");
                    int lineNumber = 1;
                    while (ruleFile.Peek() >= 0)
                    {
                        ++lineNumber;

                        string line = ruleFile.ReadLine();

                        try
                        {
                            string[] tokens = LocationHelper.SplitCsvLine(line);

                            ushort? locId = null;

                            Rule currentRule = new Rule();
                            currentRule.TargetRMB = tokens[OldBlockIndex];
                            if (!currentRule.TargetRMB.EndsWith(".RMB", StringComparison.InvariantCultureIgnoreCase))
                                currentRule.TargetRMB = currentRule.TargetRMB + ".RMB";
                            currentRule.ReplacementRMB = tokens[NewBlockIndex];
                            if (!currentRule.ReplacementRMB.EndsWith(".RMB", StringComparison.InvariantCultureIgnoreCase))
                                currentRule.ReplacementRMB = currentRule.ReplacementRMB + ".RMB";

                            if (LocIdIndex.HasValue && !string.IsNullOrEmpty(tokens[LocIdIndex.Value]))
                            {
                                locId = ushort.Parse(tokens[LocIdIndex.Value]);
                            }

                            if (LocTypeIndex.HasValue && !string.IsNullOrEmpty(tokens[LocTypeIndex.Value]))
                            {
                                currentRule.LocType = (LocationTypes)Enum.Parse(typeof(LocationTypes), tokens[LocTypeIndex.Value], ignoreCase: true); 
                            }

                            if (RegionIndex.HasValue && !string.IsNullOrEmpty(tokens[RegionIndex.Value]))
                            {
                                currentRule.Region = tokens[RegionIndex.Value];
                            }
                            
                            if (ClimateIndex.HasValue && !string.IsNullOrEmpty(tokens[ClimateIndex.Value]))
                            {
                                currentRule.Climate = (MapsFile.Climates)Enum.Parse(typeof(MapsFile.Climates), tokens[ClimateIndex.Value], ignoreCase: true);
                            }

                            if (DungeonTypeIndex.HasValue && !string.IsNullOrEmpty(tokens[DungeonTypeIndex.Value]))
                            {
                                currentRule.DungeonType = (DungeonTypes)Enum.Parse(typeof(DungeonTypes), tokens[DungeonTypeIndex.Value], ignoreCase: true);
                            }

                            if (IsPortIndex.HasValue && !string.IsNullOrEmpty(tokens[IsPortIndex.Value]))
                            {
                                currentRule.IsPort = LocationHelper.ParseBool(tokens[IsPortIndex.Value], $"{filename}:{lineNumber}:{IsPortIndex.Value + 1}");
                            }

                            if (BlockPresentIndex.HasValue && !string.IsNullOrEmpty(tokens[BlockPresentIndex.Value]))
                            {
                                currentRule.RequiredRMB = tokens[BlockPresentIndex.Value];
                            }

                            if (BlockAbsentIndex.HasValue && !string.IsNullOrEmpty(tokens[BlockAbsentIndex.Value]))
                            {
                                currentRule.ForbiddenRMB = tokens[BlockAbsentIndex.Value];
                            }

                            if (LocIdParityIndex.HasValue && !string.IsNullOrEmpty(tokens[LocIdParityIndex.Value]))
                            {
                                currentRule.LocationIdParity = LocationHelper.ParseIntArrayArg(tokens[LocIdParityIndex.Value], $"{filename}:{lineNumber}:{LocIdParityIndex.Value + 1}");
                            }

                            if(locId.HasValue)
                            {
                                if(!locationRules.TryGetValue(locId.Value, out List<Rule> locRules))
                                {
                                    locRules = new List<Rule>();
                                    locationRules.Add(locId.Value, locRules);
                                }

                                locRules.Add(currentRule);
                            }
                            else
                            {
                                rules.Add(currentRule);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error while parsing {filename}:{lineNumber}: {ex}");
                        }
                    }

                }
            }

            ForEachModFile(ParseRules);

            if (rules.Count == 0)
                return "No RMB rules found";

            IEnumerable<Rule> filteredRules = rules;

            int GeneratedVariants = 0;

            // Traverse all locations, apply rules
            MapsFile mapFileReader = DaggerfallUnity.Instance.ContentReader.MapFileReader;

            for (int regionIndex = 0; regionIndex < mapFileReader.RegionCount; ++regionIndex)
            {
                DFRegion region = mapFileReader.GetRegion(regionIndex);

                filteredRules = filteredRules.Where(r => string.IsNullOrEmpty(r.Region) || string.Equals(r.Region, region.Name, StringComparison.InvariantCultureIgnoreCase));

                if (filteredRules.Count() == 0)
                    continue;

                for (int locationIndex = 0; locationIndex < region.LocationCount; ++locationIndex)
                {
                    DFLocation location = mapFileReader.GetLocation(regionIndex, locationIndex);
                    ref DFRegion.RegionMapTable mapTable = ref location.MapTableData;
                    ref DFLocation.ExteriorData exteriorData = ref location.Exterior.ExteriorData;

                    int locationId = mapFileReader.ReadLocationIdFast(regionIndex, locationIndex);

                    DFPosition mapPosition = MapsFile.LongitudeLatitudeToMapPixel(mapTable.Longitude, mapTable.Latitude);
                    MapsFile.Climates locationClimate = (MapsFile.Climates)mapFileReader.GetClimateIndex(mapPosition.X, mapPosition.Y);
                    bool isPort = IsPort(location);

                    bool HasLocationMatch(Rule rule)
                    {
                        // Region rules are already filtered out, no need to check again here

                        ref DFRegion.RegionMapTable mapTable_ = ref location.MapTableData;
                        ref DFLocation.ExteriorData exteriorData_ = ref location.Exterior.ExteriorData;

                        if (rule.LocType.HasValue && mapTable_.LocationType != rule.LocType.Value)
                        {
                            return false;
                        }

                        if(rule.Climate.HasValue && locationClimate != rule.Climate.Value)
                        {
                            return false;
                        }

                        if (rule.DungeonType.HasValue && (!location.HasDungeon || mapTable_.DungeonType != rule.DungeonType.Value))
                        {
                            return false;
                        }

                        if (rule.IsPort.HasValue && rule.IsPort != isPort)
                        {
                            return false;
                        }

                        if(rule.LocationIdParity != null)
                        {
                            int decimalParity = mapTable_.LocationId % 10;
                            if(!rule.LocationIdParity.Contains(decimalParity))
                            {
                                return false;
                            }
                        }

                        // Slow checks
                        if (!exteriorData_.BlockNames.Contains(rule.TargetRMB, StringComparer.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        if (!string.IsNullOrEmpty(rule.RequiredRMB) && !exteriorData_.BlockNames.Contains(rule.RequiredRMB, StringComparer.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        if (!string.IsNullOrEmpty(rule.ForbiddenRMB) && exteriorData_.BlockNames.Contains(rule.ForbiddenRMB, StringComparer.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        return true;
                    }

                    void ApplyRule(Rule rule)
                    {
                        ref DFLocation.ExteriorData exteriorData_ = ref location.Exterior.ExteriorData;

                        for (int blockIndex = 0; blockIndex < exteriorData_.BlockNames.Length; ++blockIndex)
                        {
                            if (string.Equals(exteriorData_.BlockNames[blockIndex], rule.TargetRMB, StringComparison.InvariantCultureIgnoreCase))
                            {
                                exteriorData_.BlockNames[blockIndex] = rule.ReplacementRMB;
                            }
                        }
                    }

                    if(locationRules.TryGetValue(locationId, out List<Rule> currentLocRules))
                    {
                        foreach(Rule rule in currentLocRules­)
                        {
                            if(!HasLocationMatch(rule))
                            {
                                Debug.LogWarning($"Rule for location id '{locationId}' didn't match location");
                                continue;
                            }

                            ApplyRule(rule);
                        }
                    }
                                        
                    var matchedRules = filteredRules.Where(HasLocationMatch);
                    if (matchedRules.Count() == 0)
                        continue;

                    // Rules in reverse: bottom rule wins
                    foreach (Rule rule in matchedRules.Reverse())
                    {
                        ApplyRule(rule);
                    }

                    string locJson = SaveLoadManager.Serialize(typeof(DFLocation), location);
                    string fileName = WorldDataReplacement.GetDFLocationReplacementFilename(location.RegionIndex, location.LocationIndex, variantName);
                    var outputFilepath = Path.Combine(modFolder, "WorldData", fileName);
                    File.WriteAllText(outputFilepath, locJson);

                    ++GeneratedVariants;
                }
            }

            return $"Success ({GeneratedVariants} generated)";
        }

#endif
    }
}
