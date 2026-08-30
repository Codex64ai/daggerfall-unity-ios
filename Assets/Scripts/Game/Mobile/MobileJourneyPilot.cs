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

        // Real seconds of no movement before assuming something is in the way. Long enough not
        // to trip on a moment's contact with a fence or a slope, short enough that the player
        // does not watch a wall for long.
        const float blockedThresholdSeconds = 0.35f;

        // How far off the true bearing to steer while trying to get around an obstacle, and how
        // long to hold each attempt. Alternating sides at a widening angle walks a building
        // corner far more reliably than one fixed offset.
        static readonly float[] sidestepAngles = { 55f, -55f, 90f, -90f, 130f, -130f };
        const float sidestepSeconds = 0.7f;

        // After the sidesteps fail, one straight nudge through whatever it is (fence, low wall,
        // rock) before giving up. World units; a map pixel is 32768.
        const float nudgeWorldUnits = 300f;
        bool nudged;

        // How fast the body may turn toward the bearing, degrees per real second. The bearing
        // used to be recomputed only on crossing a map-pixel boundary, so the player drifted
        // for up to a pixel and was then yanked back onto the road (device report). Now it is
        // recomputed every frame and followed at this rate: a curve, not a snap.
        const float turnRateDegPerSec = 360f;

        // Progress-based stuck test. Standing against a wall at an angle SLIDES along it, which
        // the not-moving test counts as movement - so a walled city (device report: Burgwall)
        // never registered as an obstacle. If the distance to the target has not improved by
        // progressEpsilon in noProgressSeconds of real time, we are stuck whatever the feet say.
        const float noProgressSeconds = 1.0f;
        const float progressEpsilon = 8f;
        float bestDistanceToTarget = float.MaxValue;
        float lastProgressTime;

        // Attempts before giving up. Six alternating angles is a genuine try; more than that and
        // the player is somewhere a journey should not continue from.
        const int maxSidestepAttempts = 6;

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
        bool finalTarget = true;

        // Last position and how far the player moved since, in world units per frame. Used to
        // size the waypoint arrival radius: at high time compression a frame can cover more
        // ground than a waypoint's rect is wide, and a fixed radius would be flown straight
        // through without ever registering.
        float lastX, lastZ;
        bool haveLast;
        float perFrameDistance;

        // BLOCKED-PATH RECOVERY
        // The pilot steers a straight bearing and holds forward, which walks into whatever is
        // in the way. In a town that is a building, and the player stands against a wall
        // pushing forward for the rest of the journey. Device report: "the player gets stuck
        // behind a building if in a town".
        //
        // So: notice no progress, try to walk around it, and if that fails give up rather than
        // pinning the player somewhere they cannot see a reason for.
        float blockedFor;
        float steerOffset;
        int sidestepAttempt;
        float sidestepUntil;

        // Captured on the first frame that takes the camera, restored on release. Assuming
        // what "normal" looks like is how a journey ends up editing settings that were never
        // its to change.
        bool priorEnableMouseLook = true;
        bool priorSimpleCursorLock;
        bool cameraTaken;

        // Map pixel geometry, Basic Roads / Travel Options values. A path target is a small
        // rect at the centre of a map pixel rather than the whole pixel: aiming at the centre
        // keeps a route on the road, where aiming at the pixel would let the player clip its
        // corner and count as arrived while still in open country.
        const int mpWorldUnits = 32768;
        const int halfMpWorldUnits = mpWorldUnits / 2;
        const int tileSize = mpWorldUnits / MapsFile.WorldMapTileDim;
        const int pathSize = tileSize * 2;
        const int midLo = halfMpWorldUnits - tileSize;

        public MobileJourneyPilot(ContentReader.MapSummary destinationSummary)
        {
            this.destinationSummary = destinationSummary;

            destinationMapPixel = MapsFile.GetPixelFromPixelID(destinationSummary.ID);
            destinationWorldRect = ArrivalRect(GetLocationRect(destinationSummary));
            finalTarget = true;
        }

        /// <summary>
        /// Aim at one step of a road route instead of the final destination. Called again for
        /// each waypoint, so one pilot walks the whole route rather than being rebuilt per hop -
        /// rebuilding would re-snapshot the camera state on every map pixel.
        /// </summary>
        public void SetWaypoint(DFPosition mapPixel)
        {
            destinationMapPixel = mapPixel;

            DFPosition world = MapsFile.MapPixelToWorldCoord(mapPixel.X, mapPixel.Y);
            destinationWorldRect = new Rect(world.X + midLo, world.Y + midLo, pathSize, pathSize);

            finalTarget = false;
            inDestinationMapPixel = false;
            ClearBlockedState();

            // Force a fresh bearing on the next frame; without this the pilot keeps steering at
            // the previous waypoint until the player happens to cross a map pixel boundary.
            lastPlayerMapPixel = new DFPosition(int.MaxValue, int.MaxValue);
        }

        /// <summary>Aim at the journey's real destination again, after the last waypoint.</summary>
        public void SetFinalTarget()
        {
            destinationMapPixel = MapsFile.GetPixelFromPixelID(destinationSummary.ID);
            destinationWorldRect = ArrivalRect(GetLocationRect(destinationSummary));
            finalTarget = true;
            inDestinationMapPixel = false;
            ClearBlockedState();
            lastPlayerMapPixel = new DFPosition(int.MaxValue, int.MaxValue);
        }

        /// <summary>True when aiming at the destination rather than a waypoint.</summary>
        public bool AtFinalTarget { get { return finalTarget; } }

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

            TrackMovement();
            UpdateBlockedRecovery();

            // The final destination is reached by entering its rect AND being in its map pixel -
            // its rect is deliberately widened past the location, so without the pixel test it
            // would fire from a neighbour.
            //
            // A waypoint uses a radius instead, sized to how fast the player is actually moving.
            // Its rect is 512 world units across where a map pixel is 32768, so at high
            // compression a single frame covers far more than the rect and the player passes
            // through without ever being inside it on a frame we look. Then the pilot steers at
            // a waypoint behind it, forever.
            bool arrived = finalTarget
                ? (IsPlayerInArrivalRect() && inDestinationMapPixel)
                : WithinWaypointRadius();

            if (arrived)
            {
                RaiseOnArrival();
                return;
            }

            DFPosition playerPixel = Gps.CurrentMapPixel;
            if (playerPixel.X != lastPlayerMapPixel.X || playerPixel.Y != lastPlayerMapPixel.Y)
            {
                bool firstFix = lastPlayerMapPixel.X == int.MaxValue;
                lastPlayerMapPixel = playerPixel;
                inDestinationMapPixel = playerPixel.X == destinationMapPixel.X &&
                                        playerPixel.Y == destinationMapPixel.Y;
                if (firstFix)
                    journeyYaw = YawTowardDestination();      // face the target at once on a new leg
            }

            // Steer toward the target every frame, at a bounded turn rate.
            journeyYaw = TurnToward(journeyYaw, YawTowardDestination(), turnRateDegPerSec * Time.unscaledDeltaTime);

            PlayerMouseLook mouseLook = MouseLook;

            // Level the view and point the body down the journey's bearing. Pitch is zeroed
            // rather than preserved: a journey that inherits whatever the player was last
            // looking at can spend the whole trip staring at the sky or their own feet.
            mouseLook.GetComponent<Transform>().localEulerAngles = Vector3.zero;
            // steerOffset is zero unless we are trying to get around something.
            mouseLook.characterBody.transform.localEulerAngles =
                new Vector3(0f, journeyYaw + steerOffset, 0f);

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
            ClearBlockedState();

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

        /// <summary>
        /// Watch for the player making no headway, and try to steer around whatever is in the
        /// way. Times are real seconds, not scaled: at 50x a scaled timer would declare the
        /// player stuck almost instantly.
        /// </summary>
        void UpdateBlockedRecovery()
        {
            // A sidestep in progress runs for its full duration before being judged. Cutting it
            // short the moment the player moves would abandon the manoeuvre halfway around a
            // corner, straight back into the wall.
            if (sidestepUntil > 0f)
            {
                if (Time.unscaledTime < sidestepUntil)
                    return;

                sidestepUntil = 0f;
                steerOffset = 0f;
                blockedFor = 0f;
                return;
            }

            // Moving is the normal case: forget everything and carry on. The threshold is a
            // small fraction of a frame's expected travel, so this is "genuinely not moving"
            // rather than "moving slowly uphill".
            bool noProgress = Time.unscaledTime - lastProgressTime > noProgressSeconds;
            if (perFrameDistance > 1f && !noProgress)
            {
                blockedFor = 0f;
                sidestepAttempt = 0;
                nudged = false;
                return;
            }

            blockedFor += Time.unscaledDeltaTime;
            if (blockedFor < blockedThresholdSeconds)
                return;

            if (sidestepAttempt >= maxSidestepAttempts)
            {
                // Last resort before giving up: step straight through. Once.
                if (!nudged && NudgeForward(nudgeWorldUnits))
                {
                    nudged = true;
                    blockedFor = 0f;
                    sidestepAttempt = 0;
                    return;
                }
                RaiseOnBlocked();
                return;
            }

            steerOffset = sidestepAngles[sidestepAttempt];
            sidestepUntil = Time.unscaledTime + sidestepSeconds;
            sidestepAttempt++;
        }

        void TrackMovement()
        {
            PlayerGPS gps = Gps;

            // Progress toward the target, independent of how the feet are moving.
            float distance = DistanceToTarget;
            if (distance < bestDistanceToTarget - progressEpsilon)
            {
                bestDistanceToTarget = distance;
                lastProgressTime = Time.unscaledTime;
            }


            if (haveLast)
            {
                float dx = gps.WorldX - lastX;
                float dz = gps.WorldZ - lastZ;
                perFrameDistance = Mathf.Sqrt(dx * dx + dz * dz);
            }

            lastX = gps.WorldX;
            lastZ = gps.WorldZ;
            haveLast = true;
        }

        /// <summary>
        /// Close enough to a waypoint to call it reached. The radius is the larger of the
        /// waypoint's own size and the distance covered last frame with margin - so however
        /// fast a journey runs, the waypoint cannot be stepped over.
        /// </summary>
        bool WithinWaypointRadius()
        {
            PlayerGPS gps = Gps;
            Vector2 centre = destinationWorldRect.center;

            float dx = gps.WorldX - centre.x;
            float dz = gps.WorldZ - centre.y;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            return distance <= WaypointRadius(perFrameDistance);
        }

        /// <summary>
        /// How close counts as reaching a waypoint, given how far the player moved last frame.
        /// Pure and static so the overshoot guarantee can be tested headlessly - the failure it
        /// prevents (a journey stuck steering at a waypoint it already passed) only appears at
        /// high time compression, which is exactly what is hard to reproduce on demand.
        /// </summary>
        public static float WaypointRadius(float perFrameDistance)
        {
            return Mathf.Max(pathSize, perFrameDistance * 1.5f);
        }

        /// <summary>Distance to the current target, for progress reporting.</summary>
        public float DistanceToTarget
        {
            get
            {
                if (!IsPlayerReady())
                    return 0f;

                PlayerGPS gps = Gps;
                Vector2 centre = destinationWorldRect.center;
                float dx = gps.WorldX - centre.x;
                float dz = gps.WorldZ - centre.y;
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
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

        /// <summary>The bearing the body is currently being steered along, in Unity yaw degrees.</summary>
        public float JourneyYaw { get { return journeyYaw; } }

        /// <summary>Pure: turn from one yaw toward another by at most maxStep degrees.</summary>
        public static float TurnToward(float currentYaw, float targetYaw, float maxStep)
        {
            return Mathf.MoveTowardsAngle(currentYaw, targetYaw, Mathf.Max(0f, maxStep));
        }

        /// <summary>
        /// Pure: the point where a ray from p along yawDeg leaves rect, plus margin beyond it.
        /// If the ray never enters or has already left, the point is margin ahead of p.
        /// World axes: x east, y north; yaw 0 = north, 90 = east.
        /// </summary>
        public static Vector2 ExitPointThroughRect(Rect rect, Vector2 p, float yawDeg, float margin)
        {
            float rad = yawDeg * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            float tx = d.x > 1e-5f ? (rect.xMax - p.x) / d.x : d.x < -1e-5f ? (rect.xMin - p.x) / d.x : float.PositiveInfinity;
            float ty = d.y > 1e-5f ? (rect.yMax - p.y) / d.y : d.y < -1e-5f ? (rect.yMin - p.y) / d.y : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);
            if (float.IsInfinity(t) || t < 0f)
                t = 0f;
            return p + d * (t + margin);
        }

        /// <summary>
        /// Move the player straight along the bearing by the given distance, through whatever is
        /// there. Used as the last unstick step and to cross a settlement the journey is only
        /// passing through.
        /// </summary>
        public bool NudgeForward(float worldUnits)
        {
            return TeleportTo(Gps.WorldX + Mathf.Sin(journeyYaw * Mathf.Deg2Rad) * worldUnits,
                              Gps.WorldZ + Mathf.Cos(journeyYaw * Mathf.Deg2Rad) * worldUnits);
        }

        /// <summary>Put the player at a DFU world coordinate, grounded by StreamingWorld.</summary>
        public bool TeleportTo(float worldX, float worldZ)
        {
            if (!GameManager.HasInstance || GameManager.Instance.StreamingWorld == null)
                return false;
            try
            {
                GameManager.Instance.StreamingWorld.TeleportToWorldCoordinates(
                    Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldZ));
                haveLast = false;               // do not count the jump as movement
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Journey] teleport failed: " + e.Message);
                return false;
            }
        }

        void ClearBlockedState()
        {
            blockedFor = 0f;
            nudged = false;
            bestDistanceToTarget = float.MaxValue;
            lastProgressTime = Time.unscaledTime;
            steerOffset = 0f;
            sidestepAttempt = 0;
            sidestepUntil = 0f;
            haveLast = false;
            perFrameDistance = 0f;
        }

        /// <summary>True while steering around an obstacle rather than at the target.</summary>
        public bool WorkingAroundObstacle { get { return sidestepUntil > 0f; } }

        public delegate void OnBlockedHandler();
        public event OnBlockedHandler OnBlocked;

        void RaiseOnBlocked()
        {
            ClearBlockedState();

            if (OnBlocked != null)
                OnBlocked();
        }

        public delegate void OnArrivalHandler();
        public event OnArrivalHandler OnArrival;

        void RaiseOnArrival()
        {
            // Only hand the camera back when the journey is genuinely over. Releasing at every
            // waypoint would re-enable mouse look hundreds of times on a long route, and the
            // touch layer would fight for the camera between each hop.
            if (finalTarget)
                Release();

            if (OnArrival != null)
                OnArrival();
        }
    }
}
