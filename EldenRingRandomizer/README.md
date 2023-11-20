It's an item and enemy randomizer for Elden Ring which modifies the files loaded on game startup, formerly "Elden Ring Key Item Randomizer".

Items found in the world, shops items, enemy and boss drops, and character starting loadouts are all randomized. Key item randomization is supported. Most enemies can be randomized as well, with extensive customization for different enemy types.

Normally, all you need to complete the game is two Great Runes and the Rold Medallion. The key item randomizer can chain key items together, especially when the bias slider is higher. There are also options to add, change, and remove Great Rune requirements. In the most restrictive mode, you must collect all 7 Great Runes before you can challenge the final boss.

If you use this mod in a casual livestream or published video, please link this mod page from a chat command or video description if possible. If you use this mod in a unique live event like a showcase or a contest, please credit me (thefifthmatt) for the mod if it's reasonable to do so. There isn't a team behind this mod. I created it for people to use it, so please facilitate that if you manage to create engaging content from it. Thank you!

If you would like to provide feedback or playtest, you can join the discord server at https://discord.gg/QArcYud (also for Fog Gate Randomizer, Sekiro Randomizer, DS3 Static Randomizer). This mod is under development, and it will update when the game updates, so please check this mod page and the server for updates.

If you're a native speaker of languages supported by Elden Ring without existing mod translations or notice partially missing translations, you can add yourself to [this spreadsheet](https://docs.google.com/spreadsheets/d/1NfW_qniivBZgwP1O6BupFgcqUl1s2hIUliDuUzcgPAQ/edit) and fill in the files in `diste\Messages` to help make this mod available in other languages. Also let me know if features are needed like font changes or space adjustments.

## Installation

These instructions are a bit long, but mainly to account for all of the different ways you can use the randomizer with other mods and with different modding setups. For the simplest installation process, use Automatic Mod Engine. If you run into issues, check out the Troubleshooting section below.

There are mainly two types of Elden Ring mods: dll mods and file mods. dll mods hook into the game when it's running to change it in various ways, similar to what a Cheat Engine script might do. For example, [Seamless Co-op](https://www.nexusmods.com/eldenring/mods/510) or [Item and Param Randomiser](https://www.nexusmods.com/eldenring/mods/97). File mods provide an altered version of the data files used by the game, such as regulation.bin (containing game params) or files packed inside archives like Data0.bdt. For example, this randomizer or [Elden Ring Reforged](https://www.nexusmods.com/eldenring/mods/541).

File mods like this randomizer *cannot hook into the game on their own*, and require either [Mod Engine 2](https://github.com/soulsmods/ModEngine2/releases) or [Elden Ring Selective UXM](https://www.nexusmods.com/eldenring/mods/1651) to do this for them. Both of these tools are in beta and are maintained by users in the FromSoftware modding server ?ServerName? (https://discord.gg/mT2JJjx). On the other hand, dll mods can be installed with something like Elden Mod Loader, or used standalone with their own launchers.

As a result, using this randomizer requires one of these three options (more details in Step 2):
1. Automatic Mod Engine. In this case, you can download the randomizer anywhere, and you do not have to download anything else. When you run the randomizer, it outputs hundreds of game files, and also outputs a `config_eldenringrandomizer.toml` file. You can click the "Launch Elden Ring" button in the randomizer to launch a built-in Mod Engine. Mod Engine may not work unless Steam is running, Elden Ring is installed in the default Steam library, and `eldenring.exe` has *not* been modified to run as administrator.
2. Manual Mod Engine. In this case, you download standalone Mod Engine, and the randomizer is a subdirectory within Mod Engine. After running the randomizer program, you can use the Mod Engine launcher to launch the game. It will use those randomized files and automatically disable EAC, assuming all of the aforementioned Mod Engine requirements are met.
3. UXM. In this case, unpack the entire game from its archives (requiring ~50GB extra disk space) and then patch it to use the unpacked files. When you run the randomizer program in UXM mode, it replaces those unpacked game files with randomized versions. Then after disabling EAC, you can launch eldenring.exe (or any other launcher) and it will use the randomized files.

The Mod Engine approach is covered by [Elden Ring Key Item Randomizer: Installation Instructions](https://www.youtube.com/watch?v=3GdUwpzYf8o) by Nax. The same overall approach applies even with enemy randomizer, just with more options in Step 3 below.

A note about [Seamless Co-op](https://www.nexusmods.com/eldenring/mods/510): You can use [these instructions](https://www.nexusmods.com/eldenring/articles/94) for basic compatibility with Mod Engine. If you're using that guide, *ignore the second-to-last step* and follow the specific instructions below instead. Custom enemy placements (especially boss replacements) seem to result in disconnections, so try to avoid those. See also [How to Install Elden Ring Enemy/Item Randomizer and Full Co-op](https://www.youtube.com/watch?v=IM3U6oZ4egg) by itsSpicy, which recommends having only one person run the randomizer and upload their files to a host like Google Drive for their partners to download.

If you're using [Elden Ring Fog Gate Randomizer](https://www.nexusmods.com/eldenring/mods/3295), read that page *first* for the required installation steps. The overall idea is to first install Item/Enemy Randomizer with specific options, and then merge Item/Enemy Randomizer directory into a separate Fog Gate Randomizer directory.

### 1. Go offline on Steam (optional)

If you don't want randomizer runs to be interrupted or invalided, consider going offline on Steam for the duration of the run to prevent Elden Ring from automatically updating (Steam > Go Offline). This is not required, but otherwise your playthrough may not be completeable in the case of a game update.

(After the game updates, all regulation.bin edits based on the previous game version will be ignored. This includes all item location edits, character edits, and enemy attribute edits. It will be necessary to download a new version of the randomizer whenever one is available, and if possible, rerunning it with the same seed and options.)

Regarding online server bans: using mods will not ban your Steam account if you're taking necessary precautions. In particular, after you're done using mods, before logging back into game servers, restore all game files to normal (and/or stop using the Mod Engine Launcher) and delete any progress made in all save slots while the mod was active. This can be done by using a launcher with an alternate save file, or deleting all save file slots used with the mod, or restoring a backup of ER0000.sl2. More details in the "Uninstall" step below.

### 2. Install .NET 6.0 Desktop Runtime

This is needed to prevent errors about framework versions. If you don't have it, Windows will send you to a page with a bunch of download links. Most of them will not work. You specifically need the following link for the *Desktop* Runtime from Microsoft.

https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe

### 3a. Automatic Mod Engine

**Skip this step if you're using UXM or manual Mod Engine.**

To use this option, **manually** download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files), and place it anywhere. Unzip it using "Extract here" in 7-Zip. This will create a directory named `randomizer`.

Do **not** use Vortex Mod Manager, because apparently it does not know how to properly install game files generated by the randomizer.

If you want to use randomizer with other file-based mods, you will be able to specify other mods in the randomizer program using "Merge other mod" before randomizing. See the "Select options" step below for more details. When randomization is applied, randomizer will automatically merge the mods' files. When the game launches, it will load both the merged files and the mods' other files.

### 3b. Manual Mod Engine

**Skip this step if you're using UXM or automatic Mod Engine.**

This option requires using [Mod Engine 2](https://github.com/soulsmods/ModEngine2/releases) for Elden Ring. Download it anywhere, for instance the Downloads folder or Desktop, and extract it using 7-Zip or a similar tool. Then, **manually** download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files), place it inside of the `ModEngine-2.0.0-preview3-win64` directory you just created, and unzip it using "Extract here" in 7-Zip. This will create a directory named `randomizer` inside of Mod Engine, alongside the other directories `mod` and `modengine2`.

Do **not** use Vortex Mod Manager, because apparently it does not know how to properly install game files generated by the randomizer.

For a basic installation of just randomizer, edit `eldenring.config.toml` (you can open it with Notepad) so that the mod path at the bottom of the file is called "randomizer". You will want [this exact setup](https://cdn.discordapp.com/attachments/936393685585780846/960810154394284052/unknown.png) of the config file and directories.

If you want to use randomizer with other file-based mods, including regulation.bin edits, the randomizer supports this by using a second directory containing the mods. In particular, you must install that mod in a separate `mod` directory, and then add both the `randomizer` and `mod` directories to `eldenring.config.toml`. You will end up with config file [like this](https://cdn.discordapp.com/attachments/936393685585780846/980279218602643536/unknown.png).

### 3c. Set up UXM

**Skip this step if you're using Mod Engine.**

To use UXM, unpack and patch the game using UXM. Unpacking the game requires around 50 GB of disk space.

The recommended UXM version is [Elden Ring Selective UXM](https://www.nexusmods.com/eldenring/mods/1651). You should also make sure you can bypass EAC when you launch the game, or else Elden Ring with refuse to start with the modified exe. You can find guides for this online.

Download the randomizer zip from the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files). It doesn't matter where you download it to do, because you will specify the game install path inside of it. Unzip it using "Extract here" in 7-Zip.

### 4. Select options

**If you're rerunning randomizer, check the [Files tab](https://www.nexusmods.com/eldenring/mods/428?tab=files) to make sure you have the latest version with additional softlock fixes and features. Update it if not.**

Open EldenRingRandomizer.exe and select your Elden Ring game exe. Select "Output files for UXM" only if you're using UXM. When this is checked, the randomizer will output game files into the game directory, creating backups of all of the files it replaces. Otherwise, it will output game files directly into the `randomizer` directory.

If you have other file-based mods to merge with randomizer, use "Merge other mod". Select either a directory or a regulation.bin file to merge another mod's directory. (With Manual Mod Engine, you'll also need to edit the config file [like this](https://cdn.discordapp.com/attachments/936393685585780846/980279218602643536/unknown.png), as mentioned in Step 2b). Select a Mod Engine toml file to merge all of the mod directories listed in the toml file. This uses the same resolution order as Mod Engine: if a file exists in multiple mods, randomizer only merges the file which is found first in the mod order. Be warned that some mods cannot be merged with item randomizer currently, and will always result in errors.

Then, select randomization options in both the Item Randomizer and Enemy Randomizer tabs, which can be enabled or disabled with the corresponding checkboxes. In item randomizer, "Important locations" are all of the places you might have to check to finish the game or get essential upgrades. See more information about key items and locations below. By default, enemies get randomized within predefined categories (dragons bosses become other dragon bosses), but this is highly configurable with custom enemy placement.

To use the same run as someone else, you can save/load options files from the Options menu. These contain your seed, options, and customization preset. If this doesn't work for you, zip up the entire randomizer directory after randomizing and send it to someone else instead.

### 5. Randomize!

Click the "Randomize items and enemies" button to output game files for a randomized run. The button's text may change depending on whether item/enemy randomizer are enabled, or whether you've checked options like "Reroll seed". In particular, "Run with fixed seed" means it will try to make a run based on the seed(s) you provided or which were populated based on an existing run.

The randomizer creates a file in the `spoiler_logs` directory which contains hints and spoilers for the run you just generated.

After this, **do not manually copy and paste individual files!** In addition to regulation.bin, hundreds of files in the event, map, msg, script, and sfx directories are required to make the item and/or enemy randomizer function correctly, and the list of required files can change every time you randomize. The randomizer already puts all of these files in the correct place and adds/deletes them as necessary, so **never manually copy and paste them**.

### 6. Run the game

Close the game if it's currently running, then launch the game. (If using Automatic Mod Engine, press the "Launch Elden Ring" button. If using Manual Mod Engine, launch `launchmod_eldenring.bat`.)

Mod Engine's launcher seems to be incompatible with running Elden Ring as administrator, so make sure "Run this program as an administrator" is unchecked in the Compatibility Properties for eldenring.exe while using Mod Engine. If you run into other problems, see Troubleshooting below.

If items are randomized, the *second* character creation screen should have different weapons and armor (the first has not changed), and the item beside the Stranded Graveyard Site of Grace should be different. If enemies are randomized, Grafted Scion should be a different enemy.

### 7. Uninstall

If you used UXM to install randomizer, open EldenRingRandomizer.exe and select "File > Restore UXM backups" to uninstall. Review the files and click "OK" to restore all of the backups. This is also required if you're switching from UXM to Mod Engine.

If you used Mod Engine, simply don't launch the game with Mod Engine anymore, or edit the Mod Engine config to remove it or change it back.

Finally, if you plan on going back online, make sure you use restore a save file from before you started using mods, or else you will definitely get banned! Some launchers will handle this for you, so see their documentation if this is the case. Otherwise, randomizer creates a save file backup when it first does randomization, so browse `C:\Users\<user name>\AppData\Roaming\EldenRing\<save id>` from local disk. The file to restore is ER0000.sl2, the randomizer's default backup name is ER0000.sl2.randobak.

## Troubleshooting

*I launched the game with Mod Engine and it immediately closed.*

In order for Mod Engine to launch with the recommended setup, the game must be installed from Steam and Steam must be running. Due to current limitations of Mod Engine preview3, you may also run into issues if Elden Ring is installed on a different disk from the main Steam library, and may need to be reinstalled into the main Steam library location. Finally, make sure you're running `launchmod_eldenring.bat` and not `modengine2_launcher.exe`.

Mod Engine's launchmod script seems to be incompatible with running as administrator, so make sure "Run this program as an administrator" is unchecked in the Compatibility Properties for eldenring.exe while using Mod Engine. Otherwise, see "I've tried everything else" below.

*I launched the game with Mod Engine and item randomizer, but none of the items or starting classes are random.*

This could indicate two things: either `config_eldenring.toml` has not been edited with the right values, or the game has updated and so it no longer can use modded `regulation.bin` files meant for older versions of the game. If you're using a version of the randomizer from at least the latest game patch, then double-check Step 3a to make sure the config is correct.

This issue may arise if merged mods are from an older version of the game, in which case those mods need to be updated. This can still work in some cases, by loading into the main menu without any mods installed and then quitting out, or sometimes by moving the current ER0000.sl2 and letting the game re-create it.

*I loaded into the game and I keep seeing "?EventTextForMap?" or "Error: Mismatch between regulation.bin and other randomizer files".*

This is a failsafe in the randomizer when it's not properly installed, meaning it only detected some of the files it needs. It can happen when the required game files are not kept together (please see the warning in Step 4: **do not manually copy and paste individual files**), or when files are copy-pasted on top of the randomizer, or when the randomizer is not at the top of the mod list in `config_eldenring.toml`. It can also happen when the game files are re-randomized without fully shutting down the game, in which case you should exit to desktop and re-launch the game.

This can also indicate the case from "none of the items are random" where the randomizer version (or a merged regulation.bin mod) mismatches the game version.

If the issue is due to partial installation, the best way to resolve this is to rerun the randomizer and let it output files to their intended locations, and remove any previous instances of copy-pasted files. If you want to use other file-based mods with the randomizer, follow the instructions for using the separate "mod" directory. With the exception of other file-based randomizers and installers, **the randomizer should always be the last-installed mod**.

*I launched the game with Mod Engine and item/enemy randomizer, and item randomizer seems to work, but enemy randomizer doesn't.*

Make sure you followed the above installation instructions, including manually downloading the randomizer and extracting its files in the appropriate location. Do **not** use Vortex Mod Manager, because apparently it does not know how to properly install game files generated by the randomizer.

Assuming none of the other troubleshooting sections apply, this may indicate a bug in Mod Engine preview3, so see the "I've tried everything else" option.

*I've tried everything else and Mod Engine still doesn't work.*

Mod Engine is still in beta and there are a bunch of issues, and it is somewhat complicated to set up, so there are a few failsafes which seem to work for most people.

If you can spare the extra 50GB of disk space, you may want to consider using UXM installation. This almost always seems to work (assuming the game version matches the randomizer version), but has the disadvantage of being difficult to share with other people, and difficult to uninstall or merge with other mods.

An alternate Mod Engine setup involves putting Mod Engine in the game directory directly, which seems to work for unknown reasons. In this case, copy the files and directories inside of `ModEngine-2.0.0-preview3-win64` (including the `randomizer` directory) into the `ELDEN RING\Game` directory, then run `modengine2_launcher.exe` (*not* the bat file in this case) as administrator. Any confirmation of this is appreciated.

## Item logic

Important changes (and non-changes):

- Volcano Manor has an area you can access by dying to a grab attack at the bottom of Raya Lucaria. However, you can also get there from Volcano Manor's prison town, and vice versa. See [this video](https://streamable.com/je5mdi) for both paths.
- Varre's quest has been edited so you do not need to be online. Just talk to him twice in the Rose Church.
- Shop contents are randomized. All shop NPCs drop their own Bell Bearings which will match the contents of their shops.
- Optional NPC questlines are not required for key items. It is also never necessary to kill an NPC (so don't kill Patches!), and no good items are obtainable through NPC death. It is also never necessary to repeatedly farm enemies for key items.

If you find any bugs, especially those which make a run incompletable, you can upload your spoiler log in the discord server and I will take a look.

### Locations

Important locations are unmissable item locations that come in these required categories:

- Vanilla locations of key items: Locations of items you need to access all of the base game. See the key item list below. (Excludes the locations for Pureblood Knight's Medal and Carian Inverted Statue, because they're given by NPCs you can anger.)
- Major bosses: Every boss with [an achievement](https://www.trueachievements.com/game/Elden-Ring/achievements) for defeating them. (Excludes Lichdragon because he is missable.)

And these optional categories:

- Golden Seed trees: Every glowing golden tree with a Golden Seed under it.
- Sacred Tear churches: Every church with a statue that has a Sacred Tear under it.
- Merchant shops: Every shop with normal items, excluding those which only have sorceries or incantations or puppets.
- Minor bosses: Every enemy with a boss healthbar that drops an item. This includes a few arguably major bosses without achievements, like Full-Grown Fallingstar Beast, plus any enemy you can run into in the overworld or a minidungeon (unless excluded by the "Exclude caves" option).
- Vanilla locations of talisman treasures: Anywhere you can normally find a talisman on the ground or in a chest in the base game. This category excludes enemies which drop talismans.

Patches is an edge case in a few ways. He's not considered a boss and he doesn't drop anything valuable. He is considered a shop, but only if you don't select the "Exclude caves" option.

See the **[list of important locations](https://www.nexusmods.com/eldenring/articles/43)** for the full list broken down by area and category. It is a long list, so if you're unsure about whether a specific location is important or not, just use the definitions above.

There are over 3200 distinct item locations in Elden Ring. As of randomizer version v0.1, around 700 have handwritten descriptions and can contain important or noteworthy items. All of the rest currently have an automatically generated description referencing the closest map landmark.

### Key items

Key items are defined as items which unlock other unmissable items. They are only placed in important locations. These items, and no others, can block your progress in the randomizer until you find them. It's possible to use glitches to bypass some of these.

- Academy Glintstone Key: Used to access the Academy of Raya Lucaria. There are two versons of this item. Only the version that says "a glintstone key will remember its user" works here.
- Carian Inverted Statue: Used to progress in Carian Study Hall and reach the Divine Tower of Liurnia.
- Cursemark of Death: Used to access the Deathbed Dream in Deeproot Depths. (Note that the fight itself is missable and cannot have key items.)
- Dark Moon Ring: Used to progress to Moonlight Altar from Ainsel River.
- Dectus Medallion (Left and Right): Used to ascend the Grand Lift of Dectus. Other routes to enter Altus Plateau are also possible.
- Discarded Palace Key: Used to unlock the treasure chest in the Raya Lucaria Grand Library.
- Drawing-Room Key: Used to access the access the Volcano Manor Drawing Room and the lava area hidden within.
- Great Runes: By default, two of seven are required to enter Leyndell, either though Capital Outskirts or Deeproot Depths. With modified rulesets, any number of these can be required to enter Leyndell, enter Mountaintops, or access the final boss.
- Haligtree Secret Medallion (Left and Right): Used to access Consecrated Snowfield, the hidden path to the Haligtree.
- Imbued Sword Key: Used to unlock a Sending Gate at the Four Belfries in Liurnia. There are three of these in the game.
- Pureblood Knight's Medal: Used to immediately access Mohgwyn Palace without using the Sending Gate in Consecrated Snowfield.
- Rold Medallion: Used to access Mountaintops of the Giants after Leyndell. With modified rulesets, it can be replaced with a Great Runes requirement.
- Rusty Key: Used to progress into Stormveil Castle. This is optional, unless Gostoc dies before he opens the main gate for you.

### Other important items

The following items are not randomized: Flask of Crimson Tears, Flask of Cerulean Tears, Flask of Wondrous Physick, Spectral Steed Whistle, Spirit Calling Bell, Crafting Kit, Whetstone Knife, Lantern, Serpent-Hunter, and all map fragments. These are technically possible to randomize, but I'm not sure how to balance it, since they're critical to core game systems.

Talisman Pouches, Memory Stones, Whetblades are not key items but they're placed in the key item pool. Golden Seeds, Sacred Tears, and upgrade material Bell Bearings can also be assigned to important locations when selected in the randomizer.

Bell Bearings, wherever they're randomized, are placed in the following locations, matching their vanilla locations:

Smithing-Stone Miner's Bell Bearing [1]: In Liurnia
Smithing-Stone Miner's Bell Bearing [2]: In Altus Plateau
Smithing-Stone Miner's Bell Bearing [3]: In Mountaintops of the Giants
Smithing-Stone Miner's Bell Bearing [4]: In Farum Azula
Somberstone Miner's Bell Bearing [1]: In Caelid
Somberstone Miner's Bell Bearing [2]: In Altus Plateau
Somberstone Miner's Bell Bearing [3]: In Mountaintops of the Giants
Somberstone Miner's Bell Bearing [4]: In Farum Azula
Somberstone Miner's Bell Bearing [5]: In Farum Azula
Glovewort Picker's Bell Bearing [1]: In Mt. Gelmir
Glovewort Picker's Bell Bearing [2]: In Mountaintops of the Giants
Glovewort Picker's Bell Bearing [3]: In Farum Azula
Ghost-Glovewort Picker's Bell Bearing [1]: In Nokron, Eternal City
Ghost-Glovewort Picker's Bell Bearing [2]: In Ainsel River
Ghost-Glovewort Picker's Bell Bearing [3]: In Haligtree

Smithing Stones themselves can be distributed using the "Smithing Stone availability similar to base game" option. This places most stones of a given tier in the portion of the game appropriate to their level, with a few stragglers allowed elsewhere. It also adds a few new stones to guarantee 40-50 total item pickups per tier, slightly more if collectible materials are also randomized. When this option is not selected, their placement is completely random, and regular Smithing Stone upgrade weapons may not be viable.

Stones found in mining tunnels are currently only randomized within the tunnel they're found in. They will be optionally more spread out in future updates.

### Hints

There is an optional feature to purchase map marker hints when you get stuck. If enabled, they are available for purchase from Kalé in the Church of Elleh after reaching the Altus Plateau.

In short, if there is a required key item which is accessible but you haven't found it yet, you can purchase a hint for where to search for it. The first Site of Grace in the item's area will be marked, along with the overall name of the area. This costs 200 runes times your Rune Level, or 10k runes, whichever is higher.

There is also an option to mark item locations, down to the exact map coordinates of the treasure or enemy that drops it. You can purchase these only once you defeat the main healthbar boss of the item's area. (Usually this is the boss at the "end" of the area. For Liurnia overall it's Royal Knight Loretta, for Dragonbarrow and Altus Plateau it's their Godskin Apostle, for Mt. Gelmir it's Fallingstar Beast.) This costs 500 runes times your Rune Level, or 25k runes, whichever is higher.

OOT Randomizer-style hints are planned in the future, in the style of "a catacombs boss drops a key item" or "there are no key items in Leyndell".

## Enemy randomization

Enemy randomizer shuffles enemies and bosses within categories. By default, major bosses replace other bosses, minor bosses replace other minor bosses, regular enemies replace other regular enemies, and so on. Minibosses like night bosses, overworld dragon bosses, and evergaol bosses each have their own categories.

The categorization of major bosses in enemy randomizer is slightly different from the categorization in item randomizer. Some additional major bosses are added in enemy randomizer, like Fia's Champions, Fortissax, Gideon. Elemer of the Briar is a major boss in item randomizer, because he has an achievement, but is downgraded to a minor boss in enemy randomizer because room very small.

The warp from Raya Lucaria to Volcano Manor is changed so that getting consumed by the abductor enemy is no longer required. Instead, the only requirement is to die at the bottom of the elevator through any means.

Aside from default enemy categories, customization is possible if you check "Custom enemy placement" and then click "Edit custom enemy placement". Each custom configuration is called a preset and is saved as a file in the `presets` directory. Most common types of customization like "Merge non-major bosses" and "Replace wildlife with regular enemies" can be achieved by clicking on Quick Edit links at the bottom.

For each category, custom enemy pools can specify which enemies are randomly selected. Within a category, all pool frequencies add up to 100%. Click the "+" button to add a new pool. You can add enemies to pools, and when a pool is selected, an enemy in that pool will be randomly selected based on its frequency in the vanilla game. "Self" is synonymous with whichever category you're editing, so by default, all categories are replaced with 100% Self.

Finally, custom enemy placement also has "Oops All" mode, which replaces all enemies with a single boss or enemy type, and "Enemy Onslaught" which duplicates (or more) each enemy and boss. Onslaught can be used with or without randomization, by adding or removing "All Enemies and Bosses" from being randomized or not.

## Special thanks

Thanks to TKGP and katalash for SoulsFormats, Meowmaritus for SoulsFormats EMEVD editing and initial UXM, and HotPocketRemix for EMEVD instruction reversing. Thanks to everyone in ?ServerName? for being helpful and informative, and everyone in the rando discord for brainstorming ideas.

Thanks to katalash for creating Mod Engine and giving special permission to distribute it with this mod.

Finally, thanks to Saltyfish_King, Fababfan, Plante, proteh, Leekos, Kuroko, 刀子, SaniTOS, and HiJacked for contributing translations and permission to use them in this mod.

Source code is published to https://github.com/thefifthmatt/SoulsRandomizers and updated occasionally. Thanks to nex3 for creating a DSMS-compatible SoulsFormats fork to make this possible.
