using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static RandomizerCommon.LocationData;
#if ARCHIPELAGO
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
#endif

namespace RandomizerCommon
{
    public class ExternalItemPreset
    {
        // -- Required info
        public string LogInfo { get; private set; }
        // From external item id
        public Dictionary<long, LocalItem> LocalItems { get; private set; }
        // From external location id (item id may be shared between games + also local game)
        public Dictionary<long, ExternalItem> ExternalItems { get; private set; }
        // From external location id
        public Dictionary<long, LocalLocation> LocalLocations { get; private set; }
        // From external location id to external item id. Any finite locations not covered are not randomized.
        public Dictionary<long, long> FiniteItemMapping { get; private set; }

        // -- Other info
        // From source boss ItemName to target boss defeat flag (TODO: entity id might be preferable for direct config usage).
        // The source boss must appear exactly once at the given location (this could alternatively be a set of locations to allow for placement heuristics)
        public Dictionary<string, uint> ForceBossTargets { get; private set; }
        // From Item Name Shard to required shard count. Any item split into shards must have an entry here or errors may result from multiple placement.
        public Dictionary<string, int> RequiredShardCounts { get; private set; }

        public class LocalItem
        {
            // Combined mask + id format
            public uint ID { get; set; }
            // Quantity to give all at once
            public int Quantity { get; set; }
            // For debugging missing items (and also titles?)
            public string Name { get; set; }

            // Added after loading locations
            public ItemKey Key { get; set; }
        }

        public class ExternalItem
        {
            // Item name to be shown in all languages
            public string Name { get; set; }
            // Short item info/caption
            public string Desc { get; set; }
            // Sort key below 1 million
            public int SortOffset { get; set; }
            // Some measure of rarity based on known information
            public ExternalRarity Rarity { get; set; }

            // Added after loading locations
            public ItemKey Key { get; set; }
        }

        public enum ExternalRarity
        {
            Unspecified,
            Common,
            Rare,
            Legendary,
            // Use random rarity
            Trap,
        }

        public class LocalLocation
        {
            // Several external locations can map to the same slot, but each must map to a unique ItemLocKey currently.
            // TODO warn about locations which are not covered
            public string LocationScopeKey { get; set; }
            // Param row ids for resolving the item in the case of ambiguities within a SlotKey.
            // Currently this is formatted like lot:<id> and slot:<id> with no subtype support at present.
            public List<string> LocationIDs { get; set; }
            // External description of this location, mainly for error reporting.
            // Avoid using it for resolving ambiguities or anything semantic.
            public string Desc { get; set; }
            // Some item ID could be useful here (stable id might would be preferable to name because of ambiguities)
        }

#if ARCHIPELAGO
        // At the moment, these are given as individual slots. TODO look into taking it as a dictionary, especially if it gets to >2 for a game
        private static readonly List<string> BossTargetSlots = new() { "rykard", "serpent" };

        public static ExternalItemPreset FromArchipelago(
            string logInfo,
            int mySlot,
            Dictionary<string, object> slotData,
            Dictionary<long, ScoutedItemInfo> locations)
        {
            // Loading this data should work across multiple games with game-specific stuff later
            ExternalItemPreset ret = new()
            {
                LogInfo = logInfo,
                LocalItems = new(),
                ExternalItems = new(),
                LocalLocations = new(),
                FiniteItemMapping = new(),
            };
            Dictionary<long, T> getApIdMapping<T>(string key, bool required = false)
            {
                if (!slotData.TryGetValue(key, out object data))
                {
                    if (required)
                    {
                        throw new Exception($"Archipelago slot data missing required {key}");
                    }
                    return null;
                }
                return ((JObject)data)
                    .Cast<KeyValuePair<string, JToken>>()
                    .ToDictionary(e => long.Parse(e.Key), e => e.Value.ToObject<T>());
            }

            // ap item id (long) to uint local item id
            Dictionary<long, uint> itemIds = getApIdMapping<uint>("apIdsToItemIds", true);
            // ap item id to int
            Dictionary<long, int> itemCounts = getApIdMapping<int>("itemCounts", true);
            foreach ((long extId, uint localId) in itemIds)
            {
                LocalItem local = new()
                {
                    ID = localId,
                    Quantity = itemCounts.GetValueOrDefault(extId, 1)
                };
                ret.LocalItems[extId] = local;
            }

            // ap location id to slot key
            Dictionary<long, string> locationSlots = getApIdMapping<string>("locationIdsToKeys", true);
            // ap location id to list of lot:xx or shop:yy
            Dictionary<long, List<string>> locationLocs = getApIdMapping<List<string>>("locationIdsToTargets");
            // locationIdsToName is also supported in ER but try to rely on ScoutedItemInfo for now

            foreach ((long extId, string locKey) in locationSlots)
            {
                LocalLocation local = new()
                {
                    LocationScopeKey = locKey,
                    LocationIDs = locationLocs.GetValueOrDefault(extId),
                    Desc = locations.GetValueOrDefault(extId)?.LocationName,
                };
                ret.LocalLocations[extId] = local;
            }

            // Look at all locations meant for this slot to populate local mapping and necessary external items
            List<string> errors = new();
            foreach (ScoutedItemInfo info in locations.Values)
            {
                if (!ret.LocalLocations.ContainsKey(info.LocationId))
                {
                    errors.Add($"No location in locationIdsToKeys for assigned location [{info.LocationName}] in [{info.LocationGame}]");
                    continue;
                }
                if (info.Player.Slot == mySlot)
                {
                    if (ret.LocalItems.TryGetValue(info.ItemId, out LocalItem localItem))
                    {
                        localItem.Name ??= info.ItemName;
                    }
                    else
                    {
                        errors.Add($"No item in apIdsToItemIds for [{info.ItemName}] in [{info.ItemGame}] and slot {mySlot} [{info.Player}]");
                        continue;
                    }
                }
                else
                {
                    ExternalRarity rarity = ExternalRarity.Common;
                    if (info.Flags.HasFlag(ItemFlags.Advancement)) rarity = ExternalRarity.Legendary;
                    else if (info.Flags.HasFlag(ItemFlags.NeverExclude)) rarity = ExternalRarity.Rare;
                    else if (info.Flags.HasFlag(ItemFlags.Trap)) rarity = ExternalRarity.Trap;
                    ExternalItem ext = new()
                    {
                        // Use same values as fswap archi. TODO shortening routine, or keep separate if game-specific
                        Name = $"{info.Player}'s {info.ItemName}",
                        Desc = $"An object from a mysterious world known only as \"{info.Player.Game}\".",
                        // Alternatively, sort items after collecting them all and use result index? This avoids spacing issues and allows e.g. handling rarity
                        SortOffset = info.Player.Slot * 10000 + (int)(info.ItemId % 10000),
                        Rarity = rarity,
                    };
                    ret.ExternalItems[info.LocationId] = ext;
                }
                ret.FiniteItemMapping[info.LocationId] = info.ItemId;
            }
            if (errors.Count > 0)
            {
                throw new Exception($"Incomplete slot data received from Archipelago server. Cannot proceed with randomization. Issues:\n{string.Join("\n", errors)}");
            }

            foreach (string targetSlot in BossTargetSlots)
            {
                if (slotData.TryGetValue(targetSlot + "_flag", out object target) && target is uint)
                {
                    ret.ForceBossTargets ??= new();
                    ret.ForceBossTargets[targetSlot] = (uint)target;
                }
            }

            if (slotData.TryGetValue("requiredShards", out object shards))
            {
                ret.RequiredShardCounts = ((JObject)shards).ToObject<Dictionary<string, int>>();
            }

            return ret;
        }
#endif
    }
}
