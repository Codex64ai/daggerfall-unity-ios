// MOBILE PORT - source: github.com/DunnyOfPenwick/First-Person-Lighting @ 7084736201e9a5eeec223d6a4352696f0542eb86
// File Scripts/FirstPersonLightingMod.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (LICENSE): MIT, Copyright (c) 2024 DunnyOfPenwick.

// Project:     First Person Lighting for Daggerfall Unity
// Author:      DunnyOfPenwick
// Origin Date: January 2024

using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallConnect.FallExe;

namespace FirstPersonLighting
{
    public class FirstPersonLightingMod : MonoBehaviour
    {
        public static Mod Mod;
        public static FirstPersonLightingMod Instance;

        static bool useGropeLight;
        static bool allowExtinguishFlames;
        static bool alterTorchlight;

        Color PlayerLighting = Color.gray;
        Color PlayerTint = Color.white;

        Light gropeLight;
        float gropeRange = 3;
        float submergedGropeRange = 2;

        Light magicLight;

        readonly Dictionary<DaggerfallEntityBehaviour, Color> visibilityCache = new Dictionary<DaggerfallEntityBehaviour, Color>();
        readonly List<Light> lights = new List<Light>();

        GameObject PlayerTorch;
        Light torchLight;
        float torchSmoother;
        float guttering;
        float tickTimeBuffer = 0f;
        const float tickTimeInterval = 20f;
        DaggerfallUnityItem lastLightItem;



        // MOBILE: [Invoke] removed - MobilePortedMods calls this Init when the launcher entry is on.

        /// <summary>True once Init created the mod object. What MobilePortedMods' StartOne asks.</summary>
        public static bool Installed { get; private set; }

        public static void Init(InitParams initParams)
        {
            if (Installed)
                return;

            Mod = initParams.Mod;
            if (Mod == null)
            {
                Debug.Log("[FirstPersonLighting] not available: no mod entry (the bundle carries the flare art, clips and settings)");
                return;
            }

            GameObject go = new GameObject(Mod.Title);
            // MOBILE: survives the scene changes this port makes between the title and the world.
            DontDestroyOnLoad(go);

            Instance = go.AddComponent<FirstPersonLightingMod>();

            ModSettings settings = Mod.GetSettings();
            useGropeLight = settings.GetBool("Options", "GropeLight");
            allowExtinguishFlames = settings.GetBool("Options", "ExtinguishFlames");
            alterTorchlight = settings.GetBool("Options", "MovementAffectsTorches");

            Mod.MessageReceiver = MessageReceiver;

            Mod.IsReady = true;
            Installed = true;
            Debug.Log("[FirstPersonLighting] started: grope light " + useGropeLight
                + ", extinguish flames " + allowExtinguishFlames + ", torch sway " + alterTorchlight);
        }

        /// <summary>
        /// Handles messages sent by other mods.
        /// See https://www.dfworkshop.net/projects/daggerfall-unity/modding/features/#mods-interaction
        /// </summary>
        static void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            if (callBack == null)
            {
                Debug.LogError("First-Person-Lighting: MessageReceiver: expecting callback object, got null.");
            }
            else if (message.Equals("entityLighting", StringComparison.OrdinalIgnoreCase))
            {
                if (data is DaggerfallEntityBehaviour entity)
                {
                    Color areaTint = Instance.GetEntityLighting(entity);
                    callBack("colorReply", areaTint);
                }
                else
                {
                    Debug.LogError($"First-Person-Lighting:  MessageReceiver: 'entityLighting' expects a DaggerfallEntityBehaviour object, got {typeof(object)}.");
                }
            }
            else if (message.Equals("locationLighting", StringComparison.OrdinalIgnoreCase))
            {
                if (data is Vector3 location)
                {
                    Color areaTint = Instance.MeasureLight(location);
                    callBack("colorReply", areaTint);
                }
                else
                {
                    Debug.LogError($"First-Person-Lighting:  MessageReceiver: 'locationLighting' expects a Vector3 object, got {typeof(object)}.");
                }
            }
            else if (message.Equals("playerTint", StringComparison.OrdinalIgnoreCase))
            {
                Color playerTint = Instance.GetPlayerTint();
                callBack("colorReply", playerTint);
            }
            else if (message.Equals("gropeLightRange", StringComparison.OrdinalIgnoreCase))
            {
                if (useGropeLight == false)
                    callBack("floatReply", 0);
                else if (GameManager.Instance.PlayerEnterExit.IsPlayerSubmerged)
                    callBack("floatReply", Instance.submergedGropeRange);
                else
                    callBack("floatReply", Instance.gropeRange);
            }
            else
            {
                Debug.LogError($"First-Person-Lighting:  MessageReceiver: unknown message '{message}'.");
            }

        }



        // MOBILE: upstream's Start reaches straight into the player hierarchy (SmoothFollower/Torch) and
        // calls GatherLights. MobilePortedMods starts the compiled-in mods at the TITLE on a player
        // build, where the player object is not guaranteed to be there yet, so the body waits for it.
        void Start()
        {
            StartCoroutine(DeferredStart());
        }

        System.Collections.IEnumerator DeferredStart()
        {
            while (!GameManager.HasInstance
                || GameManager.Instance.PlayerObject == null
                || GameManager.Instance.PlayerEntityBehaviour == null)
                yield return null;

            Debug.Log("Start(): First-Person-Lighting");

            if (alterTorchlight && DaggerfallUnity.Settings.PlayerTorchFromItems)
            {
                EnablePlayerTorch enablePlayerTorch = GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>();

                //Disable the EnablePlayerTorch component.  We will perform the torch/lighting logic.
                enablePlayerTorch.enabled = false;

                Transform smoothFollower = GameManager.Instance.PlayerObject.transform.Find("SmoothFollower");
                PlayerTorch = smoothFollower.Find("Torch").gameObject;
                torchLight = PlayerTorch.GetComponent<Light>();
            }

            AddGropeLight();
            AddMagicLight();

            DaggerfallUnity.Instance.ItemHelper.RegisterCustomItem(
                AlchemicalFlare.AlchemicalFlareTemplateIndex,
                ItemGroups.MiscellaneousIngredients2,
                typeof(AlchemicalFlare));

            GatherLights();

            Debug.Log("Finished Start(): First-Person-Lighting");
        }


        void Update()
        {
            if (GameManager.IsGamePaused)
                return;

            //Periodically refresh the collection of nearby lights.
            if (Time.frameCount % 20 == 0)
                GatherLights();

            //Periodically update player tint information and clear the entity lighting/visibility cache.
            if (Time.frameCount % 5 == 0)
            {
                visibilityCache.Clear();
                PlayerTint = GetPlayerTint();

                GameManager.Instance.RightHandWeapon.Tint = PlayerTint;
                GameManager.Instance.TransportManager.Tint = PlayerTint;
            }

            //If underwater, extinguish player torches/lanterns
            if (GameManager.Instance.PlayerEnterExit.IsPlayerSubmerged)
            {
                if (allowExtinguishFlames)
                    ExtinguishFlames(); //Extinguish any lanterns/torches in use
            }

            if (Time.frameCount % 6 == 0)
            {
                AdjustGropeLight();

                AdjustPlayerTorchOrLantern();

                //Check if magic weapon is readied or spell is being cast, activate magic light if so.
                CheckActivateMagicLight();
            }

        }



        /// <summary>
        /// Extinguishes player torches and lanterns.
        /// </summary>
        void ExtinguishFlames()
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (player.LightSource != null && !player.LightSource.IsEnchanted)
                player.LightSource = null;
        }


        /// <summary>
        /// Adds a short-range light on the player, to see for short distances in the dark.
        /// The range of the grope light depends on racial factors and location.
        /// This light is ignored by the light sensing code and does not affect player visibility.
        /// </summary>
        void AddGropeLight()
        {

            //The grope light is manipulated in the Update() method.
            GameObject go = new GameObject("GropeLight");
            gropeLight = go.AddComponent<Light>();
            gropeLight.type = LightType.Point;
            gropeLight.range = gropeRange;
            //gropeLight.color = new Color(0.75f, 0.2f, 0.1f);
            gropeLight.color = new Color(0.5f, 0.2f, 0.5f);
            gropeLight.intensity = 1f;
            gropeLight.shadows = LightShadows.None;
            gropeLight.hideFlags = HideFlags.HideInInspector; //...so it is ignored by the light sensing code.

            gropeLight.enabled = false;
        }


        /// <summary>
        /// Adds a small light on the player, activated if a magic weapon is readied.
        /// </summary>
        void AddMagicLight()
        {
            //The grope light is manipulated in the Update() method.
            GameObject go = new GameObject("MagicWeaponLight");
            magicLight = go.AddComponent<Light>();
            magicLight.type = LightType.Point;
            magicLight.range = 3f;
            magicLight.color = new Color(0.5f, 0.5f, 1f);
            magicLight.intensity = 1f;

            magicLight.enabled = false;
        }


        /// <summary>
        /// Adjusts grope light position and radius based on environmental conditions.
        /// </summary>
        void AdjustGropeLight()
        {
            if (!useGropeLight)
                return;

            gropeLight.enabled = PlayerLighting.grayscale <= 0.05f;
            if (!gropeLight.enabled)
                return;

            gropeRange = 2.6f;
            submergedGropeRange = 2.3f;

            PlayerEntity player = GameManager.Instance.PlayerEntity;

            if (player.IsInBeastForm)
                gropeRange = 3.8f;
            else if (CheckIsVampire())
                gropeRange = 4.2f;
            else if (player.BirthRaceTemplate.ID == (int)Races.Khajiit)
                gropeRange = 3.5f;
            else if (player.BirthRaceTemplate.ID == (int)Races.Argonian)
                submergedGropeRange = 8f;

            if (GameManager.Instance.PlayerEntity.Career.AcuteHearing)
                gropeRange += 1.2f;

            if (player.ImprovedAcuteHearing)
                gropeRange += 1.2f;

            //Position grope light on player, near the knees.
            gropeLight.transform.position = GameManager.Instance.PlayerObject.transform.position - Vector3.up * 0.4f;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerSubmerged)
                gropeLight.range = submergedGropeRange;
            else
                gropeLight.range = gropeRange;
        }


        /// <summary>
        /// Checks if player is a vampire.
        /// </summary>
        bool CheckIsVampire()
        {
            LiveEffectBundle[] bundles = GameManager.Instance.PlayerEffectManager.EffectBundles;

            foreach (LiveEffectBundle bundle in bundles)
                foreach (IEntityEffect effect in bundle.liveEffects)
                    if (effect is VampirismInfection)
                        return true;

            return false;
        }


        /// <summary>
        /// If using PlayerItemTorch, adjust intensity of torch/candle to match player movement.
        /// </summary>
        void AdjustPlayerTorchOrLantern()
        {
            //Using/altering player item lightsources?
            if (!torchLight)
                return;

            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

            DaggerfallUnityItem lightSource = playerEntity.LightSource;
            if (lightSource == null)
            {
                PlayerTorch.SetActive(false);
                torchSmoother = 0;
                return;
            }

            if (lightSource != lastLightItem)
            {
                lastLightItem = lightSource;
                torchSmoother = 0;
            }

            bool isTorch = lightSource.TemplateIndex == (int)UselessItems2.Torch;
            bool isCandle = lightSource.TemplateIndex == (int)UselessItems2.Candle || lightSource.TemplateIndex == (int)ReligiousItems.Holy_candle;
            bool isLantern = lightSource.TemplateIndex == (int)UselessItems2.Lantern;

            //Torches should be the brightest/longest range light. Scale others down.
            ItemTemplate torchItem = DaggerfallUnity.Instance.ItemHelper.GetItemTemplate(ItemGroups.UselessItems2, 0);
            float torchRange = torchItem.capacityOrTarget;
            float itemIntensity = isTorch ? 1 : isLantern ? 0.7f : 0.5f;
            float range = torchRange * itemIntensity;

            float targetIntensity = itemIntensity;

            if (isTorch || isCandle)
            {
                //Player movement speed beyond a threshold causes carried light to dim, or even go out.
                float velocityMod = Mathf.Max(GameManager.Instance.PlayerController.velocity.magnitude - 2, 0) / 4;
                targetIntensity = itemIntensity - velocityMod;
            }

            //Gradually ramp-up or ramp-down the light intensity depending on movement.
            torchSmoother += targetIntensity * Time.deltaTime * 8;

            torchSmoother = Mathf.Clamp(torchSmoother, 0.1f, 1);

            if (torchSmoother > 0.1f)
                torchSmoother += Mathf.Cos(Time.time * 15) / 60f; //some extra flicker

            //Logic lifted from EnablePlayerTorch code.
            float intensityMod;
            tickTimeBuffer += Time.deltaTime;
            bool enableTorch = true;
            torchLight.range = range; //lightSource.ItemTemplate.capacityOrTarget;
            // Consume durability / fuel
            if (tickTimeBuffer > tickTimeInterval)
            {
                tickTimeBuffer = 0f;
                if (lightSource.currentCondition > 0)
                    lightSource.currentCondition--;

                if (lightSource.currentCondition == 0 && DaggerfallUnityItem.CompareItems(playerEntity.LightSource, lightSource))
                {
                    DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("lightDies").Replace("%it", lightSource.ItemName));
                    enableTorch = false;
                    playerEntity.LightSource = null;
                    if (isTorch || isCandle)
                        playerEntity.Items.RemoveItem(lightSource);
                }
            }

            if (lightSource.currentCondition < 3)
            {
                // Give warning signs if running low of fuel
                intensityMod = 0.85f + (Mathf.Cos(guttering) * 0.2f);
                guttering += UnityEngine.Random.Range(-0.02f, 0.06f);
            }
            else
            {
                intensityMod = 1.25f;
                guttering = 0;
            }

            torchLight.intensity = torchSmoother * DaggerfallUnity.Settings.PlayerTorchLightScale * intensityMod;

            PlayerTorch.SetActive(enableTorch);
        }


        /// <summary>
        /// Activates a small light on the player if a magic weapon is readied.
        /// </summary>
        void CheckActivateMagicLight()
        {
            magicLight.enabled = false;

            if (GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
            {
                magicLight.enabled = true;
                GameObject player = GameManager.Instance.PlayerObject;
                magicLight.transform.position = player.transform.position + player.transform.forward * 0.5f;
            }
            else
            {
                CheckActivateMagicWeaponLight();
            }
        }


        void CheckActivateMagicWeaponLight()
        {
            WeaponManager weaponManager = GameManager.Instance.WeaponManager;
            if (weaponManager.Sheathed)
                return;

            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            DaggerfallUnityItem rightHandItem = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            DaggerfallUnityItem leftHandItem = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftHand);

            DaggerfallUnityItem item = weaponManager.UsingRightHand ? rightHandItem : leftHandItem;

            if (item == null || !item.IsEnchanted)
                return;

            magicLight.enabled = true;
            GameObject player = GameManager.Instance.PlayerObject;
            magicLight.transform.position = player.transform.position + player.transform.forward * 0.5f;

            //Adjust weapon light position based on current attack movement
            FPSWeapon fpsWeapon = weaponManager.ScreenWeapon;
            int frame = fpsWeapon.GetCurrentFrame();
            float upOffset = 0;
            float rightOffset = 0;
            switch (fpsWeapon.WeaponState)
            {
                case WeaponStates.StrikeDown:
                    upOffset = 0.5f - (frame * 0.15f);
                    break;
                case WeaponStates.StrikeDownLeft:
                    upOffset = 0.3f - (frame * 0.1f);
                    rightOffset = 0.6f - (frame * 0.1f);
                    break;
                case WeaponStates.StrikeDownRight:
                    upOffset = 0.6f - (frame * 0.1f);
                    rightOffset = -0.6f + (frame * 0.1f);
                    break;
                case WeaponStates.StrikeLeft:
                    rightOffset = 0.6f - (frame * 0.15f);
                    break;
                case WeaponStates.StrikeRight:
                    rightOffset = -0.6f + (frame * 0.15f);
                    break;
                case WeaponStates.StrikeUp:
                    upOffset = -0.4f + (frame * 0.15f);
                    break;
                default:
                    return;
            }

            magicLight.transform.position += player.transform.up * upOffset;
            magicLight.transform.position += player.transform.right * rightOffset;
        }



        /// <summary>
        /// Gather all lights within a radius of the player.
        /// </summary>
        void GatherLights()
        {
            Vector3 playerLocation = GameManager.Instance.PlayerObject.transform.position;

            lights.Clear();

            Light[] lightArray = FindObjectsOfType<Light>();

            foreach (Light light in lightArray)
            {
                if (!light.isActiveAndEnabled)
                    continue;

                if (light.type != LightType.Point && light.type != LightType.Spot)
                    continue;

                //Ignore lights that have the HideInInspector flag set (e.g. the grope light).
                //Those lights should only be visible to the player during render.
                if (light.hideFlags == HideFlags.HideInInspector)
                    continue;

                //Ignore lights too far from player.
                float distance = Vector3.Distance(light.transform.position, playerLocation);
                if (distance < 50)
                    lights.Add(light);
            }
        }



        /// <summary>
        /// Gets the color of the total light shining on the specified entity.
        /// </summary>
        Color GetEntityLighting(DaggerfallEntityBehaviour subject)
        {
            if (visibilityCache.TryGetValue(subject, out Color value))
                return value;

            Color entityLighting = MeasureLight(subject);

            visibilityCache.Add(subject, entityLighting);

            return entityLighting;
        }


        static readonly Color shadowTint = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        /// <summary>
        /// Gets the appropriate lighting-based tint to use for drawing first-person graphics like weapons and horses.
        /// </summary>
        Color GetPlayerTint()
        {
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;

            PlayerLighting = GetEntityLighting(player);
            Color tint = PlayerLighting;

            if (player.Entity.IsMagicallyConcealed)
            {
                tint = player.Entity.IsAShade ? shadowTint : Color.white;

                if (player.Entity.IsBlending)
                    tint.a = 0.05f + Mathf.Cos(Time.time * 8) * 0.07f;
                else if (player.Entity.IsInvisible)
                    tint.a = 0.075f;
            }


            return tint;
        }


        /// <summary>
        /// Gets the combined color of all light from all relevant light sources shining on the subject entity.
        /// </summary>
        Color MeasureLight(DaggerfallEntityBehaviour subject)
        {
            if (subject == null)
                return Color.clear;

            return MeasureLight(subject.transform.position, subject.gameObject);
        }


        /// <summary>
        /// Gets the combined light from all relevant sources shining on the specified location.
        /// </summary>
        Color MeasureLight(Vector3 location)
        {
            //Mask ignores entities blocking light, only interested in terrain.
            return MeasureLight(location, null, 1);
        }


        /// <summary>
        /// Adds together all light from available sources shining on a given location.
        /// </summary>
        /// <param name="location">Location to measure light at</param>
        /// <param name="subject">The subject entity the light is shining on, if any</param>
        /// <param name="mask">The layer mask used to determine what can block light</param>
        /// <returns></returns>
        Color MeasureLight(Vector3 location, GameObject subject = null, int mask = ~0)
        {
            int terrainMask = 1;

            Color color;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInSunlight)
            {
                Light sunLight = GameObject.Find("SunLight").GetComponentInChildren<Light>();
                Vector3 sunbeamDirection = sunLight.transform.forward;
                if (Physics.Raycast(location, -sunbeamDirection, Mathf.Infinity, terrainMask))
                {
                    //The sun is blocked by terrain, get indirect sunlight
                    Light indirect = GameManager.Instance.SunlightManager.IndirectLight;
                    color = indirect.color * indirect.intensity;
                }
                else
                {
                    color = sunLight.color * sunLight.intensity;
                }
            }
            else
            {
                color = GetAmbientLight();
            }

            foreach (Light light in lights)
            {
                if (light == null || !light.isActiveAndEnabled)
                    continue;

                Vector3 direction = location - light.transform.position;
                float distance = direction.magnitude;

                if (distance > light.range)
                    continue;

                if (light.type == LightType.Spot)
                {
                    if (!CheckInSpotlight(light, location))
                        continue;
                }

                bool blocked = false;

                //Check if any intervening objects are blocking the light on the subject.
                RaycastHit[] hits = Physics.RaycastAll(light.transform.position, direction.normalized, distance, mask);
                foreach (RaycastHit hit in hits)
                {
                    GameObject obstacle = hit.collider.gameObject;

                    if (obstacle == light.gameObject)
                        continue;
                    else if (obstacle == subject)
                        continue;
                    else if (obstacle.GetComponent<DaggerfallLoot>())
                        continue; //Ignore loot piles, they have abnormally large colliders.
                    else if (hit.distance < 0.35f)
                        continue; //i.e. ignore entities that might be holding the light
                    else
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                {
                    float falloff = (light.range - distance) / light.range;
                    color += light.color * light.intensity * falloff;
                }

            }


            color.r = Mathf.Min(1, color.r);
            color.g = Mathf.Min(1, color.g);
            color.b = Mathf.Min(1, color.b);
            color.a = 1;

            return color;
        }


        /// <summary>
        /// Checks if a given location is within the light cone of a spotlight.
        /// This is in case someone adds a splotlight feature later.
        /// </summary>
        /// <returns></returns>
        bool CheckInSpotlight(Light spotlight, Vector3 location)
        {
            Vector3 v1 = spotlight.transform.forward;
            Vector3 v2 = (location - spotlight.transform.position).normalized;

            float angle = Vector3.Angle(v1, v2);

            return angle <= spotlight.spotAngle / 2;
        }


        /// <summary>
        /// Copied from the core PlayerAmbientLight class.
        /// </summary>
        Color GetAmbientLight()
        {
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
            PlayerAmbientLight playerAmbientLight = GameManager.Instance.PlayerObject.GetComponent<PlayerAmbientLight>();
            bool ambientLitInteriors = DaggerfallUnity.Settings.AmbientLitInteriors;

            if (!playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon)
            {
                SunlightManager sunlightManager = GameManager.Instance.SunlightManager;
                float scale = sunlightManager.DaylightScale * sunlightManager.ScaleFactor;
                Color startColor = playerAmbientLight.ExteriorNightAmbientLight * DaggerfallUnity.Settings.NightAmbientLightScale;
                return Color.Lerp(startColor, playerAmbientLight.ExteriorNoonAmbientLight, scale);
            }
            else if (playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon)
            {
                if (DaggerfallUnity.Instance.WorldTime.Now.IsNight)
                    return ambientLitInteriors ? playerAmbientLight.InteriorNightAmbientLight_AmbientOnly : playerAmbientLight.InteriorNightAmbientLight;
                else
                    return ambientLitInteriors ? playerAmbientLight.InteriorAmbientLight_AmbientOnly : playerAmbientLight.InteriorAmbientLight;
            }
            else if (playerEnterExit.IsPlayerInside && playerEnterExit.IsPlayerInsideDungeon)
            {
                if (playerEnterExit.IsPlayerInsideDungeonCastle)
                    return playerAmbientLight.CastleAmbientLight;
                else if (playerEnterExit.IsPlayerInsideSpecialArea)
                    return playerAmbientLight.SpecialAreaLight;
                else
                    return playerAmbientLight.DungeonAmbientLight * DaggerfallUnity.Settings.DungeonAmbientLightScale;
            }
            else
            {
                return Color.gray;
            }

        }



    } //class FirstPersonLightingMod


} //namespace