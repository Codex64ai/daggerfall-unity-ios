# Non-MIT originals research: 33 mods moving to the gated pack (2026-10-15)

Research only (no code changed). Repo `unity6-upgrade` @ `e1af5ab2f`.

**Sources:**
- Nexus public GraphQL API (`api.nexusmods.com/v2/graphql`; game id 2927 = daggerfallunity) for mod pages, authors, versions and file lists.
- Nexus "preview file contents" JSON (`file-metadata.nexusmods.com/file/nexus-files-s3-meta/2927/<modId>/<file>.json`; it needs a curl-like User-Agent). This preview is missing for uploads made after about mid-2026 that use the new hashed `uri`. That affects only DET 1.3.
- The GitHub API for repo trees, commits and releases.
- `tools/bundled-mods/mods.json` and `release/v0.2.0/MIT-ModPack-ios.zip`.

Nexus HTML pages return 403 (Cloudflare) to curl and WebFetch. The API confirms that every page cited here exists and is published.

**How the recognizer works (from `MobileOriginalMods.cs`; this matters for marker design):**
- `Normalize` keeps only the leaf name, lower-cases it, strips only `.dfmod`, and drops spaces, `-` and `_`. So `QuestList-BC.txt` becomes `questlistbc.txt`: the `.txt`/`.url`/`.json` extension is kept, so markers must include it. Apostrophes, `&` and commas are kept.
- Archive matching is **name only**: it does not check entry sizes against KnownOurs (see D1).
- The Finder scans only `*.zip|*.7z|*.rar|*.dfmod` under `Documents/Mods`. Loose `.txt` quest folders are never scanned (see D2).

**Our file names:**
- v0.2.0 MIT pack (old names): `jh-questpack1.dfmod`, `ublamf - models.dfmod`, `vanilla enhanced - base.dfmod`, `kokey's temperate.dfmod`, `daggerfall expanded textures.dfmod`, and so on.
- New names via `IosName`: `jh-questpack1-ios.dfmod`, `ublamf-models-ios.dfmod`, `vanilla-enhanced-base-ios.dfmod`, `kokey's-temperate-ios.dfmod`, `daggerfall-expanded-textures-ios.dfmod`.

**Author credit for all Jay_H rows:** "Jay_H" is the name in mod titles (e.g. "Jay_H's Battle Creatures"). He is **JehutySpeaks** on Nexus and **JayH2971** on GitHub. Suggested credit: "Jay_H (JehutySpeaks)".

## A) The 33 mods

Abbreviations:
- **QPrepo** = github.com/JayH2971/dfunity-questpacks (no releases, no licence file). We built from commit `0dd7c6f` (2025-12-20), which is repo HEAD.
- **N<id>** = https://www.nexusmods.com/daggerfallunity/mods/<id>
- **Form** = what the current MAIN files contain, per the Nexus content preview (CONFIRMED unless marked otherwise). `Mods/x.dfmod` means a zip containing a `Mods/` folder with that desktop .dfmod. "Loose" means a `QuestPacks/<folder>/*.txt` zip.
- **Collision?** = a desktop original has the SAME leaf name as our v0.2.0 file.

| # | Our file (old -> new) | Original URL | Author | Built from | Latest | Download form (current MAIN files) | Markers (dfmod / archive extra) | Collision? |
|---|---|---|---|---|---|---|---|---|
| 1 | jh-questpack1 -> jh-questpack1-ios | N2 "Daggerfall Unity Quest Pack 1"; QPrepo/QP1 | Jay_H | QPrepo `0dd7c6f` | 2025.11.20b | `Quest Pack 1 dfmod-2-2025-11-20b-*.zip` -> `Mods/quest pack 1.dfmod`; Android zip -> `qp-questpack1.dfmod` (root) | `quest pack 1`, `qp-questpack1` / `QP1Readme.txt`, `JHDBC01.txt` | no |
| 2 | jh-randomlittlequests | N204; QPrepo/Random Little Quests | Jay_H | `0dd7c6f` | 2025.01.15a | `Random Little Quests-204-*.zip` -> `Mods/random little quests.dfmod`; Android -> `qp-random-little-quests.dfmod` | `random little quests`, `qp-random-little-quests` / `QuestList-RLQ.txt` | no |
| 3 | jh-immersionroles | N228 (6 role files); QPrepo/Immersion Roles (8 roles) | Jay_H | `0dd7c6f` | page 1.0; roles 1.2/1.2/0.91/1.0/1.4a/1.0 | 6 separate loose zips: `Headhunter Role-228-1-2-*.zip` and similar -> `QuestPacks/<Role>/QuestList-<x>.txt` + quest .txt. **Dark Brotherhood Initiate and Speaker for the Dead are GitHub-only** | none / `QuestList-Headhunter.txt`, `QuestList-NobleChild.txt`, `QuestList-Outlaw.txt`, `QuestList-CursedM.txt`, `QuestList-EscapedSlave.txt`, `QuestList-Vindicator.txt`, `QuestList-DarkB.txt`, `QuestList-SpeakDead.txt` | no |
| 4 | jh-reputationconsequences | N902 | Jay_H | `0dd7c6f` | 1.0 | `Reputation Consequences-902-1-0-*.zip` -> `Mods/reputation consequences.dfmod` | `reputation consequences` / `JHRCinit.txt` | no |
| 5 | jh-chronicleofthegreatknight | N493 | Jay_H | `0dd7c6f` | 1.03 | `Chronicle of the Great Knight-493-1-03-*.zip` -> `Mods/chronicle of the great knight.dfmod`; Android -> `qp-chronicle-great-knight.dfmod` | `chronicle of the great knight`, `qp-chronicle-great-knight` / `JH_CGK1.txt` | no |
| 6 | jh-thetalecontinues | N913 | Jay_H | `0dd7c6f` | 1.21 | `The Tale Continues-913-1-21-*.zip` -> `Mods/the tale continues.dfmod` + `Quests/S0000001.txt`, `S0000004.txt`, `S0000020.txt` (main-quest overrides) | `the tale continues` / `QuestList-TTC.txt` | no |
| 7 | jh-battlecreatures | N196 | Jay_H | `0dd7c6f` | 1.0 | `Jay_H's Battle Creatures-196-1-00-*.zip` -> loose `QuestPacks/BattleCreatures/QuestList-BC.txt` + 7 .txt; Android -> `qp-battle-creatures.dfmod` | `qp-battle-creatures` / `QuestList-BC.txt`, `battlecreaturesinit.txt` | no |
| 8 | jh-mundanejobs | N356 (marked ABANDONED, file still up) | Jay_H | `0dd7c6f` | 1.13 | `Mundane Jobs-356-1-13-*.zip` -> loose `QuestPacks/Courier/…`, `QuestPacks/Treefeller/…` | none / `QuestList-Courier.txt`, `QuestList-Treefeller.txt` | no |
| 9 | jh-medicalemergency | N897 | Jay_H | `0dd7c6f` | 1.0 | `Medical Emergency dfmod-897-1-0-*.zip` -> `Mods/medical emergency.dfmod` | `medical emergency` / `QuestList-medicalemergency.txt` | no |
| 10 | jh-becomedarkbrotherhood | N917 "Receive a Dark Brotherhood or Thieves Guild Invitation" | Jay_H | `0dd7c6f` | 1.0 | `Receive a Dark Brotherhood Invitation-917-1-0-*.zip` -> `Mods/receive a dark brotherhood invitation.dfmod` | `receive a dark brotherhood invitation` / `QuestList-becomedb.txt` | no |
| 11 | jh-becomethief | N917 | Jay_H | `0dd7c6f` | 1.0 | `Receive a Thieves Guild Invitation-917-1-0-*.zip` -> `Mods/receive a thieves guild invitation.dfmod` | `receive a thieves guild invitation` / `QuestList-becomethieves.txt` | no |
| 12 | jh-becomevampire | N721 "Become a Werewolf or Vampire or Wereboar" | Jay_H | `0dd7c6f` | 1.0 | `Become a Vampire-721-1-0-*.zip` -> `Mods/become a vampire.dfmod`; Android -> `qp-become-a-vampire.dfmod` | `become a vampire`, `qp-become-a-vampire` / `QuestList-becomevampire.txt` | no |
| 13 | jh-becomewereboar | N721 | Jay_H | `0dd7c6f` | 1.0 | `Become a Wereboar-721-*.zip` -> `Mods/become a wereboar.dfmod`; Android `qp-become-a-wereboar.dfmod` | `become a wereboar`, `qp-become-a-wereboar` / `QuestList-becomewereboar.txt` | no |
| 14 | jh-becomewerewolf | N721 | Jay_H | `0dd7c6f` | 1.0 | `Become a Werewolf-721-*.zip` -> `Mods/become a werewolf.dfmod`; Android `qp-become-a-werewolf.dfmod` | `become a werewolf`, `qp-become-a-werewolf` / `QuestList-becomewerewolf.txt` | no |
| 15 | jh-cheatarmory | N899 "The Cheater's Armory" | Jay_H | `0dd7c6f` | 1.0 | `The Cheater's Armory dfmod-899-1-0-*.zip` -> `Mods/the cheater's armory.dfmod` | `the cheater's armory` / `QuestList-cheatarmory.txt` | no |
| 16 | jh-weatheritems | N133 | Jay_H | `0dd7c6f` | 1.03 | `Weather Items-133-1-03-*.zip` -> loose `QuestPacks/Weather-Items/QuestList-WeatherItems.txt`, `WITEMS.txt` | none / `QuestList-WeatherItems.txt`, `WITEMS.txt` | no |
| 17 | jh-randommonsternoises | N271 "Jay_H's Random Monster Noises Mod" | Jay_H | `0dd7c6f` | 1.0 | `Jay_H's Random Monster Noises-271-1-0-*.zip` -> loose `QuestPacks/FunSurpriseMod/QuestList-FunSurpriseMod.txt`, `FunSurpriseMod.txt` | none / `QuestList-FunSurpriseMod.txt`, `FunSurpriseMod.txt` | no |
| 18 | jh-mqrepfix | N898 "Main Quest Reputation Fix" | Jay_H | `0dd7c6f` | 1.0 | `Main Quest Reputation Fix dfmod-898-1-0-*.zip` -> `Mods/main quest reputation fix.dfmod` | `main quest reputation fix` / `QuestList-MQrepfix.txt` | no |
| 19 | jh-ironmanmadnessinfighting | N237 "Ironman Madness" (ABANDONED, files up) | Jay_H | `0dd7c6f` | 1.0 | `Ironman Madness INFIGHTING-237-1-0-*.zip` -> loose `QuestPacks/IronmanMadnessINFIGHTING/QuestList-IronmanMadness.txt`, `JHIronmanMadness.txt` | none / `QuestList-IronmanMadness.txt`, `JHIronmanMadness.txt` | no |
| 20 | jh-ironmanmadnessnoinfighting | N237 | Jay_H | `0dd7c6f` | 1.0 | `Ironman Madness NO INFIGHTING-237-*.zip` -> SAME two leaf names in folder `IronmanMadnessNOINFIGHTING` | same as #19 (leaf names cannot tell the two variants apart) | no |
| 21 | jh-startingdungeonrandomizer | N17; github.com/JayH2971/dfu-starting-dungeon-randomizer | Jay_H | commit `7227630` (2019-12-06) | 1.4 (2019-10-18) | `Starting Dungeon Randomizer-17-1-4-*.zip` -> loose `QuestPacks/READMEdfuRAND.txt` + `QuestPacks/dfu-starting-dungeon-randomizer/QuestList-Rando.txt`, `RANDO1-3.txt` (the repo has the same tree) | none / `QuestList-Rando.txt`, `READMEdfuRAND.txt`, `RANDO1.txt` | no |
| 22 | ublamf - building overrides -> ublamf-building-overrides-ios | N100 "Unofficial Block Location and Model fixes"; src github.com/XJDHDR/DFU_UBLaMF (no releases) | XJDHDR | commit `21ed9c8` = release v2026.04.0 | v2026.04.0 (same) | `UBLaMF - {Windows,MacOS,Linux} version-100-2026-04-0-*.7z` -> `Mods/ublamf - {building overrides, dungeon blocks, locations, map blocks, models, scripting, textures}.dfmod` + `Docs/Unofficial Block, Location and Model Fixes readme.url`. Old v2022.10 -> `Mods/unofficial block, location and model fixes.dfmod` + `Mods/ublamf - map blocks addon.dfmod` | see group UBLAMF | **YES** |
| 23 | ublamf - dungeon blocks | N100 | XJDHDR | v2026.04.0 | same | as #22 | " | **YES** |
| 24 | ublamf - locations | N100 | XJDHDR | v2026.04.0 | same | as #22 | " | **YES** |
| 25 | ublamf - map blocks | N100 | XJDHDR | v2026.04.0 | same | as #22 | " | **YES** |
| 26 | ublamf - models | N100 | XJDHDR | v2026.04.0 | same | as #22 | " | **YES** |
| 27 | ublamf - textures | N100 | XJDHDR | v2026.04.0 | same | as #22 | " | **YES** |
| 28 | vanilla enhanced - base -> vanilla-enhanced-base-ios | N273 "Vanilla Enhanced"; src github.com/drcarademono/vanilla-enhanced (no releases) | Nexus: "Vanilla Enhanced contributors" (uploader carademono); manifest: carademono; mods.json: drcarademono and Kokey | commit `c0c9041` (2025-05-08, HEAD; manifest 3.4.7) | 3.5.0 (2025-05-30) | `Vanilla Enhanced-273-3-5-0-*.7z` (275 MB) and `Vanilla Enhanced - Android-273-3-5-0-*.zip` (366 MB) -> `Mods/vanilla enhanced - {animated people, base, masked roads, snowless swamps and jungles, villagers, villains & monsters}.dfmod` | see group VE | **YES** |
| 29 | vanilla enhanced - masked roads | N273 | carademono & Kokey (manifest) | `c0c9041` (3.4.7) | 3.5.0 | as #28 | " | **YES** |
| 30 | vanilla enhanced - snowless swamps and jungles | N273 | carademono | `c0c9041` (3.4.7) | 3.5.0 | as #28 | " | **YES** |
| 31 | vanilla enhanced - winter tracks | **GitHub only** (not in any current Nexus file) | "Hazelnut & carademono" (manifest) | `c0c9041` (manifest 1.0) | same | GitHub source only: `Vanilla Enhanced - Winter Tracks.dfmod.json` + textures. No desktop .dfmod is distributed | `Vanilla Enhanced - Winter Tracks.dfmod.json` (repo zip) | no published binary |
| 32 | kokey's temperate -> kokey's-temperate-ios | **GitHub only** (no Nexus page found for "Kokey"/"Temperate") | Kokey (manifest ModVersion field literally "Kokey") | `c0c9041` | same | GitHub source only: `Kokey's Temperate.dfmod.json` + `Textures/Kokey's Temperate/*.png` | `Kokey's Temperate.dfmod.json` | no published binary |
| 33 | daggerfall expanded textures -> daggerfall-expanded-textures-ios | N307 | Nexus author "Ninelan-Ruinar" (uploader NinelanRuinar) | Our bundle dated 2026-09-03, from "macOS release, Standard". Nexus 1.3 was uploaded 2026-09-11, so we built from **1.2.1** "DET - Standard for MacOs" (inferred) | 1.3 | 1.3 MAIN: "DET - Standard release", "DET - Standard for MacOs", "DET - Standard for Android" (about 10.4-10.9 MB zips). Inner names **UNCONFIRMED** for 1.3 (no preview). Every earlier version from 0.2.0 to 1.2.1 (all three platforms) = `Mods/daggerfall expanded textures.dfmod`. MISC 0.9.0 LEGACY = `Mods/daggerfall expanded textures legacy.dfmod` | `daggerfall expanded textures`, `daggerfall expanded textures legacy` | **YES** |

Counts: confirmed 32. UNCONFIRMED 1: the DET 1.3 inner name, which is very likely unchanged. The "built from" versions for DET (1.2.1) and for VE vs Nexus (repo 3.4.7, Nexus 3.5.0) are inferred.

Collision list: 6 UBLaMF + VE base, masked roads and snowless swamps + DET = **10 files**. All are fixed by the `-ios` rename plus OldNames/KnownOurs, with the archive caveat in D1. Jay_H has no collision: desktop names are spelled out (`quest pack 1.dfmod`) or start with `qp-` (Android); ours are `jh-*`.

## B) Proposed recognizer rows

Notes that apply to every row:
- **DfmodMinBytes:** Jay_H dfmods are tiny. Our iOS ones are about 2 KB, so the originals are likely similar. Use **1 KB** for the JH rows, 64 KB for UBLaMF, 1 MB for VE and DET.
- **"+repo"** means the GitHub "Download ZIP" of QPrepo (`dfunity-questpacks-main.zip`). It contains every subdir as loose .txt, so it matches every JH row through the archive markers. That is correct: it is the author's own download.
- Archive markers below are the dfmod markers plus the extras listed. Threshold 1 unless stated.

| Key | Unlocks (new names) | DfmodMarkers | Archive extra markers | Notes |
|---|---|---|---|---|
| JH_QP1 | jh-questpack1-ios | quest pack 1, qp-questpack1 | QP1Readme.txt, JHDBC01.txt | |
| JH_RLQ | jh-randomlittlequests-ios | random little quests, qp-random-little-quests | QuestList-RLQ.txt | |
| JH_ROLES | jh-immersionroles-ios | - | the 8 `QuestList-<role>.txt` names (row 3) | Any one role zip unlocks our combined 8-role file (see D4) |
| JH_REPCON | jh-reputationconsequences-ios | reputation consequences | JHRCinit.txt | |
| JH_CGK | jh-chronicleofthegreatknight-ios | chronicle of the great knight, qp-chronicle-great-knight | JH_CGK1.txt | |
| JH_TTC | jh-thetalecontinues-ios | the tale continues | QuestList-TTC.txt | |
| JH_BC | jh-battlecreatures-ios | qp-battle-creatures | QuestList-BC.txt, battlecreaturesinit.txt | |
| JH_MUNDANE | jh-mundanejobs-ios | - | QuestList-Courier.txt, QuestList-Treefeller.txt | |
| JH_MEDEM | jh-medicalemergency-ios | medical emergency | QuestList-medicalemergency.txt | |
| JH_INVITE_DB | jh-becomedarkbrotherhood-ios | receive a dark brotherhood invitation | QuestList-becomedb.txt | Same Nexus page as TG; separate files, so separate rows |
| JH_INVITE_TG | jh-becomethief-ios | receive a thieves guild invitation | QuestList-becomethieves.txt | |
| JH_VAMPIRE | jh-becomevampire-ios | become a vampire, qp-become-a-vampire | QuestList-becomevampire.txt | |
| JH_WEREBOAR | jh-becomewereboar-ios | become a wereboar, qp-become-a-wereboar | QuestList-becomewereboar.txt | |
| JH_WEREWOLF | jh-becomewerewolf-ios | become a werewolf, qp-become-a-werewolf | QuestList-becomewerewolf.txt | |
| JH_CHEAT | jh-cheatarmory-ios | the cheater's armory | QuestList-cheatarmory.txt | |
| JH_WEATHER | jh-weatheritems-ios | - | QuestList-WeatherItems.txt, WITEMS.txt | |
| JH_NOISES | jh-randommonsternoises-ios | - | QuestList-FunSurpriseMod.txt, FunSurpriseMod.txt | |
| JH_MQREP | jh-mqrepfix-ios | main quest reputation fix | QuestList-MQrepfix.txt | |
| JH_IRONMAN | jh-ironmanmadnessinfighting-ios, jh-ironmanmadnessnoinfighting-ios | - | QuestList-IronmanMadness.txt, JHIronmanMadness.txt | One row: the variants are indistinguishable by leaf name |
| JH_SDR | jh-startingdungeonrandomizer-ios | - | QuestList-Rando.txt, READMEdfuRAND.txt, RANDO1.txt | Nexus zip and the repo share the same tree |
| UBLAMF | the 6 `ublamf-*-ios` | all 7 `ublamf - <module>` names + `unofficial block, location and model fixes` + `ublamf - map blocks addon` | **ONLY** `ublamf - scripting`, `unofficial block, location and model fixes`, `ublamf - map blocks addon`, `Unofficial Block, Location and Model Fixes readme.url` | Keep the 6 names we ship OUT of the archive markers, or our own v0.2.0 MIT zip self-unlocks (D1). OldNames = the 6 `ublamf - x.dfmod` |
| VE | vanilla-enhanced-base-ios, -masked-roads-ios, -snowless-swamps-and-jungles-ios (+ winter tracks and kokey's temperate? see D5) | all 6 `vanilla enhanced - <module>` names | **ONLY** `vanilla enhanced - animated people`, `vanilla enhanced - villagers`, `vanilla enhanced - villains & monsters`, plus the repo-zip names `Vanilla Enhanced - Base.dfmod.json`, `Vanilla Enhanced - Winter Tracks.dfmod.json`, `Kokey's Temperate.dfmod.json` | Same self-unlock reason. OldNames = our 5 v0.2.0 names |
| DET | daggerfall-expanded-textures-ios | daggerfall expanded textures, daggerfall expanded textures legacy | `daggerfall expanded textures legacy` only | The real archive's only entry has the same name as the file in our v0.2.0 MIT zip, so a name-only archive match is impossible. Needs D1 (size-aware archive matching) or must accept "extract the .dfmod" only. OldNames = `daggerfall expanded textures.dfmod` (KnownOurs size 13,580,519) |

## C) Credits and links for the other shipped mods

| Mod (our file) | Official page | Author credit (as presented) |
|---|---|---|
| Fixed Dungeon Exteriors (fixeddungeonexteriors) | N225 | Cliffworms |
| Varied Wealthy Homes (variedwealthyhomes) | N507 | Cliffworms |
| Aquatic Sprites (underwatersprites) | N276 | Cliffworms |
| Smaller Main Quest Dungeons (smaller main quest dungeons) | github.com/Cliffworms/SmallerMQDungeons (no Nexus page found) | Cliffworms |
| Leveling Inspiration (leveling inspiration) | N183 | Cliffworms - RebornZA |
| Skyrim's Adventures | N223 | Cliffworms |
| Jobs of the Thieves Guild (jobsofthethievesguild) | N200 | Cliffworms |
| Arena's Adventures | N185 | Cliffworms |
| Town Greetings of the Iliac Bay | N194 | Cliffworms |
| Rumors of the Iliac Bay | N199 | Cliffworms |
| Detailed Dungeon Exteriors | github.com/Cliffworms/DetailedDungeonExteriors (no Nexus page found) | Cliffworms |
| Better Ambience | N139 | Joshua Steinhauer (joshcamas) |
| Immersive Footsteps | N706 | Kirk O (Magicono43) |
| Dynamic Music | N425 | numidium3rd (GitHub numidium) |
| First-Person-Lighting | N719 | DunnyOfPenwick |
| Roleplay and Realism (built-in) | N16 | Hazelnut |
| Roleplay and Realism - Items (built-in) | N61 | Hazelnut and Ralzar |
| Climates and Calories (built-in) | N49 | Ralzar |

Already-gated rows in `release/v0.2.0/release-notes-draft.md`:
- The 4 GitHub links return 200.
- The 5 Nexus links (5, 664, 372, 20, 1284) return 403 to scripts (Cloudflare), but the API confirms they are published. Nexus names and authors: DREAM = "King of Worms - KoW" (v2.0); DREAM - SKY = King of Worms (1.2); DEX = "Kab and Kamer" (1.3.4); Real Grass = "Uncanny Valley - TheLacus - VMblast" (2.11); Distant Terrain of the WoD = MaoDeVaca (3.69).
- All links are good.

## D) Open questions and UNCONFIRMED items

1. **Self-unlock hole (important).** `MatchArchive` checks entry names only. `release/v0.2.0/MIT-ModPack-ios.zip` holds `Mods/ublamf - *.dfmod` (6), `vanilla enhanced - base/masked roads/snowless swamps and jungles.dfmod` and `daggerfall expanded textures.dfmod`. Those are exactly the desktop original names. If that zip sits in `Documents/Mods`, it would unlock UBLAMF, VE and DET once rows exist.
   - For UBLaMF and VE, the marker choice in B avoids this.
   - DET cannot be fixed by markers. Recommended fix: in the archive path, skip entries whose (leaf, size) is in KnownOurs. SharpCompress `IArchiveEntry.Size` is available from a header-only listing.
   - `tools/known_ours.py` DEFAULT_DIRS must also cover the v0.2.0 MIT-pack contents. Today `mitpack-dl` holds only the zip, and KnownOurs has none of the 33 mods yet.
2. **Loose quest originals are never scanned.** 9 of 21 Jay_H mods ship on Nexus only as loose `QuestPacks/*.txt` zips: Immersion Roles, Mundane Jobs, Weather Items, Random Monster Noises, Ironman ×2, Starting Dungeon Randomizer, and Battle Creatures (desktop form). Kept as a zip, they work with the markers above. Extracted, they are invisible to the Finder (it only scans `.zip/.7z/.rar/.dfmod`). Also, loose quest .txt files are platform-neutral, so a player who extracts them to the quest folder may already be running the original on iOS without our conversion. Decide: tell players to "keep the zip", or extend the Finder to `QuestList-*.txt`.
3. **Android builds as originals.** Jay_H and VE publish Android `.dfmod`s (`qp-*.dfmod`, the VE Android zip). They are the authors' own downloads, so I included them as markers. Confirm this is acceptable.
4. **Immersion Roles granularity.** Our single file bundles 8 roles. Nexus has 6 separate role zips, and 2 roles (Dark Brotherhood Initiate, Speaker for the Dead) are GitHub-only. Proposed: any one role zip, or the repo zip, unlocks the whole file. The alternative is to require all 6 (threshold 6), which is strict and unfriendly.
5. **VE Winter Tracks and Kokey's Temperate have no binary original.** They exist only in the GitHub source (repo zip; size not checked, likely very large). Options:
   - (a) the Nexus VE archive unlocks all 5 files (pragmatic; same authors);
   - (b) they need the repo zip;
   - (c) a separate VE_GITHUB row.

   Kokey has no Nexus page of his own (searches for "Kokey" and "Temperate" found nothing).
6. **DET 1.3 inner name UNCONFIRMED.** The Nexus preview is unavailable for new-style uploads. Every earlier release used `daggerfall expanded textures.dfmod` (legacy: `… legacy.dfmod`). Ikram could confirm by downloading. "Built from 1.2.1" is inferred from our bundle date (2026-09-03, before 1.3's upload on 2026-09-11).
7. **Nexus page-level permissions** (redistribution text) could not be read (Cloudflare). This research covers only identity and links, not licence text.
8. **Ironman variants:** one row unlocks both. They are mutually exclusive in-game, as already noted in our descriptions.
