// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// File LocationNameGenerator.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using FullSerializer;
using System;
using static DaggerfallWorkshop.Game.Utility.NameHelper;
using static DaggerfallWorkshop.Utility.ContentReader;

namespace LocationLoader
{
    static class LocationNameGenerator
    {
        static bool IsInHammerfell(LocationInstance instance)
        {
            var climateIndex = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(instance.worldX, instance.worldY);
            var politicIndex = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(instance.worldX, instance.worldY);

            switch ((MapsFile.Climates)climateIndex)
            {
                case MapsFile.Climates.Desert:
                case MapsFile.Climates.Desert2:
                case MapsFile.Climates.Swamp:
                case MapsFile.Climates.Rainforest:
                case MapsFile.Climates.Subtropical:
                    return true;
            }
                        
            switch (politicIndex & 0x7f)
            {
                case 1: // Dragontail Mountains
                case 53: // Ephesus
                    return true;
            }

            return false;
        }

        public static string GenerateDockName(LocationInstance instance, string context="")
        {
            if (instance.TryGetExtraDataAsInt64("LinkedMapId", out long linkedMapId))
            {
                DFPosition locPos = MapsFile.GetPixelFromPixelID((int)linkedMapId);

                var contentReader = DaggerfallUnity.Instance.ContentReader;
                if (!contentReader.HasLocation(locPos.X, locPos.Y, out MapSummary mapSummary))
                    throw new Exception($"Could not find location for mapID '{linkedMapId}' ({context})");

                if (!contentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex, out DFLocation locData))
                    throw new Exception($"Could not get location data for '{linkedMapId}' ({context})");

                return locData.Name + " Docks";
            }
            return instance.name;
        }

        // Name structure:
        // Adjective Name's Object
        // ex: Stabby Uthyrick's Raiders
        public static string GenerateBanditCampName(LocationInstance instance, string context="")
        {
            ulong seed = instance.locationID;
            int s1 = (int)((seed & 0xFFFFFFFF00000000) >> 32);
            int s2 = (int)(seed & 0xFFFFFFFF);
            Random rng1 = new Random(s1);
            Random rng2 = new Random(s2);

            int r1 = rng1.Next() ^ rng2.Next();
            int r2 = rng1.Next() ^ rng2.Next();
            int r3 = rng1.Next() ^ rng2.Next();
            int r4 = rng1.Next() ^ rng2.Next();

            string[] Adjectives =
            {
                "Cruel",
                "Dark",
                "Violent",
                "Bloody",
                "Bloodthirsty",
                "Black",
                "Red",
                "Brutal",
                "Crazy",
                "Fierce",
                "Savage",
                "Vicious",
                "Mad",
                "Wild",
                "Furious",
                "Berserk",
                "Great",
                "Sinister",
                "Angry",
                "Bold",
                "Cutthroat",
                "Ferocious",
                "Raging",
                "Relentless",
                "Terrible",
                "Dire",
                "Crimson",
            };

            string[] Location =
            {
                "Hideout",
                "Lair",
                "Camp",
                "Fort",
                "Burrow",
                "Den",
                "Hideaway",
                "Station",
                "Point",
                "Hangout",
                "Bastion",
                "Base",
                "Headquarters",
                "Stronghold",
            };

            string[] Gangs =
            {                
                "Killers",
                "Raiders",
                "Looters",
                "Muggers",
                "Brutes",
                "Gangsters",
                "Hooligans",
                "Mauraders",
                "Outlaws",
                "Pirates",
                "Robbers",
                "Brigands",
                "Gang",
                "Crew",
                "Pillagers",
                "Highwaymen",
                "Hunters",
                "Ruffians",
                "Ravagers",
                "Avengers",
                "Annihilators",
                "Furies",
                "Devastators",
                "Reavers",
                "Arsonists",
                "Rangers",
                "Militia",
                "Soldiers",
                "Scouts",
                "Posse",
                "Crushers",
                "Executioners",
                "Usurpers",
                "Reapers",
                "Fighters",
                "Knights",
            };

            string[] NamePart1 =
            {
                "Ember",
                "Dread",
                "Blood",
                "Knife",
                "Flame",
                "Coal",
                "Ale",
                "Shadow",
                "Ruby",
                "Silver",
                "Gold",
                "Onyx",
                "Death",
                "Doom",
                "Arrow",
                "Bomb",
                "Dark",
                "Road",
                "Diamond",
                "Steel",
                "Iron",
                "Spike",
                "Axe",
                "Demon",
                "Stone",
                "Rock",
                "Pearl",
                "Night",
                "Brood",
                "Storm",
                "Frost",
                "Blade",
                "Fire",
                "Eagle",
                "Wolf",
                "Coyote",
                "Dire",
                "Rage",
                "War",
                "Crimson",
                "Pit",
                "Murk",
            };

            string[] NamePart2 =
            {
                "strike",
                "lord",
                "shard",
                "fear",
                "dealer",
                "fist",
                "runner",
                "sneak",
                "stab",
                "baron",
                "thief",
                "boot",
                "hell",
                "shove",
                "piercer",
                "stalker",
                "crawler",
                "sneaker",
                "fury",
                "crusher",
                "hold",
                "bringer",
                "horn",
            };

            if (instance.TryGetExtraDataAsInt64("LinkedMapId", out long linkedMapId))
            {
                DFPosition locPos = MapsFile.GetPixelFromPixelID((int)linkedMapId);

                var contentReader = DaggerfallUnity.Instance.ContentReader;
                if (!contentReader.HasLocation(locPos.X, locPos.Y, out MapSummary mapSummary))
                    throw new Exception($"Could not find location for mapID '{linkedMapId}' ({context})");

                if (!contentReader.GetLocation(mapSummary.RegionIndex, mapSummary.MapIndex, out DFLocation locData))
                    throw new Exception($"Could not get location data for '{linkedMapId}' ({context})");

                return $"{Adjectives[r1 % Adjectives.Length]} {Gangs[r2 % Gangs.Length]} of {locData.Name}";
            }
            else
            {
                switch (r4 % 2)
                {
                    case 0:
                        string adjective = Adjectives[r1 % Adjectives.Length];

                        BankTypes bankType = IsInHammerfell(instance) ? BankTypes.Redguard : BankTypes.Breton;

                        DFRandom.Seed = (uint)r2;
                        string name = DaggerfallUnity.Instance.NameHelper.FirstName(bankType, Genders.Male);

                        string obj = Gangs[r3 % Gangs.Length];

                        return $"{adjective} {name}'s {obj}";

                    case 1:
                        string namePart1 = NamePart1[r1 % NamePart1.Length];
                        string namePart2 = NamePart2[r2 % NamePart2.Length];

                        string loc = Location[r3 % Location.Length];

                        return $"{namePart1}{namePart2} {loc}";
                }
            }

            return instance.name;
        }
    }
}
