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
- **Action buttons** - Activate, Weapon, Spell, Jump, Crouch always visible; Pause
  (save/load), Inventory, Status, Map, Rest and settings behind one MENU button
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
- **Mouse and trackpad support** - a native plugin, because Unity's iOS player has none:
  proper pointer lock during play, hover-driven cursor in menus, hold-right-and-drag
  attacks, and your own mouse keybinds honoured (iPadOS 14+)
- **Real haptics** via the Taptic Engine (iPhone only; iPad has no motor)

## Engine footprint

Four files, 13 hooks - five of which are one-line extensions of upstream's own macOS
colour fix to the Metal API generally. `WeaponManager`, `PlayerMouseLook` and `PlayerActivate` are
**unmodified** - the design injects into the input channels they already read.

| File | Change |
|---|---|
| `Assets/Scripts/Game/InputManager.cs` | `MousePosition`, new `MouseScroll`, 3x `GetMouseButton*`, 3x `GetBackButton*`, 2 poll calls in `Update()`, `OnGUI` cursor draw (skipped while a real pointer is active), `SetMobileMouseAxes` |
| `Assets/Scripts/Game/UserInterface/BaseScreenComponent.cs` | one line - route the scroll wheel through InputManager |
| `Assets/Scripts/Game/DaggerfallUI.cs` | 3 one-line conditions - take the Metal colour path on iOS, not just macOS |
| `Assets/Scripts/Game/UserInterface/DaggerfallFont.cs` | 2 one-line conditions - same Metal fix for glyph rendering |

The key idea: `mouseX`/`mouseY` in `InputManager` already feed **both** `PlayerMouseLook`
and `WeaponManager.TrackMouseAttack()`. Injecting one channel reproduces PC mouse
behaviour exactly, and Daggerfall's own code does the swipe-direction mapping - including
suppressing camera look mid-swing, which it already did for PC players.

## Requirements

- **Unity 6000.3.23f1** (Unity 6) with **iOS Build Support**
  (this is the `unity6-upgrade` line; the earlier `ios-touch-port` line built with 2022.3)
- **Xcode** and an Apple ID
- **Your own copy of Daggerfall** - free from Bethesda, or a GOG/Steam copy

## Build

1. Clone this repository.
2. Open in Unity 6000.3.23f1.
3. Set the iOS player settings: IL2CPP, Managed Stripping **Minimal**, Api Compatibility
   **.NET Framework**, ARM64, minimum iOS 15.0 (Unity 6's floor), Metal only, landscape only.
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
3. Copy the whole `arena2` folder into the app:
   - **With a Mac**: connect the device, open **Finder > device > Files > Daggerfall
     Unity**, and drag `arena2` in. (iTunes file sharing on Windows.)
   - **Without a computer**: get `arena2` into the Files app on the iPad itself (iCloud
     Drive, Google Drive/Dropbox, a USB-C drive, or unzip a copy downloaded on-device),
     then copy it to **On My iPad > Daggerfall Unity**.
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
| **MENU** | Drawer with Pause (save/load/exit), Inventory, Sheet, Status, Map, Automap, Rest, and Transport (the legs icon: foot / horse / cart / ship) |
| **Pause -> MOBILE SETTINGS** | The port's own settings, in four sections: **Input** (how you play: Auto / Touch / Keyboard & mouse / Controller; click to attack for mouse and pad, tap to attack for touch, cursor mode; sensitivity, swipe distance), **HUD** (control size, opacity, layout editor), **Mods** (roads & real travel), **Advanced** (diagnostics). Reachable by touch, mouse, keyboard or pad alike |
| **Hold during videos** | Skip cutscene |
| **Classic menus** | Direct touch - tap buttons; the on-screen keyboard appears for text fields |

Two-handed combat is the intended style: circle with the left thumb, aim with the right
stick, and slash with a left-thumb swipe - the aiming thumb never contaminates the
attack direction. The view holds still for the quarter-second of each strike (classic
Daggerfall behaviour); aim flows between swings.

Everything on screen can be moved, resized, or hidden individually: **Pause -> Mobile
Settings -> HUD -> Edit layout**, tap a control, then drag it, scale it with **-**/**+**, or **Hide/Show** it.
Every icon is its own control - the action row and the menu drawer are not fixed blocks.
**Reset all** returns to the shipped defaults.

### Mouse and trackpad

Plug in (or pair) a mouse, or use a Magic Keyboard's trackpad, and the touch HUD stands
down the moment the pointer moves - exactly as it does for a gamepad. A finger on the
glass brings it back. Playing is then classic Daggerfall:

| Input | Does |
|---|---|
| **Move the pointer** | Camera look; the pointer is **locked** and hidden during play, so it never runs off the edge into the system UI |
| **Left button** | Activate (or whatever `Mouse0` is bound to in your KeyBinds.txt) |
| **Right button + drag** | Attack - the drag direction picks the strike, as on PC; hold to draw a bow, release to loose |
| **Menus / pause** | Pointer unlocks and the system arrow comes back; click, drag scrollbars, and scroll with the wheel or two fingers |
| **Keyboard** | Works alongside - a Magic Keyboard is both at once |

Why a plugin: Unity's iOS runtime never asks iPadOS for a mouse. A pointer reaches a
Unity game only as touches when a button is clicked, hover is invisible, and
`Cursor.lockState` does nothing - which is why, before this, the camera stopped at the
screen edge and there was no way to swing. `DFMobilePointer.mm` reads the mouse through
Apple's GameController framework (`GCMouse`) and installs the `prefersPointerLocked`
override Unity's view controller lacks. DFU's own **Mouse sensitivity** and **Invert**
settings apply unchanged (raw counts are scaled to Unity's mouse-axis units). If the
vertical axis is ever wrong on a particular mouse, **Mobile Settings -> Input -> Invert pointer Y** flips
it without a rebuild. **Mobile Settings -> Advanced -> Show diagnostics** prints the plugin state (connected,
lock requested vs. granted, buttons, last delta).

Pointer lock is granted by iPadOS only to a full-screen, foreground scene - Stage Manager
windows and Split View will get look-by-hover up to the screen edge instead.


A trap for anyone touching this code: on iPadOS a trackpad click arrives in Unity as a touch
whose `TouchType` is reported as **Direct** (Unity maps `UITouchTypeIndirectPointer` onto it),
so `TouchType` cannot tell a finger from a click. The port uses the native plugin's button
state and a short grace window after pointer activity instead.

### Hardware keyboard

Keys are read through the same native plugin (`GCKeyboard`), not Unity's own path. Unity's iOS
player handles hardware keyboards with `UIKeyCommand` - a menu-shortcut mechanism with no
key-up event and auto-repeat timing - which made walking start late and stutter while touch
and gamepad did not. Any key the plugin's table does not cover falls back to Unity's reading.
Typing stands touch down; touching the glass returns it. Return toggles Daggerfall's cursor
mode as on PC; touch clears that mode the moment it takes over, and **Mobile Settings -> Input ->
Cursor mode** toggles it deliberately without a keyboard.

Attacks: **Click to attack (mouse & controller)** is on by default - the right button or pad
button attacks on the press, no drag, whatever the launcher's *Weapon swing mode* says. Touch
swipes by default; **Tap to attack (touch)** makes a quick tap in combat the attack instead.

### Two layouts, one per HUD mode

The touch layout is saved **separately for fullscreen and for classic mode** (see below).
Arrange each however suits it; switching between them restores each one's own
arrangement, and **Reset all** only resets the mode you are currently in.

### Classic interface bar

Daggerfall's original bottom HUD - **Settings > Interface > Large HUD**, docked - is fully
touch-driven here. Tap any icon on the bar: inventory, map, rest, options, spellbook, use
magic item, transport, sheathe weapon, the interaction-mode icon (cycles
steal/talk/grab/info), or the portrait for the character sheet. **This works with a
controller connected too**, when the rest of the touch overlay has stood down.

Taps are distinguished from drags, so sliding a finger across the bar to look around does
not press anything, and a tap on the bar never grabs a joystick.

With the bar docked, the touch controls lift above it automatically and the ones the bar
already provides start hidden. That is only a **default** - anything can be shown or
hidden in either mode. The overlay's MAP button jumps straight to the travel map, for
instance, so it is reasonable to keep it alongside the bar's own map icon.

Classic mode ships a deliberately minimal default layout: compact sticks above the bar,
activate/crouch/jump under the right thumb, menu beside them, settings and travel map top
right. The bar does the rest.

Hardware keyboards and game controllers are supported - the touch HUD hides itself
automatically while they're in use and returns at a touch.

**Gamepad:** connect one and the touch HUD hides itself. Two full layers are mapped -
a base layer, and a second layer while the **left trigger (LT)** is held. Everything is
applied as *secondary* bindings, so keyboard bindings are untouched.

Base layer:

| Input | Action | | Input | Action |
|---|---|---|---|---|
| A | Activate | | D-Up | Character sheet |
| X | Ready weapon | | D-Down | Status |
| RT | Swing weapon | | D-Left | Automap |
| B | Cast spell | | D-Right | Travel map |
| Y | Jump | | Start | Pause |
| RB | Switch hand | | L3 (stick click) | Crouch |
| LB | Autorun | | R3 (stick click) | Transport |

In menus, the classic UI pointer uses the face buttons:

| Input | Menu action |
|---|---|
| A | Select (left click) |
| B | Back / close window |
| X | Right click |
| Y | Middle click |

Hold **LT** for the second layer:

| Input | Action | | Input | Action |
|---|---|---|---|---|
| LT + Y | Inventory | | LT + D-Up | Steal mode |
| LT + A | Recast spell | | LT + D-Down | Grab mode |
| LT + B | Use magic item | | LT + D-Left | Info mode |
| LT + X | Notebook | | LT + D-Right | Talk mode |
| LT + RB | Logbook | | LT + Start | Quicksave |
| LT + LB | Run | | LT + RT | Rest |
| LT + L3 | Sneak | | LT + R3 | Quickload |

While LT is held, the base action of a button that has an LT variant does *not* also
fire - LT+Y opens the inventory without jumping. That comes from Daggerfall Unity's own
combo-keybind system rather than anything bolted on here, so combos also show up in
**Settings > Controls > Joystick** and can be rebound like any other binding.

**Select / View is not mapped.** On the Xbox controller this was measured against, that
button reports as `JoystickButton0` - and so does Start, which also reports its own
`JoystickButton16`. Binding button 0 would therefore either fire Select's action every time
you paused, or bind the phantom button iPadOS pulses during touches. Rest and Quickload
sit on `LT + RT` and `LT + R3` instead. If your controller reports Select as something
distinct, you can bind it yourself in **Settings > Controls > Joystick**.

Rebind anything that lands wrong in **Settings > Controls > Joystick**.

**If your controller maps wrongly:** Unity's legacy joystick numbering - and especially
its trigger and d-pad *axis* numbering - varies by controller model and by iOS version,
so a controller this port has never seen may report different numbers. Turn on
**Pause -> Mobile Settings -> Advanced -> Controller probe overlay** (the pause menu is
reachable with the pad, so this can be done with it already connected). The probe names each control
in turn, records what Unity actually reported, and ends on a summary page - screenshot it
and open an issue, and the defaults can be corrected for that controller.

## Interaction modes and locked doors

Classic Daggerfall picks what a click does from a mode - grab, info, talk or steal - which on
a keyboard is a modifier and on a touchscreen is nothing at all. The HUD therefore carries a
mode button that cycles **Grab -> Info -> Talk -> Steal**, and the current mode is drawn on it.

**Picking a lock is Steal mode.** Cycle to Steal and tap the door with the action button.
Tapping a locked door in any other mode now says so, in a Daggerfall-styled message, instead
of failing silently - which was previously indistinguishable from the touch input being broken.

## Real travel

Instead of fading to black and arriving, the character can walk to the destination while time
runs fast. Turn on **Mobile Settings -> Mods -> Roads & real travel**. It is off by default: it is a large change
to how the game is played, and it costs continuous terrain streaming for the length of a trip.

Travel is started the normal way, from the travel map and its popup. Cautious or reckless
speed, transport and lodging all keep their vanilla meaning and are read from that window. A
bar then appears at the top of the screen:

```
Travelling to Daggerfall            17 Frostfall, 4:12 pm
[ - ]  [ 20x ]  [ + ]            [ MAP ]  [ STOP ]
```

- **Speed** steps through 1, 5, 10, 20, 30, 50 - and 100 or 200 on **reckless** travel only.
  Cautious tops out at 50x, which is roughly the fastest the world can still be seen going by;
  reckless trades that away along with the safety.
- **MAP** opens the travel map without ending the journey.
- **STOP** ends it. The destination is kept, so opening the travel map afterwards offers to
  resume rather than making you find the same place again.

What interrupts a journey:

| | |
|---|---|
| passing a settlement | offered once per town, village or tavern on the way |
| nightfall | offered once a night, if you chose to camp out rather than take inns |
| enemies | cautious travel tries to slip past on Running or Stealth; reckless takes the fight |
| low health or fatigue | cautious travel stops at 20%, rather than letting you collapse |
| disease | stops and shows the health status box |

**Journeys make you tired, and fast travel does not.** Cautious fast travel restores health,
fatigue and magicka on arrival, so it never charges for the walk. Real travel gets no such
refund - the walking actually happened. Expect to rest on a long trip.

**Speed varies as you go.** Time compression multiplies physical movement, not just the clock,
so the player can outrun terrain streaming and walk into ground that has not been painted yet.
A journey therefore yields to the world: compression drops while terrain is building and
recovers when it settles. The bar reports what is actually happening - the speed in effect,
whether terrain is still building, and measured ground speed - which is diagnostic and will be
removed once the feature settles.

Trips with nothing to walk to fall back to classic fast travel: a destination with no location
on its map pixel, or a sea route.

### Following roads

Roads come with the same switch - **Roads & real travel** in the launcher's **Mods** window (listed
like any mod; set there, no restart needed) or **Mobile Settings -> Mods** in play (the terrain half
then needs an app restart, and the Mods section says so until then). A **cautious** journey works out a route along the
road and track network and follows it; a **reckless** one heads straight at the destination across
open country. The bar says *Following the road* when it is doing so.

Speed follows your transport: 50x on foot, 150x on a horse or cart, 200x by ship, and a journey
sets out at that ceiling (Slower/Faster on the bar work under it). Cautious journeys suppress
Daggerfall's random wilderness encounters (enemies already about still stop you); reckless ones do
not. Settlements you only pass through are crossed to the far gate rather than navigated. At
nightfall the popup's sleep option is honoured: *camp out* stops and opens the Rest screen, then resumes on its own;
*inns* takes a room in the town you are in (5 gold) and sleeps until dawn without stopping, or
walks on to the next town after dark and sleeps there.

Both ends of a journey are matched to the nearest path within about twenty map pixels, so the
character walks overland at the start and finish and takes the road in between. A route is only
used if the road actually saves walking - a short road reached by a long trudge across country
is a worse journey than simply setting off, so that case is left as direct travel.

Not every destination has a road to it, and the network is not fully connected. Direct travel is
the normal outcome in that case, not a failure.

Roads themselves are **off by default and need a restart** to take effect: terrain texturing is
consulted as each tile is built, so switching it mid-session would leave already-generated
terrain painted the old way and roads would stop at an invisible line.

Roads are ported from **Basic Roads** by Hazelnut, MIT licensed, including the authored path
network. The route-finding is this port's own - Basic Roads draws the roads, and Travel Options
follows a road the player is already standing on, but neither works out a route to a chosen
destination.

Derived from **Tedious Travel** by TheNewBob / Jedidia, MIT licensed. Reworked for this port:
no reflection into engine internals, no fork of the travel map window (14 lines of engine
change rather than 1,958), touch-sized controls, a camera hand-off so a journey and a thumb
cannot fight for the view, and the terrain throttle.

## First run tuning

Touch feel cannot be calibrated without a real finger, so the defaults are estimates.
Open **Pause -> Mobile Settings -> Input** and adjust **Swipe to attack** and **Look sensitivity** first - they matter
most. Enable `showGestureDebug` on the `MobileInput` object to see the required swipe
distance in pixels.

## Mods and loose files

Partly supported, and the boundary is sharp. Everything below was measured on device
rather than inferred.

Drop content into the app's **Documents** folder (Finder > your device > Files >
Daggerfall Unity, the same place `arena2` goes). The folders are created for you on first
launch, with a note explaining each one. Anything you add takes precedence over the copy
inside the app, and anything you leave out falls back to it - so partial packs are fine.

| Folder | Content | Status |
|---|---|---|
| `Textures/` | loose `.png`, named like `180_0-0.png` | works |
| `Textures/Img/` | loose `.png` for UI images | works |
| `Sound/` | loose `.wav` sound effects | works |
| `Quests/` | quest scripts as plain `.txt` | works |
| `Books/` | loose book text | works |
| `WorldData/` | loose location / block `.json` | works |
| `Sound/` (`.ogg`) | replacement music | first play uses the original, then swaps |
| `Mods/` | `.dfmod` packages **built for iOS** | works |

**What cannot work, ever: mods containing C# code.** iOS compiles ahead of time, so there
is no way to execute mod code that was not built into the app, and Apple forbids
downloading executable code. On device the mod's scripts are skipped with a warning in the
log and the rest of the mod loads normally, so a script mod's textures and sounds still
apply - but anything its code did will not happen. This rules out most popular gameplay
mods - Roleplay Realism, Travel Options, Archaeologists Guild, Basic Roads and Roleplay
Realism: Items all use a C# entry point.

**What cannot work as distributed: `.dfmod` packages from Nexus.** Asset bundles are built
per platform, and upstream's Mod Builder targets Windows, macOS and Linux; this fork adds
an iOS target. A macOS-built bundle is refused by iOS, so a Nexus mod has to be rebuilt
against an iOS target - either from the mod's original source assets, or by unpacking the
desktop bundle and repacking it, which this fork's converter does in one command. The
converter recovers textures, sounds and text assets and nothing else, and its limits are
worth reading before you rely on it: see [Converting a desktop `.dfmod`](#converting-a-desktop-dfmod).

**Music replacement is deliberately delayed by one play.** A replacement `.ogg` is decoded
in the background while the original track plays, and takes over the next time that song
starts. Handing over a still-loading clip would leave the game waiting on it forever, so
every failure here falls back to the original music rather than to silence.

Loose textures import uncompressed, because the runtime PNG loader cannot compress. A
large texture pack will use considerably more memory on iOS than it does on desktop.

## Installing mods

Mods load from the app's `Documents/Mods` folder - put `.dfmod` files there with the
Files app (On My iPad > DFU Test > Mods), the same way as `arena2`. New mods start
enabled; manage them from the launcher's MODS window.

A small pilot mod ships in this repo at `Assets/Game/Mods/IOSPilot/`, used to prove the
build and load path end to end. The Unity editor loads it as a virtual mod, so a fresh
clone of this fork shows its orange/checker test art on the start menu. Delete
`Assets/Game/Mods/IOSPilot/` (or untick "iOS Pilot" in the editor's MODS window) to get
the vanilla menu back.

Two iOS-specific rules:

- **A `.dfmod` must be built for iOS.** Bundles from Nexus are Windows/Linux/Mac builds
  and will not load. Mods have to be rebuilt from their source assets with the Daggerfall
  Unity Mod Builder with the iOS target ticked (this fork's Mod Builder has it), or with
  the headless builder: `-executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModBuilder.BuildFromEnv`.
  If all you have is the desktop bundle - which is all most Nexus mods ship - the
  converter below unpacks one and rebuilds it in a single step.
- **Asset mods only.** Mods that ship C# scripts need a JIT compiler, which iOS forbids;
  their scripts are skipped (assets still load). Texture and sound packs work; model
  replacements are not covered by the headless builder, which does not run the GUI Mod
  Builder's prefab serialization pass.

### Converting a desktop `.dfmod`

One mod per run, from a checkout of this fork with the Unity editor installed:

```sh
env DFU_MOD_IN="$HOME/Downloads/dream - sound.dfmod" DFU_MOD_OUT="$HOME/dev/dfu-mods" \
/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath ~/dev/daggerfall-unity \
  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModExtractor.ConvertFromEnv \
  -logFile /tmp/convert.log
```

`DFU_MOD_IN` is required; `DFU_MOD_OUT` defaults to `~/dev/dfu-mods` and
`DFU_MOD_TARGETS` to `iOS`. `DFU_MOD_AUDIO_TIMEOUT` (default 10) and `DFU_MOD_TIMEOUT`
(default 14400) are the watchdog's seconds-per-clip and seconds-per-run caps, and
`DFU_MOD_SWEEP_MB` (default 256, `0` disables) is the memory-sweep budget described below. The rebuilt
bundle lands in `$DFU_MOD_OUT/iOS/`, ready to copy to the device.

**No `-quit` and no `-nographics`**, and neither is an oversight.

- **`-nographics`**: bundle textures are compressed and non-readable, so decoding them
  needs a real graphics device, and the converter refuses rather than writing grey squares.
- **`-quit`**: some audio clips only become readable once Unity's main loop has run, so
  the converter hands control back between steps - and `-quit` kills the process before
  the first frame of that happens. It would convert **nothing** and still exit 0, so the
  converter refuses `-quit` outright rather than letting that happen. It ends the process
  itself when the work is done.

Exit codes, so a loop over a mods folder stops on failure: **0** a bundle was written,
**1** a failure - including a conversion that saved nothing, which never gets a bundle -
and **2** the watchdog gave up.

Extracted assets go to `Assets/Game/Mods/Converted/`, which is gitignored - never commit
somebody else's mod.

Read the log. Every run ends with a summary line naming what was extracted, what was
skipped and what was extracted-but-renamed; a skip means that asset is **absent from
the bundle you are about to install**, which is otherwise something you would find out
from a silent game. Known limits, in the order they bite:

- **Music usually will not convert; sound effects do.** A bundle stores an `AudioClip` as
  decoded samples, and `AudioClip.GetData` only reads samples of a clip the author
  imported as `DecompressOnLoad`. That is Unity's default, so sound-effect packs convert
  fine - DREAM 2026's sound module is **340 of 340 clips, 0 skipped, in 33 seconds**
  (10.1MB out, from 1100 seconds of audio); its `hud & menu` texture module is **332 of
  332 assets in 32 seconds** (92MB in, 23MB out). Music is the part an author *does* configure,
  and `CompressedInMemory` or `Streaming` clips are unreachable through this route -
  DREAM's music module is 81 clips and this converter gets none of them, so it **fails
  with exit 1 and writes no bundle** rather than handing you a `.dfmod` that installs and
  plays nothing. Those have to come from the module's source audio, from a desktop rebuild
  with the clips set to `DecompressOnLoad`, or from a bundle reader outside Unity; loose
  `.ogg` files dropped in `Documents/Sound` are picked up regardless of how they were
  produced.
- **A slow-looking conversion is usually audio waiting, and it is bounded.** Clips with
  *Preload Audio Data* off arrive with no samples, and if they also have *Load In
  Background* the load only finishes when Unity's main loop runs - which is the whole
  reason there is no `-quit`. 35 of DREAM's 340 clips needed that, 34 of them with a wait.
  A clip that never loads is abandoned after `DFU_MOD_AUDIO_TIMEOUT` and counted as
  `AudioClip(async)`; if you see that key in a summary, the converter was not given the
  main loop back.
- **Large texture packs need a machine with plenty of RAM, and this is the real limit.**
  Measured on DREAM's `hud & menu` module - 92MB in, 330 textures, many 1920x1200 - peak
  resident ran **1.4-1.8GB against a ~1.0GB idle editor**, and the summary line reports
  that module as holding **476MB of asset memory** - about 5.2x its own file size. On the
  ~1.7GB texture module that ratio projects to roughly **9GB of asset memory** on top of
  the editor, which will not fit on a 16GB machine without splitting the module. Convert
  the big ones on the largest machine you have, one at a time, and watch memory rather
  than assuming it fits.

  There is a periodic memory sweep (`DFU_MOD_SWEEP_MB`, default 256, `0` disables) that
  asks Unity to reclaim released assets once that many megabytes have gone by. **Measured,
  it does not help at this size**: peaks were 1438 and 1769MB with it off, 1746MB at a
  256MB budget (1 sweep) and 1789MB at a 32MB budget (13 sweeps) - all inside the same
  run-to-run band, with wall clock identical at 32s throughout. So it is neither a win nor
  a cost here, and the peak on a module this size is an early transient rather than
  accumulation. It is left on because accumulation is the only term that grows with the
  module and the largest pack is ~19x this one - but that is an extrapolation, not a
  result. Read the "holding NNNMB of asset memory" figure in the summary to judge whether
  it could matter for a given module.
- **Textures and audio change file extension.** Textures are re-encoded as `.png` and
  clips as `.wav`, which moves a texture's runtime lookup name with it (DFU keys on the
  short name *with* extension for textures, extensionless for audio). The summary counts
  every rewrite.
- **Not supported at all:** video (`VideoClip`), prefabs and model replacements. Those are
  counted in the summary and left out of the rebuilt bundle, and the rest of the mod
  converts around them.
- **A mod carrying script code converts its assets; the code rides along and never runs.**
  iOS has no JIT, so DFU skips mod script compilation entirely at runtime - the assets
  load and the code does not execute. Which is the desirable outcome, and it is what
  happens; the three shapes differ only in whether you get a bundle at all:
  - **Source-script mods convert, exit 0.** DFU's own Mod Builder ships C# as `.cs.txt`
    text assets, so the rebuild's script guard (which matches `.cs` and `.dll.bytes`)
    never fires on them. The script text is carried inside the converted bundle, inert.
  - **Precompiled `.dll.bytes` mods are the one shape refused outright.** The rebuild
    throws on that manifest entry, so there is no bundle and the exit code is non-zero.
    Repackage those by hand without the assembly entry if you want the art.
  - **A bundle asset actually named `.cs` is refused during extraction**, counted as
    `code-file-refused`, and the rest of the mod still converts. That refusal is about
    this machine rather than about iOS: the extraction root lives inside `Assets/`, so
    writing one would hand a stranger's source to Unity's compiler mid-conversion.

## Diagnostics

Two small logs are written into the app's Documents folder, alongside `arena2`. Both are
plain text and safe to delete.

- `session-log.csv` - one row per 30 seconds: frame time, fps, battery, managed memory
  and where you were. Useful for answering "does it stay healthy over a long session";
  sustained frame-time growth at a steady battery drain is what thermal throttling looks
  like from inside the app (iOS does not report thermal state to Unity).
- `controller-unknown-buttons.txt` - only appears if a gamepad sends a button this port
  does not recognise. If your controller has a button that does nothing, this file will
  name it. Sending it in is the fastest way to get that controller supported properly.

## Known limitations

- **Hardware-tested on one device.** Everything above is verified on an iPad Pro 11" (M4),
  iPadOS 26, with a Magic Keyboard and one Bluetooth controller. Untested: iPhone hardware
  (the physical-inch layout is designed for it but has never run on one), other controller
  models, older iPadOS versions, and other Xcode/Unity 6000.3 pairings.
- **Journeys do not sail.** Real travel walks. A destination pixel with no location on it
  (open sea, empty wilderness) falls back to classic fast travel automatically - but a real
  destination across open water is walked toward in a straight line, over the water. For
  overseas trips, use classic fast travel (switch Real travel off for that trip, or travel
  recklessly and expect wet feet).
- **Free Apple ID signing expires after 7 days**, after which you re-sign and redeploy.
- iPad has no vibration motor, so haptics are a deliberate no-op there.
- Mouse/trackpad support needs iPadOS 14 (`GCMouse`); on 13 the pointer still works as
  click-touches only. Pointer lock is advisory - iPadOS grants it only to a full-screen
  foreground scene.

## Licence and credits

Daggerfall Unity is MIT licensed, copyright (c) 2009-2023 Daggerfall Workshop - see
`LICENSE`. This touch layer is offered under the same licence.

Two MIT-licensed works are compiled into this port, with their headers intact - see
`THIRD-PARTY.md`:

- **Basic Roads** by Hazelnut (copyright (c) 2020) - road and track terrain texturing and the
  authored path network (`Assets/Scripts/Game/Mobile/BasicRoadsTexturing.cs`,
  `Assets/Resources/BasicRoads/`). Used with the author's direct permission.
- **Tedious Travel** by TheNewBob / Jedidia (copyright (c) 2018) - the origin of real travel
  (`MobileJourneyController.cs`, `MobileJourneyPilot.cs`, `MobileJourneyWindow.cs`), reworked
  for this port.

Daggerfall itself is copyright Bethesda Softworks. No game assets are distributed here.
