using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pidgin;
using Pidgin.Expression;
using YamlDotNet.Serialization;
using static Pidgin.Parser;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class AnnotationData
    {
        private static readonly Dictionary<string, int> LocationIndices = new Dictionary<string, int> { { "early", 0 }, { "mid", 1 }, { "late", 2 }, { "deadend", 2 } };

        private GameData game;
        private LocationData data;
        private List<ConfigAnnotation> configVars = new List<ConfigAnnotation>();

        // This is a lot of data okay
        // All annotated item slots
        public readonly Dictionary<LocationScope, SlotAnnotation> Slots = new Dictionary<LocationScope, SlotAnnotation>();
        // All areas
        public readonly Dictionary<string, AreaAnnotation> Areas = new Dictionary<string, AreaAnnotation>();
        // All events (boss fights and major game progress triggers)
        public readonly Dictionary<string, AreaAnnotation> Events = new Dictionary<string, AreaAnnotation>();
        // Areas that events belong to, when they uniquely belong to one
        public readonly Dictionary<string, string> EventAreas = new Dictionary<string, string>();
        public readonly Dictionary<string, List<string>> AreaEvents = new Dictionary<string, List<string>>();
        // The 'main' area for area aliases. To be phased out; combining areas automatically was a bad idea.
        public readonly Dictionary<string, string> AreaAliases = new Dictionary<string, string>();
        // All named items with logic associated with them.
        public readonly Dictionary<string, ItemAnnotation> Items = new Dictionary<string, ItemAnnotation>();
        // Contents of item groups by names, used for various purposes
        public readonly Dictionary<string, List<ItemKey>> ItemGroups = new Dictionary<string, List<ItemKey>>();
        // Mapping from item group names to hint group names used for them
        public readonly Dictionary<string, string> HintGroups = new Dictionary<string, string>();
        // All hint group names
        public readonly List<string> HintCategories = new List<string>();
        // All groups of items in order of priority
        public readonly List<ItemPriorityAnnotation> ItemPriority = new List<ItemPriorityAnnotation>();
        // Placement restrictions for various items, in relation to game progress
        public readonly Dictionary<ItemKey, PlacementRestrictionAnnotation> ItemRestrict = new Dictionary<ItemKey, PlacementRestrictionAnnotation>();
        // Forbidden placement flags for various items
        public readonly Dictionary<ItemKey, HashSet<string>> ExcludeTags = new Dictionary<ItemKey, HashSet<string>>();
        // Exclude tags for key items specifically
        public readonly HashSet<string> NoKeyTags = new HashSet<string>();
        // Exclude tags for quest items specifically
        public readonly HashSet<string> NoQuestTags = new HashSet<string>();
        // Eligible tags for race mode locations
        public readonly HashSet<string> RaceModeTags = new HashSet<string>();
        // Items which should be placed in race mode locations
        public readonly HashSet<ItemKey> RaceModeItems = new HashSet<ItemKey>();
        // Items which shouldn't be randomized
        public readonly HashSet<ItemKey> NorandomItems = new HashSet<ItemKey>();

        // Used for statistics and whatnot?
        public readonly SortedDictionary<string, List<LocationScope>> AllTags = new SortedDictionary<string, List<LocationScope>>();
        public readonly SortedDictionary<string, List<LocationScope>> AllAreas = new SortedDictionary<string, List<LocationScope>>();

        private static readonly List<string> ds3LocationOrder = new List<string> {
            "firelink", "highwall", "settlement", "farronkeep", "cathedral", "catacombs", "irithyll", "dungeon",
            "lothric", "archives", "archdragon", "ariandel", "dregheap", "ringedcity", "filianore", "kiln",
        };
        private static readonly List<string> sekiroLocationOrder = new List<string> {
            "ashinaoutskirts",
            "ashinareservoir",
            "ashinacastle",
            "hirata",
            "dungeon",
            "mibuvillage",
            "sunkenvalley",
            "senpou",
            "fountainhead"
        };
        private List<string> locationOrder;
        private Dictionary<string, int> locationIndex;

        public AnnotationData(GameData game, LocationData data)
        {
            this.game = game;
            this.data = data;
            locationOrder = game.Sekiro ? sekiroLocationOrder : ds3LocationOrder;
            locationIndex = Enumerable.Range(0, locationOrder.Count()).ToDictionary(i => locationOrder[i], i => i);
        }

        public void Load(RandomizerOptions options)
        {
            Annotations ann;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = game.NewAnnotationReader())
            {
                ann = deserializer.Deserialize<Annotations>(reader);
            }
            // Config vars
            foreach (ConfigAnnotation config in ann.Config)
            {
                config.UpdateOptions(options);
            }
            configVars = ann.Config;
            // Items
            ItemKey itemForAnnotation(ItemAnnotation item)
            {
                if (item.ID != null)
                {
                    string[] parts = item.ID.Split(':');
                    return new ItemKey((ItemType)int.Parse(parts[0]), int.Parse(parts[1]));
                }
                else
                {
                    return game.ItemForName(item.Name);
                }
            }
            List<string> hints = new List<string>();
            foreach (ConfigItemAnnotation configItems in ann.ConfigItems)
            {
                foreach (ItemAnnotation item in configItems.Items)
                {
                    if (item.ConfigName == null)
                    {
                        item.ConfigName = Regex.Replace(item.Name.ToLower(), @"[^a-z]", "");
                    }
                    item.Key = itemForAnnotation(item);
                    if (!configItems.NoConfigNames)
                    {
                        if (Items.ContainsKey(item.ConfigName)) throw new Exception($"Duplicate item under config name {item.ConfigName}");
                        Items[item.ConfigName] = item;
                    }
                    AddMulti(ItemGroups, configItems.GroupName, item.Key);
                }
                if (configItems.HintName != null)
                {
                    HintGroups[configItems.GroupName] = configItems.HintName;
                    hints.Add(configItems.HintName);
                }
            }
            HintCategories.AddRange(hints.Distinct().ToList());
            if (HintCategories.Count == 0)
            {
                // Fallback for DS3. TODO use these in config
                HintCategories.AddRange(new[] { "key items", "upgrade items", "quest items", "estus items" });
            }
            foreach (ItemPriorityAnnotation group in ann.ItemPriority)
            {
                group.Keys = new List<ItemKey>();
                if (group.Names != null)
                {
                    group.Keys.AddRange(group.Names.Select(name => game.ItemForName(name)));
                }
                if (group.Items != null)
                {
                    group.Keys.AddRange(group.Items.Select(item => itemForAnnotation(item)));
                }
                if (group.Includes != null)
                {
                    group.Keys.AddRange(group.Includes.Split(' ').SelectMany(s => ItemGroups[s]));
                }
                if (group.ExcludeTags != null)
                {
                    HashSet<string> exclude = new HashSet<string>(group.ExcludeTags.Split(' '));
                    if (options["unreliableenemyplacement"] && exclude.Contains("missable"))
                    {
                        // For StraySouls in DS3, enemies may be bugged out or not in their expected logical location
                        exclude.Add("enemy");
                        exclude.Add("miniboss");
                    }
                    if (exclude.Contains("missable") && group.Includes != "keyitems")
                    {
                        exclude.Add("until");
                    }
                    if (!options["dlc1"])
                    {
                        exclude.Add("dlc1");
                    }
                    if (group.Includes == "keyitems")
                    {
                        if (game.Sekiro)
                        {
                            // Random enemy key items are a bit unfair. Can make this based on options as well.
                            exclude.Add("enemy");
                            if (options["headlessignore"])
                            {
                                exclude.Add("headless");
                            }
                        }
                        NoKeyTags.UnionWith(exclude);
                    }
                    else if (group.Includes == "questitems")
                    {
                        NoQuestTags.UnionWith(exclude);
                    }
                    foreach (ItemKey key in group.Keys)
                    {
                        AddMulti(ExcludeTags, key, exclude);
                    }
                }
            }
            ItemPriority.AddRange(ann.ItemPriority.Where(priority => !priority.NoPriority));
            bool isSwitchDisabled(string sw)
            {
                return sw != null && !sw.Split(' ').All(opt => options[opt]);
            }
            foreach (PlacementRestrictionAnnotation placement in ann.PlacementRestrictions)
            {
                // If switch not enabled, ignore
                if (isSwitchDisabled(placement.Switch))
                {
                    continue;
                }
                if (placement.Name != null || placement.Item != null)
                {
                    placement.Key = placement.Name == null ? itemForAnnotation(placement.Item) : game.ItemForName(placement.Name);
                }
                else if (placement.Includes != null)
                {
                    List<ItemKey> items = ItemGroups[placement.Includes];
                    placement.Key = items[0];
                    placement.OtherKeys = items.Skip(1).ToList();
                }
                else throw new Exception();
                ItemRestrict[placement.Key] = placement;
            }
            RaceModeTags.UnionWith(new[] { "boss", "miniboss", "racemode" });
            if (!options["headlessignore"])
            {
                // Unless headless can contain key items, don't allow them to contain race mode locations either
                RaceModeTags.Add("headless");
            }
            if (ann.SpecialModes != null)
            {
                foreach (SpecialModeAnnotation group in ann.SpecialModes)
                {
                    bool raceMode = !isSwitchDisabled(group.RaceSwitch);
                    bool norandomMode = !isSwitchDisabled(group.NorandomSwitch);
                    if (!raceMode && !norandomMode)
                    {
                        continue;
                    }
                    HashSet<ItemKey> items = new HashSet<ItemKey>();
                    if (group.Names != null)
                    {
                        items.UnionWith(group.Names.Select(name => game.ItemForName(name)));
                    }
                    if (group.Items != null)
                    {
                        items.UnionWith(group.Items.Select(itemForAnnotation));
                    }
                    if (group.Includes != null)
                    {
                        items.UnionWith(ItemGroups[group.Includes]);
                    }
                    if (norandomMode)
                    {
                        NorandomItems.UnionWith(items);
                    }
                    else if (raceMode)
                    {
                        RaceModeItems.UnionWith(items);
                    }
                }
            }
            // Areas
            Parser<char, Expr> parser = ExprParser();
            string start = null;
            void parseReq(AreaAnnotation area)
            {
                if (area.Req == null)
                {
                    if (start != null)
                    {
                        throw new Exception($"{area.Name} and {start} both have no requirements");
                    }
                    area.ReqExpr = Expr.TRUE;
                    start = area.Name;
                }
                else
                {
                    area.ReqExpr = parser.ParseOrThrow(area.Req).Simplify();
                }
            }
            foreach (AreaAnnotation area in ann.Areas)
            {
                parseReq(area);
                Areas[area.Name] = area;
                if (game.Sekiro)
                {
                    // All areas are full areas in Sekiro.
                    AreaAliases[area.Name] = area.Name;
                    AllAreas[area.Name] = new List<LocationScope>();
                }
                else
                {
                    // In DS3, auto-combine areas which are connected to each other. This wasn't a great idea.
                    bool isAlias = area.ReqExpr != null && area.ReqExpr.FreeVars().Count == 1;
                    if (area.Aliases != null)
                    {
                        foreach (string alias in area.GetAliasNames())
                        {
                            AreaAliases[alias] = area.Name;
                        }
                    }
                    if (!isAlias)
                    {
                        AreaAliases[area.Name] = area.Name;
                        AllAreas[area.Name] = new List<LocationScope>();
                    }
                }
            }
            foreach (AreaAnnotation area in ann.Events)
            {
                parseReq(area);
                Events[area.Name] = area;
                // Update event areas
                SortedSet<string> frees = area.ReqExpr.FreeVars();
                if (frees.Count == 1 && Areas.ContainsKey(frees.First()))
                {
                    EventAreas[area.Name] = frees.First();
                    AddMulti(AreaEvents, frees.First(), area.Name);
                }
            }

            // Slots
            Dictionary<string, SlotAnnotation> strSlots = new Dictionary<string, SlotAnnotation>();
            foreach (SlotAnnotation slot in ann.Slots)
            {
                string key = slot.Key.Substring(slot.Key.IndexOf(',') + 1);
                strSlots[key] = slot;
            }
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                // Skip model keys for now, there is not much to configure
                if (entry.Key.Type == ScopeType.MODEL)
                {
                    continue;
                }
                string key = entry.Key.ToString();
                if (!strSlots.ContainsKey(key))
                {
                    // Warn
                    Console.WriteLine($"Warning: No annotation for slot {key}, with slots {string.Join(", ", entry.Value.Select(s => $"{s} at {string.Join(", ", data.Location(s).Keys)}"))}");
                    continue;
                }
                SlotAnnotation slot = strSlots[key];
                Slots[entry.Key] = slot;
                strSlots.Remove(key);
            }
            if (strSlots.Count() > 0)
            {
                Warn($"Keys [{string.Join(", ", strSlots.Keys)}] are in config but not in game. Remove them explicitly, so that data is not lost.");
            }

            // Simple post processing and validation
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in Slots)
            {
                LocationScope scope = entry.Key;
                SlotAnnotation slot = entry.Value;
                if (!Areas.TryGetValue(slot.Area, out AreaAnnotation areaAnn)) throw new Exception($"Slot {scope} has unknown area {slot.Area}");
                slot.AreaUntil = areaAnn.Until;
                slot.SetTags(scope.OnlyShops, options);
                foreach (string tag in slot.TagList)
                {
                    if (tag.Contains(':'))
                    {
                        string[] parts = tag.Split(':');
                        if (parts.Length != 2 || !Items.TryGetValue(parts[1], out ItemAnnotation tagItem)) throw new Exception($"Bad scoped tag {tag} in {scope}");
                        if (slot.TagItems == null) slot.TagItems = new Dictionary<string, List<ItemKey>>();
                        AddMulti(slot.TagItems, parts[0], tagItem.Key);
                    }
                    AddMulti(AllTags, tag, scope);
                }
                if (slot.QuestReqs != null)
                {
                    slot.ItemReqs = new List<ItemKey>();
                    slot.AreaReqs = new List<string>();
                    slot.FullAreaReqs = new List<string>();
                    foreach (string questReq in slot.QuestReqs.Split(' '))
                    {
                        if (Items.ContainsKey(questReq))
                        {
                            slot.ItemReqs.Add(Items[questReq].Key);
                        }
                        else if (Areas.ContainsKey(questReq))
                        {
                            slot.AreaReqs.Add(AreaAliases[questReq]);
                            slot.FullAreaReqs.Add(questReq);
                        }
                        else throw new Exception($"QuestReq {questReq} is neither an item or area");
                    }
                }
                // Include area as part of key items counting
                string effectiveArea = slot.Area;
                // In the special case of quests at the start with dependencies elsewhere, don't treat those as available after Firelink, for purpose of key item counting
                if ((slot.Area == "firelink" || slot.Area == "ashinaoutskirts_temple") && slot.QuestReqs != null && slot.AreaReqs.Count > 0)
                {
                    effectiveArea = slot.AreaReqs[0];
                }
                if (!game.Sekiro && !slot.TagList.Contains("boss") && (slot.TagList.Contains("mid") || slot.TagList.Contains("late")))
                {
                    Areas[effectiveArea].HasProgression = true;
                }
                if (AreaAliases[effectiveArea] != effectiveArea)
                {
                    effectiveArea = AreaAliases[effectiveArea];
                }
                if (slot.Area != effectiveArea)
                {
                    slot.BaseArea = effectiveArea;
                }
                AddMulti(AllAreas, slot.GetArea(), scope);
            }
        }
        public void AddSpecialItems()
        {
            // Add special unique items into game
            foreach (ItemKey addItem in ItemGroups["add"])
            {
                data.AddLocationlessItem(addItem);
            }
        }
        public void AddEnemyLocations(EnemyLocations enemies)
        {
            // Currently, enemy location processing is required for skill/prosthetics progression, to add requirements for enemies
            bool debug = false;
            List<string> getLocations(string enemy)
            {
                List<string> ret = new List<string>();
                List<string> targets;
                if (enemies == null)
                {
                    targets = new List<string> { enemy };
                }
                else if (!enemies.Target.TryGetValue(enemy, out targets))
                {
                    return ret;
                }
                foreach (string target in targets)
                {
                    if (EventAreas.TryGetValue(target, out string area))
                    {
                        ret.Add(area);
                    }
                    if (debug) Console.WriteLine($"  For {enemy} -> {target} -> {area}");
                }
                return ret;
            }
            string processLocations(string locs)
            {
                if (locs == null) return null;
                if (!locs.Contains("_location")) return locs;
                string[] reqs = locs.Split(' ');
                string newLocs = string.Join(" ", reqs.SelectMany(r => r.EndsWith("_location") ? getLocations(r.Substring(0, r.Length - 9)) : new List<string> { r }));
                if (debug) Console.WriteLine($"Replacing '{locs}' -> '{newLocs}'");
                return newLocs.Length == 0 ? null : newLocs;
            }
            void processLocationList(List<PlacementSlotAnnotation> slots)
            {
                if (slots == null) return;
                slots.RemoveAll(slot =>
                {
                    slot.Before = processLocations(slot.Before);
                    slot.UpTo = processLocations(slot.UpTo);
                    slot.After = processLocations(slot.After);
                    return slot.Before == null && slot.UpTo == null && slot.After == null;
                });
            }
            foreach (PlacementRestrictionAnnotation restrict in ItemRestrict.Values)
            {
                processLocationList(restrict.Unique);
                // TODO: How does this affect DS3?
                if (restrict.Unique.Count == 0) restrict.Unique = null;
                processLocationList(restrict.Drop);
                processLocationList(restrict.Shop);
            }
        }
        public SlotAnnotation Slot(LocationScope scope)
        {
            if (Slots.ContainsKey(scope))
            {
                return Slots[scope];
            }
            // We don't really care where all of the models are, right now, although that can be part of annotation file in the future.
            // Just make something up
            HashSet<string> locationSet = new HashSet<string>();
            foreach (SlotKey key in data.Location(scope))
            {
                ItemLocation loc = data.Location(key);
                locationSet.UnionWith(loc.GetLocations());
            }
            string location = locationOrder[locationSet.Select(loc => AreaAliases.TryGetValue(loc, out string b) ? b : loc).Select(loc => locationIndex[loc]).DefaultIfEmpty().Min()];
            SlotAnnotation slot = new SlotAnnotation
            {
                Area = location,
            };
            slot.SetTags(scope.OnlyShops, null);
            return slot;
        }

        private static readonly Dictionary<string, string> TagHints = new Dictionary<string, string>()
        {
            { "outoftheway", "out of the way" },
            { "hidden", "hidden" },
            { "unfair", "unfair" },
            { "veryunfair", "very unfair" },
            { "miniboss", "requires defeating difficult enemy" },
            { "boss", "requires defeating boss" },
        };
        private static readonly List<string> LocationHint = new List<string> { "Early in", "Midway through", "Late in" };
        private string FullArea(string area)
        {
            if (game.Sekiro && !Areas.ContainsKey(area)) return area;
            if (!Areas.ContainsKey(area)) throw new Exception($"Unknown area {area}");
            return Areas[area].Text;
        }
        public string GetLocationHint(SlotKey key, SortedSet<string> specialLocation=null)
        {
            Func<string, string> capitalize = s => $"{s[0]}".ToUpper() + s.Substring(1);
            ItemLocation loc = data.Location(key);
            LocationScope scope = loc.LocScope;
            List<SlotKey> sources = data.Location(scope);
            string specialText = "";
            if (specialLocation != null && specialLocation.Count > 0) {
                specialText = $", available after {string.Join("/", specialLocation.Select(a => FullArea(a)))}";
            }
            string text;
            if (Slots.ContainsKey(scope))
            {
                SlotAnnotation slot = Slots[scope];
                int location = slot.GetAreaIndex().Item1;
                if (specialLocation == null && Areas[slot.Area].HasProgression)
                {
                    text = $"{LocationHint[location]} {FullArea(slot.Area)}";
                }
                else
                {
                    text = $"In {FullArea(slot.Area)}{specialText}";
                }
                SortedSet<string> tagHints = new SortedSet<string>(slot.TagList.Where(t => TagHints.ContainsKey(t)).Select(t => TagHints[t]));
                // This is maybe too much hint
                if (tagHints.Count > 0 && false)
                {
                    text = $"{text} {capitalize(string.Join("; ", tagHints))}";
                }
            }
            else
            {
                SortedSet<string> locs = loc.GetLocations();
                text = $"In {(locs.Count == 0 ? "???" : string.Join(", ", locs.Select(a => FullArea(a))))}{specialText}";
            }
            return text;
        }

        public string GetLocationDescription(SlotKey key, List<LocationKey> locations=null, bool raceModeOnly=false)
        {
            ItemLocation loc = data.Location(key);
            LocationScope scope = loc.LocScope;
            // TODO: Revisit this for DS3
            List<SlotKey> sources = game.Sekiro ? new List<SlotKey> { key } : data.Location(scope);
            Func<bool, string> autoText = addPlace =>
            {
                SortedSet<string> models = new SortedSet<string>();
                SortedSet<string> locs = new SortedSet<string>();
                foreach (SlotKey sourceKey in sources)
                {
                    models.UnionWith(loc.Keys.SelectMany(k => k.Entities.Select(e => game.EntityName(e))));
                    locs.UnionWith(loc.Keys.SelectMany(k => k.Entities.Select(e => e.MapName)));
                }
                string auto = $"{scope.Description()} from {(models.Count == 0 ? "???" : string.Join(", ", models))}";
                if (addPlace) auto = $"{auto} in {(locs.Count == 0 ? "???" : string.Join(", ", locs.Select(a => FullArea(a))))}";
                return auto;
            };
            string text;
            if (Slots.ContainsKey(scope))
            {
                SlotAnnotation slot = Slots[scope];
                bool marked = slot.TagList.Contains("XX") || (slot.Text != null && slot.Text.Contains("XX"));
                text = $" in {FullArea(slot.Area)}:{(marked ? " XX" : "")} {(slot.Text == "auto" || slot.Text == null ? autoText(false) : slot.Text.TrimEnd(new char[] { '.' }))}";
                if (raceModeOnly && !slot.HasAnyTags(RaceModeTags)) return null;

            }
            else
            {
                if (raceModeOnly) return null;
                text = $": {autoText(true)}";
            }
            List<string> original = sources.Select(k => game.Name(k.Item)).ToList();
            if (original.Count > 5)
            {
                original = original.Take(5).Concat(new[] { "etc" }).ToList();
            }
            return $"{text}. {(original.Count == 1 ? "Replaces" : "In the spot of")} {string.Join(", ", original)}.";
        }

        public enum UniqueCategory { KEY_LOT, KEY_SHOP, QUEST_LOT, QUEST_SHOP };
        public Dictionary<LocationScope, (UniqueCategory, int)> GetUniqueCounts()
        {
            Dictionary<LocationScope, (UniqueCategory, int)> locationSlots = new Dictionary<LocationScope, (UniqueCategory, int)>();
            // TODO: Maybe this also puts key items in race mode locations? At least from counting perspective only
            bool isRaceMode = RaceModeItems.Count > 0;
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope scope = entry.Key;
                if (!Slots.ContainsKey(scope) || scope.Type == ScopeType.MODEL || scope.UniqueId <= 0)
                {
                    continue;
                }
                SlotAnnotation existing = Slots[scope];
                // Quick tag check
                bool noKeyTags = existing.HasAnyTags(NoKeyTags);
                bool noQuestTags = existing.HasAnyTags(NoQuestTags);
                if (noKeyTags && noQuestTags) continue;
                // Check for all items not-randomized
                List<ItemKey> maybeRandom = entry.Value.Select(slot => slot.Item).Except(NorandomItems).ToList();
                if (maybeRandom.Count == 0) continue;
                if (existing.TagItems != null && existing.TagItems.TryGetValue("norandom", out List<ItemKey> norandoms) && maybeRandom.Except(norandoms).Count() == 0)
                {
                    continue;
                }
                UniqueCategory category;
                int count;
                if (!noKeyTags && (!isRaceMode || existing.HasAnyTags(RaceModeTags)))
                {
                    category = scope.OnlyShops ? UniqueCategory.KEY_SHOP : UniqueCategory.KEY_LOT;
                    count = isRaceMode ? 1 : data.Location(scope).Count;
                }
                else if (!noQuestTags && existing.QuestReqs == null)
                {
                    category = scope.OnlyShops ? UniqueCategory.QUEST_SHOP : UniqueCategory.QUEST_LOT;
                    count = data.Location(scope).Count;
                }
                else
                {
                    continue;
                }
                locationSlots[scope] = (category, count);
            }
            return locationSlots;
        }

        public Dictionary<string, bool> GetConfig(IEnumerable<string> enabled)
        {
            SortedSet<string> enabledSet = new SortedSet<string>(enabled);
            Dictionary<string, bool> config = new Dictionary<string, bool>();
            foreach (ConfigAnnotation vars in configVars)
            {
                vars.UpdateConfig(config, enabledSet);
            }
            if (enabledSet.Count() != 0)
            {
                // These are options which are passed in
            }
            return config;
        }

        public class Annotations
        {
            public List<ConfigAnnotation> Config { get; set; }
            public List<ConfigItemAnnotation> ConfigItems { get; set; }
            public List<PlacementRestrictionAnnotation> PlacementRestrictions { get; set; }
            public List<SpecialModeAnnotation> SpecialModes { get; set; }
            public List<ItemPriorityAnnotation> ItemPriority { get; set; }
            public List<AreaAnnotation> Events { get; set; }
            public List<AreaAnnotation> Areas { get; set; }
            public List<SlotAnnotation> Slots { get; set; }
            public Annotations()
            {
                Areas = new List<AreaAnnotation>();
                ConfigItems = new List<ConfigItemAnnotation>();
                Slots = new List<SlotAnnotation>();
            }
        }
        public class SpecialModeAnnotation
        {
            public string RaceSwitch { get; set; }
            public string NorandomSwitch { get; set; }
            public string Includes { get; set; }
            public List<string> Names { get; set; }
            public List<ItemAnnotation> Items { get; set; }
        }
        public class ItemPriorityAnnotation
        {
            public bool NoPriority { get; set; }
            public int PriorityByCount { get; set; }
            public string Includes { get; set; }
            public string ExcludeTags { get; set; }
            public List<string> Names { get; set; }
            public List<ItemAnnotation> Items { get; set; }
            [YamlIgnore]
            public List<ItemKey> Keys { get; set; }
        }
        public class PlacementRestrictionAnnotation
        {
            public string Name { get; set; }
            public ItemAnnotation Item { get; set; }
            public string Includes { get; set; }
            public string Switch { get; set; }
            public List<PlacementSlotAnnotation> Unique { get; set; }
            public List<PlacementSlotAnnotation> Shop { get; set; }
            public List<PlacementSlotAnnotation> Drop { get; set; }
            [YamlIgnore]
            public ItemKey Key { get; set; }
            [YamlIgnore]
            public List<ItemKey> OtherKeys { get; set; }
        }
        public class PlacementSlotAnnotation
        {
            public int Amount { get; set; }
            public string Before { get; set; }
            public string UpTo { get; set; }
            public string After { get; set; }
            public PlacementSlotAnnotation()
            {
                this.Amount = -1;
            }
            public HashSet<string> AllowedAreas(Dictionary<string, HashSet<string>> includedAreas, bool debug=false)
            {
                debug = Before == "guardianape senpou_sanctum";
                List<HashSet<string>> requirements = new List<HashSet<string>>();
                Func<string, IEnumerable<string>> getAreasUpTo = loc =>
                {
                    if (!includedAreas.ContainsKey(loc)) throw new Exception($"Unknown area {loc} in {this}");
                    // If unused areas, consider this to mean 'all areas'
                    if (includedAreas[loc].Count == 0)
                    {
                        return includedAreas.Where(e => e.Value.Count > 0).Select(e => e.Key);
                    }
                    return includedAreas[loc];
                };
                if (Before != null)
                {
                    foreach (string loc in Before.Split(' '))
                    {
                        HashSet<string> locs = new HashSet<string>(getAreasUpTo(loc).Where(a => !includedAreas[a].Contains(loc)));
                        if (debug) Console.WriteLine($"Before: {loc} -> {string.Join(",", locs)}. Out of {string.Join(",", getAreasUpTo(loc))}");
                        requirements.Add(locs);
                    }
                }
                if (UpTo != null)
                {
                    foreach (string loc in UpTo.Split(' '))
                    {
                        HashSet<string> locs = new HashSet<string>(getAreasUpTo(loc));
                        if (debug) Console.WriteLine($"UpTo: {loc} -> {string.Join(",", locs)}");
                        requirements.Add(locs);
                    }
                }
                if (After != null)
                {
                    HashSet<string> after = new HashSet<string>();
                    foreach (string loc in After.Split(' '))
                    {
                        if (!includedAreas.ContainsKey(loc)) throw new Exception($"Unknown area {loc} in {this}");
                        if (includedAreas[loc].Count == 0) continue;
                        after.UnionWith(includedAreas.Where(e => e.Value.Contains(loc)).Select(e => e.Key));
                        if (debug) Console.WriteLine($"After: {loc} -> {string.Join(",", after)}");
                    }
                    requirements.Add(after);
                }
                if (requirements.Count == 0) throw new Exception($"No location data for {this}");
                HashSet<string> allLocs = new HashSet<string>(requirements[0]);
                for (int i = 1; i < requirements.Count; i++)
                {
                    allLocs.IntersectWith(requirements[i]);
                }
                if (debug) Console.WriteLine($"Final locations of {this}: {string.Join(",", allLocs)}");
                return allLocs;
            }
            public override string ToString() => $"Slot[{Amount}] Before [{Before}] UpTo [{UpTo}] After [{After}]";
        }
        public class ConfigItemAnnotation
        {
            public string GroupName { get; set; }
            public string HintName { get; set; }
            public bool NoConfigNames { get; set; }
            public List<ItemAnnotation> Items { get; set; }
        }
        public class ItemAnnotation
        {
            public string ConfigName { get; set; }
            public string Name { get; set; }
            public string ID { get; set; }
            [YamlIgnore]
            public ItemKey Key { get; set; }
        }
        public class SlotAnnotation
        {
            public string Key { get; set; }
            public List<string> DebugText { get; set; }
            public string Text { get; set; }
            public string Area { get; set; }
            public string Tags { get; set; }
            public string QuestReqs { get; set; }
            public string Event { get; set; }
            public string Until { get; set; }
            [YamlIgnore]
            public HashSet<string> TagList { get; set; }
            [YamlIgnore]
            public Dictionary<string, List<ItemKey>> TagItems { get; set; }
            [YamlIgnore]
            public string AreaUntil { get; set; }
            // TODO: Replace uses with TagList directly.
            public HashSet<string> GetTags() => TagList;
            public void SetTags(bool shopOnly, RandomizerOptions opt)
            {
                string tagsStr = Tags;
                if (tagsStr != null && tagsStr.Contains("aaaaa"))
                {
                    tagsStr = "missable";
                }
                if (tagsStr == null || tagsStr == "")
                {
                    TagList = new HashSet<string>();
                }
                else
                {
                    TagList = new HashSet<string>(tagsStr.Split(' '));
                }
                // Add synthetic tag for shops - mainly to control when items are available unlimited. If not already present
                if (!TagList.Contains("shop") && !TagList.Contains("noshop"))
                {
                    TagList.Add(shopOnly ? "shop" : "noshop");
                }
                if (Until != null || AreaUntil != null)
                {
                    if (Until == "always")
                    {
                        // Until always allows key item logic
                    }
                    else
                    {
                        TagList.Add("until");
                        // 'until' specified on individual items (vs the whole area) means they are not part of key item logic
                        if (Until != null)
                        {
                            TagList.Add("missable");
                        }
                    }
                }
                // Boss implies late, unless specified otherwise
                if (TagList.Contains("boss") && !TagList.Contains("early") && !TagList.Contains("mid"))
                {
                    TagList.Add("late");
                }
            }
            [YamlIgnore]
            public List<ItemKey> ItemReqs { get; set; }
            [YamlIgnore]
            public List<string> AreaReqs { get; set; }
            [YamlIgnore]
            public List<string> FullAreaReqs { get; set; }

            public (int, int) GetAreaIndex()
            {
                int explicitLocation = TagList.Where(t => LocationIndices.ContainsKey(t)).Select(t => LocationIndices[t]).DefaultIfEmpty().Max();
                return (explicitLocation, 3);
            }
            public string GetArea()
            {
                // TODO: Make sure all uses of area are aligned on alias vs not
                if (BaseArea != null)
                {
                    return BaseArea;
                }
                if (Area == null || Area == "")
                {
                    // Probably shouldn't happen. TODO: check this for DS3
                    throw new Exception($"No area for slot {Key}: {Text}");
                    // return "firelink";
                }
                return Area;
            }

            // Returns if has any of the given tags. If given empty set, returns false.
            public bool HasAnyTags(ISet<string> tags)
            {
                return GetTags().Any(t => tags.Contains(t));
            }

            [YamlIgnore]
            public string BaseArea { get; set; }
        }
        public class AreaAnnotation
        {
            public string Name { get; set; }
            public string Text { get; set; }
            public string DetailedText { get; set; }
            public string Req { get; set; }
            public string Until { get; set; }
            public string WeightBase { get; set; }
            public string AlwaysBefore { get; set; }
            public List<string> Aliases { get; set; }
            [YamlIgnore]
            public Expr ReqExpr { get; set; }
            [YamlIgnore]
            public bool HasProgression { get; set; }

            private Dictionary<(string, int), int> AliasRankings = new Dictionary<(string, int), int>();
            private SortedSet<string> AliasNames = new SortedSet<string>();
            public (int, int) GetSubAreaIndex(string subArea, int locationIndex)
            {
                if (AliasRankings.Count == 0)
                {
                    ParseAliases();
                }
                (string, int) loc = (subArea, locationIndex);
                if (!AliasRankings.ContainsKey(loc)) throw new Exception($"Unknown subarea {subArea} for {Name}: only [{string.Join(", ", AliasNames)}]");
                return (AliasRankings[loc], Aliases.Count);
            }
            public SortedSet<string> GetAliasNames()
            {
                if (AliasNames.Count == 0 && Aliases.Count > 0)
                {
                    ParseAliases();
                }
                return AliasNames;
            }
            private void ParseAliases()
            {
                for (int i = 0; i < Aliases.Count; i++)
                {
                    foreach (string part in Regex.Split(Aliases[i], ", "))
                    {
                        string[] names = part.Split(' ');
                        if (names.Length != 2 || names.Any(name => name == "")) throw new Exception($"Badly formatted alias {Aliases[i]} in {Name}");
                        string subarea = names[0];
                        string loc = names[1];
                        if (subarea != Name)
                        {
                            AliasNames.Add(subarea);
                        }
                        AliasRankings[(subarea, LocationIndices[loc])] = i;
                    }
                }
            }
        }
        public class ConfigAnnotation
        {
            public string Opt { get; set; }
            public List<string> Oneof { get; set; }
            public string Num { get; set; }
            public void UpdateOptions(RandomizerOptions options)
            {
                // Select first oneof value if none selected
                if (Oneof != null && Oneof.Count > 0)
                {
                    if (!Oneof.Any(opt => options[opt]))
                    {
                        foreach (string opt in Oneof)
                        {
                            options[opt] = false;
                        }
                        options[Oneof[0]] = true;
                    }
                }
            }
            public void UpdateConfig(Dictionary<string, bool> config, SortedSet<string> enabled)
            {
                if (Opt != null)
                {
                    config[Opt] = enabled.Contains(Opt);
                    enabled.Remove(Opt);
                }
                if (Oneof != null && Oneof.Count() > 0)
                {
                    string chosen = null;
                    foreach (string opt in Oneof)
                    {
                        if (chosen == null)
                        {
                            chosen = opt;
                        }
                        if (enabled.Contains(opt))
                        {
                            chosen = opt;
                        }
                    }
                    foreach (string opt in Oneof)
                    {
                        config[opt] = opt == chosen;
                        enabled.Remove(opt);
                    }
                }
            }
        }

        private static Parser<char, T> Token<T>(Parser<char, T> parser)
        {
            return Try(parser).Before(SkipWhitespaces);
        }
        private static Parser<char, Func<Expr, Expr, Expr>> Binop(string name, bool every)
        {
            return Token(String(name)).Select<Func<Expr, Expr, Expr>>(type => (l, r) => new Expr(new List<Expr> { l, r }, every, null));
        }
        private static Parser<char, Expr> ExprParser()
        {
            Parser<char, Expr> ident = Token(Letter.Then(LetterOrDigit.Or(Char('_')).ManyString(), (h, t) => h + t))
                .Select<Expr>(name => Expr.Named(name));
            return ExpressionParser.Build<char, Expr>(
                expr => (
                    OneOf(
                        ident,
                        expr.Between(Token(String("(")), Token(String(")")))
                    ),
                    new[] {
                        Operator.InfixL(Binop("AND", true)),
                        Operator.InfixL(Binop("OR", false)),
                    }
                )
            );
        }
        public class Expr
        {
            public static readonly Expr TRUE = new Expr(new List<Expr>(), true, null);
            public static readonly Expr FALSE = new Expr(new List<Expr>(), false, null);
            private readonly List<Expr> exprs;
            private readonly bool every;
            private readonly string name;
            public Expr(List<Expr> exprs, bool every = true, string name = null)
            {
                if (exprs.Count() > 0 && name != null) throw new Exception("Incorrect construction");
                this.exprs = exprs;
                this.every = every;
                this.name = name;
            }
            public static Expr Named(string name)
            {
                return new Expr(new List<Expr>(), true, name);
            }
            public bool IsTrue()
            {
                return name == null && exprs.Count() == 0 && every;
            }
            public bool IsFalse()
            {
                return name == null && exprs.Count() == 0 && !every;
            }
            public SortedSet<string> FreeVars()
            {
                if (name != null)
                {
                    return new SortedSet<string> { name };
                }
                return new SortedSet<string>(exprs.SelectMany(e => e.FreeVars()));
            }
            public bool Needs(string check)
            {
                if (check == name)
                {
                    return true;
                }
                if (every)
                {
                    return exprs.Any(e => e.Needs(check));
                }
                else
                {
                    return exprs.All(e => e.Needs(check));
                }
            }
            public Expr Substitute(Dictionary<string, Expr> config)
            {
                if (name != null)
                {
                    if (config.ContainsKey(name))
                    {
                        return config[name];
                    }
                    return this;
                }
                return new Expr(exprs.Select(e => e.Substitute(config)).ToList(), every);
            }
            public Expr Flatten(Func<string, IEnumerable<string>> nameMapper)
            {
                if (name != null)
                {
                    // public Expr(List<Expr> exprs, bool every=true, string name=null)
                    return new Expr(nameMapper(name).Select(n => Expr.Named(n)).ToList(), true);
                }
                return null;
            }
            public int Count(Func<string, int> func)
            {
                if (name != null)
                {
                    return func(name);
                }
                IEnumerable<int> subcounts = exprs.Select(e => e.Count(func));
                return every ? subcounts.Sum() : subcounts.Max();
            }
            public Expr Simplify()
            {
                if (name != null)
                {
                    return this;
                }
                List<Expr> newExprs = new List<Expr>();
                HashSet<string> seen = new HashSet<string>();
                foreach (Expr e in exprs)
                {
                    Expr expr = e.Simplify();
                    if (expr.name != null)
                    {
                        if (seen.Contains(expr.name)) continue;
                        seen.Add(expr.name);
                        newExprs.Add(expr);
                    }
                    else if (every == expr.every)
                    {
                        newExprs.AddRange(expr.exprs);
                    }
                    else
                    {
                        if (expr.exprs.Count() == 0)
                        {
                            // false in AND condition, or true in OR condition, overrides everything else
                            return expr.every ? TRUE : FALSE;
                        }
                        newExprs.Add(expr);
                    }
                }
                if (newExprs.Count() == 1)
                {
                    return newExprs[0];
                }
                return new Expr(newExprs, every);
            }
            public override string ToString()
            {
                if (name != null)
                {
                    return name;
                }
                if (exprs.Count() == 0)
                {
                    return every ? "true" : "false";
                }
                if (every)
                {
                    return "(" + string.Join(" AND ", exprs) + ")";
                }
                else
                {
                    return "(" + string.Join(" OR ", exprs) + ")";
                }
            }
        }

        public void Save(bool initial=false)
        {
            Annotations ann = new Annotations();
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope scope = entry.Key;
                // Skip model keys for now, there is not much to configure
                if (scope.Type == ScopeType.MODEL)
                {
                    continue;
                }
                SlotAnnotation existing = Slots.ContainsKey(scope) ? Slots[scope] : null;
                // Generate debug text and keys afresh no matter what
                List<string> debugText = new List<string>();
                SortedSet<string> locationSet = new SortedSet<string>();
                SortedSet<string> models = new SortedSet<string>();
                foreach (SlotKey key in entry.Value)
                {
                    ItemLocation loc = data.Location(key);
                    debugText.Add($"{game.Name(key.Item)} - {loc}");
                    locationSet.UnionWith(loc.GetLocations());
                    models.UnionWith(loc.Keys.SelectMany(k => k.Entities.Select(e => game.EntityName(e))));
                }
                List<int> locations = locationSet.Select(loc => locationIndex[loc]).ToList();
                locations.Sort();
                // First area that's not firelink, if in multiple areas
                int firstLoc = locations.Count() == 0 ? 99 : locations[locations[0] == 0 && locations.Count() > 1 ? 1 : 0];

                if (debugText.Count() > 1 && (locations.Count() > 1 || models.Count() > 1))
                {
                    debugText.Insert(0, $"{entry.Key.Description()} from [{string.Join(", ", models)}] in [{string.Join(", ", locations.Select(loc => locationOrder[loc]))}]");
                }
                SlotAnnotation slot = new SlotAnnotation
                {
                    Key = $"{firstLoc.ToString("00")},{entry.Key}",
                    DebugText = debugText,
                    Area = firstLoc == 99 ? "" : locationOrder[firstLoc],
                };
                if (existing != null)
                {
                    if (existing.Area != null)
                    {
                        slot.Area = existing.Area;
                    }
                    if (existing.Tags != null)
                    {
                        slot.Tags = existing.Tags;
                    }
                    if (existing.Text != null)
                    {
                        slot.Text = existing.Text;
                    }
                }
                else if (initial)
                {
                    slot.Tags = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                    slot.Text = "auto";
                }
                ann.Slots.Add(slot);
            }
            ann.Slots.Sort((a, b) => a.Key.CompareTo(b.Key));
            ISerializer serializer = new SerializerBuilder()
                .DisableAliases()
                .Build();
            Console.WriteLine(serializer.Serialize(ann));
        }
    }
}
