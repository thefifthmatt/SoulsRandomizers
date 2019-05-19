using System;
using System.Collections.Generic;
using System.Linq;
using static DS3StaticItemRandomizer.AnnotationData;
using static DS3StaticItemRandomizer.LocationData;
using static DS3StaticItemRandomizer.Util;

namespace DS3StaticItemRandomizer
{
    public class KeyItemsPermutation
    {
        private LocationData data;
        private AnnotationData ann;
        private bool explain;

        // Initial state
        private SortedSet<string> areas = new SortedSet<string>();
        private Dictionary<string, List<string>> combinedAreas = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> combinedWeights = new Dictionary<string, List<string>>();
        private HashSet<string> unusedAreas = new HashSet<string>();
        private SortedSet<string> items = new SortedSet<string>();
        // Built up over item assignment
        private SortedDictionary<string, Node> nodes = new SortedDictionary<string, Node>();
        private Dictionary<string, HashSet<string>> loops = new Dictionary<string, HashSet<string>>();

        public KeyItemsPermutation(RandomizerOptions options, LocationData data, AnnotationData ann, bool explain=false)
        {
            this.data = data;
            this.ann = ann;
            this.explain = explain;

            Dictionary<string, bool> config = ann.GetConfig(options.GetEnabled());
            Dictionary<string, Expr> configExprs = config.ToDictionary(e => e.Key, e => e.Value ? Expr.TRUE : Expr.FALSE);

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

            Dictionary<string, string> equivalentGraph = new Dictionary<string, string>();
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
                SortedSet<string> frees = req.FreeVars();
                if (frees.Count() == 1)
                {
                    // Proper aliases are already collapsed using the BaseArea slot property, skip those
                    if (ann.AreaAliases[name] != name) continue;
                    equivalentGraph[name] = frees.First();
                    if (explain) Console.WriteLine($"Collapsed areas for key item generation: {name} -> {frees.First()}");
                    continue;
                }
                nodes[name] = new Node
                {
                    Name = name,
                    Counts = areaCounts[name],
                    Req = req,
                    Weight = 1,
                    CumKeyCount = -1
                };
                areas.Add(name);
                AddMulti(combinedAreas, name, name);
                AddMulti(combinedWeights, area.WeightBase == null ? name : area.WeightBase, name);
                foreach (string free in frees)
                {
                    if (!ann.Areas.ContainsKey(free))
                    {
                        items.Add(free);
                    }
                }
            }
            foreach (string item in items)
            {
                if (!ann.Items.ContainsKey(item)) throw new Exception($"Unknown item {item} in area requirements");
            }
            foreach (KeyValuePair<string, List<string>> entry in combinedWeights.Where(e => e.Value.Count > 1).ToList())
            {
                foreach (string sharedArea in entry.Value)
                {
                    combinedWeights[sharedArea] = entry.Value;
                }
            }
            // Quick collapse of equivalence graph
            Dictionary<string, Expr> equivalent = new Dictionary<string, Expr>();
            Func<string, Expr> getBaseName = null;
            getBaseName = name =>
            {
                if (equivalent.ContainsKey(name))
                {
                    return equivalent[name];
                }
                else if (equivalentGraph.ContainsKey(name))
                {
                    Expr root = getBaseName(equivalentGraph[name]);
                    equivalent[name] = root;
                    AddMulti(combinedAreas, root.FreeVars().First(), name);
                    return root;
                }
                else
                {
                    return Expr.Named(name);
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
                        nodes[entry.Key].Merge(areaCounts[alias]);
                    }
                }
            }
            foreach (Node node in nodes.Values)
            {
                node.Req = node.Req.Substitute(equivalent).Simplify();
            }
            // Last step - calculate rough measures of area difficulty, in terms of minimal number of items required for the area
            Func<string, int> getCumulativeCounts = null;
            getCumulativeCounts = name =>
            {
                Node node = nodes[name];
                if (node.CumKeyCount != -1)
                {
                    return node.KeyCount + node.CumKeyCount;
                }
                int count = node.Req.FreeVars().Where(free => areas.Contains(free)).Select(free => getCumulativeCounts(free)).DefaultIfEmpty().Max();
                node.CumKeyCount = count;
                return node.KeyCount + count;
            };
            foreach (Node node in nodes.Values)
            {
                getCumulativeCounts(node.Name);
                if (explain) Console.WriteLine($"{node.Name} ({node.Counts[UniqueCategory.KEY_SHOP]} shop / {node.KeyCount} area / {node.CumKeyCount} cumulative): {node.Req}");
            }
        }
        public class Assignment
        {
            public readonly List<ItemKey> Priority = new List<ItemKey>();
            public readonly Dictionary<ItemKey, HashSet<string>> Assign = new Dictionary<ItemKey, HashSet<string>>();
            public readonly Dictionary<ItemKey, List<LocationScope>> RestrictedItems = new Dictionary<ItemKey, List<LocationScope>>();
            public readonly Dictionary<LocationScope, string> EffectiveLocation = new Dictionary<LocationScope, string>();
            public readonly Dictionary<string, double> LocationLateness = new Dictionary<string, double>();
            public readonly Dictionary<string, HashSet<string>> IncludedAreas = new Dictionary<string, HashSet<string>>();
        }

        public Assignment AssignItems(Random random, RandomizerOptions options)
        {
            List<string> itemOrder = new List<string>(items);
            // Right now, assign key items in a random order, with cinders last.
            // We will get more devious runs from assigning later items later, may be worth looking into, especially for game with clear phases like Sekiro has.
            Shuffle(random, itemOrder);
            itemOrder = itemOrder.Where(i => !i.StartsWith("cinder")).Concat(itemOrder.Where(i => i.StartsWith("cinder"))).ToList();
            Assignment ret = new Assignment();
            // First assign ashes for key item placement. Other quest assignments can happen later.
            Dictionary<string, int> unmissableQuestSlots = new Dictionary<string, int>();
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
                    string loc = ashesOrder[ashesIndex++];
                    if (explain) Console.WriteLine($"Assigning key quest item {slot.QuestReqs} to {loc}");
                    int ashesCount = data.Location(scope).Count;
                    nodes[loc].AddShopCapacity(false, ashesCount);
                    nodes[loc].AddItem(false, false);
                    AddMulti(ret.Assign, key, combinedAreas[loc]);
                }
            }
            // Assign key items
            float scaling = options.GetNum("keyitemchainweight");
            Dictionary<string, Expr> reqs = CollapseReqs();
            foreach (string item in itemOrder)
            {
                List<string> allowedAreas = areas.Where(a => !reqs[a].Needs(item)).ToList();
                string selected = WeightedChoice(random, allowedAreas, a => Weight(a));
                AddItem(item, selected);
                ItemKey itemKey = ann.Items[item].Key;
                ret.Priority.Add(itemKey);
                ret.Assign[itemKey] = new HashSet<string>(combinedAreas[selected]);
                if (explain) Console.WriteLine($"Adding {item} to {string.Join(", ", combinedAreas[selected])}");
                // Update weights
                reqs = CollapseReqs();
                // Heuristic which forms chains and spreads items across areas
                // Reduce weight for this area, and increase weight for areas which depend on the item
                AdjustWeight(selected, 1 / scaling);
                foreach (string area in areas)
                {
                    if (reqs[area].Needs(item) && ann.Areas[area].WeightBase == null)
                    {
                        AdjustWeight(area, scaling);
                    }
                }
            }
            ret.Priority.Reverse();

            // Now that all key items have been assigned, determine which areas are blocked by other areas.
            // This is used to determine lateness within the game (by # of items encountered up to that point).
            Func<string, List<string>, HashSet<string>> getIncludedAreas = null;
            getIncludedAreas = (name, path) =>
            {
                path = path.Concat(new[] { name }).ToList();
                Node node = nodes[name];
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
                if (areas.Contains(name))
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
                    node.CumKeyCount = ret.IncludedAreas[node.Name].Select(n => nodes[n].Count(true, true)).Sum();
                    if (explain) Console.WriteLine($"Quest area {node.Name}: {node.Count(true, true)}/{node.CumKeyCount}: {string.Join(",", ret.IncludedAreas[node.Name])}");
                }
            }
            // The above DFS adds both items and areas together, so remove the items.
            foreach (string key in ret.IncludedAreas.Keys.ToList())
            {
                if (!areas.Contains(key)) ret.IncludedAreas.Remove(key);
            }
            // Add combinedAreas to the area dependency list (which are distinct from aliases, which have already been removed)
            // It is a bit annoying to have to maintain these two systems
            foreach (KeyValuePair<string, List<string>> entry in combinedAreas)
            {
                if (entry.Value.Count == 1) continue;
                foreach (string same in entry.Value)
                {
                    // The aliases are basically the same as the base area.
                    ret.IncludedAreas[same] = ret.IncludedAreas[entry.Key];
                }
                foreach (KeyValuePair<string, HashSet<string>> included in ret.IncludedAreas)
                {
                    if (included.Value.Contains(entry.Key))
                    {
                        included.Value.UnionWith(entry.Value);
                    }
                }
            }
            foreach (string area in unusedAreas)
            {
                ret.IncludedAreas[area] = new HashSet<string>();
            }
            List<string> areaOrder = areas.OrderBy(a => nodes[a].CumKeyCount).ToList();
            Dictionary<string, int> areaIndex = Enumerable.Range(0, areaOrder.Count()).ToDictionary(i => areaOrder[i], i => i);
            // Again, can refer to area ordering based on combinedAreas
            foreach (KeyValuePair<string, List<string>> entry in combinedAreas)
            {
                foreach (string same in entry.Value)
                {
                    areaIndex[same] = areaIndex[entry.Key];
                }
            }
            Func<IEnumerable<string>, string> latestArea = ns => areaOrder[ns.Select(n => areaIndex[n]).DefaultIfEmpty().Max()];

            if (explain)
            {
                foreach (var entry in ret.IncludedAreas) Console.WriteLine($"Area scope {entry.Key}: {string.Join(" ", entry.Value)}");
            }
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
                            ItemKey itemKey = ann.Items[questReq].Key;
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
            }
            // Assign quest items to areas.
            foreach (string questItem in questItems)
            {
                ItemKey itemKey = ann.Items[questItem].Key;
                if (ret.Assign.ContainsKey(itemKey))
                {
                    if (explain) Console.WriteLine($"{questItem} already assigned to {string.Join(", ", ret.Assign[itemKey])}");
                    continue;
                }
                Dictionary<string, int> questAreas = questItemAreaSlots[questItem];
                List<string> allowed = ann.ItemRestrict.ContainsKey(itemKey) ? areas.Intersect(ann.ItemRestrict[itemKey].Unique[0].AllowedAreas(ret.IncludedAreas)).ToList() : areas.ToList();
                string selected = WeightedChoice(random, allowed, a => Weight(a, true, 0.01f, questAreas.ContainsKey(a) ? questAreas[a] : 0));
                if (explain) Console.WriteLine($"Selecting {questItem} to go in {selected}");
                nodes[selected].AddItem(true, true);
                ret.Assign[itemKey] = new HashSet<string>(combinedAreas[selected]);
            }
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in ann.Slots)
            {
                LocationScope scope = entry.Key;
                SlotAnnotation slot = entry.Value;
                HashSet<string> tags = slot.GetTags();
                if (slot.QuestReqs != null)
                {
                    string area = slot.GetArea();
                    if (unusedAreas.Contains(area))
                    {
                        continue;
                    }
                    // Set effective area if different from actual area. This is just used for placement heuristics
                    string effectiveArea = latestArea(Enumerable.Concat(slot.AreaReqs, slot.ItemReqs.SelectMany(item => ret.Assign[item])));
                    if (area != effectiveArea) ret.EffectiveLocation[scope] = effectiveArea;
                }
            }
            int combinedTotal = areas.Select(a => nodes[a].CumKeyCount).Max();
            foreach (KeyValuePair<string, List<string>> entry in combinedAreas)
            {
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
            if (count == 0)
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
                nodes[area].Weight *= factor;
            }
        }
        public void AddItem(string item, string area)
        {
            nodes[item] = new Node { Name = item, Req = Expr.Named(area) };
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
                        throw new Exception("Hard dependency loop");
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
                throw new Exception($"Cannot add item to {Name} in quest {allowQuest}, shops {allowShops}");
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
