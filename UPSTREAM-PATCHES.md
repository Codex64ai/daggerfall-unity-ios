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
and `Daggerfall/TilemapTextureArray` before the first scene loads — plus, since the WoD Biomes
entry below, `Daggerfall/BillboardBatch` and its NoShadows variant, for the two call sites in
`DaggerfallBillboardBatch.cs`; seven names and seven call sites in all. A bundle that ships a
Material embeds its own compiled copy of that material's shader, stripped to that bundle's
variants, and `Shader.Find` by name can return the copy once the bundle is loaded (two pack
bundles embed `Daggerfall/Default`). Guard, not a fix for an observed failure. *Rebase risk:
LOW.* Mechanical rename of seven calls.

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
**Extended 2026-09-10 (that one method, `+33/-1`):** `'-'` and `' '` are the same character in it.
A dependency names the file it needs, and a mod author writes that name the way the mod is titled
while whoever packages the bundle writes it the way a file is named: DREAM - SKY's manifest depends
on `dynamic skies` while the port ships that data as `dynamic-skies.dfmod`, so the ordinal `Equals`
never resolved it and DFU's launcher warned that the pair "might not work" for the whole of every
session - while the sky worked perfectly, because `MobilePortedMods` starts it directly and never
asks DFU's dependency machinery. Space and hyphen are interchangeable in every file name on both
platforms this ships to and neither carries meaning in a mod name, so treating them as equal costs
nothing and cannot collide: the separator still has to be PRESENT, only not spelled a particular way
(`dynamicskies` does not match `dynamic-skies`), and everything else stays ordinal, case included.
The comparison is now a length check and a char walk rather than `string.Equals`.
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
(2026-09-09: the stamp also goes out as a `Debug.Log`. On iOS the engine writes its own player log
to the same `Documents/Player.log` this mirror uses, from its own file offset, so it overwrote the
banner and no `[Build]` line survived anywhere in `Documents` on the simulator run. Through
`Debug.Log` the stamp lands in whichever of the two writers wins the file, and in the mirror too.)
*Rebase risk: LOW.* Both engine edits are small and self-contained, and both are marked `MOBILE:`.

### World of Daggerfall support (2026-09-09) — `Game/Mobile/{MobileMods.cs (+22),MobilePortedMods.cs (+100/-9)}`, `Game/Mobile/Ports/{LocationLoader,WorldOfDaggerfall}/` (new), `Ports/WorldOfDaggerfall/WODRocksMaterials.cs.meta` (GUID pin), `Assets/Editor/MobileSelfTest.cs (+146)`, `Game/Addons/ModSupport/ModManager.cs (+39/-3)`, `tools/bundled-mods/{fetch.py (+43/-7),mods.json (+21),test_fetch.py (+54),pack.py (+7),test_pack.py (+26)}`
Counts are `git diff --numstat` over the whole feature range, `ed8f82766^..HEAD`.
Location Loader and World of Daggerfall compiled in (see THIRD-PARTY.md). The feature rides the hooks
the survival mods and Dynamic Skies already added (`MobilePortedMods.DefaultOff` from
`ModManager.Awake`, `Mod.cs`'s `[fsProperty]` opt-in so the switches survive a relaunch); the one
upstream engine file it touches is `ModManager.cs`, for the settings write the gates rely on (below).
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
start-up. Both gates read the player's choice before either clears it, so with both dependencies absent
both notes apply.
The player's signal is the switch, not the note: the entry is simply off the next time MODS is opened,
and `Player.log` carries the reason -
`[PortedMods] World of Daggerfall switched off: Location Loader must be on` or
`[PortedMods] World of Daggerfall off: Daggerfall Expanded Textures is not enabled`. Appending
`WoDGateNote`/`WoDDetNote` to `ModInfo.ModDescription` is kept because it is the C&C gate's own pattern,
but it is best-effort and the current launcher flow never shows it: the only window that renders
`ModDescription` is `ModLoaderInterfaceWindow`, posted from the setup wizard in the Setup state - before
`ModManager.Init` and before `StartEnabled` runs - and `ModInfo` is rebuilt from the bundle manifest
every launch, so the note is gone again by the next launcher. Do not tell a tester to look for it.
DET is detected as DFU itself resolves that
manifest dependency - `CheckModDependencies` -> `GetModFromName` -> `ModManager.FileNameMatches`, an
ordinal comparison against `Mod.FileName` - matching the shipped bundle `daggerfall expanded textures.dfmod`,
never the title inside it. That comparison is case-SENSITIVE, because DFU's own dependency
check is: the bundle has to be named `daggerfall expanded textures.dfmod` exactly (up to hyphens for
spaces, since 2026-09-10 - see above), and a hand-installed
copy under any other casing switches WoD off - with the log line, not a visible note - while DFU logs
its own "Failed to retrieve mod" warning - self-consistent, and the two never disagree, since both go
through the same `FileNameMatches`.
Then `LocationModLoader.Init` runs when the loader is on, and
`WODRocksMaterials.Init` after it only when that `Init` actually returned and DET is on; when the loader
was on but its `Init` threw, WoD is left unstarted with a log line and its setting untouched - a runtime
failure is not a user choice. One caveat for reading the log: the `started Location Loader` line only
means `LocationModLoader.Init` returned. LL's real work is `LocationResourceManager.Start` ->
`CacheGlobalInstances`, a Unity message one frame later and outside `StartOne`'s try/catch, so judge
success by the `[LL]` lines (and the absence of a `LocationResourceManager` exception), not by
`started`.
Each compiled-in mod's `Init` now goes through `MobilePortedMods.StartOne(title, init)`, which holds
its own try/catch, logs `start failed` with the exception, returns whether it got through and writes
the `started` line only when it did. Before that there was one try/catch around the whole of
`StartEnabled`, so a throw from any mod's `Init` abandoned every mod after it — and the sky, resolved
last, lost its deferred start for the session. The Dynamic Skies entry is now resolved at the top of
`StartEnabled`, before any `Init` runs, for the same reason.
`ModManager.WriteModSettings` is the one upstream engine file this feature had to touch, and it is what
makes those gates safe to act on. `ModManager.Init` unloads every mod the player had switched off out
of `mods`, so by the time a gate runs at start-up that list holds only the enabled mods —
serializing it shrank `Documents/Mods/GameData/Mods.json` from 10 entries to 3 on the simulator run, and
a mod with no entry defaults to enabled, so every mod the player had switched off came back on at the
next launch. `Init` now keeps what it unloaded in a `prunedMods` list and the write merges the two
through the pure `public static MergeModSettings(current, previous)` — every current mod, plus a
last-known `Enabled`/`LoadPriority` entry for each mod no longer in the list, a title in both taking
its current value. `ModLoaderInterfaceWindow`'s own save path is unaffected: it edits and writes the
mods it lists, and the merge only adds back entries it never showed. One silent difference from
upstream's shape: `MergeModSettings` keys by `Title` and keeps the first entry per title, where upstream
wrote one entry per `Mod` object - and the pack does contain duplicate titles (`MobileModConflicts.cs`
documents them). Harmless, because `LoadModSettings` also applies a setting by title to the first
matching index, so the duplicate never had an effective setting of its own; worth knowing if that
lookup ever changes.
`WODRocksMaterials.cs.meta` pins upstream's script GUID `426f76434556e931a830bf5c83c73b54` instead of
the fresh one Unity generates on import: 113 WoD prefabs (99 in `Rocks`, 14 in `Mountains`) bind the
script by that GUID, and with a different one they build with a missing `MonoBehaviour` — no compile
error, no log line, the climate and season rock materials simply never apply.
`tools/bundled-mods/fetch.py` gains `extra_dirs`, a per-entry list of repo folders copied wholesale
(`copytree`) into the mod folder after the manifest's `Files`, and a missing folder raises `SystemExit`
naming the entry and the folder. WoD's `Prefabs/*.prefab` reference `Meshes/*.fbx|.dae` by GUID and the
manifest never names them, so a manifest-only copy ships prefabs with no model; the folders stay out
of the manifest because Unity follows the GUIDs when it builds the bundle. UBLaMF's pre-existing
`extra_roots` now routes through the same helper.
A wholesale copy bypasses the manifest's own `exclude_globs` and `strip_code` filters, and it lands
under `Assets/`, where Unity compiles any `.cs` it finds into the app with no diagnostic - so the copy
ignores `.git`, `*.cs`, `*.dll`, `*.dll.bytes`, `*.py` and `*.sh`, and, belt and braces, a `.cs`/`.dll`
surviving under the copied tree raises `SystemExit` naming the entry, the folder and the files. WoD's
own payload showed why: 36 of the authors' `.py`/`.sh` mesh-authoring scripts were being copied into
`Meshes/` (harmless to the bundle - only manifest `Files` and their GUID dependencies are built - but
not something to ship in `Assets/`; whether the already-fetched copies are deleted is the controller's
call, the folder is gitignored). `pack.py` gained the matching second line of defence for the licence:
any entry that WOULD be packed whose `licence` starts with `pending:` is a hard problem, so
`private_only` is no longer the only thing keeping an unlicensed bundle out of the public zip. The `mods.json` entry is `strip_code` (its one
script is compiled in), `private_only` with a `pending:` licence (no licence declared upstream — the
combination `fetch.py` requires), `archives_from: ["DaggerfallExpandedTextures"]`, and
`drop_dependencies` for Location Loader (built in, so it has no `FileName` to match) and the three
optional mods this port does not ship. `MobileSelfTest` covers the default-off titles, `WodRuns`, `DETFileName`, `WoDDetNote`, `StartOne`,
`MergeModSettings`, and the type-5 parse contract (`TypeRMB == 5`, `<groundPlane>` present, absent, and an
unknown type still rejected) through the same public `LocationHelper.LoadLocationPrefab(XmlDocument)` the
runtime uses.
**Location Loader object type 5 (RMB blocks).** WoD 0.4.0 was authored against Location Loader 0.4.x,
which is not upstream: type 5 — "place a whole RMB block as a prefab object" — exists only on
`drcarademono/DFU-LocationLoader` branch `rmb-object` (@ `896a574`), and our pin `a5e7a18` already **is**
the tip of `KABoissonneault/DFU-LocationLoader:main`, so there was nothing newer to move to. Without
type 5, `LocationHelper.ValidateValue` dropped the object at parse time and logged
`Invalid obj type found: 5` (8 lines on the simulator run, one per distinct prefab in streaming range):
24 of WoD's 266 prefab definitions hold a single type-5 object and nothing else, and 32,600 location
instances reference them — 32,218 wilderness farmsteads plus the 382 generic docks and lighthouses,
9.9 % of WoD's 329,040 instances, each appearing as a flattened, textured, empty clearing. The fix is a
backport of the type-5 core only, not a re-pin: `rmb-object` branched from `bee4d26` so it lacks our
pin's own tip commit, and it carries 2,400 unreviewed lines of barred doors, fake dungeons (every RMB
door hardcoded to `dungeonRegion = 43`), WOD-Biomes climate swapping and World Tooltips on top.
Three files under `Ports/LocationLoader/` gained `// MOBILE: backport of
drcarademono/DFU-LocationLoader@896a574 (rmb-object)` hunks and a second provenance line in their
header: `LocationData.cs` (`groundPlane` plus the six `Type*` constants), `LocationHelper.cs` (parse and
write the optional `<groundPlane>` element; `TypeRMB` joins types 3 and 4 in `ValidateValue`'s permissive
arm, the `Invalid obj type found` warning still firing for genuinely unknown types), and
`LocationLoader.cs` (the `else if (obj.type == LocationObject.TypeRMB)` branch in
`InstantiateInstanceDynamicObjects`, which builds the block through `RMBLayout.CreateBaseGameObject` and
adds nature flats, lights and misc/exterior flats at the current climate and season). Every DFU API it
needs exists in this fork with a matching signature. The branch stops where upstream's own
WOD-Biomes call begins: no `BiomesClimateSwap` (it is back as its own file now that the compiled-in
Biomes port owns the climate map — see the WoD Biomes entry below), and none of the
`DaggerfallStaticDoors`/`BarredDoor`
code, so vanilla doors on an RMB block behave as vanilla doors. `AssignNextIndex` and `AddGroundPlane`
stay commented exactly as upstream leaves them. The one addition beyond upstream is an iOS safety net:
the branch body sits in a try/catch that logs `[LL] RMB block <name> failed:` once per block name and
continues, because upstream lets a layout failure throw out of the whole spawn loop and take every
later object in the prefab with it — on a streaming terrain that would repeat on every step. Two
review follow-ups tighten that net: a name whose `ContentReader.BlockFileReader.GetBlockIndex` is −1 can
never resolve (a `WorldData` override is reachable only through `BlocksFile.GetBlock(int)`, so the name
needs an index first), so it is skipped before the build with `[LL] RMB block <name> skipped: not in
BLOCKS.BSA and no block index (WorldData override unreachable)` rather than surfacing as a bare
`NullReferenceException` out of `AddModels`, and the catch now destroys the block if
`CreateBaseGameObject` already built one, so a throw before the reparent cannot orphan a half-built block
at the scene root on every stream-in.
Residual, unchanged by this: `WOD_Dock_Daggerfall_01` still renders empty (3 instances). Its block name
`FOO.RMB` is in neither `BLOCKS.BSA` nor any `WorldData` `BlockNames` list, so `GetBlockIndex` returns −1;
registering it needs `WorldDataReplacement.AssignNextIndex`, which is `private` in DFU and which upstream
left commented pending a PR that was never made. Perf is the open question and the device test is the
judge: type 5 spawns per instance in `InstantiateInstanceDynamicObjects`, outside
`LocationResourceManager`'s prefab cache and outside the `ModelCombiner` batching that type 0 uses, so
each farm is an uncached, unbatched RMB block built on the terrain-streaming step that reveals it. It is
a memory question as well as a frame-time one: nature flats, lights and NPCs are spawned with
`billboardBatch = null` where DFU's own city layout passes a real `DaggerfallBillboardBatch`, so every
flat gets a freshly allocated, uncached `Mesh` (`MeshReader.cs`) and `Material`, and `DaggerfallBillboard`
has no `OnDestroy` to release either — upstream's behaviour and pre-existing DFU behaviour, not a port
defect, but 32,218 farm instances streaming in and out will accumulate native meshes and materials for
the whole session. Watch memory on the device run; the eventual fix, if it bites, is to pass a real
`DaggerfallBillboardBatch`.
*Rebase risk: LOW for the engine* — one upstream file changed, `ModManager.cs`: three `// MOBILE:`
touch points (the `prunedMods` field, the `Init` prune recording into it, and `WriteModSettings`
serializing `MergeModSettings(mods, prunedMods)`) plus the new method itself, all in hunks upstream
rarely moves. A DFU API change that breaks Location
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

### World of Daggerfall - Biomes support (2026-09-09) — `Game/Mobile/{MobilePortedMods.cs (+48/-2),MobileShaders.cs (+10)}`, `Game/Mobile/Ports/WorldOfDaggerfallBiomes/` (new, +852), `Game/Mobile/Ports/LocationLoader/{BiomesClimateSwap.cs (new, +219),LocationLoader.cs (+8)}`, `Assets/Scripts/Internal/DaggerfallBillboardBatch.cs (+10/-8)`, `Assets/Editor/{MobileModPackTextureRules.cs (new, +38),MobileModBuilder.cs (+16),MobileSelfTest.cs (+120/-1)}`, `tools/bundled-mods/mods.json (+13)`
Counts are `git diff --numstat 20fa6ba9a HEAD` (the plan commit to this feature's last).
World of Daggerfall - Biomes compiled in (see THIRD-PARTY.md). It rides the hooks the earlier ported
mods already added and touches **one** upstream engine file, `DaggerfallBillboardBatch.cs`.
**This is a newly touched upstream file**, not counted in the totals at the top of this document.

Two changes there, both small and both `MOBILE:`-marked. `CachedMaterial cachedMaterial` and
`int currentArchive` become `internal`, because the mod's `CustomBillboardHelper` wrote both by
reflection (`FieldInfo`s filled in a static constructor) to install its own atlas on a batch. The
port assigns them directly and the reflection is gone: the wins are compile-time checking (a rename
breaks the build instead of the swap), no static constructor and no three `BindingFlags` lookups.
Stripping was never the risk here — `link.xml` preserves `Assembly-CSharp` whole, and the batch reads
and writes both fields itself. Second, the two raw
`Shader.Find(MaterialReader._DaggerfallBillboardBatchShaderName)` /
`...NoShadowsShaderName` calls in `SetMaterial` and `SetMaterial(Material)` now go through
`Game.Mobile.MobileShaders.Find`, and those two names are added to `MobileShaders.names` — a
pre-existing gap in the shader-lookup patch above (a loaded mod bundle that embeds its own stripped
copy of `Daggerfall/BillboardBatch` could win the `Shader.Find` by name). The Biomes nature overrider
needs the same two shaders for the archive it swaps in, which is what surfaced it. `MobileShaders` also
gained `public static IReadOnlyList<string> Names => System.Array.AsReadOnly(names)` so the self-test
can assert the capture list rather than trusting it, without handing out the live array.

`MobilePortedMods` adds `BiomesTitle` to `Titles` (which `DefaultOff` walks, so the entry starts off),
the pure `BiomesRuns(biomesOn, detOn) => biomesOn && detOn`, `BiomesDetNote`, and a `BiomesRunning`
static flag. The gate block in `StartEnabled` mirrors WoD's Daggerfall Expanded Textures gate exactly —
switched off, note appended once, `WriteModSettings()`, log line — but there is deliberately no Location
Loader gate and no WoD gate: Biomes re-skins terrain and swaps nature billboards on its own, so it is
independent of both. Its two `Init`s then run through `StartOne` in an explicit order, terrain provider
first and nature overrider second, which is what fixes the upstream `[Invoke(Start, 0)]` race where
`NatureBatchOverriderInstaller` could read `WODBiomes.VEModEnabled` before `WODBiomes.Init` had set it.
`BiomesRunning` is set only when both returned, and only then is `[PortedMods] started World of
Daggerfall - Biomes` written; the per-`Init` `started ... (terrain)` / `started ... (nature)` lines
`StartOne` writes on its own are kept. As with WoD's notes, `BiomesDetNote` on `ModInfo.ModDescription`
is best-effort and the current launcher flow never renders it — the log line
`[PortedMods] World of Daggerfall - Biomes off: Daggerfall Expanded Textures is not enabled` is the
signal, and a tester should not be sent looking for a note.

`Assets/Editor/MobileModPackTextureRules.cs` is new and is the one thing here that can fail silently.
`MobileModPackTextureImporter` forces ASTC 6x6 and `isReadable = false` on every fetched pack texture;
Biomes cannot live with that, because its `climate_map.png` is a colour key read with `GetPixel` and
compared exactly, so any lossy format turns the swap off with no error, and its 224 point-filtered
64x64 terrain records are decompressed into an ARGB32 `Texture2DArray` by DFU regardless. The rules
class is a small pure pair — `Rule For(string assetPath)` and `bool NoMips(string assetPath)`, both
backslash-normalizing — and the importer consults it first and returns early with `isReadable = true`,
uncompressed, an overridden iPhone `RGBA32` at `maxTextureSize` 2048, and mipmaps off for the climate
map. **`For` matches on the path prefix `Assets/Game/Mods/<name>/`, where `<name>` is the `mods.json`
entry name** — today the single-element list `{ "WorldOfDaggerfallBiomes" }`. Renaming that entry
reverts these textures to ASTC and breaks the colour key with nothing in any log to say so, so the
name is a contract between `mods.json` and this file; the self-test pins the rule (both paths, a
backslash path, another mod still getting `Default`, and `NoMips` on the map but not the tiles) because
the importer itself only runs inside an import. Belt and braces at runtime,
`NatureBatchOverriderInstaller.ClimateMap` is a property that runs an unreadable map through
`TextureReplacement.EnsureReadable` once per session, so a bundle built before this rule, or
hand-installed, degrades to a GPU-blit copy instead of throwing.
**`BiomesClimateSwap` is back** (`Ports/LocationLoader/BiomesClimateSwap.cs`, +208, carademono's, LL
`rmb-object` @ 896a574). The type-5 backport above deliberately stopped short of it, because upstream
reads the climate map out of Location Loader's *own* bundle (`LocationModLoader.climate_map`, from
`mod.GetAsset<Texture2D>("climate_map")`) and the iOS Location Loader port is compiled in with no
bundle and so no `GetAsset`. The compiled-in Biomes port owns that asset now and publishes it as
`NatureBatchOverriderInstaller.ClimateMap`, so the file could come back and read it from there. It
covers what the terrain-side overrider cannot see: `WODClimates.cs` re-skins *batched* nature, while a
type-5 RMB block's nature is loose `Billboard` components, so those flats stayed vanilla in the
subtropics. It is called from the type-5 branch in `LocationLoader.cs` right after the transform is
assigned, behind the pure `ShouldSwap(MobilePortedMods.BiomesRunning, ClimateMap)` — both halves
matter, the first because archive 10030 only exists when Biomes and Daggerfall Expanded Textures are
both on, the second because the colour test samples the map on the CPU — and it shares
`NatureBatchOverrider.IsSubtropicalKey` and `MapReadable` rather than repeating either.
Three `MOBILE` hardenings on top of the copy. `ApplySwaps` swallows its own exceptions (one log line
per distinct message, `[Biomes] LL type-5 nature swap failed:`) for two reasons: the call site sits
inside `LocationLoader`'s own per-block try/catch, whose `catch` destroys and skips the block, so a
swap failure would cost a perfectly good block; and on the retry path a throw would take out the rest
of `StreamingWorld.OnUpdateTerrainsEnd`'s subscribers. Upstream's terrain-ready retry is kept, but the
`OnUpdateTerrainsEnd` subscription now happens at the point of deferral instead of in a static
constructor — a static constructor fires on the first touch of *any* member, including `ShouldSwap` and
including the editor self-test, which would have subscribed a handler in a batch-mode editor run. And
the three per-block log lines are logged once each rather than once per block: a WoD world has tens of
thousands of them. Its own success line is
`[Biomes] swapped N type-5 nature flats to archive 10030 in <block name>`,
deliberately distinct from the terrain side's `[Biomes] swapped N nature batches to archive 10030`.
The `mods.json` entry is `strip_code` (all three scripts are compiled in), `private_only` with a
`pending:` licence, `exclude_globs` for the repo's `.7z`/`.xcf`, and `archives_from:
["DaggerfallExpandedTextures"]`. No `extra_dirs`: the manifest names every asset, there are no prefabs
and no meshes. `MobileSelfTest` covers the two new shader names, the ported `Init` entry points and the
`ClimateMap` property shape, `IsSubtropicalKey` / `MapReadable` / `NeedsReadableCopy` / `HasRecords` /
`AtlasMaxSize` / `IsHammerfellRegion`, the default-off title,
`BiomesRuns` and `BiomesDetNote`, the importer rule table (including that every
`MobileModPackTextureRules.RawDataMods` name is a `mods.json` entry `name`, so a rename there fails the
suite rather than silently reverting the textures to ASTC), and `BiomesClimateSwap.ShouldSwap`. As with
Location Loader itself, the rest needs a streamed world and belongs to the simulator and device runs.
*Rebase risk: LOW.* All four edits to `Assets/Scripts/Internal/DaggerfallBillboardBatch.cs` are
mechanical — two accessibility changes (`currentArchive`, `cachedMaterial` → `internal`) and two
`Shader.Find(MaterialReader._DaggerfallBillboardBatch…)` → `Game.Mobile.MobileShaders.Find(...)`
renames, at `:318-319` and `:382-383` (one in each `SetMaterial` overload); everything else is in files
upstream does not have. The two `internal` fields are safe to lose: dropping them is a compile error in
the port, which is the good failure mode. The two `MobileShaders.Find` renames had no failure mode at
all — an upstream merge that took theirs compiled cleanly and passed every self-test, because
`MobileSelfTest` asserted only that `MobileShaders` captures and resolves the two billboard-batch shader
names, never that this engine file calls it, while the bundle-embedded-shader ambiguity the patch exists
to remove came silently back. `TestMobileShadersFind` now reads
`Assets/Scripts/Internal/DaggerfallBillboardBatch.cs` as text and fails unless all four
`MobileShaders.Find(MaterialReader._DaggerfallBillboardBatch…)` call sites are there and no raw
`Shader.Find(` is, so a rebase that takes theirs breaks the suite instead.

### World of Daggerfall - Terrain support (2026-09-09) — `Game/Mobile/MobilePortedMods.cs (+63/-3)`, `Game/Mobile/Ports/WorldOfDaggerfallTerrain/` (new, 20 files, +2,530), `Assets/Resources/WoDTerrain/` (new, 6 files, +2,557), `Assets/Editor/{MobileModPackTextureRules.cs (+54/-1),MobileModBuilder.cs (+24/-2),MobileSelfTest.cs (+282/-4)}`, `tools/bundled-mods/mods.json (+11)`
Counts are `git diff --numstat a344bd899 HEAD` (the plan commit to this feature's last), excluding
`.meta` files.
World of Daggerfall - Terrain compiled in (see THIRD-PARTY.md). **It touches no upstream engine file
at all** — not one. Everything it needed was already there: the ported-mods start-up hook, the
shader-lookup patch, the pack importer, `Mod.LoadAllAssetsFromBundle`. `DaggerfallUnity.TerrainSampler`
is a public settable property, so the most invasive thing this feature does — replacing the terrain
sampler — is done through DFU's own supported seam and needed no patch. Every file in the heading is
one upstream does not have: `MobilePortedMods.cs`, `MobileModPackTextureRules.cs`,
`MobileModBuilder.cs` and `MobileSelfTest.cs` are all this port's own, and
`Ports/WorldOfDaggerfallTerrain/` and `Assets/Resources/WoDTerrain/` are new trees. **Nothing here
adds to the file and line totals at the top of this document.**

`MobilePortedMods` adds `TerrainTitle` to `Titles` (which `DefaultOff` walks, so the entry starts
off) as the **last** element, and a start block after Biomes and before `return SkyRuns`. There is no
gate function and no dependency check here, deliberately: Basic Roads is optional to the mod and
Daggerfall Expanded Textures is not its dependency at all, so `terrain != null && terrain.Enabled` is
the whole condition, and the *capability* question — is there compute support, did the shaders
compile, is the bundle complete — belongs to `InterestingTerrains.Init`, which answers it before it
touches `DaggerfallUnity.TerrainSampler` and logs `[WoDTerrain] not available: ...` when the answer
is no. Started last of everything in `StartEnabled` for the same reason it is the most guarded: a
sampler swap that threw at that point cannot cost any mod before it its start.

Because `Init` declines by **logging and returning normally** on all four of its refusal paths rather
than by throwing, the plain `StartOne(title, init)` would have written `[PortedMods] started World of
Daggerfall - Terrain` for a device still running DFU's own sampler — the one line a `Player.log`
reader takes to mean "this mod is running". So this entry uses the four-argument overload
`StartOne(title, init, installed, hint)`, which asks the mod itself: `installed` is
`Monobelisk.InterestingTerrains.Installed`, set as the last statement of `Init` after the sampler has
been replaced and the message handler is listening, so every earlier return leaves it false. On a
clean return with `Installed` false the launcher writes
`[PortedMods] World of Daggerfall - Terrain did not start (see [WoDTerrain] lines)` instead, where
`"[WoDTerrain]"` is the `hint` argument — the log prefix whose lines carry the actual reason — and
returns false. Containment is unchanged: a throw still logs `start failed` and stops there. The two
literals are a pair, and a rename of either without the other breaks the trail a returned log is read
along.

One extra log line goes out **after** `StartOne`, and only when it returned true — that is, only on
the launch where the sampler really was installed and the ground really did move — once per launch
and at `Log` rather than `LogWarning`:
`[PortedMods] World of Daggerfall - Terrain: this changes ground height under existing saves and the
travel map (by design)` — the height curve it computes is a different world to the vanilla one, so a
character standing on ground that has moved is the expected outcome, not a bug report.

**The compute shaders are in `Assets/Resources/WoDTerrain/`, and that placement is a contract.** iOS
cannot load code from a `.dfmod` and a `.compute` is code, so the two live shaders and their four
`.cginc` are compiled into the app and loaded with `Resources.Load<ComputeShader>("WoDTerrain/" +
name)` in place of `mod.GetAsset<ComputeShader>(name)`; the `mods.json` entry excludes `*.compute`
and `*.cginc` from the fetch so the bundle cannot ship a second, stale copy of them. `Resources/` is
whole-folder-included in a player build, so nothing else was needed to ship them — but a `.compute`
that fails to compile still loads as a non-null asset with no kernels, which is why the gate checks
`HasKernel` on all three kernels rather than the null alone. One upstream line was deleted:
`#pragma exclude_renderers d3d11 gles` in `basicRoads.cginc`, a surface-shader pragma with no meaning
inside a `.cginc` included by a `.compute`. `MainHeightmapComputer.compute` gained an `int yOffset`
uniform and two lines of index arithmetic for the banded dispatch; with `yOffset` 0 it is byte for
byte upstream's mapping.

`MobileModPackTextureRules` gained a second exception kind, `Rule.LinearData`, keyed the same way as
`RawData`: **on the path prefix `Assets/Game/Mods/<name>/`, where `<name>` is the `mods.json` entry
name** — here the single-element list `{ "WorldOfDaggerfallTerrain" }`. The importer branch in
`MobileModBuilder.cs` sets `sRGBTexture = false`, uncompressed, an overridden iPhone `RGBA32` at
`maxTextureSize` 2048, `isReadable` false and `mipmapEnabled = !NoMipsForLinearData`, leaving the
upstream filter modes alone. All three of those are load-bearing and all three fail silently: sRGB
sampling would remap numeric maps, block compression would quantise them, and a rename of the
`mods.json` entry reverts both to the default ASTC path with nothing in any log to say so — which is
why the self-test checks every name in `RawDataMods` and `LinearDataMods` against `mods.json`. The
mip decision is the one that is worth restating as a contract rather than an optimisation: every read
of these maps in the shipped compute set is a level-0 fetch (`SampleLevel(..., 0)` in
`TerrainComputer.compute` and `basicRoads.cginc`, `float sampleLevel = 0` in `heightSampling.cginc`),
so the chain is ~11 MB of GPU residency nothing can sample, and the constant
`NoMipsForLinearData = true` exists so `MobileSelfTest` can pin it without running an import. **If a
future shader in this set ever samples a mip level, that constant is what has to change first** — the
five metas would still say `enableMipMap: 0` and the sample would silently read level 0. The two rule
lists must also stay disjoint (`For()` walks `rawDataMods` first, so a name in both would silently be
treated as raw data); `ListsDisjoint` is computed once, asserted by the self-test and thrown from
`For()` so a misconfiguration stops the import.

`MobileSelfTest` covers what is pure: the 1089 location-buffer constant against the shader's arrays,
the `Available` gate's three cases, `StartupBands == 10` and `GroupRowsY == 5`, that `Bands` splits
contiguously into whole thread groups and covers every row for every band count and throws on a
height that is not a whole number of groups, that `ReadbackStartRow`'s ten ranges tile
`[0, 500000)` exactly once (the mirrored offset is the deviation from the plan most likely to be
"corrected" back into a striped world), that `StartupSampleMaxIndices` keeps every start-up
`shm`/`lhm` read inside the two dummy buffers, that both compute shaders load from `Resources` with
their three kernels, that no `[Invoke]` survives, that a refused start drops the five maps and both
shaders, `ShouldRestoreWoodsBuffer`, and the importer rule table. The rest — the readback, the
sampler swap, the per-tile timings — needs a streamed world and a GPU and belongs to the simulator
and device runs.
*Rebase risk: NONE.* No upstream engine file is touched by this feature, so there is nothing here for
a rebase to conflict with or silently take theirs on. The exposure is indirect and worth naming
anyway: `DaggerfallUnity.TerrainSampler`, `TerrainSampler`/`DefaultTerrainSampler`,
`TerrainHelper.GetMapPixelData`, `MapPixelData`, `ContentReader.WoodsFileReader.Buffer` and
`DaggerfallTerrain.OnPromoteTerrainData` are all engine API the port calls into, and an upstream
change to any of them is a compile error in `Ports/WorldOfDaggerfallTerrain/` — which is the good
failure mode — except for `WoodsFileReader.Buffer`, where a change in what the engine expects that
buffer to contain would be a silent one. `DefaultTerrainSampler.MaxTerrainHeight` (1539) is read by
nothing here, but the port's own 5000 is what makes existing saves move; if upstream changes theirs,
the migration note in THIRD-PARTY.md and README-iOS.md needs the new number. One more silent-change
surface belongs on that list: `DaggerfallTerrain.CompleteMapPixelDataUpdate` **rebuilds**
`heightmapSamples` from `heightmapData` after the promote handlers have run. The port writes both —
its `To2D(...)` assignment to `heightmapSamples` is harmlessly overwritten by that rebuild — and it
is the rebuild that makes Basic Roads' `SmoothRoadsTerrainJob` edits survive into the mesh at all. An
upstream change from "rebuild it" to "use whatever the sampler set" would compile cleanly and change
the terrain, which is the bad failure mode; it is the one line in this dependency worth re-reading
after a rebase.

**The port replaces heights, not texturing.** Both `TerrainTexturing` assignments in
`InterestingTerrains` are commented out verbatim from upstream (which expects Wilderness Overhaul to
pull the tilemap through the `getTileData` mod message), so DFU's own texturing —
`BasicRoadsTexturing`, installed by `MobileRoads` — still assigns every tile. The `TilemapComputer`
dispatch and its 16,641-int blocking readback therefore run per tile and produce a `byte[16641]` that
`UncacheTileData` deletes unread: dead work, kept this round for fidelity with upstream and for the
`getTileData` contract, and a candidate for removal in a later round. It is a real slice of every
`[WoDTerrain] tile … ms` number, which matters when those numbers are the thing being judged.

### Distant Terrain (World of Daggerfall flavour) support (2026-09-09) — `Game/Mobile/Ports/DistantTerrain/` (new, 5 files, +3,918), `Assets/Shaders/DistantTerrain/` (new, 2 files, +1,081), `Game/Mobile/MobilePortedMods.cs` (+124/-8), `Game/Mobile/Ports/DynamicSkies/BLBSkybox.cs` (+50/-6), `Assets/Editor/{MobileShaderSpike.cs (new, +292),MobileSelfTest.cs (+708/-3),MobileModPackTextureRules.cs (+35/-2),MobileModBuilder.cs (+12/-1),MobileBuildSetup.cs (+11/-1)}`, `Assets/Shaders/RequiredShaderVariants.shadervariants (+7)`, `ProjectSettings/GraphicsSettings.asset (+1)`, `tools/bundled-mods/{mods.json (+11),fetch.py (+76/-15),test_fetch.py (+61)}`
Counts are `git diff --numstat 21eeff67f HEAD` (the commit before this feature's first to its last),
excluding `.meta` files.
Distant Terrain of the World of Daggerfall compiled in (see THIRD-PARTY.md). **It touches no upstream
engine *source* file** — not one; it does edit one already-ported third-party file of ours, which is
the next paragraph. Every C# file in the heading is one upstream does not have:
`MobilePortedMods.cs`, `MobileShaderSpike.cs`, `MobileSelfTest.cs`, `MobileModPackTextureRules.cs`,
`MobileModBuilder.cs` and `MobileBuildSetup.cs` are all this port's own, and
`Ports/DistantTerrain/` and `Assets/Shaders/DistantTerrain/` are new trees.

**One cross-feature touch: `Ports/DynamicSkies/BLBSkybox.cs` (+50/-6).** This feature edited an
already-shipped mod port, which is exactly the kind of reach this document exists to record. `Init`'s
branch key moved from `GameObject.Find("DistantTerrain")` to `GameObject.Find("stackedCamera")` — the
object is `DontDestroyOnLoad` and outlives a teardown that destroyed the camera, so keying on it took
the wrong branch on precisely the launch with no far terrain — and `LateUpdate` gained a throttled
re-bind for a stacked camera that turns up after `Init`'s bounded poll gave up. The three
`[DynamicSkies] clear flags on: …` lines (`player camera`, `stackedCamera`, `stackedCamera (late)`)
say which of the three ways it went, since they are indistinguishable from outside. Dynamic Skies is
not upstream DFU, so this adds nothing to the engine-patch totals, but a future rebase of the Dynamic
Skies port must carry it. Everything the feature
needed at the engine seam was already there and already patched for the four features before it: the
ported-mods start-up hook, `MobileShaders.Find` in place of `mod.GetAsset<Shader>`, the pack
importer, `Mod.LoadAllAssetsFromBundle`, and `StreamingWorld.OnReady` as a public event. **Nothing
here adds to the file and line totals at the top of this document.**

**Three engine *touch points* nonetheless, and all three are the shader pin.** They are worth naming
because they are the only part of this feature that lives outside `Mobile/` and `Ports/`, and because
two of them are generated project state that a careless rebase resolves by taking either side
wholesale. **(1)** `EnsureAlwaysIncludedShaders` in `Assets/Editor/MobileBuildSetup.cs` gains
`"Daggerfall/DistantTerrain/DistantTerrainTilemap"` as its sixth entry — the same treatment the
classic UI shaders and the BLB skybox get, and for the same reason: nothing in *project* content
references this shader by name (the C# resolves it through `MobileShaders.Find` at runtime), so
without the pin the build stripper cuts it and the far terrain has no material on a device while
compiling perfectly in the Editor. **(2)** `Assets/Shaders/RequiredShaderVariants.shadervariants`
gains an entry for guid `39d0ec48bfce941e599b735cbd64b761` with both `passType: 4` (ForwardBase)
variants — `<no keywords>` and `ENABLE_WATER_REFLECTIONS` — so the pair is warmed at player start
rather than compiled at the moment the horizon first appears. (Note against the plan, which said to
copy the shape of the BLB skybox entries: the BLB skybox has no entry in that file at all, it is
pinned through `EnsureAlwaysIncludedShaders` only, and every other project-shader entry in the file
is `variants: []`. This one names the keyword pair, and the self-test constructs a real
`ShaderVariantCollection.ShaderVariant` for each — a constructor that *throws* on a pass type or
keyword set the shader does not have, which is exactly the mistake a hand-written entry makes and
which would otherwise surface only as a player-log warning much later.) **(3)**
`ProjectSettings/GraphicsSettings.asset` gains
`- {fileID: 4800000, guid: 39d0ec48bfce941e599b735cbd64b761, type: 3}` in `m_AlwaysIncludedShaders`,
which is `ApplyIOSSettings` writing out what (1) declares. That file is upstream-tracked project
state rather than code — the same one-line-per-pinned-shader change Dynamic Skies committed, and the
precedent for committing it. If a rebase drops that line the symptom is a shader that is present in
the project, passes every Editor check, and is absent from the player.

The launcher block follows the shape the four features before it established: `DistantTitle` is the
manifest's real `ModTitle`, the full `"Distant Terrain of the World of Daggerfall"`, added to
`Titles` (which `DefaultOff` walks, so the entry starts off), with no dependency gate at all — the
mod's own `BasicMode` setting covers a no-World-of-Daggerfall setup, so its own switch is the whole
condition and the *capability* question belongs to `DistantTerrainPort.Init`, which answers it
(shader resolved and `isSupported`, three CSVs present, deriv map present) and logs
`[DistantTerrain] not available: <reason>` when the answer is no. Because that gate declines by
logging and returning normally rather than by throwing, the plain `StartOne(title, init)` would have
written `[PortedMods] started Distant Terrain of the World of Daggerfall` for a session with no far
terrain in it, so the entry uses the four-argument overload
`StartOne(title, init, installed, hint)` with hint `"[DistantTerrain]"`, giving
`[PortedMods] Distant Terrain of the World of Daggerfall did not start (see [DistantTerrain] lines)`
on a clean refusal. **The flag it asks is `Running`, not `Installed`, and the distinction is
load-bearing**: `Installed` becomes true only when a far terrain has actually been built, which
happens at `StreamingWorld.OnReady`, long after `Init` has returned — so a launcher that asked
`Installed` would report every enabled launch as a failure. `Running` is set on the last line of
`Init` when the gate passed and the hooks are subscribed, which is precisely what the launcher line
means; `[DistantTerrain] far terrain ready` from the build itself is the later half of the story,
and the two together are how a log reader tells "the mod started" from "the horizon actually got
built".

**The start order with Dynamic Skies is a contract, not a coincidence.** The Distant Terrain block
sits **before** the sky's deferred start, and `SkySceneReady` grew from two arguments to four —
`SkySceneReady(sunLight, mainCamera, distantRunning, stackedCamera) => sunLight && mainCamera &&
(!distantRunning || stackedCamera)`, with the old two-argument form kept as
`=> SkySceneReady(a, b, false, false)` so nothing else had to change — so that the sky's 1 Hz poll
additionally waits for `GameObject.Find("stackedCamera")` whenever `DistantTerrainPort.Running`.
`BLBSkybox.Init` already looks for `"DistantTerrain"` and `"stackedCamera"` and takes a different
branch when it finds them; without the wait, which branch it took depended on the order two
coroutines happened to tick. The poll asks both flags every pass rather than capturing them once —
`Running` is settled before the coroutine starts (`StartEnabled` is synchronous) but the stacked
camera is a scene object that can come and go with a reload — and it gained one log line, said once
and only when the scene is up and that camera is the sole thing still missing:
`[PortedMods] Dynamic Skies waiting for Distant Terrain's stacked camera`. The existing
`waiting for the scene (SunLight, MainCamera)` line is unchanged, and the two are deliberately
distinguishable: the first says the title screen has not finished, the second says this feature is
the reason. The consequence to know about is that **the stacked camera only exists
from world entry** — this port's `SetupGameObjects` declines until StreamingWorld, the player and
WeatherManager exist, because upstream's `Awake` would have called `Application.Quit()` at the title
screen — so with Distant Terrain on, the sky now starts at world entry rather than at the title. That
is accepted (the title screen needs no sky) and it is the single most visible difference the start
order makes.

The importer side reuses the `RawData` rule Biomes introduced rather than adding a third rule kind:
`MobileModPackTextureRules` adds `DistantTerrainWoD` to `rawDataMods`, extends `NoMips` to the deriv
map, and adds a new per-file `SingleChannel(string assetPath)` hook that `MobileModBuilder` reads to
pick `TextureImporterFormat.R8` instead of `RGBA32` on the iPhone override. **Both new rules key on
mod folder *plus* file name, never the bare file name**, and that is not fastidiousness:
`WorldOfDaggerfallTerrain` ships a different `daggerfall_deriv_map.png` under the `LinearData` rule,
and a bare-name match would have taken three channels and the mip chain off a map a compute shader
reads. `MobileBuildSetup.ReimportPacks`' single-asset path also now logs the imported size and
format, because `maxTextureSize` is the one setting a `.meta` file cannot prove — the meta says
`2048`, and only the log says the 5,000x2,500 source actually landed at 2048x1024 rather than at the
1250x625 a power-of-two halving would have given.

`MobileSelfTest` covers what is pure and what is textual: the slice contract (`SlicesPerBiome`,
`SliceIndex(3,55) == 223`, 224 distinct slices with no gap), the `Available` gate's full eight-case
truth table, `Installed`/`Running` false before `Init` and no surviving `[Invoke]`, the reach preset
(60,000 / 15,000, `BlendStartFor` at upstream's 5:6 proportion, and `blendStart < blendEnd`),
`TilesetsCompatible` accepting four vanilla tilesets and refusing a resized, recompressed or
differently-mipped one with a detail string that names the difference, the array byte arithmetic and
the ~15 MB total, the map-pixel logging cadence, every one of the port's log literals and its single
`[DistantTerrain]` prefix, the importer rule table including an `R8` `GetPixels32` round trip, and
all three pin locations plus the constructed shader variants. Source-text checks — that the atlas
machinery is gone, that the fly-map, the spell, the console command and the hotkey are gone, that
`Application.Quit` is gone — run on a **comment-stripped** copy of the sources, because the port's
comments deliberately name the upstream symbols they replaced and an "it is gone" check that a
comment can satisfy is worthless. The rest — the `Graphics.CopyTexture` pack, the timings, the
teardown, the fog, what the horizon actually looks like — needs a streamed world and a GPU, and
belongs to the simulator and device runs.
*Rebase risk: LOW, and concentrated in one line.* No upstream engine source file is touched, so
there is nothing here for a rebase to conflict with in code. The exposure is the shader pin: a
rebase that resolves `ProjectSettings/GraphicsSettings.asset` by taking theirs silently drops the
`m_AlwaysIncludedShaders` entry, and the failure mode is a far terrain that works in the Editor and
is missing in the player with nothing in any log to say why — re-run `MobileBuildSetup.ApplyIOSSettings`
after any rebase that touches that file and check the guid is back. Beyond that the exposure is
indirect and worth naming: `TextureReader.GetTerrainTextureArray`, `TextureMap.Albedo`,
`TextureReplacement.TryImportTextureArray`, `StreamingWorld.OnReady`, `PlayerGPS`, `WeatherManager`'s
five fog fields and `MaterialReader.MainFilterMode` are all engine API this port calls into, and an
upstream change to any of them is a compile error in `Ports/DistantTerrain/`, which is the good
failure mode. The two silent ones are `GetTerrainTextureArray`'s *shape* — 56 slices per archive at
64x64 ARGB32 is what the pack and the `slice = biome * 56 + record` arithmetic assume, and a change
to slice count or layout would produce a wrongly-textured horizon rather than an error — and
`WeatherManager`'s fog fields, which this mod overwrites at `Start` by upstream's design.

### Follow-ups after the WoD family (2026-09-10) — `Assets/Scripts/Utility/RMBLayout.cs (+4/-1)`

One engine file, one guard, four lines. `RMBLayout.AddProps` discarded
`MeshReader.GetModelData`'s return value. On `false` that method still assigns
`modelData = new ModelData()` — a default struct whose `SubMeshes`, `Indices`, `Vertices`
and `Normals` are all null — and `ModelCombiner.Add`'s first statement is
`foreach (var sm in modelData.SubMeshes)`. So a `Misc3dObjectRecord` naming a model id
`ARCH3D.BSA` has no record for threw `NullReferenceException` out of `AddProps`, out of
`CreateBaseGameObject`, and the caller lost the **entire block** over one missing prop.

The patch reads the return value and skips that record with the same `Debug.LogError`
message `AddModels` (`:843`, eighty lines above in the same file) has always used — the
guard already existed in the sibling loop and simply was not in this one. It sits after
`MeshReplacement.ImportCustomGameobject`, matching `AddModels`' ordering, so a custom
replacement model still wins where Daggerfall has no mesh.

Found through Location Loader's type-5 objects, which build World of Daggerfall's
`WorldData` override blocks: `ALCHAS00.RMB` and `ALCHBS01.RMB` rendered as empty
clearings and all the game reported was an unattributed NRE. It is not a mobile-only
bug — the same block from the same mod fails the same way on desktop — so this is a
candidate to send upstream rather than carry.

*Rebase risk: LOW.* A merge that takes theirs restores the discarded return and compiles
cleanly, with the dock blocks silently vanishing again. `MobileSelfTest` reads
`RMBLayout.cs` as comment-stripped text and requires exactly two `GetModelData(` call
sites, both assigned to `hasModelData` and both followed by an `if (!hasModelData)`
skip — so the tripwire fires in the Editor, not on a device.

**A second, port-only change to the same file, and this half is NOT an upstream
candidate.** With the block building, the simulator run showed what the NRE had been
hiding: both loops log one `Debug.LogError` **per record**, and World of Daggerfall's
override blocks place models from carademono's RMB Resource Pack, one of the three
optional WoD dependencies this build deliberately does not ship (`THIRD-PARTY.md`
:178). One visit to a dock produced **190** error lines from 20 distinct ids; Daggerfall
city produced 199 from 16. On desktop, with the pack installed, the condition does not
arise — this is noise created by *our* choice not to ship it, so both sites now route
through a private `ReportMissingModel(modelID, blockName)` that keeps the severity and
the message but reports each id **once per session**, naming the first block that wanted
it (190 → 20, 199 → 16). Do not offer this one upstream; on a rebase, taking theirs is
harmless (louder, not wrong).

---

### Mobile CRT filter (2026-09-10) — `Assets/Scripts/Utility/RetroPresentation.cs (+58/-1)`, `Assets/Scripts/SettingsManager.cs (+28/-2)`, `Assets/Resources/defaults.ini.txt (+6/-1)`, `Assets/Scripts/Game/UserInterfaceWindows/GameEffectsConfigWindow.cs (+1)`

Two engine files, and both patches are small because the retro path hands out an ideal
insertion point.

**`RetroPresentation.cs`** is 31 lines upstream and holds the *single* `Graphics.Blit` that
puts the retro picture on the backbuffer — the one `OnRenderImage` in the whole of
`Assets/Scripts`. The patch gives that blit a material argument:

- `:13` — `using DaggerfallWorkshop.Game.Mobile;`
- `:24-63` — a lazily-resolved `static Material crtMaterial` (built once from
  `MobileShaders.Find(MobileCrt.ShaderName)`, warns once and stays null if the shader was
  stripped) plus five cached `Shader.PropertyToID` ids.
- `:69-82` — inside the existing `if`, when `MobileCrt.Active(CRTFilter, RetroRenderingMode,
  material != null)`: push the five uniforms and
  `Graphics.Blit(RetroPresentationSource, null as RenderTexture, crt)`, then return.
- `:85` — the upstream plain blit is untouched and is still the path taken whenever the
  filter is off, retro mode is 0, or the material did not resolve.

Why here and nowhere else: this blit runs at **native backbuffer resolution**, which is what
scanlines and a phosphor grille need (the same effect inside the 640×400 intermediate would
be upscaled into mush); it respects `camera.rect`, so aspect correction and the docked large
HUD keep working; and it sits downstream of every camera target, so nothing that re-points
`Camera.main.targetTexture` — `RetroRenderer.UpdateRenderTarget`, Distant Terrain's stacked
camera — can interact with it. The PPv2 route was rejected for the opposite reason: a
`PostProcessEffectRenderer` runs on `Camera.main`, i.e. *inside* the 320×200 texture.

The world is filtered and the UI is not. `DaggerfallUI` draws in `OnGUI` after every camera,
so the HUD, menus and paper doll stay pin-sharp and flat over a curved world. That is a
decision, not an oversight: filtering the whole screen needs a second pass after the UI and
would curve the touch controls away from where fingers land.

**`SettingsManager.cs`** gains the five `CRT*` keys (`:167-171` properties, `:429-433` load,
`:634-638` save) and — the part that is worth having upstream — **clamps the two retro keys
that had no bounds at all**:

- `:421` `PostProcessingInRetroMode = GetInt(sectionVideo, "PostProcessingInRetroMode", 0, 4)`
  (was unclamped). It is the colour-crush selector, not an on/off; a value outside 0..4 falls
  through `UpdateDepthProcessMaterial`'s switch leaving `postprocessMaterial` null, and the
  next line sets `retroMode = 0` — so a typo in the ini silently turns retro mode off.
- `:428` `PalettizationLUTShift = GetInt(sectionVideo, "PalettizationLUTShift", 1, 3)`
  (was unclamped). `RetroRenderer.GetPalettizationMaterial` builds a `Texture3D` of
  `(256 >> shift)^3` RGBA32 on the main thread; the code's own comment table puts shift 0 at
  **64 MB and ~7 s**. On an A-series that is not a slow load, it is a crash. The iOS default
  in `defaults.ini.txt` moves 1 → **2** (1 MB, "slightly less crisp"); the LUT build stays
  exactly where DFU does it.

`defaults.ini.txt` `[Video]` therefore reads `PalettizationLUTShift=2`, `CRTFilter=False`,
`CRTCurvature=0.08`, `CRTScanlines=0.35`, `CRTMask=0.25`, `CRTVignette=0.25`.

Everything else the filter needs is ours and outside upstream's tree:
`Assets/Shaders/Mobile/MobileCRT.shader` (new, 181 lines, MIT, written from the formulas —
see `THIRD-PARTY.md`), `Assets/Scripts/Game/Mobile/MobileCrt.cs` (new, the pure rules),
`MobileShaders.cs (+4)` (name registration), `Assets/Editor/MobileBuildSetup.cs (+4)` and
`ProjectSettings/GraphicsSettings.asset (+1)` (Always-Included pin),
`RequiredShaderVariants.shadervariants (+7)` (the `CRT_HALATION` pair),
`Assets/Scripts/Game/UserInterfaceWindows/CRTConfigPage.cs` (new, 184 lines, the Game Effects
page — see below), `Assets/Scripts/Game/Mobile/MobileSettingsPanel.cs (+50)` (three rows in the
touch panel's HUD section: Retro mode, Aspect, CRT filter) and
`Assets/Editor/MobileSelfTest.cs (+636)` across the feature's three commits.

The `CRT_HALATION` keyword is the one loose end left deliberately: the variant compiles, both
`.shadervariants` entries exist and it costs about seven extra Metal statements and two extra
texture fetches, but **nothing turns it on** — no setting, no slider, no quality tier. It is kept because the pin is
what makes a later tier a shader-free change; if it is still unused after the device round, the
pair is two lines to delete.

**Review fixes (same day, commit 2 of the feature).** Three defects the Task 1 review found,
all in our own files, none of them in an upstream one: the vignette factor is now
`saturate(1 - _Vignette*r2)` (`r2` reaches ~1.58 inside the curved screen, so the unsaturated
form went negative above `_Vignette` ~0.63 — and the slider's range is 0..1); the scanline
phase is `0.5 + 0.5*cos`, so the darkening peaks on the seam between two source rows rather
than on their centres; and `_ScanlineCount` now comes from `RetroRenderer.RetroTexture.height`
(`MobileCrt.ScanlineCount` → `ScanlineCountFor`) rather than from `RetroRenderingMode`, because
a docked large HUD renders into a 320×154 or 640×308 raster. The fake gamma (`c*c` in, `sqrt`
out) was **dropped**: this project is Linear and the presentation render texture is not sRGB,
so the pair was an identity on the picture that applied every modulation at `sqrt` depth.

A note on the rendered evidence, since this entry is the record. `MobileSelfTest.TestMobileCRTRender`
blits the real material and reads pixels back, but its first pass targets `ARGB32`, where a negative
colour clamps to 0 — so it proves the scanline phase and the off-screen mask and, on that format,
*nothing* about the vignette: with the `sqrt` gone the saturated and unsaturated forms are
pixel-identical there. The `saturate` therefore gets a second pass on an `ARGBHalf` target at
`_Vignette = 1`, `_Curvature = 0.3`, scanlines and mask 0, asserting that no channel anywhere is
below zero. `r2` still reaches ~1.12 while the warped uv is inside the screen at that curvature, so
the unsaturated form writes about −0.057 over ~348 of the 65,536 pixels. Measured both ways: with
the `saturate` removed the readback's lowest channel is −0.0569 and the check fails; with it, it is
0.0000 and the check passes. Two probes on the centre column (0.5020 at the middle, 0.3228 at 60%
out) keep that from being vacuous if the vignette ever stopped being applied at all.

A note on the clamp evidence, since this entry is the record: the nine `GetInt/GetFloat(min,
max)` checks are a `Mathf.Clamp` exercise and passed before the clamps were added to
`LoadSettings` — they are a source-text guarantee, not a behavioural one. The behavioural
guarantee is `MobileSelfTest.TestMobileCRTSettingsEndToEnd`, which writes
`PalettizationLUTShift=0`, `PostProcessingInRetroMode=-1`, `CRTCurvature=9` and
`CRTVignette=-3` into the Editor's own `settings.ini`, constructs a `SettingsManager` (whose
constructor *is* `LoadSettings`), reads 1 / 0 / 0.3 / 0 back off the public properties, and
restores both `settings.ini` and its `.bak` byte for byte — and then checks that it did.

**`GameEffectsConfigWindow.cs`** gains exactly one line, in `AddCorePages`:
`AddConfigPage(new CRTConfigPage());`, placed immediately after `RetroModeConfigPage` because the
filter does nothing while retro mode is off. The page itself,
`Assets/Scripts/Game/UserInterfaceWindows/CRTConfigPage.cs`, is ours (MIT header) and lives in
that folder only because the window constructs its pages by name out of this namespace — nothing
discovers them. The page writes `DaggerfallUnity.Settings` directly and implements
`DeploySettings` as a no-op: the filter is not a PPv2 effect and has no
`CoreGameEffectSettingsGroups` entry, and `RetroPresentation` re-reads the five settings on every
frame it presents. *Rebase risk: LOW.* Taking theirs deletes the registration line and the page
becomes unreachable while still compiling — `MobileSelfTest.TestMobileCRTUI` checks for the line
and for its position next to Retro Mode, so the tripwire fires in the Editor.

Two notes on the page, since neither is obvious from the diff. Its sliders' ranges **are** the
loader's clamps (`0..MobileCrt.MaxCurvature`, and `0..1` three times), so the UI cannot ask for a
value the next launch would refuse; and it guards its own `OnScroll` handlers while
`ReadSettings` and `Setup` run, because `HorizontalSlider.SetIndicator` raises `OnScroll` as it
positions the thumb and the other config pages consequently write their rounded slider values
back over the settings they were built from (curvature 0.08 would become 0.1 the first time the
window opened). DFU's float slider indicator carries one decimal digit, so curvature is a
four-step control; the 0.08 default is reachable through "set page defaults".

*Rebase risk: MEDIUM for `RetroPresentation.cs`, LOW for `SettingsManager.cs`.* Taking theirs
on the 31-line presenter deletes the filter wholesale and still compiles — the `MobileCrt`
call and the material overload both vanish with the file. `MobileSelfTest.TestMobileCRT`
reads the presenter as comment-stripped text and requires the material overload, the
`MobileCrt.Active(` gate, the `MobileShaders.Find(MobileCrt.ShaderName)` lookup and the
surviving plain blit, so the tripwire fires in the Editor. Taking theirs on
`SettingsManager.cs` drops the five keys (compile error at the presenter, so it cannot pass
unnoticed) and re-opens the two clamps (source-text checks catch that too).

---

### Real Grass support (2026-09-10) — `ProjectSettings/GraphicsSettings.asset (+3)`, `Assets/Editor/MobileBuildSetup.cs (+15)`

**No engine file is patched for this one**, and that is the whole entry's point. Real Grass hooks
`DaggerfallTerrain.OnPromoteTerrainData`, a public event DFU already raises, and fills Unity's own
terrain detail layers through public `TerrainData` API. It takes none of DFU's four terrain slots.
So the port needed nothing from upstream's tree except a build setting.

That setting is the interesting half. The detail renderer draws through three **built-in engine**
shaders — `Hidden/TerrainEngine/Details/Vertexlit`, `Hidden/TerrainEngine/Details/WavingDoublePass`
and `Hidden/TerrainEngine/Details/BillboardWavingDoublePass` (lower-case `l` in `Vertexlit`; the
names were read out of `unity_builtin_extra` with `strings` and confirmed with `Shader.Find` +
`isSupported` in a 6000.3.23f1 Editor run). Nothing in this project references them: no scene holds a
`Terrain`, DFU builds every one at runtime, and no material asset names them. An IL2CPP player build
is therefore free to strip all three, and without the pin the failure mode would be silent — grass
renders as nothing at all, with no error. (The port gates on `Shader.Find` of the three at runtime as
well, so a lost pin reads as `[RealGrass] not available: ...` rather than as bare ground, but the pin
is what keeps them in the build in the first place.) `EnsureAlwaysIncludedShaders` gained the
three names as literals and `ApplyIOSSettings` wrote them into `GraphicsSettings.asset` as
`{fileID: 10500 | 10501 | 10502, guid: 0000000000000000f000000000000000}`.

`RequiredShaderVariants.shadervariants` was deliberately **not** touched, for two reasons: an
Always-Included entry pins a shader with all of its variants, which is the insurance wanted; and a
built-in shader has no project GUID, so an entry there could only name it by a fileID into
`unity_builtin_extra` — a number no test should hard-code and no Unity upgrade should be trusted to
preserve. For the same reason `MobileSelfTest` checks the pin by **object identity** (load the asset,
walk `m_AlwaysIncludedShaders`, compare against `Shader.Find(name)`) rather than by the source-text
grep the CRT and Distant Terrain pins use — those shaders are project assets, so their GUIDs are in
the file; these three are not in the text at all.

Everything else is ours and outside upstream's tree:
`Assets/Scripts/Game/Mobile/Ports/RealGrass/{RealGrass,DensityManager,DetailPrototypesManager,Range}.cs`
(new, 2,633 lines, MIT headers preserved — see `THIRD-PARTY.md`),
`Assets/Scripts/Game/Mobile/MobilePortedMods.cs (+30/-1)` (the launcher entry),
`Assets/Editor/MobileSelfTest.cs (+430/-28)` across the feature's three commits, and
`tools/bundled-mods/mods.json (+12)` with `tools/bundled-mods/manifests/RealGrass.dfmod.json (+13)`.

The launcher entry is the smallest of the family and worth naming for what it does *not* have.
`GrassTitle = "Real Grass"` — the fetched manifest's own `ModTitle`, which the self test compares the
literal against — is appended to `Titles`, so `DefaultOff` starts it off and only a saved choice
turns it on. Its block sits after Distant Terrain's and before the sky is handed back, last of the
immediate `Init`s, so an `Init` that threw here cannot cost the mods before it their start. **There
is no dependency gate**: unlike Biomes, WoD or the terrain ports there is nothing to check, because
this mod takes none of DFU's four terrain slots. It uses the **four-argument** `StartOne`
(`RealGrassPort.Installed`, hint `[RealGrass]`) for the same reason the two terrain ports do — `Init`
declines by logging `[RealGrass] not available: ...` and returning, never by throwing, and the plain
`StartOne` would report "started Real Grass" for a mod that did nothing.

One note on the fetch, because the entry does not read the way the other mods' do. Upstream is a
monorepo with **two** roots — `RealGrass/` for the code and manifest, `RealGrassAssets/` for the art
— and `fetch.py` resolves a manifest's `Files` against `<subdir>/` while reading the manifest itself
from `<subdir>/<manifest>`. No single subdir satisfies both. The entry therefore pairs
`subdir: "RealGrassAssets"` with `manifest_override: "manifests/RealGrass.dfmod.json"`, the
mechanism `DetailedDungeonExteriors` already uses; the mod folder is still named `RealGrass` (that
comes from `name`, not `subdir`), the override keeps upstream's `ModTitle`, version, authors and
GUID, and `strip_code` + `exclude_globs` become guards over the override rather than filters — they
report "0 files" today and exist so that no VMblast `.psd`, mesh, material or `.cs` can ride along
if the override ever grows.

*Rebase risk: LOW, and it is not the usual kind.* There is no hunk for a merge to take theirs on. The
two things that can silently break it are a Unity upgrade or an Editor session **reserialising
`GraphicsSettings.asset`** and dropping the three fileID entries, and an upstream change to
`DaggerfallTerrain`'s promotion event or to `TerrainData`'s detail API — the second is a compile
error in `Ports/RealGrass/`, which is the good failure mode, and the first is caught by the
self-test's object-identity check in the Editor rather than by a black patch of ground on a device.
The port also reads `StreamingWorld.TerrainDistance` (for the live-terrain count in its memory line)
and `terrainData.maxDetailScatterPerRes`; neither is load-bearing — the first falls back to DFU's
shipped 3, and the second only sets the clamp ceiling.
