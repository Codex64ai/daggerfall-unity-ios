// MOBILE PORT - source: github.com/numidium/dfu-mods @ da1bf32f8a26a82627a16b2b3e72ba0bc5704845
// File DynamicMusic/Scripts/DynamicMusic.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 numidium.
// The LICENSE lives at the repo ROOT, outside DynamicMusic/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wenzil.Console;

namespace DynamicMusic
{
    public sealed class DynamicMusic : MonoBehaviour
    {
        #region Playlists
        private enum MusicPlaylist : byte
        {
            DungeonInterior,
            Sunny,
            Cloudy,
            Overcast,
            Rain,
            Snow,
            Temple,
            Tavern,
            Night,
            Shop,
            MagesGuild,
            Interior,
            Palace,
            Castle,
            Court,
            //Sneaking,
            MainMenu,
            CharCreation,
            None
        }

        private enum MusicEnvironment  : byte
        {
            Castle,
            City,
            DungeonExterior,
            DungeonInterior,
            Graveyard,
            MagesGuild,
            Interior,
            Palace,
            Shop,
            Tavern,
            Temple,
            Wilderness
        }

        private enum State : byte
        {
            Normal,
            FadingOut,
            FadingIn
        }

        // Dungeon
        static SongFiles[] _dungeonSongs = new SongFiles[]
        {
            SongFiles.song_dungeon,
            SongFiles.song_dungeon5,
            SongFiles.song_dungeon6,
            SongFiles.song_dungeon7,
            SongFiles.song_dungeon8,
            SongFiles.song_dungeon9,
            SongFiles.song_gdngn10,
            SongFiles.song_gdngn11,
            SongFiles.song_gdungn4,
            SongFiles.song_gdungn9,
            SongFiles.song_04,
            SongFiles.song_05,
            SongFiles.song_07,
            SongFiles.song_15,
            SongFiles.song_28,
        };

        // Sunny
        static SongFiles[] _sunnySongs = new SongFiles[]
        {
            SongFiles.song_gday___d,
            SongFiles.song_swimming,
            SongFiles.song_gsunny2,
            SongFiles.song_sunnyday,
            SongFiles.song_02,
            SongFiles.song_03,
            SongFiles.song_22,
        };

        // Sunny FM Version
        static SongFiles[] _sunnySongsFM = new SongFiles[]
        {
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
        };

        // Cloudy
        static SongFiles[] _cloudySongs = new SongFiles[]
        {
            SongFiles.song_gday___d,
            SongFiles.song_swimming,
            SongFiles.song_gsunny2,
            SongFiles.song_sunnyday,
            SongFiles.song_02,
            SongFiles.song_03,
            SongFiles.song_22,
            SongFiles.song_29,
            SongFiles.song_12,
        };

        // Cloudy FM
        static SongFiles[] _cloudySongsFM = new SongFiles[]
        {
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
            SongFiles.song_29fm,
            SongFiles.song_12fm,
        };

        // Overcast/Fog
        static SongFiles[] _overcastSongs = new SongFiles[]
        {
            SongFiles.song_29,
            SongFiles.song_12,
            SongFiles.song_13,
            SongFiles.song_gpalac,
            SongFiles.song_overcast,
        };

        // Overcast/Fog FM Version
        static SongFiles[] _overcastSongsFM = new SongFiles[]
        {
            SongFiles.song_29fm,
            SongFiles.song_12fm,
            SongFiles.song_13fm,
            SongFiles.song_fpalac,
            SongFiles.song_fmover_c,
        };

        // Rain
        static SongFiles[] _rainSongs = new SongFiles[]
        {
            SongFiles.song_overlong,        // Long version of overcast
            SongFiles.song_raining,
            SongFiles.song_08,
        };

        // Snow
        static SongFiles[] _snowSongs = new SongFiles[]
        {
            SongFiles.song_20,
            SongFiles.song_gsnow__b,
            SongFiles.song_oversnow,
            SongFiles.song_snowing,         // Not used in classic
        };

        // Sneaking - Not used in classic
        static SongFiles[] _sneakingSongs = new SongFiles[]
        {
            SongFiles.song_gsneak2,
            SongFiles.song_sneaking,
            SongFiles.song_sneakng2,
            SongFiles.song_16,
            SongFiles.song_09,
            SongFiles.song_25,
            SongFiles.song_30,
        };

        // Temple
        static SongFiles[] _templeSongs = new SongFiles[]
        {
            SongFiles.song_ggood,
            SongFiles.song_gbad,
            SongFiles.song_ggood,
            SongFiles.song_gneut,
            SongFiles.song_gbad,
            SongFiles.song_ggood,
            SongFiles.song_gbad,
            SongFiles.song_gneut,
        };

        // Tavern
        static SongFiles[] _tavernSongs = new SongFiles[]
        {
            SongFiles.song_square_2,
            SongFiles.song_tavern,
            SongFiles.song_folk1,
            SongFiles.song_folk2,
            SongFiles.song_folk3,
        };

        // Night
        static SongFiles[] _nightSongs = new SongFiles[]
        {
            SongFiles.song_10,
            SongFiles.song_11,
            SongFiles.song_gcurse,
            SongFiles.song_geerie,
            SongFiles.song_gruins,
            SongFiles.song_18,
            SongFiles.song_21,          // For general midi song_10 is duplicated here in Daggerfall classic, although song_21fm is used in FM mode.
        };

        // Dungeon FM version
        static SongFiles[] _dungeonSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_dngn1,
            SongFiles.song_fm_dngn1,
            SongFiles.song_fm_dngn2,
            SongFiles.song_fm_dngn3,
            SongFiles.song_fm_dngn4,
            SongFiles.song_fm_dngn5,
            SongFiles.song_fdngn10,
            SongFiles.song_fdngn11,
            SongFiles.song_fdungn4,
            SongFiles.song_fdungn9,
            SongFiles.song_04fm,
            SongFiles.song_05fm,
            SongFiles.song_07fm,
            SongFiles.song_15fm,
            SongFiles.song_15fm,
        };

        // Day FM version
        static SongFiles[] _daySongsFM = new SongFiles[]
        {
            SongFiles.song_fday___d,
            SongFiles.song_fm_swim2,
            SongFiles.song_fm_sunny,
            SongFiles.song_02fm,
            SongFiles.song_03fm,
            SongFiles.song_22fm,
            SongFiles.song_29fm,
            SongFiles.song_12fm,
            SongFiles.song_13fm,
            SongFiles.song_fpalac,
        };

        // Weather - Raining FM version
        static SongFiles[] _weatherRainSongsFM = new SongFiles[]
        {
            SongFiles.song_fmover_c,
            SongFiles.song_fm_rain,
            SongFiles.song_08fm,
        };

        // Weather - Snowing FM version
        static SongFiles[] _weatherSnowSongsFM = new SongFiles[]
        {
            SongFiles.song_20fm,
            SongFiles.song_fsnow__b,
            SongFiles.song_fmover_s,
        };

        // Sneaking FM version
        static SongFiles[] _sneakingSongsFM = new SongFiles[]
        {
            SongFiles.song_fsneak2,
            SongFiles.song_fmsneak2,        // Used in Arena when trespassing in homes
            SongFiles.song_fsneak2,
            SongFiles.song_16fm,
            SongFiles.song_09fm,
            SongFiles.song_25fm,
            SongFiles.song_30fm,
        };

        // Temple FM version
        static SongFiles[] _templeSongsFM = new SongFiles[]
        {
            SongFiles.song_fgood,
            SongFiles.song_fbad,
            SongFiles.song_fgood,
            SongFiles.song_fneut,
            SongFiles.song_fbad,
            SongFiles.song_fgood,
            SongFiles.song_fbad,
            SongFiles.song_fneut,
        };

        // Tavern FM version
        static SongFiles[] _tavernSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_sqr_2,
        };

        // Night FM version
        static SongFiles[] _nightSongsFM = new SongFiles[]
        {
            SongFiles.song_11fm,
            SongFiles.song_fcurse,
            SongFiles.song_feerie,
            SongFiles.song_fruins,
            SongFiles.song_18fm,
            SongFiles.song_21fm,
        };

        // Unused dungeon music
        static SongFiles[] _unusedDungeonSongs = new SongFiles[]
        {
            SongFiles.song_d1,
            SongFiles.song_d2,
            SongFiles.song_d3,
            SongFiles.song_d4,
            SongFiles.song_d5,
            SongFiles.song_d6,
            SongFiles.song_d7,
            SongFiles.song_d8,
            SongFiles.song_d9,
            SongFiles.song_d10,
        };

        // Unused dungeon music FM version
        static SongFiles[] _unusedDungeonSongsFM = new SongFiles[]
        {
            SongFiles.song_d1fm,
            SongFiles.song_d2fm,
            SongFiles.song_d3fm,
            SongFiles.song_d4fm,
            SongFiles.song_d5fm,
            SongFiles.song_d6fm,
            SongFiles.song_d7fm,
            SongFiles.song_d8fm,
            SongFiles.song_d9fm,
            SongFiles.song_d10fm,
        };

        // Shop
        static SongFiles[] _shopSongs = new SongFiles[]
        {
            SongFiles.song_gshop,
        };

        // Shop FM version
        static SongFiles[] _shopSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_sqr_2,
        };

        // Mages Guild
        static SongFiles[] _magesGuildSongs = new SongFiles[]
        {
            SongFiles.song_gmage_3,
            SongFiles.song_magic_2,
        };

        // Mages Guild FM version
        static SongFiles[] _magesGuildSongsFM = new SongFiles[]
        {
            SongFiles.song_fm_nite3,
        };

        // Interior
        static SongFiles[] _interiorSongs = new SongFiles[]
        {
            SongFiles.song_23,
        };

        // Interior FM version
        static SongFiles[] _interiorSongsFM = new SongFiles[]
        {
            SongFiles.song_23fm,
        };

        // Not used in classic. There is unused code to play it in knightly orders
        static SongFiles[] _unusedKnightSong = new SongFiles[]
        {
            SongFiles.song_17,
        };

        // FM version of above
        static SongFiles[] _unusedKnightSongFM = new SongFiles[]
        {
            SongFiles.song_17fm,
        };

        // Palace
        static SongFiles[] _palaceSongs = new SongFiles[]
        {
            SongFiles.song_06,
        };

        // Palace FM version
        static SongFiles[] _palaceSongsFM = new SongFiles[]
        {
            SongFiles.song_06fm,
        };

        // Castle
        static SongFiles[] _castleSongs = new SongFiles[]
        {
            SongFiles.song_gpalac,
        };

        // Castle FM Version
        static SongFiles[] _castleSongsFM = new SongFiles[]
        {
            SongFiles.song_fpalac,
        };

        // Court
        static SongFiles[] _courtSongs = new SongFiles[]
        {
            SongFiles.song_11,
        };

        // Court FM Version
        static SongFiles[] _courtSongsFM = new SongFiles[]
        {
            SongFiles.song_11fm,
        };

        public SongFiles[] DungeonInteriorSongs = _dungeonSongs;
        public SongFiles[] SunnySongs = _sunnySongs;
        public SongFiles[] CloudySongs = _cloudySongs;
        public SongFiles[] OvercastSongs = _overcastSongs;
        public SongFiles[] RainSongs = _rainSongs;
        public SongFiles[] SnowSongs = _snowSongs;
        public SongFiles[] TempleSongs = _templeSongs;
        public SongFiles[] TavernSongs = _tavernSongs;
        public SongFiles[] NightSongs = _nightSongs;
        public SongFiles[] ShopSongs = _shopSongs;
        public SongFiles[] MagesGuildSongs = _magesGuildSongs;
        public SongFiles[] InteriorSongs = _interiorSongs;
        public SongFiles[] PalaceSongs = _palaceSongs;
        public SongFiles[] CastleSongs = _castleSongs;
        public SongFiles[] CourtSongs = _courtSongs;
        public SongFiles[] SneakingSongs = _sneakingSongs;
        #endregion Playlists

        /// <summary>
        /// Holds a list of strings that represent music tracks. Advances track and auto shuffles.
        /// </summary>
        private sealed class Playlist
        {
            public enum Flags : byte
            {
                None = 0x00,
                CrashIn = 0x01,
                ResumePrevious = 0x02,
                PlayUntilCombatEnd = 0x04,
                Sting = 0x08
            }

            private readonly string[] tracks;
            private int index = 0;
            private void ShuffleTracks()
            {
                if (tracks.Length < 3)
                    return;
                var endTrack = tracks[tracks.Length - 1];
                // Fisher-Yates shuffle
                for (var i = tracks.Length - 1; i > 0; i--)
                {
                    var randIndex = UnityEngine.Random.Range(0, i + 1);
                    (tracks[randIndex], tracks[i]) = (tracks[i], tracks[randIndex]);
                }

                // Ensure that 0 doesn't swap with n-1. Otherwise there will be a repeat on re-shuffle.
                if (tracks[0] == endTrack)
                    (tracks[0], tracks[tracks.Length - 1]) = (tracks[tracks.Length - 1], tracks[0]);
            }

            public Playlist(List<string> trackList)
            {
                tracks = trackList.ToArray();
                ShuffleTracks();
            }

            public string GetNextTrack()
            {
                index = (index + 1) % tracks.Length;
                if (index == 0)
                    ShuffleTracks();
                return tracks[index];
            }

            public static Flags GetFlagsFromText(string text)
            {
                var flagText = text.ToLower();
                var flagValues = (Flags[])Enum.GetValues(typeof(Flags));
                for (var i = 0; i < flagValues.Length; i++)
                {
                    if (flagValues[i].ToString().ToLower() == flagText)
                        return flagValues[i];
                }

                return Flags.None;
            }

            public int TrackCount => tracks.Length;
            public string CurrentTrack => tracks[index];
            public Flags PlaylistFlags;
            public bool HasFlags(Flags flags) => (PlaylistFlags & flags) == flags;
        }

        private sealed class ConditionUsage
        {
            public enum Conditions : UInt16
            {
                None,
                Night,
                Interior,
                Dungeon,
                DungeonCastle,
                LocationType,
                BuildingType,
                WeatherType,
                FactionId,
                Climate,
                ClimateIndex,
                RegionIndex,
                DungeonType,
                BuildingQuality,
                Season,
                Month,
                StartMenu,
                ReadingBook,
                Combat,
                Swimming,
                BuildingIsOpen,
                FastTraveled
            }

            public static Conditions GetConditionFromText(string text)
            {
                var conditionText = text.ToLower();
                var conditionValues = (Conditions[])Enum.GetValues(typeof(Conditions));
                for (var i = 0; i < conditionValues.Length; i++)
                {
                    if (conditionValues[i].ToString().ToLower() == conditionText)
                        return conditionValues[i];
                }

                return Conditions.None;
            }

            public bool NegateArg { get; set; }
            public int[] ParameterArgs { get; set; }
            public Conditions Condition { get; set; }
        }

        public static DynamicMusic Instance { get; private set; }
        private static Mod mod;
        private DaggerfallUnity daggerfallUnity;
        private PlayerGPS localPlayerGPS;
        private PlayerEnterExit playerEnterExit;
        private PlayerWeather playerWeather;
        private DynamicSongPlayer dynamicSongPlayer;
        private GameManager gameManager;
        private PlayerEntity playerEntity;
        private const float detectionCheckInterval = 3f;
        private float detectionCheckDelta;
        private const float fadeOutLength = 2f;
        private const float fadeInLength = 2f;
        private float fadeOutTime;
        private float fadeInTime;
        private const byte combatTaperLength = 2;
        private byte combatTaper;
        private Playlist[] customPlaylists;
        private Dictionary<int, ConditionUsage[]> userDefinedConditionSets;
        private string currentCustomTrack;
        private bool customTrackQueued;
        private bool combatMusicIsEnabled;
        private bool isInCombat;
        private int maxEnemyLevel;
        private bool resumeIsEnabled = true;
        private bool loopCustomTracks;
        private float previousTimeSinceStartup;
        private float deltaTime;
        private float resumeSeeker = 0f;
        private float stingWaitTime;
        private float stingDelay = 1f;
        private int resumePlaylist;
        private MusicPlaylist lastVanillaPlaylist;
        private bool gameLoaded;
        private int currentPlaylist;
        private bool isWaitingForTravelSting;
        private bool isPlayingSting;
        private int lastlocationIndex;
        private uint lastGameDays;
        private State currentState;
        private string debugPlaylistName;
        private string debugSongName;
        private GUIStyle guiStyle;

        private const string fileSearchPattern = "*.ogg";
        private const string modSignature = "Dynamic Music";

        // MOBILE: [Invoke] removed - MobilePortedMods calls this Init when the launcher entry is on.
        // Installed is what its four-argument StartOne asks: Init can decline (no mod entry) by logging
        // and returning, the way the other ports do, rather than by throwing.

        /// <summary>True once Init created the mod object.</summary>
        public static bool Installed { get; private set; }

        public static void Init(InitParams initParams)
        {
            if (Installed)
                return;

            mod = initParams.Mod;
            if (mod == null)
            {
                Debug.Log("[DynamicMusic] not available: no mod entry (the bundle carries modsettings.json)");
                return;
            }

            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<DynamicMusic>();
            Instance.dynamicSongPlayer = go.AddComponent<DynamicSongPlayer>();
            Instance.dynamicSongPlayer.ModSignature = modSignature;
            DontDestroyOnLoad(go);
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            StartGameBehaviour.OnStartGame += StartGameBehaviour_OnStartGame;
            DaggerfallTravelPopUp.OnPostFastTravel += OnPostFastTravel;
            mod.LoadSettingsCallback = Instance.LoadSettings;
            DynamicSongPlayer.OnSongEnd += OnSongEnd;
            Installed = true;
        }

        // Load settings that can change during runtime.
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            combatMusicIsEnabled = settings.GetValue<bool>("Options", "Enable Combat Music");
            resumeIsEnabled = settings.GetValue<bool>("Options", "Enable Track Resume");
            loopCustomTracks = settings.GetValue<bool>("Options", "Loop Custom Tracks");
        }

        // MOBILE: upstream's Start reaches straight into the scene - GameManager.Instance.DungeonParent
        // and its two siblings, to destroy the vanilla SongPlayer objects. MobilePortedMods starts the
        // compiled-in mods at the TITLE on a player build, where those parents do not exist yet, and a
        // null there would take the whole mod down before it ever read a playlist. So Start waits and
        // then runs upstream's body unchanged.
        private void Start()
        {
            StartCoroutine(DeferredStart());
        }

        private System.Collections.IEnumerator DeferredStart()
        {
            while (!GameManager.HasInstance
                || GameManager.Instance.DungeonParent == null
                || GameManager.Instance.InteriorParent == null
                || GameManager.Instance.ExteriorParent == null
                || GameManager.Instance.PlayerGPS == null)
                yield return null;

            daggerfallUnity = DaggerfallUnity.Instance;
            // Load settings that require a restart.
            var settings = mod.GetSettings();
            LoadSettings(settings, new ModSettingsChange());

            const string soundDirectory = "Sound";
            const string baseDirectory = "DynMusic";
            // MOBILE: StreamingAssets is INSIDE the signed .app on iOS and is read-only - the
            // Directory.CreateDirectory below would throw, in Start, killing the mod. MobileContentPath
            // already owns this question for the rest of the port: it maps a StreamingAssets path onto
            // the writable Documents container when one exists there. Custom tracks are therefore
            // dropped into Documents/Sound/DynMusic/<Playlist>/ on the device, which is the only place
            // a player can put a file anyway.
            var basePath = DaggerfallWorkshop.Game.Mobile.MobileContentPath.Override(
                Path.Combine(Application.streamingAssetsPath, soundDirectory, baseDirectory));
            var userDefinedPlaylists = new List<Playlist>();

            // Load user-defined playlists from disk (user-defined meaning the conditions and tracks are custom).
            const string userPlaylistsFileName = "UserDefined.txt";
            var filePath = Path.Combine(basePath, userPlaylistsFileName);
            if (Directory.Exists(basePath) && File.Exists(filePath))
            {
                using (var file = new StreamReader(filePath))
                {
                    userDefinedConditionSets = new Dictionary<int, ConditionUsage[]>();
                    ushort lineCounter = 0;
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        line = line.Trim(); // Remove whitespace to the left and right.
                        lineCounter++;
                        if (line == string.Empty || line[0] == '#') // # = Comment/memo line, ignore.
                            continue;
                        // Get track names from directory.
                        List<string> trackList;
                        var lineContainsError = false;
                        string[] flagTokens = null;
                        const char flagSeparator = '|';
                        if (line.Split(flagSeparator).Length > 1)
                            flagTokens = line.Split(flagSeparator)[1].Split(' ', ',');
                        var conditionTokens = line.Split(flagSeparator)[0].Split(' ', ',');
                        var playlistName = conditionTokens[0];
                        if (!Directory.Exists(Path.Combine(basePath, playlistName)))
                        {
                            PrintParserError($"Reference to non-existent playlist directory", lineCounter, playlistName);
                            continue; // Throw out this line.
                        }
                        else
                        {
                            var files = Directory.GetFiles(Path.Combine(basePath, playlistName), fileSearchPattern);
                            trackList = new List<string>();
                            if (files.Length > 0)
                            {
                                foreach (var fileName in files)
                                    trackList.Add(fileName);
                                // Add playlist.
                                userDefinedPlaylists.Add(new Playlist(trackList));
                            }
                        }

                        // Parse conditional tokens on current line.
                        var playlistKey = (int)MusicPlaylist.None + userDefinedPlaylists.Count;
                        var conditionSet = new List<ConditionUsage>();
                        var tokenIndex = 2;
                        while (tokenIndex < conditionTokens.Length)
                        {
                            var negate = false;
                            if (conditionTokens[tokenIndex].ToLower() == "not")
                            {
                                tokenIndex++;
                                negate = true;
                            }

                            var condition = ConditionUsage.GetConditionFromText(conditionTokens[tokenIndex]);
                            if (condition == ConditionUsage.Conditions.None)
                            {
                                PrintParserError($"Unrecognized condition", lineCounter, conditionTokens[tokenIndex]);
                                lineContainsError = true;
                                break;
                            }

                            tokenIndex++;
                            if (!lineContainsError)
                            {
                                var arguments = new List<int>();
                                while (tokenIndex < conditionTokens.Length && conditionTokens[tokenIndex] != "")
                                {
                                    if (!int.TryParse(conditionTokens[tokenIndex++], out var result))
                                    {
                                        PrintParserError($"Invalid argument", lineCounter, condition.ToString());
                                        lineContainsError = true;
                                        break;
                                    }

                                    arguments.Add(result);
                                }

                                var conditionUsage = new ConditionUsage()
                                {
                                    NegateArg = negate,
                                    ParameterArgs = arguments.ToArray(),
                                    Condition = condition
                                };

                                conditionSet.Add(conditionUsage);
                            }

                            tokenIndex++;
                        }

                        tokenIndex = 0;
                        if (flagTokens != null)
                        {
                            while (tokenIndex < flagTokens.Length)
                                userDefinedPlaylists[userDefinedPlaylists.Count - 1].PlaylistFlags |= Playlist.GetFlagsFromText(flagTokens[tokenIndex++]);
                        }

                        if (lineContainsError)
                            continue; // Don't tolerate errors.
                        userDefinedConditionSets[playlistKey] = conditionSet.ToArray();
                    }
                }
            }

            // Load custom playlists from disk.
            customPlaylists = new Playlist[(int)MusicPlaylist.None + userDefinedPlaylists.Count + 1];
            for (var i = 0; i < (int)MusicPlaylist.None + 1; i++)
            {
                var playlistPath = Path.Combine(basePath, $"{(MusicPlaylist)i}");
                if (!Directory.Exists(playlistPath))
                {
                    // MOBILE: do NOT create it. Upstream makes 30-odd empty folders on first run as a
                    // convenience for a desktop user with a file manager; here the parent may be inside
                    // the read-only app bundle, where the call throws, and on the device side an empty
                    // folder tells a player nothing they can see. A missing folder simply means no
                    // custom tracks for that playlist, which is what "continue" already meant.
                    continue;
                }

                var files = Directory.GetFiles(playlistPath, fileSearchPattern);
                if (files.Length == 0)
                    continue;
                var trackList = new List<string>();
                foreach (var fileName in files)
                    trackList.Add(fileName);
                customPlaylists[i] = new Playlist(trackList);
            }

            // Append user defined playlists to custom playlists.
            var j = 0;
            for (var i = (int)MusicPlaylist.None + 1; j < userDefinedPlaylists.Count; i++)
                customPlaylists[i] = userDefinedPlaylists[j++];

            // Remove vanilla song players from scene.
            const string songPlayerName = "SongPlayer";
            gameManager = GameManager.Instance;
            Destroy(gameManager.DungeonParent.transform.Find(songPlayerName).gameObject);
            Destroy(gameManager.InteriorParent.transform.Find(songPlayerName).gameObject);
            Destroy(gameManager.ExteriorParent.transform.Find(songPlayerName).gameObject);
            // Replace with dummy objects of same name (for compatibility).
            var dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.DungeonParent.transform;
            dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.InteriorParent.transform;
            dummySongPlayer = new GameObject(songPlayerName);
            dummySongPlayer.transform.parent = gameManager.ExteriorParent.transform;

            // Set references for quick access.
            playerEntity = gameManager.PlayerEntity;
            localPlayerGPS = gameManager.PlayerGPS;
            playerEnterExit = localPlayerGPS.GetComponent<PlayerEnterExit>();
            playerWeather = localPlayerGPS.GetComponent<PlayerWeather>();

            // Set timing variables and state.
            previousTimeSinceStartup = Time.realtimeSinceStartup;
            gameLoaded = false;
            currentState = State.FadingIn;
            fadeInTime = fadeInLength;
            currentPlaylist = (int)MusicPlaylist.None;

            // Use alternate music if set.
            if (DaggerfallUnity.Settings.AlternateMusic)
            {
                DungeonInteriorSongs = _dungeonSongsFM;
                SunnySongs = _sunnySongsFM;
                CloudySongs = _cloudySongsFM;
                OvercastSongs = _overcastSongsFM;
                RainSongs = _weatherRainSongsFM;
                SnowSongs = _weatherSnowSongsFM;
                TempleSongs = _templeSongsFM;
                TavernSongs = _tavernSongsFM;
                NightSongs = _nightSongsFM;
                ShopSongs = _shopSongsFM;
                MagesGuildSongs = _magesGuildSongsFM;
                InteriorSongs = _interiorSongsFM;
                PalaceSongs = _palaceSongsFM;
                CastleSongs = _castleSongsFM;
                CourtSongs = _courtSongsFM;
                SneakingSongs = _sneakingSongsFM;
            }

            // Setup events.
            PlayerEnterExit.OnTransitionInterior += OnTransitionInterior;
            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionDungeonExterior;
            playerEntity.OnDeath += OnDeath;
            guiStyle = new GUIStyle();
            guiStyle.normal.textColor = Color.black;
            Debug.Log($"{modSignature} initialized.");
            Debug.Log("[DynamicMusic] started: vanilla SongPlayer replaced, "
                + $"{CustomPlaylistCount()} custom playlist(s) found under {basePath}");
            mod.IsReady = true;
        }

        private void Update()
        {
            // Use custom delta time so that time continues adding on pause.
            var realTimeSinceStartup = Time.realtimeSinceStartup;
            deltaTime = realTimeSinceStartup - previousTimeSinceStartup;
            previousTimeSinceStartup = realTimeSinceStartup;
            if (deltaTime < 0f)
                deltaTime = 0f;

            if (isPlayingSting && dynamicSongPlayer.IsStoppedClip && dynamicSongPlayer.IsStinging)
            {
                dynamicSongPlayer.IsStinging = false;
                isPlayingSting = false;
                currentState = State.FadingOut;
                fadeOutTime = fadeOutLength;
            }

            var previousPlaylist = currentPlaylist;
            if (!isPlayingSting && (!isInCombat || customPlaylists[previousPlaylist] == null || customPlaylists[previousPlaylist].HasFlags(Playlist.Flags.PlayUntilCombatEnd)))
            {
                currentPlaylist = (int)GetMusicPlaylist(localPlayerGPS, playerEnterExit, playerWeather);

                // Check if conditions match any user-defined condition sets.
                if (currentPlaylist != (int)MusicPlaylist.None)
                {
                    var key = GetUserDefinedPlaylistKey(userDefinedConditionSets);
                    if (key >= 0) {
                        currentPlaylist = key;
                        isPlayingSting = customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.Sting);
                    }
                }
            }

            if (stingWaitTime > 0f)
                stingWaitTime -= deltaTime;
            else
                isWaitingForTravelSting = false;
            switch (currentState)
            {
                case State.Normal:
                    if (currentPlaylist != previousPlaylist && previousPlaylist != (int)MusicPlaylist.None)
                    {
                        if (customPlaylists[currentPlaylist] != null && 
                            customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.CrashIn))
                        {
                            currentState = State.FadingOut;
                            fadeOutTime = fadeOutLength;
                            fadeInTime = fadeInLength;
                            if (customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.ResumePrevious))
                                resumePlaylist = previousPlaylist;
                        }
                        else
                            currentState = State.FadingOut;
                    }

                    if (currentState != State.FadingOut && customPlaylists[currentPlaylist] != null)
                    {
                        if (customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.Sting) && !dynamicSongPlayer.IsStinging)
                        {
                            PlayCurrentTrack();
                            dynamicSongPlayer.IsStinging = true;
                        }
                        else if (!customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.Sting))
                        {
                            PlayCurrentTrack();
                        }
                    }

                    // Stop music if no playlist found.
                    if (currentPlaylist == (int)MusicPlaylist.None)
                    {
                        if (dynamicSongPlayer.IsPlaying)
                        {
                            currentCustomTrack = string.Empty;
                            dynamicSongPlayer.Stop();
                        }

                        break;
                    }

                    dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    break;
                case State.FadingOut:
                    fadeOutTime += deltaTime;
                    dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(DaggerfallUnity.Settings.MusicVolume, 0f, fadeOutTime / fadeOutLength);
                    if (fadeOutTime >= fadeOutLength)
                    {
                        // End fade when time elapsed.
                        fadeOutTime = 0f;
                        currentState = State.FadingIn;
                    }

                    break;
                case State.FadingIn:
                    if (currentPlaylist != previousPlaylist && previousPlaylist != (int)MusicPlaylist.None)
                    {
                        if (customPlaylists[currentPlaylist] != null 
                            && customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.CrashIn))
                        {
                            currentState = State.FadingOut;
                            fadeOutTime = fadeOutLength;
                            fadeInTime = fadeInLength;
                            break;
                        }
                        else
                        {
                            currentState = State.FadingOut;
                            fadeInTime = 0f;
                            break;
                        }
                    }

                    if (customPlaylists[currentPlaylist] != null && customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.Sting))
                        fadeInTime = fadeInLength;
                    if (currentPlaylist == (int)MusicPlaylist.None)
                        break;
                    if (dynamicSongPlayer.AudioSource.volume == 0f) 
                        PlayCurrentTrack();
                    fadeInTime += deltaTime;
                    // End fade when time elapsed.
                    if (fadeInTime >= fadeInLength)
                    {
                        fadeInTime = 0f;
                        currentState = State.Normal;
                        // Syncing the volume here instead of waiting for the next frame prevents audio hiccup.
                        dynamicSongPlayer.AudioSource.volume = DaggerfallUnity.Settings.MusicVolume;
                    }
                    else
                        dynamicSongPlayer.AudioSource.volume = Mathf.Lerp(0f, DaggerfallUnity.Settings.MusicVolume, fadeInTime / fadeInLength);
                    break;
            }

            // Check if player is in combat at every interval.
            detectionCheckDelta += Time.deltaTime;
            if (detectionCheckDelta < detectionCheckInterval)
                return;
            if (currentState == State.Normal)
            {
                if (combatMusicIsEnabled && !gameManager.PlayerDeath.DeathInProgress && !playerEntity.Arrested && GetCombatStatus(out maxEnemyLevel))
                {
                    isInCombat = true;
                    combatTaper = combatTaperLength;
                }
                else if (combatTaper == 0 || --combatTaper <= 0)
                    isInCombat = false;
            }

            detectionCheckDelta = 0f;
        }

        private void OnGUI()
        {
            if (Event.current.type.Equals(EventType.Repaint) && DefaultCommands.showDebugStrings)
            {
                var playing = dynamicSongPlayer.IsPlaying ? "Playing" : "Stopped";
                var text = $"Dynamic Music - {playing} - State: {currentState} - Playlist: {debugPlaylistName} - Song: {debugSongName}";
                GUI.Label(new Rect(10, 50, 800, 24), text, guiStyle);
                GUI.Label(new Rect(8, 48, 800, 24), text);
            }
        }

        private bool GetIsConditionTrue(ConditionUsage.Conditions condition, bool negate, int[] parameters)
        {
            var conditionResult = false;
            // Vanilla conditions:
            if (condition == ConditionUsage.Conditions.Night)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && daggerfallUnity.WorldTime.Now.IsNight;
            }
            else if (condition == ConditionUsage.Conditions.Interior)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInside;
            }
            else if (condition == ConditionUsage.Conditions.Dungeon)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInsideDungeon;
            }
            else if (condition == ConditionUsage.Conditions.DungeonCastle)
            {
                conditionResult = gameManager.StateManager.CurrentState != StateManager.StateTypes.Start && playerEnterExit.IsPlayerInsideDungeonCastle;
            }
            else if (condition == ConditionUsage.Conditions.LocationType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start || playerEnterExit.IsPlayerInsideDungeon || !localPlayerGPS.IsPlayerInLocationRect) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentLocationType == (DFRegion.LocationTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.BuildingType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.BuildingType == (DFLocation.BuildingTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.WeatherType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerWeather.WeatherType == (DaggerfallWorkshop.Game.Weather.WeatherType)parameter;
            }
            else if (condition == ConditionUsage.Conditions.FactionId)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.FactionID == parameter;
            }
            // Non-vanilla conditions:
            else if (condition == ConditionUsage.Conditions.Climate)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                // TODO: make sure parameter is valid type and catch if not
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.ClimateSettings.ClimateType == (DFLocation.ClimateBaseType)parameter;
            }
            else if (condition == ConditionUsage.Conditions.ClimateIndex)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentClimateIndex == parameter;
            }
            else if (condition == ConditionUsage.Conditions.RegionIndex)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= localPlayerGPS.CurrentRegionIndex == parameter;
            }
            else if (condition == ConditionUsage.Conditions.DungeonType)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= (playerEnterExit.Dungeon != null) && playerEnterExit.Dungeon.Summary.DungeonType == (DFRegion.DungeonTypes)parameter;
            }
            else if (condition == ConditionUsage.Conditions.BuildingQuality)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= playerEnterExit.BuildingDiscoveryData.quality == parameter;
            }
            else if (condition == ConditionUsage.Conditions.Season)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= gameManager.StreamingWorld.CurrentPlayerLocationObject.CurrentSeason == (ClimateSeason)parameter;
            }
            else if (condition == ConditionUsage.Conditions.Month)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                foreach (var parameter in parameters)
                    conditionResult |= DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.MonthOfYear == parameter;
            }
            else if (condition == ConditionUsage.Conditions.StartMenu)
            {
                conditionResult = gameManager.StateManager.CurrentState == StateManager.StateTypes.Start;
            }
            else if (condition == ConditionUsage.Conditions.Combat)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                conditionResult = isInCombat;
                if (parameters.Length > 0)
                    conditionResult &= maxEnemyLevel - playerEntity.Level >= parameters[0];
            }
            else if (condition == ConditionUsage.Conditions.Swimming)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                conditionResult = playerEnterExit.IsPlayerSwimming;
            }
            else if (condition == ConditionUsage.Conditions.BuildingIsOpen)
            {
                if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start) return false;
                if (playerEnterExit.Interior != null && (int)playerEnterExit.Interior.BuildingData.BuildingType >= PlayerActivate.openHours.Length)
                    conditionResult = true;
                else
                    conditionResult = playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon
                        && PlayerActivate.IsBuildingOpen(playerEnterExit.Interior.BuildingData.BuildingType);
            }
            else if (condition == ConditionUsage.Conditions.FastTraveled)
            {
                conditionResult = isWaitingForTravelSting;
            }

            return negate ? !conditionResult : conditionResult;
        }

        private int GetUserDefinedPlaylistKey(Dictionary<int, ConditionUsage[]> conditionSets)
        {
            foreach (var key in conditionSets.Keys)
            {
                var eval = true;
                foreach (var condition in conditionSets[key])
                {
                    eval &= GetIsConditionTrue(condition.Condition, condition.NegateArg, condition.ParameterArgs);
                    if (!eval)
                        break;
                }

                if (eval)
                {
                    return key;
                }
            }

            return -1;
        }

        private void PrintParserError(string text, ushort lineNumber, string token)
        {
            Debug.Log($"{modSignature} user-defined playlist: {text} at line {lineNumber}: {token}");
        }

        private void HandleLocationChange()
        {
            combatTaper = 0;
        }

        private void PlayCurrentTrack()
        {
            var gameDays = daggerfallUnity.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() / 1440;
            var song = dynamicSongPlayer.Song;
            var currentCustomPlaylist = customPlaylists[currentPlaylist] != null ? currentPlaylist : (int)MusicPlaylist.None;
            var isUsingCustomPlaylist = currentCustomPlaylist != (int)MusicPlaylist.None;
            // Plays random tracks continuously as long as custom tracks are available for the current context.
            // Plays one random custom track on loop if custom track looping is enabled.
            if (isUsingCustomPlaylist &&
                (currentCustomTrack != customPlaylists[currentPlaylist].CurrentTrack || customTrackQueued)) // Changed to a different custom track.
            {
                var playlist = customPlaylists[currentCustomPlaylist];
                var track = resumeSeeker > 0f || (loopCustomTracks && currentCustomTrack == customPlaylists[currentPlaylist].CurrentTrack) ? playlist.CurrentTrack : playlist.GetNextTrack();
                GetDebuggingText(track, out debugPlaylistName, out debugSongName, currentPlaylist > (int)MusicPlaylist.None);
                if (resumeIsEnabled && customPlaylists[currentPlaylist].HasFlags(Playlist.Flags.ResumePrevious))
                {
                    resumeSeeker = dynamicSongPlayer.CurrentSecond;
                    dynamicSongPlayer.Play(track, 0f);
                }
                else
                {
                    if (currentCustomPlaylist != resumePlaylist)
                        resumeSeeker = 0f;
                    dynamicSongPlayer.Play(track, resumeSeeker);
                    resumeSeeker = 0f;
                }

                currentCustomTrack = playlist.CurrentTrack;
                dynamicSongPlayer.Song = SongFiles.song_none;
                lastVanillaPlaylist = MusicPlaylist.None;
                customTrackQueued = false;
            }
            // Loop the music as usual if no custom soundtracks are found.
            else if (currentCustomPlaylist == (int)MusicPlaylist.None)
            {
                if (gameDays != lastGameDays || localPlayerGPS.CurrentLocationIndex != lastlocationIndex || (int)lastVanillaPlaylist != currentPlaylist)
                    song = GetSong((MusicPlaylist)currentPlaylist);
                if (song == dynamicSongPlayer.Song)
                {
                    lastGameDays = gameDays;
                    lastlocationIndex = localPlayerGPS.CurrentLocationIndex;
                    lastVanillaPlaylist = (MusicPlaylist)currentPlaylist;
                    return;
                }

                GetDebuggingText(song, out debugPlaylistName, out debugSongName);
                if (currentPlaylist != resumePlaylist)
                    resumeSeeker = 0f;
                dynamicSongPlayer.Play(song, resumeSeeker);
                LogTrackChoice(currentPlaylist, song);   // MOBILE: see LogTrackChoice
                resumeSeeker = 0f;
                currentCustomTrack = string.Empty; // Should not be a custom track set if one is not playing.
            }

            // Queue next custom track when at the end of the current one.
            if (isUsingCustomPlaylist && dynamicSongPlayer.IsStoppedClip)
                customTrackQueued = true;
            lastGameDays = gameDays;
            lastlocationIndex = localPlayerGPS.CurrentLocationIndex;
            lastVanillaPlaylist = (MusicPlaylist)currentPlaylist;
        }

        private MusicPlaylist GetMusicPlaylist(PlayerGPS localPlayerGPS, PlayerEnterExit playerEnterExit, PlayerWeather playerWeather)
        {
            // Note: Code was adapted from SongManager.cs. Credit goes to Interkarma for the original implementation.
            var dfUnity = DaggerfallUnity.Instance;
            var topWindow = DaggerfallUI.UIManager.TopWindow;
            if (gameManager.StateManager.CurrentState == StateManager.StateTypes.Start && !(topWindow is DaggerfallVidPlayerWindow) && !(topWindow is DaggerfallHUD))
            {
                if (topWindow is DaggerfallStartWindow ||
                            topWindow is DaggerfallUnitySaveGameWindow ||
                            (topWindow is DaggerfallPopupWindow && (topWindow as DaggerfallPopupWindow).PreviousWindow is DaggerfallUnitySaveGameWindow) ||
                            topWindow is DaggerfallLoadClassicGameWindow)
                    return MusicPlaylist.MainMenu;
                else
                    return MusicPlaylist.CharCreation;
            }

            if (!gameLoaded || !playerEnterExit || !localPlayerGPS || !dfUnity || topWindow is DaggerfallVidPlayerWindow)
                return MusicPlaylist.None;
            if (playerEntity.Arrested)
                return MusicPlaylist.Court;
            var musicEnvironment = MusicEnvironment.Wilderness;
            // Exteriors
            if (!playerEnterExit.IsPlayerInside)
            {
                if (localPlayerGPS.IsPlayerInLocationRect)
                {
                    switch (localPlayerGPS.CurrentLocationType)
                    {
                        case DFRegion.LocationTypes.DungeonKeep:
                        case DFRegion.LocationTypes.DungeonLabyrinth:
                        case DFRegion.LocationTypes.DungeonRuin:
                        case DFRegion.LocationTypes.Coven:
                        case DFRegion.LocationTypes.HomePoor:
                            musicEnvironment = MusicEnvironment.DungeonExterior;
                            break;
                        case DFRegion.LocationTypes.Graveyard:
                            musicEnvironment = MusicEnvironment.Graveyard;
                            break;
                        case DFRegion.LocationTypes.HomeFarms:
                        case DFRegion.LocationTypes.HomeWealthy:
                        case DFRegion.LocationTypes.Tavern:
                        case DFRegion.LocationTypes.TownCity:
                        case DFRegion.LocationTypes.TownHamlet:
                        case DFRegion.LocationTypes.TownVillage:
                        case DFRegion.LocationTypes.ReligionTemple:
                            musicEnvironment = MusicEnvironment.City;
                            break;
                        default:
                            musicEnvironment = MusicEnvironment.Wilderness;
                            break;
                    }
                }
                else
                {
                    musicEnvironment = MusicEnvironment.Wilderness;
                }
            }
            // Dungeons
            else if (playerEnterExit.IsPlayerInsideDungeon)
            {
                if (playerEnterExit.IsPlayerInsideDungeonCastle)
                    musicEnvironment = MusicEnvironment.Castle;
                else
                    musicEnvironment = MusicEnvironment.DungeonInterior;
            }
            // Interiors
            else if (playerEnterExit.IsPlayerInside)
            {
                switch (playerEnterExit.BuildingType)
                {
                    case DFLocation.BuildingTypes.Alchemist:
                    case DFLocation.BuildingTypes.Armorer:
                    case DFLocation.BuildingTypes.Bank:
                    case DFLocation.BuildingTypes.Bookseller:
                    case DFLocation.BuildingTypes.ClothingStore:
                    case DFLocation.BuildingTypes.FurnitureStore:
                    case DFLocation.BuildingTypes.GemStore:
                    case DFLocation.BuildingTypes.GeneralStore:
                    case DFLocation.BuildingTypes.Library:
                    case DFLocation.BuildingTypes.PawnShop:
                    case DFLocation.BuildingTypes.WeaponSmith:
                        musicEnvironment = MusicEnvironment.Shop;
                        break;
                    case DFLocation.BuildingTypes.Tavern:
                        musicEnvironment = MusicEnvironment.Tavern;
                        break;
                    case DFLocation.BuildingTypes.GuildHall:
                        if (playerEnterExit.FactionID == (int)FactionFile.FactionIDs.The_Mages_Guild)
                            musicEnvironment = MusicEnvironment.MagesGuild;
                        else
                            musicEnvironment = MusicEnvironment.Interior;
                        break;
                    case DFLocation.BuildingTypes.Palace:
                        musicEnvironment = MusicEnvironment.Palace;
                        break;
                    case DFLocation.BuildingTypes.Temple:
                        musicEnvironment = MusicEnvironment.Temple;
                        break;
                    default:
                        musicEnvironment = MusicEnvironment.Interior;
                        break;
                }
            }

            if (musicEnvironment == MusicEnvironment.City || musicEnvironment == MusicEnvironment.Wilderness)
            {
                if (dfUnity.WorldTime.Now.IsNight)
                    return MusicPlaylist.Night;
                else
                    switch (playerWeather.WeatherType)
                    {
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Cloudy:
                            return MusicPlaylist.Cloudy;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Overcast:
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Fog:
                            return MusicPlaylist.Overcast;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Rain:
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Thunder:
                            return MusicPlaylist.Rain;
                        case DaggerfallWorkshop.Game.Weather.WeatherType.Snow:
                            return MusicPlaylist.Snow;
                        default:
                            return MusicPlaylist.Sunny;
                    }
            }
            else
                switch (musicEnvironment)
                {
                    case MusicEnvironment.Castle:
                        return MusicPlaylist.Castle;
                    case MusicEnvironment.DungeonExterior:
                        return MusicPlaylist.Night;
                    case MusicEnvironment.DungeonInterior:
                        return MusicPlaylist.DungeonInterior;
                    case MusicEnvironment.Graveyard:
                        return MusicPlaylist.Night;
                    case MusicEnvironment.MagesGuild:
                        return MusicPlaylist.MagesGuild;
                    case MusicEnvironment.Interior:
                        return MusicPlaylist.Interior;
                    case MusicEnvironment.Palace:
                        return MusicPlaylist.Palace;
                    case MusicEnvironment.Shop:
                        return MusicPlaylist.Shop;
                    case MusicEnvironment.Tavern:
                        return MusicPlaylist.Tavern;
                    case MusicEnvironment.Temple:
                        return MusicPlaylist.Temple;
                    default:
                        return MusicPlaylist.None;
                }
        }

        // Adapted from SongManager.cs. Credit to Interkarma.
        private SongFiles GetSong(MusicPlaylist musicPlaylist)
        { 
            var index = 0;
            // Map playlist enum to SongManager playlist.
            SongFiles[] currentPlaylist;
            switch (musicPlaylist)
            {
                case MusicPlaylist.Night:
                    currentPlaylist = Instance.NightSongs;
                    break;
                case MusicPlaylist.Sunny:
                    currentPlaylist = Instance.SunnySongs;
                    break;
                case MusicPlaylist.Cloudy:
                    currentPlaylist = Instance.CloudySongs;
                    break;
                case MusicPlaylist.Overcast:
                    currentPlaylist = Instance.OvercastSongs;
                    break;
                case MusicPlaylist.Rain:
                    currentPlaylist = Instance.RainSongs;
                    break;
                case MusicPlaylist.Snow:
                    currentPlaylist = Instance.SnowSongs;
                    break;
                case MusicPlaylist.Temple:
                    currentPlaylist = Instance.TempleSongs;
                    break;
                case MusicPlaylist.Tavern:
                    currentPlaylist = Instance.TavernSongs;
                    break;
                case MusicPlaylist.Shop:
                    currentPlaylist = Instance.ShopSongs;
                    break;
                case MusicPlaylist.DungeonInterior:
                    currentPlaylist = Instance.DungeonInteriorSongs;
                    break;
                case MusicPlaylist.MagesGuild:
                    currentPlaylist = Instance.MagesGuildSongs;
                    break;
                case MusicPlaylist.Interior:
                    currentPlaylist = Instance.InteriorSongs;
                    break;
                case MusicPlaylist.Palace:
                    currentPlaylist = Instance.PalaceSongs;
                    break;
                case MusicPlaylist.Castle:
                    currentPlaylist = Instance.CastleSongs;
                    break;
                case MusicPlaylist.Court:
                    currentPlaylist = Instance.CourtSongs;
                    break;
                default:
                    currentPlaylist = null;
                    break;
            };

            if (currentPlaylist == null)
            {
                lastVanillaPlaylist = MusicPlaylist.None;
                return SongFiles.song_none;
            }

            // General MIDI song selection
            {
                uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
                uint gameDays = gameMinutes / 1440;

                // All taverns share the same song for the day, and are played in sequence
                // from one day to the next
                if (currentPlaylist == TavernSongs)
                {
                    index = (int)(gameDays % currentPlaylist.Length);
                }
                // Dungeons use a special field for the song selection
                else if (currentPlaylist == DungeonInteriorSongs)
                {
                    PlayerGPS gps = GameManager.Instance.PlayerGPS;
                    ushort unknown2 = 0;
                    int region = 0;
                    if (gps.HasCurrentLocation)
                    {
                        unknown2 = (ushort)gps.CurrentLocation.Dungeon.RecordElement.Header.Unknown2;
                        region = gps.CurrentRegionIndex;
                    }
                    DFRandom.srand(unknown2 ^ ((byte)region << 8));
                    uint random = DFRandom.rand();
                    index = (int)(random % DungeonInteriorSongs.Length);
                }
                // Mages Guild uses a random track each time
                // Sneaking is not used, but if it was, it would probably be random like this
                else if (currentPlaylist == SneakingSongs || currentPlaylist == MagesGuildSongs)
                {
                    if (lastVanillaPlaylist != musicPlaylist)
                        index = UnityEngine.Random.Range(0, currentPlaylist.Length);
                }
                // Most other places use a random song for the day
                else if (currentPlaylist.Length > 1)
                {
                    DFRandom.srand(gameDays);
                    uint random = DFRandom.rand();
                    index = (int)(random % currentPlaylist.Length);
                }                
            }

            lastVanillaPlaylist = musicPlaylist;
            return currentPlaylist[index];
        }

        private void GetDebuggingText(string track, out string playlistName, out string songName, bool isUserDefined)
        {
            var custom = isUserDefined ? " (User-Defined)" : "";
            playlistName = $"{Path.GetFileName(Path.GetDirectoryName(track))}{custom}";
            songName = Path.GetFileName(track);
        }

        private void GetDebuggingText(SongFiles song, out string playlistName, out string songName)
        {
            playlistName = ((MusicPlaylist)currentPlaylist).ToString();
            songName = song.ToString();
        }

        private bool GetCombatStatus(out int maxLevel)
        {
            bool isEnemyFound = false;
            int maxLevel_ = 0;
            var entityBehaviours = FindObjectsOfType<DaggerfallEntityBehaviour>();
            foreach (var entityBehaviour in entityBehaviours)
            {
                if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
                {
                    var enemySenses = entityBehaviour.GetComponent<EnemySenses>();
                    if (enemySenses && enemySenses.Target == gameManager.PlayerEntityBehaviour && enemySenses.DetectedTarget && enemySenses.TargetInSight)
                    {
                        isEnemyFound = true;
                        if (entityBehaviour.Entity.Level > maxLevel_)
                            maxLevel_ = entityBehaviour.Entity.Level;
                    }
                }
            }

            maxLevel = maxLevel_;
            return isEnemyFound;
        }

        // Load new location's song player when player moves into it.
        private void OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionDungeonInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private void OnTransitionDungeonExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            HandleLocationChange();
        }

        private static void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            Instance.HandleLocationChange();
            Instance.isInCombat = false;
            Instance.gameLoaded = true;
        }

        private static void StartGameBehaviour_OnStartGame(object sender, EventArgs e)
        {
            Instance.HandleLocationChange();
            Instance.gameLoaded = true;
        }

        private static void OnPostFastTravel()
        {
            Instance.stingWaitTime = Instance.stingDelay;
            Instance.isWaitingForTravelSting = true;
        }
        
        private static void OnSongEnd()
        {
            if (Instance.currentState == State.Normal)
                Instance.isPlayingSting = false;
        }

        private void OnDeath(DaggerfallEntity entity)
        {
            // Fade out on death.
            combatTaper = 0;
            currentState = State.FadingOut;
            currentPlaylist = (int)MusicPlaylist.None;
            isInCombat = false;
            gameLoaded = false;
        }

        // MOBILE: how many custom playlists actually had tracks - for the start line above.
        private int CustomPlaylistCount()
        {
            if (customPlaylists == null)
                return 0;
            var n = 0;
            foreach (var p in customPlaylists)
                if (p != null)
                    n++;
            return n;
        }

        // MOBILE: the line a simulator pass reads. The iOS simulator player is built with Unity audio
        // DISABLED, so "did the music change on dungeon entry" cannot be heard - only logged. Once per
        // CHANGE of playlist/song, never per frame.
        static int lastLoggedPlaylist = -1;
        static SongFiles lastLoggedSong = SongFiles.song_none;
        public static void LogTrackChoice(int playlist, SongFiles song)
        {
            if (playlist == lastLoggedPlaylist && song == lastLoggedSong)
                return;
            lastLoggedPlaylist = playlist;
            lastLoggedSong = song;
            Debug.Log($"[DynamicMusic] track: playlist {(MusicPlaylist)playlist} -> {song}");
        }
    }
}
