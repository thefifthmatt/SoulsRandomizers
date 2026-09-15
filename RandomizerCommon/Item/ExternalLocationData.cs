using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.ExternalItemPreset;

namespace RandomizerCommon
{
    public class ExternalLocationData
    {
        private readonly GameData game;
        private readonly LocationData data;
        private readonly AnnotationData ann;
        private readonly ExternalItemPreset externalPreset;

        // Comprehensive override for finite silo. This could be extended to other silos if needed.
        // All targets must be explicitly given sources, and everything not covered here is not randomized.
        // This could be usable for non-external item plando, but it might be more convenient for most targets to also be sources there.
        // Mapping from target to source items
        public Dictionary<ItemLocKey, List<ItemLocKey>> FiniteItemMapping { get; private set; }
        // Mapping from location id to either real local item or new external item proxy (limit 100k)
        // Local item proxies are created in PermutationWriter if the source is an external location
        public Dictionary<long, ItemKey> ExternalItemKeys { get; private set; }

        public bool IsActive => FiniteItemMapping != null;

        public ExternalLocationData(GameData game, LocationData data, AnnotationData ann, ExternalItemPreset externalPreset)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.externalPreset = externalPreset;
        }

        public void LoadLocations()
        {
            // Instead of always running, this could be null if ExternalItemPreset is null, but that's more annoying for DI.
            if (externalPreset == null)
            {
                return;
            }

            // All external target locations must map to an existing slot + item scraped from game data
            Dictionary<long, ItemLocKey> mappedLocIds = new();
            Dictionary<ItemLocKey, string> mappedKeys = new();
            Dictionary<string, LocationScope> keyLocScopes = ann.Slots.ToDictionary(e => e.Value.Key, e => e.Key);
            // Name, quantity, disambiguation (the extra part does not match items except Winged Misbegotten Ashes + 1)
            List<string> errors = new();
            foreach ((long id, LocalLocation loc) in externalPreset.LocalLocations)
            {
                string locName = loc.Desc;
                string key = loc.LocationScopeKey;
                if (!keyLocScopes.TryGetValue(key, out LocationScope locScope))
                {
                    errors.Add($"Unrecognized location key from external data: {key} for {locName}");
                    continue;
                }
                List<ItemLocKey> candidates = data.Locations[locScope];
                ItemLocKey itemLocKey;
                if (candidates.Count == 1)
                {
                    itemLocKey = candidates[0];
                }
                else if (loc.LocationIDs != null)
                {
                    // Use lot/shop data from vanilla game to match
                    List<ItemLocKey> match = candidates.Where(cand =>
                    {
                        ItemLocation itemLoc = data.GetItemLoc(cand);
                        List<string> dataLocIds = itemLoc.Locs.Select(loc => (loc.Type == Location.LocationType.Lot ? "lot" : "shop") + ":" + loc.ID).ToList();
                        return loc.LocationIDs.Intersect(dataLocIds).Any();
                    }).ToList();
                    if (match.Count == 1)
                    {
                        itemLocKey = match[0];
                    }
                    else
                    {
                        errors.Add($"Could not use lot/shop data to resolve location key from external data: {key} with candidates [{string.Join(", ", match)}]\n-> {locName}");
                        continue;
                    }
                }
                else
                {
                    errors.Add($"No lot/shop data for ambiguous location key from external data: {key} with candidates [{string.Join(", ", candidates)}]\n-> {locName}");
                    continue;
                }
                // This is not strictly an issue e.g. if a shop or drop has multiple instances of an item, but handle that only if needed
                if (mappedKeys.TryGetValue(itemLocKey, out string existDesc))
                {
                    errors.Add($"Multiple external locations mapped to same key {itemLocKey}:\n-> {existDesc}\n-> {locName}");
                    continue;
                }
                mappedKeys[itemLocKey] = locName;
                mappedLocIds[id] = itemLocKey;
            }
            bool validate = true;
            if (errors.Count > 0 && validate)
            {
                throw new Exception($"{errors.Count} Archipelago locations could not be mapped to static randomizer locations. See spoiler logs for details.\n\n" + string.Join("\n\n", errors));
            }

            FiniteItemMapping = new();
            ExternalItemKeys = new();

            // 268_435_455 - max item id (? actually seems lower)
            //  68_690_000 - highest weapon in Elden Ring
            //   8_388_607 - max goods id
            //   5_370_300 - highest protector in Elden Ring
            //   2_220_010 - highest good in Elden Ring
            //
            //   7_830_000 - custom new goods
            //   8_000_000 - external goods (temporary)
            //   8_100_000 - local proxy goods (temporary)
            int externalId = 8_000_000;
            int maxExternalId = 8_100_000;
            errors = new();
            foreach ((long locId, long itemId) in externalPreset.FiniteItemMapping)
            {
                if (!mappedLocIds.TryGetValue(locId, out ItemLocKey targetKey))
                {
                    if (validate)
                    {
                        errors.Add($"Internal error: slot mapping and item mapping inconsistent for location {locId}");
                    }
                    continue;
                }
                int quantity = 1;
                if (!ExternalItemKeys.TryGetValue(locId, out ItemKey itemKey))
                {
                    if (externalPreset.ExternalItems.TryGetValue(locId, out ExternalItem extItem))
                    {
                        if (externalId >= maxExternalId) throw new Exception($"{maxExternalId - externalId} external item limit exceeded");
                        // TODO: Can potentially share key by name if location id doesn't need to be encoded there
                        extItem.Key = itemKey = new ItemKey(ItemType.Goods, externalId++);
                        game.AddItemName(itemKey, extItem.Name);
                    }
                    else if (externalPreset.LocalItems.TryGetValue(itemId, out LocalItem locItem))
                    {
                        itemKey = locItem.Key;
                        if (itemKey == null)
                        {
                            quantity = locItem.Quantity;
                            if (!game.TryUintAsItem(locItem.ID, out itemKey))
                            {
                                errors.Add($"Item id {locItem.ID} ({locItem.ID:X8}) not recognized as valid item key");
                                continue;
                            }
                            // TODO: Support randomizing this
                            if (itemKey.Equals(new ItemKey(ItemType.Goods, 20090)))
                            {
                                continue;
                            }
                            if (!game.ItemNames.ContainsKey(itemKey))
                            {
                                errors.Add($"Unknown local item {itemKey} [{locItem.Name}] given in external item data");
                                continue;
                            }
                            locItem.Key = itemKey;
                        }
                    }
                    // TODO: Better error message
                    else throw new Exception($"Unknown {locId}");
                    ExternalItemKeys[locId] = itemKey;
                }
                // Special scope should be fine here, but can make a new scope type if needed.
                ItemLocation itemLoc = data.AddLocationlessItem(itemKey);
                // Except it's not locationless, this is used in PermutationWriter.
                // ID collisions can happen in theory but it's not put in a set.
                string text = externalPreset.LocalLocations.GetValueOrDefault(locId)?.Desc ?? locId.ToString();
                Location newLoc = new Location(Location.LocationType.External, (int)locId, text, new(), quantity, 1, null, ExternalID: locId);
                itemLoc.Locs.Add(newLoc);
                Util.AddMulti(FiniteItemMapping, targetKey, itemLoc.Key);
            }
            if (errors.Count > 0)
            {
                throw new Exception($"{errors.Count} Archipelago locations could not be mapped to static randomizer locations. See spoiler logs for details.\n\n" + string.Join("\n\n", errors));
            }
        }
    }
}
