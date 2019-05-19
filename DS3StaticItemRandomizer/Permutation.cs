using System;
using System.Collections.Generic;
using System.Linq;
using static DS3StaticItemRandomizer.AnnotationData;
using static DS3StaticItemRandomizer.LocationData;
using static DS3StaticItemRandomizer.LocationData.ItemScope;
using static DS3StaticItemRandomizer.Util;

namespace DS3StaticItemRandomizer
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
        public readonly Dictionary<EntityId, EntityId> EntitySwaps = new Dictionary<EntityId, EntityId>();
        public readonly Dictionary<SlotKey, string> LogOrder = new Dictionary<SlotKey, string>();
        public readonly SortedDictionary<HintCategory, Dictionary<SlotKey, SlotKey>> Hints = new SortedDictionary<HintCategory, Dictionary<SlotKey, SlotKey>>();
        // Only in logic runs
        private KeyItemsPermutation.Assignment assign;
        private readonly Dictionary<ItemKey, SlotKey> specialAssign = new Dictionary<ItemKey, SlotKey>();

        public enum HintCategory
        {
            KEY_ITEM,
            UPGRADE_ITEM,
            QUEST_ITEM,
            HEALING_ITEM
        }
        public static readonly Dictionary<HintCategory, string> HintCategories = new List<string> { "key items", "upgrade items", "quest items", "estus items" }
            .Select((s, i) => ((HintCategory)i, s))
            .ToDictionary(e => e.Item1, e => e.Item2);

        // Extremely hacky, but necessary for chest rando. Values are used by permutation writer.
        // Needed because some chests don't have collision names in-game - they just float in place. But enemies can't.
        public static readonly Dictionary<EntityId, string> ChestCollisionNames = new Dictionary<EntityId, string>
        {
            { new EntityId("catacombs", "o000200_9000"), "h010600" },
            { new EntityId("irithyll", "o000200_0007"), "h002500" },
        };
        public Permutation(GameData game, LocationData data, AnnotationData ann, bool explain=false)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.explain = explain;
            foreach (RandomSilo siloType in Enum.GetValues(typeof(RandomSilo)))
            {
                Silos[siloType] = new SiloPermutation();
            }
            HashSet<ItemKey> remove = new HashSet<ItemKey>(ann.ItemGroups["remove"]);
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                List<ItemKey> norandoms = new List<ItemKey>();
                bool crow = false;
                if (ann.Slots.ContainsKey(locScope))
                {
                    SlotAnnotation slotAnn = ann.Slots[locScope];
                    norandoms.AddRange(slotAnn.TagList.Where(t => t.StartsWith("norandom")).Select(t => t.Replace("norandom:", "")).Select(t => t == "norandom" ? null : ann.Items[t].Key));
                    crow = slotAnn.TagList.Contains("crow");
                }
                // Add items
                foreach (SlotKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.Location(itemLocKey);
                    ItemScope scope = location.Scope;
                    RandomSilo siloType = canPermuteTo[scope.Type];
                    if (norandoms.Contains(null) || norandoms.Contains(itemLocKey.Item))
                    {
                        // If nonrandom, just do everything here
                        AddMulti(Silos[RandomSilo.SELF].Mapping, itemLocKey, itemLocKey);
                    }
                    else if (remove.Contains(itemLocKey.Item))
                    {
                        AddMulti(Silos[RandomSilo.REMOVE].Mapping, itemLocKey, itemLocKey);
                    }
                    else if (!crow)
                    {
                        Silos[siloType].Sources.Add(itemLocKey);
                    }
                }
                // Add destination, assuming any of it is randomized
                // Also ignore special locations, which don't come from the game
                if (!norandoms.Contains(null) && locScope.Type != ScopeType.SPECIAL)
                {
                    RandomSilo siloType = canPermuteTo[locScope.Type];
                    if (crow) siloType = RandomSilo.CROW;
                    Silos[siloType].Targets.Add(locScope);
                }
            }
        }
        public enum RandomSilo
        {
            FINITE,
            INFINITE,
            MIXED,
            SELF,
            REMOVE,
            CROW
        }
        // List of what can permute to what.
        public static readonly Dictionary<ScopeType, RandomSilo> canPermuteTo = new Dictionary<ScopeType, RandomSilo>
        {
            { ScopeType.EVENT, RandomSilo.FINITE },
            { ScopeType.MATERIAL, RandomSilo.FINITE },
            { ScopeType.ENTITY, RandomSilo.FINITE },
            { ScopeType.SPECIAL, RandomSilo.FINITE },
            { ScopeType.SHOP_INFINITE, RandomSilo.INFINITE },
            { ScopeType.MODEL, RandomSilo.INFINITE },
            // In theory go from this to finite + infinite pair, and vice versa... but that is some super complicated multiplexing
            { ScopeType.SHOP_INFINITE_EVENT, RandomSilo.MIXED },
        };
        private static readonly HashSet<RandomSilo> specialSiloTypes = new HashSet<RandomSilo> { RandomSilo.SELF, RandomSilo.REMOVE, RandomSilo.CROW };
        public class SiloPermutation
        {
            public List<SlotKey> Sources = new List<SlotKey>();
            public List<LocationScope> Targets = new List<LocationScope>();
            // Mapping from targets to all sources going there
            public Dictionary<SlotKey, List<SlotKey>> Mapping = new Dictionary<SlotKey, List<SlotKey>>();
            public SiloPermutation() { }
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
        public void Logic(Random random, RandomizerOptions options)
        {
            // Randomize all chests.
            SortedDictionary<string, HashSet<(LocationScope, EntityId)>> chestSlots = new SortedDictionary<string, HashSet<(LocationScope, EntityId)>>();
            List<string> v = new List<string>();
            Func<EntityId, bool> isMimic = id => "c2120".Equals(id.ModelName);
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
            {
                foreach (ItemLocation loc in entry.Value.Locations.Values)
                {
                    // A bit hacky - assume that each mimic entity has one and exactly norandom slot. Could relax this invariant by grouping by entity id, but not needed now
                    LocationScope locScope = loc.LocScope;
                    if (!ann.Slots.ContainsKey(locScope) || ann.Slots[locScope].GetTags().Contains("norandom")) continue;
                    List<EntityId> chests = loc.Keys.SelectMany(k => k.Entities).Where(e => "o000200".Equals(e.ModelName) || "c2120".Equals(e.ModelName)).ToList();
                    foreach (EntityId chest in chests)
                    {
                        // Console.WriteLine($"Found chest {chest.EntityName} in {chest.MapName}");
                        AddMulti(chestSlots, chest.MapName, (locScope, chest));
                    }
                }
            }
            Dictionary<LocationScope, LocationScope> locSwaps = new Dictionary<LocationScope, LocationScope>();
            // Only do swaps if we can write to maps
            if (!options["unreliableenemyplacement"])
            {
                foreach (KeyValuePair<string, HashSet<(LocationScope, EntityId)>> entry in chestSlots)
                {
                    List<(LocationScope, EntityId)> oldChests = entry.Value.ToList();
                    oldChests.Sort();
                    List<(LocationScope, EntityId)> chests = new List<(LocationScope, EntityId)>(oldChests);
                    Shuffle(random, chests);
                    List<(LocationScope, EntityId)> newMimics = new List<(LocationScope, EntityId)>();
                    for (int i = 0; i < chests.Count; i++)
                    {
                        if (isMimic(chests[i].Item2) && !isMimic(oldChests[i].Item2))
                        {
                            newMimics.Add(chests[i]);
                        }
                    }
                    int moved = 0;
                    for (int i = 0; i < chests.Count; i++)
                    {
                        if (!isMimic(chests[i].Item2) && isMimic(oldChests[i].Item2))
                        {
                            locSwaps[chests[i].Item1] = newMimics[moved].Item1;
                            EntitySwaps[chests[i].Item2] = newMimics[moved].Item2;
                            if (explain)
                            {
                                Console.WriteLine($"Swapping chests:");
                                Console.WriteLine($"- {ann.Slots[chests[i].Item1].Text}");
                                Console.WriteLine($"- {ann.Slots[newMimics[moved].Item1].Text}");
                            }
                            moved++;
                        }
                    }
                }
            }
            HashSet<string> nonswapTags = new HashSet<string> { "norandom", "enemy", "miniboss", "boss" };
            foreach (KeyValuePair<LocationScope, LocationScope> swap in locSwaps)
            {
                // The only thing that gets swapped in the end is the physical location of the chests.
                // So interchange attributes related to location.
                SlotAnnotation s1 = ann.Slots[swap.Key];
                SlotAnnotation s2 = ann.Slots[swap.Value];
                HashSet<string> s1Tags = new HashSet<string>(s1.TagList.Where(t => nonswapTags.Contains(t)).Concat(s2.TagList.Where(t => !nonswapTags.Contains(t))));
                HashSet<string> s2Tags = new HashSet<string>(s2.TagList.Where(t => nonswapTags.Contains(t)).Concat(s1.TagList.Where(t => !nonswapTags.Contains(t))));
                s1.TagList = s2Tags;
                s2.TagList = s1Tags;
                ann.Slots[swap.Key] = s2;
                ann.Slots[swap.Value] = s1;
            }

            // Calculate key items, including area lateness ranking
            KeyItemsPermutation keyItems = new KeyItemsPermutation(options, data, ann, explain);
            assign = keyItems.AssignItems(random, options);
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
                SlotAnnotation slot = ann.Slot(loc);
                // dlc1 tag is special hack to disable Captain's Ashes locations
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
                { "ambush", 4 },
                { "miniboss", 5 },
                { "boss", 6 },
                { "deadend", 6 },
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
                difficultyTags["boss"]--;
                difficultyTags["deadend"]--;
            }
            Dictionary<SlotKey, float> keyWeights = new Dictionary<SlotKey, float>();
            Dictionary<SlotKey, float> weights = new Dictionary<SlotKey, float>();

            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                SlotAnnotation slot = entry.Value;
                HashSet<string> tags = new HashSet<string>(slot.GetTags());
                if (tags.Contains("death"))
                {
                    // If have to kill NPC to acquire, deprioritize the location
                    continue;
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
                if (gameLocation == "firelink" || gameLocation == "highwall" || tags.Contains("deadend")) location = maxItemLocation - 1;
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
            int itemGap = 2;
            double maxItemWeight = Math.Pow(2, ann.ItemPriority.Count + itemGap + 1); // Add some buffer
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
                if (explain) Console.WriteLine($"{siloType}: Mapping {silo.Sources.Count()} sources -> {silo.Targets.Count()} targets");
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
                        };
                        restrictions[key] = new PendingItem
                        {
                            ExcludeTags = ann.ExcludeTags.ContainsKey(key) ? ann.ExcludeTags[key] : new HashSet<string>(),
                            Slots = new List<PendingItemSlot> { slot },
                        };
                    }
                }
                Func<List<PlacementSlotAnnotation>, ItemKey, string, List<PendingItemSlot>> pendingSlotsFromPlacement = (slots, key, excludeTag) =>
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
                    // If restrictions already present from key item/quest assignment, this is not necessary to check - it is already taken into account
                    if (restrictions.ContainsKey(restrict.Key))
                    {
                        continue;
                    }
                    PendingItem pending = null;
                    HashSet<string> excludeTags = ann.ExcludeTags.ContainsKey(key) ? new HashSet<string>(ann.ExcludeTags[key]) : new HashSet<string>();
                    if (siloType == RandomSilo.FINITE && restrict.Unique != null)
                    {
                        pending = new PendingItem
                        {
                            Slots = pendingSlotsFromPlacement(restrict.Unique, key, null),
                            ExcludeTags = excludeTags,
                        };
                        bool debug = game.Name(key) == "Undead Bone Shark";
                        pending.Explain = debug;
                        if (debug) Console.WriteLine($"- Partitions for {game.Name(key)}");
                        pending.AddPartitions();
                        if (debug && pending.Partitions != null) foreach (PendingItemSlotPartition partition in pending.Partitions) Console.WriteLine($"- Partition: {partition}");
                    }
                    if (siloType == RandomSilo.INFINITE && (restrict.Shop != null || restrict.Drop != null))
                    {
                        pending = new PendingItem
                        {
                            Slots = pendingSlotsFromPlacement(restrict.Shop, key, "noshop").Concat(pendingSlotsFromPlacement(restrict.Drop, key, "shop")).ToList(),
                            ExcludeTags = excludeTags,
                        };
                    }
                    if (pending != null)
                    {
                        restrictions[key] = pending;
                    }
                }
                // All tag exclusions not already accounted for (maybe should do this in one pass)
                foreach (KeyValuePair<ItemKey, HashSet<string>> restrict in ann.ExcludeTags)
                {
                    if (restrictions.ContainsKey(restrict.Key)) continue;
                    restrictions[restrict.Key] = new PendingItem
                    {
                        Slots = new List<PendingItemSlot> { new PendingItemSlot() { Amount = -1 } },
                        ExcludeTags = restrict.Value,
                    };
                }
                // Add all per-location exclusions
                foreach (KeyValuePair<ItemKey, List<LocationScope>> restrict in assign.RestrictedItems)
                {
                    if (restrictions.ContainsKey(restrict.Key))
                    {
                        restrictions[restrict.Key].RestrictedLocs = restrict.Value;
                    }
                    else
                    {
                        restrictions[restrict.Key] = new PendingItem
                        {
                            Slots = new List<PendingItemSlot> { new PendingItemSlot { Amount = -1 } },
                            RestrictedLocs = restrict.Value,
                        };
                    }
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
                List<SlotKey> targets = silo.Targets.SelectMany(loc => data.Location(loc)).ToList();
                List<SlotKey> mainItems = assign.Priority.Join(silo.Sources, key => key, slot => slot.Item, (key, slot) => slot).ToList();
                if (mainItems.Count() > 0)
                {
                    List<SlotKey> mainLocations = WeightedShuffle(random, targets, slot => keyWeights.ContainsKey(slot) ? keyWeights[slot] : 0.001f);
                    AssignItemsToLocations(random, silo, mainItems, mainLocations, restrictions, assign.EffectiveLocation, partialLocations: true);
                }
                // Other items
                List<SlotKey> items = silo.Sources.Except(silo.Mapping.Values.SelectMany(used => used)).ToList();
                items = WeightedShuffle(random, items, key => {
                    if (!itemWeights.ContainsKey(key.Item)) return 1f;
                    float[] countWeights = itemWeights[key.Item];
                    return countWeights[Math.Min(data.Location(key).Quantity - 1, countWeights.Length - 1)];
                });
                List<SlotKey> locations = WeightedShuffle(random, targets, slot => weights.ContainsKey(slot) ? weights[slot] : 0.001f);
                AssignItemsToLocations(random, silo, items, locations, restrictions, assign.EffectiveLocation);
            }
            // Do crow items. Put something useful things there, but nothing especially good.
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

            // Some light post-processing
            // Construct hint log rules
            Dictionary<ItemKey, HintCategory> hintItems = new Dictionary<ItemKey, HintCategory>();
            foreach (ItemKey key in assign.Assign.Keys)
            {
                if (ann.ItemGroups["keyitems"].Contains(key)) hintItems[key] = HintCategory.KEY_ITEM;
                if (ann.ItemGroups["questitems"].Contains(key)) hintItems[key] = HintCategory.QUEST_ITEM;
            }
            foreach (ItemKey key in ann.ItemGroups["upgradehints"]) hintItems[key] = HintCategory.UPGRADE_ITEM;
            foreach (ItemKey key in ann.ItemGroups["estushints"]) hintItems[key] = HintCategory.HEALING_ITEM;
            foreach (HintCategory type in Enum.GetValues(typeof(HintCategory)))
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
                    if (source.Item.Type == ItemType.RING)
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
            public HashSet<string> AllAreas { get; set; }
            public bool Explain { get; set; }
            public int CountSlots(List<PendingItemSlot> taken) {
                return Slots.Except(taken).Select(s => s.Amount).DefaultIfEmpty().Max();
            }
            public int CountMoreRestrictiveSlots(List<PendingItemSlot> taken)
            {
                int amt = 0;
                // An location is more restrictive if it satisfies more slots than this one.
                // Usually this is just a single chain, where areas earlier in the game satisfy more slots (have x of item by location y).
                // If there are multiple partitions in MoreRestrictive, satisfying one may not satisfy the other, so take their sum to be safe.
                foreach (PendingItemSlotPartition more in MoreRestrictive)
                {
                    amt += Math.Max(more.CountSlots(taken), more.CountMoreRestrictiveSlots(taken));
                }
                return amt;
            }
            public bool PlaceItem(int remainingAmount, int quantity)
            {
                // Refuse to fill this slot if there would not be enough left for slots in more restrictive nodes.
                int moreSlots = CountMoreRestrictiveSlots(Slots);
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
            public HashSet<string> AllowedLocations { get; set; }
            // Special exclude tag for slot, mainly for controlling shops vs drops. Cannot be used with partitions.
            public string AdditionalExcludeTag { get; set; }
            public int Amount { get; set; }
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
            public List<LocationScope> RestrictedLocs { get; set; }
            public int FreeAmount { get; set; }
            public bool Explain { get; set; }
            public override string ToString() => $"{FreeAmount} left {string.Join(",", ExcludeTags)} from <{string.Join(", ", Slots)}>";
            public bool PlaceItem(HashSet<string> tags, string location, int quantity, bool debugFlag=false)
            {
                if (ExcludeTags.Any(t => tags.Contains(t)))
                {
                    return false;
                }
                if (Partitions == null)
                {
                    List<PendingItemSlot> applicable = Slots.Where(slot =>
                    {
                        if (slot.Amount == 0) return false;
                        return (slot.AllowedLocations == null || slot.AllowedLocations.Contains(location)) && (slot.AdditionalExcludeTag == null || !tags.Contains(slot.AdditionalExcludeTag));
                    }).ToList();
                    if (applicable.Count == 0) return false;
                    foreach (PendingItemSlot slot in applicable)
                    {
                        slot.PlaceItem(quantity);
                    }
                    FreeAmount--;
                    return true;
                }
                if (!Partitions[0].AllAreas.Contains(location)) return false;
                PendingItemSlotPartition partition = Partitions.Find(part => part.Areas.Contains(location));
                if (partition == null)
                {
                    // No finite slot, use infinite root (least restrictive)
                    partition = Partitions[0];
                }
                if (partition.PlaceItem(FreeAmount, quantity))
                {
                    FreeAmount--;
                    if (Explain) Console.WriteLine($"Using partition [{partition.Key}]");
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
                // Make root node (for detecting feasibility of infinite slots) and return
                PendingItemSlotPartition root = new PendingItemSlotPartition
                {
                    AllAreas = allAreas,
                    MoreRestrictive = roots.ToList(),
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
                    if (!tried.Contains(key)) {
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
            // Special restriction to make transpose items better
            if (targetKey.Scope.Type == ScopeType.MATERIAL && !data.Data[item].Unique)
            {
                return false;
            }
            if (!queue.Restrict.ContainsKey(item))
            {
                return true;
            }
            ItemLocation itemLoc = data.Location(targetKey);
            LocationScope loc = itemLoc.LocScope;
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
            bool result = pending.PlaceItem(slotAnn.TagList, location, minQuant);
            if (pending.Explain)
            {
                if (result) Console.WriteLine($"Adding {game.Name(item)} in {location} for: {pending}");
                else Console.WriteLine($"Not adding {game.Name(item)} in {location} with {pending.FreeAmount} remaining - tags {string.Join(",", slotAnn.TagList)}");
            }
            return result;
        }
        // Assign items.
        private void AssignItemsToLocations(Random random, SiloPermutation silo, List<SlotKey> items, List<SlotKey> locations, Dictionary<ItemKey, PendingItem> restrict, Dictionary<LocationScope, string> effectiveLocation, bool partialLocations=false)
        {
            RestrictedItemQueue queue = new RestrictedItemQueue
            {
                Restrict = restrict,
                EffectiveLocation = effectiveLocation,
            };
            int from = 0;
            // Initial loopthrough
            List<SlotKey> pushedLocations = new List<SlotKey>();
            foreach (SlotKey targetKey in locations)
            {
                ItemLocation targetLoc = data.Location(targetKey);
                bool debug = false; // Another per-item debug helper
                SlotAnnotation sn = debug && ann.Slots.ContainsKey(targetLoc.LocScope) ? ann.Slots[targetLoc.LocScope] : null;
                if (debug) Console.WriteLine($"- Location: {sn.Text}");
                if (!silo.Mapping.ContainsKey(targetKey))
                {
                    SlotKey fromQueue = TryGetPending(targetKey, queue);
                    if (fromQueue != null)
                    {
                        AddMulti(silo.Mapping, targetKey, fromQueue);
                        if (debug) Console.WriteLine($"  Assigning {game.Name(fromQueue.Item)}");
                        continue;
                    }
                    if (from == items.Count && !partialLocations)
                    {
                        pushedLocations.Add(targetKey);
                        if (debug) Console.WriteLine($"  Already full");
                    }
                    while (from < items.Count())
                    {
                        SlotKey sourceKey = items[from];
                        from++;
                        if (TryUseNew(sourceKey, targetKey, queue))
                        {
                            if (debug) Console.WriteLine($"  Assigning {game.Name(sourceKey.Item)}");
                            AddMulti(silo.Mapping, targetKey, sourceKey);
                            break;
                        } else if (debug) Console.WriteLine($"  Failure to assign {game.Name(sourceKey.Item)}");
                    }
                }
                else if (debug) Console.WriteLine($"  Already assigned");
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
            // List<SlotKey> remaining = Enumerable.Concat(),
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
                    if (fromQueue == null) continue;
                    if (explain) Console.WriteLine($"Trying to fit restricted item {game.Name(fromQueue.Item)} after main loop");
                    if (GetAvailableSlot(silo, targetKey) == null)
                    {
                        SlotKey victim = silo.Mapping[targetKey][0];
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
            if (explain && queue.Queue.Count != 0) Console.WriteLine($"Couldn't satisfy {queue.Queue.Count} items");
            pushedLocations.Reverse();
            otherItems.AddRange(queue.Queue);
            foreach (SlotKey sourceKey in otherItems)
            {
                SlotKey targetKey = null;
                while (targetKey == null)
                {
                    if (pushedLocations.Count > 0)
                    {
                        targetKey = pushedLocations[pushedLocations.Count - 1];
                        pushedLocations.RemoveAt(pushedLocations.Count - 1);
                    }
                    else
                    {
                        int targetIndex = random.Next(locations.Count());
                        targetKey = GetAvailableSlot(silo, locations[targetIndex]);

                    }
                }
                AddMulti(silo.Mapping, targetKey, sourceKey);
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
