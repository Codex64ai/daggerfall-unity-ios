// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/InterestingTerrainSampler.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using DaggerfallWorkshop;
using Unity.Jobs;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;                           // MOBILE: (g) Exception
using System.Collections.Generic;       // MOBILE: (g) the log-once set
using UnityEngine;                      // MOBILE: (g)(h) Debug

namespace Monobelisk
{
    public class InterestingTerrainSampler : TerrainSampler
    {
        // Declaring as class-level variables
        private Mod WOMod;
        private bool WOModEnabled;

        // MOBILE: (g) failure containment. The mod has no CPU generator, so the only thing a failed
        // MOBILE: tile can fall back to is DFU's own sampler: one bad tile becomes a hole in fidelity
        // MOBILE: rather than an exception escaping into StreamingWorld's coroutine.
        private readonly DefaultTerrainSampler fallback = new DefaultTerrainSampler();

        // MOBILE: (g) one log line per distinct failure, not one per tile - a systematic failure would
        // MOBILE: otherwise write a Player.log entry for every terrain tile for the rest of the session.
        private static readonly HashSet<string> loggedFailures = new HashSet<string>();

        // MOBILE: (h) tiles generated this session, for the "first 10 then every 25th" log sampling.
        private static int tilesGenerated;

        // Constructor
        public InterestingTerrainSampler()
        {
            // Accessing the ModManager and checking if the WOMod is enabled
            WOMod = ModManager.Instance.GetModFromGUID("2beb90e5-de58-43cf-b61c-46652f5ecbe3");
            WOModEnabled = WOMod != null && WOMod.Enabled;

            HeightmapDimension = defaultHeightmapDimension;
            MaxTerrainHeight = 5000f;
            MeanTerrainHeightScale = 5000f / 255f;
            OceanElevation = 100.01f;
            BeachElevation = 103.9f;

            // MOBILE: (g) the fallback normalises its samples against its OWN MaxTerrainHeight, and
            // MOBILE: mapPixel.maxHeight has already been set to ours by the time it runs. Point it at
            // MOBILE: our dimension and height so a fallen-back tile keeps the same vertical scale as
            // MOBILE: its neighbours (lower relief, but continuous ground) instead of being 3x too tall.
            fallback.HeightmapDimension = HeightmapDimension;
            fallback.MaxTerrainHeight = MaxTerrainHeight;
        }

        public override int Version
        {
            get { return 7; }
        }

        public override bool IsLocationTerrainBlended()
        {
            return true;
        }

        public override void GenerateSamples(ref MapPixelData mapPixel)
        {
            mapPixel.maxHeight = MaxTerrainHeight;

            // MOBILE: (h) timing evidence - the device test's only measurement of per-tile cost.
            // MOBILE: fully qualified - a "using System.Diagnostics" would make Debug ambiguous with UnityEngine.Debug.
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

            // MOBILE: (g)(I1) try/catch/finally around the whole GPU path, TerrainComputer.Create
            // MOBILE: INCLUDED. Create calls BufferIO.CreateHeightmapBuffers, which allocates three
            // MOBILE: ComputeBuffers - the one thing here that can realistically throw on iOS, under
            // MOBILE: exactly the memory pressure this port adds. Left outside the try it would skip
            // MOBILE: the fallback tile and the timing line and let the exception kill StreamingWorld's
            // MOBILE: terrain coroutine for the rest of the session - strictly worse than the crash the
            // MOBILE: containment replaces. What this finally CANNOT do on that path is release those
            // MOBILE: buffers - `computer` is assigned only when Create RETURNS - so (N1)
            // MOBILE: CreateHeightmapBuffers releases whatever it had allocated before it rethrows.
            // MOBILE: default() leaves heightmapBuffers with five nulls, which the null-safe Dispose
            // MOBILE: handles.
            TerrainComputer computer = default(TerrainComputer);

            try
            {
                computer = TerrainComputer.Create(mapPixel, this);
                computer.DispatchAndProcess(InterestingTerrains.csPrototype, ref mapPixel, InterestingTerrains.instance.csParams);
            }
            catch (Exception ex)
            {
                LogOnce(ex.GetType().FullName + "|" + ex.Message, "[WoDTerrain] tile failed: " + ex);

                // DefaultTerrainSampler.GenerateSamples throws NotImplementedException by design - the
                // MOBILE: default sampler only implements the jobified path, so complete it inline here.
                fallback.ScheduleGenerateSamplesJob(ref mapPixel).Complete();
            }
            finally
            {
                // MOBILE: (g) idempotent and null-safe (see HeightmapBufferCollection.Dispose), so this
                // MOBILE: runs whether the readback disposed the buffers already or never got to them.
                computer.heightmapBuffers.Dispose();

                watch.Stop();

                // MOBILE: (h) first ten tiles then every twenty-fifth, so a walk logs steadily and a
                // MOBILE: fast-travel arrival (49 tiles in one non-yielding call) still gets sampled.
                int n = ++tilesGenerated;
                if (n <= 10 || n % 25 == 0)
                {
                    Debug.Log(string.Format("[WoDTerrain] tile {0},{1} {2} ms (locations {3})",
                        mapPixel.mapPixelX, mapPixel.mapPixelY, watch.ElapsedMilliseconds, computer.locationCount));
                }
            }
        }

        public override JobHandle ScheduleGenerateSamplesJob(ref MapPixelData mapPixel)
        {
            GenerateSamples(ref mapPixel);


            return new JobHandle();
        }

        // MOBILE: (g)
        private static void LogOnce(string key, string message)
        {
            if (loggedFailures.Add(key))
                Debug.LogError(message);
        }
    }
}
