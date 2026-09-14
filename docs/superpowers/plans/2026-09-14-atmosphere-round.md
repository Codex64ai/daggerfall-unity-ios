# Atmosphere Round Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or executing-plans.

**Goal:** Five optional MIT audio/light mods compiled in (default off) plus sky haze, reverb zones, weather-driven grass wind and lightning flash.
**Spec:** `docs/superpowers/specs/2026-09-14-atmosphere-round-design.md`.

## Global Constraints
- As the DEX plan (branch, trailers, ONE Unity via `ps -Ao comm | grep -c "MacOS/Unity$"` == 0, never `pgrep -f`, kill nothing, no source edits during another agent's Unity run, MobileSelfTest.cs one agent at a time, self-test full app path no -nographics, TOKEN-LEAN, revert churn, files by name, do not push).
- Bundle restore after builtin builds: 53x4 from `~/dev/dfu-mods/astc-backup/` (+ readme pair) - count rises by one per new bundle; refresh the backup after each ApplyAll that adds bundles.
- Workspace `.superpowers/sdd/2026-09-14-atmosphere-round/` (ledger progress.md; use .txt if .md is refused).

### Task 1: Sky haze (native)
Files: Ports/DynamicSkies/BLBSkybox.cs (horizon colour + horizon fog parameter), Ports/DistantTerrain (fog colour push), the stock sky draw (DaggerfallSky.cs - MOBILE gradient), SettingsManager/defaults.ini (`SkyHaze`), MobileSettingsPanel row, MobileSelfTest. Fog colour from the horizon colour each frame (both skies); haze band thickness = DistantFogStrength; 0 % clean. Sim screenshots 0/100/200 with Dynamic Skies on and off. Commit `Sky haze: fog colour from the sky horizon; horizon haze band follows the fog dial`.

### Task 2: Reverb, weather grass wind, lightning (native)
Files: new `Assets/Scripts/Game/Mobile/MobileAmbience.cs` (reverb + lightning), Ports/RealGrass/RealGrass.cs (weather multiplier on the wind dials), SettingsManager/defaults.ini (`AudioReverb`, `GrassWindFollowsWeather`, `LightningFlash`), panel rows, self-tests (pure multiplier table, delay range, preset map). Sim: `weather 3` -> flash log + thunder delay log + grass wind line; dungeon entry -> reverb preset log. Commit `Ambience: reverb zones, weather-driven grass wind, lightning flash with delayed thunder`.

### Task 3: Better Ambience + Immersive Footsteps (ports)
Per mod: mods.json entry (repo/commit/subdir/manifest, strip_code, MIT licence text verbatim), `MobileModPackAudioRules` importer (Vorbis; SFX compressed-in-memory mono; beds streaming), fetch/--check/unittest, port under Ports/<Mod>/ with header + `[Invoke]` removed + Init/Installed, launcher entries default off, self-tests (titles == manifests, no [Invoke], audio rules), bundle build (unscoped ReimportPacks + ApplyAll), inspect (clip count, formats, MB), backup refresh, upload. Commit per mod.

### Task 4: Dynamic Music + Dynamic Ambience + First-Person Lighting (ports)
Same recipe (numidium/dfu-mods two subdirs; DunnyOfPenwick repo). Dynamic Music: confirm it drives `SongManager`/`DaggerfallSongPlayer` through public API in 1.1.1; any keybinds -> panel rows or dropped. Commit per mod.

### Task 5: Simulator pass, docs, device ipa
One sim run per the spec's verification list (builtin build; bundles in the container; each mod on); THIRD-PARTY.md (5 MIT entries), UPSTREAM-PATCHES.md (engine edits: DaggerfallSky gradient, any audio hooks), README-iOS sections (each mod: what it does, cost, its settings; the four switches). `DFU-Test-unity6-atmosphere.ipa` per the fog2 recipe (bundle id pinned), upload, install if the iPad is available, copy the new bundles into `Documents/Mods`.
