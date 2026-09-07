# Survival mods port (Roleplay & Realism, RR Items, Climates & Calories) — design

Date: 2026-09-07. Approved approach: **A — compile the mods' C# into the app, ship each mod's data as
an in-app bundle, bridge Travel Options to the port's real travel.** Decisions taken with Ikram: port all
three faithfully; rewrite the unlicensed tavern window ourselves; three independent switches, off by
default; **every mod switch lives only in the launcher's MODS window, never in play.**

## What ships

| Mod | Author / licence | Code | Data bundle (in-app, `StreamingAssets/Mods`) |
|---|---|---|---|
| Roleplay and Realism 1.8 | Hazelnut, MIT (file headers) | 7 `.cs`, ~84 KB | 23 PNG, 3 quests + QuestList, WorldData, `modsettings.json`, `modpresets.json`, `.csv`, `indexButtons.txt` |
| Roleplay and Realism – Items 1.3 | Hazelnut & Ralzar, MIT (headers) | 15 `.cs`, ~104 KB | 280 PNG + 236 XML, `ItemTemplates.json`, `modsettings.json`, `.csv` |
| Climates & Calories 1.7.0 | Ralzar, MIT (headers) | 8 `.cs`, ~235 KB (TavernWindow.cs **excluded**) | 18 PNG (archives 532-539, 50_7, 67_10), `ItemTemplates.json`; **tent model, tent materials and the two tavern PNGs excluded** |

Pinned sources: `github.com/ajrb/dfunity-mods` @ `0af2ec99b5ad7e6722ddc9ce284987d4d6fa262d` (2024-07-21),
`github.com/Ralzar81/Climates-Calories` @ `c33a04f8d200615b01d09987262b1ed318e7615a` (2023-10-20).

Excluded on licence grounds: `TavernWindow.cs` (no header) is replaced by `MobileTavernWindow` written
from the engine's `DaggerfallTavernWindow` and C&C's public functions; `blb_tent.fbx` + `41606.prefab`
(author unstated) are dropped and the engine's own model 41606 is used for the camp tent.

## Architecture

```
Assets/Scripts/Game/Mobile/Ports/
  RoleplayRealism/        7 .cs   (namespace kept: RoleplayRealism)
  RoleplayRealismItems/  15 .cs   (namespace kept: RoleplayRealism)
  ClimatesCalories/       8 .cs   (namespace kept: ClimatesCalories) + MobileTavernWindow.cs (ours)
Assets/Scripts/Game/Mobile/MobilePortedMods.cs     bootstrap + dependency gate
Assets/Scripts/Game/Mobile/MobileTravelOptionsBridge.cs   answers "TravelOptions" mod messages
tools/bundled-mods/mods.json                        three new entries, kind "builtin" (data bundle only)
```

**Bootstrap.** `MobilePortedMods` runs from `ModManager.OnRegisterBuiltInMods`-time hooks: after
ModManager has loaded bundles (`StateManager.StateTypes.Start` phase, same point where DFU would call
`[Invoke]` loaders), for each of the three titles in dependency order it finds the bundle entry
(`ModManager.Instance.GetMod(title)`); if `Enabled`, calls the mod's own `Init(new InitParams(mod,
index, count))`. `[Invoke]` attributes are removed from the ported files (DFU only honours them on
compiled-from-bundle code). Nothing else in the mod files changes except: `SendModMessage("TravelOptions",
…)` stays as written (the bridge answers it), `UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Tavern,
typeof(TavernWindow))` becomes `typeof(MobileTavernWindow)`, and the tent prefab lookup falls back to the
engine mesh.

**Dependency gate.** RR Items requires RR (its manifest says optional; C&C requires both). Rule: if C&C
is enabled and RR or RR Items is disabled, C&C is switched off, `WriteModSettings()` is called and the
entry's description gains "Needs Roleplay & Realism and its Items switched on." No half-running.

**Data bundles.** Built by the existing pack pipeline (`fetch.py` + `MobileBuildSetup.BuildBundledMods`)
into `StreamingAssets/Mods`, shipped inside the app (not in the pack zip). `fetch.py` gains
`"strip_code": true` (drop `.cs` from `Files` instead of refusing) and `"builtin": true` (excluded from
`pack.py`). Manifests keep their titles so `GetMod(title)`, `GetSettings()`, `GetAsset()` and save data
work exactly as on desktop.

**Travel Options bridge.** `MobileTravelOptionsBridge` registers a message receiver on a built-in entry
titled `TravelOptions` (hidden from the MODS list: `RegisterBuiltInMod` with `Hidden` semantics = not
user-switchable; if the Mods window cannot hide it, it is listed as "Travel Options bridge (always on)"
and its checkbox is ignored). Answers:

| message | answer |
|---|---|
| `isTravelActive` | callback(`MobileJourneyPilot.Active`) |
| `pauseTravel` | `MobileJourneyController.Instance.Stop(reason)` if active (C&C pauses for camp/hunger prompts; resuming is the player's tap, as today) |
| `noStopForUIWindow` (window) | remember the window so the journey does not treat it as an interrupt |
| `showMessage` (text) | `MobileJourneyController` HUD text if active, else `DaggerfallUI.AddHUDText` |
| `isPathFollowing` / `isFollowingRoad` | callback(cautious mode and currently on the road network) |

Vanilla fast travel is untouched. C&C's `GetMod("TediousTravel")` etc. stay and return null.

**Tavern window (ours).** `MobileTavernWindow : DaggerfallTavernWindow` adds Food and Drinks buttons.
Food list = C&C's regional food table (public `ItemsFood` data and `Hunger` functions); buying calls
`Hunger.EatFood(calories)` equivalents exposed by C&C (`ClimateCalories` public statics); Drinks list =
ale/beer/mead/wine with price by region, drinking calls the public drunk functions. Room renting is
vanilla. No text or layout copied from Ralzar's file.

**Launcher-only switches.** `MobileSettingsPanel` loses its Mods section; `MobileMods` descriptions drop
"also switchable in play"; the Mods window (setup wizard) is the single place. Roads/Real travel/Summer
start keep their `MobileMods` flags (still read at startup).

## Saves
C&C uses `mod.SaveDataInterface` keyed by mod title; unchanged, so PC saves carry state. Disabled mod +
old save: DFU ignores the mod's save block. RR Items template indices unchanged.

## iOS hazards checklist (each verified in the plan)
No `Shader.Find`, reflection, `Type.GetType`, `Activator` in the three code bases (grep proved empty).
No readable-texture needs beyond what `EnsureReadable` now covers. Tent FBX dropped. `EnhancedRiding`
and other optional `GetMod` lookups return null safely. `PlayerActivate.RegisterCustomActivation(mod, …)`
needs a real `Mod` instance: the bundle entry provides it.

## Testing
Self-tests: bridge answers with pilot on/off; dependency gate truth table; tavern list building (pure).
Simulator: debug start + all three on: hunger/thirst tick (HUD text), camp equip deploys tent (engine
mesh), rest in camp, tavern Food/Drinks lists open, real travel stops on `pauseTravel`. Device build.

## Docs
`THIRD-PARTY.md` (three entries, commits, exclusions), `README-iOS.md` (Survival section + launcher-only
rule), `UPSTREAM-PATCHES.md` (settings panel change, bridge), memory handoff.
