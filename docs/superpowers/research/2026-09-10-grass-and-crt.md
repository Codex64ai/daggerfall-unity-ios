# Grass and CRT on iOS: candidate research

Date: 2026-09-10. Read-only research; no code changed, nothing committed.
Repo: `/Users/ikrammassabini/dev/daggerfall-unity`, branch `unity6-upgrade` (DFU 1.1.1 fork,
Unity 6000.3.23f1, IL2CPP + Metal, **built-in render pipeline**).

Two asks: **(a) a grass mod**, **(b) a CRT filter**. They turn out to be very different problems.
Grass is a *licence + CPU/allocation* problem with one viable code base. CRT is not a mod problem at
all — nothing exists for DFU, and the port already owns the one composition point a CRT filter needs.

Working clones used for the code facts below (temporary, `/tmp/gcrt/`):
- `TheLacus/daggerfall-unity-mods` @ **`556ef6e1dd0f2da95aa34275a30861daf58fee86`** ("Bump version",
  author date 2023-08-25T20:39:31Z, **commit date 2023-08-26T00:38:46Z**) — contains `RealGrass/`,
  `RealGrassAssets/`, `VibrantWind/`. The newest commit touching `RealGrassAssets/` is a different
  SHA, **`9568b654`** "Fix load times" (2023-08-25T14:16:15Z); the newest touching `VibrantWind/` is
  **`2399820`** (author 2021-09-19, commit 2021-11-10). **The repo is archived** (2025-11-22), with
  no releases or tags. `realgrass-du-mod` and `daggerfall-unity-realgrass` both 301 here;
  `vibrantwind-du-mod` is 404 (VibrantWind was merged in from a since-deleted repo, merge `ebabe33f`).
- `Bl4ckh34d/daggerfall-wilderness-overhaul` @ **`b17c467b1b88b081c5d5a954e133456a2a3496a4`**
  ("Compatibility fix", **2026-03-15** — much newer than MOD-MASTER-LIST row 563 assessed).
- `DunnyOfPenwick/Retro-Frame` @ **`01fe089e6f1f8520c1b569602b26ac8330b3239e`** (2024-04-22).

---

## 0. The four terrain slots, and who is already in them

Everything on the grass side hangs off DFU's pluggable terrain slots plus one event. Current state of
this fork:

| Slot / hook | Who holds it on iOS today | File |
|---|---|---|
| `ITerrainTexturing` | **Basic Roads (compiled in)** | `Assets/Scripts/Game/Mobile/BasicRoadsTexturing.cs` |
| `ITerrainSampler` | WoD Terrain port (opt-in, default off) | `Ports/WorldOfDaggerfallTerrain/` |
| `ITerrainMaterialProvider` | WoD Biomes port (opt-in, default off) | `Ports/WorldOfDaggerfallBiomes/` |
| `ITerrainNature` | **Location Loader port** (`LocationTerrainNature.cs`), else `DefaultTerrainNature` | `Assets/Scripts/Terrain/TerrainNature.cs` |
| `DaggerfallTerrain.OnPromoteTerrainData` (static event) | WoD Terrain (`tileDataCache.UncacheTileData`), Location Loader | `Assets/Scripts/Terrain/DaggerfallTerrain.cs:394` |

Two mechanisms are available for "grass", and the candidates split cleanly along that line:

1. **Unity terrain details** — `TerrainData.SetDetailResolution` + `SetDetailLayer`, hooked from
   `OnPromoteTerrainData`. Real actual groundcover. This is Real Grass. Costs allocations and a
   detail-render pass.
2. **Nature billboards** — records added to the existing per-terrain `DaggerfallBillboardBatch` via
   `ITerrainNature.LayoutNature`. One mesh, one draw call per terrain, already drawn every frame.
   This is Wilderness Overhaul. Essentially free, but it is *sprites of plants*, not a grass carpet.

Baseline numbers this fork actually runs with (`Assets/Resources/defaults.ini.txt`):
`TerrainDistance=3` → **49 live terrains** ((2·3+1)²), pooled and reused by
`StreamingWorld` (`Assets/Scripts/Terrain/StreamingWorld.cs`); default detail resolution is
`heightmapDimension` with `resolutionPerPatch = 16` (`DaggerfallTerrain.cs:33-34, 270, 285`);
nature layout is 128×128 tiles per terrain, records 1–31 of the climate nature archive
(`TerrainNature.cs`, `DefaultTerrainNature.LayoutNature`).

---

# PART A — GRASS

## A1. Real Grass (Uncanny_Valley / TheLacus) — the only real candidate

**Repo.** `github.com/TheLacus/daggerfall-unity-mods`, folders `RealGrass/` (code) and
`RealGrassAssets/` (art). `TheLacus/realgrass-du-mod` is archived and redirects here; the source
headers still name it. Nexus 20.
**Latest commit touching the code:** `556ef6e` "Bump version", **2023-08-25**.
**Latest commit touching the art:** `9568b65` "Fix load times", 2023-08-25.
**Manifest:** `RealGrass.dfmod.json` — `ModTitle` "Real Grass", `ModVersion` **2.11**,
`ModAuthor` "Uncanny_Valley, TheLacus", `GUID` `2185b00e-bc5d-4758-81f5-7540817e2cbc`,
**`DFUnity_Version` "0.15.3"**.
**The repo is archived** (archived 2025-11-22), with no releases and no tags — so there is no
prebuilt bundle on GitHub, and `realgrass-du-mod` / `daggerfall-unity-realgrass` both 301 here.
Nexus 20 offers Windows / OSX / Linux builds (~3.2–3.5 MB) plus a 23 MB "Source" download —
**no Android and no iOS bundle**, so an AssetBundle rebuild was always going to be required.

### Licence — split, and the split is the whole story

`RealGrass/LICENSE` is plain **MIT**, verbatim first lines:

> MIT License
> Copyright (c) 2016-2019 Uncanny_Valley, TheLacus

`README.md`: *"The code in the RealGrass folder is licensed under MIT. Graphical assets in the
RealGrassAssets folder have different licenses. See credits.txt for license and attribution."*

`RealGrass/credits.txt`, verbatim on the restricted part:

> - VMblast, author of textures 'Grass_tex.psd' and all 'GrassDetails_*.psd'; 'DesertGrass.psd' is an
>   edited version of 'Grass.psd'. **Use of these textures is authorized for this project only
>   [RealGrass for Daggerfall Unity].**

and on the free part:

> - 60 CC0 Vegetation textures by rubberduck (License: CC0) …
> - Free 3D plants models by yughues (License: CC0) …
> - Free Handpainted Plants by yughues (License: CC0) …

So: **code MIT and clean; some grass textures are project-scoped to VMblast, and the rest of the art
is CC0.** Whether "this project" covers an iOS rebuild is a judgement call, not a fact
(MOD-MASTER-LIST §7 item 14 already flags this).

**The important new finding is that we never have to resolve that question, because the mod already
contains a licence-clean grass path.** The restricted list is exactly
`Grass_tex.psd`, `GrassDetails_01…06.psd` and `DesertGrass.psd`. Now read
`DetailPrototypesManager.SetGrass(string classic, string realistic)` (`:535-543`):

```csharp
string assetName = (options.GrassStyle & GrassStyle.Full) == GrassStyle.Full ? realistic : classic;
```

and its four call sites (`:428, 438, 444, 477-487`): they pass `("BrownGrass", "Grass")` or
`("GreenGrass", "Grass")`. **`Grass` — VMblast's texture — is used only by the `Full` style.** The
`Classic` and `Mixed` styles use `BrownGrass_tex.png` (28 KB) and `GreenGrass_tex.png` (40 KB), and
**neither of those is in VMblast's list**, so by `credits.txt` they come from the CC0 packs.
`Mixed` additionally pulls `GrassDetails_*` (restricted), so the clean set is:

> **`Style = Classic`, non-desert climates ⇒ only CC0 textures**, with zero licence question and a
> combined texture payload of **64 KB** (25,073 + 40,306 bytes).

The two exceptions to handle: `SetGrass(desertGrass, desertGrass)` at `:510` uses VMblast's
`DesertGrass` in *both* styles (desert climates need one replacement texture), and the plants /
stones layers use the CC0 `yughues` / `rubberduck` art anyway. Classic style is also the cheaper
render path, so the licence-clean configuration and the phone-friendly configuration are the *same*
configuration — a rare alignment, and the basis of the recommendation in §A5.

Two caveats on that, in fairness. `RealGrass/Changelog.txt` for 2.11 reads *"Use VMblast textures in
more places"* and *"New grass textures by VMblast"* — **2.11 increased the restricted surface**, so
this analysis is version-specific and would need redoing if anyone bumps the mod. On the other side,
the same changelog says *"Improved the green grass texture (I'm no artist but I try)"* in TheLacus's
own voice, which reads as his authorship of `GreenGrass_tex.png`; but `credits.txt` attributes
nothing to him explicitly and the two Classic textures' provenance is **inferred from the absence of
a VMblast claim, not positively established.** If we ship them, ship them with the credits file.

**Nexus 20's permissions grid is UNVERIFIED.** nexusmods.com returns 403 to every automated fetch
(as does forums.dfworkshop.net, behind Cloudflare), so the six-line
Upload/Modification/Conversion/Asset-use grid was never retrieved; that needs a browser or an
authenticated API key. Relayed second-hand from the GraphQL endpoint, and to be treated as a lead
rather than a fact:

> *"You are allowed to modify my files and release bug fixes or improve on the features so long as
> you credit me (TheLacus). This permission doesn't extend to the other authors of this mod (you
> will need to seek permission from them)."*
> File credits: *"VMblast - author of some textures (use authorized for this project only)."*

If accurate that is consistent with the repo files and with the plan above — TheLacus's half is
permissive with credit, VMblast's is not, and the Classic path avoids VMblast entirely. **Read the
page before relying on it.** (Also unresolved: Nexus reports the files updated 2023-08-26 in one
place and 2024-12-09 in another.)

### Code size and entry points

`RealGrass/Scripts/` — **5 `.cs`, 83.0 KB total**, no editor code, no `#if UNITY_EDITOR`:

| File | Bytes | Role |
|---|---|---|
| `DensityManager.cs` | 38,517 | per-tile density decisions; allocates the detail layers |
| `DetailPrototypesManager.cs` | 24,318 | builds `DetailPrototype[]`, loads assets, seasonal colours |
| `RealGrass.cs` | 15,866 | `MonoBehaviour`, mod entry, settings, terrain hook |
| `External/RealGrassConsoleCommands.cs` | 2,790 | console commands (droppable) |
| `Range.cs` | 1,548 | small struct |

Entry point is the standard DFU pattern our ports already reproduce:

```csharp
[Invoke(StateManager.StateTypes.Start, 0)]
public static void Init(InitParams initParams)   // RealGrass.cs:73
```
then `Start()` → `StartMod(true, false)` → **`DaggerfallTerrain.OnPromoteTerrainData += DaggerfallTerrain_OnPromoteTerrainData`**
(`RealGrass.cs:241`). `StopMod()` unsubscribes and blanks every live terrain's layers. It also sets
`mod.MessageReceiver`, `mod.IsReady`, `mod.LoadSettingsCallback` — all of which
`MobilePortedMods` already knows how to fake for a compiled-in mod.

DFU hooks used, complete list: `DaggerfallTerrain.OnPromoteTerrainData`,
`TerrainHelper.MakeTerrainKey`, `DaggerfallUnity.Instance.WorldTime.Now` (season/day/night),
`GameManager.Instance.StreamingWorld.StreamingTarget` (+ `TrackLooseObject` for the fireflies),
`ModSettings` / `modpresets.json`, `TextureReplacement.TryImportTextureFromLooseFiles`,
`mod.GetAsset<Texture2D>` / `mod.GetAsset<GameObject>`. It does **not** take any of the four terrain
slots — so **no conflict with Basic Roads, WoD Biomes, WoD Terrain or Location Loader.** That is a
genuinely good property; Real Grass is additive.

### Shaders — there are none, and that is a trap of its own

**Zero `.shader`, `.cginc`, `.compute`, `.hlsl` in the whole repo** (verified by `find`). No
geometry, no tessellation, no compute — so the Metal iOS restrictions do not bite directly.

But it does not follow that nothing needs to be built into the app. Real Grass renders through
Unity's stock terrain-detail path, and *that* path uses shaders:

- `DetailRenderMode.GrassBillboard` (`Billboard` style, `usePrototypeMesh = false`) → Unity's
  built-in `Hidden/TerrainEngine/Details/BillboardWavingDoublePass`, fed by `prototypeTexture`.
- `DetailRenderMode.Grass` with `usePrototypeMesh = true` (the "grass shader" style, and the default:
  `UseGrassShader = !settings.GetBool("Style", "Billboard")`) → renders the FBX prototypes with
  **their own materials**, and every one of the 12 `.mat` files in `RealGrassAssets/Materials/` is
  Unity's built-in **Standard** shader (`m_Shader: {fileID: 46, …}`) with `_ALPHATEST_ON`
  (+`_EMISSION` on five of them).
- `DetailRenderMode.VertexLit` is also referenced for stones/plants prototypes.

Two consequences. First, **Standard + alpha test on thousands of detail instances is the wrong
shader for a phone**; the `GrassBillboard` path is much cheaper and Real Grass already exposes it as
a setting (`Style/Billboard`). Second, `ProjectSettings/GraphicsSettings.asset` has exactly nine
always-included shaders — three built-ins plus `DaggerfallUIBlitShader`, `DaggerfallUIBlendShader`,
`DaggerfallPixelFont`, `DaggerfallSDFFont`, **`BLB/BLBProceduralSkybox`** and
**`DistantTerrain/DistantTerrainTilemap`** — and **none of them is a `Hidden/TerrainEngine/*`
shader**. (Note what the last two are: the shaders of the two *ported mods* that needed them. The
precedent for adding a port's shaders here is already set.) This fork
creates `Terrain` components purely at runtime (`GameObjectHelper.CreateDaggerfallTerrainGameObject`,
`StreamingWorld.cs:1157`) with no `Terrain` in any scene. On desktop the mod survives because its
`.dfmod` bundle embeds the shaders its own materials reference. **Compiled into the app with no
bundle-embedded materials, the terrain detail shaders can be stripped and the grass renders as
nothing — silently.**

That risk was probed further and it firmed up rather than dissolving. The three shaders
(`Hidden/TerrainEngine/Details/WavingDoublePass`, `.../BillboardWavingDoublePass`,
`.../Vertexlit`) are present in the Editor's `unity_builtin_extra` for 6000.3.23f1, but grep finds
**zero occurrences in `libiPhone-lib.a`** and none in
`PlaybackEngines/iOSSupport/Data/Resources/"unity default resources"`. Whether the player
force-includes them anyway could not be settled statically — it needs a device test. **The insurance
is three lines in a settings file, so just take it.**

The good news on the other side of the same question: **the API is not stripped.** Grepping the
actual `PlaybackEngines/iOSSupport/Variations/il2cpp/Managed/UnityEngine.TerrainModule.dll` finds
`SetDetailResolution`, `SetDetailLayer`, `wavingGrass{Strength,Amount,Speed,Tint}`,
`detailObjectDistance`/`Density`, `DetailRenderMode`, `useInstancing` and
`ComputeDetailInstanceTransforms` all present. And the detail shaders themselves are Metal-safe —
plain `#pragma vertex`/`#pragma fragment` at `#pragma target 2.0`, no geometry, no tessellation.

Mitigation is cheap and known: add the `Hidden/TerrainEngine/Details/*`
shaders to Always Included Shaders (and to `Assets/Shaders/RequiredShaderVariants.shadervariants`,
which already carries the `fileID: 46` Standard variants with exactly the
`SHADOWS_DEPTH / _ALPHATEST_ON / LIGHTMAP_OFF / _EMISSION` keyword sets these materials want — a
convenient coincidence, not a plan).

### How it actually renders, per terrain promotion

`AddTerrainDetails(daggerTerrain, terrainData)` (`RealGrass.cs:151`), called on **every**
`OnPromoteTerrainData`:

```csharp
terrainData.SetDetailResolution(256, 8);       // vs DFU's own (heightmapDimension, 16)
terrainData.wavingGrassTint = Color.gray;
terrain.detailObjectDistance = options.DetailObjectDistance;
terrain.detailObjectDensity  = options.DetailObjectDensity;
…
densityManager.InitDetailsLayers();            // allocates the layers
… 128×128 loop over daggerTerrain.TileMap deciding densities …
terrainData.detailPrototypes = detailPrototypesManager.DetailPrototypes;
terrainData.SetDetailLayer(0, 0, …Grass,        densityManager.Grass);
terrainData.SetDetailLayer(0, 0, …GrassDetails,  …);   // if Mixed style
terrainData.SetDetailLayer(0, 0, …GrassAccents,  …);   // if Mixed style
terrainData.SetDetailLayer(0, 0, …WaterPlants,   …);   // if enabled
terrainData.SetDetailLayer(0, 0, …Rocks,         …);   // if enabled
```

`InitDetailsLayers()` calls `EmptyMap()` per layer, and `EmptyMap` is
`new int[256, 256]` (`DensityManager.cs:861-866`). That is **256 KB of zeroed managed `int[,]` per
layer**, up to **5 layers ⇒ ~1.25 MB allocated and thrown away per terrain promotion**, on the
single most latency-sensitive path in the port. `SetDetailResolution(256, 8)` also *reallocates* the
terrain's detail store; and because `StreamingWorld` **pools and reuses** terrain objects, this is
re-run on the same `TerrainData` every time a map pixel is recycled — which at `TerrainDistance=3`
is a whole ring of terrains per map-pixel crossing.

**And the patch arithmetic is worse than the allocation figure suggests.** DFU's own call is
`SetDetailResolution(heightmapDimension, 16)` — 129/16 ⇒ ~8 patches per side, **~65 patches per
terrain**. Real Grass overrides to `(256, 8)`: 256/8 ⇒ 32 per side, **1,024 patches per terrain**, a
~16× increase, at *half* Unity's recommended `resolutionPerPatch`. At the shipped
`TerrainDistance=3` (49 live terrains) that is **~50,176 detail patches to cull every frame**, and
**~15.3 MB of resident detail-density data**. Dropping to `(128, 8)` quarters the resident data and
cuts patches to 256 per terrain; going to `(128, 16)` cuts them to 64 — which is *exactly* DFU's
own detail store, not a coarser or a finer one: `DaggerfallTerrain.PromoteTerrainData` calls
`SetDetailResolution(TerrainSampler.HeightmapDimension = 129, 16)` and Unity resolves that to a
128-square store at 16 per patch. (An earlier draft of this section claimed `(128, 16)` still kept
“four times DFU's detail resolution”; it does not — it *matches* DFU. The 4× data / 16× patch wins
are real, but they are against **upstream's** `(256, 8)`.) That is the setting to reach for first.

**Two field reports, snippet-sourced and therefore unverified** (dfworkshop.net 403s), both of which
land squarely on this port because we ship *both* mods involved: a thread titled *"Distant Terrain +
Real Grass High Memory Usage"* reports memory that **grows with fast travel**, scaling with terrain
count × enabled layers — and we ship `distantterrain.dfmod`; and a separate report that *"frame rate
performance when moving between cells with Real Grass is abysmal"* under Interesting Terrains — and
we ship that as `wod-terrain`. Neither is confirmed, but they describe exactly the two failure modes
the code above predicts, on exactly the two mods we already carry. **Test Real Grass with Distant
Terrain and WoD Terrain both on, not just standalone.**

**The shipped defaults are the worst possible configuration for a phone**, and every one of them is
a setting we control (`RealGrass/modsettings.json`):

| Setting | Shipped default | What it costs |
|---|---|---|
| `Style/Style` | **Full** (Classic\|Mixed\|Full) | all three grass layers ⇒ 3 × `SetDetailLayer` |
| `Style/Billboard` | **False** | ⇒ `DetailRenderMode.Grass` + `usePrototypeMesh` ⇒ FBX prototypes with the **Standard** shader |
| `Style/Stones` | **Full** | a 4th layer, and drags in the 13.7 MB `Rock.png`/`RockWinter.png` |
| `WaterPlants/Enabled` | **True** | a 5th layer |
| `Advanced/DetailDistance` | **120** (max 300) | detail draw radius in metres |
| `Advanced/DetailDensity` | **1.0** | full density |
| `Grass/ThickDensity` | **6–20** | instances per detail cell, at 256² cells |
| `Advanced/FlyingInsects` | False | the one thing already off |

No `DrawMeshInstanced`, no `CommandBuffer`, no `Graphics.DrawProcedural`: it is 100% Unity's
`Terrain` detail renderer. That renderer *does* batch/instance internally, and it *does* work on
Metal/iOS — the risk is not "will it draw", it is "what does it cost per frame and per stream".

### Data

`RealGrassAssets/` is **28 MB** on disk, of which 27 MB is `Textures/`:
`Rock.png` 7.9 MB, `RockWinter.png` 5.8 MB, `GrassDetails_04.psd` 4.7 MB, `DesertGrass.psd` 3.5 MB,
`Grass_tex.psd` 2.0 MB, five more `GrassDetails_*.psd` 0.4–0.8 MB, plus small PNGs. Meshes:
5 `.fbx` (16–40 KB each). 7 `.prefab`, 12 `.mat`, 1 particle prefab (`Fireflies.prefab`, 112 KB).

Note the shape of that: **the two biggest files are the terrain *stones*, an optional feature, and
the `.psd`s are source art, not shipping textures.** A shipping iOS bundle for grass alone —
one grass billboard texture plus three or four detail textures at 256², ASTC — is
**well under 2 MB**. The 28 MB figure is a red herring for our purposes; the licence-restricted
subset and the size-dominant subset are almost the same files, and both are droppable.

`.prefab` / `.fbx` / `.mat` cannot ship loose, so the art half is an **AssetBundle rebuild** either
way (the established path: `tools/bundled-mods/pack.py`, `~/daggerfall-mobile/editor`).

### Dependencies

None hard. Reads `Vibrant Wind`-style waving indirectly (it sets `wavingGrassTint` itself). Uses
`TextureReplacement.TryImportTextureFromLooseFiles(Path.Combine("Grass", name), …)` so loose-file
texture overrides work. `modpresets.json` ships four presets. No `SendModMessage` to other mods.

### iOS verdict — **MEDIUM**, and only in a reduced configuration

Top three risks, in order:

1. **Per-promotion allocation, patch explosion and stall.** Up to ~1.25 MB of `int[256,256]` per
   terrain, plus a `SetDetailResolution` reallocation and up to 5 `SetDetailLayer` uploads, on
   `OnPromoteTerrainData` — which at `TerrainDistance=3` fires for a ring of terrains on every map
   pixel crossing, already the port's worst hitch — **and a jump from DFU's ~65 detail patches per
   terrain to 1,024** (~50,176 across 49 live terrains). Fix: `SetDetailResolution(128, 16)` rather
   than `(256, 8)`, cache and `Array.Clear` the layer arrays instead of reallocating, and cut to a
   single grass layer.
2. **Terrain detail shaders stripped from an IL2CPP build.** The three
   `Hidden/TerrainEngine/Details/*` shaders are absent from `libiPhone-lib.a` and from the iOS
   player's default resources, nothing in the project references them, and DFU builds every terrain
   at runtime. A compiled-in Real Grass with no bundle-embedded materials can render **nothing at
   all, with no error**. Must be proven on device, not in the Editor; the fix is three lines.
3. **Stacking with what we already ship.** Two unverified field reports describe Real Grass's memory
   growing with fast travel *under Distant Terrain*, and cell-crossing frame rate collapsing *under
   Interesting Terrains* — and this port carries `distantterrain.dfmod` and `wod-terrain`. Whatever
   Real Grass costs standalone, the number that matters is the number with those two on.

(Fragment cost of the detail pass is the fourth risk and is configuration-dependent: the default
style renders FBX prototypes with the **Standard** shader, which on a 3.0 MP iPhone backbuffer with
alpha-tested overdraw is the kind of thing that halves frame rate. `Style = Classic` +
`Billboard = true` + `DetailDistance` ~30–40 m is the only configuration worth measuring.)

Rough cost: **CPU** the dominant term — ~1.25 MB transient allocation per promotion at stock
settings, ~0.3 MB at the reduced config, plus the patch-culling load above. **Memory** ~64 KB per
layer per terrain inside `TerrainData` (256² bytes) ⇒ 49 terrains × 1 layer ≈ **3 MB**, ×5 layers
≈ **16 MB** (≈15.3 MB measured from the code); quartered at detail resolution 128.
**Texture memory** ~4.9 MB for the whole art set as ASTC 6×6 with mips, and negligible for the
Classic subset. **GPU** unquantified without a device run; the honest position from
MOD-MASTER-LIST row 358 stands — *needs a device measurement before anyone promises it.*

**Good news on the staleness, though.** `DFUnity_Version` 0.15.3 vs our 1.1.1 looked like it implied
API drift; it does not. A symbol-by-symbol check against this tree found **zero gaps** — every
surface Real Grass 2.11 touches exists here: `OnPromoteTerrainData` (raised at
`DaggerfallTerrain.cs:311`), `ClimateBases`, `Seasons`, `TerrainHelper.MakeTerrainKey`,
`StreamingWorld.StreamingTarget`, `TrackLooseObject`, `PlayerTerrainTransform`,
`ModSettings.GetTupleFloat`/`GetTupleInt`/`GetColor`, `ModSettingsChange.HasChanged`,
`mod.MessageReceiver`/`IsReady`/`LoadSettingsCallback`,
`TextureReplacement.TryImportTextureFromLooseFiles`, and `Wenzil.Console`. The manifest version is
informational once the code is compiled in-tree. **The porting work here is configuration and
performance, not API archaeology** — which is unusual for this list and materially lowers the
estimate.

One import-pipeline detail to fix before any measurement: `ProjectSettings` carries **only a
`DefaultTexturePlatform` override and no iPhone override**, so grass textures would import at
`maxTextureSize` 2048 with default compression unless a per-platform override is added.
(`Rock.png`/`RockWinter.png` are 2048² but already capped at 512 on import by their own `.meta`.)
As ASTC 6×6 with mips the full art set is ~4.9 MB of VRAM; the Classic subset is a rounding error.

## A2. Wilderness Overhaul's grass (Bl4ckh34d & Macadaynu) — wrong mechanism, right instinct

`github.com/Bl4ckh34d/daggerfall-wilderness-overhaul` @ **`b17c467b`**, "Compatibility fix",
**2026-03-15**. Manifest: `ModTitle` "Wilderness Overhaul", `ModVersion` **1.1**,
`ModAuthor` "Bl4ckh34d & Macadaynu", `GUID` `153a082a-c594-4d5d-8093-5376a17c22b9`,
**`DFUnity_Version` "1.1.1"** — current with our fork, unlike Real Grass.
(Attribution note for the brief: **not Lypyl.** Lypyl is not in the manifest, the headers or the
commit history. The authors are Bl4ckh34d — who also goes by Daniel87 — and Macadaynu.)

**Provenance wrinkle:** Nexus 390 ships **v0.3.2** (2025-01-06) while this GitHub manifest says
**1.1**, so **GitHub is ahead of the published build and no repo exists for the 0.3.2 binary
players actually run.** For an unlicensed mod that makes the provenance of the shipped artefact
unverifiable, which is worth knowing before anyone opens a permission conversation.

**Licence: NONE DECLARED.** No `LICENSE` file, no `licence` field in the manifest, no per-file
headers, `README.md` is three repeated copies of the repo name. Same posture as the WoD family —
`private_only` at best, pending permission.

**Size:** 38 MB working tree. `Scripts/` 476 KB across 11 `.cs` — dominated by
`WOTerrainNature.cs` **186,945 bytes** and `WOVegetationList.cs` **137,075 bytes** (both are giant
hand-written decision tables). `Shaders/` is one file, `WOTilemapTextureArray.shader` (5.5 KB).
`Textures/` 14 MB — twelve `*-TexArray.asset` `Texture2DArray`s at 352 KB each plus loose PNGs.
`Out/` 8.3 MB of prebuilt bundles.

**How its "grass" renders.** `WOTerrainNature` is an `ITerrainNature`, and its one rendering call is
`baseData.dfBillboardBatch.AddItem(record, pos)` (`WOTerrainNature.cs:749`). There is **no
`SetDetailLayer`, no `SetDetailResolution`, no `DrawMeshInstanced`** anywhere in it. Its
`vegetationList.mountainsGrass` is a list of *record indices in the vanilla nature archive* —
`{ 7, 7, 7, 9, 26 }`, sometimes with `23` — i.e. Daggerfall's own plant sprites, weighted.

That is the key finding for the recommendation: **WO does not add a grass carpet. It adds a lot more
vanilla plant billboards, better distributed.** If Ikram's mental image is "lawn", WO is not it; if
it is "the wilderness stops looking bald", WO's mechanism is exactly right and costs essentially
nothing, because those billboards go into the batch DFU already builds and draws once per terrain.

**Blocking problem, unchanged from MOD-MASTER-LIST row 563.** `WildernessOverhaulMod.cs:111-116`
installs **three** slots at once:

```csharp
DaggerfallUnity.Instance.TerrainNature            = woNature;
DaggerfallUnity.Instance.TerrainTexturing         = woTexturing;   // ← collides with Basic Roads
DaggerfallUnity.Instance.TerrainMaterialProvider  = woMatProvider; // ← collides with WoD Biomes
```

`ITerrainTexturing` is held by our compiled-in Basic Roads; `ITerrainMaterialProvider` by the WoD
Biomes port. Upstream WO resolves the roads clash by `SendModMessage("BasicRoads", "scheduleRoadsJob", …)`
— which on iOS reaches nobody, because Basic Roads is compiled in and is not a mod
(MOD-MASTER-LIST §7 item 7). Its shader is a `#pragma target 3.5` surface shader (`BlinnPhong`, three
`multi_compile_local` keywords ⇒ 8 variants, `UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD`) — compiles for
Metal, no geometry/tessellation/compute — so the shader is not the problem. In fact
**`WOTilemapTextureArray.shader` appears to be dead weight**: the only `Shader.Find` in the whole mod
is `WOTerrainMaterialProvider.cs:33`,
`Shader.Find(MaterialReader._DaggerfallTilemapTextureArrayShaderName)` — DFU core's shader, not its
own. Nothing references the shipped file. The slot collision and the missing licence are the
problems.

**iOS verdict: HARD as shipped / BLOCKED on licence.** Top three risks: (1) no licence at all;
(2) a three-slot takeover that displaces Basic Roads and WoD Biomes and needs the `SendModMessage`
shim built first; (3) 324 KB of hand-written nature tables to port and keep in sync, for a result
that is *not* the grass carpet the ask implies.

**But its mechanism is free to imitate.** A ~200-line `ITerrainNature` of our own, subclassing
`DefaultTerrainNature` and adding weighted grass/plant records to the same batch, is licence-free,
takes only the slot Location Loader already contends for, and costs one extra pass over the 128×128
tile loop with no new allocation and no new draw call. Filed as the fallback in §A5.

## A3. Vibrant Wind (TheLacus) — animation only, confirmed, and useless alone

`TheLacus/daggerfall-unity-mods`, folder `VibrantWind/`. Latest commit `2399820` "Update
configuration", **2021-09-19**. Manifest: `ModVersion` **0.6**, `DFUnity_Version` **0.10.25**,
`GUID` `d2fcfc26-4c33-4d47-b566-920c8d3f5757`. **Licence: MIT** (`LICENSE`,
"Copyright (c) 2017 TheLacus"), plus MIT per-file headers.

**5 `.cs`, 20.2 KB.** Entry `[Invoke(StateManager.StateTypes.Start, 0)]`; hooks
`StreamingWorld.OnInitWorld` and `DaggerfallTerrain.OnPromoteTerrainData`. Its entire rendering
contribution is three property writes (`WindStrength.Assign`, `WindStrength.cs:49-54`):

```csharp
terrainData.wavingGrassStrength = Speed;
terrainData.wavingGrassAmount   = Bending;
terrainData.wavingGrassSpeed    = Size;
```

plus `WindZone` main/turbulence for trees and particles (DFU creates that `WindZone` itself in
`WeatherManager.AddWindZone()`, `WeatherManager.cs:478-482` — so the hook exists in our tree).

**Confirmed animation-only, and it does not require Real Grass in code** — but with no detail-grass
layers present, `wavingGrass*` drives nothing visible, so functionally it is a no-op without a
grass mod. Zero new shaders, zero data, zero draw calls, no slot taken.

**iOS verdict: EASY (trivial).** Top three risks: (1) it is decoration on top of whatever grass
decision we make, never a substitute; (2) `DFUnity_Version` 0.10.25 is the oldest thing in this
document — five versions behind — though the three API surfaces it touches are unchanged;
(3) if it drives `WindZone` turbulence up, DFU's rain/snow particle systems get more expensive, so
tune the weather curve rather than shipping the desktop defaults. Cost: nil.

## A4. Other grass / vegetation in the master list

Searched `MOD-MASTER-LIST.md` (270 rows) and the five backing reports for
`grass`, `vegetation`, `flora`, `foliage`, `Vibrant Wind`, `Wilderness Overhaul`, `Lypyl`, `DREAM`.
The complete set of hits is:

| Row | Mod | Grass? | Status |
|---|---|---|---|
| 358 / 303 | Real Grass (code / art) | **yes — terrain details** | §A1 |
| 563 | Wilderness Overhaul | plant billboards, not a carpet | §A2 |
| 554 | Distant Terrain (Nystul) | no — `ImprovedWorldTerrain` tree *coverage* maps only | already ported (opt-in) |
| — | WoD Biomes | **no placement at all** — re-textures ground + swaps nature *archives* (per `2026-09-09-wod-biomes.md` §1) | ported |
| — | WoD Terrain / Interesting Terrains | **no — confirmed by grep**: `grass|DetailPrototype|SetDetailLayer|vegetation|flora` across all of `Ports/WorldOfDaggerfallTerrain/` returns **zero hits**; it assigns `ITerrainSampler` + `ITerrainTexturing` only, and its only "grass" is a comment about marching-squares tile classification | ported |
| — | Upstream `monobelisk/DFUnity-InterestingTerrains` (Nexus 115) | no — never touches `ITerrainNature` | **`license: null`**, last push 2020-11-21, Nexus marks it **discontinued**; successor `Freak2121/DFUnity-InterestingErodedTerrains` (Nexus 193) also `license: null` |
| — | DREAM (13 modules) | **no grass or terrain-detail component at all** — asset packages, no C#, no shaders of its own; its only link to grass is a "DREAM 2025 Preset" *for* Real Grass | permission grid unretrievable (**unverified**); siblings 664 and 911 fully restrictive |
| 300 | 90s Villains and Villagers | no — NPC sprites, 704 MB | out of scope |

There is **no fourth grass mod for DFU** in the assessed corpus. Vibrant Wind is not in the master
list at all (it is named once, in `mod-inventory-github.md:20`) — this document is its assessment.

Corroborated by a second, independent sweep of **all 1,020 mods** under `daggerfallunity`
(`gameId 2927`) via the Nexus GraphQL API. Result:

> **Real Grass is the only mod in the entire Daggerfall Unity ecosystem that uses Unity Terrain
> detail layers.** "Basic Grass", "Lively Grass" and "Bloom Grass" **do not exist** for DFU (zero
> catalogue, GitHub and web hits). Name searches for `vegetation`, `flora`, `foliage`,
> `groundcover`, `plant`, `forest` returned **0** each. `grass` returned exactly three: Real Grass
> (20), **Pixelated Grass** (252, a texture replacer *for* Real Grass), and **Real Grass UA** (880,
> Ivan Kuzyk — a Ukrainian translation of the settings text, rendering-irrelevant).

Everything else in the vegetation space takes one of two other paths, neither of which is grass:

| Path | Mods | Notes |
|---|---|---|
| **Flat/sprite retexturing** (no placement code) | **Vibrant Terrain Flats** 82 (jman0war, v1.1 2020-05-17, 99 MB, repaints ~300 flats in archives 500–511; its page says these are included in DREAM); **Seasons of the Iliac Bay** 1377 (RosyTheRascal, v1.1 2026-09-03); **Snowless Woodlands** 1230 and **Snowless Swamp and Jungle** 735 (A Sphincter Says What); **Weeberfall Trees and Weeds** 1124 (Hellford, 917 loose PNGs) | Seasons 1377 is described by its author as *"script-injected"* so it may carry C#; no repo and Nexus does not index the archive, so **contents unverified**. Notably it **ships an Android build marked "Tested and working"** — relevant precedent. Permissions unverified throughout. |
| **Unity Terrain *tree* instances** (name-keyed prefab bundles, no C#, no shaders — via `MeshReplacement.ImportNatureGameObject` → `terrain.AddTreeInstance`) | **Low Poly Trees** 380 (SquidKamer v5, restrictive); **Simple 3d Trees** 438 (Nickoladze v0.971 2025-03-31, HD bundle **411 MB**, and **permissive**: *"You are allowed to use the assets in this file without permission as long as you credit me"*, *"You can convert this file to work with other games as long as you credit me"*); **Handpainted model replacements** 9; **Invisible Trees** 405 (most permissive of all — no permission or credit required) | A different mechanism from either grass path, and cheap, but it is trees, not groundcover. Simple 3d Trees' explicit conversion permission makes it the standout if trees ever come up separately. |

**Nothing in the DFU ecosystem uses geometry shaders, tessellation, or `DrawMeshInstanced` for
vegetation** — so the Metal restrictions in the brief never bind on any candidate.

**One adjacent find worth acting on:** **Better Terrain Loading** (Nexus **1389**, WinchesterUTD,
v0.1, published **2026-09-10** — today) — *"Attempts to optimize terrain loading, especially when
crossing tile borders. Should reduce 'hitches'."* 382 KB, no repo, licence unverified. That is aimed
at precisely the per-promotion cost that makes Real Grass risky here. Brand new and unassessed, but
it belongs on the list to look at alongside any grass work.

## A5. Grass recommendation

**Port Real Grass's MIT C# half, ship it in its Classic style — which is both the licence-clean and
the phone-friendly configuration — and make it opt-in.** Concretely:

1. Take `RealGrass/Scripts/` (83 KB, MIT, 5 files) into
   `Assets/Scripts/Game/Mobile/Ports/RealGrass/`, drop `RealGrassConsoleCommands.cs`, and start it
   from `MobilePortedMods` as an ordinary launcher entry, default **off**.
2. **Ship `Style = Classic` and do not ship VMblast's textures.** Classic uses only
   `BrownGrass_tex.png` + `GreenGrass_tex.png` — **25,073 + 40,306 = 64 KB, CC0** — so the project-scoped grant never
   comes into it. Ship one in-house or CC0 replacement for `DesertGrass` (or simply reuse
   `BrownGrass` in desert climates: a two-line change at `DetailPrototypesManager.cs:510`).
   Drop `Grass_tex.psd`, all six `GrassDetails_*.psd`, `DesertGrass.psd`, and drop
   `Rock.png`/`RockWinter.png` (13.7 MB of the 28) with the stones feature.
   **Data bundle goes from 28 MB to well under 1 MB.**
3. **Ship it in a reduced configuration and hard-cap the settings** — note that every one of these
   differs from the mod's shipped default, so none of them can be left to the mod's own
   `modsettings.json`: `Style = Classic` (not Full), `Billboard = true` (⇒ `GrassBillboard`, not
   Standard-shaded FBX prototypes), `Stones = Disabled`, `WaterPlants = false`,
   **`SetDetailResolution(128, 16)`** not `(256, 8)` — that quarters the resident detail data *and*
   brings patches per terrain from 1,024 back to 64 — which is DFU's own detail store exactly
   (`SetDetailResolution(129, 16)` ⇒ a 128-square store, 64 patches), not four times its detail
   resolution as an earlier draft said — `DetailDistance` ~30–40 (not 120),
   `FlyingInsects` off (already the default). Add an iPhone texture-platform override so the
   textures do not import at 2048.
4. **Two code changes before any device test**: cache the layer arrays and `Array.Clear` them in
   `InitDetailsLayers` instead of `new int[256,256]` per promotion; and add the three
   `Hidden/TerrainEngine/Details/*` shaders to Always Included Shaders + `RequiredShaderVariants`
   (they are absent from the iOS player libraries — see §A1 risk 2).
5. **Measure it with Distant Terrain and WoD Terrain switched on**, not standalone. That is where
   the two community reports of memory growth and cell-crossing stalls come from, and it is the
   configuration a player who turned the pretty things on will actually run.
6. **Pair it with Vibrant Wind** (MIT, 20 KB, trivial) — it is what makes detail grass read as grass
   rather than as stickers, and it is free.
7. Look at **Better Terrain Loading** (Nexus 1389, published 2026-09-10, licence unverified) while
   doing this — it targets exactly the tile-crossing hitch that Real Grass amplifies.

**Fallback if the device measurement says no** (and it may): our own ~200-line `ITerrainNature`
subclassing `DefaultTerrainNature`, adding weighted grass/plant records to the existing
`DaggerfallBillboardBatch` — Wilderness Overhaul's mechanism, none of its licence or slot problems,
no new draw calls, no new allocations. Not a carpet, but it fixes "the wilderness looks bald" for
approximately zero frame cost. Do this one **only** as the fallback; it competes with Location
Loader for `ITerrainNature`, so it must subclass `LocationTerrainNature` when that port is on.

---

# PART B — CRT / RETRO

## B1. What this fork already has, precisely

This matters more than any mod, because a "CRT feel" is mostly resolution and palette, and both are
already implemented, shipped, and reachable on iOS.

**Files.** `Assets/Scripts/Utility/RetroRenderer.cs` (507 lines),
`Assets/Scripts/Utility/RetroPresentation.cs` (31 lines),
`Assets/Scripts/Utility/ViewportChanger.cs` (150),
`Assets/Scripts/Utility/CameraClearManager.cs`,
`Assets/Scripts/Game/UserInterfaceWindows/RetroModeConfigPage.cs` (152).
All MIT (Daggerfall Workshop, Interkarma + Pango).

**Shaders.** Three, all in `Assets/Shaders/`, all trivially Metal-safe, all already in the app:

| Shader | Lines/size | `#pragma target` | Keywords | Cost |
|---|---|---|---|---|
| `DaggerfallRetroPosterization.shader` | 1,382 B | *(none — defaults to 2.5)* | `multi_compile __ EXCLUDE_SKY` (2 variants) | 1 `tex2D`, +1 depth sample under `EXCLUDE_SKY`; rounds each channel to 4 bits |
| `DaggerfallRetroPalettization.shader` | 1,591 B | **3.0** | `multi_compile __ EXCLUDE_SKY` (2 variants) | 1 `tex2D` + 1 **`tex3D`** LUT, +1 depth sample under `EXCLUDE_SKY` |
| `DepthProcessShader.shader` | 1,234 B | *(none)* | — | pass-through (the "off" case) |

No geometry, no tessellation, no compute anywhere. Nothing here is an iOS problem.

**Settings** (`SettingsManager.cs:159-163, 408-412`; defaults in `Assets/Resources/defaults.ini.txt:9-13`):

| Key | Range | Default | Meaning |
|---|---|---|---|
| `RetroRenderingMode` | clamped 0–2 | **0** | off / 320×200 / 640×400. UI tip: *"Renders world at lower resolutions"* |
| `PostProcessingInRetroMode` | 0–4, **unclamped** | **0** | *not* an on/off for Unity's PP stack — it is the colour-crush selector: pass-through / posterize / posterize-minus-sky / palettize / palettize-minus-sky |
| `RetroModeAspectCorrection` | clamped 0–2 | **0** | `enum RetroModeAspects { Off, FourThree, SixteenTen }` (`DaggerfallUnityEnums.cs:797-802`). 320×200 *is* 16:10, so mode 2 pillarboxes at 6× in both axes and mode 1 stretches 20% taller to 4:3 (`ViewportChanger.SetRetroAspectViewport`) |
| `PalettizationLUTShift` | **unclamped** | **1** | LUT downsample exponent, `size = 256 >> shift`; 1 ⇒ 128³ |
| `UseMipMapsInRetroMode` | bool | **False** | when false, `TextureReader` disables mipmaps (`TextureReader.cs:93,123`). Not read by `RetroRenderer` |

Two of those are **unclamped** — `PostProcessingInRetroMode` and `PalettizationLUTShift` take whatever
integer is in the ini. That is the mechanism behind risk (1) in §B6.

Provenance, for the record (upstream `Interkarma/daggerfall-unity`): retro rendering landed in
`39f66203` "Retro 320x200 world rendering" (2019-05-29); posterization in `817477d1` (2020-04-19);
palettization in `29dc3cb3` (2020-05-02), merged as PR **#1807** in `b69d2e3e` (2020-05-06) and
rewritten to the `Texture3D` LUT in `58f35127` (2020-05-14); aspect correction plus the Effect
Settings page shipped in **v0.13.3-beta** (2021-11-24).

**The palette is the real deal**, not an approximation: `RetroRenderer.art_pal` is Daggerfall's
259-entry VGA `ART_PAL` written out as `Color32` literals (`RetroRenderer.cs:53-296`), fed to
`FastColorPalette.BuildPalette` and baked into a `Texture3D` nearest-colour LUT at
`FilterMode.Point` (`InitLut`, `RetroRenderer.cs:305-347`). At the shipped `PalettizationLUTShift=1`
that is a **128³ RGBA32 = 8 MB** 3D texture with an author-measured **~850 ms** build. The code's own
comment table warns that shift 0 is "64 MB, 7 s init" — **on iOS, `PalettizationLUTShift` must never
reach the app as 0**; at 8 MB and ~1 s (likely worse on an A-series) shift 1 is acceptable but should
be built off the first frame.

**Reachability on iOS — confirmed, and it is not obvious.** The port's own `MobileSettingsPanel`
has only Input / HUD / Advanced sections and **no video settings at all**
(`MobileSettingsPanel.cs:58`, "mod switches live only in the launcher's MODS window"). But
`MobilePauseOptionsWindow` extends `DaggerfallPauseOptionsWindow`, which builds a
`PauseOptionsDropdown` (`DaggerfallPauseOptionsWindow.cs:81`), whose "gameEffectsSettings" item
pushes `GameEffectsConfigWindow` (`PauseOptionsDropdown.cs:199-202, 275-277`), which does
`AddConfigPage(new RetroModeConfigPage())` (`GameEffectsConfigWindow.cs:106`). **So retro mode,
posterization, palettization and aspect correction are all already adjustable in-game on iOS**, via
pause → options dropdown → Game Effects → Retro Mode. Worth verifying on device; if that dropdown
item is hard to hit with a thumb, exposing the same three settings in `MobileSettingsPanel` is a
30-minute job and buys most of the "CRT feel" ask with no new code.

**What it does *not* offer:** no scanlines, no aperture grille / phosphor mask, no barrel curvature,
no vignette, no bloom/halation, no chromatic aberration. Confirmed by reading all three shaders and
`RetroModeConfigPage` here, and independently upstream —
`grep -rniE 'scanline|curvature|phosphor|shadow ?mask|aperture ?grille|barrel distort'` over
`Assets/Scripts` + `Assets/Shaders` of `Interkarma/daggerfall-unity` master
(`81e89e90c27bc3c1a7a61871e545fad129174dec`, 2026-06-30) returns **zero matches**. So the "CRT" half
of the ask genuinely is not covered — but "320×200 + true VGA palette + 4:3 stretch" is, and that is
the larger part of what people mean by "looks like DOS Daggerfall".

**There is no authoritative prose reference for these keys.** `dfworkshop.net/…/settings/` is 404 and
the GitHub wiki has only `Home` + `Enabling-Retro-Mode`; dfworkshop.net, the forums and Nexus all
return 403 to automated fetches. `SettingsManager.cs` + `defaults.ini.txt` *are* the documentation.

## B2. The composition point — this is the important part

The retro path is a three-stage chain, and it hands us an ideal, already-existing insertion point.

```
Camera.main ──renders into──▶  RetroTexture320x200 (320×200)   [Point filter, no mips,
   │                            or RetroTexture640x400          m_ColorFormat 24, depth 16]
   │                            (or the *_HUD variants, 320×154 / 640×308, docked large HUD)
   │
   │  PostProcessLayer (PPv2) runs HERE — inside the 320×200 texture, NOT gated by retro mode
   │
   │  RetroRenderer.OnPostRender():
   ▼      Graphics.Blit(retroTexture, RetroPresentationTarget, postprocessMaterial)
RetroPresentation RT (640×400, Point)
   │
   │  RetroPresentation.OnRenderImage(source, destination):
   ▼      Graphics.Blit(RetroPresentationSource, null as RenderTexture)   ◀── THE HOOK
device backbuffer (native: 2556×1179 iPhone 15 Pro ≈ 3.0 MP, 2732×2048 iPad Pro ≈ 5.6 MP)
```

Scene wiring (`Assets/Scenes/DaggerfallUnityGame.unity`): the `RetroPresentation` GameObject carries
a `Camera` with **`m_CullingMask: 0`** (renders nothing), `m_Depth: 0`, `m_ClearFlags: 3`,
`m_HDR: 0`, `m_AllowMSAA: 0`, plus `RetroPresentation` and a `ViewportChanger` with
`isRetroPresenter: 1` and `retroClearerCamera` pointing at a depth **−1** `RetroClearer` camera
(which exists solely to paint the pillarbox bars black when aspect correction is on).
`Camera.main` in `Assets/Prefabs/Player/PlayerAdvanced.prefab` is also `m_Depth: 0`,
`m_ClearFlags: 3`, `m_HDR: 0`, `m_AllowMSAA: 0`.

Three things follow, and each is load-bearing:

1. **`RetroPresentation.OnRenderImage` is exactly where a CRT filter goes.** It already blits
   through `Graphics.Blit`; adding a material is a one-argument change. It runs at **native
   backbuffer resolution**, which is what scanlines and a phosphor mask need — a CRT applied to the
   640×400 intermediate would alias into mush. And it respects `camera.rect`, so aspect correction
   and the docked large HUD keep working.
2. **The port's `Camera.main.targetTexture` handling composes cleanly with this.** `UpdateRenderTarget`
   (`RetroRenderer.cs:415-449`) is the only writer of `MainCamera.targetTexture`, and the CRT would
   sit two stages downstream of it. The known interaction from
   `2026-09-09-distant-terrain-wod.md` §7 item 4 — Distant Terrain's `SetUpCameras()` copies
   `Camera.main.targetTexture` to its `stackedCamera` (depth 2, vs main's 3) and re-runs on
   `RetroRenderingMode` change — happens **upstream of the presentation blit**, so a CRT at the hook
   above is indifferent to it. That is the answer to the brief's question: **no conflict, by
   construction**, because the CRT never touches a camera target.
3. **The UI will not be filtered, and that is a decision to make deliberately.** `DaggerfallUI` draws
   in `OnGUI` (`DaggerfallUI.cs:441`, `GUI.depth = 0`) — IMGUI, at screen resolution, after every
   camera. A CRT at the presentation blit therefore curves and scanlines the *world* and leaves the
   HUD, menus and paper doll pin-sharp and flat. Purists will want the whole screen curved; that
   needs a second full-screen pass after the UI (a `UserInterfaceRenderTarget`-based route, or a
   depth-1000 camera with the effect), which is materially more work and would also curve the touch
   controls away from where fingers land. **Recommendation: filter the world only.** On a touch port
   that is the correct answer for input reasons, not just cost.

**And when retro mode is off?** There is no presenter — `RetroPresentation.OnRenderImage` early-outs
on `RetroRenderingMode != 0` and `UpdateRenderTarget` sets `MainCamera.targetTexture = null`. A CRT
with retro off would need its own component on `Camera.main`, which collides with the large-HUD
`camera.rect` path (`ViewportChanger.SetViewport`) — Unity image effects and non-full viewport rects
are a known bad combination. **Scope the CRT to retro mode only.** That is also where it belongs
aesthetically, and it keeps the change inside one 31-line file.

**PPv2 is present, is fully extensible, and is still the wrong hook.** `Packages/manifest.json`
carries `com.unity.postprocessing: 3.5.4` (upstream is on 3.1.1 — we are ahead), and DFU drives a
`PostProcessLayer` on `Camera.main` (`StartGameBehaviour.DeployCoreGameEffectSettings`,
`WeatherManager.cs:133`) for AA / AO / bloom / vignette / DoF / dither. There is even a worked
in-tree example of a custom effect — `Assets/Scripts/Internal/ColorBoost.cs`, DFU's own, MIT:

```csharp
[PostProcess(typeof(ColorBoostRenderer), PostProcessEvent.BeforeStack, "Daggerfall/PostProcess/ColorBoost")]
…
var sheet = context.propertySheets.Get(Shader.Find("Daggerfall/PostProcess/ColorBoost"));
context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
```

so PPv2 here is `CommandBuffer`-based (`BlitFullscreenTriangle`), not `OnRenderImage` — and in fact
`grep -rn OnRenderImage` over `Assets/Scripts` returns **exactly one hit**,
`RetroPresentation.cs:23`, and `AddCommandBuffer` returns **none**.

But a custom `PostProcessEffectRenderer` would run **on the main camera, i.e. inside the 320×200
render texture**, before the presentation upscale — scanlines drawn there would be upscaled into
mush. Also, PPv2 needs the `[PostProcess]`-attributed type discovered at init, which is fragile
under IL2CPP. **Use the presenter blit, and patch `RetroPresentation.cs` directly** — the whole file
is MIT and 31 lines; there is no reason to route this through the mod system at all.

## B3. Retro Frame (DunnyOfPenwick) — adjacent, not a CRT, and it fights for the same chain

`github.com/DunnyOfPenwick/Retro-Frame` @ **`01fe089e6f1f8520c1b569602b26ac8330b3239e`**
("Update README.md", **2024-04-22**). MOD-MASTER-LIST rows 365 and 471.
Manifest: `ModTitle` "Retro-Frame", `ModVersion` **1.1.1**, `DFUnity_Version` **1.0.0**,
`GUID` `3be30a41-be01-4577-89be-5bfb0ba94088`, description *"Adds a Frame-HUD with hotkeys when in
retro 4:3 aspect mode."*
**Licence: MIT** (`LICENSE`, "Copyright (c) 2024 DunnyOfPenwick").
**Size: 1.8 MB total** — `Scripts/` 196 KB across **6 `.cs`** (`OverlayPanel.cs` 86,952 B,
`Hotkeys.cs` 45,022 B, `RetroFrameMod.cs` 17,816 B, `ModSave.cs`, `HotkeyPopupWindow.cs`,
`Text.cs`), `Textures/` 780 KB, `Sound/` 12 KB.

Nexus mod **586**, created 2024-01-27. **Dormant ~2.4 years** (last code commit `05e8a7ac`, same day
as the README touch). README states *"I build with the pre-compile option"* — it **ships a
precompiled assembly**, which is an IL2CPP concern for a drop-in but irrelevant to us, since we would
port the source like every other mod in `Ports/`.

**How it renders.** It is a **decorative bezel plus a hotkey bar, drawn in IMGUI** — `OverlayPanel`
is a DFU `Panel` painted from the mod's own `OnGUI` with `GUI.depth = GameManager.IsGamePaused ? 1 : 0`
(`RetroFrameMod.cs:420-425`), and it temporarily overrides `DaggerfallUI.Instance.CustomScreenRect`
to the full screen while it lays itself out (`:385-386`, `:428-429`). **No shader, no `Material(...)`,
no `Graphics.Blit`, no `OnRenderImage`, and it never touches `camera.rect`** — verified by grep over
all six `.cs` files; the only camera reference in 87 KB of `OverlayPanel.cs` is
`MainCamera.transform.eulerAngles.y`, for the compass. Textures are 780 KB across 24 PNGs, largest
`FrameHi.png` 416 KB.

**`IsRetroMode()` gates everything, and its condition is narrow:** `RetroRenderingMode != 0` **AND**
`RetroModeAspectCorrection == FourThree`. Outside that the overlay is inert and it restores DFU's
own `ShowCompass` / `ShowVitals` / `ShowInteractionModeIcon` / `ShowActiveSpells` flags. So Retro
Frame is only ever visible in the one aspect mode — worth knowing before promising it on a
19.5:9 phone.

**The part that matters for us.** `Swap640Mode()` (`RetroFrameMod.cs:159-185`) reaches into the
retro chain and **replaces the engine's render textures with its own**:

```csharp
retroRenderer.RetroTexture640x400      = rt;      // its "RetroTarget720x540" or "RetroTarget1440x1080"
retroRenderer.RetroTexture640x400_HUD  = rt_hud;
retroRenderer.RetroPresentationTarget  = presentationTarget;
GameManager.Instance.RetroPresenter.RetroPresentationSource = retroRenderer.RetroPresentationTarget;
```

(the `RenderTexture` assets are shipped inside its own bundle; the setting is
`Mode640x400Replacement`: `640x400` / `720x540` / `1440x1080`.)

So it (a) re-resolutions the 640×400 mode to a **native 4:3** 720×540 or 1440×1080, and (b)
re-points `RetroPresentationSource` — the exact field our CRT blit would read. **That is the only
collision, and it is avoidable by construction.** If our CRT *reassigns* those two fields, we and
Retro Frame fight over them and load order decides. If instead the CRT is the **material argument of
the existing blit** (or a component *after* `RetroPresentation` on a higher-depth camera), it reads
whatever source is assigned and composes cleanly — and a 4:3 source is *better* for a CRT than
640×400 (16:10). Two consequences if both ship: the scanline count must come from the source
texture's height, not a hardcoded 200/400; and the bezel art, drawn in screen space over the
presented image, will stay crisp and flat and therefore **will not line up with a curved viewport
edge** — so either keep curvature low, or mask the frame's inner edge.

It does nothing about scanlines, curvature or phosphor, so it does not answer the ask on its own.
It is genuinely complementary — bezel plus curved scanlined image is the full arcade look — and it
is the natural occupant of the pillarbox area `RetroModeAspectCorrection` creates on a 19.5:9 phone.
**Not assessed in depth here**: it is a separate, small, MIT port and deserves its own pass, whose
first question is how 167 KB of `OverlayPanel` + `Hotkeys` IMGUI lands on our
`ViewportChanger` / `CustomScreenRect` / safe-area / touch-control layout, all of which it touches.

## B4. Nexus / GitHub CRT mods for DFU — **there are none, and this was checked exhaustively**

Searched `MOD-MASTER-LIST.md` and the five backing reports for
`CRT`, `scanline`, `retro`, `posteriz`, `palettiz`, `pixelate`, `shader`. In 270 assessed rows the
only `retro` hits are **Retro Frame** (365) and **Retro Frame Custom** (471).

Then the whole of Nexus, properly: the **Nexus GraphQL v2 API** (`api.nexusmods.com/v2/graphql`)
answers unauthenticated even though the web pages are Cloudflare-403, so all
**1,020 mods** under `gameDomainName: daggerfallunity` (`gameId: 2927`) were enumerated in 21 pages
and matched on name + summary against
`crt|scanline|phosphor|curvat|grille|shader|post.?process|bezel|vhs|cathode|dosbox|palettiz|posteriz`.
**Exactly one CRT hit in the entire game, and it is not a mod:**

| Mod | Author | Date | What it is |
|---|---|---|---|
| **DOMINION CRT RESHADE** (Nexus 894) | BSoD / Rex_ink | 2024-12-18, v1.0, **5 KB**, 239 downloads | a **ReShade `.ini` preset** — *"A simple ReShade preset made to mimic that of a (albeit kinda cliché) CRT!"* |

Siblings from the same author: `DOMINION RESHADE` (892), `DOMINION JPEG RESHADE` (895),
`DOMINION PIXEL RESHADE` (896, *"a pixel effect that's a higher resolution than the one provided in
the game options"*). Per-title Nexus searches for `scanline`, `scanlines`, `pixelation`, `shader`,
`post process`, `postprocess`, `retro shader`, `VGA`, `dosbox`, `phosphor`, `royale`, `curvature`
all returned **0**; description-field searches for `scanline`, `aperture grille`, `phosphor`,
`curvature`, `OnRenderImage` also **0**. GitHub repo search for `daggerfall+unity+crt`,
`daggerfall+unity+scanline`, `daggerfall+shader+mod`, `dfmod+shader` → **0** each (control queries
returned 84 and 270, so the API was live). The two DFU forum threads that exist —
`forums.dfworkshop.net/viewtopic.php?t=5085` "CRT Reshade" and `t=5893` "CRT Filter" — are both
**ReShade + the DREAM preset with crt-lottes added**, and there is a still-unfilled
`forums.nexusmods.com/topic/9408768` *request* for a scanline/CRT shader.

**Conclusion, stated plainly: no CRT, scanline, curvature or phosphor mod exists for Daggerfall
Unity in any form.** Every community CRT solution is an external Windows D3D injector (ReShade),
which is structurally impossible on iOS.

This is not a gap in the research — it is structural. DFU's modding docs
(`dfworkshop.net/projects/daggerfall-unity/modding/{,features,textures,additional-resources}`)
contain **no mention of post-processing or custom screen effects**; the only shader-adjacent
statement is that a bundle *"can contain any kind of asset that derives from UnityEngine.Object,
including textures, meshes, sounds, shaders etc."* A full-screen post-process needs a hook on the
camera stack that the mod API does not offer, which is exactly why upstream implemented retro
rendering *in the engine*. A CRT filter for DFU is an engine change by nature — and on this fork we
own the engine, and it is MIT.

Two adjacent finds worth keeping:
- **Nexus 1189 "Retro-Deframed"** (twinIndifferent, 2026-02-03) — *"a custom touchscreen layout for
  Vwing's DFU android port, made with love to imitate Retro-Frame"*. Direct precedent for a
  touch-port bezel; look at it before porting Retro Frame proper.
- **Nexus 252 "Pixelated Grass for Real Grass"** — a texture replacer for Real Grass. If we need
  grass art and want a lo-fi look that matches retro mode, this is a candidate source, **licence
  unverified** (the Nexus API does not expose permissions blocks; every Nexus permission claim in
  this document is therefore unverified and must be read on the page before use).

## B5. Writing our own — the recommendation, with effort

A single-pass mobile CRT under `Assets/Shaders/Mobile/MobileCRT.shader`, applied at the
`RetroPresentation` hook. Everything about this is favourable: it is our code, MIT like the rest of
the port, no permission to ask, no bundle to build, no `DFUnity_Version` drift, and it is the one
approach that is *cheaper* than porting a mod.

**Shape of it** (`#pragma target 3.0`, no keywords or at most one `multi_compile` for a
quality tier; 1 `tex2D` in the cheap tier, 3 in the mask tier):

- **Curvature** — barrel-distort the UV in the fragment shader (`uv = uv*2-1; uv *= 1 + k*dot(uv,uv); uv = uv*.5+.5`),
  discard/black outside. ~8 ALU. Keep `k` small; on a phone held close, aggressive curvature reads as
  a defect.
- **Scanlines** — `1 - amp * (0.5 - 0.5*cos(uv.y * lines * 2π))`, `lines` = 200 or 400 tied to
  `RetroRenderingMode` so the lines land on the *source* raster, not on device pixels (this is the
  detail that separates a good CRT filter from a moiré generator). ~6 ALU.
- **Phosphor / aperture grille** — modulate RGB by `frac(screenPos.x/3)` bands. ~5 ALU, no texture.
  Cheaper and more stable on a high-DPI panel than a mask texture, which will alias.
- **Vignette** — `1 - v*dot(c,c)` on the centred UV. ~4 ALU.
- **Optional halation** — a 2-tap horizontal blur added back at low weight; this is the only part
  that costs texture bandwidth, so put it behind the quality tier and default it off on phones.

**Cost.** The cheap tier is **1 texture fetch and ~25–30 ALU per pixel** at native resolution:
3.0 MP on an iPhone 15 Pro, 5.6 MP on a 12.9″ iPad Pro. That is one dependent-read-free full-screen
pass — the sort of thing an A17 does in well under a millisecond, and an A12-class iPad in one to two.
It is unambiguously cheaper than the grass work. Memory cost: **zero new render textures** (it
replaces the existing blit's material), zero new textures if the grille is procedural.

**Effort.** The shader is ~80 lines. The C# is: one `Material` field and one `Graphics.Blit`
argument in `RetroPresentation.cs` (31 lines today); a `MobileShaders.Find` registration so a mod
bundle cannot shadow it (the existing `MobileShaders` pattern, `MobileShaders.cs:20-31`); four
settings keys (`CRTEnable`, `CRTScanlines`, `CRTCurvature`, `CRTMask`) in `SettingsManager` +
`defaults.ini.txt`; and either a fourth page in `GameEffectsConfigWindow` or three rows in
`MobileSettingsPanel`. Plus an entry in `RequiredShaderVariants`. **Estimate: half a day to a first
on-device look, one to two days to ship with settings, a settings page and the variant plumbing.**

**Reference implementations, with licences actually checked.** One assumption worth correcting up
front: **`libretro/glsl-shaders`, `libretro/slang-shaders` and `libretro/common-shaders` have no
repo-level LICENSE file at all** (`LICENSE`/`LICENSE.md`/`LICENSE.txt`/`COPYING` all 404 on master).
Licensing there is strictly per-file and genuinely mixed — a public-domain file sits in the same
directory as a GPL one — so reading `crt-lottes.glsl` out of a libretro checkout does *not* pull GPL
onto us. The only `LICENSE.TXT` anywhere in those trees is scoped to `crt/shaders/crt-royale/`.

| Shader | Licence | Passes | **Tex samples/px** | Features |
|---|---|---|---|---|
| **crt-lottes** (Timothy Lottes) | **public domain** (informal, see below) | 1 | **42** default / **11** with `DO_BLOOM` off | scanlines, 4 mask types, barrel warp, bloom, sRGB gamma; no vignette |
| crt-geom (cgwg / Themaister) | **GPL-2.0-or-later** — and its own header says the barrel code *"was taken from the Curvature shader, which is under the GPL"* | 1 | 8 | curvature, scanlines, dot mask, gamma |
| crt-easymode | "License: GPL" (no version) | 1 | 8 (4 without Lanczos) | scanlines, grille/shadow mask, gamma; flat |
| **crt-pi** (davej) | **GPL-2.0-or-later** | 1 | **1** | scanlines, ALU mask, fake gamma; curvature off by default |
| zfast_crt (Greg Hogan) | GPL-2.0-or-later | 1 | 1 | same architecture as crt-pi |
| crt-royale (TroggleMonkey — *not* Lottes) | GPL-2.0 | ~5 | many | everything, with mask textures |
| **crtemu.h** (Mattias Gustavsson) | **MIT ∥ Unlicense**, dual, "Copyright (c) 2016 Mattias Gustavsson" | multi | 7 final + blur chain | curvature, **vignette**, scanlines, stripe mask, RGB bleed, ghosting, bezel |
| **JoshuaJRideout/godot-crt-shader** | **MIT**, "(c) 2025 Joshua J Rideout", no derivation claim | 1 | 3 | curvature, scanlines, vignette |
| hiulit `Godot-3-2D-CRT-Shader` | MIT "(c) 2019 Xavier Gómez Gosálbez" — **⚠ but** its README credits a Shadertoy original whose default licence is **CC BY-NC-SA 3.0** (upstream unverified, Shadertoy 403s) | 1 | 1 | curvature + scanlines + vignette |

crt-lottes' entire licence statement, byte-identical across five independent copies (libretro glsl,
libretro slang, libretro common-shaders, `hizzlekizzle/quark-shaders`, and — notably — GPL-licensed
DuckStation, which kept the bare header):

> // PUBLIC DOMAIN CRT STYLED SCAN-LINE SHADER
> //   by Timothy Lottes
> … // Please take and use, change, or whatever.

Informal public domain, not CC0, no warranty disclaimer. **Read it for theory, don't ship it**: all
42 samples are dependent and `floor()`-snapped to texel centres, so hardware bilinear cannot help.

**The awkward part is that the shader whose techniques we most want — crt-pi — is GPL.** Its
cheapness, though, comes from *omissions and prose-documented choices*, which are not copyrightable
and which davej states in comments: curvature `#define`d out (*"Curvature slows things down a lot"*),
`filter_linear0 = "true"` so the **hardware bilinear unit** does the horizontal filtering for free,
`MULTISAMPLE` evaluating the *analytic* scanline function three times (zero extra texture reads) to
kill moiré, mask from `fract(gl_FragCoord.x * 0.5)` with no lookup texture, and `FAKE_GAMMA`
swapping `pow()` for `c*c` / `sqrt(c)`.

**Recommended safe path:** take crt-pi's *architecture* from that prose (1 sample + hardware bilinear
+ ALU mask + analytic scanline multisampling + `x*x`/`sqrt` gamma); take curvature and vignette
*code* from **`crtemu.h`** (MIT ∥ Unlicense — cite `mattiasgustavsson/libs/crtemu.h`, **not**
`mattiasgustavsson/crtview`, which has no LICENSE at all, and not his `doom-crt`/`dosbox-crt`/`steem-crt`,
which are GPL-2.0/3.0 because their host emulators are); take mask and scanline weighting from
crt-lottes (public domain), simplified from 42 samples to 1; and do **not** copy Lottes' `floor()`
texel snapping, which is precisely what would blow the one-sample budget. Declare the mask and
scanline math `half` for Metal.

If a ready-made Unity reference is wanted instead of writing from primitives, the best-matching
permissive option found is **`Luci0n/CrowFX-Unity-Image-Effects`** — MIT, pushed 2026-09-07,
*"Free CRT, VHS, film, glitch, dithering, and retro post-processing for Unity **Built-in**, URP…"* —
built-in pipeline being exactly ours. Others: `yunoda-3DCG/Simple-CRT-Shader` (MIT),
`jaroslavstehlik/RetroLooks` (MIT), `avvie/CRT-Scanlice-Interlacing` (MIT),
`XJINE/Unity_CRTEffect` (BSD-3-Clause), `Cyanilux/URP_RetroCRTShader` (MIT, URP-only so not useful).
**Their licences were confirmed via the GitHub API; their sample counts and mobile suitability were
not read and are unverified.**

The honest bottom line on "is a generic approach simpler and licence-free": **yes, on both counts,
and by a wide margin.** Barrel distortion, a cosine scanline, an RGB stripe and a radial falloff are
textbook operations with no authorship to inherit; write them, head the file MIT like the rest of
the port, and the question never arises.

## B6. CRT recommendation

**Do not port anything. Do three things, in this order:**

1. **Ship what exists, and make it findable.** Verify on device that pause → options →
   Game Effects → Retro Mode works under the touch UI, then surface `RetroRenderingMode`,
   `PostProcessingInRetroMode` and `RetroModeAspectCorrection` as three rows in
   `MobileSettingsPanel`. Zero new rendering code; delivers 320×200 + the true 259-entry VGA palette
   + the 4:3 Mode 13h stretch. **And clamp the two unclamped keys**: `PalettizationLUTShift` and
   `PostProcessingInRetroMode` take any integer from the ini. Shift 0 is a **64 MB `Texture3D` and a
   ~7 s build**, per the code's own comment table; shift 1 (the shipped default) is 8 MB and
   ~850 ms on desktop, worse on an A-series; **shift 2 is 1 MB and visually "slightly less crisp"** —
   that is the right iOS default. Build the LUT off the first frame either way.
2. **Write `Assets/Shaders/Mobile/MobileCRT.shader`** (scanlines + curvature + procedural RGB
   grille + vignette, `#pragma target 3.0`, one `tex2D`) and apply it as the material argument of the
   single `Graphics.Blit` in `RetroPresentation.OnRenderImage`. World only, retro mode only.
   Half a day to first light.
3. **Only then** consider Retro Frame (MIT, 1.8 MB) for the bezel, as its own port — after checking
   how its `OverlayPanel` lands on `ViewportChanger` / `CustomScreenRect` / safe-area layout, and
   noting that it re-points `RetroPresentationSource`, so if both ship the CRT's scanline count must
   be derived from the source texture height rather than hardcoded.

Top three risks for the CRT work: (1) **moiré** — scanline frequency must be derived from
`RetroRenderingMode` (200/400 lines) and not from device pixels, or high-DPI panels shimmer;
(2) **the UI stays unfiltered**, which is deliberate and input-correct but will read as a bug to
someone, so say so in the setting's tip text; (3) **fill rate on the largest iPads** — 5.6 MP × ~30
ALU is fine but not free, so ship a quality tier and default halation off. None of these is a
feasibility risk. **Verdict: EASY.**

---

## C. Summary table

| Candidate | Licence (exact) | Mechanism | Shaders | Data | iOS | Key risk |
|---|---|---|---|---|---|---|
| **Real Grass** code | **MIT**, "(c) 2016-2019 Uncanny_Valley, TheLacus" | Unity terrain details via `OnPromoteTerrainData`; `SetDetailResolution(256,8)` + up to 5 `SetDetailLayer` | **none of its own**; relies on stock `Hidden/TerrainEngine/*` + Standard | 83 KB C# | **Medium** | ~1.25 MB `int[256,256]` churn per terrain promotion |
| **Real Grass** art | **split** — VMblast's `Grass_tex` / `GrassDetails_*` / `DesertGrass` are *"authorized for this project only"*; everything else CC0 (rubberduck, yughues) | — | 12 `.mat` all Unity **Standard** | 28 MB total; but the **`Style=Classic` path needs only 64 KB of CC0 texture** | **Medium** | judgement call is **avoidable** — Classic style never loads VMblast's files |
| **Wilderness Overhaul** | **NONE DECLARED** | `ITerrainNature` → `dfBillboardBatch.AddItem`; also takes `ITerrainTexturing` + `ITerrainMaterialProvider` | 1, `target 3.5` surface, 8 variants — Metal-safe | 476 KB C# + 14 MB texarrays | **Hard / blocked** | no licence; 3-slot takeover displaces Basic Roads + WoD Biomes |
| **Vibrant Wind** | **MIT**, "(c) 2017 TheLacus" | 3 `TerrainData.wavingGrass*` writes + `WindZone` | none | 20 KB C# | **Easy** | no-op without a grass mod |
| **DFU built-in retro** | **MIT** (Daggerfall Workshop) | `Camera.main.targetTexture` → posterize/palettize blit → presenter blit | 3, ≤`target 3.0`, 2 variants each; **no scanline/curvature/phosphor anywhere, verified upstream too** | 8 MB LUT at shift 1; 1 MB at shift 2 | **already shipping and reachable on iOS** | `PalettizationLUTShift` is **unclamped** — 0 ⇒ 64 MB + ~7 s |
| **Any DFU CRT mod** | — | — | — | — | **does not exist** | all 1,020 Nexus mods enumerated; the one CRT hit is a 5 KB ReShade preset |
| **Retro Frame** | **MIT**, "(c) 2024 DunnyOfPenwick" | IMGUI bezel + hotkey bar; **swaps the retro RTs** to 720×540 / 1440×1080 and re-points `RetroPresentationSource` | **none** | 196 KB C# (6 `.cs`) + 780 KB PNG, 1.8 MB total | Small (own pass) | overrides `CustomScreenRect`; must be ordered with the CRT |
| **Our own `MobileCRT`** | **MIT (ours)** | material on the existing `RetroPresentation` blit, at native res | 1, `target 3.0`, 1 `tex2D` | none | **Easy** | scanline moiré if frequency is tied to device pixels |

## D. What is verified, and what is not

**Verified by reading source** (working clones and this repo): every licence string, commit hash,
file size, entry point, hook, shader `#pragma`/keyword/sample count, settings default and render-path
claim in this document. The absence of scanline/curvature/phosphor code was verified by grep both
here and against upstream master.

**Not verified, and flagged where it matters:**
- **Every Nexus permissions grid in this document.** nexusmods.com returns HTTP 403 to WebFetch and
  to curl with a browser user-agent; forums.dfworkshop.net is behind the same Cloudflare wall. The
  Real Grass mod-20 grid quoted in §A1 is **relayed second-hand from the unauthenticated GraphQL
  endpoint**, not read; so are the permission claims for DOMINION CRT RESHADE, Pixelated Grass,
  Simple 3d Trees, Invisible Trees, Weeberfall, Seasons of the Iliac Bay, Better Terrain Loading and
  DREAM. Retrieving any of them properly needs a browser or a Nexus API key. The repo
  `LICENSE`/`credits.txt`/`Changelog.txt` files quoted above *are* verified.
- **The two community performance reports** in §A1 (Distant Terrain memory growth; Interesting
  Terrains cell-crossing frame rate) are **search-snippet-sourced only** — the forum pages 403.
  They are consistent with the code, which is why they are in the document, but they are hearsay.
- **Whether the iOS player force-includes the terrain detail shaders** could not be settled
  statically. Their absence from `libiPhone-lib.a` and the iOS default resources *is* verified;
  the consequence is not.
- **The provenance of `BrownGrass_tex.png` / `GreenGrass_tex.png`** is inferred from the absence of a
  VMblast claim in `credits.txt` (and a changelog line in TheLacus's voice), not positively
  established.
- **Seasons of the Iliac Bay (Nexus 1377)** may ship C# — its author calls it "script-injected", it
  has no repo, and Nexus does not index the archive. Contents unknown.
- **GitHub code search** needs authentication, so "no CRT mod on GitHub" rests on repo-name search
  plus the Nexus sweep, not on a code-level sweep.
- **Real Grass's on-device cost.** Nothing here is a measurement. The allocation and memory figures
  are computed from the code (`int[256,256]` per layer, 49 terrains at `TerrainDistance=3`); the GPU
  cost of the detail pass is genuinely unknown until someone runs it on a device.
- **Retro Frame** was inspected but not assessed for a port (§B3).
- The **Unity MIT CRT references** in §B5 had licences confirmed via API but their shaders were not
  read; sample counts and mobile suitability are unverified.

## E. Files to touch (for whoever implements)

Grass: `Assets/Scripts/Game/Mobile/Ports/RealGrass/` (new),
`Assets/Scripts/Game/Mobile/MobilePortedMods.cs`, `MobileMods.cs`,
`ProjectSettings/GraphicsSettings.asset` (always-included terrain detail shaders),
`Assets/Shaders/RequiredShaderVariants.shadervariants`, `tools/bundled-mods/mods.json`,
`THIRD-PARTY.md`.

CRT: `Assets/Shaders/Mobile/MobileCRT.shader` (new),
`Assets/Scripts/Utility/RetroPresentation.cs` (one `Graphics.Blit` argument),
`Assets/Scripts/Game/Mobile/MobileShaders.cs` (register the name),
`Assets/Scripts/SettingsManager.cs` + `Assets/Resources/defaults.ini.txt` (four keys),
`Assets/Scripts/Game/Mobile/MobileSettingsPanel.cs` **or**
`Assets/Scripts/Game/UserInterfaceWindows/GameEffectsConfigWindow.cs` (a page),
`Assets/Shaders/RequiredShaderVariants.shadervariants`.
