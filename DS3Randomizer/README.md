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
1. Install [DS3 Mod Engine](https://www.nexusmods.com/darksouls3/mods/332) (note: you must use the version from DS3 Enemy Randomizer if you plan on running that, to avoid crashes!). This requires the latest patch, installed legitimately from Steam. You should edit modengine.ini to change modOverrideDirectory="\randomizer" and useModOverrideDirectory=1. Make sure useAlternateSaveFile=1 and blockNetworkAccess=1 are also set to avoid irreversable bans.
2. Unzip DS3StaticItemRandomizer.zip directly into your DS3 game directory - "C:\Program Files (x86)\steam\steamapps\common\DARK SOULS III\Game" by default. The zip archive contains only a folder called 'randomizer', which contains the randomizer executable and all other files. This folder must be added directly to your game directory.
3. Install any other mods you want to use, such as [DS3 Enemy Randomizer](https://www.nexusmods.com/darksouls3/mods/484) or [Poorly Translated Mod](https://www.nexusmods.com/darksouls3/mods/316), in the 'mod' directory, like you would normally. Item randomizer can only use these mods if they are compatible, i.e. if critical game items are found in roughly their expected locations.
4. In the 'randomizer' folder, run DS3Randomizer.exe to open the UI. Choose your settings, selecting "Merge mods" if you have other mods to merge in, click "Randomize", and wait for "Done" in the status bar. This will also create a hint/spoiler text file in the 'spoiler_logs' folder for that run, which should be the last file alphabetically.
5. Close DS3 if it's running. The param files are only loaded when the game starts up.
6. Launch DS3, and start a new save file! (If just installed Mod Engine, go offline in settings to avoid a 10 second wait at the start.) If the starting character weapons and armor are different, then it's working. You may soft lock if you change the item randomizer seed or options in the middle of a playthrough.

In order to alter other mods in the middle of a playthrough, e.g. to reroll DS3 Enemy Randomizer, you should change those files in the 'mod' directory, and then run item randomizer again with the same seed and settings. Note that for Enemy Randomizer in particular, you can also temporarily rename the 'map' folder to disable it, e.g. if the Ariandel Chapel basement crashes.

To keep item randomizer while uninstalling other mods, you can delete other mods' files (basically every directory except for the 'dist' directory) and then rerun item randomizer with the same seed and settings. A full list of extra files can be found at the bottom of the spoiler log.

To uninstall the mod, change useModOverrideDirectory back. To remove Mod Engine altogether, delete input8.dll.

## Logic

Important changes (and non-changes):

- Handmaid's shop can contain key items, and unmissable ashes/shops can contain key items if shop locations are enabled. NPC shops considered unmissable are the initial shops for Greirat, Cornyx, Irina, Karla, and the Stone-humped Hag. NPCs with shops always drop their own ashes. (One softlock possibility - make sure to buy key items from Greirat before sending him away, in case you can't progress his questline.)
- Optional NPC questlines are not required for key items. It is also never necessary to kill an NPC (except Emma), and no good items are obtainable through NPC death.
- Crow items are randomized into upgrade materials. Boss weapons are available after a boss's soul is found. Transposition gives unique items, usually pretty good ones.
- All starting classes are given 10 intelligence for free so that Orbeck can be recruited. Starting stats for classes other than Deprived may be slightly increased for better starting weapons, and starting SL increased the same amount.

Unusual locations:

- Grave Key is not necessary to access the ravine/skeleton tomb in Undead Settlement - unequip everything and drop down around where Eygon of Carim is sitting.
- It is never necessary to repeatedly farm enemies for key items. Powerful non-boss enemies which normally have one-time guaranteed item drops can drop key items if that option is enabled. This includes mimics,  hostile human NPCs, Ravenous Crystal Lizards, Outrider Knights, Greater Crabs, and others. A full list is below. Crystal Lizards can also be enabled for key item drops.
- Some item locations only show up after you've defeated an enemy elsewhere, like the locations for Drakeblood Set, Havel's Set, Hornet Ring, Archives Key, and many Handmaid shop items.
- You can enable NG+ locations in the base NG, in which case you can find more powerful rings, at the cost of more hidden locations. Check the NG+1 and NG+2 tabs in http://darksouls3.wikidot.com/rings if you are unfamiliar with the locations.

If you find any bugs, especially those which make a run uncompleteable, you can send me info about your run. Do not send me only the seed, since item locations heavily depend on settings. Instead, just copy the first line from the spoiler log.

## Special thanks

Thanks to TKGP and Pav for SoulsFormats and the layout files for all game data, HotPocketRemix for DSEventScriptTools for scripting changes and identifying scripted items, katalash for ModEngine and DSMapStudio for identifying locations, and too many people to mention in Fromsoft modding discord (https://discord.gg/mT2JJjx) for being so helpful. The newer version also incorporates EMEVD editing thanks to Meowmaritus.

## Key items

Key items are defined as items which unlock other unmissable items. They are only placed in unmissable locations, which excludes many of the game's non-main quests. These items, and no others, can block your progress in the randomizer until you find them, with the ultimate goal of defeating Soul of Cinder and getting to the credits.

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

Spook and Tears of Denial are also considered key items if the corresponding speedrunner skips are enabled.

Coals and Transposing Kiln are not key items exactly but are placed in the same pool as them. There is an option for Estus Shards and Undead Bone Shards to also be placed in the same pool.

## Key item locations
Some items have the option to be placed in select locations. This can be used for shorter runs or live races. If enabled, all of the same item logic still applies, so it shouldn't be possible to get softlocked. The minimal set of locations are the vanilla locations of bosses, healing upgrades, coals, key items, and ending items. You can check checkboxes to enable more such locations.

In Cemetery of Ash: Dropped by Iudex Gundyr. Replaces Coiled Sword.
In Firelink Shrine: Sold by both Shrine Handmaid and Untended Graves Handmaid. Replaces Tower Key.
In Firelink Shrine Roof: In the Firelink Shrine rafters, accessible from the roof. Replaces Estus Shard.
In Firelink Bell Tower: At the top of the Bell Tower. Replaces Fire Keeper Soul.
In High Wall: In the basement of the building with the Pus of Man on the roof, on the blacksmith anvil. Replaces Estus Shard.
In High Wall: In the basement of the building with Pus of Man on the roof, down the stairs guarded by a dog. Replaces Cell Key.
In High Wall: Given by Emma, or dropped upon death. Replaces Small Lothric Banner.
In High Wall: Dropped by Emma upon killing her. This is possible to do at any time. Replaces Basin of Vows.
In High Wall: Dropped by Vordt of the Boreal Valley. Replaces Soul of Boreal Valley Vordt.
In Undead Settlement: In front of the burning tree guarded by the Cathedral Evangelist. Replaces Estus Shard.
In Undead Settlement: In the area past the Dilapidated Bridge bonfire, where the Giant is shooting arrows, jumping to the floating platform on the right. Replaces Undead Bone Shard.
In Undead Settlement: Dropped by Curse Rotted Greatwood. Replaces Soul of the Rotted Greatwood.
In Road of Sacrifices: Behind the building leading to Crystal Sage, approached from Crucifixion Woods bonfire. Can drop down on left side of go under bridge on right side. Replaces Estus Shard.
In Road of Sacrifices: At the end of the Farron Keep Perimeter building on Crucifixion Woods side, behind the Black Knight. Replaces Farron Coal.
In Road of Sacrifices: Dropped by Crystal Sage. Replaces Soul of a Crystal Sage.
In Farron Keep: Straight ahead from the Farron Keep bonfire to the ritual fire stairs, guarded by a slug. Replaces Estus Shard.
In Farron Keep: In the pavillion guarded by a Darkwraith, straight ahead from the Farron Keep bonfire to the left of the ritual fire stairs. Replaces Sage's Coal.
In Farron Keep: In a standalone pavillion down the ramp from Keep Ruins bonfire and to the right. Replaces Undead Bone Shard.
In Farron Keep: Dropped by Abyss Watchers. Replaces Cinders of a Lord (Abyss Watchers).
In Cathedral of the Deep: Right outside of the Cleansing Chapel. Requires killing praying hollows. Replaces Estus Shard.
In Cathedral of the Deep: In the graveyard with the Infested Corpses, on a coffin partly hanging off of the ledge. Replaces Undead Bone Shard.
In Cathedral of the Deep: Dropped by Deacons of the Deep. Replaces Small Doll.
In Catacombs of Carthus: Dropped by first Skeleton Ball after killing its sorcerer skeleton. Replaces Undead Bone Shard.
In Catacombs of Carthus: Dropped by High Lord Wolnir. Replaces Soul of High Lord Wolnir.
In Smouldering Lake: Dropped by the giant Carthus Sandworm. Replaces Lightning Stake.
In Smouldering Lake: In the close end of the Demon Ruins main hall, right below a Smouldering Writhing Flesh. Replaces Undead Bone Shard.
In Smouldering Lake: Behind an illusory wall and Smouldering Writhing Flesh-filled corridor from Antechamber bonfire. Replaces Estus Shard.
In Smouldering Lake: Dropped by Old Demon King in Smouldering Lake. Replaces Soul of the Old Demon King.
In Irithyll: In the graveyard down the stairs from the Church of Yorshka, behind the grave with the Corvian. Replaces Undead Bone Shard.
In Irithyll: Dropped by Pontiff Sulyvahn. Replaces Soul of Pontiff Sulyvahn.
In Irithyll: On the Giant Blacksmith's corpse in Anor Londo. Replaces Giant's Coal.
In Irithyll: In a chest on the floor of the Anor Londo cathedral. Replaces Estus Shard.
In Irithyll: Dropped by Aldrich. Replaces Cinders of a Lord (Aldrich).
In Irithyll Dungeon: In the cell of the top corridor opposite to the bonfire in Irithyll Dungeon. Replaces Jailbreaker's Key.
In Irithyll Dungeon: Dropped by the mimic in the room after the outside area of Irithyll Dungeon overlooking Profaned Capital. Replaces Estus Shard.
In Irithyll Dungeon: In a chest found going past the Giant Slave to the sewer with the rats and the basilisks, up the stairs to the end, on the right side. Replaces Old Cell Key.
In Irithyll Dungeon: In the room with the Wretches next to the main Jailer cell block, guarded by a Wretch. Replaces Profaned Coal.
In Profaned Capital: Dropped by Yhorm the Giant. Replaces Cinders of a Lord (Yhorm).
In Profaned Capital: On the corpse of Laddersmith Gilligan next to the Profaned Capital bonfire. Replaces Undead Bone Shard.
In Profaned Capital: Past the Profaned Capital Court Sorcerer, in the corridor overlooking the Irithyll Dungeon Giant Slave area. Replaces Jailer's Key Ring.
In Lothric Castle: Dropped by Dancer of the Boreal Valley. Replaces Soul of the Dancer.
In Lothric Castle: Dropping down from the bridge where the Pus of Man wyverns breathe fire on the far side from the bonfire. Replaces Undead Bone Shard.
In Lothric Castle: Dropped by Dragonslayer Armour. Replaces Soul of Dragonslayer Armour.
In Lothric Castle after Yhorm and Aldrich: Before the door to the Grand Archives after Aldrich and Yhorm are killed. Replaces Grand Archives Key.
In Consumed King's Gardens: From the middle level of the first Consumed King's Gardens elevator, out the balcony and to the right. Replaces Estus Shard.
In Consumed King's Gardens: Dropped by Consumed King Oceiros. Replaces Soul of Consumed Oceiros.
In Untended Graves: Dropped by Champion Gundyr. Replaces Soul of Champion Gundyr.
In Untended Graves: Behind an illusory wall, in the same location Irina sits in Firelink Shrine. Replaces Eyes of a Fire Keeper.
In Grand Archives: On the corpse of a sitting Archives Scholar on a balcony high in the Archives overlooking the area with the Grand Archives Scholars with a shortcut ladder, near the final wax pool. Replaces Undead Bone Shard.
In Grand Archives: On the Archives roof near the three Winged Knights, in a side area overlooking the ocean. Replaces Estus Shard.
In Grand Archives: Dropped by Twin Princes. Replaces Cinders of a Lord (Lothric).
In Archdragon Peak: Dropped by Ancient Wyvern. Replaces Dragon Head Stone.
In Archdragon Peak: Dropped by Nameless King. Replaces Soul of the Nameless King.
In pre-Contraption Ariandel: Dropped by Champion's Gravetender. Replaces Valorheart.
In pre-Contraption Ariandel: Dropped by Sir Vilhelm. Replaces Contraption Key.
In post-Contraption Ariandel: One-time drop after killing Father Ariandel and Friede (phase 2) for the first time. Replaces Titanite Slab.
In post-Contraption Ariandel: Dropped by Sister Friede. Replaces Soul of Sister Friede.
In Dreg Heap: Dropped by Demon Prince. Replaces Soul of the Demon Prince.
In Dreg Heap: Found in the small room after beating Demon Prince. Replaces Small Envoy Banner.
In Ringed City: Dropped by Halflight, Spear of the Church. Replaces Filianore's Spear Ornament, Titanite Slab.
In Ringed City: Dropped by Darkeater Midir. Replaces Soul of Darkeater Midir, Spears of the Church.
In Filianore's Rest: Dropped by Slave Knight Gael. Replaces Soul of Slave Knight Gael.
In Kiln of the First Flame: Dropped by Soul of Cinder. Replaces Soul of the Lords.

The chest option includes these locations (some of which are included in other groups as well):

In Firelink Shrine Roof: From the Firelink Shrine roof, past the rafters and an illusory wall. Replaces Covetous Silver Serpent Ring.
In High Wall: In the building with the Pus of Man on the roof, past the Lothric Knight down a hallway obscured by a wooden wheel, dropping down past the edge. Replaces Astora Straight Sword.
In High Wall: In the chest on the balcony overlooking the basement of the building with the Pus of Man on the roof. Replaces Silver Eagle Kite Shield.
In Undead Settlement: In a chest after Fire Demon. Cage Spiders activate open opening it. Replaces Human Pine Resin.
In Farron Keep: In a chest in the cave found along the keep wall in the basilisk area, with the Elizabeth corpse. Replaces Antiquated Dress.
In Smouldering Lake: In a chest past an illusory wall to the left of the Large Hound Rat in Demon Ruins, before the basilisk area. Replaces Large Titanite Shard.
In Irithyll: In a chest up the stairs in the room with the Silver Knight staring at the painting. Replaces Leo Ring.
In Irithyll: In a chest in the rafters of the dark area with the Irithyllian slaves. Replaces Yorshka's Spear.
In Irithyll: In a chest in Darkmoon Tomb. Replaces Reversal Ring.
In Irithyll: In a chest up the stairs in the room with the Silver Knight staring at the painting. Replaces Smough's Great Hammer.
In Irithyll: In a chest up the stairs in the room with the Silver Knight staring at the painting. Replaces Divine Blessing.
In Irithyll: In a chest on the floor of the Anor Londo cathedral. Replaces Estus Shard.
In Irithyll Dungeon: In a chest found going past the Giant Slave to the sewer with the rats and the basilisks, up the stairs to the end, on the right side. Replaces Old Cell Key.
In Profaned Capital: To the right of the Profaned Flame, in the room with the many Jailers looking at the mimics. Replaces Ember.
In Lothric Castle: In a chest right after the Lothric Castle bonfire. Replaces Hood of Prayer.
In Lothric Castle: In a chest in the basement with the Outrider Knight. Replaces Twinkling Titanite.
In Lothric Castle: In a chest in the basement with the Outrider Knight. Replaces Titanite Scale.
In Lothric Castle: In a chest in the basement with the Outrider Knight. Replaces Spirit Tree Crest Shield.
In Lothric Castle: In a chest in the chapel to the right of the Dragonslayer Armour fight. Replaces Titanite Scale.
In Lothric Castle: In a chest climbing the ladder to the rooftop outside the Dragonslayer Armour fight, continuing the loop past the Red-Eyed Lothric Knight. Replaces Titanite Scale.
In Consumed King's Gardens: Chest after Oceiros fight. Replaces Titanite Scale.
In Consumed King's Gardens: Chest after Oceiros fight. Replaces Titanite Scale.
In Grand Archives: Behind a bookshelf in the dark room with the Crystal Lizards, moved by a lever in the same room. Replaces Witch's Locks.
In Grand Archives: In a chest on the Archives first floor, behind a bookshelf moved by pulling a lever in the middle of the second floor between two cursed bookshelves. Replaces Titanite Slab.
In Grand Archives: In a chest after the first elevator shortcut with the movable bookshelf, in the area with the Grand Archives Scholars, to the left of the stairwell leading up to the roof. Replaces Titanite Scale.
In Grand Archives: In a chest reachable after dropping down from the Archives rafters and down a ladder near the Corpse-grub. Replaces Twinkling Titanite.
In Grand Archives: In a chest reachable after dropping down from the Archives rafters and down a ladder near the Corpse-grub. Replaces Divine Blessing.
In Archdragon Peak: In the chest before the ladder climbing up to the Havel Knight. Replaces Twinkling Titanite.
In Archdragon Peak: In a chest after the sagging wooden bridge on the way to the Belfry, in the building with the Crystal Lizard. Replaces Titanite Scale.

The shops and ashes option includes these locations (some of which are included in other groups as well):

In Firelink Shrine: Sold by Shrine Handmaid at the start.
In Firelink Shrine: Sold by Greirat after recruiting him, or in his ashes.
In Firelink Shrine: Sold by Cornyx after recruiting him, or in his ashes.
In Firelink Shrine: Sold by Irina after recruiting her, or in her ashes.
In Firelink Shrine: Sold by Karla after recruiting her, or in her ashes.
In Firelink Shrine: Sold by Handmaid after giving Mortician's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Dreamchaser's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Prisoner Chief's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Captain's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Xanthous Ashes.
In Firelink Shrine: Sold by Handmaid after giving Dragon Chaser's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Grave Warden's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Paladin's Ashes.
In Firelink Shrine: Sold by Handmaid after giving Easterner's Ashes.
In Firelink Shrine: Sold by Handmaid after defeating Crystal Sage.
In Firelink Shrine: Sold by Handmaid after defeating Abyss Watchers.
In Firelink Shrine: Sold by Handmaid after defeating High Lord Wolnir.
In Firelink Shrine: Sold by Handmaid after defeating Alrich, Devourer of Gods.
In Firelink Shrine: Sold by Handmaid after defeating Dancer of the Boreal Valley.
In Firelink Shrine: Sold by Handmaid after defeating Champion Gundyr.
In Firelink Shrine: Sold by Handmaid after defeating Nameless King.
In Firelink Shrine: Sold by Handmaid after defeating Lothric, Younger Prince.
In Firelink Shrine: Sold by Handmaid after defeating Sister Friede.
In Firelink Shrine: Sold by Handmaid after defeating Soul of Cinder.
In Firelink Shrine: Sold by Handmaid after placing all Cinders of a Lord on their thrones.
In Untended Graves: Sold or dropped by Untended Graves Handmaid. Killing her is not recommended.
In Dreg Heap: Sold by Stone-humped Hag, or in her ashes.

The miniboss option includes these locations (some of which are included in other groups as well):

In Cemetery of Ash: Dropped by Ravenous Crystal Lizard. Replaces Titanite Scale.
In Firelink Shrine: Dropped by Sword Master. Replaces Uchigatana.
In High Wall: Dropped by mimic in the building guarded by the fire-breathing wyvern. Replaces Deep Battle Axe.
In High Wall Lift Cell: Dropped by the Darkwraith past the Lift Chamber Key. Replaces Red Eye Orb.
In Undead Settlement: Dropped by the Fire Demon you fight with Siegward. Replaces Fire Gem.
In Undead Settlement: Dropped by the Boreal Outright Knight before Road of Sacrifices. Replaces Irithyll Straight Sword.
In Road of Sacrifices: Dropped by the greatsword-wielding Exile Knight before the ladder down to Farron Keep. Replaces Exile Greatsword.
In Road of Sacrifices: Dropped by the club-wielding Exile Knight before the ladder down to Farron Keep. Replaces Great Club.
In Farron Keep: Dropped by Stray Demon on the bridge above Farron Keep. Replaces Soul of a Stray Demon.
In Farron Keep: Dropped by Ravenous Crystal Lizard near the shortcut from Farron Keep back to Road of Sacrifices. Replaces Titanite Scale.
In Cathedral of the Deep: Dropped by the brigand at the start of Cathedral of the Deep. Replaces Spider Shield.
In Cathedral of the Deep: Dropped by the Ravenous Crystal Lizard outside of the Cathedral. Replaces Titanite Scale.
In Cathedral of the Deep: Dropped by the Mimic before the room with the patrolling Cathedral Knight and Deep Accursed. Replaces Deep Braille Divine Tome.
In Catacombs of Carthus: Dropped by first Skeleton Ball after killing its sorcerer skeleton. Replaces Undead Bone Shard.
In Catacombs of Carthus: Dropped by the Fire Demon before Smouldering Lake. Replaces Soul of a Demon.
In Catacombs of Carthus: Dropped by the mimic before Smouldering Lake. Replaces Black Blade.
In Smouldering Lake: Dropped by the giant Carthus Sandworm. Replaces Lightning Stake.
In Smouldering Lake: Dropped by Knight Slayer Tsorig in Smouldering Lake. Replaces Fume Ultra Greatsword.
In Irithyll: Dropped by killing Sulyvahn's Beast on the bridge to Irithyll or in the lake below. Replaces Pontiff's Right Eye.
In Irithyll: Drop from a mimic in the higher levels of Pontiff's cathedral, accessible from the Deacons after the Pontiff fight. Replaces Golden Ritual Spear.
In Irithyll: Dropped after killing both of Sulyvahn's Beasts in the Water Reserves. Replaces Ring of Favor.
In Irithyll Dungeon: Dropped by the mimic in the room after the outside area of Irithyll Dungeon overlooking Profaned Capital. Replaces Estus Shard.
In Irithyll Dungeon: Dropped by the mimic in the side corridor from where the Giant Slave is standing, before the long ladder. Replaces Dragonslayer Lightning Arrow.
In Irithyll Dungeon: Dropped by the mimic found going past the Giant Slave to the sewer with the rats and the basilisks, up the first flight of stairs, on the left side. Replaces Dark Clutch Ring.
In Irithyll Dungeon: Dropped by the mimic in the main Jailer cell block. Replaces Titanite Scale.
In Profaned Capital: Dropped by the court sorcerer above the toxic pool. Replaces Logan's Scroll.
In Profaned Capital: Dropped by the left mimic surrounded by the Jailers to the right of the Profaned Flame. Replaces Greatshield of Glory.
In Profaned Capital: Dropped by the right mimic surrounded by the Jailers to the right of the Profaned Flame. Replaces Rusted Gold Coin.
In Profaned Capital: Dropped by the mimic on the second floor of the Monstrosity of Sin building. Replaces Court Sorcerer's Staff.
In Lothric Castle: Dropped by the Boreal Outrider Knight in the basement. Replaces Irithyll Rapier.
In Lothric Castle: Dropped by the first Pus of Man wyvern. Replaces Titanite Chunk.
In Lothric Castle: Dropped by the second Pus of Man wyvern. Replaces Titanite Chunk.
In Lothric Castle: Dropped by the crawling mimic at the top of the room with the firebomb-throwing hollows. Replaces Titanite Scale.
In Lothric Castle: Dropped by the mimic in the room next to the second Pus of Man wyvern. Replaces Sunlight Straight Sword.
In Grand Archives: Dropped by the Grand Archives Crystal Sage. Replaces Crystal Scroll.
In Grand Archives: Dropped by an Outrider Knight past the Crystal Sage's third floor location and an illusory wall. Replaces Outrider Knight Helm.
In Grand Archives: Dropped by Lion Knight Albert before the stairs leading up to Twin Princes. Replaces Golden Wing Crest Shield.
In Grand Archives: Dropped by Black Hand Kamui before the stairs leading up to Twin Princes. Replaces Onikiri and Ubadachi.
In Grand Archives: Dropped by Daughter of Crystal Kriemhild before the stairs leading up to Twin Princes. Replaces Sage's Crystal Staff.
In Archdragon Peak: Dropped by the second Ancient Wyvern patrolling the path up to the Belfry. Replaces Titanite Chunk.
In Archdragon Peak: Dropped from any of the Havel Knights. Replaces Dragon Tooth.
In pre-Contraption Ariandel: Dropped by Sir Vilhelm. Replaces Contraption Key.
In Dreg Heap: Dropped by Desert Pyromancer Zoey. Replaces Flame Fan.
In Ringed City: Dropped by mimic in Shared Grave. In one of the rooms after dropping down near Gael's flag and then dropping down again. Replaces Ring of the Evil Eye+3.
In Ringed City: Given by Shira after defeating Midir, or dropped by her in post-Filianore Ringed City. Replaces Titanite Slab.
In Filianore's Rest: Dropped by Shira, who invades you (ember not required) in the far-future version of her room. Replaces Crucifix of the Mad King.

The ring option includes these locations (some of which are included in other groups as well):

In Firelink Shrine Roof: From the Firelink Shrine roof, past the rafters and an illusory wall. Replaces Covetous Silver Serpent Ring.
In Firelink Bell Tower: Dropping down from the Bell Tower to where Irina eventually resides. Replaces Estus Ring.
In High Wall: Coming from the elevator shortcut, on a side path to the left (toward Winged Knight area), jumping onto a wooden support. Replaces Ring of Sacrifice.
In Undead Settlement: On a hanging corpse in the area with the Pit of Hollows cage manservant, after the thrall area, overlooking the entrance to the Giant's tower. Replaces Flame Stoneplate Ring.
In Undead Settlement: From the area bombarded by firebombs above the Cliff Underside bonfire. Replaces Fire Clutch Ring.
In Undead Settlement: Dropped by the large rat in the sewers with grave access. Replaces Bloodbite Ring.
In Undead Settlement: On the roof toward the end of the Fire Demon loop, past the Cathedral Evangelists. Replaces Flynn's Ring.
In Undead Settlement: At the end of the Fire Demon loop, in the tower where you have to drop down after the roof. Replaces Chloranthy Ring.
In Road of Sacrifices: Dropping down before the bridge leading up to Halfway Fortress from Road of Sacrifices, guarded by the maggot belly dog. Replaces Morne's Ring.
In Road of Sacrifices: In an alcove under the building before Crystal Sage, guarded by a Lycanthrope, accessible from the swamp or from dropping down. Replaces Sage Ring.
In Road of Sacrifices: Drop down from the platform behind the sorcerer in the building before Crystal Sage, entering from the stairs leading up from the crab area. Replaces Ring of Sacrifice.
In Road of Sacrifices: Dropped by Greater Crab in Crucifixion Woods close to the Farron Keep outer wall. Replaces Great Swamp Ring.
In Farron Keep: Dropped by the Greater Crab patrolling the birch tree where the Giant shoots arrows. Replaces Lingering Dragoncrest Ring.
In Cathedral of the Deep: In the pit with the Infested Corpse, accessible from the Ravenous Crystal Lizard area or from dropping down near the second Cleansing Chapel shortcut. Replaces Poisonbite Ring.
In Cathedral of the Deep: On the ledge where the Giant Slave slams his arms down. Replaces Lloyd's Sword Ring.
In Catacombs of Carthus: Down the ramp from the Fire Demon, straight down the hallway past the room with the Abandoned Tomb bonfire. Replaces Witch's Ring.
In Catacombs of Carthus: After the first Skeleton Ball, in the hallway alcove with the many dark-exploding pots. Replaces Carthus Milkring.
In Catacombs of Carthus: At the very end of the Bonewheel Skeleton area. Replaces Carthus Bloodring.
In Smouldering Lake: Behind a destructible wall in Smouldering Lake which the ballista has to destroy. Replaces Speckled Stoneplate Ring.
In Irithyll: In the area before and below Pontiff's cathedral, behind an illusory wall to the right. Replaces Magic Clutch Ring.
In Irithyll: Near the sewer centipede at the start of the lake leading to the Distant Manor bonfire. Replaces Ring of Sacrifice.
In Irithyll: In a chest up the stairs in the room with the Silver Knight staring at the painting. Replaces Leo Ring.
In Irithyll: Dropping down from in front of Pontiff Sulyvahn's church toward the Church of Yorshka. Replaces Ring of the Sun's First Born.
In Irithyll: After the Pontiff fight, in the dark hallways to the left of the area with the Giant Slaves. Replaces Dark Stoneplate Ring.
In Irithyll: In a chest in Darkmoon Tomb. Replaces Reversal Ring.
In Irithyll: In the Anor Londo cathedral after defeating Aldrich, up the elevators in Gwynevere's Chamber. Replaces Sun Princess Ring.
In Irithyll Dungeon: In the cell in the main Jailer cell block to the left of the Profaned Capital exit. Replaces Dusk Crown Ring.
In Irithyll Dungeon with Jailbreaker's Key: Dropping down from the Jailbreaker's Key shortcut at the end of the top corridor on the bonfire side in Irithyll Dungeon. Replaces Bellowing Dragoncrest Ring.
In Profaned Capital: In the inner cave of the Profaned Capital toxic pool. Replaces Cursebite Ring.
In Irithyll Dungeon Old Cell: In the Old Cell where Siegward is rescued. Replaces Covetous Gold Serpent Ring.
In Lothric Castle: From the chapel to the right of the Dragonslayer Armour fight, on the balcony to the left. Replaces Red Tearstone Ring.
In Lothric Castle: Climbing the ladder to the rooftop outside the Dragonslayer Armour fight, past the Large Hollow Soldier, down into the room with the tables. Replaces Knight's Ring.
In Consumed King's Gardens: Along the right wall of the garden, next to the first elevator building. Replaces Ring of Sacrifice.
In Consumed King's Gardens: From the middle level of the second elevator, toward the Oceiros boss fight. Replaces Dragonscale Ring.
In Untended Graves: In the coffin similar to your initial spawn location, guarded by Corvians. Replaces Ashen Estus Ring.
In Untended Graves: On a cliffside to the right of the main path leading up to dark Firelink Shrine, after Abyss Watchers is defeated. Replaces Hornet Ring.
In Grand Archives: On the corpse of a sitting Archives Scholar between two bookshelves, accessible by activating a lever before crossing the bridge that is the Crystal Sage's final location. Replaces Scholar Ring.
n Grand Archives: From the first shortcut elevator with the movable bookshelf, past the Scholars right before going outside onto the roof, in an alcove to the right with many Clawed Curse bookshelves. Replaces Fleshbite Ring.
In Grand Archives: At the top of the ladder in roof the area with the Winged Knights. Replaces Hunter's Ring.
In Archdragon Peak: To the left of gate leading to Ancient Wyvern, past the Rock Lizard. Replaces Lightning Clutch Ring.
In Archdragon Peak: To the right of the area where the Ancient Wyvern lands, dropping down onto the ledge. Replaces Ring of Steel Protection.
In Archdragon Peak: Received using Path of the Dragon at the Altar by the Mausoleum bonfire. Replaces Calamity Ring.
In Archdragon Peak: After the long hallway after the Mausoleum bonfire, before the rope bridge, up the long ladder. Replaces Thunder Stoneplate Ring.
In pre-Contraption Ariandel: Given by Sister Friede while she is sitting in the Ariandel Chapel, or on the stool after she moves. Replaces Chillbite Ring.
In Dreg Heap: After exiting the building with the Lothric Knights where the front crumbles, to the last room of the building to the right, up stairs past an illusory wall to the left, then dropping down after exiting the building from the last room. Replaces Covetous Silver Serpent Ring+3.
In Dreg Heap: After the dropdown where an angel first targets you, on an exposed edge to the left. Difficult to get without killing the angel. Replaces Ring of Steel Protection+3.
In Dreg Heap: Up the long branch close to the dropdown into the poison swamp area, in front of the cavern. Replaces Ring of Favor+3.
In Ringed City: Going up the very long ladder from the muck pit, then up some stairs, to the left, and across the bridge, in a building past the Ringed Knights. Also where Lapp can be found to tell him of the Purging Monument. Replaces Covetous Gold Serpent Ring+3.
In Ringed City: Dropping down from the building where Silver Knight Ledo invades. The building is up the very long ladder from the muck pit, down the path all the way to the right. Replaces Havel's Ring+3.
In Ringed City: From the mid level of the Ringed Inner Wall elevator that leads to the Midir fight, dropping back down toward the way to Filianore, onto a platform with a Gwyn statue. Try to land on the platform rather than the statue. Replaces Chloranthy Ring+3.

Crystal Lizard locations can be seen at http://darksouls3.wikidot.com/enemygroup:crystal-lizard.
