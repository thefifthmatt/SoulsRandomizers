using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class Permutation
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private bool explain;
        public readonly Dictionary<RandomSilo, SiloPermutation> Silos = new Dictionary<RandomSilo, SiloPermutation>();
        public readonly Dictionary<ItemKey, double> ItemLateness = new Dictionary<ItemKey, double>();
        public readonly HashSet<ItemKey> KeyItems = new HashSet<ItemKey>();
        public readonly Dictionary<ItemKey, ItemKey> SkillAssignment = new Dictionary<ItemKey, ItemKey>();
        public readonly Dictionary<SlotKey, string> LogOrder = new Dictionary<SlotKey, string>();
        // public readonly SortedDictionary<HintCategory, Dictionary<SlotKey, SlotKey>> Hints = new SortedDictionary<HintCategory, Dictionary<SlotKey, SlotKey>>();\
        public readonly Dictionary<string, Dictionary<SlotKey, SlotKey>> Hints = new Dictionary<string, Dictionary<SlotKey, SlotKey>>();
        public Dictionary<string, HashSet<string>> IncludedAreas => assign.IncludedAreas;
        public HashSet<ItemKey> NotRequiredKeyItems => assign.NotRequiredKeyItems;
        // Only in logic runs
        private KeyItemsPermutation.Assignment assign;
        private readonly Dictionary<ItemKey, SlotKey> specialAssign = new Dictionary<ItemKey, SlotKey>();

        public Permutation(GameData game, LocationData data, AnnotationData ann, bool explain=false)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.explain = explain;
            foreach (RandomSilo siloType in Enum.GetValues(typeof(RandomSilo)))
            {
                Silos[siloType] = new SiloPermutation { Type = siloType };
            }
            HashSet<ItemKey> remove = new HashSet<ItemKey>(ann.ItemGroups["remove"]);
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                List<ItemKey> norandoms = new List<ItemKey>();
                bool crow = false;
                bool removeSlot = false;
                if (ann.Slots.ContainsKey(locScope))
                {
                    SlotAnnotation slotAnn = ann.Slots[locScope];
                    if (slotAnn.TagList.Contains("norandom")) norandoms.Add(null);
                    if (slotAnn.TagItems != null && slotAnn.TagItems.TryGetValue("norandom", out List<ItemKey> items)) norandoms.AddRange(items);
                    // Crow targets get special handling
                    crow = slotAnn.TagList.Contains("crow");
                    if (slotAnn.TagList.Contains("remove"))
                    {
                        removeSlot = true;
                    }
                    else if (game.Sekiro)
                    {
                        // Just remove NG+ in Sekiro.
                        removeSlot = slotAnn.TagList.Contains("ng+");
                    }
                }
                // Add items
                SortedSet<RandomSilo> sourceSilos = new SortedSet<RandomSilo>();
                List<SlotKey> norandomSlots = new List<SlotKey>();
                foreach (SlotKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.Location(itemLocKey);
                    ItemScope scope = location.Scope;
                    RandomSilo siloType = canPermuteTo[scope.Type];
                    if (removeSlot)
                    {
                        AddMulti(Silos[RandomSilo.REMOVE].Mapping, itemLocKey, itemLocKey);
                        Silos[RandomSilo.FINITE].ExcludeTargets.Add(itemLocKey);
                    }
                    else if (remove.Contains(itemLocKey.Item))
                    {
                        // If only the item is removed, still allow the target slot to be used
                        AddMulti(Silos[RandomSilo.REMOVE].Mapping, itemLocKey, itemLocKey);
                        sourceSilos.Add(siloType);
                    }
                    else if (norandoms.Contains(null) || norandoms.Contains(itemLocKey.Item) || ann.NorandomItems.Contains(itemLocKey.Item))
                    {
                        // If nonrandom, do the mapping here
                        AddMulti(Silos[RandomSilo.SELF].Mapping, itemLocKey, itemLocKey);
                        // Also prevent it from being added to other silos which use the same locScope
                        norandomSlots.Add(itemLocKey);
                    }
                    else if (crow)
                    {
                        // A source exists, but we're not adding the item, as the items are already in the "add" group.
                        sourceSilos.Add(RandomSilo.CROW);
                    }
                    else
                    {
                        RandomSilo itemSilo = siloType;
                        if (gearTypes.Contains(itemLocKey.Item.Type)
                            && gearSiloVariants.TryGetValue(itemSilo, out RandomSilo gearSilo)
                            // Exclude arrows a bit hackily
                            && itemLocKey.Item.ID >= 1000000)
                        {
                            itemSilo = gearSilo;
                        }
                        Silos[itemSilo].Sources.Add(itemLocKey);
                        sourceSilos.Add(itemSilo);
                    }
                }
                // Add destination, assuming any of it is randomized
                // Also ignore special locations, which don't come from the game
                if (sourceSilos.Count > 0 && locScope.Type != ScopeType.SPECIAL)
                {
                    foreach (RandomSilo sourceSilo in sourceSilos)
                    {
                        Silos[sourceSilo].Targets.Add(locScope);
                        foreach (SlotKey slotKey in norandomSlots)
                        {
                            // Partial exclusion of norandom items
                            Silos[sourceSilo].ExcludeTargets.Add(slotKey);
                        }
                    }
                }
            }
        }
        public enum RandomSilo
        {
            // Event flag
            FINITE,
            // Infinite model drop items
            INFINITE,
            // Shop items which have a finite and infinite variant
            MIXED,
            // Non-randomized
            SELF,
            // Removed
            REMOVE,
            // Given random resource drops
            CROW,
            // Infinite shop items
            INFINITE_SHOP,
            // Infinite gear items, which are shared across shops and models
            INFINITE_GEAR,
        }
        // List of what can permute to what.
        public static readonly Dictionary<ScopeType, RandomSilo> canPermuteTo = new Dictionary<ScopeType, RandomSilo>
        {
            { ScopeType.EVENT, RandomSilo.FINITE },
            { ScopeType.MATERIAL, RandomSilo.FINITE },
            { ScopeType.ENTITY, RandomSilo.FINITE },
            { ScopeType.SPECIAL, RandomSilo.FINITE },
            { ScopeType.SHOP_INFINITE, RandomSilo.INFINITE_SHOP },
            { ScopeType.MODEL, RandomSilo.INFINITE },
            // In theory go from this to finite + infinite pair, and vice versa... but that is some super complicated multiplexing
            { ScopeType.SHOP_INFINITE_EVENT, RandomSilo.MIXED },
        };
        public static readonly Dictionary<RandomSilo, RandomSilo> gearSiloVariants = new Dictionary<RandomSilo, RandomSilo>
        {
            [RandomSilo.INFINITE] = RandomSilo.INFINITE_GEAR,
            [RandomSilo.INFINITE_SHOP] = RandomSilo.INFINITE_GEAR,
        };
        public static readonly HashSet<ItemType> gearTypes = new HashSet<ItemType> { ItemType.WEAPON, ItemType.ARMOR };
        private static readonly HashSet<RandomSilo> specialSiloTypes = new HashSet<RandomSilo> { RandomSilo.SELF, RandomSilo.REMOVE, RandomSilo.CROW };
        public class SiloPermutation
        {
            public RandomSilo Type { get; set; }
            public List<SlotKey> Sources = new List<SlotKey>();
            public List<LocationScope> Targets = new List<LocationScope>();
            // Locations for which some targets should not be considered part of this silo. TODO: Should just make targets a list of SlotKeys also?
            public HashSet<SlotKey> ExcludeTargets = new HashSet<SlotKey>();
            // Mapping from targets to all sources going there
            public Dictionary<SlotKey, List<SlotKey>> Mapping = new Dictionary<SlotKey, List<SlotKey>>();
        }

        private static (double, double) GetSubRange((double, double) range, int index, int total)
        {
            if (index < 0 || index > total) throw new Exception("Invalid range split");
            (double start, double end) = range;
            double subdivs = Math.Log(end / start, 2);
            double newStart = start * Math.Pow(2, subdivs * index / total);
            double newEnd = start * Math.Pow(2, subdivs * (index + 1) / total);
            return (newStart, newEnd);
        }
        public void Logic(Random random, RandomizerOptions options, Preset preset)
        {
            if (preset != null)
            {
                preset.ProcessItemPreset(ann);
            }

            // Calculate key items, including area lateness ranking
            KeyItemsPermutation keyItems = new KeyItemsPermutation(options, data, ann, null, explain);
            assign = keyItems.AssignItems(random, options, preset);
            foreach (KeyValuePair<ItemKey, HashSet<string>> entry in assign.Assign)
            {
                ItemKey key = entry.Key;
                if (!data.Data.ContainsKey(key))
                {
                    Console.WriteLine($"Assigned item {key} does not exist in data");
                    continue;
                }
                ItemLocations locs = data.Data[key];
                ItemLateness[key] = assign.LocationLateness[entry.Value.First()];
                if (locs.Locations.Keys.Any(loc => canPermuteTo[loc.Type] != RandomSilo.FINITE)) throw new Exception($"Assigned item {key} cannot be key item");
            }
            KeyItems.UnionWith(assign.Priority);

            // Disable unused locations from DLCs, according to areas unused in key item processing, as well as make ordering
            HashSet<SlotKey> unusedSlots = new HashSet<SlotKey>();
            HashSet<LocationScope> unusedLocations = new HashSet<LocationScope>();
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope loc = entry.Key;
                if (loc.Type == ScopeType.MODEL) continue;  // when will this come to bite...
                SlotAnnotation slot = ann.Slot(loc);
                // dlc1 tag is special hack to disable Captain's Ashes locations
                // also, hack to avoid Sekiro locations... please remove this...
                if (assign.IncludedAreas[slot.GetArea()].Count == 0 || (slot.TagList.Contains("dlc1") && !options["dlc1"]))
                {
                    unusedLocations.Add(entry.Key);
                    unusedSlots.UnionWith(entry.Value);
                }
                else
                {
                    string area = assign.EffectiveLocation.ContainsKey(loc) ? assign.EffectiveLocation[loc] : slot.GetArea();
                    int areaLate = (int)(assign.LocationLateness[area] * 9999);
                    string orderKey = $"{areaLate.ToString("0000")},{entry.Key}";
                    foreach (SlotKey order in entry.Value)
                    {
                        LogOrder[order] = orderKey;
                    }
                }
            }
            foreach (SiloPermutation silo in Silos.Values)
            {
                silo.Sources = silo.Sources.Where(slot => !unusedSlots.Contains(slot)).ToList();
                silo.Targets = silo.Targets.Where(loc=> !unusedLocations.Contains(loc)).ToList();
                silo.Mapping = silo.Mapping.Where(e => !unusedSlots.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);
            }

            // Categorize locations
            List<string> locationTags = new List<string> { "early", "mid", "late" };
            Dictionary<string, int> difficultyTags = new Dictionary<string, int>
            {
                { "enemy", 1 },
                { "shop", 1 },
                { "outoftheway", 2 },
                { "hidden", 3 },
                { "reqevent", 3 },
                { "ambush", 4 },
                { "miniboss", 5 },
                { "minibossrespawn", 5 },
                { "boss", 6 },
                { "deadend", 6 },
                { "premium", 6 },
            };
            int maxDifficulty = 6; // difficultyTags.Values.Max();

            // Initialize all weights based on difficulty settings
            int unfairRanking = (int)Math.Round((maxDifficulty - 1) * options.GetNum("unfairweight"));
            int veryUnfairRanking = (int)Math.Round(maxDifficulty * options.GetNum("veryunfairweight"));
            int keyItemDifficulty = (int)Math.Round(20 * options.GetNum("keyitemdifficulty"));
            int desirableDifficulty = (int)Math.Round(15 * options.GetNum("allitemdifficulty"));

            difficultyTags["unfair"] = unfairRanking;
            difficultyTags["veryunfair"] = veryUnfairRanking;
            if (options.GetNum("keyitemdifficulty") > 0.9)
            {
                difficultyTags["miniboss"]--;
                difficultyTags["minibossrespawn"]--;
                difficultyTags["boss"]--;
                difficultyTags["deadend"]--;
            }
            Dictionary<SlotKey, float> keyWeights = new Dictionary<SlotKey, float>();
            Dictionary<SlotKey, float> weights = new Dictionary<SlotKey, float>();

            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                SlotAnnotation slot = entry.Value;
                HashSet<string> tags = new HashSet<string>(slot.GetTags());
                // If have to kill NPC to acquire, or is an early required boss, deprioritize the location
                if (tags.Contains("death") || tags.Contains("boring"))
                {
                    continue;
                }
                // Demote events which unlock other items anyway
                if (slot.Event != null && assign.RequiredEvents.Contains(slot.Event) && (tags.Contains("miniboss") || tags.Contains("boss")))
                {
                    tags.Remove("miniboss");
                    tags.Remove("boss");
                    tags.Add("reqevent");
                }
                double maxWeight = Math.Pow(2, keyItemDifficulty);
                string gameLocation = assign.EffectiveLocation.ContainsKey(entry.Key) ? assign.EffectiveLocation[entry.Key] : slot.GetArea();
                if (assign.IncludedAreas[gameLocation].Count == 0) continue;
                // Weights for key items (lateness within location)
                (double, double) keyWeight = (1, maxWeight);
                (int location, int maxItemLocation) = slot.GetAreaIndex();
                if (ann.AreaAliases[slot.Area] != slot.Area)
                {
                    (location, maxItemLocation) = ann.Areas[slot.BaseArea].GetSubAreaIndex(slot.Area, location);
                }
                // Oof hardcoding... but these areas are so small, it's more of a challenge to put the item anywhere in the area, for key items
                if (gameLocation.StartsWith("firelink") || gameLocation == "highwall" || tags.Contains("deadend")) location = maxItemLocation - 1;
                keyWeight = GetSubRange(keyWeight, location, maxItemLocation);
                // Weights for all items (lateness within game)
                maxWeight = Math.Pow(2, desirableDifficulty);
                (double, double) weight = (1, maxWeight);
                weight = GetSubRange(weight, (int) (assign.LocationLateness[gameLocation] * 20), 20);
                // Difficulty of slot
                List<int> diffTags = tags.Where(t => difficultyTags.ContainsKey(t)).Select(t => difficultyTags[t]).ToList();
                diffTags.Sort((a, b) => b.CompareTo(a));
                foreach (int diffTag in diffTags)
                {
                    if (diffTag != 0)
                    {
                        keyWeight = GetSubRange(keyWeight, diffTag, maxDifficulty + 1);
                        weight = GetSubRange(weight, diffTag, maxDifficulty + 1);
                    }
                }
                foreach (SlotKey target in data.Location(entry.Key))
                {
                    keyWeights[target] = (float)keyWeight.Item1;
                    weights[target] = (float)weight.Item1;
                }
            }
            // Classify items
            Dictionary<ItemKey, float[]> itemWeights = new Dictionary<ItemKey, float[]>();
            // Add some buffer between priority items and others in DS3
            // In Sekiro there are much fewer items, so high priority items can afford to spread out more evenly.
            int itemGap = game.Sekiro ? 0 : 2;
            double maxItemWeight = Math.Pow(game.Sekiro ? 1.2 : 2, ann.ItemPriority.Count + itemGap + 1);
            int priority = ann.ItemPriority.Count + itemGap;
            foreach (ItemPriorityAnnotation group in ann.ItemPriority)
            {
                (double, double) itemWeight = GetSubRange((1, maxItemWeight), priority, ann.ItemPriority.Count + itemGap + 1);
                float[] countWeights = new float[Math.Max(1, group.PriorityByCount)];
                countWeights[0] = (float)itemWeight.Item1;
                for (int i = 1; i < countWeights.Length; i++)
                {
                    countWeights[i] = (float)GetSubRange(itemWeight, i, group.PriorityByCount).Item1;
                }
                foreach (ItemKey key in group.Keys)
                {
                    if (group.Includes != "keyitems" || assign.Priority.Contains(key))
                    {
                        itemWeights[key] = countWeights;
                    }
                }
                priority--;
            }

            // Do randomizations per silo
            foreach (KeyValuePair<RandomSilo, SiloPermutation> siloEntry in Silos)
            {
                RandomSilo siloType = siloEntry.Key;
                SiloPermutation silo = siloEntry.Value;
#if DEBUG
                Console.WriteLine($"{siloType}: Mapping {silo.Sources.Count()} sources -> {silo.Targets.Count()} targets");
#endif
                // Filled in before or after
                if (specialSiloTypes.Contains(siloType))
                {
                    continue;
                }

                // Add placement restrictions. There are a lot of these
                Dictionary<ItemKey, PendingItem> restrictions = new Dictionary<ItemKey, PendingItem>();
                if (siloType == RandomSilo.FINITE)
                {
                    foreach (KeyValuePair<ItemKey, HashSet<string>> itemLoc in assign.Assign)
                    {
                        ItemKey key = itemLoc.Key;
                        PendingItemSlot slot = new PendingItemSlot
                        {
                            AllowedLocations = itemLoc.Value,
                            Amount = -1,
                            Assigned = true,
                        };
                        restrictions[key] = new PendingItem
                        {
                            Slots = new List<PendingItemSlot> { slot },
                        };
                    }
                }
                List<PendingItemSlot> pendingSlotsFromPlacement(List<PlacementSlotAnnotation> slots, ItemKey key, string excludeTag)
                {
                    List<PendingItemSlot> ret = new List<PendingItemSlot>();
                    if (slots == null) return ret;
                    foreach (PlacementSlotAnnotation slot in slots)
                    {
                        // TODO: Make sure this returns areas and not items from within KeyItemsPermutation
                        HashSet<string> locs = new HashSet<string>(slot.AllowedAreas(assign.IncludedAreas).Where(a => ann.Areas.ContainsKey(a)));
                        ret.Add(new PendingItemSlot
                        {
                            AllowedLocations = locs,
                            Amount = slot.Amount,
                            AdditionalExcludeTag = excludeTag,
                        });
                    }
                    return ret;
                };
                foreach (PlacementRestrictionAnnotation restrict in ann.ItemRestrict.Values)
                {
                    ItemKey key = restrict.Key;
                    // If restrictions already present from key item/quest assignment, location restrictions are not necessary - this is already taken into account
                    if (restrictions.ContainsKey(restrict.Key))
                    {
                        continue;
                    }
                    PendingItem pending = null;
                    if (siloType == RandomSilo.FINITE && restrict.Unique != null)
                    {
                        pending = new PendingItem
                        {
                            Slots = pendingSlotsFromPlacement(restrict.Unique, key, null),
                        };
                        bool debug = game.Name(key) == "No item";
                        pending.Explain = debug;
                        if (debug) Console.WriteLine($"- Partitions for {game.Name(key)}");
                        pending.AddPartitions();
                        if (debug && pending.Partitions != null) foreach (PendingItemSlotPartition partition in pending.Partitions) Console.WriteLine($"- Partition: {partition}");
                    }
                    if ((siloType == RandomSilo.INFINITE || siloType == RandomSilo.INFINITE_SHOP) && (restrict.Shop != null || restrict.Drop != null))
                    {
                        pending = new PendingItem
                        {
                            Slots = pendingSlotsFromPlacement(restrict.Shop, key, "noshop").Concat(pendingSlotsFromPlacement(restrict.Drop, key, "shop")).ToList(),
                        };
                    }
                    if (pending != null)
                    {
                        restrictions[key] = pending;
                        if (restrict.OtherKeys != null)
                        {
                            foreach (ItemKey otherKey in restrict.OtherKeys)
                            {
                                if (explain) Console.WriteLine($"Also setting {game.Name(key)} to {game.Name(otherKey)}");
                                restrictions[otherKey] = pending;
                            }
                        }
                    }
                }
                void addRestrictionIfNoneExists(ItemKey key)
                {
                    if (!restrictions.ContainsKey(key))
                    {
                        restrictions[key] = new PendingItem
                        {
                            Slots = new List<PendingItemSlot> { new PendingItemSlot() { Amount = -1 } },
                        };
                    }
                }
                foreach (KeyValuePair<ItemKey, HashSet<string>> restrict in ann.ExcludeTags)
                {
                    addRestrictionIfNoneExists(restrict.Key);
                    restrictions[restrict.Key].ExcludeTags = restrict.Value;
                    // Console.WriteLine($"Restrictions for {game.Name(restrict.Key)}: {string.Join(",", restrict.Value)}");
                }
                foreach (ItemKey item in ann.RaceModeItems)
                {
                    addRestrictionIfNoneExists(item);
                    restrictions[item].RequireTags = ann.RaceModeTags;
                }
                // Add all per-location exclusions
                foreach (KeyValuePair<ItemKey, List<LocationScope>> restrict in assign.RestrictedItems)
                {
                    addRestrictionIfNoneExists(restrict.Key);
                    restrictions[restrict.Key].RestrictedLocs = restrict.Value;
                }
                // Preprocessing of restrictions - get total count for items, so we know how to distribute them
                foreach (SlotKey source in silo.Sources)
                {
                    if (restrictions.ContainsKey(source.Item))
                    {
                        restrictions[source.Item].FreeAmount++;
                    }
                }

                // Shuffle!
                // Key items are done first because their ordering is different
                float getItemWeight(SlotKey key)
                {
                    if (!itemWeights.ContainsKey(key.Item)) return 1f;
                    float[] countWeights = itemWeights[key.Item];
                    return countWeights[Math.Min(data.Location(key).Quantity - 1, countWeights.Length - 1)];
                }
                List<SlotKey> targets = silo.Targets.SelectMany(loc => data.Location(loc)).Where(slot => !silo.ExcludeTargets.Contains(slot)).ToList();
                if (explain) Console.WriteLine($"Generating {silo.Targets.Count} -> {targets.Count} slots, {targets.Distinct().Count()} distinct");
                List<ItemKey> priorityItems = assign.Priority.ToList();
                List<SlotKey> mainItems = priorityItems.Join(silo.Sources, key => key, slot => slot.Item, (key, slot) => slot).ToList();
                // Add race mode items, except those already assigned
                List<ItemKey> raceItems = ann.RaceModeItems.Except(assign.Priority).ToList();
                if (raceItems.Count > 0)
                {
                    int beadCount = 0;
                    raceItems.RemoveAll(item =>
                    {
                        if (item.ID == 4000)
                        {
                            beadCount++;
                            if (beadCount > 20) return true;
                        }
                        return false;
                    });
                    List<SlotKey> raceItemSlots = raceItems.Join(silo.Sources, key => key, slot => slot.Item, (key, slot) => slot).ToList();
                    // Use a separate seed to avoid interfering with key item locations
                    raceItemSlots = WeightedShuffle(new Random((int)options.Seed + 10), raceItemSlots, getItemWeight);
                    mainItems.AddRange(raceItemSlots);
                }
                if (mainItems.Count() > 0)
                {
                    List<SlotKey> mainLocations = WeightedShuffle(random, targets, slot => keyWeights.ContainsKey(slot) ? keyWeights[slot] : 0.001f);
                    AssignItemsToLocations(random, silo, mainItems, mainLocations, restrictions, assign.EffectiveLocation, partialLocations: true);
                }
                // Other items
                List<SlotKey> items = silo.Sources.Except(silo.Mapping.Values.SelectMany(used => used)).ToList();
                items = WeightedShuffle(random, items, getItemWeight);
                List<SlotKey> locations = WeightedShuffle(random, targets, slot => weights.ContainsKey(slot) ? weights[slot] : 0.001f);
                // A complete ordered list of non-key items and locations
                bool debugSortedEntries = false;
                if (siloType == RandomSilo.FINITE && debugSortedEntries)
                {
                    Console.WriteLine("### ITEMS AND LOCATIONS");
                    for (int i = 0; i < Math.Max(items.Count, locations.Count); i++)
                    {
                        Console.WriteLine($"{(i < items.Count ? game.Name(items[i].Item) : ".")} - {(i < locations.Count ? (ann.Slot(data.Location(locations[i]).LocScope) is SlotAnnotation s ? $"{s.Area} - {s.Text}" : "") : ".")}");
                    }
                }
                AssignItemsToLocations(random, silo, items, locations, restrictions, assign.EffectiveLocation);
            }
            // Do crow items. Put something useful things there, but nothing especially good.
            if (ann.ItemGroups.ContainsKey("crowrewards"))
            {
                List<ItemKey> rewards = ann.ItemGroups["crowrewards"].ToList();
                foreach (ItemKey item in rewards.Distinct())
                {
                    // Hack: Add to data. This is the permutation is generated, so won't be randomized, but is necessary to write the permutation.
                    data.AddLocationlessItem(item);
                }
                foreach (LocationScope target in Silos[RandomSilo.CROW].Targets)
                {
                    ItemKey item = Choice(random, rewards);
                    // Just one titanite slab
                    if (item.ID == 1003) rewards.Remove(item);
                    SlotKey sourceKey = new SlotKey(item, new ItemScope(ScopeType.SPECIAL, -1));
                    foreach (SlotKey targetKey in data.Location(target))
                    {
                        AddMulti(Silos[RandomSilo.CROW].Mapping, targetKey, sourceKey);
                    }
                }
            }
            // Also, do various extra items
            if (ann.ItemGroups.TryGetValue("fodder", out List<ItemKey> fodders))
            {
                foreach (ItemKey item in fodders.Distinct())
                {
                    // Hack: Add to data. This is the permutation is generated, so won't be randomized, but is necessary to write the permutation.
                    data.AddLocationlessItem(item);
                }
            }

            // Some light post-processing
            // Construct hint log rules
            Dictionary<ItemKey, string> hintItems = new Dictionary<ItemKey, string>();
            foreach (ItemKey key in assign.Assign.Keys)
            {
                if (ann.ItemGroups["keyitems"].Contains(key)) hintItems[key] = "key items";
                if (ann.ItemGroups["questitems"].Contains(key)) hintItems[key] = "quest items";
            }
            foreach (KeyValuePair<string, string> hintGroup in ann.HintGroups)
            {
                if (hintGroup.Key == "keyitems") continue;
                foreach (ItemKey key in ann.ItemGroups[hintGroup.Key])
                {
                    hintItems[key] = hintGroup.Value;
                }
            }
            foreach (string type in ann.HintCategories)
            {
                Hints[type] = new Dictionary<SlotKey, SlotKey>();
            }
            // Fill in hint log, and also sort ring items roughly by progression in the game (to avoid disappointing later drops)
            Dictionary<int, List<(SlotKey, SlotKey)>> ringGroups = new Dictionary<int, List<(SlotKey, SlotKey)>>();
            foreach (KeyValuePair<SlotKey, List<SlotKey>> entry in Silos[RandomSilo.FINITE].Mapping)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    SlotKey source = entry.Value[i];
                    SlotKey target = entry.Key;
                    if (hintItems.ContainsKey(source.Item))
                    {
                        Hints[hintItems[source.Item]][source] = target;
                    }
                    if (assign.Assign.ContainsKey(source.Item))
                    {
                        specialAssign[source.Item] = target;
                    }
                    if (source.Item.Type == ItemType.RING && !game.Sekiro)
                    {
                        AddMulti(ringGroups, source.Item.ID - (source.Item.ID % 10), (source, target));
                    }
                }
            }
            foreach (List<(SlotKey, SlotKey)> pairs in ringGroups.Values)
            {
                if (pairs.Count == 1) continue;
                List<SlotKey> sourceOrder = pairs.Select(p => p.Item1).OrderBy(k => k.Item).ToList();
                List<(SlotKey, SlotKey)> targetOrder = pairs.OrderBy(p => GetLogOrder(p.Item2)).ToList();
                for (int i = 0; i < sourceOrder.Count; i++)
                {
                    SlotKey newSource = sourceOrder[i];
                    (SlotKey oldSource, SlotKey target) = targetOrder[i];
                    if (explain) Console.WriteLine($"Moving {game.Name(newSource.Item)}{ann.GetLocationDescription(target)}");
                    List<SlotKey> targetSources = Silos[RandomSilo.FINITE].Mapping[target];
                    targetSources.Remove(oldSource);
                    targetSources.Add(newSource);
                }
            }
        }
        public void NoLogic(Random random)
        {
            // Simple flat permutation algorithm. Almost all gets stuck at High Wall.
            foreach (KeyValuePair<RandomSilo, SiloPermutation> entry in Silos)
            {
                RandomSilo siloType = entry.Key;
                SiloPermutation silo = entry.Value;
                if (explain) Console.WriteLine($"{siloType}: Mapping {silo.Sources.Count()} sources -> {silo.Targets.Count()} targets");
                if (siloType == RandomSilo.SELF)
                {
                    continue;
                }
                List<SlotKey> targets = silo.Targets.SelectMany(loc => data.Location(loc)).ToList();
                Shuffle(random, targets);
                AssignItemsToLocations(random, silo, silo.Sources, targets, new Dictionary<ItemKey, PendingItem>(), new Dictionary<LocationScope, string>());
            }
        }
        public SortedSet<string> SpecialLocation(LocationScope scope)
        {
            // Replication of EffectiveLocation logic, but with precise knowledge of dependent item locations
            if (assign == null) return null;
            if (!ann.Slots.ContainsKey(scope)) return null;
            SlotAnnotation slot = ann.Slots[scope];
            if (slot.QuestReqs == null) return null;
            return new SortedSet<string>(Enumerable.Concat(
                slot.FullAreaReqs,
                slot.ItemReqs.Select(item =>
                {
                    if (specialAssign.ContainsKey(item))
                    {
                        // Really this should go recursively, for quest item chains. But tbh this is fine, it's just for hints
                        SlotAnnotation otherSlot = ann.Slot(data.Location(specialAssign[item]).LocScope);
                        return otherSlot.Area;
                    }
                    return null;
                })
            ).Where(a => a != null && a != slot.Area));
        }
        public string GetLogOrder(SlotKey key)
        {
            return LogOrder.ContainsKey(key) ? LogOrder[key] : $"z{key}";
        }
        public SlotKey GetFiniteTargetKey(ItemKey key)
        {
            SlotKey target = Silos[RandomSilo.FINITE].Mapping.Where(e => e.Value.Any(s => s.Item.Equals(key))).Select(e => e.Key).FirstOrDefault();
            if (target == null)
            {
                // If not randomized, try to get a vanilla location
                return new SlotKey(key, data.Data[key].Locations.Keys.First());
            }
            return target;
        }

        // A partition of areas an item can go to, out of all areas, based on the slots it fills.
        // If a slot does not have an amount, it is ignored for the sake of this algorithm.
        // This forms a tree structure (or rather, directed acyclic graph) where the root is the least restrictive.
        public class PendingItemSlotPartition
        {
            public string Key { get; set; }
            // Only slots with fixed amounts, amount > 0
            public List<PendingItemSlot> Slots { get; set; }
            public HashSet<string> Areas { get; set; }
            public List<PendingItemSlotPartition> MoreRestrictive { get; set; }
            public List<PendingItemSlotPartition> Alternates { get; set; }
            public HashSet<string> AllAreas { get; set; }
            public bool Explain { get; set; }
            public int CountSlots(List<PendingItemSlot> taken)
            {
                return Slots.Except(taken).Select(s => s.Amount).DefaultIfEmpty().Max();
            }
            public int CountMoreRestrictiveSlots(List<PendingItemSlot> taken, bool debug)
            {
                // New approach
                // Other approach
                List<PendingItemSlotPartition> children = new List<PendingItemSlotPartition>();
                void addChildren(PendingItemSlotPartition part)
                {
                    foreach (PendingItemSlotPartition child in part.MoreRestrictive)
                    {
                        children.Add(child);
                        addChildren(child);
                    }
                }
                addChildren(this);
                if (debug) Console.WriteLine($"For {this}, found {children.Count} total child slots: {string.Join(",", children.Select(c => c.CountSlots(Slots)))}");
                return children.Count == 0 ? 0 : children.Select(c => c.CountSlots(Slots)).Max();
            }
            public bool PlaceItem(int remainingAmount, int quantity, bool debug)
            {
                // Refuse to fill this slot if there would not be enough left for slots in more restrictive nodes.
                int moreSlots = CountMoreRestrictiveSlots(Slots, debug);
                foreach (PendingItemSlotPartition alt in Alternates)
                {
                    moreSlots = Math.Max(moreSlots, alt.CountMoreRestrictiveSlots(Slots, debug));
                }
                if (debug) Console.WriteLine($"Writing if amount {remainingAmount} > more restrictive slots {moreSlots}");
                if (remainingAmount <= moreSlots)
                {
                    return false;
                }
                for (int i = Slots.Count - 1; i >= 0; i--)
                {
                    PendingItemSlot slot = Slots[i];
                    slot.PlaceItem(quantity);
                    if (slot.Amount == 0)
                    {
                        Slots.RemoveAt(i);
                    }
                }
                return true;
            }
            public override string ToString() => $"[{Key}]({string.Join(", ", Slots.Select(s => s.Amount))}) -> [{string.Join(", ", MoreRestrictive.Select(n => n.Key))}]: {string.Join(", ", Areas.Count == 0 ? AllAreas : Areas)}";
        }

        public class PendingItemSlot
        {
            // List of all locations, either from area ordering heuristics or from fixed assignment
            public HashSet<string> AllowedLocations { get; set; }
            // Special exclude tag for slot, mainly for controlling shops vs drops. Cannot be used with partitions.
            public string AdditionalExcludeTag { get; set; }
            // Amount remaining
            public int Amount { get; set; }
            // Whether locations are from a fixed assignment, and should be taken literally rather than substituted with effective area
            public bool Assigned { get; set; }
            public override string ToString() => $"[{Amount} in [{(AllowedLocations == null ? "" : string.Join(",", AllowedLocations))}]";
            public void PlaceItem(int quantity)
            {
                if (Amount > 0)
                {
                    Amount = Math.Max(0, Amount - quantity);
                }
            }
        }

        public class PendingItem
        {
            public List<PendingItemSlot> Slots { get; set; }
            // Tiered system for making sure each items get enough slots. All slots must have the same exclude tags.
            public List<PendingItemSlotPartition> Partitions { get; set; }
            public HashSet<string> ExcludeTags { get; set; }
            public HashSet<string> RequireTags { get; set; }
            public List<LocationScope> RestrictedLocs { get; set; }
            public int FreeAmount { get; set; }
            public bool Explain { get; set; }
            public override string ToString() => $"{FreeAmount} left exclude:[{string.Join(",", ExcludeTags ?? new HashSet<string>())}] from <{string.Join(", ", Slots)}>";
            public bool PlaceItem(HashSet<string> tags, string effectiveLoc, string actualLoc, string ev, int quantity, bool debugFlag=false)
            {
                if (ExcludeTags != null && ExcludeTags.Any(t => tags.Contains(t)))
                {
                    if (debugFlag) Console.WriteLine($"- Excluded because of tags [{string.Join(",", tags)}] containing [{string.Join(",", ExcludeTags)}], in location {effectiveLoc}");
                    return false;
                }
                if (RequireTags != null && !RequireTags.Any(t => tags.Contains(t)))
                {
                    if (debugFlag) Console.WriteLine($"- Excluded because of tags [{string.Join(",", tags)}] not containing [{string.Join(",", RequireTags)}], in location {effectiveLoc}");
                    return false;
                }
                if (Partitions == null)
                {
                    List<PendingItemSlot> applicable = Slots.Where(slot =>
                    {
                        if (slot.Amount == 0) return false;
                        if (slot.AllowedLocations != null)
                        {
                            // TODO: effectiveLoc is not good for key item placement, for non-missable items. Just make them all areas with WeightBases.
                            if (!slot.AllowedLocations.Contains(ev ?? effectiveLoc))
                            {
                                if (debugFlag) Console.WriteLine($"- Excluded because of location {effectiveLoc} (ev {ev}) not in {string.Join(",", slot.AllowedLocations)}");
                                return false;
                            }
                        }
                        if (slot.AdditionalExcludeTag != null)
                        {
                            if (!tags.Contains(slot.AdditionalExcludeTag))
                            {
                                if (debugFlag) Console.WriteLine($"- Excluded because of tags [{string.Join(",", tags)}] containing {slot.AdditionalExcludeTag}");
                                return false;
                            }
                        }
                        return true;
                    }).ToList();
                    if (applicable.Count == 0) return false;
                    foreach (PendingItemSlot slot in applicable)
                    {
                        slot.PlaceItem(quantity);
                    }
                    FreeAmount--;
                    return true;
                }
                if (!Partitions[0].AllAreas.Contains(effectiveLoc)) return false;
                PendingItemSlotPartition partition = Partitions.Find(part => part.Areas.Contains(effectiveLoc));
                if (partition == null)
                {
                    // No finite slot, use infinite root (least restrictive)
                    partition = Partitions[0];
                }
                if (partition.PlaceItem(FreeAmount, quantity, Explain))
                {
                    if (Explain) Console.WriteLine($"Using partition [{partition.Key}] with remaining amount {FreeAmount}");
                    FreeAmount--;
                    return true;
                }
                return false;
            }
            public void AddPartitions()
            {
                if (Slots.All(slot => slot.Amount == -1)) return;
                // Make mapping from <set of slots> to <set of partitioned locations>. Use bitmap to represent a powerset of slots.
                Dictionary<string, int> areaSlots = new Dictionary<string, int>();
                HashSet<string> allAreas = new HashSet<string>();
                for (int i = 0; i < Slots.Count; i++)
                {
                    PendingItemSlot slot = Slots[i];
                    allAreas.UnionWith(slot.AllowedLocations);
                    if (slot.Amount == -1) continue;
                    foreach (string loc in slot.AllowedLocations)
                    {
                        if (!areaSlots.ContainsKey(loc)) areaSlots[loc] = 0;
                        areaSlots[loc] |= (1 << i);
                    }
                }
                Func<int, string> fmt = i => string.Concat(Convert.ToString(i, 2).PadLeft(Slots.Count, '0').Reverse());
                Dictionary<int, HashSet<string>> slotAreas = new Dictionary<int, HashSet<string>>();
                foreach (KeyValuePair<string, int> entry in areaSlots)
                {
                    AddMulti(slotAreas, entry.Value, entry.Key);
                }
                if (Explain) foreach (KeyValuePair<int, HashSet<string>> entry in slotAreas) Console.WriteLine($"{fmt(entry.Key)}: {string.Join(", ", entry.Value)}");
                if (Explain) Console.WriteLine($"All: {string.Join(", ", allAreas)}");
                // Make graph of which locations are strictly more restrictive than others, then take transitive reduction
                // Location A is more restrictive than location B if it has a strict superset of slots, determined using bit ops.
                HashSet<(int, int)> edges = new HashSet<(int, int)>();
                foreach (int u in slotAreas.Keys)
                {
                    foreach (int v in slotAreas.Keys)
                    {
                        if (u != v && (u & v) == u)
                        {
                            edges.Add((u, v));
                        }
                    }
                }
                if (Explain) Console.WriteLine("Graph: " + string.Join(", ", edges.Select(e => $"{fmt(e.Item1)}->{fmt(e.Item2)}")));
                foreach ((int, int) edge in edges.ToList())
                {
                    if (!edges.Contains(edge)) continue;
                    (int u, int v) = edge;
                    foreach (int w in slotAreas.Keys)
                    {
                        if (w != u && w != v && (u & w) == u && (v & w) == v)
                        {
                            edges.Remove((u, w));
                        }
                    }
                }
                if (Explain) Console.WriteLine("Reduc: " + string.Join(", ", edges.Select(e => $"{fmt(e.Item1)}->{fmt(e.Item2)}")));
                // Construct each partition
                Dictionary<int, PendingItemSlotPartition> partitions = slotAreas.ToDictionary(e => e.Key, e => new PendingItemSlotPartition {
                    AllAreas = allAreas,
                    MoreRestrictive = new List<PendingItemSlotPartition>(),
                    Alternates = new List<PendingItemSlotPartition>(),
                    Areas = e.Value,
                    Slots = this.Slots.Where((slot, i) => (e.Key & (1 << i)) != 0).ToList(),
                    Key = fmt(e.Key),
                    Explain = Explain,
                });
                if (Explain) foreach (PendingItemSlotPartition part in partitions.Values) Console.WriteLine($"Made partition {part.Key}: {string.Join(",", part.Slots)}");
                HashSet<PendingItemSlotPartition> roots = new HashSet<PendingItemSlotPartition>(partitions.Values);
                foreach ((int, int) edge in edges)
                {
                    (int u, int v) = edge;
                    partitions[u].MoreRestrictive.Add(partitions[v]);
                    roots.Remove(partitions[v]);
                }
                // Root nodes should include each other as more restrictive
                foreach (PendingItemSlotPartition otherRoot in roots)
                {
                    otherRoot.Alternates.AddRange(roots.Except(new[] { otherRoot }));
                }
                // Make root node (for detecting feasibility of infinite slots) and return
                PendingItemSlotPartition root = new PendingItemSlotPartition
                {
                    AllAreas = allAreas,
                    MoreRestrictive = roots.ToList(),
                    Alternates = new List<PendingItemSlotPartition>(),
                    Areas = new HashSet<string>(),
                    Slots = new List<PendingItemSlot>(),
                    Key = "",
                };
                Partitions = new[] { root }.Concat(partitions.Values).ToList();
            }
        }

        public class RestrictedItemQueue
        {
            public readonly LinkedList<SlotKey> Queue = new LinkedList<SlotKey>();
            public Dictionary<ItemKey, PendingItem> Restrict { get; set; }
            public Dictionary<LocationScope, string> EffectiveLocation { get; set; }
            public SlotKey Dequeue(Func<SlotKey, bool> predicate)
            {

                HashSet<ItemKey> tried = new HashSet<ItemKey>();
                LinkedListNode<SlotKey> node = Queue.First;
                while (node != null)
                {
                    SlotKey slot = node.Value;
                    ItemKey key = slot.Item;
                    if (!tried.Contains(key))
                    {
                        if (predicate(slot))
                        {
                            Queue.Remove(node);
                            return slot;
                        }
                    }
                    tried.Add(key);
                    node = node.Next;
                }
                return null;
            }
            public void Enqueue(SlotKey key)
            {
                Queue.AddLast(key);
            }
        }

        private SlotKey TryGetPending(SlotKey targetKey, RestrictedItemQueue queue)
        {
            return queue.Dequeue(key =>
            {
                return TryUse(targetKey, key, queue);
            });
        }

        private bool TryUseNew(SlotKey sourceKey, SlotKey targetKey, RestrictedItemQueue queue)
        {
            if (TryUse(targetKey, sourceKey, queue))
            {
                return true;
            }
            queue.Enqueue(sourceKey);
            return false;
        }

        private bool TryUse(SlotKey targetKey, SlotKey sourceKey, RestrictedItemQueue queue)
        {
            ItemKey item = sourceKey.Item;
            ItemLocation itemLoc = data.Location(targetKey);
            LocationScope loc = itemLoc.LocScope;
            // Special restriction to make transpose items better
            if (targetKey.Scope.Type == ScopeType.MATERIAL && !data.Data[item].Unique)
            {
                return false;
            }
            // Special restriction to make scale shop items better
            if (ann.Slots.TryGetValue(loc, out SlotAnnotation prem) && prem.TagList.Contains("premium") && ann.ItemGroups.ContainsKey("premium") && !ann.ItemGroups["premium"].Contains(item))
            {
                return false;
            }
            if (!queue.Restrict.ContainsKey(item))
            {
                return true;
            }
            // Hacky check for Path of the Dragon - must be attached to an event
            if (item.Equals(new ItemKey(ItemType.GOOD, 9030)) && itemLoc.Scope.Type != ScopeType.EVENT)
            {
                return false;
            }

            PendingItem pending = queue.Restrict[item];
            if (pending.RestrictedLocs != null && pending.RestrictedLocs.Contains(loc))
            {
                return false;
            }
            SlotAnnotation slotAnn = ann.Slot(loc);
            string location = queue.EffectiveLocation.ContainsKey(loc) ? queue.EffectiveLocation[loc] : slotAnn.GetArea();
            ItemLocation sourceLoc = data.Location(sourceKey);
            int minQuant = sourceLoc.Keys.Select(k => k.Quantity).Where(k => k > 0).DefaultIfEmpty(1).Min();
            // Also, premium shop items should be singletons
            if (slotAnn.TagList.Contains("premium") && minQuant > 1) return false;
            bool debug = game.Name(item) == "No item";
            bool result = pending.PlaceItem(slotAnn.TagList, location, slotAnn.GetArea(), slotAnn.Event, minQuant, debug);
            if (pending.Explain || debug)
            {
                if (result) Console.WriteLine($"Adding {game.Name(item)} in {location} - tags {string.Join(",", slotAnn.TagList)} for: {pending}");
                else Console.WriteLine($"Not adding {game.Name(item)} in {location} with {pending.FreeAmount} remaining - tags {string.Join(",", slotAnn.TagList)}");
            }
            return result;
        }

        private void AssignItemsToLocations(
            Random random,
            SiloPermutation silo,
            List<SlotKey> items,
            List<SlotKey> locations,
            Dictionary<ItemKey, PendingItem> restrict,
            Dictionary<LocationScope, string> effectiveLocation,
            bool partialLocations=false)
        {
            RestrictedItemQueue queue = new RestrictedItemQueue
            {
                Restrict = restrict,
                EffectiveLocation = effectiveLocation,
            };
            int from = 0;
            // Initial loopthrough
            List<SlotKey> pushedLocations = new List<SlotKey>();
            // We really want to do this on race mode only
            if (partialLocations)
            {
                foreach (SlotKey source in items)
                {
                    queue.Enqueue(source);
                    from++;
                }
            }
            foreach (SlotKey targetKey in locations)
            {
                ItemLocation targetLoc = data.Location(targetKey);
                bool debug = false;  // Another per-item debug helper
                SlotAnnotation sn = debug && ann.Slots.TryGetValue(targetLoc.LocScope, out SlotAnnotation slotAnn) ? slotAnn : null;
                if (debug) Console.WriteLine($"PLACE: - Location: {sn.Text} in {sn.Area}, Partial {partialLocations}. Queue: [{string.Join(",", queue.Queue.Select(s => game.Name(s.Item)))}]");
                if (!silo.Mapping.ContainsKey(targetKey))
                {
                    SlotKey fromQueue = TryGetPending(targetKey, queue);
                    if (fromQueue != null)
                    {
                        AddMulti(silo.Mapping, targetKey, fromQueue);
                        if (debug) Console.WriteLine($"PLACE: . Assigning {game.Name(fromQueue.Item)}");
                        continue;
                    }
                    if (from == items.Count)
                    {
                        if (!partialLocations)
                        {
                            pushedLocations.Add(targetKey);
                        }
                        if (debug) Console.WriteLine($"PLACE: . Already full");
                    }
                    while (from < items.Count())
                    {
                        SlotKey sourceKey = items[from];
                        from++;
                        if (TryUseNew(sourceKey, targetKey, queue))
                        {
                            if (debug) Console.WriteLine($"PLACE: . Assigning {game.Name(sourceKey.Item)}");
                            AddMulti(silo.Mapping, targetKey, sourceKey);
                            break;
                        }
                        else if (debug) Console.WriteLine($"PLACE: . Failure to assign {game.Name(sourceKey.Item)}");
                    }
                }
                else
                {
                    // This is fine, this location can still be randomly placed to.
                    // But it should not take priority over actually unassigned locations.
                    if (debug) Console.WriteLine($"PLACE: . Already assigned");
                }
            }
            if (explain)
            {
                foreach (SlotKey i in new SortedSet<SlotKey>(queue.Queue))
                {
                    Console.WriteLine($"Could not satisfy {game.Name(i.Item)}: {(restrict.ContainsKey(i.Item) ? restrict[i.Item] : null)}");
                }
            }
            // Go through all remaining items. First dump the restricted items wherever possible
            List<SlotKey> otherItems = items.GetRange(from, items.Count() - from);
            foreach (SlotKey slot in otherItems.Where(slot => queue.Restrict.ContainsKey(slot.Item)))
            {
                queue.Enqueue(slot);
            }
            locations.Reverse();
            int iters = 0;
            while (queue.Queue.Count() > 0 && iters++ < 5)
            {
                foreach (SlotKey targetKey in locations)
                {
                    SlotKey fromQueue = TryGetPending(targetKey, queue);
                    ItemLocation targetLoc = data.Location(targetKey);
                    if (fromQueue == null) continue;
                    if (explain) Console.WriteLine($"Trying to fit restricted item {game.Name(fromQueue.Item)} after main loop");
                    if (GetAvailableSlot(silo, targetKey) == null)
                    {
                        SlotKey victim = silo.Mapping[targetKey][0];
                        // Special hack to avoid moving around key items.
                        // This can happen if an area has 1 quest slot and 1 key slot, but the quest slot cannot be satisfied due to tags.
                        // Really this pass-based system should be rewritten to avoid such destructive behavior, or at least make better decisions naturally.
                        if (ann.ItemGroups["keyitems"].Contains(victim.Item))
                        {
                            queue.Enqueue(fromQueue);
                            continue;
                        }
                        silo.Mapping[targetKey].RemoveAt(0);
                        if (queue.Restrict.ContainsKey(victim.Item))
                        {
                            queue.Enqueue(victim);
                            if (explain) Console.WriteLine($"  Re-enqueueing {game.Name(victim.Item)}");
                        }
                        else
                        {
                            otherItems.Add(victim);
                            if (explain) Console.WriteLine($"  Removing {game.Name(victim.Item)}");
                        }
                    }
                    AddMulti(silo.Mapping, targetKey, fromQueue);
                }
            }
            if (explain && queue.Queue.Count != 0) Console.WriteLine($"Couldn't satisfy {queue.Queue.Count} restricted items");
            pushedLocations.Reverse();
            otherItems.AddRange(queue.Queue);
            if (partialLocations && otherItems.Count > 0) throw new Exception($"Could not place all key items... giving up now. This can happen with full race mode.");
            if (explain) Console.WriteLine($"Attempting to satisfy {pushedLocations.Count} remaining locations with {otherItems.Count} items");
            foreach (SlotKey sourceKey in otherItems)
            {
                SlotKey targetKey = null;
                while (targetKey == null)
                {
                    if (pushedLocations.Count > 0)
                    {
                        targetKey = pushedLocations[pushedLocations.Count - 1];
                        pushedLocations.RemoveAt(pushedLocations.Count - 1);
                        if (explain) Console.WriteLine($"Assigning {game.Name(sourceKey.Item)} to unused spot {targetKey}");
                    }
                    else
                    {
                        int targetIndex = random.Next(locations.Count());
                        targetKey = GetAvailableSlot(silo, locations[targetIndex]);
                        if (explain) Console.WriteLine($"Assigning {game.Name(sourceKey.Item)} to random spot {targetKey}");
                    }
                }
                AddMulti(silo.Mapping, targetKey, sourceKey);
            }
            if (!partialLocations && silo.Type == RandomSilo.FINITE)
            {
                foreach (SlotKey targetKey in pushedLocations)
                {
                    SlotAnnotation sn = ann.Slots.TryGetValue(data.Location(targetKey).LocScope, out SlotAnnotation s) ? s : null;
                    ItemKey fodderItem = null;
                    if (ann.ItemGroups.TryGetValue("fodder", out List<ItemKey> cands))
                    {
                        fodderItem = Choice(random, cands);
                        SlotKey sourceKey = new SlotKey(fodderItem, new ItemScope(ScopeType.SPECIAL, -1));
                        AddMulti(silo.Mapping, targetKey, sourceKey);
                    }
                    if (explain) Console.WriteLine($"Unable to satisfy location {sn?.Area}: {sn?.Text}. Using {(fodderItem == null ? "<nothing>" : game.Name(fodderItem))}");
                }
            }
        }

        private SlotKey GetAvailableSlot(SiloPermutation silo, SlotKey potential)
        {
            int existingSources = silo.Mapping.ContainsKey(potential) ? silo.Mapping[potential].Count : 0;
            if (data.Location(potential).Keys.All(k => existingSources < k.MaxSlots))
            {
                return potential;
            }
            return null;
        }
    }
}
