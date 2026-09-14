// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/BestiaryMod.cs, copied unchanged for iOS except lines marked MOBILE. 1 [Invoke]
// attribute(s) removed: MobilePortedMods calls DEXPort.Init when the mod entry is enabled.
// Upstream declares no licence and no copyright header; see THIRD-PARTY.md and mods.json.
//
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallConnect;
using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using System.Linq;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallConnect.Save;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace DaggerfallBestiaryProject
{
    public class BestiaryMod : MonoBehaviour
    {
        private static Mod mod;

        private static bool disableTrollRespawn = false;

        public static bool DisableTrollRespawn { get { return disableTrollRespawn; } }

        public static BestiaryMod Instance { get; private set; }

        // MOBILE: what the databases produced, for the launcher log line and MobileSelfTest. Static so
        // a reader can ask without a live Instance; set once, at the end of Start.
        public static int loadedEnemies;
        public static int loadedCareers;
        public static int loadedTables;
        /// <summary>MOBILE: true once Start parsed the databases - what DEXPort.Loaded reports.</summary>
        public static bool DatabasesLoaded { get { return loadedEnemies > 0; } }
        public static BestiarySaveInterface SaveInterface { get { return Instance.GetComponent<BestiarySaveInterface>(); } }

        public class CustomCareer
        {
            public DFCareer dfCareer;
        }

        private Dictionary<string, CustomCareer> customCareers = new Dictionary<string, CustomCareer>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CustomCareer> CustomCareers { get { return customCareers; } }

        public class CustomEnemyProperties
        {
            public string name;
            public string career;
            public DFCareer.EnemyGroups group = DFCareer.EnemyGroups.None;
            public DFCareer.Skills language = DFCareer.Skills.None;
            public string spellbookTable;
            public int onHitEffect;
            public DFBlock.EnemyGenders forcedGender;
            public bool isTransparent;
            public bool isSkeletal;
            public bool hasRangedAttack;
            public bool hasNoSpellAnimations;
            public string equipmentTable;
        }

        private Dictionary<int, CustomEnemyProperties> customEnemies = new Dictionary<int, CustomEnemyProperties>();

        public Dictionary<int, CustomEnemyProperties> CustomEnemies { get { return customEnemies; } }

        private Dictionary<int, CustomEnemyProperties> classicEnemies = new Dictionary<int, CustomEnemyProperties>();

        public bool GetCustomProperties(int mobileID, out CustomEnemyProperties customEnemyProperties)
        {
            if(Enum.IsDefined(typeof(MobileTypes), mobileID))
            {
                return classicEnemies.TryGetValue(mobileID, out customEnemyProperties);
            }
            else
            {
                return customEnemies.TryGetValue(mobileID, out customEnemyProperties);
            }            
        }

        public class EncounterTable
        {
            public string name;
            public int[] tableIndices;
            public int[] enemyIds;
        }

        Dictionary<int, List<EncounterTable>> dungeonTypeTables = new Dictionary<int, List<EncounterTable>>();
        Dictionary<string, EncounterTable> encounterTables = new Dictionary<string, EncounterTable>(StringComparer.OrdinalIgnoreCase);
        bool readDefaultTables = false;

        class Spellbook
        {
            public int[] spellIds;
            public int minLevel;
        }

        class SpellbookTable
        {
            public Spellbook[] spellbooks;

            public Spellbook GetSpellbook(int level)
            {
                return spellbooks.LastOrDefault(spellbook => level >= spellbook.minLevel);
            }
        }

        Dictionary<string, SpellbookTable> spellbookTables = new Dictionary<string, SpellbookTable>(StringComparer.OrdinalIgnoreCase);

        // Returns true if the id is a monster id. False if it's a career id
        // The id doesn't have to refer to an actual enemy
        public static bool IsMonster(int enemyId)
        {
            // Ids 0 to 127 are monsters
            // 128 to 255 is classes
            // and these two alternate every 128 ids
            return ((enemyId / 128) % 2) == 0;
        }

        // MOBILE: [Invoke(StateManager.StateTypes.Start, 0)] removed - DFU's loader only invokes
        // attributes on a mod it loaded from a .dfmod assembly. This code is compiled into the app,
        // so MobilePortedMods calls DEXPort.Init (which calls this) when the launcher entry is on.
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<BestiaryMod>();
            mod.SaveDataInterface = go.AddComponent<BestiarySaveInterface>();

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            mod.MessageReceiver = ModMessage;

            mod.IsReady = true;
        }
        
        static void LoadSettings(ModSettings modSettings, ModSettingsChange change)
        {
            disableTrollRespawn = modSettings.GetBool("Cheat", "DisableTrollRespawn");
        }

        static void ModMessage(string message, object data, DFModMessageCallback callBack)
        {
            switch(message)
            {
            case "getEnemySpells":
                var (mobileId, level) = (System.Tuple<int, int>)data;
                if(!Instance.GetCustomProperties(mobileId, out CustomEnemyProperties customEnemy))
                {
                    callBack(message, null);
                }
                else
                {
                    if(string.IsNullOrEmpty(customEnemy.spellbookTable))
                    {
                        callBack(message, null);
                    }
                    else if(!Instance.spellbookTables.TryGetValue(customEnemy.spellbookTable, out SpellbookTable spellbookTable))
                    {
                        Debug.LogError($"[DEX] Unknown enemy spell table '{customEnemy.spellbookTable}'");
                        callBack(message, null);
                    }
                    else
                    { 
                        Spellbook spellbook = spellbookTable.GetSpellbook(level);
                        callBack(message, spellbook.spellIds);
                    }
                }
                break;
            }
        }

        private void Start()
        {
            DaggerfallUnity.Instance.TextProvider = new BestiaryTextProvider(DaggerfallUnity.Instance.TextProvider);
			
            ParseDfCareers();
            // MOBILE: the vanilla careers ParseDfCareers just added, so the line below can say how many
            // careers the CSV databases brought rather than how many the dictionary holds.
            int dfCareerCount = customCareers.Count;
            ParseCustomCareers();
            ParseCustomEnemies();
            ParseEncounterTables();
            ParseSpellbookTables();

            // MOBILE: one line saying what the six databases in the bundle actually produced. The
            // shipped set is 50 enemies (35 monsters 256-383, 15 class enemies 384-511), 43 careers and
            // 39 encounter tables; a bundle that failed to load says 0 on every count and every [DEX]
            // parse error above it says why. encounterTables holds only the parsed .tdb rows at this
            // point - the 38 vanilla defaults are added by Update(), on the frame after this one.
            loadedEnemies = customEnemies.Count;
            loadedCareers = customCareers.Count - dfCareerCount;
            loadedTables = encounterTables.Count;
            Debug.Log($"[DEX] loaded {loadedEnemies} enemies, {loadedCareers} careers, {loadedTables} tables");

            EnemyEntity.OnLootSpawned += OnEnemySpawn;
            FormulaHelper.RegisterOverride<Action<EnemyEntity, DaggerfallEntity, int>>(mod, "OnMonsterHit", OnMonsterHit);
            FormulaHelper.RegisterOverride<Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int>>(mod, "CalculateWeaponAttackDamage", CalculateWeaponAttackDamage);
            FormulaHelper.RegisterOverride<Func<EnemyEntity, DFCareer.EnemyGroups>>(mod, "GetEnemyEntityEnemyGroup", GetEnemyGroup);
            FormulaHelper.RegisterOverride<Func<EnemyEntity, DFCareer.Skills>>(mod, "GetEnemyEntityLanguageSkill", GetEnemyLanguage);
            PlayerEnterExit.OnPreTransition += PlayerEnterExit_OnPreTransition;
            PlayerGPS.OnEnterLocationRect += PlayerGPS_OnEnterLocationRect;
            PlayerGPS.OnExitLocationRect += PlayerGPS_OnExitLocationRect;
            PlayerGPS.OnClimateIndexChanged += PlayerGPS_OnClimateIndexChanged;

            EnemyDeath.OnEnemyDeath += EnemyDeath_OnEnemyDeath;

            EnemyEntity.AssignEnemyEquipment = AssignEnemyStartingEquipment;

            DEX_RRICompat.OnStart();
        }

        List<EncounterTable> GetIndexEncounterTables(int index)
        {
            if(!dungeonTypeTables.TryGetValue(index, out List<EncounterTable> encounterTables))
            {
                encounterTables = new List<EncounterTable>();
                dungeonTypeTables.Add(index, encounterTables);
            }

            return encounterTables;
        }

        private void Update()
        {
            // Run this post Start so we can get modded default encounter tables
            if(!readDefaultTables)
            {
                void CreateDefaultTable(int index, string name)
                {
                    if (encounterTables.ContainsKey(name))
                    {
                        if(GetIndexEncounterTables(index).Count == 0)
                            Debug.LogError($"[DEX] Encounter index '{name}' has no tables");
                        return;
                    }

                    List<EncounterTable> indexEncounterTables = GetIndexEncounterTables(index);

                    EncounterTable table = new EncounterTable();
                    table.name = name;
                    table.enemyIds = RandomEncounters.EncounterTables[index].Enemies.Select(id => (int)id).ToArray();

                    indexEncounterTables.Add(table);
                    encounterTables.Add(table.name, table);
                }

                // Parse the 19 dungeon types
                int dungeonTypeCount = Enum.GetValues(typeof(DFRegion.DungeonTypes)).Length - 1;

                for (int i = 0; i < dungeonTypeCount; ++i)
                {
                    CreateDefaultTable(i, $"Default{(DFRegion.DungeonTypes)i}");
                }

                // Parse underwater
                CreateDefaultTable(19, "DefaultUnderwater");

                // Parse city night for Desert/Subtropical/Swamp/Haunted Woodlands
                CreateDefaultTable(20, "DefaultDesertCityNight");
                CreateDefaultTable(21, "DefaultDesertDay");
                CreateDefaultTable(22, "DefaultDesertNight");
                CreateDefaultTable(23, "DefaultMountainCityNight");
                CreateDefaultTable(24, "DefaultMountainDay");
                CreateDefaultTable(25, "DefaultMountainNight");
                CreateDefaultTable(26, "DefaultRainforestCityNight");
                CreateDefaultTable(27, "DefaultRainforestDay");
                CreateDefaultTable(28, "DefaultRainforestNight");
                CreateDefaultTable(29, "DefaultSubtropicalCityNight");
                CreateDefaultTable(30, "DefaultSubtropicalDay");
                CreateDefaultTable(31, "DefaultSubtropicalNight");
                CreateDefaultTable(32, "DefaultWoodlandsCityNight");
                CreateDefaultTable(33, "DefaultWoodlandsDay");
                CreateDefaultTable(34, "DefaultWoodlandsNight");
                CreateDefaultTable(35, "DefaultHauntedCityNight");
                CreateDefaultTable(36, "DefaultHauntedDay");
                CreateDefaultTable(37, "DefaultHauntedNight");

                // No Building tables for now

                readDefaultTables = true; 
            }
        }

        IEnumerable<Mod> EnumerateEnabledMods()
        {
            IEnumerable<Mod> query = ModManager.Instance.Mods;

            return query.Where(x => x.Enabled);
        }

        IEnumerable<TextAsset> GetDBAssets(string extension)
        {
            HashSet<string> names = new HashSet<string>();
            foreach(Mod mod in EnumerateEnabledMods())
            {
                foreach(string file in mod.ModInfo.Files.Where(filePath => filePath.EndsWith(extension)).Select(filePath => Path.GetFileName(filePath)))
                {
                    names.Add(file);
                }
            }

            foreach(string name in names)
            {
                ModManager.Instance.TryGetAsset(name, clone: false, out TextAsset asset);
                yield return asset;
            }
        }

        void ParseDfCareers()
        {
            foreach(MonsterCareers career in Enum.GetValues(typeof(MonsterCareers)).Cast<MonsterCareers>())
            {
                DFCareer dfCareer = DaggerfallEntity.GetMonsterCareerTemplate(career);
                if(dfCareer != null)
                {
                    customCareers.Add(career.ToString(), new CustomCareer { dfCareer = dfCareer });
                }
            }

            foreach (ClassCareers career in Enum.GetValues(typeof(ClassCareers)).Cast<ClassCareers>())
            {
                DFCareer dfCareer = DaggerfallEntity.GetClassCareerTemplate(career);
                if (dfCareer != null)
                {
                    customCareers.Add(career.ToString(), new CustomCareer { dfCareer = dfCareer });
                }
            }

            {
                // Handle guard manually, it's not in the enum
                DFCareer dfCareer = DaggerfallEntity.GetClassCareerTemplate((ClassCareers)18);
                if (dfCareer != null)
                {
                    customCareers.Add("Guard", new CustomCareer { dfCareer = dfCareer });
                }
            }
        }

        void ParseCustomCareers()
        {
            foreach (TextAsset asset in GetDBAssets(".cdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();

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
                        Debug.LogError($"[DEX] Career DB file '{asset.name}': could not find field '{fieldName}' in header");
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

                if (!GetIndex("Name", out int NameIndex)) continue;

                int? HPIndex = GetIndexOpt("HitPointsPerLevel");
                int? StrengthIndex = GetIndexOpt("Strength");
                int? IntelligenceIndex = GetIndexOpt("Intelligence");
                int? WillpowerIndex = GetIndexOpt("Willpower");
                int? AgilityIndex = GetIndexOpt("Agility");
                int? EnduranceIndex = GetIndexOpt("Endurance");
                int? PersonalityIndex = GetIndexOpt("Personality");
                int? SpeedIndex = GetIndexOpt("Speed");
                int? LuckIndex = GetIndexOpt("Luck");

                int? magicToleranceIndex = GetIndexOpt("Magic");
                int? fireToleranceIndex = GetIndexOpt("Fire");
                int? frostToleranceIndex = GetIndexOpt("Frost");
                int? shockToleranceIndex = GetIndexOpt("Shock");
                int? poisonToleranceIndex = GetIndexOpt("Poison");
                int? paralysisToleranceIndex = GetIndexOpt("Paralysis");

                int? UndeadAttackModifierIndex = GetIndexOpt("UndeadAttackModifier");
                int? DaedraAttackModifierIndex = GetIndexOpt("DaedraAttackModifier");
                int? HumanoidAttackModifierIndex = GetIndexOpt("HumanoidAttackModifier");
                int? AnimalsAttackModifierIndex = GetIndexOpt("AnimalsAttackModifier");
                
                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();

                    try
                    {
                        string[] tokens = SplitCsvLine(line);

                        string careerName = tokens[NameIndex];

                        bool replacement;
                        DFCareer career;
                        if (customCareers.TryGetValue(careerName, out CustomCareer existingCustomCareer))
                        {
                            replacement = true;
                            career = existingCustomCareer.dfCareer;
                        }
                        else
                        {
                            replacement = false;
                            career = new DFCareer();
                            career.Name = careerName;
                        }

                        if (HPIndex.HasValue && !string.IsNullOrEmpty(tokens[HPIndex.Value]))
                        {
                            career.HitPointsPerLevel = int.Parse(tokens[HPIndex.Value], cultureInfo);
                        }
                        else if(!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{careerName}' did not have HitPointsPerLevel specified");
                            continue;
                        }

                        if (StrengthIndex.HasValue && !string.IsNullOrEmpty(tokens[StrengthIndex.Value]))
                        {
                            career.Strength = int.Parse(tokens[StrengthIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Strength specified");
                            continue;
                        }

                        if (IntelligenceIndex.HasValue && !string.IsNullOrEmpty(tokens[IntelligenceIndex.Value]))
                        {
                            career.Intelligence = int.Parse(tokens[IntelligenceIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Intelligence specified");
                            continue;
                        }

                        if (WillpowerIndex.HasValue && !string.IsNullOrEmpty(tokens[WillpowerIndex.Value]))
                        {
                            career.Willpower = int.Parse(tokens[WillpowerIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Willpower specified");
                            continue;
                        }

                        if (AgilityIndex.HasValue && !string.IsNullOrEmpty(tokens[AgilityIndex.Value]))
                        {
                            career.Agility = int.Parse(tokens[AgilityIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Agility specified");
                            continue;
                        }

                        if (EnduranceIndex.HasValue && !string.IsNullOrEmpty(tokens[EnduranceIndex.Value]))
                        {
                            career.Endurance = int.Parse(tokens[EnduranceIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Strength specified");
                            continue;
                        }

                        if (EnduranceIndex.HasValue && !string.IsNullOrEmpty(tokens[EnduranceIndex.Value]))
                        {
                            career.Endurance = int.Parse(tokens[EnduranceIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Strength specified");
                            continue;
                        }

                        if (PersonalityIndex.HasValue && !string.IsNullOrEmpty(tokens[PersonalityIndex.Value]))
                        {
                            career.Personality = int.Parse(tokens[PersonalityIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Personality specified");
                            continue;
                        }

                        if (SpeedIndex.HasValue && !string.IsNullOrEmpty(tokens[SpeedIndex.Value]))
                        {
                            career.Speed = int.Parse(tokens[SpeedIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Speed specified");
                            continue;
                        }

                        if (LuckIndex.HasValue && !string.IsNullOrEmpty(tokens[LuckIndex.Value]))
                        {
                            career.Luck = int.Parse(tokens[LuckIndex.Value], cultureInfo);
                        }
                        else if (!replacement)
                        {
                            Debug.LogError($"[DEX] Career '{career.Name}' did not have Luck specified");
                            continue;
                        }

                        if(magicToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[magicToleranceIndex.Value]))
                        {
                            career.Magic = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[magicToleranceIndex.Value]);
                        }

                        if (fireToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[fireToleranceIndex.Value]))
                        {
                            career.Fire = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[fireToleranceIndex.Value]);
                        }

                        if (frostToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[frostToleranceIndex.Value]))
                        {
                            career.Frost = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[frostToleranceIndex.Value]);
                        }

                        if (shockToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[shockToleranceIndex.Value]))
                        {
                            career.Shock = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[shockToleranceIndex.Value]);
                        }

                        if (poisonToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[poisonToleranceIndex.Value]))
                        {
                            career.Poison = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[poisonToleranceIndex.Value]);
                        }

                        if (paralysisToleranceIndex.HasValue && !string.IsNullOrEmpty(tokens[paralysisToleranceIndex.Value]))
                        {
                            career.Paralysis = (DFCareer.Tolerance)Enum.Parse(typeof(DFCareer.Tolerance), tokens[paralysisToleranceIndex.Value]);
                        }

                        if (UndeadAttackModifierIndex.HasValue && !string.IsNullOrEmpty(tokens[UndeadAttackModifierIndex.Value]))
                        {
                            career.UndeadAttackModifier = (DFCareer.AttackModifier)Enum.Parse(typeof(DFCareer.AttackModifier), tokens[UndeadAttackModifierIndex.Value]);
                        }

                        if (DaedraAttackModifierIndex.HasValue && !string.IsNullOrEmpty(tokens[DaedraAttackModifierIndex.Value]))
                        {
                            career.DaedraAttackModifier = (DFCareer.AttackModifier)Enum.Parse(typeof(DFCareer.AttackModifier), tokens[DaedraAttackModifierIndex.Value]);
                        }

                        if (HumanoidAttackModifierIndex.HasValue && !string.IsNullOrEmpty(tokens[HumanoidAttackModifierIndex.Value]))
                        {
                            career.HumanoidAttackModifier = (DFCareer.AttackModifier)Enum.Parse(typeof(DFCareer.AttackModifier), tokens[HumanoidAttackModifierIndex.Value]);
                        }

                        if (AnimalsAttackModifierIndex.HasValue && !string.IsNullOrEmpty(tokens[AnimalsAttackModifierIndex.Value]))
                        {
                            career.AnimalsAttackModifier = (DFCareer.AttackModifier)Enum.Parse(typeof(DFCareer.AttackModifier), tokens[AnimalsAttackModifierIndex.Value]);
                        }

                        if (!replacement)
                        {
                            CustomCareer customCareer = new CustomCareer();
                            customCareer.dfCareer = career;

                            customCareers.Add(career.Name, customCareer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DEX] Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }
        }

        static Regex CsvSplit = new Regex("(?:^|,)(\"(?:\\\\\"|[^\"])*\"|[^,]*)", RegexOptions.Compiled);

        static string[] SplitCsvLine(string line)
        {
            List<string> list = new List<string>();
            foreach (Match match in CsvSplit.Matches(line))
            {
                string curr = match.Value;
                if (0 == curr.Length)
                {
                    list.Add("");
                }

                list.Add(curr.TrimStart(',', ';').Replace("\\\"", "\"").Trim('\"'));
            }

            return list.ToArray();
        }

        int[] ParseArrayArg(string Arg, string Context)
        {
            if (string.IsNullOrEmpty(Arg))
                return Array.Empty<int>();

            // Strip brackets
            if(Arg[0] == '[' || Arg[0] == '{')
            {
                // Check for end bracket
                if(Arg[0] == '[' && Arg[Arg.Length - 1] != ']'
                    || Arg[0] == '{' && Arg[Arg.Length - 1] != '}')
                    throw new InvalidDataException($"Error parsing ({Context}): array argument has mismatched brackets");

                Arg = Arg.Substring(1, Arg.Length - 2);
            }

            string[] Frames = Arg.Split(',', ';');
            return Frames.Select(Frame => string.IsNullOrEmpty(Frame) ? "-1" : Frame).Select(int.Parse).ToArray();
        }

        float[] ParseFloatArrayArg(string Arg, string Context)
        {
            if (string.IsNullOrEmpty(Arg))
                return Array.Empty<float>();

            // Strip brackets
            if (Arg[0] == '[' || Arg[0] == '{')
            {
                // Check for end bracket
                if (Arg[0] == '[' && Arg[Arg.Length - 1] != ']'
                    || Arg[0] == '{' && Arg[Arg.Length - 1] != '}')
                    throw new InvalidDataException($"Error parsing ({Context}): array argument has mismatched brackets");

                Arg = Arg.Substring(1, Arg.Length - 2);
            }

            string[] Frames = Arg.Split(',', ';');
            return Frames.Select(Frame => string.IsNullOrEmpty(Frame) ? "-1" : Frame).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }

        bool ParseBool(string Value, string Context)
        {
            if (string.IsNullOrEmpty(Value))
                return false;

            if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new InvalidDataException($"Error parsing ({Context}): invalid boolean value '{Value}'");
        }

        void ParseCustomEnemies()
        {
            List<MobileEnemy> enemies = EnemyBasics.Enemies.ToList();
            List<string> questEnemyLines = new List<string>();

            foreach (TextAsset asset in GetDBAssets(".mdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();
                if (header == null)
                    continue;

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
                        Debug.LogError($"[DEX] Monster DB file '{asset.name}': could not find field '{fieldName}' in header");
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

                if (!GetIndex("ID", out int IdIndex)) continue;
                int? NameIndex = GetIndexOpt("Name");
                int? CareerIndex = GetIndexOpt("Career");
                int? MaleTextureIndex = GetIndexOpt("MaleTexture");
                int? FemaleTextureIndex = GetIndexOpt("FemaleTexture");
                int? CorpseTextureArchiveIndex = GetIndexOpt("CorpseTextureArchive");
                int? CorpseTextureRecordIndex = GetIndexOpt("CorpseTextureRecord");
                int? FemaleCorpseTextureArchiveIndex = GetIndexOpt("FemaleCorpseTextureArchive");
                int? FemaleCorpseTextureRecordIndex = GetIndexOpt("FemaleCorpseTextureRecord");
                int? HasIdleIndex = GetIndexOpt("HasIdle");
                int? CastsMagicIndex = GetIndexOpt("CastsMagic");
                int? NoSpellAnimsIndex = GetIndexOpt("NoSpellAnims");
                int? HasRangedAttackIndex = GetIndexOpt("HasRangedAttack");
                int? PrimaryAttackAnimFramesIndex = GetIndexOpt("PrimaryAttackAnimFrames");
                int? TeamIndex = GetIndexOpt("Team");
                int? LevelIndex = GetIndexOpt("Level");
                int? BehaviourIndex = GetIndexOpt("Behaviour");
                int? AffinityIndex = GetIndexOpt("Affinity");
                int? GroupIndex = GetIndexOpt("Group");
                int? LanguageIndex = GetIndexOpt("Language");
                int? MinDamageIndex = GetIndexOpt("MinDamage");
                int? MaxDamageIndex = GetIndexOpt("MaxDamage");
                int? MinDamage2Index = GetIndexOpt("MinDamage2");
                int? MaxDamage2Index = GetIndexOpt("MaxDamage2");
                int? MinDamage3Index = GetIndexOpt("MinDamage3");
                int? MaxDamage3Index = GetIndexOpt("MaxDamage3");
                int? MinHealthIndex = GetIndexOpt("MinHealth");
                int? MaxHealthIndex = GetIndexOpt("MaxHealth");
                int? ArmorValueIndex = GetIndexOpt("ArmorValue");
                int? MinMetalToHitIndex = GetIndexOpt("MinMetalToHit");
                int? WeightIndex = GetIndexOpt("Weight");
                int? SeesThroughInvisibilityIndex = GetIndexOpt("SeesThroughInvisibility");
                int? MoveSoundIndex = GetIndexOpt("MoveSound");
                int? BarkSoundIndex = GetIndexOpt("BarkSound");
                int? AttackSoundIndex = GetIndexOpt("AttackSound");
                int? ParrySoundsIndex = GetIndexOpt("ParrySounds");
                int? CanOpenDoorsIndex = GetIndexOpt("CanOpenDoors");
                int? LootTableKeyIndex = GetIndexOpt("LootTableKey");
                int? MapChanceIndex = GetIndexOpt("MapChance");
                int? SpellBookIndex = GetIndexOpt("Spellbook");
                int? OnHitIndex = GetIndexOpt("OnHit");
                int? NoBloodIndex = GetIndexOpt("NoBlood");
                int? PrimaryAttackAnimFrames2Index = GetIndexOpt("PrimaryAttackAnimFrames2");
                int? ChanceForAttack2Index = GetIndexOpt("ChanceForAttack2");
                int? PrimaryAttackAnimFrames3Index = GetIndexOpt("PrimaryAttackAnimFrames3");
                int? ChanceForAttack3Index = GetIndexOpt("ChanceForAttack3");
                int? PrimaryAttackAnimFrames4Index = GetIndexOpt("PrimaryAttackAnimFrames4");
                int? ChanceForAttack4Index = GetIndexOpt("ChanceForAttack4");
                int? PrimaryAttackAnimFrames5Index = GetIndexOpt("PrimaryAttackAnimFrames5");
                int? ChanceForAttack5Index = GetIndexOpt("ChanceForAttack5");
                int? NoShadowIndex = GetIndexOpt("NoShadow");
                int? ForcedGenderIndex = GetIndexOpt("ForcedGender");
                int? GlowColorIndex = GetIndexOpt("GlowColor");
                int? TransparentIndex = GetIndexOpt("Transparent");
                int? EquipmentTableIndex = GetIndexOpt("EquipmentTable");

                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();

                    try
                    {
                        string[] tokens = SplitCsvLine(line);

                        int mobileID = int.Parse(tokens[IdIndex]);
                                                
                        MobileEnemy mobile;

                        bool classicReplacement = Enum.IsDefined(typeof(MobileTypes), mobileID);
                        bool enemyReplacement = false;
                        int enemyReplacementIndex = -1;
                        if (classicReplacement || customEnemies.ContainsKey(mobileID))
                        {
                            enemyReplacementIndex = enemies.FindIndex(m => m.ID == mobileID);
                            mobile = enemies[enemyReplacementIndex];
                            enemyReplacement = true;
                        }
                        else
                        {
                            mobile = new MobileEnemy();
                            mobile.ID = mobileID;
                        }

                        CustomEnemyProperties customEnemyProperties;

                        if (classicReplacement)
                        {
                            if (!classicEnemies.TryGetValue(mobileID, out customEnemyProperties))
                            {
                                customEnemyProperties = new CustomEnemyProperties();
                                InitializeDefaultClassicCustomProperties(mobileID, customEnemyProperties);
                                classicEnemies.Add(mobileID, customEnemyProperties);
                            }
                        }
                        else if (!customEnemies.TryGetValue(mobileID, out customEnemyProperties))
                        {
                            customEnemyProperties = new CustomEnemyProperties();
                        }

                        if (NameIndex.HasValue && !string.IsNullOrEmpty(tokens[NameIndex.Value]))
                        {
                            customEnemyProperties.name = tokens[NameIndex.Value];
                        }
                        else if (!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{mobile.ID}' did not have a Name specified.");
                            continue;
                        }

                        string enemyName;
                        if (!string.IsNullOrEmpty(customEnemyProperties.name))
                        {
                            enemyName = customEnemyProperties.name;
                        }
                        else 
                        {
                            enemyName = TextManager.Instance.GetLocalizedEnemyName(mobileID);
                        }

                        if (CareerIndex.HasValue && !string.IsNullOrEmpty(tokens[CareerIndex.Value]))
                        {
                            customEnemyProperties.career = tokens[CareerIndex.Value];
                        }
                        else if (!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' did not have a Career specified.");
                            continue;
                        }

                        string careerName;
                        if(!string.IsNullOrEmpty(customEnemyProperties.career))
                        {
                            careerName = customEnemyProperties.career;
                        }
                        else if(classicReplacement)
                        {
                            careerName = GetClassicCareerName(mobileID);
                        }
                        else
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' has no defined career");
                            continue;
                        }

                        if (!customCareers.TryGetValue(careerName, out CustomCareer customCareer))
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' has unknown career '{careerName}'");
                            continue;
                        }

                        if (BehaviourIndex.HasValue && !string.IsNullOrEmpty(tokens[BehaviourIndex.Value]))
                        {
                            mobile.Behaviour = (MobileBehaviour)Enum.Parse(typeof(MobileBehaviour), tokens[BehaviourIndex.Value], ignoreCase: true);
                        }

                        bool isSkeletal = false;
                        if (AffinityIndex.HasValue && !string.IsNullOrEmpty(tokens[AffinityIndex.Value]))
                        {
                            if (tokens[AffinityIndex.Value].Equals("Skeletal", StringComparison.OrdinalIgnoreCase))
                            {
                                isSkeletal = true;
                                mobile.Affinity = MobileAffinity.Undead;
                            }
                            else
                            {
                                mobile.Affinity = (MobileAffinity)Enum.Parse(typeof(MobileAffinity), tokens[AffinityIndex.Value], ignoreCase: true);
                            }
                        }

                        if (TeamIndex.HasValue && !string.IsNullOrEmpty(tokens[TeamIndex.Value]))
                        {
                            mobile.Team = (MobileTeams)Enum.Parse(typeof(MobileTeams), tokens[TeamIndex.Value], ignoreCase: true);
                        }
                        else if(!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' did not have a Team specified.");
                            continue;
                        }

                        if (MaleTextureIndex.HasValue && !string.IsNullOrEmpty(tokens[MaleTextureIndex.Value]))
                        {
                            mobile.MaleTexture = int.Parse(tokens[MaleTextureIndex.Value]);
                        }
                        else if(!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' did not have a MaleTexture specified.");
                            continue;
                        }

                        if (FemaleTextureIndex.HasValue && !string.IsNullOrEmpty(tokens[FemaleTextureIndex.Value]))
                        {
                            mobile.FemaleTexture = int.Parse(tokens[FemaleTextureIndex.Value]);
                        }
                        else if(!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' did not have a FemaleTexture specified.");
                            continue;
                        }

                        if (CorpseTextureArchiveIndex.HasValue && !string.IsNullOrEmpty(tokens[CorpseTextureArchiveIndex.Value])
                            && CorpseTextureRecordIndex.HasValue && !string.IsNullOrEmpty(tokens[CorpseTextureRecordIndex.Value]))
                        {
                            int CorpseArchive = int.Parse(tokens[CorpseTextureArchiveIndex.Value]);
                            int CorpseRecord = int.Parse(tokens[CorpseTextureRecordIndex.Value]);
                            mobile.CorpseTexture = EnemyBasics.CorpseTexture(CorpseArchive, CorpseRecord);
                        }
                        else if(!enemyReplacement)
                        {
                            Debug.LogError($"[DEX] Enemy '{enemyName}' did not have a CorpseTextureArchive or CorpseTextureRecord specified.");
                            continue;
                        }

                        if (FemaleCorpseTextureArchiveIndex.HasValue && !string.IsNullOrEmpty(tokens[FemaleCorpseTextureArchiveIndex.Value])
                            && FemaleCorpseTextureRecordIndex.HasValue && !string.IsNullOrEmpty(tokens[FemaleCorpseTextureRecordIndex.Value]))
                        {
                            int CorpseArchive = int.Parse(tokens[FemaleCorpseTextureArchiveIndex.Value]);
                            int CorpseRecord = int.Parse(tokens[FemaleCorpseTextureRecordIndex.Value]);
                            mobile.FemaleCorpseTexture = EnemyBasics.CorpseTexture(CorpseArchive, CorpseRecord);
                        }

                        if (HasIdleIndex.HasValue && !string.IsNullOrEmpty(tokens[HasIdleIndex.Value]))
                        {
                            mobile.HasIdle = ParseBool(tokens[HasIdleIndex.Value], $"line={lineNumber}, column={HasIdleIndex + 1}");
                        }

                        if (CastsMagicIndex.HasValue && !string.IsNullOrEmpty(tokens[CastsMagicIndex.Value]))
                        {
                            mobile.CastsMagic = ParseBool(tokens[CastsMagicIndex.Value], $"line={lineNumber}, column={CastsMagicIndex + 1}");
                        }

                        if (NoSpellAnimsIndex.HasValue && !string.IsNullOrEmpty(tokens[NoSpellAnimsIndex.Value]))
                        {
                            customEnemyProperties.hasNoSpellAnimations = ParseBool(tokens[NoSpellAnimsIndex.Value], $"line={lineNumber}, column={NoSpellAnimsIndex + 1}");
                        }

                        if (HasRangedAttackIndex.HasValue && !string.IsNullOrEmpty(tokens[HasRangedAttackIndex.Value]))
                        {
                            customEnemyProperties.hasRangedAttack = ParseBool(tokens[HasRangedAttackIndex.Value], $"line={lineNumber}, column={HasRangedAttackIndex + 1}");
                        }

                        if (customEnemyProperties.hasRangedAttack && mobile.RangedAttackAnimFrames == null)
                        {
                            mobile.RangedAttackAnimFrames = new int[] { 3, 2, 0, 0, 0, -1, 1, 1, 2, 3 };
                        }

                        // Update mobile "ranged attack" properties based on DEX properties
                        if (mobile.CastsMagic)
                        {
                            if (customEnemyProperties.hasRangedAttack)
                            {
                                // We have both ranged and casting
                                mobile.HasRangedAttack1 = true;
                                mobile.HasRangedAttack2 = true;
                            }
                            else
                            {
                                // Casting is our only ranged attack
                                mobile.HasRangedAttack1 = true;
                                mobile.HasRangedAttack2 = false;
                            }

                            if(!customEnemyProperties.hasNoSpellAnimations)
                            {
                                mobile.HasSpellAnimation = true;
                            }

                            if (mobile.SpellAnimFrames == null)
                            {
                                mobile.SpellAnimFrames = new int[] { 0, 1, 2, 3, 3 };
                            }
                        }
                        else
                        {
                            mobile.HasRangedAttack1 = customEnemyProperties.hasRangedAttack;
                            mobile.HasRangedAttack2 = false;
                        }
                                                
                        if (PrimaryAttackAnimFramesIndex.HasValue && !string.IsNullOrEmpty(tokens[PrimaryAttackAnimFramesIndex.Value]))
                        {
                            mobile.PrimaryAttackAnimFrames = ParseArrayArg(tokens[PrimaryAttackAnimFramesIndex.Value], $"line={lineNumber}, column={PrimaryAttackAnimFramesIndex + 1}");
                        }

                        if(PrimaryAttackAnimFrames2Index.HasValue && !string.IsNullOrEmpty(tokens[PrimaryAttackAnimFrames2Index.Value]))
                        {
                            mobile.PrimaryAttackAnimFrames2 = ParseArrayArg(tokens[PrimaryAttackAnimFrames2Index.Value], $"line={lineNumber}, column={PrimaryAttackAnimFrames2Index + 1}");
                            if(ChanceForAttack2Index.HasValue && !string.IsNullOrEmpty(tokens[ChanceForAttack2Index.Value]))
                            {
                                mobile.ChanceForAttack2 = int.Parse(tokens[ChanceForAttack2Index.Value]);
                            }
                            else
                            {
                                mobile.ChanceForAttack2 = 50;
                            }
                        }

                        if (PrimaryAttackAnimFrames3Index.HasValue && !string.IsNullOrEmpty(tokens[PrimaryAttackAnimFrames3Index.Value]))
                        {
                            mobile.PrimaryAttackAnimFrames3 = ParseArrayArg(tokens[PrimaryAttackAnimFrames3Index.Value], $"line={lineNumber}, column={PrimaryAttackAnimFrames3Index + 1}");
                            if (ChanceForAttack3Index.HasValue && !string.IsNullOrEmpty(tokens[ChanceForAttack3Index.Value]))
                            {
                                mobile.ChanceForAttack3 = int.Parse(tokens[ChanceForAttack3Index.Value]);
                            }
                            else
                            {
                                mobile.ChanceForAttack3 = 25;
                            }
                        }

                        if (PrimaryAttackAnimFrames4Index.HasValue && !string.IsNullOrEmpty(tokens[PrimaryAttackAnimFrames4Index.Value]))
                        {
                            mobile.PrimaryAttackAnimFrames4 = ParseArrayArg(tokens[PrimaryAttackAnimFrames4Index.Value], $"line={lineNumber}, column={PrimaryAttackAnimFrames4Index + 1}");
                            if (ChanceForAttack4Index.HasValue && !string.IsNullOrEmpty(tokens[ChanceForAttack4Index.Value]))
                            {
                                mobile.ChanceForAttack4 = int.Parse(tokens[ChanceForAttack4Index.Value]);
                            }
                            else
                            {
                                mobile.ChanceForAttack4 = 12;
                            }
                        }

                        if (PrimaryAttackAnimFrames5Index.HasValue && !string.IsNullOrEmpty(tokens[PrimaryAttackAnimFrames5Index.Value]))
                        {
                            mobile.PrimaryAttackAnimFrames5 = ParseArrayArg(tokens[PrimaryAttackAnimFrames5Index.Value], $"line={lineNumber}, column={PrimaryAttackAnimFrames5Index + 1}");
                            if (ChanceForAttack5Index.HasValue && !string.IsNullOrEmpty(tokens[ChanceForAttack5Index.Value]))
                            {
                                mobile.ChanceForAttack5 = int.Parse(tokens[ChanceForAttack5Index.Value]);
                            }
                            else
                            {
                                mobile.ChanceForAttack5 = 6;
                            }
                        }

                        if(LevelIndex.HasValue && !string.IsNullOrEmpty(tokens[LevelIndex.Value]))
                        {
                            mobile.Level = int.Parse(tokens[LevelIndex.Value]);
                        }
                        else if(!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a level specified. Defaulting to 1");
                            mobile.Level = 1;
                        }

                        if(MinDamageIndex.HasValue && !string.IsNullOrEmpty(tokens[MinDamageIndex.Value]))
                        {
                            mobile.MinDamage = int.Parse(tokens[MinDamageIndex.Value]);
                        }
                        else if(!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a min damage specified. Defaulting to 1");
                            mobile.MinDamage = 1;
                        }

                        if(MaxDamageIndex.HasValue && !string.IsNullOrEmpty(tokens[MaxDamageIndex.Value]))
                        {
                            mobile.MaxDamage = int.Parse(tokens[MaxDamageIndex.Value]);
                        }
                        else if (!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a max damage specified. Defaulting to {mobile.MinDamage + 1}");
                            mobile.MaxDamage = mobile.MinDamage + 1;
                        }

                        if (MinDamage2Index.HasValue && !string.IsNullOrEmpty(tokens[MinDamage2Index.Value]))
                        {
                            mobile.MinDamage2 = int.Parse(tokens[MinDamage2Index.Value]);
                        }

                        if (MaxDamage2Index.HasValue && !string.IsNullOrEmpty(tokens[MaxDamage2Index.Value]))
                        {
                            mobile.MaxDamage2 = int.Parse(tokens[MaxDamage2Index.Value]);
                        }

                        if (MinDamage3Index.HasValue && !string.IsNullOrEmpty(tokens[MinDamage3Index.Value]))
                        {
                            mobile.MinDamage3 = int.Parse(tokens[MinDamage3Index.Value]);
                        }

                        if (MaxDamage3Index.HasValue && !string.IsNullOrEmpty(tokens[MaxDamage3Index.Value]))
                        {
                            mobile.MaxDamage3 = int.Parse(tokens[MaxDamage3Index.Value]);
                        }

                        if (MinHealthIndex.HasValue && !string.IsNullOrEmpty(tokens[MinHealthIndex.Value]))
                        {
                            mobile.MinHealth = int.Parse(tokens[MinHealthIndex.Value]);
                        }
                        else if (!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a min health specified. Defaulting to 1");
                            mobile.MinHealth = 1;
                        }

                        if (MaxHealthIndex.HasValue && !string.IsNullOrEmpty(tokens[MaxHealthIndex.Value]))
                        {
                            mobile.MaxHealth = int.Parse(tokens[MaxHealthIndex.Value]);
                        }
                        else if (!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a max health specified. Defaulting to {mobile.MinHealth + 1}");
                            mobile.MaxHealth = mobile.MinHealth + 1;
                        }

                        if (ArmorValueIndex.HasValue && !string.IsNullOrEmpty(tokens[ArmorValueIndex.Value]))
                        {
                            mobile.ArmorValue = int.Parse(tokens[ArmorValueIndex.Value]);
                        }
                        else if (!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have an armor value specified. Defaulting to 0");
                            mobile.ArmorValue = 0;
                        }

                        if (MinMetalToHitIndex.HasValue && !string.IsNullOrEmpty(tokens[MinMetalToHitIndex.Value]))
                        {
                            mobile.MinMetalToHit = (WeaponMaterialTypes)Enum.Parse(typeof(WeaponMaterialTypes), tokens[MinMetalToHitIndex.Value], ignoreCase: true);
                        }

                        if (WeightIndex.HasValue && !string.IsNullOrEmpty(tokens[WeightIndex.Value]))
                        {
                            mobile.Weight = int.Parse(tokens[WeightIndex.Value]);
                        }
                        else if (!enemyReplacement && IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"[DEX] Monster '{enemyName}' did not have a weight specified. Defaulting to 100");
                            mobile.Weight = 100;
                        }

                        if (SeesThroughInvisibilityIndex.HasValue && !string.IsNullOrEmpty(tokens[SeesThroughInvisibilityIndex.Value]))
                        {
                            mobile.SeesThroughInvisibility = ParseBool(tokens[SeesThroughInvisibilityIndex.Value], $"line={lineNumber},column={SeesThroughInvisibilityIndex.Value}");
                        }

                        if(MoveSoundIndex.HasValue && !string.IsNullOrEmpty(tokens[MoveSoundIndex.Value]))
                        {
                            mobile.MoveSound = int.Parse(tokens[MoveSoundIndex.Value]);
                        }
                        else if(!enemyReplacement)
                        {
                            if(IsMonster(mobile.ID))
                            {
                                Debug.LogWarning($"[DEX] Monster '{enemyName}' does not have a defined MoveSound. This can cause issues.");
                                mobile.MoveSound = -1;
                            }
                            else
                            {
                                mobile.MoveSound = 243; // Default class move
                            }
                        }

                        if (BarkSoundIndex.HasValue && !string.IsNullOrEmpty(tokens[BarkSoundIndex.Value]))
                        {
                            mobile.BarkSound = int.Parse(tokens[BarkSoundIndex.Value]);
                        }
                        else if (!enemyReplacement)
                        {
                            if (IsMonster(mobile.ID))
                            {
                                Debug.LogWarning($"[DEX] Monster '{enemyName}' does not have a defined BarkSound. This can cause issues.");
                                mobile.BarkSound = -1;
                            }
                            else
                            {
                                mobile.BarkSound = 244; // Default class bark
                            }
                        }

                        if (AttackSoundIndex.HasValue && !string.IsNullOrEmpty(tokens[AttackSoundIndex.Value]))
                        {
                            mobile.AttackSound = int.Parse(tokens[AttackSoundIndex.Value]);
                        }
                        else if (!enemyReplacement)
                        {
                            if (IsMonster(mobile.ID))
                            {
                                Debug.LogWarning($"[DEX] Monster '{enemyName}' does not have a defined AttackSound. This can cause issues.");
                                mobile.BarkSound = -1;
                            }
                            else
                            {
                                mobile.BarkSound = 245; // Default class attack
                            }
                        }

                        if (ParrySoundsIndex.HasValue && !string.IsNullOrEmpty(tokens[ParrySoundsIndex.Value]))
                        {
                            mobile.ParrySounds = ParseBool(tokens[ParrySoundsIndex.Value], $"line={lineNumber},column={ParrySoundsIndex.Value}");
                        }

                        if (CanOpenDoorsIndex.HasValue && !string.IsNullOrEmpty(tokens[CanOpenDoorsIndex.Value]))
                        {
                            mobile.CanOpenDoors = ParseBool(tokens[CanOpenDoorsIndex.Value], $"line={lineNumber},column={CanOpenDoorsIndex.Value}");
                        }

                        if(LootTableKeyIndex.HasValue && !string.IsNullOrEmpty(tokens[LootTableKeyIndex.Value]))
                        {
                            mobile.LootTableKey = tokens[LootTableKeyIndex.Value];
                        }

                        if (MapChanceIndex.HasValue && !string.IsNullOrEmpty(tokens[MapChanceIndex.Value]))
                        {
                            mobile.MapChance = int.Parse(tokens[MapChanceIndex.Value]);
                        }

                        if(NoBloodIndex.HasValue && !string.IsNullOrEmpty(tokens[NoBloodIndex.Value]))
                        {
                            if(ParseBool(tokens[NoBloodIndex.Value], $"line={lineNumber},column={NoBloodIndex.Value}"))
                            {
                                mobile.BloodIndex = 2;
                            }
                            else
                            {
                                mobile.BloodIndex = 0;
                            }
                        }

                        if(NoShadowIndex.HasValue && !string.IsNullOrEmpty(tokens[NoShadowIndex.Value]))
                        {
                            mobile.NoShadow = ParseBool(tokens[NoShadowIndex.Value], $"line={lineNumber},column={NoShadowIndex.Value}");
                        }

                        if(GlowColorIndex.HasValue && !string.IsNullOrEmpty(tokens[GlowColorIndex.Value]))
                        {
                            float[] components = ParseFloatArrayArg(tokens[GlowColorIndex.Value], $"line={lineNumber},column={GlowColorIndex}");
                            if(components.Length < 3)
                            {
                                Debug.LogError($"[DEX] Enemy '{enemyName}' had invalid glow color '{tokens[GlowColorIndex.Value]}'");
                            }
                            else
                            {
                                Color glowColor = new Color(components[0], components[1], components[2]);
                                if (components.Length >= 4)
                                    glowColor.a = components[3];

                                mobile.GlowColor = glowColor;
                            }                
                        }
                                                                        
                        customEnemyProperties.isSkeletal = isSkeletal;                                              

                        if (GroupIndex.HasValue && !string.IsNullOrEmpty(tokens[GroupIndex.Value]))
                        {
                            customEnemyProperties.group = (DFCareer.EnemyGroups)Enum.Parse(typeof(DFCareer.EnemyGroups), tokens[GroupIndex.Value]); 
                        }

                        if (LanguageIndex.HasValue && !string.IsNullOrEmpty(tokens[LanguageIndex.Value]))
                        {
                            customEnemyProperties.language = (DFCareer.Skills)Enum.Parse(typeof(DFCareer.Skills), tokens[LanguageIndex.Value]);
                        }

                        if (SpellBookIndex.HasValue && !string.IsNullOrEmpty(tokens[SpellBookIndex.Value]))
                        {
                            string spellBookToken = tokens[SpellBookIndex.Value];

                            // Raw spellbook
                            if (char.IsDigit(spellBookToken[0]) || spellBookToken[0] == '[' || spellBookToken[0] == '{')
                            {
                                // Add a spellbook named after the mobile id
                                Spellbook spellbook = new Spellbook();
                                spellbook.spellIds = ParseArrayArg(spellBookToken, $"line={lineNumber}, column={SpellBookIndex.Value + 1}");

                                string rawSpellbookName = mobileID.ToString();
                                if(!spellbookTables.TryGetValue(rawSpellbookName, out SpellbookTable spellbookTable))
                                {
                                    spellbookTable = new SpellbookTable();
                                    spellbookTables.Add(rawSpellbookName, spellbookTable);
                                }
                                spellbookTable.spellbooks = new Spellbook[] { spellbook };

                                customEnemyProperties.spellbookTable = rawSpellbookName;
                            }
                            // Try as fixed spellbook type
                            else
                            {
                                customEnemyProperties.spellbookTable = spellBookToken;
                            }
                        }

                        if(OnHitIndex.HasValue && !string.IsNullOrEmpty(tokens[OnHitIndex.Value]))
                        {
                            customEnemyProperties.onHitEffect = int.Parse(tokens[OnHitIndex.Value]);
                        }

                        if(ForcedGenderIndex.HasValue && !string.IsNullOrEmpty(tokens[ForcedGenderIndex.Value]))
                        {
                            customEnemyProperties.forcedGender = (DFBlock.EnemyGenders)Enum.Parse(typeof(DFBlock.EnemyGenders), tokens[ForcedGenderIndex.Value]);
                        }

                        if(TransparentIndex.HasValue && !string.IsNullOrEmpty(tokens[TransparentIndex.Value]))
                        {
                            customEnemyProperties.isTransparent = ParseBool(tokens[TransparentIndex.Value], $"line={lineNumber}, column={TransparentIndex.Value + 1}");
                        }

                        if(EquipmentTableIndex.HasValue && !string.IsNullOrEmpty(tokens[EquipmentTableIndex.Value]))
                        {
                            customEnemyProperties.equipmentTable = tokens[EquipmentTableIndex.Value];
                        }

                        if (!enemyReplacement)
                        {
                            customEnemies.Add(mobileID, customEnemyProperties);
                            enemies.Add(mobile);

                            DaggerfallEntity.RegisterCustomCareerTemplate(mobileID, customCareer.dfCareer);

                            string questName = customEnemyProperties.name.Replace(' ', '_');
                            if (!QuestMachine.Instance.FoesTable.HasValue(questName))
                            {
                                questEnemyLines.Add($"{mobileID}, {questName}");
                            }
                        }
                        else
                        {
                            enemies[enemyReplacementIndex] = mobile;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DEX] Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }

            EnemyBasics.Enemies = enemies.ToArray();
            QuestMachine.Instance.FoesTable.AddIntoTable(questEnemyLines.ToArray());
        }

        void OnEnemySpawn(object source, EnemyLootSpawnedEventArgs args)
        {
            var enemyEntity = source as EnemyEntity;
            if (enemyEntity == null)
                return;

            var mobileId = args.MobileEnemy.ID;
            // Only custom enemies moving forward
            if (!GetCustomProperties(mobileId, out CustomEnemyProperties customEnemyProperties))
                return;

            // Spellbook
            if (!string.IsNullOrEmpty(customEnemyProperties.spellbookTable))
            {
                SetEnemySpells(enemyEntity, customEnemyProperties);
            }

            // Forced gender
            // Sometimes we only have archives for one gender. Force the entity gender so we get the correct groans
            if (customEnemyProperties.forcedGender != DFBlock.EnemyGenders.Unspecified)
            {
                // Using reflection, yes
                MobileUnit enemyMobileUnit = enemyEntity.EntityBehaviour.GetComponentInChildren<MobileUnit>();
                if (enemyMobileUnit != null)
                {
                    FieldInfo enemySummaryField = enemyMobileUnit.GetType().GetField("summary", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (enemySummaryField != null)
                    {
                        var enemySummary = (MobileUnit.MobileUnitSummary)enemySummaryField.GetValue(enemyMobileUnit);

                        if (customEnemyProperties.forcedGender == DFBlock.EnemyGenders.Male)
                        {
                            enemyEntity.Gender = Genders.Male;
                            enemySummary.Enemy.Gender = MobileGender.Male;
                        }
                        else
                        {
                            enemyEntity.Gender = Genders.Female;
                            enemySummary.Enemy.Gender = MobileGender.Female;
                        }

                        enemySummaryField.SetValue(enemyMobileUnit, enemySummary);
                    }
                }
            }

            // Transparent
            if(customEnemyProperties.isTransparent && args.MobileEnemy.Behaviour != MobileBehaviour.Spectral)
            {                
                MeshRenderer meshRenderer = enemyEntity.EntityBehaviour.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.material.shader = Shader.Find(MaterialReader._DaggerfallGhostShaderName);
                    meshRenderer.material.SetFloat("_Cutoff", 0.1f);
                }
            }
        }

        void SetEnemySpells(EnemyEntity enemyEntity, CustomEnemyProperties customEnemy)
        {
            if(!spellbookTables.TryGetValue(customEnemy.spellbookTable, out SpellbookTable spellbookTable))
            {
                Debug.LogError($"[DEX] Unknown enemy spell table '{customEnemy.spellbookTable}'");
                return;
            }

            Spellbook spellbook = spellbookTable.GetSpellbook(enemyEntity.Level);
            if(spellbook == null)
                return;

            // Reset spells, just in case
            while (enemyEntity.SpellbookCount() > 0)
                enemyEntity.DeleteSpell(enemyEntity.SpellbookCount() - 1);

            // Custom monsters haven't had its magicka stats set
            // Call SetEnemySpells with an empty list to initialize the stat
            if (enemyEntity.EntityType == EntityTypes.EnemyMonster)
            {
                enemyEntity.SetEnemySpells(new byte[] {});
            }

            foreach (int spellID in spellbook.spellIds)
            {
                SpellRecord.SpellRecordData spellData;
                GameManager.Instance.EntityEffectBroker.GetClassicSpellRecord(spellID, out spellData);
                if (spellData.index == -1)
                {
                    Debug.LogError($"[DEX] Failed to locate enemy spell '{spellID}' in standard spells list.");
                    continue;
                }

                EffectBundleSettings bundle;
                if (!GameManager.Instance.EntityEffectBroker.ClassicSpellRecordDataToEffectBundleSettings(spellData, BundleTypes.Spell, out bundle))
                {
                    Debug.LogError("[DEX] Failed to create effect bundle for enemy spell: " + spellData.spellName);
                    continue;
                }
                enemyEntity.AddSpell(bundle);
            }
        }

        public void OnMonsterHit(EnemyEntity attacker, DaggerfallEntity target, int damage)
        {
            void SetSpellReady(int spellId)
            {
                SpellRecord.SpellRecordData spellData;
                GameManager.Instance.EntityEffectBroker.GetClassicSpellRecord(spellId, out spellData);
                EffectBundleSettings bundle;
                GameManager.Instance.EntityEffectBroker.ClassicSpellRecordDataToEffectBundleSettings(spellData, BundleTypes.Spell, out bundle);
                EntityEffectBundle spell = new EntityEffectBundle(bundle, attacker.EntityBehaviour);
                EntityEffectManager attackerEffectManager = attacker.EntityBehaviour.GetComponent<EntityEffectManager>();
                attackerEffectManager.SetReadySpell(spell, true);
            }

            Diseases[] diseaseListA = { Diseases.Plague };
            Diseases[] diseaseListB = { Diseases.Plague, Diseases.StomachRot, Diseases.BrainFever };
            Diseases[] diseaseListC =
            {
                Diseases.Plague, Diseases.YellowFever, Diseases.StomachRot, Diseases.Consumption,
                Diseases.BrainFever, Diseases.SwampRot, Diseases.Cholera, Diseases.Leprosy, Diseases.RedDeath,
                Diseases.TyphoidFever, Diseases.Dementia
            };

            int customEffect = 0;
            if(GetCustomProperties(attacker.MobileEnemy.ID, out CustomEnemyProperties customEnemyProperties))
            {
                customEffect = customEnemyProperties.onHitEffect;
            }

            float random;
            if(attacker.MobileEnemy.ID == (int)MonsterCareers.Rat || customEffect == 1)
			{
                // In classic rat can only give plague (diseaseListA), but DF Chronicles says plague, stomach rot and brain fever (diseaseListB).
                // Don't know which was intended. Using B since it has more variety.
                if (Dice100.SuccessRoll(5))
                    FormulaHelper.InflictDisease(attacker, target, diseaseListB);
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.GiantBat || customEffect == 2)
			{
                // Classic uses 2% chance, but DF Chronicles says 5% chance. Not sure which was intended.
                if (Dice100.SuccessRoll(2))
                    FormulaHelper.InflictDisease(attacker, target, diseaseListB);
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Spider
                || attacker.MobileEnemy.ID == (int)MonsterCareers.GiantScorpion
                || customEffect == 3)
			{
                EntityEffectManager targetEffectManager = target.EntityBehaviour.GetComponent<EntityEffectManager>();
                if (targetEffectManager.FindIncumbentEffect<Paralyze>() == null)
                {
                    SetSpellReady(66); // Spider Touch
                }
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Werewolf
                || customEffect == 4)
			{
                random = UnityEngine.Random.Range(0f, 100f);
                if (random <= FormulaHelper.specialInfectionChance && target.EntityBehaviour.EntityType == EntityTypes.Player)
                {
                    // Werewolf
                    EntityEffectBundle bundle = GameManager.Instance.PlayerEffectManager.CreateLycanthropyDisease(LycanthropyTypes.Werewolf);
                    GameManager.Instance.PlayerEffectManager.AssignBundle(bundle, AssignBundleFlags.SpecialInfection);
                }
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Nymph
                || attacker.MobileEnemy.ID == (int)MonsterCareers.Lamia
                || customEffect == 5)
			{
                FormulaHelper.FatigueDamage(attacker, target, damage);
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Wereboar
                || customEffect == 6)
			{
                random = UnityEngine.Random.Range(0f, 100f);
                if (random <= FormulaHelper.specialInfectionChance && target.EntityBehaviour.EntityType == EntityTypes.Player)
                {
                    // Wereboar
                    EntityEffectBundle bundle = GameManager.Instance.PlayerEffectManager.CreateLycanthropyDisease(LycanthropyTypes.Wereboar);
                    GameManager.Instance.PlayerEffectManager.AssignBundle(bundle, AssignBundleFlags.SpecialInfection);
                }
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Zombie
                || customEffect == 7)
			{
                // Nothing in classic. DF Chronicles says 2% chance of disease, which seems like it was probably intended.
                // Diseases listed in DF Chronicles match those of mummy (except missing cholera, probably a mistake)
                if (Dice100.SuccessRoll(2))
                    FormulaHelper.InflictDisease(attacker, target, diseaseListC);
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Mummy
                || customEffect == 8)
			{
                if (Dice100.SuccessRoll(5))
                    FormulaHelper.InflictDisease(attacker, target, diseaseListC);
            }
			else if(attacker.MobileEnemy.ID == (int)MonsterCareers.Vampire
                || attacker.MobileEnemy.ID == (int)MonsterCareers.VampireAncient
                || customEffect == 9)
			{
                random = UnityEngine.Random.Range(0f, 100f);
                if (random <= FormulaHelper.specialInfectionChance && target.EntityBehaviour.EntityType == EntityTypes.Player)
                {
                    // Inflict stage one vampirism disease
                    EntityEffectBundle bundle = GameManager.Instance.PlayerEffectManager.CreateVampirismDisease();
                    GameManager.Instance.PlayerEffectManager.AssignBundle(bundle, AssignBundleFlags.SpecialInfection);
                }
                else if (random <= 2.0f)
                {
                    FormulaHelper.InflictDisease(attacker, target, diseaseListA);
                }
            }
            else
            {
                // Custom effects start at 10
                switch(customEffect)
                {
                    // Blood Spider (level 7) effect
                    case 10:
                        SetSpellReady(BestiaryEnemySpells.BloodSpiderSuck);
                        break;

                    // Gloom Wraith (level 19) effect
                    case 11:
                        SetSpellReady(BestiaryEnemySpells.Silence);
                        break;

                    // Fire Daemon (level 22) effect
                    case 12:
                        SetSpellReady(BestiaryEnemySpells.DaemonFireTouch);
                        break;

                    // Will-o'-wisp (level 9) effect
                    case 13:
                        SetSpellReady(BestiaryEnemySpells.WispDrain);
                        break;
                }
            }
        }

        int CalculateWeaponAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, int damageModifier, int weaponAnimTime, DaggerfallUnityItem weapon)
        {
            int damage = UnityEngine.Random.Range(weapon.GetBaseDamageMin(), weapon.GetBaseDamageMax() + 1) + damageModifier;

            if (target != GameManager.Instance.PlayerEntity)
            {
                var targetEnemy = (EnemyEntity)target;
                bool isSkeletal = targetEnemy.MobileEnemy.ID == (int)MonsterCareers.SkeletalWarrior;
                if(!isSkeletal)
                {
                    if(GetCustomProperties(targetEnemy.MobileEnemy.ID, out CustomEnemyProperties customEnemy))
                    {
                        isSkeletal = customEnemy.isSkeletal;
                    }
                }

                if (isSkeletal)
                {
                    // Apply edged-weapon damage modifier for Skeletal Warrior
                    if ((weapon.flags & 0x10) == 0)
                        damage /= 2;

                    // Apply silver weapon damage modifier for Skeletal Warrior
                    // Arena applies a silver weapon damage bonus for undead enemies, which is probably where this comes from.
                    if (weapon.NativeMaterialValue == (int)WeaponMaterialTypes.Silver)
                        damage *= 2;
                }
            }

            // Apply strength modifier
            damage += FormulaHelper.DamageModifier(attacker.Stats.LiveStrength);

            // Apply material modifier.
            // The in-game display in Daggerfall of weapon damages with material modifiers is incorrect. The material modifier is half of what the display suggests.
            damage += weapon.GetWeaponMaterialModifier();
            if (damage < 1)
                damage = 0;

            damage += FormulaHelper.GetBonusOrPenaltyByEnemyType(attacker, target);

            // Mod hook for adjusting final weapon damage. (no-op in DFU)
            damage = FormulaHelper.AdjustWeaponAttackDamage(attacker, target, damage, weaponAnimTime, weapon);

            return damage;
        }

        void ParseEncounterTables()
        {
            foreach (TextAsset asset in GetDBAssets(".tdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();

                string[] fields = header.Split(';', ',').Select(field => field.Trim('\"')).ToArray();

                if (fields.Length < 21)
                {
                    Debug.LogError($"[DEX] Error while parsing {asset.name}: table database has invalid format (expected at least 21 columns)");
                    continue;
                }

                bool hasTableIndices = fields[1].Equals("Table Indices", StringComparison.InvariantCultureIgnoreCase);

                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();
                    try
                    {
                        string[] tokens = SplitCsvLine(line);
                        if(tokens.Length < 21)
                        {
                            Debug.LogError($"[DEX] Error while parsing {asset.name}:{lineNumber}: table database line has invalid format (expected at least 21 columns)");
                            continue; 
                        }

                        var name = tokens[0];
                        if (encounterTables.TryGetValue(name, out EncounterTable table))
                        {
                            // Table replacement
                            if (hasTableIndices)
                            {
                                var tableIndices = ParseArrayArg(tokens[1], $"line={lineNumber}, column=2");
                                if (tableIndices != null && tableIndices.Length != 0 && !tableIndices.SequenceEqual(table.tableIndices))
                                {
                                    // Remove it from all previous indices
                                    foreach (int tableIndex in table.tableIndices)
                                    {
                                        List<EncounterTable> indexTables = GetIndexEncounterTables(tableIndex);
                                        indexTables.RemoveAll(t => t.name == name);
                                    }

                                    table.tableIndices = tableIndices;

                                    // Add it back to the new indices
                                    foreach (int tableIndex in table.tableIndices)
                                    {
                                        List<EncounterTable> indexTables = GetIndexEncounterTables(tableIndex);
                                        indexTables.Add(table);
                                    }                                    
                                }                                
                            }

                            table.enemyIds = tokens.Skip(hasTableIndices ? 2 : 1).Select(id => int.Parse(id)).ToArray();
                        }
                        else
                        {
                            if(!hasTableIndices)
                            {
                                Debug.LogError($"[DEX] Error while parsing new encounter tables: table indices not specified");
                                continue;
                            }

                            table = new EncounterTable();
                            table.name = tokens[0];
                            table.tableIndices = ParseArrayArg(tokens[1], $"line={lineNumber}, column=2");
                            foreach (int tableIndex in table.tableIndices)
                            {
                                List<EncounterTable> indexTables = GetIndexEncounterTables(tableIndex);
                                indexTables.Add(table);
                            }

                            table.enemyIds = tokens.Skip(2).Select(id => int.Parse(id)).ToArray();

                            
                            encounterTables.Add(table.name, table);
                        }
                        
                    }
                    catch(Exception ex)
                    {
                        Debug.LogError($"[DEX] Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }
        }

        void SelectDungeonTable(int index)
        {
            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if(gps == null || !gps.HasCurrentLocation || !gps.CurrentLocation.HasDungeon)
            {
                Debug.LogError($"[DEX] Dungeon table index '{index}' selected outside of dungeon");
                SelectTable(index);
                return;
            }

            List<EncounterTable> tables = GetIndexEncounterTables(index);
            if (tables == null || tables.Count == 0)
                return;

            ushort unknown2 = (ushort)gps.CurrentLocation.Dungeon.RecordElement.Header.Unknown2;
            byte region = (byte)gps.CurrentRegionIndex;
            DFRandom.srand(unknown2 ^ ((byte)region << 8));
            var random = DFRandom.rand();
            var tableIndex = (int)(random % tables.Count);

            EncounterTable selectedTable = tables[tableIndex];

            ref RandomEncounterTable table = ref RandomEncounters.EncounterTables[index];

            table.Enemies = selectedTable.enemyIds.Select(id => (MobileTypes)id).ToArray();
        }

        void SelectTable(int index)
        {
            List<EncounterTable> tables = GetIndexEncounterTables(index);
            if (tables == null || tables.Count == 0)
                return;

            EncounterTable selectedTable = tables[UnityEngine.Random.Range(0, tables.Count)];

            ref RandomEncounterTable table = ref RandomEncounters.EncounterTables[index];

            table.Enemies = selectedTable.enemyIds.Select(id => (MobileTypes)id).ToArray();
        }

        private void PlayerEnterExit_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            if (args.TransitionType != PlayerEnterExit.TransitionType.ToDungeonInterior)
                return;

            DFLocation location = GameManager.Instance.PlayerGPS.CurrentLocation;
            if (!location.Loaded || !location.HasDungeon)
                return; // Shouldn't happen?

            if (!Enum.IsDefined(typeof(DFRegion.DungeonTypes), (int)location.MapTableData.DungeonType))
                return;

            SelectDungeonTable((int)location.MapTableData.DungeonType);
            SelectDungeonTable(19); // Pick an underwater one too
        }

        void SelectClimateTables(int climateIndex)
        {
            if (!Enum.IsDefined(typeof(MapsFile.Climates), climateIndex))
                return;

            switch ((MapsFile.Climates)climateIndex)
            {
                case MapsFile.Climates.Desert:
                case MapsFile.Climates.Desert2:
                    SelectTable(21); // Day
                    SelectTable(22); // Night
                    break;

                case MapsFile.Climates.Mountain:
                    SelectTable(24); // Day
                    SelectTable(25); // Night
                    break;

                case MapsFile.Climates.Rainforest:
                    SelectTable(27); // Day
                    SelectTable(28); // Night
                    break;

                case MapsFile.Climates.Subtropical:
                    SelectTable(30); // Day
                    SelectTable(31); // Night
                    break;

                case MapsFile.Climates.Swamp:
                case MapsFile.Climates.MountainWoods:
                case MapsFile.Climates.Woodlands:
                    SelectTable(33); // Day
                    SelectTable(34); // Night
                    break;

                case MapsFile.Climates.HauntedWoodlands:
                    SelectTable(36); // Day
                    SelectTable(37); // Night
                    break;
            }
        }

        private void PlayerGPS_OnClimateIndexChanged(int climateIndex)
        {
            SelectClimateTables(climateIndex);
        }

        private void PlayerGPS_OnExitLocationRect()
        {
            SelectClimateTables(GameManager.Instance.PlayerGPS.CurrentClimateIndex);
        }

        private void PlayerGPS_OnEnterLocationRect(DFLocation location)
        {
            int climateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;
            if (!Enum.IsDefined(typeof(MapsFile.Climates), climateIndex))
                return;

            switch ((MapsFile.Climates)climateIndex)
            {
                case MapsFile.Climates.Desert:
                case MapsFile.Climates.Desert2:
                    SelectTable(20); // City Night
                    break;

                case MapsFile.Climates.Mountain:
                    SelectTable(23); // City Night
                    break;

                case MapsFile.Climates.Rainforest:
                    SelectTable(26); // City Night
                    break;

                case MapsFile.Climates.Subtropical:
                    SelectTable(29); // City Night
                    break;

                case MapsFile.Climates.Swamp:
                case MapsFile.Climates.MountainWoods:
                case MapsFile.Climates.Woodlands:
                    SelectTable(32); // City Night
                    break;

                case MapsFile.Climates.HauntedWoodlands:
                    SelectTable(35); // City Night
                    break;
            }
        }

        SpellbookTable MakeSingleSpellbookTable(int[] spells)
        {
            return new SpellbookTable()
            {
                spellbooks = new Spellbook[]
                {
                    new Spellbook { spellIds = spells }
                }
            };
        }

        void ParseSpellbookTables()
        {
            int[] ImpSpells = { 0x07, 0x0A, 0x1D, 0x2C };
            int[] GhostSpells = { 0x22 };
            int[] OrcShamanSpells = { 0x06, 0x07, 0x16, 0x19, 0x1F };
            int[] WraithSpells = { 0x1C, 0x1F };
            int[] FrostDaedraSpells = { 0x10, 0x14 };
            int[] FireDaedraSpells = { 0x0E, 0x19 };
            int[] DaedrothSpells = { 0x16, 0x17, 0x1F };
            int[] VampireSpells = { 0x33 };
            int[] SeducerSpells = { 0x34, 0x43 };
            int[] VampireAncientSpells = { 0x08, 0x32 };
            int[] DaedraLordSpells = { 0x08, 0x0A, 0x0E, 0x3C, 0x43 };
            int[] LichSpells = { 0x08, 0x0A, 0x0E, 0x22, 0x3C };
            int[] AncientLichSpells = { 0x08, 0x0A, 0x0E, 0x1D, 0x1F, 0x22, 0x3C };

            spellbookTables.Add("Imp", MakeSingleSpellbookTable(ImpSpells));
            spellbookTables.Add("Ghost", MakeSingleSpellbookTable(GhostSpells));
            spellbookTables.Add("OrcShaman", MakeSingleSpellbookTable(OrcShamanSpells));
            spellbookTables.Add("Wraith", MakeSingleSpellbookTable(WraithSpells));
            spellbookTables.Add("FrostDaedra", MakeSingleSpellbookTable(FrostDaedraSpells));
            spellbookTables.Add("FireDaedra", MakeSingleSpellbookTable(FireDaedraSpells));
            spellbookTables.Add("Daedroth", MakeSingleSpellbookTable(DaedrothSpells));
            spellbookTables.Add("Vampire", MakeSingleSpellbookTable(VampireSpells));
            spellbookTables.Add("Seducer", MakeSingleSpellbookTable(SeducerSpells));
            spellbookTables.Add("VampireAncient", MakeSingleSpellbookTable(VampireAncientSpells));
            spellbookTables.Add("DaedraLord", MakeSingleSpellbookTable(DaedraLordSpells));
            spellbookTables.Add("Lich", MakeSingleSpellbookTable(LichSpells));
            spellbookTables.Add("AncientLich", MakeSingleSpellbookTable(AncientLichSpells));

            spellbookTables.Add("Class", new SpellbookTable()
            {
                spellbooks = new Spellbook []
                {
                    new Spellbook()
                    {
                        spellIds = FrostDaedraSpells,
                    },
                    new Spellbook()
                    {
                        spellIds = DaedrothSpells,
                        minLevel = 3
                    },
                    new Spellbook()
                    {
                        spellIds = OrcShamanSpells,
                        minLevel = 6
                    },
                    new Spellbook()
                    {
                        spellIds = VampireAncientSpells,
                        minLevel = 9
                    },
                    new Spellbook()
                    {
                        spellIds = DaedraLordSpells,
                        minLevel = 12
                    },
                    new Spellbook()
                    {
                        spellIds = LichSpells,
                        minLevel = 15
                    },
                    new Spellbook()
                    {
                        spellIds = AncientLichSpells,
                        minLevel = 18
                    },
                }
            });
        }

        void EnemyDeath_OnEnemyDeath(object sender, EventArgs _)
        {
            var enemyDeath = sender as EnemyDeath;
            if (enemyDeath == null)
                return;

            var enemyEntityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();
            if (enemyEntityBehaviour == null)
                return;

            if (enemyEntityBehaviour.CorpseLootContainer == null)
                return;

            var trollCorpseBillboard = enemyEntityBehaviour.CorpseLootContainer.GetComponentInChildren<BestiaryTrollCorpseBillboard>();
            if (trollCorpseBillboard == null)
                return;

            var trollMobile = enemyEntityBehaviour.GetComponentInChildren<MobileUnit>();
            if (trollMobile == null)
                return;

            trollCorpseBillboard.SetEnemyProperties(enemyEntityBehaviour, trollMobile);
        }

        DFCareer.EnemyGroups GetEnemyGroup(EnemyEntity e)
        {
            if (customEnemies.TryGetValue(e.MobileEnemy.ID, out CustomEnemyProperties customEnemy))
            {
                return customEnemy.group;
            }
            else
            {
                switch (e.CareerIndex)
                {
                    case (int)MonsterCareers.Rat:
                    case (int)MonsterCareers.GiantBat:
                    case (int)MonsterCareers.GrizzlyBear:
                    case (int)MonsterCareers.SabertoothTiger:
                    case (int)MonsterCareers.Spider:
                    case (int)MonsterCareers.Slaughterfish:
                    case (int)MonsterCareers.GiantScorpion:
                    case (int)MonsterCareers.Dragonling:
                    case (int)MonsterCareers.Horse_Invalid:             // (grouped as undead in classic)
                    case (int)MonsterCareers.Dragonling_Alternate:      // (grouped as undead in classic)
                        return DFCareer.EnemyGroups.Animals;
                    case (int)MonsterCareers.Imp:
                    case (int)MonsterCareers.Spriggan:
                    case (int)MonsterCareers.Orc:
                    case (int)MonsterCareers.Centaur:
                    case (int)MonsterCareers.Werewolf:
                    case (int)MonsterCareers.Nymph:
                    case (int)MonsterCareers.OrcSergeant:
                    case (int)MonsterCareers.Harpy:
                    case (int)MonsterCareers.Wereboar:
                    case (int)MonsterCareers.Giant:
                    case (int)MonsterCareers.OrcShaman:
                    case (int)MonsterCareers.Gargoyle:
                    case (int)MonsterCareers.OrcWarlord:
                    case (int)MonsterCareers.Dreugh:                    // (grouped as undead in classic)
                    case (int)MonsterCareers.Lamia:                     // (grouped as undead in classic)
                        return DFCareer.EnemyGroups.Humanoid;
                    case (int)MonsterCareers.SkeletalWarrior:
                    case (int)MonsterCareers.Zombie:                    // (grouped as animal in classic)
                    case (int)MonsterCareers.Ghost:
                    case (int)MonsterCareers.Mummy:
                    case (int)MonsterCareers.Wraith:
                    case (int)MonsterCareers.Vampire:
                    case (int)MonsterCareers.VampireAncient:
                    case (int)MonsterCareers.Lich:
                    case (int)MonsterCareers.AncientLich:
                        return DFCareer.EnemyGroups.Undead;
                    case (int)MonsterCareers.FrostDaedra:
                    case (int)MonsterCareers.FireDaedra:
                    case (int)MonsterCareers.Daedroth:
                    case (int)MonsterCareers.DaedraSeducer:
                    case (int)MonsterCareers.DaedraLord:
                        return DFCareer.EnemyGroups.Daedra;
                    case (int)MonsterCareers.FireAtronach:
                    case (int)MonsterCareers.IronAtronach:
                    case (int)MonsterCareers.FleshAtronach:
                    case (int)MonsterCareers.IceAtronach:
                        return DFCareer.EnemyGroups.None;

                    default:
                        return DFCareer.EnemyGroups.None;
                }
            }
        }

        DFCareer.Skills GetEnemyLanguage(EnemyEntity e)
        {
            if (customEnemies.TryGetValue(e.MobileEnemy.ID, out CustomEnemyProperties customEnemy))
            {
                return customEnemy.language;
            }
            else
            {
                if (e.EntityType == EntityTypes.EnemyClass)
                {
                    switch (e.CareerIndex)
                    {   // BCHG: classic uses Ettiquette for all
                        case (int)ClassCareers.Burglar:
                        case (int)ClassCareers.Rogue:
                        case (int)ClassCareers.Acrobat:
                        case (int)ClassCareers.Thief:
                        case (int)ClassCareers.Assassin:
                        case (int)ClassCareers.Nightblade:
                            return DFCareer.Skills.Streetwise;
                        default:
                            return DFCareer.Skills.Etiquette;
                    }
                }

                switch (e.CareerIndex)
                {
                    case (int)MonsterCareers.Orc:
                    case (int)MonsterCareers.OrcSergeant:
                    case (int)MonsterCareers.OrcShaman:
                    case (int)MonsterCareers.OrcWarlord:
                        return DFCareer.Skills.Orcish;

                    case (int)MonsterCareers.Harpy:
                        return DFCareer.Skills.Harpy;

                    case (int)MonsterCareers.Giant:
                    case (int)MonsterCareers.Gargoyle:
                        return DFCareer.Skills.Giantish;

                    case (int)MonsterCareers.Dragonling:
                    case (int)MonsterCareers.Dragonling_Alternate:
                        return DFCareer.Skills.Dragonish;

                    case (int)MonsterCareers.Nymph:
                    case (int)MonsterCareers.Lamia:
                        return DFCareer.Skills.Nymph;

                    case (int)MonsterCareers.FrostDaedra:
                    case (int)MonsterCareers.FireDaedra:
                    case (int)MonsterCareers.Daedroth:
                    case (int)MonsterCareers.DaedraSeducer:
                    case (int)MonsterCareers.DaedraLord:
                        return DFCareer.Skills.Daedric;

                    case (int)MonsterCareers.Spriggan:
                        return DFCareer.Skills.Spriggan;

                    case (int)MonsterCareers.Centaur:
                        return DFCareer.Skills.Centaurian;

                    case (int)MonsterCareers.Imp:
                    case (int)MonsterCareers.Dreugh:
                        return DFCareer.Skills.Impish;

                    case (int)MonsterCareers.Vampire:
                    case (int)MonsterCareers.VampireAncient:
                    case (int)MonsterCareers.Lich:
                    case (int)MonsterCareers.AncientLich:
                        return DFCareer.Skills.Etiquette;

                    default:
                        return DFCareer.Skills.None;
                }
            }
        }

        void AssignEnemyStartingEquipment(PlayerEntity playerEntity, EnemyEntity enemyEntity, int variant)
        {
            if (DEX_RRICompat.hasRRIRealisticEnemyEquipment)
            {
                DEX_RRICompat.AssignEnemyStartingEquipment(playerEntity, enemyEntity, variant);
            }
            else
            {
                DaggerfallUnity.Instance.ItemHelper.AssignEnemyStartingEquipment(playerEntity, enemyEntity, variant);
            }
        }

        void InitializeDefaultClassicCustomProperties(int mobileID, CustomEnemyProperties classicCustomProperties)
        {
            classicCustomProperties.hasRangedAttack = HasClassicRangedAttack(mobileID);
        }

        bool HasClassicRangedAttack(int mobileID)
        {
            switch ((MobileTypes)mobileID)
            {
                case MobileTypes.Battlemage:
                case MobileTypes.Nightblade:
                case MobileTypes.Bard:
                case MobileTypes.Burglar:
                case MobileTypes.Rogue:
                case MobileTypes.Acrobat:
                case MobileTypes.Thief:
                case MobileTypes.Assassin:
                case MobileTypes.Monk:
                case MobileTypes.Archer:
                case MobileTypes.Ranger:
                case MobileTypes.Barbarian:
                case MobileTypes.Warrior:
                case MobileTypes.Knight:
                    return true;
            }

            return false;
        }

        string GetClassicCareerName(int mobileID)
        {
            // Monster id
            if (mobileID < 128)
            {
                int careerIndex = mobileID;
                var monsterCareer = (MonsterCareers)careerIndex;
                return monsterCareer.ToString();
            }
            else if (mobileID == (int)MobileTypes.Knight_CityWatch)
            {
                return "Guard";
            }
            // Class id
            else if(mobileID < 256)
            {
                int careerIndex = mobileID - 128;
                var classCareer = (ClassCareers)careerIndex;
                return classCareer.ToString();
            }
            else
            {
                throw new ArgumentException($"Mobile '{mobileID}' has unknown career");
            }
        }

        public string GetCareerName(int mobileID)
        {
            if (GetCustomProperties(mobileID, out CustomEnemyProperties customEnemyProperties) && !string.IsNullOrEmpty(customEnemyProperties.career))
            {
                return customEnemyProperties.career;
            }
            else if (Enum.IsDefined(typeof(MobileTypes), mobileID))
            {
                return GetClassicCareerName(mobileID);
            }
            else
            {
                throw new ArgumentException($"Mobile '{mobileID}' has unknown career");
            }
        }

        public CustomCareer GetCustomCareer(int mobileID)
        {
            return customCareers[GetCareerName(mobileID)];
        }
    }
}
