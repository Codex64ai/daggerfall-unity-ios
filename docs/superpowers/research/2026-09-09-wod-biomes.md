# World of Daggerfall – Biomes: iOS port research

Date: 2026-09-09. Read-only research, no code changed, nothing committed.
Target: **World of Daggerfall - Biomes**, `github.com/drcarademono/wod-biomes` @ **`40449fc5fc55c85c8089b068acefe1db4b61534c`**
("Make textures readable", 2025-06-02) — tip of `main`, the only branch, 9 commits total.
Working clone: `/Users/ikrammassabini/.claude/jobs/e1476d2f/tmp/research-biomes/wod-biomes`.

Manifest: `World of Daggerfall - Biomes.dfmod.json` — `ModTitle` "World of Daggerfall - Biomes",
`ModVersion` 0.4.0, `ModAuthor` "carademono and Kab the Bird Ranger",
`GUID` **`3b4319ac-34bb-411d-aa2c-d52b7b9eb69d`**, `DFUnity_Version` **1.0.0**,
`ModDescription` "Diversifies biomes across the Iliac Bay".

**Forks checked.** `AsesinoBlade/wod-biomes` and `SquidKamer/wod-biomes` are the only two, and
`gh api .../compare` reports both **`status=identical, ahead=0, behind=0`** — no fork carries fixes.
`somestupidgirl` has **no** wod-biomes fork; of that account's 24 repos the only Daggerfall one is
`Distant-Terrain-of-the-World-of-Daggerfall` (a different mod, already covered in MOD-MASTER-LIST row 556).

---

## 1. What it does functionally

Three separate mechanisms, all keyed off Daggerfall's world-climate index. **No tree or grass
*placement* at all** — placement stays DFU's (`ITerrainNature`, which Location Loader takes). Biomes
only changes *which textures* the ground and the nature flats use.

### 1a. Terrain tileset swaps by climate — the main feature (`WODTerrainMaterialProvider.cs`)

It takes DFU's **`ITerrainMaterialProvider`** slot (the fourth pluggable terrain slot; `ITerrainSampler`,
`ITerrainTexturing` and `ITerrainNature` are the other three) and overrides one protected virtual,
`GetClimateInfo(int worldClimate)`, to re-route four climates to different ground archives:

| Climate | Vanilla ground archive (`MapsFile.GetWorldClimateSettings`) | Biomes routes to | Biomes ships PNGs for it? |
|---|---|---|---|
| `Subtropical` (229) | 2 (Desert base type, so no winter bump) | **4**, summer and winter alike | yes, `Textures/Terrain/Subtropical/004_*` (56) |
| `Desert2` (225, Dak'fron) | 2 | **3**, summer and winter alike | yes, `Textures/Terrain/Deep Desert/003_*` (56) |
| `Mountain` (226) **only in 7 Hammerfell regions** | 102 / 103 winter | **104** summer, **103** winter | yes, `Textures/Terrain/HF Mountain/104_*` (56) |
| `HauntedWoodlands` (232) | 302 / 303 winter | **304** summer, **303** winter | yes, `Textures/Terrain/Haunted Woodland/304_*` (56) |

The Hammerfell test is a **string compare against `GameManager.Instance.PlayerGPS.CurrentRegionName`**:

```csharp
case (int)Climates.Mountain:
    string[] hammerfellRegions = new string[] { "Alik'r Desert", "Dragontail Mountains", "Dak'fron",
        "Lainlyn", "Tigonus", "Ephesus", "Santaki" };
    if (hammerfellRegions.Contains(GameManager.Instance.PlayerGPS.CurrentRegionName))
        groundArchive = isWinter ? 103 : 104;
```

It also **un-does DFU's winter bump for Rainforest** (the `worldClimate != (int)Climates.Rainforest`
term is commented out, so rainforest *does* get winter now — commit `4f9dd70` "Let rainforest have
winter for now").

**Verified: archives 4, 104 and 304 all exist in vanilla Arena2.** I checked
`/Users/ikrammassabini/dev/daggerfall-data/arena2`: `TEXTURE.004`, `TEXTURE.104`, `TEXTURE.304` are
all present at 260,762 bytes, the same size as the eight known terrain tilesets (002/003/102/103/302/303/402/403).
This matters because `TextureReader.GetTerrainTextureArray` opens the vanilla file first and
**`return null`s unless `RecordCount == 56`**, and `MaterialReader.GetTerrainTextureArrayMaterial`
would then NRE on `textureArrayTerrainTiles.filterMode`. It does not, because the archives are real.
Biomes is therefore a *reskin of three unused vanilla tilesets plus one repurposed one*, not an
invention of new archive numbers — which is exactly why it needs no engine change.

Both provider variants subclass a shared abstract base and mirror DFU's own two providers 1:1:

```csharp
private void Start()
{
    if(WODTilemapTextureArrayTerrainMaterialProvider.IsSupported)
        DaggerfallUnity.Instance.TerrainMaterialProvider = new WODTilemapTextureArrayTerrainMaterialProvider();
    else
        DaggerfallUnity.Instance.TerrainMaterialProvider = new WODTilemapTerrainMaterialProvider();
}
```

`IsSupported` is `SystemInfo.supports2DArrayTextures && DaggerfallUnity.Settings.EnableTextureArrays`,
byte-identical to `TilemapTextureArrayTerrainMaterialProvider.IsSupported` in
`Assets/Scripts/Terrain/TerrainMaterialProvider.cs:171`. **The fallback path already exists**, so a
device without texture arrays degrades to the tilemap-atlas provider on its own.

### 1b. Nature-flat archive swap by colour key (`WODClimates.cs`, `NatureBatchOverrider`)

Where `Assets/Maps/climate_map.png` is exactly **`#FFA500`** (orange), subtropical nature billboards
are swapped from vanilla archive **501** (`Nature_SubTropical`) to archive **10030** and scaled 2×:

```csharp
void OnEnable()  => StreamingWorld.OnUpdateTerrainsEnd += ApplyOverrides;
...
foreach (var batch in FindObjectsOfType<DaggerfallBillboardBatch>())
{
    if (batch.TextureArchive != 501) continue;
    ...
    int ty = mapH - 1 - my;
    Color32 c = NatureBatchOverriderInstaller.climateMap.GetPixel(mx, ty);
    if (c.r == 255 && c.g == 165 && c.b == 0)
    {
        CustomBillboardHelper.RevisedSetMaterial(batch, NEW_ARCHIVE, true);   // NEW_ARCHIVE = 10030
        batch.Apply();
    }
}
```

**Archive 10030 is not in this mod.** It is Daggerfall Expanded Textures': I found it at
`Assets/Game/Mods/Converted/daggerfall expanded textures/Assets/Game/Mods/Daggerfall_expanded_textures/Climates/Makija 10030/`
— **32 records** (`10030_0-0.png` … ). That is the whole reason DET is the one non-optional dependency.

`CustomBillboardHelper.RevisedSetMaterial` exists because archive 10030 is **> 511**, outside
Daggerfall's archive space, so `DaggerfallBillboardBatch.SetMaterial` →
`MaterialReader.GetCachedMaterialAtlas` cannot build it. The helper re-implements the atlas build and
then writes the result back into the batch **by reflection** (see §2).

### 1c. `BiomesClimateSwap` — NOT in this repo

Our ported Location Loader type-5 branch references two symbols that live in **carademono's Location
Loader fork, not in wod-biomes**: `github.com/drcarademono/DFU-LocationLoader` branch `rmb-object`
@ `896a5741e5c9badd47fbb6c0f9926a95a79cb776`. There:

- `Scripts/LocationModLoader.cs:17` `public static bool WODBiomesModEnabled;`
  and `:60` `WODBiomesMod = ModManager.Instance.GetModFromGUID("3b4319ac-34bb-411d-aa2c-d52b7b9eb69d");`
- `Scripts/LocationLoader.cs:362`
  `if (LocationModLoader.WODBiomesModEnabled) BiomesClimateSwap.ApplySwaps(rmbBlock);`
- `Scripts/BiomesClimateSwap.cs` — 101 lines, `LocationLoader` namespace, same `#FFA500` → archive
  10030 rule as §1b but applied to the loose `Billboard` components of a freshly built **type-5 RMB
  block** rather than to batched terrain nature, with a `pendingBlocks` retry list drained on
  `StreamingWorld.OnUpdateTerrainsEnd`. It reads `LocationModLoader.climate_map`, i.e. **Location
  Loader's own copy of the same asset** (`mod.GetAsset<Texture2D>("climate_map")` in that fork's
  `Init`), not the Biomes mod's. It guards `if (climate_map == null || !climate_map.isReadable)`.

Our port deliberately omitted this (spec item 6, `THIRD-PARTY.md`, `UPSTREAM-PATCHES.md:321`:
"WOD-Biomes call begins: no `BiomesClimateSwap`"). **Consequence for a Biomes port: nature flats on
WoD's 32,600 type-5 RMB blocks would NOT get the archive-10030 swap even with Biomes on**, because the
call site is in Location Loader, not in Biomes. Restoring it is ~110 lines across the same three
already-backported files plus one new file — but note the upstream `climate_map` lookup it depends on
is on the LL side, and our LL port has no bundle and so no `GetAsset` to load it from. That is a real
design item, not a copy-paste.

Separately, `WODRocksMaterials.cs` (already compiled in) probes the Biomes GUID and changes rock
materials in Hammerfell accordingly —
`Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfall/WODRocksMaterials.cs`:

```csharp
Mod worldOfDaggerfallBiomesMod = ModManager.Instance.GetModFromGUID("3b4319ac-34bb-411d-aa2c-d52b7b9eb69d");
WorldOfDaggerfallBiomesModEnabled = worldOfDaggerfallBiomesMod != null && worldOfDaggerfallBiomesMod.Enabled;
```

used at `:277` / `:280` to pick `climateMaterialSettings.mountainHammerfell` when Biomes is on and
`mountainBalfiera` when it is not. **This already works today** — with no Biomes bundle installed the
GUID lookup returns null and the Balfiera branch is taken, which is the correct no-Biomes behaviour.
Installing a Biomes bundle flips it with **zero code change on our side**.

---

## 2. Code inventory

**3 files, 559 lines total. No Editor-only files, no `Scripts/Editor/`, no `.asmdef`.**

| File | Lines | Contents |
|---|---|---|
| `Scripts/WODBiomes.cs` | 43 | `MonoBehaviour`. `[Invoke(StateManager.StateTypes.Start, 0)] Init(InitParams)`; probes Vanilla Enhanced GUID `1f124f8c-dd01-48ad-a5b9-0b4a0e4702d2` → `VEModEnabled`; `Start()` installs the terrain material provider |
| `Scripts/WODClimates.cs` | 372 | `NatureBatchOverriderInstaller` (**second** `[Invoke(Start, 0)] Init`), `NatureBatchOverrider` MonoBehaviour, `static class CustomBillboardHelper` (the >511-archive atlas builder) |
| `Scripts/WODTerrainMaterialProvider.cs` | 144 | `abstract WODTerrainMaterialProvider : ITerrainMaterialProvider` + `WODTilemapTerrainMaterialProvider` + `WODTilemapTextureArrayTerrainMaterialProvider` |

All three are `namespace WorldOfDaggerfall`. All three are listed in the manifest's `Files`, so the
pack tooling's existing **`strip_code: true`** flag removes them from the bundle unchanged.

### `[Invoke]` entry points — two, both `Start, 0`

1. `WorldOfDaggerfall.WODBiomes.Init(InitParams)`
2. `WorldOfDaggerfall.NatureBatchOverriderInstaller.Init(InitParams)` — calls
   `mod.LoadAllAssetsFromBundle()` then `mod.GetAsset<Texture2D>("climate_map")`, and creates a
   `DontDestroyOnLoad` GameObject "NatureBatchOverrider"

Order between them is undefined by DFU (same state, same priority). `WODBiomes.VEModEnabled` is read
inside `CustomBillboardHelper.RevisedGetTextureResults` (line 317), so if the overrider runs first the
2× scaling could read a stale `false`. In a compiled-in port we control the order explicitly — call
`WODBiomes.Init` first — which removes a latent upstream race for free.

### DFU APIs and hooks used

| API | Where | Present in our 1.1.1 fork? |
|---|---|---|
| `ITerrainMaterialProvider` (`CreateMaterial`, `PromoteMaterial`) | `WODTerrainMaterialProvider` | yes, `Assets/Scripts/Terrain/TerrainMaterialProvider.cs:66-80`, signatures identical |
| `DaggerfallUnity.Instance.TerrainMaterialProvider` setter | `WODBiomes.Start` | yes, `Assets/Scripts/DaggerfallUnity.cs:202` |
| `StreamingWorld.OnUpdateTerrainsEnd` | `NatureBatchOverrider.OnEnable/OnDisable` | yes — the same event our LL port already hooks |
| `MapsFile.GetWorldClimateSettings`, `MapsFile.WorldMapTileDim` | both providers | yes |
| `MaterialReader.GetTerrainTilesetMaterial` / `GetTerrainTextureArrayMaterial` | `PromoteMaterial` | yes, `Assets/Scripts/MaterialReader.cs:678` / `:734` |
| `TileUniforms.*` / `TileTexArrUniforms.*` | `PromoteMaterial` | yes |
| `GameManager.Instance.PlayerGPS.CurrentRegionName` | Hammerfell test | yes |
| `DaggerfallUnity.Instance.WorldTime.Now.SeasonValue` | winter test | yes |
| `DaggerfallBillboardBatch` (`TextureArchive`, `Apply()`), `DaggerfallLocation.Summary.MapPixelX/Y`, `DaggerfallTerrain.MapPixelX/Y` | overrider | yes |
| `TextureReader.GetTexture2D(settings, SupportedAlphaTextureFormats, TextureImport)` | atlas builder | yes, `Assets/Scripts/Utility/TextureReader.cs:187` |
| `TextureReplacement.TryImportTexture(int,int,int,out Texture2D)` | `ProcessCustomTextures` | yes, `TextureReplacement.cs:203` |
| `TextureFile(path, FileUsage.UseMemory, true)`, `TextureFile.IndexToFileName` | atlas builder | yes |
| `CachedMaterial`, `GetTextureSettings`, `GetTextureResults`, `RecordIndex` | atlas builder | yes |
| `MaterialReader._DaggerfallTilemap*ShaderName`, `_DaggerfallBillboardBatch*ShaderName` | `Shader.Find` calls | yes |
| `ModManager.Instance.GetModFromGUID` | VE probe | yes |
| **`DaggerfallUnity.Instance.TerrainTexturing`** | — | **never touched.** No `ITerrainTexturing`, no `ITerrainSampler`, no `ITerrainNature`. **Basic Roads is safe** |

### Reflection — 3 private/public fields of `DaggerfallBillboardBatch`

```csharp
static CustomBillboardHelper()
{
    var bt = typeof(DaggerfallBillboardBatch);
    _currentArchiveField = bt.GetField("currentArchive", BindingFlags.Instance | BindingFlags.NonPublic);
    _cachedMaterialField = bt.GetField("cachedMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
    _textureArchiveField = bt.GetField("TextureArchive", BindingFlags.Instance | BindingFlags.Public);
}
```

All three exist in our fork with those exact names: `DaggerfallBillboardBatch.cs:73` `int currentArchive`,
`:42` `CachedMaterial cachedMaterial`, `:61` `public int TextureArchive`. All three are read/written by
the class itself, so IL2CPP field stripping will not remove them — but see §7.

### Threading, Jobs, Burst, file IO, `Resources.Load`

- **No Jobs, no Burst, no `NativeArray`, no threads, no coroutines, no `async`.** Everything runs on
  the main thread. (Contrast WoD Terrain, which is 4 `.compute` files and two synchronous
  `ComputeBuffer.GetData()` readbacks per map pixel.)
- **No `Resources.Load`.** Assets come from the mod bundle via `mod.GetAsset<Texture2D>`.
- **File IO: one site**, in `RevisedGetTextureResults` —
  `Path.Combine(DaggerfallUnity.Instance.Arena2Path, TextureFile.IndexToFileName(settings.archive))`
  guarded by `File.Exists`, then `new TextureFile(...)`. Reads the player's own Arena2, which on iOS is
  in the app sandbox and already used this way throughout DFU. Safe.
- `System.Linq` used once (`hammerfellRegions.Contains`).

---

## 3. Data inventory

Manifest `Files`: **229 entries, 1.64 MB on disk** (225 png + 1 json manifest + 3 cs).

| Path | Files | Bytes |
|---|---|---|
| `Textures/Terrain/Subtropical/` (`004_0-0` … `004_55-0`) | 56 | 0.75 MB |
| `Textures/Terrain/HF Mountain/` (`104_*`) | 56 | 0.50 MB |
| `Textures/Terrain/Haunted Woodland/` (`304_*`) | 56 | 0.17 MB |
| `Textures/Terrain/Deep Desert/` (`003_*`) | 56 | 0.16 MB |
| `Assets/Maps/climate_map.png` | 1 | 0.02 MB |
| `Scripts/*.cs` | 3 | 0.02 MB |
| manifest json | 1 | 0.02 MB |
| **total** | **229** | **1.64 MB** |

Manifest paths are all prefixed `Assets/Game/Mods/wod-biomes/`, so the repo root maps to that folder —
the ordinary layout `fetch.py` already expects. Zero missing files: every one of the 229 resolves.

**PNG geometry** (read from the IHDR chunks): **224 terrain tiles are all exactly 64×64, 8-bit** —
197 palette (colortype 3) and 27 RGBA (colortype 6). Power-of-two, so no NPOT concerns.
**`climate_map.png` is 1000×500, 8-bit RGBA (colortype 6), 19,635 bytes** — the same 1000×500 shape as
Daggerfall's world map pixel grid, which is what the `mapH - 1 - my` flip and the
`mx < mapW && my < mapH` bounds check are for.

**Import settings** (`.meta`): every one of the 225 has **`isReadable: 1`** (that is what the tip
commit `40449fc` "Make textures readable" did), `enableMipMap: 1`, `filterMode: 0` (**Point**),
`maxTextureSize: 2048`, `textureCompression: 2` on `DefaultTexturePlatform`/`Standalone`/`Android`,
**no `iPhone` platform override**, `nPOTScale: 1` on the tiles (harmless, they are POT) and `0` on
`climate_map`.

**Not in the manifest, must not ship**: `Textures/Terrain/WOD Biomes Terrain.7z` (1.29 MB, source art
archive) and `Textures/Terrain/Deep Desert/55-0.xcf` (19 KB, GIMP source). `fetch.py` copies manifest
files only, so both are excluded automatically; `exclude_globs` is available if belt-and-braces is wanted.

**No JSON or CSV data at all** beyond the manifest itself — no runtime-materials JSON, no
`climate_map` metadata, no `WorldData`, no prefabs, no meshes, no materials. This is why no
`extra_dirs` flag is needed, unlike WoD.

---

## 4. Shaders and compute shaders

**None. The mod ships zero shader files** — no `.shader`, no `.compute`, no `.cginc`, no `.hlsl`.
It reuses DFU's own three by name:

| `Shader.Find` call site | Shader name |
|---|---|
| `WODTilemapTerrainMaterialProvider` field initialiser | `MaterialReader._DaggerfallTilemapShaderName` |
| `WODTilemapTextureArrayTerrainMaterialProvider` field initialiser | `MaterialReader._DaggerfallTilemapTextureArrayShaderName` |
| `CustomBillboardHelper.RevisedSetMaterial` | `_DaggerfallBillboardBatchShaderName` / `_DaggerfallBillboardBatchNoShadowsShaderName`, chosen by `DaggerfallUnity.Settings.NatureBillboardShadows` |

So: **no `#pragma target` to audit, no new keywords, no shader-variant explosion** (contrast Dynamic
Skies' 54 variants), no `RWTexture`, no `ComputeBuffer`, no `AsyncGPUReadback`, no `Graphics.Blit`.
Metal shader-compilation risk is **nil** — these are shaders our build already compiles and ships.

Keywords are only *forwarded*, never authored — `AssignKeyword("NORMAL_MAP" | "HEIGHT_MAP" |
"METALLIC_GLOSS_MAP", …)` copies whatever `MaterialReader` already enabled on the source tileset
material onto the per-terrain material, exactly as DFU's own provider does.

**CPU readbacks — the one place to look.** Two `GetPixel`-family calls, both on CPU-side `Texture2D`s,
neither a GPU readback:

1. `NatureBatchOverriderInstaller.climateMap.GetPixel(mx, ty)` — one call per matching billboard batch
   per terrain-streaming update. **This is the iOS blocker; see §7 risk 1.**
2. `Texture2D.PackTextures(...)` inside `RevisedGetTextureResults`, and DFU's own
   `EnsureReadable(texture).GetPixels32()` inside `TextureReader.GetTerrainTextureArray` when it
   rolls the replacement tiles into the `Texture2DArray`.

`_lastAtlas` is created as `new Texture2D(settings.atlasMaxSize, settings.atlasMaxSize, ARGB32, true)`
with `atlasMaxSize = DaggerfallUnity.Settings.AssetInjection ? 4096 : 2048` — a 4096² ARGB32 mipmapped
allocation is ~85 MB. `PackTextures` immediately shrinks it to fit (32 records of 64×64 needs ~512²),
so the steady-state cost is small, but there is a **transient ~85 MB spike**, once, on the first
orange-pixel terrain. `_atlasCache` means it happens once per archive for the session.

---

## 5. Dependencies

Declared in the manifest — exactly one, and it is required:

```json
"Dependencies": [{"Name": "daggerfall expanded textures", "IsOptional": false, "IsPeer": true, "Version": "1.2.0"}]
```

Probed by GUID at runtime, tolerating absence:

| Mod | GUID | Effect if missing |
|---|---|---|
| Vanilla Enhanced | `1f124f8c-dd01-48ad-a5b9-0b4a0e4702d2` | `VEModEnabled = false` → swapped nature flats get `recordSizes *= 2f`. Cosmetic only |

**Not dependencies, in either direction: WoD Terrain, Distant Terrain, Wilderness Overhaul, WoD
Project / Location Loader, RMB Resource Pack, Beautiful Villages.** None is named in the manifest and
none is probed in code.

### Does Biomes work WITHOUT WoD Terrain? — Yes, fully.

Biomes and WoD Terrain occupy **different** terrain slots: Biomes takes `ITerrainMaterialProvider`,
WoD Terrain takes `ITerrainSampler` (and has both its `TerrainTexturing` assignments commented out).
Nothing in Biomes reads a sampler, a heightmap, a `.wld` file, or WoD Terrain's GUID. Biomes on
vanilla DFU terrain simply reskins four climates.

The reverse is also true and is the one thing to keep in mind: since Biomes needs neither WoD Terrain
nor WoD Project, **it can ship independently of the whole WoD stack** — which is the opposite of the
situation for WoD Project, whose entry the launcher must gate on Location Loader. Biomes needs only DET.

DET's status in our tree is settled and favourable: `tools/bundled-mods/mods.json` carries
`DaggerfallExpandedTextures` as a **pre-built bundle** with a `permission:` licence ("granted to Ikram
Massabini in September 2026"), so it is already in the public pack and already the gate WoD is checked
against (`MobilePortedMods.WodRuns(llStarted, wodOn, detOn)`). A Biomes entry can reuse that same
`detOn` gate verbatim.

---

## 6. Licence status

**None. All rights reserved by default.**

- `gh api repos/drcarademono/wod-biomes` → `license=null`
- No `LICENSE`, `LICENCE`, `COPYING` or `NOTICE` file in the repo
- No `README` at all
- **No licence header on any of the three `.cs`** — first lines are `using DaggerfallWorkshop;`,
  `using UnityEngine;`, `using System;`
- The manifest has no licence field; `ContactInfo` is "Lysandus' Tomb Discord server"

Identical to `world-of-daggerfall`, `wod-terrain` and both Location Loader repos, and consistent with
MOD-MASTER-LIST row 560 ("none declared", "Blocked (licence)") and row 559 ("**Treat the whole family
as all rights reserved**"). Ikram is already seeking permission from the same authors
(carademono / KABoissonneault) for WoD Project and Location Loader, so **Biomes adds no new
counterparty** — it rides on a conversation already open.

Tooling consequence: a Biomes entry needs `"licence": "pending:…"` plus `"private_only": true`, and
`pack.py` then keeps it out of the public zip (`excluded_from_pack` returns true for `private_only`,
and `pack.py:84` refuses `pending:` in the pack manifest). Same treatment as `WorldOfDaggerfall`.

---

## 7. iOS risk list

### Risk 1 — CRITICAL: `climate_map.GetPixel()` on an ASTC, non-readable bundle texture

`Assets/Editor/MobileModBuilder.cs`'s `MobileModPackTextureImporter.OnPreprocessTexture` applies to
everything under `Assets/Game/Mods/` except `IOSPilot/` and `Converted/` — which is exactly where
`fetch.py` puts a fetched mod — and it forces:

```csharp
importer.isReadable = false;
var ios = importer.GetPlatformTextureSettings("iPhone");
ios.overridden = true;
ios.format = TextureImporterFormat.ASTC_6x6;
```

That breaks the colour key **twice over**:

1. **`isReadable = false`** → `climateMap.GetPixel(mx, ty)` throws. The mod's own `Init` logs
   `readable={climateMap.isReadable}` but `NatureBatchOverrider.ApplyOverrides` **never checks it** —
   it only null-checks. (`BiomesClimateSwap` in the LL fork *does* check and bails; this one does not.)
   So the first orange terrain throws once per matching batch, every streaming update.
2. **ASTC_6x6 is lossy**, so even made readable the exact test `c.r == 255 && c.g == 165 && c.b == 0`
   would fail on most or all pixels. A block-compressed colour-index map is not a colour-index map.

Both are fixable and the fix is small, but it is **mandatory, not optional**, and it is a change to
*our* tooling rather than to the mod:

- Add a per-mod or per-path exemption in `MobileModPackTextureImporter` for colour-key maps
  (`Assets/Maps/climate_map.png`), setting `isReadable = true`,
  `textureCompression = Uncompressed`, `mipmapEnabled = false`, `filterMode = Point`. The importer
  already has precedent for exactly this shape — `MobileModTextureImporter` does all four for
  `IOSPilot/`. 1000×500 RGBA32 uncompressed = **2.0 MB**, entirely affordable.
- Belt-and-braces in the ported C#: route the read through
  `TextureReplacement.EnsureReadable(climateMap)` (`TextureReplacement.cs:855`, our own MOBILE patch,
  which Blits to an `ARGB32` RenderTexture and caches the copy) and add the `isReadable` guard the LL
  fork already has. Note `EnsureReadable` alone does **not** fix problem 2 — the ASTC loss is already
  baked in by then. The importer exemption is the load-bearing half.

Simulator note: `DFU_PACK_TEX_FORMAT=RGBA32` masks this entirely (that is why the WoD run used it), so
**a simulator pass will not catch it**. This must be verified on device, or with an ASTC bundle.

### Risk 2 — HIGH: ASTC on 64×64 point-filtered terrain tiles

The 224 tiles go the same ASTC_6x6 route. They are `filterMode: 0` (Point) by design, and
MOD-MASTER-LIST row 554 already records the general finding for terrain atlases: point-filtered tiles
"**cannot be block-compressed without artifacts**". At 64×64 with a 6×6 block that is ~11 blocks
across — coarse. Worse, DFU then decompresses them anyway: `TextureReader.GetTerrainTextureArray` does
`EnsureReadable(texture).GetPixels32()` per record and rolls them into an **uncompressed ARGB32
`Texture2DArray`**, so ASTC buys *no runtime memory at all here* and costs only quality. Ship these
four folders uncompressed: 224 × 64 × 64 × 4 B = **3.7 MB** RGBA32, plus mips ≈ 4.9 MB. Trivially
worth it, and it removes the artifact question rather than arguing about it.

### Risk 3 — MEDIUM: `FindObjectsOfType` + material churn every terrain update

```csharp
void OnEnable() => StreamingWorld.OnUpdateTerrainsEnd += ApplyOverrides;
...
foreach (var batch in FindObjectsOfType<DaggerfallBillboardBatch>())
```

`FindObjectsOfType` is a full scene scan with an allocation, run on **every** `OnUpdateTerrainsEnd` —
i.e. every time the player crosses a map pixel, which is precisely when the frame is already busiest.
At `TerrainDistance` 3 that is ~49 terrains plus every location's batches. Two compounding problems:

- `RevisedSetMaterial(batch, NEW_ARCHIVE, **true**)` is called with `force: true`, so the
  `if (archive == cur && !force) return;` early-out never fires, and **`new Material(Shader.Find(...))`
  runs again for every matching batch on every update** — the atlas is cached in `_atlasCache` but the
  `Material` is not. That is an unbounded material leak plus a `Shader.Find` per batch per update.
- `batch.Apply()` rebuilds the batch mesh each time.

Compiled in, all three are cheap to fix and worth fixing before any device test: cache the `Material`
alongside the atlas; drop `force` to `false` (or track already-swapped batches in a `HashSet`); and
replace `FindObjectsOfType` with the terrain list `StreamingWorld` already has, or hook
`DaggerfallTerrain.OnPromoteTerrainData` per-terrain the way our LL port does. **Unfixed, this is the
most likely source of travel hitching** — and unlike WoD Terrain's readback stalls it is ordinary
managed-code overhead, not an architectural dead end.

### Risk 4 — MEDIUM: `Shader.Find` must become `MobileShaders.Find`

Our fork replaced `Shader.Find` with `Game.Mobile.MobileShaders.Find` inside `MaterialReader` for a
reason documented in `Assets/Scripts/Game/Mobile/MobileShaders.cs`: once mod bundles are loaded,
`Shader.Find` by name can hand back a *bundle's* embedded, variant-stripped copy of a Daggerfall
shader. Biomes has four raw `Shader.Find` calls. Three of the four names are already in
`MobileShaders.names` (`_DaggerfallTilemapShaderName`, `_DaggerfallTilemapTextureArrayShaderName`,
plus `_DaggerfallDefaultShaderName`/`_DaggerfallBillboardShaderName`/`_StandardShaderName`), so the
port edit is mechanical. **`_DaggerfallBillboardBatchShaderName` and its NoShadows variant are NOT in
that list** — note that upstream `DaggerfallBillboardBatch.cs:316-317` and `:380-381` also still use
raw `Shader.Find` for them, so this is a pre-existing gap in our fork rather than something Biomes
introduces; adding both names to `MobileShaders.names` fixes it for the engine and the port at once.

Also worth noting: both providers call `Shader.Find` in a **field initialiser**, so it runs at
construction time inside `WODBiomes.Start()`. That is after `MobileShaders.Capture()`
(`RuntimeInitializeOnLoadMethod`, `BeforeSceneLoad`), so the captured shaders are available — good.

### Risk 5 — LOW: IL2CPP stripping of the three reflected fields

`currentArchive`, `cachedMaterial` and `TextureArchive` are all read and written by
`DaggerfallBillboardBatch` itself, so managed stripping will keep them; and once the mod is compiled
into the app there is no assembly boundary and no `link.xml` needed. The clean move is to **delete the
reflection entirely** — make the two private fields `internal`, and assign them directly. That
eliminates the risk, the static-constructor cost and three `BindingFlags` lookups. This is the same
call already made for Flat Replacer (MOD-MASTER-LIST §15: "unnecessary once compiled in — a public
getter exists").

### Risk 6 — LOW: transient 85 MB atlas allocation

`new Texture2D(4096, 4096, ARGB32, true)` before `PackTextures` shrinks it (§4). One-off, cached
after, and only when the player first reaches an orange-keyed pixel. Pin `atlasMaxSize` to 1024 for
the port (32 records of 64×64 need ~512²) and it disappears. Add it to the device memory budget
(MOD-MASTER-LIST §17) but do not treat it as a blocker.

### Risk 7 — LOW: two `[Invoke]`s at the same priority

Undefined ordering upstream can leave `VEModEnabled` stale (§2). Compiled in, we order the calls; no
risk remains.

### What it patches or replaces in DFU 1.1.1

**One slot, cleanly, through a public interface: `DaggerfallUnity.Instance.TerrainMaterialProvider`.**
Plus one event subscription (`StreamingWorld.OnUpdateTerrainsEnd`) and one reflective write into
`DaggerfallBillboardBatch` instances. It **replaces no engine file, patches no engine method, and
overrides one protected virtual** (`GetClimateInfo`) on a class it does not inherit from — it
re-implements the abstract base. That means **no `UPSTREAM-PATCHES.md` entry is needed**, which is
unusual for this family and is the single best signal for feasibility.

Slot-conflict check against what we already ship: `ITerrainTexturing` is **ours** (`BasicRoadsTexturing`,
compiled in) — untouched. `ITerrainNature` is **Location Loader's** (`LocationTerrainNature`) —
untouched. `ITerrainSampler` is DFU's default — untouched. `ITerrainMaterialProvider` is **currently
unoccupied**, so Biomes is the first and only claimant. **No conflict with Basic Roads, Location
Loader, WoD Project or Climates & Calories.**

### Estimated cost

- **App/pack size**: 1.64 MB of source PNG → about **4.9 MB** as an uncompressed RGBA32 bundle if
  Risk 2's recommendation is taken (or ~0.6 MB as ASTC, at a quality cost that buys nothing at runtime).
- **Runtime memory**: 3-4 extra `Texture2DArray`s at 64×64×56 ARGB32 + mips ≈ **1.2 MB each, ~5 MB
  total**, and these largely *replace* the vanilla arrays the same climates would otherwise build
  rather than adding to them. `climate_map` 2.0 MB RGBA32 held resident. The 10030 atlas ~1 MB steady
  state. **Call it 8-10 MB steady state** — negligible against DREAM's measured 0.69 GB.
- **CPU**: dominated entirely by Risk 3. Fixed, it is a per-terrain-promote branch and a dictionary
  lookup. Unfixed, a scene scan plus N material allocations per map-pixel crossing.
- **GPU**: zero delta. Same shaders, same draw calls, same tilemap; only the texture contents differ.

### DFU version match

Manifest `DFUnity_Version` is **1.0.0**; our fork is **1.1.1** (`Assets/Scripts/VersionInfo.cs:20`).
This field is a minimum-target declaration and 1.0.0 ≤ 1.1.1, so nothing warns. It is also entirely
normal in our pack: `Vanilla Enhanced - Base`, `Dynamic Skies`, `Kokey's Temperate` and
`Fixed Dungeon Exteriors` all declare 1.0.0, and several shipped mods declare 0.12.0-0.15.3. More to
the point, **every API Biomes touches was verified present in our tree with matching signatures**
(§2) — the version string is not the compatibility question here, and the answer to the real one is yes.

---

## Verdict: MEDIUM (and the easiest member of the WoD family by a wide margin)

559 lines, three files, no shaders, no compute, no Jobs, no editor tooling, 1.64 MB of data, one
public interface slot, no engine patch, no new licence counterparty, and no dependency on WoD Terrain
or Distant Terrain. This corroborates MOD-MASTER-LIST row 560's "**the most portable piece of the WoD
stack**" and refutes nothing in it.

It is not "easy" for two reasons, both concrete: the **ASTC/readability break on `climate_map`
(Risk 1)** is a real blocker that requires a tooling change and that a simulator run will silently
mask; and the **`FindObjectsOfType`-plus-material-churn hot path (Risk 3)** should be rewritten before
any device test rather than after. Both are bounded, well-understood, and touch our code rather than
requiring anything unreviewed from upstream.

Suggested shape, following the WoD precedent exactly:

1. Compile the three files into `Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallBiomes/`,
   `[Invoke]` removed, port header naming `drcarademono/wod-biomes @ 40449fc` and "no licence header
   upstream; private draft only", `// MOBILE` on each edit.
2. `mods.json` entry `WorldOfDaggerfallBiomes`: `strip_code`, `private_only`, `licence: "pending:…"`,
   `archives_from: ["DaggerfallExpandedTextures"]`. **No `extra_dirs`, no `drop_dependencies`, no
   `manifest_override`** — the DET dependency is the only one and it stays.
3. Launcher entry via `MobilePortedMods.Titles`, default OFF, gated on DET only — reuse the existing
   `detOn` probe. **Not** gated on Location Loader or WoD, unlike WoD Project.
4. Tooling: the `climate_map` compression exemption (Risk 1) and the four terrain folders uncompressed
   (Risk 2).
5. Optional, decide separately: restoring `BiomesClimateSwap` (§1c) so WoD's type-5 RMB blocks get the
   nature swap. That work lands in the **Location Loader** port, not here, and needs a home for
   `climate_map` on the LL side. Biomes is complete and correct without it.
