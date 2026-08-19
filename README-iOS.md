# Daggerfall Unity - iOS touch port

> **PRE-ALPHA.** This is an early, playable build - expect rough edges, missing
> conveniences, and the occasional bug. Core gameplay (exploration, combat, doors,
> menus, saving) is verified working on an iPad Pro 11" (M4), iPadOS 26. Feedback and
> issue reports are very welcome.

A complete touchscreen input layer for [Daggerfall Unity](https://github.com/Interkarma/daggerfall-unity),
making the game playable on iPhone and iPad without a keyboard or mouse.

**You must supply your own Daggerfall game data. It is not included** - see below.

## What this adds

- **Twin-stick controls** - left stick moves, right stick looks; drag anywhere on empty
  screen also looks
- **Swipe-to-swing combat** mapped onto Daggerfall's own directional attack system;
  drawing a weapon enters combat automatically
- **Action buttons** - Activate, Weapon, Spell, Jump, Crouch always visible; Inventory,
  Status, Map, Rest and settings behind one MENU button
- **Direct touch in the classic menus** - tap a button to click it; the original 1996
  windows (inventory, spellbook, travel map, dialogue) were not rebuilt
- **On-screen keyboard** appears automatically for text fields (character name etc.)
- **Metal colour fix** - extends Daggerfall Unity's macOS colour-space correction to iOS,
  fixing washed-out videos, weapon sprites and fonts (same Metal API, same bug)
- **On-device tuning panel** - sensitivity, swipe distance, control size and opacity,
  palm rejection, all live with no rebuild
- **Physical-inch layout** so controls are thumb-sized from an iPhone mini to a 13in iPad
- **Controller support** - hides the touch HUD automatically and hands input back to
  Daggerfall Unity's existing gamepad support
- **Real haptics** via the Taptic Engine (iPhone only; iPad has no motor)

## Engine footprint

Four files, 13 hooks - five of which are one-line extensions of upstream's own macOS
colour fix to the Metal API generally. `WeaponManager`, `PlayerMouseLook` and `PlayerActivate` are
**unmodified** - the design injects into the input channels they already read.

| File | Change |
|---|---|
| `Assets/Scripts/Game/InputManager.cs` | `MousePosition`, new `MouseScroll`, 3x `GetMouseButton*`, 3x `GetBackButton*`, 2 poll calls in `Update()`, `OnGUI` cursor draw, `SetMobileMouseAxes` |
| `Assets/Scripts/Game/UserInterface/BaseScreenComponent.cs` | one line - route the scroll wheel through InputManager |
| `Assets/Scripts/Game/DaggerfallUI.cs` | 3 one-line conditions - take the Metal colour path on iOS, not just macOS |
| `Assets/Scripts/Game/UserInterface/DaggerfallFont.cs` | 2 one-line conditions - same Metal fix for glyph rendering |

The key idea: `mouseX`/`mouseY` in `InputManager` already feed **both** `PlayerMouseLook`
and `WeaponManager.TrackMouseAttack()`. Injecting one channel reproduces PC mouse
behaviour exactly, and Daggerfall's own code does the swipe-direction mapping - including
suppressing camera look mid-swing, which it already did for PC players.

## Requirements

- **Unity 2022.3.21f1** with **iOS Build Support**
  (this project targets the `2022_2_21f1-lts-upgrade` branch; Unity 2019.4 cannot build
  for modern iOS)
- **Xcode** and an Apple ID
- **Your own copy of Daggerfall** - free from Bethesda, or a GOG/Steam copy

## Build

1. Clone this repository.
2. Open in Unity 2022.3.21f1.
3. Set the iOS player settings: IL2CPP, Managed Stripping **Minimal**, Api Compatibility
   **.NET Framework**, ARM64, minimum iOS 13.0, Metal only, landscape orientation only.
   `Tools > Daggerfall Mobile > Apply iOS Player Settings` does all of this.
4. `Tools > Daggerfall Mobile > Build Touch HUD` to construct the on-screen controls.
5. Build to Xcode, then deploy to your device.

`Tools > Daggerfall Mobile > Run Self Test` verifies the input logic headlessly
(31 checks) and exits non-zero on failure.

## Installing game data on the device

The app ships without game data because Daggerfall's assets remain Bethesda's copyright,
freeware download notwithstanding. Upstream Daggerfall Unity does the same.

1. Get Daggerfall (free from Bethesda, or GOG/Steam).
2. Find the `arena2` folder inside the install.
3. Connect your device, open **Finder > device > Files > Daggerfall Unity**, and copy the
   whole `arena2` folder in. The Files app on iOS works too.
4. Relaunch.

You should end up with `arena2/ARCH3D.BSA`, `arena2/BLOCKS.BSA`, `arena2/MAPS.BSA` and the
rest - roughly 512 MB across ~1560 files.

**If your copy is in iCloud Drive, force a full download first.** iCloud placeholders
report the correct file size while containing no data, and the game will fail at world
load in a way that looks like a bug.

## Controls

### The control system

| Input | Does |
|---|---|
| **Left stick** | Move - walk, strafe; full tilt runs |
| **Right stick** | Camera - always and only, in or out of combat |
| **Swipe (weapon drawn)** | **Attack** - swipe direction picks the strike: down = chop, sideways = slash, up = thrust |
| **Drag empty screen (sheathed)** | Camera look (right side of screen) |
| **Hand-and-ring button** | Activate - doors, NPCs, loot (aims from the centre crosshair) |
| **WEAPON / SPELL / JUMP / CROUCH** | Always-visible action row |
| **MENU** | Drawer with Inventory, Status, Map, Rest, and TUNE |
| **TUNE** | Live settings: sensitivity, swipe distance, control size/opacity, layout editor |
| **Hold during videos** | Skip cutscene |
| **Classic menus** | Direct touch - tap buttons; the on-screen keyboard appears for text fields |

Two-handed combat is the intended style: circle with the left thumb, aim with the right
stick, and slash with a left-thumb swipe - the aiming thumb never contaminates the
attack direction. The view holds still for the quarter-second of each strike (classic
Daggerfall behaviour); aim flows between swings.

Everything on screen can be moved, resized, or hidden: **TUNE -> Edit layout**.

Hardware keyboards and game controllers are supported - the touch HUD hides itself
automatically while they're in use and returns at a touch.

**Gamepad:** connect one and the touch HUD hides itself. Defaults are applied as
*secondary* bindings so keyboard bindings are untouched:

| Button | Action | | Button | Action |
|---|---|---|---|---|
| A | Activate | | Back | Inventory |
| B | Jump | | Start | Pause |
| X | Ready Weapon | | L-Stick | Crouch |
| Y | Cast Spell | | R-Stick | Sneak |
| LB | Switch Hand | | RB | Attack |

Unity's legacy joystick numbering is not consistent across controllers, so rebind in
**Settings > Controls > Joystick** if these land wrong.

## First run tuning

Touch feel cannot be calibrated without a real finger, so the defaults are estimates.
Open **TUNE** and adjust **Swipe to attack** and **Look sensitivity** first - they matter
most. Enable `showGestureDebug` on the `MobileInput` object to see the required swipe
distance in pixels.

## Known limitations

- **Mods are not supported.** iOS forbids the runtime code compilation DFU's mod
  system relies on; asset-only mods would need iOS-specific rebuilds. Untested either way.
- **Xcode/Unity pairing.** Unity 2022.3 predates current Xcode releases; the generated
  Xcode project may need manual fixes.
- **Free Apple ID signing expires after 7 days**, after which you re-sign and redeploy.
- iPad has no vibration motor, so haptics are a deliberate no-op there.

## Licence and credits

Daggerfall Unity is MIT licensed, copyright (c) 2009-2023 Daggerfall Workshop - see
`LICENSE`. This touch layer is offered under the same licence.

Daggerfall itself is copyright Bethesda Softworks. No game assets are distributed here.
