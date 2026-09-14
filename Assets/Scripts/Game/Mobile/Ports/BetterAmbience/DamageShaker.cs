// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/CameraShake/DamageShaker.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using DaggerfallWorkshop.Game.Entity;

namespace SpellcastStudios.CameraShake
{
    public class DamageShaker : MonoBehaviour
    {
        public float shakeAmountAdd = 0;
        public float shakeAmountMultiplier = 10;
        public float maxShake = 10;
        public float roughness = 10;

        public float fadeInTime = 0.3f;
        public float fadeOutTime = 0.5f;

        private PlayerEntity player;
        private CameraShaker shaker;

        public void SetPlayer(PlayerEntity player, CameraShaker shaker)
        {
            this.player = player;
            this.shaker = shaker;
        }

        void RemoveHealth(int amount)
        {
            if (player == null || shaker == null)
                return;

            float shakeAmount = shakeAmountAdd + (shakeAmountMultiplier * amount / player.MaxHealth);
            shakeAmount = Mathf.Clamp(shakeAmount, 0, maxShake);

            shaker.ShakeOnce(shakeAmount, roughness, fadeInTime, fadeOutTime);
        }
    }
}