// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/BetterFootsteps/BetterFootstepsComponentPlayer.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using System.Collections.Generic;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Mobile;

namespace SpellcastStudios.BetterFootsteps
{
    public class BetterFootstepsComponentPlayer : BetterFootstepsComponent
    {
        PlayerMotor playerMotor;

        private bool disableFootsteps;
        private float lastTravelOptionsCheckTime;
        protected override void Start()
        {
            playerMotor = GetComponent<PlayerMotor>();

            base.Start();
        }

        protected override void Update()
        {
            // MOBILE: ask the port's own autopilot instead of round-tripping through a mod message.
            // MobileTravelOptionsBridge answers "isTravelActive" from exactly this flag, so the answer
            // is identical - this just skips the allocation of a closure and a callback every half
            // second, on the player's Update, for a value that is one static bool away.
            if (Time.time > lastTravelOptionsCheckTime + 0.5f)
            {
                disableFootsteps = MobileJourneyPilot.Active;
                lastTravelOptionsCheckTime = Time.time;
            }

            base.Update();
        }

        protected override bool FootstepsEnabled()
        {
            return !disableFootsteps;
        }

        protected override bool IsOnExteriorWater()
        {
            return playerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming || playerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.WaterWalking;
        }

        protected override bool IsOnExteriorPath()
        {
            return playerMotor.OnExteriorPath;
        }

        protected override bool IsOnStaticGeometry()
        {
            return playerMotor.OnExteriorStaticGeometry;
        }

        protected override bool IsRunning()
        {
            return playerMotor.IsRunning;
        }

        protected override bool IsLevitating()
        {
            return playerMotor.IsLevitating;
        }

        protected override bool IsMovingLessThanHalfSpeed()
        {
            return playerMotor.IsMovingLessThanHalfSpeed;
        }

        protected override bool IsGrounded()
        {
            return playerMotor.IsGrounded;
        }

        protected override bool IsSwimming()
        {
            return playerMotor.IsSwimming;
        }

        protected override bool IsStandingStill()
        {
            return playerMotor.IsStandingStill;
        }
    }
}