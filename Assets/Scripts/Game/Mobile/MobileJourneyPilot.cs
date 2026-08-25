// Project:         Daggerfall Unity iOS touch port
// Copyright:       Copyright (c) 2009-2023 Daggerfall Workshop
// License:         MIT License (LICENSE file)
//
// Derived from Tedious Travel by TheNewBob / Jedidia, used under the MIT License:
//     MIT License, Copyright (c) 2018 TheNewBob
//     https://github.com/TheNewBob/TediousTravel
//
// Adapted for this port: the reflection hack is gone (we own the engine source, so
// InputManager.ApplyVerticalForce is called directly), dependencies resolve lazily rather
// than at construction, per-frame logging is removed, and a hand-off was added so the
// touch look zone stands down while a journey drives the camera.

using System;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Utility;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Walks the player toward a travel destination under accelerated time, instead of
    /// teleporting there. One journey leg: point the body at the destination, hold forward,
    /// and raise OnArrival once the player is inside the destination's rect.
    ///
    /// WHY THIS STEERS THE BODY DIRECTLY RATHER THAN FEEDING INPUT
    /// The player could in principle be driven by synthesising look input, but the touch
    /// layer already owns the mouse axes - it feeds mouseX/mouseY from the look zone, which
    /// in turn drive both PlayerMouseLook.ApplyLook() and the virtual cursor. Two writers on
    /// one channel means the journey and the player's thumb fight for the camera, and the
    /// thumb wins whenever it moves. So a journey sets the body's yaw outright and disables
    /// mouse look for its duration; <see cref="Active"/> tells the touch layer to stop
    /// pumping look for the same window. Exactly one writer at a time.
    /// </summary>
    public class MobileJourneyPilot
    {
        // How far outside the destination's own rect a journey stops. Arriving is a separate
        // act from entering: stopping short leaves the player outside the gates, facing the
        // place, free to walk in (or not) under their own control.
        const float arrivalMarginWorldUnits = 1000f;

        /// <summary>
        /// True while any journey is steering the player. The touch input layer checks this
        /// and stops feeding look deltas, so the thumb cannot fight the journey for the
        /// camera. Static because there is only ever one player to steer.
        /// </summary>
        public static bool Active { get; private set; }

        readonly ContentReader.MapSummary destinationSummary;
        DFPosition destinationMapPixel;
        Rect destinationWorldRect;

        // Last map pixel the player was seen in. Yaw is only recomputed when this changes,
        // which is far less often than every frame and is frequent enough to stay on course
        // over a journey of hundreds of map pixels.
        DFPosition lastPlayerMapPixel = new DFPosition(int.MaxValue, int.MaxValue);
        bool inDestinationMapPixel;

        float journeyYaw;

        // Captured on the first frame that takes the camera, restored on release. Assuming
        // what "normal" looks like is how a journey ends up editing settings that were never
        // its to change.
        bool priorEnableMouseLook = true;
        bool priorSimpleCursorLock;
        bool cameraTaken;

        public MobileJourneyPilot(ContentReader.MapSummary destinationSummary)
        {
            this.destinationSummary = destinationSummary;

            destinationMapPixel = MapsFile.GetPixelFromPixelID(destinationSummary.ID);
            destinationWorldRect = ArrivalRect(GetLocationRect(destinationSummary));
        }

        // Resolved on use, not in field initialisers. A journey can be constructed from a UI
        // window during a scene change, when GameManager.Instance is mid-rebuild; touching
        // it that early throws or - worse - caches a stale PlayerMouseLook that belongs to
        // the previous scene's player object.
        static PlayerGPS Gps { get { return GameManager.Instance.PlayerGPS; } }
        static PlayerMouseLook MouseLook { get { return GameManager.Instance.PlayerMouseLook; } }
        static InputManager Input { get { return InputManager.Instance; } }

        /// <summary>Call once per frame while the journey runs.</summary>
        public void Update()
        {
            if (!IsPlayerReady())
                return;

            Active = true;

            if (inDestinationMapPixel && IsPlayerInArrivalRect())
            {
                RaiseOnArrival();
                return;
            }

            DFPosition playerPixel = Gps.CurrentMapPixel;
            if (playerPixel.X != lastPlayerMapPixel.X || playerPixel.Y != lastPlayerMapPixel.Y)
            {
                lastPlayerMapPixel = playerPixel;
                journeyYaw = YawTowardDestination();

                inDestinationMapPixel = playerPixel.X == destinationMapPixel.X &&
                                        playerPixel.Y == destinationMapPixel.Y;
            }

            PlayerMouseLook mouseLook = MouseLook;

            // Level the view and point the body down the journey's bearing. Pitch is zeroed
            // rather than preserved: a journey that inherits whatever the player was last
            // looking at can spend the whole trip staring at the sky or their own feet.
            mouseLook.GetComponent<Transform>().localEulerAngles = Vector3.zero;
            mouseLook.characterBody.transform.localEulerAngles = new Vector3(0f, journeyYaw, 0f);

            // Snapshot before the first change, so release puts back what was actually there.
            if (!cameraTaken)
            {
                priorEnableMouseLook = mouseLook.enableMouseLook;
                priorSimpleCursorLock = mouseLook.simpleCursorLock;
                cameraTaken = true;
            }

            // Hold mouse look off for the journey's duration. This is re-asserted every frame
            // on purpose - opening and closing a UI window re-enables it, so setting it once
            // at journey start would silently stop working the first time the player checked
            // their inventory en route.
            mouseLook.simpleCursorLock = true;
            mouseLook.enableMouseLook = false;

            // FORWARD MOVEMENT IS NOT APPLIED HERE.
            //
            // It lives in InputManager.Update(), next to ToggleAutorun. Applying it from this
            // class meant applying it from a MonoBehaviour whose Update order relative to
            // InputManager is undefined - and InputManager clears the impulse flags at the top
            // of its Update, then decays the axis in ApplyFriction() at the bottom when no
            // impulse was raised. Whenever this ran first, the force was wiped before
            // PlayerMotor read it: the player stood still for the entire journey while the
            // clock ran. Steering stays here; driving belongs where the engine drives.
        }

        /// <summary>
        /// Hand the camera back. Must be called on every exit path - arrival, interruption,
        /// or the player cancelling - or mouse look stays dead and the touch layer stays
        /// stood down, which reads to the player as the game having frozen its camera.
        /// </summary>
        public void Release()
        {
            Active = false;

            if (!IsPlayerReady())
                return;

            PlayerMouseLook mouseLook = MouseLook;

            if (cameraTaken)
            {
                mouseLook.enableMouseLook = priorEnableMouseLook;
                mouseLook.simpleCursorLock = priorSimpleCursorLock;
                cameraTaken = false;
            }

            // Leave the player looking where they were going, so the destination is in front
            // of them when control returns.
            mouseLook.Pitch = 0f;
            mouseLook.Yaw = journeyYaw;
        }

        /// <summary>
        /// The player is inside the (widened) destination rect. Deliberately not PlayerGPS's
        /// own location test, which uses the true rect and would only fire once the player
        /// had already walked into the location.
        /// </summary>
        bool IsPlayerInArrivalRect()
        {
            PlayerGPS gps = Gps;
            return destinationWorldRect.Contains(new Vector2(gps.WorldX, gps.WorldZ));
        }

        float YawTowardDestination()
        {
            PlayerGPS gps = Gps;
            return BearingDegrees(gps.WorldX, gps.WorldZ,
                                  destinationWorldRect.center.x, destinationWorldRect.center.y);
        }

        /// <summary>
        /// Unity yaw, in degrees, pointing from one world position toward another.
        /// 0 faces +Z (north), 90 faces +X (east).
        ///
        /// Pure and static so it can be tested headlessly - the walking itself needs a device,
        /// but getting the bearing wrong would send the player away from the destination for
        /// the whole journey, and that is worth catching on the desk.
        /// </summary>
        public static float BearingDegrees(float fromX, float fromZ, float toX, float toZ)
        {
            double deg = Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI;

            // Normalised to 0-360. localEulerAngles tolerates negatives, but a stable range
            // makes the value comparable and testable.
            if (deg < 0.0)
                deg += 360.0;

            return (float)deg;
        }

        /// <summary>
        /// Grow a location's rect into the rect a journey stops in. Arriving is a separate act
        /// from entering: stopping short leaves the player outside, facing the place.
        /// </summary>
        public static Rect ArrivalRect(Rect locationRect)
        {
            Rect r = locationRect;
            r.xMin -= arrivalMarginWorldUnits;
            r.xMax += arrivalMarginWorldUnits;
            r.yMin -= arrivalMarginWorldUnits;
            r.yMax += arrivalMarginWorldUnits;
            return r;
        }

        static bool IsPlayerReady()
        {
            return GameManager.HasInstance &&
                   GameManager.Instance.PlayerGPS != null &&
                   GameManager.Instance.PlayerMouseLook != null &&
                   GameManager.Instance.PlayerMouseLook.characterBody != null &&
                   InputManager.Instance != null;
        }

        public static Rect GetLocationRect(ContentReader.MapSummary mapSummary)
        {
            DFLocation location;
            if (!DaggerfallUnity.Instance.ContentReader.GetLocation(
                    mapSummary.RegionIndex, mapSummary.MapIndex, out location))
                throw new ArgumentException("Journey destination not found in map data.");

            return DaggerfallLocation.GetLocationRect(location);
        }

        public delegate void OnArrivalHandler();
        public event OnArrivalHandler OnArrival;

        void RaiseOnArrival()
        {
            Release();

            if (OnArrival != null)
                OnArrival();
        }
    }
}
