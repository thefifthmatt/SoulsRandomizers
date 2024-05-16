using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.AnnotationData;
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
        private SortedSet<string> areas = new SortedSet<string>();
        private Dictionary<string, List<string>> combinedAreas = new Dictionary<string, List<string>>();
        private Dictionary<string, HashSet<string>> combinedWeights = new Dictionary<string, HashSet<string>>();
        private HashSet<string> unusedAreas = new HashSet<string>();
        private SortedSet<string> items = new SortedSet<string>();
        // Built up over item assignment
        private SortedDictionary<string, Node> nodes = new SortedDictionary<string, Node>();
        private Dictionary<string, HashSet<string>> loops = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> itemEvents = new Dictionary<string, HashSet<string>>();

        public KeyItemsPermutation(RandomizerOptions options, LocationData data, AnnotationData ann, bool explain)
        {
            this.data = data;
            this.ann = ann;
            this.explain = explain;

            Dictionary<string, bool> config = ann.GetConfig(options.GetLogicOptions());
            Dictionary<string, Expr> configExprs = config.ToDictionary(e => e.Key, e => e.Value ? Expr.TRUE : Expr.FALSE);
            if (explain) Console.WriteLine($"Using config {string.Join(", ", config)}");

            Dictionary<LocationScope, (UniqueCategory, int)> counts = ann.GetUniqueCounts();
            Dictionary<string, Dictionary<UniqueCategory, int>> areaCounts = ann.AllAreas.ToDictionary(e => e.Key, e =>
            {
                Dictionary<UniqueCategory, int> dict = Node.EmptyCounts();
                foreach (LocationScope scope in e.Value.Where(s => counts.ContainsKey(s)))
                {
                    (UniqueCategory cat, int count) = counts[scope];
                    dict[cat] += count;
                }
                return dict;
            });

            if (ann.Events.ContainsKey("ashen"))
            {
                // Elden Ring has dynamic conditions which are hard to genericize logic for, so fill those in here.
                // runes_leyndell, runes_rold, runes_rold_event can be replaced. runes_end is also dynamic, but does not affect logic.
                List<string> runes;
                if (options["norandom"])
                {
                    // Use vanilla ordering or else we may have late-game Great Runes being required to enter Leyndell
                    runes = ann.Items.Keys.Where(i => i.StartsWith("rune")).ToList();
                }
                else
                {
                    runes = ann.Items.Keys.Where(i => i.StartsWith("rune")).OrderBy(i => i).ToList();
                    Shuffle(new Random((int)options.Seed), runes);
                }
                if (explain) Console.WriteLine($"Order of required Great Runes: {string.Join(", ", runes)}");
                int leyndellRunes = 2;
                if (options.GetInt("runes_leyndell", 0, 7, out int runeOpt))
                {
                    leyndellRunes = runeOpt;
                }
                int roldRunes = -1;
                if (options.GetInt("runes_rold", 0, 7, out runeOpt))
                {
                    roldRunes = runeOpt;
                }
                // Logic-only "free rune". This might be sensible to do given high bias and rune count.
                if (options["runebuffer"])
                {
                    leyndellRunes = Math.Min(leyndellRunes + 1, 7);
                    if (roldRunes >= 0)
                    {
                        roldRunes = Math.Min(roldRunes + 1, 7);
                    }
                }

                //If noDeath guaranteed, set the drawingroomkey forced requirements
                if (options["nodeathvalid"])
                {
                    configExprs["drawingroomkeyreq"] = Expr.Named("volcano_drawingroom");
                }
                else
                {
                    //"volcano_drawingroom OR (academy AND altus);
                    configExprs["drawingroomkeyreq"] =
                        new Expr(
                            new List<Expr>(){
                                Expr.Named("volcano_drawingroom"),
                                new Expr(
                                    new List<Expr>(){
                                        Expr.Named("academy"),
                                        Expr.Named("altus") }) }, false);
                }

                // If leyndellRunes is 0, this condition just becomes true
                configExprs["runes_leyndell"] = new Expr(runes.Take(leyndellRunes).Select(r => Expr.Named(r)).ToList(), true);
                if (roldRunes == -1)
                {
                    // Default, just require Rold
                    configExprs["runes_rold"] = Expr.Named("roldmedallion");
                    configExprs["runes_rold_event"] = Expr.Named("roldmedallion");
                }
                else
                {
                    // Do not reference Rold. It won't be placed here (and it is removed from the item pool anyway).
                    configExprs["runes_rold"] = new Expr(runes.Take(roldRunes).Select(r => Expr.Named(r)).ToList(), true);
                    // This is used for Until logic and redundant to mountaintops access condition, so blank it out if not a single item anymore.
                    configExprs["runes_rold_event"] = Expr.TRUE;
                }
                if (options["fog"] && ann.ItemGroups.TryGetValue("nofogaccess", out List<ItemKey> fogItems))
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
                        configExprs[name] = Expr.TRUE;
                    }
                }
                if (explain) Console.WriteLine($"Using special Great Rune config {string.Join(", ", configExprs)}");
            }

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
                    CumKeyCount = -1
                };
                areas.Add(name);
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

            // TODO: I don't think we need combinedAreas anymore? Only combinedWeights
            combinedAreas = combinedWeights.ToDictionary(e => e.Key, e => e.Value.ToList());

            // Last step - calculate rough measures of area difficulty, in terms of minimal number of items required for the area
            // In Elden Ring, this step encounters recursion with leyndell -> sewer -> sewer_flame -> deeproot -> leyndell
            HashSet<string> counting = new HashSet<string>();
            int getCumulativeCounts(string name)
            {
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
                if (unusedAreas.Contains(node.Name)) continue;
                getCumulativeCounts(node.Name);
                if (explain) Console.WriteLine($"{node.Name} ({node.Counts[UniqueCategory.KEY_SHOP]} shop / {node.KeyCount} area / ({node.Counts[UniqueCategory.QUEST_LOT]} quest / {node.CumKeyCount} cumulative): {node.Req}");
            }
        }

        public class Assignment
        {
            // All key items, in decreasing order of priority
            public List<ItemKey> Priority = new List<ItemKey>();
            // Required events to access other areas, to reduce their priority at higher biases
            public HashSet<string> RequiredEvents = new HashSet<string>();
            // For a given item key, which areas it can appear in
            public Dictionary<ItemKey, HashSet<string>> Assign = new Dictionary<ItemKey, HashSet<string>>();
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

        public Assignment AssignItems(Random random, RandomizerOptions options, Preset preset)
        {
            List<string> itemOrder = new List<string>(items);

            // Right now, assign key items in a random order, with endgame items last.
            // We will get more devious runs from assigning later items later, may be worth looking into, especially for game with clear phases like Sekiro has.
            Shuffle(random, itemOrder);
            bool isEndgame(string i) => i.StartsWith("cinder") || i == "secretpassagekey" || i.StartsWith("rune");
            itemOrder = itemOrder.OrderBy(i => isEndgame(i) ? 1 : 0).ToList();

            // In race mode, always put shinobiprosthetic and younglordsbellcharm first, since there is only one ashinaoutskirts_template spot for key item placement logic
            if (ann.RaceModeItems.Count > 0)
            {
                itemOrder = itemOrder.OrderBy(i => i == "shinobiprosthetic" || i == "younglordsbellcharm" ? 0 : 1).ToList();
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
            if (ann.ItemGroups.ContainsKey("ashes"))
            {
                List<string> ashesOrder = new List<string>(areas);
                ashesOrder = WeightedShuffle(random, ashesOrder, loc => Math.Min(nodes[loc].KeyCount - nodes[loc].Counts[UniqueCategory.KEY_SHOP], 3));
                int ashesIndex = 0;
                foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
                {
                    LocationScope scope = entry.Key;
                    SlotAnnotation slot = entry.Value;
                    HashSet<string> tags = slot.GetTags();
                    // Unique item unlocks other unique items unconditionally - can add a location for key item. Mainly for ashes.
                    if (slot.QuestReqs != null && !slot.HasAnyTags(ann.NoKeyTags) && scope.UniqueId > 0 && slot.ItemReqs.Count == 1)
                    {
                        ItemKey key = slot.ItemReqs[0];
                        if (ret.Assign.ContainsKey(key)) throw new Exception($"Multiple assignments for {slot.QuestReqs}");
                        string selected = ashesOrder[ashesIndex++];
                        if (explain) Console.WriteLine($"Assigning key quest item {slot.QuestReqs} to {selected}");
                        int ashesCount = data.Location(scope).Count;
                        nodes[selected].AddShopCapacity(false, ashesCount);
                        nodes[selected].AddItem(false, false);
                        ret.Assign[key] = new HashSet<string> { selected };
                        if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[key].UnionWith(events);
                    }
                }
            }
            if (explain)
            {
                int raceModeCount = 0;
                foreach (string area in areas)
                {
                    int rmode = nodes[area].Count(false, true);
                    if (rmode > 0)
                    {
                        Console.WriteLine($"RACEMODE {area}: {rmode}");
                        raceModeCount += rmode;
                    }
                }
                Console.WriteLine($"TOTAL: {raceModeCount}");
            }

            Dictionary<string, string> forcemap = new Dictionary<string, string>();
            if (options["norandom"])
            {
                foreach (string item in itemOrder)
                {
                    ItemKey itemKey = ann.Items[item];
                    // Some hardcoding for Path of the Dragon which is not an actual item
                    if (item == "pathofthedragon")
                    {
                        forcemap[item] = "highwall_garden";
                    }
                    else
                    {
                        // Check multiple item locations, since some norandom unused slots may be configured
                        string keyArea = null;
                        foreach (ItemLocation loc in data.Data[itemKey].Locations.Values)
                        {
                            if (ann.Slots.TryGetValue(loc.LocScope, out SlotAnnotation sn) && ann.Areas.ContainsKey(sn.Area))
                            {
                                keyArea = sn.Area;
                            }
                        }
                        if (keyArea == null) throw new Exception($"Internal error not randomizing {item}: unknown vanilla location");
                        forcemap[item] = keyArea;
                        // Console.WriteLine($"- Name: {item}\n  ID: {(int)itemKey.Type}:{itemKey.ID}\n  Area: {keyArea}");
                    }
                }
            }
            else if (preset?.Items != null)
            {
                foreach (KeyValuePair<string, string> entry in preset.Items)
                {
                    forcemap[entry.Key] = entry.Value;
                }
            }

            // Assign key items
            bool debugChoices = false;
            // public readonly Dictionary<ItemKey, PlacementRestrictionAnnotation> ItemRestrict = new Dictionary<ItemKey, PlacementRestrictionAnnotation>();
            float scaling = options.GetNum("keyitemchainweight");
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
                // Prevent Dectus in Precipice also hackily. We don't want to do this in logic directly because of precipice earliness
                if (item == "dectusmedallionleft" || item == "dectusmedallionright")
                {
                    specialScaling = new Dictionary<string, float> { ["precipice"] = 0 };
                }
                if (ann.ItemRestrict != null && ann.ItemRestrict.TryGetValue(itemKey, out PlacementRestrictionAnnotation restrict) && restrict.KeyAreas != null)
                {
                    string[] restrictAreas = restrict.KeyAreas.Split(' ');
                    specialScaling = allowedAreas.Where(area => !restrictAreas.Contains(area)).ToDictionary(area => area, _ => 0f);
                    if (specialScaling.Count == allowedAreas.Count) throw new Exception($"Can't place extra-restricted item {item}");
                    if (debugChoices) Console.WriteLine($">{item} restricted to {string.Join(",", restrictAreas)}");
                }
                Dictionary<string, float> weights = allowedAreas.ToDictionary(area => area, area => Weight(area) * (specialScaling.TryGetValue(area, out float sp) ? sp : 1));

                if (debugChoices) Console.WriteLine($"> Choices for {item}: " + string.Join(", ", allowedAreas.OrderBy(a => weights[a]).Select(a => $"{a} {weights[a]}")));
                string selected = WeightedChoice(random, allowedAreas, a => weights[a]);

                if (forcemap.TryGetValue(item, out string forced))
                {
                    if (explain && !allowedAreas.Contains(forced)) Console.WriteLine($"Key item {item} put in non-random location {forced} which isn't normally allowed by logic");
                    selected = forced;
                }
                AddItem(item, selected, forced != null);

                ret.Priority.Add(itemKey);
                ret.Assign[itemKey] = new HashSet<string> { selected };
                // Areas should include events there. Except for bell charm being dropped by chained ogre, if that option is enabled
                // todo: check this works okay with racemode key items, and nothing else randomized.
                if (!(item == "younglordsbellcharm" && options["earlyhirata"]))
                {
                    if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[itemKey].UnionWith(events);
                }
                if (explain || debugChoices) Console.WriteLine($"Adding {item} to {string.Join(",", ret.Assign[itemKey])}");

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
                AdjustWeight(selected, 1 / scaling);
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
            // Academy Glintstone Key is not a key in crawl mode but it needs to be at Loretta with min locations and earlylegacy
            if (options["crawl"] && options["earlylegacy"] && !ret.Priority.Contains(new ItemKey(ItemType.GOOD, 8109)))
            {
                ret.Priority.Add(new ItemKey(ItemType.GOOD, 8109));
            }

            // Now that all key items have been assigned, determine which areas are blocked by other areas.
            // This is used to determine lateness within the game (by # of items encountered up to that point).
            HashSet<string> getIncludedAreas(string name, List<string> path)
            {
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
                // Redefine weights for quest selection
                node.Weight = 1;
                if (areas.Contains(node.Name))
                {
                    node.CumKeyCount = ret.IncludedAreas[node.Name].Where(n => nodes[n].Counts != null).Select(n => nodes[n].Count(true, true)).Sum();
                    if (explain) Console.WriteLine($"Included {node.Name}: {node.Count(true, true)}/{node.CumKeyCount}: {string.Join(",", ret.IncludedAreas[node.Name])}");
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

            // Make an area order
            List<string> areaOrder = areas.OrderBy(a => nodes[a].CumKeyCount).ToList();
            Dictionary<string, int> areaIndex = Enumerable.Range(0, areaOrder.Count()).ToDictionary(i => areaOrder[i], i => i);
            string latestArea(IEnumerable<string> ns)
            {
                return areaOrder[ns.Select(n => areaIndex.TryGetValue(n, out int i) ? i : throw new Exception($"No order for area {n}")).DefaultIfEmpty().Max()];
            }

            if (explain && false)
            {
                foreach (var entry in ret.IncludedAreas) Console.WriteLine($"Area scope {entry.Key}: {string.Join(" ", entry.Value)}");
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
                HashSet<string> tags = slot.GetTags();
                if (slot.QuestReqs != null)
                {
                    // Should likely use slot.ItemReqs. But strings are nice for debugging.
                    string[] questReqs = slot.QuestReqs.Split(' ');
                    foreach (string questReq in questReqs)
                    {
                        if (ann.Items.ContainsKey(questReq))
                        {
                            ItemKey itemKey = ann.Items[questReq];
                            AddMulti(ret.RestrictedItems, itemKey, scope);
                            if (!questItemAreaSlots.ContainsKey(questReq))
                            {
                                questItemAreaSlots[questReq] = new Dictionary<string, int>();
                            }
                            questItemAreaSlots[questReq][area] = questItemAreaSlots[questReq].ContainsKey(area) ? questItemAreaSlots[questReq][area] + 1 : 1;
                            questItems.Add(questReq);
                        }
                    }
                }
                foreach (string tag in slot.TagList)
                {
                    if (tag.Contains(':'))
                    {
                        string[] parts = tag.Split(':');
                        if (parts[0] == "exclude")
                        {
                            // Console.WriteLine($"Adding exclude {ann.Items[parts[1]].Key} to {slot.Text}");
                            AddMulti(ret.RestrictedItems, ann.Items[parts[1]], scope);
                        }
                    }
                }
            }
            // Assign quest items to areas.
            foreach (string questItem in questItems)
            {
                ItemKey itemKey = ann.Items[questItem];
                if (ret.Assign.ContainsKey(itemKey))
                {
                    if (explain) Console.WriteLine($"{questItem} already assigned to {string.Join(", ", ret.Assign[itemKey])}");
                    continue;
                }
                Dictionary<string, int> questAreas = questItemAreaSlots[questItem];
                // TODO: Does this contain items? If so, filter those out
                List<string> allowedAreas = ann.ItemRestrict.ContainsKey(itemKey)
                    ? areas.Intersect(ann.ItemRestrict[itemKey].Unique[0].AllowedAreas(ret.IncludedAreas, combinedWeights)).ToList()
                    : areas.ToList();
                bool itemAllowMissable = ann.ExcludeTags.TryGetValue(itemKey, out HashSet<string> excludeTags) ? !excludeTags.Contains("missable") : true;
                bool allowQuest = itemAllowMissable || ann.NoQuestTags.Contains("missable");
                string selected = WeightedChoice(random, allowedAreas, a => Weight(a, allowQuest, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0));
                if (explain) Console.WriteLine($"Selecting {questItem} to go in {selected} ({string.Join(" ", nodes[selected].Counts)})");
                // if (selected == "volcano" || true) Console.WriteLine($"  can miss {allowMissable}, areas {string.Join(" ", questAreas)}. weights {string.Join(", ", allowedAreas.Select(a => $"{a}={Weight(a, allowMissable, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0)}"))}");
                // if (selected == "volcano" || true) Console.WriteLine($"  areas {string.Join(" ", questAreas)}. weights {string.Join(", ", allowedAreas.Where(a => nodes[a].Count(true, true) >= 0).Select(a => $"{a}={nodes[a].Count(true, true)}"))}");
                // if (selected == "volcano" || true) Console.WriteLine($"  can miss {allowMissable}, areas {string.Join(" ", questAreas)}. weights sum >>{allowedAreas.Sum(a => Weight(a, allowMissable, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0))}");
                nodes[selected].AddItem(allowQuest, true);
                ret.Assign[itemKey] = new HashSet<string> { selected };
                if (ann.AreaEvents.TryGetValue(selected, out List<string> events)) ret.Assign[itemKey].UnionWith(events);
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
                    string effectiveArea = latestArea(Enumerable.Concat(slot.AreaReqs, slot.ItemReqs.SelectMany(item => ret.Assign[item].Where(a => areas.Contains(a)))));
                    if (area != effectiveArea) ret.EffectiveLocation[scope] = effectiveArea;
                }
            }
            int combinedTotal = areas.Select(a => nodes[a].CumKeyCount).Max();
            // TODO: Use combinedWeights here if they're the same
            foreach (KeyValuePair<string, List<string>> entry in combinedAreas)
            {
                ret.CombinedWeights[entry.Key] = new HashSet<string>(entry.Value);
                double partial = (double) nodes[entry.Key].CumKeyCount / combinedTotal;
                foreach (string same in entry.Value)
                {
                    ret.LocationLateness[same] = partial;
                }
            }
            return ret;
        }

        public float Weight(string area, bool allowQuest = false, float lateFactor = 0.1f, int removeQuest = 0)
        {
            Node node = nodes[area];
            int count = node.Count(allowQuest, true) - removeQuest;
            if (count <= 0)
            {
                return 0;
            }
            count += (int)(node.CumKeyCount * lateFactor);
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
            nodes[area].AddItem(allowQuest: false, allowShops: true);
        }

        // The core routine at the center of placing key items.
        // Given the current area layout and pending item assignment, reduce the condition for each area to be only in terms of items, not in terms of other areas.
        // Then, for a given item, it is possible to tell which areas unconditionally depend on that item. The item can then be placed anywhere else.
        private Dictionary<string, Expr> CollapseReqs()
        {
            Dictionary<string, bool> allDepsProcessed = new Dictionary<string, bool>();
            Action<List<string>, string> findLoops = null;
            findLoops = (path, name) =>
            {
                path = path.Concat(new[] { name }).ToList();
                if (allDepsProcessed.ContainsKey(name))
                {
                    // If all deps satisfied, no issue. Otherwise...
                    if (!allDepsProcessed[name])
                    {
                        List<string> subpath = path.Skip(path.IndexOf(name)).ToList();
                        // Use a heuristic to see where we should snip the path. This doesn't work in a very small portion
                        // of cases, but most valid randomizations are interesting so just try again with a different seed.
                        foreach ((string fro, string to) in subpath.Zip(subpath.Skip(1), (a, b) => (a, b)))
                        {
                            if (!nodes[fro].Req.Needs(to))
                            {
                                AddMulti(loops, fro, to);
                                return;
                            }
                        }
                        throw new Exception($"Unsolvable seed: hard dependency loop [{string.Join(",", subpath)}]");
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
            Func<string, Expr> simplifyReqs = null;
            simplifyReqs = name =>
            {
                if (simplifiedReqs.ContainsKey(name))
                {
                    if (simplifiedReqs[name] == null)
                    {
                        throw new Exception($"Loop detection failed on {name} - internal error");
                    }
                    return simplifiedReqs[name];
                }
                simplifiedReqs[name] = null;
                Expr req = nodes[name].Req;
                // Delete loops
                if (loops.ContainsKey(name))
                {
                    req = req.Substitute(loops[name].ToDictionary(l => l, l => Expr.FALSE)).Simplify();
                }
                // Replace recursively
                // Console.WriteLine($"Trying to simplify {name} -> {string.Join(", ", req.FreeVars())}");
                req = req.Substitute(req.FreeVars()
                        .Where(free => nodes.ContainsKey(free))
                        .ToDictionary(free => free, free => simplifyReqs(free)))
                    .Simplify();
                simplifiedReqs[name] = req;
                return req;
            };
            foreach (string name in nodes.Keys)
            {
                simplifyReqs(name);
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
            public float Weight { get; set; }
            public void Merge(Dictionary<UniqueCategory, int> other)
            {
                foreach (UniqueCategory cat in Categories(true, true))
                {
                    Counts[cat] += other[cat];
                }
            }
            public int KeyCount { get => Count(false, true); }
            public int Count(bool allowQuest, bool allowShops)
            {
                return Categories(allowQuest, allowShops).Select(cat => Counts[cat]).Sum();
            }
            public void AddItem(bool allowQuest, bool allowShops)
            {
                foreach (UniqueCategory category in Categories(allowQuest, allowShops))
                {
                    if (Counts[category] > 0)
                    {
                        Counts[category]--;
                        return;
                    }
                }
                throw new Exception($"Cannot add item to {Name} in quest {allowQuest}, shops {allowShops} (counts: {string.Join(" ", Counts)})");
            }
            public void AddShopCapacity(bool allowQuest, int amount)
            {
                Counts[allowQuest ? UniqueCategory.QUEST_SHOP : UniqueCategory.KEY_SHOP] += amount;
            }
            public static IEnumerable<UniqueCategory> Categories(bool allowQuest, bool allowShops)
            {
                yield return UniqueCategory.KEY_LOT;
                if (allowShops) yield return UniqueCategory.KEY_SHOP;
                if (allowQuest)
                {
                    yield return UniqueCategory.QUEST_LOT;
                    if (allowShops) yield return UniqueCategory.QUEST_SHOP;
                }
            }
            public static Dictionary<UniqueCategory, int> EmptyCounts() => Categories(true, true).ToDictionary(c => c, c => 0);
        }
    }
}
