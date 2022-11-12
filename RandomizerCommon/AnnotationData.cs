using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using Pidgin;
using Pidgin.Expression;
using YamlDotNet.Serialization;
using static Pidgin.Parser;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;
using System.Numerics;

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
        // All named items with logic associated with them.
        public readonly Dictionary<string, ItemKey> Items = new Dictionary<string, ItemKey>();
        // Contents of item groups by names, used for various purposes
        // Required ones: keyitems, questitems, remove
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
        // Exclude tags for key items specifically. Used for counting purposes and ash placement.
        public readonly HashSet<string> NoKeyTags = new HashSet<string>();
        // Exclude tags for quest items specifically. Used for counting purposes.
        private readonly HashSet<string> NoQuestTags = new HashSet<string>();
        // Exclude tags for race mode items. Used for exclude tags
        private readonly HashSet<string> NoRaceModeTags = new HashSet<string>();
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
        private Dictionary<string, List<string>> mapIdAreas = new Dictionary<string, List<string>>();

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
            string annPath = $@"{game.Dir}\Base\annotations.txt";
            using (var reader = File.OpenText(annPath))
            {
                ann = deserializer.Deserialize<Annotations>(reader);
            }
            string slotPath = $@"{game.Dir}\Base\itemslots.txt";
            if (File.Exists(slotPath))
            {
                if (ann.Slots.Count > 0) throw new Exception($"Internal error: Item slots defined in {annPath}:");
                Annotations slotAnn;
                using (var reader = File.OpenText(slotPath))
                {
                    slotAnn = deserializer.Deserialize<Annotations>(reader);
                }
                ann.Slots = slotAnn.Slots;
            }

            // Config vars
            foreach (ConfigAnnotation config in ann.Config)
            {
                config.UpdateOptions(options);
            }
            configVars = ann.Config;
            // Items
            List<ItemKey> itemsForAnnotation(ItemAnnotation item)
            {
                if (item.ID != null)
                {
                    string[] parts = item.ID.Split(':');
                    ItemKey key = new ItemKey((ItemType)int.Parse(parts[0]), int.Parse(parts[1]));
                    if (item.EndID == null)
                    {
                        return new List<ItemKey> { key };
                    }
                    else
                    {
                        parts = item.EndID.Split(':');
                        ItemKey end = new ItemKey((ItemType)int.Parse(parts[0]), int.Parse(parts[1]));
                        if (key.Type != end.Type || key.ID > end.ID)
                        {
                            throw new Exception($"Invalid item range {key} {end}");
                        }
                        List<ItemKey> ret = new List<ItemKey>();
                        if (end.ID - key.ID < 100)
                        {
                            for (int i = key.ID; i <= end.ID; i++)
                            {
                                ItemKey cand = new ItemKey(key.Type, i);
                                if (game.Names().ContainsKey(cand))
                                {
                                    ret.Add(cand);
                                }
                            }
                        }
                        else
                        {
                            // This is a SortedDictionary but it's not easily binary-searchable by index afaik
                            foreach (KeyValuePair<ItemKey, string> entry in game.Names())
                            {
                                ItemKey cand = entry.Key;
                                if (cand.Type == key.Type && cand.ID >= key.ID && cand.ID <= end.ID)
                                {
                                    ret.Add(cand);
                                }
                            }
                        }
                        if (ret.Count == 0) throw new Exception($"Empty item range {key} {end}");
                        return ret;
                    }
                }
                else
                {
                    return new List<ItemKey> { game.ItemForName(item.Name) };
                }
            }
            List<string> hints = new List<string>();
            foreach (ConfigItemAnnotation configItems in ann.ConfigItems)
            {
                ItemGroups[configItems.GroupName] = new List<ItemKey>();
                foreach (ItemAnnotation item in configItems.Items)
                {
                    if (item.ConfigName == null && item.Name != null)
                    {
                        item.ConfigName = Regex.Replace(item.Name.ToLowerInvariant(), @"[^a-z]", "");
                    }
                    item.Keys = itemsForAnnotation(item);
                    if (!configItems.NoConfigNames)
                    {
                        if (Items.ContainsKey(item.ConfigName)) throw new Exception($"Duplicate item under config name {item.ConfigName}");
                        if (item.Keys.Count != 1) throw new Exception($"Config names disallowed for ranged item {item.ID}");
                        Items[item.ConfigName] = item.Keys[0];
                    }
                    AddMulti(ItemGroups, configItems.GroupName, item.Keys);
                }
                if (configItems.HintName != null)
                {
                    HintGroups[configItems.GroupName] = configItems.HintName;
                    hints.Add(configItems.HintName);
                }
            }
            HintCategories.AddRange(hints.Distinct().ToList());
            foreach (ItemPriorityAnnotation group in ann.ItemPriority)
            {
                group.Keys = new List<ItemKey>();
                if (group.Names != null)
                {
                    group.Keys.AddRange(group.Names.Select(name => game.ItemForName(name)));
                }
                if (group.Items != null)
                {
                    group.Keys.AddRange(group.Items.SelectMany(item => itemsForAnnotation(item)));
                }
                if (group.Includes != null)
                {
                    group.Keys.AddRange(group.Includes.Split(' ').SelectMany(s => ItemGroups[s]));
                }
                if (group.ExcludeTags != null)
                {
                    HashSet<string> exclude = new HashSet<string>(group.ExcludeTags.Split(' '));
                    // Random enemies are a bit unfair for important items. Can make this based on options as well.
                    if (exclude.Contains("missable"))
                    {
                        // Regular enemies are not expected to be defeatable, and are excluded
                        exclude.Add("enemy");
                        // In Sekiro, minibosses are a special class of enemy which block progression.
                        // In DS3, minibosses are enemies which are powerful/unique and confirmed to drop their items with enemy rando.
                    }
                    // Consider 'until' missable, except for key items, for which there is special area-level logic
                    if (exclude.Contains("missable") && group.Includes != "keyitems")
                    {
                        exclude.Add("until");
                    }
                    if (game.DS3 && !options["dlc1"])
                    {
                        // In DS3, some handmaid shops become available from DLC1
                        exclude.Add("dlc1");
                    }
                    if (group.Includes == "keyitems")
                    {
                        if (options["headlessignore"])
                        {
                            exclude.Add("headless");
                        }
                        NoKeyTags.UnionWith(exclude);
                    }
                    else if (group.Includes == "questitems")
                    {
                        NoQuestTags.UnionWith(exclude);
                    }
                    // Other exclude tags are added later for 
                    foreach (ItemKey key in group.Keys)
                    {
                        AddMulti(ExcludeTags, key, exclude);
                    }
                }
            }
            ItemPriority.AddRange(ann.ItemPriority.Where(priority => !priority.NoPriority));
            bool isSwitchDisabled(string sw)
            {
                if (sw == "always") return false;
                return sw != null && !sw.Split(' ').All(opt => options[opt]);
            }
            foreach (PlacementRestrictionAnnotation placement in ann.PlacementRestrictions)
            {
                // If switch not enabled, ignore
                if (isSwitchDisabled(placement.Switch))
                {
                    continue;
                }
                List<ItemKey> keys = new List<ItemKey>();
                if (placement.Name != null)
                {
                    keys.Add(game.ItemForName(placement.Name));
                }
                else if (placement.Item != null)
                {
                    keys.AddRange(itemsForAnnotation(placement.Item));
                }
                else if (placement.Includes != null)
                {
                    keys.AddRange(ItemGroups[placement.Includes]);
                }
                else throw new Exception();
                placement.Key = keys[0];
                if (keys.Count > 1)
                {
                    placement.OtherKeys = keys.Skip(1).ToList();
                }
                ItemRestrict[placement.Key] = placement;
            }
            // Set up race mode tags
            if (game.Sekiro)
            {
                RaceModeTags.UnionWith(new[] { "boss", "miniboss", "racemode" });
                if (!options["headlessignore"])
                {
                    // Unless headless can contain key items, don't allow them to contain race mode locations either
                    RaceModeTags.Add("headless");
                }
            }
            else if (game.DS3)
            {
                RaceModeTags.UnionWith(new[] { "boss", "racemode" });
                if (options["raceloc_ashes"])
                {
                    // This also adds items in ashes to race locations, vs previously they're random.
                    RaceModeTags.Add("raceshop");
                    RaceModeTags.Add("ashes");
                }
                if (options["raceloc_miniboss"])
                {
                    RaceModeTags.Add("miniboss");
                }
                if (options["raceloc_lizard"])
                {
                    RaceModeTags.Add("lizard");
                }
                if (options["raceloc_chest"])
                {
                    RaceModeTags.Add("chest");
                }
                if (options["raceloc_ring"])
                {
                    RaceModeTags.Add("ring");
                }
            }
            else if (game.EldenRing)
            {
                RaceModeTags.UnionWith(new[] { "racemode", "boss" });
                if (options["raceloc_health"])
                {
                    RaceModeTags.UnionWith(new[] { "seedtree", "church" });
                }
                if (options["raceloc_shops"])
                {
                    RaceModeTags.Add("raceshop");
                }
                if (options["raceloc_altboss"])
                {
                    RaceModeTags.Add("altboss");
                }
                if (options["raceloc_talisman"])
                {
                    RaceModeTags.Add("talisman");
                }
                if (!options["night"])
                {
                    NoRaceModeTags.Add("night");
                }
                if (options["nocaves"])
                {
                    NoRaceModeTags.Add("minidungeon");
                }
            }
            if (ItemGroups.ContainsKey("norandom"))
            {
                NorandomItems.UnionWith(ItemGroups["norandom"]);
            }
            if (ann.SpecialModes != null)
            {
                foreach (SpecialModeAnnotation group in ann.SpecialModes)
                {
                    bool raceMode = group.RaceSwitch != null && !isSwitchDisabled(group.RaceSwitch);
                    bool norandomMode = group.NorandomSwitch != null && !isSwitchDisabled(group.NorandomSwitch);
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
                        items.UnionWith(group.Items.SelectMany(itemsForAnnotation));
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
            // Process race mode items and race mode exclude tags together
            // For now, in Elden Ring, key items are race-mode-only
            NoKeyTags.UnionWith(NoRaceModeTags);
            foreach (ItemKey key in RaceModeItems)
            {
                AddMulti(ExcludeTags, key, NoRaceModeTags);
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
                AllAreas[area.Name] = new List<LocationScope>();
                if (area.Maps != null)
                {
                    foreach (string mapId in area.Maps.Split(' '))
                    {
                        AddMulti(mapIdAreas, mapId, area.Name);
                    }
                }
            }
            foreach (AreaAnnotation area in ann.Areas)
            {
                if (area.MainMaps != null)
                {
                    foreach (string mapId in area.MainMaps.Split(' '))
                    {
                        mapIdAreas[mapId].Remove(area.Name);
                        mapIdAreas[mapId].Insert(0, area.Name);
                    }
                }
            }
            HashSet<string> configVarNames = new HashSet<string>(GetConfig(new HashSet<string>()).Keys);
            foreach (AreaAnnotation area in ann.Events)
            {
                parseReq(area);
                Events[area.Name] = area;
                // Update event areas
                SortedSet<string> frees = area.ReqExpr.FreeVars();
                frees.ExceptWith(configVarNames);
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
                    // TODO: Fill these in
                    if (game.EldenRing) continue;
                    Console.WriteLine($"Warning: No annotation for slot {key}, with slots {string.Join(", ", entry.Value.Select(s => $"{s} at {string.Join(", ", data.Location(s).Keys)}"))}");
                    continue;
                }
                SlotAnnotation slot = strSlots[key];
                Slots[entry.Key] = slot;
                strSlots.Remove(key);
            }
            if (strSlots.Count() > 0)
            {
                // Should this be an error? Try seeing if merging in really big overhaul mods is actually playable.
                Console.WriteLine($"Warning: Keys [{string.Join(", ", strSlots.Keys)}] are in config but not in game. Make sure your base mods have all of the required item lots. Will try to proceed without them, but this will result in errors if any key or important items are missing.");
            }

            // Simple post processing and validation
            foreach (KeyValuePair<LocationScope, SlotAnnotation> entry in Slots)
            {
                LocationScope scope = entry.Key;
                SlotAnnotation slot = entry.Value;
                if (!Areas.TryGetValue(slot.Area, out AreaAnnotation areaAnn) && slot.Area != "unknown")
                {
                    throw new Exception($"Slot {scope} has unknown area {slot.Area}");
                }
                slot.AreaUntil = areaAnn?.Until;
                slot.SetTags(scope.OnlyShops, options, areaAnn?.Tags);
                foreach (string tag in slot.TagList)
                {
                    if (tag.Contains(':'))
                    {
                        string[] parts = tag.Split(':');
                        if (parts.Length != 2 || !Items.TryGetValue(parts[1], out ItemKey tagItem)) throw new Exception($"Bad scoped item tag {tag} in {scope}");
                        if (slot.TagItems == null) slot.TagItems = new Dictionary<string, List<ItemKey>>();
                        AddMulti(slot.TagItems, parts[0], tagItem);
                    }
                    AddMulti(AllTags, tag, scope);
                }
                if (slot.QuestReqs != null)
                {
                    slot.ItemReqs = new List<ItemKey>();
                    slot.AreaReqs = new List<string>();
                    foreach (string questReq in slot.QuestReqs.Split(' '))
                    {
                        if (Items.ContainsKey(questReq))
                        {
                            slot.ItemReqs.Add(Items[questReq]);
                        }
                        else if (Areas.ContainsKey(questReq))
                        {
                            slot.AreaReqs.Add(questReq);
                        }
                        else if (EventAreas.TryGetValue(questReq, out string eventArea))
                        {
                            // TODO: I'm not sure if event info is available everywhere so just transform it here
                            slot.AreaReqs.Add(eventArea);
                        }
                        else throw new Exception($"QuestReq {questReq} is neither an item or area or area-eligible event");
                    }
                }
                // Include area as part of key items counting
                string effectiveArea = slot.Area;
                // In the special case of quests at the start with dependencies elsewhere, don't treat those as available after Firelink, for purpose of key item counting
                if ((slot.Area == "firelink" || slot.Area == "ashinaoutskirts_temple" || slot.Area == "roundtable")
                    && slot.QuestReqs != null && slot.AreaReqs.Count > 0)
                {
                    effectiveArea = slot.AreaReqs[0];
                }
                if (game.DS3 && !slot.TagList.Contains("boss") && (slot.TagList.Contains("mid") || slot.TagList.Contains("late")))
                {
                    Areas[effectiveArea].HasProgression = true;
                }
                if (slot.Area != effectiveArea)
                {
                    slot.BaseArea = effectiveArea;
                }
                if (slot.Area != "unknown")
                {
                    AddMulti(AllAreas, slot.GetArea(), scope);
                }
            }
        }

        public void AddSpecialItems()
        {
            // Add special unique items into game
            foreach (ItemKey addItem in ItemGroups["add"])
            {
                // Mostly to exclude Path of the Dragon from being added when key items are not randomized
                if (NorandomItems.Contains(addItem)) continue;
                data.AddLocationlessItem(addItem);
            }
        }

        public void AddMaterialItems(bool matsRandomized)
        {
            bool debug = false;
            foreach (PlacementRestrictionAnnotation restrict in ItemRestrict.Values)
            {
                if (restrict.BaseLocationBuffer <= 0 || restrict.Unique == null) continue;
                ItemLocations itemLocs = data.Data[restrict.Key];
                int baseCount = itemLocs.Locations.Count(l => l.Key.Type == ScopeType.EVENT);
                if (debug) Console.WriteLine($"Locations for {restrict.Name}: {baseCount} base, {restrict.MatsAmount} mats");
                int addAmount = restrict.Unique.Sum(slot => slot.Amount);
                if (restrict.MatsAmount > 0 && !matsRandomized)
                {
                    // Reduce requirements if materials are reliably present
                    // previously: mats / 2
                    int cutMats = restrict.MatsAmount;
                    float cutRate = (float)cutMats / addAmount;
                    foreach (PlacementSlotAnnotation slot in restrict.Unique)
                    {
                        if (slot.Amount <= 0) continue;
                        int reduced = slot.Amount - (int)(slot.Amount * cutRate);
                        reduced = Math.Max(5, reduced);
                        if (debug) Console.WriteLine($"  Cutting {restrict.Name} slot from {slot.Amount} to {reduced}");
                        slot.Amount = reduced;
                    }
                }
                int bufferAmount = restrict.Unique.Sum(slot => slot.Amount) + restrict.BaseLocationBuffer;
                int toAdd = bufferAmount - baseCount;
                if (debug) Console.WriteLine($"  Adding {toAdd} to reach {bufferAmount}");
                if (toAdd > 0)
                {
                    for (int i = 0; i < toAdd; i++)
                    {
                        data.AddLocationlessItem(restrict.Key);
                    }
                }
            }
        }

        public void ProcessRestrictions(EnemyLocations enemies)
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
                if (!locs.Contains("_location") && !locs.Contains("_minidungeons") && !locs.Contains("*")) return locs;
                string[] reqs = locs.Split(' ');
                List<string> newReqs = new List<string>();
                foreach (string r in reqs)
                {
                    if (r.EndsWith("_location"))
                    {
                        string prefix = r.Substring(0, r.LastIndexOf("_location"));
                        newReqs.AddRange(getLocations(prefix));
                    }
                    else if (r.EndsWith("_minidungeons"))
                    {
                        // In this case, the prefix is like limgrave_
                        string prefix = r.Substring(0, r.LastIndexOf("minidungeons"));
                        newReqs.AddRange(Areas
                            .Where(a => a.Key.StartsWith(prefix))
                            .Where(a => a.Value.Tags != null && a.Value.Tags.Contains("minidungeon"))
                            .Select(a => a.Key));
                    }
                    else if (r.EndsWith("*"))
                    {
                        // Not full wildcard or god forbid regex, just prefix here
                        string prefix = r.Replace("*", "");
                        newReqs.AddRange(Areas
                            .Where(a => a.Key.StartsWith(prefix))
                            .Select(a => a.Key));
                    }
                    else
                    {
                        newReqs.Add(r);
                    }
                }
                string newLocs = string.Join(" ", newReqs);
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
                    slot.UpToAny = processLocations(slot.UpToAny);
                    slot.After = processLocations(slot.After);
                    slot.Inside = processLocations(slot.Inside);
                    return slot.Before == null && slot.UpTo == null && slot.UpToAny == null && slot.After == null && slot.Inside == null;
                });
            }
            foreach (PlacementRestrictionAnnotation restrict in ItemRestrict.Values)
            {
                if (restrict.KeyAreas != null)
                {
                    restrict.KeyAreas = processLocations(restrict.KeyAreas);
                }
                processLocationList(restrict.Unique);
                // TODO: How does this affect DS3?
                if (restrict.Unique != null && restrict.Unique.Count == 0) restrict.Unique = null;
                processLocationList(restrict.Drop);
                processLocationList(restrict.Shop);
            }
        }

        // For creationg new items, like esoteric texts -> skills
        public void CopyRestrictions(Dictionary<ItemKey, ItemKey> mapping)
        {
            foreach (ItemPriorityAnnotation priority in ItemPriority)
            {
                priority.Keys.AddRange(priority.Keys.Where(k => mapping.ContainsKey(k)).Select(k => mapping[k]).ToList());
            }
            RaceModeItems.UnionWith(RaceModeItems.Where(k => mapping.ContainsKey(k)).Select(k => mapping[k]).ToList());

            foreach (KeyValuePair<ItemKey, ItemKey> entry in mapping)
            {
                ItemKey from = entry.Key;
                ItemKey to = entry.Value;
                if (ItemRestrict.ContainsKey(from))
                {
                    if (ItemRestrict.ContainsKey(to)) throw new Exception($"Can't copy {from}->{to}; the latter is already present");
                    ItemRestrict[to] = ItemRestrict[from];
                    // Not making a deep copy here, but this should fine, as the old items are expected to not be used
                    ItemRestrict[to].Key = to;
                }
                if (ExcludeTags.ContainsKey(from))
                {
                    if (ExcludeTags.ContainsKey(to)) throw new Exception($"Can't copy {from}->{to}; the latter is already present");
                    ExcludeTags[to] = ExcludeTags[from];
                }
            }
        }

        public List<string> ItemAreasForMap(string mapId)
        {
            if (game.EldenRing)
            {
                if (!mapIdAreas.TryGetValue(mapId, out List<string> mapAreas)) throw new Exception($"No item region defined for {mapId}");
                return mapAreas;
            }
            else
            {
                return new List<string> { game.Locations[mapId] };
            }
        }

        // Hints and heuristics
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
            string location = game.Sekiro ? "ashinaoutskirts_temple" : (game.EldenRing ? "limgrave" : "firelink");
            if (locationSet.Count > 0)
            {
                if (game.EldenRing)
                {
                    string mapId = locationSet.OrderBy(loc => (loc == "m11_10_00_00" ? 1 : 0, loc)).First();
                    location = ItemAreasForMap(mapId)[0];
                }
                else
                {
                    location = locationOrder[locationSet.Select(loc => locationIndex[loc]).DefaultIfEmpty().Min()];
                }
            }
            SlotAnnotation slot = new SlotAnnotation
            {
                Area = location,
            };
            if (game.EldenRing)
            {
                if (true)
                {
                    slot.Tags = "norandom";
                }
                else
                {
                    // Alternate logic: unknown tags are missable
                    // These probably don't do anything, though - the real conditions are in Permutation
                    string defaultTag = "missable";
                    if (scope.Type == ScopeType.ASSET)
                    {
                        // Just to be safe, assets never randomizable
                        defaultTag = "norandom";
                    }
                    else if (scope.OnlyShops)
                    {
                        // Unknown shop slots are not randomized, since they are explicitly listed normally
                        // Trying to make ERR work
                        defaultTag = "norandom";
                    }
                    slot.Tags = defaultTag;
                }
            }
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
            if (!game.DS3 && !Areas.ContainsKey(area))
            {
                if (game.EldenRing && game.LocationNames.TryGetValue(area, out string mapName))
                {
                    return mapName;
                }
                return area;
            }
            if (!Areas.ContainsKey(area)) throw new Exception($"Unknown area {area}");
            return Areas[area].Text ?? area;
        }

        public string GetLocationHint(SlotKey key, SortedSet<string> specialLocation=null)
        {
            Func<string, string> capitalize = s => $"{s[0]}".ToUpperInvariant() + s.Substring(1);
            ItemLocation loc = data.Location(key);
            LocationScope scope = loc.LocScope;
            List<SlotKey> sources = data.Location(scope);
            string specialText = "";
            if (specialLocation != null && specialLocation.Count > 0)
            {
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
                // This doesn't work as well for Elden Ring, because map names are not area shortnames.
                // Requires some indirection in FullArea. Ideally just fill in all slots.
                SortedSet<string> locs = loc.GetLocations();
                text = $"In {(locs.Count == 0 ? "???" : string.Join(", ", locs.Select(a => FullArea(a))))}{specialText}";
            }
            return text;
        }

        public string GetLocationDescription(SlotKey key, HashSet<string> filterTags = null, HashSet<string> excludeTags = null, EldenCoordinator coord = null)
        {
            // Previously this method took List<LocationKey> for targetLocation.Keys, for filtering, but this is no longer used.
            ItemLocation loc = data.Location(key);
            LocationScope scope = loc.LocScope;
            // Can do data.Location(scope) to list all items in the same scope, but this is more specific and useful.
            List<SlotKey> sources = new List<SlotKey> { key };
            Func<bool, string> autoText = addPlace =>
            {
                SortedSet<string> models = new SortedSet<string>();
                SortedSet<string> locs = new SortedSet<string>();
                List<EntityId> entities = loc.Keys
                    .SelectMany(k => k.Entities)
                    .Where(e => !string.IsNullOrEmpty(e.MapName))
                    .ToList();
                if (game.EldenRing) entities.RemoveAll(e => e.ModelName == "c1000");
                foreach (SlotKey sourceKey in sources)
                {
                    models.UnionWith(entities.Select(e => game.EntityName(e)));
                    locs.UnionWith(entities.Select(e => e.MapName));
                }
                string auto = $"From {(models.Count == 0 ? "???" : string.Join(", ", models))}";
                if (game.EldenRing && coord != null && scope.Type != ScopeType.MODEL)
                {
                    List<EntityId> mapEntities = entities
                        .Where(e => e.Type != null && (e.Type.Contains("asset") || e.Type.Contains("enemy")))
                        .ToList();
                    if (mapEntities.Count == 1 && mapEntities[0].Position is Vector3 pos)
                    {
                        if (mapEntities[0].Type.Contains("enemy") && mapEntities[0].EntityID > 0)
                        {
                            auto += $" (#{mapEntities[0].EntityID})";
                        }
                        auto = $"{auto}. Near {coord.ClosestLandmark(mapEntities[0].MapName, pos)}";
                    }
                }
                string placeText = "";
                if (addPlace && scope.Type != ScopeType.MODEL)
                {
                    if (game.EldenRing)
                    {
                        string mapId = locs.OrderBy(m => (m == "m11_10_00_00" ? 1 : 0, m)).FirstOrDefault();
                        if (mapId != null)
                        {
                            placeText = " in " + (game.LocationNames.TryGetValue(mapId, out string mapName) ? mapName : mapId);
                        }
                    }
                    else
                    {
                        placeText = $" in {(locs.Count == 0 ? "???" : string.Join(", ", locs.Select(a => FullArea(a))))}";
                    }
                }
                if (addPlace) placeText += ": ";
                return placeText + auto;
            };
            string text;
            if (Slots.TryGetValue(scope, out SlotAnnotation slot) && !(slot.Text != null && slot.Text.Contains("aaa")))
            {
                bool marked = slot.TagList.Contains("XX") || (slot.Text != null && slot.Text.Contains("XX"));
                string slotText = slot.Text == null || slot.Text == "auto" ? autoText(false) : slot.Text.TrimEnd(new char[] { '.' });
                text = $" in {FullArea(slot.Area)}:{(marked ? " XX" : "")} {slotText}";
                if (filterTags != null && !slot.HasAnyTags(filterTags)) return null;
                if (excludeTags != null && slot.HasAnyTags(excludeTags)) return null;
            }
            else
            {
                if (filterTags != null) return null;
                text = autoText(true);
            }
            List<string> original = sources.Select(k => game.DisplayName(k.Item, loc.Quantity)).ToList();
            if (original.Count > 5)
            {
                original = original.Take(5).Concat(new[] { "etc" }).ToList();
            }
            return $"{text}. {(original.Count == 1 ? "Replaces" : "In the spot of")} {string.Join(", ", original)}.";
        }

        // Counting for key item assignment
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
            public string KeyAreas { get; set; }
            // Indication to add new synthetic item locations, until locations >= sum of slot amounts + buffer 
            public int BaseLocationBuffer { get; set; }
            // Number of materials found in reliable places in the world, used to offset the slot amounts
            public int MatsAmount { get; set; }
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
            public string UpToAny { get; set; }
            public string After { get; set; }
            public string Inside { get; set; }
            public bool UseGroups { get; set; }
            public PlacementSlotAnnotation()
            {
                this.Amount = -1;
            }
            public HashSet<string> AllowedAreas(Dictionary<string, HashSet<string>> includedAreas, Dictionary<string, HashSet<string>> areaGroups, bool debug=false)
            {
                // debug = Before == "moonlight" || Before == "ainsel";
                debug = false;
                List<HashSet<string>> requirements = new List<HashSet<string>>();
                IEnumerable<string> expandGroups(string a)
                {
                    return areaGroups != null && areaGroups.TryGetValue(a, out HashSet<string> group)
                        ? group
                        : new HashSet<string> { a };
                }
                IEnumerable<string> getAreasUpTo(string loc)
                {
                    if (!includedAreas.ContainsKey(loc)) throw new Exception($"Unknown area {loc} in {this}");
                    IEnumerable<string> ret;
                    if (includedAreas[loc].Count == 0)
                    {
                        // If unused areas, consider this to mean 'all areas'
                        ret = includedAreas.Where(e => e.Value.Count > 0).Select(e => e.Key);
                    }
                    else
                    {
                        ret = includedAreas[loc];
                    }
                    if (UseGroups)
                    {
                        return ret.SelectMany(expandGroups).Distinct();
                    }
                    return ret;
                };
                if (Before != null)
                {
                    foreach (string loc in Before.Split(' '))
                    {
                        HashSet<string> locs = new HashSet<string>(getAreasUpTo(loc).Where(a => !includedAreas[a].Contains(loc)));
                        if (debug) Console.WriteLine($"Before: {loc} -> {string.Join(",", locs)}");
                        // if (debug) Console.WriteLine($"Before candidates: {string.Join(",", getAreasUpTo(loc))}");
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
                if (UpToAny != null)
                {
                    HashSet<string> upto = new HashSet<string>();
                    foreach (string loc in UpToAny.Split(' '))
                    {
                        HashSet<string> locs = new HashSet<string>(getAreasUpTo(loc));
                        if (debug) Console.WriteLine($"UpToAny: {loc} -> {string.Join(",", locs)}");
                        upto.UnionWith(locs);
                    }
                    requirements.Add(upto);
                }
                if (Inside != null)
                {
                    HashSet<string> inside = new HashSet<string>();
                    foreach (string loc in Inside.Split(' '))
                    {
                        HashSet<string> locs = new HashSet<string> { loc };
                        if (UseGroups)
                        {
                            inside.UnionWith(expandGroups(loc));
                        }
                        if (debug) Console.WriteLine($"Inside: {loc} -> {string.Join(",", locs)}");
                        inside.UnionWith(locs);
                    }
                    requirements.Add(inside);
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
                if (debug) Console.WriteLine($"** Final: {this}: {string.Join(",", allLocs)}\n");
                return allLocs;
            }
            public override string ToString()
            {
                string f(string type, string field)
                {
                    return string.IsNullOrEmpty(field) ? "" : $" {type} [{field}]";
                }
                return $"{(UseGroups ? "Group " : "")}Slot[{Amount}]"
                    + $"{f("Before", Before)}{f("UpTo", UpTo)}{f("UpToAny", UpToAny)}{f("After", After)}{f("Inside", Inside)}";
            }
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
            public string EndID { get; set; }
            [YamlIgnore]
            public List<ItemKey> Keys { get; set; }
        }

        public class SlotAnnotation
        {
            public string Key { get; set; }
            public List<string> DebugText { get; set; }
            public string Text { get; set; }
            public string Comment { get; set; }
            public string Area { get; set; }
            public string QuestReqs { get; set; }
            public string Until { get; set; }
            public string FullArea { get; set; }  // Ignored, just for filling in area when multiple exist
            public string Event { get; set; }
            public string Tags { get; set; }
            [YamlIgnore]
            public HashSet<string> TagList { get; set; }
            [YamlIgnore]
            public Dictionary<string, List<ItemKey>> TagItems { get; set; }
            [YamlIgnore]
            public string AreaUntil { get; set; }
            // TODO: Replace uses with TagList directly.
            public HashSet<string> GetTags() => TagList;
            public void SetTags(bool shopOnly, RandomizerOptions opt, string areaTags = null)
            {
                string tagsStr = Tags;
                if (tagsStr != null && tagsStr.Contains("aaaaa"))
                {
                    tagsStr = "missable";
                }
                if (string.IsNullOrWhiteSpace(tagsStr))
                {
                    TagList = new HashSet<string>();
                }
                else
                {
                    TagList = new HashSet<string>(tagsStr.Split(' '));
                }
                if (!string.IsNullOrWhiteSpace(areaTags))
                {
                    TagList.UnionWith(areaTags.Split(' '));
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
                // opt may be null, so only check it if there's a manual tag
                if (TagList.Contains("carp") && !opt["carpsanity"])
                {
                    TagList.Add("norandom");
                }
                if (TagList.Contains("ng+") && opt["nongplusrings"])
                {
                    TagList.Add("norandom");
                }
                if ((TagList.Contains("sorceries") || TagList.Contains("incantations")) && opt["spellshops"])
                {
                    TagList.Add("restrict");
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
                return TagList.Any(t => tags.Contains(t));
            }
            public bool HasTag(string tag)
            {
                return TagList.Contains(tag);
            }

            [YamlIgnore]
            public string BaseArea { get; set; }
        }

        public class AreaAnnotation
        {
            // Internal name
            public string Name { get; set; }
            // Display name
            public string Text { get; set; }
            // Requirements expression
            public string Req { get; set; }
            // Item unavailability event for the entire area
            // TODO: Override these... "always" works?
            public string Until { get; set; }
            // Combined weight area for item ordering
            public string WeightBase { get; set; }
            // Events which always precede other events, for directly calculating all dependent items (?)
            public string AlwaysBefore { get; set; }
            // If there is no name<->map mapping above, space-separated map ids for this area. May overlap with other areas.
            public string Maps { get; set; }
            // Maps which should get automatically assigned here.
            // Otherwise, they get automatically assigned to the first area in the list in which they appear.
            public string MainMaps { get; set; }
            // Do not use. I think we can delete this.
            public List<string> Aliases { get; set; }
            // Deprioritize the area for key item weights
            public bool BoringKeyItem { get; set; }
            // Tags to copy to slots in the area
            public string Tags { get; set; }
            [YamlIgnore]
            public Expr ReqExpr { get; set; }
            [YamlIgnore]
            public bool HasProgression { get; set; }
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
            // TODO: Using lowercase AND or OR apparently is ignored. Probably just handroll a parser at some point.
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

        public void Save(bool initial = false, bool filter = false, EldenCoordinator coord = null)
        {
            Annotations ann = new Annotations();
            bool interestingEldenItem(ItemKey item)
            {
                if (item.Type == ItemType.RING) return true;
                if (item.Type != ItemType.GOOD) return false;
                int id = item.ID;
                return (id == 181 // Whistle
                    || (id >= 100 && id < 190) // Multiplayer items
                    || (id >= 191 && id < 300) // GRs and physick
                    || (id >= 1000 && id < 1100) // Flasks
                    || id == 2080 // Blasphemous Claw
                    || id == 2090 // Deathroot
                    || (id >= 11000 && id < 11050) // Tears
                    || (item.ID >= 2130 && item.ID <= 2250) // Celestial Dew to Prattling Pates
                    // Exclude Stonesword Key 8000 for now
                    || (item.ID >= 8010 && item.ID <= 8650) // Most key items and map fragments
                    || (item.ID >= 8850 && item.ID < 9000) // Scrolls, bell bearings, whetblades, misc keys
                    || (item.ID >= 9300 && item.ID <= 9510) // Cookbooks to perfume bottles, hopefully isn't too much
                    || (item.ID >= 9990 && item.ID <= 9999) // Custom items
                    || (item.ID >= 10010 && item.ID <= 10060) // Important upgrades (flask, memory, talisman)
                    || item.ID == 10080 // Rennala GR
                    // 10100 to 10919: Upgrade materials
                    );
            }
            foreach (KeyValuePair<LocationScope, List<SlotKey>> entry in data.Locations)
            {
                LocationScope scope = entry.Key;
                // Skip model keys for now, there is not much to configure
                if (scope.Type == ScopeType.MODEL || scope.Type == ScopeType.ASSET)
                {
                    continue;
                }
                SlotAnnotation existing = Slots.ContainsKey(scope) ? Slots[scope] : null;
                // Generate debug text and keys afresh no matter what
                List<string> debugText = new List<string>();
                SortedSet<string> locationSet = new SortedSet<string>();
                SortedSet<EntityId> entities = new SortedSet<EntityId>();
                HashSet<LocationKey.LocationType> locTypes = new HashSet<LocationKey.LocationType>();
                foreach (SlotKey key in entry.Value)
                {
                    ItemLocation loc = data.Location(key);
                    debugText.Add($"{game.Name(key.Item)} - {loc}");
                    locationSet.UnionWith(loc.GetLocations());
                    entities.UnionWith(loc.Keys.SelectMany(k => k.Entities));
                    locTypes.UnionWith(loc.Keys.Select(k => k.Type));
                }
                SortedSet<string> models = new SortedSet<string>(
                    entities.Select(e => game.EntityName(e, true, true)));
                List<EntityId> mapEntities = entities.Where(e => e.Type != null && (e.Type.Contains("asset") || e.Type.Contains("enemy"))).ToList();

                // Elden Ring filtering: only list shops, unique-ish items, talismans, boss drops
                if (game.EldenRing && filter)
                {
                    bool eligible = false;
                    foreach (SlotKey key in entry.Value)
                    {
                        if (interestingEldenItem(key.Item))
                        {
                            eligible = true;
                            break;
                        }
                        if (game.Name(key.Item).Contains("?ITEM?"))
                        {
                            // Put these in the config just to disable them from randomization
                            eligible = true;
                            break;
                        }
                    }
                    if (locTypes.Contains(LocationKey.LocationType.SHOP))
                    {
                        eligible = true;
                    }
                    if (entities.Any(e => e.Type != null && e.Type.Contains("event")))
                    {
                        eligible = true;
                    }
                    if (models.Any(m => m.Contains("Chest")))
                    {
                        eligible = true;
                    }
                    if (mapEntities.Count != 1)
                    {
                        // Disambiguate ambiguous entries and norandom bad ones
                        eligible = true;
                    }
                    if (!eligible) continue;
                }

                int locId;
                List<string> itemAreas;  // A map in the case of Elden Ring, regular location otherwise
                if (game.EldenRing)
                {
                    if (locationSet.Count == 0)
                    {
                        locId = 9999;
                        itemAreas = new List<string>();
                    }
                    else
                    {
                        List<string> mapIds = locationSet.OrderBy(loc => (loc == "m11_10_00_00" ? 1 : 0, loc)).ToList();
                        string mapId = mapIds[0];
                        if (mapId.StartsWith("m60"))
                        {
                            // e.g. m60_51_36_00
                            locId = int.Parse(mapId.Substring(4, 2) + mapId.Substring(7, 2));
                        }
                        else
                        {
                            // e.g. m34_11_00_00
                            locId = int.Parse(mapId.Substring(1, 2) + mapId.Substring(4, 2));
                        }
                        itemAreas = mapIds.SelectMany(m => ItemAreasForMap(m)).Distinct().ToList();
                    }
                    // locationSet.FirstOrDefault()
                    // Console.WriteLine(string.Join(", ", locationSet));
                }
                else
                {
                    List<int> locations = locationSet.Select(loc => locationIndex[loc]).ToList();
                    locations.Sort();
                    // First area that's not firelink, if in multiple areas
                    locId = locations.Count() == 0 ? 99 : locations[locations[0] == 0 && locations.Count() > 1 ? 1 : 0];
                    itemAreas = locId == 99 ? new List<string>() : new List<string> { locationOrder[locId] };
                }

                if (debugText.Count() > 1 && (locationSet.Count() > 1 || models.Count() > 1))
                {
                    // debugText.Insert(0, $"{entry.Key.Description()} from [{string.Join(", ", models)}] in [{string.Join(", ", locations.Select(loc => locationOrder[loc]))}]");
                    // TODO map names?
                    debugText.Insert(0, $"{entry.Key.Description()} in [{string.Join(", ", locationSet.Select(l => game.MapLocationName(l)))}]");
                }
                // List<EntityId> mapEntities = entities.Where(e => e.Type != null && (e.Type.Contains("asset") || e.Type.Contains("enemy"))).ToList();
                if (mapEntities.Count <= 2 && coord != null)
                {
                    foreach (EntityId id in mapEntities)
                    {
                        if (id.Position is Vector3 pos)
                        {
                            debugText.Add("By " + coord.ClosestLandmark(id.MapName, pos, true));
                        }
                    }
                }
                // if (entry.Key.ToString() == "0:10007440::") Console.WriteLine(string.Join(" / ", models));
                string area = itemAreas.FirstOrDefault() ?? "";
                SlotAnnotation slot = new SlotAnnotation
                {
                    Key = $"{locId.ToString(game.EldenRing ? "0000" : "00")},{entry.Key}",
                    DebugText = debugText,
                    // Area = firstLoc == 99 ? "" : locationOrder[firstLoc],
                    Area = area,
                    FullArea = itemAreas.Count > 1 || (existing != null && area != existing.Area && itemAreas.Count > 0) ? string.Join(" ", itemAreas) : null,
                };
                if (existing != null)
                {
                    if (existing.Area != null) slot.Area = existing.Area;
                    if (existing.Tags != null) slot.Tags = existing.Tags;
                    if (existing.Text != null) slot.Text = existing.Text;
                    if (existing.Event != null) slot.Event = existing.Event;
                    if (existing.Until != null) slot.Until = existing.Until;
                    if (existing.Comment != null) slot.Comment = existing.Comment;
                    if (existing.QuestReqs != null) slot.QuestReqs = existing.QuestReqs;
                }
                if (slot.Tags == null)
                {
                    slot.Tags = itemAreas.Count > 0 ? "aaaaaaaaaaaaaaaaa" : "norandom ignore";
                }
                if (slot.Text == null)
                {
                    slot.Text = "aaaaaaaaaaaaaaaaa";
                }
                ann.Slots.Add(slot);
            }
            ann.Slots.Sort((a, b) => a.Key.CompareTo(b.Key));
            Console.WriteLine(GameData.Serializer.Serialize(ann));
        }
    }
}
