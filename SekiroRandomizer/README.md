A comprehensive enemy and item randomizer for Sekiro, including boss randomization and key item randomization.

Things to definitely know for item randomizer:
- The first castle invasion starts when you have Mortal Blade, Shelter Stone, and Lotus of the Palace; you have Isshin asked about the Mortal Blade location in the watchtower (talked to him twice); and you are in Senpou Temple/Sunken Valley/Ashina Depths/Hirata Temple.
- You can get into Ashina Castle after the first invasion even if you can't swim underwater, through the Ashina Reservoir gate
- To enter Hirata Estate without the Shinobi Prosthetic, do a precise running jump onto the tree branch you could otherwise grapple onto. If you get stuck in water, you may have to use Homeward Idol to get out. The prosthetic activates as soon as you acquire it; even if you don't see green circles, you can still grapple.

Things to definitely know for enemy randomizer:
- Game progression things that apply to enemies will apply to their replacements instead. e.g. killing Guardian Ape's replacement will spawn Headless Ape's replacement.
- Armored Warrior will now fall off the edge of anything the game considers a floor collision. This is easiest off of ledges/cliffs. In some cases you can kick him directly into a wall or a big rock, but some floors extend beyond walls, so you may have to try a few spots. It is always possible to do.

## Installation

To install and run:

1. Download Sekiro Randomizer and Mod Engine Beta. You will also need to download Combined SFX if you haven't unpacked the game using UXM.

2. Add the randomizer directory directly into the Sekiro.exe directory (*not* into an existing mods directory).

https://media.discordapp.net/attachments/319013592332763136/685713247692914708/rando.png

3. Install Sekiro Mod Engine. It must be a version which supports the randomizer, which as of March 2020 is not officially released yet (it will be when katalash has the time). This version allocates 200 MB additional memory for sound files and is required for avoiding crashes. It should suffice to copy dinput8.dll and modengine.ini into your Sekiro directory.

https://media.discordapp.net/attachments/319013592332763136/685713245063217189/modini.png

4. Change modengine.ini to have modOverrideDirectory="\randomizer", if it's not already.

5. All enemy sfx must be combined. There are two ways to do this.

5a. Download the sfx separately (76 MB download) and add the sfx directory into the randomizer directory.

https://media.discordapp.net/attachments/319013592332763136/685713250062565383/sfx.png

5b. Alternatively, unpack Sekiro using UXM (needs about 14 GB disk space) and the randomizer will combine the SFX automatically on startup. This will take a few seconds. (After this, the sfx directory should match the above screenshots.)

6. Run the randomizer. There is an experimental option to merge files from your 'mods' directory, if you have one, and those mod files will get added into the 'randomizer' directory. For some files this is a simple copy, but for others it has to merge them.

7. Make sure that the randomizer succeeds (shows "Done" in the status bar), and then *close Sekiro if you have it open*, and start up Sekiro for changes to be reflected, and create a new save file.

To uninstall, change back modOverrideDirectory="\mods", or delete the randomizer directory. The new version of mod engine will also work for previous mods, if you don't mind the extra memory allocation.

## Gameplay details

Misc general info:
- There is a hint/spoiler log that is generated and put in the 'runs' folder. Check it out if you are lost or want to check enemy placements.
- For convenience, tutorials pop-ups have been removed.
- It should be fine to re-run the randomizer with a different seed after starting NG+ playthroughs (after viewing cutscene, before picking up first item). If item randomizer is off, it should also be fine to re-run enemy randomizer with a different seed in the middle of a playthrough.

Misc info about enemy randomizer:
- Sword Saint Isshin doesn't require Mortal Blade to deathblow anymore.
- If you get really stuck on a randomized enemy, you can rerun the seed with enemy rando turned off (use the big checkbox), defeat the enemy, then turn it back on again. This won't change the location of key items, but if item rando uses enemy placement data, some non-key item drops may become inaccessible.

Misc info about item randomizer:
- Remember that additional enemies spawn in Guardian Ape's Burrow after defeating Guardian Ape, and Fujioka will also sell Valley Apparitions Memo when the final enemy spawns.
- Divine Dragon's Tears and Aromatic Branch have no effect in the game. You can complete the game and enter Fountainhead Palace, respectively, without them.
- Item logic assumes that you can get into Sunken Passage Serpent Shrine if you have either Puppeteer Ninjutsu or Mist Raven's Feathers.
- There are options like 'Item availability similar to base game' which I would strongly recommend. Otherwise, at high biases, it is possible for e.g. all Gourd Seeds and Memories to be in Fountainhead Palace.

If you encounter any issues, like softlocks from inaccessible items or unbeatable enemies, feel free to report a bug. It will help a lot if you include the log in the 'runs' folder, or at least the full first line of the log (starting with "Options and seed"). The seed by itself probably won't help.

There are also some features I could implement in the future. Let me know if any of these would be interesting, or if you have any other suggestions:
- Config for certain enemies to not be randomized or 'randomized' to specific spots (should happen soon)
- Adding minibosses in the world as regular enemies (this is pretty straightforward)
- Adding bosses in the world as minibosses or regular enemies (non-trivial, requires some custom scripting)
- Adding boss vs boss or multi-boss fights (requires a lot of custom scripting)
- Making all Esoteric Texts skills item drops in the world instead
- Randomizing enemies in Divine Dragon phase 1
- Scaling minibosses/bosses down or up if they are moved from late game to early game or vice versa
- Adding the ability to reroll enemy rando in the middle of a combined run
- Semi-related, backporting some of the new features to DS3 Static Item Randomizer which uses the same codebase

## Acknowledgements

The fantastic art in the randomizer UI (the title and hand-drawn characters) was made specially for the randomizer by Souv, who has also created many wonderful emotes for the FromSoft modding discord server (https://discord.gg/mT2JJjx). Thanks to JesterPatches, Tolvo, and Shoobe for their valuable alpha testing feedback, and JP for some of the lovely screenshots as well. Thanks to katalash, who made DSTools and DSMapStudio and made a version of Mod Engine which fixed the audio crashes. Thanks to TKGP and Pav and others for SoulsFormats and Paramdex, and Pav for the CE table which was useful for testing. Thanks to HotPocketRemix and Meowmaritus and AinTunez for developing tools for event script editing which this mod uses. Thanks to everyone else who helped debug issues and provide feedback. The initial version of this project took 3-4 months of development time and makes some really advanced dynamic edits to game scripts and systems, and I'd love to see more gameplay-oriented Sekiro mods like it.

## Important locations

Some items have the option to be placed in "important locations". This can be used for shorter runs or live races. If enabled, all of the same item logic still applies, so it shouldn't be possible to get softlocked. Generally, these include the vanilla locations of bosses, minibosses, prayer beads, gourd seeds, prosthetics, esoteric texts, key items, and serpent viscera. Currently, they are:

In Dilapidated Temple: Given upon arriving in Dilapidated Temple. Replaces Shinobi Prosthetic.
In Dilapidated Temple: Unique initial item sold by Fujioka the Info Broker. Replaces Gourd Seed.
In Dilapidated Temple: Given by the Sculptor after acquiring 1 skill point. Replaces Shinobi Esoteric Text.
In Dilapidated Temple: Given by the Sculptor after collecting 3 prosthetic tools. Replaces Prosthetic Esoteric Text.
In Ashina Outskirts Gate Path: On the second floor of the building next to the Gate Path Sculptor's Idol, or in the Offering Box after the second invasion. Replaces Shuriken Wheel.
In Ashina Outskirts Gate Path: Dropped by General Naomori Kawarada, or in Offering Box after the Central Forces invasion. Replaces Gourd Seed.
In Ashina Outskirts Stairway: Unique item sold by Crow's Bed Memorial Mob and Battlefield Memorial Mob. Replaces Robert's Firecrackers.
In Ashina Outskirts Stairway: Given by Inosuke's Mother. Replaces Young Lord's Bell Charm.
In Ashina Outskirts Stairway: Unique location from Chained Ogre. Replaces Shinobi Medicine Rank 1, Prayer Bead.
In Ashina Outskirts Courtyard: In the left room of the lookout building after Chained Ogre. Replaces Gourd Seed.
In Ashina Outskirts Courtyard: Dropped by General Naomori Kawarada, or in Offering Box after the Central Forces invasion. Replaces Prayer Bead.
In Ashina Outskirts Courtyard: Dropped by General Tenzen Yamauchi, or in the Offering Box after the Central Forces invasion. Replaces Prayer Bead.
In Ashina Castle Fortress: Unique location from Gyoubu Oniwa. Replaces Memory: Gyoubu Oniwa, Mechanical Barrel.
In Ashina Castle Gate: Unique item sold by Battlefield Memorial Mob. Replaces Gourd Seed.
In Ashina Castle Gate: Given by Tengu after killing rats, or in his spot in Ashina Castle if the quest is refused and after Divine Dragon. Replaces Ashina Esoteric Text.
In Ashina Castle Gate: Unique location from Blazing Bull. Replaces Shinobi Medicine Rank 2, Prayer Bead.
In Ashina Castle Gate: In a chest on the top floor of the building up the stairs after Gyoubu. Replaces Prayer Bead.
In Ashina Outskirts after Central Forces: Unique location from Demon of Hatred. Replaces Lapis Lazuli, Memory: Hatred Demon.
In Ashina Outskirts after Central Forces: Dropped by Shigekichi of the Red Guard. Replaces Prayer Bead.
In Ashina Reservoir: Dropped by a soldier on the bridge leading to the Abandoned Dungeon entrance, or in the courtyard immediately to the left after the Ashina Castle Sculptor's Idol. Replaces Gatehouse Key.
In Ashina Reservoir: Dropped by Seven Ashina Spears - Shikibu Toshikatsu Yamauchi, or in the Offering Box after the Central Forces invasion. Replaces Prayer Bead.
In Ashina Reservoir: Dropped by Lone Shadow Longswordsman. Replaces Prayer Bead.
In Ashina Reservoir: In the chest in Ashina Reservoir Gate House. Replaces Gyoubu's Broken Horn.
In Ashina Reservoir after Central Forces: Dropped by Seven Ashina Spears - Shume Masaji Oniwa. Replaces Prayer Bead.
In Ashina Reservoir Ending: Unique location from Sword Saint Isshin. Replaces Memory: Saint Isshin, Dragon Flash.
In Ashina Castle: Dropped by Genichiro. Replaces Memory: Genichiro, Bloodsmoke Ninjutsu.
In Ashina Castle: In the library area in Kuro's Room which opens up after you ask Isshin where the Mortal Blade can be found. Replaces Gun Fort Shrine Key.
In Ashina Castle: In a chest in the Upper Tower basement. Replaces Sabimaru.
In Ashina Castle: Sold by Blackhat Badger, or left behind after he moves to Senpou Temple, or in Offering Box if killed. Replaces Iron Fortress.
In Ashina Castle: In a chest before the Upper Tower Antechamber. Replaces Gourd Seed.
In Ashina Castle: Dropped by General Kuranosuke Matsumoto, or in the Offering Box after the Interior Ministry invasion. Replaces Prayer Bead.
In Ashina Castle: Dropped by Ashina Elite Jinsuke Saze, or in the Offering Box after the Interior Ministry invasion. Replaces Prayer Bead.
In Ashina Castle: In a chest past a scroll-covered shinobi door in the Upper Tower room where two fencers are studying a map. Replaces Prayer Bead.
In Ashina Castle after Interior Ministry: Unique location from Owl. Replaces Aromatic Branch, Memory: Great Shinobi.
In Ashina Castle after Interior Ministry: Dropped by Isshin Ashina. Replaces One Mind.
In Ashina Castle after Interior Ministry: Unique location from Chained Ogre. Replaces Shinobi Medicine Rank 3, Prayer Bead.
In Ashina Castle after Interior Ministry: Dropped by Lone Shadow Vilehand. Replaces Prayer Bead.
In Ashina Castle after Interior Ministry: Dropped by Lone Shadow Masanaga the Spear-bearer, or in the Offering Box after the Central Forces invasion. Replaces Prayer Bead.
In Ashina Castle after Central Forces: Given by Emma in Kuro's Room after Divine Dragon. Replaces Secret Passage Key.
In Ashina Castle after Central Forces: Dropped by Ashina Elite Ujinari Mizuo. Replaces Prayer Bead.
In Hirata Estate: Near the bandits' bonfire. Replaces Flame Barrel.
In Hirata Estate: In the shrine where you can eavesdrop bandits arguing about whether to loot the shrine. Replaces Shinobi Axe of the Monkey.
In Hirata Estate: Dropped by Shinobi Hunter Enshin of Misen. Replaces Prayer Bead.
In Hirata Estate: Dropped by Juzou the Drunkard. Replaces Prayer Bead.
In Hirata Audience Chamber: In a chest behind the scroll-covered shinobi door before the Audience Chamber. Replaces Prayer Bead.
In Hirata Estate Hidden Temple: Unique location from Lady Butterfly. Replaces Memory: Lady Butterfly, Sakura Droplet.
In Hirata Estate Revisited: Dropped by Lone Shadow Masanaga the Spear-bearer. Replaces Prayer Bead.
In Hirata Estate Revisited: Dropped by Juzou the Drunkard. Replaces Prayer Bead.
In Hirata Estate Hidden Temple Revisited: Unique location from Owl. Replaces Aromatic Flower, Memory: Foster Father.
In Abandoned Dungeon: Unique location from Shichimen Warrior. Replaces Ceremonial Tanto.
In Senpou Temple: Unique location from Armored Warrior. Replaces Breath of Nature: Shadow, Prayer Bead.
In Senpou Temple: In the main room of the cricket building, in front of the Infested Seeker. Replaces Gourd Seed.
In Senpou Temple: Dropped by Long Arm Centipede Sen-Un. Replaces Prayer Bead.
In Senpou Temple Grounds: Inside of the pagoda after the cave acessible near the Main Hall side entrance. Replaces Senpou Esoteric Text.
In Senpou Temple Grounds: Underwater in the Temple Grounds pond. Replaces Prayer Bead.
In Senpou Temple Inner Sanctum: Unique drop from Folding Screen Monkeys. Replaces Memory: Screen Monkeys, Puppeteer Ninjutsu.
In Senpou Temple Inner Sanctum: Given by the Divine Child. Replaces Mortal Blade.
In Upper Sunken Valley: In the room with Long-arm Centipede Giraffe in front of the statue. Replaces Large Fan.
In Upper Sunken Valley: After two tree branch grapples in Under-Shrine Valley, rather than going right toward the Sunken Valley Sculptor's Idol, climb up the wall to the left and follow that path. Replaces Gourd Seed.
In Upper Sunken Valley: Dropped by Snake Eyes Shirafuji. Replaces Prayer Bead.
In Upper Sunken Valley: Dropped by Long-arm Centipede Giraffe. Replaces Prayer Bead.
In Upper Sunken Valley: In the area before the Headless cave, found going backwards from the Under-Shrine Valley Sculptor's Idol. Replaces Prayer Bead.
In Sunken Valley Passage: Unique location from Guardian Ape. Replaces Memory: Guardian Ape, Slender Finger.
In Sunken Valley Passage: After Guardian Ape. Replaces Lotus of the Palace.
In Sunken Valley Passage Shrine: Held by a statue after the shrine building. Replaces Dried Serpent Viscera.
In Sunken Valley Passage after Snake Deathblow: Unique location from Great Serpent. Replaces Fresh Serpent Viscera.
In Poison Pool: Dropped by Snake Eyes Shirahagi. Replaces Prayer Bead.
In Guardian Ape's Burrow: Unique location from Guardian Ape. Replaces Bestowal Ninjutsu.
In Guardian Ape's Burrow: Rather than dropping down into the burrow, grapple to the left and wall jump up to the cliff edge overlooking Poison Pool, and then grapple onto the nearby statue's head. Replaces Prayer Bead.
In Guardian Ape's Burrow: Unique drop from Headless Ape. Replaces Memory: Headless Ape, Prayer Bead, Prayer Bead.
In Guardian Ape's Burrow: Unique location from Shichimen Warrior. Replaces Malcontent's Ring.
In Hidden Forest: Dropped by the Mist Noble in the temple. Replaces Lump of Grave Wax.
In Hidden Forest: Dropped by Tokujiro the Glutton. Replaces Prayer Bead.
In Mibu Village: Unique location from Corrupted Monk. Replaces Mibu Breathing Technique, Memory: Corrupted Monk.
In Mibu Village: On the cave altar. Replaces Shelter Stone.
In Mibu Village: Unique location from O'Rin. Replaces Breath of Life: Shadow, Prayer Bead.
In Mibu Village: In front of the tree in the area with many underground villagers. Replaces Gourd Seed.
In Mibu Village: On a small shrine in the upper floor of the Head Priest's house. Replaces Prayer Bead.
In Mibu Village: In a chest at the bottom of the village pond. Replaces Prayer Bead.
In Fountainhead Vermillion Bridge: Unique drop from True Monk. Replaces Memory: True Monk, Dragon's Tally Board.
In Fountainhead Palace: Unique location from Sakura Bull of the Palace. Replaces Prayer Bead.
In Fountainhead Palace: Unique location from Divine Dragon. Replaces Divine Dragon's Tears, Memory: Divine Dragon.
In Fountainhead Palace: Unique location from Shichimen Warrior. Replaces Lapis Lazuli.
In Fountainhead Palace: In a chest in the Palace Grounds building. Replaces Gourd Seed.
In Fountainhead Palace: Unique drop from Okami Leader Shizu. Replaces Prayer Bead.
In Fountainhead Palace: In the chest guarded by the two Underwater Headless. Replaces Prayer Bead.

If Headless locations are allowed to be important, these are also added:
In Ashina Outskirts Courtyard: Unique location from Headless. Replaces Ako's Spiritfall.
In Ashina Castle: Unique location from Underwater Headless. Replaces Ungo's Spiritfall.
In Hidden Forest: Unique location from Headless. Replaces Gachiin's Spiritfall.
In Upper Sunken Valley: Dropped by the Headless found going backwards from the Under-Shrine Valley Sculptor's Idol and under the pond. Replaces Gokan's Spiritfall.
In Fountainhead Palace: Unique location from Underwater Headless. Replaces Yashariku's Spiritfall.
