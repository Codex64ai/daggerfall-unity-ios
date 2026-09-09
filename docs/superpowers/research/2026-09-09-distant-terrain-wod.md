# Distant Terrain of the World of Daggerfall — iOS port research

Date: 2026-09-09. Read-only research; nothing built, nothing committed. Target: decide whether
`Distant Terrain of the World of Daggerfall` (Nexus mods/1284) can join the compiled-in mod ports
under `Assets/Scripts/Game/Mobile/Ports/`, its shader under `Assets/Shaders/`, its data as an
AssetBundle — the pattern established by Dynamic Skies (see `THIRD-PARTY.md`) and World of
Daggerfall (`docs/superpowers/specs/2026-09-09-world-of-daggerfall-port-design.md`).

**Verdict up front: HARD, and it should not be attempted as a straight port.** It is feasible only
after real re-engineering (a texture-array rewrite of the far-terrain shader). Details in §9.

## 0. Repos and pinned commits

| Role | Repo | Commit | Notes |
|---|---|---|---|
| The build-system fork the contact pointed at | github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall | `d454b30af12c9f22c9ab5ef8ad5dbd9171d1cf1f` (tip of `master`, 2026-09-01) | 2 commits total |
| — its vendored upstream drop | same repo | `a722b337935dd8a57a913eb413c918c351e68e67` ("Add files") | the Nexus 1284 payload, unmodified |
| The true code ancestor (MIT) | github.com/Nystul-the-Magician/dfunity-mods, `DistantTerrain/` | `fc58546c3eae964babfdfeea51e97a47ba57cdce` (tip of `master`; last commit touching `DistantTerrain/`) | Distant Terrain 2.9.1, MIT, `LICENSE` at repo root, "Copyright (c) 2017 Nystul-the-Magician" |

### There is no GitHub upstream for the WoD fork

Searched exhaustively. `drcarademono` has 40 public repos (`wod-biomes`, `wod-terrain`,
`wod-farmlands`, `wod-location-generator`, `world-of-daggerfall`, …) and **none** is a Distant
Terrain fork. GitHub code/repo search for "Distant Terrain World of Daggerfall" returns exactly one
hit: somestupidgirl's fork. So the "carademono fork of Nystul's Distant Terrain" hypothesis is
wrong: the WoD flavour is a **Nexus-only release** (nexusmods.com/daggerfallunity/mods/1284),
manifest `ModAuthor: "Nystul (MaoDeVaca Support)"`, `ReadMe` crediting "Main developer: Nystul,
Contributor: MaoDeVaca". **MaoDeVaca is the WoD-fork author.** Commit `a722b33` in somestupidgirl's
repo is therefore the only versioned copy of it that exists, and it is the commit to cite as
"upstream".

### What somestupidgirl's fork changes: build scripts only

`git show --stat d454b30` — 3 files, 152 insertions, zero deletions, no source touched:

- `Editor/BuildDistantTerrainMods.cs` (89 lines) — `BuildDistantTerrainMods.Build()`, reads
  `DT_MOD_OUTPUT` / `DT_MOD_PLATFORMS`, finds `*.dfmod.json` under
  `Assets/Game/Mods/DistantTerrain`, strips `.cs` and `.cginc` from the `Files` list (its comment:
  "compile-time only and cannot be embedded in an AssetBundle (ShaderInclude is editor-only)"),
  then `BuildPipeline.BuildAssetBundles(..., ChunkBasedCompression, target)` per platform for
  macOS / Windows / Linux / iOS / Android.
- `Makefile` (BSD make) — `sync` rsyncs the repo into
  `${DFU_PROJECT}/Assets/Game/Mods/DistantTerrain`, then drives Unity `-batchmode -nographics
  -quit -executeMethod BuildDistantTerrainMods.Build`. Hard-coded to her machine
  (`/Users/sunneva/Development/Games/daggerfall-unity-ios`, Unity `6000.5.10f1` — note: **not**
  our `6000.3.23f1`). Android deliberately omitted.
- `.gitignore` — adds `build.log`.

So "I added a build system that compiles these for iOS" means exactly that: an AssetBundle built
with `BuildTarget.iOS`. It **does not** mean the mod's C# runs on iOS — iOS cannot load managed
code from a `.dfmod` at all, and her build script strips the `.cs` files from the bundle. Anything
it produces is a data-only bundle whose scripts will never execute unless the code is compiled into
the app, which is precisely the work this document scopes. Treat the fork as a build harness, not a
port.

### What the WoD fork changes vs Nystul MIT 2.9.1

Diffs are line-ending-normalised (`diff --strip-trailing-cr`).

| File | Change |
|---|---|
| `Scripts/DistantTerrain.cs` | 2029 → 2162 lines; +1273 / −1109. Effectively a rewrite of the terrain-build path. |
| `Scripts/_startupMod.cs` | 138 → 811 lines; +714 / −83. Ten new mod-settings sections and config holders. |
| `Scripts/DistantTerrainFlyMap.cs` | **NEW**, 930 lines. Point-and-click teleport / fly map. |
| `Scripts/ThirteenthPassageEffect.cs` | **NEW**, 144 lines. A custom Mysticism spell + `passage` console command. |
| `Scripts/CloneCamera{Position,Rotation}FromMainCamera.cs` | +7 / −1 each: a `Camera.main == null` guard. |
| `Scripts/RenderSkyboxWithoutSun.cs` | +1 / −1 (cosmetic). |
| `Scripts/ImprovedTerrainSampler.cs` | **DELETED** (was 808 lines). |
| `Scripts/ImprovedWorldTerrain.cs` | **DELETED** (was 631 lines). |
| `Shaders/FarTerrainCommon.cginc` | 806 → 805 lines; +557 / −104. Snow caps, region tints, procedural detail, location beacons, world-edge fade, skirt. |
| `Shaders/DistantTerrainTilemap.shader` | 275 lines; +141 / −65. Lambert instead of Standard, a new explicit depth pass, the cutout/skirt logic. |
| `Shaders/TransitionRingTilemap.shader` | **DELETED** |
| `Shaders/TransitionRingTilemapTextureArray.shader` | **DELETED** — the only `Texture2DArray` shader in the family is gone. |
| `Shaders/DaggerfallBillboardBatchFaded.shader` | +1 / −0 — but now **unreferenced** (see §2). |
| `Resources/Mountains{,_Small,_Foothills}.csv`, `Resources/daggerfall_deriv_map.png` | **NEW** — 3.6 MB of WoD-specific data. |
| `Resources/modsettings.json` | **NEW** (319 lines / 16.6 KB, 10 sections). |
| `distantterrain.dfmod.json` | Title → "Distant Terrain of the World of Daggerfall", version 2.9.1 → 3.69, `DFUnity_Version` 0.13.0 → 1.1.1. **Same GUID `9632a2ad-2ea0-46b6-b9b1-a4dafcca8a9a` as Nystul's** — so it is a drop-in replacement, and the two can never coexist. |

The two deletions are the architecturally important ones: Nystul's mod **replaced DFU's terrain
sampler** (`ImprovedTerrainSampler`, plugged into `DaggerfallUnity.Instance.TerrainSampler`) and
carried a companion world-terrain helper. The WoD fork drops both and reads DFU's *existing*
sampler read-only. Consequence: it does not change the near terrain at all, and it works with the
vanilla sampler (see §6).

## 1. What it does

A **second, coarse Unity `Terrain`** ("WorldTerrain") covering the whole province, drawn by a
**stacked camera** behind the main one, so the horizon shows real terrain instead of fog.

1. **Far mesh from the world heightmap.** `GenerateWorldTerrain()` calls
   `Terrain.CreateTerrainGameObject(null)`, then samples
   `dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue(x, y)` over the whole map
   (`MapsFile.MaxMapPixelX/Y` = 1000 × 500), scaled by
   `dfUnity.TerrainSampler.TerrainHeightScale(x,y)` and normalised by
   `TerrainSampler.MaxTerrainHeight`. `terrainData.heightmapResolution = max(1000,500) = 1000`
   (Unity rounds up to 1025); `terrainData.size = 819.2 × MaxTerrainHeight × 819.2 × 1024`
   ⇒ ~838,860 world units square (one map pixel = `32768 * MeshReader.GlobalScale(0.025)` = 819.2).
   Layer `layerWorldTerrain`; `shadowCastingMode = Off`; `heightmapPixelError = 0`;
   the auto-added `TerrainCollider` is **disabled** (the FlyMap re-adds it on demand).
   The object is reparented under `/Exterior` and repositioned every frame from `PlayerGPS`
   (floating-origin tracking, `UpdatePositionWorldTerrain`).
2. **WoD mountain lifts (new in this fork).** Three CSVs of WoD mountain-prefab placements
   (32,928 rows) are parsed into `MountainDef` records and stamped into the far heightmap as
   clipped hexagonal pyramids (`LiftHexagonalPyramidClipped`), with a per-prefab-name radius/lift
   table (18 `case "WOD_..."` arms). Mountains inside the near-terrain footprint are **un-lifted**
   so the visible far peak does not fight the streamed near terrain; the state is re-evaluated on
   every map-pixel cross and pushed back with a dirty-rect `terrainData.SetHeights(x0,y0,sub)`.
3. **River / coast carving (new).** `daggerfall_deriv_map.png` (5000 × 2500 grayscale, a
   black = water mask) is `GetPixels32()`-read and every water cell clamped to
   `TerrainSampler.OceanElevation`, producing distant rivers and proper coastlines. A persistent
   `oceanMask` is re-stamped after every mountain lift so peaks never poke out of the sea. A
   per-climate "disable winter rivers" option re-carves from a pristine copy when the calendar
   crosses the winter boundary.
4. **Climate/biome texturing.** `InitFarTerrain()` bakes a `Texture2D` "terrain info tilemap"
   (`RGBA32`, `terrainInfoTileMapDim = heightmapResolution - 1 = 1024`, point-filtered):
   **R** = climate index from `MapFileReader.GetClimateIndex`, **G** = per-region treatment index
   (from `GetPoliticIndex`, mapped through a hard-coded `DistantTerrainRegionConfig` table:
   colour tint + snow mode; Wrothgarian/Orsinium/Gavaudon and Balfiera/Dragontail/Ephesus are
   special-cased), **B/A** = a location beacon marker + a compact `DFRegion.LocationTypes`
   category (1–6) from `ContentReader.HasLocation`. The shader paints from four 2048² biome tile
   atlases (Desert / Woodland / Mountain / Swamp) × three season sets.
5. **Horizon / sky / fog integration.** A **third camera** renders the skybox alone into a
   256×256 `ARGB32` RenderTexture (`RenderSkyboxWithoutSun` zeroes the skybox material's
   `_SunSize` in `OnPreRender` and restores it in `OnPostRender`); the far-terrain shader can
   blend to that texture for fog colour (`_FogFromSkyTex`) and uses it for a world-edge fade band
   that hides the ocean padding at the map boundary. `DistantTerrain.Start()` **overwrites all five
   `GameManager.Instance.WeatherManager.*FogSettings`** with exponential-fog densities from mod
   settings.
6. **Near/far seam.** Fragments strictly inside the near-terrain footprint are `discard`ed; the
   innermost ring is kept and pinned down `_SkirtDepth` (600) units in the vertex shader to form a
   curtain sealing the seam. The cutout centre is advanced on `StreamingWorld.OnUpdateTerrainsEnd`
   (not on `CurrentMapPixel` change) so the far terrain stays authoritative until the near terrain
   has finished loading.
7. **Reflections: inert.** `_SeaReflectionTex` / `_UseSeaReflectionTex` exist behind
   `#pragma multi_compile_local __ ENABLE_WATER_REFLECTIONS`, but C# always writes
   `_UseSeaReflectionTex = 0` and never enables the keyword. The hook is for Nystul's separate
   `RealtimeReflections` mod. **Nothing to integrate.**
8. **Extras this fork bolts on** (not terrain at all): a fly-map / point-and-click teleporter with
   IMGUI readouts, and "The 13th Passage", a registered `BaseEntityEffect` spell + `passage`
   console command that opens it.

### vs Nystul's original, in one line
Nystul: replace the terrain sampler, plus a transition ring with a `Texture2DArray` variant.
WoD fork: leave the sampler alone, add WoD mountain lifts + a hand-painted river mask + per-region
colour/snow treatments + altitude snow caps + procedural tree specks and dirt + location beacons +
a teleport spell; drop the transition-ring shaders (and with them the only texture-array path).

## 2. Code inventory

Runtime C#, `Scripts/` — **4,141 lines** total:

| File | Lines | Role |
|---|---|---|
| `DistantTerrain.cs` | 2,162 | The whole far-terrain system (`MonoBehaviour`) |
| `DistantTerrainFlyMap.cs` | 930 | Teleport targeting, IMGUI readouts, location hover scan |
| `_startupMod.cs` | 811 | `[Invoke]` entry, 10 settings loaders, 12 static config holders |
| `ThirteenthPassageEffect.cs` | 144 | `BaseEntityEffect` + `ThirteenthPassageConsoleCommand` |
| `RenderSkyboxWithoutSun.cs` | 42 | Zeroes skybox `_SunSize` around the sky-RT render |
| `CloneCameraRotationFromMainCamera.cs` | 26 | `LateUpdate` rotation copy |
| `CloneCameraPositionFromMainCamera.cs` | 26 | `LateUpdate` position copy |

Editor-only: `Editor/BuildDistantTerrainMods.cs` (89) — somestupidgirl's, `using UnityEditor`,
excluded from any port. Also `Prefabs/debug_DistantTerrain.prefab` (a stale 2017-era debug prefab,
unreferenced by any script) and two stray `daggerfall_deriv_map.png~` editor backups.

**`[Invoke]` entry points: exactly one.**
`_startupMod.InitStart(InitParams)` at `[Invoke(StateManager.StateTypes.Start, 0)]`. It loads
settings, creates a `GameObject("DistantTerrain")` with the `DistantTerrain` component and (guarded
by `FindObjectOfType`) a `DistantTerrainFlyMap`, assigns
`mod.GetAsset<Shader>("Shaders/DistantTerrainTilemap.shader")`, registers the spell template and
the console command. `Awake()` sets `mod.IsReady = true`. Same shape as our other ports: strip
`[Invoke]`, call `Init` from `MobilePortedMods.StartEnabled`.

**DFU APIs hooked** (all present in our 1.1.1 fork — verified):

- `StreamingWorld.OnReady += InitFarTerrain` (`StreamingWorld.cs:1770`)
- `StreamingWorld.OnTeleportToCoordinates += UpdateWorldTerrain(DFPosition)`
- `StreamingWorld.OnUpdateTerrainsEnd += WorldTerrainAfterTerrainsUpdated` (raised at
  `StreamingWorld.cs:694`)
- `GameObject.Find("StreamingWorld").GetComponent<StreamingWorld>()`; reads `TerrainDistance`,
  `TerrainScale`
- `GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerGPS>()`; reads `CurrentMapPixel`
- `GameObject.Find("WeatherManager").GetComponent<WeatherManager>()`; **writes** all five
  `GameManager.Instance.WeatherManager.*FogSettings`
- `DaggerfallUnity.Instance.TerrainSampler` — read-only: `MaxTerrainHeight`, `OceanElevation`,
  `TerrainHeightScale(x,y)`. **Never assigned** (unlike Nystul's original).
- `dfUnity.ContentReader.WoodsFileReader.GetHeightMapValue`, `.MapFileReader.GetClimateIndex`,
  `.MapFileReader.GetPoliticIndex`, `.HasLocation(x,y,out MapSummary)`, `GetRegionName`
- `dfUnity.MaterialReader.TextureReader.GetTerrainTilesetTexture(archive).albedoMap` × **12**
  (archives 2/3/4, 102/103/104, 302/303/304, 402/403/404)
- `dfUnity.WorldTime.Now.SeasonValue`; `DaggerfallUnity.Settings.RetroRenderingMode`
- `PlayerEnterExit.TransitionEventArgs`, `SaveLoadManager` load event → `SetUpCameras()`
- `GameManager.Instance.EntityEffectBroker.RegisterEffectTemplate(new ThirteenthPassageEffect())`
- `Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("passage", …)`
- `Camera.main` (writes `farClipPlane = 15000`, `clearFlags = Depth`, `depth`), `RenderSettings.fog`
  / `fogMode` / `skybox`, `LayerMask.NameToLayer("Water")`

**Threading, reflection, `Resources.Load`, file IO: none.** Grepped for `System.Reflection`,
`Type.GetType`, `GetMethod`, `Resources.Load`, `File.*`, `Directory.*`, `StreamReader`, `Thread`,
`Task.`, `async`, `StartCoroutine` — zero hits in runtime code. All assets come through
`_startupMod.mod.GetAsset<T>(...)` (i.e. the AssetBundle): `GetAsset<Shader>` ×1,
`GetAsset<TextAsset>` ×3 (the CSVs), `GetAsset<Texture2D>` ×1 (the deriv map). Everything runs on
the main thread, synchronously, in `InitFarTerrain` (i.e. `StreamingWorld.OnReady`). IL2CPP-clean:
one `System.Enum.TryParse<KeyCode>` in the settings loader is the only generic worth noting and it
is instantiated concretely.

**iOS-hostile input in `DistantTerrainFlyMap`**: `Input.GetKeyDown(KeyCode.F12)`,
`Input.GetKeyDown(DistantTerrainLocationConfig.ToggleKey)` (default `End`),
`Input.GetMouseButtonDown(0)`, `OnGUI()` with `GUI.Label`, `Physics.Raycast`, and
`SetTerrainColliderEnabled` which **adds a `TerrainCollider` to the 1025² LOD terrain** while
targeting is open. On a device with no keyboard the whole file is unreachable, and the collider is
a memory spike we would never want. Recommendation: **do not port `DistantTerrainFlyMap.cs` or
`ThirteenthPassageEffect.cs`** — 1,074 of the 4,141 lines (26%) drop out, and with them the spell
registration (which would otherwise write a custom effect key into saves) and the `passage` console
command. `_startupMod` needs the corresponding `MOBILE`-marked removals, plus one real fix: the
FlyMap is also the only thing that polls the location-highlight hotkey, so that feature dies with
it (fine — it is a debug overlay).

**Known bug, for the record**: `ScanHoveredLocation` and `UpdateHighlightAndHandleTeleport` use
`const float mapPixelToUnityScale = 640f`, but the real map-pixel size is 819.2 — the teleport
targeting is ~22% off. Moot if we drop the file.

**Dead code carried along**: `DaggerfallBillboardBatchFaded.shader` is in the manifest but no
surviving script references it (`ImprovedWorldTerrain.cs`, deleted, was its only consumer);
`mapLocationRangeX/Y.bin.txt` and `mapTreeCoverage.bin.txt` (1.5 MB) are in the manifest and read
by nobody — `updateColorWithInfoForTreeCoverageAndLocations` is now fed `treeCoverage = 0` and the
tilemap's B/A channels. All four can be dropped from a port.

## 3. Data inventory

Manifest `Files`: **19 entries, 5,455,206 bytes = 5.20 MB** on disk (of which 5 entries / 208 KB
are the `.cs` sources and 3 / 57 KB the shaders — neither of which ships in an iOS bundle).

| Asset | Bytes | Note |
|---|---|---|
| `Resources/Mountains.csv` | 1,924,106 | 18,198 rows |
| `Resources/Mountains_Foothills.csv` | 1,322,388 | 11,938 rows |
| `Resources/Mountains_Small.csv` | 294,659 | 2,792 rows |
| `Resources/mapLocationRangeX.bin.txt` | 500,000 | **unused** |
| `Resources/mapLocationRangeY.bin.txt` | 500,000 | **unused** |
| `Resources/mapTreeCoverage.bin.txt` | 500,000 | **unused** |
| `Resources/daggerfall_deriv_map.png` | 130,507 | 5000 × 2500, 8-bit grayscale |
| `Resources/modsettings.json` | 16,658 | 10 sections |
| 5 × `.cs` | 208,000 | not shippable in a bundle |
| 3 × shader/cginc | 56,926 | compiled in, not shipped as data |

**Shippable data for a port: 3.68 MB** (3 CSVs + deriv map + modsettings), or **5.18 MB** if the
three dead `.bin.txt` are kept. No splat textures, no world heightmap binary (the heightmap comes
from the player's own `WOODS.WLD` at runtime).

Runtime footprint is much larger than the on-disk figure:

- **Deriv map.** The `.meta` has `nPOTScale: 1` (ToNearest) + `maxTextureSize: 2048` +
  `textureCompression: 0` + `textureFormat: 4` (RGBA32), no iOS platform override ⇒ imported as
  **2048 × 1024 RGBA32 uncompressed with mipmaps ≈ 11 MB VRAM**, and `isReadable: 1` keeps a
  **second ~11 MB CPU copy** for the life of the session (the code explicitly refuses to
  `Destroy()` it because the bundle owns it). `GetPixels32()` then allocates a transient
  `Color32[2,097,152]` ≈ **8 MB**. Good news: because compression is off, `GetPixels32` will
  actually succeed on iOS — **if our bundle pipeline forces ASTC on this texture the read throws**,
  the `catch (UnityException)` fires and all distant rivers and coastlines silently vanish. It must
  stay uncompressed, and mipmaps should be turned off (it is a data mask, never sampled by a
  shader).
- **CSVs.** 3.54 MB of text parsed with `string.Split` at world entry: ~33k rows × ~15 fields ⇒
  roughly half a million transient strings plus a 3.5 MB `char[]` split array, on the main thread,
  inside `StreamingWorld.OnReady`. Expect a multi-hundred-millisecond stall and a large gen-0 spike
  on device. A port should pre-bake these into a binary blob at pack time.
- **Heightmap working set.** `worldHeights`, `baseWorldHeights`, `preDerivWorldHeights` are each
  `float[1000,1000]` = 4 MB (12 MB), plus `oceanMask` `bool[1000,1000]` = 1 MB, plus
  `mountainLifted` and 32,928 × 28-byte `MountainDef` ≈ 0.9 MB. ~14 MB of managed arrays held for
  the session.
- **Tilemap texture.** `Texture2D(1024, 1024, RGBA32, mipmaps:false)` = 4 MB, plus its
  `Color32[1,048,576]` staging array (4 MB).
- **Unity `TerrainData`.** `heightmapResolution` 1025, and it also sets
  `alphamapResolution = 1000`, `baseMapResolution = 1000`, `SetDetailResolution(1000, 16)` — all
  three are pure waste (the material is fully custom; no splats, no details, no basemap, and
  `basemapDistance` is pushed to 1,000,000). The 1000² alphamap alone is several MB. A port should
  set these to the minimum Unity accepts.
- **The 12 terrain tile atlases — the real problem, see §5.**

## 4. Shaders

Three files, **1,080 lines** in the two that matter.

### `DistantTerrainTilemap.shader` (275 lines)
One `SubShader`, `Tags { "RenderType"="Transparent" "Queue" = "Transparent-499" }`, `LOD 200`,
containing **three programs**:

1. `ZWrite Off` surface shader — `#pragma target 3.0`,
   `#pragma surface surf Lambert vertex:vert noforwardadd finalcolor:fcolor alpha:fade keepalpha nolightmap`,
   `#pragma glsl`, `#pragma multi_compile_local __ ENABLE_WATER_REFLECTIONS`.
2. An explicit depth-only `Pass` — `ZWrite On`, `ColorMask 0`, hand-written
   `#pragma vertex vertDepth` / `#pragma fragment fragDepth`, `#pragma target 3.0`, mirrors the
   skirt pin-down and the cutout `discard`. New in this fork.
3. `ZWrite On` surface shader — identical pragmas to (1).

`FallBack "Diffuse"`.

**`#pragma target`: 3.0 everywhere.** **Tessellation: none. Geometry shaders: none. Compute: none.
`Texture2DArray`: none** (the only texture-array shader in the family,
`TransitionRingTilemapTextureArray.shader`, was deleted by this fork). No `GrabPass`, no UAVs, no
`StructuredBuffer`, no `only_renderers` / `exclude_renderers`. **Nothing in the list of things
Metal iOS cannot do.** `#pragma glsl` is a legacy no-op in modern Unity (may emit a warning).

**Keywords / variant count.** Exactly one mod-declared keyword,
`multi_compile_local __ ENABLE_WATER_REFLECTIONS` (2 variants), on each of the two surface shaders.
Everything else is a uniform, not a keyword — the mod deliberately branches on `int` uniforms
(`_FogMode`, `_SnowCapsEnabled`, `_EnableTreesAndDirt`, `_IPNMWater`, `_DisableCutout`,
`_HighlightLocations`, `_TextureSetSeasonCode`, `_UseSeaReflectionTex`) rather than compiling
variants. So there is **no variant explosion**: the count is whatever Unity's `surface … Lambert
alpha:fade noforwardadd nolightmap` generator produces (forward-base + shadow-collector family,
`DIRECTIONAL`/`SHADOWS_SCREEN`/`VERTEXLIGHT_ON`/fog) × 2 for the keyword × 2 programs — tens, not
thousands. It will need entries in `Assets/Shaders/RequiredShaderVariants.shadervariants` the way
the BLB skybox does, otherwise the variants get stripped from the iOS build.

**Sampler pressure — the one genuine Metal risk.** `FarTerrainCommon.cginc` declares
**19 `sampler2D`s**: 4 seasonal biome atlases + 4 `…SnowFree` + 4 `…Snow` = 12, plus
`_FarTerrainTilemapTex`, `_SkyTex`, `_SeaReflectionTex`, `sampler2D_float _CameraDepthTexture`,
and 3 legacy leftovers (`_TileAtlasTex`, `_TilemapTex`, `_BumpMap`) that nothing samples. **16 are
actually sampled** in the fragment stage. Unity's documented portable ceiling is 16 unique samplers
per stage, and the surface-shader generator adds its own (`unity_ShadowMask`, `_ShadowMapTexture`,
`unity_ProbeVolumeSH`, …). This shader sits exactly on the line and may fail to compile for Metal
with "maximum sampler count exceeded" — **must be verified by an actual iOS shader compile before
anything else is planned.**

**Per-fragment cost.** Up to 3 `tex2Dgrad` for the slope-blended biome (grass/dirt/stone slots),
plus up to 3 more when a snow cap re-samples the summer set (`ATLAS_SUMMER`), plus 1 for the snow
tile, plus 1 for woodland dirt, plus 2 `tex2D` on the tilemap (climate and region), plus up to 2 on
`_SkyTex` in `fcolor` — **≈12 texture fetches per fragment**, with `ddx`/`ddy` for the analytic
normal (`normalize(cross(ddx(p), -ddy(p)))`), two octaves of value noise for the snow line, two more
for the dirt patches, plus the beacon maths. And it runs `alpha:fade` in the `Transparent-499`
queue, with `discard` inside the near footprint — i.e. an alpha-blended, discarding, ~12-fetch
full-screen-ish layer. On a tile-based iOS GPU `discard` forces the fragment out of the fast path
and defeats hidden-surface removal. **This is fragment-bound, and it is the second big risk.**

One dead-code note: `fcolor` samples `_CameraDepthTexture` into `rawZ`/`sceneZ` and never uses
them. Nothing sets `Camera.depthTextureMode`, so on Metal it reads an unbound texture (harmless
default) — but it should be deleted rather than shipped.

`DaggerfallBillboardBatchFaded.shader` (97 lines, `target 3.0`, `surface surf Standard vertex:vert
addshadow alpha:fade keepalpha`) is unreferenced dead weight; drop it.

Compiled-in mechanics: the shader must live under `Assets/Shaders/` with `FarTerrainCommon.cginc`
beside it (relative `#include`), and `_startupMod`'s `mod.GetAsset<Shader>(...)` becomes
`MobileShaders.Find("Daggerfall/DistantTerrain/DistantTerrainTilemap")` — exactly the
`BLBSkybox.cs:103` pattern.

## 5. Rendering cost model

**Extra cameras: two (three total).**

| Camera | `depth` | Clear | Culling mask | Target | Clip |
|---|---|---|---|---|---|
| `stackedCameraSkyboxRenderToTextureGeneric` | −10 (setting) | Skybox | `0` (nothing) | `renderTextureSky` **256×256 ARGB32, 16-bit depth** | copies main |
| `stackedCamera` | 2 (setting) | Depth | `layerWorldTerrain` + `Water` | `Camera.main.targetTexture` | near = adaptive 5…980, far = `blendEnd` 120000 |
| `Camera.main` | 3 (setting) | **forced to `Depth`** | unchanged | unchanged | **`farClipPlane` forced to 15000** |

Both extra cameras carry `CloneCameraRotationFromMainCamera` (and the stacked one also
`CloneCameraPositionFromMainCamera`) doing a `LateUpdate` transform copy. `SetUpCameras()` re-runs on
exterior transition, save load, and retro-mode change, and `Update()` calls `SyncStackedCameraToMain()`
**every frame** (FOV match + `ComputeSafeNearClipPlane` recompute).

**Render textures: one, 256×256 ARGB32 + 16-bit depth** (~0.3 MB) — but it is **re-rendered every
frame**, and with our Dynamic Skies port active that means a **third full evaluation of the BLB
procedural skybox shader** per frame (main camera, stacked camera clear, and this RT). Cheap in
pixels, not cheap in shader.

**Draw calls / vertices.** Unity splits terrain into 32×32-quad patches: 1024 quads per side ⇒
32 × 32 = **1024 patches**, one draw call each. With `farClipPlane = 120000` the visible radius is
120000 / 819.2 ≈ **146 map pixels**, i.e. ~4.6 patches — so realistically **~30–80 patch draw
calls** and, at one quad per 819-unit map pixel, only **~20–40k triangles**. `heightmapPixelError = 0`
disables Unity's terrain LOD, but the mesh is coarse to begin with, so **geometry is not the
problem** — the fragment shader and the texture memory are.

**Per-frame work in `DistantTerrain.Update()`**: `shouldUpdateSeasonalTextures()`; a
`materialTemplate` fetch and 7 change-tracked uniform pushes (`pushIntCached` / `pushFloatCached`
short-circuit when unchanged — well optimised); the winter-river season gate; the retro-mode check;
`SyncStackedCameraToMain()`. Plus 3 `LateUpdate` transform copies. Negligible CPU.

**Per-map-pixel-cross work** (on `OnUpdateTerrainsEnd`, i.e. on top of DFU's own terrain rebuild):
two `SetInt` for the cutout centre, then `UpdateMountainVisibility` → a loop over all **32,928**
`MountainDef`s (four integer compares each — cheap), and if any flipped, a dirty-rect reset from
`baseWorldHeights`, re-lift of every overlapping mountain, `ReapplyOceanMask`, a fresh
`float[rectH,rectW]` allocation, and one `terrainData.SetHeights(x0, y0, sub)`. `SetHeights` forces
Unity to recompute normals and re-upload for that rect — a main-thread hitch stacked on the frame
DFU is already spending on the near terrain. Worse on horseback / fast travel, where pixel crosses
come in bursts.

**Twice-per-in-game-year**: `RebuildFarTerrainForSeasonRivers()` — full 1000² restore, full deriv
re-carve (2M-pixel loop), full clone, full `InitMountainVisibility` (all 32,928 lifts), and a full
`SetHeights(0,0,worldHeights)`. Hundreds of milliseconds.

**World-entry cost** (all synchronous, inside `StreamingWorld.OnReady`): 500k-pixel heightmap
sample; 2M-pixel deriv carve; 3.5 MB of CSV string-splitting; 32,928 pyramid lifts; a 1000²
climate+region+location bake with an `HasLocation` call per pixel; **12 × `GetTerrainTilesetTexture`**;
one full `SetHeights`. Seconds, not milliseconds.

### The memory blocker

`GenerateWorldTerrain()` calls `GetTerrainTilesetTexture` for **12 archives**
(2/3/4, 102/103/104, 302/303/304, 402/403/404 — desert/mountain/woodland/swamp × summer/winter/rain).
In our fork `TextureReader.GetTerrainTilesetTexture` builds
`new Texture2D(2048, 2048, TextureFormat.ARGB32, MipMaps)` — **uncompressed ARGB32 with mipmaps
≈ 22.4 MB each ⇒ ~268 MB resident**, plus twelve transient `Color32[2048*2048]` (16 MB each) and
twelve `TextureFile` decodes during the build.

And our build gets **none of this for free**: `Assets/Resources/defaults.ini.txt` ships
`EnableTextureArrays=True`, and `TerrainMaterialProvider.cs:171` enables the texture-array path
whenever `SystemInfo.supports2DArrayTextures` — which Metal does. So the near terrain uses
`Daggerfall/TilemapTextureArray` with a 56-slice 64² `Texture2DArray` (~1 MB) and **never builds a
2048² atlas at all**. Distant Terrain would therefore add the entire ~268 MB from scratch, on a
platform where the whole app budget is ~2 GB and iPads with 3–4 GB of RAM are in scope. **This
alone is disqualifying for a straight port.**

The fix is not a setting — it is a shader rewrite: port `FarTerrainCommon.cginc` from
12 × `sampler2D` atlas + `getColorByTextureAtlasIndex` (gutter/atlas UV maths, `tex2Dgrad`) to
`UNITY_DECLARE_TEX2DARRAY` slices, the way DFU's own `DaggerfallTilemapTextureArray.shader` does.
That also fixes the sampler-count risk in one stroke (12 samplers → 3 arrays), and Nystul's deleted
`TransitionRingTilemapTextureArray.shader` is at least a reference for the idiom. Call it a
significant, from-scratch piece of shader work with its own visual-regression risk.

### Quality settings — is there a low preset?

`modsettings.json` has 10 sections and 30 keys, and several are genuine cost dials:
`BasicMode/EnableBasicMode` (skips **all** 32,928 CSV mountain lifts and the entire deriv-map carve
and its 11 MB texture — the single biggest lever, and it exists precisely for setups without WoD),
`TreesAndDirt/EnableTreesAndDirt` (drops the procedural specks and the woodland-dirt fetch),
`LocationHighlight/HighlightDistantLocations` (skips the per-pixel `HasLocation` bake and the
beacon branch), the 5 `Fog` densities, 8 `WinterSnow` + 8 `WinterRivers` per-climate toggles, and 3
`CameraStacking` depths. **But the two dominant costs — the 12 atlases and the fragment shader's
fetch count — have no switch at all.** Snow caps (`DistantTerrainSnowCapConfig.EnableSnowCaps`) and
the region treatment table are code-only, not exposed. Also note `blendEnd = 120000` and
`mainCameraFarClipPlane = 15000` are public inspector fields with no settings binding — trivially
lowerable in a port, and lowering `blendEnd` is the cheapest real win (fewer patches, fewer
fragments).

So: a low preset is buildable (Basic Mode + trees/dirt off + no beacons + a much shorter
`blendEnd`), and Basic Mode conveniently sheds the WoD-specific data entirely — but it does not
touch the 268 MB.

## 6. Dependencies

**Manifest declares no `Dependencies` at all.** The `ReadMe` says only "This mod is compatible with
World of Daggerfall."

- **WoD Terrain: not required.** No reference anywhere. (Good — it is the compute-shader mod we
  already ruled out.)
- **WoD Biomes: not required.** No reference.
- **Daggerfall Expanded Textures / DET: not required.** No reference. It reads DFU's own
  `GetTerrainTilesetTexture`, which honours whatever texture replacement is installed.
- **World of Daggerfall (the location mod): not required to run, but required for the point.** The
  three CSVs are placements of WoD's `WOD_Mountain*` / `WOD_Mountain_Small*` /
  `WOD_Foothills*` prefabs; without WoD placing those meshes in the near world, the far heightmap
  grows 32,928 peaks that simply are not there when you walk up to them (the un-lift-on-approach
  logic hides the discontinuity, which is exactly why it exists). `BasicMode` is the author's own
  supported answer: it skips both the lifts and the deriv carve and gives you plain Nystul-style
  distant terrain.
- **Location Loader**: only transitively, as WoD's own dependency. Not referenced.
- **Vanilla terrain sampler: yes, works.** This fork deleted `ImprovedTerrainSampler.cs` and never
  assigns `DaggerfallUnity.Instance.TerrainSampler`; it only reads `MaxTerrainHeight`,
  `OceanElevation` and `TerrainHeightScale(x,y)` off whatever sampler is installed. So it composes
  with our `DefaultTerrainSampler` and with `BasicRoadsTexturing`.
- **Nystul's `Distant Terrain` (MIT): mutually exclusive** — identical GUID.
- **Nystul's `RealtimeReflections`: optional and currently inert** (see §1.7).
- **Dynamic Skies: already mutually aware, see §7.**

## 7. Interaction with our compiled-in Dynamic Skies port

Good news: **upstream already solved this, in Dynamic Skies' favour.** Our port at
`Assets/Scripts/Game/Mobile/Ports/DynamicSkies/BLBSkybox.cs` contains explicit Distant Terrain
handling:

- `BLBSkybox.cs:182` — `GameObject.Find("DistantTerrain")`; if absent it sets
  `CameraClearManager.cameraClearExterior = CameraClearFlags.Skybox` on the player camera; if
  present it instead grabs `GameObject.Find("stackedCamera")` into `stackedCam` (line 188–191).
- Everywhere it would set `playerCam.clearFlags = Skybox` it sets `stackedCam.clearFlags = Skybox`
  instead when `stackedCam != null` (lines 345–360, 373–383, 395–405).
- `LateUpdate()` is commented *"Force skybox flag to prevent Distant Terrain from overriding it
  again in it's Update function"* — i.e. it deliberately wins the clear-flags fight that
  `DistantTerrain.SetUpCameras()` (`Camera.main.clearFlags = Depth`,
  `stackedCamera.clearFlags = Depth`) would otherwise pick every transition.

So they do **not** fight over the sky. The remaining friction points, all manageable:

1. **Start order is load-bearing.** `BLBSkybox.Init` looks up `"DistantTerrain"` and
   `"stackedCamera"` by name *at Init time*. `MobilePortedMods.StartEnabled` must therefore run
   `_startupMod.InitStart` **before** `BLBSkybox.Init` — but `stackedCamera` is created in
   `DistantTerrain.SetupGameObjects()`, which runs from `Start()`/`SetUpCameras()`, i.e. a frame
   later than `InitStart`. On desktop this works because DFU's `[Invoke]` ordering plus MonoBehaviour
   `Start()` happen to interleave favourably. In our port the ordering is explicit and must be got
   right, or Dynamic Skies silently takes the no-Distant-Terrain branch and the sky clears on the
   wrong camera.
2. **Fog is contested.** `DistantTerrain.Start()` unconditionally overwrites all five
   `WeatherManager.*FogSettings` with its own exponential densities; Dynamic Skies drives
   `_FogDayColor` / `_FogNightColor` / fog distance on the skybox material and reads DFU's weather
   settings. Not a crash, but the two will disagree about fog until reconciled — a visual-tuning
   task, on device, with someone's eyes on it.
3. **`RenderSkyboxWithoutSun`** mutates `RenderSettings.skybox`'s `_SunSize` in `OnPreRender` and
   restores it in `OnPostRender`. The BLB skybox does have `_SunSize` (`BLBSkybox.cs:155`), so it
   works — but it is a per-frame material write on the shared skybox material, and if the ordering
   ever slips the sun stays at size 0.
4. **Far clip plane.** `DistantTerrain` forces `Camera.main.farClipPlane = 15000` and puts the far
   terrain on a camera reaching 120000. Dynamic Skies never touches `farClipPlane`, so no conflict
   — but our retro-rendering path does touch `Camera.main.targetTexture`, and
   `SetUpCameras()` copies it to the stacked camera and re-runs on `RetroRenderingMode` change.
   Untested combination.
5. **`CameraClearManager`** is DFU's own; the `stackedCam` branch bypasses it entirely, so DFU's
   exterior clear behaviour changes shape when Distant Terrain is on. Worth a look before shipping.

## 8. Licences

| Component | Licence |
|---|---|
| Nystul's `DistantTerrain` (the code ancestor: `DistantTerrain.cs`, `_startupMod.cs`, the two camera clones, `RenderSkyboxWithoutSun.cs`, `DistantTerrainTilemap.shader`, `FarTerrainCommon.cginc`, `DaggerfallBillboardBatchFaded.shader`) | **MIT.** `LICENSE` at `Nystul-the-Magician/dfunity-mods` root, "Copyright (c) 2017 Nystul-the-Magician". Every source file carries `//License: MIT License (http://www.opensource.org/licenses/mit-license.php)`. |
| The WoD fork's own additions (the ~1,270 changed lines of `DistantTerrain.cs`, the 714 added to `_startupMod.cs`, `DistantTerrainFlyMap.cs`, `ThirteenthPassageEffect.cs`, the ~557 added cginc lines, the 3 Mountains CSVs, `daggerfall_deriv_map.png`, `modsettings.json`) | **NO LICENCE DECLARED.** No `LICENSE` file, no `licence` field in the manifest, no per-file header on the new files. The modified files keep Nystul's MIT header and add `//Contributor: MaoDeVaca` — which reads as MIT-derived, but that is inference, not a declaration. GitHub reports the fork repo as `license: none`. |
| somestupidgirl's build scripts (`Editor/BuildDistantTerrainMods.cs`, `Makefile`) | **NO LICENCE DECLARED.** Repo has no `LICENSE`. Not needed for a port anyway. |

Practical position: identical to Dynamic Skies and World of Daggerfall. The MIT base is safe; the
WoD fork's additions are unlicensed, so this would ship `private_only` in
`tools/bundled-mods/mods.json`, never in the public MIT pack, pending Ikram's permission from
MaoDeVaca (and note the file naming: the manifest credits Nystul as author, so the permission
conversation is with MaoDeVaca for the fork). A `pending:` licence line and a port header naming
both commits, per the established pattern.

Nystul's MIT base being clean does mean a **fallback exists**: port Nystul's original 2.9.1
`DistantTerrain` under MIT (no permission needed, publishable), which is smaller (3,686 lines, no
CSVs, no deriv map) — but it replaces DFU's terrain sampler, brings back the transition-ring
shaders, and carries the same 12-atlas memory problem. Worth pricing separately if the WoD flavour
is refused.

## 9. iOS risk list and verdict

### IL2CPP
Essentially clean. No reflection, no `Type.GetType`, no dynamic codegen, no threading, no
coroutines, no file IO — the least IL2CPP-hostile mod we have looked at. One
`Enum.TryParse<KeyCode>`, concretely instantiated. `Wenzil.Console` and `EntityEffectBroker`
registrations drop out with the FlyMap/spell files. **Low risk.**

### Metal
No tessellation, no geometry shaders, no compute, no `Texture2DArray`, `#pragma target 3.0` only,
no `GrabPass`. **The one real hazard is sampler count: 16 sampled `sampler2D`s in one fragment
stage, exactly at Unity's portable ceiling, before the surface-shader generator adds its own.** A
Metal compile could fail outright. Secondary: `Input` declares `float4 pos : SV_POSITION` inside a
surface-shader `Input` struct (unusual; Unity tolerates it on desktop, unverified on Metal), and
`fcolor` samples an unbound `_CameraDepthTexture`. **Medium risk, and cheap to falsify — one
iOS shader compile answers it.**

### Memory
**The blocker.** ~268 MB of uncompressed 2048² ARGB32 mipmapped tile atlases that our build would
not otherwise allocate at all (we ship `EnableTextureArrays=True` and Metal supports arrays, so the
near terrain uses a ~1 MB `Texture2DArray`). On top: ~11 MB VRAM + ~11 MB CPU for the deriv map,
~14 MB of managed heightmap arrays, 8 MB for the tilemap texture and its staging array, a multi-MB
1000² alphamap that serves no purpose, plus ~200 MB of transient `Color32[2048*2048]` churn during
atlas construction. **High risk; disqualifying without the texture-array rewrite.**

### Thermal / sustained frame rate
An alpha-blended, `discard`-ing, ~12-fetch fragment layer across most of the screen, a third
per-frame skybox evaluation into a RenderTexture, two extra cameras with per-frame FOV/near-clip
resync, a main-thread `SetHeights` hitch on every map-pixel cross (worse on horseback and fast
travel), a seconds-long synchronous world-entry build, and two full 1000² rebuilds per in-game
year. Even if it fits in memory, sustained outdoor travel is where iPads throttle. **High risk.**

### Other
- If our pack pipeline ASTC-compresses `daggerfall_deriv_map.png`, `GetPixels32()` throws, the
  `catch` swallows it, and every distant river and coastline silently disappears. Must be pinned
  uncompressed, and mipmaps disabled.
- Mutually exclusive with Nystul's `Distant Terrain` (same GUID) — needs a `MobileModConflicts`
  entry if both are ever offered.
- `DistantTerrainFlyMap.cs` is keyboard+mouse+IMGUI only and adds a `TerrainCollider` to a 1025²
  terrain; `ThirteenthPassageEffect` writes a custom effect key into saves. Both should be dropped,
  which is 26% of the code and no loss.
- Dead weight to drop: `DaggerfallBillboardBatchFaded.shader`, the three unused 500 KB `.bin.txt`
  files, `Prefabs/debug_DistantTerrain.prefab`, the two `.png~` backups, `Editor/`.

### Top 3 risks

1. **~268 MB of terrain tile atlases we do not currently allocate.** The mod's shader is
   atlas-based (12 × 2048² ARGB32 + mips) while our build runs DFU's texture-array terrain path,
   so this is additive, not shared. No setting mitigates it. Fixing it means rewriting
   `FarTerrainCommon.cginc` onto `UNITY_DECLARE_TEX2DARRAY` — significant new shader work with its
   own visual-regression risk, and it is a precondition for everything else.
2. **Fragment cost and thermals.** ~12 texture fetches, `ddx`/`ddy`, four octaves of noise,
   `alpha:fade` in the transparent queue, and `discard` (which defeats iOS tile-GPU hidden-surface
   removal) over most of the screen, plus a third per-frame skybox render. Lowering `blendEnd` and
   turning off trees/dirt/beacons helps; the fetch count does not go away.
3. **Sampler-count / Metal compile.** 16 sampled `sampler2D`s in one fragment stage, at Unity's
   portable ceiling, before the surface-shader generator's own additions. Cheapest thing to test
   and it gates the whole effort. (The texture-array rewrite in risk 1 also fixes this — 12
   samplers become 3 arrays — which is another reason it is the right first move.)

Honourable mentions: the main-thread `SetHeights` hitch on every map-pixel cross; the seconds-long
synchronous world-entry build (12 atlas decodes + 3.5 MB of CSV string-splitting + 2M-pixel deriv
carve); the unlicensed WoD-fork additions; and the Dynamic Skies start-order dependency on
`GameObject.Find("stackedCamera")`.

### Verdict: **HARD**

Not because of IL2CPP or Metal features — the code is unusually clean and the shaders use nothing
iOS forbids. It is hard because the mod's texturing architecture is fundamentally the wrong one for
our build: it wants twelve uncompressed 2048² atlases that our texture-array terrain path never
creates, and no configuration option reduces that. A straight copy-in port is not viable. The
realistic path, in order:

1. Compile the shader for iOS as-is and see whether it clears the sampler limit (hours). If it does
   not, that is the answer.
2. Rewrite `FarTerrainCommon.cginc` + `DistantTerrainTilemap.shader` onto
   `Texture2DArray` (using DFU's own `DaggerfallTilemapTextureArray.shader` and Nystul's deleted
   `TransitionRingTilemapTextureArray.shader` as references). This is the real work and the real
   risk — days, with visual regression against desktop.
3. Port the 3 runtime files worth keeping (`DistantTerrain.cs`, `_startupMod.cs` trimmed, the two
   camera clones, `RenderSkyboxWithoutSun.cs` — ~3,050 lines), drop the FlyMap and the spell, fix
   the `TerrainData` over-allocation (alphamap / basemap / detail resolution), pre-bake the CSVs to
   binary, pin the deriv map uncompressed and mip-free, bind `blendEnd` to a settings dial, and
   wire the Dynamic Skies start order.
4. Ship it off by default with a low preset (Basic Mode, no trees/dirt, no beacons, short
   `blendEnd`) and measure on a device before believing any of the above.

Compared with the mods already ported: Dynamic Skies was one shader and 2,926 lines of clean C#;
World of Daggerfall was data plus one script. This is a renderer. Recommend **deferring it** behind
device verification of the survival mods and WoD, and treating step 1 as a cheap go/no-go spike if
someone wants an answer sooner.

## Appendix: reproduction

```
git clone https://github.com/somestupidgirl/Distant-Terrain-of-the-World-of-Daggerfall
git -C Distant-Terrain-of-the-World-of-Daggerfall rev-parse HEAD   # d454b30…
git clone https://github.com/Nystul-the-Magician/dfunity-mods
git -C dfunity-mods rev-parse HEAD                                 # fc58546…
diff -u --strip-trailing-cr dfunity-mods/DistantTerrain/Scripts/DistantTerrain.cs \
        Distant-Terrain-of-the-World-of-Daggerfall/Scripts/DistantTerrain.cs
```
