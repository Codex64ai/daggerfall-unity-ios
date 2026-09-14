// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/CameraShake/CameraShakeMod.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using System;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace SpellcastStudios.CameraShake
{
    public class CameraShakeMod : MonoBehaviour
    {
        private static Mod mod;
        private CameraShaker cameraShaker;
        private DamageShaker damageShaker;


        // MOBILE: [Invoke] and Init removed; BetterAmbiencePort creates this component and sets the Mod.
        public static void SetMod(Mod m) { mod = m; }

        private void Start()
        {
            mod.IsReady = true;

            SetUpPlayer();
            LoadSettings(mod.GetSettings());
        }

        private void LoadSettings(ModSettings settings)
        {
            damageShaker.fadeInTime = settings.GetValue<float>("Camera Shake", "fadeInTime");
            damageShaker.fadeOutTime = settings.GetValue<float>("Camera Shake", "fadeOutTime");
            damageShaker.maxShake = settings.GetValue<float>("Camera Shake", "maxShake");
            damageShaker.roughness = settings.GetValue<float>("Camera Shake", "roughness");
            damageShaker.shakeAmountAdd = settings.GetValue<float>("Camera Shake", "shakeAmountAdd");
            damageShaker.shakeAmountMultiplier = settings.GetValue<float>("Camera Shake", "shakeAmountMultiplier");
        }

        private void SetUpPlayer()
        {
            //Inject a new object between the "smooth follow" object and the camera.
            GameObject shakeObject = new GameObject();
            shakeObject.name = "Shaker";
            shakeObject.transform.parent = GameManager.Instance.MainCamera.transform.parent;
            shakeObject.transform.localPosition = Vector3.zero;
            shakeObject.transform.localRotation = Quaternion.identity;

            //Set as first sibling, since some mods (IE Improved interior lighting) require nothing to change in hiearchy
            shakeObject.transform.SetAsFirstSibling();

            cameraShaker = shakeObject.AddComponent<CameraShaker>();

            GameManager.Instance.MainCamera.transform.parent = shakeObject.transform;

            damageShaker = GameManager.Instance.PlayerObject.gameObject.AddComponent<DamageShaker>();
            damageShaker.SetPlayer(GameManager.Instance.PlayerEntity, cameraShaker);

        }
    }
}