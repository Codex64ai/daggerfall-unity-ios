// MOBILE PORT - source: github.com/TheLacus/daggerfall-unity-mods @ 556ef6e1dd0f2da95aa34275a30861daf58fee86
// File RealGrass/Scripts/RealGrass.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (RealGrass/LICENSE): MIT, Copyright (c) 2016-2019 Uncanny_Valley, TheLacus.
//
// MOBILE: the mod is compiled into the app and started by MobilePortedMods, not by DFU's [Invoke]
// loader, and it runs in ONE configuration - the cheap one: Classic style, billboard prototypes,
// no stones, no water plants, no fireflies. Detail resolution is upstream's own 256, at DFU's 16
// per patch rather than upstream's 8 - same cells, same placement, a quarter of the patches. See
// RealGrassPort.
//
// MOBILE: there is no modsettings.json in the iOS bundle (it ships the two Classic textures and
// nothing else), so mod.GetSettings() has nothing to read and the settings callback is gone. Every
// value below is a code constant; the two dials a player would plausibly want are the public
// static fields RealGrassPort.DetailDistance / .DetailDensity, and a settings hook can be added
// later without touching anything else.

// Project:         Real Grass for Daggerfall Unity
// Web Site:        http://forums.dfworkshop.net/viewtopic.php?f=14&t=17
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/TheLacus/realgrass-du-mod
// Original Author: Uncanny_Valley (original Real Grass)
// Contributors:    TheLacus (mod version, additional terrain details) 
//                  Midopa

// #define TEST_PERFORMANCE

using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections;
using System.Collections.Generic;   // MOBILE: the once-per-message log set
using System.Linq;
using UnityEngine;
using Climates = DaggerfallConnect.Arena2.MapsFile.Climates;

namespace RealGrass
{
    [Flags]
    public enum GrassStyle
    {
        Classic = 0,
        Mixed = 1,
        Full = 1 | 2,
    }

    public static class DaysOfYear
    {
        public const int

            GrowDay = 1 * 30 + 15,
            Spring = 2 * 30 + 1,
            Summer = 5 * 30 + 1,
            MidYear = 6 * 30 + 15,
            Fall = 8 * 30 + 1,
            Winter = 11 * 30 + 1,
            DieDay = 12 * 30 - 15;
    }

    public class RealGrassOptions
    {
        internal GrassStyle GrassStyle { get; set; }
        public bool WaterPlants { get; set; }
        public bool TerrainStones { get; set; }
        public bool FlyingInsects { get; set; }
        public float DetailObjectDistance { get; set; }
        public float DetailObjectDensity { get; set; }
    }

    /// <summary>
    /// MOBILE: upstream's static <c>RealGrass.Init</c>, lifted out of the MonoBehaviour into a
    /// loader of its own with the <c>[Invoke(StateManager.StateTypes.Start, 0)]</c> attribute
    /// REMOVED - MobilePortedMods calls this directly when the launcher entry is on, exactly as
    /// DFU's mod loader would have. Nothing here runs otherwise.
    ///
    /// It also owns the forced configuration. Upstream read all of it from modsettings.json; this
    /// bundle has no settings file, and the shipped defaults were the worst possible ones for a
    /// phone anyway (Full style, mesh prototypes on the Standard shader, stones, water plants,
    /// 256 at 8 per patch = 1,024 detail patches per terrain against DFU's own ~65). Detail
    /// DENSITY and RESOLUTION are not among the things it forces down any more - see
    /// DefaultDetailDensity and DetailResolution for why forcing those down was the bug.
    /// </summary>
    public static class RealGrassPort
    {
        #region The forced configuration

        /// <summary>
        /// MOBILE: upstream's detail RESOLUTION, kept. This was 128 - DFU's own store - and 128 is
        /// what made the grass thin on device, because a detail cell's value is capped and the cap
        /// does not scale with the cell.
        ///
        /// Upstream addresses a 256-square detail map for DFU's 128-square tilemap: four detail
        /// cells of 3.2 m per terrain tile, each holding its own instance count. At 128 those four
        /// counts have to become one, and their SUM (upstream's thick tile owes 4 x 6..19 = 24..76
        /// billboards) has to fit in one cell - but <see cref="DetailScatterMode.InstanceCountMode"/>
        /// stores at most <c>TerrainData.maxDetailScatterPerRes</c> = 16 per cell. Every grass cell
        /// would clamp to 16 on 40.96 m^2 = 0.39 instances/m^2 against upstream's 1.18, i.e. 3.1x
        /// thinner, with the thick and thin bands crushed into the same value. 256 is therefore not
        /// a luxury here: it is the resolution upstream's numbers were authored for, and at it the
        /// map is 1:1 with upstream's and the counts are written through unchanged.
        ///
        /// The previous 128 store went the other way - CoverageMode, whose ceiling IS 255 - and
        /// calibrated counts into coverage values. That worked on paper (and is what Task 6
        /// measured) but saturated: a thick cell wanted coverage 254 of 255, so half the thick band
        /// hit the ceiling and the fold could not represent upstream's spread. Reproducing upstream
        /// exactly costs less thought than calibrating against it.
        /// </summary>
        public const int DetailResolution = 256;

        /// <summary>
        /// MOBILE: 16, not upstream's 8 - DFU's own patch size, and the one lever this port still
        /// pulls against upstream's cost. resolutionPerPatch does not move a single billboard; it
        /// only decides how many cells share one culled, batched patch mesh. Upstream's (256, 8) is
        /// 1,024 patches per terrain; (256, 16) is 256, four times fewer things to cull and draw
        /// for identical placement. See <see cref="DetailPatchesPerTerrain"/>.
        /// </summary>
        public const int DetailResolutionPerPatch = 16;

        /// <summary>
        /// MOBILE: forced. Classic is both the cheap path (one grass layer, no flower/accent
        /// layers) and the licence-clean one (the Mixed and Full textures are VMblast's, not
        /// shipped). Upstream's default was Full.
        /// </summary>
        public const GrassStyle ForcedStyle = GrassStyle.Classic;

        /// <summary>
        /// MOBILE: forced. Billboard means DetailRenderMode.GrassBillboard fed by a texture -
        /// Unity's cheapest detail path. Upstream's default (false) renders FBX prototypes with
        /// their own Standard-shader materials, which on a phone backbuffer with alpha-tested
        /// overdraw is the single most expensive thing this mod can do.
        /// </summary>
        public const bool ForcedBillboard = true;

        /// <summary>MOBILE: forced off - a whole extra detail layer, and the 13.7 MB rock textures.</summary>
        public const bool ForcedTerrainStones = false;

        /// <summary>MOBILE: forced off - another detail layer, and mesh prototypes from the bundle.</summary>
        public const bool ForcedWaterPlants = false;

        /// <summary>MOBILE: forced off - a particle system per night-time water tile.</summary>
        public const bool ForcedFlyingInsects = false;

        /// <summary>
        /// MOBILE: forced off, which is also upstream's default. With it on, every texture load
        /// first stats a loose file under Textures/Grass/ - on iOS a Documents-folder round trip
        /// inside terrain promotion.
        /// </summary>
        public const bool ForcedTextureOverride = false;

        /// <summary>
        /// MOBILE: 100 m. The detail draw radius, in metres - the distance beyond which Unity draws
        /// no detail at all. Upstream's default is 120 and its own settings slider stops at 300;
        /// 100 is that default clamped to what a phone should be asked for, and it is the honest
        /// cost lever of this port (the drawn area goes as its square). The shipped 40 was the
        /// other half of why the device read as bare: grass existed only inside a 40 m disc and
        /// popped in at its edge.
        /// </summary>
        public const float DefaultDetailDistance = 100f;

        /// <summary>
        /// MOBILE: 1.0 - upstream's own default, where the shipped build had 0.6. It becomes
        /// Terrain.detailObjectDensity (see AddTerrainDetails), a global multiplier Unity applies
        /// to the instance count it takes from a detail cell.
        ///
        /// It is a THINNING dial and only that. Measured against 6000.3.23f1 (Task 6 report §4d):
        /// <c>Terrain.detailObjectDensity</c> is hard-clamped by the engine to 0..1 - `set 2 ->
        /// reads 1`, `set 5 -> reads 1`, `set -1 -> reads 0`. So 1.0 is not "more grass than
        /// upstream", it is the ceiling, and the 0.6 the device saw was a flat 40 % cut on top of
        /// everything else. Anything below 1.0 is a fill-rate discount paid in grass, which is what
        /// this dial is for if a smaller device needs one.
        /// </summary>
        public const float DefaultDetailDensity = 1f;

        /// <summary>
        /// MOBILE: the two dials, as fields rather than constants. There is no modsettings.json in
        /// the bundle, so nothing reads them from a file today; they are public and static so a
        /// settings hook (or a console command, or the launcher) can move them later without
        /// touching the port. Read by Init, so a change takes effect on the next start.
        /// </summary>
        public static float DetailDistance = DefaultDetailDistance;

        /// <summary>MOBILE: see DetailDistance.</summary>
        public static float DetailDensity = DefaultDetailDensity;

        /// <summary>
        /// MOBILE: the device-class line, in MB of system memory. Below it a device gets
        /// <see cref="SmallDeviceDetailDistance"/> instead of <see cref="DefaultDetailDistance"/>.
        /// 6 GB is where Apple's current line sits: the 4 GB iPhone 12/13/SE and the 4 GB base
        /// iPads are under it, the 6 GB iPhone 15 Pro and every M-series iPad are over.
        /// </summary>
        public const int SmallDeviceMemoryMb = 6144;

        /// <summary>
        /// MOBILE: 70 m for a small device. Half the drawn grass of 100 m, because the drawn area
        /// goes as the square of the radius - pi*70^2 / pi*100^2 = 0.49.
        /// </summary>
        public const float SmallDeviceDetailDistance = 70f;

        /// <summary>
        /// MOBILE pure: the detail draw radius this device class should default to. The iOS build
        /// ships to iPhone as well as iPad (MobileBuildSetup sets iOSTargetDevice.iPhoneAndiPad),
        /// so the same 100 m disc would otherwise be asked of a 4 GB phone and an M4 iPad alike,
        /// and this is the quadratic dial - the one place where a device class is worth branching
        /// on. An unknown or unreported memory size (0 or negative) takes the SMALL value: the
        /// cheap answer is the safe one when the device cannot be identified.
        /// </summary>
        public static float DefaultDetailDistanceFor(int systemMemoryMb)
        {
            return systemMemoryMb >= SmallDeviceMemoryMb ? DefaultDetailDistance : SmallDeviceDetailDistance;
        }

        /// <summary>
        /// MOBILE: the third dial, and it is a QualitySettings global rather than a terrain one.
        /// <c>QualitySettings.softVegetation</c> decides whether Unity's terrain detail pass draws
        /// with a blended alpha edge or an alpha-tested cutout; it changes no placement and no
        /// count, only the edge of every blade. Cut out, the thin tips of a grass billboard fall
        /// below the cutoff and vanish, which reads as wispier, sparser grass - exactly the
        /// complaint - and it is off here for a reason that has nothing to do with grass: iOS runs
        /// quality level 2 ("Simple"), and ProjectSettings/QualitySettings.asset has
        /// <c>softVegetation: 0</c> for levels 0-2 and 1 for 3-5. DFU's own desktop default is
        /// level 3, so every player upstream ever had saw the blended edge.
        ///
        /// Turning it on costs a blend rather than a discard in the detail pass, which on a
        /// tile-based GPU is not free - it is the first thing to try switching off if the device
        /// round reports a frame-rate cost. Applied by <see cref="ApplySoftVegetation"/>, which is
        /// called from Init AND re-asserted per promotion, because DFU's own options windows call
        /// <c>QualitySettings.SetQualityLevel</c> and that reapplies the level's own value.
        /// </summary>
        public const bool DefaultSoftVegetation = true;

        /// <summary>MOBILE: see DefaultSoftVegetation.</summary>
        public static bool SoftVegetation = DefaultSoftVegetation;

        /// <summary>
        /// MOBILE: QualitySettings.softVegetation as Init found it, so StopMod can put it back.
        /// Runtime writes to QualitySettings are not persisted into the player's
        /// QualitySettings.asset, so there is no cross-launch leak to fix here - this is the
        /// symmetry, for the next port that draws a detail layer and would otherwise inherit a
        /// global this one turned on and walked away from.
        /// </summary>
        static bool softVegetationWas;

        /// <summary>MOBILE: true once <see cref="CaptureSoftVegetation"/> has run, so a second
        /// Init cannot overwrite the original value with this port's own.</summary>
        static bool softVegetationCaptured;

        /// <summary>MOBILE: remember the quality level's own value. Called by Init, before the
        /// first <see cref="ApplySoftVegetation"/>.</summary>
        public static void CaptureSoftVegetation()
        {
            if (softVegetationCaptured)
                return;
            softVegetationCaptured = true;
            softVegetationWas = QualitySettings.softVegetation;
        }

        /// <summary>MOBILE: give it back. Called by StopMod, alongside the layer blanking.</summary>
        public static void RestoreSoftVegetation()
        {
            if (!softVegetationCaptured)
                return;
            softVegetationCaptured = false;
            QualitySettings.softVegetation = softVegetationWas;
        }

        /// <summary>
        /// MOBILE: set Unity's soft-vegetation flag if this port wants it and the quality level has
        /// it off. Never turns it OFF - a player or a quality level that asked for it keeps it.
        /// </summary>
        public static void ApplySoftVegetation()
        {
            if (SoftVegetation && !QualitySettings.softVegetation)
                QualitySettings.softVegetation = true;
        }

        #endregion

        #region Pins and gates

        /// <summary>
        /// MOBILE: Unity's built-in terrain detail shaders, verified by name against
        /// 6000.3.23f1's unity_builtin_extra. The mod has no shaders of its own - it renders
        /// through Unity's stock terrain detail path, and an iOS player build is free to strip
        /// these because nothing in the project references them (DFU creates every Terrain at
        /// runtime and no scene holds one). They are pinned into GraphicsSettings' Always
        /// Included Shaders list by MobileBuildSetup.EnsureAlwaysIncludedShaders; Init checks
        /// them at runtime so a build that lost them says so instead of drawing nothing.
        /// </summary>
        public static readonly string[] ShaderNames =
        {
            "Hidden/TerrainEngine/Details/Vertexlit",
            "Hidden/TerrainEngine/Details/WavingDoublePass",
            "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
        };

        /// <summary>
        /// MOBILE: the two Classic billboard textures the bundle ships. BrownGrass_tex is used by
        /// Mountain, Swamp and (see DesertGrassTexture) Desert; GreenGrass_tex by Temperate.
        /// Both must be there or the gate refuses: a null prototypeTexture draws nothing.
        /// </summary>
        public static readonly string[] GrassTextureNames = { "BrownGrass_tex", "GreenGrass_tex" };

        /// <summary>
        /// MOBILE: did Init get past the gate and subscribe to DaggerfallTerrain.OnPromoteTerrainData?
        /// This is the flag MobilePortedMods' four-argument StartOne asks, because Init declines by
        /// logging "[RealGrass] not available: ..." and returning rather than by throwing.
        /// </summary>
        public static bool Installed { get; private set; }

        /// <summary>
        /// MOBILE pure: nothing is created unless Unity's three detail shaders are USABLE AND the
        /// bundle's Classic grass textures are there. Either half missing means grass that renders
        /// as nothing at all, with no error - which is the failure this gate exists to name.
        ///
        /// "Usable", not "found": Init resolves each name with Shader.Find AND asks
        /// <c>Shader.isSupported</c>. A shader object can resolve and still have no variant the
        /// device's graphics API can run - a stripped or unsupported subshader compiles away and
        /// leaves a Shader whose isSupported is false, which draws nothing and says nothing. The
        /// simulator probe (Task 6 §4e) read true for all three, so this term has never yet
        /// changed the answer; it is here because the one build where it would have is exactly the
        /// build nobody would think to check.
        /// </summary>
        public static bool Available(bool shadersUsable, bool grassTexturePresent)
        {
            return shadersUsable && grassTexturePresent;
        }

        #endregion

        #region Cost arithmetic (pure)

        /// <summary>
        /// MOBILE pure: detail patches per terrain, the number Unity culls and draws every frame.
        /// Upstream's (256, 8) is 1,024; this port's (256, 16) is 256 - the same detail cells, and
        /// therefore the same billboards in the same places, batched four to a patch instead of one.
        /// </summary>
        public static int DetailPatchesPerTerrain(int res, int perPatch)
        {
            if (res <= 0 || perPatch <= 0) return 0;
            int perSide = res / perPatch;
            return perSide * perSide;
        }

        /// <summary>
        /// MOBILE pure: resident detail-density data across the live terrains, in MB. TerrainData
        /// keeps one byte per detail cell per layer. Upstream at its defaults - 256, five layers,
        /// 49 live terrains at TerrainDistance 3 - is ~15.3 MB; this port is ~3.06 MB, one layer at
        /// the same resolution. (The 128 store it replaced was ~0.77 MB and 3.1x too thin - see
        /// <see cref="DetailResolution"/>.)
        /// </summary>
        public static float DetailDataMegabytes(int res, int layers, int terrains)
        {
            if (res <= 0 || layers <= 0 || terrains <= 0) return 0f;
            return (float)((double)res * res * layers * terrains / (1024.0 * 1024.0));
        }

        /// <summary>
        /// MOBILE pure: live terrains at a given StreamingWorld.TerrainDistance - the ring the
        /// promotion handler is called for on every map-pixel crossing. 3 (the shipped value) is 49.
        /// </summary>
        public static int LiveTerrainCount(int terrainDistance)
        {
            if (terrainDistance < 0) return 0;
            int side = terrainDistance * 2 + 1;
            return side * side;
        }

        /// <summary>
        /// MOBILE: the terrain's detail scatter mode, set EXPLICITLY on every TerrainData this port
        /// writes details to rather than inherited from whatever a runtime-constructed TerrainData
        /// happens to be in. DFU builds its terrains with a bare <c>new TerrainData()</c> and
        /// nothing else in this project ever calls SetDetailScatterMode, so the mode was an
        /// undocumented engine default that a Unity upgrade - or a TerrainData copy - could move
        /// under us, and the two modes do not merely scale differently - they denominate different
        /// things.
        ///
        /// InstanceCountMode - what this port forces, and what upstream ran on - is Unity's own
        /// words: "the detail map holds values that represent the number of detail instances to
        /// render at each sample". A cell holding 12 gets 12 billboards. The ceiling is
        /// <c>TerrainData.maxDetailScatterPerRes</c> = 16 per cell (Unity: "values of up to 16 are
        /// stored"), and that is the same ceiling upstream's authored thick range ran into on every
        /// engine it shipped on: Random.Range(6, 20) draws 6..19, so its top four values were
        /// already being clamped to 16 for every player it ever had.
        /// CoverageMode is the other one: a cell holds 0..255, "how much area to cover at each
        /// sample, based on the detail's density", and the instance count comes out as the SQUARE
        /// of that value divided by the prototype's footprint.
        ///
        /// The port ran on CoverageMode and does not any more. Coverage was chosen so a 128-square
        /// store could carry a per-square-metre quantity that upstream authored for a 256-square
        /// one, and it needed a calibration - v = 255 * w * sqrt(N / A), derived from Unity's own
        /// ComputeDetailInstanceTransforms - to turn counts into values at all. That calibration
        /// was right to within 1 % and still produced grass the device round called thin, for two
        /// reasons the arithmetic hid. It SATURATES: upstream's thick mean cell wants v = 254 of
        /// 255, so half the thick band pinned at the ceiling and the authored spread collapsed into
        /// one value. And it spreads each tile's grass evenly across a 6.4 m cell instead of the
        /// four 3.2 m quadrants upstream wrote it into, which is the difference between clumps and
        /// an even speckle. At <see cref="DetailResolution"/> 256 the map is 1:1 with upstream's,
        /// so there is no conversion left to get wrong: upstream's counts go into cells that mean
        /// counts, and Unity clamps them exactly where upstream's engine clamped them.
        ///
        /// Set before the first SetDetailLayer of a promotion: switching mode erases existing
        /// detail placements.
        /// </summary>
        public const DetailScatterMode ForcedScatterMode = DetailScatterMode.InstanceCountMode;

        /// <summary>
        /// MOBILE: Unity's ceiling on a single detail-map cell, read from
        /// TerrainData.maxDetailScatterPerRes per promotion - after <see cref="ForcedScatterMode"/>
        /// has been applied, so in practice it is instance-count mode's 16. The fallback is that
        /// same value. Unlike coverage's 255 this ceiling IS reached: upstream's thick range runs
        /// to 19, so its top four draws clamp here - as they did upstream, on the same engine
        /// ceiling, which is why clamping is reproduction rather than loss. The memory log line
        /// writes the mode and this number out, so a Player.log settles it empirically.
        /// </summary>
        public const int FallbackMaxDetailValue = 16;

        /// <summary>MOBILE: see FallbackMaxDetailValue.</summary>
        public static int MaxDetailValue = FallbackMaxDetailValue;

        /// <summary>
        /// MOBILE pure: the accumulating half of the fold. Upstream addresses a 256x256 detail map
        /// as (tile * 2 + sub), so each of the 128x128 tilemap's cells owns four detail cells. At
        /// <see cref="DetailResolution"/> 256 the map is upstream's own and <see cref="DetailMap"/>
        /// .Fold is 1, so each of those writes lands in its own cell and this adds nothing to a
        /// zero - the sum IS upstream's value, which is the whole reason 256 was chosen. It stays a
        /// sum rather than an assignment for the resolution-drop case it was written for (at 128,
        /// four sub-cell counts land in one cell and counts add), so that changing DetailResolution
        /// keeps a defined meaning instead of silently keeping only the last write.
        /// Deliberately NOT clamped here: the ceiling belongs to the finished cell value, and
        /// <see cref="FoldDetailValue"/> applies it once, at the end.
        /// </summary>
        public static int AccumulateSubCell(int existing, int added)
        {
            if (existing < 0) existing = 0;
            if (added < 0) added = 0;
            long sum = (long)existing + added;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>
        /// MOBILE pure: the fold's final-value site, run once per promotion over the accumulated
        /// cells. Under <see cref="ForcedScatterMode"/> a cell value IS an instance count and
        /// upstream's numbers are instance counts, so all this does is apply Unity's per-cell
        /// ceiling (<see cref="MaxDetailValue"/>) - and it applies it exactly where upstream's own
        /// engine applied it, to upstream's own numbers, on upstream's own resolution.
        ///
        /// It used to be a unit conversion: this port ran on CoverageMode at half upstream's
        /// resolution, so the four sub-cell counts were summed and the total turned into a coverage
        /// value by v = 255 * w * sqrt(N / A). That is gone with the mode - see
        /// <see cref="ForcedScatterMode"/> for why, and note that the conversion before it was a
        /// MEAN, which shipped 0.0024 instances/m^2 against upstream's 0.195-1.56. Two rewrites of
        /// this one method is the measure of how much a denomination can hide; the version that
        /// needs no conversion at all is the one that cannot be wrong about it.
        /// </summary>
        public static int FoldDetailValue(int value, int max)
        {
            if (max < 0) max = 0;
            if (value <= 0) return 0;
            return value > max ? max : value;
        }

        #endregion

        #region The density arithmetic

        /// <summary>
        /// MOBILE: the terrain's detail-cell area in square metres, read from the live TerrainData
        /// per promotion (see AddTerrainDetails) rather than assumed. The fallback is DFU's own
        /// geometry: a terrain is 32768 * MeshReader.GlobalScale (0.025) = 819.2 m square, over a
        /// <see cref="DetailResolution"/>-square detail store, so a cell is 3.2 m on a side and
        /// 10.24 m^2 - upstream's own cell, because this port now uses upstream's own resolution.
        /// Confirmed in-player by Task 6 (`size=(819.20, 1923.75, 819.20)`). Nothing PLACES grass
        /// from this number any more; it is what turns a cell's instance count into the
        /// instances/m^2 the log line quotes.
        /// </summary>
        public const float FallbackDetailCellAreaM2 = 10.24f;

        /// <summary>MOBILE: see FallbackDetailCellAreaM2.</summary>
        public static float DetailCellAreaM2 = FallbackDetailCellAreaM2;

        /// <summary>
        /// MOBILE: upstream's thick density is Random.Range(6, 20), i.e. 6..19 clamped by the
        /// engine to 6..16, mean 12.07; 12 is the integer the logged target quotes and the one
        /// Task 6 measured against. Used only to name a representative density in the memory log
        /// line - nothing places grass from it.
        /// </summary>
        public const int UpstreamThickMeanPerSubCell = 12;

        /// <summary>MOBILE pure: a detail cell's ground area, from the live terrain geometry.</summary>
        public static float DetailCellArea(float terrainSizeMetres, int detailResolution)
        {
            if (terrainSizeMetres <= 0f || detailResolution < 1) return 0f;
            float side = terrainSizeMetres / detailResolution;
            return side * side;
        }

        /// <summary>
        /// MOBILE pure: what a detail cell's value is worth on the ground, for the log line. Under
        /// <see cref="ForcedScatterMode"/> a cell value IS an instance count, so this is a division
        /// and not a calibration - which is the entire point of the mode change. It replaced
        /// InstancesPerSquareMetre(coverage, width), whose law was (v/255)^2 / w^2.
        /// </summary>
        public static float InstancesPerSquareMetre(int cellValue, float cellAreaM2)
        {
            if (cellValue <= 0 || cellAreaM2 <= 0f) return 0f;
            return cellValue / cellAreaM2;
        }

        /// <summary>
        /// MOBILE pure: the density this port aims at, for the log line - upstream's thick mean
        /// cell, clamped by Unity's ceiling exactly as a real cell would be. At the shipped
        /// geometry that is 12 / 10.24 = 1.17 instances/m^2, which is upstream's own figure.
        /// </summary>
        public static float TargetInstancesPerSquareMetre(float cellAreaM2, int max)
        {
            return InstancesPerSquareMetre(FoldDetailValue(UpstreamThickMeanPerSubCell, max), cellAreaM2);
        }

        #endregion

        #region Init

        public static Mod mod;

        /// <summary>MOBILE: the mod whose bundle holds the two Classic textures. Null until Init.</summary>
        public static Mod Mod { get { return mod; } }

        static readonly HashSet<string> loggedOnce = new HashSet<string>();

        /// <summary>
        /// MOBILE: log a message the first time only. The promotion handler is called for a ring
        /// of terrains on every map-pixel crossing, so an exception in there would otherwise fill
        /// the log at the frame rate of walking.
        /// </summary>
        internal static void LogOnce(string message)
        {
            if (loggedOnce.Add(message))
                Debug.Log(message);
        }

        internal static void MarkInstalled()
        {
            Installed = true;
        }

        internal static void MarkStopped()
        {
            Installed = false;
        }

        /// <summary>
        /// MOBILE: one prototype texture, described the way the device question needs it -
        /// "'GreenGrass_tex' 256x256 RGBA32 readable=True cpuRead=OK opaque=41127/65536
        /// atlasSource=OK". <c>cpuRead</c> is the try/catch around a CPU pixel read; it is the fact
        /// the native error hides. <c>atlasSource</c> is the verdict Unity's DetailDatabase will
        /// reach when it packs this texture into the Terrain Detail Atlas: UNUSABLE (no CPU copy -
        /// the atlas gets nothing), EMPTY (readable but fully transparent - the atlas is built and
        /// alpha-tested away, which looks identical to grass being off) or OK.
        ///
        /// When the raw bundle texture cannot be read, the line also reports what
        /// TextureReplacement.EnsureReadable makes of it, because that is the fallback
        /// DetailPrototypesManager.LoadPrototypeTexture actually hands to the prototype - so the
        /// same line distinguishes "the import rule is wrong" from "the blit fallback is wrong".
        /// </summary>
        static string DescribePrototypeTexture(Texture2D tex)
        {
            if (tex == null)
                return "MISSING";
            string s = "'" + tex.name + "' " + tex.width + "x" + tex.height + " " + tex.format
                + " readable=" + tex.isReadable + DescribeCpuRead(tex);
            if (tex.isReadable)
                return s;
            // MOBILE: fully qualified - this file already has four Daggerfall usings and the
            // AssetInjection namespace is not one of them.
            Texture2D copy = DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.EnsureReadable(tex);
            if (copy == null || copy == tex)
                return s;
            return s + " -> readable copy " + copy.width + "x" + copy.height + " " + copy.format
                + " readable=" + copy.isReadable + DescribeCpuRead(copy);
        }

        /// <summary>
        /// MOBILE: the CPU read itself, collapsed to words. GetPixel first because that is the
        /// smallest read that can throw the "not accessible" error; GetPixels32 after it because
        /// cpuRead=OK on an all-transparent texture is still an empty atlas, and telling those two
        /// apart is the whole point of shipping this line.
        /// </summary>
        static string DescribeCpuRead(Texture2D tex)
        {
            try
            {
                tex.GetPixel(0, 0);
                Color32[] pixels = tex.GetPixels32();
                int opaque = 0;
                foreach (Color32 c in pixels)
                    if (c.a > 8) opaque++;
                return " cpuRead=OK opaque=" + opaque + "/" + pixels.Length
                    + " atlasSource=" + (opaque > 0 ? "OK" : "EMPTY");
            }
            catch (Exception ex)
            {
                return " cpuRead=FAILED(" + ex.GetType().Name + ") atlasSource=UNUSABLE";
            }
        }

        /// <summary>
        /// MOBILE: rename of upstream's <c>RealGrass.Init</c>, with the [Invoke] attribute removed
        /// and an availability gate in front of everything. Upstream created the component
        /// unconditionally and let a stripped shader or a missing texture surface as grass that
        /// simply is not there.
        /// </summary>
        public static void Init(InitParams initParams)
        {
            Installed = false;
            mod = initParams.Mod;

            // MOBILE: Shader.Find, not MobileShaders.Find - these are engine shaders, not the
            // port's own, so there is no captured copy to prefer and nothing for a mod bundle to
            // shadow. All three, because the gate cannot tell which render mode a later climate
            // will ask for and a half-present set is a build problem worth naming.
            //
            // isSupported as well as non-null: a Shader that resolves but has no variant this
            // device's graphics API can run draws nothing and reports nothing. See Available.
            string missingShader = null;
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null || !shader.isSupported)
                    missingShader = missingShader == null ? name : missingShader + ", " + name;
            }

            string missingTexture = null;
            foreach (string name in GrassTextureNames)
            {
                if (mod == null || mod.GetAsset<Texture2D>(name) == null)
                    missingTexture = missingTexture == null ? name : missingTexture + ", " + name;
            }

            if (!Available(missingShader == null, missingTexture == null))
            {
                string reason = missingShader != null
                    ? "Unity terrain detail shaders missing or unsupported in this build (" + missingShader + ")"
                    : "the bundle has no Classic grass texture (" + missingTexture + ")";
                Debug.Log("[RealGrass] not available: " + reason);
                return;
            }

            // MOBILE: the one line that settles a device round. The way this port fails on hardware -
            // an unreadable prototype texture, so Unity's terrain detail atlas is built from nothing -
            // reports itself only as a NATIVE "Texture of width 256 and height 256 is not accessible."
            // from Texture2D.cpp, which reaches the on-screen console and never reaches
            // Documents/Player.log. Grepping this literal answers "did the CPU read work" from a log
            // file. Unconditional and at Debug.Log, not behind a define: it is two lines per session
            // and it is the only thing in the app that can see that error class.
            foreach (string name in GrassTextureNames)
                LogOnce("[RealGrass] prototype " + DescribePrototypeTexture(mod.GetAsset<Texture2D>(name))
                    + " mode=" + (ForcedBillboard ? "GrassBillboard" : "Grass")
                    + " gfx=" + SystemInfo.graphicsDeviceType);

            // MOBILE: the blended blade edge, on. iOS runs quality level 2, whose softVegetation
            // is 0; upstream's desktop default is level 3, whose is 1. AddTerrainDetails re-asserts
            // it per promotion because SetQualityLevel would take it back. See
            // RealGrassPort.SoftVegetation. Captured first, so StopMod can hand the global back.
            CaptureSoftVegetation();
            ApplySoftVegetation();

            // MOBILE: the device-class guard on the quadratic dial. The drawn area goes as the
            // square of DetailDistance and the iOS build ships to iPhone as well as iPad
            // (MobileBuildSetup: iOSTargetDevice.iPhoneAndiPad), so a 4 GB phone would otherwise be
            // asked for the same 100 m disc as an M4 iPad. Only while the dial is still on its
            // default, so a later settings hook keeps the last word; what it chose and the memory
            // it chose from are both in the [RealGrass] detail data line.
            if (DetailDistance == DefaultDetailDistance)
                DetailDistance = DefaultDetailDistanceFor(SystemInfo.systemMemorySize);

            GameObject go = new GameObject(mod != null && !string.IsNullOrEmpty(mod.Title) ? mod.Title : "Real Grass");
            RealGrass component = go.AddComponent<RealGrass>();

            // MOBILE: started HERE rather than from the component's Start(). MobilePortedMods asks
            // Installed the moment Init returns, and Start() does not run until the next frame -
            // which would always read "did not start". AddComponent has already run Awake().
            component.StartPort();
        }

        #endregion
    }

    /// <summary>
    /// Places grass and other details on Daggerall Unity terrain.
    /// </summary>
    public class RealGrass : MonoBehaviour
    {
        #region Fields

        internal const string TexturesFolder = "Grass";

        // MOBILE: upstream kept its own static `mod`; there is one copy now, on RealGrassPort.
        private static Mod mod { get { return RealGrassPort.mod; } }

        private readonly RealGrassOptions options = new RealGrassOptions();
        DetailPrototypesManager detailPrototypesManager;
        DensityManager densityManager;
        bool isEnabled;
        Coroutine initTerrains;

        // MOBILE: the two log lines the simulator and device runs are read for. `promotions` counts
        // handled promotions (not terrains alive), so the counter line is a heartbeat that also
        // says the handler is still being reached; the memory line is said once, on the first
        // promotion, because only then are the layer count and the terrain ring both known.
        int promotions;
        bool memoryLogged;

        // MOBILE: every 25th promotion. At TerrainDistance 3 a map-pixel crossing promotes a ring
        // of about 7 terrains, so this is roughly one line per four crossings.
        public const int CounterInterval = 25;

        #endregion

        #region MOBILE: the forced configuration

        /// <summary>
        /// MOBILE: upstream's LoadSettings, with the settings reads replaced by the values
        /// modsettings.json shipped for everything this port keeps (grass size, noise spread,
        /// seasonal colours, thick/thin density) and by RealGrassPort's constants for everything it
        /// forces. There is no modsettings.json in the iOS bundle, so mod.LoadSettings() /
        /// mod.GetSettings() would have nothing to read; nothing here can throw.
        /// </summary>
        void ApplyForcedSettings()
        {
            options.GrassStyle = RealGrassPort.ForcedStyle;
            options.WaterPlants = RealGrassPort.ForcedWaterPlants;
            options.TerrainStones = RealGrassPort.ForcedTerrainStones;
            options.FlyingInsects = RealGrassPort.ForcedFlyingInsects;
            options.DetailObjectDistance = RealGrassPort.DetailDistance;
            options.DetailObjectDensity = RealGrassPort.DetailDensity;

            var properties = new PrototypesProperties()
            {
                // Upstream defaults: Grass/Height (0.65, 1.05), Grass/Width (0.8, 1.0),
                // Grass/NoiseSpread 0.4, Grass/SeasonInterpolation false.
                GrassHeight = new Range<float>(0.65f, 1.05f),
                GrassWidth = new Range<float>(0.8f, 1.0f),
                NoiseSpread = 0.4f,
                GrassColors = new GrassColors()
                {
                    SpringHealthy = new Color32(130, 206, 112, 255),
                    SpringDry = new Color32(137, 180, 75, 255),
                    SummerHealty = new Color32(163, 204, 162, 255),
                    SummerDry = new Color32(128, 125, 104, 255),
                    FallHealty = new Color32(166, 116, 43, 255),
                    FallDry = new Color32(105, 51, 29, 255),
                    SeasonInterpolation = false
                },
                // MOBILE: the billboard path. Upstream computed this as !settings Style/Billboard.
                UseGrassShader = !RealGrassPort.ForcedBillboard,
                TextureOverride = RealGrassPort.ForcedTextureOverride
            };

            var density = new Density()
            {
                // Upstream defaults: Grass/ThickDensity (6, 20), Grass/ThinDensity (2, 9).
                GrassThick = new Range<int>(6, 20),
                GrassThin = new Range<int>(2, 9),
                // MOBILE: both features are off, so these are never read. Kept non-null because a
                // Range<int> field the disabled branches would touch is worse than a dead value.
                WaterPlants = new Range<int>(1, 5),
                DesertPlants = new Range<int>(4, 8),
                Rocks = 0,
            };

            detailPrototypesManager = new DetailPrototypesManager(mod, transform, options, properties);
            densityManager = new DensityManager(mod, options, density);
        }

        /// <summary>
        /// MOBILE: what upstream's Start() did, called straight from RealGrassPort.Init so that
        /// Installed is settled before Init returns. No console commands (the External/
        /// RealGrassConsoleCommands.cs file is not part of this port - Wenzil.Console commands
        /// have no place on a touch build).
        /// </summary>
        internal void StartPort()
        {
            ApplyForcedSettings();
            StartMod(false, false);
            isEnabled = true;

            if (mod != null)
            {
                mod.MessageReceiver = MessageReceiver;
                mod.IsReady = true;
            }
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            if (mod == null)
                return base.ToString();

            return string.Format("{0} v.{1}", mod.Title, mod.ModInfo.ModVersion);
        }

        /// <summary>
        /// Toggle mod and add/remove grass on existing terrains.
        /// </summary>
        /// <returns>New status of mod.</returns>
        public bool ToggleMod()
        {
            ToggleMod(!isEnabled);
            return isEnabled;
        }

        /// <summary>
        /// Set status of mod and add/remove grass on existing terrains.
        /// </summary>
        /// <param name="enable">New status to set.</param>
        public void ToggleMod(bool enable)
        {
            if (isEnabled == enable)
                return;

            if (enable)
                StartMod(false, true);
            else
                StopMod();

            isEnabled = enable;
        }

        /// <summary>
        /// Restart mod to apply changes.
        /// </summary>
        public void RestartMod()
        {
            if (enabled)
                StopMod();

            StartMod(false, true);

            isEnabled = true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Add Grass and other details on terrain.
        /// </summary>
        private void AddTerrainDetails(DaggerfallTerrain daggerTerrain, TerrainData terrainData)
        {
#if TEST_PERFORMANCE

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

#endif

            UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(daggerTerrain.MapPixelX, daggerTerrain.MapPixelY));

            // Terrain settings
            // MOBILE: (256, 16) - upstream's resolution at DFU's patch size, and only when the
            // store is not already that shape. SetDetailResolution REALLOCATES the terrain's detail
            // store and clears every layer; StreamingWorld pools and reuses TerrainData, so
            // upstream paid for that reallocation on every promotion of every recycled terrain. DFU
            // creates its own store at (heightmapDimension -> 128, 16), so this fires once per
            // TerrainData - on its first promotion with grass on - and then never again.
            if (terrainData.detailWidth != RealGrassPort.DetailResolution
                || terrainData.detailHeight != RealGrassPort.DetailResolution
                || terrainData.detailResolutionPerPatch != RealGrassPort.DetailResolutionPerPatch)
            {
                terrainData.SetDetailResolution(RealGrassPort.DetailResolution, RealGrassPort.DetailResolutionPerPatch);
            }
            // MOBILE: the detail scatter mode, set explicitly and only when it differs - once per
            // TerrainData in practice, then never. It must happen BEFORE the first SetDetailLayer
            // of this promotion, because switching mode erases existing detail placements. What a
            // detail value means depends on this (an instance count capped at 16 vs coverage
            // 0..255), and everything DensityManager writes is an instance count; see
            // RealGrassPort.ForcedScatterMode.
            if (terrainData.detailScatterMode != RealGrassPort.ForcedScatterMode)
                terrainData.SetDetailScatterMode(RealGrassPort.ForcedScatterMode);
            // MOBILE: Unity's ceiling for one detail-map cell, which DensityManager's fold clamps
            // to. It follows from the scatter mode just set, so under InstanceCountMode this reads
            // 16 (coverage's would be 255) - but it is read, not assumed, and it is logged.
            RealGrassPort.MaxDetailValue = terrainData.maxDetailScatterPerRes > 0
                ? terrainData.maxDetailScatterPerRes
                : RealGrassPort.FallbackMaxDetailValue;
            // MOBILE: the live cell area. Nothing places grass from it - it is what turns a cell's
            // instance count into the instances/m^2 the memory line quotes - but it is read from
            // this TerrainData rather than assumed, so a terrain sampler that changes the world
            // scale reports the real density instead of the shipped one.
            float cellArea = RealGrassPort.DetailCellArea(terrainData.size.x, terrainData.detailWidth);
            RealGrassPort.DetailCellAreaM2 = cellArea > 0f ? cellArea : RealGrassPort.FallbackDetailCellAreaM2;
            // MOBILE: re-asserted per promotion, not just at Init - DFU's options windows call
            // QualitySettings.SetQualityLevel, which reapplies the level's own softVegetation and
            // would silently take the blended blade edge back. See RealGrassPort.SoftVegetation.
            RealGrassPort.ApplySoftVegetation();
            terrainData.wavingGrassTint = Color.gray;
            Terrain terrain = daggerTerrain.gameObject.GetComponent<Terrain>();
            terrain.detailObjectDistance = options.DetailObjectDistance;
            terrain.detailObjectDensity = options.DetailObjectDensity;

            // Get the current season and climate
            var currentSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
            ClimateBases climate = GetClimate(daggerTerrain.MapData.worldClimate);

            // Update detail layers
            Color32[] tilemap = daggerTerrain.TileMap;
            densityManager.InitDetailsLayers();
            switch (climate)
            {
                case ClimateBases.Temperate:
                case ClimateBases.Mountain:
                case ClimateBases.Swamp:
                    if (currentSeason != DaggerfallDateTime.Seasons.Winter)
                    {
                        detailPrototypesManager.UpdateClimateSummer(climate);
                        densityManager.SetDensitySummer(terrain, tilemap, climate);
                    }
                    else
                    {
                        detailPrototypesManager.UpdateClimateWinter(climate);
                        densityManager.SetDensityWinter(terrain, tilemap, climate);
                    }
                    break;

                case ClimateBases.Desert:
                    detailPrototypesManager.UpdateClimateDesert();
                    densityManager.SetDensityDesert(tilemap);
                    break;
            }

            // Assign detail prototypes to the terrain
            terrainData.detailPrototypes = detailPrototypesManager.DetailPrototypes;
            // MOBILE: assigning the array is not enough in a player. Unity's DetailDatabase packs
            // every non-instanced prototype - which is every GrassBillboard, i.e. all of this port -
            // into one "Terrain Detail Atlas", and a TerrainData built at RUNTIME (DaggerfallTerrain
            // .PromoteTerrainData's `new TerrainData()`) has no serialized atlas to start from.
            // RefreshPrototypes is the documented "reload the prototype assets" call and the trigger
            // that rebuilds the atlas for the prototypes just assigned; without it the terrain can
            // keep drawing from the atlas built for the PREVIOUS prototype set, which after a season
            // or climate change is the wrong texture and on first promotion is no texture at all.
            // DFU does exactly this pair after every runtime prototype change of its own -
            // MeshReplacement.ClearNatureGameObjects (MeshReplacement.cs:257-260) and :391.
            terrainData.RefreshPrototypes();

            // MOBILE: the second half of the fold. The density pass above wrote upstream's counts
            // into upstream's own cells; this applies Unity's per-cell ceiling to them once, before
            // the layers go to the terrain. (It used to be a count -> coverage conversion that
            // needed the prototype's mean billboard width; the width read that fed it is gone with
            // it - see RealGrassPort.ForcedScatterMode.)
            densityManager.FoldDetailLayers();

            // Assign detail layers to the terrain
            // MOBILE: .Cells - the layers are DetailMap now (one cached int[256,256] per layer,
            // cleared per promotion), not a fresh int[256,256] allocated per layer per promotion.
            terrainData.SetDetailLayer(0, 0, detailPrototypesManager.Grass, densityManager.Grass.Cells);
            if ((options.GrassStyle & GrassStyle.Mixed) == GrassStyle.Mixed && climate != ClimateBases.Desert)
            {
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.GrassDetails, densityManager.GrassDetails.Cells);
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.GrassAccents, densityManager.GrassAccents.Cells);
            }
            if (options.WaterPlants)
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.WaterPlants, densityManager.WaterPlants.Cells);
            if (options.TerrainStones)
                terrainData.SetDetailLayer(0, 0, detailPrototypesManager.Rocks, densityManager.Rocks.Cells);

            // MOBILE: the other half of the pair above. Flush pushes the changed detail data (and
            // the refreshed prototypes) into the live Terrain rather than waiting for whatever
            // would otherwise happen to invalidate it. Same precedent: MeshReplacement.cs:257-260.
            terrain.Flush();

            // MOBILE: the two lines a Player.log is read for.
            promotions++;
            if (!memoryLogged)
            {
                memoryLogged = true;
                int layers = detailPrototypesManager.DetailPrototypes != null ? detailPrototypesManager.DetailPrototypes.Length : 0;
                int terrains = RealGrassPort.LiveTerrainCount(TerrainDistance());
                // MOBILE: the mode, the per-cell ceiling, the two dials AS APPLIED and the density
                // they come to, so a Player.log answers what a detail value means on the device,
                // what the terrain was actually told, and what it is worth in billboards per square
                // metre - instead of leaving any of it to documentation. The target is upstream's
                // thick mean cell; upstream's own figure is 1.17/m2.
                Debug.Log(string.Format(
                    "[RealGrass] detail data ~{0:0.0} MB (res {1}, layers {2}, terrains {3}, scatter {4}/{5}, density {6:0.00}, distance {7:0} m [device {10} MB -> class default {11:0} m], soft veg {8}, target {9:0.00} inst/m2)",
                    RealGrassPort.DetailDataMegabytes(RealGrassPort.DetailResolution, layers, terrains),
                    RealGrassPort.DetailResolution, layers, terrains,
                    terrainData.detailScatterMode, RealGrassPort.MaxDetailValue,
                    terrain.detailObjectDensity, terrain.detailObjectDistance,
                    QualitySettings.softVegetation,
                    RealGrassPort.TargetInstancesPerSquareMetre(
                        RealGrassPort.DetailCellAreaM2, RealGrassPort.MaxDetailValue),
                    SystemInfo.systemMemorySize,
                    RealGrassPort.DefaultDetailDistanceFor(SystemInfo.systemMemorySize)));
            }
            if (promotions % CounterInterval == 0)
                Debug.Log("[RealGrass] details on " + promotions + " terrains");

#if TEST_PERFORMANCE

            stopwatch.Stop();
            Debug.LogFormat("RealGrass - Time elapsed: {0} ms.", stopwatch.Elapsed.Milliseconds);

#endif
        }

        /// <summary>
        /// MOBILE: StreamingWorld's terrain ring radius, or DFU's shipped 3 when the world is not
        /// there yet. Only the memory line reads it.
        /// </summary>
        static int TerrainDistance()
        {
            StreamingWorld world = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            return world != null ? world.TerrainDistance : 3;
        }

        /// <summary>
        /// Start mod and optionally add grass to existing terrains.
        /// </summary>
        /// <param name="loadSettings">Load user settings.</param>
        /// <param name="initTerrains">Add Grass to existing terrains (unnecessary at startup).</param>
        private void StartMod(bool loadSettings, bool initTerrains)
        {
            // MOBILE: the settings load is gone with modsettings.json - see ApplyForcedSettings.
            // The parameter is kept so the call sites read as upstream's do.
            if (loadSettings)
                ApplyForcedSettings();

            // Subscribe to events
            DaggerfallTerrain.OnPromoteTerrainData += DaggerfallTerrain_OnPromoteTerrainData;

            // Place details on existing terrains
            if (initTerrains)
                RefreshTerrainDetailsAsync();

            // MOBILE: the flag MobilePortedMods asks, set the moment the subscription exists.
            RealGrassPort.MarkInstalled();

            Debug.Log("[RealGrass] enabled; subscribed to terrain promotion");
        }

        /// <summary>
        /// Stop mod and remove grass fom existing terrains.
        /// </summary>
        private void StopMod()
        {
            // Unsubscribe from events
            DaggerfallTerrain.OnPromoteTerrainData -= DaggerfallTerrain_OnPromoteTerrainData;
            RealGrassPort.MarkStopped();   // MOBILE: symmetric with StartMod

            // Remove details from terrains
            // MOBILE: guarded. Upstream reached straight into StreamingTarget, which is null until
            // world entry - and this can now be reached from a mod message at the title screen.
            StreamingWorld streamingWorld = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            if (streamingWorld != null && streamingWorld.StreamingTarget != null)
            {
                Terrain[] terrains = streamingWorld.StreamingTarget.GetComponentsInChildren<Terrain>();
                foreach (TerrainData terrainData in terrains.Select(x => x.terrainData))
                {
                    foreach (var layer in terrainData.GetSupportedLayers(0, 0, terrainData.detailWidth, terrainData.detailHeight))
                        terrainData.SetDetailLayer(0, 0, layer, DensityManager.Empty);
                    terrainData.detailPrototypes = null;
                }
            }

            // MOBILE: and the one global this port writes goes back with them. Init turned
            // QualitySettings.softVegetation on over the quality level's own 0; leaving it on after
            // the layers are blank is a global set by a mod that is no longer running. See
            // RealGrassPort.CaptureSoftVegetation.
            RealGrassPort.RestoreSoftVegetation();

            Debug.Log("[RealGrass] disabled; unsubscribed from terrain promotion");
        }

        /// <summary>
        /// Re-applies detail layers to terrains.
        /// </summary>
        private void RefreshTerrainDetailsAsync()
        {
            if (initTerrains != null)
                StopCoroutine(initTerrains);

            initTerrains = StartCoroutine(RefreshTerrainDetailsCoroutine());
        }

        private IEnumerator RefreshTerrainDetailsCoroutine()
        {
            // MOBILE: guarded, for the same reason StopMod is - and each terrain is wrapped, so one
            // bad terrain does not abandon the rest of the ring.
            StreamingWorld streamingWorld = GameManager.Instance != null ? GameManager.Instance.StreamingWorld : null;
            if (streamingWorld == null || streamingWorld.StreamingTarget == null || streamingWorld.PlayerTerrainTransform == null)
            {
                initTerrains = null;
                yield break;
            }

            // Do player terrain first
            var playerTerrain = streamingWorld.PlayerTerrainTransform.GetComponentInChildren<DaggerfallTerrain>();
            if (playerTerrain != null)
            {
                AddTerrainDetailsContained(playerTerrain);
                yield return null;
            }

            // Do other terrains
            var terrains = streamingWorld.StreamingTarget.GetComponentsInChildren<DaggerfallTerrain>();
            foreach (DaggerfallTerrain daggerTerrain in terrains.Where(x => x != playerTerrain))
            {
                AddTerrainDetailsContained(daggerTerrain);
                yield return null;
            }

            initTerrains = null;
        }

        private static ClimateBases GetClimate(int climateIndex)
        {
            switch ((Climates)climateIndex)
            {
                case Climates.Swamp:
                case Climates.Rainforest:
                    return ClimateBases.Swamp;

                case Climates.Desert:
                case Climates.Desert2:
                case Climates.Subtropical:
                    return ClimateBases.Desert;

                case Climates.Mountain:
                case Climates.MountainWoods:
                    return ClimateBases.Mountain;

                case Climates.Woodlands:
                case Climates.HauntedWoodlands:
                    return ClimateBases.Temperate;

                case Climates.Ocean:
                default:
                    return (ClimateBases)(-1);
            }
        }

        private void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            switch (message)
            {
                case "toggle":
                    if (data is bool)
                        ToggleMod((bool)data);
                    break;

                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        /// <summary>
        /// MOBILE: the containment. OnPromoteTerrainData fires for a ring of terrains on every
        /// map-pixel crossing - the most latency-sensitive path in the port - and a throw in here
        /// used to propagate into DaggerfallTerrain.PromoteTerrainData, i.e. into DFU's own world
        /// streaming. The terrain keeps DFU's empty detail layers instead, and the message is
        /// logged once however many times it happens.
        /// </summary>
        private void AddTerrainDetailsContained(DaggerfallTerrain sender)
        {
            try
            {
                Terrain terrain = sender.gameObject.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null)
                    return;
                AddTerrainDetails(sender, terrain.terrainData);
            }
            catch (Exception ex)
            {
                RealGrassPort.LogOnce("[RealGrass] terrain details failed: " + ex.Message);
            }
        }

        #endregion

        #region Event Handlers

        private void DaggerfallTerrain_OnPromoteTerrainData(DaggerfallTerrain sender, TerrainData terrainData)
        {
            // MOBILE: contained - see AddTerrainDetailsContained. The TerrainData the event hands
            // over is the one to use (the component's own may not be assigned yet on a first
            // promotion), so this path passes it through rather than looking it up.
            try
            {
                AddTerrainDetails(sender, terrainData);
            }
            catch (Exception ex)
            {
                RealGrassPort.LogOnce("[RealGrass] terrain details failed: " + ex.Message);
            }
        }

        #endregion
    }
}
