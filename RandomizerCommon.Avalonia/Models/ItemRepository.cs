using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.ItemPreset;
using static RandomizerCommon.Messages;

namespace RandomizerCommon.Models
{
    public class ItemRepository
    {
        private readonly Annotations ann;
        private readonly PresetInfo presetAnn;
        private readonly Messages messages;

        // State taken as-is from PresetItemForm, not the most Avalonia-friendly

        // From display string to ItemRef
        private readonly Dictionary<string, ItemRef> toInternalItem = new();
        // From ItemRef.Group ?? ItemRef.ID to display name
        private readonly Dictionary<string, string> toDisplayItem = new();
        // From display string to LocationRef
        private readonly Dictionary<string, LocationRef> toInternalLoc = new();
        // From LocationRef.Tag to display name
        private readonly Dictionary<string, string> toDisplayLoc = new();

        // From preset config, for counts and name-checking
        private readonly Dictionary<string, List<PresetTagInfo>> tagLocations = new();
        // For applying item group filters mainly. Also includes dlc/nodlc groups not in config.
        private readonly Dictionary<string, HashSet<ItemRef>> tagItems = new();

        // Template for all lists to use
        private readonly List<AutocompleteItem> placementItems = new();
        private readonly List<AutocompleteItem> allItems = new();
        private readonly List<AutocompleteItem> multiKeyItems = new();
        private readonly List<AutocompleteItem> requirableLocations = new();
        private readonly List<AutocompleteItem> allLocations = new();
        private readonly List<AutocompleteItem> nonRegionLocations = new();
        private readonly List<AutocompleteItem> norandomLocations = new();

        public ItemRepository(Annotations ann, PresetInfo presetAnn)
        {
            this.ann = ann;
            this.presetAnn = presetAnn;
            // TODO: Respond to language changes
            this.messages = Messages.GetInstance();

            // Set up ComboBox mapping, and internal<->display names
            void mapItem(string display, ItemRef item)
            {
                toInternalItem[display] = item;
                toDisplayItem[item.RefID] = display;
            }
            void mapLoc(string display, LocationRef loc)
            {
                toInternalLoc[display] = loc;
                toDisplayLoc[loc.RefID] = display;
            }

            // -- Items
            foreach (PresetTagInfo info in presetAnn.Locations)
            {
                foreach (string tag in info.Tags)
                {
                    Util.AddMulti(tagLocations, tag, info);
                }
            }
            // Preprocess config
            HashSet<string> noplaceItems = new();
            // Item groups: allItems, placementItems
            // Process these first to classify items as placement items, and for dropdown order
            foreach ((string name, Text text) in ItemGroupNames)
            {
                string display = messages.Get(text);
                string displayCount = "";
                bool place = !NoPlacementItemGroups.Contains(name);
                if (presetAnn.ItemGroups.TryGetValue(name, out PresetItemGroup? info))
                {
                    displayCount = $" ({info.LocationCount})";
                    if (!place)
                    {
                        foreach ((string type, List<int> ids) in info.Items)
                        {
                            foreach (int id in ids)
                            {
                                noplaceItems.Add($"{type}:{id}");
                            }
                        }
                    }
                }
                ItemRef obj = new ItemRef { Group = name };
                mapItem(display, obj);
                AutocompleteItem auto = new AutocompleteItem(display, display + displayCount, obj);
                allItems.Add(auto);
                if (place)
                {
                    placementItems.Add(auto);
                }
            }
            int groupAllCount = allItems.Count;
            int groupPlaceCount = placementItems.Count;
            // Regular items: allItems, placementItems. Make temporary separate lists so they can be sorted together
            Dictionary<(string, int), ItemRef> itemsById = new();
            foreach ((string type, var typeItems) in presetAnn.ItemNames)
            {
                if (!presetAnn.ItemCounts.TryGetValue(type, out SortedDictionary<int, int>? totals)) totals = new();
                if (!presetAnn.UniqueCounts.TryGetValue(type, out SortedDictionary<int, int>? uniques)) uniques = new();
                if (!ItemNameTypes.TryGetValue(type, out LocationData.ItemType keyType)) keyType = (LocationData.ItemType)(-1);
                foreach ((int id, string name) in typeItems)
                {
                    if (!totals.TryGetValue(id, out int count)) count = 1;
                    if (!uniques.TryGetValue(id, out int uniqueCount)) uniqueCount = count;
                    string itemId = $"{type}:{id}";
                    string displayCount = count > 1 ? $" ({count})" : "";
                    ItemRef obj = new ItemRef { Name = name, ID = itemId };
                    mapItem(name, obj);
                    itemsById[(type, id)] = obj;
                    AutocompleteItem auto = new AutocompleteItem(name, name + displayCount, obj);
                    allItems.Add(auto);
                    if (!noplaceItems.Contains(itemId))
                    {
                        placementItems.Add(auto);
                    }
                    // There are no explicit item groups for this because every item falls into one. Merging support will make this more complicated
                    LocationData.ItemKey gameKey = new LocationData.ItemKey(keyType, id);
                    string contentGroup = GameData.CheckEldenDlcItem(gameKey) ? "dlc" : "nodlc";
                    Util.AddMulti(tagItems, contentGroup, obj);
                }
            }
            // Why is comparer null?
            allItems.Sort(0, groupAllCount, null);
            allItems.Sort(groupAllCount, allItems.Count - groupAllCount, null);
            placementItems.Sort(0, groupPlaceCount, null);
            placementItems.Sort(groupPlaceCount, placementItems.Count - groupPlaceCount, null);
            // Finally, another pass of item groups for filtering/display purposes, now that item refs exist
            foreach (string name in ItemGroupNames.Keys.Concat(ItemFilterGroupNames.Keys).Concat(OtherConfigGroupNames))
            {
                if (!presetAnn.ItemGroups.TryGetValue(name, out PresetItemGroup? info))
                {
                    continue;
                }
                HashSet<ItemRef> items = tagItems[name] = new();
                foreach ((string type, List<int> ids) in info.Items)
                {
                    foreach (int id in ids)
                    {
                        if (itemsById.TryGetValue((type, id), out ItemRef? item))
                        {
                            items.Add(item);
                        }
                    }
                }
            }
            // Shard items come from item group
            if (tagItems.TryGetValue("multikey", out HashSet<ItemRef>? multiItems))
            {
                multiKeyItems = allItems.Where(i => multiItems.Contains(i.Obj)).ToList();
            }

            // -- Locations
            foreach ((string name, Text text) in LocationGroupNames)
            {
                string display = messages.Get(text);
                LocationRef obj = new LocationRef { Tag = name };
                mapLoc(display, obj);
                string displayCount = "";
                if (tagLocations.TryGetValue(name, out List<PresetTagInfo>? locs))
                {
                    // Ignore RealCount for now, as this is calculated based on union/difference per-category
                    int count = locs.Select(l => l.Count).Sum();
                    displayCount = $" ({count})";
                }
                AutocompleteItem auto = new AutocompleteItem(display, display + displayCount, obj);
                if (DlcTags.Contains(name))
                {
                    allLocations.Add(auto);
                    continue;
                }
                if (name == "enemyinf")
                {
                    norandomLocations.Add(auto);
                    continue;
                }
                allLocations.Add(auto);
                nonRegionLocations.Add(auto);
                norandomLocations.Add(auto);
                if (RequirableTags.Contains(name))
                {
                    requirableLocations.Add(auto);
                }
            }
            requirableLocations.Sort();
            allLocations.Sort();
            nonRegionLocations.Sort();
            norandomLocations.Sort();
        }

        public string GetItemGroupDesc(ItemRef obj)
        {
            if (obj.Group == null || !tagItems.TryGetValue(obj.Group, out HashSet<ItemRef>? items))
            {
                return ToDisplayItem(obj);
            }
            HashSet<ItemRef>? rarityFilter = null;
            HashSet<ItemRef>? contentFilter = null;
            if (obj.Rarity == "rare" || obj.Rarity == "legendary")
            {
                tagItems.TryGetValue(obj.Rarity, out rarityFilter);
            }
            if (obj.Content == "nodlc" || obj.Content == "dlc")
            {
                tagItems.TryGetValue(obj.Content, out contentFilter);
            }
            SortedSet<string> names = new();
            foreach (ItemRef item in items)
            {
                if (rarityFilter != null && !rarityFilter.Contains(item)) continue;
                if (contentFilter != null && !contentFilter.Contains(item)) continue;
                names.Add(ToDisplayItem(item));
            }
            return MultiLineList(names);
        }

        public List<string> GetItemGroupFilters(ItemRef obj)
        {
            // Don't calculate all intersections of variants, just do it incrementally from given obj
            if (obj.Rarity != null && obj.Content != null)
            {
                return [];
            }
            if (obj.Group == null || !tagItems.TryGetValue(obj.Group, out HashSet<ItemRef>? items))
            {
                return [];
            }
            HashSet<string>? usedContent = null;
            if (obj.Content == null && tagItems.ContainsKey("dlc"))
            {
                usedContent = new();
            }
            HashSet<string>? usedRarity = null;
            if (obj.Rarity == null && tagItems.ContainsKey("rare") && tagItems.ContainsKey("legendary"))
            {
                usedRarity = new();
            }
            foreach (ItemRef item in items)
            {
                if (usedContent != null)
                {
                    // All items are one or the other
                    usedContent.Add(tagItems["dlc"].Contains(item) ? "dlc" : "nodlc");
                }
                if (usedRarity != null)
                {
                    if (tagItems["rare"].Contains(item))
                    {
                        usedRarity.Add("rare");
                    }
                    else if (tagItems["legendary"].Contains(item))
                    {
                        usedRarity.Add("legendary");
                    }
                    else
                    {
                        usedRarity.Add("none");
                    }
                }
            }
            // This could return ItemRef, but strings are more useful for UI layer
            List<string> ret = [];
            if (usedContent != null && usedContent.Count > 1)
            {
                ret.AddRange(["nodlc", "dlc"]);
            }
            if (usedRarity != null && usedRarity.Count > 1)
            {
                if (usedRarity.Contains("rare"))
                {
                    ret.Add("rare");
                }
                if (usedRarity.Contains("legendary"))
                {
                    ret.Add("legendary");
                }
            }
            return ret;
        }

        public static ItemRef AddItemGroupFilter(ItemRef item, string filter)
        {
            if (filter == "nodlc" || filter == "dlc")
            {
                return item.WithContent(filter);
            }
            if (filter == "rare" || filter == "legendary")
            {
                return item.WithRarity(filter);
            }
            return item;
        }

        public enum ItemListType
        {
            Unspecified,
            // Items with placement restrictions
            PlacementItems,
            // Other item configuration like global norandom
            AllItems,
            // Selecting item shards only
            MultiKeyItems,
            // Groups where items must appear
            RequirableLocations,
            // General purpose location groups, where the first includes dlc/nodlc tags, which are processed
            // specially during placement. The last contains items with special norandom handling.
            AllLocations,
            NonRegionLocations,
            NorandomLocations,
        }

        public IReadOnlyList<AutocompleteItem> GetAutocompleteItems(ItemListType type)
        {
            switch (type)
            {
                case ItemListType.PlacementItems:
                    return placementItems;
                case ItemListType.AllItems:
                    return allItems;
                case ItemListType.MultiKeyItems:
                    return multiKeyItems;
                case ItemListType.RequirableLocations:
                    return requirableLocations;
                case ItemListType.AllLocations:
                    return allLocations;
                case ItemListType.NonRegionLocations:
                    return nonRegionLocations;
                case ItemListType.NorandomLocations:
                    return norandomLocations;
                default:
                    throw new Exception($"Unsupported {type}");
            }
        }

        // Temporary, pending AutoCompleteBox integration with enemy presets
        public record AutocompleteItem(string Name, string DropdownName, SwitchRef Obj) : IComparable<AutocompleteItem>
        {
            public int CompareTo(AutocompleteItem? other) => Name.CompareTo(other?.Name);
        }

        public ItemPreset DefaultPreset => ann.DefaultPreset;

        public string ToDisplay(SwitchRef re)
        {
            if (re is ItemRef itemRef)
            {
                return ToDisplayItem(itemRef);
            }
            else if (re is LocationRef locRef)
            {
                return ToDisplayLoc(locRef);
            }
            return re.RefID;
        }
        public string ToDisplayItem(ItemRef re)
        {
            // RefID isn't a directly usable key here as it includes a filter
            if (re.ID != null && toDisplayItem.TryGetValue(re.ID, out string? name))
            {
                return name;
            }
            return re.Group == null ? re.RefID : GetItemGroupName(messages, re);
        }
        public string ToDisplayLoc(LocationRef re) => re.Tag != null && toDisplayLoc.TryGetValue(re.Tag, out string? res) ? res : re.RefID;

        // Not used because of string typing
        private string? ToDisplayItem(string? val) => val != null && toDisplayItem.TryGetValue(val, out string res) ? res : val;
        private string? ToDisplayLoc(string? val) => val != null && toDisplayLoc.TryGetValue(val, out string res) ? res : val;
        // Not used since Ref is retained
        private string? ToInternalItem(string? val) => val != null && toInternalItem.TryGetValue(val, out ItemRef res) ? res.RefID : val;
        private string? ToInternalLoc(string? val) => val != null && toInternalLoc.TryGetValue(val, out LocationRef res) ? res.RefID : val;

        // Location reporting
        public record LocationList(int Count, List<PresetTagInfo> Locations);

        public LocationList? CalculateLocations(IEnumerable<LocationRef> requires, IEnumerable<LocationRef> excludes)
        {
            HashSet<string> requireTags = new(requires.Select(i => i.RefID));
            HashSet<string> excludeTags = new(excludes.Select(i => i.RefID));
            if (requireTags.Count == 0)
            {
                return null;
            }
            List<PresetTagInfo> locations = new();
            int count = 0;
            foreach (PresetTagInfo info in presetAnn.Locations)
            {
                if (info.Tags.Intersect(requireTags).Any() && !info.Tags.Intersect(excludeTags).Any())
                {
                    locations.Add(info);
                    count += info.Count;
                }
            }
            return new LocationList(count, locations);
        }

        public string ListLocations(LocationRef loc)
        {
            SortedSet<(int, string)> entries = new();
            if (tagLocations.TryGetValue(loc.Tag, out List<PresetTagInfo>? infos))
            {
                foreach (PresetTagInfo info in infos)
                {
                    if (info.Locations == null) continue;
                    entries.UnionWith(info.Locations.Select(e => (e.Value, e.Key)));
                }
            }
            return MultiLineList(entries.Select(e => e.Item2));
        }

        public string ListLocations(LocationList list)
        {
            SortedSet<(int, string)> entries = new();
            foreach (PresetTagInfo info in list.Locations)
            {
                if (info.Locations == null) continue;
                entries.UnionWith(info.Locations.Select(e => (e.Value, e.Key)));
            }
            return MultiLineList(entries.Select(e => e.Item2));
        }

        private string MultiLineList(IEnumerable<string> entries)
        {
            // Not the most performant but it's fine, this is done in user interactions
            return string.Join("\n", entries.Select(e => $"- {e}"));
        }
    }
}
