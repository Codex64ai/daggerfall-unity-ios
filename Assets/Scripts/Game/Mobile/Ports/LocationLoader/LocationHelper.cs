// MOBILE PORT - source: github.com/KABoissonneault/DFU-LocationLoader @ a5e7a187de1e89465b29001cd7f0b88ecd6d4aa0
// MOBILE PORT - also: github.com/drcarademono/DFU-LocationLoader @ 896a5741e5c9badd47fbb6c0f9926a95a79cb776
//   (branch rmb-object) - object type 5 (RMB block) backport only.
// File LocationHelper.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.Items;
using System.Globalization;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect.Arena2;
using System.Text;

namespace LocationLoader
{
    public static class LocationHelper
    {
        public const string locationInstanceFolder = "StreamingAssets/Locations/";
        public const string locationPrefabFolder = "StreamingAssets/Locations/LocationPrefab/";

        const float animalSoundMaxDistance = 768 * MeshReader.GlobalScale; //Used for the objects with sound

        //List of objects

        public struct ObjectSet
        {
            public string Name;
            public string[] Ids;
        }

        public static ObjectSet[] modelsStructure = new ObjectSet[]
        {
            new ObjectSet { Name = "Buildings - Barracks", Ids = new string[] { "516" } },
            new ObjectSet { Name = "Buildings - Fighters Guild", Ids = new string[] { "300" } },
            new ObjectSet { Name = "Buildings - Knightly Orders", Ids = new string[] { "343", "345", "346", "347", "349", "462" } },
            new ObjectSet { Name = "Buildings - Mages Guild", Ids = new string[] { "223", "224", "225", "226", "227", "228", "229", "230", "317", "361", "423" } },
            new ObjectSet { Name = "Buildings - Medium", Ids = new string[] { "109", "111", "115", "116", "117", "122", "127", "128", "129", "130", "131", "132", "133", "134", "135", "136", "137", "138", "139", "221", "231", "301", "302", "303", "304", "307", "308", "309", "311", "312", "313", "315", "320", "321", "325", "326", "337", "338", "339", "434", "440", "460", "461", "762", "763", "800", "801", "804", "814", "212", "213", "214", "216" } },
            new ObjectSet { Name = "Buildings - Medium - Fenced", Ids = new string[] { "323" } },
            new ObjectSet { Name = "Buildings - Medium - Flat", Ids = new string[] { "526", "528", "537", "538", "539", "540", "541", "542", "543", "544", "545", "546", "601", "602", "606" } },
            new ObjectSet { Name = "Buildings - Medium - Flat - Fenced", Ids = new string[] { "605" } },
            new ObjectSet { Name = "Buildings - Medium - Flat - L Shape", Ids = new string[] { "560", "561", "562", "563" } },
            new ObjectSet { Name = "Buildings - Medium - Flat - Round", Ids = new string[] { "603", "604", "613" } },
            new ObjectSet { Name = "Buildings - Medium - Flat - Semi-Detached", Ids = new string[] { "707", "708", "709" } },
            new ObjectSet { Name = "Buildings - Medium - L Shape", Ids = new string[] { "126", "202", "203", "204", "205", "206", "207", "208", "209", "211", "439", "755" } },
            new ObjectSet { Name = "Buildings - Medium - Noble", Ids = new string[] { "759", "760", "761", "802", "803", "805", "806", "807", "810", "811", "813" } },
            new ObjectSet { Name = "Buildings - Medium - Round", Ids = new string[] { "502", "503", "504", "505" } },
            new ObjectSet { Name = "Buildings - Medium - Tower", Ids = new string[] { "501" } },
            new ObjectSet { Name = "Buildings - Large", Ids = new string[] { "112", "123", "140", "141", "142", "143", "144", "145", "146", "147", "148", "149", "150", "151", "152", "153", "215", "222", "233", "234", "235", "237", "239", "240", "241", "242", "243", "245", "246", "262", "316", "319", "340", "413", "414", "415", "416", "417", "418", "419", "420", "421", "422", "424", "437", "463", "757", "808", "812", "816", "817", "835" } },
            new ObjectSet { Name = "Buildings - Large - Flat", Ids = new string[] { "121", "238", "244", "247", "547", "548", "549", "550", "551", "552", "553", "616", "618", "621", "623", "624", "640", "641", "642", "643", "660", "661", "700", "862", "863" } },
            new ObjectSet { Name = "Buildings - Large - Flat - Round", Ids = new string[] { "617", "620" } },
            new ObjectSet { Name = "Buildings - Large - Flat - Terraced", Ids = new string[] { "658", "659", "701", "702", "703", "704", "705", "706", "710" } },
            new ObjectSet { Name = "Buildings - Large - L Shape", Ids = new string[] { "0", "108" } },
            new ObjectSet { Name = "Buildings - Large - Noble", Ids = new string[] { "758", "809", "815", "819", "820", "821", "825", "826", "827", "833", "834", "842", "849", "850", "851" } },
            new ObjectSet { Name = "Buildings - Large - Semi-Detached", Ids = new string[] { "843", "845", "848" } },
            new ObjectSet { Name = "Buildings - Large - Semi-Detached - Noble", Ids = new string[] { "822", "823", "824" } },
            new ObjectSet { Name = "Buildings - Large - Terraced - Noble", Ids = new string[] { "828", "829", "830", "831", "832", "844", "846", "847" } },
            new ObjectSet { Name = "Buildings - Palaces", Ids = new string[] { "407", "408", "409", "451", "458", "459", "507", "508", "509", "510", "511", "730", "731", "732", "733", "734" } },
            new ObjectSet { Name = "Buildings - Small", Ids = new string[] { "107", "118", "119", "120", "125", "236", "318", "322", "327", "328", "330", "331", "332", "333", "334", "335", "336", "410", "411", "412" } },
            new ObjectSet { Name = "Buildings - Small - Fenced", Ids = new string[] { "329" } },
            new ObjectSet { Name = "Buildings - Small - Flat", Ids = new string[] { "527", "529", "530", "531", "532", "533", "534", "535", "536", "609", "610", "611", "614", "615", "622", "627", "637" } },
            new ObjectSet { Name = "Buildings - Small - Flat - Fenced", Ids = new string[] { "607", "608" } },
            new ObjectSet { Name = "Buildings - Small - Flat - L Shape", Ids = new string[] { "554", "555", "556", "557", "558", "559", "600" } },
            new ObjectSet { Name = "Buildings - Small - Flat - Round", Ids = new string[] { "655", "656", "657" } },
            new ObjectSet { Name = "Buildings - Small - L SHape", Ids = new string[] { "154", "155", "156", "157", "158", "159", "160", "161", "162", "163", "200", "201", "210", "756" } },
            new ObjectSet { Name = "Buildings - Small - Tower", Ids = new string[] { "263" } },
            new ObjectSet { Name = "Buildings - Taverns - Large", Ids = new string[] { "220", "248", "250", "253", "428", "836", "838", "839", "840", "841" } },
            new ObjectSet { Name = "Buildings - Taverns - Large - Flat", Ids = new string[] { "113", "626", "662", "663" } },
            new ObjectSet { Name = "Buildings - Taverns - Medium - Flat", Ids = new string[] { "625" } },
            new ObjectSet { Name = "Buildings - Taverns - Medium", Ids = new string[] { "249", "251", "252", "429", "430", "431", "432", "438", "837" } },
            new ObjectSet { Name = "Buildings - Taverns - Small", Ids = new string[] { "110" } },
            new ObjectSet { Name = "Buildings - Templar Orders", Ids = new string[] { "103", "259", "305", "435", "260" } },
            new ObjectSet { Name = "Buildings - Temples", Ids = new string[] { "100", "101", "102", "257", "258", "261", "362", "363", "400", "402", "406", "433", "436" } },
            new ObjectSet { Name = "Buildings - Two Houses", Ids = new string[] { "324" } },
            new ObjectSet { Name = "Brown Stone Fence Corner", Ids = new string[] { "517", "21105" } },
            new ObjectSet { Name = "Brown Stone Fence Straight", Ids = new string[] { "518", "21104" } },
            new ObjectSet { Name = "Brown Stone Fence Gateway", Ids = new string[] { "21106" } },
            new ObjectSet { Name = "Brown Stone Fence End Cap", Ids = new string[] { "21107" } },
            new ObjectSet { Name = "City Walls - Corner Tower", Ids = new string[] { "444" } },
            new ObjectSet { Name = "City Walls - Gateway Closed", Ids = new string[] { "447" } },
            new ObjectSet { Name = "City Walls - Gateway Opened", Ids = new string[] { "446" } },
            new ObjectSet { Name = "City Walls - Straight", Ids = new string[] { "445", "20026", "20028" } },
            new ObjectSet { Name = "Column", Ids = new string[] { "41900" } },
            new ObjectSet { Name = "Dungeon - Cairn Entrance", Ids = new string[] { "42000" } },
            new ObjectSet { Name = "Dungeon - Castle 00", Ids = new string[] { "512", "513" } },
            new ObjectSet { Name = "Dungeon - Castle 01", Ids = new string[] { "644", "645", "646" } },
            new ObjectSet { Name = "Dungeon - Castle 02", Ids = new string[] { "711" } },
            new ObjectSet { Name = "Dungeon - Castle 03", Ids = new string[] { "647", "648" } },
            new ObjectSet { Name = "Dungeon - Castle 04", Ids = new string[] { "649", "650", "651", "652", "653", "654" } },
            new ObjectSet { Name = "Dungeon - Castle 05", Ids = new string[] { "712", "713", "714", "715", "716" } },
            new ObjectSet { Name = "Dungeon - Castle 06", Ids = new string[] { "717", "718" } },
            new ObjectSet { Name = "Dungeon - Castle 07", Ids = new string[] { "719", "720", "721", "722" } },
            new ObjectSet { Name = "Dungeon - Castle 08", Ids = new string[] { "726", "727", "728", "729" } },
            new ObjectSet { Name = "Dungeon - Castle 09", Ids = new string[] { "723", "724", "725" } },
            new ObjectSet { Name = "Dungeon - Castle 10", Ids = new string[] { "852" } },
            new ObjectSet { Name = "Dungeon - Castle 11", Ids = new string[] { "853" } },
            new ObjectSet { Name = "Dungeon - Castle 12", Ids = new string[] { "854" } },
            new ObjectSet { Name = "Dungeon - Castle 13", Ids = new string[] { "855" } },
            new ObjectSet { Name = "Dungeon - Castle 14", Ids = new string[] { "856" } },
            new ObjectSet { Name = "Dungeon - Castle 15", Ids = new string[] { "857" } },
            new ObjectSet { Name = "Dungeon - Castle 16", Ids = new string[] { "858" } },
            new ObjectSet { Name = "Dungeon - Castle 17", Ids = new string[] { "859" } },
            new ObjectSet { Name = "Dungeon - Castle 18", Ids = new string[] { "860" } },
            new ObjectSet { Name = "Dungeon - Castle 19", Ids = new string[] { "861" } },
            new ObjectSet { Name = "Dungeon - Ground Entrance", Ids = new string[] { "43600" } },
            new ObjectSet { Name = "Dungeon - Mound Entrance", Ids = new string[] { "43601", "43602", "43603" } },
            new ObjectSet { Name = "Dungeon - Ruin Entrance", Ids = new string[] { "43604" } },
            new ObjectSet { Name = "Dungeon - Stone Entrance", Ids = new string[] { "40012" } },
            new ObjectSet { Name = "Dungeon - Tree Entrance", Ids = new string[] { "42001" } },
            new ObjectSet { Name = "Fortifications - Corner", Ids = new string[] { "448", "39002" } },
            new ObjectSet { Name = "Fortifications - Gateway", Ids = new string[] { "450", "39001" } },
            new ObjectSet { Name = "Fortifications - S Shape", Ids = new string[] { "39003" } },
            new ObjectSet { Name = "Fortifications - Straight", Ids = new string[] { "449", "39000", "39004", "39005" } },
            new ObjectSet { Name = "Fountains", Ids = new string[] { "41220", "41221", "41222" } },
            new ObjectSet { Name = "Guard Tower", Ids = new string[] { "522" } },
            new ObjectSet { Name = "Guard Tower - Top", Ids = new string[] { "20027", "20033", "20127", "20227", "20327", "20427", "20527", "20627", "20727", "20827" } },
            new ObjectSet { Name = "Hedge Maze", Ids = new string[] { "735", "736", "737", "738", "740", "741", "742", "743" } },
            new ObjectSet { Name = "Hedgerow - Corner - Entrance", Ids = new string[] { "745" } },
            new ObjectSet { Name = "Hedgerow - Corner", Ids = new string[] { "744", "750", "40015", "41235" } },
            new ObjectSet { Name = "Hedgerow - 3 Way", Ids = new string[] { "751", "40016" } },
            new ObjectSet { Name = "Hedgerow - 4 Way", Ids = new string[] { "40017" } },
            new ObjectSet { Name = "Hedgerow - End", Ids = new string[] { "739" } },
            new ObjectSet { Name = "Hedgerow - End Cap", Ids = new string[] { "753", "905", "40013" } },
            new ObjectSet { Name = "Hedgerow - Entrance", Ids = new string[] { "746", "749", "752" } },
            new ObjectSet { Name = "Hedgerow - Single", Ids = new string[] { "41236", "41237" } },
            new ObjectSet { Name = "Hedgerow - Straight", Ids = new string[] { "747", "748", "754", "40014" } },
            new ObjectSet { Name = "Grass", Ids = new string[] { "21017", "21020" } },
            new ObjectSet { Name = "Large Shrines", Ids = new string[] { "74094", "74088" } },
            new ObjectSet { Name = "Mounds", Ids = new string[] { "41215", "41218", "41219" } },
            new ObjectSet { Name = "Mounds - Carved", Ids = new string[] { "41216", "41217" } },
            new ObjectSet { Name = "Pillars - Wood", Ids = new string[] { "41901", "41902" } },
            new ObjectSet { Name = "Pillars - Marble", Ids = new string[] { "62315", "62320", "62316" } },
            new ObjectSet { Name = "Pyramid", Ids = new string[] { "74121" } },
            new ObjectSet { Name = "Ruins - Columns", Ids = new string[] { "41722", "41723", "41730", "41731", "41732", "41733" } },
            new ObjectSet { Name = "Ruins - Buildings", Ids = new string[] { "41720", "41721", "41724", "41725", "41726", "41727", "41728", "41729", "43400", "43401", "43402", "43403", "43404", "43405", "43406", "43407", "43408", "43409", "43410", "43411", "43412", "43413", "43414", "43415", "43416", "43417", "43500", "43501", "43502", "43503", "43504", "43505", "43506", "43507", "43508", "43509", "43510", "43511", "43512", "43513", "43514", "43515", "43516", "43517" } },
            new ObjectSet { Name = "Ships / Boats", Ids = new string[] { "41502", "41504", "41501", "909", "910" } },
            new ObjectSet { Name = "Shipwreck", Ids = new string[] { "41509" } },
            new ObjectSet { Name = "Signs - Akatosh Temple", Ids = new string[] { "43733", "43752", "43714" } },
            new ObjectSet { Name = "Signs - Alchemist Shop", Ids = new string[] { "43720", "43739", "43702" } },
            new ObjectSet { Name = "Signs - Arkay Temple", Ids = new string[] { "43718", "43737", "43700" } },
            new ObjectSet { Name = "Signs - Armor Shop", Ids = new string[] { "43725", "43744" } },
            new ObjectSet { Name = "Signs - Bank", Ids = new string[] { "43730", "43749", "43711" } },
            new ObjectSet { Name = "Signs - Clothes Shop", Ids = new string[] { "43721", "43740", "43703" } },
            new ObjectSet { Name = "Signs - Dibella Temple", Ids = new string[] { "43724", "43743", "43706" } },
            new ObjectSet { Name = "Signs - General Store", Ids = new string[] { "43731", "43750", "43712" } },
            new ObjectSet { Name = "Signs - Jewelry Shop", Ids = new string[] { "43726", "43745", "43707" } },
            new ObjectSet { Name = "Signs - Julianos Temple", Ids = new string[] { "43719", "43738", "43701" } },
            new ObjectSet { Name = "Signs - Kynareth Temple", Ids = new string[] { "43734", "43753", "43715" } },
            new ObjectSet { Name = "Signs - Library", Ids = new string[] { "43735", "43754", "43716" } },
            new ObjectSet { Name = "Signs - Mages Guild", Ids = new string[] { "43722", "43741", "43704" } },
            new ObjectSet { Name = "Signs - Mara Temple", Ids = new string[] { "43728", "43747", "43709" } },
            new ObjectSet { Name = "Signs - Pawn Shop", Ids = new string[] { "43732", "43751", "43713" } },
            new ObjectSet { Name = "Signs - Stendarr Temple", Ids = new string[] { "43729", "43748", "43710" } },
            new ObjectSet { Name = "Signs - Tavern", Ids = new string[] { "43727", "43746", "43708" } },
            new ObjectSet { Name = "Signs - Weapon Shop", Ids = new string[] { "43736", "43755", "43717" } },
            new ObjectSet { Name = "Signs - Zenithar Temple", Ids = new string[] { "43723", "43742", "43705" } },
            new ObjectSet { Name = "Stone Bridges", Ids = new string[] { "41211", "41212", "41213" } },
            new ObjectSet { Name = "Towers - Hexagonal", Ids = new string[] { "41223", "41224", "41225", "41226", "41227", "41228", "41229" } },
            new ObjectSet { Name = "Towers - Pentagonal", Ids = new string[] { "41230", "41231", "41232", "41233", "41234" } },
            new ObjectSet { Name = "Unique - Castle Daggerfall", Ids = new string[] { "521", "523", "524", "525" } },
            new ObjectSet { Name = "Unique - Castle Llugwych", Ids = new string[] { "457" } },
            new ObjectSet { Name = "Unique - Castle Wayrest", Ids = new string[] { "900", "901", "902", "903", "904" } },
            new ObjectSet { Name = "Unique - Direnni Tower", Ids = new string[] { "500" } },
            new ObjectSet { Name = "Unique - Lysandus' Tomb", Ids = new string[] { "454" } },
            new ObjectSet { Name = "Unique - Orsinium Palace", Ids = new string[] { "455" } },
            new ObjectSet { Name = "Unique - Scourg Barrow", Ids = new string[] { "456" } },
            new ObjectSet { Name = "Unique - Sentinel Palace", Ids = new string[] { "633", "634", "635" } },
            new ObjectSet { Name = "Unique - Sentinel Palace - Walls?", Ids = new string[] { "628", "629", "630", "631", "632", "636" } },
            new ObjectSet { Name = "Unique - Shedungent", Ids = new string[] { "452" } },
            new ObjectSet { Name = "Unique - Woodborne Hall", Ids = new string[] { "453" } },
            new ObjectSet { Name = "Water", Ids = new string[] { "73000", "73001", "73002", "73003", "73004", "73005", "73006", "73007", "73008", "73009", "73010", "73011", "73012", "73013", "73014", "73015", "73016" } },
            new ObjectSet { Name = "Windmill", Ids = new string[] { "41600" } },
            new ObjectSet { Name = "Wooden Fence - Broken", Ids = new string[] { "21103" } },
            new ObjectSet { Name = "Wooden Fence 01 - End Cap", Ids = new string[] { "41200", "41202" } },
            new ObjectSet { Name = "Wooden Fence 01 - Mid", Ids = new string[] { "41201" } },
            new ObjectSet { Name = "Wooden Fence 02 - End Cap", Ids = new string[] { "41203", "41205" } },
            new ObjectSet { Name = "Wooden Fence 02 - Mid", Ids = new string[] { "41204" } },
            new ObjectSet { Name = "Wooden Fence 03 - End Cap", Ids = new string[] { "41206", "41208" } },
            new ObjectSet { Name = "Wooden Fence 03 - Mid", Ids = new string[] { "41207" } },
            new ObjectSet { Name = "Ext. Balcony", Ids = new string[] { "40021", "40121", "40221", "40321", "40421", "40521", "40621", "40721", "40821" } },
            new ObjectSet { Name = "Ext. Door", Ids = new string[] { "19024", "19124", "19224", "19324", "19424", "19524", "19624", "19724", "19824" } },
            new ObjectSet { Name = "Ext. Wall - House A", Ids = new string[] { "19000", "19006", "19007", "19008", "19009", "19010", "19011", "19012", "19013", "19014", "19015", "19016", "19017", "20000", "20006", "20007", "40000", "40001", "40002", "40003", "40004", "40005" } },
            new ObjectSet { Name = "Ext. Wall - House B", Ids = new string[] { "19100", "19106", "19107", "19108", "19109", "19110", "19111", "19112", "19113", "19114", "19115", "19116", "19117", "20100", "20106", "20107", "40100", "40101", "40102", "40103", "40104", "40105" } },
            new ObjectSet { Name = "Ext. Wall - House C", Ids = new string[] { "19200", "19206", "19207", "19208", "19209", "19210", "19211", "19212", "19213", "19214", "19215", "19216", "19217", "20200", "20206", "20207", "40200", "40201", "40202", "40203", "40204", "40205" } },
            new ObjectSet { Name = "Ext. Wall - House D", Ids = new string[] { "19400", "19406", "19407", "19408", "19409", "19410", "19411", "19412", "19413", "19414", "19415", "19416", "19417", "20400", "20406", "20407", "40400", "40401", "40402", "40403", "40404", "40405" } },
            new ObjectSet { Name = "Ext. Wall - House E", Ids = new string[] { "19500", "19506", "19507", "19508", "19509", "19510", "19511", "19512", "19513", "19514", "19515", "19516", "19517", "20500", "20506", "20507", "40500", "40501", "40502", "40503", "40504", "40505" } },
            new ObjectSet { Name = "Ext. Wall - House F", Ids = new string[] { "19800", "19806", "19807", "19808", "19809", "19810", "19811", "19812", "19813", "19814", "19815", "19816", "19817", "20800", "20806", "20807", "40800", "40801", "40802", "40803", "40804", "40805" } },
            new ObjectSet { Name = "Ext. Wall - Inn", Ids = new string[] { "19600", "19606", "19607", "19608", "19609", "19610", "19611", "19612", "19613", "19614", "19615", "19616", "19617", "20600", "20606", "20607", "40600", "40601", "40602", "40603", "40604", "40605" } },
            new ObjectSet { Name = "Ext. Wall - Mages Guild", Ids = new string[] { "19300", "19306", "19307", "19308", "19309", "19310", "19311", "19312", "19313", "19314", "19315", "19316", "19317", "20300", "20306", "20307", "40300", "40301", "40302", "40303", "40304", "40305" } },
            new ObjectSet { Name = "Ext. Wall - Temple", Ids = new string[] { "19700", "19706", "19707", "19708", "19709", "19710", "19711", "19712", "19713", "19714", "19715", "19716", "19717", "20700", "20706", "20707", "40700", "40701", "40702", "40703", "40704", "40705" } },
            new ObjectSet { Name = "Ext. Wall - Win - House A", Ids = new string[] { "19001", "19002", "19003", "19004", "19005", "19018", "19019", "19020", "19021", "19022", "19023", "20001", "20002", "20003", "20004", "20005", "20008", "20009", "20010", "20011", "20012", "20013", "20014", "20015", "20016", "20017", "20018" } },
            new ObjectSet { Name = "Ext. Wall - Win - House B", Ids = new string[] { "19101", "19102", "19103", "19104", "19105", "19118", "19119", "19120", "19121", "19122", "19123", "20101", "20102", "20103", "20104", "20105", "20108", "20109", "20110", "20111", "20112", "20113", "20114", "20115", "20116", "20117", "20118" } },
            new ObjectSet { Name = "Ext. Wall - Win - House C", Ids = new string[] { "19201", "19202", "19203", "19204", "19205", "19218", "19219", "19220", "19221", "19222", "19223", "20201", "20202", "20203", "20204", "20205", "20208", "20209", "20210", "20211", "20212", "20213", "20214", "20215", "20216", "20217", "20218" } },
            new ObjectSet { Name = "Ext. Wall - Win - House D", Ids = new string[] { "19401", "19402", "19403", "19404", "19405", "19418", "19419", "19420", "19421", "19422", "19423", "20401", "20402", "20403", "20404", "20405", "20408", "20409", "20410", "20411", "20412", "20413", "20414", "20415", "20416", "20417", "20418" } },
            new ObjectSet { Name = "Ext. Wall - Win - House E", Ids = new string[] { "19501", "19502", "19503", "19504", "19505", "19518", "19519", "19520", "19521", "19522", "19523", "20501", "20502", "20503", "20504", "20505", "20508", "20509", "20510", "20511", "20512", "20513", "20514", "20515", "20516", "20517", "20518" } },
            new ObjectSet { Name = "Ext. Wall - Win - House F", Ids = new string[] { "19801", "19802", "19803", "19804", "19805", "19818", "19819", "19820", "19821", "19822", "19823", "20801", "20802", "20803", "20804", "20805", "20808", "20809", "20810", "20811", "20812", "20813", "20814", "20815", "20816", "20817", "20818" } },
            new ObjectSet { Name = "Ext. Wall - Win - Inn", Ids = new string[] { "19601", "19602", "19603", "19604", "19605", "19618", "19619", "19620", "19621", "19622", "19623", "20601", "20602", "20603", "20604", "20605", "20608", "20609", "20610", "20611", "20612", "20613", "20614", "20615", "20616", "20617", "20618" } },
            new ObjectSet { Name = "Ext. Wall - Win - Mages Guild", Ids = new string[] { "19301", "19302", "19303", "19304", "19305", "19318", "19319", "19320", "19321", "19322", "19323", "20301", "20302", "20303", "20304", "20305", "20308", "20309", "20310", "20311", "20312", "20313", "20314", "20315", "20316", "20317", "20318" } },
            new ObjectSet { Name = "Ext. Wall - Win - Temple", Ids = new string[] { "19701", "19702", "19703", "19704", "19705", "19718", "19719", "19720", "19721", "19722", "19723", "20701", "20702", "20703", "20704", "20705", "20708", "20709", "20710", "20711", "20712", "20713", "20714", "20715", "20716", "20717", "20718" } },
            new ObjectSet { Name = "Ext. Railing - House A", Ids = new string[] { "21000", "21001", "21002", "21039", "21040", "21043", "21050", "21051", "21052", "21053" } },
            new ObjectSet { Name = "Ext. Railing - House B", Ids = new string[] { "21100", "21101", "21102", "21139", "21140", "21143", "21150", "21151", "21152", "21153" } },
            new ObjectSet { Name = "Ext. Railing - House C", Ids = new string[] { "21200", "21201", "21202", "21239", "21240", "21243", "21250", "21251", "21252", "21253" } },
            new ObjectSet { Name = "Ext. Railing - House D", Ids = new string[] { "21400", "21401", "21402", "21439", "21440", "21443", "21450", "21451", "21452", "21453" } },
            new ObjectSet { Name = "Ext. Railing - House E", Ids = new string[] { "21500", "21501", "21502", "21539", "21540", "21543", "21550", "21551", "21552", "21553" } },
            new ObjectSet { Name = "Ext. Railing - House F", Ids = new string[] { "21800", "21801", "21802", "21839", "21840", "21843", "21850", "21851", "21852", "21853" } },
            new ObjectSet { Name = "Ext. Railing - Inn", Ids = new string[] { "21600", "21601", "21602", "21639", "21640", "21643", "21650", "21651", "21652", "21653" } },
            new ObjectSet { Name = "Ext. Railing - Mages Guild", Ids = new string[] { "21300", "21301", "21302", "21339", "21340", "21343", "21350", "21351", "21352", "21353" } },
            new ObjectSet { Name = "Ext. Railing - Temple", Ids = new string[] { "21700", "21701", "21702", "21739", "21740", "21743", "21750", "21751", "21752", "21753" } },
            new ObjectSet { Name = "Ext. Roofs - House A", Ids = new string[] { "26000", "26001", "26002", "26003", "26004", "26005", "26006", "26007", "26008", "26009", "26010", "26011", "26012", "26013", "26014", "26015", "26016", "26017", "26018", "26019", "26020", "26021", "26022", "26023", "26024", "26025", "26026", "26027", "26028", "26029", "27000", "27001", "27002", "27003", "27004", "27005", "27006", "27007", "27008", "27009", "27010", "27011", "27012" } },
            new ObjectSet { Name = "Ext. Roofs - House B", Ids = new string[] { "26100", "26101", "26102", "26103", "26104", "26105", "26106", "26107", "26108", "26109", "26110", "26111", "26112", "26113", "26114", "26115", "26116", "26117", "26118", "26119", "26120", "26121", "26122", "26123", "26124", "26125", "26126", "26127", "26128", "26129", "27100", "27101", "27102", "27103", "27104", "27105", "27106", "27107", "27108", "27109", "27110", "27111", "27112" } },
            new ObjectSet { Name = "Ext. Roofs - House C", Ids = new string[] { "26200", "26201", "26202", "26203", "26204", "26205", "26206", "26207", "26208", "26209", "26210", "26211", "26212", "26213", "26214", "26215", "26216", "26217", "26218", "26219", "26220", "26221", "26222", "26223", "26224", "26225", "26226", "26227", "26228", "26229", "27200", "27201", "27202", "27203", "27204", "27205", "27206", "27207", "27208", "27209", "27210", "27211", "27212" } },
            new ObjectSet { Name = "Ext. Roofs - House D", Ids = new string[] { "26400", "26401", "26402", "26403", "26404", "26405", "26406", "26407", "26408", "26409", "26410", "26411", "26412", "26413", "26414", "26415", "26416", "26417", "26418", "26419", "26420", "26421", "26422", "26423", "26424", "26425", "26426", "26427", "26428", "26429", "27400", "27401", "27402", "27403", "27404", "27405", "27406", "27407", "27408", "27409", "27410", "27411", "27412" } },
            new ObjectSet { Name = "Ext. Roofs - House E", Ids = new string[] { "26500", "26501", "26502", "26503", "26504", "26505", "26506", "26507", "26508", "26509", "26510", "26511", "26512", "26513", "26514", "26515", "26516", "26517", "26518", "26519", "26520", "26521", "26522", "26523", "26524", "26525", "26526", "26527", "26528", "26529", "27500", "27501", "27502", "27503", "27504", "27505", "27506", "27507", "27508", "27509", "27510", "27511", "27512" } },
            new ObjectSet { Name = "Ext. Roofs - House F", Ids = new string[] { "26800", "26801", "26802", "26803", "26804", "26805", "26806", "26807", "26808", "26809", "26810", "26811", "26812", "26813", "26814", "26815", "26816", "26817", "26818", "26819", "26820", "26821", "26822", "26823", "26824", "26825", "26826", "26827", "26828", "26829", "27800", "27801", "27802", "27803", "27804", "27805", "27806", "27807", "27808", "27809", "27810", "27811", "27812" } },
            new ObjectSet { Name = "Ext. Roofs - Inn", Ids = new string[] { "26600", "26601", "26602", "26603", "26604", "26605", "26606", "26607", "26608", "26609", "26610", "26611", "26612", "26613", "26614", "26615", "26616", "26617", "26618", "26619", "26620", "26621", "26622", "26623", "26624", "26625", "26626", "26627", "26628", "26629", "27600", "27601", "27602", "27603", "27604", "27605", "27606", "27607", "27608", "27609", "27610", "27611", "27612" } },
            new ObjectSet { Name = "Ext. Roofs - Mages Guild", Ids = new string[] { "26300", "26301", "26302", "26303", "26304", "26305", "26306", "26307", "26308", "26309", "26310", "26311", "26312", "26313", "26314", "26315", "26316", "26317", "26318", "26319", "26320", "26321", "26322", "26323", "26324", "26325", "26326", "26327", "26328", "26329", "27300", "27301", "27302", "27303", "27304", "27305", "27306", "27307", "27308", "27309", "27310", "27311", "27312" } },
            new ObjectSet { Name = "Ext. Roofs - Temple", Ids = new string[] { "26700", "26701", "26702", "26703", "26704", "26705", "26706", "26707", "26708", "26709", "26710", "26711", "26712", "26713", "26714", "26715", "26716", "26717", "26718", "26719", "26720", "26721", "26722", "26723", "26724", "26725", "26726", "26727", "26728", "26729", "27700", "27701", "27702", "27703", "27704", "27705", "27706", "27707", "27708", "27709", "27710", "27711", "27712" } },
            new ObjectSet { Name = "Ext. Stairs - House A", Ids = new string[] { "21027", "21028", "21029", "21044", "40018", "40019", "40020" } },
            new ObjectSet { Name = "Ext. Stairs - House B", Ids = new string[] { "21127", "21128", "21129", "21144", "40118", "40119", "40120" } },
            new ObjectSet { Name = "Ext. Stairs - House C", Ids = new string[] { "21227", "21228", "21229", "21244", "40218", "40219", "40220" } },
            new ObjectSet { Name = "Ext. Stairs - House D", Ids = new string[] { "21427", "21428", "21429", "21444", "40418", "40419", "40420" } },
            new ObjectSet { Name = "Ext. Stairs - House E", Ids = new string[] { "21527", "21528", "21529", "21544", "40518", "40519", "40520" } },
            new ObjectSet { Name = "Ext. Stairs - House F", Ids = new string[] { "21827", "21828", "21829", "21844", "40818", "40819", "40820" } },
            new ObjectSet { Name = "Ext. Stairs - Inn", Ids = new string[] { "21627", "21628", "21629", "21644", "40618", "40619", "40620" } },
            new ObjectSet { Name = "Ext. Stairs - Mages Guild", Ids = new string[] { "21327", "21328", "21329", "21344", "40318", "40319", "40320" } },
            new ObjectSet { Name = "Ext. Stairs - Temple", Ids = new string[] { "21727", "21728", "21729", "21744", "40718", "40719", "40720" } },
        };

        public static ObjectSet[] modelsClutter = new ObjectSet[]
        {
            new ObjectSet { Name = "Anvil", Ids = new string[] { "41118" } },
            new ObjectSet { Name = "Armor", Ids = new string[] { "74226" } },
            new ObjectSet { Name = "Arrow", Ids = new string[] { "99800" } },
            new ObjectSet { Name = "Axe", Ids = new string[] { "74225" } },
            new ObjectSet { Name = "Banners (Daggerfall)", Ids = new string[] { "42536", "42548", "42560", "42500", "42512", "42524" } },
            new ObjectSet { Name = "Banners (Direnni)", Ids = new string[] { "42540", "42552", "42564", "42504", "42516", "42528" } },
            new ObjectSet { Name = "Banners (Dwynnen)", Ids = new string[] { "42541", "42553", "42565", "42505", "42517", "42529" } },
            new ObjectSet { Name = "Banners (Order of the Flame)", Ids = new string[] { "42543", "42507", "42519", "42531", "42555", "42567" } },
            new ObjectSet { Name = "Banners (Order of the Lamp)", Ids = new string[] { "42542", "42554", "42566", "42506", "42518", "42530" } },
            new ObjectSet { Name = "Banners (Sentinel)", Ids = new string[] { "42537", "42549", "42561", "42501", "42513", "42525" } },
            new ObjectSet { Name = "Banners (Wayrest)", Ids = new string[] { "42538", "42550", "42562", "42502", "42514", "42526" } },
            new ObjectSet { Name = "Banners (Large)", Ids = new string[] { "42558", "42570", "42546", "42557", "42569", "42545", "42544", "42547", "42556", "42568", "42559", "42571", "42539", "42551", "42563" } },
            new ObjectSet { Name = "Banners (Small)", Ids = new string[] { "42510", "42522", "42534", "42533", "42509", "42521", "42508", "42520", "42532", "42511", "42523", "42503", "42515", "42527", "42535" } },
            new ObjectSet { Name = "Block Noose Support", Ids = new string[] { "41703" } },
            new ObjectSet { Name = "Bulletin Board", Ids = new string[] { "41739" } },
            new ObjectSet { Name = "Carpets", Ids = new string[] { "74800", "74801", "74802", "74803", "74804", "74805", "74806", "74807", "74808" } },
            new ObjectSet { Name = "Cart - Empty", Ids = new string[] { "41214" } },
            new ObjectSet { Name = "Cart - Full", Ids = new string[] { "41241" } },
            new ObjectSet { Name = "Catapult", Ids = new string[] { "41407" } },
            new ObjectSet { Name = "Corner Shelf", Ids = new string[] { "41128" } },
            new ObjectSet { Name = "Crossbow", Ids = new string[] { "74228" } },
            new ObjectSet { Name = "Fireplace", Ids = new string[] { "41116" } },
            new ObjectSet { Name = "Fireplace - Corner", Ids = new string[] { "41117" } },
            new ObjectSet { Name = "Ladder", Ids = new string[] { "41409" } },
            new ObjectSet { Name = "Large Raised Wooden Platform", Ids = new string[] { "41405" } },
            new ObjectSet { Name = "Mantlet", Ids = new string[] { "41402" } },
            new ObjectSet { Name = "Mantlet - Large", Ids = new string[] { "41401" } },
            new ObjectSet { Name = "Organ", Ids = new string[] { "41120" } },
            new ObjectSet { Name = "Paintings", Ids = new string[] { "51115", "51116", "51117", "51118", "51119", "51120" } },
            new ObjectSet { Name = "Rocks - Small", Ids = new string[] { "41704", "41710", "41712", "60711", "60712", "60713", "60714", "60715", "60716", "60717", "60718", "60719", "60720" } },
            new ObjectSet { Name = "Rocks - Medium", Ids = new string[] { "41705", "41706", "41707", "41708", "41709", "41711", "41713" } },
            new ObjectSet { Name = "Rocks - Large", Ids = new string[] { "41714", "41715", "41716", "41717", "41718", "41719", "60710" } },
            new ObjectSet { Name = "Sawhorse", Ids = new string[] { "41125" } },
            new ObjectSet { Name = "Scaffolding 1", Ids = new string[] { "41403" } },
            new ObjectSet { Name = "Scaffolding 2", Ids = new string[] { "41404" } },
            new ObjectSet { Name = "Spinning Wheel", Ids = new string[] { "41009" } },
            new ObjectSet { Name = "Stocks 1", Ids = new string[] { "41700" } },
            new ObjectSet { Name = "Stocks 2", Ids = new string[] { "41701" } },
            new ObjectSet { Name = "Support Beam", Ids = new string[] { "62319" } },
            new ObjectSet { Name = "Support Beam - Arched", Ids = new string[] { "62321" } },
            new ObjectSet { Name = "Support Beam - Diagonal", Ids = new string[] { "62318" } },
            new ObjectSet { Name = "Swords", Ids = new string[] { "74224", "74227" } },
            new ObjectSet { Name = "Sword - Large", Ids = new string[] { "74095" } },
            new ObjectSet { Name = "Tents", Ids = new string[] { "41606", "41607", "41608", "41609", "41610" } },
            new ObjectSet { Name = "Trebuchet", Ids = new string[] { "41406" } },
            new ObjectSet { Name = "Trellis", Ids = new string[] { "41240" } },
            new ObjectSet { Name = "Water Trough", Ids = new string[] { "41209" } },
            new ObjectSet { Name = "Water Trough - Empty", Ids = new string[] { "41210" } },
            new ObjectSet { Name = "Wood Stake", Ids = new string[] { "41400" } },
            new ObjectSet { Name = "Wooden Plank", Ids = new string[] { "41242" } },
            new ObjectSet { Name = "Wooden Tree Log", Ids = new string[] { "41734", "41735", "41736", "41737", "41738" } },
        };

        public static ObjectSet[] modelsDungeon = new ObjectSet[]
        {
            new ObjectSet { Name = "Altars", Ids = new string[] { "51110", "51111", "41304", "41307", "41309", "41310", "41311", "41305", "51112", "51113", "51114" } },
            new ObjectSet { Name = "Cage - Medium", Ids = new string[] { "41313" } },
            new ObjectSet { Name = "Cage - Small", Ids = new string[] { "41312" } },
            new ObjectSet { Name = "Coffins - Stone", Ids = new string[] { "41319", "41320", "41321", "41324", "41327" } },
            new ObjectSet { Name = "Coffins - Wood", Ids = new string[] { "41315", "41317", "41316", "41318", "41322", "41323", "41325", "41326" } },
            new ObjectSet { Name = "Grates", Ids = new string[] { "41603", "41605", "41604", "41314" } },
            new ObjectSet { Name = "Statues", Ids = new string[] { "62323", "62325", "62327", "62329" } },
            new ObjectSet { Name = "Statues - Large", Ids = new string[] { "62324", "62326", "62328", "62330" } },
            new ObjectSet { Name = "Pedestal - Stone", Ids = new string[] { "74091" } },
            new ObjectSet { Name = "Pedestal - Wood", Ids = new string[] { "74082" } },
            new ObjectSet { Name = "Torture Recliner - Knives", Ids = new string[] { "41300" } },
            new ObjectSet { Name = "Torture Table - Knives", Ids = new string[] { "41301" } },
            new ObjectSet { Name = "Torture Table - Rack", Ids = new string[] { "41303" } },
            new ObjectSet { Name = "Torture Table - Spikes", Ids = new string[] { "41302" } },
        };

        public static ObjectSet[] modelsFurniture = new ObjectSet[]
        {
            new ObjectSet { Name = "Beds", Ids = new string[] { "41000", "41001", "41002" } },
            new ObjectSet { Name = "Benches", Ids = new string[] { "41105", "41106", "41107", "41126", "51108", "51109", "43307" } },
            new ObjectSet { Name = "Cabinet", Ids = new string[] { "41007" } },
            new ObjectSet { Name = "Cabinet - Double", Ids = new string[] { "41051" } },
            new ObjectSet { Name = "Chairs", Ids = new string[] { "41100", "41101", "41102", "41103" } },
            new ObjectSet { Name = "Chests", Ids = new string[] { "41811", "41812", "41813" } },
            new ObjectSet { Name = "Crates", Ids = new string[] { "41815", "41816", "41817", "41818", "41819", "41820", "41821", "41822", "41823", "41824", "41825", "41826", "41827", "41828", "41829", "41830", "41831", "41832", "41833", "41834" } },
            new ObjectSet { Name = "Cupboard", Ids = new string[] { "41003" } },
            new ObjectSet { Name = "Cupboard - Double", Ids = new string[] { "41004" } },
            new ObjectSet { Name = "Drawers", Ids = new string[] { "41034", "41050", "41037", "41036", "41035" } },
            new ObjectSet { Name = "Dresser", Ids = new string[] { "41032" } },
            new ObjectSet { Name = "Lectern", Ids = new string[] { "41024", "41020", "41021", "41022" } },
            new ObjectSet { Name = "Stool", Ids = new string[] { "41113", "41114" } },
            new ObjectSet { Name = "Shelves - Alchemy", Ids = new string[] { "41042", "41043", "41041", "41044" } },
            new ObjectSet { Name = "Shelves - Books", Ids = new string[] { "41006", "41019", "41026", "41015", "41018", "41025", "41014" } },
            new ObjectSet { Name = "Shelves - Clothes", Ids = new string[] { "41013", "41011", "41012", "41010" } },
            new ObjectSet { Name = "Shelves - Drinks", Ids = new string[] { "41124" } },
            new ObjectSet { Name = "Shelves - Empty", Ids = new string[] { "41030" } },
            new ObjectSet { Name = "Shelves - Food", Ids = new string[] { "41040", "41029", "41039", "41027", "41046" } },
            new ObjectSet { Name = "Shelves - Utility", Ids = new string[] { "41005", "41045" } },
            new ObjectSet { Name = "Shelves - Weapons", Ids = new string[] { "41031", "41028", "41048", "41049", "41047" } },
            new ObjectSet { Name = "Tables", Ids = new string[] { "41108", "41109", "41110", "41111", "41112", "41130", "51103", "51104" } },
            new ObjectSet { Name = "Thrones", Ids = new string[] { "41122", "41123", "41104" } },
        };

        public static ObjectSet[] modelsGraveyard = new ObjectSet[]
        {
            new ObjectSet { Name = "Graveyard Gate Door Right", Ids = new string[] { "43000" } },
            new ObjectSet { Name = "Graveyard Gate Door Mid", Ids = new string[] { "43001" } },
            new ObjectSet { Name = "Graveyard Gate Door Left", Ids = new string[] { "43002" } },
            new ObjectSet { Name = "Graveyard Gates", Ids = new string[] { "43003", "43004", "43005", "43006", "43007", "43008", "43009", "43010", "62310", "62312", "62317" } },
            new ObjectSet { Name = "Graveyard Monuments", Ids = new string[] { "43079", "43080", "43081", "43082", "43109", "43110", "43111", "43112", "43202", "43204", "62314" } },
            new ObjectSet { Name = "Mausoleum Dark", Ids = new string[] { "43138" } },
            new ObjectSet { Name = "Mausoleum Gray", Ids = new string[] { "43140" } },
            new ObjectSet { Name = "Mausoleum Red 1", Ids = new string[] { "43139" } },
            new ObjectSet { Name = "Mausoleum Red 2", Ids = new string[] { "41619" } },
            new ObjectSet { Name = "Mausoleum White 1", Ids = new string[] { "43141" } },
            new ObjectSet { Name = "Mausoleum White 2", Ids = new string[] { "43137" } },
            new ObjectSet { Name = "Pillar Tombs (Dark)", Ids = new string[] { "43071", "43105", "43125" } },
            new ObjectSet { Name = "Pillar Tombs (Gray)", Ids = new string[] { "43073", "43107", "43127" } },
            new ObjectSet { Name = "Pillar Tombs (Red)", Ids = new string[] { "43072", "43106", "43126" } },
            new ObjectSet { Name = "Pillar Tombs (White)", Ids = new string[] { "43074", "43108", "43128", "43303" } },
            new ObjectSet { Name = "Slabs - Stone (Dark)", Ids = new string[] { "43027", "43031", "43035" } },
            new ObjectSet { Name = "Slabs - Stone (Gray)", Ids = new string[] { "43029", "43033", "43037" } },
            new ObjectSet { Name = "Slabs - Stone (Red)", Ids = new string[] { "43028", "43032", "43036" } },
            new ObjectSet { Name = "Slabs - Stone (White)", Ids = new string[] { "43030", "43034", "43038" } },
            new ObjectSet { Name = "Stone Ankhs", Ids = new string[] { "43083", "43084", "43085", "43086", "43121", "43122", "43123", "43124" } },
            new ObjectSet { Name = "Stone Caskets", Ids = new string[] { "43075", "43076", "43077", "43078", "43304", "43305", "43306" } },
            new ObjectSet { Name = "Tombstone Wall Dark", Ids = new string[] { "43113" } },
            new ObjectSet { Name = "Tombstone Wall Red", Ids = new string[] { "43114" } },
            new ObjectSet { Name = "Tombstone Wall Gray", Ids = new string[] { "43115" } },
            new ObjectSet { Name = "Tombstone Wall White", Ids = new string[] { "43116" } },
            new ObjectSet { Name = "Tombstones - Broken", Ids = new string[] { "43129", "43130", "43131", "43132", "43206" } },
            new ObjectSet { Name = "Tombstones - Large (Dark)", Ids = new string[] { "43133", "43142", "43200", "43201", "43203", "43205" } },
            new ObjectSet { Name = "Tombstones - Large (Gray)", Ids = new string[] { "43135", "43144" } },
            new ObjectSet { Name = "Tombstones - Large (Red)", Ids = new string[] { "43134", "43143" } },
            new ObjectSet { Name = "Tombstones - Large (White)", Ids = new string[] { "43136", "43145" } },
            new ObjectSet { Name = "Tombstones - Medium (Dark)", Ids = new string[] { "43055", "43059", "43063", "43067", "43101", "43117" } },
            new ObjectSet { Name = "Tombstones - Medium (Gray)", Ids = new string[] { "43057", "43061", "43065", "43069", "43103", "43119" } },
            new ObjectSet { Name = "Tombstones - Medium (Red)", Ids = new string[] { "43056", "43060", "43064", "43068", "43102", "43118" } },
            new ObjectSet { Name = "Tombstones - Medium (White)", Ids = new string[] { "43058", "43062", "43066", "43070", "43104", "43120" } },
            new ObjectSet { Name = "Tombstones - Small (Dark)", Ids = new string[] { "43011", "43015", "43019", "43023", "43039", "43043", "43047", "43051" } },
            new ObjectSet { Name = "Tombstones - Small (Gray)", Ids = new string[] { "43013", "43017", "43021", "43025", "43041", "43045", "43049", "43053", "43300", "43301" } },
            new ObjectSet { Name = "Tombstones - Small (Red)", Ids = new string[] { "43012", "43016", "43020", "43024", "43040", "43044", "43048", "43052", "43302" } },
            new ObjectSet { Name = "Tombstones - Small (White)", Ids = new string[] { "43014", "43018", "43022", "43026", "43042", "43046", "43050", "43054" } },
        };

        public static ObjectSet[] models = modelsStructure
            .Concat(modelsClutter)
            .Concat(modelsDungeon)
            .Concat(modelsFurniture)
            .Concat(modelsGraveyard)
            .OrderBy(set => set.Name)
            .ToArray();

        public static ObjectSet[] billboardsPeople = new ObjectSet[]
        {
            new ObjectSet { Name = "Beggars", Ids = new string[] { "182.30", "182.21", "182.31", "182.29", "184.27" } },
            new ObjectSet { Name = "Children", Ids = new string[] { "182.4", "184.15", "182.38", "182.42", "182.43", "182.53", "182.52" } },
            new ObjectSet { Name = "Commoners - Men", Ids = new string[] { "182.20", "182.35", "197.1", "184.17", "182.46", "182.17", "182.16", "182.24", "182.23", "184.20", "184.24", "184.25", "182.25", "182.39", "182.18", "182.13", "182.14", "182.19" } },
            new ObjectSet { Name = "Commoners - Women", Ids = new string[] { "184.30", "184.32", "182.47", "182.12", "182.26", "184.28", "184.29", "184.33", "182.45", "184.18", "184.23", "184.22", "184.26" } },
            new ObjectSet { Name = "Daedric Princes", Ids = new string[] { "175.0", "175.1", "175.2", "175.3", "175.4", "175.5", "175.6", "175.7", "175.8", "175.9", "175.10", "175.11", "175.12", "175.13", "175.14", "175.15" } },
            new ObjectSet { Name = "Dark Brotherhood", Ids = new string[] { "176.5", "176.4", "176.3", "176.2", "176.1", "176.0" } },
            new ObjectSet { Name = "Elders", Ids = new string[] { "184.21", "182.44", "184.2" } },
            new ObjectSet { Name = "Horse Rider", Ids = new string[] { "184.34" } },
            new ObjectSet { Name = "Inn", Ids = new string[] { "184.16", "182.1", "182.2", "182.3", "182.11", "182.7", "182.8" } },
            new ObjectSet { Name = "Jesters", Ids = new string[] { "182.5", "182.6", "182.49" } },
            new ObjectSet { Name = "Knights", Ids = new string[] { "183.2", "183.3", "183.4", "349.19" } },
            new ObjectSet { Name = "Mages", Ids = new string[] { "177.4", "177.3", "177.2", "177.1", "177.0", "182.41", "182.40", "184.1" } },
            new ObjectSet { Name = "Minstrels", Ids = new string[] { "182.37", "184.3", "182.50", "182.51", "182.36" } },
            new ObjectSet { Name = "Necromancers", Ids = new string[] { "178.5", "178.6", "178.1", "178.0", "178.4", "178.2", "178.3" } },
            new ObjectSet { Name = "Noblemen", Ids = new string[] { "183.5", "183.16", "180.3", "183.0", "183.10", "183.13", "183.20", "183.6", "197.10", "197.4", "197.9", "182.15", "184.0", "184.4", "195.11", "180.2", "183.7" } },
            new ObjectSet { Name = "Noblewomen", Ids = new string[] { "180.0", "197.7", "197.8", "182.27", "184.19", "183.11", "182.9", "182.10", "184.5", "180.1", "183.1", "183.18", "183.21", "183.8", "183.9" } },
            new ObjectSet { Name = "Prisoner", Ids = new string[] { "184.31" } },
            new ObjectSet { Name = "Prostitutes", Ids = new string[] { "184.6", "184.7", "184.8", "184.9", "184.10", "184.11", "184.12", "184.13", "184.14", "182.34", "182.48" } },
            new ObjectSet { Name = "Shopkeeper", Ids = new string[] { "182.0" } },
            new ObjectSet { Name = "Smiths", Ids = new string[] { "177.5", "182.59" } },
            new ObjectSet { Name = "Temple", Ids = new string[] { "183.12", "182.33", "182.57", "181.7", "181.6", "181.5", "182.58", "181.4", "182.32", "182.28", "182.22", "181.3", "181.2", "181.1", "181.0" } },
            new ObjectSet { Name = "Vampires", Ids = new string[] { "182.56", "182.54", "182.55" } },
            new ObjectSet { Name = "Witch Covens", Ids = new string[] { "179.3", "179.2", "179.1", "179.0", "179.4" } },
        };

        public static ObjectSet[] billboardsInterior = new ObjectSet[]
        {
            new ObjectSet { Name = "Clothing - Boots", Ids = new string[] { "204.1", "204.2" } },
            new ObjectSet { Name = "Clothing - Pile of Clothes", Ids = new string[] { "204.0" } },
            new ObjectSet { Name = "Clothing - Hats", Ids = new string[] { "204.3", "204.4", "204.5" } },
            new ObjectSet { Name = "Clothing - Rolls of Cloth", Ids = new string[] { "204.6", "204.7", "204.8" } },
            new ObjectSet { Name = "Containers - Barrel", Ids = new string[] { "205.0" } },
            new ObjectSet { Name = "Containers - Baskets", Ids = new string[] { "205.8", "205.9", "205.10" } },
            new ObjectSet { Name = "Containers - Buckets", Ids = new string[] { "205.29", "205.30" } },
            new ObjectSet { Name = "Containers - Chests", Ids = new string[] { "205.21", "205.22", "205.23", "205.24", "205.25", "205.26" } },
            new ObjectSet { Name = "Containers - Grain Sacks", Ids = new string[] { "205.17", "205.18", "205.19", "205.20" } },
            new ObjectSet { Name = "Containers - Pots", Ids = new string[] { "218.0", "218.1", "218.2", "218.3", "211.2", "205.41" } },
            new ObjectSet { Name = "Containers - Pouch", Ids = new string[] { "205.36" } },
            new ObjectSet { Name = "Containers - Sack", Ids = new string[] { "205.44" } },
            new ObjectSet { Name = "Equipment - Lance Rack", Ids = new string[] { "211.13" } },
            new ObjectSet { Name = "Equipment - Armor", Ids = new string[] { "207.9", "207.10", "207.11", "207.12", "207.13", "207.14" } },
            new ObjectSet { Name = "Equipment - Quiver", Ids = new string[] { "205.42" } },
            new ObjectSet { Name = "Equipment - Saddle", Ids = new string[] { "204.9" } },
            new ObjectSet { Name = "Equipment - Spear Rack", Ids = new string[] { "211.14" } },
            new ObjectSet { Name = "Equipment - Sword Rack", Ids = new string[] { "211.12" } },
            new ObjectSet { Name = "Equipment - Weapons", Ids = new string[] { "207.0", "207.1", "207.2", "207.3", "207.4", "207.5", "207.6", "207.7", "207.8", "207.15", "207.16" } },
            new ObjectSet { Name = "Food - Apple", Ids = new string[] { "213.1" } },
            new ObjectSet { Name = "Food - Bread", Ids = new string[] { "211.31" } },
            new ObjectSet { Name = "Food - Fish Fillets", Ids = new string[] { "211.41", "211.42" } },
            new ObjectSet { Name = "Food - Fishes", Ids = new string[] { "211.8", "211.9", "211.10", "211.11" } },
            new ObjectSet { Name = "Food - Meat", Ids = new string[] { "211.40" } },
            new ObjectSet { Name = "Food - Orange", Ids = new string[] { "213.0" } },
            new ObjectSet { Name = "Housing - Bottles", Ids = new string[] { "205.11", "205.12", "205.13", "205.14", "205.15", "205.16" } },
            new ObjectSet { Name = "Housing - Chair", Ids = new string[] { "200.14" } },
            new ObjectSet { Name = "Housing - Cooking Pan", Ids = new string[] { "218.4" } },
            new ObjectSet { Name = "Housing - Cradle", Ids = new string[] { "200.18" } },
            new ObjectSet { Name = "Housing - Cups", Ids = new string[] { "200.0", "200.1", "200.2", "200.3", "200.4", "200.5", "200.6" } },
            new ObjectSet { Name = "Housing - Drapes", Ids = new string[] { "211.43", "211.44", "211.45", "211.46" } },
            new ObjectSet { Name = "Housing - Flowers", Ids = new string[] { "254.26", "254.27", "254.28", "254.29", "432.19", "254.11", "254.12", "254.30", "254.31", "254.32", "254.33" } },
            new ObjectSet { Name = "Housing - Hanging Spoon", Ids = new string[] { "218.6" } },
            new ObjectSet { Name = "Housing - Pillows", Ids = new string[] { "200.11", "200.13" } },
            new ObjectSet { Name = "Housing - Rocking Horse", Ids = new string[] { "211.21" } },
            new ObjectSet { Name = "Jewelry", Ids = new string[] { "211.48", "216.4", "216.5", "216.21", "254.56" } },
            new ObjectSet { Name = "Laboratory - Boiling Potions", Ids = new string[] { "208.2", "253.41" } },
            new ObjectSet { Name = "Laboratory - Alchemy Bottles", Ids = new string[] { "205.1", "205.2", "205.3", "205.4", "205.5", "205.6", "205.7" } },
            new ObjectSet { Name = "Laboratory - Flasks", Ids = new string[] { "205.31", "205.32", "205.33", "205.34", "205.35", "205.43" } },
            new ObjectSet { Name = "Laboratory - Globe", Ids = new string[] { "208.0" } },
            new ObjectSet { Name = "Laboratory - Hourglass", Ids = new string[] { "208.6" } },
            new ObjectSet { Name = "Laboratory - Ing. - Flora", Ids = new string[] { "254.9", "254.10",  "254.13", "254.14", "254.15", "254.16", "254.17", "254.18", "254.19", "254.20", "254.21", "254.22", "254.23", "254.24", "254.25" } },
            new ObjectSet { Name = "Laboratory - Ing. - Foes", Ids = new string[] { "254.37", "254.38", "254.39", "254.40", "254.41", "254.42", "254.43", "254.44", "254.45", "254.47", "254.48", "254.50", "254.51", "254.53", "254.54", "254.55", "254.57", "254.58", "254.59", "254.60" } },
            new ObjectSet { Name = "Laboratory - Ing. - Minerals", Ids = new string[] { "254.0", "254.1", "254.2", "254.3", "254.4", "254.5", "254.6", "254.7", "254.8", "254.61", "254.62", "254.63", "254.64", "254.65", "254.66", "254.67", "254.68", "254.69", "254.70", "254.71" } },
            new ObjectSet { Name = "Laboratory - Magnifying Glasses", Ids = new string[] { "208.1", "208.5" } },
            new ObjectSet { Name = "Laboratory - Scales", Ids = new string[] { "208.3" } },
            new ObjectSet { Name = "Laboratory - Telescope", Ids = new string[] { "208.4" } },
            new ObjectSet { Name = "Library - Books", Ids = new string[] { "209.0", "209.1", "209.2", "209.3", "209.4" } },
            new ObjectSet { Name = "Library - Parchments", Ids = new string[] { "209.5", "209.6", "209.7", "209.8", "209.9", "209.10" } },
            new ObjectSet { Name = "Library - Quill", Ids = new string[] { "211.1" } },
            new ObjectSet { Name = "Library - Tablets", Ids = new string[] { "209.11", "209.12", "209.13", "209.14", "209.15" } },
            new ObjectSet { Name = "Misc. - Bandages", Ids = new string[] { "211.0" } },
            new ObjectSet { Name = "Misc. - Candle Snuffer", Ids = new string[] { "211.23" } },
            new ObjectSet { Name = "Misc. - Coal Pile", Ids = new string[] { "200.17" } },
            new ObjectSet { Name = "Misc. - Crystal In a Bag", Ids = new string[] { "211.55" } },
            new ObjectSet { Name = "Misc. - Finger/Burrito", Ids = new string[] { "211.56" } },
            new ObjectSet { Name = "Misc. - Meat Hanger", Ids = new string[] { "211.34" } },
            new ObjectSet { Name = "Misc. - Miniature Houses", Ids = new string[] { "211.37", "211.38", "211.39" } },
            new ObjectSet { Name = "Misc. - Painting", Ids = new string[] { "211.57" } },
            new ObjectSet { Name = "Misc. - Smoking Pipes", Ids = new string[] { "211.24", "211.25" } },
            new ObjectSet { Name = "Misc. - Totem", Ids = new string[] { "211.54" } },
            new ObjectSet { Name = "Misc. - Training Dummy", Ids = new string[] { "211.20" } },
            new ObjectSet { Name = "Misc. - Training Pole", Ids = new string[] { "211.30" } },
            new ObjectSet { Name = "Plants - Hanged", Ids = new string[] { "213.13", "213.14" } },
            new ObjectSet { Name = "Plants - Potted", Ids = new string[] { "213.2", "213.3", "213.4", "213.5", "213.6" } },
            new ObjectSet { Name = "Religious - Bell", Ids = new string[] { "211.47" } },
            new ObjectSet { Name = "Religious - Holy Water", Ids = new string[] { "211.49" } },
            new ObjectSet { Name = "Religious - Icon", Ids = new string[] { "211.51" } },
            new ObjectSet { Name = "Religious - Religious Item", Ids = new string[] { "211.50" } },
            new ObjectSet { Name = "Religious - Scarab", Ids = new string[] { "211.52" } },
            new ObjectSet { Name = "Religious - Statuettes", Ids = new string[] { "202.5", "202.6" } },
            new ObjectSet { Name = "Religious - Trinket", Ids = new string[] { "211.53" } },
            new ObjectSet { Name = "Statues - Dibella", Ids = new string[] { "97.12" } },
            new ObjectSet { Name = "Statues - Julianos", Ids = new string[] { "97.6", "97.7", "97.8" } },
            new ObjectSet { Name = "Statues - Kynareth", Ids = new string[] { "97.13", "97.14", "97.15", "97.16", "97.17" } },
            new ObjectSet { Name = "Statues - Man", Ids = new string[] { "97.0", "97.9" } },
            new ObjectSet { Name = "Statues - Stendarr", Ids = new string[] { "97.3" } },
            new ObjectSet { Name = "Statues - Women", Ids = new string[] { "97.2", "97.4", "97.5", "97.10", "97.11" } },
            new ObjectSet { Name = "Statues - Zenithar", Ids = new string[] { "97.1", "97.18", "97.19", "97.20", "97.21" } },
            new ObjectSet { Name = "Statues - Monsters", Ids = new string[] { "98.0", "98.1", "98.2", "98.3", "98.4", "98.5", "98.6", "98.7", "98.8", "98.9", "98.10", "98.11", "98.12", "98.13", "98.14", "202.0", "202.1", "202.2", "202.3", "202.4" } },
            new ObjectSet { Name = "Tools - Anvil", Ids = new string[] { "211.35" } },
            new ObjectSet { Name = "Tools - Bellows", Ids = new string[] { "214.9" } },
            new ObjectSet { Name = "Tools - Broom", Ids = new string[] { "214.10" } },
            new ObjectSet { Name = "Tools - Brush", Ids = new string[] { "214.12" } },
            new ObjectSet { Name = "Tools - Butter Churn", Ids = new string[] { "214.5" } },
            new ObjectSet { Name = "Tools - Fish Net", Ids = new string[] { "212.7" } },
            new ObjectSet { Name = "Tools - Hammers", Ids = new string[] { "214.2", "214.3" } },
            new ObjectSet { Name = "Tools - Hoe", Ids = new string[] { "214.6" } },
            new ObjectSet { Name = "Tools - Iron", Ids = new string[] { "214.15" } },
            new ObjectSet { Name = "Tools - Loom Tables", Ids = new string[] { "200.15", "200.16" } },
            new ObjectSet { Name = "Tools - Meat Hook", Ids = new string[] { "211.36" } },
            new ObjectSet { Name = "Tools - Rope", Ids = new string[] { "214.8" } },
            new ObjectSet { Name = "Tools - Scoops", Ids = new string[] { "214.4", "214.11" } },
            new ObjectSet { Name = "Tools - Scythe", Ids = new string[] { "214.7" } },
            new ObjectSet { Name = "Tools - Shears", Ids = new string[] { "214.14" } },
            new ObjectSet { Name = "Tools - Shovels", Ids = new string[] { "214.0", "214.1" } },
            new ObjectSet { Name = "Tools - Tongs", Ids = new string[] { "214.13" } },
        };

        public static ObjectSet[] billboardsNature = new ObjectSet[]
        {
            new ObjectSet { Name = "Animals - Camel", Ids = new string[] { "201.2" } },
            new ObjectSet { Name = "Animals - Cats", Ids = new string[] { "201.7", "201.8" } },
            new ObjectSet { Name = "Animals - Cows", Ids = new string[] { "201.3", "201.4" } },
            new ObjectSet { Name = "Animals - Dogs", Ids = new string[] { "201.9", "201.10" } },
            new ObjectSet { Name = "Animals - Horses", Ids = new string[] { "201.0", "201.1" } },
            new ObjectSet { Name = "Animals - Pigs", Ids = new string[] { "201.5", "201.6" } },
            new ObjectSet { Name = "Animals - Seagull", Ids = new string[] { "201.11" } },
            new ObjectSet { Name = "Crops", Ids = new string[] { "301.0", "301.1", "301.2", "301.3", "301.4", "301.5", "301.6", "301.7", "301.8", "301.9", "301.10", "301.11", "301.12", "301.13", "301.14", "301.15", "301.16", "301.17", "301.18", "301.19", "301.20", "301.21", "301.22", "301.23" } },
            new ObjectSet { Name = "Dead Wood", Ids = new string[] { "213.11", "213.12" } },
            new ObjectSet { Name = "Fountains", Ids = new string[] { "212.2", "212.3" } },
            new ObjectSet { Name = "Hay Ricks", Ids = new string[] { "212.15", "212.16" } },
            new ObjectSet { Name = "Hay Stack", Ids = new string[] { "212.1" } },
            new ObjectSet { Name = "Manure", Ids = new string[] { "253.21" } },
            new ObjectSet { Name = "Plants - Desert", Ids = new string[] { "503.1", "503.7", "503.8", "503.9", "503.10", "503.14", "503.15", "503.16", "503.17", "503.23", "503.24", "503.25", "503.26", "503.27", "503.29", "503.31" } },
            new ObjectSet { Name = "Plants - Haunted Woodland", Ids = new string[] { "508.2", "508.7", "508.8", "508.9", "508.11", "508.14", "508.21", "508.22", "508.23", "508.26", "508.27", "508.28", "508.29" } },
            new ObjectSet { Name = "Plants - Mountains", Ids = new string[] { "510.2", "510.7", "510.8", "510.9", "510.10", "510.21", "510.22", "510.23", "510.26", "510.29" } },
            new ObjectSet { Name = "Plants - Rain Forest", Ids = new string[] { "500.1", "500.2", "500.5", "500.6", "500.7", "500.8", "500.9", "500.10", "500.11", "500.20", "500.21", "500.22", "500.23", "500.24", "500.26", "500.27", "500.29", "500.31" } },
            new ObjectSet { Name = "Plants - Steppes", Ids = new string[] { "5.7", "5.8", "5.9", "5.10", "5.17", "5.23", "5.24", "5.25", "5.26", "5.27", "5.29", "5.31" } },
            new ObjectSet { Name = "Plants - Subtropical", Ids = new string[] { "501.1", "501.2", "501.7", "501.8", "501.9", "501.14", "501.18", "501.20", "501.21", "501.22", "501.25", "501.26", "501.27", "501.28", "501.29", "501.31" } },
            new ObjectSet { Name = "Plants - Swamp", Ids = new string[] { "502.1", "502.7", "502.8", "502.9", "502.11", "502.14", "502.20", "502.21", "502.22", "502.23", "502.26", "502.27", "502.28", "502.29", "502.31" } },
            new ObjectSet { Name = "Plants - Woodland", Ids = new string[] { "504.21", "504.22", "504.23", "504.26", "504.2", "504.1", "504.7", "504.8", "504.9", "504.10", "504.24", "504.27", "504.28", "504.29" } },
            new ObjectSet { Name = "Plants - Woodland Hills", Ids = new string[] { "506.2", "506.7", "506.8", "506.9", "506.21", "506.22", "506.23", "506.26", "506.27", "506.29", "506.31" } },
            new ObjectSet { Name = "Rocks - Desert", Ids = new string[] { "503.2", "503.3", "503.4", "503.6", "503.18", "503.19", "503.20", "503.21", "503.22" } },
            new ObjectSet { Name = "Rocks - Haunted Woodland", Ids = new string[] { "508.1", "508.3", "508.4", "508.5", "508.6", "508.10", "508.12", "508.17" } },
            new ObjectSet { Name = "Rocks - Mountains", Ids = new string[] { "510.1", "510.3", "510.4", "510.6", "510.14", "510.17", "510.18", "510.27", "510.28", "510.31" } },
            new ObjectSet { Name = "Rocks - Rain Forest", Ids = new string[] { "500.4", "500.17", "500.19", "500.28" } },
            new ObjectSet { Name = "Rocks - Steppes", Ids = new string[] { "5.1", "5.2", "5.3", "5.4", "5.6", "5.18", "5.19", "5.20", "5.21", "5.22" } },
            new ObjectSet { Name = "Rocks - Subtropical", Ids = new string[] { "501.3", "501.4", "501.5", "501.6", "501.10", "501.23" } },
            new ObjectSet { Name = "Rocks - Swamp", Ids = new string[] { "502.2", "502.3", "502.4", "502.5", "502.6", "502.10" } },
            new ObjectSet { Name = "Rocks - Woodland", Ids = new string[] { "504.4", "504.3", "504.5", "504.6" } },
            new ObjectSet { Name = "Rocks - Woodland Hills", Ids = new string[] { "506.1", "506.3", "506.4", "506.6", "506.17", "506.18", "506.28" } },
            new ObjectSet { Name = "Shrubs", Ids = new string[] { "213.15", "213.16", "213.17" } },
            new ObjectSet { Name = "Signposts", Ids = new string[] { "212.4", "212.5", "212.6" } },
            new ObjectSet { Name = "Standing Stones", Ids = new string[] { "212.17", "212.18" } },
            new ObjectSet { Name = "Trees - Desert", Ids = new string[] { "503.5", "503.11", "503.12", "503.13", "503.28", "503.30" } },
            new ObjectSet { Name = "Trees - Haunted Woodland", Ids = new string[] { "508.13", "508.15", "508.16", "508.18", "508.19", "508.20", "508.24", "508.25", "508.30", "508.31" } },
            new ObjectSet { Name = "Trees - Mountains", Ids = new string[] { "510.5", "510.11", "510.12", "510.13", "510.15", "510.16", "510.19", "510.20", "510.24", "510.25", "510.30" } },
            new ObjectSet { Name = "Trees - Rain Forest", Ids = new string[] { "500.3", "500.12", "500.13", "500.14", "500.15", "500.16", "500.18", "500.25", "500.30" } },
            new ObjectSet { Name = "Trees - Steppes", Ids = new string[] { "5.5", "5.11", "5.12", "5.13", "5.14", "5.15", "5.16", "5.28", "5.30" } },
            new ObjectSet { Name = "Trees - Subtropical", Ids = new string[] { "501.11", "501.12", "501.13", "501.15", "501.16", "501.17", "501.19", "501.24", "501.30" } },
            new ObjectSet { Name = "Trees - Swamp", Ids = new string[] { "502.12", "502.13", "502.15", "502.16", "502.17", "502.18", "502.19", "502.24", "502.25", "502.30" } },
            new ObjectSet { Name = "Trees - Woodland", Ids = new string[] { "504.11", "504.12", "504.13", "504.14", "504.15", "504.16", "504.17", "504.18", "504.19", "504.20", "504.25", "504.30", "504.31" } },
            new ObjectSet { Name = "Trees - Woodland Hills", Ids = new string[] { "506.5", "506.10", "506.11", "506.12", "506.13", "506.14", "506.15", "506.16", "506.19", "506.20", "506.24", "506.25", "506.30" } },
            new ObjectSet { Name = "Wagon Wheels", Ids = new string[] { "211.15", "211.16" } },
            new ObjectSet { Name = "Well", Ids = new string[] { "212.0" } },
            new ObjectSet { Name = "Well Pumps", Ids = new string[] { "212.8", "212.9", "212.10" } },
            new ObjectSet { Name = "Wheelbarrows", Ids = new string[] { "205.27", "205.28" } },
            new ObjectSet { Name = "Wood Pile", Ids = new string[] { "212.11" } },
            new ObjectSet { Name = "Wood Posts", Ids = new string[] { "212.13", "212.14" } },
        };

        public static ObjectSet[] billboardsLights = new ObjectSet[]
        {
            new ObjectSet { Name = "Brazier", Ids = new string[] { "210.0" } },
            new ObjectSet { Name = "Brazier - Pillar", Ids = new string[] { "210.19" } },
            new ObjectSet { Name = "Camp Fire", Ids = new string[] { "210.1" } },
            new ObjectSet { Name = "Candles", Ids = new string[] { "210.2", "210.3", "210.4" } },
            new ObjectSet { Name = "Candlestick", Ids = new string[] { "210.5" } },
            new ObjectSet { Name = "Chandleliers", Ids = new string[] { "210.7", "210.9", "210.10", "210.23" } },
            new ObjectSet { Name = "Hanging Lamps", Ids = new string[] { "210.8", "210.11", "210.12", "210.13" } },
            new ObjectSet { Name = "Hanging Lantern", Ids = new string[] { "210.22", "210.24", "210.25", "210.26", "210.27" } },
            new ObjectSet { Name = "Mounted Torches", Ids = new string[] { "210.16", "210.17", "210.18" } },
            new ObjectSet { Name = "Standing Candle", Ids = new string[] { "210.21" } },
            new ObjectSet { Name = "Standing Lanterns", Ids = new string[] { "210.14", "210.15" } },
            new ObjectSet { Name = "Torch - Skull", Ids = new string[] { "210.6" } },
            new ObjectSet { Name = "Torch - Standing", Ids = new string[] { "210.20" } },
            new ObjectSet { Name = "Street Lamps", Ids = new string[] { "210.28", "210.29" } },
        };

        public static ObjectSet[] billboardsTreasure = new ObjectSet[]
        {
                new ObjectSet { Name = "Goldpile", Ids = new string[] { "216.0", "216.1", "216.2" } },
                new ObjectSet { Name = "Gold Ingot", Ids = new string[] { "216.3" } },
                new ObjectSet { Name = "Crowns", Ids = new string[] { "216.6", "216.7", "216.8", "216.9" } },
                new ObjectSet { Name = "Gems", Ids = new string[] { "216.10", "216.11", "216.12", "216.13", "216.14", "216.15", "216.16", "216.17", "216.18", "216.19" } },
                new ObjectSet { Name = "Treasure Pile", Ids = new string[] { "216.20", "216.22", "216.23", "216.24", "216.25", "216.26", "216.27", "216.28", "216.29", "216.30"
                    , "216.31", "216.32", "216.33", "216.34", "216.35", "216.36", "216.37", "216.38", "216.39", "216.40", "216.41", "216.42", "216.43"
                    , "216.44", "216.45", "216.46", "216.47"
                } },
        };

        public static ObjectSet[] billboardsDungeon = new ObjectSet[]
        {
            new ObjectSet { Name = "Blood", Ids = new string[] { "206.33", "206.34" } },
            new ObjectSet { Name = "Bloody Pike", Ids = new string[] { "206.27" } },
            new ObjectSet { Name = "Bones", Ids = new string[] { "206.29", "206.30", "206.31", "206.32", "206.8" } },
            new ObjectSet { Name = "Ceiling Roots", Ids = new string[] { "213.7", "213.8", "213.9", "213.10" } },
            new ObjectSet { Name = "Chained Woman", Ids = new string[] { "211.33" } },
            new ObjectSet { Name = "Columns", Ids = new string[] { "203.2", "203.3", "203.4", "203.0", "203.1", "203.5", "203.6" } },
            new ObjectSet { Name = "Corpse Pile", Ids = new string[] { "206.22", "206.23", "206.24" } },
            new ObjectSet { Name = "Cross", Ids = new string[] { "211.32" } },
            new ObjectSet { Name = "Crucified Corpse", Ids = new string[] { "206.15", "206.16" } },
            new ObjectSet { Name = "Eviscerated Animal", Ids = new string[] { "206.35" } },
            new ObjectSet { Name = "Hanged Person", Ids = new string[] { "206.36" } },
            new ObjectSet { Name = "Hanging Chains", Ids = new string[] { "211.4", "211.5", "211.6", "211.7" } },
            new ObjectSet { Name = "Hanging Skeleton", Ids = new string[] { "206.10" } },
            new ObjectSet { Name = "Head Pile", Ids = new string[] { "206.14" } },
            new ObjectSet { Name = "Heads on Pikes", Ids = new string[] { "206.17", "206.18" } },
            new ObjectSet { Name = "Impaled Corpses", Ids = new string[] { "206.11", "206.12", "206.13" } },
            new ObjectSet { Name = "Iron Maidens", Ids = new string[] { "211.26", "211.27" } },
            new ObjectSet { Name = "Noose", Ids = new string[] { "211.22" } },
            new ObjectSet { Name = "Skeleton in Cage", Ids = new string[] { "206.26" } },
            new ObjectSet { Name = "Skulls", Ids = new string[] { "206.0", "206.1", "206.3", "206.4", "206.5", "206.6" } },
            new ObjectSet { Name = "Skulls on Pikes", Ids = new string[] { "206.2", "206.7", "206.9" } },
            new ObjectSet { Name = "Stalactites", Ids = new string[] { "300.0", "300.1", "300.2", "300.3", "300.4", "300.5" } },
            new ObjectSet { Name = "Stalagmites", Ids = new string[] { "300.6", "300.7", "300.8", "300.9", "300.10", "300.11", "300.12", "300.13", "300.14", "300.15" } },
            new ObjectSet { Name = "Statues - Monsters", Ids = new string[] { "098.0" } },
            new ObjectSet { Name = "Stocks", Ids = new string[] { "211.18", "211.19" } },
            new ObjectSet { Name = "Tombstones", Ids = new string[] { "206.19", "206.20", "206.21" } },
            new ObjectSet { Name = "Torture Rack", Ids = new string[] { "211.28" } },
            new ObjectSet { Name = "Underwater", Ids = new string[] { "105.0", "105.1", "105.2", "105.3", "105.4", "105.5", "105.6", "105.7", "105.8", "105.9", "105.10" } },
            new ObjectSet { Name = "Underwater - Animated", Ids = new string[] { "106.0", "106.1", "106.2", "106.3", "106.4", "106.5", "106.6" } },
        };

        public static ObjectSet[] billboardsCorpses = new ObjectSet[]
        {
            new ObjectSet { Name = "Corpse - Animal", Ids = new string[] { "401.0", "401.1", "401.2", "401.3", "401.4", "401.5" } },
            new ObjectSet { Name = "Corpse - Aquatic Monster", Ids = new string[] { "305.0", "305.1", "305.2" } },
            new ObjectSet { Name = "Corpse - Atronach", Ids = new string[] { "405.0", "405.1", "405.2", "405.3" } },
            new ObjectSet { Name = "Corpse - Daedra", Ids = new string[] { "400.0", "400.1", "400.2", "400.3", "400.4", "400.5", "400.6" } },
            new ObjectSet { Name = "Corpse - Diurnal Monster", Ids = new string[] { "406.0", "406.1", "406.2", "406.3", "406.4", "406.5" } },
            new ObjectSet { Name = "Corpse - Human", Ids = new string[] { "380.1" } },
            new ObjectSet { Name = "Corpse - Shadow Monster", Ids = new string[] { "96.0", "96.1", "96.2", "96.3", "96.4", "96.5" } },
            new ObjectSet { Name = "Corpse - Undead", Ids = new string[] { "306.0", "306.1", "306.2", "306.3", "306.4", "306.5" } },
        };


        public static ObjectSet[] billboards = billboardsPeople
            .Concat(billboardsInterior)
            .Concat(billboardsNature)
            .Concat(billboardsLights)
            .Concat(billboardsTreasure)
            .Concat(billboardsDungeon)
            .Concat(billboardsCorpses)
            .OrderBy(set => set.Name)
            .ToArray();

        public static Dictionary<string, string> editor = new Dictionary<string, string>()
        {
            { "199.4", "Rest Marker" },
            { "199.8", "Enter Marker" },
            { "199.10", "Start Marker" },
            { "199.11", "Quest Marker" },
            { "199.15", "Random Monster" },
            { "199.16", "Monster" },
            { "199.18", "Quest Item" },
            { "199.19", "Random Treasure" },
            { "199.21", "Ladder Bottom" },
            { "199.22", "Ladder Top" },
        };

        public static ObjectSet[] houseParts = new ObjectSet[]
        {
            new ObjectSet { Name = "2 Way", Ids = new string[] { "15005", "15105", "15205", "15305", "15405", "15505", "15605", "15705", "15805", "15006", "15106", "15206", "15306","15406", "15506", "15606", "15706", "15806", "15007", "15107", "15207", "15307", "15407", "15507", "15607", "15707", "15807", "44001", "44002", "44003" } },
            new ObjectSet { Name = "2 Way - Exit", Ids = new string[] { "31029", "31129", "31229", "31329", "31429", "31529", "31629", "31729", "31829", "44000" } },
            new ObjectSet { Name = "2 Way - 1 Door", Ids = new string[] { "15008", "15108", "15208", "15308", "15408", "15508", "15608", "15708", "15808", "44004" } },
            new ObjectSet { Name = "2 Way - 2 Door", Ids = new string[] { "15009", "15109", "15209", "15309", "15409", "15509", "15609", "15709", "15809", "44025" } },
            new ObjectSet { Name = "3 Way", Ids = new string[] { "31024", "31124", "31224", "31324", "31424", "31524", "31624", "31724", "31824", "44017" } },
            new ObjectSet { Name = "3 Way - Exit", Ids = new string[] { "31030", "31130", "31230", "31330", "31430", "31530", "31630", "31730", "31830", "44016" } },
            new ObjectSet { Name = "4 Way", Ids = new string[] { "31025", "31125", "31225", "31325", "31425", "31525", "31625", "31725", "31825", "44019" } },
            new ObjectSet { Name = "Corner", Ids = new string[] { "15014", "15114", "15214", "15314", "15414", "15514", "15614", "15714", "15814", "44023" } },
            new ObjectSet { Name = "Corner - 1 Door", Ids = new string[] { "31026", "31027", "31126", "31226", "31326", "31426", "31526", "31626", "31726", "31826", "31127", "31227", "31327", "31427", "31527", "31627", "31727", "31827", "44021", "44022" } },
            new ObjectSet { Name = "Corner - 2 Doors", Ids = new string[] { "31028", "31128", "31228", "31328", "31428", "31528", "31628", "31728", "31828", "44020" } },
            new ObjectSet { Name = "Corner - Diagonal", Ids = new string[] { "31031", "31131", "31231", "31331", "31431", "31531", "31631", "31731", "31831", "44024" } },
            new ObjectSet { Name = "Dead End", Ids = new string[] { "31018", "31118", "31218", "31318", "31418", "31518", "31618", "31718", "31818", "44009" } },
            new ObjectSet { Name = "Dead End - Exit", Ids = new string[] { "31019", "31119", "31219", "31319", "31419", "31519", "31619", "31719", "31819", "44006", "44007", "44008" } },
            new ObjectSet { Name = "Dead End - 1 Door", Ids = new string[] { "15012", "15112", "15212", "15312", "15412", "15512","15612", "15712", "15812", "15013", "15113", "15213", "15313", "15413", "15513", "15613", "15713", "15813", "15015", "15115", "15215", "15315", "15415", "15515", "15615", "15715", "15815", "44010", "44013", "44015" } },
            new ObjectSet { Name = "Dead End - 2 Doors", Ids = new string[] { "15010", "15110", "15210", "15310", "15410", "15510","15610", "15710", "15810", "44011", "44012", "44014" } },
            new ObjectSet { Name = "Dead End - 2 Doors - Exit", Ids = new string[] { "31020", "31120", "31220", "31320", "31420", "31520", "31620", "31720", "31820", "31021", "31121", "31221", "31321", "31421", "31521", "31621", "31721", "31821" } },
            new ObjectSet { Name = "Dead End - 3 Doors", Ids = new string[] { "15011", "15111", "15211", "15311", "15411", "15511","15611", "15711", "15811", "44005" } },
            new ObjectSet { Name = "Half Wall - House A", Ids = new string[] { "6000", "6001", "6002", "6003", "6004" } },
            new ObjectSet { Name = "Half Wall - House B", Ids = new string[] { "6100", "6101", "6102", "6103", "6104" } },
            new ObjectSet { Name = "Half Wall - House C", Ids = new string[] { "6300", "6301", "6302", "6303", "6304" } },
            new ObjectSet { Name = "Half Wall - House D", Ids = new string[] { "6400", "6401", "6402", "6403", "6404" } },
            new ObjectSet { Name = "Half Wall - House E", Ids = new string[] { "6700", "6701", "6702", "6703", "6704" } },
            new ObjectSet { Name = "Half Wall - House F", Ids = new string[] { "6800", "6801", "6802", "6803", "6804" } },
            new ObjectSet { Name = "Half Wall - Inn    ", Ids = new string[] { "6500", "6501", "6502", "6503", "6504" } },
            new ObjectSet { Name = "Half Wall - Mages G", Ids = new string[] { "6200", "6201", "6202", "6203", "6204" } },
            new ObjectSet { Name = "Half Wall - Temple ", Ids = new string[] { "6600", "6601", "6602", "6603", "6604" } },
            new ObjectSet { Name = "Stairwell", Ids = new string[] { "5002",  "5003", "5004", "5005", "5006", "5007", "31023", "5102", "5202", "5302", "5402", "5502", "5602", "5702", "5802", "5103", "5203", "5303", "5403", "5503", "5603", "5703", "5803", "5104", "5204", "5304", "5404", "5504", "5604", "5704", "5804", "5105", "5205", "5305", "5405", "5505", "5605", "5705", "5805", "5106", "5206", "5306", "5406", "5506", "5606", "5706", "5806", "5107", "5207", "5307", "5407", "5507", "5607", "5707", "5807", "31123", "31223", "31323", "31423", "31523", "31623", "31723", "31823" } },
            new ObjectSet { Name = "Stairwell - Circular", Ids = new string[] { "5000", "5100", "5200", "5300", "5400", "5500", "5600", "5700", "5800", "31022", "44026" } },
            new ObjectSet { Name = "Stairwell - Curved", Ids = new string[] { "5001", "5101", "5201", "5301", "5401", "5501", "5601", "5701", "5801" } },
            new ObjectSet { Name = "Room 1x1 - 1 Door", Ids = new string[] { "8000", "8100", "8200", "8300", "8400", "8500", "8600", "8700", "8800" } },
            new ObjectSet { Name = "Room 1x1 - 2 Doors", Ids = new string[] { "31011" } },
            new ObjectSet { Name = "Room 1x1 - 2 Doors - Exit", Ids = new string[] { "31014" } },
            new ObjectSet { Name = "Room 1x2 - 1 Door", Ids = new string[] { "8001", "8101", "8201", "8301", "8401", "8501", "8601", "8701", "8801" } },
            new ObjectSet { Name = "Room 1x3 - 1 Door", Ids = new string[] { "8002", "8102", "8202", "8302", "8402", "8502", "8602", "8702", "8802" } },
            new ObjectSet { Name = "Room 1x4 - 1 Door", Ids = new string[] { "8003", "8103", "8203", "8303", "8403", "8503", "8603", "8703", "8803" } },
            new ObjectSet { Name = "Room 1x5 - 1 Door", Ids = new string[] { "8004", "8104", "8204", "8304", "8404", "8504", "8604", "8704", "8804" } },
            new ObjectSet { Name = "Room 1x6 - 1 Door", Ids = new string[] { "8005", "8105", "8205", "8305", "8405", "8505", "8605", "8705", "8805" } },
            new ObjectSet { Name = "Room 2x1 - 1 Door", Ids = new string[] { "8006", "8106", "8206", "8306", "8406", "8506", "8606", "8706", "8806" } },
            new ObjectSet { Name = "Room 2x2 - 1 Door", Ids = new string[] { "8007", "8107", "8207", "8307", "8407", "8507", "8607", "8707", "8807" } },
            new ObjectSet { Name = "Room 2x2 - 2 Doors", Ids = new string[] { "34008" } },
            new ObjectSet { Name = "Room 2x2 - 2 Doors - Win", Ids = new string[] { "34000", "34004", "34100", "34200", "34300", "34400", "34500", "34600", "34700", "34800", "31004", "34204", "34304", "34404", "34504", "34604", "34704", "34804" } },
            new ObjectSet { Name = "Room 2x2 - 3 Doors", Ids = new string[] { "35000" } },
            new ObjectSet { Name = "Room 2x2 - 4 Doors", Ids = new string[] { "35001" } },
            new ObjectSet { Name = "Room 2x3 - 3 Doors", Ids = new string[] { "35003", "35004" } },
            new ObjectSet { Name = "Room 2x4 - 2 Doors", Ids = new string[] { "34005" } },
            new ObjectSet { Name = "Room 3x2 - 2 Doors", Ids = new string[] { "34002", "34006" } },
            new ObjectSet { Name = "Room 3x2 - 3 Doors", Ids = new string[] { "34009" } },
            new ObjectSet { Name = "Room 3x3 - 4 Doors", Ids = new string[] { "35002", "35009" } },
            new ObjectSet { Name = "Room 4x2 - 2 Doors", Ids = new string[] { "34001", "34003" } },
            new ObjectSet { Name = "Room 4x4 - 2 Doors", Ids = new string[] { "34007" } },
            new ObjectSet { Name = "Room 2x3 - 1 Door", Ids = new string[] { "8008", "8108", "8208", "8308", "8408", "8508", "8608", "8708", "8808" } },
            new ObjectSet { Name = "Room 2x4 - 1 Door", Ids = new string[] { "8009", "8109", "8209", "8309", "8409", "8509", "8609", "8709", "8809" } },
            new ObjectSet { Name = "Room 2x5 - 1 Door", Ids = new string[] { "8010", "8110", "8210", "8310", "8410", "8510", "8610", "8710", "8810" } },
            new ObjectSet { Name = "Room 2x6 - 1 Door", Ids = new string[] { "8011", "8111", "8211", "8311", "8411", "8511", "8611", "8711", "8811" } },
            new ObjectSet { Name = "Room 3x1 - 1 Door", Ids = new string[] { "8012", "8112", "8212", "8312", "8412", "8512", "8612", "8712", "8812" } },
            new ObjectSet { Name = "Room 3x2 - 1 Door", Ids = new string[] { "8013", "8113", "8213", "8313", "8413", "8513", "8613", "8713", "8813" } },
            new ObjectSet { Name = "Room 3x3 - 1 Door", Ids = new string[] { "8014", "8114", "8214", "8314", "8414", "8514", "8614", "8714", "8814" } },
            new ObjectSet { Name = "Room 3x4 - 1 Door", Ids = new string[] { "8015", "8115", "8215", "8315", "8415", "8515", "8615", "8715", "8815" } },
            new ObjectSet { Name = "Room 3x5 - 1 Door", Ids = new string[] { "8016", "8116", "8216", "8316", "8416", "8516", "8616", "8716", "8816" } },
            new ObjectSet { Name = "Room 3x6 - 1 Door", Ids = new string[] { "8017", "8117", "8217", "8317", "8417", "8517", "8617", "8717", "8817" } },
            new ObjectSet { Name = "Room 4x1 - 1 Door", Ids = new string[] { "8018", "8118", "8218", "8318", "8418", "8518", "8618", "8718", "8818" } },
            new ObjectSet { Name = "Room 4x2 - 1 Door", Ids = new string[] { "8019", "8119", "8219", "8319", "8419", "8519", "8619", "8719", "8819" } },
            new ObjectSet { Name = "Room 4x3 - 1 Door", Ids = new string[] { "8020", "8120", "8220", "8320", "8420", "8520", "8620", "8720", "8820" } },
            new ObjectSet { Name = "Room 4x4 - 1 Door", Ids = new string[] { "8021", "8121", "8221", "8321", "8421", "8521", "8621", "8721", "8821" } },
            new ObjectSet { Name = "Room 4x5 - 1 Door", Ids = new string[] { "8022", "8122", "8222", "8322", "8422", "8522", "8622", "8722", "8822" } },
            new ObjectSet { Name = "Room 4x6 - 1 Door", Ids = new string[] { "8023", "8123", "8223", "8323", "8423", "8523", "8623", "8723", "8823" } },
            new ObjectSet { Name = "Room 5x1 - 1 Door", Ids = new string[] { "8024", "8124", "8224", "8324", "8424", "8524", "8624", "8724", "8824" } },
            new ObjectSet { Name = "Room 5x2 - 1 Door", Ids = new string[] { "8025", "8125", "8225", "8325", "8425", "8525", "8625", "8725", "8825" } },
            new ObjectSet { Name = "Room 5x3 - 1 Door", Ids = new string[] { "8026", "8126", "8226", "8326", "8426", "8526", "8626", "8726", "8826" } },
            new ObjectSet { Name = "Room 5x4 - 1 Door", Ids = new string[] { "8027", "8127", "8227", "8327", "8427", "8527", "8627", "8727", "8827" } },
            new ObjectSet { Name = "Room 5x5 - 1 Door", Ids = new string[] { "8028", "8128", "8228", "8328", "8428", "8528", "8628", "8728", "8828" } },
            new ObjectSet { Name = "Room 5x6 - 1 Door", Ids = new string[] { "8029", "8129", "8229", "8329", "8429", "8529", "8629", "8729", "8829" } },
            new ObjectSet { Name = "Room 6x1 - 1 Door", Ids = new string[] { "8030", "8130", "8230", "8330", "8430", "8530", "8630", "8730", "8830" } },
            new ObjectSet { Name = "Room 6x2 - 1 Door", Ids = new string[] { "8031", "8131", "8231", "8331", "8431", "8531", "8631", "8731", "8831" } },
            new ObjectSet { Name = "Room 6x3 - 1 Door", Ids = new string[] { "8032", "8132", "8232", "8332", "8432", "8532", "8632", "8732", "8832" } },
            new ObjectSet { Name = "Room 6x4 - 1 Door", Ids = new string[] { "8033", "8133", "8233", "8333", "8433", "8533", "8633", "8733", "8833" } },
            new ObjectSet { Name = "Room 6x5 - 1 Door", Ids = new string[] { "8034", "8134", "8234", "8334", "8434", "8534", "8634", "8734", "8834" } },
            new ObjectSet { Name = "Room 6x6 - 1 Door", Ids = new string[] { "8035", "8135", "8235", "8335", "8435", "8535", "8635", "8735", "8835" } },
            new ObjectSet { Name = "Room 3x2 - Angled 1 Door", Ids = new string[] { "35005" } },
            new ObjectSet { Name = "Room 3x3 - Angled - 1 Door", Ids = new string[] { "32000" } },
            new ObjectSet { Name = "Room 5x4 - Angled - 1 Door", Ids = new string[] { "32001" } },
            new ObjectSet { Name = "Room 3x2 - Closet - 1 Door", Ids = new string[] { "32003" } },
            new ObjectSet { Name = "Room 3x2 - Closet - 2 Doors", Ids = new string[] { "34010" } },
            new ObjectSet { Name = "Room 3x3 - Closet - 1 Door", Ids = new string[] { "32002" } },
            new ObjectSet { Name = "Room 3x3 - Closet - 2 Doors", Ids = new string[] { "35012" } },
            new ObjectSet { Name = "Room 4x4 - Closet - 4 doors", Ids = new string[] { "34011" } },
            new ObjectSet { Name = "Room 3x3 - J Shape - 1 Door", Ids = new string[] { "11000", "11100", "11200", "11300", "11400", "11500", "11600", "11700", "11800" } },
            new ObjectSet { Name = "Room 4x4 - L Shape - 3 Doors", Ids = new string[] { "35008" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 1 Door", Ids = new string[] { "33001" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 3 Doors", Ids = new string[] { "35010", "35011" } },
            new ObjectSet { Name = "Room 3x2 - P Shape - 3 Doors", Ids = new string[] { "35007" } },
            new ObjectSet { Name = "Room 4x5 - Splitted - 1 Door", Ids = new string[] { "33002" } },
            new ObjectSet { Name = "Room 2x3 - Vaulted - 1 Door", Ids = new string[] { "33003" } },
            new ObjectSet { Name = "Room 3x3 - Vaulted - 1 Door", Ids = new string[] { "33004" } },
            new ObjectSet { Name = "Room 3x4 - Vaulted - 1 Door", Ids = new string[] { "33005" } },
            new ObjectSet { Name = "Room 3x5 - Vaulted - 1 Door", Ids = new string[] { "33006" } },
            new ObjectSet { Name = "Room 4x6 - Vaulted - 1 Door", Ids = new string[] { "33007" } },
            new ObjectSet { Name = "Single Floor - House A", Ids = new string[] { "1000", "1001", "1002", "1003", "1004", "1005", "1006", "1007", "1008", "1009", "1010", "1011", "1012", "1013", "1014", "1015", "1016", "1017", "1018", "1019", "1020", "1021", "1022", "1023", "1024", "1025", "1026", "1027", "1028", "1029", "1030", "1031", "1032", "1033", "1034", "1035" } },
            new ObjectSet { Name = "Single Floor - House B", Ids = new string[] { "1100", "1101", "1102", "1103", "1104", "1105", "1106", "1107", "1108", "1109", "1110", "1111", "1112", "1113", "1114", "1115", "1116", "1117", "1118", "1119", "1120", "1121", "1122", "1123", "1124", "1125", "1126", "1127", "1128", "1129", "1130", "1131", "1132", "1133", "1134", "1135" } },
            new ObjectSet { Name = "Single Floor - House C", Ids = new string[] { "1300", "1301", "1302", "1303", "1304", "1305", "1306", "1307", "1308", "1309", "1310", "1311", "1312", "1313", "1314", "1315", "1316", "1317", "1318", "1319", "1320", "1321", "1322", "1323", "1324", "1325", "1326", "1327", "1328", "1329", "1330", "1331", "1332", "1333", "1334", "1335" } },
            new ObjectSet { Name = "Single Floor - House D", Ids = new string[] { "1400", "1401", "1402", "1403", "1404", "1405", "1406", "1407", "1408", "1409", "1410", "1411", "1412", "1413", "1414", "1415", "1416", "1417", "1418", "1419", "1420", "1421", "1422", "1423", "1424", "1425", "1426", "1427", "1428", "1429", "1430", "1431", "1432", "1433", "1434", "1435" } },
            new ObjectSet { Name = "Single Floor - House E", Ids = new string[] { "1700", "1701", "1702", "1703", "1704", "1705", "1706", "1707", "1708", "1709", "1710", "1711", "1712", "1713", "1714", "1715", "1716", "1717", "1718", "1719", "1720", "1721", "1722", "1723", "1724", "1725", "1726", "1727", "1728", "1729", "1730", "1731", "1732", "1733", "1734", "1735" } },
            new ObjectSet { Name = "Single Floor - House F", Ids = new string[] { "1800", "1801", "1802", "1803", "1804", "1805", "1806", "1807", "1808", "1809", "1810", "1811", "1812", "1813", "1814", "1815", "1816", "1817", "1818", "1819", "1820", "1821", "1822", "1823", "1824", "1825", "1826", "1827", "1828", "1829", "1830", "1831", "1832", "1833", "1834", "1835" } },
            new ObjectSet { Name = "Single Floor - Inn", Ids = new string[] { "1500", "1501", "1502", "1503", "1504", "1505", "1506", "1507", "1508", "1509", "1510", "1511", "1512", "1513", "1514", "1515", "1516", "1517", "1518", "1519", "1520", "1521", "1522", "1523", "1524", "1525", "1526", "1527", "1528", "1529", "1530", "1531", "1532", "1533", "1534", "1535" } },
            new ObjectSet { Name = "Single Floor - Mages Guild", Ids = new string[] { "1200", "1201", "1202", "1203", "1204", "1205", "1206", "1207", "1208", "1209", "1210", "1211", "1212", "1213", "1214", "1215", "1216", "1217", "1218", "1219", "1220", "1221", "1222", "1223", "1224", "1225", "1226", "1227", "1228", "1229", "1230", "1231", "1232", "1233", "1234", "1235" } },
            new ObjectSet { Name = "Single Floor - Temple", Ids = new string[] { "1600", "1601", "1602", "1603", "1604", "1605", "1606", "1607", "1608", "1609", "1610", "1611", "1612", "1613", "1614", "1615", "1616", "1617", "1618", "1619", "1620", "1621", "1622", "1623", "1624", "1625", "1626", "1627", "1628", "1629", "1630", "1631", "1632", "1633", "1634", "1635" } },
            new ObjectSet { Name = "Single Ceiling - House A", Ids = new string[] { "2000", "2001", "2002", "2003", "2004", "2005", "2006", "2007", "2008", "2009", "2010", "2011", "2012", "2013", "2014", "2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022", "2023", "2024", "2025", "2026", "2027", "2028", "2029", "2030", "2031", "2032", "2033", "2034", "2035" } },
            new ObjectSet { Name = "Single Ceiling - House B", Ids = new string[] { "2100", "2101", "2102", "2103", "2104", "2105", "2106", "2107", "2108", "2109", "2110", "2111", "2112", "2113", "2114", "2115", "2116", "2117", "2118", "2119", "2120", "2121", "2122", "2123", "2124", "2125", "2126", "2127", "2128", "2129", "2130", "2131", "2132", "2133", "2134", "2135" } },
            new ObjectSet { Name = "Single Ceiling - House C", Ids = new string[] { "2300", "2301", "2302", "2303", "2304", "2305", "2306", "2307", "2308", "2309", "2310", "2311", "2312", "2313", "2314", "2315", "2316", "2317", "2318", "2319", "2320", "2321", "2322", "2323", "2324", "2325", "2326", "2327", "2328", "2329", "2330", "2331", "2332", "2333", "2334", "2335" } },
            new ObjectSet { Name = "Single Ceiling - House D", Ids = new string[] { "2400", "2401", "2402", "2403", "2404", "2405", "2406", "2407", "2408", "2409", "2410", "2411", "2412", "2413", "2414", "2415", "2416", "2417", "2418", "2419", "2420", "2421", "2422", "2423", "2424", "2425", "2426", "2427", "2428", "2429", "2430", "2431", "2432", "2433", "2434", "2435" } },
            new ObjectSet { Name = "Single Ceiling - House E", Ids = new string[] { "2700", "2701", "2702", "2703", "2704", "2705", "2706", "2707", "2708", "2709", "2710", "2711", "2712", "2713", "2714", "2715", "2716", "2717", "2718", "2719", "2720", "2721", "2722", "2723", "2724", "2725", "2726", "2727", "2728", "2729", "2730", "2731", "2732", "2733", "2734", "2735" } },
            new ObjectSet { Name = "Single Ceiling - House F", Ids = new string[] { "2800", "2801", "2802", "2803", "2804", "2805", "2806", "2807", "2808", "2809", "2810", "2811", "2812", "2813", "2814", "2815", "2816", "2817", "2818", "2819", "2820", "2821", "2822", "2823", "2824", "2825", "2826", "2827", "2828", "2829", "2830", "2831", "2832", "2833", "2834", "2835" } },
            new ObjectSet { Name = "Single Ceiling - Inn", Ids = new string[] { "2500", "2501", "2502", "2503", "2504", "2505", "2506", "2507", "2508", "2509", "2510", "2511", "2512", "2513", "2514", "2515", "2516", "2517", "2518", "2519", "2520", "2521", "2522", "2523", "2524", "2525", "2526", "2527", "2528", "2529", "2530", "2531", "2532", "2533", "2534", "2535" } },
            new ObjectSet { Name = "Single Ceiling - Mages Guild", Ids = new string[] { "2200", "2201", "2202", "2203", "2204", "2205", "2206", "2207", "2208", "2209", "2210", "2211", "2212", "2213", "2214", "2215", "2216", "2217", "2218", "2219", "2220", "2221", "2222", "2223", "2224", "2225", "2226", "2227", "2228", "2229", "2230", "2231", "2232", "2233", "2234", "2235" } },
            new ObjectSet { Name = "Single Ceiling - Temple", Ids = new string[] { "2600", "2601", "2602", "2603", "2604", "2605", "2606", "2607", "2608", "2609", "2610", "2611", "2612", "2613", "2614", "2615", "2616", "2617", "2618", "2619", "2620", "2621", "2622", "2623", "2624", "2625", "2626", "2627", "2628", "2629", "2630", "2631", "2632", "2633", "2634", "2635" } },
            new ObjectSet { Name = "Single Doorway - House A", Ids = new string[] { "3000", "3001" } },
            new ObjectSet { Name = "Single Doorway - House B", Ids = new string[] { "3100", "3101" } },
            new ObjectSet { Name = "Single Doorway - House C", Ids = new string[] { "3300", "3301" } },
            new ObjectSet { Name = "Single Doorway - House D", Ids = new string[] { "3400", "3401" } },
            new ObjectSet { Name = "Single Doorway - House E", Ids = new string[] { "3700", "3701" } },
            new ObjectSet { Name = "Single Doorway - House F", Ids = new string[] { "3800", "3801" } },
            new ObjectSet { Name = "Single Doorway - Inn", Ids = new string[] { "3500", "3501" } },
            new ObjectSet { Name = "Single Doorway - Mages Guild", Ids = new string[] { "3200", "3201" } },
            new ObjectSet { Name = "Single Doorway - Temple", Ids = new string[] { "3600", "3601" } },
            new ObjectSet { Name = "Single Exit - House A", Ids = new string[] { "3002", "3003" } },
            new ObjectSet { Name = "Single Exit - House B", Ids = new string[] { "3102", "3103" } },
            new ObjectSet { Name = "Single Exit - House C", Ids = new string[] { "3702", "3703" } },
            new ObjectSet { Name = "Single Exit - House D", Ids = new string[] { "3302", "3303" } },
            new ObjectSet { Name = "Single Exit - House E", Ids = new string[] { "3402", "3403" } },
            new ObjectSet { Name = "Single Exit - House F", Ids = new string[] { "3802", "3803" } },
            new ObjectSet { Name = "Single Exit - Inn", Ids = new string[] { "3502", "3503" } },
            new ObjectSet { Name = "Single Exit - Mages Guild", Ids = new string[] { "3202", "3203" } },
            new ObjectSet { Name = "Single Exit - Temple", Ids = new string[] { "3602", "3603" } },
            new ObjectSet { Name = "Single Wall - House A", Ids = new string[] { "3004", "3005", "3010", "3031", "3032", "3033", "3034", "3035", "3036", "3037", "3038", "3039", "3040", "3041" } },
            new ObjectSet { Name = "Single Wall - House B", Ids = new string[] { "3104", "3105", "3110", "3131", "3132", "3133", "3134", "3135", "3136", "3137", "3138", "3139", "3140", "3141" } },
            new ObjectSet { Name = "Single Wall - House C", Ids = new string[] { "3304", "3305", "3310", "3331", "3332", "3333", "3334", "3335", "3336", "3337", "3338", "3339", "3340", "3341" } },
            new ObjectSet { Name = "Single Wall - House D", Ids = new string[] { "3404", "3405", "3410", "3431", "3432", "3433", "3434", "3435", "3436", "3437", "3438", "3439", "3440", "3441" } },
            new ObjectSet { Name = "Single Wall - House E", Ids = new string[] { "3704", "3705", "3710", "3731", "3732", "3733", "3734", "3735", "3736", "3737", "3738", "3739", "3740", "3741" } },
            new ObjectSet { Name = "Single Wall - House F", Ids = new string[] { "3804", "3805", "3810", "3831", "3832", "3833", "3834", "3835", "3836", "3837", "3838", "3839", "3840", "3841" } },
            new ObjectSet { Name = "Single Wall - Inn", Ids = new string[] { "3504", "3505", "3510", "3531", "3532", "3533", "3534", "3535", "3536", "3537", "3538", "3539", "3540", "3541" } },
            new ObjectSet { Name = "Single Wall - Mages Guild", Ids = new string[] { "3204", "3205", "3210", "3231", "3232", "3233", "3234", "3235", "3236", "3237", "3238", "3239", "3240", "3241" } },
            new ObjectSet { Name = "Single Wall - Temple", Ids = new string[] { "3604", "3605", "3610", "3631", "3632", "3633", "3634", "3635", "3636", "3637", "3638", "3639", "3640", "3641" } },
            new ObjectSet { Name = "Single Wall - Win - House A", Ids = new string[] { "3006", "3007", "3008", "3009", "3011", "3012", "3013", "3014", "3015", "3016", "3017", "3018", "3019", "3020", "3021", "3022", "3023", "3024", "3025", "3026", "3027", "3028", "3029", "3030" } },
            new ObjectSet { Name = "Single Wall - Win - House B", Ids = new string[] { "3106", "3107", "3108", "3109", "3111", "3112", "3113", "3114", "3115", "3116", "3117", "3118", "3119", "3120", "3121", "3122", "3123", "3124", "3125", "3126", "3127", "3128", "3129", "3130" } },
            new ObjectSet { Name = "Single Wall - Win - House C", Ids = new string[] { "3306", "3307", "3308", "3309", "3311", "3312", "3313", "3314", "3315", "3316", "3317", "3318", "3319", "3320", "3321", "3322", "3323", "3324", "3325", "3326", "3327", "3328", "3329", "3330" } },
            new ObjectSet { Name = "Single Wall - Win - House D", Ids = new string[] { "3406", "3407", "3408", "3409", "3411", "3412", "3413", "3414", "3415", "3416", "3417", "3418", "3419", "3420", "3421", "3422", "3423", "3424", "3425", "3426", "3427", "3428", "3429", "3430" } },
            new ObjectSet { Name = "Single Wall - Win - House E", Ids = new string[] { "3706", "3707", "3708", "3709", "3711", "3712", "3713", "3714", "3715", "3716", "3717", "3718", "3719", "3720", "3721", "3722", "3723", "3724", "3725", "3726", "3727", "3728", "3729", "3730" } },
            new ObjectSet { Name = "Single Wall - Win - House F", Ids = new string[] { "3806", "3807", "3808", "3809", "3811", "3812", "3813", "3814", "3815", "3816", "3817", "3818", "3819", "3820", "3821", "3822", "3823", "3824", "3825", "3826", "3827", "3828", "3829", "3830" } },
            new ObjectSet { Name = "Single Wall - Win - Inn", Ids = new string[] { "3506", "3507", "3508", "3509", "3511", "3512", "3513", "3514", "3515", "3516", "3517", "3518", "3519", "3520", "3521", "3522", "3523", "3524", "3525", "3526", "3527", "3528", "3529", "3530" } },
            new ObjectSet { Name = "Single Wall - Win - Mages Guild", Ids = new string[] { "3206", "3207", "3208", "3209", "3211", "3212", "3213", "3214", "3215", "3216", "3217", "3218", "3219", "3220", "3221", "3222", "3223", "3224", "3225", "3226", "3227", "3228", "3229", "3230" } },
            new ObjectSet { Name = "Single Wall - Win - Temple", Ids = new string[] { "3606", "3607", "3608", "3609", "3611", "3612", "3613", "3614", "3615", "3616", "3617", "3618", "3619", "3620", "3621", "3622", "3623", "3624", "3625", "3626", "3627", "3628", "3629", "3630" } },
            new ObjectSet { Name = "Single Pillar - House A", Ids = new string[] { "4000", "4001", "4002", "4003", "4004", "4005", "4006", "4007" } },
            new ObjectSet { Name = "Single Pillar - House B", Ids = new string[] { "4100", "4101", "4102", "4103", "4104", "4105", "4106", "4107" } },
            new ObjectSet { Name = "Single Pillar - House C", Ids = new string[] { "4300", "4301", "4302", "4303", "4304", "4305", "4306", "4307" } },
            new ObjectSet { Name = "Single Pillar - House D", Ids = new string[] { "4400", "4401", "4402", "4403", "4404", "4405", "4406", "4407" } },
            new ObjectSet { Name = "Single Pillar - House E", Ids = new string[] { "4700", "4701", "4702", "4703", "4704", "4705", "4706", "4707" } },
            new ObjectSet { Name = "Single Pillar - House F", Ids = new string[] { "4800", "4801", "4802", "4803", "4804", "4805", "4806", "4807" } },
            new ObjectSet { Name = "Single Pillar - Inn", Ids = new string[] { "4500", "4501", "4502", "4503", "4504", "4505", "4506", "4507" } },
            new ObjectSet { Name = "Single Pillar - Mages Guild", Ids = new string[] { "4200", "4201", "4202", "4203", "4204", "4205", "4206", "4207" } },
            new ObjectSet { Name = "Single Pillar - Temple", Ids = new string[] { "4600", "4601", "4602", "4603", "4604", "4605", "4606", "4607" } },
        };

        public static ObjectSet[] dungeonPartsRooms = new ObjectSet[]
        {
            new ObjectSet { Name = "Large Hall", Ids = new string[] { "58009", "58050" } },
            new ObjectSet { Name = "Large Hall - Corner", Ids = new string[] { "58022" } },
            new ObjectSet { Name = "Large Hall - Corner - 1 Door", Ids = new string[] { "58048", "58049" } },
            new ObjectSet { Name = "Large Hall - Corner - Floor - 1 Door", Ids = new string[] { "58029" } },
            new ObjectSet { Name = "Large Hall - Corner - Floor - 4 Doors", Ids = new string[] { "58031" } },
            new ObjectSet { Name = "Large Hall - Corner - Floor - 5 Doors", Ids = new string[] { "58053" } },
            new ObjectSet { Name = "Lrg Hall - Corner - Flr. - Pltfrm - 1 Chute", Ids = new string[] { "58030", "58028" } },
            new ObjectSet { Name = "Large Hall - Corner - Ledge Gaps - 1 Door", Ids = new string[] { "58021" } },
            new ObjectSet { Name = "Large Hall - Corner - Ledge Gaps", Ids = new string[] { "58020", "58023" } },
            new ObjectSet { Name = "Large Hall - Corner - Walls/Ceiling", Ids = new string[] { "58024" } },
            new ObjectSet { Name = "Lrg Hall - Corner - Walls/Ceiling - Chute", Ids = new string[] { "58026", "58025", "58027" } },
            new ObjectSet { Name = "Large Hall - Diagonal Wall - Platform", Ids = new string[] { "58052" } },
            new ObjectSet { Name = "Large Hall - External Corridor", Ids = new string[] { "58054" } },
            new ObjectSet { Name = "Ledge - Arched - 2 Way", Ids = new string[] { "61304", "61316", "61317", "61404", "61416", "61417", "61504", "61516", "61517" } },
            new ObjectSet { Name = "Ledge - Arched - 2 Way - 1 Door", Ids = new string[] { "61308", "61309", "61310", "61324", "61408", "61409", "61410", "61424", "61508", "61509", "61510", "61524" } },
            new ObjectSet { Name = "Ledge - Arched - 2 Way - 2 Doors", Ids = new string[] { "61305", "61306", "61307", "61320", "61405", "61406", "61407", "61420", "61505", "61506", "61507", "61520" } },
            new ObjectSet { Name = "Ledge - Arched - Corner", Ids = new string[] { "61303", "61311", "61312", "61321", "61323", "61403", "61411", "61412", "61421", "61423", "61503", "61511", "61512", "61521", "61523" } },
            new ObjectSet { Name = "Ledge - Arched - Corner - Inner", Ids = new string[] { "61313", "61314", "61315", "61322", "61413", "61414", "61415", "61422", "61513", "61514", "61515", "61522" } },
            new ObjectSet { Name = "Ledge - Arched - Dead End - 1 Door", Ids = new string[] { "61318", "61319", "61418", "61419", "61518", "61519" } },
            new ObjectSet { Name = "Ledge - Square - 2 Way", Ids = new string[] { "67303", "67317", "67319", "67320", "67331" } },
            new ObjectSet { Name = "Ledge - Square - 2 Way - 1 Door", Ids = new string[] { "67304", "67305", "67306", "67318", "67323" } },
            new ObjectSet { Name = "Ledge - Square - 2 Way - 2 Doors", Ids = new string[] { "67307", "67308", "67309", "67324" } },
            new ObjectSet { Name = "Ledge - Square - Corner", Ids = new string[] { "67311", "67312", "67313", "67321" } },
            new ObjectSet { Name = "Ledge - Square - Corner - Inner", Ids = new string[] { "67314", "67315", "67316", "67322" } },
            new ObjectSet { Name = "Ledge - Square - Dead End", Ids = new string[] { "67332", "67333", "67334", "67336", "67337" } },
            new ObjectSet { Name = "Ledge - Square - Dead End - 1 Door", Ids = new string[] { "67310" } },
            new ObjectSet { Name = "Pyramid Room", Ids = new string[] { "58013", "58014", "58015", "58016" } },
            new ObjectSet { Name = "Room 1x1 - 1 Door", Ids = new string[] { "67033", "67133", "67233", "70000", "70100", "70036", "70136" } },
            new ObjectSet { Name = "Room 1x1 - 2 Doors", Ids = new string[] { "60502", "60602", "60702" } },
            new ObjectSet { Name = "Room 1x2 - 1 Door", Ids = new string[] { "70001", "70037", "70101", "70137" } },
            new ObjectSet { Name = "Room 1x3 - 1 Door", Ids = new string[] { "70038", "70102", "70138" } },
            new ObjectSet { Name = "Room 1x4 - 1 Door", Ids = new string[] { "70039", "70103", "70139", "70003" } },
            new ObjectSet { Name = "Room 1x5 - 1 Door", Ids = new string[] { "70004", "70040", "70104", "70140" } },
            new ObjectSet { Name = "Room 1x6 - 1 Door", Ids = new string[] { "70005", "70041", "70105", "70141" } },
            new ObjectSet { Name = "Room 2x1 - 1 Door", Ids = new string[] { "70006", "70106", "70042", "70142" } },
            new ObjectSet { Name = "Room 2x2 - 1 Door", Ids = new string[] { "62008", "62108", "62208", "64300", "64301", "64302", "64303", "68000", "68100", "68200", "70007", "70043", "70107", "70143" } },
            new ObjectSet { Name = "Room 2x2 - 2 Doors", Ids = new string[] { "58038", "62028", "62128", "62228", "64305", "71101", "71106", "71201", "71206", "71301", "71306" } },
            new ObjectSet { Name = "Room 2x2 - 3 Doors", Ids = new string[] { "71400", "71402", "71500", "71502", "71600" } },
            new ObjectSet { Name = "Room 2x3 - 1 Door", Ids = new string[] { "70008", "70108", "70144", "70044" } },
            new ObjectSet { Name = "Room 2x2 - 4 Doors", Ids = new string[] { "71403", "71503", "71603", } },
            new ObjectSet { Name = "Room 2x4 - 1 Door", Ids = new string[] { "70009", "70045", "70109", "70145", } },
            new ObjectSet { Name = "Room 2x4 - 2 Doors", Ids = new string[] { "71107", "71207", "71307" } },
            new ObjectSet { Name = "Room 2x5 - 1 Door", Ids = new string[] { "70010", "70046", "70110", "70146" } },
            new ObjectSet { Name = "Room 2x6 - 1 Door", Ids = new string[] { "70011", "70047", "70111", "70147" } },
            new ObjectSet { Name = "Room 3x1 - 1 Door", Ids = new string[] { "70012", "70048", "70112", "70148" } },
            new ObjectSet { Name = "Room 3x2 - 1 Door", Ids = new string[] { "70113", "70013", "70049", "70149" } },
            new ObjectSet { Name = "Room 3x2 - 3 Doors", Ids = new string[] { "71405", "71406", "71505", "71506", "71605", "71606", "71401", "71501", "71601" } },
            new ObjectSet { Name = "Room 3x2 - 2 Doors", Ids = new string[] { "71104", "71108", "71204", "71208", "71304", "71308" } },
            new ObjectSet { Name = "Room 3x3 - 1 Doors", Ids = new string[] { "70014", "70114", "70050", "70150" } },
            new ObjectSet { Name = "Room 3x3 - 2 Doors", Ids = new string[] { "71710", "71810", "71910" } },
            new ObjectSet { Name = "Room 3x3 - 4 Doors", Ids = new string[] { "71404", "71504", "71604", "71705", "71805", "71905" } },
            new ObjectSet { Name = "Room 3x4 - 1 Door", Ids = new string[] { "70015", "70051", "70115", "70151" } },
            new ObjectSet { Name = "Room 3x5 - 4 Doors", Ids = new string[] { "70016", "70052", "70116", "70152" } },
            new ObjectSet { Name = "Room 3x6 - 1 Door", Ids = new string[] { "70017", "70053", "70117", "70153" } },
            new ObjectSet { Name = "Room 4x1 - 1 Door", Ids = new string[] { "70018", "70054", "70118", "70154" } },
            new ObjectSet { Name = "Room 4x2 - 1 Door", Ids = new string[] { "62009", "62109", "62209", "70019", "70055", "70119", "70155", "71100", "71200", "71300" } },
            new ObjectSet { Name = "Room 4x2 - 2 Doors", Ids = new string[] { "71102", "71202", "71302", "71105", "71205", "71305", "71701", "71801", "71901" } },
            new ObjectSet { Name = "Room 4x3 - 1 Door", Ids = new string[] { "70020", "70056", "70120", "70156" } },
            new ObjectSet { Name = "Room 4x4 - 1 Door", Ids = new string[] { "62010", "62110", "62210", "68001", "68101", "72010", "68201", "70021", "70057", "70121", "70157" } },
            new ObjectSet { Name = "Room 4x4 - 2 Doors", Ids = new string[] { "72000" } },
            new ObjectSet { Name = "Room 4x4 - 3 Doors", Ids = new string[] { "71109", "71209", "71309", "72016" } },
            new ObjectSet { Name = "Room 4x4 - 8 Doors", Ids = new string[] { "58007" } },
            new ObjectSet { Name = "Room 4x5 - 1 Door", Ids = new string[] { "70022", "70058", "70122", "70158" } },
            new ObjectSet { Name = "Room 4x6 - 1 Door", Ids = new string[] { "70023", "70059", "70123", "70159", "72011" } },
            new ObjectSet { Name = "Room 4x6 - 2 Doors", Ids = new string[] { "58039", "72001", "72002" } },
            new ObjectSet { Name = "Room 4x6 - 3 Doors", Ids = new string[] { "72006", "72007", "72008" } },
            new ObjectSet { Name = "Room 4x10 - 1 Door", Ids = new string[] { "62011", "62111", "62211" } },
            new ObjectSet { Name = "Room 5x1 - 1 Door", Ids = new string[] { "70024", "70060", "70124", "70160" } },
            new ObjectSet { Name = "Room 5x2 - 1 Door", Ids = new string[] { "70025", "70061", "70125", "70161" } },
            new ObjectSet { Name = "Room 5x3 - 1 Door", Ids = new string[] { "70026", "70062", "70126", "70162" } },
            new ObjectSet { Name = "Room 5x4 - 1 Door", Ids = new string[] { "70027", "70063", "70127", "70163" } },
            new ObjectSet { Name = "Room 5x5 - 1 Door", Ids = new string[] { "70028", "70064", "70128", "70164" } },
            new ObjectSet { Name = "Room 5x6 - 1 Door", Ids = new string[] { "70029", "70065", "70129", "70165" } },
            new ObjectSet { Name = "Room 6x1 - 1 Door", Ids = new string[] { "70030", "70066", "70130", "70166" } },
            new ObjectSet { Name = "Room 6x2 - 1 Door", Ids = new string[] { "70031", "70067", "70131", "70167" } },
            new ObjectSet { Name = "Room 6x3 - 1 Door", Ids = new string[] { "70032", "70068", "70132", "70168" } },
            new ObjectSet { Name = "Room 6x4 - 1 Door", Ids = new string[] { "70033", "70069", "70133", "70169", "72012" } },
            new ObjectSet { Name = "Room 6x5 - 1 Door", Ids = new string[] { "70034", "70070", "70134", "70170" } },
            new ObjectSet { Name = "Room 6x6 - 1 Door", Ids = new string[] { "70035", "70071", "70135", "70171", "72009" } },
            new ObjectSet { Name = "Room 6x6 - 2 Doors", Ids = new string[] { "58037", "72003", "72004" } },
            new ObjectSet { Name = "Room 6x6 - 3 Doors", Ids = new string[] { "72005" } },
            new ObjectSet { Name = "Room 2x4 - 1 Door Arched", Ids = new string[] { "62306", "62406", "62506" } },
            new ObjectSet { Name = "Room 3x5 - 1 Door Arched", Ids = new string[] { "64304" } },
            new ObjectSet { Name = "Room 4x2 - 1 Door Arched", Ids = new string[] { "62305", "62405", "62505" } },
            new ObjectSet { Name = "Room 4x4 - 1 Door Arched", Ids = new string[] { "62307", "62407", "62507" } },
            new ObjectSet { Name = "Room 4x4 - 2 Doors Arched", Ids = new string[] { "62308", "62309", "62408", "62409", "62508", "62509" } },
            new ObjectSet { Name = "Room 4x4 - Ledge Gap - 1 Door Arched", Ids = new string[] { "62029", "62129", "62229" } },
            new ObjectSet { Name = "Room 6x4 - 3 Doors Arched", Ids = new string[] { "62005", "62105", "62205" } },
            new ObjectSet { Name = "Room 6x6 - 2 Doors Arched", Ids = new string[] { "62003", "62006", "62103", "62106", "62203", "62206" } },
            new ObjectSet { Name = "Room 6x6 - 4 Doors Arched", Ids = new string[] { "62301", "62401", "62501" } },
            new ObjectSet { Name = "Room 10x10 - 2 Doors Arched", Ids = new string[] { "62004", "62104", "62204" } },
            new ObjectSet { Name = "Room 2x3 - Angled - 1 Door", Ids = new string[] { "70506", "70706", "70606" } },
            new ObjectSet { Name = "Room 3x2 - Angled - 1 Door", Ids = new string[] { "71408", "71508", "71608" } },
            new ObjectSet { Name = "Room 3x3 - Angled - 1 Door", Ids = new string[] { "70608", "70708", "70507", "70508", "70607", "70707" } },
            new ObjectSet { Name = "Room 4x3 - Angled - 1 Door", Ids = new string[] { "70502", "70602", "70702" } },
            new ObjectSet { Name = "Room 6x10 - Angled - 1 Door", Ids = new string[] { "58006" } },
            new ObjectSet { Name = "Room 6x6 - Bridge - 4 Doors Arched", Ids = new string[] { "62302", "62402", "62502" } },
            new ObjectSet { Name = "Room 1x1 - Chute Ceiling - 1 Door", Ids = new string[] { "67326", "67426", "67526" } },
            new ObjectSet { Name = "Room 3x2 - Chute Ceiling - 2 Doors", Ids = new string[] { "72013" } },
            new ObjectSet { Name = "Room 3x2 - Chute Floor - 2 Doors", Ids = new string[] { "72015" } },
            new ObjectSet { Name = "Room 3x2 - Chute Floor/Celing - 1 Door", Ids = new string[] { "72014" } },
            new ObjectSet { Name = "Room 3x3 - Chute Floor - 3 Doors", Ids = new string[] { "72017" } },
            new ObjectSet { Name = "Room 4x3 - Chute Ceiling - 2 Doors", Ids = new string[] { "72018" } },
            new ObjectSet { Name = "Room 1x1 - Closet - 2 Doors", Ids = new string[] { "71409", "71509", "71609", "71700", "71800", "71900" } },
            new ObjectSet { Name = "Room 2x2 - Closet - 1 Door", Ids = new string[] { "71407", "71507", "71607", "71708", "71808", "71908", } },
            new ObjectSet { Name = "Room 3x2 - Closet - 1 Door", Ids = new string[] { "70504", "70604", "70704" } },
            new ObjectSet { Name = "Room 3x2 - Closet - 2 Doors", Ids = new string[] { "71103", "71203", "71303" } },
            new ObjectSet { Name = "Room 3x3 - Closet - 1 Door", Ids = new string[] { "70503", "70703", "70603" } },
            new ObjectSet { Name = "Room 3x3 - Closet - 2 Doors", Ids = new string[] { "71709", "71809", "71909" } },
            new ObjectSet { Name = "Room 1x1 - Closets - 3 Doors", Ids = new string[] { "71702", "71802", "71902" } },
            new ObjectSet { Name = "Room 3x3 - Closets - 1 Door", Ids = new string[] { "70505", "70605", "70705" } },
            new ObjectSet { Name = "Room 2x1 - J Shape - 1 Door", Ids = new string[] { "70501", "70601", "70701" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 1 Door", Ids = new string[] { "71012" } },
            new ObjectSet { Name = "Room 3x2 - B Shape - 1 Door", Ids = new string[] { "70509", "70609", "70709" } },
            new ObjectSet { Name = "Room 3x2 - P Shape - 3 Doors", Ids = new string[] { "71703", "71803", "71903" } },
            new ObjectSet { Name = "Room 3x3 - L Shape - 3 Doors", Ids = new string[] { "71704", "71804", "71904" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 1 Door", Ids = new string[] { "70800", "71000", "70900" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 2 Doors Arched", Ids = new string[] { "62007", "62107", "62207" } },
            new ObjectSet { Name = "Room 3x3 - M Shape - 3 Doors", Ids = new string[] { "71706", "71707", "71806", "71807", "71906", "71907" } },
            new ObjectSet { Name = "Room 4x2 - B Shape - 1 Door", Ids = new string[] { "70500", "70600", "70700" } },
            new ObjectSet { Name = "Room 4x5 - Splitted - 1 Door", Ids = new string[] { "70901", "71001", "70801" } },
            new ObjectSet { Name = "Room 4x5 - Splitted - 1 Door", Ids = new string[] { "71010" } },
            new ObjectSet { Name = "Room 10x8 - 2 Stories - Ledge - 4 Doors", Ids = new string[] { "58010" } },
            new ObjectSet { Name = "Room 10x8 - 2 Stories - Ramp - 2 Doors", Ids = new string[] { "58011" } },
            new ObjectSet { Name = "Room 2x3 - Vaulted - 1 Door", Ids = new string[] { "70802", "70806", "71002", "70902", "70906", "71006" } },
            new ObjectSet { Name = "Room 2x4 - Vaulted - 1 Door", Ids = new string[] { "71011" } },
            new ObjectSet { Name = "Room 2x4 - Vaulted - 2 Doors", Ids = new string[] { "70807", "70907", "71007" } },
            new ObjectSet { Name = "Room 3x3 - Vaulted - 1 Door", Ids = new string[] { "70803", "70903", "71003" } },
            new ObjectSet { Name = "Room 3x4 - Vaulted - 1 Door", Ids = new string[] { "70804", "70904", "71004" } },
            new ObjectSet { Name = "Room 3x5 - Vaulted - 1 Door", Ids = new string[] { "70805", "70905", "71005" } },
            new ObjectSet { Name = "Room 3x5 - Vaulted - 2 Door", Ids = new string[] { "70808", "70908", "71008" } },
            new ObjectSet { Name = "Room 4x5 - Vaulted - 2 Doors", Ids = new string[] { "70809", "70909", "71009" } },
};

        public static ObjectSet[] dungeonPartsCorridors = new ObjectSet[]
        {
            new ObjectSet { Name = "Arched - 2 Way", Ids = new string[] { "61003", "61103", "61203", "61008", "61108", "61208", "61006", "61106", "61206", "61007", "61107", "61207" } },
            new ObjectSet { Name = "Arched - 2 Way - 1 Door", Ids = new string[] { "61011", "61111", "61211" } },
            new ObjectSet { Name = "Arched - 2 Way - 2 Doors", Ids = new string[] { "61009", "61010", "61109", "61110", "61209", "61210" } },
            new ObjectSet { Name = "Arched - 2 Way - Chute - Floor", Ids = new string[] { "61016", "61116", "61216", "61302", "61402", "61502" } },
            new ObjectSet { Name = "Arched - 2 Way - Window", Ids = new string[] { "61029", "61129", "61229" } },
            new ObjectSet { Name = "Arched - 2 Way - Wooden Beams", Ids = new string[] { "60509", "60609", "60709" } },
            new ObjectSet { Name = "Arched - 3 Way", Ids = new string[] { "61012", "61112", "61212" } },
            new ObjectSet { Name = "Arched - 3 Way - 1 Door", Ids = new string[] { "61000", "61100", "61200" } },
            new ObjectSet { Name = "Arched - 4 Way", Ids = new string[] { "61001", "61101", "61201" } },
            new ObjectSet { Name = "Arched - Corner - 1 Door", Ids = new string[] { "61023", "61024", "61123", "61124", "61223", "61224" } },
            new ObjectSet { Name = "Arched - Corner - Chute - Floor", Ids = new string[] { "61300", "61400", "61500" } },
            new ObjectSet { Name = "Arched - Corner - Chute - Ceiling/Floor", Ids = new string[] { "60501", "60601", "60701" } },
            new ObjectSet { Name = "Arched - Corner - Porticullis", Ids = new string[] { "60504", "60604", "60704" } },
            new ObjectSet { Name = "Arched - Corner - Porticullis - 1 Door", Ids = new string[] { "60505", "60605", "60705" } },
            new ObjectSet { Name = "Arched - Corner", Ids = new string[] { "61002", "61102", "61202", "61004", "61104", "61204", "61005", "61105", "61205" } },
            new ObjectSet { Name = "Arched - Dead End", Ids = new string[] { "61013", "61113", "61213" } },
            new ObjectSet { Name = "Arched - Dead End - 1 Door", Ids = new string[] { "61014", "61019", "61020", "61114", "61119", "61120", "61219", "61220" } },
            new ObjectSet { Name = "Arched - Dead End - 2 Doors", Ids = new string[] { "61021", "61022", "61121", "61122", "61221", "61222" } },
            new ObjectSet { Name = "Arched - Dead End - Chute - Floor", Ids = new string[] { "61301", "61401", "61501" } },
            new ObjectSet { Name = "Arched - Junction 2x2 - 2 Way", Ids = new string[] { "62303", "62403", "62503" } },
            new ObjectSet { Name = "Arched - Junction 2x2 - 3 Way", Ids = new string[] { "62300", "62400", "62500" } },
            new ObjectSet { Name = "Arched - Junction 2x2 - 4 Way", Ids = new string[] { "62000", "62100", "62200" } },
            new ObjectSet { Name = "Arched - Junction 2x2 - Dead End", Ids = new string[] { "62304", "62404", "62504" } },
            new ObjectSet { Name = "Arched - Junction 2x4 - 4 Way", Ids = new string[] { "62001", "62101", "62201" } },
            new ObjectSet { Name = "Arched - Junction 2x6 - 4 Way", Ids = new string[] { "62002", "62102", "62202" } },
            new ObjectSet { Name = "Arched - Ramp", Ids = new string[] { "60500", "60600", "60700", "61015", "61031", "61115", "61131", "61215", "61231" } },
            new ObjectSet { Name = "Arched - Slope", Ids = new string[] { "61030", "61130", "61230" } },
            new ObjectSet { Name = "Arched - Stairs", Ids = new string[] { "61017", "61018", "61117", "61118", "61217", "61218" } },
            new ObjectSet { Name = "Arched - Stairs - Ledge", Ids = new string[] { "59011", "59013" } },
            new ObjectSet { Name = "Hexagon - 2 Way", Ids = new string[] { "63000", "63024", "63100", "63124", "63200", "63224", "63001", "63101", "63201", "63002", "63102", "63202" } },
            new ObjectSet { Name = "Hexagon - 2 Way - 1 Door", Ids = new string[] { "63008", "63009", "63010", "63011", "63030", "63037", "63108", "63109", "63110", "63111", "63130", "63137", "63208", "63209", "63210", "63211", "63230", "63237" } },
            new ObjectSet { Name = "Hexagon - 2 Way - 2 Doors", Ids = new string[] { "63012", "63013", "63018", "63019", "63020", "63021", "63038", "63112", "63113", "63118", "63119", "63120", "63121", "63138", "63212", "63213", "63218", "63219", "63220", "63221", "63238" } },
            new ObjectSet { Name = "Hexagon - 2 Way - 3 Doors", Ids = new string[] { "63014", "63015", "63016", "63017", "63114", "63115", "63116", "63117", "63214", "63215", "63216", "63217" } },
            new ObjectSet { Name = "Hexagon - 2 Way - 4 Doors", Ids = new string[] { "63025", "63125", "63225" } },
            new ObjectSet { Name = "Hexagon - 2 Way - Beams", Ids = new string[] { "63022", "63023", "63059", "63122", "63123", "63159", "63222", "63223", "63259" } },
            new ObjectSet { Name = "Hexagon - 2 Way - Diagonal", Ids = new string[] { "63035", "63036", "63135", "63136", "63235", "63236" } },
            new ObjectSet { Name = "Hexagon - 2 Way - Diagonal - 1 Door", Ids = new string[] { "63056", "63156", "63256" } },
            new ObjectSet { Name = "Hexagon - 2 Way - 2 Corrs. - Niches", Ids = new string[] { "63050", "63150", "63250" } },
            new ObjectSet { Name = "Hexagon - 2 Way - Niches", Ids = new string[] { "63049", "63149", "63249" } },
            new ObjectSet { Name = "Hexagon - 2 Way - S", Ids = new string[] { "63039", "63139", "63239" } },
            new ObjectSet { Name = "Hexagon - 2 Way - Window", Ids = new string[] { "63053", "63153", "63253", "63054", "63154", "63254", "63055", "63155", "63255" } },
            new ObjectSet { Name = "Hexagon - 3 Way", Ids = new string[] { "63028", "63128", "63228" } },
            new ObjectSet { Name = "Hexagon - 3 Way - 1 Door", Ids = new string[] { "63029", "63129", "63229" } },
            new ObjectSet { Name = "Hexagon - 3 Way - Diagonal", Ids = new string[] { "63032", "63132", "63232" } },
            new ObjectSet { Name = "Hexagon - 4 Way", Ids = new string[] { "63042", "63142", "63242" } },
            new ObjectSet { Name = "Hexagon - 4 Way - Diagonal", Ids = new string[] { "63031", "63131", "63231" } },
            new ObjectSet { Name = "Hexagon - Corner", Ids = new string[] { "63033", "63034", "63133", "63134", "63233", "63234" } },
            new ObjectSet { Name = "Hexagon - Corner - 1 Door", Ids = new string[] { "63051", "63052", "63151", "63152", "63251", "63252" } },
            new ObjectSet { Name = "Hexagon - Dead End", Ids = new string[] { "63003", "63103", "63203" } },
            new ObjectSet { Name = "Hexagon - Dead End - 1 Door", Ids = new string[] { "63004", "63005", "63007", "63104", "63105", "63107", "63204", "63205", "63207" } },
            new ObjectSet { Name = "Hexagon - Dead End - 2 Doors", Ids = new string[] { "63006", "63058", "63106", "63158", "63206", "63258" } },
            new ObjectSet { Name = "Hexagon - Ramp", Ids = new string[] { "63040", "63041", "63140", "63141", "63240", "63241" } },
            new ObjectSet { Name = "Hexagon - Ramp - Diagonal", Ids = new string[] { "63057", "63157", "63257" } },
            new ObjectSet { Name = "Hexagon - Ramp/Corridor", Ids = new string[] { "63043", "63044", "63143", "63144" } },
            new ObjectSet { Name = "Hexagon - Stairs", Ids = new string[] { "63026", "63027", "63126", "63127", "63226", "63227" } },
            new ObjectSet { Name = "Hexagon - Transition - Cave", Ids = new string[] { "63047", "63147", "63247" } },
            new ObjectSet { Name = "Hexagon - Transition - Narrow", Ids = new string[] { "63045", "63145", "63245" } },
            new ObjectSet { Name = "Hexagon - Transition - Square", Ids = new string[] { "63048", "63148", "63248" } },
            new ObjectSet { Name = "Hexagon - Transition - Arched", Ids = new string[] { "63046", "63146", "63246" } },
            new ObjectSet { Name = "Narrow - 2 Way", Ids = new string[] { "69010", "69007" } },
            new ObjectSet { Name = "Narrow - 2 Way - 1 Door", Ids = new string[] { "69008" } },
            new ObjectSet { Name = "Narrow - 2 Way - 2 Doors", Ids = new string[] { "69009" } },
            new ObjectSet { Name = "Narrow - 3 Way", Ids = new string[] { "69002", "69004" } },
            new ObjectSet { Name = "Narrow - 3 Way - 1 Door", Ids = new string[] { "69000" } },
            new ObjectSet { Name = "Narrow - 4 Way", Ids = new string[] { "69003" } },
            new ObjectSet { Name = "Narrow - Corner", Ids = new string[] { "69005", "69006" } },
            new ObjectSet { Name = "Narrow - Dead End", Ids = new string[] { "69011" } },
            new ObjectSet { Name = "Narrow - Dead End - 1 Door", Ids = new string[] { "69012" } },
            new ObjectSet { Name = "Narrow - Ramped Stairwell", Ids = new string[] { "59016" } },
            new ObjectSet { Name = "Narrow - Stairs", Ids = new string[] { "69001" } },
            new ObjectSet { Name = "Sewers - 2 Way", Ids = new string[] { "74523", "74524", "74525", "74526", "74528", "74529", "74533", "74534" } },
            new ObjectSet { Name = "Sewers - 2 Way - Door", Ids = new string[] { "74535", "74536", "74537" } },
            new ObjectSet { Name = "Sewers - 3 Way", Ids = new string[] { "74541", "74543" } },
            new ObjectSet { Name = "Sewers - 4 Way", Ids = new string[] { "74540", "74542" } },
            new ObjectSet { Name = "Sewers - Corner", Ids = new string[] { "74532", "74538", "74539" } },
            new ObjectSet { Name = "Sewers - Dead End", Ids = new string[] { "74527" } },
            new ObjectSet { Name = "Sewers - Ramp", Ids = new string[] { "74530", "74531" } },
            new ObjectSet { Name = "Sewers - Transition - Deep", Ids = new string[] { "74500" } },
            new ObjectSet { Name = "Sewers - Deep - 2 Way", Ids = new string[] { "74501", "74502", "74503", "74504", "74506", "74507" } },
            new ObjectSet { Name = "Sewers - Deep - 2 Way - 1 Door", Ids = new string[] { "74513", "74514", "74515", "74516" } },
            new ObjectSet { Name = "Sewers - Deep - 3 Way", Ids = new string[] { "74520", "74522" } },
            new ObjectSet { Name = "Sewers - Deep - 4 Way", Ids = new string[] { "74519", "74521" } },
            new ObjectSet { Name = "Sewers - Deep - Corner", Ids = new string[] { "74510", "74511", "74517", "74518" } },
            new ObjectSet { Name = "Sewers - Deep - Dead End", Ids = new string[] { "74505" } },
            new ObjectSet { Name = "Sewers - Deep - Ramp", Ids = new string[] { "74508", "74509" } },
            new ObjectSet { Name = "Square - 2 Way", Ids = new string[] { "66000", "66100", "66200", "67000", "67007", "67100", "67107", "67200", "67207", "66001", "66101", "66201", "67001", "67101", "67201", "66002", "66102", "66202", "67002", "67003", "67102", "67103", "67202", "67203" } },
            new ObjectSet { Name = "Square - 2 Way - 1 Door", Ids = new string[] { "66007", "66107", "66207", "67011", "67111", "67211" } },
            new ObjectSet { Name = "Square - 2 Way - 2 Doors", Ids = new string[] { "66021", "66121", "66221", "67012", "67013", "67112", "67113", "67212", "67213" } },
            new ObjectSet { Name = "Square - 3 Way - 1 Door", Ids = new string[] { "67032", "67132", "67232" } },
            new ObjectSet { Name = "Square - 2 Way - Chute - Ceiling", Ids = new string[] { "67325", "67425", "67525" } },
            new ObjectSet { Name = "Square - 2 Way - Chute - Floor", Ids = new string[] { "67302", "67402", "67502" } },
            new ObjectSet { Name = "Square - 2 Way - Chute - Ceiling/Floor", Ids = new string[] { "67335", "67435", "67535" } },
            new ObjectSet { Name = "Square - 3 Way", Ids = new string[] { "67005", "67006", "67105", "67106", "67205", "67206" } },
            new ObjectSet { Name = "Square - 4 Way", Ids = new string[] { "67004", "67104", "67204" } },
            new ObjectSet { Name = "Square - Corner", Ids = new string[] { "66006", "66106", "66206", "67009", "67109", "67209", "67008", "67108", "67208", "67010", "67110", "67210" } },
            new ObjectSet { Name = "Square - Corner - 1 Door", Ids = new string[] { "67022", "67023", "67122", "67123", "67222", "67223" } },
            new ObjectSet { Name = "Square - Corner - Chute - Ceiling", Ids = new string[] { "67328", "67428", "67528" } },
            new ObjectSet { Name = "Square - Corner - Chute - Floor", Ids = new string[] { "67300", "67400", "67500" } },
            new ObjectSet { Name = "Square - Corner - Chute - Ceiling/Floor", Ids = new string[] { "67330", "67430", "67530" } },
            new ObjectSet { Name = "Square - Corner - Porticullis Slot", Ids = new string[] { "67031", "67131", "67231" } },
            new ObjectSet { Name = "Square - Dead End", Ids = new string[] { "66023", "66123", "66223", "67014", "67114", "67214" } },
            new ObjectSet { Name = "Square - Dead End - 1 Door", Ids = new string[] { "66003", "66004", "66005", "66103", "66104", "66105", "66203", "66204", "66205", "67017", "67018", "67019", "67117", "67118", "67119", "67217", "67218", "67219" } },
            new ObjectSet { Name = "Square - Dead End - 2 Doors", Ids = new string[] { "66008", "66009", "66020", "66108", "66109", "66120", "66208", "66209", "66220", "67020", "67021", "67120", "67121", "67220", "67221" } },
            new ObjectSet { Name = "Square - Dead End - 3 Doors", Ids = new string[] { "66022", "66122", "66222" } },
            new ObjectSet { Name = "Square - Dead End - Chute - Ceiling", Ids = new string[] { "67327", "67427", "67527" } },
            new ObjectSet { Name = "Square - Dead End - Chute - Floor", Ids = new string[] { "67301", "67401", "67501" } },
            new ObjectSet { Name = "Square - Dead End - Chute - Ceiling/Floor", Ids = new string[] { "67329", "67429", "67529" } },
            new ObjectSet { Name = "Square - Junction 2x2 - 2 Way", Ids = new string[] { "68003" } },
            new ObjectSet { Name = "Square - Junction 2x2 - 3 Way", Ids = new string[] { "68004" } },
            new ObjectSet { Name = "Square - Junction 2x2 - 4 Way", Ids = new string[] { "68005" } },
            new ObjectSet { Name = "Square - Junction 2x2 - Dead End", Ids = new string[] { "68002" } },
            new ObjectSet { Name = "Square - Ramp", Ids = new string[] { "65017", "67015", "67024", "67026", "67115", "67124", "67126", "67215", "67224", "67226" } },
            new ObjectSet { Name = "Square - Ramp - 1 Door", Ids = new string[] { "67030", "67130", "67230" } },
            new ObjectSet { Name = "Square - Slope", Ids = new string[] { "67027", "67028", "67029", "67127", "67128", "67129", "67227", "67228", "67229" } },
            new ObjectSet { Name = "Square - Stairs", Ids = new string[] { "65018", "67016", "67025", "67116", "67125", "67216", "67225" } },
            new ObjectSet { Name = "Square - Stairs - 2 Doors", Ids = new string[] { "58008" } },
        };

        public static ObjectSet[] dungeonPartsMisc = new ObjectSet[]
        {
            new ObjectSet { Name = "Bridge - Rope", Ids = new string[] { "62031" } },
            new ObjectSet { Name = "Bridge - Stone", Ids = new string[] { "62012", "62112", "62212" } },
            new ObjectSet { Name = "Bridge - Stone - Mid", Ids = new string[] { "61608", "61609" } },
            new ObjectSet { Name = "Bridge - Stone - End", Ids = new string[] { "61600", "61601" } },
            new ObjectSet { Name = "Ceiling 2x2", Ids = new string[] { "64000" } },
            new ObjectSet { Name = "Ceiling 6x6", Ids = new string[] { "58019" } },
            new ObjectSet { Name = "Chute - Ceiling - Hole", Ids = new string[] { "58003" } },
            new ObjectSet { Name = "Chute - Dirt Floor", Ids = new string[] { "58002" } },
            new ObjectSet { Name = "Chutes", Ids = new string[] { "58004", "58012" } },
            new ObjectSet { Name = "Circular Staircase - Bottom", Ids = new string[] { "56000", "56300" } },
            new ObjectSet { Name = "Circular Staircase - Landing", Ids = new string[] { "56002", "56303", "56305" } },
            new ObjectSet { Name = "Circular Staircase - Mid", Ids = new string[] { "56001", "56301" } },
            new ObjectSet { Name = "Circular Staircase - Room - Ceiling", Ids = new string[] { "63700" } },
            new ObjectSet { Name = "Circular Staircase - Room - Mid", Ids = new string[] { "56006", "56007", "56008", "56009" } },
            new ObjectSet { Name = "Circular Staircase - Room - Top", Ids = new string[] { "56010" } },
            new ObjectSet { Name = "Doorway", Ids = new string[] { "58005" } },
            new ObjectSet { Name = "Floor 6x3", Ids = new string[] { "64001" } },
            new ObjectSet { Name = "Floor 6x6", Ids = new string[] { "64003" } },
            new ObjectSet { Name = "Floor 7x3", Ids = new string[] { "64004" } },
            new ObjectSet { Name = "Floor 7x6", Ids = new string[] { "64005" } },
            new ObjectSet { Name = "Floor/Ceiling 2x2 - Chute 1x0.5", Ids = new string[] { "58001" } },
            new ObjectSet { Name = "Floor/Ceiling 2x2 - Chute 1x1", Ids = new string[] { "58000" } },
            new ObjectSet { Name = "Pit - Floor 4x4 - 4 Chutes", Ids = new string[] { "58017" } },
            new ObjectSet { Name = "Pit - Half-Room - 1 Door", Ids = new string[] { "58018" } },
            new ObjectSet { Name = "Platform - 1x1", Ids = new string[] { "54000" } },
            new ObjectSet { Name = "Platform - Block Stairs", Ids = new string[] { "59010" } },
            new ObjectSet { Name = "Platform - Bridge - Rectangle", Ids = new string[] { "58041" } },
            new ObjectSet { Name = "Platform - Bridge - Spike", Ids = new string[] { "58040" } },
            new ObjectSet { Name = "Platform - Chipped Beam", Ids = new string[] { "58042" } },
            new ObjectSet { Name = "Platform - Corner Beam", Ids = new string[] { "58055" } },
            new ObjectSet { Name = "Platform - Marble", Ids = new string[] { "62322" } },
            new ObjectSet { Name = "Platform - Mid - 1x1", Ids = new string[] { "58034" } },
            new ObjectSet { Name = "Platform - Mid - 2x2", Ids = new string[] { "58035" } },
            new ObjectSet { Name = "Platform - Mid - 2x2 - 1 Door", Ids = new string[] { "58036" } },
            new ObjectSet { Name = "Platform - Mid - 2x2 - Gap", Ids = new string[] { "58046", "58047" } },
            new ObjectSet { Name = "Platform - Mid", Ids = new string[] { "59014", "59015" } },
            new ObjectSet { Name = "Platform - Side", Ids = new string[] { "58043" } },
            new ObjectSet { Name = "Platform - Spike", Ids = new string[] { "58044" } },
            new ObjectSet { Name = "Platform - Top - 2x2 - 1 Door", Ids = new string[] { "58033" } },
            new ObjectSet { Name = "Platform 1x1x0.5", Ids = new string[] { "72019" } },
            new ObjectSet { Name = "Platform 1x3 - With 1x1 Floor", Ids = new string[] { "58045" } },
            new ObjectSet { Name = "Platform 3x3x1 - Bridges", Ids = new string[] { "58032" } },
            new ObjectSet { Name = "Portcullis", Ids = new string[] { "60506" } },
            new ObjectSet { Name = "Ramp - Big", Ids = new string[] { "59006" } },
            new ObjectSet { Name = "Ramp - Small", Ids = new string[] { "59008", "59009" } },
            new ObjectSet { Name = "Secret Wall Block", Ids = new string[] { "61025" } },
            new ObjectSet { Name = "Stairs", Ids = new string[] { "59000", "59001", "59002", "59003", "59007", "59012", "59017" } },
            new ObjectSet { Name = "Stairs - Corner", Ids = new string[] { "59004", "59005" } },
            new ObjectSet { Name = "Switch - Lever Base", Ids = new string[] { "61026" } },
            new ObjectSet { Name = "Switch - Levers", Ids = new string[] { "61027", "61028" } },
            new ObjectSet { Name = "Switch - Wheel", Ids = new string[] { "61032" } },
            new ObjectSet { Name = "Trapdoor", Ids = new string[] { "54001" } },
        };

        public static ObjectSet[] dungeonPartsCaves = new ObjectSet[]
        {
            new ObjectSet { Name = "Cave - 2 Way", Ids = new string[] { "60100", "60101", "60102", "60103", "60107", "60108", "60109" } },
            new ObjectSet { Name = "Cave - 3 Way", Ids = new string[] { "60106" } },
            new ObjectSet { Name = "Cave - 4 Way", Ids = new string[] { "60111" } },
            new ObjectSet { Name = "Cave - Corner", Ids = new string[] { "60104", "60105", "60110" } },
            new ObjectSet { Name = "Cave - Dead End", Ids = new string[] { "60113" } },
            new ObjectSet { Name = "Cave - Ramp", Ids = new string[] { "60112" } },
            new ObjectSet { Name = "Cave - Room - 1 Way", Ids = new string[] { "60200" } },
            new ObjectSet { Name = "Cave - Room - 2 Way", Ids = new string[] { "60201" } },
            new ObjectSet { Name = "Cave - Room - 2 Way - No Ceiling", Ids = new string[] { "60202" } },
            new ObjectSet { Name = "Chasm - 2 Way", Ids = new string[] { "62014", "62015" } },
            new ObjectSet { Name = "Chasm - 2 Way - Bridge Gap", Ids = new string[] { "62013" } },
            new ObjectSet { Name = "Chasm - 2 Way - Pathway Up", Ids = new string[] { "62026", "74112" } },
            new ObjectSet { Name = "Chasm - 3 Way", Ids = new string[] { "60203" } },
            new ObjectSet { Name = "Chasm - Ceiling", Ids = new string[] { "62017", "62018", "62021", "74111" } },
            new ObjectSet { Name = "Chasm - Corner", Ids = new string[] { "62019" } },
            new ObjectSet { Name = "Chasm - Dead End", Ids = new string[] { "62016" } },
            new ObjectSet { Name = "Chasm - Dead End - 1 Way", Ids = new string[] { "62020" } },
            new ObjectSet { Name = "Chasm - Ledge - 2 Way", Ids = new string[] { "62023", "62123", "62223" } },
            new ObjectSet { Name = "Chasm - Ledge - 2 Way - 1 Arched Door", Ids = new string[] { "62022", "62122", "62222" } },
            new ObjectSet { Name = "Chasm - Ledge - 2 Way - Gap", Ids = new string[] { "62030", "62130", "62230" } },
            new ObjectSet { Name = "Chasm - Ledge - Dead End", Ids = new string[] { "62024", "62025", "62124", "62125", "62224", "62225" } },
            new ObjectSet { Name = "Chasm - Ledge - Wall", Ids = new string[] { "62027", "62127", "62227" } },
            new ObjectSet { Name = "Chasm - Ramp", Ids = new string[] { "74110" } },
        };

        public static ObjectSet[] dungeonPartsDoors = new ObjectSet[]
        {
            new ObjectSet { Name = "Door", Ids = new string[] { "55000", "55005", "9003", "9004" } },
            new ObjectSet { Name = "Entrance/Exit - Standalone", Ids = new string[] { "70300" } },
            new ObjectSet { Name = "Entrance/Exit - Crypt Entry Room", Ids = new string[] { "58051" } },
            new ObjectSet { Name = "Red Brick Door", Ids = new string[] { "72100" } },
            new ObjectSet { Name = "Secret Door - Hexagonal", Ids = new string[] { "55007", "55018", "55024" } },
            new ObjectSet { Name = "Secret Door - Hexagonal - Large", Ids = new string[] { "55019", "55025" } },
            new ObjectSet { Name = "Secret Door - Large", Ids = new string[] { "55010", "55021", "55027", "55032", "55011", "55022", "55028" } },
            new ObjectSet { Name = "Secret Door - Narrow", Ids = new string[] { "55008" } },
            new ObjectSet { Name = "Secret Door - Standard", Ids = new string[] { "55006", "55009", "55012", "55017", "55020", "55023", "55026", "55029", "55030", "55031" } },
        };

        public static ObjectSet[] interiorParts = houseParts
            .Concat(dungeonPartsRooms)
            .Concat(dungeonPartsCorridors)
            .Concat(dungeonPartsMisc)
            .Concat(dungeonPartsCaves)
            .Concat(dungeonPartsDoors)
            .OrderBy(set => set.Name)
            .ToArray();

        public static ObjectSet[] objects = models
            .Concat(billboards)
            .Concat(interiorParts)
            .OrderBy(set => set.Name)
            .ToArray();

        /// <summary>
        /// Returns list of locations based on file path. Return null if not found or wrong file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IEnumerable<LocationInstance> LoadLocationInstance(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Location instance file could not be found at '{path}'");
                return null;
            }


            if (path.EndsWith(".csv"))
            {
                TextReader reader = File.OpenText(path);

                return LoadLocationInstanceCsv(reader, $"file={path}");
            }
            else
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                return LoadLocationInstance(xmlDoc, $"file={path}");
            }
        }

        public static IEnumerable<LocationInstance> LoadLocationInstance(Mod mod, string assetName)
        {
            TextAsset asset = mod.GetAsset<TextAsset>(assetName);
            if (asset == null)
            {
                Debug.LogWarning($"Asset '{assetName}' could not be found in mod '{mod.Title}'");
                return Enumerable.Empty<LocationInstance>();
            }

            TextReader reader = new StringReader(asset.text);
            if (assetName.EndsWith(".csv"))
            {
                return LoadLocationInstanceCsv(reader, $"mod={mod.Title}, asset={assetName}");
            }
            else
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(reader);

                return LoadLocationInstance(xmlDoc, $"mod={mod.Title}, asset={assetName}");
            }
        }

        public static IEnumerable<LocationInstance> LoadLocationInstance(XmlDocument xmlDoc, string contextString)
        {
            if (xmlDoc.SelectSingleNode("//locations") == null)
            {
                Debug.LogWarning("Wrong file format");
                yield return null;
            }

            CultureInfo cultureInfo = new CultureInfo("en-US");

            XmlNodeList instanceNodes = xmlDoc.GetElementsByTagName("locationInstance");
            for (int i = 0; i < instanceNodes.Count; i++)
            {
                XmlNode node = instanceNodes[i];
                if (node["prefab"].InnerXml == "")
                {
                    Debug.LogWarning("Locationinstance must have a assigned prefab to be valid");
                    continue;
                }

                LocationInstance tmpInst = new LocationInstance();
                try
                {
                    tmpInst.name = node["name"].InnerXml;
                    tmpInst.type = int.Parse(node["type"].InnerXml);
                    tmpInst.prefab = node["prefab"].InnerXml;
                    tmpInst.worldX = int.Parse(node["worldX"].InnerXml);
                    tmpInst.worldY = int.Parse(node["worldY"].InnerXml);
                    tmpInst.terrainX = int.Parse(node["terrainX"].InnerXml);
                    tmpInst.terrainY = int.Parse(node["terrainY"].InnerXml);

                    XmlNode child = node["rotW"];
                    if (child != null)
                    {
                        tmpInst.rot.w = float.Parse(child.InnerXml, cultureInfo);
                        tmpInst.rot.x = float.Parse(node["rotX"].InnerXml, cultureInfo);
                        tmpInst.rot.y = float.Parse(node["rotY"].InnerXml, cultureInfo);
                        tmpInst.rot.z = float.Parse(node["rotZ"].InnerXml, cultureInfo);
                    }
                    else
                    {
                        child = node["rotYAxis"];
                        if (child != null)
                        {
                            float yRot = float.Parse(child.InnerXml, cultureInfo);
                            tmpInst.rot.eulerAngles = new Vector3(0, yRot, 0);
                        }
                    }

                    child = node["sink"];
                    if (child != null)
                    {
                        tmpInst.sink = float.Parse(child.InnerXml, cultureInfo);
                    }

                    child = node["extraData"];
                    if (child != null)
                    {
                        tmpInst.extraData = child.InnerXml;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error while parsing location instance: {e.Message}");
                    continue;
                }

                if (tmpInst.terrainX < 0 || tmpInst.terrainY < 0 || tmpInst.terrainX >= 128 || tmpInst.terrainY >= 128)
                {
                    Debug.LogWarning($"Invalid location instance '{tmpInst.name}' ({contextString}): terrainX and terrainY must be higher than 0 and lower than 128");
                    continue;
                }

                yield return tmpInst;
            }
        }

        static Regex CsvSplit = new Regex("(?:^|,)(\"(?:\"\"|[^\"])*\"|[^,]*)", RegexOptions.Compiled);

        public static string[] SplitCsvLine(string line)
        {
            List<string> list = new List<string>();
            foreach (Match match in CsvSplit.Matches(line))
            {
                string curr = match.Value;
                if (0 == curr.Length)
                {
                    list.Add("");
                }

                list.Add(curr.TrimStart(',', ';').Replace("\"\"", "\"").Trim('\"'));
            }

            return list.ToArray();
        }

        public static bool ParseBool(string Value, string Context)
        {
            if (string.IsNullOrEmpty(Value))
                return false;

            if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new InvalidDataException($"Error parsing ({Context}): invalid boolean value '{Value}'");
        }

        public static int[] ParseIntArrayArg(string Arg, string Context)
        {
            if (string.IsNullOrEmpty(Arg))
                return Array.Empty<int>();

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
            return Frames.Select(int.Parse).ToArray();
        }

        public static IEnumerable<LocationInstance> LoadLocationInstanceCsv(TextReader csvStream, string contextString)
        {
            string header = csvStream.ReadLine();
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
                    Debug.LogError($"Location instance file failed ({contextString}): could not find field '{fieldName}' in header");
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

            if (!GetIndex("name", out int nameIndex)) yield break;
            if (!GetIndex("type", out int typeIndex)) yield break;
            if (!GetIndex("prefab", out int prefabIndex)) yield break;
            if (!GetIndex("worldX", out int worldXIndex)) yield break;
            if (!GetIndex("worldY", out int worldYIndex)) yield break;
            if (!GetIndex("terrainX", out int terrainXIndex)) yield break;
            if (!GetIndex("terrainY", out int terrainYIndex)) yield break;
            int? rotWIndex = GetIndexOpt("rotW");
            int? rotXIndex = GetIndexOpt("rotX");
            int? rotYIndex = GetIndexOpt("rotY");
            int? rotZIndex = GetIndexOpt("rotZ");
            int? rotXAxisIndex = GetIndexOpt("rotXAxis");
            int? rotYAxisIndex = GetIndexOpt("rotYAxis");
            int? rotZAxisIndex = GetIndexOpt("rotZAxis");
            int? sinkIndex = GetIndexOpt("sink");
            int? scaleIndex = GetIndexOpt("scale");
            int? extraDataIndex = GetIndexOpt("extraData");

            CultureInfo cultureInfo = new CultureInfo("en-US");
            int lineNumber = 1;
            while (csvStream.Peek() >= 0)
            {
                ++lineNumber;
                string line = csvStream.ReadLine();

                string[] tokens = SplitCsvLine(line);

                LocationInstance tmpInst = new LocationInstance();

                try
                {
                    tmpInst.name = tokens[nameIndex];
                    tmpInst.type = int.Parse(tokens[typeIndex]);
                    tmpInst.prefab = tokens[prefabIndex];
                    tmpInst.worldX = int.Parse(tokens[worldXIndex]);
                    tmpInst.worldY = int.Parse(tokens[worldYIndex]);
                    tmpInst.terrainX = int.Parse(tokens[terrainXIndex]);
                    tmpInst.terrainY = int.Parse(tokens[terrainYIndex]);

                    if (rotWIndex.HasValue && !string.IsNullOrEmpty(tokens[rotWIndex.Value])) tmpInst.rot.w = float.Parse(tokens[rotWIndex.Value], cultureInfo);
                    if (rotXIndex.HasValue && !string.IsNullOrEmpty(tokens[rotXIndex.Value])) tmpInst.rot.x = float.Parse(tokens[rotXIndex.Value], cultureInfo);
                    if (rotYIndex.HasValue && !string.IsNullOrEmpty(tokens[rotYIndex.Value])) tmpInst.rot.y = float.Parse(tokens[rotYIndex.Value], cultureInfo);
                    if (rotZIndex.HasValue && !string.IsNullOrEmpty(tokens[rotZIndex.Value])) tmpInst.rot.z = float.Parse(tokens[rotZIndex.Value], cultureInfo);
                    if (rotXAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotXAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(float.Parse(tokens[rotXAxisIndex.Value], cultureInfo), tmpInst.rot.eulerAngles.y, tmpInst.rot.eulerAngles.z);
                    if (rotYAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotYAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(tmpInst.rot.eulerAngles.x, float.Parse(tokens[rotYAxisIndex.Value], cultureInfo), tmpInst.rot.eulerAngles.z);
                    if (rotZAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotZAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(tmpInst.rot.eulerAngles.x, tmpInst.rot.eulerAngles.y, float.Parse(tokens[rotZAxisIndex.Value], cultureInfo));
                    if (sinkIndex.HasValue && !string.IsNullOrEmpty(tokens[sinkIndex.Value])) tmpInst.sink = float.Parse(tokens[sinkIndex.Value], cultureInfo);
                    if (scaleIndex.HasValue && !string.IsNullOrEmpty(tokens[scaleIndex.Value])) tmpInst.scale = float.Parse(tokens[scaleIndex.Value], cultureInfo);
                    if (extraDataIndex.HasValue && !string.IsNullOrEmpty(tokens[extraDataIndex.Value])) tmpInst.extraData = tokens[extraDataIndex.Value];
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse a location instance ({contextString}, line {lineNumber}): {e.Message}");
                    continue;
                }

                yield return tmpInst;

            }
        }

        public static LocationInstance LoadSingleLocationInstanceCsv(string line, string[] fields, string contextString)
        {
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
                    Debug.LogError($"Location instance file failed ({contextString}): could not find field '{fieldName}' in header");
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

            if (!GetIndex("name", out int nameIndex)) return null;
            if (!GetIndex("type", out int typeIndex)) return null;
            if (!GetIndex("prefab", out int prefabIndex)) return null;
            if (!GetIndex("worldX", out int worldXIndex)) return null;
            if (!GetIndex("worldY", out int worldYIndex)) return null;
            if (!GetIndex("terrainX", out int terrainXIndex)) return null;
            if (!GetIndex("terrainY", out int terrainYIndex)) return null;
            int? rotWIndex = GetIndexOpt("rotW");
            int? rotXIndex = GetIndexOpt("rotX");
            int? rotYIndex = GetIndexOpt("rotY");
            int? rotZIndex = GetIndexOpt("rotZ");
            int? rotXAxisIndex = GetIndexOpt("rotXAxis");
            int? rotYAxisIndex = GetIndexOpt("rotYAxis");
            int? rotZAxisIndex = GetIndexOpt("rotZAxis");
            int? sinkIndex = GetIndexOpt("sink");
            int? scaleIndex = GetIndexOpt("scale");
            int? extraDataIndex = GetIndexOpt("extraData");

            CultureInfo cultureInfo = new CultureInfo("en-US");

            string[] tokens = SplitCsvLine(line);

            LocationInstance tmpInst = new LocationInstance();

            try
            {
                tmpInst.name = tokens[nameIndex];
                tmpInst.type = int.Parse(tokens[typeIndex]);
                tmpInst.prefab = tokens[prefabIndex];
                tmpInst.worldX = int.Parse(tokens[worldXIndex]);
                tmpInst.worldY = int.Parse(tokens[worldYIndex]);
                tmpInst.terrainX = int.Parse(tokens[terrainXIndex]);
                tmpInst.terrainY = int.Parse(tokens[terrainYIndex]);

                if (rotWIndex.HasValue && !string.IsNullOrEmpty(tokens[rotWIndex.Value])) tmpInst.rot.w = float.Parse(tokens[rotWIndex.Value], cultureInfo);
                if (rotXIndex.HasValue && !string.IsNullOrEmpty(tokens[rotXIndex.Value])) tmpInst.rot.x = float.Parse(tokens[rotXIndex.Value], cultureInfo);
                if (rotYIndex.HasValue && !string.IsNullOrEmpty(tokens[rotYIndex.Value])) tmpInst.rot.y = float.Parse(tokens[rotYIndex.Value], cultureInfo);
                if (rotZIndex.HasValue && !string.IsNullOrEmpty(tokens[rotZIndex.Value])) tmpInst.rot.z = float.Parse(tokens[rotZIndex.Value], cultureInfo);
                if (rotXAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotXAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(float.Parse(tokens[rotXAxisIndex.Value], cultureInfo), tmpInst.rot.eulerAngles.y, tmpInst.rot.eulerAngles.z);
                if (rotYAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotYAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(tmpInst.rot.eulerAngles.x, float.Parse(tokens[rotYAxisIndex.Value], cultureInfo), tmpInst.rot.eulerAngles.z);
                if (rotZAxisIndex.HasValue && !string.IsNullOrEmpty(tokens[rotZAxisIndex.Value])) tmpInst.rot.eulerAngles = new Vector3(tmpInst.rot.eulerAngles.x, tmpInst.rot.eulerAngles.y, float.Parse(tokens[rotZAxisIndex.Value], cultureInfo));
                if (sinkIndex.HasValue && !string.IsNullOrEmpty(tokens[sinkIndex.Value])) tmpInst.sink = float.Parse(tokens[sinkIndex.Value], cultureInfo);
                if (scaleIndex.HasValue && !string.IsNullOrEmpty(tokens[scaleIndex.Value])) tmpInst.scale = float.Parse(tokens[scaleIndex.Value], cultureInfo);
                if (extraDataIndex.HasValue && !string.IsNullOrEmpty(tokens[extraDataIndex.Value])) tmpInst.extraData = tokens[extraDataIndex.Value];
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse a location instance ({contextString}): {e.Message}");
                return null;
            }

            return tmpInst;
        }

        /// <summary>
        /// Save location list to path
        /// </summary>
        /// <param name="locationInstance"></param>
        /// <param name="path"></param>
        public static void SaveLocationInstance(LocationInstance[] locationInstance, string path)
        {
            CultureInfo cultureInfo = new CultureInfo("en-US");

            StreamWriter writer = new StreamWriter(path, false);
            writer.WriteLine("<locations>");

            foreach (LocationInstance inst in locationInstance)
            {
                writer.WriteLine("\t<locationInstance>");
                writer.WriteLine("\t\t<name>" + inst.name + "</name>");
                writer.WriteLine("\t\t<type>" + inst.type + "</type>");
                writer.WriteLine("\t\t<prefab>" + inst.prefab + "</prefab>");
                writer.WriteLine("\t\t<worldX>" + inst.worldX + "</worldX>");
                writer.WriteLine("\t\t<worldY>" + inst.worldY + "</worldY>");
                writer.WriteLine("\t\t<terrainX>" + inst.terrainX + "</terrainX>");
                writer.WriteLine("\t\t<terrainY>" + inst.terrainY + "</terrainY>");
                if(inst.rot != Quaternion.identity)
                {
                    writer.WriteLine("\t\t<rotW>" + inst.rot.w.ToString(cultureInfo) + "</rotW>");
                    writer.WriteLine("\t\t<rotX>" + inst.rot.x.ToString(cultureInfo) + "</rotX>");
                    writer.WriteLine("\t\t<rotY>" + inst.rot.y.ToString(cultureInfo) + "</rotY>");
                    writer.WriteLine("\t\t<rotZ>" + inst.rot.z.ToString(cultureInfo) + "</rotZ>");
                }
                if(inst.sink != 0f)
                {
                    writer.WriteLine("\t\t<sink>" + inst.sink.ToString(cultureInfo) + "</sink>");
                }
                if(inst.scale != 1f)
                {
                    writer.WriteLine("\t\t<scale>" + inst.scale.ToString(cultureInfo) + "</scale>");
                }
                if(!string.IsNullOrEmpty(inst.extraData))
                {
                    writer.WriteLine("\t\t<extraData>" + inst.extraData + "</extraData>");
                }
                writer.WriteLine("\t</locationInstance>");
            }

            writer.WriteLine("</locations>");
            writer.Close();
        }

        public static string SaveSingleLocationInstanceCsv(LocationInstance instance, string[] fields, string[] originalValues)
        {
            StringBuilder result = new StringBuilder();

            bool first = true;
            foreach(var pair in fields.Zip(originalValues, Tuple.Create))
            {
                if (!first)
                    result.Append(",");
                else
                    first = false;

                switch(pair.Item1)
                {
                    case "name":
                        result.Append(instance.name);
                        break;

                    case "type":
                        result.Append(instance.type);
                        break;

                    case "prefab":
                        result.Append(instance.prefab);
                        break;

                    case "worldX":
                        result.Append(instance.worldX);
                        break;

                    case "worldY":
                        result.Append(instance.worldY);
                        break;

                    case "terrainX":
                        result.Append(instance.terrainX);
                        break;

                    case "terrainY":
                        result.Append(instance.terrainY);
                        break;

                    case "rotW":
                        result.Append(instance.rot.w);
                        break;

                    case "rotX":
                        result.Append(instance.rot.x);
                        break;

                    case "rotY":
                        result.Append(instance.rot.y);
                        break;

                    case "rotZ":
                        result.Append(instance.rot.z);
                        break;

                    case "rotXAxis":
                        result.Append(instance.rot.eulerAngles.x);
                        break;

                    case "rotYAxis":
                        result.Append(instance.rot.eulerAngles.y);
                        break;

                    case "rotZAxis":
                        result.Append(instance.rot.eulerAngles.z);
                        break;

                    case "sink":
                        result.Append(instance.sink);
                        break;

                    case "scale":
                        result.Append(instance.scale);
                        break;

                    case "extraData":
                        result.Append($"\"{instance.extraData.Replace("\"", "\"\"")}\"");
                        break;

                    default:
                        result.Append(pair.Item2);
                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Returns locationprefab based on file path. Return null if not found or wrong file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static LocationPrefab LoadLocationPrefab(string path)
        {
            if (!File.Exists(path))
                return null;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(path);

            return LoadLocationPrefab(xmlDoc);
        }

        public static LocationPrefab LoadLocationPrefab(Mod mod, string assetName)
        {
            TextAsset asset = mod.GetAsset<TextAsset>(assetName);
            if (asset == null)
                return null;

            TextReader reader = new StringReader(asset.text);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(reader);

            return LoadLocationPrefab(xmlDoc);
        }

        public static LocationPrefab LoadLocationPrefab(XmlDocument xmlDoc)
        {
            XmlNode prefabNode = xmlDoc.SelectSingleNode("//locationPrefab");
            if (prefabNode == null)
            {
                Debug.LogWarning("Wrong file format");
                return null;
            }

            CultureInfo cultureInfo = new CultureInfo("en-US");

            LocationPrefab locationPrefab = new LocationPrefab();

            try
            {
                locationPrefab.height = int.Parse(prefabNode["height"].InnerXml);
                locationPrefab.width = int.Parse(prefabNode["width"].InnerXml);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error while parsing location prefab: {e.Message}");
                return null;
            }

            var winterPrefabNode = prefabNode["winterPrefab"];
            if (winterPrefabNode != null)
                locationPrefab.winterPrefab = winterPrefabNode.InnerText;

            var objects = xmlDoc.GetElementsByTagName("object");
            for (int i = 0; i < objects.Count; i++)
            {
                XmlNode objectNode = objects[i];

                var obj = new LocationObject();

                try
                {
                    obj.type = int.Parse(objectNode["type"].InnerXml);
                    obj.name = objectNode["name"].InnerXml;

                    obj.objectID = int.Parse(objectNode["objectID"].InnerXml);

                    obj.pos.x = float.Parse(objectNode["posX"].InnerXml, cultureInfo);
                    obj.pos.y = float.Parse(objectNode["posY"].InnerXml, cultureInfo);
                    obj.pos.z = float.Parse(objectNode["posZ"].InnerXml, cultureInfo);

                    obj.scale.x = float.Parse(objectNode["scaleX"].InnerXml, cultureInfo);
                    obj.scale.y = float.Parse(objectNode["scaleY"].InnerXml, cultureInfo);
                    obj.scale.z = float.Parse(objectNode["scaleZ"].InnerXml, cultureInfo);

                    if (HasRotation(obj))
                    {
                        var node = objectNode["rotW"];
                        if(node != null)
                            obj.rot.w = float.Parse(node.InnerXml, cultureInfo);

                        node = objectNode["rotX"];
                        if (node != null)
                            obj.rot.x = float.Parse(node.InnerXml, cultureInfo);

                        node = objectNode["rotY"];
                        if (node != null)
                            obj.rot.y = float.Parse(node.InnerXml, cultureInfo);

                        node = objectNode["rotZ"];
                        if (node != null)
                            obj.rot.z = float.Parse(node.InnerXml, cultureInfo);
                    }

                    // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)
                    if (obj.type == LocationObject.TypeRMB)
                    {
                        var groundPlaneNode = objectNode["groundPlane"];
                        if (groundPlaneNode != null)
                            obj.groundPlane = bool.Parse(groundPlaneNode.InnerXml);
                    }
                    // MOBILE: end backport

                    var extraDataNode = objectNode["extraData"];
                    if (extraDataNode != null)
                        obj.extraData = extraDataNode.InnerXml;

                    FixupLocationObjectData(obj);

                    if (!ValidateValue(obj.type, obj.name))
                        continue;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error while parsing location prefab object: {e.Message}");
                    continue;
                }

                locationPrefab.obj.Add(obj);
            }
            return locationPrefab;
        }

        static void FixupLocationObjectData(LocationObject obj)
        {
            if(obj.type == 2)
            {
                switch(obj.name)
                {
                    case "199.16":
                        if(int.TryParse(obj.extraData, out int parsedValue))
                        {
                            EnemyMarkerExtraData enemyExtraData;
                            enemyExtraData.EnemyId = parsedValue;
                            enemyExtraData.TeamOverride = 0;

                            obj.extraData = SaveLoadManager.Serialize(typeof(EnemyMarkerExtraData), enemyExtraData, pretty: false);
                        }
                        break;
                }
            }
            else if(obj.type == 4)
            {
                if(!string.IsNullOrEmpty(Path.GetDirectoryName(obj.name)))
                    obj.name = Path.GetFileNameWithoutExtension(obj.name);

                // Treat numeric names as custom models
                if (int.TryParse(obj.name, out int _))
                    obj.type = 0;
            }
        }

        public static bool HasRotation(LocationObject obj)
        {
            return obj.type == 0 || obj.type == 3 || obj.type == 4;
        }

        /// <summary>
        /// Save locationprefab to path
        /// </summary>
        /// <param name="locationPrefab"></param>
        /// <param name="path"></param>
        public static void SaveLocationPrefab(LocationPrefab locationPrefab, string path)
        {
            CultureInfo cultureInfo = new CultureInfo("en-US");

            //Write some text to the test.txt file
            StreamWriter writer = new StreamWriter(path, false);

            writer.WriteLine("<locationPrefab>");
            writer.WriteLine("\t<height>" + locationPrefab.height + "</height>");
            writer.WriteLine("\t<width>" + locationPrefab.width + "</width>");

            foreach (LocationObject obj in locationPrefab.obj)
            {
                writer.WriteLine("\t<object>");
                writer.WriteLine("\t\t<type>" + obj.type + "</type>");
                writer.WriteLine("\t\t<objectID>" + obj.objectID + "</objectID>");
                writer.WriteLine("\t\t<name>" + obj.name + "</name>");

                writer.WriteLine("\t\t<posX>" + obj.pos.x.ToString(cultureInfo) + "</posX>");
                writer.WriteLine("\t\t<posY>" + obj.pos.y.ToString(cultureInfo) + "</posY>");
                writer.WriteLine("\t\t<posZ>" + obj.pos.z.ToString(cultureInfo) + "</posZ>");

                writer.WriteLine("\t\t<scaleX>" + obj.scale.x.ToString(cultureInfo) + "</scaleX>");
                writer.WriteLine("\t\t<scaleY>" + obj.scale.y.ToString(cultureInfo) + "</scaleY>");
                writer.WriteLine("\t\t<scaleZ>" + obj.scale.z.ToString(cultureInfo) + "</scaleZ>");

                if (!string.IsNullOrEmpty(obj.extraData))
                {
                    writer.WriteLine("\t\t<extraData>" + obj.extraData + "</extraData>");
                }

                if (HasRotation(obj))
                {
                    writer.WriteLine("\t\t<rotW>" + obj.rot.w.ToString(cultureInfo) + "</rotW>");
                    writer.WriteLine("\t\t<rotX>" + obj.rot.x.ToString(cultureInfo) + "</rotX>");
                    writer.WriteLine("\t\t<rotY>" + obj.rot.y.ToString(cultureInfo) + "</rotY>");
                    writer.WriteLine("\t\t<rotZ>" + obj.rot.z.ToString(cultureInfo) + "</rotZ>");
                }

                // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)
                if (obj.type == LocationObject.TypeRMB && obj.groundPlane)
                {
                    writer.WriteLine("\t\t<groundPlane>" + obj.groundPlane.ToString().ToLower() + "</groundPlane>");
                }
                // MOBILE: end backport

                writer.WriteLine("\t</object>");
            }

            if (!string.IsNullOrEmpty(locationPrefab.winterPrefab))
                writer.WriteLine($"\t<winterPrefab>{locationPrefab.winterPrefab}</winterPrefab>");

            writer.WriteLine("</locationPrefab>");
            writer.Close();
        }

        /// <summary>
        /// Validate the name of the object
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool ValidateValue(int type, string name)
        {
            if (type == 0)
            {
                try
                {
                    int.Parse(name);
                }
                catch (FormatException)
                {
                    Debug.LogWarning("Object type is set incorrectly: 0 = Model, 1 = Flat");
                    return false;
                }
                catch (OverflowException)
                {
                    Debug.LogWarning("Object type is set incorrectly: 0 = Model, 1 = Flat");
                    return false;
                }

                return true;
            }

            else if (type == 1)
            {
                string[] arg = name.Split('.');

                if (arg.Length == 2)
                {
                    try
                    {
                        int.Parse(arg[0]);
                        int.Parse(arg[1]);
                    }
                    catch (FormatException)
                    {
                        Debug.LogWarning("Billboard string format is invalid, use ARCHIVEID.RECORDID");
                        return false;
                    }
                    catch (OverflowException)
                    {
                        Debug.LogWarning("Billboard string format is invalid, use ARCHIVEID.RECORDID");
                        return false;
                    }

                    return true;
                }

                Debug.LogWarning("Billboard string format is invalid, use ARCHIVEID.RECORDID");
                return false;

            }
            else if (type == 2)
            {
                string[] arg = name.Split('.');

                if (arg.Length == 2)
                {
                    try
                    {
                        if (int.Parse(arg[0]) != 199)
                        {
                            Debug.LogWarning("Editor marker name format is invalid, use 199.RECORDID");
                            return false;
                        }
                        int.Parse(arg[1]);
                    }
                    catch (FormatException)
                    {
                        Debug.LogWarning("Editor marker name format is invalid, use 199.RECORDID");
                        return false;
                    }
                    catch (OverflowException)
                    {
                        Debug.LogWarning("Editor marker name format is invalid, use 199.RECORDID");
                        return false;
                    }

                    return true;
                }

                Debug.LogWarning("Editor marker name format is invalid, use 199.RECORDID");
                return false;
            }
            // MOBILE: backport of drcarademono/DFU-LocationLoader@896a574 (rmb-object) - object type 5 (RMB block)
            else if(type == 3 || type == 4 || type == LocationObject.TypeRMB)
            {
                // Just assume valid for now
                return true;
            }
            // MOBILE: end backport
            else
            {
                Debug.LogWarning($"Invalid obj type found: {type}");
                return false;
            }
        }

        /// <summary>
        /// Load a Game Object
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parent"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static GameObject LoadStaticObject(int type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, ModelCombiner modelCombiner = null)
        {
            GameObject go = null;
            //Model
            if (type == 0)
            {
                if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0)
                {
                    Debug.LogWarning($"Object {name} inside prefab has invalid rotation: {rot}");
                    rot = Quaternion.identity;
                }

                Matrix4x4 mat = Matrix4x4.TRS(pos, rot, scale);

                uint modelId = uint.Parse(name);

                go = MeshReplacement.ImportCustomGameobject(modelId, parent, mat);

                if (go == null) //if no mesh replacment exist
                {
                    if (modelCombiner != null
                        && !PlayerActivate.HasCustomActivation(modelId)
                        && DaggerfallUnity.Instance.MeshReader.GetModelData(modelId, out ModelData modelData))
                    {
                        modelCombiner.Add(ref modelData, mat);
                    }
                    else
                    {
                        go = GameObjectHelper.CreateDaggerfallMeshGameObject(modelId, parent);
                        if (go != null)
                        {
                            go.transform.localPosition = pos;
                            go.transform.localRotation = rot;
                            go.transform.localScale = scale;
                        }
                    }
                }
            }
            //Flat
            else if (type == 1)
            {
                string[] arg = name.Split('.');

                go = MeshReplacement.ImportCustomFlatGameobject(int.Parse(arg[0]), int.Parse(arg[1]), pos, parent);

                if (go == null)
                {
                    go = GameObjectHelper.CreateDaggerfallBillboardGameObject(int.Parse(arg[0]), int.Parse(arg[1]), parent);

                    if (go != null)
                    {
                        go.transform.localPosition = pos;

                        if (arg[0] == "210")
                            AddLight(int.Parse(arg[1]), go.transform);

                        if (arg[0] == "201")
                            AddAnimalAudioSource(int.Parse(arg[1]), go);
                    }
                }

                if (go != null)
                {
                    go.transform.localScale = new Vector3(go.transform.localScale.x * scale.x, go.transform.localScale.y * scale.y, go.transform.localScale.z * scale.z);
                }
            }

            return go;
        }

        /// <summary>
        /// Adds a light to a flat. This is a modified copy of a method with the same name, found in DaggerfallInterior.cs
        /// </summary>
        /// <param name="id"></param>
        /// <param name="parent"></param>
        public static void AddLight(int textureRecord, Transform parent)
        {
            GameObject go = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_InteriorLightPrefab.gameObject, string.Empty, parent, parent.position);
            Vector2 size = DaggerfallUnity.Instance.MeshReader.GetScaledBillboardSize(210, textureRecord) * MeshReader.GlobalScale;
            Light light = go.GetComponent<Light>();
            switch (textureRecord)
            {
                case 0:         // Bowl with fire
                    go.transform.localPosition += new Vector3(0, -0.1f, 0);
                    break;
                case 1:         // Campfire
                                // todo
                    break;
                case 2:         // Skull candle
                    go.transform.localPosition += new Vector3(0, 0.1f, 0);
                    break;
                case 3:         // Candle
                    go.transform.localPosition += new Vector3(0, 0.1f, 0);
                    break;
                case 4:         // Candle in bowl
                                // todo
                    break;
                case 5:         // Candleholder with 3 candles
                    go.transform.localPosition += new Vector3(0, 0.15f, 0);
                    break;
                case 6:         // Skull torch
                    go.transform.localPosition += new Vector3(0, 0.6f, 0);
                    break;
                case 7:         // Wooden chandelier with extinguished candles
                                // todo
                    break;
                case 8:         // Turkis lamp
                                // do nothing
                    break;
                case 9:        // Metallic chandelier with burning candles
                    go.transform.localPosition += new Vector3(0, 0.4f, 0);
                    break;
                case 10:         // Metallic chandelier with extinguished candles
                                 // todo
                    break;
                case 11:        // Candle in lamp
                    go.transform.localPosition += new Vector3(0, -0.4f, 0);
                    break;
                case 12:         // Extinguished lamp
                                 // todo
                    break;
                case 13:        // Round lamp (e.g. main lamp in mages guild)
                    go.transform.localPosition += new Vector3(0, -0.35f, 0);
                    break;
                case 14:        // Standing lantern
                    go.transform.localPosition += new Vector3(0, size.y / 2, 0);
                    break;
                case 15:        // Standing lantern round
                    go.transform.localPosition += new Vector3(0, size.y / 2, 0);
                    break;
                case 16:         // Mounted Torch with thin holder
                                 // todo
                    break;
                case 17:        // Mounted torch 1
                    go.transform.localPosition += new Vector3(0, 0.2f, 0);
                    break;
                case 18:         // Mounted Torch 2
                                 // todo
                    break;
                case 19:         // Pillar with firebowl
                                 // todo
                    break;
                case 20:        // Brazier torch
                    go.transform.localPosition += new Vector3(0, 0.6f, 0);
                    break;
                case 21:        // Standing candle
                    go.transform.localPosition += new Vector3(0, size.y / 2.4f, 0);
                    break;
                case 22:         // Round lantern with medium chain
                    go.transform.localPosition += new Vector3(0, -0.5f, 0);
                    break;
                case 23:         // Wooden chandelier with burning candles
                                 // todo
                    break;
                case 24:        // Lantern with long chain
                    go.transform.localPosition += new Vector3(0, -1.85f, 0);
                    break;
                case 25:        // Lantern with medium chain
                    go.transform.localPosition += new Vector3(0, -1.0f, 0);
                    break;
                case 26:        // Lantern with short chain
                                // todo
                    break;
                case 27:        // Lantern with no chain
                    go.transform.localPosition += new Vector3(0, -0.02f, 0);
                    break;
                case 28:        // Street Lantern 1
                                // todo
                    break;
                case 29:        // Street Lantern 2
                    go.transform.localPosition += new Vector3(0, size.y / 2, 0);
                    break;
            }
            switch (textureRecord)
            {
                case 0:         // Bowl with fire
                    light.intensity = 1.2f;
                    light.range = 15f;
                    light.color = new Color32(255, 147, 41, 255);
                    break;
                case 1:         // Campfire
                                // todo
                    break;
                case 2:         // Skull candle
                    light.range /= 3f;
                    light.intensity = 0.6f;
                    light.color = new Color(1.0f, 0.99f, 0.82f);
                    break;
                case 3:         // Candle
                    light.range /= 3f;
                    break;
                case 4:         // Candle with base
                    light.range /= 3f;
                    break;
                case 5:         // Candleholder with 3 candles
                    light.range = 7.5f;
                    light.intensity = 0.33f;
                    light.color = new Color(1.0f, 0.89f, 0.61f);
                    break;
                case 6:         // Skull torch
                    light.range = 15.0f;
                    light.intensity = 0.75f;
                    light.color = new Color(1.0f, 0.93f, 0.62f);
                    break;
                case 7:         // Wooden chandelier with extinguished candles
                                // todo
                    break;
                case 8:         // Turkis lamp
                    light.color = new Color(0.68f, 1.0f, 0.94f);
                    break;
                case 9:        // metallic chandelier with burning candles
                    light.range = 15.0f;
                    light.intensity = 0.65f;
                    light.color = new Color(1.0f, 0.92f, 0.6f);
                    break;
                case 10:         // Metallic chandelier with extinguished candles
                                 // todo
                    break;
                case 11:        // Candle in lamp
                    light.range = 5.0f;
                    light.intensity = 0.5f;
                    break;
                case 12:         // Extinguished lamp
                                 // todo
                    break;
                case 13:        // Round lamp (e.g. main lamp in mages guild)
                    light.range *= 1.2f;
                    light.intensity = 1.1f;
                    light.color = new Color(0.93f, 0.84f, 0.49f);
                    break;
                case 14:        // Standing lantern
                                // todo
                    break;
                case 15:        // Standing lantern round
                                // todo
                    break;
                case 16:         // Mounted Torch with thin holder
                                 // todo
                    break;
                case 17:        // Mounted torch 1
                    light.intensity = 0.8f;
                    light.color = new Color(1.0f, 0.97f, 0.87f);
                    break;
                case 18:         // Mounted Torch 2
                                 // todo
                    break;
                case 19:         // Pillar with firebowl
                                 // todo
                    break;
                case 20:        // Brazier torch
                    light.range = 12.0f;
                    light.intensity = 0.75f;
                    light.color = new Color(1.0f, 0.92f, 0.72f);
                    break;
                case 21:        // Standing candle
                    light.range /= 3f;
                    light.intensity = 0.5f;
                    light.color = new Color(1.0f, 0.95f, 0.67f);
                    break;
                case 22:         // Round lantern with medium chain
                    light.intensity = 1.5f;
                    light.color = new Color(1.0f, 0.95f, 0.78f);
                    break;
                case 23:         // Wooden chandelier with burning candles
                                 // todo
                    break;
                case 24:        // Lantern with long chain
                    light.intensity = 1.4f;
                    light.color = new Color(1.0f, 0.98f, 0.64f);
                    break;
                case 25:        // Lantern with medium chain
                    light.intensity = 1.4f;
                    light.color = new Color(1.0f, 0.98f, 0.64f);
                    break;
                case 26:        // Lantern with short chain
                    light.intensity = 1.4f;
                    light.color = new Color(1.0f, 0.98f, 0.64f);
                    break;
                case 27:        // Lantern with no chain
                    light.intensity = 1.4f;
                    light.color = new Color(1.0f, 0.98f, 0.64f);
                    break;
                case 28:        // Street Lantern 1
                                // todo
                    break;
                case 29:        // Street Lantern 2
                                // todo
                    break;
                default:
                    light.intensity = 1.2f;
                    light.range = 15f;
                    light.color = new Color32(255, 147, 41, 255);
                    break;
            }
        }
        /// <summary>
        /// Add audioSource to animals. Is a modified copy version of a method with the same name, found in RMBLayout.cs
        /// </summary>
        /// <param name="id"></param>
        /// <param name="go"></param>
        public static void AddAnimalAudioSource(int textureRecord, GameObject go)
        {
            DaggerfallAudioSource source = go.AddComponent<DaggerfallAudioSource>();
            source.AudioSource.maxDistance = animalSoundMaxDistance;

            SoundClips sound = SoundClips.None;
            switch (textureRecord)
            {
                case 0:
                case 1:
                    sound = SoundClips.AnimalHorse;
                    break;
                case 3:
                case 4:
                    sound = SoundClips.AnimalCow;
                    break;
                case 5:
                case 6:
                    sound = SoundClips.AnimalPig;
                    break;
                case 7:
                case 8:
                    sound = SoundClips.AnimalCat;
                    break;
                case 9:
                case 10:
                    sound = SoundClips.AnimalDog;
                    break;
                default:
                    sound = SoundClips.None;
                    break;
            }

            source.SetSound(sound, AudioPresets.PlayRandomlyIfPlayerNear);
        }

        /// <summary>
        /// Creates a loot container. Is a modified copy version of a method with the same name, found in GameObjectHelper.cs
        /// </summary>
        /// <param name="billboardPosition"></param>
        /// <param name="parent"></param>
        /// <param name="locationID">48-bits id</param>
        /// <param name="objID"></param>
        /// <param name="textureArchive"></param>
        /// <param name="textureRecord"></param>
        public static GameObject CreateLootContainer(ulong locationID, int objID, int textureArchive, int textureRecord, Transform parent)
        {
            GameObject go = UnityEngine.Object.Instantiate(DaggerfallUnity.Instance.Option_LootContainerPrefab.gameObject);

            // We use our own serializer, get rid of the DFU one
            SerializableLootContainer serializableLootContainer = go.GetComponent<SerializableLootContainer>();
            if (serializableLootContainer)
            {
                UnityEngine.Object.Destroy(serializableLootContainer);
            }

            // Setup DaggerfallLoot component to make lootable
            DaggerfallLoot loot = go.GetComponent<DaggerfallLoot>();
            if (loot)
            {
                loot.LoadID = LocationSaveDataInterface.ToObjectLoadId(locationID, objID);
                loot.WorldContext = WorldContext.Exterior;
                loot.ContainerType = LootContainerTypes.RandomTreasure;
                loot.TextureArchive = textureArchive;
                loot.TextureRecord = textureRecord;

                LocationLootSerializer serializer = go.AddComponent<LocationLootSerializer>();
                if (!serializer.TryLoadSavedData())
                {
                    // We had no existing save, but do not generate loot. Wait for instance activation
                    var billboard = go.GetComponent<Billboard>();
                    if (billboard)
                        billboard.SetMaterial(textureArchive, textureRecord);
                }
            }

            go.transform.parent = parent;

            return go;
        }

        public static void GenerateLoot(DaggerfallLoot loot)
        {
            // Generate loot for Human Stronghold
            if (!LootTables.GenerateLoot(loot, 2))
                Debug.LogError($"[LL] Failed to generate loot for object '{loot.LoadID}'");

            loot.stockedDate = DaggerfallLoot.CreateStockedDate(DaggerfallUnity.Instance.WorldTime.Now);
        }

        const byte Road_N = 128;//0b_1000_0000;
        const byte Road_NE = 64; //0b_0100_0000;
        const byte Road_E = 32; //0b_0010_0000;
        const byte Road_SE = 16; //0b_0001_0000;
        const byte Road_S = 8;  //0b_0000_1000;
        const byte Road_SW = 4;  //0b_0000_0100;
        const byte Road_W = 2;  //0b_0000_0010;
        const byte Road_NW = 1;  //0b_0000_0001;

        /// <summary>
        /// Checks if a location overlaps with a BasicRoad road.
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="locationPrefab"></param>
        /// <param name="pathsDataPoint">Bytefield representing which cardinal directions have roads on the terrain</param>
        /// <returns></returns>
        public static bool OverlapsRoad(RectInt locationRect, byte pathsDataPoint)
        {
            Vector2Int locationTopLeft = new Vector2Int(locationRect.xMin, locationRect.yMax);
            Vector2Int locationTopRight = new Vector2Int(locationRect.xMax, locationRect.yMax);
            Vector2Int locationBottomLeft = new Vector2Int(locationRect.xMin, locationRect.yMin);
            Vector2Int locationBottomRight = new Vector2Int(locationRect.xMax, locationRect.yMin);

            const int TERRAIN_SIZE = LocationLoader.TERRAIN_SIZE;
            const int HALF_TERRAIN_SIZE = LocationLoader.TERRAIN_SIZE / 2;
            const int ROAD_WIDTH = LocationLoader.ROAD_WIDTH;
            const int HALF_ROAD_WIDTH = LocationLoader.ROAD_WIDTH / 2;

            if ((pathsDataPoint & Road_N) != 0)
            {
                if (locationRect.Overlaps(new RectInt(HALF_TERRAIN_SIZE - HALF_ROAD_WIDTH, HALF_TERRAIN_SIZE, ROAD_WIDTH, HALF_TERRAIN_SIZE)))
                    return true;
            }

            if ((pathsDataPoint & Road_E) != 0)
            {
                if (locationRect.Overlaps(new RectInt(HALF_TERRAIN_SIZE, HALF_TERRAIN_SIZE - HALF_ROAD_WIDTH, HALF_TERRAIN_SIZE, ROAD_WIDTH)))
                    return true;
            }

            if ((pathsDataPoint & Road_S) != 0)
            {
                if (locationRect.Overlaps(new RectInt(HALF_TERRAIN_SIZE - HALF_ROAD_WIDTH, 0, ROAD_WIDTH, HALF_TERRAIN_SIZE)))
                    return true;
            }

            if ((pathsDataPoint & Road_W) != 0)
            {
                if (locationRect.Overlaps(new RectInt(0, HALF_TERRAIN_SIZE - HALF_ROAD_WIDTH, HALF_TERRAIN_SIZE, ROAD_WIDTH)))
                    return true;
            }

            if ((pathsDataPoint & Road_NE) != 0)
            {
                // Location can only overlap if anywhere in the top-right quadrant
                if (locationTopRight.x >= HALF_TERRAIN_SIZE && locationTopRight.y >= HALF_TERRAIN_SIZE)
                {
                    float topLeftDiff = locationTopLeft.x - locationTopLeft.y;
                    float bottomRightDiff = locationBottomRight.x - locationBottomRight.y;

                    // Corner overlaps the path
                    if (Mathf.Abs(topLeftDiff) <= HALF_ROAD_WIDTH || Mathf.Abs(bottomRightDiff) <= HALF_ROAD_WIDTH)
                    {
                        return true;
                    }

                    // If corners are on different sides of the path, we have an overlap
                    if (Mathf.Sign(topLeftDiff) != Mathf.Sign(bottomRightDiff))
                    {
                        return true;
                    }
                }
            }

            if ((pathsDataPoint & Road_SE) != 0)
            {
                // Location can only overlap if anywhere in the bottom-right quadrant
                if (locationBottomRight.x >= HALF_TERRAIN_SIZE && locationBottomRight.y <= HALF_TERRAIN_SIZE)
                {
                    float bottomLeftDiff = locationBottomLeft.x + locationBottomLeft.y - TERRAIN_SIZE;
                    float topRightDiff = locationTopRight.x + locationTopRight.y - TERRAIN_SIZE;

                    // Corner overlaps the path
                    if (Mathf.Abs(bottomLeftDiff) <= HALF_ROAD_WIDTH || Mathf.Abs(topRightDiff) <= HALF_ROAD_WIDTH)
                    {
                        return true;
                    }

                    // If corners are on different sides of the path, we have an overlap
                    if (Mathf.Sign(bottomLeftDiff) != Mathf.Sign(topRightDiff))
                    {
                        return true;
                    }
                }
            }

            if ((pathsDataPoint & Road_SW) != 0)
            {
                // Location can only overlap if anywhere in the bottom-left quadrant
                if (locationBottomLeft.x <= HALF_TERRAIN_SIZE && locationBottomLeft.y <= HALF_TERRAIN_SIZE)
                {
                    float topLeftDiff = locationTopLeft.x - locationTopLeft.y;
                    float bottomRightDiff = locationBottomRight.x - locationBottomRight.y;

                    // Corner overlaps the path
                    if (Mathf.Abs(topLeftDiff) <= HALF_ROAD_WIDTH || Mathf.Abs(bottomRightDiff) <= HALF_ROAD_WIDTH)
                    {
                        return true;
                    }

                    // If corners are on different sides of the path, we have an overlap
                    if (Mathf.Sign(topLeftDiff) != Mathf.Sign(bottomRightDiff))
                    {
                        return true;
                    }
                }
            }

            if ((pathsDataPoint & Road_NW) != 0)
            {
                // Location can only overlap if anywhere in the bottom-right quadrant
                if (locationTopLeft.x <= HALF_TERRAIN_SIZE && locationTopLeft.y >= HALF_TERRAIN_SIZE)
                {
                    float bottomLeftDiff = locationBottomLeft.x + locationBottomLeft.y - TERRAIN_SIZE;
                    float topRightDiff = locationTopRight.x + locationTopRight.y - TERRAIN_SIZE;

                    // Corner overlaps the path
                    if (Mathf.Abs(bottomLeftDiff) <= HALF_ROAD_WIDTH || Mathf.Abs(topRightDiff) <= HALF_ROAD_WIDTH)
                    {
                        return true;
                    }

                    // If corners are on different sides of the path, we have an overlap
                    if (Mathf.Sign(bottomLeftDiff) != Mathf.Sign(topRightDiff))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsOutOfBounds(LocationInstance loc, LocationPrefab prefab)
        {
            if (loc.type == 1 || loc.type == 2 || loc.type == 3)
                return false;

            var (halfWidth, halfHeight) = GetHalfDimensions(loc, prefab);

            return loc.terrainX + halfWidth > 128
                || loc.terrainX - halfWidth < 0
                || loc.terrainY + halfHeight > 128
                || loc.terrainY - halfHeight < 0;
        }

        public struct TerrainSection
        {
            public Vector2Int WorldCoord;
            public RectInt Section;

            public void Deconstruct(out Vector2Int coord, out RectInt section)
            {
                coord = WorldCoord;
                section = Section;
            }
        };

        public static List<TerrainSection> GetOverlappingTerrainSections(LocationInstance loc, LocationPrefab locationPrefab, out bool overflow)
        {
            overflow = false;

            var (halfWidth, halfHeight) = GetHalfDimensions(loc, locationPrefab);

            List<TerrainSection> overlappingCoordinates = new List<TerrainSection>();
            // Type 0 and type 2 instances only fit within their own map pixel, but type 1 and 3 can go out of bounds
            if (loc.type == 1 || loc.type == 3)
            {
                int xOffsetMin = (int)Math.Floor((loc.terrainX - halfWidth) / (float)LocationLoader.TERRAIN_SIZE);
                int yOffsetMin = (int)Math.Floor((loc.terrainY - halfHeight) / (float)LocationLoader.TERRAIN_SIZE);
                int xOffsetMax = (loc.terrainX + halfWidth) / LocationLoader.TERRAIN_SIZE;
                int yOffsetMax = (loc.terrainY + halfHeight) / LocationLoader.TERRAIN_SIZE;

                // Check for instance overflow from the bounds of the world
                if (loc.worldX + xOffsetMin < MapsFile.MinMapPixelX)
                {
                    overflow = true;
                    xOffsetMin = MapsFile.MinMapPixelX - loc.worldX;
                }

                if (loc.worldX + xOffsetMax >= MapsFile.MaxMapPixelX)
                {
                    overflow = true;
                    xOffsetMax = MapsFile.MaxMapPixelX - loc.worldX - 1;
                }

                if (loc.worldY - yOffsetMax < MapsFile.MinMapPixelY)
                {
                    overflow = true;
                    yOffsetMax = loc.worldY - MapsFile.MinMapPixelY;
                }

                if (loc.worldY - yOffsetMin >= MapsFile.MaxMapPixelY)
                {
                    overflow = true;
                    yOffsetMin = loc.worldY - MapsFile.MaxMapPixelY - 1;
                }

                // Find all overlapping coordinates and their overlap rectangle
                for (int xOffset = xOffsetMin; xOffset <= xOffsetMax; ++xOffset)
                {
                    for (int yOffset = yOffsetMin; yOffset <= yOffsetMax; ++yOffset)
                    {
                        int xMin = Math.Max(loc.terrainX - halfWidth - xOffset * LocationLoader.TERRAIN_SIZE, 0);
                        int xMax = Math.Min(loc.terrainX + halfWidth - xOffset * LocationLoader.TERRAIN_SIZE, 128);
                        int yMin = Math.Max(loc.terrainY - halfHeight - yOffset * LocationLoader.TERRAIN_SIZE, 0);
                        int yMax = Math.Min(loc.terrainY + halfHeight - yOffset * LocationLoader.TERRAIN_SIZE, 128);

                        overlappingCoordinates.Add(
                            new TerrainSection
                            {
                                WorldCoord = new Vector2Int(loc.worldX + xOffset, loc.worldY - yOffset),
                                Section = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin)
                            });
                    }
                }
            }
            else
            {
                overlappingCoordinates.Add(
                    new TerrainSection
                    {
                        WorldCoord = new Vector2Int(loc.worldX, loc.worldY),
                        Section = new RectInt(loc.terrainX - halfWidth, loc.terrainY - halfHeight, halfWidth * 2, halfHeight * 2)
                    });
            }

            return overlappingCoordinates;
        }

        public static List<TerrainSection> GetOverlappingTerrainSections(LocationInstance loc, LocationPrefab locationPrefab)
        {
            return GetOverlappingTerrainSections(loc, locationPrefab, out bool _);
        }

        // Returns location dimensions in terrain tiles. Takes into account instance scale and rotation
        public static (int, int) GetDimensions(LocationInstance loc, LocationPrefab prefab)
        {
            float rot = Mathf.Deg2Rad * loc.rot.eulerAngles.y;
            float cosRot = Mathf.Cos(rot);
            float sinRot = Mathf.Sin(rot);
            cosRot = Mathf.Abs(cosRot);
            sinRot = Mathf.Abs(sinRot);

            // These functions tend to return 1E-8 values for the usual 90 degree rotations
            // Mathf.Approximately and float.Epsilon won't do for these, so let's do this by hand
            if (cosRot < 0.01f)
                cosRot = 0.0f;

            if (sinRot < 0.01f)
                sinRot = 0.0f;

            if (Mathf.Abs(cosRot - 1.0f) < 0.01f)
                cosRot = 1.0f;

            if (Mathf.Abs(sinRot - 1.0f) < 0.01f)
                sinRot = 1.0f;

            int width = Mathf.CeilToInt(cosRot * loc.scale * prefab.width + sinRot * loc.scale * prefab.height);
            int height = Mathf.CeilToInt(sinRot * loc.scale * prefab.width + cosRot * loc.scale * prefab.height);

            return (width, height);
        }

        // Returns half of the location height in terrain tiles. This takes prefab height and instance scale into account
        // Prefab dimensions are specified in terrain tiles
        // Instances are located with their center between four tiles
        // It does not make sense for a location to have odd numbers on either dimension,
        // we round up to the next number
        // If a prefab has a width of 5, then we consider its half-width to be 3 on both sides
        // If the location has a scale of 0.8, then its effective width is 4, and the half-width will be 2
        public static (int, int) GetHalfDimensions(LocationInstance loc, LocationPrefab prefab)
        {
            var (width, height) = GetDimensions(loc, prefab);

            int halfWidth = (width + 1) / 2;
            int halfHeight = (height + 1) / 2;
            return (halfWidth, halfHeight);
        }
    }
}