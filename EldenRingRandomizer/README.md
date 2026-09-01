It's an item and enemy randomizer for Elden Ring which modifies the files loaded on game startup.

Items found in the world, shops items, enemy and boss drops, and character starting loadouts are all randomized. Key item randomization is supported. Most enemies can be randomized as well, with extensive customization for different enemy types.

Normally, all you need to complete the game is two Great Runes and the Rold Medallion. The key item randomizer can chain key items together, especially when the bias slider is higher. There are also options to add, change, and remove key item requirements. In the most restrictive mode, you must collect all 7 Great Runes before you can challenge the final boss.

If you would like to provide feedback or playtest or see detailed changelogs, you can join the discord server at https://discord.gg/QArcYud (also for Fog Gate Randomizer, Sekiro Randomizer, DS3 Static Randomizer). This mod is under development, and it will update when the game updates, so please check this mod page and the server for updates.

If you're a native speaker of languages supported by Elden Ring without existing mod translations or notice partially missing translations, you can add yourself to [this spreadsheet](https://docs.google.com/spreadsheets/d/1NfW_qniivBZgwP1O6BupFgcqUl1s2hIUliDuUzcgPAQ/edit) and fill in the files in `diste\Messages` to help make this mod available in other languages. Also let me know if features are needed like font changes or space adjustments.

If you'd like to support my work you can use [my Patreon](https://patreon.com/thefifthmatt) or [my Ko-fi page](https://ko-fi.com/thefifthmatt). There is no obligation at all, and randomizer will always be free.

## Installation

These instructions are a bit long, but mainly to account for all of the different ways you can use the randomizer with other mods and with different modding setups. For the simplest installation process, use Automatic Mod Engine. If you run into issues, check out the Troubleshooting section below.

There are mainly two types of Elden Ring mods: dll mods and file mods. dll mods hook into the game's memory while it's running, similar to what Cheat Engine scripts do. For example, [Seamless Co-op](https://www.nexusmods.com/eldenring/mods/510) or [Item and Param Randomiser](https://www.nexusmods.com/eldenring/mods/97). File mods provide an altered version of the data files used by the game, such as regulation.bin (containing game params) or files packed inside archives like Data0.bdt. For example, this randomizer or [Elden Ring Reforged](https://www.nexusmods.com/eldenring/mods/541).

File mods cannot hook into the game on their own, and require one of [Mod Engine 2](https://github.com/soulsmods/ModEngine2/releases), [me3](https://me3.help/en/latest/), or [Nuxe](https://github.com/JKAnderson/Nuxe/releases) to do this for them. These tools are supported in the FromSoftware modding server ?ServerName? (https://discord.gg/mT2JJjx). Dll mods can be installed with something like Elden Mod Loader, or used standalone with their own launchers.

As a result, using this randomizer requires one of these three options (more details in Step 2):
1. Built-in Launcher (recommended). In this case, you can download the randomizer anywhere, and you do not have to download anything else. When you run the randomizer, it outputs hundreds of game files, and also outputs a `config_eldenringrandomizer.toml` file. You can click the "Launch Elden Ring" button in the randomizer to launch a built-in Mod Engine. Mod Engine may not work unless Steam is running, Elden Ring is installed in the default Steam library, and `eldenring.exe` has *not* been modified to run as administrator. To use additional dll mods, configure them with the "All dll mod" button in the randomizer.
2. Manual Mod Engine. In this case, you download standalone Mod Engine, and the randomizer is a subdirectory within Mod Engine. After running the randomizer program, you can use the Mod Engine launcher to launch the game. It will use those randomized files and automatically disable EAC, assuming all of the aforementioned Mod Engine requirements are met. To use additional dll mods, you must configure them yourself.
3. Nuxe. In this case, unpack the entire game from its archives (requiring ~50GB extra disk space) and then patch it to use the unpacked files. When you run the randomizer program in Nuxe mode, it replaces those unpacked game files with randomized versions. Then after disabling EAC, you can launch eldenring.exe (or any other launcher) and it will use the randomized files. This is *not preferred* as it can results in issues like long or infinite loading screens, for instance at evergaols.

Randomizer is partly compatible with [Seamless Co-op](https://www.nexusmods.com/eldenring/mods/510). There are two main ways to set up randomizer so everyone has the same seed: either everyone separately downloads randomizer and runs it using the same options and seed (use Options > Import/Export in the randomizer's menu bar), or one person downloads the randomizer, runs it, and distributes a zip file to everyone else. Both of these are covered in the detailed instructions below. The second approach is usually simpler since it requires only one person to set up randomizer. *Note: Enemy Onslaught (boss multiplier) is likely to run into issues with Seamless Co-op. If any player dies in Enemy Onslaught, all players may need to warp away before reattempting a boss.*

If you're using [Elden Ring Fog Gate Randomizer](https://www.nexusmods.com/eldenring/mods/3295), read that page *first* for the required installation steps. The overall idea is to first install Item/Enemy Randomizer with specific options, and then set up Fog Gate Randomizer to merge the Item/Enemy Randomizer directory.

**Warning: Fog Gate Randomizer may not compatible with all randomizer features as of Item and Enemy Randomizer v0.12 and may require downpatching the game to use.** The longer-term plan is to merge it into Item and Enemy Randomizer as the interactions between them have become too complicated to manage separately.

### 1. Go offline on Steam (optional)

If you don't want randomizer runs to be interrupted or invalided, consider going offline on Steam for the duration of the run to prevent Elden Ring from automatically updating (Steam > Go Offline). This is not required, but otherwise your playthrough may not be completeable in the case of a game update.

Regarding online server bans: using mods will not ban your Steam account if you're taking necessary precautions. In particular, after you're done using mods, before logging back into game servers, restore all game files to normal (and/or stop using the Mod Engine Launcher) and delete any progress made in all save slots while the mod was active. This can be done by using a launcher with an alternate save file, or deleting all save file slots used with the mod, or restoring a backup of ER0000.sl2. More details in the "Uninstall" step below.

### 2a. Built-in Launcher

**Skip this step if you're using Nuxe or manual Mod Engine.**

To use this option, download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files) to your local disk. Unzip it using "Extract here" in 7-Zip. This will create a directory named `randomizer`.

Do not download it to a OneDrive-synced folder, like your Desktop on some Windows installations, as files may not load in-game. Do not use Vortex Mod Manager, because it does not know how to use game files generated by the randomizer.

If you want to use randomizer with other file-based mods, you will be able to specify other mods in the randomizer program using "Merge other mod" before randomizing. See the "Select options" step below for more details. When randomization is applied, randomizer will automatically merge the mods' files. When the game launches, it will load both the merged files and the mods' other files.

If you want to use randomizer with dll-based mods, including Seamless Co-op (`ersc.dll`), you can specify them using "All dll mod" and they'll be loaded through Mod Engine after clicking "Launch Elden Ring". In the "Misc Options" tab, built-in dll mods which come with randomizer can be selected as well. This includes `RandomizerCrashFix.dll`, which is strongly recommended if base game and DLC enemies are randomized together.

### 2b. Manual Mod Engine

**Skip this step if you're using Nuxe or the built-in launcher.**

This option requires using [Mod Engine 2](https://github.com/soulsmods/ModEngine2/releases) for Elden Ring. Download it anywhere on your local disk and extract it using 7-Zip or a similar tool. Then, download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files), place it inside of the `ModEngine-2.1.0.0-win64` directory you just created, and unzip it using "Extract here" in 7-Zip. This will create a directory named `randomizer` inside of Mod Engine, alongside the other directories `mod` and `modengine2`.

Do not download it to a OneDrive-synced folder, like your Desktop on some Windows installations, as files may not load in-game. Do not use Vortex Mod Manager, because it does not know how to use game files generated by the randomizer.

For a basic installation of just randomizer, edit `eldenring.config.toml` (you can open it with Notepad) so that the mod path at the bottom of the file is called "randomizer". You will want [this exact setup](https://i.imgur.com/xhkz2Cn.png) of the config file and directories. This approach is covered by [Elden Ring Key Item Randomizer: Installation Instructions](https://www.youtube.com/watch?v=3GdUwpzYf8o) by Nax.

If you want to use randomizer with other file-based mods, including regulation.bin edits, the randomizer supports this by using a second directory containing the mods. In particular, you must install that mod in a separate `mod` directory, and then add both the `randomizer` and `mod` directories to `eldenring.config.toml`. You will end up with config file [like this](https://i.imgur.com/sFflndU.png).

If you want to use randomizer with dll-based mods with a manual `eldenring.config.toml`, Mod Engine 2.1.0.0 supports specifying dlls in the `external_dlls` field. To use built-in randomizer dlls with a manual config, they can be added to the config like `"randomizer\\dll\\RandomizerCrashFix.dll"` or `"randomizer\\dll\\RandomizerHelper.dll"`, and in the case of RandomizerHelper, options for this dll can be configured using the "Edit RandomizerHelper options" menu in the "Misc Options" tab.

### 2c. Set up Nuxe

**Skip this step if you're using manual Mod Engine or the built-in launcher.**

To use Nuxe, unpack and patch the game using Nuxe. Unpacking the game requires around 50 GB of disk space. Note this is *not preferred* as it can results in issues like long or infinite loading screens, for instance at evergaols.

You can get Nuxe from [GitHub](https://github.com/JKAnderson/Nuxe/releases). It is a rewrite of [UXM](https://github.com/Nordgaren/UXM-Selective-Unpack) with additional features and fixes. You should also make sure you can bypass EAC when you launch the game, or else Elden Ring with refuse to start with the modified exe. You can find guides for this online.

Download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files). It doesn't matter where you download it to do, because you will specify the game install path inside of it. Unzip it using "Extract here" in 7-Zip.

If you want to use randomizer with dll-based mods, you must load them yourself, for instance using something like Elden Mod Loader. However, you may need to use `randomizer\dll\RandomizerCrashFix.dll` to avoid crashes, which is not compatible with Elden Mod Loader due to the initialization delay. To work around this, `RandomizerCrashFix.dll` can be copied to the game directory and renamed to `dinput8.dll`.

### 3. Select options

**If you're rerunning randomizer, check the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files) to make sure you have the latest version. Update it if not.**

Open EldenRingRandomizer.exe and select your Elden Ring game exe. This must be a valid game directory to extract all game files. Select "Output files for Nuxe" only if you're using Nuxe. When this is checked, the randomizer will output game files into the game directory, creating backups of all of the files it replaces. Otherwise, it will output game files directly into the `randomizer` directory.

If you have other file-based mods to merge with randomizer, use "Merge other mod". Select either a directory a regulation.bin file to merge another mod's directory. (With Manual Mod Engine, you'll also need to edit the config file [like this](https://i.imgur.com/sFflndU.png), as mentioned in Step 2b). Select a Mod Engine toml file to merge all of the mod directories listed in the toml file. This uses the same resolution order as Mod Engine: if a file exists in multiple mods, randomizer only merges the file which is found first in the mod order. Be warned that some mods cannot be merged with item randomizer currently, and will always result in errors.

Then, select randomization options in randomizer tabs, which can be enabled or disabled with the corresponding checkboxes. In item randomizer, "Important locations" are all of the places you might have to check to finish the game or get essential upgrades. See more information about key items and locations below. By default, enemies get randomized within predefined categories (dragons bosses become other dragon bosses), but this is highly configurable with custom enemy placement.

To get the same run as someone else, you can Export/Import options files from the Options menu. These contain your seed, options, and enemy preset. To get the same enemies as someone else but different items, use different "Overall seed" numbers but the same "Separate enemy seed" (in the Enemy Randomizer tab). You can instead zip up the mod files after randomizing, covered in the next section.

### 4. Randomize!

Click the "Randomize items and enemies" button to output game files for a randomized run. The button's text may change depending on whether item/enemy randomizer are enabled, or whether you've checked options like "Reroll seed". In particular, "Run with fixed seed" means it will try to make a run based on the seed(s) you provided or which were populated based on an existing run.

The randomizer creates a file in the `spoiler_logs` directory which contains hints and spoilers for the run you just generated.

After this, **do not manually copy and paste individual files!** In addition to regulation.bin, hundreds of files in the event, map, msg, script, and sfx directories are required to make the item and/or enemy randomizer function correctly, and the list of required files can change every time you randomize. The randomizer already puts all of these files in the correct place and adds/deletes them as necessary, so **never manually copy and paste them**.

If you plan to use Seamless Co-op, and you're generating the mod files for everyone else (as opposed to Export/Import options), you can now send the files. In particular, randomizer can create a zip file using File > Zip mod files, and you can send the zip file. The zip file contains the randomized game files, any merged file mods, and a `launchmod_rando.bat` file to launch Elden Ring with the modded files. Remember that everyone still needs to install Seamless Co-op separately using [its install instructions](https://www.nexusmods.com/eldenring/mods/510).

### 5. Run the game

Close the game if it's currently running, then launch the game. (If using Automatic Mod Engine, press the "Launch Elden Ring" button. If using Manual Mod Engine, launch `launchmod_eldenring.bat`.)

Mod Engine's launcher seems to be incompatible with running Elden Ring as administrator, so make sure "Run this program as an administrator" is unchecked in the Compatibility Properties for eldenring.exe while using Mod Engine. If you run into other problems, see Troubleshooting below.

If items are randomized, the *second* character creation screen should have different weapons and armor (the first has not changed), and the item beside the Stranded Graveyard Site of Grace should be different. If enemies are randomized, Grafted Scion should be a different enemy.

### 6. Uninstall

If you used Nuxe to install randomizer, open EldenRingRandomizer.exe and select "File > Restore Nuxe backups" to uninstall. Review the files and click "OK" to restore all of the backups. This is also required if you're switching from Nuxe to Mod Engine.

If you used Mod Engine, simply don't launch the game with Mod Engine anymore, or edit the Mod Engine config to remove it or change it back.

Finally, if you plan on going back online, make sure you use restore a save file from before you started using mods, or else you will definitely get banned! Some launchers will handle this for you, so see their documentation if this is the case. Otherwise, randomizer creates a save file backup when it first does randomization, so browse `C:\Users\<user name>\AppData\Roaming\EldenRing\<save id>` from local disk. The file to restore is ER0000.sl2, the randomizer's default backup name is ER0000.sl2.randobak.

## Troubleshooting

*I launched the game with Mod Engine and it immediately closed.*

In order for Mod Engine to launch with the recommended setup, the game must be installed from Steam and Steam must be running. Due to current limitations of Mod Engine preview3, you may also run into issues if Elden Ring is installed on a different disk from the main Steam library, and may need to be reinstalled into the main Steam library location. Finally, make sure you're running `launchmod_eldenring.bat` and not `modengine2_launcher.exe`.

Mod Engine's launchmod script seems to be incompatible with running as administrator, so make sure "Run this program as an administrator" is unchecked in the Compatibility Properties for eldenring.exe while using Mod Engine. Otherwise, see "I've tried everything else" below.

*I launched the game with Mod Engine and item randomizer, but none of the items or starting classes are random.*

This could indicate two things: either `config_eldenring.toml` has not been edited with the right values, or the game has updated and so it no longer can use modded `regulation.bin` files meant for older versions of the game. If you're using a version of the randomizer from at least the latest game patch, then double-check the launch file to make sure the paths are correct.

This issue may arise if merged mods are from an older version of the game, in which case those mods need to be updated. This can still work in some cases, by loading into the main menu without any mods installed and then quitting out, or sometimes by moving the current ER0000.sl2 and letting the game re-create it.

*I loaded into the game and I keep seeing "?EventTextForMap?" or "Error: Mismatch between regulation.bin and other randomizer files".*

This is a failsafe in the randomizer when it's not properly installed, meaning it only detected some of the files it needs. It can happen when the required game files are not kept together (please see the warning in Step 4: **do not manually copy and paste individual files**), or when files are copy-pasted on top of the randomizer, or when the randomizer is not at the top of the mod list in `config_eldenring.toml`. It can also happen when the game files are re-randomized without fully shutting down the game, in which case you should exit to desktop and re-launch the game.

If the issue is due to partial installation, the best way to resolve this is to rerun the randomizer and let it output files to their intended locations, and remove any previous instances of copy-pasted files. If you want to use other file-based mods with the randomizer, follow the instructions for using a directory for them. With the exception of other file-based randomizers and installers, **the randomizer should always be the last-installed mod**.

Finally, a popup may appear if you don't have DLC installed but the checkboxes to include DLC content are checked in the DLC tab. This will show up as ?EventTextForMap? if you didn't install or merge mod files correctly. This message appears because randomizer playthroughs will softlock or become unplayable if content you don't own is randomized.

*I launched the game with Mod Engine and item/enemy randomizer, and item randomizer seems to work, but enemy randomizer doesn't.*

Make sure you followed the above installation instructions, including downloading the randomizer and extracting its files in the appropriate location. Do **not** use Vortex Mod Manager, because apparently it does not know how to properly install game files generated by the randomizer.

Assuming none of the other troubleshooting sections apply, this may indicate a bug in Mod Engine, so see the "I've tried everything else" option.

*I've tried everything else and Mod Engine still doesn't work.*

Mod Engine can have issues, and it is somewhat complicated to set up, so there are a few failsafes which seem to work for most people.

Instead of Mod Engine 2, you can try me3 which is more up-to-date and generally more stable but requires creating your own config. Randomizer may support it in the future.

An alternate Mod Engine 2 setup involves putting Mod Engine in the game directory directly, which seems to work for unknown reasons. In this case, copy the files and directories inside of `ModEngine-2.1.0.0-win64` (including the `randomizer` directory) into the `ELDEN RING\Game` directory, then run `modengine2_launcher.exe` (*not* the bat file in this case) as administrator.

If you can spare extra 50GB of disk space, you can consider using Nuxe. This makes the randomizer at least partly functional (assuming the game version matches the randomizer version), but has the disadvantages of being difficult to share with other people, difficult to use dll mods, and difficult to uninstall or merge with other mods.

## Item logic

Important changes (and non-changes):

- Volcano Manor has an area you can access by dying to a grab attack at the bottom of Raya Lucaria. However, you can also get there from Volcano Manor's prison town, and vice versa. See [this video](https://streamable.com/je5mdi) for both paths.
- Shop contents are randomized. All shop NPCs drop their own Bell Bearings which will match the contents of their shops.
- Optional NPC questlines are not required for key items. It is never necessary to kill an NPC (not even Patches!). It is also never necessary to repeatedly farm enemies for key items.

If you find any bugs, especially those which make a run incompletable, you can upload your spoiler log in the discord server and I will take a look.

### Locations

Important locations are unmissable item locations that come in these required categories:

- Vanilla locations of major key items: Locations of items you need to access all of the vanilla game. See the key item list below. (Excludes the *locations* for Pureblood Knight's Medal and Carian Inverted Statue, because it's possible to mess up those quests.)
- Major bosses: Every boss with [an achievement](https://www.trueachievements.com/game/Elden-Ring/achievements) for defeating them. (Excludes Lichdragon Fortissax because he is missable.) In the DLC, this is defined as every remembrance boss plus Bayle and Golden Hippopotamus.

And these optional categories:

- Golden Seed trees: Every glowing golden tree with a Golden Seed under it.
- Sacred Tear churches: Every church with a statue that has a Sacred Tear under it.
- Merchant shops: Every shop with normal items, excluding those which only have sorceries or incantations or puppets.
- Other bosses: Every enemy with a boss healthbar that drops an item.
- Shadow Realm Blessing pickups: Anywhere you normally find a Scadutree Blessing or Revered Spirit Ash on the ground in the DLC. This includes Miquella's Crosses, tutelary deities (the withered corpses with Revered Spirit Ashes), the Scadutree Chalice, and various random pickups. However, it excludes enemies which normally drop these items.

Patches is an edge case in a few ways. He's not considered a boss and he doesn't drop anything valuable. He is considered a shop, but only if you don't select the "Exclude caves" option.

All categories have blue links beneath them in the randomizer. Click on them to open a popup window with a full list of locations broken down by area.

There are over 3200 distinct item locations in Elden Ring. As of randomizer version v0.11, around 1900 have handwritten descriptions and can contain important or noteworthy items. All of the rest currently have an automatically generated description referencing the closest map landmark.

### Major key items

Major key items are defined as items which unlock other unmissable items. They are only placed in important locations. These items, and no others, can block your progress in the randomizer until you find them.

- Academy Glintstone Key: Used to access the Academy of Raya Lucaria. There are two versons of this item. Only the version that says "a glintstone key will remember its user" works here.
- Carian Inverted Statue: Used to progress in Carian Study Hall and reach the Divine Tower of Liurnia.
- Cursemark of Death: Used to access the Deathbed Dream in Deeproot Depths. (Note that the fight itself is missable and cannot have key items.)
- Dark Moon Ring: Used to progress to Moonlight Altar from Ainsel River.
- Dectus Medallion (Left and Right): Used to ascend the Grand Lift of Dectus. Other routes to enter Altus Plateau are also possible.
- Discarded Palace Key: Used to unlock the treasure chest in the Raya Lucaria Grand Library.
- Drawing-Room Key: Used to access the Volcano Manor Drawing Room and the lava area hidden within.
- Great Runes: By default, two of seven are required to enter Leyndell, either though Capital Outskirts or Deeproot Depths. With modified rulesets, any number of these can be required to enter Leyndell, enter Mountaintops, or access the final boss.
- Haligtree Secret Medallion (Left and Right): Used to access Consecrated Snowfield, the hidden path to the Haligtree.
- Imbued Sword Key: Used to unlock a Sending Gate at the Four Belfries in Liurnia. There are three of these in the game.
- Pureblood Knight's Medal: Used to immediately access Mohgwyn Palace without using the Sending Gate in Consecrated Snowfield.
- Rold Medallion: Used to access Mountaintops of the Giants after Leyndell. With modified rulesets, it can be replaced with a Great Runes requirement.
- Rusty Key: Used to progress into Stormveil Castle. This is optional, unless Gostoc dies before he opens the main gate for you.

The DLC has these key items:
- Messmer's Kindling: Used to burn the Sealing Tree in Rauh Ruins. With modified rulesets, this can be split into some number of Messmer's Kindling Shards with a minimum shard count required to burn the Sealing Tree.
- O Mother: Used to access Hinterland by using the gesture in front of the Marika statue behind Shadow Keep.
- Hole-Laden Necklace: Used at Finger Ruins to progress Count Ymir's quest and access the Metyr boss fight.
- Well Depths Key: Used to access the Belurat swamp.

### Other important items

By default, the following items are not randomized: Flask of Crimson Tears, Flask of Cerulean Tears, Flask of Wondrous Physick, Spectral Steed Whistle, Spirit Calling Bell, Crafting Kit, Whetstone Knife, Lantern, Serpent-Hunter, and all map fragments. Some of these can be randomized using custom item placement.

Talisman Pouches, Memory Stones, Whetblades are not key items but they're placed in the key item pool by default. Golden Seeds, Sacred Tears, and upgrade material Bell Bearings can also be assigned to important locations when selected in the randomizer.

When Bell Bearings are randomized and DLC Start is disabled, they are placed in the following locations, matching their vanilla locations:

- Smithing-Stone Miner's Bell Bearing [1]: In Liurnia
- Smithing-Stone Miner's Bell Bearing [2]: In Altus Plateau
- Smithing-Stone Miner's Bell Bearing [3]: In Mountaintops of the Giants
- Smithing-Stone Miner's Bell Bearing [4]: In Farum Azula
- Somberstone Miner's Bell Bearing [1]: In Caelid
- Somberstone Miner's Bell Bearing [2]: In Altus Plateau
- Somberstone Miner's Bell Bearing [3]: In Mountaintops of the Giants
- Somberstone Miner's Bell Bearing [4]: In Farum Azula
- Somberstone Miner's Bell Bearing [5]: In Farum Azula
- Glovewort Picker's Bell Bearing [1]: In Mt. Gelmir
- Glovewort Picker's Bell Bearing [2]: In Mountaintops of the Giants
- Glovewort Picker's Bell Bearing [3]: In Farum Azula
- Ghost-Glovewort Picker's Bell Bearing [1]: In Nokron, Eternal City
- Ghost-Glovewort Picker's Bell Bearing [2]: In Ainsel River
- Ghost-Glovewort Picker's Bell Bearing [3]: In Haligtree

Smithing Stones themselves can be distributed using the "Smithing Stone availability similar to base game" option. This places *most* stones of a given tier in the portion of the game appropriate to their level, but there is still a chance to pick up any stone anywhere. When this option is disabled, their placement is completely random, and you may be stuck at lower upgrade levels for a long time, especially for regular Smithing Stone weapons. The option also adds a few new stones to guarantee 40-50 total item pickups per tier, slightly more if collectible materials are also randomized.

These are the main locations for Smithing Stones when the option is enabled. Again note that a handful of Smithing Stones of each tier *will* be placed outside of these areas to allow getting better weapons early with more thorough exploration.

- Smithing Stone [1] and [2] can be found mainly in Limgrave, Liurnia, and Caelid.
- Smithing Stone [3] can be found mainly in Liurnia and Caelid, and occasionally in Limgrave, Altus Plateau, and early underground areas.
- Smithing Stone [4] can be found mainly in Liurnia and Caelid, Altus Plateau, and underground areas.
- Smithing Stone [5] can be found mainly in Altus Plateau, Leyndell, Sewers, and later underground areas.
- Smithing Stone [6] can be found mainly in Leyndell, Sewers, later underground areas, and Mountaintops or later.
- Smithing Stone [7] can be found mainly in Sewers and Moonlight Altar and Mountaintops or later.
- Smithing Stone [8] can be found mainly in Moonlight Altar and Mountaintops or later.
- Somber Smithing Stone [1] and [2] can be found mainly in Limgrave, Liurnia, Caelid, and early underground areas.
- Somber Smithing Stone [3] and [4] can be found mainly in Liurnia, Caelid, and early underground areas.
- Somber Smithing Stone [5] can be found mainly in Caelid, Altus Plateau, and Leyndell.
- Somber Smithing Stone [6] can be found mainly in Altus Plateau, and Leyndell, and later underground areas.
- Somber Smithing Stone [7] [8] and [9] can be found mainly in Mountaintops or later, with a few in Altus Plateau or later.
- Ancient Dragon Smithing Stones and Somber Ancient Dragon Smithing Stones can be found throughout the game.

### Hints

There is an optional feature to purchase map marker hints when you get stuck. If enabled, they are available for purchase from Kalé in the Church of Elleh after reaching the Altus Plateau. These only support key items from the base game, not the DLC.

In short, if there is a required key item which is accessible but you haven't found it yet, you can purchase a hint for where to search for it. The first Site of Grace in the item's area will be marked, along with the overall name of the area. This costs 200 runes times your Rune Level, or 10k runes, whichever is higher.

There is also an option to mark item locations, down to the exact map coordinates of the treasure or enemy that drops it. You can purchase these only once you defeat the main healthbar boss of the item's area. (Usually this is the boss at the "end" of the area. For Liurnia overall it's Royal Knight Loretta, for Dragonbarrow and Altus Plateau it's their Godskin Apostle, for Mt. Gelmir it's Fallingstar Beast.) This costs 500 runes times your Rune Level, or 25k runes, whichever is higher.

OOT Randomizer-style hints are planned in the future, in the style of "a catacombs boss drops a key item" or "there are no key items in Leyndell".

## Enemy randomization

Enemy randomizer shuffles enemies and bosses within categories. By default, major bosses replace other bosses, minor bosses replace other minor bosses, regular enemies replace other regular enemies, and so on. Minibosses like night bosses, overworld dragon bosses, and evergaol bosses each have their own categories.

The categorization of major bosses in enemy randomizer is slightly different from the categorization in item randomizer. Some additional major bosses are added in enemy randomizer, like Fia's Champions, Fortissax, Gideon. Elemer of the Briar is a major boss in item randomizer, because he has an achievement, but is downgraded to a minor boss in enemy randomizer because room very small.

The warp from Raya Lucaria to Volcano Manor is changed so that getting consumed by the abductor enemy is no longer required. Instead, the only requirement is to die at the bottom of the elevator through any means.

Aside from default enemy categories, customization is possible if you check "Custom enemy placement" and then click "Edit custom enemy placement". Custom configuration is saved as a file in the `presets` directory and can be imported and exported. Most common types of customization like "Merge non-major bosses" and "Replace wildlife with regular enemies" can be achieved by clicking buttons in the relevant settings.

For each category, custom enemy lists can specify which enemies are randomly selected. Within a category, you can have several source groups (also known as "pools") whose percentages add up to 100%. Click the "+" button to add a new source group. You can add enemy types to source groups, and when a group is used, an enemy in that group will be randomly selected based on its frequency in the vanilla game. "Same category" is synonymous with whichever category you're editing, so by default, all categories are replaced with 100% same category. There's also a special source group called "Not randomized" which gives a % chance for some enemies in a category to be randomized and for others to not be randomized.

Finally, custom enemy placement also has "Oops All" mode, which replaces all enemies with a single boss or enemy type, and "Enemy Onslaught" which duplicates (or more) each enemy and boss. Onslaught can be used with or without randomization, by adding or removing "All Enemies and Bosses" from being randomized or not.

## Customization for merged mods

If you get errors merging a mod you've created and want to try for compatibility with randomizer, you can place a config file named `randomizer_merge_config.yml` in the mod's root directory (the same directory which contains `regulation.bin`) which randomizer will read when merging the mod. There is an example of this file inside of the `diste` directory distributed with the randomizer.

At the moment, customization is geared towards basic compatibility, containing lists of items/enemies locations in the merged mod to avoid randomizing. There is no configuration for additional randomization of new items/enemies yet, as this can get extremely complicated. The supported fields are `NorandomEntities`, `NorandomLots`, `NorandomEnemyLots`, `NorandomLotFlags`, `NorandomShops`, and `NorandomItems`. These can be given individual numbers or inclusive number ranges as a YAML list, as follows:

NorandomLots:
- 10811
- 1038540500-1038540599
NorandomShops:
- 100945-100949

Note that enemies which are added by mods, with brand new entity ids, are already not randomized and don't need to be listed. For items, *avoid specifying vanilla lots and shops* as much as possible and prefer to only include ids added by your mod. Some item randomization features require being able to randomize specific items, especially key items. Supporting custom randomization is conceivable in the future but depends on the needs of each mod. If there are multiple merged mods, all of their configs will be combined together, even if only the first one's lots are used.

## Special thanks

Thanks to TKGP and katalash for SoulsFormats, Meowmaritus for SoulsFormats EMEVD editing and initial UXM, and HotPocketRemix for EMEVD instruction reversing. Thanks to everyone in ?ServerName? for being helpful and informative, everyone in the rando discord for ideas and bug reports, and lazyoldman and LilAggy for working with me directly on new features.

RandomizerCrashFix.dll includes convenience patches for the game based on SoulSplitter by FrankvdStam, alt-saves by vswarte, and eldenring-menu-input-delay-fix by rbignon, with some modifications to remain compatible with those mods.

Finally, thanks to Saltyfish_King, Fababfan, Plante, proteh, Meliar, Leekos, Kuroko, 刀子, SaniTOS, HiJacked, frioosin, and Cogumellow for contributing translations and permission to use them in this mod.

Source code is published to https://github.com/thefifthmatt/SoulsRandomizers and updated occasionally.
