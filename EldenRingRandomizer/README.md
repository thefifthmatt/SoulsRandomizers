It's an offline item randomizer for Elden Ring which modifies the files loaded on game startup. A full enemy and boss randomizer is also planned.

The following items are supported for randomization:

- Items found in the world, including key items
- Enemy and boss drops, including key items
- Shop items
- Character starting weapons and armor
- And more!

Normally, all you need to complete the game is two Great Runes and the Rold Medallion. The key item randomizer can chain key items together, especially when the bias slider is set high. There's also a mode to collect all 7 Great Runes before you can challenge the final boss.

If you would like to provide feedback or playtest, you can join the discord server at https://discord.gg/QArcYud (also for Fog Gate Randomizer, Sekiro Randomizer, DS3 Static Randomizer). This mod is under development, and it will update when the game updates, so please check this mod page and the server for updates.

For additional randomization, see Luke's [Item and Param Randomiser](https://www.nexusmods.com/eldenring/mods/97). It should be compatible with this mod if you disable randomizing items and shops in it.

**Translators wanted!** If you're a native speaker of any language supported by Elden Ring, fill in the files in `diste\Messages` to help make this mod available in other languages. Please contact me if features are needed like font changes or space adjustments.

## Installation

The main installation path uses Mod Engine 2, available from GitHub. An alternate UXM installation path is also available. Both of these tools are in beta and are maintained by users in the FromSoftware modding server ?ServerName? (https://discord.gg/mT2JJjx).

### 1. Back up your save file if you haven't already

Your Elden Ring save file is found in your Windows user directory. Start from "Local Disk" and find `C:\Users\<user name>\AppData\Roaming\EldenRing\<save id>`. Make a copy of ER0000.sl2 and make sure you restore it before going back online. This also happens automatically the first time you click "Randomize", but if you want to be sure, do it manually!

Additionally, if you don't want randomizer runs to be interrupted or invalided by game updates, consider going offline on Steam for the duration of the run.

### 2a. Mod Engine

This option requires using the [beta version of Mod Engine 2](https://github.com/soulsmods/ModEngine2/releases) for Elden Ring, which may be simpler than UXM if you're familiar with Mod Engine from previous titles like DS3 and Sekiro.

The primary difference between Mod Engine and Mod Engine 2 is that *mod directories can be anywhere now*. You don't have to touch your game directory! So you should download Mod Engine 2 to anywhere, and then download EldenRingRandomizer.zip *inside* of the Mod Engine folder.

To use Mod Engine 2, uncheck "Output files for UXM", in which case all randomized files will be written directly into the `randomizer` directory. It can then be directly used as the Mod Engine mod directory. Either edit `eldenring.config.toml` to change the default mod directory, or add the randomizer as a new mod directory, or rename the randomizer directory to match the config. For instance, [this configuration](https://cdn.discordapp.com/attachments/936393685585780846/960810154394284052/unknown.png) should work.

Then, instead of launching the game directly, run `launchmod_eldenring.bat`. This bypasses EAC and launches the game with the mod loaded. (UXM mods may still be active, but randomizer files will override those mods' files.)

### 2b. UXM

**Skip this step if you're using Mod Engine.** To use UXM, unpack and patch the game using UXM. Unpacking the game requires around 50 GB of disk space.

The most official UXM version that exists is in #tools-and-resources in ?ServerName? ([link for Elden Ring 1.03.2 and 1.03.3](https://discord.com/channels/529802828278005773/529900741998149643/955650211215204433), after [joining the server](https://discord.gg/mT2JJjx)). You should also make sure you can bypass EAC when you launch the game, or else Elden Ring with refuse to start with the modified exe. You can find guides for this online.

It doesn't matter where you unzip EldenRingRandomizer.zip. When you check "Output files for UXM", randomization will write all files to the selected game directory. It will also create backups of all of the files it replaces. For instance, the existing regulation.bin will be copied to regulation.bin.randobak.

### 3. Select options

Run EldenRingRandomizer.exe and select your Elden Ring game exe. Select "Output files for UXM" only if you're using UXM.

Then, select randomization options. "Important locations" are all of the places you might have to check to finish the game or get essential upgrades. As the mod gets updated, more locations will become eligible for this. See more information about key items and locations below.

To restore all options to default, click "Set options from string", delete all of the text in the text field, and click "Select". To get options/seed from someone else, click "Set options from string" and paste that text instead.

The "Merge existing param mods" option is for temporary merging support until Mod Engine supports merging. If enabled, randomizer will edit params starting from `regulation.bin` in the game directory. After that, it will use `regulation.bin.randobak` so it doesn't re-randomize the same file.

### 4. Randomize!

Click "Randomize new run!" (Or click "Run with fixed seed" if you specified a seed and unchecked "Reroll seed")

This creates a file in the `spoiler_logs` directory which contains hints and spoilers.

### 5. Run the game

Close the game if it's currently running, then launch the game. (If using Mod Engine, *do not launch from Steam*, instead launch `launchmod_eldenring.bat`.)

The initial character selection images won't change, but the detailed character preview screen should have different weapons and armor. The first random item in the game will be beside the Stranded Graveyard Site of Grace.

### 6. Uninstall

If you used UXM to install randomizer, open EldenRingRandomizer.exe and click "Restore backups" to uninstall. Review the files and click "OK" to restore all of the backups. This is also required if you're switching from UXM to Mod Engine.

If you used Mod Engine, simply don't launch the game with Mod Engine anymore, or edit the Mod Engine config or mod folder contents.

Make sure you restore your backed up save file before going online or else you will definitely get banned!

## Item logic

Important changes (and non-changes):

- Volcano Manor has an area you can access by dying to a grab attack at the bottom of Raya Lucaria. However, you can also get there from Volcano Manor's prison town, and vice versa. See [this video](https://streamable.com/je5mdi) for both paths.
- Varre's quest has been edited so you do not need to be online. Just talk to him twice in the Rose Church.
- Shop contents are randomized. All shop NPCs drop their own Bell Bearings which will match the contents of their shops.
- Optional NPC questlines are not required for key items. It is also never necessary to kill an NPC (so don't kill Patches!), and no good items are obtainable through NPC death. It is also never necessary to repeatedly farm enemies for key items.

Planned future features include an cookbook randomization, glitched logic, and of course an enemy randomizer. Feedback is greatly appreciated.

If you find any bugs, especially those which make a run incompletable, you can upload your spoiler log in the discord server and I will take a look. Otherwise, open the spoiler log and send me the first line (the one that starts with "Options and seed").

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

See [the list of important locations](https://www.nexusmods.com/eldenring/articles/43) for the full list broken down by area and category. It is a long list, so if you're unsure about whether a specific location is important or not, just use the definitions above.

There are over 3200 distinct item locations in Elden Ring. As of randomizer version v0.1, over 550 have handwritten descriptions and can contain important or noteworthy items. All of the rest currently have an automatically generated description referencing the closest map landmark.

### Key items

Key items are defined as items which unlock other unmissable items. They are only placed in important locations. These items, and no others, can block your progress in the randomizer until you find them. It's possible to use glitches to bypass some of these.

- Academy Glintstone Key: Used to access the Academy of Raya Lucaria. There are two versons of this item. Only the version that says "a glintstone key will remember its user" works here.
- Carian Inverted Statue: Used to progress in Carian Study Hall and reach the Divine Tower of Liurnia.
- Cursemark of Death: Used to access the Deathbed Dream in Deeproot Depths. (Note that the fight itself is missable and cannot have key items.)
- Dark Moon Ring: Used to progress to Moonlight Altar from Ainsel River.
- Dectus Medallion (Left and Right): Used to ascend the Grand Lift of Dectus. Other routes to enter Altus Plateau are also possible.
- Discarded Palace Key: Used to unlock the treasure chest in the Raya Lucaria Grand Library.
- Drawing-Room Key: Used to access the access the Volcano Manor Drawing Room and the lava area hidden within.
- Great Runes: Two are required to enter Leyndell, either though Capital Outskirts or Deeproot Depths. All seven are required to fight the final boss if that option is selected.
- Haligtree Secret Medallion (Left and Right): Used to access Consecrated Snowfield, the hidden path to the Haligtree.
- Imbued Sword Key: Used to unlock a Sending Gate at the Four Belfries in Liurnia. There are three of these in the game.
- Pureblood Knight's Medal: Used to immediately access Mohgwyn Palace without using the Sending Gate in Consecrated Snowfield.
- Rold Medallion: Used to access Mountaintops of the Giants after Leyndell.
- Rusty Key: Used to progress into Stormveil Castle. This is optional... unless Gostoc dies before he opens the main gate for you.

### Other important items

The following items are not randomized: Flask of Crimson Tears, Flask of Cerulean Tears, Flask of Wondrous Physick, Spectral Steed Whistle, Spirit Calling Bell, Crafting Kit, Whetstone Knife, Lantern, Serpent-Hunter, and all map fragments. These are technically possible to randomize, but I'm not sure how to balance it, since they're critical to core game systems.

Talisman Pouches and Whetblades are not key items but they're placed in the key item pool. Golden Seeds, Sacred Tears, and upgrade material Bell Bearings can also be assigned to important locations when selected in the randomizer.

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

Otherwise, upgrade materials have no placement logic. Collectible materials are currently only randomized within the tunnel they've found in. Suggestions are appreciated for how to balance this better.

### Hints

There is an optional feature to purchase map marker hints when you get stuck. If enabled, they are available for purchase from Kalé in the Church of Elleh after reaching the Altus Plateau.

In short, if there is a required key item which is accessible but you haven't found it yet, you can purchase a hint for where to search for it. The first Site of Grace in the item's area will be marked, along with the overall name of the area. This costs 200 runes times your Rune Level, or 10k runes, whichever is higher.

There is also an option to mark item locations, down to the exact map coordinates of the treasure or enemy that drops it. You can purchase these only once you defeat the main healthbar boss of the item's area. (Usually this is the boss at the "end" of the area. For Liurnia overall it's Royal Knight Loretta, for Dragonbarrow and Altus Plateau it's their Godskin Apostle, for Mt. Gelmir it's Fallingstar Beast.) This costs 500 runes times your Rune Level, or 25k runes, whichever is higher.

OOT Randomizer-style hints are planned in the future, in the style of "a catacombs boss drops a key item" or "there are no key items in Leyndell".

## Special thanks

Thanks to TKGP for SoulsFormats, Meowmaritus for SoulsFormats EMEVD editing and quickhack UXM, and HotPocketRemix for EMEVD instruction reversing. Thanks to everyone in ?ServerName? for being helpful and informative, and everyone in the rando discord for brainstorming ideas.

The source code will be partly published to https://github.com/thefifthmatt/SoulsRandomizers soon. The full thing won't be available until it can migrate to MSBE.
