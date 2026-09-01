It's an offline item and enemy randomizer for DS3 which modifies the static files loaded on game startup. The item randomizer and enemy randomizer parts can be enabled or disabled separately.

When item randomizer is enabled, the following items are supported for randomization:

- Items found in the world, including key items
- Enemy and boss drops, including key items
- Shop items
- Path of the Dragon gesture
- Items given by NPCs
- Character starting weapons and armor
- And more!

There are also difficulty settings. The most important setting is a bias slider, which can place desirable items completely randomly, or in difficult and unfair locations behind long item chains, or somewhere in between. There are also sequence break settings - in the most punishing variant, it is possible for Twin Princes to drop Small Lothric Banner or Gael to drop Small Doll. And Cinders of a Lord can be potentially anywhere.

In enemy randomizer, game progression things that apply to enemies will apply to their replacements instead. Killing a mimic's replacement will give you the mimic's item. Killing Dancer's replacement will allow you to enter Lothric Castle.

If you have issues installing the mod or would like to provide feedback or playtest, you can join the discord server at https://discord.gg/QArcYud (also for Fog Gate Randomizer, Sekiro Randomizer, and hopefully Elden Ring Randomizer too)

For a more dynamic item randomizer experience, see Yui's [DS3 Item Randomizer and Auto-Equip mod](https://www.nexusmods.com/darksouls3/mods/241). For an alternate enemy randomizer, see GodFilm's [Dark Souls 3 Enemy Randomizer](https://www.nexusmods.com/darksouls3/mods/484). They are compatible with the enemy and item parts of this randomizer, respectively.

## Installation

As of v0.4, randomizer comes with a built-in game launcher based on Mod Engine 2.1 which is compatible with the latest version of DS3 (1.15.2) and other mods like Yui's [DS3 Seamless Co-op](https://www.nexusmods.com/darksouls3/mods/1895). Other installation approaches are possible if the built-in launcher doesn't work, but require familiarity with alternate launchers. Either way, read the instructions carefully to understand the requirements.

### 1. Go offline

This is critical to avoid getting soft-banned. You can select "Go Offline" from Steam's menu, or else go to "System > Network settings" in-game to select Play Offline.

Additionally/alternatively, download [DS3 Seamless Co-op](https://www.nexusmods.com/darksouls3/mods/1895) which makes the game offline and uses an alternate save file. See below for how to use it with randomizer (do *not* use the Seamless Co-op launch exe).

### 2. Download and extract

Download the mod's zip file and extract the entire zip contents to somewhere on your local disk. It does not have to be in the DS3 game directory. Make sure to:

- Explicitly click "Extract here" using a program like 7-Zip and launch DS3Randomizer.exe from a real directory on your disk, not from within the zip file.
- Don't download the mod to a synced filesystem like OneDrive, as required files can appear missing from the mod's perspective.

If you get an error about framework versions running DS3Randomizer.exe, Windows will send you to a page with a bunch of download links, most of which will not work. Ignore that and install the following .NET *Desktop* Runtime from Microsoft: https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe

### 3. Install any other mods first

Install any other file-based mods you want to randomize on top of, such as [Poorly Translated Mod](https://www.nexusmods.com/darksouls3/mods/316), in an adjacent directory called `mod\`. This setup is based on ME1. Merging will only work if the base mods don't significantly modify item or enemy locations or behaviors.

Other dll-based mods can be loaded with the randomizer's built-in launcher.

[DS3 Fog Gate Randomizer](https://nexusmods.com/darksouls3/mods/551/) is installed *after* DS3 Static Randomizer. If you're using that, see the installation instructions on that page. Its installation flow does not support DS3 1.15.2 yet, however.

### 4. Randomize the game

Run DS3Randomizer.exe and select options in both the Item and Enemy Randomizer tabs. To disable either randomizer, uncheck the checkbox next to the tab.

Use "Select game exe" to select the location of your `DarkSoulsIII.exe`, which *must* be in a valid game directory for installation to succeed.

At this point, you can select any dll mods which should be loaded by the built-in launcher. This is where you'd select `C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\SeamlessCoop\ds3sc.dll` (depending on your game install location) to use Seamless Co-op.

By default, randomizer adds a crash fix dll called `RandomizerCrashFix.dll`. If you don't use this, you are likely get enemy randomizer crashes in at the High Wall stairs and Ringed City stairs which can softlock your run.

Finally, click "Randomize" and wait for the status bar to show success. The first time you run randomizer, it will read from the game directory to create merged SFX files which are needed to avoid enemy attacks from being invisible outside of their original maps. With all of its files combined, the randomizer requires ~160 MB disk space.

Whether it succeeds or fails, a file is created for the run in the `spoiler_logs` directory, which contains boss and enemy locations if it succeeds and additional debugging information if it fails.

### 5. Launch the game

Click the "Launch DS3" button inside randomizer.
 
If DS3 does not get launched, especially if a command window pops up for a split second and closes, that could indicate an issue with Mod Engine. Make sure Steam is running. DS3 may need to be installed on the default drive, and it should *not* be set to launch as administrator (under Properties > Compatibility for DarkSoulsIII.exe).

Some notes on other manual launcher setups, if you can't use the built-in launcher for some reason:

- Make sure to load `dll\RandomizerCrashFix.dll` in your launcher or you likely **will** get game crashes. Do not skip this!
- If you have to use Mod Engine 1 (only compatible with DS3 1.15), you must use the version that comes in the randomizer's `dist\ModEngine1` directory which has a required fmod crash fix.
- The randomizer produces a [Mod Engine 2](https://github.com/soulsmods/ModEngine2) config file `config_ds3randomizer.toml` which you can use as a reference to manual ME2 setups.
- As of January 2026, Mod Engine 3 is not yet stable enough for enemy randomizer. At least v0.10.0 is required, but you may still get a dozen or crashes throughout a playthrough.

### 6. Uninstall

To stop using the mod, simply launch DS3 from Steam and not from the randomizer launcher.

If you plan on going back online, delete all save slots with randomizer playthroughs so you don't accidentally load into them while online, risking a ban. Additionally, randomizer tries to create a save file backup when it first does randomization, usually located at `C:\Users\<yourname>\AppData\Roaming\DarkSoulsIII\<steamid>`. The backup file is called `DS30000.sl2.randobak`. If the backup file exists and the file timestamps look correct, then delete `DS30000.sl2` and rename `DS30000.sl2.randobak` to `DS30000.sl2` to restore the backup.

## Item logic

Important changes (and non-changes):

- Handmaid's shop can contain key items, and unmissable ashes/shops can contain key items if shop locations are enabled. NPC shops considered unmissable are the initial shops for Greirat, Cornyx, Irina, Karla, and the Stone-humped Hag. NPCs with shops always drop their own ashes. (Greirat's quest is patched so you need Grand Archives Key first to send him to Lothric Castle.)
- Optional NPC questlines are not required for key items. It is also never necessary to kill an NPC (except Emma), and no good items are obtainable through NPC death.
- Crow items are randomized into upgrade materials. Boss weapons are available after a boss's soul is found. Transposition gives unique items, usually decent ones.
- All starting classes are given 10 intelligence for free so that Orbeck can be recruited. Starting stats for classes other than Deprived may be slightly increased for better starting weapons, and starting SL increased the same amount.

Unusual locations:

- Grave Key is not necessary to access the ravine/skeleton tomb in Undead Settlement - unequip everything and drop down around where Eygon of Carim is sitting.
- It is never necessary to repeatedly farm enemies for key items. Powerful non-boss enemies which normally have one-time guaranteed item drops can drop key items if that option is enabled. This includes mimics,  hostile human NPCs, Ravenous Crystal Lizards, Outrider Knights, Greater Crabs, and others. A full list is linked below. Crystal Lizards can also be enabled for key item drops.
- Some item locations only show up after you've defeated an enemy elsewhere, like the locations for Drakeblood Set, Havel's Set, Hornet Ring, Archives Key, and many Handmaid shop items.
- You can enable NG+ locations in the base NG, in which case you can find more powerful rings, at the cost of more hidden locations. Check the NG+1 and NG+2 tabs in http://darksouls3.wikidot.com/rings if you are unfamiliar with the locations.

The "Doll skip and Vilhelm skip" option is not recommended unless you're a DS3 speedrunner with experience using glitches in runs. See the [SoulsSpeedruns wiki](https://soulsspeedruns.com/darksouls3) for more info. Doll skip [is technically possible](https://www.twitch.tv/yojosherino/clip/FragileFilthyChowderSmoocherZ-lA0BRO9znS9iW488) with Catacombs ladder storage and enough dex to survive the drop. Turning on the option puts Spook and Tears of Denial before Irithyll, but they are not considered key items.

If you find any bugs, especially those which make a run incompletable, you can send me info about your run. Do not send me only the seed, since item locations heavily depend on settings. Instead, just copy the first line from the spoiler log.

## Enemy presets and boss replacement

The randomizer can be heavily customized using text files in the 'presets' folder. These are meant to be edited as you see fit. If you add a new preset to the directory, like by copying an existing file, it will automatically show up in the randomizer's preset selection. Check out README.txt in the presets folder to learn more.

There are a few built-in presets, like "Oops All" runs, and randomly replacing enemies in the world with bosses. These may push the game to its limit and may cause crashes or certain death.

## Major key items

Major key items are defined as items which unlock other unmissable items. They are only placed in unmissable locations, which excludes many of the game's non-main quests. These items, and no others, can block your progress in the randomizer until you find them, with the ultimate goal of defeating Soul of Cinder and getting to the credits.

- Basin of Vows: Used to access Lothric Castle from High Wall.
- Cell Key: Used to free Greirat.
- Cinders of a Lord (Abyss Watchers): Used to access the kiln.
- Cinders of a Lord (Aldrich): Used to access the kiln.
- Cinders of a Lord (Yhorm): Used to access the kiln.
- Cinders of a Lord (Lothric): Used to access the kiln.
- Coiled Sword: Used to activate Firelink Shrine and activate High Wall.
- Contraption Key: Used to activate the contraption in Ariandel at the end of Corvian Settlement.
- Grand Archives Key: Used to access Grand Archives from Lothric Castle.
- Jailbreaker's Key: Used to open a shortcut in Irithyll Dungeon, which also has an item.
- Jailer's Key Ring: Used to unlock three cells in Irithyll Dungeon: one at the start, one with a prisoner chief's corpse, and one with Karla.
- Lift Chamber Key: Used to confront a Darkwraith in the basement of the Tower on the Wall building in High Wall.
- Old Cell Key: Used to access a hard-to-find cell where Siegward is imprisoned in Profaned Capital.
- Path of the Dragon: Used to access Archdragon Peak from Irithyll Dungeon.
- Small Doll: Used to cross the magic barrier into Irithyll.
- Small Envoy Banner: Used to progress from Dreg Heap to the Ringed City.
- Small Lothric Banner: Used to progress from High Wall to Undead Settlement.
- Tower Key: Used to access the Bell Tower in Firelink Shrine.

Coals and Transposing Kiln are not key items exactly but are placed in the same pool as them. There is an option for Estus Shards and Undead Bone Shards to also be placed in the same pool.

## Key item locations

Some items have the option to be placed in select locations. This can be used for shorter runs or live races. If enabled, all of the same item logic still applies, so it shouldn't be possible to get softlocked. The minimal set of locations are the vanilla locations of bosses, coals, and key items, and ending items. You can check checkboxes to enable more such locations.

A full list of these locations is available at [Important locations in DS3 Static Item Randomizer](https://www.nexusmods.com/darksouls3/articles/310), which also comes with the mod in `locations.txt`.

## Special thanks

Thanks to TKGP and Pav for SoulsFormats and the layout files for all game data, HotPocketRemix for DSEventScriptTools for scripting changes and identifying scripted items, katalash for ModEngine and DSMapStudio for identifying locations, and too many people to mention in Fromsoft modding discord (https://discord.gg/mT2JJjx) for being so helpful. The newer version also incorporates EMEVD editing thanks to Meowmaritus.

RandomizerCrashFix.dll would not be possible without [dearxan](https://github.com/tremwil/dearxan) by tremwil which is provided under the MIT and Apache 2.0 licenses.
