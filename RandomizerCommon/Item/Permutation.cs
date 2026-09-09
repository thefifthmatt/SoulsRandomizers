using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class Permutation
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private Messages messages;

        private bool explain;
        private bool copydrops;
        private ItemKey debugPlacement;
        // Just a whole lot of data used for writing the permutation and auxiliary randomization
        public readonly SortedDictionary<LocationSilo, SiloPermutation> Silos = new();
        // Global mapping of base locations to silo targets across multiple silos.
        public readonly Dictionary<Location, List<ItemLocKey>> BaseTargets = new();
        // Global mapping from keys to silos, mainly just for slot counting
        public readonly Dictionary<ItemLocKey, LocationSilo> TargetSilos = new();
        // TODO: Should probably be string-keyed. It's mainly used for pricing.
        public readonly Dictionary<ItemKey, double> ItemLateness = new();
        public readonly HashSet<ItemKey> KeyItems = new();
        public readonly Dictionary<ItemKey, ItemKey> SkillAssignment = new();
        public readonly Dictionary<ItemLocKey, string> LogOrder = new();
        public readonly Dictionary<string, Dictionary<ItemLocKey, ItemLocKey>> Hints = new();
        // Only in logic runs
        public Dictionary<string, HashSet<string>> IncludedAreas => assign.IncludedAreas;
        public Dictionary<string, HashSet<string>> IncludedItems => assign.IncludedItems;
        public Dictionary<string, HashSet<string>> CombinedWeights => assign.CombinedWeights;
        public HashSet<ItemKey> NotRequiredKeyItems => assign.NotRequiredKeyItems;
        private KeyItemsPermutation.Assignment assign;
        private readonly Dictionary<string, ItemLocKey> specialAssign = new();

        [Localize]
        private static readonly Text keyItemError = new Text(
            "Could not place all important items... giving up now. Try rerolling the seed, reducing the number of important items or item restrictions, or increasing the number of important locations.",
            "Randomizer_keyItemError");

        public Permutation(GameData game, LocationData data, AnnotationData ann, Messages messages)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.messages = messages;
        }

        public void LoadLocations(RandomizerOptions opt, MergeModManifest merge = null)
        {
            HashSet<ItemKey> removeGlobal = ann.RemoveItems; // new HashSet<ItemKey>(ann.ItemGroups["remove"]);
            ann.ItemGroups.TryGetValue("norandomshop", out List<ItemKey> norandomShop);

            SiloPermutation getSilo(LocationSilo type)
            {
                if (!Silos.TryGetValue(type, out SiloPermutation silo))
                {
                    Silos[type] = silo = new SiloPermutation { Type = type };
                }
                return silo;
            }

            // Hardcoded parameterized silos based on params
            Dictionary<ItemKey, int> weaponTypes = new();
            if (game.EldenRing)
            {
                foreach (SoulsFormats.PARAM.Row row in game.Param("EquipParamWeapon").Rows)
                {
                    int weaponType = (ushort)row["wepType"].Value;
                    weaponTypes[new ItemKey(ItemType.Weapon, row.ID)] = weaponType;
                }
            }

            foreach (KeyValuePair<LocationScope, List<ItemLocKey>> entry in data.Locations)
            {
                LocationScope locScope = entry.Key;
                List<ItemKey> norandoms = new List<ItemKey>();
                List<ItemKey> removeItems = null;
                bool crow = false;
                bool removeSlot = false;
                bool racemodeSlot = false;
                bool partDlc = true;
                if (ann.Slots.ContainsKey(locScope))
                {
                    SlotAnnotation slotAnn = ann.Slots[locScope];
                    if (slotAnn.TagList.Contains("norandom") || slotAnn.HasAnyTags(ann.NorandomTags))
                    {
                        norandoms.Add(null);
                    }
                    if (slotAnn.TagItems != null && slotAnn.TagItems.TryGetValue("norandom", out List<ItemKey> items))
                    {
                        norandoms.AddRange(items);
                    }
                    if (norandomShop != null && slotAnn.HasTag("shop"))
                    {
                        norandoms.AddRange(norandomShop);
                    }
                    if (slotAnn.HasAnyTags(ann.RaceModeTags))
                    {
                        racemodeSlot = true;
                    }
                    // Crow targets get special handling
                    crow = slotAnn.TagList.Contains("crow");
                    partDlc = slotAnn.TagList.Contains("partdlc");
                    if (game.Sekiro)
                    {
                        // The remove tag is only used by Sekiro for memories, and NG+ is drop replacements.
                        removeSlot = slotAnn.TagList.Contains("remove") || slotAnn.TagList.Contains("ng+");
                    }
                    if (slotAnn.TagItems != null && slotAnn.TagItems.TryGetValue("removeitem", out List<ItemKey> tagItems))
                    {
                        removeItems = tagItems;
                    }
                }
                else if (game.EldenRing)
                {
                    // If no slot in Elden Ring, avoid randoming it entirely, since merging is quite complicated.
                    // MODEL (INFINITE, INFINITE_GEAR, INFINITE_CERTAIN) are excluded from annotation config
                    if (locScope.Type == ScopeType.Event || locScope.Type == ScopeType.ShopInfinite)
                    {
                        norandoms.Add(null);
                    }
                    // No usage for these currently, but there is a flag to use them for infinite ESD items.
                    // (This can't be used for DS3 because of Crystal Lizards - why relevant to Elden Ring?)
                    if (locScope.Type == ScopeType.Entity)
                    {
                        norandoms.Add(null);
                    }
                    // Feature for disabling enemy drop randomization with fake tag
                    if (locScope.Type == ScopeType.Model && ann.NorandomTags.Contains("enemyinf"))
                    {
                        norandoms.Add(null);
                    }
                }
                if (merge != null && locScope.Type == ScopeType.Event && merge.NorandomLotFlags.Includes(locScope.UniqueID))
                {
                    norandoms.Add(null);
                }

                // Add items
                SortedDictionary<LocationSilo, List<ItemLocKey>> targetSilos = new();
                List<ItemLocKey> norandomSlots = new();
                bool norandomLocs = merge != null && (merge.NorandomLots.Any() || merge.NorandomEnemyLots.Any() || merge.NorandomShops.Any());
                foreach (ItemLocKey itemLocKey in entry.Value)
                {
                    ItemLocation location = data.GetItemLoc(itemLocKey);
                    ItemScope scope = location.Scope;
                    // TODO: Randomize these as items? but not as lots
                    if (scope.Type == ScopeType.Asset) continue;
                    // Calculate for per-location norandom
                    bool norandomLoc = false;
                    if (norandomLocs)
                    {
                        foreach (Location locKey in location.Keys)
                        {
                            if (locKey.Type == Location.LocationType.Lot)
                            {
                                norandomLoc = locKey.Subtype == "enemy"
                                    ? merge.NorandomEnemyLots.Includes(locKey.ID)
                                    : merge.NorandomLots.Includes(locKey.ID);
                            }
                            else if (locKey.Type == Location.LocationType.Shop)
                            {
                                norandomLoc = merge.NorandomShops.Includes(locKey.ID);
                            }
                            if (norandomLoc) break;
                        }
                    }
                    // DLC items may get put into normal silos, but will be excluded through UnusedAreas.
                    // This doesn't apply to mixed locations or models (they have no locations), so use partDlc for this.
                    if (partDlc && location.DLC && !opt["dlc"])
                    {
                        norandomLoc = true;
                    }

                    ItemKey item = itemLocKey.Item;
                    RandomSilo mainSilo = canPermuteTo[scope.Type];
                    LocationSilo defaultSilo = LocationSilo.Of(mainSilo);
                    if (location.Silo == null) throw new Exception($"Internal error: unset silo for {game.Name(item)} in {itemLocKey}");
                    defaultSilo.Area = location.Silo;
                    defaultSilo.Item = ann.GetItemItemSilo(item);
                    // We have to plan ahead for silo placement of these items, as otherwise all placement will fail
                    AreaSilo tagAreaSilo = ann.GetItemAreaSilo(item);

                    // Another hack for O Mother when DLC is disabled, likely not as necessary if it becomes a real slot
                    // In general, special items should not be used as sources
                    bool norandom = norandoms.Contains(null) || norandoms.Contains(item) || ann.NorandomItems.Contains(item) || norandomLoc;
                    if (scope.Type == ScopeType.Special && norandom)
                    {
                        continue;
                    }

                    // Mainly for debugging info
                    LocationSilo usedSilo = defaultSilo.WithType(mainSilo);
                    if (norandom)
                    {
                        // If nonrandom, do the mapping here. This could use subsilos but it probably doesn't matter.
                        usedSilo = LocationSilo.Of(RandomSilo.Self);
                        AddMulti(getSilo(usedSilo).Mapping, itemLocKey, itemLocKey);
                        // Also prevent it from being added to other silos which use the same locScope
                        norandomSlots.Add(itemLocKey);
                    }
                    else if (removeSlot)
                    {
                        usedSilo = LocationSilo.Of(RandomSilo.Remove);
                        AddMulti(getSilo(usedSilo).Mapping, itemLocKey, itemLocKey);
                        getSilo(defaultSilo).ExcludeTargets.Add(itemLocKey);
                    }
                    else if (removeGlobal.Contains(item) || (removeItems != null && removeItems.Contains(item)))
                    {
                        // If only the item is removed, still allow the target slot to be used
                        usedSilo = LocationSilo.Of(RandomSilo.Remove);
                        AddMulti(getSilo(usedSilo).Mapping, itemLocKey, itemLocKey);
                        // Don't directly add default silo, especially for removeitem,
                        // because ansbachslongbow can be in a small silo and source/target mismatch is obvious.
                        LocationSilo targetSilo = defaultSilo;
                        if (!targetSilo.Item.IsDefault())
                        {
                            targetSilo = targetSilo.WithItem(ItemSilo.Default);
                        }
                        AddMulti(targetSilos, targetSilo, itemLocKey);
                    }
                    else if (crow)
                    {
                        // A source exists, but we're not adding the item, as the items are already in the "add" group.
                        usedSilo = LocationSilo.Of(RandomSilo.Crow);
                        AddMulti(targetSilos, usedSilo, itemLocKey);
                    }
                    else
                    {
                        RandomSilo newType = usedSilo.Type;
                        if (isGear(item) && gearSiloVariants.TryGetValue(newType, out RandomSilo gearSilo))
                        {
                            newType = gearSilo;
                        }
                        if (scope.Type == ScopeType.Model && location.Keys.All(k => k.Chance >= 0.99))
                        {
                            newType = RandomSilo.InfiniteCertain;
                        }
                        usedSilo = usedSilo.WithType(newType);
                        LocationSilo sourceSilo = usedSilo;
                        // This check also won't work great with region silos
                        if (sourceSilo.Area.Type == AreaSiloType.DLC && !tagAreaSilo.IsDefault())
                        {
                            sourceSilo = sourceSilo.WithArea(tagAreaSilo);
                        }
                        getSilo(sourceSilo).Sources.Add(itemLocKey);
                        AddMulti(targetSilos, usedSilo, itemLocKey);
                        if (racemodeSlot && defaultSilo.Item.Type != ItemSiloType.None)
                        {
                            // Should areaSilo use Finite always? Race mode only applies to it.
                            // Console.WriteLine($"Adding {game.Name(item)} {itemLocKey}, {usedSilo} -> {areaSilo}");
                            LocationSilo areaOnlySilo = defaultSilo.WithItem(ItemSilo.Default);
                            getSilo(areaOnlySilo).MainOnlyTargets.Add(itemLocKey);
                        }
                    }
                    // if (partDlc) Console.WriteLine($"{game.Name(itemLocKey.Item)}: {(game.EldenDlcItem(itemLocKey.Item) ? "dlc*" : "no")}{(partDlc ? " <<<<<<<" : "")} in {itemSilo}");
                    // if (locScope.Type == ScopeType.Model) Console.WriteLine($"{itemLocKey} ({game.Name(item)}) was placed in {usedSilo}, with norandoms {string.Join(",", norandoms)} and excluded {string.Join(",", norandomSlots)}. {string.Join(" ", targetSilos.Keys)}");
                }
                // Add destination, assuming any of it is randomized
                // Also ignore special locations, which don't come from the game
                if (targetSilos.Count > 0 && locScope.Type != ScopeType.Special)
                {
                    foreach ((LocationSilo targetSilo, List<ItemLocKey> sourceKeys) in targetSilos)
                    {
                        List<ItemLocKey> newKeys = new();
                        // Do some computation to avoid adding multiple items for the same base slot per silo, which ensures that
                        // fine-grained silos work and that sources outnumber targets. This doesn't account for multiple slots
                        // which share a base, like Erdtree Avatars with multiple tear drops, as each needs to be a distinct target,
                        // so another pass is needed across silos below.
                        // Previous version of this was complicated and wrong.
                        HashSet<Location> bases = new();
                        foreach (ItemLocKey key in sourceKeys)
                        {
                            if (norandomSlots.Contains(key)) continue;
                            List<Location> baseLocs = data.GetItemLoc(key).Keys.Select(k => k.BaseLocation).ToList();
                            bool otherBase = data.GetItemLoc(key).Keys.Any(k => bases.Contains(k.OtherBase));
                            if (!otherBase || targetSilo.Item.Type != ItemSiloType.None)
                            {
                                newKeys.Add(key);
                            }
                            bases.UnionWith(baseLocs);
                        }
                        getSilo(targetSilo).Targets.AddRange(newKeys);
                        foreach (ItemLocKey slotKey in norandomSlots)
                        {
                            // Partial exclusion of norandom items
                            // TODO: Is this necessary anymore? Since they are skipped being added to newKeys
                            getSilo(targetSilo).ExcludeTargets.Add(slotKey);
                        }
                    }
                }
            }

            // Populate global mapping of bases, for adding additional items to spots with items already.
            // This is the main case where ItemLocKeys across and within silos will directly influence each other, to avoid hitting the max slot count.
            foreach ((LocationSilo siloType, SiloPermutation silo) in Silos)
            {
                foreach (ItemLocKey target in silo.Targets)
                {
                    TargetSilos[target] = siloType;
                    foreach (Location loc in data.GetItemLoc(target).Keys)
                    {
                        AddMulti(BaseTargets, loc.BaseLocation, target);
                        // Console.WriteLine($"Adding {baseLoc} -> {target}");
                    }
                }
            }
        }

        public enum RandomSilo
        {
            // Event flag
            Finite,
            // Infinite model drop items
            Infinite,
            // Shop items which have a finite and infinite variant
            Mixed,
            // Non-randomized
            Self,
            // Removed
            Remove,
            // Given random resource drops
            Crow,
            // Infinite shop items
            InfiniteShop,
            // Infinite gear items, which are shared across shops and models
            InfiniteGear,
            // Infinite model drops which are also guaranteed
            InfiniteCertain,
        }

        // List of what can permute to what.
        public static readonly Dictionary<ScopeType, RandomSilo> canPermuteTo = new Dictionary<ScopeType, RandomSilo>
        {
            { ScopeType.Event, RandomSilo.Finite },
            { ScopeType.Material, RandomSilo.Finite },
            { ScopeType.Entity, RandomSilo.Finite },
            { ScopeType.Special, RandomSilo.Finite },
            { ScopeType.ShopInfinite, RandomSilo.InfiniteShop },
            { ScopeType.Model, RandomSilo.Infinite },
            // In theory go from this to finite + infinite pair, and vice versa... but that is some super complicated multiplexing
            { ScopeType.ShopInfiniteEvent, RandomSilo.Mixed },
        };

        private static readonly Dictionary<RandomSilo, RandomSilo> gearSiloVariants = new Dictionary<RandomSilo, RandomSilo>
        {
            [RandomSilo.Infinite] = RandomSilo.InfiniteGear,
            [RandomSilo.InfiniteShop] = RandomSilo.InfiniteGear,
        };

        private bool isGear(ItemKey item)
        {
            if (item.Type == ItemType.Protector) return true;
            // Arrows are at the start
            if (game.DS3 && item.Type == ItemType.Weapon && item.ID >= 1000000) return true;
            // Arrows are at the end
            if (game.EldenRing && item.Type == ItemType.Weapon && !(item.ID >= 50000000 && item.ID < 60000000)) return true;
            return false;
        }

        private static readonly HashSet<RandomSilo> specialSiloTypes = new HashSet<RandomSilo> { RandomSilo.Self, RandomSilo.Remove, RandomSilo.Crow };

        public class LocationSilo : IComparable<LocationSilo>
        {
            public RandomSilo Type { get; set; }
            public AreaSilo Area { get; set; } = AreaSilo.Default;
            public ItemSilo Item { get; set; } = ItemSilo.Default;

            public static LocationSilo Of(RandomSilo type) => new LocationSilo { Type = type };

            public LocationSilo WithType(RandomSilo type) => new LocationSilo { Type = type, Area = Area, Item = Item };
            public LocationSilo WithArea(AreaSilo area) => new LocationSilo { Type = Type, Area = area, Item = Item };
            public LocationSilo WithItem(ItemSilo item) => new LocationSilo { Type = Type, Area = Area, Item = item };
            public LocationSilo Copy() => WithType(Type);

            public override string ToString() => string.Join(" ", new[] { Type.ToString(), Area.Name, Item.Name }.Where(s => s != null));
            public int CompareTo(LocationSilo o)
            {
                return (Type, Area.Type, Area.Index, Item.Type, Item.Index).CompareTo((o.Type, o.Area.Type, o.Area.Index, o.Item.Type, o.Item.Index));
            }
        }

        public IEnumerable<KeyValuePair<LocationSilo, SiloPermutation>> GetSilos(RandomSilo type)
        {
            return Silos.Where(e => e.Key.Type == type);
        }

        public IEnumerable<KeyValuePair<ItemLocKey, List<ItemLocKey>>> GetMapping(RandomSilo type)
        {
            return Silos.Where(e => e.Key.Type == type).SelectMany(e => e.Value.Mapping);
        }

        public class SiloPermutation
        {
            public LocationSilo Type { get; set; }
            public List<ItemLocKey> Sources = new();
            public List<LocationScope> PrevTargets = new();
            public List<ItemLocKey> Targets = new();
            // Locations for which some targets should not be considered part of this silo. TODO: Should just make targets a list of SlotKeys also?
            public HashSet<ItemLocKey> ExcludeTargets = new();
            // Targets which can be added for the main pass, as otherwise they are in a more specific item silo.
            // This prevents e.g. a key items from being unplaceable at Elemer/Niall if weapons have their own silos.
            public List<ItemLocKey> MainOnlyTargets = new();
            // Mapping from targets to all sources going there
            public Dictionary<ItemLocKey, List<ItemLocKey>> Mapping = new();
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

        public void Logic(Random random, RandomizerOptions options, EnemyPreset preset)
        {
            if (preset != null)
            {
                preset.ProcessItemPreset(ann);
            }
#if DEBUG
            explain = options["explain"];
            copydrops = options["copydrops"];
            // debugPlacement = game.ItemForName("Somberstone Miner's Bell Bearing [2]");
            // debugPlacement = game.ItemForName("O Mother");
            // debugPlacement = game.ItemForName("Rya's Necklace");
            // debugPlacement = new ItemKey(ItemType.GOOD, 8109);
            // debugPlacement = game.ItemForName("Prisoner Chief's Ashes");
#endif

            // Calculate key items, including area lateness ranking
            KeyItemsPermutation keyItems = new KeyItemsPermutation(options, data, ann, explain);
            assign = keyItems.AssignItems(random, options, preset);
            foreach ((string item, HashSet<string> areas) in assign.Assign)
            {
                ItemKey key = ann.Items[item];
                if (!data.Items.ContainsKey(key))
                {
                    if (!ann.NorandomItems.Contains(key))
                    {
                        Console.WriteLine($"Warning: item {game.DisplayName(key)} is part of logic but does not exist in data");
                    }
                    continue;
                }
                ItemLocations locs = data.Items[key];
                if (!ItemLateness.ContainsKey(key))
                {
                    // TODO: Area set order is arbitrary, and this could be string-keyed for multi-items. Would need to find item name during pricing, though.
                    ItemLateness[key] = assign.LocationLateness[areas.First()];
                }
                ICollection<ItemLocation> itemLocs = locs.Locations.Values;
                if (ann.MultiItems.ContainsKey(key))
                {
                    itemLocs = itemLocs.Where(itemLoc => itemLoc.ItemName == item).ToList();
                }
                // Changed in ER DLC because of Thiollier's concoction, requires just one finite location rather than only finite
                if (itemLocs.All(loc => canPermuteTo[loc.LocScope.Type] != RandomSilo.Finite) && !ann.NorandomItems.Contains(key))
                {
                    throw new Exception($"Can't randomize {game.DisplayName(key)} ({item}, {key}) because it was moved to an unusable location" + (game.HasMods ? " (likely by a merged mod)" : ""));
                }
                if (explain) Console.WriteLine($"Locations for {item}: {string.Join(", ", itemLocs.Select(i => i.LocScope))}");
            }
            KeyItems.UnionWith(assign.Priority);

            // Disable unused locations from DLCs, according to areas unused in key item processing, as well as make ordering
            HashSet<ItemLocKey> unusedSlots = new HashSet<ItemLocKey>();
            HashSet<LocationScope> unusedLocations = new HashSet<LocationScope>();
            foreach (KeyValuePair<LocationScope, List<ItemLocKey>> entry in data.Locations)
            {
                LocationScope loc = entry.Key;
                if (loc.Type == ScopeType.Model) continue;  // when will this come to bite...
                SlotAnnotation slot = ann.Slot(loc);
                // dlc1 tag is special hack to disable Captain's Ashes locations
                // also, hack to avoid Sekiro locations... please remove this...
                if (slot.GetArea() == "unknown"
                    || assign.IncludedAreas[slot.GetArea()].Count == 0
                    || (slot.TagList.Contains("dlc1") && !options["dlc1"]))
                {
                    unusedLocations.Add(entry.Key);
                    unusedSlots.UnionWith(entry.Value);
                }
                else
                {
                    string area = assign.EffectiveLocation.ContainsKey(loc) ? assign.EffectiveLocation[loc] : slot.GetArea();
                    int areaLate = (int)(assign.LocationLateness[area] * 9999);
                    string orderKey = $"{areaLate.ToString("0000")},{entry.Key}";
                    // if (slot.HasTag("furnacegolem")) Console.WriteLine($"{areaLate} -> {orderKey} in {area} ({slot.Text})");
                    foreach (ItemLocKey order in entry.Value)
                    {
                        LogOrder[order] = orderKey;
                    }
                }
            }
            foreach (SiloPermutation silo in Silos.Values)
            {
                silo.Sources.RemoveAll(slot => unusedSlots.Contains(slot));
                silo.Targets.RemoveAll(slot => unusedSlots.Contains(slot));
                silo.PrevTargets.RemoveAll(loc => unusedLocations.Contains(loc));
                // All mappings so far are self mappings
                silo.Mapping = silo.Mapping.Where(e => !unusedSlots.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);
            }

            // Categorize locations
            List<string> locationTags = new List<string> { "early", "mid", "late" };
            Dictionary<string, int> difficultyTags = new Dictionary<string, int>
            {
                { "enemy", 1 },
                { "enemytalisman", 1 },
                { "shop", 1 },
                { "outoftheway", 2 },
                { "hardenemy", 2 },
                { "hidden", 3 },
                { "reqevent", 3 },
                { "enemyfragment", 3 },
                { "enemyrevered", 3 },
                { "ambush", 4 }, // TODO: Should be less
                { "furnacegolem", 4 },
                { "miniboss", 5 },
                { "minibossrespawn", 5 },
                { "altboss", 5 },
                { "altbossnight", 5 },
                { "boss", 6 },
                { "deadend", 6 },
                { "premium", 6 },
            };
            int maxDifficulty = 6; // difficultyTags.Values.Max();

            // Initialize all weights based on difficulty settings
            int unfairRanking = (int)Math.Round((maxDifficulty - 1) * ann.Weights.Unfair);
            int veryUnfairRanking = (int)Math.Round(maxDifficulty * ann.Weights.VeryUnfair);
            // This was previously rounded to an integer for cleaner numbers, but it should be deterministic even if not
            double keyItemDifficulty = ann.Weights.KeyItemSpread;
            double desirableDifficulty = ann.Weights.ItemSpread;

            difficultyTags["unfair"] = unfairRanking;
            difficultyTags["veryunfair"] = veryUnfairRanking;
            if (keyItemDifficulty > 18)
            {
                difficultyTags["miniboss"]--;
                difficultyTags["minibossrespawn"]--;
                difficultyTags["boss"]--;
                difficultyTags["deadend"]--;
            }
            Dictionary<ItemLocKey, float> keyWeights = new Dictionary<ItemLocKey, float>();
            Dictionary<ItemLocKey, float> weights = new Dictionary<ItemLocKey, float>();

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
                if (slot.Event != null && assign.RequiredEvents.Contains(slot.Event) && (tags.Contains("miniboss") || tags.Contains("altboss") || tags.Contains("boss")))
                {
                    tags.Remove("altboss");
                    tags.Remove("miniboss");
                    tags.Remove("boss");
                    tags.Add("reqevent");
                }
                double maxWeight = Math.Pow(2, keyItemDifficulty);
                string gameLocation = assign.EffectiveLocation.ContainsKey(entry.Key) ? assign.EffectiveLocation[entry.Key] : slot.GetArea();
                if (gameLocation == "unknown" || assign.IncludedAreas[gameLocation].Count == 0) continue;
                // Weights for key items (lateness within location)
                (double, double) keyWeight = (1, maxWeight);
                (int location, int maxItemLocation) = slot.GetAreaIndex();
                // Oof hardcoding... but these areas are so small, it's more of a challenge to put the item anywhere in the area, for key items
                if (gameLocation.StartsWith("firelink") || gameLocation == "highwall" || tags.Contains("deadend"))
                {
                    location = maxItemLocation - 1;
                }
                keyWeight = GetSubRange(keyWeight, location, maxItemLocation);
                // Weights for all items (lateness within game)
                maxWeight = Math.Pow(2, desirableDifficulty);
                (double, double) weight = (1, maxWeight);
                if (!options["fog"])
                {
                    weight = GetSubRange(weight, (int)(assign.LocationLateness[gameLocation] * 20), 20);
                }
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
                float reduceKeyQuantity = 1;
                // TODO: Do this for all shops if it's worth doing, try for Golden Seeds/Bell Bearings/etc
                if (game.EldenRing && slot.HasTag("raceshop"))
                {
                    // There are many merchant shops with many dozens of slots, so given them the aggregate probability of a single slot (ish).
                    reduceKeyQuantity = data.GetBaseItemLocs(entry.Key).Count;
                    if (explain) Console.WriteLine($"Found shop {entry.Key} slot with targets {data.GetBaseItemLocs(entry.Key).Count}: {slot.Text}");
                }
                foreach (ItemLocKey target in data.GetBaseItemLocs(entry.Key))
                {
                    keyWeights[target] = (float)keyWeight.Item1 / reduceKeyQuantity;
                    weights[target] = (float)weight.Item1 / reduceKeyQuantity;
                }
            }
            // Classify items
            Dictionary<ItemKey, float[]> itemWeights = new Dictionary<ItemKey, float[]>();
            // Add some buffer between priority items and others in DS3
            // In Sekiro there are much fewer items, so high priority items can afford to spread out more evenly.
            // In Elden Ring I guess do the same.
            int itemGap = !game.DS3 ? 0 : 2;
            double maxItemWeight = Math.Pow(!game.DS3 ? 1.2 : 2, ann.ItemPriority.Count + itemGap + 1);
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
                        if (!itemWeights.ContainsKey(key))
                        {
                            itemWeights[key] = countWeights;
                        }
                    }
                }
                priority--;
            }

            // Do randomizations per silo
            foreach ((LocationSilo siloType, SiloPermutation silo) in Silos)
            {
                RandomSilo mainSilo = siloType.Type;
#if DEBUG
                Console.WriteLine($"{siloType}: Mapping {silo.Sources.Count()} sources -> {silo.Targets.Count()} targets");
#endif
                // Filled in before or after
                if (specialSiloTypes.Contains(mainSilo))
                {
                    continue;
                }

                // Add placement restrictions. There are a lot of these
                Dictionary<ItemKey, PendingItem> restrictions = new Dictionary<ItemKey, PendingItem>();
                PendingItem directAreaPlacement(HashSet<string> areas)
                {
                    PendingItemSlot slot = new PendingItemSlot
                    {
                        AllowedLocations = areas,
                        Amount = -1,
                        Assigned = true,
                    };
                    return new PendingItem
                    {
                        Slots = new List<PendingItemSlot> { slot },
                    };
                }
                PendingItem directMultiAreaPlacement(Dictionary<string, HashSet<string>> nameAreas)
                {
                    List<PendingItemSlot> slots = nameAreas.Select(e => new PendingItemSlot
                    {
                        AllowedLocations = e.Value,
                        Amount = -1,
                        SourceItemName = e.Key,
                        Assigned = true,
                    }).ToList();
                    return new PendingItem
                    {
                        Slots = slots,
                    };
                }
                if (mainSilo == RandomSilo.Finite)
                {
                    HashSet<ItemKey> processed = new();
                    Dictionary<ItemKey, List<string>> assignByKey = assign.Assign.Keys
                        .Select(item => (ann.Items[item], item))
                        .GroupBy(e => e.Item1)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.Item2).ToList());
                    foreach ((ItemKey key, List<string> names) in assignByKey)
                    {
                        if (names.Count == 1)
                        {
                            restrictions[key] = directAreaPlacement(assign.Assign[names[0]]);
                        }
                        else
                        {
                            if (!ann.MultiItems.ContainsKey(key))
                            {
                                throw new Exception($"Internal error: item {key} has multiple instances {string.Join(",", names)} but source locations are not marked");
                            }
                            restrictions[key] = directMultiAreaPlacement(names.ToDictionary(name => name, name => assign.Assign[name]));
                        }
                    }
                }
                bool debugNonEventAreas = false;
                List<PendingItemSlot> pendingSlotsFromPlacement(List<PlacementSlotAnnotation> slots, ItemKey key, string excludeTag)
                {
                    List<PendingItemSlot> ret = new List<PendingItemSlot>();
                    if (slots == null) return ret;
                    foreach (PlacementSlotAnnotation slot in slots)
                    {
                        // Note: These locs must contain eligible events, or else it won't be possible to place items there
                        // (the event takes precedence over the area when both exist)
                        HashSet<string> locs = new HashSet<string>(
                            slot.AllowedAreas(assign.IncludedAreas, assign.CombinedWeights, key.Equals(debugPlacement))
                                .Where(a => ann.Areas.ContainsKey(a) || ann.Events.ContainsKey(a)));
                        ret.Add(new PendingItemSlot
                        {
                            AllowedLocations = locs,
                            Amount = slot.Amount,
                            Expected = slot.Amount,
                            AdditionalExcludeTag = excludeTag,
                        });
#if DEBUG
                        if (debugNonEventAreas)
                        {
                            // These are placements that would need to be change to start moving away from events
                            HashSet<string> allowed = slot.AllowedAreas(assign.IncludedAreas, assign.CombinedWeights, key.Equals(debugPlacement));
                            List<(string, string)> noEventAreas = allowed
                                .SelectMany(a =>
                                    ann.AreaEvents.TryGetValue(a, out List<string> evs)
                                        ? evs.Where(ev => !allowed.Contains(ev)).Select(ev => (a, ev))
                                        : Enumerable.Empty<(string, string)>())
                                .ToList();
                            if (noEventAreas.Count > 0)
                            {
                                Console.WriteLine($"{game.Name(key)} areas missing events: {string.Join(", ", noEventAreas)}");
                            }
                        }
#endif
                    }
                    return ret;
                };
                foreach (PlacementRestrictionAnnotation restrict in ann.ItemRestrict.Values)
                {
                    ItemKey key = restrict.Key;
                    // If restrictions already present from key item/quest assignment, location restrictions are not necessary
                    // They're already taken into account using KeyAreas or quest before/after checks
                    if (restrictions.ContainsKey(key))
                    {
                        continue;
                    }
                    // All location restrictions are currently assumed to be base-game only, so if this is DLC silo, they do not apply.
                    // Also if this is base game and the item is DLC-only, don't use it either, but this probably has no effect except for accounting at the end.
                    if (silo.Type.Area.IsDlc() || ann.GetItemAreaSilo(key).IsDlc())
                    {
                        continue;
                    }
                    PendingItem pending = null;
                    if (mainSilo == RandomSilo.Finite && restrict.Unique != null)
                    {
                        pending = new PendingItem
                        {
                            Slots = pendingSlotsFromPlacement(restrict.Unique, key, null),
                        };
                        bool debug = key.Equals(debugPlacement); // game.Name(key) == "Item";
                        pending.Explain = debug;
                        if (debug) Console.WriteLine($"- Partitions for {game.Name(key)}");
                        pending.AddPartitions();
                        if (debug && pending.Partitions != null) foreach (PendingItemSlotPartition partition in pending.Partitions) Console.WriteLine($"- Partition: {partition}");
                    }
                    else if (silo.Type.Type == RandomSilo.Finite && restrict.KeyAreas != null)
                    {
                        // This is not used? Only if the item is excluded from key item placement
                        HashSet<string> keyAreas = new HashSet<string>();
                        foreach (string area in restrict.KeyAreas.Split(' '))
                        {
                            keyAreas.Add(area);
                            if (ann.AreaEvents.TryGetValue(area, out List<string> events)) keyAreas.UnionWith(events);
                        }
                        pending = directAreaPlacement(keyAreas);
                    }
                    if ((mainSilo == RandomSilo.Infinite || mainSilo == RandomSilo.InfiniteShop) && (restrict.Shop != null || restrict.Drop != null))
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
                foreach (KeyValuePair<ItemKey, HashSet<string>> restrict in ann.RequireTags)
                {
                    addRestrictionIfNoneExists(restrict.Key);
                    if (restrict.Value.Count > 0)
                    {
                        restrictions[restrict.Key].RequireTags = restrict.Value;
                        // Console.WriteLine($"Restrictions for {game.Name(restrict.Key)}: {string.Join(",", restrict.Value)}");
                    }
                }
                // Add all per-location exclusions
                foreach (KeyValuePair<ItemKey, List<LocationScope>> restrict in assign.RestrictedItems)
                {
                    addRestrictionIfNoneExists(restrict.Key);
                    restrictions[restrict.Key].RestrictedLocs = restrict.Value;
                }
                // Preprocessing of restrictions - get total count for items, so we know how to distribute them
                foreach (ItemLocKey source in silo.Sources)
                {
                    if (restrictions.ContainsKey(source.Item))
                    {
                        PendingItem pending = restrictions[source.Item];
                        pending.FreeAmount++;
                        pending.TotalAmount++;
                    }
                }

                // Shuffle!
                // Key items are done first because their ordering is different
                float getItemWeight(ItemLocKey key)
                {
                    if (!itemWeights.ContainsKey(key.Item)) return 1f;
                    float[] countWeights = itemWeights[key.Item];
                    return countWeights[Math.Min(data.GetItemLoc(key).Quantity - 1, countWeights.Length - 1)];
                }
                List<ItemLocKey> targets = silo.Targets.Where(slot => !silo.ExcludeTargets.Contains(slot)).ToList();
                if (explain) Console.WriteLine($"Generating {silo.Targets.Count} -> {targets.Count} slots, {targets.Distinct().Count()} distinct");
                List<ItemKey> priorityItems = assign.Priority.ToList();
                List<ItemLocKey> mainItems = priorityItems.Join(silo.Sources, key => key, slot => slot.Item, (key, slot) => slot).ToList();
                // Add race mode items, except those already assigned
                List<ItemKey> raceItems = ann.RaceModeItems.Except(assign.Priority).ToList();
                if (raceItems.Count > 0)
                {
                    int beadCount = 0;
                    // TODO: Group this up by total amount for region silo. ItemKeys are either placed in one silo or another.
                    raceItems.RemoveAll(item =>
                    {
                        // 20 out of 40 beads in Sekiro
                        if (game.Sekiro && item.ID == 4000)
                        {
                            beadCount++;
                            if (beadCount > 20) return true;
                        }
                        // 16 out of 33 golden seeds in Elden Ring (11 flasks)
                        // 10 flasks would be 12 seeds, 11 is 16, 12 is 20, 13 is 25, 14 is 30
                        else if (game.EldenRing && item.ID == 10010)
                        {
                            beadCount++;
                            if (beadCount > 16) return true;
                        }
                        return false;
                    });
                    List<ItemLocKey> raceItemSlots = raceItems.Join(silo.Sources, key => key, slot => slot.Item, (key, slot) => slot).ToList();
                    // Use a separate seed to avoid interfering with key item locations
                    raceItemSlots = WeightedShuffle(new Random((int)options.Seed + 10), raceItemSlots, getItemWeight);
                    mainItems.AddRange(raceItemSlots);
                }
                bool debugSortedEntries = false;
                if (mainItems.Count() > 0)
                {
                    // Console.WriteLine($"Silos: {string.Join(", ", Silos.Keys)}\nAdding {string.Join(", ", silo.MainOnlyTargets)}");
                    List<ItemLocKey> importantTargets = targets.Concat(silo.MainOnlyTargets).ToList();
                    List<ItemLocKey> mainLocations = WeightedShuffle(random, importantTargets, slot => keyWeights.ContainsKey(slot) ? keyWeights[slot] : 0.001f);
#if DEBUG
                    Console.WriteLine($"{mainSilo} main: Mapping {mainItems.Count} sources -> {mainLocations.Count} targets");
#endif
                    if (debugSortedEntries && ann.RaceModeItems.Count > 0)
                    {
                        // Console.WriteLine($"### MAIN ITEMS AND LOCATIONS ({string.Join(", ", ann.RaceModeItems.Select(game.Name))})");
                        string mainName(ItemLocKey s) => game.Name(s.Item) + (assign.Priority.Contains(s.Item) ? "*" : "");
                        Console.WriteLine($"### MAIN ITEMS AND LOCATIONS {silo.Type}: {mainItems.Count} items ({string.Join(", ", mainItems.Select(mainName))})");
                        int show = 1;
                        for (int i = 0; i < mainLocations.Count; i++)
                        {
                            if (ann.Slot(data.GetItemLoc(mainLocations[i]).LocScope) is SlotAnnotation s && s.HasAnyTags(ann.RaceModeTags))
                            {
                                Console.WriteLine($"{show++}. {s.Area} - {s.Text}");
                            }
                        }
                    }
                    AssignItemsToLocations(random, silo, mainItems, mainLocations, restrictions, assign.EffectiveLocation, partialLocations: true);
                }
                // Other items
                List<ItemLocKey> items = silo.Sources.Except(silo.Mapping.Values.SelectMany(used => used)).ToList();
                items = WeightedShuffle(random, items, getItemWeight);
                List<ItemLocKey> locations = WeightedShuffle(random, targets, slot => weights.ContainsKey(slot) ? weights[slot] : 0.001f);
                // A complete ordered list of non-key items and locations
                if (mainSilo == RandomSilo.Finite && debugSortedEntries)
                {
                    Console.WriteLine("### ITEMS AND LOCATIONS");
                    for (int i = 0; i < Math.Max(items.Count, locations.Count); i++)
                    {
                        string itemDesc = ".";
                        if (i < items.Count)
                        {
                            itemDesc = game.Name(items[i].Item);
                        }
                        string locDesc = ".";
                        if (i < locations.Count)
                        {
                            SlotAnnotation s = ann.Slot(data.GetItemLoc(locations[i]).LocScope);
                            locDesc = $"{s.Area} - {s.Text} - {locations[i]}";
                        }
                        Console.WriteLine($"Rank {itemDesc} <-> {locDesc}");
                    }
                }
                AssignItemsToLocations(random, silo, items, locations, restrictions, assign.EffectiveLocation);
#if DEBUG
                foreach ((ItemKey key, PendingItem pending) in restrictions)
                {
                    // TODO: Make this reasonable for item silos, not directly DLC related
                    break;
                    if (pending.Explain || !pending.Satisfied)
                    {
                        Console.WriteLine($"Final partitions for {game.Name(key)}, {pending.DisplayAmount}");
                        if (pending.Partitions != null)
                        {
                            foreach (PendingItemSlotPartition partition in pending.Partitions)
                            {
                                Console.WriteLine($"- Partition: {partition}");
                            }
                        }
                    }
                }
#endif
            }
            // Do crow items. Put something useful things there, but nothing especially good.
            if (ann.ItemGroups.ContainsKey("crowrewards"))
            {
                List<ItemKey> rewards = ann.ItemGroups["crowrewards"].ToList();
                Dictionary<ItemKey, ItemLocation> crowLocs = new();
                foreach (ItemKey item in rewards.Distinct())
                {
                    // Hack: Add to data. This is the permutation is generated, so won't be randomized, but is necessary to write the permutation.
                    crowLocs[item] = data.AddLocationlessItem(item);
                }
                foreach ((LocationSilo siloType, SiloPermutation silo) in GetSilos(RandomSilo.Crow))
                {
                    // TODO DS3: This used to select an item per LocationScope when Targets was LocationScopes.
                    // Check if the new impl produces anything funny.
                    foreach (ItemLocKey targetKey in silo.Targets)
                    {
                        ItemKey item = Choice(random, rewards);
                        // Just one titanite slab
                        if (item.ID == 1003) rewards.Remove(item);
                        ItemLocKey sourceKey = new ItemLocKey(item, crowLocs[item].LocScope);
                        AddMulti(silo.Mapping, targetKey, sourceKey);
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
            foreach (string item in assign.Assign.Keys)
            {
                ItemKey key = ann.Items[item];
                if (ann.ItemGroups["keyitems"].Contains(key)) hintItems[key] = "key items";
                if (ann.ItemGroups["questitems"].Contains(key)) hintItems[key] = "quest items";
            }
            // Key item detection is based on area logic which is mostly eliminated by fog rando mode.
            // Just use all keys in this case.
            if (options["fog"])
            {
                foreach (ItemKey key in ann.ItemGroups["keyitems"])
                {
                    hintItems[key] = "key items";
                }
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
                Hints[type] = new Dictionary<ItemLocKey, ItemLocKey>();
            }

            // Fill in hint log, and also sort ring items roughly by progression in the game (to avoid disappointing later drops)
            Dictionary<ItemKey, string> uniqueItemNames = ann.Items.GroupBy(e => e.Value).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First().Key);
            foreach ((LocationSilo siloType, SiloPermutation silo) in GetSilos(RandomSilo.Finite))
            {
                Dictionary<int, List<(ItemLocKey, ItemLocKey)>> ringGroups = new Dictionary<int, List<(ItemLocKey, ItemLocKey)>>();
                foreach (KeyValuePair<ItemLocKey, List<ItemLocKey>> entry in silo.Mapping)
                {
                    for (int i = 0; i < entry.Value.Count; i++)
                    {
                        ItemLocKey source = entry.Value[i];
                        ItemLocKey target = entry.Key;
                        // Console.WriteLine($"hint for {game.Name(source.Item)}");
                        if (hintItems.ContainsKey(source.Item))
                        {
                            Hints[hintItems[source.Item]][source] = target;
                        }
                        // Used for chained hints only (ItemReqs). This does not fully support all items, missing multi-item support.
                        if (uniqueItemNames.TryGetValue(source.Item, out string itemName) && assign.Assign.TryGetValue(itemName, out HashSet<string> reqs))
                        {
                            specialAssign[itemName] = target;
                        }
                        if (source.Item.Type == ItemType.Accessory && !game.Sekiro && !options["fog"] && game.GetBaseAccessory(source.Item) is int baseAcc)
                        {
                            AddMulti(ringGroups, baseAcc, (source, target));
                        }
                    }
                }
                foreach (List<(ItemLocKey, ItemLocKey)> pairs in ringGroups.Values)
                {
                    if (pairs.Count == 1) continue;
                    List<ItemLocKey> sourceOrder = pairs.Select(p => p.Item1).OrderBy(k => k.Item).ToList();
                    List<(ItemLocKey, ItemLocKey)> targetOrder = pairs.OrderBy(p => GetLogOrder(p.Item2)).ToList();
                    for (int i = 0; i < sourceOrder.Count; i++)
                    {
                        ItemLocKey newSource = sourceOrder[i];
                        (ItemLocKey oldSource, ItemLocKey target) = targetOrder[i];
                        if (explain) Console.WriteLine($"Moving accessory: {game.Name(newSource.Item)}{ann.GetLocationDescription(target)}");
                        List<ItemLocKey> targetSources = silo.Mapping[target];
                        targetSources.Remove(oldSource);
                        targetSources.Add(newSource);
                    }
                }
            }
        }

        public void NoLogic(Random random)
        {
            // Simple flat permutation algorithm. Almost all gets stuck at High Wall.
            foreach ((LocationSilo siloType, SiloPermutation silo) in Silos)
            {
                if (explain) Console.WriteLine($"{siloType}: Mapping {silo.Sources.Count()} sources -> {silo.Targets.Count()} targets");
                if (siloType.Type == RandomSilo.Self)
                {
                    continue;
                }
                List<ItemLocKey> targets = silo.Targets.ToList();
                Shuffle(random, targets);
                AssignItemsToLocations(random, silo, silo.Sources, targets, new Dictionary<ItemKey, PendingItem>(), new Dictionary<LocationScope, string>());
            }
        }

        // Used for hints
        public SortedSet<string> SpecialLocation(LocationScope scope)
        {
            // Replication of EffectiveLocation logic, but with precise knowledge of dependent item locations
            if (assign == null) return null;
            if (!ann.Slots.ContainsKey(scope)) return null;
            SlotAnnotation slot = ann.Slots[scope];
            if (slot.QuestReqs == null) return null;
            return new SortedSet<string>(Enumerable.Concat(
                slot.AreaReqs,
                slot.ItemReqs.Select(item =>
                {
                    if (specialAssign.ContainsKey(item))
                    {
                        // Really this should go recursively, for quest item chains. But tbh this is fine, it's just for hints
                        SlotAnnotation otherSlot = ann.Slot(data.GetItemLoc(specialAssign[item]).LocScope);
                        return otherSlot.Area;
                    }
                    return null;
                })
            ).Where(a => a != null && a != slot.Area));
        }

        public string GetLogOrder(ItemLocKey key)
        {
            return LogOrder.ContainsKey(key) ? LogOrder[key] : $"z{key}";
        }

        // Used by HintWriter and SkillWriter, mainly to get the area of a given item.
        // Also PermutationWriter for upgrading remembrance shops or not.
        public ItemLocKey GetFiniteTargetKey(ItemKey key)
        {
            ItemLocKey target = GetMapping(RandomSilo.Finite).Where(e => e.Value.Any(s => s.Item.Equals(key))).Select(e => e.Key).FirstOrDefault();
            if (target == null)
            {
                // If not randomized, try to get a vanilla location
                LocationScope locScope = data.Items[key].Locations.Keys.FirstOrDefault();
                if (locScope == null) return null;
                return new ItemLocKey(key, locScope);
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
            private IEnumerable<PendingItemSlot> NonEmptySlots => Slots.Where(s => s.Amount > 0);
            public HashSet<string> Areas { get; set; }
            public List<PendingItemSlotPartition> MoreRestrictive { get; set; }
            public List<PendingItemSlotPartition> Alternates { get; set; }
            public HashSet<string> AllAreas { get; set; }
            public bool Explain { get; set; }
            public bool Satisfied => !NonEmptySlots.Any();

            private int CountSlots(HashSet<PendingItemSlot> taken)
            {
                return NonEmptySlots.Except(taken).Select(s => s.Amount).DefaultIfEmpty().Max();
            }

            // TODO: Just make MoreRestrictive equivalent to the result of this
            private int CountMoreRestrictiveSlots(HashSet<PendingItemSlot> taken, bool debug)
            {
                /*List<int> slots = new List<int> { CountSlots(taken) };
                int slots = CountSlots(taken);
                List<int> children = MoreRestrictive.Select(c => c.CountSlots(taken)).ToList();
                if (children.Count > 0)
                {
                    slots = Math.Max(slots, children).Max());
                }
                Console.WriteLine($"Found {CountSlots(taken)} and {} For {this}, found {CountSlots(taken)} total child slots: {string.Join(",", children.Select(c => c.CountSlots(taken)))}")
                taken.UnionWith(NonEmptySlots);
                taken.UnionWith(MoreRestrictive.SelectMany(c => c.NonEmptySlots));
                return slots;*/
                List<PendingItemSlotPartition> children = new List<PendingItemSlotPartition>();
                children.Add(this);
                void addChildren(PendingItemSlotPartition part)
                {
                    foreach (PendingItemSlotPartition child in part.MoreRestrictive)
                    {
                        children.Add(child);
                        // addChildren(child);
                    }
                }
                addChildren(this);
                if (debug) Console.WriteLine($"For {this}, found child slots: {string.Join(",", children.Select(c => c.CountSlots(taken)))}");
                int slots = children.Select(c => c.CountSlots(taken)).Max();
                taken.UnionWith(children.SelectMany(c => c.NonEmptySlots));
                return slots;
                // return children.Count == 0 ? 0 : children.Select(c => c.CountSlots(taken)).Max();
            }

            public bool TryPlaceItemInPartition(int remainingAmount, int quantity, bool debug)
            {
                // List<PendingItemSlot> ownSlots = NonEmptySlots.ToList();
                // Refuse to fill this slot if there would not be enough left for slots in more restrictive or unrelated exclusive nodes.
                HashSet<PendingItemSlot> accountedFor = new HashSet<PendingItemSlot>(NonEmptySlots);
                int moreSlots = CountMoreRestrictiveSlots(accountedFor, debug);
                // TODO: This does not work well in Elden Ring, in that with locations.Reverse() below, this adds to earlier slots
                // even though exclusive later slots cannot be filled as a result.
                int exclusiveSlots = 0;
                foreach (PendingItemSlotPartition alt in Alternates)
                {
                    exclusiveSlots += alt.CountMoreRestrictiveSlots(accountedFor, debug);
                    // moreSlots = Math.Max(moreSlots, alt.CountMoreRestrictiveSlots(ownSlots, debug));
                }
                moreSlots = Math.Max(moreSlots, exclusiveSlots);
                if (debug) Console.WriteLine($"Writing {quantity} if amount {remainingAmount} > more restrictive slots {moreSlots}, from alts {Key}->{string.Join(",", Alternates.Select(alt => alt.Key))}");
                if (remainingAmount > moreSlots)
                {
                    foreach (PendingItemSlot slot in Slots)
                    {
                        slot.PlaceItemInSlot(quantity);
                    }
                    return true;
                }
                return false;
            }
            public override string ToString() => $"[{Key}]({string.Join(",", Slots.Select(s => s.DisplayAmount))}) -> [{string.Join(",", MoreRestrictive.Select(n => n.Key))}]: {string.Join(",", Areas.Count == 0 ? AllAreas : Areas)}";
        }

        public class PendingItemSlot
        {
            // List of all locations, either from area ordering heuristics or from fixed assignment
            public HashSet<string> AllowedLocations { get; set; }
            // Special exclude tag for slot, mainly for controlling shops vs drops. Cannot be used with partitions.
            public string AdditionalExcludeTag { get; set; }
            // Original expected amount to be filled
            public int Expected { get; set; }
            // Amount remaining
            public int Amount { get; set; }
            // Whether locations are from a fixed assignment, and should be taken literally rather than substituted with effective area
            // Unused?
            public bool Assigned { get; set; }
            // Only applies to source items from slots with this ItemName
            public string SourceItemName { get; set; }

            public override string ToString() => $"[{Amount} in [{(AllowedLocations == null ? "" : string.Join(",", AllowedLocations))}]";
            public string DisplayAmount => Amount == Expected ? $"{Amount}" : $"{Amount}/{Expected}";

            public void PlaceItemInSlot(int quantity)
            {
                Amount = Amount - quantity;
                /*if (Amount > 0)
                {
                    Amount = Math.Max(0, Amount - quantity);
                }*/
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
            public int TotalAmount { get; set; }
            public int FreeAmount { get; set; }
            public bool Explain { get; set; }
            public bool Satisfied => Partitions == null || Partitions.All(p => p.Satisfied);
            public string DisplayAmount => $"{FreeAmount}/{TotalAmount} left";

            public override string ToString() => $"{FreeAmount} left exclude:[{string.Join(",", ExcludeTags ?? new HashSet<string>())}] from <{string.Join(", ", Slots)}>";

            public bool TryPlaceItemInLocation(
                HashSet<string> tags,
                string effectiveLoc,
                string actualLoc,
                string ev,
                string sourceName,
                int quantity,
                bool dlc,
                bool debugFlag=false)
            {
                // Hardcode dlc tag check as some slots are split, and adding nodlc everywhere would be a waste
                if (ExcludeTags != null && (ExcludeTags.Any(t => tags.Contains(t)) || (dlc ? ExcludeTags.Contains("dlc") : ExcludeTags.Contains("nodlc"))))
                {
                    if (debugFlag) Console.WriteLine($"- Excluded because of tags [{string.Join(",", tags)}]+ {(dlc ? "dlc" : "nodlc")} containing [{string.Join(",", ExcludeTags)}], in location {effectiveLoc}");
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
                        if (slot.SourceItemName != null && sourceName != null && slot.SourceItemName != sourceName) return false;
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
                        slot.PlaceItemInSlot(quantity);
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
                if (partition.TryPlaceItemInPartition(FreeAmount, quantity, Explain))
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
                if (false)
                {
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
                }
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
            // Queue itself
            public readonly LinkedList<ItemLocKey> Queue = new();
            // Mutable. Must be set
            public Dictionary<ItemKey, PendingItem> Restrict { get; set; }
            // Mutable. If set, limits instances of items placed in shops
            public Dictionary<(ItemKey, LocationScope), int> ShopCount { get; set; }
            // Data to reference. Must be set
            public HashSet<ItemKey> MultiItems { get; set; }
            public Dictionary<LocationScope, string> EffectiveLocation { get; set; }

            public ItemLocKey Dequeue(Func<ItemLocKey, bool> predicate)
            {

                HashSet<ItemKey> tried = new HashSet<ItemKey>();
                LinkedListNode<ItemLocKey> node = Queue.First;
                while (node != null)
                {
                    ItemLocKey slot = node.Value;
                    ItemKey key = slot.Item;
                    if (tried.Add(key) || MultiItems.Contains(key))
                    {
                        if (predicate(slot))
                        {
                            Queue.Remove(node);
                            return slot;
                        }
                    }
                    node = node.Next;
                }
                return null;
            }
            public void Enqueue(ItemLocKey key)
            {
                Queue.AddLast(key);
            }
        }

        private ItemLocKey TryGetPending(ItemLocKey targetKey, RestrictedItemQueue queue)
        {
            return queue.Dequeue(key =>
            {
                return TryUse(targetKey, key, queue);
            });
        }

        private bool TryUseNew(ItemLocKey sourceKey, ItemLocKey targetKey, RestrictedItemQueue queue)
        {
            if (TryUse(targetKey, sourceKey, queue))
            {
                return true;
            }
            queue.Enqueue(sourceKey);
            return false;
        }

        private bool TryUse(ItemLocKey targetKey, ItemLocKey sourceKey, RestrictedItemQueue queue)
        {
            // bool extraDebug = false; // game.Name(targetKey.Item) == "Cerulean Seed Talisman +1" && game.Name(sourceKey.Item) == "Dark Moon Ring";
            // if (extraDebug) Console.WriteLine($"{sourceKey} -> {targetKey}");
            ItemKey item = sourceKey.Item;
            ItemLocation targetLoc = data.GetItemLoc(targetKey);
            LocationScope loc = targetLoc.LocScope;
            // Special restriction to make transpose items better
            if (targetKey.Scope.Type == ScopeType.Material && !data.Items[item].Unique)
            {
                return false;
            }
            // Special restriction to make various other items better or accord to specific lists
            if (ann.UsePremiumGroups)
            {
                if (ann.PremiumGroups.TryGetValue(loc, out HashSet<ItemKey> allowedItems) && !allowedItems.Contains(item))
                {
                    return false;
                }
            }
            else if (ann.Slots.TryGetValue(loc, out SlotAnnotation prem))
            {
                if (prem.TagList.Contains("premium") || prem.TagList.Contains("dragonshop") || prem.TagList.Contains("bossshop"))
                {
                    if (game.Sekiro && ann.ItemGroups.ContainsKey("premium") && !ann.ItemGroups["premium"].Contains(item))
                    {
                        return false;
                    }
                    else if (game.EldenRing && !data.Items[item].Unique)
                    {
                        return false;
                    }
                }
                if (game.EldenRing && prem.TagList.Contains("restrict"))
                {
                    if (prem.TagList.Contains("sorceries") && ann.ItemGroups.ContainsKey("sorceries") && !ann.ItemGroups["sorceries"].Contains(item))
                    {
                        return false;
                    }
                    else if (prem.TagList.Contains("incantations") && ann.ItemGroups.ContainsKey("incantations") && !ann.ItemGroups["incantations"].Contains(item))
                    {
                        return false;
                    }
                }
            }
            // Hacky check for Path of the Dragon - must be attached to an event
            if (game.DS3 && item.Equals(new ItemKey(ItemType.Goods, 9030)) && targetLoc.Scope.Type != ScopeType.Event)
            {
                return false;
            }
            if (game.EldenRing && item.Equals(new ItemKey(ItemType.Goods, 2009004)) && targetLoc.Scope.Type != ScopeType.Event)
            {
                return false;
            }
            SlotAnnotation slotAnn = ann.Slot(loc);
            ItemLocation sourceLoc = data.GetItemLoc(sourceKey);
            int minQuant = sourceLoc.Keys.Select(k => k.Quantity).Where(k => k > 0).DefaultIfEmpty(1).Min();
            // Also, premium shop items should have quantity 1, so only 1 can be sold. Restrict amount of premiums as a result
            if (slotAnn.TagList.Contains("premium") && minQuant > 1) return false;
            // Ignore quantities for Elden Ring, because the main objective is to ensure sufficient coverage across locations
            if (game.EldenRing && minQuant > 1) minQuant = 1;

            // Checks for items with placement restrictions
            if (!queue.Restrict.TryGetValue(item, out PendingItem pending))
            {
                return true;
            }
            if (pending.RestrictedLocs != null && pending.RestrictedLocs.Contains(loc))
            {
                return false;
            }
            // Filter to avoid same-y shops for restricted items
            // Meant for DLC where Messmer Shards can add more items than default important locations
            if (queue.ShopCount != null && slotAnn.HasTag("shop"))
            {
                int shopCount = AddCount(queue.ShopCount, (item, loc));
                if (shopCount > 3)
                {
                    return false;
                }
            }
            string location = queue.EffectiveLocation.ContainsKey(loc) ? queue.EffectiveLocation[loc] : slotAnn.GetArea();
            string sourceName = sourceLoc.ItemName;
            bool debug = item.Equals(debugPlacement);
            // debug = targetLoc.Scope.EventID == 290430;
            // slotAnn.Text == "Exchanged for Remembrances by Finger Reader Enia"; // slotAnn.TagList.Contains("premium");
            // game.Name(item) == "Medicine Peddler's Bell Bearing" && location == "snowfield" && !slotAnn.TagList.Contains("missable") && loc.UniqueId == 1042527040
            // debug = item.Equals(new ItemKey(ItemType.GOOD, 8109)) && slotAnn.Key == "3550,0:0000510810::";
            // debug = game.Name(item) == "Dark Moon Ring" && location == "hinterland_dheo";
            // debug = location == "altus_oldtunnel"; // messmerskindling16
            // debug = location == "dungeon_oldcell" && game.Name(item) == "Easterner's Ashes";
            // debug = location == "firelink_cemetery";
            // debug = game.Name(item) == "Somberstone Miner's Bell Bearing [2]" && slotAnn.Event == "elemer";

            bool result = pending.TryPlaceItemInLocation(slotAnn.TagList, location, slotAnn.GetArea(), slotAnn.Event, sourceName, minQuant, targetLoc.DLC, debug);
            // Additional condition: pending.Explain
            if (debug)
            {
                string itemName = sourceName ?? game.Name(item);
                if (result) Console.WriteLine($"Adding {itemName} {minQuant} in {location} - tags {string.Join(",", slotAnn.TagList)} for: {pending} ({slotAnn.Text})");
                else Console.WriteLine($"Not adding {itemName} in {location} with {pending.FreeAmount} remaining - tags {string.Join(",", slotAnn.TagList)} ({slotAnn.Text})");
            }
            return result;
        }

        private void PlaceItemInSilo(SiloPermutation silo, ItemLocKey sourceKey, ItemLocKey targetKey, string phase)
        {
            AddMulti(silo.Mapping, targetKey, sourceKey);
            if (debugPlacement != null && silo.Type.Type == RandomSilo.Finite && debugPlacement.Equals(sourceKey.Item))
            {
                ItemLocation targetLoc = data.GetItemLoc(targetKey);
                SlotAnnotation slotAnn = ann.Slot(targetLoc.LocScope);
                Console.WriteLine($"{phase} phase: {sourceKey.Scope} -> {targetKey}, in {slotAnn.Area}. {slotAnn.Text}");
            }
        }

        private string GetSourceName(ItemLocKey source)
        {
            ItemLocation itemLoc = data.GetItemLoc(source);
            return itemLoc.ItemName ?? game.Name(source.Item);
        }

        private void AssignItemsToLocations(
            Random random,
            SiloPermutation silo,
            List<ItemLocKey> items,
            List<ItemLocKey> locations,
            Dictionary<ItemKey, PendingItem> restrict,
            Dictionary<LocationScope, string> effectiveLocation,
            bool partialLocations=false)
        {
            RestrictedItemQueue queue = new RestrictedItemQueue
            {
                Restrict = restrict,
                MultiItems = new(ann.MultiItems.Keys),
                EffectiveLocation = effectiveLocation,
            };
            int from = 0;
            // Initial loopthrough
            List<ItemLocKey> pushedLocations = new List<ItemLocKey>();
            // We really want to do this on race mode only
            if (partialLocations)
            {
                foreach (ItemLocKey source in items)
                {
                    queue.Enqueue(source);
                    from++;
                }
                queue.ShopCount = new();
            }
            Predicate<SlotAnnotation> debugSlot = null;
#if DEBUG
            // debugSlot = sa => sa.Area == "dungeon_oldcell";
            // debugSlot = sa => sa.Area == "altus_oldtunnel";
            // debugSlot = sa => sa.Key == "614542,0:0000000000:102250:";
            // debugSlot = sa => silo.Type.Name == "Weapon 55";
            // debugSlot = sa => sa.Text != null && sa.Text.Contains("Minor Erdtree south of Frenzied")
            // debugSlot = sa => sa.Text == "Exchanged for Remembrances by Finger Reader Enia";
#endif
            foreach (ItemLocKey targetKey in locations)
            {
                ItemLocation targetLoc = data.GetItemLoc(targetKey);
                SlotAnnotation sn = debugSlot != null && ann.Slots.TryGetValue(targetLoc.LocScope, out SlotAnnotation slotAnn) && debugSlot(slotAnn) ? slotAnn : null;
                bool debug = sn != null;
                if (debug) Console.WriteLine($" PLACE: {from}/{items.Count} - {targetLoc.LocScope} {targetKey}: [{sn?.Text}] in [{sn?.Area}], Partial {partialLocations}. Queue: [{string.Join(",", queue.Queue.Select(GetSourceName))}]");
                if (!silo.Mapping.ContainsKey(targetKey))
                {
                    ItemLocKey fromQueue = TryGetPending(targetKey, queue);
                    if (fromQueue != null)
                    {
                        PlaceItemInSilo(silo, fromQueue, targetKey, "primary");
                        if (debug) Console.WriteLine($"PLACE: . Assigning {game.Name(fromQueue.Item)}");
                        continue;
                    }
                    if (from == items.Count)
                    {
                        if (!partialLocations)
                        {
                            pushedLocations.Add(targetKey);
                        }
                        if (debug) Console.WriteLine($"PLACE: . No items left and no applicable queue items");
                    }
                    while (from < items.Count())
                    {
                        ItemLocKey sourceKey = items[from];
                        from++;
                        if (TryUseNew(sourceKey, targetKey, queue))
                        {
                            if (debug) Console.WriteLine($"PLACE: . Assigning {game.Name(sourceKey.Item)}");
                            PlaceItemInSilo(silo, sourceKey, targetKey, "dequeued");
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
            queue.ShopCount = null;
#if DEBUG
            // explain = !silo.Type.Item.IsDefault();
            if (explain)
            {
                foreach (ItemLocKey i in new SortedSet<ItemLocKey>(queue.Queue))
                {
                    Console.WriteLine($"Could not satisfy {game.Name(i.Item)} in {silo.Type}: {(restrict.ContainsKey(i.Item) ? restrict[i.Item] : null)}");
                }
            }
#endif
            // Go through all remaining items. First dump the restricted items wherever possible, splitting it from otherItems to avoid duplication
            List<ItemLocKey> otherItems = items.GetRange(from, items.Count() - from);
            otherItems.RemoveAll(slot =>
            {
                if (queue.Restrict.ContainsKey(slot.Item))
                {
                    queue.Enqueue(slot);
                    return true;
                }
                return false;
            });
            // TODO: At high bias, this results in earlier slots getting filled in, but the slots
            // are set up in an exclusive way in Elden Ring, so later location constraints are not getting met.
            // locations.Reverse();
            int iters = 0;
            // Console.WriteLine($"-- {silo.Type}: {queue.Queue.Count()} left with {locations.Count} locations");
            while (queue.Queue.Count() > 0 && iters++ < 5)
            {
                foreach (ItemLocKey targetKey in locations)
                {
                    ItemLocKey fromQueue = TryGetPending(targetKey, queue);
                    ItemLocation targetLoc = data.GetItemLoc(targetKey);
                    if (fromQueue == null) continue;
                    if (explain) Console.WriteLine($"Fitting restricted item {game.Name(fromQueue.Item)} after main loop -> {targetLoc}");
                    if (!IsAvailableSlot(silo, targetKey))
                    {
                        ItemLocKey victim = silo.Mapping[targetKey][0];
                        // Special hack to avoid moving around key items.
                        // This can happen if an area has 1 quest slot and 1 key slot, but the quest slot cannot be satisfied due to tags.
                        // Really this pass-based system should be rewritten to avoid such destructive behavior, or at least make better decisions naturally.
                        if (ann.ItemGroups["keyitems"].Contains(victim.Item))
                        {
                            if (explain) Console.WriteLine($"  Skipping {game.Name(victim.Item)}, it would dislodge a key item");
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
                    // if (explain) Console.WriteLine($"Count for item: {queue.Queue.Count(k => k == fromQueue)} queue, {otherItems.Count(k => k == fromQueue)} other");
                    PlaceItemInSilo(silo, fromQueue, targetKey, "reenqueued");
                }
            }
            if (explain && queue.Queue.Count != 0) Console.WriteLine($"Couldn't satisfy {queue.Queue.Count} restricted items");
            pushedLocations.Reverse();
            otherItems.AddRange(queue.Queue);
            if (partialLocations && otherItems.Count > 0)
            {
                HashSet<(ItemKey, string)> restrictShown = new();
                string unplacedInfo(ItemLocKey source)
                {
                    string basicInfo = $"({game.Name(source.Item)}, {source.Scope})";
                    if (restrict.TryGetValue(source.Item, out PendingItem pending) && pending.Slots != null)
                    {
                        ItemLocation itemLoc = data.GetItemLoc(source);
                        ICollection<string> locs = null;
                        if (itemLoc.ItemName != null)
                        {
                            basicInfo += $" {itemLoc.ItemName}";
                            PendingItemSlot slot = pending.Slots.Find(s => s.SourceItemName == itemLoc.ItemName);
                            if (slot != null) locs = slot.AllowedLocations;
                        }
                        else if (pending.Slots.Count == 1)
                        {
                            locs = pending.Slots[0].AllowedLocations;
                        }
                        if (locs != null)
                        {
                            basicInfo += $" -> {string.Join(",", locs)}";
                        }
                    }
                    return basicInfo;
                }
                Console.WriteLine($"Unplaced:\n{string.Join("\n", otherItems.Select(unplacedInfo))}");
                throw new Exception(messages.Get(keyItemError));
            }
            if (explain) Console.WriteLine($"Attempting to satisfy {pushedLocations.Count} remaining locations with {otherItems.Count} items");
            bool debugEnd = false;
            foreach (ItemLocKey sourceKey in otherItems)
            {
                ItemLocKey targetKey = null;
                int targetAttempt = 0;
                while (targetKey == null)
                {
                    if (pushedLocations.Count > 0)
                    {
                        targetKey = pushedLocations[pushedLocations.Count - 1];
                        pushedLocations.RemoveAt(pushedLocations.Count - 1);
                        if (explain && debugEnd) Console.WriteLine($"Assigning {game.Name(sourceKey.Item)} to unused spot {targetKey} #{targetAttempt}");
                        PlaceItemInSilo(silo, sourceKey, targetKey, "unused");
                    }
                    else
                    {
                        int targetIndex = random.Next(locations.Count());
                        targetKey = locations[targetIndex];
                        if (IsAvailableSlot(silo, targetKey))
                        {
                            if (explain && debugEnd) Console.WriteLine($"Assigning {game.Name(sourceKey.Item)} to random spot {targetKey} #{targetAttempt}");
                            PlaceItemInSilo(silo, sourceKey, targetKey, "random");
                        }
                        else
                        {
                            targetKey = null;
                        }
                    }
                    if (targetAttempt++ > 10000)
                    {
                        string info = $"Couldn't find space in {silo.Type} silo for {items.Count} items and {locations.Count} locations.";
                        if (!silo.Type.Item.IsDefault())
                        {
                            if (items.Count > locations.Count)
                            {
                                // This could happen from any option which adds locationless items
                                info += " Each item in an item silo can only be replaced by at most one other item.";
                                if (copydrops)
                                {
                                    info += " Try disable adding guaranteed copies of enemy drops, which adds items but not locations.";
                                }
                            }
                            else
                            {
                                info += " Try removing restrictions placed on items in the item silo through item placement or premium location rules.";
                            }
                        }
                        else if (game.HasMods)
                        {
                            // Diablo loot mods tend to cause this
                            info += " If a merged mod adds many items per lot, it may be incompatible with Item Randomizer.";
                        }
                        throw new Exception(info);
                    }
                }
            }
            if (!partialLocations && silo.Type.Type == RandomSilo.Finite)
            {
                // Fodder has an issue with item silos if the targets are not exactly precise (e.g. before SlotKey Targets rewrite)
                // As a result print out a warning below, but still not sure if they should be used
                foreach (ItemLocKey targetKey in pushedLocations)
                {
                    SlotAnnotation sn = ann.Slots.TryGetValue(data.GetItemLoc(targetKey).LocScope, out SlotAnnotation s) ? s : null;
                    ItemKey fodderItem = null;
                    if (ann.ItemGroups.TryGetValue("fodder", out List<ItemKey> cands))
                    {
                        fodderItem = Choice(random, cands);
                        ItemLocation fodderLoc = data.AddLocationlessItem(fodderItem);
                        ItemLocKey sourceKey = new ItemLocKey(fodderItem, fodderLoc.LocScope);
                        PlaceItemInSilo(silo, sourceKey, targetKey, "fodder");
                    }
                    if (explain) Console.WriteLine($"Unable to satisfy location {sn?.Area}: {sn?.Text}. Using fodder {(fodderItem == null ? "<nothing>" : game.Name(fodderItem))}");
                }
                if (pushedLocations.Count > 0)
                {
                    Console.WriteLine($"Could not satisfy all locations in {silo.Type} silo, so some items are not randomized");
                }
            }
        }

        private bool IsAvailableSlot(SiloPermutation silo, ItemLocKey potential)
        {
            // TODO: See if this is sufficient to avoid overslotting. Also revisit MaxSlot calculation
            // int existingSources = silo.Mapping.ContainsKey(potential) ? silo.Mapping[potential].Count : 0;
            HashSet<ItemLocKey> targets = new();
            int maxSlots = 100;
            if (!silo.Type.Item.IsDefault())
            {
                maxSlots = 1;
            }
            // This mapping could be precomputed, since it's just one-to-one in most cases, but could be a unique list for other ones
            foreach (Location loc in data.GetItemLoc(potential).Keys)
            {
                Location baseLoc = loc.BaseLocation;
                maxSlots = Math.Min(maxSlots, baseLoc.MaxSlots);
                if (BaseTargets.TryGetValue(baseLoc, out List<ItemLocKey> ts))
                {
                    targets.UnionWith(ts);
                }
            }
#if DEBUG
            // This shouldn't happen but don't fail over it normally
            if (targets.Count == 0)
            {
                // No locations will lead to true condition below, matching previous version of this function
                throw new Exception($"No base locations in global dictionary for {potential}");
            }
            if (!targets.Contains(potential))
            {
                throw new Exception($"{potential} not in its own list of locations");
            }
#endif
            // This could be done in the above pass, maybe. How common is it for targets to be duplicated across multiple bases?
            int existingSources = 0;
            foreach (ItemLocKey target in targets)
            {
                if (TargetSilos.TryGetValue(target, out LocationSilo siloType)
                    && Silos.TryGetValue(siloType, out SiloPermutation targetSilo)
                    && targetSilo.Mapping.TryGetValue(target, out List<ItemLocKey> sources))
                {
                    existingSources += sources.Count;
                }
            }
            if (existingSources < maxSlots)
            {
                return true;
            }
            return false;
        }

#if DEBUG
        private bool IsAvailableSlotPrev(SiloPermutation silo, ItemLocKey potential)
        {
            int existingSources = GetSilos(silo.Type.Type).Sum(e => e.Value.Mapping.TryGetValue(potential, out var sources) ? sources.Count : 0);
            // TODO: Fix shops a better different way
            // if (silo.Type.Type == RandomSilo.Finite && silo.Type.ItemType != ItemSiloType.None && existingSources >= 1) return null;
            if (potential.Scope.UniqueID >= 65290 && potential.Scope.UniqueID <= 65310 && false)
            {
                Console.WriteLine($"^^^^^ Target {potential}: {existingSources} sources");
                foreach (Location loc in data.GetItemLoc(potential).Keys)
                {
                    Location baseLoc = loc.OtherBase ?? loc;
                    if (!BaseTargets.TryGetValue(baseLoc, out List<ItemLocKey> ts)) ts = new();
                    Console.WriteLine($"- {baseLoc} -> [{string.Join(", ", ts)}]");
                }
                foreach ((LocationSilo siloType, SiloPermutation allSilo) in GetSilos(silo.Type.Type))
                {
                    Console.WriteLine(siloType);
                    if (allSilo.Mapping.TryGetValue(potential, out List<ItemLocKey> sources))
                    {
                        foreach (ItemLocKey source in sources)
                        {
                            Console.WriteLine($"- {source}");
                        }
                    }
                }
            }
            if (data.GetItemLoc(potential).Keys.All(k => existingSources < k.MaxSlots))
            {
                return true;
            }
            return false;
        }
#endif

    }
}
