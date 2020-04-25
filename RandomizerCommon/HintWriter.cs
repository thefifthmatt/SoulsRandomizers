using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SoulsFormats;
using YamlDotNet.Serialization;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class HintWriter
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        public HintWriter(GameData game, LocationData data, AnnotationData ann)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
        }

        public void Write(RandomizerOptions opt, Permutation permutation)
        {
            // Overall: 9020, and 9200 to 9228
            // Exclude 9209 and 9215 and 9228 bc it's useful
            // Also, 9221 9223 9225 are 'purchase to read'
            // Can replace everything in quotes
            // Or after the first :\n
            // Or after the last \n\n
            // If the message ends with '               -?Name' can leave that. space or ideographic space (3000). multiple whitespace either way
            // 
            // Alternatively: replace individual text, like 'moon-view tower'
            // defeat a named enemy
            // defeat a formidable foe
            // defeat a powerful enemy
            FMG itemDesc = game.ItemFMGs["アイテム説明"];
            FMG eventText = game.MenuFMGs["イベントテキスト"];
            if (opt["writehints"])
            {
                List<int> eventIds = new List<int>
                {
                    12000000, 12000331, 12000021, 12000261, 12000275, 12000321, 12000285,
                    12000241, 12000011, 12000311, 12000231, 12000291, 12000341,
                };
                eventIds.Sort();
                HintData write = new HintData();
                void createHint(int id, string name, string text)
                {
                    Hint hint = new Hint
                    {
                        ID = id,
                        Name = name,
                        Versions = new List<HintTemplate>
                        {
                            new HintTemplate
                            {
                                Type = "default",
                                Text = text.Split('\n').ToList(),
                            }
                        },
                    };
                    write.Hints.Add(hint);
                }
                foreach (KeyValuePair<ItemKey, string> entry in game.Names())
                {
                    ItemKey key = entry.Key;
                    if (!(key.Type == ItemType.GOOD && (key.ID == 9020 || key.ID >= 9200 && key.ID <= 9228))) continue;
                    createHint(key.ID, entry.Value, itemDesc[key.ID]);
                }
                foreach (int id in eventIds)
                {
                    createHint(id, null, eventText[id]);
                }
                ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
                using (var writer = File.CreateText("hints.txt"))
                {
                    serializer.Serialize(writer, write);
                }
                return;
            }

            IDeserializer deserializer = new DeserializerBuilder().Build();
            HintData hints;
            using (var reader = File.OpenText("dists/Base/hints.txt"))
            {
                hints = deserializer.Deserialize<HintData>(reader);
            }

            // Preprocess some items
            Dictionary<HintType, TypeHint> typeNames = hints.Types.ToDictionary(e => e.Name, e => e);
            Dictionary<ItemCategory, List<ItemKey>> categories = ((ItemCategory[])Enum.GetValues(typeof(ItemCategory))).ToDictionary(e => e, e => new List<ItemKey>());
            Dictionary<ItemCategory, string> categoryText = new Dictionary<ItemCategory, string>();
            foreach (ItemHint cat in hints.ItemCategories)
            {
                if (cat.Includes != null)
                {
                    categories[cat.Name].AddRange(cat.Includes.Split(' ').Select(i => ann.Items.TryGetValue(i, out ItemKey key) ? key : throw new Exception($"Unrecognized name {i}")));
                }
                if (cat.IncludesName != null)
                {
                    categories[cat.Name].AddRange(phraseRe.Split(cat.IncludesName).Select(i => game.ItemForName(i)));
                }
                if (cat.Text != null)
                {
                    categoryText[cat.Name] = cat.Text;
                }
            }
            if (opt["earlyhirata"]) categories[ItemCategory.ExcludeHints].Add(ann.Items["younglordsbellcharm"]);
            categories[ItemCategory.ExcludeHints].AddRange(permutation.NotRequiredKeyItems);

            // TODO: Exclude non-technically-required items... calculate this in key item permutations
            List<ItemKey> allItems = permutation.KeyItems.ToList();
            if (opt["norandom_skills"])
            {
                categories[ItemCategory.ImportantTool].Clear();
            }
            else
            {
                allItems.AddRange(categories[ItemCategory.ImportantTool]);
            }
            allItems.AddRange(categories[ItemCategory.HintFodder]);

            Dictionary<string, ItemHintName> specialItemNames = hints.ItemNames.ToDictionary(n => n.Name, n => n);

            // Process areas
            Dictionary<string, AreaHint> areas = new Dictionary<string, AreaHint>();
            HashSet<string> gameAreas = new HashSet<string>(ann.Areas.Keys);
            List<string> getAreasForName(string names)
            {
                List<string> nameList = new List<string>();
                foreach (string name in names.Split(' '))
                {
                    if (name.EndsWith("*"))
                    {
                        string prefix = name.Substring(0, name.Length - 1);
                        List<string> matching = gameAreas.Where(n => n.StartsWith(prefix)).ToList();
                        if (matching.Count == 0) throw new Exception($"Unrecognized area in hint config: {name}");
                        nameList.AddRange(matching);
                    }
                    else
                    {
                        if (!gameAreas.Contains(name)) throw new Exception($"Unrecognized area in hint config: {name}");
                        nameList.Add(name);
                    }
                }
                return nameList;
            }
            foreach (AreaHint area in hints.Areas)
            {
                if (area.Name == null || area.Includes == null) throw new Exception($"Missing data in area hint grouping {area.Name}");
                areas[area.Name] = area;
                area.Areas.UnionWith(getAreasForName(area.Includes));
                if (area.Excludes != null)
                {
                    area.Areas.ExceptWith(getAreasForName(area.Excludes));
                }
                if (area.LaterIncludes != null)
                {
                    area.LaterAreas.UnionWith(getAreasForName(area.LaterIncludes));
                    if (!area.LaterAreas.IsSubsetOf(area.Areas)) throw new Exception($"Error in hint config: later areas of {area.Name} are not a subset of all areas");
                }
                area.EarlyAreas.UnionWith(area.Areas.Except(area.LaterAreas));
                if (area.Parent != null)
                {
                    if (!areas.TryGetValue(area.Parent, out AreaHint parent)) throw new Exception($"Error in hint config: parent of {area.Name} does not exist: {area.Parent}");
                    area.Parents.Add(parent);
                    area.Parents.UnionWith(parent.Parents);
                }
                if (area.Present != null)
                {
                    area.Types = area.Present.Split(' ').Select(t => (HintType)Enum.Parse(typeof(HintType), t)).ToList();
                }
            }

            bool printText = opt["hinttext"];

            // Process items to search for
            List<ItemCategory> categoryOverrides = new List<ItemCategory> { ItemCategory.RequiredKey, ItemCategory.RequiredAbility, ItemCategory.ImportantTool, ItemCategory.HintFodder };
            HashSet<string> chests = new HashSet<string> { "o005300", "o005400", "o255300" };
            List<Placement> placements = new List<Placement>();
            Dictionary<ItemKey, Placement> itemPlacement = new Dictionary<ItemKey, Placement>();
            foreach (ItemKey key in allItems)
            {
                if (categories[ItemCategory.ExcludeHints].Contains(key)) continue;
                if (!permutation.SkillAssignment.TryGetValue(key, out ItemKey lookup))
                {
                    lookup = key;
                }
                SlotKey targetKey = permutation.GetFiniteTargetKey(lookup);
                ItemLocation itemLoc = data.Location(targetKey);
                if (!ann.Slots.TryGetValue(itemLoc.LocScope, out SlotAnnotation slot)) continue;
                ItemCategory category = ItemCategory.RequiredItem;
                foreach (ItemCategory cat in categoryOverrides)
                {
                    // Use the last applicable category
                    if (categories[cat].Contains(key)) category = cat;
                }
                List<HintType> types = new List<HintType>();
                if (slot.HasTag("boss") || slot.HasTag("bosshint"))
                {
                    types.Add(HintType.Boss);
                    types.Add(HintType.Enemy);
                }
                else if (slot.HasTag("miniboss"))
                {
                    types.Add(HintType.Miniboss);
                    types.Add(HintType.Enemy);
                }
                else if (slot.HasTag("enemyhint"))
                {
                    types.Add(HintType.Enemy);
                }
                else if (slot.HasTag("carp"))
                {
                    types.Add(HintType.Carp);
                }
                else if (itemLoc.Keys.Any(k => k.Type == LocationKey.LocationType.SHOP && k.ID / 100 != 11005))
                {
                    types.Add(HintType.Shop);
                }
                else
                {
                    if (slot.Area.EndsWith("_underwater") || slot.HasTag("underwater"))
                    {
                        types.Add(HintType.Underwater);
                    }
                    if (itemLoc.Keys.Any(k => k.Type == LocationKey.LocationType.LOT && k.Entities.Any(e => chests.Contains(e.ModelName))))
                    {
                        types.Add(HintType.Chest);
                    }
                    types.Add(HintType.Treasure);
                }

                string name = game.Name(key);
                Placement placement = new Placement
                {
                    Item = key,
                    FullName = name,
                    Category = category,
                    LateEligible = categories[ItemCategory.LatenessHints].Contains(key),
                    Important = category != ItemCategory.HintFodder,
                    Area = slot.Area,
                    Types = types,
                };
                if (placement.Important) placements.Add(placement);
                itemPlacement[key] = placement;
                if (printText) Console.WriteLine(placement);
            }

            foreach (Hint hint in hints.Hints)  // Lovely
            {
                hint.Types = hint.Versions.Where(v => v.Type != "default").ToDictionary(v => v.Type, v => v);
            }

            // Classify early and late areas
            HashSet<string> early = new HashSet<string>(permutation.IncludedAreas.Where(e => !e.Value.Contains("ashinacastle")).Select(e => e.Key));
            HashSet<string> late = new HashSet<string>(permutation.IncludedAreas.Where(e => e.Value.Contains("fountainhead_bridge")).Select(e => e.Key));

            // Start hints
            Random random = new Random((int)opt.Seed);
            List<Hint> sources = hints.Hints.Where(s => s.Types.Any(e => e.Key != "default")).ToList();
            Shuffle(random, sources);
            sources = sources.OrderBy(s => (s.HasInfix("bad") && s.Types.ContainsKey("hint")) ? 1 : 0).ToList();
            string choose(List<string> items)
            {
                return items.Count == 1 ? items[0] : Choice(random, items);
            }

            // Process all hint types. There are 20 item locations in the entire game, plus 13 fixed texts, for a total of 33
            Regex format = new Regex(@"\(([^\)]*)\)");
            if (printText)
            {
                Console.WriteLine($"No hint items: {string.Join(", ", categories[ItemCategory.ExcludeHints].Select(k => game.Name(k)))}");
            }
            void addHint(Hint hint, HintTemplate t, Placement mainPlacement, Placement otherPlacement = null)
            {
                string text = printText ? string.Join(" ", t.Text.Select(l => l.Trim())) : string.Join("\n", t.Text);
                bool positive = !t.Type.Contains("bad");
                foreach (Match m in format.Matches(text))
                {
                    string variable = m.Groups[1].Value;
                    string[] parts = variable.Split('_');
                    string kind = parts[0].ToLowerInvariant();
                    bool upper = char.IsUpper(parts[0][0]);
                    string subkind = parts.Length > 1 ? parts[1] : null;
                    string value;
                    Placement placement = kind == "location2" && otherPlacement != null ? otherPlacement : mainPlacement;
                    if (kind == "item")
                    {
                        if (positive)
                        {
                            if (placement.LateHint || !categoryText.ContainsKey(placement.Category))
                            {
                                value = placement.FullName;
                                if (specialItemNames.TryGetValue(value, out ItemHintName vagueName))
                                {
                                    value = choose(vagueName.GetNames());
                                }
                            }
                            else
                            {
                                value = categoryText[placement.Category];
                            }
                        }
                        else
                        {
                            // Shouldn't be used in this context, but fall back
                            value = "nothing";
                        }
                    }
                    else if (kind == "type")
                    {
                        HintType type = placement.Types.FirstOrDefault();
                        value = choose(typeNames[placement.Types[0]].GetNames(subkind));
                    }
                    else if (kind == "location" || kind == "location2")
                    {
                        bool prep;
                        if (subkind == null) prep = false;
                        else if (subkind == "preposition") prep = true;
                        else throw new Exception($"Unknown hint config variable {variable}");
                        if (positive || placement.Types.Count == 0)
                        {
                            value = placement.AreaHint == null ? (positive ? "somewhere" : "anywhere") : choose(placement.AreaHint.GetNames(prep));
                        }
                        else
                        {
                            if (prep || placement.AreaHint != null)
                            {
                                value = (prep ? "from " : "") + choose(typeNames[placement.Types[0]].GetNames("noun")) + " " + (placement.AreaHint == null ? (positive ? "somewhere" : "anywhere") : choose(placement.AreaHint.GetNames(true)));
                            }
                            else
                            {
                                value = value = choose(typeNames[placement.Types[0]].GetNames("gerund"));
                            }
                        }
                    }
                    else throw new Exception($"Unknown hint variable {variable}");
                    if (upper)
                    {
                        value = value[0].ToString().ToUpperInvariant() + value.Substring(1);
                    }
                    text = text.Replace($"({variable})", value);
                }
                if (printText) Console.WriteLine(text + "\n");
                if (hint.ID < 10000)
                {
                    itemDesc[hint.ID] = text;
                }
                else
                {
                    eventText[hint.ID] = text;
                }
            }
            AreaHint mostSpecificArea(string name)
            {
                AreaHint selected = null;
                foreach (AreaHint area in hints.Areas)
                {
                    if (area.EarlyAreas.Contains(name))
                    {
                        if (selected == null || area.Parents.Contains(selected))
                        {
                            selected = area;
                        }
                    }
                }
                return selected;
            }
            T pop<T>(List<T> list)
            {
                T ret = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                return ret;
            }

            // In priority order:
            // Item hints: Find item at (type) and (location). Always filled in. 1 of these.
            HashSet<ItemKey> exactKey = new HashSet<ItemKey>();
            foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("itemhint")).ToList())
            {
                HintTemplate t = hint.Types["itemhint"];
                if (!ann.Items.TryGetValue(t.Req, out ItemKey key)) throw new Exception($"Unrecognized name {t.Req}");
                if (!itemPlacement.TryGetValue(key, out Placement placement)) continue;
                exactKey.Add(key);
                placement = placement.Copy();
                placement.AreaHint = mostSpecificArea(placement.Area);
                addHint(hint, t, placement);
                sources.Remove(hint);
            }

            // Location hints: Find (item/item category/nothing) at (location). Always filled in. 2 of these.
            foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("locationhint")).ToList())
            {
                HintTemplate t = hint.Types["locationhint"];
                List<string> reqAreas = t.Req.Split(' ').ToList();
                List<Placement> places = placements.Where(p => reqAreas.Contains(p.Area)).ToList();
                if (places.Count == 0 && hint.Types.TryGetValue("locationbadhint", out HintTemplate t2))
                {
                    addHint(hint, t2, null);
                }
                else
                {
                    Placement placement = places[0].Copy();
                    addHint(hint, t, placement);
                }
                sources.Remove(hint);
            }

            // Global negative hint: There is nothing at (type). Always include as many as applicable for likely types, treasure/chest/boss/miniboss/enemy/underwater/shop (but more like 1-2)
            List<HintType> present = placements.SelectMany(p => p.Types).Distinct().ToList();
            List<HintType> absent = new List<HintType>
            {
                HintType.Treasure, HintType.Chest, HintType.Boss, HintType.Miniboss, HintType.Enemy, HintType.Underwater, HintType.Shop
            }
            .Except(present).ToList();
            foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("badhint")).ToList())
            {
                if (absent.Count == 0) break;
                HintType type = pop(absent);
                HintTemplate t = hint.Types["badhint"];
                addHint(hint, t, new Placement
                {
                    Types = new List<HintType> { type },
                });
                sources.Remove(hint);
            }

            // Positive hint: There is (item/item category) at (type) in (location). Include one per key item, up to 13, and special items, currently 3.
            List<Placement> toPlace = placements.Where(p => !exactKey.Contains(p.Item)).ToList();
            foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("hint")).ToList())
            {
                if (toPlace.Count == 0) break;
                HintTemplate t = hint.Types["hint"];
                Placement placement = pop(toPlace);
                placement = placement.Copy();
                placement.AreaHint = mostSpecificArea(placement.Area);
                addHint(hint, t, placement);
                sources.Remove(hint);
            }

            // Lateness hint: There is (item) at (type) (late/early). Include one per location item, up to 6.
            toPlace = placements.Where(p => p.LateEligible).ToList();
            foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("hint")).ToList())
            {
                if (toPlace.Count == 0) break;
                HintTemplate t = hint.Types["hint"];
                Placement placement = pop(toPlace);
                placement = placement.Copy();
                placement.LateHint = true;
                string info = early.Contains(placement.Area) ? "an early game location" : (late.Contains(placement.Area) ? "a late game location" : "a mid game location");
                string prep = early.Contains(placement.Area) ? "in the early game" : (late.Contains(placement.Area) ? "in the late game" : "in the mid game");
                placement.AreaHint = new AreaHint { Name = info, Vague = info, VaguePrep = prep };
                addHint(hint, t, placement);
                sources.Remove(hint);
            }

            // So far, around 25 hints created, with ~10 left to go. At this point, pick randomly from either of these categories.
            List<AreaHint> withoutRedundantChildren(List<AreaHint> allAreas)
            {
                return allAreas.Where(area => area.Parents.Count == 0 || !area.Parents.Any(p => allAreas.Contains(p))).ToList();
            }
            List<AreaHint> areasWithoutPlacements(HintType type = HintType.None)
            {
                HashSet<string> importantAreas = new HashSet<string>(placements.Where(p => type == HintType.None || p.Types.Contains(type)).Select(p => p.Area));
                return hints.Areas.Where(h => (type == HintType.None || h.Types.Contains(type)) && !importantAreas.Overlaps(h.Areas)).ToList();
            }
            // Area negative hint: There is nothing in (location). Can be included for all such areas, but eliminate a hint if its parent also applies.
            Dictionary<HintType, List<AreaHint>> negativeHints = new Dictionary<HintType, List<AreaHint>>();
            List<AreaHint> unimportantAreas = areasWithoutPlacements();
            negativeHints[HintType.None] = withoutRedundantChildren(unimportantAreas);
            foreach (HintType noType in new[] { HintType.Boss, HintType.Miniboss, HintType.Treasure })
            {
                negativeHints[noType] = withoutRedundantChildren(areasWithoutPlacements(noType)).Except(unimportantAreas).ToList();
            }
            negativeHints[HintType.Enemy] = new List<AreaHint>();
            foreach (AreaHint allEnemy in negativeHints[HintType.Boss].Intersect(negativeHints[HintType.Miniboss]).ToList())
            {
                // Add 'no powerful enemy' hints if no boss and no miniboss.
                negativeHints[HintType.Enemy].Add(allEnemy);
                negativeHints[HintType.Boss].Remove(allEnemy);
                negativeHints[HintType.Miniboss].Remove(allEnemy);
            }
            List<Placement> negatives = new List<Placement>();
            foreach (KeyValuePair<HintType, List<AreaHint>> entry in negativeHints)
            {
                List<HintType> types = entry.Key == HintType.None ? new List<HintType>() : new List<HintType> { entry.Key };
                foreach (AreaHint area in entry.Value)
                {
                    negatives.Add(new Placement
                    {
                        AreaHint = area,
                        Types = types,
                    });
                }
            }
            // Area type hint: There is nothing at (type) in (location). Include this for treasure and for miniboss/boss/enemy, eliminating when parent (or parent type) applies.
            Shuffle(random, negatives);
            negatives = negatives.OrderBy(a => a.AreaHint.AreaRank).ToList();
            // If there are at least 4 negative hints, allow up to 3 of the remainder to become fodder.
            List<Hint> negativeHintTemplates = sources.Where(s => s.Types.ContainsKey("badhint") || s.Types.ContainsKey("badhint2")).ToList();
            if (negatives.Count >= 4 && negativeHintTemplates.Count > 4)
            {
                int cutoffIndex = Math.Max(negativeHintTemplates.Count - 3, 4);
                negativeHintTemplates.RemoveRange(cutoffIndex, negativeHintTemplates.Count - cutoffIndex);
            }
            foreach (Hint hint in negativeHintTemplates)
            {
                if (negatives.Count == 0) break;
                HintTemplate t = hint.Types.ContainsKey("badhint") ? hint.Types["badhint"] : hint.Types["badhint2"];
                Placement placement = pop(negatives);
                Placement otherPlacement = t.Type == "badhint2" && negatives.Count > 0 ? pop(negatives) : null;
                addHint(hint, t, placement, otherPlacement);
                sources.Remove(hint);
            }

            if (printText) Console.WriteLine($"{sources.Count} remaining hints: [{string.Join(", ", sources.Select(s => string.Join("/", s.Types.Keys)))}]");
            if (sources.Count > 0)
            {
                // At this point, pull in misc hints for somewhat useful items
                toPlace = categories[ItemCategory.HintFodder].Select(k => itemPlacement[k]).ToList();
                Shuffle(random, toPlace);
                foreach (Hint hint in sources.Where(s => s.Types.ContainsKey("hint")).ToList())
                {
                    if (toPlace.Count == 0) break;
                    HintTemplate t = hint.Types["hint"];
                    Placement placement = pop(toPlace);
                    placement = placement.Copy();
                    placement.AreaHint = mostSpecificArea(placement.Area);
                    addHint(hint, t, placement);
                    sources.Remove(hint);
                }
            }

            // Need to figure out which items are strictly required to beat the game
            // Also, for bad hints, find all strictly required items plus key items
            // List all locations which can have hints scoped to them. The entirety of Sunken Valley, or just lower/upper, or burrow
            // Most key items get minimum specificity
            // Required side area items will get early/lateness specificity when that applies
            // Skills/prosthetics will get maximum specificity, maybe even two for Mikiri

            // Mortal Blade is excluded, since it has its own explicit hint
            // Young Lord Bell Charm is excluded if earlyhirata
        }
        public class Placement
        {
            public ItemKey Item { get; set; }
            public string FullName { get; set; }
            public ItemCategory Category { get; set; }
            public bool LateHint { get; set; }
            public bool LateEligible { get; set; }
            public bool Important { get; set; }
            public string Area { get; set; }
            public AreaHint AreaHint { get; set; }
            public List<HintType> Types = new List<HintType>();

            public override string ToString() => $"{Category} {FullName} [in {Area} and {string.Join(", ", Types)}]";
            public Placement Copy()
            {
                Placement other = (Placement)MemberwiseClone();
                other.Types = other.Types.ToList();
                return other;
            }
        }
        public enum HintType
        {
            // Nothing in scope
            None,
            // Search for treasure (can optionally be chest and/or underwater)
            Treasure,
            // Search treasure chests (can optionally be underwater)
            Chest,
            // Defeat a boss
            Boss,
            // Defeat a miniboss
            Miniboss,
            // Defeat a powerful enemy (usually either boss or miniboss)
            Enemy,
            // Go fishing
            Carp,
            // Go underwater
            Underwater,
            // Go shopping
            Shop,
        }
        // Also, some locations can have custom quest descriptions? But this should probably be covered in location name
        public enum ItemCategory
        {
            RequiredItem,
            RequiredKey,
            RequiredAbility,
            ImportantTool,
            HintFodder,
            LatenessHints,
            ExcludeHints,
        }
        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");
        public class HintData
        {
            public List<TypeHint> Types = new List<TypeHint>();
            public List<ItemHint> ItemCategories = new List<ItemHint>();
            public List<ItemHintName> ItemNames = new List<ItemHintName>();
            public List<AreaHint> Areas = new List<AreaHint>();
            public List<Hint> Hints = new List<Hint>();
        }
        public class TypeHint
        {
            // A location type. It can be used 3 grammatical contexts
            public HintType Name { get; set; }
            public string Imperative { get; set; }
            public string Gerund { get; set; }
            public string Adjective { get; set; }
            public string Noun { get; set; }
            public List<string> GetNames(string type)
            {
                string name;
                if (type == "gerund") name = Gerund;
                else if (type == "imperative") name = Imperative;
                else if (type == "adjective") name = Adjective;
                else if (type == "noun") name = Noun;
                else throw new Exception($"Unknown hint config variable {Name} {type}");
                return new List<string> { name };
            }
        }
        public class ItemHint
        {
            public ItemCategory Name { get; set; }
            public string Text { get; set; }
            public string Includes { get; set; }
            public string IncludesName { get; set; }
        }
        public class ItemHintName
        {
            public string Name { get; set; }
            public string Vague { get; set; }
            public List<string> GetNames()
            {
                return phraseRe.Split(Vague).ToList();
            }
        }
        public class AreaHint
        {
            // An area name. Areas may overlap.
            // The name to use. When used as a prepositional phrase, "in" will be added in front
            public string Name { get; set; }
            // A location larger than this one. If negative hint applies to parent, use that instead, to avoid accidentally misleading.
            public string Parent { get; set; }
            // Space-separated list of included areas, may include a glob* at the end for prefix selection
            public string Includes { get; set; }
            // Areas to take out of the include list if globbing is used
            public string Excludes { get; set; }
            // Areas which are included in Includes, but only spawn at some later point
            // If this is a positive hint, it should not apply if the item is only in a later area.
            // If this is a negative hint, all areas should apply as per usual.
            // This is so we can e.g. avoid saying an item is in Hirata Estate if it's only in Hirata Estate Revisited,
            // but also avoid saying an item *isn't* in Hirata Estate when it's in Hirata Estate Revisited instead.
            public string LaterIncludes { get; set; }
            // Alternate names to use, either as nouns or as the full prepositional phrase
            public string Vague { get; set; }
            public string VaguePrep { get; set; }
            // Which types are present
            public string Present { get; set; }
            [YamlIgnore]
            public HashSet<string> Areas = new HashSet<string>();
            [YamlIgnore]
            public HashSet<string> LaterAreas = new HashSet<string>();
            [YamlIgnore]
            public HashSet<string> EarlyAreas = new HashSet<string>();
            [YamlIgnore]
            public HashSet<AreaHint> Parents = new HashSet<AreaHint>();
            [YamlIgnore]
            public List<HintType> Types = new List<HintType>();
            public int AreaRank => Types.Count();
            public List<string> GetNames(bool prep)
            {
                List<string> names = new List<string>();
                if (prep)
                {
                    if (Vague != null) names.AddRange(phraseRe.Split(Vague).Select(p => "in " + p));
                    if (VaguePrep != null) names.AddRange(phraseRe.Split(VaguePrep));
                }
                else
                {
                    if (Vague != null) names.AddRange(phraseRe.Split(Vague));
                }
                if (names.Count == 0)
                {
                    names.Add((prep ? "in " : "") + Name);
                }
                return names;
            }
        }
        public class Hint
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public List<HintTemplate> Versions { get; set; }
            [YamlIgnore]
            public Dictionary<string, HintTemplate> Types { get; set; }
            public bool HasInfix(string name) => Types.Keys.Any(k => k.Contains(name));
        }
        public class HintTemplate
        {
            public string Type { get; set; }
            public string Req { get; set; }
            public string MainPath { get; set; }
            public List<string> Text { get; set; }
        }
    }
}
