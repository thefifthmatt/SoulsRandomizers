using Org.BouncyCastle.Asn1.X509;
using Pidgin;
using Pidgin.Expression;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using static Pidgin.Parser;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using CustomSilo = RandomizerCommon.ItemPreset.CustomSilo;
using Placement = RandomizerCommon.ItemPreset.Placement;
using PlacementMode = RandomizerCommon.ItemPreset.PlacementMode;
using PlacementType = RandomizerCommon.ItemPreset.PlacementType;
using PremiumGroup = RandomizerCommon.ItemPreset.PremiumGroup;

namespace RandomizerCommon
{
    public class AnnotationData
    {
        private static readonly Dictionary<string, int> LocationIndices = new Dictionary<string, int> { { "early", 0 }, { "mid", 1 }, { "late", 2 }, { "deadend", 2 } };

        private GameData game;
        private LocationData data;
        private List<ConfigAnnotation> configVars = new List<ConfigAnnotation>();
        private bool tagslot;

        // This is a lot of data okay

        // -- Data based on locations
        // All annotated item slots
        public readonly Dictionary<LocationScope, SlotAnnotation> Slots = new();

        // -- Configuration
        // All areas
        public readonly Dictionary<string, AreaAnnotation> Areas = new();
        // All events (boss fights and major game progress triggers)
        public readonly Dictionary<string, AreaAnnotation> Events = new();
        // Areas that events belong to, when they uniquely belong to one
        public readonly Dictionary<string, string> EventAreas = new();
        public readonly Dictionary<string, List<string>> AreaEvents = new();
        // Default config variables used as macros in other expressions
        public readonly Dictionary<string, Expr> DefaultConfigExprs = new();
        // All named items with logic associated with them.
        public readonly Dictionary<string, ItemKey> Items = new();
        // All items with names in their LocationScopes, ideally one scope per name, to distinguish between them for key item placement.
        public readonly Dictionary<ItemKey, MultiItem> MultiItems = new();
        // Contents of item groups by names, used for various purposes
        // Required ones: keyitems, questitems, remove
        public readonly Dictionary<string, List<ItemKey>> ItemGroups = new();
        // Mapping from item group names to hint group names used for them
        public readonly Dictionary<string, string> HintGroups = new();
        // All hint group names
        public readonly List<string> HintCategories = new();
        // All starting groups, used by name in CharacterWriter
        public readonly Dictionary<string, StartingGroupAnnotation> StartingGroups = new();
        // All groups of items in order of priority, used for bias weighting
        public readonly List<ItemPriorityAnnotation> ItemPriority = new();
        // Placement restrictions for various items, in relation to game progress. Includes some key item location restrictions.
        public readonly Dictionary<ItemKey, PlacementRestrictionAnnotation> ItemRestrict = new();
        // Items which should be placed in race mode locations. Key items come first (in 'Priority' list). Some slots may be removed if there's insufficient space for them.
        // These are in a deterministic order as they're placed in the first permutation pass.
        public readonly SortedSet<ItemKey> RaceModeItems = new();
        // Items which shouldn't be randomized
        public readonly HashSet<ItemKey> NorandomItems = new();
        // Items whose sources should be removed
        public readonly HashSet<ItemKey> RemoveItems = new();
        // Map from built-in parameterized silo types to index assignments based on params.
        // This may overinclude items without other data, and requires looking up the base ItemKey.
        public readonly Dictionary<ItemSiloType, Dictionary<ItemKey, int>> ParameterizedSilos = new();
        // Restrictions for items per-location
        // For a given location, this is the intersection of groups with tags at that location, plus any items with RequireTags there.
        public bool UsePremiumGroups { get; private set; }
        public readonly Dictionary<LocationScope, HashSet<ItemKey>> PremiumGroups = new();
        // Bias weights, which may come from preset
        public RandomizerOptions.BiasWeights Weights { get; private set; }

        // Tag stuff, more customizable with ItemPreset
        // Forbidden placement flags for various items
        public readonly Dictionary<ItemKey, HashSet<string>> ExcludeTags = new();
        // Required locations for various items
        public readonly Dictionary<ItemKey, HashSet<string>> RequireTags = new();
        // Exclude tags for key items specifically. Used for counting purposes, and ash placement in DS3.
        public readonly HashSet<string> NoKeyTags = new();
        // Exclude tags for quest items specifically. Used for counting purposes.
        public readonly HashSet<string> NoQuestTags = new();
        // Require tags for key items. Used for counting purposes.
        private readonly HashSet<string> KeyTags = new();
        // Require tags for quest items. Used for counting purposes.
        private readonly HashSet<string> QuestTags = new();
        // Eligible tags for race mode locations. No longer used for placement, as RequireTags now does this.
        // Used for determining if item silos need locations added outside of the silo for key item placement (e.g Elemer with weapon silo)
        // As a result, these will also include KeyTags.
        public readonly HashSet<string> RaceModeTags = new();
        // Exclude tags for race mode items. Used for exclude tags
        private readonly HashSet<string> NoRaceModeTags = new();
        // Tags which shouldn't be randomized (includes special tag enemyinf)
        public readonly HashSet<string> NorandomTags = new();
        // Misc named options only configured in preset
        public readonly HashSet<string> PresetOpts = new();

        public AreaSiloType OverallSilo { get; private set; } = AreaSiloType.None;
        public AreaSiloType KeyItemSilo { get; private set; } = AreaSiloType.None;

        // As preset is optional and complicated, it is not currently exposed directly outside this class after being processed
        private ItemPreset ConfigPreset { get; set; }

        // Used for key item counting
        public readonly SortedDictionary<string, List<LocationScope>> AllTags = new SortedDictionary<string, List<LocationScope>>();
        public readonly SortedDictionary<string, List<LocationScope>> AllAreas = new SortedDictionary<string, List<LocationScope>>();
        private bool MoreMaxSlots;

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
        private HashSet<string> bigMultiAreas = new HashSet<string>();

        public AnnotationData(GameData game, LocationData data)
        {
            this.game = game;
            this.data = data;
            locationOrder = game.Sekiro ? sekiroLocationOrder : ds3LocationOrder;
            locationIndex = Enumerable.Range(0, locationOrder.Count()).ToDictionary(i => locationOrder[i], i => i);
        }

        public void Load(RandomizerOptions opt, ItemPreset itemPreset = null, bool processSlots = true, MergeModManifest merge = null)
        {
            Annotations ann;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            string annPath = $@"{game.Dir}\Base\annotations.txt";
            using (var reader = File.OpenText(annPath))
            {
                ann = deserializer.Deserialize<Annotations>(reader);
            }
            string slotPath = $@"{game.Dir}\Base\itemslots.txt";
            if (processSlots && File.Exists(slotPath))
            {
                if (ann.Slots.Count > 0) throw new Exception($"Internal error: Item slots defined in {annPath}:");
                Annotations slotAnn;
                using (var reader = File.OpenText(slotPath))
                {
                    slotAnn = deserializer.Deserialize<Annotations>(reader);
                }
                ann.Slots = slotAnn.Slots;
            }
            tagslot = opt["tagslot"];

            // Preprocess aliases for custom weapons and upgraded weapons, used in Elden Ring and DS3
            Dictionary<ItemKey, List<ItemKey>> keyAliases = new();
            foreach (ItemKey dataKey in data.Items.Keys)
            {
                // Similar to loop in CharacterWriter
                ItemKey key = game.GetBaseWeapon(dataKey);
                if (!dataKey.Equals(key))
                {
                    AddMulti(keyAliases, key, dataKey);
                }
            }

            // Config vars
            foreach (ConfigAnnotation config in ann.Config)
            {
                config.UpdateOptions(opt);
            }
            configVars = ann.Config;
            // Items
            List<ItemKey> itemsForAnnotation(ItemAnnotation item)
            {
                List<ItemKey> ret = new();
                void addItem(ItemKey key)
                {
                    ret.Add(key);
                    if (keyAliases.TryGetValue(key, out List<ItemKey> altKeys))
                    {
                        ret.AddRange(altKeys);
                    }
                }
                if (item.ID != null)
                {
                    string[] parts = item.ID.Split(':');
                    ItemKey key = new ItemKey(game.ShopItemTypes[int.Parse(parts[0])], int.Parse(parts[1]));
                    if (item.EndID == null)
                    {
                        addItem(key);
                    }
                    else
                    {
                        parts = item.EndID.Split(':');
                        ItemKey end = new ItemKey(game.ShopItemTypes[int.Parse(parts[0])], int.Parse(parts[1]));
                        if (key.Type != end.Type || key.ID > end.ID)
                        {
                            throw new Exception($"Invalid item range {key} {end}");
                        }
                        if (end.ID - key.ID < 100)
                        {
                            for (int i = key.ID; i <= end.ID; i++)
                            {
                                ItemKey cand = new ItemKey(key.Type, i);
                                if (game.Names().ContainsKey(cand))
                                {
                                    addItem(cand);
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
                                    addItem(cand);
                                }
                            }
                        }
                        if (ret.Count == 0) throw new Exception($"Empty item range {key} {end}");
                    }
                }
                else
                {
                    ItemKey key = game.ItemForName(item.Name);
                    addItem(key);
                }
                return ret;
            }
            List<string> hints = new List<string>();
            foreach (ConfigItemAnnotation configItems in ann.ConfigItems)
            {
                ItemGroups[configItems.GroupName] = new List<ItemKey>();
                foreach (ItemAnnotation item in configItems.Items)
                {
                    if (item.ConfigName == null && item.Name != null)
                    {
                        item.ConfigName = Regex.Replace(item.Name.ToLowerInvariant(), @"[^a-z0-9]", "");
                    }
                    item.Keys = itemsForAnnotation(item);
                    // Add to Items and MultiItems in second pass, because MultiItems itself requires the multikey group
                    AddMulti(ItemGroups, configItems.GroupName, item.Keys);
                }
                // Console.WriteLine($"{configItems.GroupName}: {ItemGroups[configItems.GroupName].Count} items of {string.Join(",", ItemGroups[configItems.GroupName].Select(c => c.Type).Distinct())}");
                if (configItems.HintName != null)
                {
                    HintGroups[configItems.GroupName] = configItems.HintName;
                    hints.Add(configItems.HintName);
                }
            }
            HintCategories.AddRange(hints.Distinct().ToList());

            if (game.EldenRing)
            {
                if (opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out _))
                {
                    // Manual group addition based on replacement. These are expected to exist in the config.
                    ItemGroups["remove"].AddRange(ItemGroups["removerold"]);
                }
                if (opt["allcraft"])
                {
                    ItemGroups["remove"].AddRange(ItemGroups["removecraft"]);
                }
                if (opt["dlc"] && (opt["dlcstart"] || opt["quickstart"]))
                {
                    foreach (string group in new[] { "maxflask", "maxflasknum", "maxpouch", "maxmemslot", "maxwhetblade", "maxbb" })
                    {
                        if (!ItemGroups.TryGetValue(group, out List<ItemKey> groupKeys)) throw new Exception($"Missing configured group {group} for corresponding option");
                        if (opt[group])
                        {
                            ItemGroups["remove"].AddRange(groupKeys);
                        }
                    }
                }
            }

            // Dynamic filter groups, computed here based on live data
            if (game.EldenRing)
            {
                // All named items, contains invalid and unreachable items
                ItemGroups["rare"] = new();
                ItemGroups["legendary"] = new();
                ItemGroups["dlc"] = new();
                ItemGroups["nodlc"] = new();
                foreach (KeyValuePair<ItemKey, string> entry in game.Names())
                {
                    ItemKey key = entry.Key;
                    string content = game.IsEldenDlcItem(key) ? "dlc" : "nodlc";
                    ItemGroups[content].Add(key);
                    // Custom weapons are excluded from Names. The remaining 5 base types have a rarity field.
                    PARAM.Row row = game.Item(key);
                    if (row != null)
                    {
                        byte rarity = (byte)row["rarity"].Value;
                        if (rarity == 2)
                        {
                            ItemGroups["rare"].Add(key);
                        }
                        else if (rarity == 3)
                        {
                            ItemGroups["legendary"].Add(key);
                        }
                    }
                }
            }

            void setPriorityKeys(ItemPriorityAnnotation group)
            {
                if (group.Keys != null) return;
                group.Keys = new List<ItemKey>();
                if (group.Names != null)
                {
                    group.Keys.AddRange(SoulsIds.Events.DecommentCmds(group.Names).Select(name => game.ItemForName(name)));
                }
                if (group.Items != null)
                {
                    group.Keys.AddRange(group.Items.SelectMany(item => itemsForAnnotation(item)));
                }
                if (group.Includes != null)
                {
                    group.Keys.AddRange(group.Includes.Split(' ').SelectMany(s => ItemGroups[s]));
                }
            }
            foreach (ItemPriorityAnnotation group in ann.ItemPriority)
            {
                setPriorityKeys(group);
            }
            foreach (ItemPriorityAnnotation group in game.EldenRing ? ann.ItemExcludes : ann.ItemPriority)
            {
                setPriorityKeys(group);
                if (group.ExcludeTags == null) continue;
                HashSet<string> exclude = new HashSet<string>(group.ExcludeTags.Split(' '));
                // Random enemies are a bit unfair for important items. Can make this based on options as well.
                if (exclude.Contains("missable") && !game.EldenRing)
                {
                    // Regular enemies are not expected to be defeatable, and are excluded
                    // In Elden Ring, this is left configurable as it should be safe probably
                    exclude.Add("enemy");
                    // In Sekiro, minibosses are a special class of enemy which block progression.
                    // In DS3, minibosses are enemies which are powerful/unique and confirmed to drop their items with enemy rando.
                }
                // Consider 'until' missable, except for key items, for which there is special area-level logic
                if (exclude.Contains("missable") && group.Includes != "keyitems")
                {
                    exclude.Add("until");
                }
                if (game.DS3 && !opt["dlc1"])
                {
                    // In DS3, some handmaid shops become available from DLC1
                    exclude.Add("dlc1");
                }
                if (group.Includes == "keyitems")
                {
                    if (opt["headlessignore"])
                    {
                        exclude.Add("headless");
                    }
                    NoKeyTags.UnionWith(exclude);
                }
                else if (group.Includes == "questitems")
                {
                    NoQuestTags.UnionWith(exclude);
                }
                // Other exclude tags are added later for key items
                foreach (ItemKey key in group.Keys)
                {
                    AddMulti(ExcludeTags, key, exclude);
                }
            }
            ItemPriority.AddRange(ann.ItemPriority.Where(priority => !priority.NoPriority));

            // The below stuff can be augmented by presets, so load it now
            bool usePreset = game.EldenRing;
            if (game.EldenRing)
            {
                OverallSilo = opt["dlc"] && opt["dlcsilo"] ? AreaSiloType.DLC : AreaSiloType.None;
                // TODO: Make this come from preset processing
                bool dlcKeySilo = itemPreset == null && opt["dlckeysilo"];
                KeyItemSilo = opt["dlc"] && (opt["dlcstart"] || dlcKeySilo) ? AreaSiloType.DLC : OverallSilo;
            }
            else if (game.DS3)
            {
                OverallSilo = AreaSiloType.None;
                if ((opt["dlc1"] || opt["dlc2"]) && opt["dlckeysilo"])
                {
                    KeyItemSilo = AreaSiloType.DLC;
                }
            }

            bool explainPreset = false;
            if (usePreset)
            {
                ItemPreset defPreset = ann.DefaultPreset;
                if (defPreset == null) throw new Exception($"Missing ItemPreset in annotations file");
                defPreset = defPreset.ProcessBuiltins(opt);
                defPreset.Resolve(game, ItemGroups, keyAliases);
                // Use custom one if passed in
                ConfigPreset = defPreset;
                if (itemPreset != null)
                {
                    itemPreset.Resolve(game, ItemGroups, keyAliases, defPreset);
                    ConfigPreset = itemPreset;
                }
                // Console.WriteLine(ConfigPreset.ToYamlString());
                if (opt["dlc"])
                {
                    // Config silo must be *more* specific than options-based silo.
                    // As a result, if options-based silo is DLC, it can't changed back to None.
                    // We already check consistency between overall/key config silo in Resolve.
                    if (OverallSilo == AreaSiloType.None)
                    {
                        OverallSilo = ConfigPreset.OverallSilo;
                    }
                    if (KeyItemSilo == AreaSiloType.None)
                    {
                        KeyItemSilo = ConfigPreset.KeyItemSilo;
                    }
                }
                // Update ExcludeTags here, also new RequireTags system
                foreach (Placement place in ConfigPreset.Placements)
                {
                    if (place.Disabled)
                    {
                        continue;
                    }
                    if (place.Excludes != null && place.Excludes.Count > 0)
                    {
                        List<string> tags = place.Excludes.Select(p => p.Tag).ToList();
                        if (explainPreset)
                        {
                            Console.WriteLine($"{place.Type}: excludes {string.Join(",", tags)} ({string.Join(", ", place.Keys.Select(game.Name))})");
                        }
                        if (place.Type == PlacementType.KeyItems)
                        {
                            NoKeyTags.UnionWith(tags);
                        }
                        else if (place.Type == PlacementType.QuestItems)
                        {
                            NoQuestTags.UnionWith(tags);
                        }
                        foreach (ItemKey key in place.Keys)
                        {
                            AddMulti(ExcludeTags, key, tags);
                        }
                    }
                    if (place.Requires != null && place.Requires.Count > 0)
                    {
                        List<string> tags = place.Requires.Select(p => p.Tag).ToList();
                        if (explainPreset)
                        {
                            Console.WriteLine($"{place.Type}: requires {string.Join(",", tags)} ({string.Join(", ", place.Keys.Select(game.Name))})");
                        }
                        if (place.Type == PlacementType.KeyItems)
                        {
                            KeyTags.UnionWith(tags);
                        }
                        else if (place.Type == PlacementType.QuestItems)
                        {
                            QuestTags.UnionWith(tags);
                        }
                        foreach (ItemKey key in place.Keys)
                        {
                            AddMulti(RequireTags, key, tags);
                        }
                    }
                }
                Weights = ConfigPreset.BiasWeights;
            }
            Weights ??= RandomizerOptions.BiasWeights.Calculate(opt.Difficulty, game.Type);

            foreach (PlacementRestrictionAnnotation placement in ann.PlacementRestrictions)
            {
                if (!opt.IsSwitchEnabled(placement.Switch))
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
                if (!opt["headlessignore"])
                {
                    // Unless headless can contain key items, don't allow them to contain race mode locations either
                    RaceModeTags.Add("headless");
                }
                KeyTags.UnionWith(RaceModeTags);
            }
            else if (game.DS3)
            {
                RaceModeTags.UnionWith(new[] { "boss", "racemode" });
                if (opt["raceloc_health"])
                {
                    RaceModeTags.Add("health");
                }
                if (opt["raceloc_ashes"])
                {
                    // This also adds items in ashes to race locations, vs previously they're random.
                    RaceModeTags.Add("raceshop");
                    RaceModeTags.Add("ashes");
                }
                if (opt["raceloc_miniboss"])
                {
                    RaceModeTags.Add("miniboss");
                }
                if (opt["raceloc_lizard"])
                {
                    RaceModeTags.Add("lizard");
                }
                if (opt["raceloc_chest"])
                {
                    RaceModeTags.Add("chest");
                }
                if (opt["raceloc_ring"])
                {
                    RaceModeTags.Add("ring");
                }
                KeyTags.UnionWith(RaceModeTags);
            }
            else if (game.EldenRing)
            {
                if (usePreset)
                {
                    RaceModeTags.UnionWith(ConfigPreset.ImportantGroup.Requires.Select(loc => loc.Tag));
                    NoRaceModeTags.UnionWith(ConfigPreset.ImportantGroup.Excludes.Select(loc => loc.Tag));
                    // Still special handling of this for now, todo tag overworld (not fortress) reliably
                    if (opt["fog"] && opt["crawl"])
                    {
                        NoRaceModeTags.Add("nocrawl");
                        NoKeyTags.Add("nocrawl");
                    }
                    // At this point, only needed for item silo stuff. Opposite of previous approach
                    RaceModeTags.UnionWith(KeyTags);
                }
                else
                {
                    RaceModeTags.UnionWith(new[] { "racemode", "boss" });
                    if (opt["raceloc_health"])
                    {
                        RaceModeTags.UnionWith(new[] { "seedtree", "church" });
                    }
                    if (opt["raceloc_scadu"])
                    {
                        RaceModeTags.UnionWith(new[] { "cross", "fragment", "enemyfragment", "revered", "enemyrevered" });
                    }
                    if (opt["raceloc_shops"])
                    {
                        RaceModeTags.Add("raceshop");
                    }
                    if (opt["raceloc_altboss"])
                    {
                        RaceModeTags.Add("altboss");
                    }
                    if (opt["raceloc_talisman"])
                    {
                        RaceModeTags.Add("talisman");
                    }
                    if (!opt["night"])
                    {
                        NoRaceModeTags.Add("night");
                    }
                    if (opt["nocaves"])
                    {
                        NoRaceModeTags.Add("minidungeon");
                    }
                    if (opt["fog"] && opt["crawl"])
                    {
                        NoRaceModeTags.Add("nocrawl");
                    }
                    // TODO: Before full customization, find a way to add items to norandom slots.
                    // Until then, we may need to add some extra slots to fit all racemode items.
                    // Or alternatively - just add this logic in upgradeshop itself
                    if (opt["racemode_upgrades"] && opt["norandom"]
                        && !opt["raceloc_health"] && !opt["raceloc_altboss"] && !opt["raceloc_talisman"])
                    {
                        RaceModeTags.Add("upgradeshop");
                    }
                    NoKeyTags.UnionWith(NoRaceModeTags);
                    KeyTags.UnionWith(RaceModeTags);
                }
                if (explainPreset)
                {
                    Console.WriteLine($"RaceModeTags: {string.Join(" ", RaceModeTags)}");
                    Console.WriteLine($"NoRaceModeTags: {string.Join(" ", NoRaceModeTags)}");
                }
            }

            // Norandom items, ones required for rando logic and user-configured ones.
            if (ItemGroups.ContainsKey("norandom"))
            {
                NorandomItems.UnionWith(ItemGroups["norandom"]);
            }
            if (usePreset)
            {
                NorandomItems.UnionWith(ConfigPreset.NorandomItems.SelectMany(i => i.Keys));
            }
            if (merge?.NorandomItems != null)
            {
                NorandomItems.UnionWith(merge.NorandomItems);
            }
            // Removed items, some from features like removerold and removecraft, and also user-configured ones.
            // Users cannot remove items which are unmissable, and the latter should take priority.
            string itemsDesc(IEnumerable<ItemKey> items) => string.Join("; ", items.Select(game.Name));
            if (usePreset)
            {
                HashSet<ItemKey> unmissableItems = new();
                foreach (Placement place in ConfigPreset.Placements)
                {
                    if (place.Disabled)
                    {
                        continue;
                    }
                    if (place.Excludes != null && place.Excludes.Any(p => p.Tag == "missable"))
                    {
                        unmissableItems.UnionWith(place.Keys);
                    }
                }
                // Allow partial inclusion of item groups (note filtering of key items in preset processing is not partial, but also forbidden by UI)
                List<ItemKey> removeItems = ConfigPreset.RemoveItems.SelectMany(i => i.Keys).Distinct().ToList();
                List<ItemKey> missableRemove = removeItems.Except(unmissableItems).ToList();
                if (removeItems.Count != missableRemove.Count)
                {
                    Console.WriteLine($"Warning: Ignoring these RemoveItems because they are required to be unmissable: {itemsDesc(removeItems.Intersect(unmissableItems))}");
                }
                RemoveItems.UnionWith(missableRemove);
            }
            if (ItemGroups.ContainsKey("remove"))
            {
                // The remove group is authoritative and can contain key items, so always add it
                RemoveItems.UnionWith(ItemGroups["remove"]);
            }

            if (usePreset)
            {
                foreach (Placement place in ConfigPreset.Placements)
                {
                    if (place.Disabled)
                    {
                        continue;
                    }
                    if (place.Mode == PlacementMode.Norandom)
                    {
                        NorandomItems.UnionWith(place.Keys);
                    }
                    if (place.Mode == PlacementMode.Important || place.Type == PlacementType.KeyItems)
                    {
                        // This doesn't mean anything for tags (already processed in Requires/Excludes above), but does affect the placement pass
                        RaceModeItems.UnionWith(place.Keys);
                    }
                }
            }
            else if (ann.SpecialModes != null)
            {
                foreach (SpecialModeAnnotation group in ann.SpecialModes)
                {
                    bool raceMode = group.RaceSwitch != null && opt.IsSwitchEnabled(group.RaceSwitch);
                    bool norandomMode = group.NorandomSwitch != null && opt.IsSwitchEnabled(group.NorandomSwitch);
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
                // Process race mode items and race mode exclude tags together
                // For presets, these tags are already populated above
                foreach (ItemKey key in RaceModeItems)
                {
                    AddMulti(ExcludeTags, key, NoRaceModeTags);
                    AddMulti(RequireTags, key, RaceModeTags);
                }
            }
            if (explainPreset)
            {
                Console.WriteLine($"NorandomItems: {string.Join(", ", NorandomItems.Select(game.Name))}");
                Console.WriteLine($"RaceModeItems: {string.Join(", ", RaceModeItems.Select(game.Name))}");
            }

            // Fill in Items and MultiItems for logic. This can be done earlier, but can't be done while processing items as multi-items depends on both multikey group and preset.
            foreach (ConfigItemAnnotation configItems in ann.ConfigItems)
            {
                foreach (ItemAnnotation item in configItems.Items)
                {
                    if (!configItems.NoConfigNames && item.ConfigName != null)
                    {
                        if (Items.ContainsKey(item.ConfigName)) throw new Exception($"Duplicate item under config name {item.ConfigName}");
                        if (item.Keys.Count != 1)
                        {
                            // Custom weapons could be present in otheritems (mainly used with removeitem and exclude tags), markhints (no config names) and any weapon configuration.
                            // TODO: Is this fixed with keyAliases...?
                            Console.WriteLine($"Warning: Item group {item.ConfigName} won't be comprehensive for item {item.ID ?? item.Name} which has multiple ids: {string.Join(", ", item.Keys)}");
                        }
                        ItemKey key = item.Keys[0];
                        int shardReq = -1;
                        // Adding an item to MultiItem causes rewriting later, so avoid doing that for DLC-only key items
                        // But still add the config name in case that comes in up in logic somewhere
                        bool noMulti = game.EldenRing && game.IsEldenDlcItem(key) && !opt["dlc"];
                        // Use preset before it's processed because processing requires mapping groups to
                        if (itemPreset != null)
                        {
                            if (itemPreset.ItemShards != null && !noMulti)
                            {
                                ItemPreset.ItemShard shard = itemPreset.ItemShards.Find(s => key.Equals(s.Key));
                                if (shard != null)
                                {
                                    item.KeyCount = shard.Count;
                                    shardReq = shard.ReqCount;
                                }
                            }
                        }
                        else if (!noMulti && item.ConfigName == "messmerskindling" && opt.GetInt(RandomizerOptions.IntOpt.Messmershard, out int shardCount))
                        {
                            item.KeyCount = shardCount;
                            if (!opt.GetInt(RandomizerOptions.IntOpt.Shardreq, out shardReq))
                            {
                                shardReq = shardCount;
                            }
                        }
#if DEBUG
                        if (item.KeyCount == 0 && opt["shardall"] && ItemGroups["multikey"].Contains(key)
                            && !NorandomItems.Contains(key) && !noMulti
                            && !(item.ConfigName == "roldmedallion" && opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out _)))
                        {
                            item.KeyCount = 2;
                            shardReq = 2;
                        }
#endif
                        if (item.KeyCount == 0)
                        {
                            Items[item.ConfigName] = key;
                        }
                        else
                        {
                            if (shardReq >= 0 && shardReq > item.KeyCount)
                            {
                                // UI validation should prevent this now
                                throw new Exception($"{shardReq} {game.Name(key)} Shards are required but only {item.KeyCount} shards are configured");
                            }
                            // Item logic validation
                            if (item.ConfigName == "roldmedallion" && opt.GetInt(RandomizerOptions.IntOpt.RunesRold, out int roldRune))
                            {
                                throw new Exception($"{game.Name(key)} cannot be split into shards because it was replaced with a {roldRune} Great Rune requirement");
                            }
                            else if (shardReq >= 0 && noMulti)
                            {
                                throw new Exception($"Internal error: {game.Name(key)} is configured as a multi-item but DLC is disabled");
                            }
                            List<string> names = Enumerable.Range(1, item.KeyCount).Select(i => $"{item.ConfigName}{i}").ToList();
                            MultiItems[key] = new MultiItem
                            {
                                Key = key,
                                BaseName = item.ConfigName,
                                Names = names,
                                IsShard = shardReq >= 0,
                                ReqCount = shardReq,
                            };
                            foreach (string name in names)
                            {
                                Items[name] = key;
                            }
                        }
                    }
                }
            }
            // More logic validation. It is possible to allow this but keep it simple for now
            // This would only result in a true softlock if Dragonbarrow were literally blocked off and Dectus was required to reach Altus
            if (opt["latedragonbarrow"] && Items.TryGetValue("dectusmedallionright", out ItemKey dectusRight) && NorandomItems.Contains(dectusRight))
            {
                throw new Exception($"You must randomize key items if Leyndell is required to reach Dragonbarrow, as otherwise Dectus Medallion (Right) is not considered reachable");
            }
            MoreMaxSlots = opt["moremaxslots"] || PresetOpts.Contains("moremaxslots");

            if (usePreset)
            {
                // Misc other metadata only used by presets
                if (game.EldenRing)
                {
                    Dictionary<ItemKey, int> weaponTypes = ParameterizedSilos[ItemSiloType.WeaponType] = new();
                    foreach (PARAM.Row row in game.Param("EquipParamWeapon").Rows)
                    {
                        int type = (ushort)row["wepType"].Value;
                        weaponTypes[new ItemKey(ItemType.Weapon, row.ID)] = type;
                    }
                    Dictionary<ItemKey, int> armorTypes = ParameterizedSilos[ItemSiloType.ArmorType] = new();
                    foreach (PARAM.Row row in game.Param("EquipParamProtector").Rows)
                    {
                        int type = (byte)row["protectorCategory"].Value;
                        armorTypes[new ItemKey(ItemType.Protector, row.ID)] = type;
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
                    area.ReqExpr = Expr.True;
                    start = area.Name;
                }
                else
                {
                    area.ReqExpr = parser.ParseOrThrow(area.Req).Simplify();
                    if (game.EldenRing && area.HasTag("dlc"))
                    {
                        area.ReqExpr = Expr.And(Expr.Named("dlc"), area.ReqExpr);
                    }
                }
            }
            foreach (AreaAnnotation area in ann.Areas)
            {
                parseReq(area);
                Areas[area.Name] = area;
                AllAreas[area.Name] = new List<LocationScope>();
                if (area.Maps != null || area.MapNames != null)
                {
                    string[] mapIds = area.Maps == null ? area.MapNames.Select(m => m.Split(' ')[0]).ToArray() : area.Maps.Split(' ');
                    foreach (string mapId in mapIds)
                    {
                        AddMulti(mapIdAreas, mapId, area.Name);
                    }
                    if (mapIds.Length >= 8)
                    {
                        bigMultiAreas.Add(area.Name);
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
            if (ann.ConfigVars != null)
            {
                foreach ((string name, string req) in ann.ConfigVars)
                {
                    DefaultConfigExprs[name] = parser.ParseOrThrow(req).Simplify();
                }
            }
            if (ann.StartingGroups != null)
            {
                foreach (StartingGroupAnnotation group in ann.StartingGroups)
                {
                    StartingGroups[group.GroupName] = group;
                    foreach (StartingItemAnnotation items in group.Entries)
                    {
                        foreach (ItemAnnotation item in items.Items)
                        {
                            item.Keys = itemsForAnnotation(item);
                            if (!item.Keys.All(i => i.Type == ItemType.Goods || i.Type == ItemType.Accessory))
                            {
                                throw new Exception($"Invalid starting gift item in config: {string.Join(", ", item.Keys)}");
                            }
                        }
                        if (items.Quantity == null)
                        {
                            items.Quantities = new List<int> { 1 };
                        }
                        else
                        {
                            items.Quantities = items.Quantity.Split(' ').Select(int.Parse).ToList();
                        }
                    }
                }
            }

            if (!processSlots) return;

            // Slots
            Dictionary<string, SlotAnnotation> strSlots = new Dictionary<string, SlotAnnotation>();
            HashSet<string> okayMissingSlots = new();
            foreach (SlotAnnotation slot in ann.Slots)
            {
                string key = slot.Key.Substring(slot.Key.IndexOf(',') + 1);
                strSlots[key] = slot;
                // Special case for DLC, just to avoid spamming unused slots from scraper missing maps
                bool excluded(string tag)
                {
                    if (opt[tag])
                    {
                        return false;
                    }
                    if (slot.Area != null && Areas.TryGetValue(slot.Area, out AreaAnnotation areaAnn) && areaAnn.HasTag(tag))
                    {
                        return true;
                    }
                    // TagList not initialized yet
                    if (slot.Tags != null && slot.Tags.Contains(tag))
                    {
                        return true;
                    }
                    return false;
                };
                if (game.EldenRing && excluded("dlc"))
                {
                    okayMissingSlots.Add(key);
                }
                else if (game.DS3 && (excluded("dlc1") || excluded("dlc2")))
                {
                    okayMissingSlots.Add(key);
                }
            }
            foreach (KeyValuePair<LocationScope, List<ItemLocKey>> entry in data.Locations)
            {
                // Skip model keys for now, there is not much to configure
                if (entry.Key.Type == ScopeType.Model || entry.Key.Type == ScopeType.Asset)
                {
                    continue;
                }
                string key = entry.Key.ToString();
                if (!strSlots.ContainsKey(key))
                {
                    // Warn about this. Except for merging mods, as mods can't add slots yet
                    if (!game.HasMods)
                    {
                        Console.WriteLine($"Warning: No annotation for slot {key}, with slots {string.Join(", ", entry.Value.Select(s => $"{s} at {string.Join(", ", data.GetItemLoc(s).Keys)}"))}");
                    }
                    continue;
                }
                SlotAnnotation slot = strSlots[key];
                Slots[entry.Key] = slot;
                strSlots.Remove(key);
            }
            if (strSlots.Count > 0 && !opt["dumpann"])
            {
                // Avoid known spam from DLC maps missing
                strSlots = new(strSlots.Where(e => !okayMissingSlots.Contains(e.Key)));
                if (strSlots.Count > 0)
                {
                    Console.WriteLine($"Warning: Keys [{string.Join(", ", strSlots.Keys)}] are in config but not in game. Make sure your base mods have all of the required item lots. Will try to proceed without them, but this will result in errors if any key or important items are missing.");
                }
            }

            // Simple post processing and validation
            foreach ((LocationScope scope, SlotAnnotation slot) in Slots)
            {
                if (!Areas.TryGetValue(slot.Area, out AreaAnnotation areaAnn) && slot.Area != "unknown")
                {
                    throw new Exception($"Slot {scope} has unknown area \"{slot.Area}\"");
                }
                slot.AreaUntil = areaAnn?.Until;
                slot.SetTags(scope.OnlyShops, opt, areaAnn?.TagList, KeyItemSilo);
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
                    slot.ItemReqs = new List<string>();
                    slot.AreaReqs = new List<string>();
                    foreach (string questReq in slot.QuestReqs.Split(' '))
                    {
                        if (Items.ContainsKey(questReq))
                        {
                            slot.ItemReqs.Add(questReq);
                        }
                        // Handle multi-items this way. Otherwise use mapping from base name to MultiItems. Check uses of ItemReq to verify this.
                        else if (Items.ContainsKey(questReq + "1"))
                        {
                            slot.ItemReqs.Add(questReq + "1");
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

            // With slot data, and specifically tags, assign per-slot data from presets - premium groups and non-random locations
            UsePremiumGroups = usePreset;
            if (usePreset)
            {
                // Non-random locations are only tags currently
                NorandomTags.UnionWith(ConfigPreset.NorandomLocations.Select(l => l.Tag));
                // Initial pass of matching individual tags to sets of items, as all groups are intersected together
                // e.g. Dragon Heart shops may only contain incantations and also legendary/rare items
                Dictionary<string, HashSet<ItemKey>> tagItems = new();
                foreach (PremiumGroup group in ConfigPreset.PremiumGroups)
                {
                    if (group.Disabled)
                    {
                        continue;
                    }
                    // This set is shared among multiple tags and should be cloned before set operations
                    HashSet<ItemKey> items = new(group.Keys);
                    foreach (ItemPreset.LocationRef loc in group.Locations)
                    {
                        HashSet<ItemKey> locItems = items;
                        if (tagItems.TryGetValue(loc.Tag, out HashSet<ItemKey> existItems))
                        {
                            items = new(items);
                            items.IntersectWith(existItems);
                        }
                        tagItems[loc.Tag] = locItems;
                    }
                }
                // PremiumGroups need to take RequireTags into account to avoid unplaceable key items etc
                // Note this doesn't add them in Anywhere mode; reconsider this if placement failures result.
                Dictionary<string, HashSet<ItemKey>> requiredItems = new();
                foreach (Placement place in ConfigPreset.Placements)
                {
                    // Note ImportantGroup.Requires is denormalized into place.Requires in PlacementMode.Important, as part of resolve flow
                    if (place.Disabled || place.Requires == null)
                    {
                        continue;
                    }
                    foreach (ItemPreset.LocationRef loc in place.Requires)
                    {
                        // Only record this if it intersects a premium tag. Ignore exclude tags for now as this can only add items to be placed, which are ignored if excluded
                        if (!tagItems.ContainsKey(loc.Tag))
                        {
                            continue;
                        }
                        AddMulti(requiredItems, loc.Tag, place.Keys);
                    }
                }
                // Finally, intersection of everything per location. Key is sorted comma-separated tag list, for caching purposes.
                Dictionary<string, HashSet<ItemKey>> tagGroupItems = new();
                List<string> augments = new();
                foreach ((LocationScope scope, SlotAnnotation slot) in Slots)
                {
                    List<string> premiumTags = slot.TagList.Intersect(tagItems.Keys).ToList();
                    if (premiumTags.Count == 0)
                    {
                        continue;
                    }
                    string premiumKey = string.Join("+", premiumTags.OrderBy(x => x).Distinct());
                    if (!tagGroupItems.TryGetValue(premiumKey, out HashSet<ItemKey> items))
                    {
                        items = IntersectAll(premiumTags.Select(t => tagItems[t]));
                        if (items.Count == 0)
                        {
                            // This could also be an explicit error
                            Console.WriteLine($"Warning: Ignoring premium rule for location with tags {premiumKey} because no eligible items were found there");
                            tagGroupItems[premiumKey] = null;
                            continue;
                        }
                        List<HashSet<ItemKey>> requirableItems = premiumTags.Select(t => requiredItems.TryGetValue(t, out HashSet<ItemKey> s) ? s : null).Where(s => s != null).ToList();
                        if (requirableItems.Count > 0)
                        {
                            // This is done here, rather than postprocessing tagItems, to avoid these items being intersected away in the above step
                            HashSet<ItemKey> locItems = new(items);
                            locItems.UnionWith(UnionAll(requirableItems));
                            if (locItems.Count > items.Count)
                            {
                                augments.Add(premiumKey);
                                items = locItems;
                            }
                        }
                        tagGroupItems[premiumKey] = items;
                        // Console.WriteLine($"{premiumKey} -> {itemsDesc(items)}");
                    }
                    else if (items == null)
                    {
                        continue;
                    }
                    PremiumGroups[scope] = items;
                }
                if (augments.Count > 0)
                {
                    Console.WriteLine($"Note: Augmenting premium rules for locations with these tag combinations: {string.Join(", ", augments)}");
                }
            }

            // With the slot data, also postprocess all ItemLocation instances to add DLC/silo info and MultiItems info
            bool printLocNaming = false;
            foreach ((ItemKey item, ItemLocations itemLocs) in data.Items)
            {
                // Process these, removing items found in the config, then try to assign the rest
                if (!MultiItems.TryGetValue(item, out MultiItem multiItem)) continue;
                List<string> names = multiItem.Names;
                List<string> usedNames = new();
                // This approach is flexible, adding known multi-item locations and automatically tagging others
                // First, items in game data with multiple instances
                foreach ((LocationScope locScope, ItemLocation itemLoc) in itemLocs.Locations)
                {
                    if (locScope.Type != ScopeType.Event) continue;
                    if (Slots.TryGetValue(locScope, out SlotAnnotation slotAnn) && names.Contains(slotAnn.ItemName))
                    {
                        itemLoc.ItemName = slotAnn.ItemName;
#if DEBUG
                        // This might be eventually fine
                        if (usedNames.Contains(slotAnn.ItemName))
                        {
                            throw new Exception($"Internal error: {slotAnn.ItemName} appears multiple times in config");
                        }
#endif
                        usedNames.Add(slotAnn.ItemName);
                        if (printLocNaming) Console.WriteLine($"Mapping {itemLoc.ItemName} -> {locScope}");
                    }
                }
                // Split items. The other names are added in AddSpecialItems
                foreach ((LocationScope locScope, ItemLocation itemLoc) in itemLocs.Locations)
                {
                    if (locScope.Type != ScopeType.Event) continue;
                    // Unused lots have key items
                    if (Slots.TryGetValue(locScope, out SlotAnnotation slotAnn) && slotAnn.HasTag("ignore")) continue;
                    if (itemLoc.ItemName == null)
                    {
                        string remainingName = names.Except(usedNames).FirstOrDefault(names[0]);
                        itemLoc.ItemName = remainingName;
                        usedNames.Add(remainingName);
                        if (printLocNaming) Console.WriteLine($"Mapping remaining {remainingName} -> {locScope}");
                    }
                }
            }
            foreach ((LocationScope locScope, List<ItemLocKey> locKeys) in data.Locations)
            {
                if (!game.EldenRing) break;
                bool dlc = false;
                // I guess unknown locations should get broken down like this?
                bool partDlc = true;
                string area = null;
                if (Slots.TryGetValue(locScope, out SlotAnnotation slotAnn))
                {
                    // Added from area tags, or tagged manually for e.g. DLC bell bearing shops
                    dlc = slotAnn.TagList.Contains("dlc");
                    // Temporary handling.
                    partDlc = slotAnn.TagList.Contains("partdlc");
                    area = slotAnn.Area;
                }
                foreach (ItemLocKey itemLocKey in locKeys)
                {
                    ItemLocation itemLoc = data[itemLocKey];
                    ItemScope scope = itemLoc.Scope;
                    bool slotDlc = false;
                    // At this point, there are no Special scopes, those have to be resolved later
                    if (partDlc || locScope.Type == ScopeType.Special)
                    {
                        if (game.IsEldenDlcItem(itemLocKey.Item))
                        {
                            slotDlc = true;
                        }
                    }
                    if (scope.Type == ScopeType.Model)
                    {
                        if (game.IsEldenDlcModel(scope.ID))
                        {
                            slotDlc = true;
                        }
                    }
                    itemLoc.DLC = dlc || slotDlc;
                    SetAreaSilo(itemLoc, area);
                }
            }
        }

        private void SetAreaSilo(ItemLocation itemLoc, string area)
        {
            if (OverallSilo == AreaSiloType.DLC)
            {
                itemLoc.Silo = AreaSilo.ForDlc(itemLoc.DLC);
            }
            // TODO: Region silo
        }

        public void AddSpecialItems()
        {
            // Add special unique items into game
            foreach (ItemKey addItem in ItemGroups["add"])
            {
                // Mostly to exclude Path of the Dragon from being added when key items are not randomized
                if (NorandomItems.Contains(addItem)) continue;
                ItemLocation itemLoc = data.AddLocationlessItem(addItem);
                // TODO: This is a bad heuristic for O Mother so eliminate this from Elden Ring for now
                // TODO: Support region lock here
                itemLoc.DLC = game.IsEldenDlcItem(addItem);
                SetAreaSilo(itemLoc, null);
            }
            if (ItemGroups.TryGetValue("multikey", out List<ItemKey> multiKeys))
            {
                List<ItemKey> norandomMultikey = new();
                foreach (ItemKey key in multiKeys)
                {
                    if (!MultiItems.TryGetValue(key, out MultiItem multiItem) || !data.Items.TryGetValue(key, out ItemLocations itemLocs)) continue;
                    // These options are awkward together, just avoid adding the items and hope they don't come up in permutations
                    if (NorandomItems.Contains(key))
                    {
                        norandomMultikey.Add(key);
                    }
                    HashSet<string> usedNames = new();
                    bool dlc = game.IsEldenDlcItem(key);
                    foreach ((LocationScope locScope, ItemLocation itemLoc) in itemLocs.Locations)
                    {
                        // It really should be present at this point
                        if (itemLoc.ItemName == null) continue;
                        usedNames.Add(itemLoc.ItemName);
                        dlc |= itemLoc.DLC;
                    }
                    foreach (string name in multiItem.Names)
                    {
                        if (usedNames.Contains(name)) continue;
                        ItemLocation itemLoc = data.AddLocationlessItem(key);
                        itemLoc.DLC = dlc;
                        SetAreaSilo(itemLoc, null);
                        itemLoc.ItemName = name;
                    }
                }
                if (norandomMultikey.Count > 0)
                {
                    // This could have alternate behaviors like keeping 1 fixed and randomizing the rest, but it might be unintuitive to configure it in this way
                    throw new Exception($"The following items are both split into shards and not randomized, which is not supported: {string.Join(", ", norandomMultikey.Select(game.Name))}");
                }
            }
        }

        public void AddFiniteEquipmentCopies()
        {
            // For copydrops option
            // Map to whether in DLC (true) or base game (false), based on model ids of the drops
            Dictionary<ItemKey, List<bool>> items = new();
            bool isEquipment(ItemKey key)
            {
                if (key.Type != ItemType.Weapon && key.Type != ItemType.Protector)
                {
                    return false;
                }
                if (game.EldenRing)
                {
                    return !game.IsEldenArrow(key) && game.ItemNames.TryGetValue(key, out string name) && !name.Contains("ERROR");
                }
                else if (game.DS3)
                {
                    return key.Type == ItemType.Weapon
                        ? key.ID >= 1000000 && key.ID < 24000000
                        : key.ID >= 19000000;

                }
                else throw new NotImplementedException();
            }
            foreach ((ItemKey key, ItemLocations locs) in data.Items)
            {
                // Could look up slot to see if acquirable in game, but this is probably fine
                if (isEquipment(key) && locs.Locations.Keys.All(loc => loc.Type == ScopeType.Model))
                {
                    List<bool> vals = new();
                    // Don't duplicate it if there's no silos, I suppose
                    if (OverallSilo == AreaSiloType.DLC)
                    {
                        if (locs.Locations.Values.Any(loc => !loc.DLC)) vals.Add(false);
                        if (locs.Locations.Values.Any(loc => loc.DLC)) vals.Add(true);
                    }
                    else if (game.EldenRing)
                    {
                        // Note this will add DLC drops even if DLC is not enabled, but it will labeled as DLC so excluded during Permutation creation.
                        vals.Add(game.IsEldenDlcItem(key));
                    }
                    else if (game.DS3)
                    {
                        // DS3 doesn't have a DLC-only mode, and all DLC weapons are usable without owning the DLC, so it's fine
                        vals.Add(false);
                    }
                    items[key] = vals;
                }
            }
            foreach ((ItemKey item, List<bool> vals) in items)
            {
                foreach (bool val in vals)
                {
                    ItemLocation loc = data.AddLocationlessItem(item);
                    loc.DLC = val;
                    // TODO: Region lock?
                    SetAreaSilo(loc, null);
                }
            }
        }

        public void AddRandomaniaItems()
        {
            Dictionary<int, string> names = new()
            {
                [2550] = "Blue Shroom",
                [2551] = "Red Shroom",
                [2552] = "Gold Shroom",
            };
            Dictionary<int, string> infos = new()
            {
                [2550] = "Can be redeemed for any base game item",
                [2551] = "Inflicts a debuff on the enemy team",
                [2552] = "Reveals a Shard's location",
            };
            Dictionary<int, int> baseItems = new()
            {
                [2550] = 920, // Thawfrost
                [2551] = 910, // Stanching
                [2552] = 960, // Clarifying
            };
            Dictionary<int, int> amounts = new()
            {
                [2550] = 20,
                [2551] = 20,
                [2552] = 5,
            };
            // A bit inappropriate to make this edit here, but allow it for now
            foreach (int id in names.Keys)
            {
                PARAM.Row row = game.AddRow("EquipParamGoods", id, baseItems[id]);
                // row["refCategory"].Value = (byte)0;
                row["refId_default"].Value = 5025;
                row["iconId"].Value = id;
                row["rarity"].Value = (byte)2;
                ItemKey key = new ItemKey(ItemType.Goods, id);
                game.AddItemName(key, names[id]);
                game.ItemFMGs["GoodsName"][id] = names[id];
                game.ItemFMGs["GoodsInfo"][id] = infos[id];
                game.ItemFMGs["GoodsCaption"][id] = infos[id];
                AddMulti(ExcludeTags, key, "nodlc");
                AddMulti(ExcludeTags, key, "shop");
                for (int i = 0; i < amounts[id]; i++)
                {
                    ItemLocation loc = data.AddLocationlessItem(key);
                    loc.DLC = true;
                    SetAreaSilo(loc, null);
                }
            }
            Console.WriteLine("Added Randomania shrooms");
            Console.WriteLine();
            // For testing: torrent is good 130, default icon 13
        }

        public void WeaponSpam(GameData game)
        {
            List<ItemKey> weps = new();
            foreach ((ItemKey key, ItemLocations locs) in data.Items)
            {
                // Can't really see if it's acquirable in game, so do this instead
                if (key.Type == ItemType.Weapon && game.ItemNames.TryGetValue(key, out string name) && !name.Contains("ERROR"))
                {
                    weps.Add(key);
                }
            }
            int copies = 2;
            foreach (ItemKey wep in weps)
            {
                for (int i = 0; i < copies; i++)
                {
                    data.AddLocationlessItem(wep);
                }
            }
        }

        public void AddMaterialItems(bool matsRandomized)
        {
            bool debug = false;
            foreach (PlacementRestrictionAnnotation restrict in ItemRestrict.Values)
            {
                if (restrict.Unique == null) continue;
                bool fillInPercents = restrict.Unique.Any(slot => slot.Percent > 0);
                if (restrict.MinAvailable <= 0 && !fillInPercents) continue;
                ItemLocations itemLocs = data.Items[restrict.Key];
                // Use restrictions when an item is placed in the base game or in a base game+DLC run. If it's siloed to DLC, don't use the restriction.
                // TODO: Update this when DLC restrictions are added
                int baseCount = itemLocs.Locations.Count(l => l.Key.Type == ScopeType.Event && !l.Value.DLC);
                int guaranteeAmount = restrict.MinAvailable;
                if (debug) Console.WriteLine($"Locations for {restrict.Name}: {baseCount} base, {restrict.MatsAmount} mats");
                // Whittle down requirements based on non-randomized materials
                if (restrict.MatsAmount > 0 && !matsRandomized)
                {
                    guaranteeAmount -= restrict.MatsAmount;
                    // Reduce requirements if materials are reliably present
                    int totalSlotAmount = restrict.Unique.Sum(slot => slot.Amount);
                    // previously: mats / 2
                    int cutMats = restrict.MatsAmount;
                    float cutRate = (float)cutMats / totalSlotAmount;
                    foreach (PlacementSlotAnnotation slot in restrict.Unique)
                    {
                        if (slot.Amount <= 0) continue;
                        int reduced = slot.Amount - (int)(slot.Amount * cutRate);
                        reduced = Math.Max(5, reduced);
                        if (debug) Console.WriteLine($"  Cutting {restrict.Name} slot from {slot.Amount} to {reduced}");
                        slot.Amount = reduced;
                    }
                }
                // Fill in amounts from percentages
                if (fillInPercents)
                {
                    int totalAmount = guaranteeAmount > 0 ? Math.Max(guaranteeAmount, baseCount) : baseCount;
                    int cumPercent = 0;
                    int cumAmount = 0;
                    foreach (PlacementSlotAnnotation slot in restrict.Unique)
                    {
                        if (slot.Percent <= 0 || slot.Amount > 0) continue;
                        double amount = totalAmount * slot.Percent / 100.0;
                        // Nudge with rounding to try to get a final total as close as possible
                        slot.Amount = Math.Max(1, (int)Math.Round(amount));
                        cumPercent += slot.Percent;
                        cumAmount += slot.Amount;
                    }
                    // A pass to try to make it add up to the ideal total. Doing this optimally is tricky, just nudge up or down.
                    double idealTotal = totalAmount * cumPercent / 100.0;
                    int amtToAdd = (int)Math.Round(idealTotal - cumAmount);
                    foreach (PlacementSlotAnnotation slot in restrict.Unique.OrderByDescending(slot => slot.Percent))
                    {
                        if (slot.Percent <= 0) continue;
                        if (amtToAdd > 0)
                        {
                            slot.Amount++;
                            amtToAdd--;
                        }
                        else if (amtToAdd < 0 && slot.Amount > 1)
                        {
                            slot.Amount--;
                            amtToAdd++;
                        }
                        if (debug) Console.WriteLine($"  Percent {slot.Percent} out of {totalAmount} is {slot.Amount}");
                    }
                }
                if (guaranteeAmount > 0)
                {
                    int toAdd = guaranteeAmount - baseCount;
                    if (debug) Console.WriteLine($"  Adding {toAdd} to reach {guaranteeAmount}");
                    if (toAdd > 0)
                    {
                        for (int i = 0; i < toAdd; i++)
                        {
                            // At least for base game material progression, keep it limited to base game
                            ItemLocation loc = data.AddLocationlessItem(restrict.Key);
                            SetAreaSilo(loc, null);
                        }
                    }
                }
            }
        }

        public void ProcessRestrictions(RandomizerOptions opt, EnemyRandomizer.Result enemies)
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
            void processLocationList(List<PlacementSlotAnnotation> slots, bool allowAny)
            {
                if (slots == null) return;
                slots.RemoveAll(slot =>
                {
                    slot.Before = processLocations(slot.Before);
                    slot.UpTo = processLocations(slot.UpTo);
                    slot.UpToAny = processLocations(slot.UpToAny);
                    slot.After = processLocations(slot.After);
                    slot.Inside = processLocations(slot.Inside);
                    // In unrestricted mode, all of the above are overridden. This doesn't affect key item options, which use KeyAreas instead.
                    if (allowAny)
                    {
                        slot.AllowAny = true;
                    }
                    return slot.Before == null && slot.UpTo == null && slot.UpToAny == null && slot.After == null && slot.Inside == null;
                });
            }
            foreach (PlacementRestrictionAnnotation restrict in ItemRestrict.Values)
            {
                if (restrict.KeyAreas != null)
                {
                    restrict.KeyAreas = processLocations(restrict.KeyAreas);
                }
                // For now, keep Bell Bearings roughly where they are, even in fog mode.
                bool allowAny = opt["fog"] && (restrict.Name == null || !restrict.Name.Contains("Bell Bearing"));
                processLocationList(restrict.Unique, allowAny);
                // TODO: How does this affect DS3?
                if (restrict.Unique != null && restrict.Unique.Count == 0) restrict.Unique = null;
                processLocationList(restrict.Drop, allowAny);
                processLocationList(restrict.Shop, allowAny);
            }
        }

        // For creating new items, like esoteric texts -> skills
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

        // -- Config access
        public AreaSilo GetItemAreaSilo(ItemKey item)
        {
            AreaSilo tagAreaSilo = AreaSilo.Default;
            if (ExcludeTags.TryGetValue(item, out HashSet<string> excludeTags))
            {
                if (excludeTags.Contains("dlc")) tagAreaSilo = AreaSilo.ForDlc(false);
                else if (excludeTags.Contains("nodlc")) tagAreaSilo = AreaSilo.ForDlc(true);
            }
            return tagAreaSilo;
        }

        public ItemSilo GetItemItemSilo(ItemKey key)
        {
            // This could be cached per-item if desired
            if (ConfigPreset == null || ConfigPreset.CustomSilos == null)
            {
                return ItemSilo.Default;
            }
            // First, exclude item silos for key/quest items until that system can be made more flexible
            if (ItemGroups["keyitems"].Contains(key) || ItemGroups["questitems"].Contains(key))
            {
                return ItemSilo.Default;
            }

            key = game.GetBaseWeapon(key);
            for (int i = 0; i < ConfigPreset.CustomSilos.Count; i++)
            {
                CustomSilo customSilo = ConfigPreset.CustomSilos[i];
                if (customSilo.Disabled)
                {
                    continue;
                }
                // Use first matching preset
                if (customSilo.Type == ItemSiloType.Manual && customSilo.Keys.Contains(key))
                {
                    return new ItemSilo(customSilo.Type, i, customSilo.Name ?? $"Custom {i}");
                }
                else if (ParameterizedSilos.TryGetValue(customSilo.Type, out Dictionary<ItemKey, int> types)
                         && types.TryGetValue(key, out int type))
                {
                    return new ItemSilo(customSilo.Type, type, $"{customSilo.Type} {type}");
                }
            }

            return ItemSilo.Default;
        }

        // -- Hints and heuristics
        public SlotAnnotation Slot(LocationScope scope)
        {
            if (Slots.ContainsKey(scope))
            {
                return Slots[scope];
            }
            // We don't really care where all of the models are, right now, although that can be part of annotation file in the future.
            // Just make something up
            HashSet<string> locationSet = new HashSet<string>();
            foreach (ItemLocKey key in data.GetBaseItemLocs(scope))
            {
                ItemLocation loc = data.GetItemLoc(key);
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
                    if (scope.Type == ScopeType.Asset)
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
            slot.SetTags(scope.OnlyShops);
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

        public string GetLocationHint(ItemLocKey key, SortedSet<string> specialLocation = null)
        {
            Func<string, string> capitalize = s => $"{s[0]}".ToUpperInvariant() + s.Substring(1);
            ItemLocation loc = data.GetItemLoc(key);
            LocationScope scope = loc.LocScope;
            List<ItemLocKey> sources = data.GetBaseItemLocs(scope);
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

        public string GetLocationDescription(ItemLocKey key, HashSet<string> filterTags = null, HashSet<string> excludeTags = null, EldenCoordinator coord = null)
        {
            // Previously this method took List<LocationKey> for targetLocation.Keys, for filtering, but this is no longer used.
            ItemLocation loc = data.GetItemLoc(key);
            LocationScope scope = loc.LocScope;
            // Can do data.Location(scope) to list all items in the same scope, but this is more specific and useful.
            List<ItemLocKey> sources = new List<ItemLocKey> { key };
            Func<bool, string> autoText = addPlace =>
            {
                SortedSet<string> models = new SortedSet<string>();
                SortedSet<string> locs = new SortedSet<string>();
                List<Entity> entities = loc.Keys
                    .SelectMany(k => k.Entities)
                    .Where(e => !string.IsNullOrEmpty(e.MapName))
                    .ToList();
                if (game.EldenRing) entities.RemoveAll(e => e.ModelName == "c1000");
                foreach (ItemLocKey sourceKey in sources)
                {
                    // ??
                    models.UnionWith(entities.Select(e => game.EntityName(e)));
                    locs.UnionWith(entities.Select(e => e.MapName));
                }
                string auto = $"From {(models.Count == 0 ? "???" : string.Join(", ", models))}";
                if (game.EldenRing && coord != null && scope.Type != ScopeType.Model)
                {
                    List<Entity> mapEntities = entities
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
                if (addPlace && scope.Type != ScopeType.Model)
                {
                    if (game.EldenRing)
                    {
                        Entity mapEntity = entities.OrderBy(e => (e.MapName == "m11_10_00_00" ? 1 : 0, e.MapName)).FirstOrDefault();
                        if (mapEntity != null && game.LocationName(mapEntity.MapName, out string mapName, pos: mapEntity.Position))
                        {
                            placeText = $" in {mapName}";
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
            if (Slots.TryGetValue(scope, out SlotAnnotation slot))
            {
                // Stop using addPlace autotext for Elden Ring, as all locations are defined
                bool auto = slot.Text == null || (slot.Text.Contains("aaa") || slot.Text == "auto");
                bool marked = slot.TagList.Contains("XX") || (slot.Text != null && slot.Text.Contains("XX"));
                // Detect overworld through having 8 maps
                if (auto && bigMultiAreas.Contains(slot.Area))
                {
                    text = autoText(true);
                }
                else
                {
                    string slotText = auto ? autoText(false) : slot.Text.TrimEnd(new char[] { '.' });
                    text = $" in {FullArea(slot.Area)}:{(marked ? " XX" : "")} {slotText}";
                }
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
            string extraInfo = "";
            if (tagslot)
            {
                extraInfo = $" {key}";
            }
            return $"{text}. {(original.Count == 1 ? "Replaces" : "In the spot of")} {string.Join(", ", original)}.{extraInfo}";
        }

        // Counting for key item assignment
        // TODO: Key item placement just needs to assign the locations directly by factoring out placement conditions
        // public enum UniqueCategory { KeyLot, KeyShop, QuestLot, QuestShop };
        public enum UniqueCategory { Key, Quest, KeyQuest, Other };
        public Dictionary<LocationScope, (UniqueCategory, int)> GetUniqueCounts()
        {
            Dictionary<LocationScope, (UniqueCategory, int)> locationSlots = new Dictionary<LocationScope, (UniqueCategory, int)>();
            foreach ((LocationScope scope, List<ItemLocKey> locKeys) in data.Locations)
            {
                if (!Slots.ContainsKey(scope) || scope.Type == ScopeType.Model || scope.UniqueID <= 0)
                {
                    continue;
                }
                SlotAnnotation existing = Slots[scope];
                // Quick tag check
                bool noKeyTags = existing.HasAnyTags(NoKeyTags);
                bool noQuestTags = existing.HasAnyTags(NoQuestTags);
                if (noKeyTags && noQuestTags) continue;
                if (existing.HasAnyTags(NorandomTags)) continue;
                // Check for any items not-randomized
                List<ItemKey> maybeRandom = locKeys.Select(slot => slot.Item).Except(NorandomItems).ToList();
                if (maybeRandom.Count == 0) continue;
                if (existing.TagItems != null && existing.TagItems.TryGetValue("norandom", out List<ItemKey> norandoms) && maybeRandom.Except(norandoms).Count() == 0)
                {
                    continue;
                }
                // This used to use data.GetBaseItemLocs(scope).Count which isn't right, the locations are equivalent.
                // Now calculate combined MaxSlots using routine similar to Permutation.IsAvailableSlot, based on option.
                int count = 1;
                if (MoreMaxSlots)
                {
                    // TODO: This will overcount slots where scopes share a base loc, so this may require something like BaseLocations in Permutation. Keep the max count lower for now.
                    count = 4;
                    foreach (Location loc in locKeys.SelectMany(key => data.GetItemLoc(key).Keys))
                    {
                        Location baseLoc = loc.BaseLocation;
                        count = Math.Min(count, baseLoc.MaxSlots);
                    }
                }
                bool isKey = false, isQuest = false;
                if (!noKeyTags && (KeyTags.Count == 0 || existing.HasAnyTags(KeyTags)))
                {
                    isKey = true;
                }
                if (!noQuestTags && (QuestTags.Count == 0 || existing.HasAnyTags(QuestTags)))
                {
                    isQuest = true;
                }
                UniqueCategory category;
                if (isKey && isQuest)
                {
                    category = UniqueCategory.KeyQuest;
                }
                else if (isKey)
                {
                    category = UniqueCategory.Key;
                }
                else if (isQuest)
                {
                    category = UniqueCategory.Quest;
                }
                else
                {
                    category = UniqueCategory.Other;
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
            public List<ConfigItemAnnotation> ConfigItems { get; set; } = new();
            public List<StartingGroupAnnotation> StartingGroups { get; set; }
            public List<ConfigAnnotation> Config { get; set; }
            public Dictionary<string, string> ConfigVars { get; set; }
            public List<AreaAnnotation> Events { get; set; }
            public List<AreaAnnotation> Areas { get; set; } = new();
            public List<PlacementRestrictionAnnotation> PlacementRestrictions { get; set; }
            public List<SpecialModeAnnotation> SpecialModes { get; set; }
            public ItemPreset DefaultPreset { get; set; }
            public List<ItemPriorityAnnotation> ItemExcludes { get; set; }
            public List<ItemPriorityAnnotation> ItemPriority { get; set; }
            public List<SlotAnnotation> Slots { get; set; } = new();
        }

        public class PresetInfo
        {
            // Could also just use the name files, but this is limited to items acquirable in the game
            // Does not include custom weapons as these should be included automatically.
            public SortedDictionary<string, SortedDictionary<int, string>> ItemNames { get; set; } = new();
            // Not present in map means 0 or 1 instances.
            public SortedDictionary<string, SortedDictionary<int, int>> ItemCounts { get; set; } = new();
            // -1 means no instances, not present means 1 instance.
            public SortedDictionary<string, SortedDictionary<int, int>> UniqueCounts { get; set; } = new();
            public SortedDictionary<string, PresetItemGroup> ItemGroups { get; set; } = new();
            public List<PresetTagInfo> Locations { get; set; } = new();

            public string GetItemName(ItemPreset.ItemRef item)
            {
                // TODO: Ensure this is valid in callers, localize, etc
                if (item.Name != null) return item.Name;
                if (item.ID == null || !ItemPreset.TryParseItemID(item.ID, out ItemKey key)) return null;
                if (ItemNames.TryGetValue(ItemPreset.ItemTypeNames[key.Type], out var idNames) && idNames.TryGetValue(key.ID, out string name))
                {
                    return name;
                }
                return null;
            }
        }

        public class PresetItemGroup
        {
            public SortedDictionary<string, List<int>> Items { get; set; } = new();
            public int ItemCount { get; set; }
            public int LocationCount { get; set; }
            public int UniqueCount { get; set; }
        }

        public class PresetTagInfo
        {
            public List<string> Tags { get; set; } = new();
            public int Count { get; set; }
            // Number of slots, not deduped by identical descriptions
            public int RealCount { get; set; }
            // Map from slot text to area index, currently only for displaying lists
            public Dictionary<string, int> Locations { get; set; }
        }

        public class SpecialModeAnnotation
        {
            public string RaceSwitch { get; set; }
            public string NorandomSwitch { get; set; }
            public string Includes { get; set; }
            public string Comment { get; set; }
            public List<string> Names { get; set; }
            public List<ItemAnnotation> Items { get; set; }
        }

        public class ItemPriorityAnnotation
        {
            public bool NoPriority { get; set; }
            // Higher weights for different quantities of the item. Used only for upgrade stones in DS3.
            public int PriorityByCount { get; set; }
            public string ExcludeTags { get; set; }
            public string Includes { get; set; }
            public string Comment { get; set; }
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
            public string Comment { get; set; }
            public string Switch { get; set; }
            public string KeyAreas { get; set; }
            // Number of total locations to guarantee. If materials are randomized, MatsAmount is subtracted from this,
            // and if Amounts are defined, they are reduced by this portion.
            public int MinAvailable { get; set; }
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
            // Used to be -1 by default, so 0 should be treated as non-applicable
            public int Amount { get; set; }
            // Transformed to Amount based on existing slots, to enroll more items in area restriction logic.
            public int Percent { get; set; }
            public string Before { get; set; }
            public string UpTo { get; set; }
            public string UpToAny { get; set; }
            public string After { get; set; }
            public string Inside { get; set; }
            public bool UseGroups { get; set; }
            public string Comment { get; set; }
            [YamlIgnore]
            public bool AllowAny { get; set; }

            public HashSet<string> AllowedAreas(
                Dictionary<string, HashSet<string>> includedAreas,
                Dictionary<string, HashSet<string>> areaGroups,
                bool debug = false)
            {
                if (AllowAny)
                {
                    // Allow everything. But exclude final boss, manually for now
                    // This doesn't seem to actually work, or maybe it's random placement.
                    return new HashSet<string>(includedAreas.Keys.Except(new[] { "erdtree", "radagon" }));
                }
                // debug = Before == "moonlight" || Before == "ainsel";
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
                HashSet<string> allLocs = IntersectAll(requirements);
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
            public string Comment { get; set; }
            public string HintName { get; set; }
            public bool NoConfigNames { get; set; }
            public List<ItemAnnotation> Items { get; set; }
        }

        public class StartingGroupAnnotation
        {
            public string GroupName { get; set; }
            public string Comment { get; set; }
            public List<StartingItemAnnotation> Entries { get; set; }
        }

        public class StartingItemAnnotation : Taggable
        {
            public List<ItemAnnotation> Items { get; set; }
            // Space-separated numbers, or 1 if not specified
            public string Quantity { get; set; }
            public string Comment { get; set; }
            [YamlIgnore]
            public List<int> Quantities { get; set; }
        }

        public class ItemAnnotation
        {
            public string ConfigName { get; set; }
            // Set with ConfigName, adds 1 2 3 etc to all names
            public string Name { get; set; }
            public string ID { get; set; }
            public string EndID { get; set; }
            public int KeyCount { get; set; }
            public string Comment { get; set; }
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
            public string ItemName { get; set; }
            public string Tags { get; set; }
            [YamlIgnore]
            public HashSet<string> TagList { get; set; }
            [YamlIgnore]
            public Dictionary<string, List<ItemKey>> TagItems { get; set; }
            // Inherited from area
            [YamlIgnore]
            public string AreaUntil { get; set; }
            // TODO: Replace uses with TagList directly.
            public HashSet<string> GetTags() => TagList;
            internal void SetTags(bool shopOnly, RandomizerOptions opt = null, List<string> areaTags = null, AreaSiloType keyItemSilo = AreaSiloType.None)
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
                if (areaTags != null)
                {
                    TagList.UnionWith(areaTags);
                }
                // Add synthetic tag for shops - mainly to control when items are available unlimited. If not already present
                if (!TagList.Contains("shop") && !TagList.Contains("noshop"))
                {
                    TagList.Add(shopOnly ? "shop" : "noshop");
                }
                if (TagList.Contains("upgradeshop") && !TagList.Contains("boss"))
                {
                    // Do this to allow highly constrained runs to use upgrade shops (as above) without requiring key item checks there
                    // This shouldn't be done if there is an option to explicitly allow bearings to have key items
                    TagList.Add("nokey");
                }
                if (Until != null || AreaUntil != null)
                {
                    if (Until == "always" || ((opt["fog"] || opt["royalaccess"]) && !TagList.Contains("foguntil")))
                    {
                        // 'Until always' allows key item logic
                        // Fog gate randomizer is expected to make these permanently available as well, unless marked otherwise.
                        // In Elden Ring, royalaccess also nullifies the only Until in the game
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
                if (TagList.Contains("carp") && !opt["carpsanity"])
                {
                    TagList.Add("norandom");
                }
                if (TagList.Contains("ng+") && opt["nongplusrings"])
                {
                    TagList.Add("norandom");
                }
                if (TagList.Contains("tarnished") && !opt["tarnished"])
                {
                    // This is not the same as excluding it, as it's still in logic, but these are otherwise inconsequential items
                    TagList.Add("norandom");
                }
                if ((TagList.Contains("sorceries") || TagList.Contains("incantations")) && opt["spellshops"])
                {
                    // This is ignored when PremiumGroups is present
                    TagList.Add("restrict");
                }
                // TODO: Do this for any key item silo
                if (TagList.Contains("dlcend") && keyItemSilo == AreaSiloType.DLC)
                {
                    TagList.Add("end");
                }
                // Boss implies late, unless specified otherwise
                if (TagList.Contains("boss") && !TagList.Contains("early") && !TagList.Contains("mid"))
                {
                    TagList.Add("late");
                }
            }
            [YamlIgnore]
            public List<string> ItemReqs { get; set; }
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

        public class AreaAnnotation : Taggable
        {
            // Internal name
            public string Name { get; set; }
            // Display name
            public string Text { get; set; }
            // Info
            public string Comment { get; set; }
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
            // More explicit map listing, while setting up the list
            public List<string> MapNames { get; set; }
            // Do not use. I think we can delete this.
            public List<string> Aliases { get; set; }
            // Deprioritize the area for key item weights
            public bool BoringKeyItem { get; set; }
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
            public static readonly Expr True = new Expr(new List<Expr>(), true, null);
            public static readonly Expr False = new Expr(new List<Expr>(), false, null);
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

            public static Expr And(params Expr[] exprs)
            {
                return new Expr(exprs.ToList(), true);
            }

            public static Expr Or(params Expr[] exprs)
            {
                return new Expr(exprs.ToList(), false);
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
                    // This is used as a bit of a hack to turn these literals into a logical value right after they're parsed
                    if (name == "true")
                    {
                        return True;
                    }
                    else if (name == "false")
                    {
                        return False;
                    }
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
                            return expr.every ? True : False;
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

        // Runtime-only tracking for custom multi-items with key item logic
        public class MultiItem
        {
            public ItemKey Key { get; set; }
            public string BaseName { get; set; }
            public List<string> Names { get; set; }
            public int Count => Names.Count;
            // If true, script edits are required to make the item work.
            public bool IsShard { get; set; }
            // For shards, the number required. This currently only supports one gate per item.
            // As a result, existing multi-items like Imbued Sword Keys can't be further split, and all items are placed before the gate.
            // Expanding this with the current key item placement algorithm should probably done Great-Rune-style, with an explicit order.
            public int ReqCount { get; set; }
        }

        // This can probably go in a config, but it should be accessible from main UI easily
        public static readonly Dictionary<string, int[]> EldenTagCounts = new()
        {
            // none; dlc; minidungeon; dlc,minidungeon
            ["default_bossL"] = new[] { 30, 12, 0, 0 },
            ["default_keylocL"] = new[] { 13, 4, 0, 2 },
            ["raceloc_altbossL"] = new[] { 74, 21, 52, 8 },
            ["raceloc_healthL"] = new[] { 43, 0, 0, 0 },
            ["raceloc_scaduL"] = new[] { 0, 50, 0, 0 },
            ["raceloc_shopsL"] = new[] { 23, 1, 1, 0 },
        };
        public static readonly Dictionary<string, string> EldenTagOpts = new()
        {
            // A bit redundant to preset
            ["altboss"] = "raceloc_altbossL",
            ["boss"] = "default_bossL",
            ["church"] = "raceloc_healthL",
            ["cross"] = "raceloc_scaduL",
            ["fragment"] = "raceloc_scaduL",
            ["racemode"] = "default_keylocL",
            ["raceshop"] = "raceloc_shopsL",
            ["revered"] = "raceloc_scaduL",
            ["seedtree"] = "raceloc_healthL",
        };
    }
}
