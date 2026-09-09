# Engine patch inventory

The iOS port is a fork. Almost all of it lives in files upstream does not have
(`Assets/Scripts/Game/Mobile/`, `Assets/Editor/Mobile*`), which merge for free. This
document covers the exception: **15 upstream files we modify**, and what each change is
for. Keep it current — it is the difference between a routine rebase and an archaeology
session.

Measured against the port's first commit (`cc434d5e7`) as of v0.1.6-prealpha:
**16 files, roughly 455 lines added, 54 removed.** That is a small, tractable footprint for
a platform port, and worth defending.

## Ground rules that keep it small

1. **Prefer a new file in `Mobile/` over an engine edit.** Most of the port obeys this.
2. **Every engine edit is guarded** — `MobileInput.Enabled`, `MobileContentPath.Active`,
   or `#if UNITY_IOS && !UNITY_EDITOR` — so desktop behaviour is bit-identical and an
   upstream user of this fork loses nothing.
3. **Every edit carries a comment saying it is not upstream** and why. Grep for
   `MOBILE` / `EXPERIMENT` / `not upstream` to find them all.
4. **No reformatting, no drive-by cleanups** in engine files. Diff noise is what makes
   rebases expensive.

## The patches

### Input — `Assets/Scripts/Game/InputManager.cs` (+77, 12 hunks)
The largest patch, and the most load-bearing. Call-outs into the mobile layer at the
points where the engine collects input: `PollCursorStage()` before the paused
early-return (so menus work while the game is paused) and `PollGameplayStage()` after the
vanilla mouse axes are read. Also the touch-device branches that keep the engine's own
mouse path from fighting the touch layer.
*Rebase risk: HIGH.* Upstream touches InputManager often. If hunks conflict, re-anchor on
the same semantic points rather than line numbers.

### Classic HUD — `Assets/Scripts/Game/UserInterface/HUDLarge.cs` (+41, 2 hunks)
`IsLargeHUDInteractable()` passes on mobile when unpaused (the desktop `cursorActive`
gate makes every bar icon dead on a touchscreen), plus a new `TriggerTap()` that
hit-tests the eleven interactive panels so the touch layer can fire them.
*Rebase risk: LOW.* Self-contained; new method plus one early-return.

### Loose content paths — `AssetInjection/` (6 files, +265, 26 hunks)
`TextureReplacement`, `SoundReplacement`, `BookReplacement`, `VideoReplacement`,
`TextAssetReader`, `WorldDataReplacement`. All the same shape: read sites go through
`MobileContentPath.Override()` so a player copy under Documents wins, falling back to the
shipped file. Directory scans MERGE instead. Two are more than plumbing:
- **TextureReplacement** also forces `ARGB32` on iOS (DXT5 does not exist on iOS GPUs).
- **SoundReplacement** carries the whole WAV decoder and the ogg preload path (+212 alone),
  because the legacy `WWW("file://")` route returns empty clips on iOS.
*Rebase risk: LOW-MEDIUM.* Mechanical, but spread across six files.

### Quests — `QuestMachine.cs` (+7), `QuestListsManager.cs` (+21)
Quest sources and quest packs resolve from Documents first, then the 265 shipped quests.
Additive by necessity: a straight redirect would leave the game with no quests.
*Rebase risk: LOW.*

### Mods — `ModManager.cs` (+11, +45), `ModSupport/Editor/CreateModEditorWindow.cs` (+10)
`ModDirectory` points at Documents on iOS (the shipped folder is read-only, so the mod
system was enabled but permanently empty), and the mod builder gains an iOS build target,
off by default. 2026-09-01: `FindModsFromDirectory` scans BOTH Documents/Mods and the
shipped `StreamingAssets/Mods` on iOS (`MobileContentPath.Active`), merged by
`MergeModFiles` with the player's copy winning by file name - so bundled `.dfmod` files can
ship inside the app. Off iOS the two roots are the same folder and nothing changes.
*Rebase risk: LOW.*

### Billboards — `Internal/DaggerfallBillboard.cs` (+16)
`SetMaterial` returns null with one warning per archive when a flat's texture archive has no
such record, instead of throwing IndexOutOfRange from inside RDBLayout and aborting the
whole block - a mod block built against Daggerfall Expanded Textures blacked out
Privateer's Hold this way on device (2026-09-01). NOT platform-guarded on purpose: a
missing sprite beats a missing dungeon on desktop too, and nothing upstream relies on the
throw. *Rebase risk: LOW.* One guarded block after `GetMaterialAtlas`.

### Streaming world — `Terrain/StreamingWorld.cs` (+8)
`UpdateLocation()` refreshes `currentPlayerLocationObject` when it finishes building the
location for the player's own pixel. `UpdateLocations()` had looked it up in the same call
that STARTED the build coroutine, so for a freshly entered town the property stayed null
until the next pixel change - the journey pilot (and anything else asking whether the town
under the player exists) was told "no town" while standing in one. Correctness fix, not
guarded. *Rebase risk: LOW.* One block at the end of the coroutine.

### Status effects during real travel — `Effects/Poisons/PoisonEffect.cs` (+8), `Effects/Diseases/DiseaseEffect.cs` (+8)
While a journey walks, `UpdatePoison`/`UpdateDisease` consume the elapsed minutes/days
without applying them, so neither can kill a travelling player (vanilla fast travel heals to
full on arrival and never had this problem). Gated by
`MobileJourneyController.StatusEffectsPaused`, i.e. the pilot's Active flag - a no-op on
desktop and during vanilla fast travel. *Rebase risk: LOW.* One block each.

### Travel popup — `UserInterfaceWindows/DaggerfallTravelPopUp.cs` (+36)
`RouteOceanPixels` exposes the calculator's ocean-pixel count so real travel can refuse sea
routes and fall back to classic fast travel. The vanilla "you are not well and may not
survive an extended period of travel" prompt is skipped when the trip will be a walking
journey (`MobileJourneyController.WouldWalk`), since poison and disease pause during one.
With Ship ticked on a route that crosses no ocean, `CallFastTravelGoldCheck` first asks
"No ship sails to X from here. Travel there by horse/on foot instead?" (Yes continues, No
returns to the map) instead of silently walking. The journey call site in this file predates
this ledger entry (see the MOBILE comment there). *Rebase risk: LOW.*

### Texture readability — `Utility/AssetInjection/TextureReplacement.cs` (+40), `Utility/TextureReader.cs` (+4/-4)
`TextureReplacement.EnsureReadable` returns a CPU-readable RGBA32 copy of a texture that has
no CPU copy (GPU blit into a temporary RenderTexture, then ReadPixels; cached per source).
`GetTexture2DAtlas` feeds atlas inputs through it before `PackTextures`, and the terrain
texture-array fallback before `GetPixels32`. Textures inside asset bundles arrive with
Read/Write disabled - the iOS pack imports them that way on purpose - and DFU only logs
"Texture atlas needs textures to have Readable flag set!" when `PackTextures` refuses them;
the nature billboard atlas then stays blank and every tree and plant in the wilderness and
in towns renders as a flat grey rectangle (device report, 2026-09-03, with Vanilla Enhanced
installed; reproduced in the Simulator). Format-agnostic, so it also covers ASTC. Not
platform-guarded: readable textures pass straight through, and desktop mods with the flag
off get the same repair. *Rebase risk: LOW.* Three wrapped `Add` calls and one `GetPixels32`.

### Shader lookup — `MaterialReader.cs` (5 sites), new `Game/Mobile/MobileShaders.cs`
`MaterialReader` resolves its shaders through `MobileShaders.Find`, which captures the
player's own `Daggerfall/Default`, `Daggerfall/Billboard`, `Standard`, `Daggerfall/Tilemap`
and `Daggerfall/TilemapTextureArray` before the first scene loads. A bundle that ships a
Material embeds its own compiled copy of that material's shader, stripped to that bundle's
variants, and `Shader.Find` by name can return the copy once the bundle is loaded (two pack
bundles embed `Daggerfall/Default`). Guard, not a fix for an observed failure. *Rebase risk:
LOW.* Mechanical rename of five calls.

### Ported mods — `Game/Mobile/Ports/**` (new, not upstream code), `Game/Mobile/MobilePortedMods.cs`, `MobileTravelOptionsBridge.cs`, `MobileTavernWindow.cs`, `ModManager.cs` (+1), `Mod.cs` (+2)
Third-party mod code compiled into the app; see THIRD-PARTY.md "Survival mods". One engine line:
`ModManager.Awake` calls `MobilePortedMods.DefaultOff` between finding bundles and loading saved
settings, so the three start switched off (a found bundle otherwise defaults to on); and
`Mod.CompileSourceToAssemblies` returns early when a mod has no source list (a built-in entry with no
bundle threw NullReference there once enabled). Otherwise the bootstrap subscribes to `StateManager.OnStateChange` and calls each mod's own
`Init` the way `ModManager.InvokeModLoaders` would. *Rebase risk: LOW* (additive), but a DFU API change
that breaks the mods' code shows up as compile errors in `Ports/`.

### Input, journey hold — `InputManager.cs` (+1)
The journey's forward force is skipped while `MobileJourneyPilot.Holding` (the town under
the player is still being built). Part of the Input patch above.

### Lockpicking feedback — `Internal/DaggerfallActionDoor.cs` (+15)
`AttemptLockpicking()` returns silently when the player has already failed this door at
their current Lockpicking skill - correct vanilla rule, but on a touchscreen a mute door
is indistinguishable from a dead button, and it read as one in testing. Guarded by
`MobileInput.Enabled`, so desktop keeps the original silence. Uses the same
`PopupMessage` the success/failure paths use, with a reversion string because the text key
is not in the built StringTables yet (it IS in Internal_Strings.csv for a future import).
*Rebase risk: LOW.* One guarded block inside one method.

### UI plumbing — `DaggerfallUI.cs` (+3/-3), `BaseScreenComponent.cs` (+2/-1), `DaggerfallFont.cs` (+2/-2)
Small touch/scaling accommodations. Three lines each; check them by eye after a rebase
rather than trusting the merge.
*Rebase risk: LOW, but easy to lose silently — they are one-liners.*

### Dynamic Skies support — `Assets/Editor/MobileBuildSetup.cs` (+1), `Assets/Editor/MobileModExtractor.cs` (+few lines), `Assets/Game/Addons/ModSupport/{ModManager.cs (+5),Mod.cs (+4)}`, `tools/bundled-mods/{fetch.py,mods.json}`
`MobileBuildSetup.EnsureAlwaysIncludedShaders` - which already pins the classic UI's shaders into
GraphicsSettings because nothing in the project references them by name, so the build stripper would
otherwise cut them - now also pins `BLB/SkyBox/BLBProceduralSkybox`. Dynamic Skies' compiled-in
skybox shader has the same problem: it is referenced only from a Material inside a `.dfmod`, never
from project content, so without the pin the stripper cuts it on any build where that mod isn't
already loaded. `MobileModExtractor.IsNormalMapName` now also treats
a bare `Normal` name tail as a normal map: Dynamic Skies names its cloud normal map
`CdMCloudsNormal`, with no underscore, and imported as colour it lit nothing; the existing
underscore-suffix rule (`_Normal`) is unchanged for every other map. Separately, `tools/bundled-mods`
gained a `private_only` entry flag and a `pending:` licence form for a manifest entry whose licence
is not yet secured: `pack.py` excludes both `builtin` and `private_only` entries from the public zip,
and `fetch.py` refuses any entry that declares a `pending:` licence without `private_only` set. This
is what keeps Dynamic Skies (see THIRD-PARTY.md) off the public mod pack.
Two engine files carry a line each. `ModManager.Awake` calls `MobilePortedMods.DefaultOff(this)`
between `FindModsFromDirectory()` and `LoadModSettings()`, so a launcher entry the engine has just
discovered (Dynamic Skies, and the survival mods) starts switched OFF instead of the engine's
default ON, while a saved choice in `Mods.json` still wins because `LoadModSettings` runs after.
`Mod.cs` marks `FileName`, `Title`, `Enabled` and `LoadPriority` with `[fsProperty]`: the class is
`[fsObject(MemberSerialization = fsMemberSerialization.OptIn)]` and nothing was opted in, so
`Mods.json` was written as `[{},{},...]` and no enabled/priority choice ever survived a relaunch.
Upstream master uses `[SerializeField]` for this; it does not compile on a property in this Unity
(CS0592, field-only), and `Title` is not an auto-property so `[field: SerializeField]` is out too -
`[fsProperty]` is FullSerializer's own opt-in attribute and is honoured identically.
One-off upgrade effect: on an existing install the three survival switches (RoleplayRealism,
RoleplayRealism-Items, Climates & Calories) and Dynamic Skies read as OFF after the first launch of
this build, because switch state was never actually saved before (`Mods.json` was written empty) and
the off-by-default hook now runs; saves are unaffected, and re-enabling them in MODS now sticks.
`MobileBuildSetup.ApplyAll` also sets `PlayerSettings.enableFrameTimingStats = true`, which the
`sky ... gpu N ms` diagnostics line needs - a project setting, so it affects every build, not just
ones with the sky on.
*Rebase risk: LOW.* `MobileBuildSetup.cs` and `MobileModExtractor.cs` are new files, not upstream
ones; the tooling change touches only this fork's own `tools/`. The two engine lines are single
insertions next to stable upstream code - re-check them by eye after a rebase.

### DREAM follow-up (2026-09-08) — `ModManager.cs` (+17/-1), `UserInterface/FLCPlayer.cs` (+22/-4), `Game/Mobile/{MobileContentPath.cs,MobileLog.cs}`
Four small fixes found while putting DREAM on the device. Two touch upstream files; the other
two are in the port's own `Mobile/` folder and merge for free.
`ModManager.GetModFromName` compared `x.FileName.Equals(name, ...)` straight off each mod, which
throws the moment the walk reaches one of the four mods this port builds in code (Roads and tracks,
Real travel, Summer start, the TravelOptions bridge) — they have no `FileName`. On the iPad it fired
every frame the MODS window was open (`CheckDependencies`) and swallowed every conflict reorder
(`MobileModConflicts.MoveBelow`), so the player's conflict choices never applied. The comparison is
now `ModManager.FileNameMatches`, `public static` so the self test can reach it from the editor
assembly, and a mod with no file never matches.
`FLCPlayer.Load` looked for `.FLC` files only in the Movies folder inside the app bundle before
falling back to arena2, never consulting `MobileContentPath` the way `VideoReplacement` does — so
DREAM's 16 HD Daedra summoning animations in `Documents/Movies` were ignored. The folder now goes
through `MobileContentPath.Override` (whose existence check covers directories as well as files) and
the movies-or-arena2 choice is the pure, testable `FLCPlayer.ResolvePath`. **This is a newly touched
upstream file**, not counted in the totals at the top of this document.
`MobileContentPath` creates `Documents/Presets`: `ModSettingsData.LoadPresets` reads
`Presets/<mod file name>/*.json` through `TextAssetReader`, which already honours the redirect —
only the folder was missing, so a player had to create it by hand.
`MobileLog` writes a build stamp (`[Build] <product> <version> <bundle id> guid= unity= dfu=`) with
the session banner, before any Unity message reaches the mirror; twice in one week a `Player.log`
kept from an older build was read as the current one and sent a device test down the wrong path.
*Rebase risk: LOW.* Both engine edits are small and self-contained, and both are marked `MOBILE:`.

### World of Daggerfall support (2026-09-09) — `Game/Mobile/{MobileMods.cs (+22),MobilePortedMods.cs (+95/-8)}`, `Game/Mobile/Ports/{LocationLoader,WorldOfDaggerfall}/` (new), `Ports/WorldOfDaggerfall/WODRocksMaterials.cs.meta` (GUID pin), `Assets/Editor/MobileSelfTest.cs (+19)`, `tools/bundled-mods/{fetch.py,mods.json}`
Location Loader and World of Daggerfall compiled in (see THIRD-PARTY.md). **No upstream engine file
is touched** — the whole feature rides the hooks the survival mods and Dynamic Skies already added
(`MobilePortedMods.DefaultOff` from `ModManager.Awake`, `Mod.cs`'s `[fsProperty]` opt-in so the
switches survive a relaunch), so there is nothing new to re-apply after a rebase.
`MobileMods.Register` gains a built-in `Location Loader` entry with upstream's own GUID
(`fc5c0fa6-...`), version and contact, `Enabled = false`. It has to be registered in code rather than
discovered: Location Loader is pure code, its upstream manifest ships no data, so there is no bundle
for the engine to find and the player could otherwise never switch it on. `MobilePortedMods` adds both
titles to `Titles` (which is what `DefaultOff` walks, so both start off), the pure
`WodRuns(locationLoaderStarted, wodOn, detOn) = locationLoaderStarted && wodOn && detOn`, and a gate in
`StartEnabled` mirroring the Climates & Calories one: an enabled `World of Daggerfall` with Location
Loader off is switched off, `WoDGateNote` appended to its description once, `WriteModSettings()`, and a
log line. World of Daggerfall's other requirement, Daggerfall Expanded Textures, is gated the same way
by a second block: with that mod missing or switched off, World of Daggerfall is switched off at
start-up and `WoDDetNote` says why in MODS. Both gates read the player's choice before either clears
it, so with both dependencies absent both notes apply. DET is detected as DFU itself resolves that
manifest dependency - `CheckModDependencies` -> `GetModFromName` -> `ModManager.FileNameMatches`, an
ordinal `Equals` against `Mod.FileName` - matching the shipped bundle `daggerfall expanded textures.dfmod`,
never the title inside it. That comparison is ordinal, so case-SENSITIVE, because DFU's own dependency
check is: the bundle has to be named `daggerfall expanded textures.dfmod` exactly, and a hand-installed
copy under any other casing switches WoD off with the note while DFU logs its own "Failed to retrieve
mod" warning - self-consistent, and the two never disagree.
Then `LocationModLoader.Init` runs when the loader is on, and
`WODRocksMaterials.Init` after it only when that `Init` actually returned and DET is on; when the loader
was on but its `Init` threw, WoD is left unstarted with a log line and its setting untouched - a runtime
failure is not a user choice.
Each compiled-in mod's `Init` now goes through `MobilePortedMods.StartOne(title, init)`, which holds
its own try/catch, logs `start failed` with the exception, returns whether it got through and writes
the `started` line only when it did. Before that there was one try/catch around the whole of
`StartEnabled`, so a throw from any mod's `Init` abandoned every mod after it — and the sky, resolved
last, lost its deferred start for the session. The Dynamic Skies entry is now resolved at the top of
`StartEnabled`, before any `Init` runs, for the same reason.
`WODRocksMaterials.cs.meta` pins upstream's script GUID `426f76434556e931a830bf5c83c73b54` instead of
the fresh one Unity generates on import: 113 WoD prefabs (99 in `Rocks`, 14 in `Mountains`) bind the
script by that GUID, and with a different one they build with a missing `MonoBehaviour` — no compile
error, no log line, the climate and season rock materials simply never apply.
`tools/bundled-mods/fetch.py` gains `extra_dirs`, a per-entry list of repo folders copied wholesale
(`copytree`, `.git` excluded) into the mod folder after the manifest's `Files`, and missing raises
`SystemExit`. WoD's `Prefabs/*.prefab` reference `Meshes/*.fbx|.dae` by GUID and the
manifest never names them, so a manifest-only copy ships prefabs with no model; the folders stay out
of the manifest because Unity follows the GUIDs when it builds the bundle. UBLaMF's pre-existing
`extra_roots` now routes through the same helper. The `mods.json` entry is `strip_code` (its one
script is compiled in), `private_only` with a `pending:` licence (no licence declared upstream — the
combination `fetch.py` requires), `archives_from: ["DaggerfallExpandedTextures"]`, and
`drop_dependencies` for Location Loader (built in, so it has no `FileName` to match) and the three
optional mods this port does not ship. `MobileSelfTest` covers the default-off titles, `WodRuns`, `DETFileName`, `WoDDetNote` and `StartOne`.
*Rebase risk: NONE for the engine* — no upstream file changed. A DFU API change that breaks Location
Loader's code shows up as compile errors in `Ports/LocationLoader/`, and it hooks
`DaggerfallTerrain.OnPromoteTerrainData` and `StreamingWorld.OnInitWorld/OnUpdateTerrainsEnd` and
replaces `DaggerfallUnity.Instance.TerrainNature`, so watch those four after a rebase. The one thing
here that fails silently is the GUID pin: `WODRocksMaterials.cs.meta` carries upstream's script GUID
`426f76434556e931a830bf5c83c73b54` (commit `9d6c83c1a`), and re-copying the port source from upstream
without re-pinning the meta unbinds the rock materials in all 113 prefabs with nothing to notice it by.

## Rebase procedure

    git remote add upstream https://github.com/Interkarma/daggerfall-unity.git
    git fetch upstream master
    git checkout -b rebase-try ios-touch-port
    git rebase upstream/master

Then, in order:

1. **Resolve InputManager first.** It is the one that matters and the one most likely to
   conflict. Re-anchor on the semantic call sites, not line numbers.
2. **Run the self test** — `-executeMethod ...MobileSelfTest.RunAll`, 45 checks. It will
   not catch an input regression, but it catches broken maths and dead paths for free.
3. **Build for iOS.** The editor compile does NOT cover `#if UNITY_IOS` code, so a rebase
   can look clean and still have broken iOS-only branches. `BuildIOS` is the real check.
4. **Device-test the input paths by hand.** Nothing automated covers doors opening, the
   soft keyboard, or classic-bar taps, and all three have broken before.

## What a rebase cannot verify

The port's hardest-won knowledge is about device behaviour, not code: the iPadOS phantom
mouse/joystick pulses, dead UGUI pointer events, the 0.75s self-healing binding guard,
DXT5's absence, empty `WWW` audio clips. None of that is expressed as a test. If a rebase
changes behaviour in those areas it will look fine on the Mac and fail on the iPad — see
HANDOFF-controller.md for the full list before touching input or asset injection.
