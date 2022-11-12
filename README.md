See project readmes in DS3Randomizer, SekiroRandomizer, and EldenRingRandomizer

Find the mod releases on Nexus:

- DS3: https://www.nexusmods.com/darksouls3/mods/361
- Sekiro: https://www.nexusmods.com/sekiro/mods/543
- Elden Ring: https://www.nexusmods.com/eldenring/mods/428

## Licensing and contributions

These randomizers are source-available but due to past issues, they are currently **not freely licensed**.

At least at the moment, contributions are not accepted. Do not distribute the randomizer, forks of the randomizer programs, or forks of config files. This section will be updated if that changes.

## Item randomizer algorithm

Here is some documentation for how randomization works.

There are two points of interest for the randomizer. The first is in Program.cs, which calls into Randomizer.cs, which executes each phase of the randomizer. The second is in dist(s)/Base/annotations.txt, which
has nearly all configuration for specific items and locations.

The core algorithm for the randomizer is:

1. Calculate a list of all obtainable items and a list of all accessible item locations.
2. Partition the list items into different silos. The main ones are:
    - The 'finite' silo contains guaranteed one-time items like world pickups, finite shop items, and some enemy/boss drops. This includes key items and most quest items.
    - The 'infinite' silo contains infinite shop items and farmable enemy drops.
    - The 'mixed' silo contains finite shop items which can become infinite shop items.
    - The 'self' silo contains items which are always randomized to themselves, for softlock/balance reasons.
3. Assign all key items, items which affect the availabity of other unmissable items, to logical areas.
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
