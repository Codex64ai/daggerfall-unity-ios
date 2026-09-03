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

### Travel popup — `UserInterfaceWindows/DaggerfallTravelPopUp.cs` (+3)
`RouteOceanPixels` exposes the calculator's ocean-pixel count so real travel can refuse sea
routes and fall back to classic fast travel. The journey call site in this file predates
this ledger entry (see the MOBILE comment there). *Rebase risk: LOW.*

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
