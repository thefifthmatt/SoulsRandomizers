It's an offline item randomizer for DS3 which modifies the static files loaded on game startup, including key item locations and shops, turning the game into a scavenger hunt. **Only supports the latest game patch, installed legitimately from Steam. Use with default modengine options (offline, alternate save file) to prevent bans.** This is not the only DS3 item randomizer - for a more dynamic experience, see Luke's [DS3 Item Randomizer and Auto-Equip mod](https://www.nexusmods.com/darksouls3/mods/241).

The following are all randomized with each other:

- Items found in the world, including key items
- Enemy and boss drops, including key items
- Shop items
- Path of the Dragon gesture
- Items given by NPCs
- Character starting weapons and armor
- And more!

There are also difficulty settings. The most important setting is a bias slider, which can place desirable items completely randomly, or in difficult and unfair locations behind long item chains, or somewhere in between. There are also sequence break settings - in the most punishing variant, it is possible for e.g. Twin Princes to drop Small Lothric Banner, or Gael to drop Small Doll. And Cinders of a Lord can be potentially anywhere.

## Installation

To use the mod, you must first install DS3 Mod Engine, and then run the randomizer executable.
1. Install [DS3 Mod Engine](https://www.nexusmods.com/darksouls3/mods/332). This requires the latest patch, installed legitimately from Steam.
2. Unzip DS3StaticItemRandomizer.zip directly into your DS3 game directory - "C:\Program Files (x86)\steam\steamapps\common\DARK SOULS III\Game" by default. The zip archive contains only a folder called 'randomizer', which contains the randomizer executable and all other files. This folder must be added directly to your game directory, not in a subdirectory.
3. Close DS3 if it's running. The param files are only loaded when the game starts up.
4. In Game\randomizer, run DS3StaticItemRandomizer.exe. Choose your settings, click "Randomize", and wait for "Done" in the status bar.
5. Back in the game directory, edit modengine.ini to change modOverrideDirectory="\randomizer" and useModOverrideDirectory=1. Make sure you set useAlternateSaveFile=1 and blockNetworkAccess=1.
6. Launch DS3, and start a new save file! (If just installed Mod Engine, go offline in settings.) If the starting character weapons and armor are different, then it's working. You may soft lock if you use re-randomize in the middle of a playthrough.

To uninstall the mod, disable useModOverrideDirectory. To remove Mod Engine altogether, delete input8.dll.

The randomizer has limited or no compatibility with other mods, depending on how they are implemented. If its files are overridden by other mods, it is highly likely the randomizer will either have no effect or result in a softlock. See below for details.

## Logic

Important changes (and non-changes):

- Handmaid's shop and unmissable ashes/shops can contain key items. NPC shops considered unmissable are the initial shops for Greirat, Cornyx, Irina, Karla, and the Stone-humped Hag. NPCs with shops always drop their own ashes. (One softlock possibility - make sure to buy key items from Greirat before sending him away, in case you can't progress his questline.)
- Optional NPC questlines are not required for key items: you can still kill anyone. But it is never necessary to kill an NPC (except Emma), and items only obtainable through NPC death are usually not very good.
- Crow items are randomized into upgrade materials. Boss weapons are available are a boss is defeated and their soul is found. Transposition gives unique items, usually pretty good ones.
- All starting classes are given 10 intelligence for free so that Orbeck can be recruited. Starting stats for classes other than Deprived may be slightly increased for better starting weapons, and starting SL increased the same amount.

Unusual locations

- All NG+ locations have been enabled in the base NG. Check the NG+1 and NG+2 tabs in http://darksouls3.wikidot.com/rings if you are unfamiliar - some are very well hidden.
- Grave Key is not necessary to access the ravine/skeleton tomb in Undead Settlement - unequip everything and drop down around where Eygon of Carim is sitting.
- Some item locations are guaranteed one-time drops from killing certain enemies, like Pus of Man after they transform, DLC NPC invasions, the last Cathedral Knight before the Oceiros fight, the trio of Winged Knights in the Archives, Crystal Lizards, etc. It is never necessary to repeatedly farm enemies for key items.
- Some item locations only show up after you've defeated an enemy elsewhere, like the locations for Drakeblood Set, Havel's Set, Hornet Ring, Archives Key, and many many Handmaid shop items.

## Mod compatibility

The randomizer is a file-based mod. It is incompatible with any other mods which:

- Edit game params (`Data0.bdt` or `param\gameparam\parambnd.dcx`)
- Edit event scripts (in `event\`)
- Edit maps (in `map\`) - with the exception of mods which only displace non-boss enemies, such as StraySouls
- Edit English messages (in `msg\engus\item_dlc2.msgbnd.dcx`) - however, you can safely override this with mods such as Poorly Translated mod, since it's only needed for minor cosmetic reasons.

The randomizer is probably compatible with mods editing any other files, such as cosmetic/reskin mods, as well as any cheat engine-based mods which don't modify item drops.

[StraySouls](https://www.nexusmods.com/darksouls3/mods/318) is a mod which randomizes enemy locations within a map. It can be used with the item randomizer for some extra randomization, although be warned that the item randomizer is balanced around the base game, so some important item locations may be trivialized and vice versa. To install both:

1. Install UXM and StraySouls. Set loadUXMFiles=1 in modengine.ini.
2. Before doing any enemy randomizations in StraySouls, run the `backup` command
3. Randomize enemies. `random -m -o` seems to be the most fun/difficult. Make sure to `restore` before any subsequent randomizations, or the game may softlock.
4. Run the item randomizer. Make sure to check the setting "Do not depend on the location of non-boss enemies". This disables some features in the item randomizer and more locations are marked as missable.

## Bugs

Please report bugs! @ me on Discord (thefifthmatt#1614) or on Twitter (thefifthmatt), and send me info about the item/location that had an issue. Also send a re-zipped randomizer.zip with the randomization that is responsible for the bug, especially if it's a softlock. Some examples of bugs are:

- Softlock: Key item required to beat the game (Soul of Cinder) is not available
- Item received at location does not match spoiler log
- Location description is confusing or wrong

## Special thanks

Thanks to TKGP and Pav for SoulsFormats and the layout files for all game data, HotPocketRemix for DSEventScriptTools for scripting changes and identifying scripted items, katalash for ModEngine and DSTools for identifying locations, and too many people to mention in Fromsoft modding discord (https://discord.gg/mT2JJjx) for being so helpful

## Fun technical details

You're on Github, so maybe you're interested in how this whole thing works

There are two starting points for the randomizer. The first is of course in Program.cs, which calls into Randomizer.cs, which executes each phase of the randomizer. The second is in dist/Base/annotations.yaml, which
has nearly all configuration for specific items and locations. You can edit it to mod the mod, but beware it's not very well documented and you can induce crashes pretty easily.

The core algorithm for the randomizer is:

1. Calculate a list of all obtainable items and a list of all accessible item locations.
2. Partition the list items into different silos. The main ones are:
    - The 'finite' silo contains guaranteed one-time items like world pickups, finite shop items, and some enemy/boss drops. This includes key items and most quest items.
    - The 'infinite' silo contains infinite shop items and farmable enemy drops.
    - The 'mixed' silo contains finite shop items which can become infinite shop items.
    - The 'self' silo contains items which are always randomized to themselves, for softlock/balance reasons.
3. Assign all items which affect the availabity of other locations to logical areas.
    - Key items are items which block access to areas; they may be required to complete the game. Key items are placed one by one in a random order, with Cinders always placed last.
      Items can only be placed in areas which do not unconditionally require that item. Area requirements are updated as the algorithm progresses. For instance, once Contraption Key is placed in Irithyll, Small Doll cannot drop from Friede.
      Placing an item in an area will make that area less likely to be chosen in the future and make areas depending on that item more likely. The severity of this depends on bias %.
    - Quest items usually block access to a handful of item locations, and do not block completing the game. They are assigned randomly, sometimes with custom restrictions.
4. Within each silo, assign weights to all items by desirability, and assign weights to all locations based on difficulty and lateness. Run a weighted analogue of Fisher-Yates on both lists. This is like a fuzzy sort by decreasing weight.
   Bias % is used to control the disparity between weights (all same, or in the range of 1x to 2x, or in the range of 1x to 1000000x).
5. Zip together the item list and the location list. At each step check if the item is allowed to go into the location (mainly due to prior area assignment/distribution requirements for that item); if it isn't, put it in a queue.
   Check the queue for each new location seen. Assign leftover items randomly to locations.
6. For each silo, for each location, override item data at that location with the newly assigned items.
   Calculate shop prices by randomly selecting from prices of similar items. Calculate drop rates from a combination of base game drop rate and enemy difficulty scaling.
