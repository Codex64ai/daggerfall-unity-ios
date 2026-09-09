// MOBILE PORT - source: github.com/drcarademono/DFU-LocationLoader branch rmb-object
// @ 896a5741e5c9badd47fbb6c0f9926a95a79cb776, file Scripts/BiomesClimateSwap.cs.
// Copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
//
// Deliberately left out of the first Location Loader port (see UPSTREAM-PATCHES.md, "WOD-Biomes call
// begins: no BiomesClimateSwap"): upstream reads the climate map out of Location Loader's OWN bundle
// (LocationModLoader.climate_map, set from mod.GetAsset<Texture2D>("climate_map")), and the iOS
// Location Loader port is compiled in with no bundle and so no GetAsset. It is back now that the
// compiled-in World of Daggerfall - Biomes port owns that asset and publishes it as
// WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap.
//
// What it does: WoD Biomes' terrain-side overrider (WODClimates.cs) re-skins *batched* nature to
// Daggerfall Expanded Textures' archive 10030 in the subtropics. Location Loader's type-5 RMB blocks
// carry their nature as loose Billboard components built by RMBLayout.AddNatureFlats, which that
// batch walk never sees - without this file WoD's 32,600 type-5 blocks keep vanilla flats in a
// re-skinned landscape.
using UnityEngine;
// MOBILE: using System.Collections removed (nothing here is a coroutine), and with it the
// ModSupport / AssetInjection / DaggerfallConnect.Arena2 usings that only served the bundle lookup.
using System.Collections.Generic;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop;

namespace LocationLoader
{
    public static class BiomesClimateSwap
    {
        const int    CUSTOM_ARCHIVE = 10030;
        // MOBILE: upstream's `static Color32 TriggerColor = new Color32(255,165,0,255)` and its
        // component-by-component comparison are gone. The colour key now lives in one place for the
        // whole port - WorldOfDaggerfall.NatureBatchOverrider.IsSubtropicalKey, which is pure and
        // self-tested - so the terrain swap and this one cannot drift apart.

        // pending swaps waiting for terrain to be ready
        static bool isSubscribed = false;
        static List<GameObject> pendingBlocks = new List<GameObject>();

        // MOBILE: one log line per distinct failure, not one per RMB block. A WoD world has ~32,600
        // type-5 blocks and this runs as each one streams in.
        static readonly HashSet<string> loggedErrors = new HashSet<string>();
        static bool warnedUnreadableMap;
        static bool warnedDeferred;
        static bool warnedOutOfRange;

        // MOBILE: upstream did this in a static constructor. A static constructor fires on the first
        // touch of ANY member - including ShouldSwap, and including the editor self-test - so it
        // subscribed to a game event from contexts that have no streaming world. Subscribe instead at
        // the one place that needs the event: just before a block is put on the retry list. The static
        // flag keeps it to a single subscription for the life of the domain (upstream's flag guarded a
        // constructor that could only run once anyway).
        static void EnsureSubscribed()
        {
            if (isSubscribed)
                return;
            StreamingWorld.OnUpdateTerrainsEnd += OnUpdateTerrainsEnd;
            isSubscribed = true;
        }

        /// <summary>
        /// MOBILE: pure gate for the call site in LocationLoader's type-5 branch. Both halves matter:
        /// the Biomes launcher entry has to have actually started (its bundle supplies the map, and
        /// Daggerfall Expanded Textures supplies archive 10030), and the map has to be samplable on
        /// the CPU, because the colour test below is an exact match and GetPixel on an unreadable
        /// texture throws. Kept separate from ApplySwaps so the self-test can pin it.
        /// </summary>
        public static bool ShouldSwap(bool biomesRunning, Texture2D map)
        {
            return biomesRunning && WorldOfDaggerfall.NatureBatchOverrider.MapReadable(map);
        }

        public static void ApplySwaps(GameObject rmbBlock)
        {
            // MOBILE: whole body guarded. The call site sits inside LocationLoader's own try/catch,
            // which destroys the half-placed block when anything throws - a swap failure must not cost
            // the player the building. The retry path below has no protection at all upstream: a throw
            // there would take the rest of StreamingWorld.OnUpdateTerrainsEnd's subscribers with it,
            // the Biomes terrain overrider among them.
            try
            {
                ApplySwapsInner(rmbBlock);
            }
            catch (System.Exception ex)
            {
                if (loggedErrors.Add(ex.Message))
                    Debug.LogError("[Biomes] LL type-5 nature swap failed: " + ex);
            }
        }

        static void ApplySwapsInner(GameObject rmbBlock)
        {
            if (rmbBlock == null)
                return;

            // MOBILE: was LocationModLoader.climate_map, Location Loader's own GetAsset copy of the
            // same texture. One owner now, and the property hands back the readable copy.
            var climate_map = WorldOfDaggerfall.NatureBatchOverriderInstaller.ClimateMap;
            // MOBILE: shared guard, and logged once - upstream logged an error per call.
            if (!WorldOfDaggerfall.NatureBatchOverrider.MapReadable(climate_map))
            {
                if (!warnedUnreadableMap)
                {
                    Debug.LogWarning("[Biomes] climate map missing or not readable; type-5 nature swap disabled");
                    warnedUnreadableMap = true;
                }
                return;
            }

            int mapW = climate_map.width;
            int mapH = climate_map.height;

            int n = 0;                                  // MOBILE: swap counter for the log line below
            var flats = rmbBlock.GetComponentsInChildren<Billboard>(true);
            foreach (var b in flats)
            {
                if (b.Summary.FlatType != FlatTypes.Nature)
                    continue;

                // MOBILE: belt-and-braces. What actually stops a second swap on the retry path (a
                // block is deferred whole, so the flats swapped before the unparented one are seen
                // again) is the FlatType filter one line up: SetMaterial reassigns Summary.FlatType
                // from MaterialReader.GetFlatType(10030), which is Decoration because Nature is only
                // archives 500-511, so a swapped flat never reaches this test. Keep the archive guard
                // anyway - one int compare, and it is what remains if GetFlatType ever widens - but do
                // not read it as the thing preventing the double scale and re-ground.
                if (b.Summary.Archive == CUSTOM_ARCHIVE)
                    continue;

                var terrain = b.GetComponentInParent<DaggerfallTerrain>();
                if (terrain == null)
                {
                    // schedule retry next update
                    // MOBILE: logged once. LocationLoader parents the block under the location
                    // instance, which hangs off the DaggerfallTerrain (LocationLoader.cs:557), so this
                    // is a timing miss, not an error - it clears on the next terrain update.
                    if (!warnedDeferred)
                    {
                        Debug.LogWarning($"[Biomes] terrain not ready for {b.name}; type-5 nature swap deferred to the next terrain update");
                        warnedDeferred = true;
                    }
                    EnsureSubscribed();                 // MOBILE: subscribe only now that we need the event
                    if (!pendingBlocks.Contains(rmbBlock))
                        pendingBlocks.Add(rmbBlock);
                    return;
                }

                int mx = terrain.MapPixelX;
                int my = terrain.MapPixelY;

                if (mx < 0 || mx >= mapW || my < 0 || my >= mapH)
                {
                    // MOBILE: logged once, same reason as above.
                    if (!warnedOutOfRange)
                    {
                        Debug.LogError($"[Biomes] LocationData out of range: ({mx},{my}) vs map {mapW}x{mapH}");
                        warnedOutOfRange = true;
                    }
                    continue;
                }

                int ty = mapH - 1 - my;
                Color32 c = climate_map.GetPixel(mx, ty);
                // MOBILE: was an inline r/g/b comparison against TriggerColor - identical test, one owner.
                if (!WorldOfDaggerfall.NatureBatchOverrider.IsSubtropicalKey(c))
                    continue;

                // swap material
                // MOBILE: kept as upstream wrote it. This is a loose Billboard, not a
                // DaggerfallBillboardBatch, so the engine's own DaggerfallBillboard.SetMaterial does
                // the work - archive 10030 arrives through TextureReplacement's loose-texture import
                // from Daggerfall Expanded Textures, and this fork's SetMaterial already fails soft on
                // a missing archive. Upstream does NOT duplicate the atlas builder here, so there is
                // nothing for WorldOfDaggerfall.CustomBillboardHelper to replace: RevisedSetMaterial
                // exists only because DaggerfallBillboardBatch.SetMaterial cannot build an atlas for
                // an archive above 511, and it takes a batch, not a billboard.
                // MOBILE: upstream discarded the return value. DaggerfallBillboard.SetMaterial returns
                // null and leaves Summary untouched when the archive has no such record (this fork's
                // fail-soft guard, DaggerfallBillboard.cs:287-297), and the flat then kept its vanilla
                // 501 texture while still being doubled, re-grounded and counted - a 2x vanilla tree,
                // doubled again on every retry because neither guard above sees it, and a "swapped N"
                // line claiming a swap that never happened.
                if (b.SetMaterial(CUSTOM_ARCHIVE, b.Summary.Record) == null)
                    continue;
                // MOBILE: was LocationModLoader.VEModEnabled - the same Vanilla Enhanced probe, read
                // from the compiled-in Biomes port that now owns it.
                if (!WorldOfDaggerfall.WODBiomes.VEModEnabled)
                    b.transform.localScale *= 2f;

                // re-ground
                float halfH = b.Summary.Size.y * b.transform.localScale.y * 0.5f;
                var lp = b.transform.localPosition;
                lp.y = halfH;
                b.transform.localPosition = lp;
                n++;                                    // MOBILE
            }

            // MOBILE: only when something actually changed. The terrain-side overrider logs the same
            // way ("[Biomes] swapped N nature batches ..."); the simulator and device runs grep for
            // these two lines as the proof each half of the swap fired.
            if (n > 0)
                Debug.Log("[Biomes] swapped " + n + " type-5 nature flats to archive " + CUSTOM_ARCHIVE);
        }

        static void OnUpdateTerrainsEnd()
        {
            // retry all pending swaps now that terrains are (re)parented
            if (pendingBlocks.Count == 0)
                return;

            var retryList = new List<GameObject>(pendingBlocks);
            pendingBlocks.Clear();
            foreach (var block in retryList)
            {
                if (block != null)
                    ApplySwaps(block);
            }
        }
    }
}
