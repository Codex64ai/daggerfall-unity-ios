// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/BetterFootsteps/BetterFootstepsComponent.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;

namespace SpellcastStudios.BetterFootsteps
{
    public class BetterFootstepsComponent : MonoBehaviour
    {
        public float WalkStepInterval = 2.5f; // Matched to classic. Was 1.6f;
        public float RunStepInterval = 2.5f; // Matched to classic. Was 1.8f;
        public float FootstepVolumeScale = 0.7f;

        public SoundList FootstepSoundDungeon = new SoundList();
        public SoundList FootstepSoundOutside = new SoundList();
        public SoundList FootstepSoundSnow = new SoundList();
        public SoundList FootstepSoundBuilding = new SoundList();
        public SoundList FootstepSoundShallow = new SoundList();
        public SoundList FootstepSoundSubmerged = new SoundList();
        public SoundList FootstepsArmor = new SoundList();

        DaggerfallUnity dfUnity;
        PlayerEnterExit playerEnterExit;
        DaggerfallAudioSource dfAudioSource;
        TransportManager transportManager;
        AudioSource customAudioSource;
        DaggerfallEntityBehaviour entityBehaviour;

        Vector3 lastPosition;
        bool lostGrounding;
        float distance;
        bool ignoreLostGrounding = true;

        SoundList currentFootstepSoundList = null;


        DaggerfallDateTime.Seasons currentSeason = DaggerfallDateTime.Seasons.Summer;
        int currentClimateIndex;
        bool isInside = false;
        bool isInOutsideWater = false;
        bool isInOutsidePath = false;
        bool isOnStaticGeometry = false;

        protected virtual void Start()
        {
            // Store references
            dfUnity = DaggerfallUnity.Instance;
            dfAudioSource = GetComponent<DaggerfallAudioSource>();
            playerEnterExit = GetComponent<PlayerEnterExit>();
            transportManager = GetComponent<TransportManager>();
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();

            GameObject audioSourceObject = new GameObject("Footsteps Source");
            audioSourceObject.transform.SetParent(transform);
            audioSourceObject.transform.localPosition = Vector3.zero;

            customAudioSource = audioSourceObject.AddComponent<AudioSource>();
            customAudioSource.playOnAwake = false;
            customAudioSource.loop = false;
            customAudioSource.dopplerLevel = 0f;
            customAudioSource.spatialBlend = 1;

            // Set start position
            lastPosition = GetHorizontalPosition();

            // Set starting footsteps
            currentFootstepSoundList = FootstepSoundDungeon;
        }

        protected virtual void Update()
        {
            if (!FootstepsEnabled())
                return;

            //this condition helps prevent making a nuisance footstep noise when the player first
            //loads a save, or into an interior or exterior location
            if (GameManager.Instance.SaveLoadManager.LoadInProgress || GameManager.Instance.StreamingWorld.IsRepositioningPlayer)
            {
                ignoreLostGrounding = true;
                return;
            }

            DaggerfallDateTime.Seasons playerSeason = dfUnity.WorldTime.Now.SeasonValue;
            int climateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;

            // Get player inside flag
            // Can only do this when PlayerEnterExit is available, otherwise default to true
            bool inside = (playerEnterExit == null) ? true : playerEnterExit.IsPlayerInside;
            bool inBuilding = (playerEnterExit == null) ? false : playerEnterExit.IsPlayerInsideBuilding;

            // Play splash footsteps whether player is walking on or swimming in exterior water
            bool onExteriorWater = IsOnExteriorWater();

            bool pnExteriorPath = IsOnExteriorPath();
            bool onStaticGeometry = IsOnStaticGeometry();

            // Change footstep sounds between winter/summer variants, when player enters/exits an interior space, or changes between path, water, or other outdoor ground
            if (playerSeason != currentSeason || climateIndex != currentClimateIndex || isInside != inside || onExteriorWater != isInOutsideWater || pnExteriorPath != isInOutsidePath || onStaticGeometry != isOnStaticGeometry)
            {
                currentSeason = playerSeason;
                currentClimateIndex = climateIndex;
                isInside = inside;
                isInOutsideWater = onExteriorWater;
                isInOutsidePath = pnExteriorPath;
                isOnStaticGeometry = onStaticGeometry;

                if (!isInside && !onStaticGeometry)
                {
                    if (currentSeason == DaggerfallDateTime.Seasons.Winter && !WeatherManager.IsSnowFreeClimate(currentClimateIndex))
                    {
                        currentFootstepSoundList = FootstepSoundSnow;
                    }
                    else
                    {
                        currentFootstepSoundList = FootstepSoundOutside;
                    }
                }
                else if (inBuilding)
                {
                    currentFootstepSoundList = FootstepSoundBuilding;
                }
                else // in dungeon
                {
                    currentFootstepSoundList = FootstepSoundDungeon;
                }
            }

            // walking on water tile
            if (onExteriorWater)
            {
                currentFootstepSoundList = FootstepSoundSubmerged;
            }

            // walking on path tile
            if (pnExteriorPath)
            {
                currentFootstepSoundList = FootstepSoundDungeon;
            }

            // Use water sounds if in dungeon water
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && playerEnterExit.blockWaterLevel != 10000)
            {
                // In water, deep depth
                if ((currentFootstepSoundList != FootstepSoundSubmerged) && playerEnterExit.IsPlayerSwimming)
                {
                    currentFootstepSoundList = FootstepSoundSubmerged;
                }
                // In water, shallow depth
                else if ((currentFootstepSoundList != FootstepSoundShallow) && !playerEnterExit.IsPlayerSwimming && (transform.position.y - 0.55f) < (playerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale))
                {
                    currentFootstepSoundList = FootstepSoundShallow;
                }
            }

            // Not in water, reset footsteps to normal
            if ((!onExteriorWater)
                && (currentFootstepSoundList == FootstepSoundSubmerged || currentFootstepSoundList == FootstepSoundShallow)
                && (playerEnterExit.blockWaterLevel == 10000 || (transform.position.y - 0.95f) >= (playerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)))
            {
                currentFootstepSoundList = FootstepSoundDungeon;
            }

            // Check whether player is on foot and abort playing footsteps if not.
            if (IsLevitating() || !transportManager.IsOnFoot && GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.None)
            {
                distance = 0f;
                return;
            }

            // Check if player is grounded
            // Note: In classic, submerged "footstep" sound is only played when walking on the floor while in the water, but it sounds like a swimming sound
            // and when outside is played while swimming at the water's surface, so it seems better to play it all the time while submerged in water.
            if (!IsSwimming())
            {
                if (!IsGrounded())
                {
                    // Player has lost grounding
                    distance = 0f;
                    lostGrounding = true;
                    return;
                }
                else
                {
                    // Player is grounded but we might need to reset after losing grounding
                    if (lostGrounding)
                    {
                        distance = 0f;
                        lastPosition = GetHorizontalPosition();
                        lostGrounding = false;

                        if (ignoreLostGrounding)
                            ignoreLostGrounding = false;
                        else if (customAudioSource && currentFootstepSoundList != null)
                            PlayFootstep(FootstepVolumeScale * DaggerfallUnity.Settings.SoundVolume);

                        return;
                    }
                }
            }

            if (IsStandingStill())
                return;

            // Get distance player travelled horizontally
            Vector3 position = GetHorizontalPosition();
            distance += Vector3.Distance(position, lastPosition);
            lastPosition = position;

            // Get threshold
            float threshold = IsRunning() ? RunStepInterval : WalkStepInterval;

            // Play sound if over distance threshold
            if (distance > threshold && customAudioSource && currentFootstepSoundList != null)
            {
                float volumeScale = FootstepVolumeScale;
                if (IsMovingLessThanHalfSpeed())
                    volumeScale *= 0.5f;

                PlayFootstep(volumeScale * DaggerfallUnity.Settings.SoundVolume);

                distance = 0f;
            }
        }

        private void PlayFootstep(float volume)
        {
            if (currentFootstepSoundList == null)
                return;

            currentFootstepSoundList.PlayRandomClip(dfAudioSource, customAudioSource, volume);

            if (currentFootstepSoundList != this.FootstepSoundSubmerged && HasArmor())
            {
                FootstepsArmor.PlayRandomClip(dfAudioSource, customAudioSource, volume);
            }
        }

        private Vector3 GetHorizontalPosition()
        {
            return new Vector3(transform.position.x, 0, transform.position.z);
        }

        protected virtual bool FootstepsEnabled()
        {
            return true;
        }

        protected virtual bool HasArmor()
        {
            DaggerfallUnityItem chest = entityBehaviour.Entity.ItemEquipTable.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem legs = entityBehaviour.Entity.ItemEquipTable.GetItem(EquipSlots.LegsArmor);

            if (chest != null && chest.NativeMaterialValue != (int)ArmorMaterialTypes.Leather)
                return true;

            if (legs != null && legs.NativeMaterialValue != (int)ArmorMaterialTypes.Leather)
                return true;

            return false;
        }

        protected virtual bool IsOnStaticGeometry()
        {
            return false;
        }

        protected virtual bool IsOnExteriorPath()
        {
            return false;
        }

        protected virtual bool IsOnExteriorWater()
        {
            return false;
        }

        protected virtual bool IsGrounded()
        {
            return false;
        }

        protected virtual bool IsSwimming()
        {
            return false;
        }

        protected virtual bool IsStandingStill()
        {
            return false;
        }

        protected virtual bool IsRunning()
        {
            return false;
        }
        protected virtual bool IsLevitating()
        {
            return false;
        }

        protected virtual bool IsMovingLessThanHalfSpeed()
        {
            return false;
        }

    }
}