using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.LocationKey;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class SekiroLocationDataScraper
    {
        private static SortedDictionary<int, string> shopSplits = new SortedDictionary<int, string>
        {
            { 1, null },
            { 1000, "Purchase Spirit Emblems" },
            { 1000000, "Pot Noble Harunaga" },
            { 1100000, "Crow's Bed Memorial Mob" },
            { 1100100, "Battlefield Memorial Mob" },
            { 1100200, "Anayama the Peddler" },
            { 1100300, "Anayama the Peddler (dying)" },
            { 1100400, "Fujioka the Info Broker" },
            { 1100500, "Offering Box" },
            { 1101000, "Fit New Prosthetic Tool" },
            { 1110000, "Dungeon Memorial Mob" },
            { 1111400, "Blackhat Badger" },
            { 1500000, "Exiled Memorial Mob" },
            { 1700000, "Toxic Memorial Mob" },
            { 2000000, "Shugendo Memorial Mob" },
            { 2500000, "Pot Noble Koremori" },
            { 9000000, null }
        };
        private static readonly Dictionary<int, int> shopEsds = new Dictionary<int, int> {
            { 1000, 1 },
            { 1000000, 100360 },
            { 1100000, 110410 },
            { 1100100, 110420 },
            { 1100200, 110171 },
            { 1100300, 110171 },
            { 1100400, 110470 },
            { 1100500, 110010 },
            { 1101000, 110100 },
            { 1110000, 111430 },
            { 1111400, 111140 },
            { 1500000, 150460 },
            { 1700000, 170440 },
            { 2000000, 200450 },
            { 2500000, 250360 },
        };

        private bool logUnused;
        private bool addUnused;

        public SekiroLocationDataScraper(bool logUnused = false)
        {
            this.logUnused = logUnused;
            this.addUnused = false;
        }

        private static int GetShopType(int shopID)
        {
            int shopType = 1;
            foreach (KeyValuePair<int, string> entry in shopSplits)
            {
                if (entry.Key > shopID)
                {
                    break;
                }
                shopType = entry.Key;
            }
            return shopType;
        }

        public LocationData FindItems(GameData game)
        {
            ItemLocs allLocs = FindItemLocs(game);
            SortedDictionary<int, List<EntityId>> usedItemLots = allLocs.usedItemLots;
            SortedDictionary<int, List<EntityId>> usedBaseShops = allLocs.usedBaseShops;

            PARAM shops = game.Param("ShopLineupParam");
            PARAM itemLots = game.Param("ItemLotParam");
            PARAM materials = game.Param("EquipMtrlSetParam");
            PARAM npcs = game.Param("NpcParam");

            // First we may have to create lots - easier to do this at the start than keep data in side channels all the way through
            int baseEvent = 52500960;
            foreach (KeyValuePair<int, int> toCreate in allLocs.baseLotsToCreate)
            {
                PARAM.Row baseRow = itemLots[toCreate.Value];
                int newLot = toCreate.Key;
                PARAM.Row row = itemLots[newLot];
                if (row == null)
                {
                    row = game.AddRow("ItemLotParam", newLot);
                    foreach (PARAM.Cell newCell in row.Cells)
                    {
                        newCell.Value = baseRow[newCell.Def.InternalName].Value;
                    }
                }
                // TODO: Re-enable this with flag id validation
                row["getItemFlagId"].Value = baseEvent;
                baseEvent++;
            }

            LocationData data = new LocationData();
            data.NewEntityLots = allLocs.newEntityLots;

            LocationKey prevLocation = null;
            foreach (KeyValuePair<int, List<EntityId>> entry in usedItemLots)
            {
                int itemLot = entry.Key;
                if (prevLocation != null)
                {
                    // TODO: If event flag is tracked in script, allow 1 maxslot
                    prevLocation.MaxSlots = Math.Max(Math.Min(itemLot - prevLocation.ID - 1, 8), 1);
                    if (prevLocation.MaxSlots < 1)
                    {
                        Console.WriteLine($"XX Overlapping slots at {itemLot}");
                    }
                    prevLocation = null;
                }
                List<EntityId> entities = entry.Value;
                string locs = string.Join(", ", entities.Select(e => game.EntityName(e, true) + (e.MapName == "" ? "" : " " + e.MapName)));
                if (itemLots[itemLot] == null)
                {
                    string text = game.LotName(itemLot);
                    // These are fine - no-ops to game
                    if (logUnused) Console.WriteLine($"MISSING connected item lot!! {itemLot}: {text} @ {locs}");
                    continue;
                }
                LocationKey baseLocation = null;
                while (itemLots[itemLot] != null)
                {
                    bool isBase = itemLot == entry.Key;
                    string text = game.LotName(itemLot);

                    PARAM.Row row = itemLots[itemLot];
                    int clearCount = (sbyte)row["ClearCount"].Value;
                    int eventFlag = (int)row["getItemFlagId"].Value;
                    int totalPoints = 0;
                    for (int i = 1; i <= 8; i++)
                    {
                        totalPoints += (short)row[$"LotItemBasePoint0{i}"].Value;
                    }
                    List<string> itemLotOutput = new List<string>();
                    for (int i = 1; i <= 8; i++)
                    {
                        int id = (int)row[$"ItemLotId{i}"].Value;
                        if (id != 0)
                        {
                            uint type = (uint)row[$"LotItemCategory0{i}"].Value;
                            int points = (short)row[$"LotItemBasePoint0{i}"].Value;
                            int quantity = (ushort)row[$"NewLotItemNum{i}"].Value;
                            if (type == 0xFFFFFFFF) continue;
                            ItemKey item = new ItemKey(LocationData.LotTypes[type], id);
                            string itemText = $"{itemLot}[{locs}]";
                            List<string> lotInfo = new List<string>();
                            if (quantity > 1)
                            {
                                lotInfo.Add($"{quantity}x");
                            }
                            if (points != totalPoints)
                            {
                                lotInfo.Add($"{100.0 * points / totalPoints:0.##}%");
                            }
                            PARAM.Row itemRow = game.Item(item);
                            if (itemRow != null && item.Type == ItemType.GOOD && (byte)itemRow["goodsType"].Value == 7)
                            {
                                // Items which are not shown in inventory menu for various reasons, but having them still does something.
                                lotInfo.Add($"(hidden)");
                            }
                            itemLotOutput.Add($"{game.Name(item)} " + string.Join(" ", lotInfo));
                            if (lotInfo.Count() > 0)
                            {
                                itemText += $" {string.Join(", ", lotInfo)}";
                            }
                            if (quantity <= 0)
                            {
                                Console.WriteLine($"XX There is 0! of {itemText}");
                            }
                            ItemScope scope;
                            if (eventFlag != -1)
                            {
                                if (equivalentEvents.ContainsKey(eventFlag))
                                {
                                    eventFlag = equivalentEvents[eventFlag];
                                }
                                scope = new ItemScope(ScopeType.EVENT, eventFlag);
                                // Note this doesn't necessarily have to be slot 1. But it should be only one slot...
                                if (points != totalPoints)
                                {
                                    Console.WriteLine($"Has event flag? But random? {itemText}");
                                }
                            }
                            else
                            {
                                // One time drops that directly award, that aren't covered by event flags. Mostly crystal lizards.
                                if (entities.Count() == 1 && entityItemLots.ContainsKey(entities[0].EventEntityID) && entityItemLots[entities[0].EventEntityID] == entry.Key)
                                {
                                    scope = new ItemScope(ScopeType.ENTITY, entities[0].EventEntityID);
                                }
                                // Non-respawning enemies with drops which can be missed. These are reused between different entities, so can drop multiple times.
                                else if (entities.All(e => nonRespawningEntities.Contains(e.EventEntityID)))
                                {
                                    scope = new ItemScope(ScopeType.ENTITY, entities.Select(e => e.EventEntityID).Min());
                                }
                                else
                                {
                                    int model = entities.Select(e => e.GetModelID()).Min();
                                    // Infinite guaranteed or scripted drops are not randomized unless specifically added to entityItemLots
                                    if (model == -1 || points == totalPoints)
                                    {
                                        if (logUnused)
                                        {
                                            Console.WriteLine($"XX Item {game.Name(item)} {itemLot} has no associated event, but is guaranteed or global: {itemText}");
                                        }
                                        continue;
                                    }
                                    scope = new ItemScope(ScopeType.MODEL, model);
                                }
                            }
                            LocationKey location = new LocationKey(LocationType.LOT, itemLot, itemText, entities, quantity, baseLocation);
                            data.AddLocation(item, scope, location);
                            if (baseLocation == null)
                            {
                                baseLocation = location;
                            }
                        }
                    }
                    // Write out the info. Some deduplication of sources to make prettier output.
                    string lotOutput = string.Join(", ", itemLotOutput);
                    bool simple = false;
                    string text2;
                    if (simple)
                    {

                        SortedSet<string> locations = new SortedSet<string>(entities.Select(e => game.LocationNames[e.MapName]));
                        SortedSet<string> models = new SortedSet<string>(entities.Select(e => e.EntityName.StartsWith("o") ? "Treasure" : game.EntityName(e)));
                        text2 = $"{string.Join(", ", models)}: {string.Join(", ", locations)}";
                        if (models.All(x => x == "unknown")) { text2 = "Unused/Unknown"; }
                        if (models.All(x => x == "Unused NPC")) { text2 = "Unused NPC"; }
                        else if (models.Any(x => x == "Unused NPC")) { models.Remove("Unused NPC"); if (locations.Count > 1) locations.Remove("Global"); text2 = $"{string.Join(", ", models)}: {string.Join(", ", locations)}"; }
                    }
                    else
                    {
                        // e.NPCParamID > -1 ? $" #{e.NPCParamID}" : ""
                        // SortedSet<string> models = new SortedSet<string>(entities.Select(e => e.EntityName.StartsWith("o") ? $"Treasure in {e.MapName}" : $"{game.ModelName(e, true)} in {e.MapName}"));
                        SortedSet<string> models = new SortedSet<string>(entities.Select(e => game.EntityName(e, true) + (e.MapName == "" ? "" : $" in {e.MapName}")));
                        text2 = $"{string.Join(", ", models)}";
                    }
                    // Console.WriteLine($"{itemLot} [{text2}] {lotOutput}");

                    if (itemLot == 2014) break;  // Unused, and item lot immediately after it is used. Won't be an issue once. ... ??

                    // Try to navigate resource drops (affected by Bell Demon).
                    if ((byte)row["LotItemNum1"].Value == 1)
                    {
                        int curOffset = itemLot % 100;
                        int curBase = itemLot / 100 * 100;
                        int offset;
                        for (offset = curOffset + 10; offset <= 50; offset += 10)
                        {
                            PARAM.Row offRow = itemLots[curBase + offset];
                            if (offRow != null && (byte)offRow["LotItemNum1"].Value == 1)
                            {
                                break;
                            }
                        }
                        if (offset <= 50)
                        {
                            itemLot = curBase + offset;
                        }
                        else
                        {
                            itemLot++;
                        }
                    }
                    else
                    {
                        itemLot++;
                    }
                }
                prevLocation = baseLocation;
            }
            if (prevLocation != null)
            {
                prevLocation.MaxSlots = 5;
            }
            SortedDictionary<int, List<string>> qwcs = new SortedDictionary<int, List<string>>();
            Dictionary<int, LocationKey> baseShops = new Dictionary<int, LocationKey>();
            foreach (PARAM.Row row in shops.Rows)
            {
                int shopID = (int)row.ID;
                int baseShop = GetShopType(shopID);
                string shopName = shopSplits[baseShop];
                if (shopName == null)
                {
                    if (!addUnused) continue;
                    shopName = "Unknown shop";
                }
                if (shopID >= 9000000) continue;
                int qwc = (int)row["qwcID"].Value;

                int type = (byte)row["equipType"].Value;
                int id = (int)row["EquipId"].Value;
                int quantity = (short)row["sellQuantity"].Value;
                int eventFlag = (int)row["EventFlag"].Value;
                int material = (int)row["mtrlId"].Value;
                int value = (int)row["value"].Value;
                float priceRate = (float)row["PriceRate"].Value;
                string quantityText = quantity == -1 ? "" : $" ({quantity})"; // (unlimited)
                string qwcText = qwc == -1 ? "" : $" {game.QwcName(qwc)}";
                string costText = "";
                ItemKey item = new ItemKey((ItemType)type, id);
                if (material != -1)
                {
                    PARAM.Row matRow = materials[material];
                    int materialQuant = (sbyte)matRow["ItemNum01"].Value;
                    int materialItem = (int)matRow["MaterialId01"].Value;
                    costText = $" for {materialQuant} {game.Name(new ItemKey(ItemType.GOOD, materialItem))}";
                }
                if (value != 0 || costText == "")
                {
                    int actualCost = value;
                    if (actualCost == -1)
                    {
                        actualCost = (int)game.Item(item)["shopId"].Value;
                    }
                    if (priceRate != 0) actualCost = (int)(actualCost * priceRate);
                    costText = costText == "" ? $" for {actualCost} Sen" : $"{costText} and {actualCost} Sen";
                }
                string shopText = $"{shopName}{qwcText}{quantityText}{costText} - event {eventFlag}";
                string text = $"{shopID}[{shopText}]";
                // Console.WriteLine($"{shopID} [{shopName}{qwcText}] {game.Name(item)}{quantityText}{costText}");
                LocationKey location = new LocationKey(
                    LocationType.SHOP, shopID, text,
                    usedBaseShops.ContainsKey(baseShop) ? usedBaseShops[baseShop] : new List<EntityId>(),
                    quantity,
                    null); // try not to use base shops - baseShops.ContainsKey(baseShop) ? baseShops[baseShop] : null);
                if (shopID == baseShop)
                {
                    baseShops[baseShop] = location;
                }
                ItemScope scope;
                AddMulti(qwcs, qwc, $"{game.Name(item)}: {text}");
                if (eventFlag != -1)
                {
                    if (equivalentEvents.ContainsKey(eventFlag))
                    {
                        eventFlag = equivalentEvents[eventFlag];
                    }
                    if (quantity <= 0)
                    {
                        Console.WriteLine("XX No quantity for event flag shop entry {text}");
                    }
                    ScopeType scopeType = ScopeType.EVENT;
                    if (restrictiveQwcs.Contains(qwc))
                    {
                        // If item becomes unavailable at some point, it returns in infinite form
                        scopeType = ScopeType.SHOP_INFINITE_EVENT;
                    }
                    scope = new ItemScope(scopeType, eventFlag);
                }
                else if (material != -1)
                {
                    int materialItem = (int)materials[material]["MaterialId01"].Value;
                    scope = new ItemScope(ScopeType.MATERIAL, materialItem);
                }
                else
                {
                    scope = new ItemScope(ScopeType.SHOP_INFINITE, -1);
                }
                data.AddLocation(item, scope, location);
            }
            // Merge infinite and finite shops. Mostly done via heuristic (when event and infinite both exist), with exception of one event
            ItemScope infiniteKey = new ItemScope(ScopeType.SHOP_INFINITE, -1);
            foreach (ItemLocations locations in data.Data.Values)
            {
                foreach (ItemLocation restrict in locations.Locations.Values.Where(loc => loc.Scope.Type == ScopeType.SHOP_INFINITE_EVENT).ToList())
                {
                    if (locations.Locations.ContainsKey(infiniteKey))
                    {
                        // Combine infinite shops into event
                        ItemLocation infinite = locations.Locations[infiniteKey];
                        restrict.Keys.AddRange(infinite.Keys);
                        locations.Locations.Remove(infiniteKey);
                    }
                    else
                    {
                        Console.WriteLine($"XX: No 1:1 match between event shops and infinite shops for {restrict}");
                        // No infinite shops, turn this into a regular event shop. (Doesn't happen in base DS3)
                        ItemLocation eventLoc = new ItemLocation(new ItemScope(ScopeType.EVENT, restrict.Scope.ID));
                        eventLoc.Keys.AddRange(restrict.Keys);
                        locations.Locations[eventLoc.Scope] = eventLoc;
                        locations.Locations.Remove(restrict.Scope);
                    }
                }
            }
            // Now can find all location scopes
            List<ScopeType> uniqueTypes = new List<ScopeType> { ScopeType.EVENT, ScopeType.ENTITY, ScopeType.MATERIAL };
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
            {
                int unique = 0;
                foreach (KeyValuePair<ItemScope, ItemLocation> entry2 in entry.Value.Locations)
                {
                    ItemScope scope = entry2.Key;
                    ItemLocation loc = entry2.Value;
                    int id = uniqueTypes.Contains(scope.Type) ? scope.ID : -1;
                    unique = unique == -1 ? -1 : (id == -1 ? -1 : unique + 1);
                    SortedSet<int> shopIds = new SortedSet<int>(loc.Keys.Where(k => k.Type == LocationType.SHOP).Select(k => GetShopType(k.ID)));
                    SortedSet<int> shopQwcs = new SortedSet<int>(loc.Keys.Where(k => k.Type == LocationType.SHOP).Select(k => (int)shops[k.ID]["qwcID"].Value)
                        .Select(qwc => equivalentEvents.ContainsKey(qwc) ? equivalentEvents[qwc] : qwc)
                        .Where(qwc => qwc != -1 && !restrictiveQwcs.Contains(qwc)));
                    SortedSet<int> allShop = new SortedSet<int>(shopIds.Union(shopQwcs));
                    if (shopIds.Count() + shopQwcs.Count() != allShop.Count())
                    {
                        Console.WriteLine($"XX Overlapping qwc/shop ids for location {loc}");
                    }
                    SortedSet<int> modelBase = scope.Type == ScopeType.MODEL ? new SortedSet<int>(loc.Keys.Select(k => k.BaseID)) : new SortedSet<int>();
                    bool onlyShops = loc.Keys.All(k => k.Type == LocationType.SHOP) && allShop.Count() > 0;
                    LocationScope locationScope = new LocationScope(scope.Type, id, allShop, modelBase, onlyShops);
                    data.AddLocationScope(entry.Key, scope, locationScope);
                    loc.LocScope = locationScope;
                }
                entry.Value.Unique = unique > 0;
            }

            if (logUnused)
            {
                Console.WriteLine("---------------------------------------------------------------------------");
                foreach (KeyValuePair<ItemKey, string> entry in game.Names())
                {
                    ItemKey item = entry.Key;
                    if (item.Type == 0)
                    {
                        item = new ItemKey(item.Type, item.ID - (item.ID % 10000));
                    }
                    if (!data.Data.ContainsKey(item))
                    {
                        // Mostly pulls up old DS1 items, crow items, and gestures.
                        Console.WriteLine($"Unused item {item.Type}-{entry.Key.ID}: {entry.Value}");
                    }
                }
            }

            return data;
        }

        private class ItemLocs
        {
            public SortedDictionary<int, List<EntityId>> usedItemLots { get; set; }
            public SortedDictionary<int, List<EntityId>> usedBaseShops { get; set; }
            public Dictionary<int, int> baseLotsToCreate { get; set; }
            public Dictionary<int, int> newEntityLots { get; set; }
        }
        private ItemLocs FindItemLocs(GameData game)
        {
            PARAM itemLots = game.Param("ItemLotParam");
            PARAM npcs = game.Param("NpcParam");

            ItemLocs ret = new ItemLocs
            {
                usedItemLots = new SortedDictionary<int, List<EntityId>>(),
                usedBaseShops = new SortedDictionary<int, List<EntityId>>(),
                baseLotsToCreate = new Dictionary<int, int>(),
                // TODO: See if we can have a reasaonable processing step
                newEntityLots = new Dictionary<int, int>(),
            };

            Dictionary<EntityId, EntityId> objects = new Dictionary<EntityId, EntityId>();
            Dictionary<int, List<EntityId>> usedNpcs = new Dictionary<int, List<EntityId>>();
            Dictionary<int, List<EntityId>> usedEntities = new Dictionary<int, List<EntityId>>();
            Dictionary<int, List<EntityId>> usedEsds = new Dictionary<int, List<EntityId>>();

            // Map from item lot to ESD id, hackily extracted from ESD.
            Dictionary<int, HashSet<int>> talkItemLocations = new Dictionary<int, HashSet<int>>();
            // This is a bit intensive. Ideally just dump this somewhere else
            HashSet<int> debugEsd = new HashSet<int>();
            IEnumerable<ESD.Condition> GetCommands(List<ESD.Condition> condList) => Enumerable.Concat(condList, condList.SelectMany(cond => GetCommands(cond.Subconditions)));
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                string location = game.Locations.ContainsKey(entry.Key) ? game.Locations[entry.Key] : "";
                // 62210
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    int esdId = int.Parse(esdEntry.Key.Substring(1));
                    foreach ((int, int, ESD.State) stateDesc in esd.StateGroups.SelectMany(stateGroup => stateGroup.Value.Select(state => (stateGroup.Key, state.Key, state.Value))))
                    {
                        (int groupId, int id, ESD.State state) = stateDesc;
                        foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, GetCommands(state.Conditions).SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                        {
                            foreach (byte[] arg in cmd.Arguments)
                            {
                                if (arg.Length == 6 && arg[0] == 0x82 && arg[5] == 0xA1)
                                {
                                    int opt = BitConverter.ToInt32(arg, 1);
                                    if (opt >= 60000 && opt <= 69900 && itemLots[opt] != null)
                                    {
                                        AddMulti(talkItemLocations, opt, esdId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            bool logEntities = false;
            foreach (KeyValuePair<string, MSBS> entry in game.Smaps)
            {
                string location = game.Locations[entry.Key];
                MSBS msb = entry.Value;
                Dictionary<string, int> partEsds = new Dictionary<string, int>();
                List<MSBS.Part.Enemy> enemies = msb.Parts.Enemies;
                // TODO: Update SoulsFormat and migrate to new names
                foreach (MSBS.Event.Talk ev in entry.Value.Events.Talks)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        string part = ev.EnemyNames[i];
                        int esdId = ev.TalkIDs[i];
                        if (esdId < 0 || part == null)
                        {
                            continue;
                        }
                        partEsds[part] = esdId;
                    }
                }
                foreach (MSBS.Entry obj in entry.Value.Parts.GetEntries())
                {
                    MSBS.Part part = obj as MSBS.Part;
                    if (part == null) continue;
                    EntityId id;
                    int esdId = 0;
                    List<int> groupIDs = part.EntityGroupIDs.Where(groupID => groupID > 0).ToList();
                    if (part is MSBS.Part.Enemy enemy)
                    {
                        esdId = partEsds.ContainsKey(enemy.Name) ? partEsds[enemy.Name] : -1;
                        id = new EntityId(location, enemy.Name, enemy.EntityID, enemy.NPCParamID, enemy.CharaInitID, groupIDs);
                    }
                    else if (part is MSBS.Part.Object || logEntities)
                    {
                        id = new EntityId(location, part.Name, part.EntityID, GroupIds: groupIDs);
                    }
                    else
                    {
                        continue;
                    }
                    objects[id] = id;
                    if (id.EventEntityID > 0)
                    {
                        AddMulti(usedEntities, id.EventEntityID, id);
                    }
                    foreach (int groupID in groupIDs)
                    {
                        AddMulti(usedEntities, groupID, id);
                    }
                    if (id.NPCParamID > 0)
                    {
                        AddMulti(usedNpcs, id.NPCParamID, id);
                    }
                    if (esdId > 0)
                    {
                        if (debugEsd.Contains(esdId)) Console.WriteLine($"ESD {esdId} belongs to {game.EntityName(id, true)} in {location}, entity id {id.EventEntityID}");
                        AddMulti(usedEsds, esdId, id);
                    }
                }
            }
            if (logEntities)
            {
                foreach (KeyValuePair<int, List<EntityId>> entry in usedEntities.OrderBy(e => e.Key))
                {
                    Console.WriteLine($"{entry.Key}: {string.Join(", ", entry.Value.Select(e => $"{game.EntityName(e, true)} in {e.MapName}"))}");
                }
            }
            List<EntityId> unusedEsd = new List<EntityId> { new EntityId("", "Unknown Dialogue") };
            foreach (KeyValuePair<int, HashSet<int>> entry in talkItemLocations)
            {
                List<EntityId> talkIds;
                if (addUnused)
                {
                    talkIds = entry.Value.SelectMany(esd => usedEsds.ContainsKey(esd) ? usedEsds[esd] : unusedEsd).ToList();
                }
                else
                {
                    talkIds = entry.Value.Where(esd => usedEsds.ContainsKey(esd)).SelectMany(esd => usedEsds[esd]).ToList();
                    if (talkIds.Count == 0) continue;
                }
                AddMulti(ret.usedItemLots, entry.Key, talkIds);
            }
            foreach (KeyValuePair<int, int> entry in shopEsds)
            {
                if (!usedEsds.ContainsKey(entry.Value) && !addUnused)
                {
                    continue;
                }
                AddMulti(ret.usedBaseShops, entry.Key, usedEsds.ContainsKey(entry.Value) ? usedEsds[entry.Value] : unusedEsd);
            }
            foreach (KeyValuePair<string, MSBS> entry in game.Smaps)
            {
                string location = game.Locations[entry.Key];
                foreach (MSBS.Event.Treasure treasure in entry.Value.Events.Treasures)
                {
                    if (treasure.TreasurePartName != null)
                    {
                        EntityId id = new EntityId(location, treasure.TreasurePartName);
                        if (!objects.ContainsKey(id))
                        {
                            if (logUnused) Console.WriteLine($"Missing entity for treasure {treasure.Name} with entity {treasure.TreasurePartName} and lot {treasure.ItemLotID}");
                            continue;
                        }
                        AddMulti(ret.usedItemLots, treasure.ItemLotID, objects[id]);
                    }
                }
            }
            foreach (PARAM.Row row in npcs.Rows)
            {
                int npcID = (int)row.ID;
                PARAM.Cell cell = row["ItemLotId1"];
                if (cell == null || (int)cell.Value == -1)
                {
                    continue;
                }
                int itemLot = (int)cell.Value;
                if (itemLots[itemLot] == null)
                {
                    if (logUnused) Console.WriteLine($"Invalid NPC lot item for {npcID} with lot {itemLot}");
                    continue;
                }
                if (!usedNpcs.ContainsKey(npcID))
                {
                    if (logUnused) Console.WriteLine($"Unused NPC: {npcID}");
                    if (addUnused)
                    {
                        AddMulti(ret.usedItemLots, itemLot, new EntityId("", "Unused NPC"));
                    }
                    continue;
                }
                AddMulti(ret.usedItemLots, itemLot, usedNpcs[npcID]);
            }
            foreach (KeyValuePair<int, int> entry in entityItemLots)
            {
                int entityId = entry.Key;
                List<int> itemLot = new List<int> { entry.Value };
                if (additionalEntityItemLots.ContainsKey(entityId))
                {
                    itemLot.AddRange(additionalEntityItemLots[entityId]);
                }
                List<EntityId> id;
                if (usedEntities.ContainsKey(entityId))
                {
                    id = usedEntities[entityId];
                }
                else
                {
                    Console.WriteLine($"XX Missing entity {entityId} with item lot {String.Join(", ", itemLot)}");
                    id = new List<EntityId> { new EntityId("", "from entity") };
                }

                foreach (int lot in itemLot)
                {
                    if (logUnused && (int)itemLots[lot]["getItemFlagId"].Value == -1 && id[0].GetModelID() != 2150)
                    {
                        Console.WriteLine($"Eventless entity drop, not crystal lizard, for {String.Join(", ", id)} item lot {lot}");
                    }
                    AddMulti(ret.usedItemLots, lot, id);
                }
            }
            Dictionary<int, int> syntheticLotBase = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in treasureCarpDrops)
            {
                int entityId = entry.Key;
                int baseLot = entry.Value;
                List<EntityId> id;
                if (usedEntities.ContainsKey(entityId))
                {
                    id = usedEntities[entityId];
                }
                else
                {
                    Console.WriteLine($"XX Missing entity {entityId} with item lot {baseLot}");
                    id = new List<EntityId> { new EntityId("", "from entity") };
                }
                if (syntheticLotBase.ContainsKey(baseLot))
                {
                    syntheticLotBase[baseLot] += 5;
                }
                else
                {
                    syntheticLotBase[baseLot] = baseLot;
                }
                int itemLot = syntheticLotBase[baseLot];
                ret.baseLotsToCreate[itemLot] = baseLot;
                // TODO: See if this info can be extracted anywhere else
                ret.newEntityLots[entityId] = itemLot;
                AddMulti(ret.usedItemLots, itemLot, id);
            }
            foreach (KeyValuePair<int, string> entry in scriptLots)
            {
                int itemLot = entry.Key;
                if ((int)itemLots[itemLot]["getItemFlagId"].Value == -1)
                {
                    // TODO: Make sure there are all classified...
                    // Console.WriteLine($"XX No event id attached to script event {itemLot}");
                }
                AddMulti(ret.usedItemLots, itemLot, new EntityId(entry.Value, "Scripted"));
            }
            foreach (int itemLot in talkLots)
            {
                if ((int)itemLots[itemLot]["getItemFlagId"].Value == -1)
                {
                    Console.WriteLine($"XX No event id attached to talk event {itemLot}");
                }
                AddMulti(ret.usedItemLots, itemLot, new EntityId("", "from talk"));
            }
            int lastLot = 0;
            bool lastConnected = false;
            foreach (PARAM.Row lot in itemLots.Rows)
            {
                int itemLot = (int)lot.ID;
                bool connected = (byte)lot["LotItemNum1"].Value == 1;
                if (itemLot == lastLot + 1)
                {
                    // Don't require groups of item lots to be connected, only the base lot
                }
                else if (connected && lastConnected && (itemLot - lastLot) % 10 == 0 && (itemLot - lastLot) <= 40)
                {
                    // This is also fine.... aaaa. Bell Demon drops.
                }
                else
                {
                    if (!ret.usedItemLots.ContainsKey(itemLot))
                    {
                        if (logUnused) Console.WriteLine($"Unconnected item lot {itemLot}: {game.LotName(itemLot)}");
                        if (addUnused)
                        {
                            AddMulti(ret.usedItemLots, itemLot, new EntityId("", "unknown"));
                        }
                    }
                }
                lastLot = itemLot;
                lastConnected = connected;
            }
            return ret;
        }

        // Non-respawning entities. Their drops are somewhat unique (and missable) if they don't have event ids.
        private static readonly HashSet<int> nonRespawningEntities = new HashSet<int>
        {
            // Treasure carp
            1000450,
            1000451,
            1000452,
            1000453,
            1000459,
            1000460,
            1110650,
            1500562,
            1700450,
            1700451,
            2000690,
            2000691,
            2500600,
            2500601,
            2500602,
            2500611,
            2500612,
            2500613,
            2500617,
            // Special treasure carp
            1500460,
        };
        // Item event flags with equivalent items, of which it is only possible to get one
        public static readonly Dictionary<int, int> equivalentEvents = new Dictionary<int, int>()
        {
            // Droplet shop items
            { 9095, 9090 },
            { 9096, 9091 },
            { 9097, 9092 },
            { 9098, 9093 },
            { 9099, 9094 },
            // Shop items with corresponding permanent flags. These are used for NG+ support, not put in the shop params.
            { 71001010, 6754 },
            { 71001020, 6757 },
            { 71101000, 6501 },
            { 71101210, 6721 },
            { 71101400, 6741 },
            { 71101800, 6752 },
            { 71102000, 6722 },
            { 71111000, 6768 },
            { 71111010, 6759 },
            { 71111500, 6506 },
            { 71111510, 6719 },
            { 71501000, 6755 },
            { 71701000, 6753 },
            { 72001000, 6756 },
            { 72501000, 6758 },
            { 71102200, 6719 },
            { 71102210, 6766 },
            { 71102220, 6767 },
            { 71102230, 6500 },
            { 71102240, 6508 },
            { 71102250, 6723 },
            { 71102260, 6741 },
            { 71102270, 6760 },
            { 71102280, 6762 },
            { 71102290, 6769 },
            // Unused slots
            // { 71102300, 6501 },
            // { 71102310, 6721 },
            // { 71102320, 6768 },
            // { 71102330, 6759 },
            { 71102340, 6506 },
            { 71102350, 6780 },
            // Harunaga/Koremori transformation
            // { 50006366, 50006361 },
            // Unrefined Sake from Glutton in NG and NG+. (note: sake not given by Juzou in NG+)
            { 51500936, 51500931 }
        };
        // QWC ids which remove items from Handmaid's shop, to later add infinite ones (either cheaper or replacing finite)
        private static readonly HashSet<int> restrictiveQwcs = new HashSet<int>()
        {
        };
        // Entities which have one-time item drops through scripting, event entity ID to item lot ID.
        // Multiple entities can map to the same lot if any of them would grant the item.
        // If a lot requires a set of entities in some area to be killed, only one is listed here.
        private static readonly Dictionary<int, int> entityItemLots = new Dictionary<int, int>()
        {
            // Bosses
            { 1100800, 2000 },
            { 1000800, 2030 },
            { 1110801, 2080 },
            { 1700800, 2010 },
            { 2000800, 2020 }, // Bit weird - all monkeys
            { 1500800, 2050 },
            { 1700850, 2015 },
            { 1110850, 2090 },
            { 2500850, 2060 },
            { 2500800, 2070 },
            { 1120860, 2130 },
            { 1100900, 2040 },
            { 1110920, 2120 },
            { 1000900, 2100 },
            // One-time drops
            {1000300,10700000},
            {1000301,10700100},
            {1000330,10500000},
            {1005331,10500100},
            {1000350,14703000},
            {1000353,14703100},
            {1100300,10200000},
            {1100301,10200100},
            {1100310,50201000},
            {1100450,13604000},
            {1100370,11902000},
            {1100330,13501000},
            {1100480,10701000},
            {1110440,13700000},
            {1110504,14000000},
            {1110410,10202000},
            {1110610,50200000},
            {1110316,14701000},
            {1110305,14700000},
            {1110660,13400000},
            {1110511,14001000},
            {1110432,11801000},
            {1110433,11801000},
            {1110225,10101400},
            {1110460,10501100},
            {1110461,10501100},
            {1110462,10501100},
            {1110640,13605100},
            {1115250,12504000},
            {1120250,10203000},
            {1120530,10212000},
            {1120521,10212100},
            {1120300,14702000},
            {1300200,10800000},
            {1500495,14704000},
            {1500200,13001000},
            {1500680,13500000},
            {1500490,10702000},
            {1500460,13200100},
            {1700200,11900000},
            {1700201,11901100},
            {1700500,13502000},
            {1700425,10401100},
            {1700350,12500000},
            {1700351,12500005},
            {1705299,1700670},
            {1700520,10802000},
            {2000650,10600000},
            {2000400,11300000},
            {2000670,11802000},
            {2000279,10400000},
            {2500401,13100100},
            {2500406,13100000},
            {2500580,10801000},
            {2500550,13401000},
            {2500570,13800000},
            // Per area
            {1110641, 13605200},
            // {1500200, 13001000}, dupe
            {1501220, 1500050},
            {1500690, 70000000},
            {1701200, 1700000},
            {1701210, 1700810},
            {1700202, 11900210}, // trio of them
            {1700600, 50100000},
            {2001250, 2000010},
            {2001260, 2000170},
            {2001261, 2000030},
            {2000466, 10600100}, // Also 2000467
        };
        // These have special handling, because we want to turn them all into unique drops
        private static readonly Dictionary<int, int> treasureCarpDrops = new Dictionary<int, int>
        {
            {1000450,13200000},
            {1000451,13200000},
            {1000452,13200000},
            {1000453,13200000},
            {1000459,13200000},
            {1000460,13200000},
            {1110650,13200000},
            {1500562,13200000},
            {1700450,13200000},
            {1700451,13200000},
            {2000690,13200000},
            {2000691,13200000},
            {2500600,13200000},
            {2500601,13200000},
            {2500602,13200000},
            {2500611,13200000},
            {2500612,13200000},
            {2500613,13200000},
            {2500617,13200000},
        };
        // Magic: grep '2000534[01]' * | awk '{ print $5 $8; }' | grep -v '0,$' | sed -e 's/^/{/' -e 's/,$/},/g' | less
        // These are NG+ replacement for upgrade items. But they are completely removed for now.
        private static readonly Dictionary<int, List<int>> extraEntityScriptDrops = new Dictionary<int, int>
        {
            // O'Rin gets her own script
            {1500690,70000005},
            // From script
            {1000300, 10700005},
            {1000301, 10700105},
            {1000330, 10500005},
            {1000353, 14703105},
            {1100300, 10200005},
            {1100301, 10200105},
            {1100310, 50201005},
            {1100330, 13501005},
            {1100480, 10701005},
            {1110440, 13700005},
            {1110504, 14000005},
            {1110410, 10202005},
            {1110610, 50200005},
            {1110316, 14701005},
            {1110305, 14700005},
            {1110660, 13400005},
            {1110511, 14001005},
            {1120530, 10212005},
            {1120521, 10212105},
            {1120300, 14702005},
            {1300200, 10800005},
            {1500680, 13500005},
            {1500490, 10702005},
            {1700200, 11900005},
            {1700201, 11901105},
            {1700500, 13502005},
            {1700425, 10401105},
            {1700520, 10802005},
            {2000400, 11300005},
            {2000279, 10400005},
            {2500406, 13100005},
            {2500550, 13401005},
            {2500570, 13800005},
        }.ToDictionary(e => e.Key, e => new List<int> { e.Value });

        // In truth, the entity id/item lot mapping is not 1:1, so this hack is needed.
        private static readonly Dictionary<int, List<int>> additionalEntityItemLots =
            extraEntityScriptDrops
                .Union(new Dictionary<int, List<int>>
                {
                    // Additional lot from ape: bestowal ninjutsu
                    { 1700800, new List<int> { 2200 } },
                })
                .ToDictionary(e => e.Key, e => e.Value);
        private static readonly Dictionary<int, string> scriptLots = new Dictionary<int, string>()
        {
            {3010, ""},
            {3200, ""},
            {3210, ""},
            {3220, ""},
            {3800, ""},
            {3500, ""},
            {3510, ""},
            {3520, ""},
            {3530, ""},
            {3540, ""},
            {3550, ""},
            {3600, ""},
            {3610, ""},
            {3650, ""},
            {3660, ""},
            {3700, ""},
            {3710, ""},
            {3720, ""},
            {3730, ""},
            {3740, ""},
            {3750, ""},
            {3760, ""},
            { 3070, "ashinaoutskirts" },
            { 3060, "ashinaoutskirts" },
            { 3300, "ashinareservoir" },
            { 3080, "ashinareservoir" },
            { 3001, "ashinareservoir" },
            { 50500000, "sunkenvalley" },
            { 3400, "senpou"},
            { 2000060, "senpou" },
            { 3090, "senpou" },
            { 62200, "senpou" },
            { 62202, "senpou" },
            { 62204, "senpou" },
        };
        // Items given in the talk system. These can have all sorts of conditions attached.
        // A few of these can also drop on the NPC's death, usually with the same event flag.
        // TODO: Can be replaced with ESD parsing?
        private static readonly List<int> talkLots = new List<int>()
        {
        };
    }
}
