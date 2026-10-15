# Install guide: Daggerfall Unity on iPad and iPhone

For people who have never sideloaded an app or used the Files app. Go one step at a time. **You
should see:** lines let you check you are on track.

All downloads are on the [latest release](https://github.com/Codex64ai/daggerfall-unity-ios/releases)
page, in its **Assets** list.

## Words you'll see

- **Sideload**: install an app that is not from the App Store, using your own Apple ID.
- **Apple ID**: the email and password you use for the App Store and iCloud.
- **.ipa**: the file the app comes in. A sideloading tool installs it for you.
- **.zip**: many files packed into one. Tapping it in the Files app unpacks it into a folder.
- **Files app**: the blue folder app on every iPhone and iPad.
- **Long-press**: hold your finger on something for about a second until a menu pops up.
- **Assets**: the list of downloads at the bottom of the release page. Tap **Assets** if it is folded shut.
- **The app's folder**: **On My iPad > Daggerfall Unity** in the Files app (**On My iPhone** on a phone).
- **Mods folder**: the **Mods** folder inside the app's folder.
- **.dfmod**: one mod, as a single file.
- **arena2**: the one folder from Daggerfall that the app needs.
- **Original mod**: the mod exactly as its author shares it on their own page.

Below says "iPad". On an iPhone it works the same way.

## What you need

1. An iPad or iPhone with **iOS 15 or later**.
2. A computer (Windows or Mac), to copy the game files (Part B) and for Way 2 in Part A.
3. A cable to plug the iPad into the computer.
4. Your own copy of **Daggerfall** (Steam or GOG). It is not included.
5. An Apple ID (the free kind is fine).
6. Free space: about 1 GB to play. About 4 GB more for the Non-MIT pack, plus room for an original
   download while it unlocks (DREAM is several GB; you can delete it afterwards).

## Part A. Put the app on your iPad

**Already use SideStore or AltStore?** Use Way 1. **Never done this before?** Use Way 2.

A free Apple ID lets a sideloaded app run for **7 days** (Apple's rule). SideStore and AltStore
renew it for you. With Sideloadly, plug in and click **Start** again every 7 days; your saves are kept.

### Way 1: SideStore or AltStore

1. On the iPad, open **SideStore** or **AltStore**.
2. Go to the **Sources** tab.
3. Tap **+**, paste this address, tap **Add**: `https://codex64ai.github.io/daggerfall-unity-ios/altstore.json`
   - **You should see:** "Daggerfall Unity" in the store.
4. Tap the button next to **Daggerfall Unity** (**FREE** or **GET**) and wait.
   - **You should see:** the Daggerfall Unity icon on your home screen.
5. Go to Part B. New versions show up in the store as updates.

### Way 2: Sideloadly (computer + cable)

1. On your computer, download Sideloadly from [sideloadly.io](https://sideloadly.io/).
2. Install it and open it. (If it says your Windows PC needs iTunes, install that too.)
3. Download the `.ipa` file from **Assets** on the [latest release](https://github.com/Codex64ai/daggerfall-unity-ios/releases). It lands in **Downloads**.
4. Plug the iPad into the computer.
5. If the iPad asks **Trust This Computer?**, tap **Trust** and type your passcode.
   - **You should see:** your iPad's name in the top box of Sideloadly.
6. Drag the `.ipa` from **Downloads** onto the Sideloadly window.
7. Type your Apple ID email in the **Apple account** box.
8. Click **Start**.
9. Type your Apple ID password when asked, and any code Apple sends you.
10. Wait until Sideloadly says it is done.
    - **You should see:** the Daggerfall Unity icon on your iPad.

**Let the iPad trust the app** (once, after Way 2):

11. On the iPad, open **Settings > General > VPN & Device Management**.
12. Under **Developer App**, tap your Apple ID email.
13. Tap **Trust**, then **Trust** again.
14. **iOS 16 or later only:** open **Settings > Privacy & Security**.
    - **You should see:** a **Developer Mode** row near the bottom. If it isn't there, open the app once (an alert appears) and look again.
15. Tap **Developer Mode**, switch it on, tap **Restart**. After the restart, tap **Turn On** and type your passcode.
    - **You should see:** tapping the icon opens the app.

## Part B. Put your Daggerfall game files in place

1. Open the app once, then go back to the home screen. (This creates the app's folder.)
   - **You should see:** in Files, **Browse > On My iPad > Daggerfall Unity** with `PUT-ARENA2-FOLDER-HERE.txt` in it.
2. On your computer, install **The Elder Scrolls II: Daggerfall** from Steam or GOG.
3. Find the **arena2** folder in the install.
   - **Steam:** right-click the game > **Manage** > **Browse local files**. **arena2** is in there, maybe a folder or two deeper.
   - **Otherwise:** search your computer for **arena2**.
4. Copy the whole **arena2** folder (not just the files inside) to the iPad. Pick one way:
   - **Mac:** plug the iPad in. In **Finder**, click your iPad in the sidebar, open the **Files** tab, and drag **arena2** onto **Daggerfall Unity**.
   - **Windows:** plug the iPad in. In iTunes (or Apple Devices), open your iPad, go to **File Sharing**, pick **Daggerfall Unity**, and drag **arena2** from File Explorer into the list on the right. (**Add...** can't pick a folder.)
   - **No cable:** get **arena2** into Files (iCloud Drive or a USB drive). Tap **Select**, tap **arena2**, tap **Move**, choose **On My iPad > Daggerfall Unity**, tap **Move**.
5. From **iCloud Drive**? Make sure no file shows a small cloud icon. Files that are not fully downloaded make the game fail when the world loads.
6. In Files, open **On My iPad > Daggerfall Unity > arena2**.
   - **You should see:** about 1,560 files (about 512 MB), such as **ARCH3D.BSA** and **MAPS.BSA**.
7. Open the app.
   - **You should see:** the start screen with **Play** at the bottom and **Mods** at the bottom left.

You can play now: tap **Play**. Mods are optional (Parts C to F).

## Part C. Add the MIT Mod Pack (15 free mods)

1. On the iPad, open the [latest release](https://github.com/Codex64ai/daggerfall-unity-ios/releases) in **Safari**.
2. Under **Assets**, tap `MIT-ModPack-ios.zip`. If Safari asks, tap **Download**.
   - **You should see:** a download arrow near the top of Safari.
3. Open **Files** and tap **Recents**. The zip is at the top. (Not there? **Browse > Downloads**, under **iCloud Drive** or **On My iPad**.)
4. Tap `MIT-ModPack-ios.zip` **once**.
   - **You should see:** a new folder **MIT-ModPack-ios** (in **Downloads**, next to the zip).
5. Open **MIT-ModPack-ios**, then its **Mods** folder.
   - **You should see:** 15 `.dfmod` files and a **Licenses** folder.
6. Tap **Select** (top right; on iPhone maybe inside **...**), then **Select All**.
7. Tap **Move** (folder icon at the bottom; maybe inside **More**).
8. Tap **On My iPad > Daggerfall Unity > Mods**, then **Move** (or **Copy**).
9. Open the app, tap **Mods**, tap **Refresh**.
   - **You should see:** the 15 mods in the list.
   - Not there? Close the app fully (swipe up from the bottom and hold, then swipe the app away; with a Home button, press it twice and swipe up) and open it again.

## Part D. Add the Non-MIT Mod Pack (optional)

These mods are by permission, CC BY-NC-SA, or have no license. Each one stays locked until you
add its original (Part E). The files are big: use Wi-Fi.

1. In Safari, download `NonMIT-ModPack-ios-part1.zip` and `NonMIT-ModPack-ios-part2.zip` from **Assets**.
2. In **Files** (**Recents** or **Downloads**), tap `NonMIT-ModPack-ios-part1.zip` **once**.
   - **You should see:** a folder **NonMIT-ModPack-ios-part1**.
3. Open it, then its **Mods** folder.
4. **Select > Select All > Move > On My iPad > Daggerfall Unity > Mods > Move**.
5. Go back to **NonMIT-ModPack-ios-part1**. Long-press `pack-index.json`, tap **Move**, choose **On My iPad > Daggerfall Unity > Mods**, tap **Move**.
6. Do steps 2 to 5 again for **part2**. If Files says `pack-index.json` exists, tap **Replace**.
7. Delete the zips and unpacked folders from **Downloads** to get the space back.
8. Open the app, tap **Mods**, tap **Refresh**.
   - **You should see:** grey rows at the bottom: **Needs <mod name> - tap for the download page**. That is normal.

**Only if you use DREAM** (these three are our zips, so unpacking them is right):

9. Download `DREAM-music-ios.zip`, `DREAM-movies-ios.zip` and `DREAM-extras-ios.zip` from **Assets**.
10. Tap `DREAM-music-ios.zip` **once** and open the **dream-music-ogg** folder it makes. **Select > Select All > Move > On My iPad > Daggerfall Unity > Sound > Move**.
11. Tap `DREAM-movies-ios.zip` **once** and open **dream-movies-ios**. Move everything to **Daggerfall Unity > Movies**.
12. Tap `DREAM-extras-ios.zip` **once** and open **SpellIcons**. Move everything to **Daggerfall Unity > SpellIcons**.
13. Go back one folder, open **Presets**, and move everything to **Daggerfall Unity > Presets**.

They stay unused until your DREAM original is seen (Part E).

## Part E. Unlock a Non-MIT mod

Do this once for each mod you want.

1. Find the mod in the Non-MIT table of the [latest release](https://github.com/Codex64ai/daggerfall-unity-ios/releases) notes and tap its link. (Or in **Mods**, tap a grey **Needs ...** row, then tap it again.)
2. Download the original:
   - **Nexus:** sign in (free account), tap **Files**, find the main file, tap **Manual download**, then **Slow download**.
   - **GitHub:** tap the green **Code** button, then **Download ZIP**.
3. Wait for the download to finish. Keep Safari open for big files.
   - **You should see:** Safari's download arrow shows it is done.
4. In **Files**, tap **Recents** (or **Browse > Downloads**).
5. **Do not tap the downloaded file.** The app needs it exactly as downloaded. (Jay_H quest packs are the exception: an unpacked folder also works.) The app only reads the names inside; it never loads the original itself.
6. Long-press the download, tap **Move**, choose **On My iPad > Daggerfall Unity > Mods**, tap **Move**.
   - **You should see:** the download (for example `Random Little Quests-204-1-15a.zip`) in **Mods**, name unchanged.
7. Open the app, tap **Mods**, tap **Refresh**. (Still grey? Close the app fully and open it again.)
   - **Locked:** a dark grey row, **Needs <mod name> - tap for the download page**.
   - **Unlocked:** the grey row is gone and the mod's own name is in the list. (World of Daggerfall entries start **off**; see Part F.)
8. Optional: delete the original from **Mods**. The app remembers it, and the mod keeps working.

**Special cases**

- **Immersion Roles:** any one role download unlocks all eight roles.
- **Ironman Madness:** one download unlocks both variants. Switch on only one.
- **Vanilla Enhanced:** the Nexus download (or the GitHub **Download ZIP**) unlocks all five files, including Winter Tracks and Kokey's Temperate.
- **Jay_H pages with several files** (*Dark Brotherhood or Thieves Guild Invitation*, *Werewolf or Vampire or Wereboar*): each file unlocks only its own mod.
- **Many Jay_H packs at once:** the **Download ZIP** of [JayH2971/dfunity-questpacks](https://github.com/JayH2971/dfunity-questpacks) unlocks most of them.
- **Desktop `.dfmod` files** also count as the original. Put them in **Mods** the same way.

## Part F. Turn mods on or off

1. Open the app and tap **Mods**.
   - **You should see:** a list of mods. Light grey names are on, darker grey are off.
2. Tap a mod's name, then tick or untick **Enabled** on the right. (**ALL ON** switches everything on.)
3. The built-in survival mods (Roleplay & Realism, Roleplay & Realism: Items, Climates & Calories) start **off**. Turn them on here if you want them.
4. Tap **Save and Close**. (**Options** goes back *without* saving.)

## Part G. Play

1. On the start screen, tap **Play**.
2. If two mods change the same thing, the game asks once which one wins. Tap one, or **None of these**.
   - **You should see:** Daggerfall's main menu. Enjoy.

## If something went wrong

| What you see | What to do |
|---|---|
| **Untrusted Developer**, **Developer Mode Required**, or nothing happens. | Do the trust steps (Part A, Way 2, steps 11-15). |
| After about a week the app won't open. | The 7-day signature ran out. Don't delete the app (your saves are in it). Sideloadly: plug in and click **Start** again. SideStore/AltStore: open it and refresh. |
| No **Daggerfall Unity** in Files. | Open the app once (Part B, step 1). Look under **Browse > On My iPad**, not iCloud Drive. |
| The game won't start, or closes while the world loads. | **arena2** is missing, incomplete or misplaced. It must be directly in **On My iPad > Daggerfall Unity**, about 1,560 files. From iCloud? Download it fully first. |
| The app closes later, with lots of mods on. | Out of memory. Switch off big mods (DREAM, World of Daggerfall). |
| The **Mods** list is empty, or a mod is missing. | The files are still in **Downloads** or the zip. Move the `.dfmod` files into **Mods** (Part C, steps 6-8), then tap **Refresh**. |
| A grey **Needs ...** row stays. | Check the download is in **Mods**, finished, and not renamed. Tap **Refresh**, or close and reopen the app. |
| A grey row says **Couldn't read <file>**. | The download is broken. Delete it from **Mods**, download again, move it in again. |
| I tapped the download and it became a folder. | The zip is usually still next to the folder: move *that* into **Mods**. (Jay_H packs: the folder works too.) Otherwise download it again. |
| Mods from an earlier test pack are now grey. | Unlock them (Part E), or delete the old files (names not ending in `-ios`). |
| Something else. | Ask on the [Issues page](https://github.com/Codex64ai/daggerfall-unity-ios/issues). Attach **Player.log** from **On My iPad > Daggerfall Unity**. |
