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

                // MOBILE: (D2) a failed tile is filled FLAT at its own map pixel's height, taken from
                // MOBILE: the mod's own altered world heightmap, rather than handed to
                // MOBILE: DefaultTerrainSampler. The default sampler generates its relief from a
                // MOBILE: DIFFERENT height model - WOODS.WLD bicubic plus its own noise - and even with
                // MOBILE: MaxTerrainHeight pointed at ours (constructor above) there is nothing tying
                // MOBILE: its output to what THIS generator put in the eight neighbouring tiles: a
                // MOBILE: fallen-back tile can land hundreds of units below them, which is a
                // MOBILE: tile-sized pit with vertical walls - the exact artefact the containment is
                // MOBILE: supposed to survive, manufactured by the containment itself. Flat at the map
                // MOBILE: pixel's own altered height cannot do that: it is the same number
                // MOBILE: LocationWeight flattens towns to, the same one the travel map shows, and the
                // MOBILE: same scale the neighbours were normalised in. Boring ground, continuous edges.
                FillFlatFromWorldHeightmap(ref mapPixel);
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

                // MOBILE: (D1) after the readback, whether it came from the GPU or the flat fallback.
                DiagnoseTile(ref mapPixel, n, ref computer);
            }
        }

        public override JobHandle ScheduleGenerateSamplesJob(ref MapPixelData mapPixel)
        {
            GenerateSamples(ref mapPixel);


            return new JobHandle();
        }

        /// <summary>
        /// MOBILE: (D2) fill the whole tile with the normalised height the mod's own altered world
        /// heightmap holds for this map pixel, clamped to at least ocean level so the tile is ground and
        /// not sea floor. Falls back to DFU's sampler ONLY when there is no altered heightmap at all -
        /// i.e. the start-up pass never ran, in which case there is no WoD relief anywhere to be
        /// discontinuous with. DefaultTerrainSampler.GenerateSamples throws NotImplementedException by
        /// design (it only implements the jobified path), so that branch completes the job inline.
        /// </summary>
        private void FillFlatFromWorldHeightmap(ref MapPixelData mapPixel)
        {
            float h = TerrainComputer.WorldHeightmapSample(
                TerrainComputer.alteredHeightmapBuffer, mapPixel.mapPixelX, mapPixel.mapPixelY);

            if (h < 0f || !mapPixel.heightmapData.IsCreated)
            {
                fallback.ScheduleGenerateSamplesJob(ref mapPixel).Complete();
                return;
            }

            h = Mathf.Max(h, OceanElevation / MaxTerrainHeight);

            var data = mapPixel.heightmapData;
            for (int i = 0; i < data.Length; i++)
                data[i] = h;

            Debug.LogWarning(string.Format(
                "[WoDTerrain] tile {0},{1} filled flat from the world heightmap (height {2:F4})",
                mapPixel.mapPixelX, mapPixel.mapPixelY, h));
        }

        /// <summary>
        /// MOBILE: (D1) one pass over the tile's 129x129 readback - min, max, mean, NaN count, the
        /// largest step between adjacent samples and how many samples sit on the pit floor - plus the
        /// map pixel's own height out of the altered world heightmap (which is what the tile should be
        /// centred on) and the lowest height any of this tile's locations will be flattened to.
        ///
        /// One line for EVERY tile, not a sample: a terrain distance of 3 is 49 tiles, so a whole
        /// session is 49 lines, and the point of them is to be read against the simulator baseline for
        /// the same map pixels in .superpowers/sdd/2026-09-10-terrain-pits/player-sim460.log. The
        /// inputs match exactly there (locations 46 at 460,51 on both the device and the simulator), so
        /// any tile whose numbers differ from the baseline's is the bug, and no threshold has to be
        /// guessed. SUSPECT still marks the three unambiguous shapes IsSuspectTile names.
        ///
        /// ~16,600 float compares and ~50 bilinear texture reads per tile against a 5-315 ms dispatch:
        /// unmeasurable, and it runs on the fallback path too, which is exactly where a pit would
        /// otherwise be silent.
        /// </summary>
        private void DiagnoseTile(ref MapPixelData mapPixel, int tileNumber, ref TerrainComputer computer)
        {
            var data = mapPixel.heightmapData;
            if (!data.IsCreated || data.Length == 0)
                return;

            float min = float.MaxValue, max = float.MinValue, sum = 0f, maxStep = 0f;
            int nan = 0, atFloor = 0;

            // MOBILE: (D1) heightmapData is row-major at HeightmapDimension, so i-1 is the sample to the
            // MOBILE: west (except across a row boundary, skipped) and i-dim the one to the south. One
            // MOBILE: pass, two compares per sample.
            int dim = HeightmapDimension;
            for (int i = 0; i < data.Length; i++)
            {
                float h = data[i];
                if (float.IsNaN(h) || float.IsInfinity(h))
                {
                    nan++;
                    continue;
                }
                if (h < min) min = h;
                if (h > max) max = h;
                if (h <= TerrainComputer.PitFloor) atFloor++;
                sum += h;

                if (i >= dim)
                {
                    float d = Mathf.Abs(h - data[i - dim]);
                    if (d > maxStep) maxStep = d;
                }
                if (i % dim != 0)
                {
                    float d = Mathf.Abs(h - data[i - 1]);
                    if (d > maxStep) maxStep = d;
                }
            }

            int good = data.Length - nan;
            if (good == 0)
            {
                min = 0f;
                max = 0f;
            }
            float mean = good > 0 ? sum / good : 0f;
            float mapPixelBase = TerrainComputer.WorldHeightmapSample(
                TerrainComputer.alteredHeightmapBuffer, mapPixel.mapPixelX, mapPixel.mapPixelY);

            bool suspect = TerrainComputer.IsSuspectTile(min, max, nan);

            Debug.Log(string.Format(
                "[WoDTerrain] tile {0},{1} {2} min={3:F4} max={4:F4} mean={5:F4} nan={6} step={7:F4} " +
                "floor={8}/{9} base={10:F4} minFlat={11:F4}@{12} locations={13}",
                mapPixel.mapPixelX, mapPixel.mapPixelY, suspect ? "SUSPECT" : "sample",
                min, max, mean, nan, maxStep, atFloor, data.Length, mapPixelBase,
                computer.minLocationFlattenHeight, computer.minLocationFlattenIndex, computer.locationCount));

            if (!suspect)
                return;

            // MOBILE: (D1) for a suspect tile, the three rects most likely to have made the pit: the
            // MOBILE: lowest-flattening one and the two after it. The static arrays still hold THIS
            // MOBILE: tile's rects - the sampler's finally runs before the next tile is dispatched - so
            // MOBILE: this reads the exact numbers the shader was handed, not a reconstruction.
            int first = Mathf.Max(computer.minLocationFlattenIndex, 0);
            for (int i = first; i < computer.locationCount && i < first + 3; i++)
            {
                Debug.Log(string.Format(
                    "[WoDTerrain]   suspect location {0} rect min=({1:F1},{2:F1}) size=({3:F1},{4:F1}) flattenHeight={5:F4}",
                    i,
                    TerrainComputer.LocationPositions[i].x, TerrainComputer.LocationPositions[i].y,
                    TerrainComputer.LocationSizes[i].x, TerrainComputer.LocationSizes[i].y,
                    TerrainComputer.LocationFlattenHeight(
                        TerrainComputer.LocationPositions[i], TerrainComputer.LocationSizes[i])));
            }
        }

        // MOBILE: (g)
        private static void LogOnce(string key, string message)
        {
            if (loggedFailures.Add(key))
                Debug.LogError(message);
        }
    }
}
