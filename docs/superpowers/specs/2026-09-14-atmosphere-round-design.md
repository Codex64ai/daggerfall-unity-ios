# Atmosphere round: five optional audio/light mods + four native touches

Date: 2026-09-14. Approved (Ikram: "Do all and if it's a mod it's an optional add on people can add or leave."). Fable plans; Opus builds.
Research: ~/daggerfall-mobile/mod-inventory-github.md rows 96 (Better Ambience), 116-117 (Dynamic Ambience, Dynamic Music), 121 (First-Person
Lighting), 130 (Immersive Footsteps). All MIT.

## Mods (each: code compiled in under `Ports/<Mod>/`, data as its own bundle, launcher entry default OFF via `MobilePortedMods.Titles`,
4-arg `StartOne`, mod settings honoured from the bundle's modsettings.json, no gate between them - the player mixes freely)
1. **Better Ambience** (joshcamas/daggerfall-unity-mods `BetterAmbience/`, MIT): location/weather ambient beds, 15 cs + 51 WAV (8.3 MB).
2. **Immersive Footsteps** (magicono43/DFU-Mod_Immersive-Footsteps, MIT header): surface footsteps + armour rustle, 2 cs + 210 clips (723 KB).
3. **Dynamic Music** (numidium/dfu-mods `DynamicMusic/`, MIT): context-driven song selection with cross-fades; vanilla songs, plus its own
   clips only if the repo ships them.
4. **Dynamic Ambience** (numidium/dfu-mods `DynamicAmbience/`, MIT): context ambient sound; overlaps Better Ambience - documented, both optional.
5. **First-Person Lighting** (DunnyOfPenwick/First-Person-Lighting, MIT): player light affects hands/weapon; dungeon darkness reads correctly.
Audio import for iOS: Vorbis compressed (quality ~0.5), `Load type` Compressed in memory for short SFX / Streaming for beds > 10 s, force mono
for SFX, no preload for beds; a `MobileModPackAudioRules` importer (versioned) alongside the texture rules; bundles named without spaces.
Each port keeps its upstream MIT header; every edit `// MOBILE:`; `[Invoke]` removed; ModManager-only APIs (GetSettings, GetAsset) resolved
through the bundle mod by title the way the other strip_code ports do. Touch: any keybind the mod adds gets a row in the mobile panel or is
dropped (documented).

## Native touches (settings in `[Enhancements]`, rows in the mobile panel; all default ON except where stated)
A. **Sky haze** (`SkyHaze` bool, on): fog colour = the sky's horizon colour every frame (Dynamic Skies exposes/derives it; stock sky: sample
   the sky image's horizon row once per sky frame), and a horizon haze band on the sky itself whose thickness follows `DistantFogStrength`
   (Dynamic Skies: drive its horizon-fog parameter; stock sky: a bottom-of-sky gradient toward the fog colour in the sky draw). 0 % = clean sky.
B. **Reverb zones** (`AudioReverb` bool, on): Unity `AudioReverbPreset` per space - dungeon (StoneCorridor/Cave by block type if cheap, else
   one dungeon preset), building interior (Room), exterior (Off/Plain) - switched on PlayerEnterExit transitions; applies to the AudioListener.
C. **Weather-driven grass wind** (`GrassWindFollowsWeather` bool, on): Real Grass wind speed/strength scale with weather (sunny 1.0, overcast 1.2,
   rain 1.6, storm/thunder 2.2, snow 0.8, fog 0.6) on top of the player's dials, applied on weather change to every live terrain.
D. **Lightning** (`LightningFlash` bool, on): during thunder weather, a brief full-screen white flash (post-presentation, HUD-safe) followed by
   the thunder clip after a delay proportional to a random distance (0.3-4 s); uses the existing thunder sound; rate limited.

## Verification
Self-tests per item (constants, settings clamps, launcher titles == manifest titles, importer rules, no [Invoke]); one simulator pass at the
end: each mod started (`[PortedMods] started <T>` x5), a footstep line on grass vs stone, an ambience bed line by weather, music track change on
dungeon entry, reverb preset log on entering a dungeon, grass wind line on `weather 3`, lightning flash log on `weather 3`; sky haze screenshot
at 0/100/200 % with Dynamic Skies on and off; no red errors. Device ipa `DFU-Test-unity6-atmosphere.ipa`; bundles pushed to the container.

## Out of scope
New music packs, DREAM audio, weather particle rework, Handpainted/3D model mods.
