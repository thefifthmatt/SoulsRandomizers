using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Tokens;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class KeyItemsPermutation
    {
        private LocationData data;
        private AnnotationData ann;
        private bool explain;

        // Initial state
        private SortedSet<string> areas = new();
        // Mapping from areas to equivalent other areas. TODO still look into if these should be different
        private Dictionary<string, HashSet<string>> combinedWeights = new();
        // Mapping from areas to other areas in the same silo
        private Dictionary<string, List<string>> areaSilos = new();
        private HashSet<string> unusedAreas = new();
        private SortedSet<string> items = new();
        // Built up over item assignment
        private SortedDictionary<string, Node> nodes = new();
        private Dictionary<string, HashSet<string>> loops = new();
        private Dictionary<string, HashSet<string>> itemEvents = new();

        public KeyItemsPermutation(RandomizerOptions opt, LocationData data, AnnotationData ann, bool explain)
        {
            this.data = data;
            this.ann = ann;
            this.explain = explain;

            Dictionary<string, Expr> configExprs = new(ann.DefaultConfigExprs);
            foreach ((string name, bool value) in ann.GetConfig(opt.GetLogicOptions()))
            {
                configExprs[name] = value ? Expr.True : Expr.False;
            }
            if (explain) Console.WriteLine($"Using config {string.Join(", ", configExprs)}");

            Dictionary<LocationScope, (UniqueCategory, int)> counts = ann.GetUniqueCounts();
            Dictionary<string, int> nonShopKeyCounts = new();
            Dictionary<string, Dictionary<UniqueCategory, int>> areaCounts = ann.AllAreas.ToDictionary(e => e.Key, e =>
            {
                Dictionary<UniqueCategory, int> dict = Node.EmptyCounts();
                int nonShopKeyCount = 0;
                foreach (LocationScope scope in e.Value.Where(s => counts.ContainsKey(s)))
                {
                    (UniqueCategory cat, int count) = counts[scope];
                    dict[cat] += count;
                    if ((cat == UniqueCategory.Key || cat == UniqueCategory.KeyQuest) && !scope.OnlyShops)
                    {
                        nonShopKeyCount += count;
                    }
                }
                nonShopKeyCounts[e.Key] = nonShopKeyCount;
                return dict;
            });

            // Add config exprs for all shards, as the item name by itself is no longer a valid reference (messmershard is replaced with messmershard1 etc)
            foreach (MultiItem multiItem in ann.MultiItems.Values)
            {
                if (multiItem.IsShard)
                {
                    string name = multiItem.BaseName;
                    if (ann.Items.ContainsKey(name))
                    {
                        throw new Exception($"Internal error: Could not edit logic for {name} shards as the item has conflicting configuration");
                    }
                    // In the case of shards, put all of them behind gates, regardless of the number required
                    configExprs.Add(name, new Expr(multiItem.Names.Select(Expr.Named).ToList(), true));
                }
            }
            if (ann.Events.ContainsKey("ashen"))
            {
                // Elden Ring has dynamic conditions which are hard to genericize logic for, so fill those in here.
                // runes_leyndell, runes_rold, runes_rold_event can be replaced. runes_end is also dynamic, but does not affect logic.
                List<string> runes;
                List<string> vanillaRunes = ann.Items.Keys.Where(i => i.StartsWith("rune")).ToList();
                if (opt["norandom"] || vanillaRunes.Any(r => ann.NorandomItems.Contains(ann.Items[r])))
                {
                    // Use vanilla ordering or else we may have late-game Great Runes being required to enter Leyndell
                    runes = vanillaRunes;
                }
                else
                {
                    runes = vanillaRunes.OrderBy(i => i).ToList();
                    Shuffle(new Random((int)opt.Seed), runes);
                }
                if (explain) Console.WriteLine($"Order of required Great Runes: {string.Join(", ", runes)}");
                int leyndellRunes = 2;
                if (opt.GetInt(RandomizerOptions.IntOpt.RunesLeyndell, out int runeOpt))
                {
                    leyndellRunes = runeOpt;
                }
                int roldRunes = -1;
                if (opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out runeOpt))
                {
                    roldRunes = runeOpt;
                }
                // Logic-only "free rune". This might be sensible to do given high bias and rune count.
                if (opt["runebuffer"])
                {
                    leyndellRunes = Math.Min(leyndellRunes + 1, 7);
                    if (roldRunes >= 0)
                    {
                        roldRunes = Math.Min(roldRunes + 1, 7);
                    }
                }

                // If leyndellRunes is 0, this condition just becomes true
                configExprs["runes_leyndell"] = new Expr(runes.Take(leyndellRunes).Select(r => Expr.Named(r)).ToList(), true);
                if (roldRunes == -1)
                {
                    // Default, just require Rold (or replacement)
                    if (!configExprs.TryGetValue("roldmedallion", out Expr roldReq))
                    {
                        roldReq = Expr.Named("roldmedallion");
                    }
                    configExprs["runes_rold"] = roldReq;
                    configExprs["runes_rold_event"] = roldReq;
                }
                else
                {
                    // Do not reference Rold. It won't be placed here (and it is removed from the item pool anyway).
                    configExprs["runes_rold"] = new Expr(runes.Take(roldRunes).Select(r => Expr.Named(r)).ToList(), true);
                    // This is used for Until logic and redundant to mountaintops access condition, so blank it out if not a single item anymore.
                    // Unlike other multi-item requirements, the listed Great Runes may not be strictly required for Rold (unlisted GRs could be used instead)
                    configExprs["runes_rold_event"] = Expr.True;
                }
                // Pre-Rold gate added for Randomania balancing
                if (opt["randomania"] && !opt["fog"] && roldRunes >= 1)
                {
                    configExprs["runes_randomania"] = new Expr(runes.Select(r => Expr.Named(r)).ToList(), true);
                }
                else
                {
                    configExprs["runes_randomania"] = Expr.True;
                }
                if (opt["fog"] && ann.ItemGroups.TryGetValue("nofogaccess", out List<ItemKey> fogItems))
                {
                    // For fog gate randomizer (currently Elden Ring only), override the above.
                    // Also ignore keys unless the locked area is not accessible by gates/warps.
                    List<string> trueConds = new List<string> { "runes_leyndell", "runes_rold", "runes_rold_event" };
                    foreach ((string name, ItemKey item) in ann.Items)
                    {
                        if (!fogItems.Contains(item) && !runes.Contains(name))
                        {
                            trueConds.Add(name);
                        }
                    }
                    foreach (string name in trueConds)
                    {
                        // Overrides above configuration
                        configExprs[name] = Expr.True;
                    }
                }
                // Switch for Imbued Sword Keys to avoid cross-dependencies, mainly for weight purposes.
                // Split dependencies may softlock in rare base->DLC->base cases?
                if (ann.KeyItemSilo == AreaSiloType.None && opt["dlc"])
                {
                    List<string> keys = Enumerable.Range(1, 4).Select(i => $"imbuedswordkey{i}").ToList();
                    configExprs["imbued_base"] = new Expr(keys.Select(Expr.Named).ToList(), true);
                    configExprs["imbued_dlc"] = new Expr(keys.Select(Expr.Named).ToList(), true);
                }
            }
            if (explain) Console.WriteLine($"Using special config {string.Join(", ", configExprs)}");

            Dictionary<string, List<string>> combinedAreas = new();
            Dictionary<string, string> equivalentGraph = new Dictionary<string, string>();
            void processDependencies(AreaAnnotation area, ISet<string> frees, bool assignItems)
            {
                string name = area.Name;
                HashSet<string> dependentAreas = new HashSet<string>();
                bool other = false;
                foreach (string free in frees)
                {
                    if (ann.Items.ContainsKey(free))
                    {
                        items.Add(free);
                        if (assignItems)
                        {
                            if (itemEvents.ContainsKey(free)) throw new Exception($"Internal error: {free} activates multiple events");
                            AddMulti(itemEvents, free, name);
                            if (area.AlwaysBefore != null)
                            {
                                // Another placement for key items, so e.g. Mortal Blade can be required in area with until=invasion2
                                AddMulti(itemEvents, free, area.AlwaysBefore);
                            }
                        }
                        other = true;
                    }
                    else if (ann.Areas.ContainsKey(free))
                    {
                        dependentAreas.Add(free);
                    }
                    else if (ann.Events.ContainsKey(free))
                    {
                        if (ann.EventAreas.TryGetValue(free, out string evArea))
                        {
                            dependentAreas.Add(evArea);
                        }
                        else
                        {
                            other = true;
                        }
                    }
                    else throw new Exception($"Internal error: Unknown dependency {free} in requirements for {area.Name}");
                }
                if (dependentAreas.Count == 1 && !other)
                {
                    equivalentGraph[name] = dependentAreas.First();
                    if (explain) Console.WriteLine($"Collapsed events for key item generation: {name} -> {frees.First()} (all: {string.Join(" ", frees)})");
                }
                // This is used for equivalence graph things. Should probably use this information in weight groups instead of actually combining the areas
                AddMulti(combinedAreas, name, name);
                // Weight base is used to specify that a key item, if placed in the base area, should also apply to this other area.
                AddMulti(combinedWeights, name, name);
                if (area.WeightBase != null)
                {
                    AddMulti(combinedWeights, area.WeightBase, name);
                }
            }
            foreach (AreaAnnotation ev in ann.Events.Values)
            {
                string name = ev.Name;
                Expr req = ev.ReqExpr.Substitute(configExprs).Simplify();
                if (req.IsFalse())
                {
                    // Can happen with DLC
                    unusedAreas.Add(ev.Name);
                    continue;
                }
                // Console.WriteLine($"-- event {name}: {ev.ReqExpr} -> {req}");
                processDependencies(ev, req.FreeVars(), true);
                // Events are not dynamically placed anywhere, nor is anything placed inside of them, so they are always added to the graph upfront
                nodes[name] = new Node
                {
                    Name = name,
                    Req = req,
                    Counts = Node.EmptyCounts(),
                    CumKeyCount = -1,
                };
            }
            Dictionary<string, AreaSilo> silos = new();
            foreach (AreaAnnotation area in ann.Areas.Values)
            {
                string name = area.Name;
                Expr req = area.ReqExpr.Substitute(configExprs).Simplify();
                if (req.IsFalse())
                {
                    // Can happen with DLC
                    unusedAreas.Add(area.Name);
                    continue;
                }
                // Console.WriteLine($"-- area {name}: {area.ReqExpr} -> {req}");
                processDependencies(area, req.FreeVars(), false);
                // This is where we used to skip combined areas in DS3, but now weight bases are added automatically
                nodes[name] = new Node
                {
                    Name = name,
                    Counts = areaCounts[name],
                    Req = req,
                    Weight = 1,
                    CumKeyCount = -1,
                    NonShopKeyCount = nonShopKeyCounts.TryGetValue(name, out int count) ? count : 0,
                };
                areas.Add(name);
                if (ann.KeyItemSilo == AreaSiloType.DLC)
                {
                    silos[name] = AreaSilo.ForDlc(area.HasTag("dlc"));
                }
            }
            // Console.WriteLine(string.Join(" ", equivalentGraph));
            // Adjustment to prevent so many GRs from appearing in Volcano Manor with limited item placements
            if (opt["randomania"])
            {
#if DEBUG
                if (!nodes.ContainsKey("volcano_town") || !nodes.ContainsKey("volcano") || equivalentGraph.ContainsKey("volcano_town"))
                {
                    throw new Exception("Assumptions for randomania Volcano Manor logic broken");
                }
#endif
                // TODO: This can result in issues with items UpTo: farumazula After: farumazula_start UseGroups: true, e.g. bias:25 seed:1878359958
                // It may be necessary to split up weights after all
                equivalentGraph["volcano_town"] = "volcano";
            }
            // Quick collapse of equivalence graph
            Dictionary<string, string> equivalent = new Dictionary<string, string>();
            string getBaseName(string name)
            {
                if (equivalent.ContainsKey(name))
                {
                    return equivalent[name];
                }
                else if (equivalentGraph.ContainsKey(name))
                {
                    string root = getBaseName(equivalentGraph[name]);
                    equivalent[name] = root;
                    AddMulti(combinedAreas, root, name);
                    return root;
                }
                else
                {
                    return name;
                }
            };
            foreach (KeyValuePair<string, string> equivalence in equivalentGraph)
            {
                getBaseName(equivalence.Key);
            }
            // TODO: Can equivalent be done on combinedWeights directly, instead of adding a relation on combinedAreas and a disjoint set union step after?
            foreach (KeyValuePair<string, List<string>> entry in combinedAreas)
            {
                foreach (string alias in entry.Value)
                {
                    if (alias != entry.Key)
                    {
                        AddMulti(combinedWeights, entry.Key, alias);
                    }
                }
            }
            foreach (KeyValuePair<string, HashSet<string>> entry in combinedWeights.Where(e => e.Value.Count > 1).ToList())
            {
                foreach (string sharedArea in entry.Value.ToList())
                {
                    entry.Value.UnionWith(combinedWeights[sharedArea]);
                    combinedWeights[sharedArea] = entry.Value;
                }
            }
            if (explain)
            {
                HashSet<string> explained = new HashSet<string>();
                foreach (KeyValuePair<string, HashSet<string>> entry in combinedWeights)
                {
                    if (explained.Contains(entry.Key)) continue;
                    Console.WriteLine($"Combined weights: [{string.Join(",", entry.Value)}]");
                    explained.UnionWith(entry.Value);
                }
            }

            // I don't think we need combinedAreas anymore? Only combinedWeights
            // Maybe this is needed in cases where there is additional weight combination where areas aren't mutually reachable (see comment below).
            // TODO audit how both are used.
            combinedAreas = combinedWeights.ToDictionary(e => e.Key, e => e.Value.ToList());

            // Last step - calculate rough measures of area difficulty, in terms of minimal number of items required for the area
            // In Elden Ring, this step encounters recursion with leyndell -> sewer -> sewer_flame -> deeproot -> leyndell
            HashSet<string> counting = new HashSet<string>();
            int getCumulativeCounts(string name)
            {
                if (unusedAreas.Contains(name)) return 0;
                Node node = nodes[name];
                if (node.CumKeyCount != -1 || counting.Contains(name))
                {
                    return node.KeyCount + node.CumKeyCount;
                }
                counting.Add(name);
                List<string> deps = node.Req.FreeVars().Where(free => areas.Contains(free) || ann.Events.ContainsKey(free)).ToList();
                int count = deps.Select(free => getCumulativeCounts(free)).DefaultIfEmpty().Max();
                node.CumKeyCount = count;
                return node.KeyCount + count;
            };
            foreach (Node node in nodes.Values)
            {
                getCumulativeCounts(node.Name);
                if (explain) Console.WriteLine($"{node.Name} ({node.KeyCount} key / {node.Counts[UniqueCategory.Quest]} quest-only / {node.CumKeyCount} cumulative): {node.Req}");
            }

            // Calculate silos
            if (silos.Count > 0)
            {
                List<List<string>> siloGroups = silos.GroupBy(e => e.Value).Select(g => g.Select(e => e.Key).ToList()).ToList();
                foreach (List<string> group in siloGroups)
                {
                    foreach (string area in group)
                    {
                        areaSilos[area] = group;
                    }
                }
            }
        }

        public class Assignment
        {
            // All key items, in decreasing order of priority. Doesn't exactly match assign order because of MultiItems, can switch to string if needed
            public List<ItemKey> Priority = new List<ItemKey>();
            // Required events to access other areas, to reduce their priority at higher biases
            public HashSet<string> RequiredEvents = new HashSet<string>();
            // For a given item name, which areas it can appear in. Check MultiItems when mapping to ItemKey
            public Dictionary<string, HashSet<string>> Assign = new Dictionary<string, HashSet<string>>();
            // Specific list of locations for quest items
            public Dictionary<ItemKey, List<LocationScope>> RestrictedItems = new Dictionary<ItemKey, List<LocationScope>>();
            // When an item effectively becomes available, especially if it has quest dependencies
            public Dictionary<LocationScope, string> EffectiveLocation = new Dictionary<LocationScope, string>();
            // The lateness of areas from 0 to 1, based on how many items are accessible by that point
            public Dictionary<string, double> LocationLateness = new Dictionary<string, double>();
            // All areas which are required to access the given area.
            public Dictionary<string, HashSet<string>> IncludedAreas = new Dictionary<string, HashSet<string>>();
            // Same, but for items.
            public Dictionary<string, HashSet<string>> IncludedItems = new Dictionary<string, HashSet<string>>();
            // Areas which can be considered equivalent to each other for heuristic placement purposes (used here for weight management)
            // These are not all mutually accessible, especially if WeightBase is used, but they should be most of the time.
            public Dictionary<string, HashSet<string>> CombinedWeights = new Dictionary<string, HashSet<string>>();
            // Key items which are not required for the ending. Used for in-game hints.
            // TODO: Can replace with IncludedItems
            public HashSet<ItemKey> NotRequiredKeyItems = new HashSet<ItemKey>();
        }

        public Assignment AssignItems(Random random, RandomizerOptions opt, EnemyPreset preset)
        {
            List<string> itemOrder = new List<string>(items);

            // Right now, assign key items in a random order, with endgame items last to produce more interesting runs and increase odds of not breaking.
            Shuffle(random, itemOrder);
            bool isEndgame(string i) => i.StartsWith("cinder") || i == "secretpassagekey" || i.StartsWith("rune") || i.StartsWith("messmer");
            itemOrder = itemOrder.OrderBy(i => isEndgame(i) ? 1 : 0).ToList();

            // In race mode, always put shinobiprosthetic and younglordsbellcharm first, since there is only one ashinaoutskirts_template spot for key item placement logic.
            // This still fails like 20% of the time with veryearlyhirata.
            if (ann.RaceModeItems.Count > 0)
            {
                itemOrder = itemOrder.OrderBy(i => i == "shinobiprosthetic" || i == "younglordsbellcharm" ? 0 : 1).ToList();
            }

            // Pass for classifying key item locations
            Dictionary<string, string> vanillaAreas = new();
            void setVanillaArea(string item)
            {
                ItemKey itemKey = ann.Items[item];
                // Filter ItemLocation.ItemName for in-game items like Imbued Sword Keys, but ignore it for locationless ones like Messmer's Kindling Shard
                bool filterItemName = ann.MultiItems.ContainsKey(itemKey) && !(ann.ItemGroups.TryGetValue("multikey", out List<ItemKey> multiKeys) && multiKeys.Contains(itemKey));
                // Check multiple item locations, since some norandom unused slots may be configured
                string keyArea = null;
                if (!data.Items.TryGetValue(itemKey, out ItemLocations itemLocs))
                {
                    throw new Exception($"Error randomizing {item}: vanilla location was not found in game data");
                }
                foreach (ItemLocation loc in itemLocs.Locations.Values)
                {
                    if (filterItemName && loc.ItemName != item) continue;
                    if (ann.Slots.TryGetValue(loc.LocScope, out SlotAnnotation sn) && ann.Areas.ContainsKey(sn.Area))
                    {
                        keyArea = sn.Area;
                    }
                }
                if (keyArea == null) throw new Exception($"Error randomizing {item}: vanilla location was not found in game data");
                vanillaAreas[item] = keyArea;
                // Console.WriteLine($"- Name: {item}\n  ID: {(int)itemKey.Type}:{itemKey.ID}\n  Area: {keyArea}");
            }

            foreach (string item in itemOrder)
            {
                // Some hardcoding for Path of the Dragon which is not an actual item
                if (item == "pathofthedragon")
                {
                    vanillaAreas[item] = "highwall_garden";
                }
                else if (item == "omother")
                {
                    vanillaAreas[item] = "scadualtus";
                }
                else
                {
                    setVanillaArea(item);
                }
            }

            Dictionary<string, string> forcemap = new Dictionary<string, string>();
            foreach (string item in itemOrder)
            {
                ItemKey itemKey = ann.Items[item];
                if (ann.NorandomItems.Contains(itemKey))
                {
                    if (opt["latedragonbarrow"] && item == "dectusmedallionright")
                    {
                        // Special case to trick logic into allowing access to Dectus before Altus (the item is still be placed in Dragonbarrow).
                        // This may be forbidden altogether in AnnotationData validation for now
                        forcemap[item] = "caelid";
                        continue;
                    }
                    forcemap[item] = vanillaAreas[item];
                }
            }

            Assignment ret = new Assignment();
            // Find which events are required to access other things, and do not give them as much weight as dead ends with placement biases
            foreach (Node node in nodes.Values)
            {
                if (node.Req != null)
                {
                    ret.RequiredEvents.UnionWith(node.Req.FreeVars().Where(v => ann.Events.ContainsKey(v)));
                }
            }
            // First assign ashes for key item placement. Other quest assignments can happen later.
            // TODO: Make ashes its own system, rather than abusing quest system.
            List<ItemKey> ashesKeys = new();
            if (ann.ItemGroups.ContainsKey("ashes"))
            {
                // Prioritize locations with fewer non-shop items, I guess?
                // This used to be: Math.Min(nodes[loc].KeyCount - nodes[loc].Counts[UniqueCategory.KeyShop], 3)
                // At least avoid placing items in areas without any slots whatsoever
                foreach ((string item, string area) in forcemap)
                {
                    // To do this, first need to take norandom into account, as otherwise e.g. Iudex Gundyr may appear to be available
                    // Also done with awkward heuristics
                    nodes[area].NonShopKeyCount--;
                }
                List<string> ashesOrder = new List<string>(areas.Where(a => nodes[a].NonShopKeyCount > 0));
                ashesOrder = WeightedShuffle(random, ashesOrder, loc => Math.Min(nodes[loc].NonShopKeyCount, 3));
                if (opt["explainashes"])
                {
                    foreach (string ash in ashesOrder)
                    {
                        Node node = nodes[ash];
                        Console.WriteLine($"{ash}: {node.KeyCount} keys - nonshop {node.NonShopKeyCount}");
                    }
                }
                int ashesIndex = 0;
                foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
                {
                    LocationScope scope = entry.Key;
                    SlotAnnotation slot = entry.Value;
                    HashSet<string> tags = slot.GetTags();
                    // Unique item unlocks other unique items unconditionally - can add a location for key item. Mainly for ashes.
                    // TODO: Actually check this applies only to ashes
                    if (slot.QuestReqs != null && !slot.HasAnyTags(ann.NoKeyTags) && scope.UniqueID > 0 && slot.ItemReqs.Count == 1)
                    {
                        string item = slot.ItemReqs[0];
                        if (ret.Assign.ContainsKey(item)) throw new Exception($"Multiple assignments of {item} for {slot.QuestReqs}");
                        string selected = ashesOrder[ashesIndex++];
                        // TODO: Is this even accurate for shops?
                        int ashesCount = data.GetBaseItemLocs(scope).Count;
                        if (explain) Console.WriteLine($"Assigning key quest item {slot.QuestReqs} to {selected} ({ashesCount}x)");
                        nodes[selected].AddKeyShopCapacity(Math.Max(0, ashesCount - 1));
                        ret.Assign[item] = new HashSet<string> { selected };
                        if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[item].UnionWith(events);
                        ItemKey itemKey = ann.Items[item];
                        if (ann.RaceModeItems.Contains(itemKey))
                        {
                            // Assign these before Estus Shards etc, but after key items
                            // To get rid of the KeyCount heuristic above, this should probably be tighter by having even higher priority
                            ashesKeys.Add(itemKey);
                        }
                    }
                }
            }
            ret.Priority.AddRange(ashesKeys);
            if (explain)
            {
                int raceModeCount = 0;
                foreach (string area in areas)
                {
                    int rmode = nodes[area].KeyCount;
                    if (rmode > 0)
                    {
                        Console.WriteLine($"RACEMODE {area}: {rmode}");
                        raceModeCount += rmode;
                    }
                }
                Console.WriteLine($"TOTAL: {raceModeCount}");
            }

            // Assign key items
            bool zeroWeights = false;
            bool debugChoices = false;
            float scaling = ann.Weights.KeyItemChain;
            float spreadScaling = ann.Weights.KeyItemRegionSpread;
            Dictionary<string, Expr> reqs = CollapseReqs();
            foreach (string item in itemOrder)
            {
                ItemKey itemKey = ann.Items[item];

                List<string> allowedAreas = areas.Where(a => !reqs[a].Needs(item)).ToList();
                if (debugChoices) Console.WriteLine($"\n> {item} not allowed in areas: {string.Join(",", areas.Where(a => !allowedAreas.Contains(a)))}");
                bool redundant = allowedAreas.Count == areas.Count;
                HashSet<string> neededForEvent = itemEvents.TryGetValue(item, out HashSet<string> ev) ? ev : null;
                allowedAreas.RemoveAll(a => ann.Areas[a].Until != null && (neededForEvent == null || !neededForEvent.Contains(ann.Areas[a].Until)));

                Dictionary<string, float> specialScaling = new Dictionary<string, float>();
                // A bit hacky, but prevent Small Lothric Banner from being so prevalent in Firelink Shrine
                if (item == "smalllothricbanner")
                {
                    specialScaling = allowedAreas.Where(area => ann.Areas[area].BoringKeyItem).ToDictionary(area => area, _ => 1 / scaling);
                }
                HashSet<string> preferredAreas = new(allowedAreas);
                // Prevent Dectus in Precipice also hackily. We don't want to do this in logic directly because of precipice earliness
                if (item == "dectusmedallionleft" || item == "dectusmedallionright")
                {
                    preferredAreas.Remove("precipice");
                }
                if (ann.ItemRestrict != null && ann.ItemRestrict.TryGetValue(itemKey, out PlacementRestrictionAnnotation restrict) && restrict.KeyAreas != null)
                {
                    string[] restrictAreas = restrict.KeyAreas.Split(' ');
                    preferredAreas.IntersectWith(restrictAreas);
                    // specialScaling = allowedAreas.Where(area => !restrictAreas.Contains(area)).ToDictionary(area => area, _ => 0f);
                    // if (specialScaling.Count == allowedAreas.Count) throw new Exception($"Can't place extra-restricted item {item}");
                    if (debugChoices) Console.WriteLine($">{item} restricted to {string.Join(",", restrictAreas)}");
                }
                if (areaSilos.TryGetValue(vanillaAreas[item], out List<string> siloAreas))
                {
                    preferredAreas.IntersectWith(siloAreas);
                }
                // Special check to prevent error message with WeightedChoice
                if (preferredAreas.Count == 0)
                {
                    throw new Exception($"Nowhere to place item with extra restrictions {item}");
                }
                Dictionary<string, float> weights = allowedAreas.ToDictionary(
                    area => area,
                    area => Weight(area) * (!preferredAreas.Contains(area) ? 0 : (specialScaling.TryGetValue(area, out float sp) ? sp : 1)));

                if (debugChoices)
                {
                    Console.WriteLine($"> Choices for {item}: " + string.Join(", ", allowedAreas
                        .OrderBy(a => weights[a])
                        .Where(a => zeroWeights || weights[a] > 0)
                        .Select(a => $"{a} {weights[a]}")));
                }
                string selected = WeightedChoice(random, allowedAreas, a => weights[a]);

                if (forcemap.TryGetValue(item, out string forced))
                {
                    if (explain && !allowedAreas.Contains(forced)) Console.WriteLine($"Key item {item} put in non-random location {forced} which isn't normally allowed by logic");
                    selected = forced;
                }
                AddItem(item, selected, forced != null);

                ret.Priority.Add(itemKey);
                ret.Assign[item] = new HashSet<string> { selected };
                // Areas should include events there. Except for bell charm being dropped by chained ogre, if that option is enabled
                // todo: check this works okay with racemode key items, and nothing else randomized.
                if (!(item == "younglordsbellcharm" && opt["earlyhirata"]))
                {
                    if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[item].UnionWith(events);
                }
#if DEBUG
                if (explain || debugChoices || opt["keychoice"]) Console.WriteLine($"Adding {item} to {string.Join(",", ret.Assign[item])}");
#endif

                // Update weights
                reqs = CollapseReqs();
                // If item was not really needed, don't update weights
                if (redundant)
                {
                    if (explain) Console.WriteLine($"{item} is redundant to another key item (does not uniquely make new areas available)");
                    continue;
                }
                // Heuristic which forms chains and spreads items across areas
                // Reduce weight for this area, and increase weight for areas which depend on the item
                AdjustWeight(selected, 1 / (scaling * spreadScaling));
                HashSet<string> addedAreas = new HashSet<string>();
                foreach (string area in areas)
                {
                    if (addedAreas.Contains(combinedWeights[area].First())) continue;
                    if (reqs[area].Needs(item))
                    {
                        AdjustWeight(area, scaling);
                        addedAreas.Add(combinedWeights[area].First());
                    }
                }
            }
            // The last placed item has the highest priority
            ret.Priority.Reverse();
            // Deal with MultiItems this way. Can also make Priority string-keyed, but for most usages (detecting key items) it doesn't matter.
            ret.Priority = ret.Priority.Distinct().ToList();
            // Academy Glintstone Key is not a key in crawl mode but it needs to be at Loretta with min locations and earlylegacy
            if (opt["crawl"] && opt["earlylegacy"] && !ret.Priority.Contains(new ItemKey(ItemType.Goods, 8109)))
            {
                ret.Priority.Add(new ItemKey(ItemType.Goods, 8109));
            }

            // Now that all key items have been assigned, determine which areas are blocked by other areas.
            // This is used to determine lateness within the game (by # of items encountered up to that point).
            HashSet<string> getIncludedAreas(string name, List<string> path)
            {
                if (unusedAreas.Contains(name)) return new();
                path = path.Concat(new[] { name }).ToList();
                if (!nodes.TryGetValue(name, out Node node)) throw new Exception($"Bad options: no way to access area \"{name}\"");
                if (ret.IncludedAreas.ContainsKey(name))
                {
                    if (ret.IncludedAreas[name] == null)
                    {
                        throw new Exception($"Loop from {name} to {node.Req} - path {string.Join(",", path)}");
                    }
                    return ret.IncludedAreas[name];
                }
                ret.IncludedAreas[name] = null;
                HashSet<string> result = new HashSet<string>();
                // Add all nodes for now, but remove items later
                if (areas.Contains(name) || ann.Events.ContainsKey(name) || ann.Items.ContainsKey(name))
                {
                    result.Add(name);
                }
                foreach (string free in node.Req.FreeVars())
                {
                    if (!(loops.ContainsKey(name) && loops[name].Contains(free)))
                    {
                        result.UnionWith(getIncludedAreas(free, path));
                    }
                }
                ret.IncludedAreas[name] = result;
                return result;
            };
            foreach (Node node in nodes.Values)
            {
                getIncludedAreas(node.Name, new List<string>());
                // Recalculate weights for quest selection and for log order
                node.Weight = 1;
                if (areas.Contains(node.Name))
                {
                    node.CumKeyCount = ret.IncludedAreas[node.Name].Where(n => nodes[n].Counts != null).Select(n => nodes[n].KeyCount).Sum();
                    node.CumCount = ret.IncludedAreas[node.Name].Where(n => nodes[n].Counts != null).Select(n => nodes[n].AllCount).Sum();
                    if (explain) Console.WriteLine($"Included for {node.Name}: {node.KeyCount}/{node.CumKeyCount}, {node.AllCount}/{node.CumCount}: {string.Join(",", ret.IncludedAreas[node.Name])}");
                }
            }

            // Find out which items are required to access the ending, while items are still included in the graph.
            // For hints later.
            // Also do this in DS3 after deciding where to add hints.
            if (ret.IncludedAreas.TryGetValue("ashinareservoir_end", out HashSet<string> requiredForEnd))
            {
                List<ItemKey> requiredItems = requiredForEnd.Where(t => ann.Items.ContainsKey(t)).Select(t => ann.Items[t]).ToList();
                ret.NotRequiredKeyItems.UnionWith(ret.Priority.Except(requiredItems));
            }
            // TODO: See if this can/should be cut down
            ret.IncludedItems = ret.IncludedAreas.ToDictionary(e => e.Key, e => new HashSet<string>(e.Value));

            // The above DFS adds both items and areas together, so remove the items (move them to a separate dictionary).
            foreach (string key in ret.IncludedAreas.Keys.ToList())
            {
                if (ann.Items.ContainsKey(key))
                {
                    ret.IncludedAreas.Remove(key);
                }
                else
                {
                    ret.IncludedAreas[key].RemoveWhere(v => ann.Items.ContainsKey(v));
                }
            }
            foreach (string area in unusedAreas)
            {
                ret.IncludedAreas[area] = new HashSet<string>();
            }

            // Make a rough area order
            List<string> areaOrder = areas.OrderBy(a => nodes[a].CumKeyCount).ToList();
            Dictionary<string, int> areaIndex = Enumerable.Range(0, areaOrder.Count()).ToDictionary(i => areaOrder[i], i => i);
            string latestArea(IEnumerable<string> ns)
            {
                return areaOrder[ns.Select(n => areaIndex.TryGetValue(n, out int i) ? i : throw new Exception($"No order for area {n}")).DefaultIfEmpty().Max()];
            }

            if (explain)
            {
                foreach (var entry in ret.IncludedAreas) Console.WriteLine($"Area scope {entry.Key}: {string.Join(" ", entry.Value)}");
            }

            // Allow Permutation to add restrictions for key items excluded from above logic by fog rando.
            // This is probably the simplest way to inform Permutation of restrictions.
            // These could alternatively be explicitly treated as unrestricted (except silo) key items
            if (opt["fog"] && ann.KeyItemSilo != AreaSiloType.None)
            {
                foreach ((string name, ItemKey item) in ann.Items)
                {
                    if (ret.Assign.ContainsKey(name) || ann.MultiItems.ContainsKey(item))
                    {
                        continue;
                    }
                    if (ann.ItemGroups["keyitems"].Contains(item))
                    {
                        setVanillaArea(name);
                        if (areaSilos.TryGetValue(vanillaAreas[name], out List<string> siloAreas))
                        {
                            ret.Assign[name] = new(siloAreas);
                        }
                    }
                }
            }

            // Quest time
            // The main difficult part is determining the effective area of locations. That requires assigning quest items to a location.
            SortedSet<string> questItems = new SortedSet<string>();
            // Dictionary for quest item -> area requiring quest item -> # of slots requiring quest item in that area. Used to customize eligible slots for placing the quest item.
            Dictionary<string, Dictionary<string, int>> questItemAreaSlots = new Dictionary<string, Dictionary<string, int>>();
            // TODO: Check that this works with ashes chaining - slot can't have ashes which transitively requires that slot.
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                LocationScope scope = entry.Key;
                SlotAnnotation slot = entry.Value;
                string area = slot.GetArea();
                if (unusedAreas.Contains(area))
                {
                    continue;
                }
                HashSet<string> tags = slot.GetTags();
                if (slot.QuestReqs != null)
                {
                    // Special check for base game locations which only exist in DLC, mainly Roundtable Hold shops with DLC dependencies.
                    // Otherwise, it can get added as a quest item and setVanillaArea will fail.
                    // This may need a general exclusion mechanism to work across games.
                    if (tags.Contains("dlc") && !opt["dlc"])
                    {
                        continue;
                    }
                    foreach (string questReq in slot.ItemReqs)
                    {
                        // Note: This doesn't work for multi-item, like holeladennecklace
                        if (ann.Items.TryGetValue(questReq, out ItemKey itemKey) && !ann.ItemGroups["remove"].Contains(itemKey))
                        {
                            AddMulti(ret.RestrictedItems, itemKey, scope);
                            if (!questItemAreaSlots.TryGetValue(questReq, out Dictionary<string, int> reqSlots))
                            {
                                reqSlots = questItemAreaSlots[questReq] = new();
                            }
                            reqSlots[area] = reqSlots.ContainsKey(area) ? reqSlots[area] + 1 : 1;
                            questItems.Add(questReq);
                        }
                    }
                }
                if (slot.TagItems != null && slot.TagItems.TryGetValue("exclude", out List<ItemKey> tagItems))
                {
                    foreach (ItemKey tagItem in tagItems)
                    {
                        // Console.WriteLine($"Adding exclude {tagItem} to {slot.Text}");
                        AddMulti(ret.RestrictedItems, tagItem, scope);
                    }
                }
            }
            // Add vanilla mapping here, same as key items
            foreach (string questItem in questItems)
            {
                if (ret.Assign.ContainsKey(questItem)) continue;
                setVanillaArea(questItem);
            }
            // Assign quest items to areas.
            foreach (string questItem in questItems)
            {
                ItemKey itemKey = ann.Items[questItem];
                if (ret.Assign.ContainsKey(questItem))
                {
                    if (explain) Console.WriteLine($"{questItem} already assigned to {string.Join(", ", ret.Assign[questItem])}");
                    continue;
                }
                Dictionary<string, int> questAreas = questItemAreaSlots[questItem];
                // TODO: Does this contain items? If so, filter those out
                List<string> allowedAreas = ann.ItemRestrict.ContainsKey(itemKey)
                    ? areas.Intersect(ann.ItemRestrict[itemKey].Unique[0].AllowedAreas(ret.IncludedAreas, combinedWeights)).ToList()
                    : areas.ToList();
                if (areaSilos.TryGetValue(vanillaAreas[questItem], out List<string> siloAreas))
                {
                    allowedAreas = allowedAreas.Intersect(siloAreas).ToList();
                }
                bool itemAllowMissable = ann.ExcludeTags.TryGetValue(itemKey, out HashSet<string> excludeTags) ? !excludeTags.Contains("missable") : true;
                bool allowQuest = itemAllowMissable || ann.NoQuestTags.Contains("missable");
                string selected = WeightedChoice(random, allowedAreas, a => Weight(a, allowQuest, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0));
                if (explain) Console.WriteLine($"Selecting {questItem} to go in {selected} ({string.Join(" ", nodes[selected].Counts)})");
                // if (selected == "volcano" || true) Console.WriteLine($"  can miss {allowMissable}, areas {string.Join(" ", questAreas)}. weights {string.Join(", ", allowedAreas.Select(a => $"{a}={Weight(a, allowMissable, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0)}"))}");
                // if (selected == "volcano" || true) Console.WriteLine($"  areas {string.Join(" ", questAreas)}. weights {string.Join(", ", allowedAreas.Where(a => nodes[a].Count(true, true) >= 0).Select(a => $"{a}={nodes[a].Count(true, true)}"))}");
                // if (selected == "volcano" || true) Console.WriteLine($"  can miss {allowMissable}, areas {string.Join(" ", questAreas)}. weights sum >>{allowedAreas.Sum(a => Weight(a, allowMissable, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0))}");
                nodes[selected].AddItem(questItem, allowQuest);
                ret.Assign[questItem] = new HashSet<string> { selected };
                if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[questItem].UnionWith(events);
            }
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                LocationScope scope = entry.Key;
                SlotAnnotation slot = entry.Value;
                HashSet<string> tags = slot.GetTags();
                if (slot.QuestReqs != null)
                {
                    // TODO: Worst case situation: There is a key item. There is an area, with slots, it gets assigned to. Each has a questreq pointing to another area.
                    // For key items, quest locations are not counted, except for upfront, where capacity is explicitly added.
                    // If it is a quest item instead, perhaps don't allow questreq locations to have quest items. They are just unstable. Stop abusing quests to do ashes.
                    string area = slot.GetArea();
                    if (unusedAreas.Contains(area))
                    {
                        continue;
                    }
                    // Set effective area if different from actual area. This is just used for placement heuristics
                    if (explain) Console.WriteLine($"For questreqs {slot.QuestReqs} - items {string.Join(",", slot.ItemReqs.SelectMany(item => ret.Assign[item]))}");
                    List<string> areaReqs = new(slot.AreaReqs);
                    areaReqs.AddRange(slot.ItemReqs
                        .SelectMany(item => ret.Assign.TryGetValue(item, out HashSet<string> assignAreas) ? assignAreas.Where(a => areas.Contains(a)) : Array.Empty<string>()));
                    // Avoid putting an effective area if no areas detected at all, as it overrides area. This happens with Furnace Golems and allcraft
                    if (areaReqs.Count == 0) continue;
                    string effectiveArea = latestArea(areaReqs);
                    if (area != effectiveArea) ret.EffectiveLocation[scope] = effectiveArea;
                }
            }
            int combinedTotal = areas.Select(a => nodes[a].CumKeyCount).Max() + 1;
            // Because CumKeyCount is too coarse-grained for Elden Ring where so many areas are combined, further break it down using CumCount
            // We may also want to use config file order for a further tie-breaker
            SortedDictionary<int, List<string>> similarWeightAreas = new();
            foreach ((string area, HashSet<string> same) in combinedWeights)
            {
                ret.CombinedWeights[area] = same;
                AddMulti(similarWeightAreas, nodes[area].CumKeyCount, area);
            }
            foreach ((int keyWeight, List<string> areas) in similarWeightAreas)
            {
                List<string> sortAreas = areas.OrderBy(a => nodes[a].CumCount).ToList();
                for (int i = 0; i < sortAreas.Count; i++)
                {
                    string area = sortAreas[i];
                    double weight = keyWeight + (double)i / sortAreas.Count;
                    double partial = weight / combinedTotal;
                    ret.LocationLateness[area] = partial;
                    // Console.WriteLine($"Lateness for {area}: {partial}");
                }
            }
            return ret;
        }

        public float Weight(string area, bool quest = false, float lateFactor = 0.1f, int removeQuest = 0)
        {
            Node node = nodes[area];
            int count = node.Count(quest) - removeQuest;
            if (count <= 0)
            {
                return 0;
            }
            count += (int)(node.CumKeyCount * lateFactor);
            // volcano: 1 key items (44.60%), 2 key items (34.00%), 3 key items (6.60%), 4 key items (0.30%)
            // volcano: 1 key items (46.30%), 2 key items (32.70%), 3 key items (5.40%) <- 0.5
            // volcano: 1 key items (52.50%), 2 key items (26.10%), 3 key items (2.50%) <- 0.1
            return count * node.Weight;
        }

        public void AdjustWeight(string area, float factor)
        {
            if (factor == 1f) return;
            foreach (string sharedArea in combinedWeights[area])
            {
                nodes[sharedArea].Weight *= factor;
            }
        }

        public void AddItem(string item, string area, bool forced)
        {
            nodes[item] = new Node { Name = item, Req = Expr.Named(area) };
            if (forced) return;
            nodes[area].AddItem(item, quest: false);
        }

        // The core routine at the center of placing key items.
        // Given the current area layout and pending item assignment, reduce the condition for each area to be only in terms of items, not in terms of other areas.
        // Then, for a given item, it is possible to tell which areas unconditionally depend on that item. The item can then be placed anywhere else.
        private Dictionary<string, Expr> CollapseReqs()
        {
            bool debugReqs = false;
            Dictionary<string, bool> allDepsProcessed = new Dictionary<string, bool>();
            void findLoops(List<string> path, string name)
            {
                path = path.Concat(new[] { name }).ToList();
                if (debugReqs)
                {
                    Console.WriteLine($"Loop path: {string.Join(",", path)}. {name} processed: {(allDepsProcessed.ContainsKey(name) ? allDepsProcessed[name].ToString() : "unreached")}");
                }
                if (allDepsProcessed.ContainsKey(name))
                {
                    // If all deps satisfied, no issue. Otherwise...
                    if (!allDepsProcessed[name])
                    {
                        List<string> subpath = path.Skip(path.IndexOf(name)).ToList();
                        // Use a heuristic to see where we should snip the path. This doesn't work in a very small portion
                        // of cases, but most valid randomizations are interesting so just try again with a different seed.
                        if (debugReqs) Console.WriteLine($"=> Snip subpath {string.Join(",", subpath)}");
                        bool found = false;
                        foreach ((string fro, string to) in subpath.Zip(subpath.Skip(1), (a, b) => (a, b)))
                        {
                            bool revdep = nodes[fro].Req.Needs(to);
                            if (!revdep)
                            {
                                // if (!found)
                                {
                                    found = true;
                                    AddMulti(loops, fro, to);
                                }
                            }
                            if (debugReqs) Console.WriteLine($"=> {fro} needs {to}: {revdep}");
                        }
                        if (!found)
                        {
                            throw new Exception($"Unsolvable seed: hard dependency loop [{string.Join(",", subpath)}]");
                        }
                    }
                    return;
                }
                allDepsProcessed[name] = false;
                HashSet<string> nodeLoops = loops.ContainsKey(name) ? loops[name] : new HashSet<string>();
                foreach (string free in nodes[name].Req.FreeVars())
                {
                    if (nodes.ContainsKey(free) && !nodeLoops.Contains(free))
                    {
                        findLoops(path, free);
                    }
                }
                allDepsProcessed[name] = true;
            };
            foreach (string name in nodes.Keys)
            {
                findLoops(new List<string>(), name);
            }
            Dictionary<string, Expr> simplifiedReqs = new Dictionary<string, Expr>();
            Expr simplifyReqs(List<string> path, string name)
            {
                path = path.Concat(new[] { name }).ToList();
                if (simplifiedReqs.ContainsKey(name))
                {
                    if (simplifiedReqs[name] == null)
                    {
                        Console.WriteLine($"Path: {string.Join(",", path)}\nLoops: {string.Join("", loops.Select(e => $"\n- {e.Key}: " + string.Join(",", e.Value)))}");
                        throw new Exception($"Internal error: loop detection failed on {name}");
                    }
                    return simplifiedReqs[name];
                }
                simplifiedReqs[name] = null;
                Expr req = nodes[name].Req;
                // Delete loops
                if (loops.ContainsKey(name))
                {
                    req = req.Substitute(loops[name].ToDictionary(l => l, l => Expr.False)).Simplify();
                }
                // Replace recursively
                if (debugReqs) Console.WriteLine($"Trying to simplify {name} -> {string.Join(", ", req.FreeVars())}");
                req = req.Substitute(req.FreeVars()
                        .Where(free => nodes.ContainsKey(free))
                        .ToDictionary(free => free, free => simplifyReqs(path, free)))
                    .Simplify();
                simplifiedReqs[name] = req;
                return req;
            };
            foreach (string name in nodes.Keys)
            {
                simplifyReqs(new(), name);
            }
            return simplifiedReqs;
        }

        public class Node
        {
            public string Name { get; set; }
            public Dictionary<UniqueCategory, int> Counts { get; set; }
            public Expr Req { get; set; }
            // Rough measure of difficulty - how many checks are available before getting to this point
            public int CumKeyCount { get; set; }
            // More fine-grained
            public int CumCount { get; set; }
            public float Weight { get; set; }
            public int NonShopKeyCount { get; set; }
            public int KeyCount => Count(false);
            public int QuestCount => Count(true);
            public int AllCount => Counts.Values.Sum();
            public int Count(bool quest)
            {
                return Categories(quest).Select(cat => Counts[cat]).Sum();
            }
            public void AddItem(string item, bool quest)
            {
                foreach (UniqueCategory category in Categories(quest))
                {
                    if (Counts[category] > 0)
                    {
                        Counts[category]--;
                        return;
                    }
                }
                throw new Exception($"Ran out of space to add {(quest ? "quest" : "key")} item {item} ({Name} counts: {string.Join(" ", Counts)})");
            }
            // This doesn't distinguish between shop and non-shop, but this is fine as only DS3 ashes have this limitation to avoid recursion
            public void AddKeyShopCapacity(int amount)
            {
                Counts[UniqueCategory.Key] += amount;
            }
            public static IEnumerable<UniqueCategory> Categories(bool quest)
            {
                // From more specific to less specific
                yield return quest ? UniqueCategory.Quest : UniqueCategory.Key;
                yield return UniqueCategory.KeyQuest;
            }
            public static Dictionary<UniqueCategory, int> EmptyCounts() => GetEnumValues<UniqueCategory>().ToDictionary(c => c, c => 0);

        }

        internal static void DumpStats(RandomizerOptions options, GameData game, LocationData data, AnnotationData ann, int baseSeed)
        {
            Random seedRandom = new Random(baseSeed);
            SortedDictionary<string, List<int>> areaCount = new();
            List<string[]> groupings = new()
            {
                new[]{ "academy", "academy_chest", "academy_courtyard" },
                new[]{ "caelid", "dragonbarrow" },
                new[]{ "volcano", "volcano_temple", "volcano_town" },
                new[]{ "leyndell_erdtree", "leyndell_throne" },
                new[]{ "limgrave", "peninsula" },
                new[]{ "stormhill", "stormveil_start", "stormveil" },
                new[]{ "siofra_nokronbank", "siofra_nokron" },
            };
            List<string> lateAreas = new()
            {
                "mountaintops", "haligtree", "haligtree", "mohgwyn", "farumazula", "leyndell2",
            };
            int fail = 0;
            int tests = 10_000;
            string pct(int val) => $"{100.0 * val / tests:0.00}%";
            for (int i = 0; i < tests; i++)
            {
                int seed = seedRandom.Next();
                Random random = new Random(seed);
                KeyItemsPermutation keyItems = new KeyItemsPermutation(options, data, ann, false);
                Assignment assign;
                try
                {
                    assign = keyItems.AssignItems(random, options, null);
                }
                catch (Exception)
                {
                    fail++;
                    continue;
                }
                Dictionary<string, int> localCount = new();
                bool medal = false;
                bool mohgwyn = false;
                foreach (string configName in assign.Assign.Keys)
                {
                    ItemKey item = ann.Items[configName];
                    string name = game.Name(item);
                    List<string> areas = assign.Assign[configName].OrderBy(x => (!ann.Areas.ContainsKey(x), x)).ToList();
                    List<string> keys = new();
                    if (options["dlcstats"])
                    {
                        if (name != "Messmer's Kindling") continue;
                        // if (name != "O Mother") continue;
                        // keys.Add(string.Join(",", areas));
                        keys.Add(ann.Areas[areas[0]].Text);
                    }
                    else
                    {
                        if (name.StartsWith("Pureblood"))
                        {
                            if (!areas.Any(a => lateAreas.Any(l => a.StartsWith(l)))) medal = true;
                        }
                        if (!name.Contains("Great Rune")) continue;
                        mohgwyn |= areas.Contains("mohgwyn");
                        string[] g = groupings.Find(g => g.Intersect(areas).Count() > 0);
                        if (g != null) keys.Add(string.Join(",", g) + "**");
                        keys.Add(string.Join(",", areas));
                    }
                    foreach (string key in keys)
                    {
                        string area = key;
                        // area = area.Split('_')[0];
                        if (!localCount.ContainsKey(area)) localCount[area] = 0;
                        localCount[area]++;
                    }
                }
                if (medal)
                {
                    AddMulti(areaCount, "medal", mohgwyn ? 2 : 1);
                }
                foreach ((string area, int count) in localCount)
                {
                    AddMulti(areaCount, area, count);
                }
            }
            string obj = options["dlcstats"] ? "shard" : "GR";
            foreach (var entry in areaCount)
            {
                Console.WriteLine($"{entry.Key}: {string.Join(", ", entry.Value.GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {obj}{(g.Key == 1 ? "" : "s")} ({pct(g.Count())})"))}, overall {pct(entry.Value.Count)}");
            }
            Console.WriteLine($"{fail} failures");
        }
    }
}
