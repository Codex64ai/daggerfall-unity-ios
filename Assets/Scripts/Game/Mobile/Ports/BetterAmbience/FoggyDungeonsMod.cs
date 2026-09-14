// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/FoggyDungeons/FoggyDungeonsMod.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using System;
using System.Collections;
using UnityEngine.Rendering.PostProcessing;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Serialization;

namespace SpellcastStudios.FoggyDungeons
{
    public class FoggyDungeonsMod : MonoBehaviour
    {
        public float maxFogDistance = 100;
        public float minFogDistance = 80;
        public float maxFogStart = 10;
        public float minFogStart = 0;

        public bool enableFog = true;
        public bool enableAO = true;
        public bool enableVignette = true;
        public bool enableAmbientLighting = true;
        public float ambientLerp = 0.2f;
        public float dungeonDarkness = 1;

        public bool totalRandom = false;

        private static Mod mod;
        private PostProcessLayer postProcessLayer;
        private PostProcessVolume postProcessVolume;
        private PlayerAmbientLight playerAmbientLight;

        // MOBILE: [Invoke] and Init removed; BetterAmbiencePort creates this component and sets the Mod.
        public static void SetMod(Mod m) { mod = m; }

        // MOBILE: the post-process stack this reaches into is SHARED - ColorBoost drives the same
        // PostProcessVolume profile, and PostProcessLayer.fog.excludeSkybox is what the sky-haze work
        // (MobileSkyHaze / DaggerfallSky) and Distant Terrain both read the fog through. Upstream
        // hard-codes "off" on exit, which silently overwrites whatever the rest of the app had asked
        // for. So: snapshot the four values the first time this mod turns anything on, and put THOSE
        // back on exit rather than a constant.
        bool snapshotTaken;
        bool wasAoEnabled, wasVignetteEnabled, wasExcludeSkybox;
        float wasVignetteIntensity, wasAoIntensity, wasAoRadius;

        void TakeSnapshot()
        {
            if (snapshotTaken)
                return;
            snapshotTaken = true;
            if (postProcessLayer != null)
                wasExcludeSkybox = postProcessLayer.fog.excludeSkybox;
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                AmbientOcclusion ao;
                if (postProcessVolume.profile.TryGetSettings(out ao))
                {
                    wasAoEnabled = ao.enabled.value;
                    wasAoIntensity = ao.intensity.value;
                    wasAoRadius = ao.radius.value;
                }
                Vignette vig;
                if (postProcessVolume.profile.TryGetSettings(out vig))
                {
                    wasVignetteEnabled = vig.enabled.value;
                    wasVignetteIntensity = vig.intensity.value;
                }
            }
        }

        private void Start()
        {
            postProcessLayer = GameManager.Instance.MainCamera.GetComponent<PostProcessLayer>();
            postProcessVolume = GameManager.Instance.PostProcessVolume;
            playerAmbientLight = GameManager.Instance.PlayerObject.GetComponent<PlayerAmbientLight>();

            StartGameBehaviour.OnStartGame += OnStartGame;
            PlayerEnterExit.OnTransitionDungeonInterior += OnEnterDungeon;
            PlayerEnterExit.OnTransitionDungeonExterior += OnExitDungeon;
            SaveLoadManager.OnLoad += OnLoad;
        }

        private void LoadSettings(ModSettings settings)
        {
            enableFog = settings.GetValue<bool>("Dungeon Fog", "enableFog");
            maxFogDistance = settings.GetValue<float>("Dungeon Fog", "maxFogDistance");
            minFogDistance = settings.GetValue<float>("Dungeon Fog", "minFogDistance");
            maxFogStart = settings.GetValue<float>("Dungeon Fog", "maxFogStart");
            minFogStart = settings.GetValue<float>("Dungeon Fog", "minFogStart");

            enableAmbientLighting = settings.GetValue<bool>("Dungeon Lighting", "enableFogAmbientEffect");
            dungeonDarkness = settings.GetValue<float>("Dungeon Lighting", "dungeonDarkness");
            ambientLerp = settings.GetValue<float>("Dungeon Lighting", "fogAmbientEffect");

            enableAO = settings.GetValue<bool>("Effects", "enableAO");
            enableVignette = settings.GetValue<bool>("Effects", "enableVignette");
        }

        private void OnStartGame(object sender, EventArgs e)
        {
            StartCoroutine(UpdateDungeonFog());
        }

        private void OnEnterDungeon(PlayerEnterExit.TransitionEventArgs args)
        {
            StartCoroutine(UpdateDungeonFog());
        }

        private void OnExitDungeon(PlayerEnterExit.TransitionEventArgs args)
        {
            StartCoroutine(UpdateDungeonFog());
        }

        private void OnLoad(SaveData_v1 saveData)
        {
            StartCoroutine(UpdateDungeonFog());
        }


        private void DisableDungeonFog()
        {
            // MOBILE: restore what was there before this mod touched it (see TakeSnapshot). When the
            // mod never turned anything on, there is nothing to put back and the snapshot is not taken.
            if (!snapshotTaken)
                return;

            if (postProcessLayer != null)
                postProcessLayer.fog.excludeSkybox = wasExcludeSkybox;

            if (enableAO && postProcessVolume != null && postProcessVolume.profile != null)
            {
                AmbientOcclusion ambientOcclusionSettings;
                if (postProcessVolume.profile.TryGetSettings(out ambientOcclusionSettings))
                {
                    ambientOcclusionSettings.intensity.value = wasAoIntensity;
                    ambientOcclusionSettings.radius.value = wasAoRadius;
                    ambientOcclusionSettings.enabled.value = wasAoEnabled;
                }
            }

            if (enableVignette && postProcessVolume != null && postProcessVolume.profile != null)
            {
                Vignette vignetteSettings;
                if (postProcessVolume.profile.TryGetSettings(out vignetteSettings))
                {
                    vignetteSettings.intensity.value = wasVignetteIntensity;
                    vignetteSettings.enabled.value = wasVignetteEnabled;
                }
            }

            if (enableAmbientLighting && playerAmbientLight != null)
            {
                playerAmbientLight.enabled = true;

                //Force everything to reset
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

                typeof(PlayerAmbientLight).GetField("fadeRunning", flags).SetValue(playerAmbientLight, false);
                typeof(PlayerAmbientLight).GetMethod("Start", flags).Invoke(playerAmbientLight, new object[0]);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            }

            // MOBILE: the fog itself is NOT restored here on purpose. WeatherManager already owns the
            // outdoor/indoor fog handover (previousOutdoorFogColor, SetFog(DungeonFogSettings)) and
            // DaggerfallSky rewrites RenderSettings.fogColor from the sky's horizon every sky frame
            // once SkyHaze is on, so anything written here is gone within a frame of stepping outside.
            Debug.Log("[BetterAmbience] dungeon fog: off, post-process restored");
        }

        private void EnableDungeonFog(DaggerfallDungeon dungeon)
        {
            // MOBILE: snapshot before the first write, so the exit path has something true to restore.
            TakeSnapshot();

            var rnd = totalRandom ? new System.Random(Time.time.GetHashCode()) : new System.Random(dungeon.name.GetHashCode());
            Color fogColor = new Color((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble());
            // MOBILE: the log line a sim pass reads - the simulator has no audio, and this is the one
            // module of Better Ambience whose effect is visible rather than audible.
            Debug.Log(string.Format("[BetterAmbience] dungeon fog: {0} colour RGB({1:F2},{2:F2},{3:F2})",
                dungeon.name, fogColor.r, fogColor.g, fogColor.b));

            if (enableFog)
            {
                float start = ((float)rnd.NextDouble() * (maxFogStart - minFogStart)) + minFogStart;
                float end = ((float)rnd.NextDouble() * (maxFogDistance - minFogDistance)) + minFogDistance + start;

                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
                RenderSettings.fogMode = FogMode.Linear;

            }

            if (postProcessLayer != null)
            {
                postProcessLayer.fog.excludeSkybox = true;
            }

            if (enableAO)
            {
                AmbientOcclusion ambientOcclusionSettings;
                if (postProcessVolume.profile.TryGetSettings(out ambientOcclusionSettings))
                {
                    ambientOcclusionSettings.intensity.value = 0.65f;
                    ambientOcclusionSettings.radius.value = 1.64f;
                    ambientOcclusionSettings.enabled.value = true;
                }
            }

            if (enableVignette)
            {
                Vignette vignetteSettings;
                if (postProcessVolume.profile.TryGetSettings(out vignetteSettings))
                {
                    vignetteSettings.intensity.value = 0.273f;
                    vignetteSettings.enabled.value = true;
                }
            }

            if(enableAmbientLighting)
            {
                if (playerAmbientLight != null)
                {
                    playerAmbientLight.enabled = false;
                    playerAmbientLight.StopAllCoroutines();
                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

                Color sky = Color.Lerp(new Color(0.433f, 0.433f, 0.433f), fogColor, ambientLerp) * dungeonDarkness;
                Color equator = Color.Lerp(new Color(0.396f, 0.396f, 0.396f), fogColor, ambientLerp) * dungeonDarkness;
                Color ground = Color.Lerp(new Color(0.254f, 0.254f, 0.254f), fogColor, ambientLerp) * dungeonDarkness;

                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
            }
        }

        private IEnumerator UpdateDungeonFog()
        {
            //Wait some frames XD
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            LoadSettings(mod.GetSettings());

            var dungeon = GameManager.Instance.PlayerEnterExit.Dungeon;

            // MOBILE: IsPlayerInsideDungeon added to the guard. Upstream tested only Dungeon != null,
            // and PlayerEnterExit keeps the Dungeon reference for a while after the transition out -
            // so on the exit event this could still paint a random fog colour over the open sky, which
            // is exactly the fight with sky haze this port must not have.
            if (dungeon == null
                || !GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon
                || GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeonCastle)
            {
                DisableDungeonFog();
                yield break;
            }

            EnableDungeonFog(dungeon);
        }

    }
}