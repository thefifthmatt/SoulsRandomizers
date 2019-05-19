using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using static DS3StaticItemRandomizer.LocationData;
using static DS3StaticItemRandomizer.LocationData.LocationKey;
using static DS3StaticItemRandomizer.LocationData.ItemScope;
using static DS3StaticItemRandomizer.Util;

namespace DS3StaticItemRandomizer
{
    public class SekiroLocationDataScraper
    {
        private static Dictionary<string, string> locations = new Dictionary<string, string>
        {
            { "m10_00_00_00", "hirata" },
            { "m11_00_00_00", "ashinaoutskirts" },
            { "m11_01_00_00", "ashinacastle" },
            { "m11_02_00_00", "ashinareservoir" },
            { "m13_00_00_00", "dungeon" },
            { "m15_00_00_00", "mibuvillage" },
            { "m17_00_00_00", "sunkenvalley" },
            { "m20_00_00_00", "senpou" },
            { "m25_00_00_00", "fountainhead" },
        };
        private static Dictionary<string, string> fullMapName = new Dictionary<string, string>
        {
            { "", "Global" },
            { "hirata", "Hirata Estate" },
            { "ashinaoutskirts", "Ashina Outskirts" },
            { "ashinacastle", "Ashina Castle" },
            { "ashinareservoir", "Ashina Reservoir" },
            { "dungeon", "Abandoned Dungeon" },
            { "mibuvillage", "Mibu Village" },
            { "sunkenvalley", "Sunken Valley" },
            { "senpou", "Senpou Temple" },
            { "fountainhead", "Fountainhead Palace" },
        };
        private static SortedDictionary<int, string> shopSplits = new SortedDictionary<int, string>
        {
            { 1, null },
            { 1000000, null }
        };

        private bool logUnused;

        public SekiroLocationDataScraper(bool logUnused = false)
        {
            this.logUnused = logUnused;
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
            SortedDictionary<int, List<EntityId>> usedItemLots = FindItemLots(game);

            PARAM64 shops = game.Param("ShopLineupParam");
            PARAM64 itemLots = game.Param("ItemLotParam");
            PARAM64 materials = game.Param("EquipMtrlSetParam");
            PARAM64 npcs = game.Param("NpcParam");

            LocationData data = new LocationData();

            LocationKey prevLocation = null;
            foreach (KeyValuePair<int, List<EntityId>> entry in usedItemLots)
            {
                int itemLot = entry.Key;
                if (prevLocation != null)
                {
                    prevLocation.MaxSlots = Math.Max(itemLot - prevLocation.ID - 1, 1);
                    if (prevLocation.MaxSlots < 1)
                    {
                        Console.WriteLine($"XX Overlapping slots at {itemLot}");
                    }
                    prevLocation = null;
                }
                List<EntityId> entities = entry.Value;
                string locs = String.Join(", ", entities.Select(e => game.ModelName(e) + $" {e}"));
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

                    PARAM64.Row row = itemLots[itemLot];
                    int clearCount = (sbyte)row["ClearCount"].Value;
                    int eventFlag = (int)row["getItemFlagId"].Value;
                    // Console.WriteLine($"--------------- lot {itemLot}. flag {eventFlag}");
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
                            // Console.WriteLine($"{itemLot}: {type:X}");
                            ItemKey item = new ItemKey(LocationData.LotTypes[type], id);
                            string itemText = $"{itemLot}[{locs}]";
                            // Check out script about CC, btw
                            List<string> info = new List<string>();
                            List<string> lotInfo = new List<string>();
                            lotInfo.Add(game.Name(item));
                            if (clearCount != -1)
                            {
                                // info.Add($"CC{clearCount}");
                            }
                            if (quantity > 1)
                            {
                                info.Add($"{quantity}");
                                lotInfo.Add($"{quantity}x");
                            }
                            if (points != totalPoints)
                            {
                                info.Add($"{100.0 * points / totalPoints}%");
                                lotInfo.Add($"{100.0 * points / totalPoints:0.##}%");
                            }
                            PARAM64.Row itemRow = game.Item(item);
                            if (itemRow != null && itemRow["goodsType"] != null && (byte)itemRow["goodsType"].Value == 7 && game.Name(item) != "Memory")
                            {
                                // Items which are not shown in inventory menu for various reasons, but having them still does something.
                                // lotInfo.Add($"(proxy)");
                            }
                            itemLotOutput.Add(string.Join(" ", lotInfo));
                            if (info.Count() > 0)
                            {
                                itemText += $" ({string.Join(", ", info)})";
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
                                    if (model == -1)
                                    {
                                        // This is ok in Sekiro? Or just for generating item lot names?
                                        Console.WriteLine($"XX Infinite item {itemLot} with no event flags, entity flags, or models: {itemText}");
                                        // continue;
                                    }
                                    scope = new ItemScope(ScopeType.MODEL, model);
                                }
                                // Console.WriteLine($"Infinite {itemText}: [{String.Join(", ", entry.Value.Select(e => e.EntityName))}], [{String.Join(", ", entry.Value.Select(e => e.EventEntityID))}]");
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
                    SortedSet<string> locations = new SortedSet<string>(entities.Select(e => fullMapName[e.MapName]));
                    SortedSet<string> models = new SortedSet<string>(entities.Select(e => e.EntityName.StartsWith("o") ? "Treasure" : game.ModelName(e) + (e.NPCParamID < -10 ? $" #{e.NPCParamID}" : "")));
                    string text2 = $"{string.Join(", ", models)}: {string.Join(", ", locations)}";
                    if (models.All(x => x == "unknown")) { text2 = "Unused/Unknown"; }
                    if (models.All(x => x == "Unused NPC")) { text2 = "Unused NPC"; }
                    else if (models.Any(x => x == "Unused NPC")) { models.Remove("Unused NPC"); if (locations.Count > 1) locations.Remove("Global"); text2 = $"{string.Join(", ", models)}: {string.Join(", ", locations)}"; }
                    Console.WriteLine($"{itemLot} [{text2}] {lotOutput}");
                    if (itemLot == 2014) break;  // Unused, and item lot immediately after it is used. Won't be an issue once.

                    // Try to navigate resource drops (affected by Bell Demon).
                    if ((byte)row["LotItemNum1"].Value == 1)
                    {
                        int curOffset = itemLot % 100;
                        int curBase = itemLot / 100 * 100;
                        int offset;
                        for (offset = curOffset + 10; offset <= 50; offset += 10)
                        {
                            PARAM64.Row offRow = itemLots[curBase + offset];
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
            foreach (PARAM64.Row row in shops.Rows)
            {
                int shopID = (int)row.ID;
                string shopName = shopSplits[GetShopType(shopID)];
                if (shopName == null)
                {
                    shopName = "Unknown shop";
                    //continue;
                }
                if (shopID >= 9000000) continue;
                int qwc = (int)row["qwcID"].Value;
                int type = (byte)row["equipType"].Value;
                int id = (int)row["EquipId"].Value;
                int quantity = (short)row["sellQuantity"].Value;
                int eventFlag = (int)row["EventFlag"].Value;
                int material = (int)row["mtrlId"].Value;
                string quantityText = quantity == -1 ? " (unlimited)" : $" ({quantity})";
                string qwcText = qwc == -1 ? "" : $" {game.QwcName(qwc)}";
                ItemKey item = new ItemKey((ItemType)type, id);
                string text = $"{shopName}{qwcText}{quantityText}";
                text = $"{shopID}[{text}]";
                LocationKey location = new LocationKey(LocationType.SHOP, shopID, text, new List<EntityId>(), quantity, null);
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
            // foreach (KeyValuePair<int, List<string>> entry in qwcs) { Console.WriteLine($"QWC {entry.Key}: {string.Join(" | ", entry.Value)}"); }
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
                foreach (KeyValuePair<ItemScope, ItemLocation> entry2 in entry.Value.Locations)
                {
                    ItemScope scope = entry2.Key;
                    ItemLocation loc = entry2.Value;
                    int id = uniqueTypes.Contains(scope.Type) ? scope.ID : -1;
                    SortedSet<int> shopIds = new SortedSet<int>(loc.Keys.Where(k => k.Type == LocationType.SHOP).Select(k => GetShopType(k.ID)));
                    SortedSet<int> shopQwcs = new SortedSet<int>(loc.Keys.Where(k => k.Type == LocationType.SHOP).Select(k => (int)shops[k.ID]["qwcID"].Value)
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
            }

            Console.WriteLine("---------------------------------------------------------------------------");
            if (logUnused || true)
            {
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

        private SortedDictionary<int, List<EntityId>> FindItemLots(GameData game)
        {
            PARAM64 itemLots = game.Param("ItemLotParam");
            PARAM64 npcs = game.Param("NpcParam");

            SortedDictionary<int, List<EntityId>> usedItemLots = new SortedDictionary<int, List<EntityId>>();

            Dictionary<EntityId, EntityId> objects = new Dictionary<EntityId, EntityId>();
            Dictionary<int, List<EntityId>> usedNpcs = new Dictionary<int, List<EntityId>>();
            Dictionary<int, List<EntityId>> usedEntities = new Dictionary<int, List<EntityId>>();
            Dictionary<int, List<EntityId>> usedEsds = new Dictionary<int, List<EntityId>>();

            Dictionary<int, HashSet<int>> talkItemLocations = new Dictionary<int, HashSet<int>>();
            // This is a bit intensive. Ideally save this somewhere
            Func<List<ESD.Condition>, IEnumerable<ESD.Condition>> GetCommands = null;
            GetCommands = condList => Enumerable.Concat(condList, condList.SelectMany(cond => GetCommands(cond.Subconditions)));
            foreach (KeyValuePair<string, List<ESD>> entry in game.Talk())
            {
                string location = locations.ContainsKey(entry.Key) ? locations[entry.Key] : "";
                // 62210
                foreach (ESD esd in entry.Value)
                {
                    foreach (ESD.State state in esd.StateGroups.Values.SelectMany(group => group.Values))
                    {
                        // foreach (KeyValuePair<long, Dictionary<long, ESD.State>> stateGroup in esd.StateGroups) foreach (KeyValuePair<long, ESD.State> stateEntry in stateGroup.Value)
                        // ESD.State state = stateEntry.Value;
                        int esdId = int.Parse(esd.Name.Substring(1));
                        foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, GetCommands(state.Conditions).SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                        {
                            foreach (byte[] arg in cmd.Arguments)
                            {
                                // if (SearchInt(arg, 2701132019)) Console.WriteLine($"Found hex {esd.Name}, arg length {arg.Length}: {string.Join(" ", cmd.Arguments.Select(a => string.Join("", a.Select(x => $"{x:x2}"))))}");
                                if (arg.Length == 6 && arg[0] == 0x82 && arg[5] == 0xA1)
                                {
                                    int opt = BitConverter.ToInt32(arg, 1);
                                    if (opt >= 60000 && opt <= 69900 && itemLots[opt] != null)
                                    {
                                        // if (opt == 62214) Console.WriteLine($"ESD {location} {esd.Name}: {stateGroup.Key:X} {stateEntry.Key}");
                                        AddMulti(talkItemLocations, opt, esdId);
                                        // break;
                                    }
                                }
                            }
                        }
                        foreach (byte[] arg in GetCommands(state.Conditions).Select(c => c.Evaluator))
                        {
                            if (SearchInt(arg, 62214) != -1) Console.WriteLine($"Found {esd.Name}");
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, MSBS> entry in game.Smaps())
            {
                string location = locations[entry.Key];
                MSBS msb = entry.Value;
                Dictionary<int, int> entityEsds = new Dictionary<int, int>();
                List<MSBS.Part.Enemy> enemies = msb.Parts.Enemies;
                // TODO: Update SoulsFormat and migrate to new names
                foreach (MSBS.Event.Event23 ev in entry.Value.Events.Event23s)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        int partIndex = ev.UnkT04[i];
                        int esdId = ev.UnkT24[i];
                        if (partIndex < 0)
                        {
                            continue;
                        }
                        if (partIndex > enemies.Count)
                        {
                            Console.WriteLine("Bad");
                            continue;
                        }
                        int entityId = enemies[partIndex].EntityID;
                        if (entityId <= 0 || entityEsds.ContainsKey(entityId))
                        {
                            Console.WriteLine("Bad/duplicate");
                            continue;
                        }
                        entityEsds[entityId] = esdId;
                    }
                }
                foreach (MSBS.Entry obj in entry.Value.Parts.GetEntries())
                {
                    MSBS.Part part = obj as MSBS.Part;
                    if (part == null) continue;
                    EntityId id;
                    int esdId = 0;
                    if (part is MSBS.Part.Enemy enemy)
                    {
                        esdId = entityEsds.ContainsKey(enemy.EntityID) ? entityEsds[enemy.EntityID] : -1;
                        id = new EntityId(location, enemy.Name, enemy.EntityID, enemy.NPCParamID, enemy.CharaInitID);
                    }
                    else
                    {
                        id = new EntityId(location, part.Name, part.EntityID);
                    }
                    objects[id] = id;
                    if (id.EventEntityID > 0)
                    {
                        AddMulti(usedEntities, id.EventEntityID, id);
                    }
                    foreach (int groupID in part.EntityGroupIDs)
                    {
                        if (groupID > 0)
                        {
                            AddMulti(usedEntities, groupID, id);
                        }
                    }
                    if (id.NPCParamID > 0)
                    {
                        AddMulti(usedNpcs, id.NPCParamID, id);
                    }
                    if (esdId > 0)
                    {
                        AddMulti(usedEsds, esdId, id);
                    }
                    // Console.WriteLine($"Model for {id.EventEntityID} in {location}: {game.ModelName(id)}, npc {id.NPCParamID}. My ESD is {esdId}");
                }
            }
            foreach (KeyValuePair<int, HashSet<int>> entry in talkItemLocations)
            {
                if (entry.Key == 62302)
                {
                    Console.WriteLine($"For emma: {string.Join(" ", entry.Value)}");
                }
                AddMulti(usedItemLots, entry.Key, entry.Value.SelectMany(esd => usedEsds.ContainsKey(esd) ? usedEsds[esd] : new List<EntityId> { new EntityId("", $"Unknown Dialogue") })); // For debugging: {esd}
            }
            foreach (KeyValuePair<string, MSBS> entry in game.Smaps())
            {
                string location = locations[entry.Key];
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
                        AddMulti(usedItemLots, treasure.ItemLotID, objects[id]);
                    }
                }
            }
            foreach (PARAM64.Row row in npcs.Rows)
            {
                int npcID = (int)row.ID;
                PARAM64.Cell cell = row["ItemLotId1"];
                if (cell == null || (int)cell.Value == -1)
                {
                    continue;
                }
                int itemLot = (int)cell.Value;
                if (itemLot == 14502300) { Console.WriteLine("blah " + row.ID); }
                if (itemLots[itemLot] == null)
                {
                    if (logUnused) Console.WriteLine($"Invalid NPC lot item for {npcID} with lot {itemLot}");
                    continue;
                }
                if (!usedNpcs.ContainsKey(npcID))
                {
                    if (logUnused) Console.WriteLine($"Unused NPC: {npcID}");
                    AddMulti(usedItemLots, itemLot, new EntityId("", "Unused NPC"));
                    continue;
                }
                AddMulti(usedItemLots, itemLot, usedNpcs[npcID]);
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
                    AddMulti(usedItemLots, lot, id);
                }
                // Console.WriteLine($"{game.LotName(itemLot)} from {entityId}: {itemLots[itemLot]["getItemFlagId"].Value}");
            }
            foreach (KeyValuePair<int, string> entry in scriptLots)
            {
                int itemLot = entry.Key;
                if ((int)itemLots[itemLot]["getItemFlagId"].Value == -1)
                {
                    Console.WriteLine($"XX No event id attached to talk event {itemLot}");
                }
                AddMulti(usedItemLots, itemLot, new EntityId(entry.Value, "Scripted"));
            }
            foreach (int itemLot in talkLots)
            {
                if ((int)itemLots[itemLot]["getItemFlagId"].Value == -1)
                {
                    Console.WriteLine($"XX No event id attached to talk event {itemLot}");
                }
                AddMulti(usedItemLots, itemLot, new EntityId("", "from talk"));
            }
            int lastLot = 0;
            bool lastConnected = false;
            foreach (PARAM64.Row lot in itemLots.Rows)
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
                    if (!usedItemLots.ContainsKey(itemLot))
                    {
                        if (logUnused) Console.WriteLine($"Unconnected item lot {itemLot}: {game.LotName(itemLot)}");
                        // But still add
                        AddMulti(usedItemLots, itemLot, new EntityId("", "unknown"));
                    }
                }
                lastLot = itemLot;
                lastConnected = connected;
            }
            return usedItemLots;
        }

        // Non-respawning entities. Their drops are somewhat unique (and missable) if they don't have event ids.
        private static readonly HashSet<int> nonRespawningEntities = new HashSet<int>
        {
        };
        // Item event flags with equivalent items, of which it is only possible to get one
        private static readonly Dictionary<int, int> equivalentEvents = new Dictionary<int, int>()
        {
        };
        // QWC ids which remove items from Handmaid's shop, rather than adding ones
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
            { 1700801, 2200 }, // Also 1700801
            { 1110920, 2120 },
            { 1000900, 2100 },
            // One-time drops
            {1000450,13200000},
            {1000451,13200000},
            {1000452,13200000},
            {1000453,13200000},
            {1000459,13200000},
            {1000460,13200000},
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
            {1110650,13200000},
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
            {1500562,13200000},
            {1700200,11900000},
            {1700201,11901100},
            {1700500,13502000},
            {1700425,10401100},
            {1700350,12500000},
            {1700351,12500005},
            {1705299,1700670},
            {1700450,13200000},
            {1700451,13200000},
            {1700520,10802000},
            {2000650,10600000},
            {2000400,11300000},
            {2000670,11802000},
            {2000279,10400000},
            {2000690,13200000},
            {2000691,13200000},
            {2500401,13100100},
            {2500406,13100000},
            {2500580,10801000},
            {2500550,13401000},
            {2500570,13800000},
            {2500600,13200000},
            {2500601,13200000},
            {2500602,13200000},
            {2500611,13200000},
            {2500612,13200000},
            {2500613,13200000},
            {2500617,13200000},
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
        // Magic: grep '2000534[01]' * | awk '{ print $5 $8; }' | grep -v '0,$' | sed -e 's/^/{/' -e 's/,$/},/g' | less
        // These are NG+ replacement for upgrade items
        private static readonly Dictionary<int, int> coinDrops = new Dictionary<int, int>
        {
            // O'Rin gets her own script
            {1500690,70000005},
            // From script
            {1000300,10700005},
            {1000301,10700105},
            {1000330,10500005},
            {1000353,14703105},
            {1100300,10200005},
            {1100301,10200105},
            {1100310,50201005},
            {1100330,13501005},
            {1100480,10701005},
            {1110440,13700005},
            {1110504,14000005},
            {1110410,10202005},
            {1110610,50200005},
            {1110316,14701005},
            {1110305,14700005},
            {1110660,13400005},
            {1110511,14001005},
            {1120530,10212005},
            {1120521,10212105},
            {1120300,14702005},
            {1300200,10800005},
            {1500680,13500005},
            {1500490,10702005},
            {1700200,11900005},
            {1700201,11901105},
            {1700500,13502005},
            {1700425,10401105},
            {1700520,10802005},
            {2000400,11300005},
            {2000279,10400005},
            {2500406,13100005},
            {2500550,13401005},
            {2500570,13800005},
        };
        // In truth, the entity id/item lot mapping is not 1:1, so this hack is needed.
        private static readonly Dictionary<int, List<int>> additionalEntityItemLots = coinDrops.ToDictionary(e => e.Key, e => new List<int> { e.Value });
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
