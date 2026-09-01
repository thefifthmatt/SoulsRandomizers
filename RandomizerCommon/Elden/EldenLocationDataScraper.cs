using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsFormats.EMEVD.Instruction;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.Location;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.Util;
using System.Text.RegularExpressions;

namespace RandomizerCommon
{
    public class EldenLocationDataScraper
    {
        private bool addUnused = false;
        private bool logUnused = false;

        private readonly GameData game;
        private readonly EldenCoordinator coord;
        private readonly RandomizerOptions opt;

        public EldenLocationDataScraper(GameData game, EldenCoordinator coord, RandomizerOptions opt)
        {
            this.game = game;
            this.coord = coord;
            this.opt = opt;
        }

        public class ItemLocationConfig
        {
            public List<int> GlobalLots { get; set; }
            // ESD id -> start shop range, end shop range
            public Dictionary<int, string> EsdShops { get; set; }
            // event id -> entity id / arg position
            public Dictionary<int, string> EventEntities { get; set; }
            // flag id -> entity id
            public Dictionary<uint, string> FlagEntities { get; set; }
            // actual entity id -> display entity id
            // [2-9][0-9][0-9]\.[0-9][0-9] away
            public Dictionary<uint, uint> DisplayEntities { get; set; }
        }

        public LocationData FindItems()
        {
            if (opt["dumplot"]) addUnused = true;

            ItemLocs allLocs = FindItemLocs();

            SortedDictionary<ItemKey, Dictionary<ItemScope, List<Location>>> locations = new();

            if (opt["html"])
            {
                Console.Out.NewLine = "\n";
                string title = "Elden Ring Item Analysis";
                Console.Write($"<html><head><meta charset=\"UTF-8\"><title>{System.Net.WebUtility.HtmlEncode(title)}</title></head><body style=\"white-space: pre-wrap; font-size: 120%; margin: 1em;\">");
            }
            void writeHtmlSection(string name, string color)
            {
                Console.WriteLine($"<h2>{name}</h2><div style=\"background-color: {color};\">");
            }
            void endHtmlSection()
            {
                Console.WriteLine("</div>");
            }
            void dump(string text)
            {
                if (opt["html"])
                {
                    Console.WriteLine(System.Net.WebUtility.HtmlEncode(text));
                }
                else
                {
                    Console.WriteLine(text);
                }
            }

            foreach ((int flag, ItemKey item) in allLocs.LotsToCreate)
            {
                PARAM.Row row = GameEditor.AddRow(game.Params["ItemLotParam_map"], flag);
                row["lotItemId01"].Value = item.ID;
                row["lotItemCategory01"].Value = (int)game.LotValues[item.Type];
                row["lotItemBasePoint01"].Value = (ushort)1000;
                row["lotItemNum01"].Value = (byte)1;
                row["getItemFlagId"].Value = (uint)flag;
            }

            foreach (string lotType in new List<string> { "map", "enemy" })
            {
                string paramName = $"ItemLotParam_{lotType}";
                string paramPrint = opt["machine"] ? paramName + " " : "";
                PARAM itemLots = game.Params[paramName];
                if (opt["html"]) writeHtmlSection(paramName, lotType == "map" ? "#FFF" : "#F8FFFF");
                Location prevLocation = null;
                foreach (KeyValuePair<int, List<Entity>> entry in allLocs.UsedItemLots[lotType])
                {
                    int itemLot = entry.Key;
                    if (prevLocation != null)
                    {
                        // TODO: If event flag is tracked in script, allow 1 maxslot (?)
                        prevLocation.MaxSlots = Math.Max(Math.Min(itemLot - prevLocation.ID - 1, 6), 1);
                        if (prevLocation.MaxSlots < 1)
                        {
                            Console.WriteLine($"XX Overlapping slots at {itemLot}");
                        }
                        // if (lotType == "map" && itemLot < 40_000) Console.WriteLine($"{itemLot} after {prevLocation.ID}: {prevLocation.MaxSlots}");
                        prevLocation = null;
                    }
                    List<Entity> entities = entry.Value;
                    // string locs = string.Join(", ", entities.Select(e => game.EntityName(e, true) + (e.MapName == "" ? "" : " " + e.MapName)));
                    string locs;
                    // There are just too many of these
                    if (entities.All(e => e.AssetID > 0))
                    {
                        locs = string.Join(", ", entities.Select(e => $"asset {e.AssetID}").Distinct());
                    }
                    else
                    {
                        locs = string.Join(", ", entities.Select(e => game.EntityName(e, true, true)));
                    }
                    Location baseLocation = null;
                    PARAM.Row row = itemLots[itemLot];
                    if (row == null)
                    {
                        string text = game.LotName(itemLot);
                        // These are fine - no-ops to game
                        if (logUnused && opt["reflot"])
                        {
                            Console.WriteLine($"*Item lot referenced but missing: {itemLot} [{locs}]");
                        }
                        continue;
                    }
                    while (row != null)
                    {
                        bool isBase = itemLot == entry.Key;
                        // There are a bunch of weird unused lots adjacent to used ones, so hopefully this heuristic is fine
                        if (!isBase && allLocs.UsedItemLots[lotType].ContainsKey(itemLot)) break;
                        string text = game.LotName(itemLot);

                        // int clearCount = (sbyte)row["ClearCount"].Value;
                        // For now, use old reliable int. We can transition later if needed
                        uint ueventFlag = (uint)row["getItemFlagId"].Value;
                        if (ueventFlag > int.MaxValue) throw new Exception($"Event flag {row.ID} {ueventFlag} too big");
                        int eventFlag = (int)ueventFlag;
                        int totalPoints = 0;
                        for (int i = 1; i <= 8; i++)
                        {
                            totalPoints += (ushort)row[$"lotItemBasePoint0{i}"].Value;
                        }
                        List<string> itemLotOutput = new List<string>();
                        for (int i = 1; i <= 8; i++)
                        {
                            int id = (int)row[$"lotItemId0{i}"].Value;
                            if (id != 0)
                            {
                                int type = (int)row[$"lotItemCategory0{i}"].Value;
                                int points = (ushort)row[$"lotItemBasePoint0{i}"].Value;
                                int quantity = (byte)row[$"lotItemNum0{i}"].Value;
                                if (type == 0) continue;
                                ItemKey item = new ItemKey(game.LotItemTypes[(uint)type], id);

                                string itemText = (lotType == "enemy" ? "enemy " : "") + $"lot {itemLot}[{locs}]";
                                // TODO: See if there are hidden items, handled by goodsType in Sekiro
                                string name = ItemName(game, item);
                                List<string> lotInfo = new List<string> { name };
                                if (quantity > 1)
                                {
                                    lotInfo.Add($"{quantity}x");
                                }
                                if (points != totalPoints)
                                {
                                    lotInfo.Add($"{100.0 * points / totalPoints:0.##}%");
                                }
                                itemLotOutput.Add(string.Join(" ", lotInfo));
                                if (lotInfo.Count() > 0)
                                {
                                    itemText += $" {string.Join(", ", lotInfo)}";
                                }
                                if (quantity <= 0)
                                {
                                    Console.WriteLine($"Warning: Item lot has 0 quantity: {itemText}");
                                }
                                ItemScope scope;
                                if (eventFlag > 0)
                                {
                                    if (!opt["dumplot"])
                                    {
                                        if (equivalentEvents.ContainsKey(eventFlag))
                                        {
                                            eventFlag = equivalentEvents[eventFlag];
                                        }
                                        // Bleh
                                        if (game.EldenRing && eventFlag == 197 && game.Name(item) == "Great Rune of the Unborn")
                                        {
                                            eventFlag = 177;
                                        }
                                    }
                                    scope = new ItemScope(ScopeType.Event, eventFlag);
                                    // Note this doesn't necessarily have to be slot 1. But it should be only one slot...
                                    if (points != totalPoints)
                                    {
                                        // Console.WriteLine($"Has event flag? But random? {itemText}");
                                    }
                                }
                                else
                                {
                                    // First check if it's an ESD gift without a flag, add that as a special case, mainly so it's not accidentally a model
                                    // TODO a less hacky way of extracting this
                                    Entity esdEntity = entities.Find(e => string.IsNullOrEmpty(e.MapName) && e.Name.StartsWith("ESD "));
                                    if (esdEntity != null)
                                    {
                                        // Entity isn't quite accurate. Ideally it should be norandomed, so excluding it is simpler for now.
                                        // They could be exchanged among themselves carefully with a lot of rewriting.
#if DEBUG
                                        if (opt["esdentity"])
                                        {
                                            scope = new ItemScope(ScopeType.Entity, int.Parse(esdEntity.Name.Substring(4)));
                                        }
                                        else
#endif
                                        {
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        // Don't count humans here I guess
                                        int model = entities.Select(e => e.GetModelID()).Where(m => m > 0).OrderBy(m => m).FirstOrDefault();
                                        if (model <= 0)
                                        {
                                            continue;
                                        }
                                        // Note that Unk95 is 0 for finite assets and 1 otherwise, but changing it doesn't seem to change that
                                        bool objModels = entities.All(e => e.Type == "asset");
                                        scope = new ItemScope(objModels ? ScopeType.Asset : ScopeType.Model, model);
                                    }
                                }
                                float chance = (float)points / totalPoints;
                                Location location = new Location(LocationType.Lot, itemLot, itemText, entities, quantity, chance, baseLocation, lotType);
                                AddMultiNest(locations, item, scope, location);
                                if (baseLocation == null)
                                {
                                    baseLocation = location;
                                }

                                if (opt["dumpmapfragments"])
                                {
                                    if (item.Type == ItemType.Goods && (item.ID >= 8600 && item.ID < 8650))
                                    {
                                        Console.WriteLine($"[{item.ID}] = {eventFlag},  // {game.Name(item)}");
                                    }
                                }
                            }
                        }
                        // Write out the info. Some deduplication of sources to make prettier output.
                        if (opt["dumplot"] || entities.Count == 0)
                        {
                            string lotOutput = string.Join(", ", itemLotOutput);
                            if (lotOutput == "") lotOutput = "(no items)";
                            bool simple = false;
                            string text2;
                            if (simple)
                            {
                                // TODO: Add simple cases here (grouping together model names, for instance) for enemy lots
                                SortedSet<string> sources = new SortedSet<string>(entities.Select(e => game.EntityName(e, true) + (e.MapName == "" ? "" : $" in {e.MapName}")));
                                text2 = $"{string.Join(", ", sources)}";
                            }
                            else
                            {
                                // e.NPCParamID > -1 ? $" #{e.NPCParamID}" : ""
                                // SortedSet<string> models = new SortedSet<string>(entities.Select(e => e.EntityName.StartsWith("o") ? $"Treasure in {e.MapName}" : $"{game.ModelName(e, true)} in {e.MapName}"));
                                // SortedSet<string> models = new SortedSet<string>(entities.Select(e => game.EntityName(e, true) + (e.MapName == "" ? "" : $" in {e.MapName}")));
                                SortedSet<string> models = new SortedSet<string>();
                                // Try to find infinite items
                                if (eventFlag > 0 || entities.Any(e => e.Name.Contains("ESD")))
                                {
                                    // Add map detail if the map doesn't exist elsewhere, very fragile
                                    SortedSet<string> maps = new(entities.Where(e => !string.IsNullOrEmpty(e.MapName)).Select(e => e.MapName));
                                    string mapEntityName(Entity e)
                                    {
                                        string s = game.EntityName(e, true, true);
                                        if (s.Contains("event "))
                                        {
                                            string map = s.Split(' ').Last();
                                            if (!maps.Contains(map))
                                            {
                                                s = s.Replace(map, game.MapLocationName(map));
                                            }
                                        }
                                        return s;
                                    }
                                    models.UnionWith(entities.Select(mapEntityName));
                                }
                                else
                                {
                                    Dictionary<string, SortedSet<int>> npcIds = new Dictionary<string, SortedSet<int>>();
                                    Dictionary<string, SortedSet<int>> assetIds = new Dictionary<string, SortedSet<int>>();
                                    foreach (Entity e in entities)
                                    {
                                        string model = e.ModelName;
                                        if (e.NPCParamID > 0 && model != null)
                                        {
                                            AddMulti(npcIds, model, e.NPCParamID);
                                        }
                                        else if (e.AssetID > 0 && model != null)
                                        {
                                            AddMulti(assetIds, model, e.AssetID);
                                        }
                                        else
                                        {
                                            models.Add(game.EntityName(e, true, true));
                                        }
                                    }
                                    models.UnionWith(npcIds.Select(e => $"{e.Key} ({game.ModelName(e.Key)}) npc {string.Join(" ", e.Value)}"));
                                    models.UnionWith(assetIds.Select(e => $"{e.Key} asset {string.Join(" ", e.Value)}"));
                                }
                                text2 = $"{string.Join(", ", models)}";
                            }
                            if (!isBase && opt["dumplot"] && !opt["machine"]) text2 = "^";
                            if (eventFlag > 0) lotOutput += $" - flag {eventFlag}";
                            // string inter = $"[x{row["Unk94"].Value} y{row["Unk95"].Value}]";
                            dump($"{paramPrint}{itemLot} [{text2}] {lotOutput}");
                        }

                        itemLot++;
                        row = itemLots[itemLot];
                    }
                    prevLocation = baseLocation;
                }
                if (prevLocation != null)
                {
                    // 6 is probably too small, but it's safe. Some enemy lots like 431300000 look broken
                    prevLocation.MaxSlots = 6;
                }
                if (opt["html"]) endHtmlSection();
            }

            Dictionary<int, Location> shopLocations = new Dictionary<int, Location>();
            // Unfortunately this does not work great with dupe shops
            List<int> baseShopIds = allLocs.UsedBaseShops.Keys.OrderBy(k => k).ToList();
            // For dumping purposes, mapping from item name to (shop id, event flag)
            SortedDictionary<ItemKey, List<(int, int)>> spellShops = new SortedDictionary<ItemKey, List<(int, int)>>();
            List<int> getBaseShops(int shopID)
            {
                return allLocs.ShopRanges.Where(e => shopID >= e.Key && shopID <= e.Value).Select(e => e.Key).ToList();
            }
            string lastSource = null;
            Dictionary<int, ItemType> matTypes = new Dictionary<int, ItemType> { [1] = ItemType.Protector, [4] = ItemType.Goods };
            Dictionary<int, int> qwcs = new Dictionary<int, int>();
            HashSet<int> excludeBaseShops = new HashSet<int>()
            {
                101775, 101785, // Mausoleums. This may require event flag edits. Now unused
                102700, 102710, 102800, // Mausoleums after 1.12
                110000, // Boc tailoring
                111000, // Self tailoring
                112000, // Ash of War duplication. This may require event flag edits
            };
            foreach (string shopType in new[] { null, "Recipe" })
            {
                string suffix = shopType == null ? "" : $"_{shopType}";
                string paramName = "ShopLineupParam" + suffix;
                string paramPrint = opt["machine"] ? paramName + " " : "";
                if (opt["html"]) writeHtmlSection(paramName, shopType == null ? "#FFF" : "#F8FFFF");
                foreach (PARAM.Row row in game.Params[paramName].Rows)
                {
                    int shopID = row.ID;
                    List<int> baseShops = getBaseShops(shopID);
                    if (baseShops.Count == 0)
                    {
                        // Console.WriteLine($"No base shop for {shopID}");
                        if (!addUnused) continue;
                    }
                    else if (baseShops.All(baseShop => excludeBaseShops.Contains(baseShop)))
                    {
                        // Exclude some known bad/nonrandomized shop ranges
                        if (!addUnused) continue;
                    }

                    int qwc = (int)(uint)row["eventFlag_forRelease"].Value;
                    int type = (byte)row["equipType"].Value;
                    int id = (int)row["equipId"].Value;
                    int quantity = (short)row["sellQuantity"].Value;
                    int eventFlag = (int)(uint)row["eventFlag_forStock"].Value;
                    int material = (int)row["mtrlId"].Value;
                    int value = (int)row["value"].Value;
                    int display = (int)row["nameMsgId"].Value;
                    int purchaseType = (byte)row["costType"].Value;
                    qwcs[shopID] = qwc;
                    // string quantityText = quantity == -1 ? "" : $" ({quantity})"; // (unlimited)
                    // string qwcText = qwc == -1 ? "" : $" {game.QwcName(qwc)}";

                    ItemKey item = new ItemKey(game.ShopItemTypes[type], id);
                    List<string> costs = new List<string>();
                    PARAM.Row matRow = material <= 0 ? null : game.Params["EquipMtrlSetParam"][material];
                    if (matRow != null)
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            int matItem = (int)matRow[$"materialId0{i}"].Value;
                            int matType = (byte)matRow[$"materialCate0{i}"].Value;
                            int matQuant = (sbyte)matRow[$"itemNum0{i}"].Value;
                            if (matItem > 0)
                            {
                                ItemKey mat = new ItemKey(matTypes[matType], matItem);
                                string matQuantString = matQuant > 1 ? $" {matQuant}x" : "";
                                costs.Add($"{ItemName(game, mat)}{matQuantString}");
                            }
                            // costText = $" for {materialQuant} {game.Name(new ItemKey(ItemType.GOOD, materialItem))}";
                        }
                    }
                    // Used to be value != 0, but debug stuff uses weird stuff
                    if (value > 0)
                    {
                        if (value < 0) throw new Exception($"{row.ID}");
                        // 930: 0, 96: 4, 21: 1, 9: 2, 5: 3
                        List<string> units = new List<string> { "rune", "Dragon Heart", "Starlight Shard", "unknown", "Lost Ashes of War", "Heart of Bayle" };
                        string unit = units[purchaseType];
                        costs.Add(value == 1 ? $"{value} {unit}" : $"{value} {unit}s");
                    }
                    // string shopText = $"{qwcText}{quantityText}{costText} - event {eventFlag}";
                    SortedSet<Entity> entities = new SortedSet<Entity>();
                    foreach (int baseShop in baseShops)
                    {
                        if (baseShop == 110000)
                        {
                            entities.Add(new Entity("", "Boc"));
                        }
                        else if (baseShop == 111000)
                        {
                            entities.Add(new Entity("", "Sites of Grace"));
                        }
                        else if (allLocs.UsedBaseShops.TryGetValue(baseShop, out List<Entity> baseEntities))
                        {
                            entities.UnionWith(baseEntities);
                        }
                    }

                    string source = "unknown";
                    if (entities.Count > 0)
                    {
                        SortedSet<string> sources = new SortedSet<string>();
                        sources.UnionWith(entities.Select(e => game.EntityName(e, true, true)));
                        source = $"{string.Join(", ", sources)}";
                    }
                    string flagText = "";
                    if (display > 0) flagText += $" - display {display}";
                    if (eventFlag > 0) flagText += $" - flag {eventFlag}";
                    if (qwc > 0)
                    {
                        // if (!opt["dumpshop"])
                        flagText += $" - unlock {qwc}";
                        if (game.QwcNames.TryGetValue(qwc, out string qwcText)) flagText += $" - {qwcText}";
                    }
                    if (opt["dumpshop"] && source != lastSource)
                    {
                        dump($"--- {source}");
                        lastSource = source;
                    }
                    string quantityStr = quantity > 0 ? $" {quantity}x" : "";
                    string costText = "";
                    if (costs.Count > 0) costText = " for " + string.Join(", ", costs);
                    string shopSuffix = $"{quantityStr}{costText}{flagText}";

                    if (opt["dumpshop"])
                    {
                        dump($"{paramPrint}{shopID}: {ItemName(game, item)}{shopSuffix}");
                    }
                    if (opt["dumpspells"])
                    {
                        if (shopID < 600000 && item.Type == ItemType.Goods && item.ID >= 4000 && item.ID < 8000)
                        {
                            AddMulti(spellShops, item, (shopID, eventFlag));
                        }
                    }
                    Location location = new Location(
                        LocationType.Shop, shopID, $"shop {shopID}[{source}]{shopSuffix}",
                        entities.ToList(), quantity, 1, null, shopType);
                    ItemScope scope;
                    if (eventFlag > 0)
                    {
                        if (equivalentEvents.ContainsKey(eventFlag))
                        {
                            eventFlag = equivalentEvents[eventFlag];
                        }
                        if (quantity <= 0)
                        {
                            // Console.WriteLine($"XX No quantity for event flag shop entry {shopID}");
                        }
                        ScopeType scopeType = quantity > 0 ? ScopeType.Event : ScopeType.ShopInfinite;
                        scope = new ItemScope(scopeType, eventFlag);
                    }
                    // TODO: Figure out what to do with materials. Most of these are probably not randomized.
                    else if (false)
                    {
                        int materialItem = (int)game.Params["EquipMtrlSetParam"][material]["MaterialId01"].Value;
                        scope = new ItemScope(ScopeType.Material, materialItem);
                    }
                    else
                    {
                        // Console.WriteLine($"infinite eventless {shopID}: {ItemName(game, item)}{shopSuffix}");
                        scope = new ItemScope(ScopeType.ShopInfinite, -1);
                    }
                    AddMultiNest(locations, item, scope, location);
                }
                if (opt["html"]) endHtmlSection();
            }
            if (opt["dumpspells"])
            {
                foreach (KeyValuePair<ItemKey, List<(int, int)>> entry in spellShops)
                {
                    if (entry.Value.Select(e => e.Item2).Distinct().Count() <= 1) continue;
                    int mirielFlag = entry.Value.Where(e => e.Item1 >= 100400 && e.Item1 < 100450).Select(e => e.Item2).FirstOrDefault();
                    if (mirielFlag == 0)
                    {
                        Console.WriteLine($"// {game.Name(entry.Key)} ??? {string.Join(" ", entry.Value.Select(e => e.Item2))}");
                    }
                    else
                    {
                        Console.WriteLine($"// {game.Name(entry.Key)}");
                        foreach (int flag in entry.Value.Select(e => e.Item2))
                        {
                            if (flag == mirielFlag) continue;
                            Console.WriteLine($"[{flag}] = {mirielFlag},");
                        }
                    }
                }
            }

            LocationData data = new LocationData();
            data.BossFlags = allLocs.BossFlags;

            // Now can find all location scopes
            List<ScopeType> uniqueTypes = new List<ScopeType> { ScopeType.Event, ScopeType.Entity, ScopeType.Material };
            Regex loreRe = new Regex(@"Note|Letter|Message|Map|Riddle|Sellia's Secret");
            bool printUnique = false;
            foreach ((ItemKey item, Dictionary<ItemScope, List<Location>> itemDict) in locations)
            {
                int unique = 0;
                // List<int> flags = entry.Value.Locations.Keys.Where(s => s.Type == ScopeType.EVENT).Select(s => s.ID).Distinct().ToList();
                // if (flags.Count >= 2) Console.WriteLine($"--- {game.Name(entry.Key)}");
                foreach ((ItemScope scope, List<Location> locKeys) in itemDict)
                {
                    int id = uniqueTypes.Contains(scope.Type) ? scope.ID : -1;
                    unique = unique == -1 ? -1 : (id == -1 ? -1 : unique + 1);
                    SortedSet<int> shopIds = new SortedSet<int>(locKeys.Where(k => k.Type == LocationType.Shop).SelectMany(k => getBaseShops(k.ID)));
                    SortedSet<int> shopQwcs = new SortedSet<int>(locKeys.Where(k => k.Type == LocationType.Shop)
                        .Select(k => qwcs.TryGetValue(k.ID, out int qwc) ? qwc : 0)
                        //.Select(qwc => equivalentEvents.ContainsKey(qwc) ? equivalentEvents[qwc] : qwc)
                        //.Where(qwc => qwc != -1 && !restrictiveQwcs.Contains(qwc)));
                        .Where(qwc => qwc > 0)
                        );
                    SortedSet<int> allShop = new SortedSet<int>(shopIds.Union(shopQwcs));
                    if (shopIds.Count() + shopQwcs.Count() != allShop.Count())
                    {
                        Console.WriteLine($"XX Overlapping qwc/shop ids for location {scope}");
                    }
                    SortedSet<int> modelBase = new SortedSet<int>();
                    if (scope.Type == ScopeType.Model || scope.Type == ScopeType.Asset)
                    {
                        modelBase = new SortedSet<int>(locKeys.Select(k => k.BaseID));
                    }
                    bool onlyShops = locKeys.All(k => k.Type == LocationType.Shop) && allShop.Count() > 0;
                    LocationScope locationScope = new LocationScope(GameSpec.FromGame.ER, scope.Type, id == -1 ? 0 : id, allShop, modelBase, onlyShops);
                    data.AddLocations(item, scope, locationScope, locKeys);
                }
                if (data.Items.TryGetValue(item, out ItemLocations itemLocs))
                {
                    string name = game.Name(item);
                    // This also includes ashes, talismans, weapons, spells, quest items, ashes of war
                    itemLocs.Unique = unique > 0 && unique < 10
                        && item.Type != ItemType.Protector && !game.IsEldenArrow(item)
                        && !loreRe.IsMatch(name);
                    if (printUnique)
                    {
                        PARAM.Row row = game.Item(game.GetBaseWeapon(item));
                        int rarity = row == null ? -1 : (byte)row["rarity"].Value;
                        Console.WriteLine($"{(itemLocs.Unique ? "**" : "")}{name}  {rarity}");
                    }
                }
            }
            if (opt["dumplot"] || opt["dumpshop"])
            {
                Environment.Exit(0);
            }
            return data;
        }

        private string GenericItemName(ItemKey item)
        {
            string t = item.Type.ToString().ToLowerInvariant();
            t = t[0].ToString().ToUpperInvariant() + t.Substring(1);
            return $"{t} {item.ID}";
        }

        private string ItemName(GameData game, ItemKey item)
        {
            // Move this to main stuff?
            string gem = null;
            if (item.Type == ItemType.Custom)
            {
                PARAM.Row custom = game.Item(item);
                if (custom != null)
                {
                    item = new ItemKey(ItemType.Weapon, (int)custom["baseWepId"].Value + (byte)custom["reinforceLv"].Value);
                    int gemId = (int)custom["gemId"].Value;
                    if (gemId > 0)
                    {
                        gem = ItemName(game, new ItemKey(ItemType.Gem, gemId));
                    }
                }
            }
            int upgrade = item.Type == ItemType.Weapon ? item.ID % 100 : 0;
            if (upgrade > 0)
            {
                item = new ItemKey(ItemType.Weapon, item.ID - upgrade);
            }
            game.ItemNames.TryGetValue(item, out string name);
            if (name == null || name == "")
            {
                name = GenericItemName(item);
                PARAM.Row itemRow = game.Item(item);
                if (itemRow == null) name += "*";
            }
            if (game.RevItemNames.TryGetValue(name, out List<ItemKey> alts) && alts.Count > 1)
            {
                name += $" ({GenericItemName(item)})";
            }
            if (upgrade > 0)
            {
                name += $" +{upgrade}";
            }
            if (gem != null)
            {
                name += $" with {gem}";
            }
            return name;
        }

        public static readonly Dictionary<int, int> equivalentEvents = new Dictionary<int, int>
        {
            // This could probably be in a config, but there aren't many in Elden Ring anyway
            // Gideon gifts, map from shop items -> ESD gift items
            [280040] = 400282,
            [280050] = 400283,
            [68210] = 68210,
            [280070] = 400285,
            // Bernahl gift after Volcano quests, map from Farm Azula drop -> ESD gift item
            [400295] = 400291,
            // Lone Wolf Ashes, map from shop item -> ESD gift item.
            // Not necessary for the bell itself since it has the same flag in both cases, and also not randomized.
            [280030] = 400390,

            // From Ansbach corpse locations to Enir-Ilim
            [400622] = 400624,
            [400623] = 400624,
            [400625] = 400624,
            // Igon drop locations, mapping to end-of-quest drop. This drop is also needed for AoW dupe
            [400711] = 400712,
            [400714] = 400712,
            // Death Knight, no need for separate flags affecting weights
            [520711] = 520710,
            // Gravesite Dragon, likewise
            [530861] = 530860,

            // Various scroll shops, mapping from other locations to Miriel
            // Great Glintstone Shard
            [100590] = 140040,
            [120810] = 140040,
            // Swift Glintstone Shard
            [100600] = 140050,
            [120820] = 140050,
            // Glintstone Cometshard
            [100570] = 140020,
            [120790] = 140020,
            // Star Shower
            [100580] = 140030,
            [120800] = 140030,
            // Glintblade Phalanx
            [100610] = 140060,
            [120830] = 140060,
            // Carian Slicer
            [100620] = 140070,
            [120840] = 140070,
            // O, Flame!
            [130620] = 140260,
            // Flame, Fall Upon Them
            [130650] = 140300,
            // Giantsflame Take Thee
            [130640] = 140290,
            // Black Flame
            [130660] = 140310,
            // Surge, O Flame!
            [130630] = 140280,
            // Black Flame Blade
            [130670] = 140320,
            // Lord's Heal
            [130680] = 140330,
            // Lord's Aid
            [130690] = 140340,
            // Assassin's Approach
            [130700] = 140350,
            // Darkness
            [130710] = 140360,
            // Radagon's Rings of Light
            [130720] = 140370,
            // Law of Regression
            [130730] = 140380,
            // Lightning Spear
            [130740] = 140390,
            // Ancient Dragons' Lightning Strike
            [130780] = 140430,
            // Honed Bolt
            [130750] = 140400,
            // Ancient Dragons' Lightning Spear
            [130770] = 140420,
            // Electrify Armament
            [130760] = 140410,

            // You can get multiple of these in-game
            // Glintstone Pebble ??? 100500 120500
            // Glintstone Stars ??? 100550 110770
            // Glintstone Arc ??? 100540 120510
        };

        private class ItemLocs
        {
            // Map from lot type to map item lot to locations
            public Dictionary<string, SortedDictionary<int, List<Entity>>> UsedItemLots { get; set; }
            // Map from starting shop to locations
            public SortedDictionary<int, List<Entity>> UsedBaseShops { get; set; }
            // Map from starting shop to ending shop
            public Dictionary<int, int> ShopRanges { get; set; }
            // Map from boss entities to their flags, used for hints
            public Dictionary<uint, uint> BossFlags { get; set; }
            // Map from lot/event flag to ItemKey. Similar to Sekiro, but doesn't edit existing lots
            public Dictionary<int, ItemKey> LotsToCreate { get; set; }
        }

        private ItemLocs FindItemLocs()
        {
            ItemLocs ret = new ItemLocs
            {
                UsedItemLots = new()
                {
                    ["map"] = new(),
                    ["enemy"] = new(),
                },
                UsedBaseShops = new(),
                ShopRanges = new(),
                BossFlags = new(),
                LotsToCreate = new(),
            };
            // Hardcode recipes
            ret.ShopRanges[30000] = 39999;

            // Config
            // ESD id -> start shop range, end shop range
            Dictionary<int, (int, int)> parameterizedEsdShops = new();
            // event id -> entity id / arg position
            Dictionary<int, uint> eventEntities = new();
            Dictionary<int, int> eventPositions = new();
            // flag id -> entity id
            Dictionary<uint, uint> flagEntities = new();
            // actual entity id -> display entity id
            Dictionary<uint, uint> displayEntities = new();
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = System.IO.File.OpenText($@"{game.Dir}\Base\itemlocations.txt"))
            {
                ItemLocationConfig locConfig = deserializer.Deserialize<ItemLocationConfig>(reader);
                if (locConfig.EsdShops != null)
                {
                    foreach (KeyValuePair<int, string> entry in locConfig.EsdShops)
                    {
                        string[] range = entry.Value.Split(' ');
                        parameterizedEsdShops[entry.Key] = (int.Parse(range[0]), int.Parse(range[1]));
                    }
                }
                if (locConfig.EventEntities != null)
                {
                    foreach ((int ev, string entity) in locConfig.EventEntities)
                    {
                        if (entity.StartsWith("X") && int.TryParse(entity.Substring(1), out int pos))
                        {
                            eventPositions[ev] = pos / 4;
                        }
                        else
                        {
                            eventEntities[ev] = uint.Parse(entity);
                        }
                    }
                }
                if (locConfig.FlagEntities != null)
                {
                    foreach ((uint flag, string entity) in locConfig.FlagEntities)
                    {
                        flagEntities[flag] = uint.Parse(entity);
                    }
                }
                if (locConfig.DisplayEntities != null)
                {
                    foreach ((uint actual, uint display) in locConfig.DisplayEntities)
                    {
                        displayEntities[actual] = display;
                    }
                }
            }
            foreach ((uint flag, uint entity) in flagEntities)
            {
                ret.BossFlags[entity] = flag;
            }
            foreach ((uint actual, uint display) in displayEntities)
            {
                if (ret.BossFlags.TryGetValue(actual, out uint flag))
                {
                    ret.BossFlags[display] = flag;
                }
            }

            Dictionary<Entity, Entity> objects = new();
            Dictionary<int, List<Entity>> usedNpcs = new();
            Dictionary<uint, List<Entity>> usedEntities = new();
            Dictionary<int, List<Entity>> usedEsds = new();
            Dictionary<int, List<Entity>> usedAssets = new();

            // Hardcode Melina, she's evidently not an MSB part
            AddMulti(usedEsds, 3000, new Entity("", "Melina"));

            // Map from item lot to entity id (if applicable), string description
            Dictionary<int, List<(uint, string)>> eventItemLocations = new();
            // Map from event id to arg positions of item lot
            Dictionary<int, SortedSet<int>> eventLotCalls = new();
            // Map from entity id to NPC name ids
            Dictionary<uint, SortedSet<int>> entityNpcNames = new();
            // Map from event id to arg positions of (entity id, NPC name id)
            Dictionary<int, SortedSet<(int, int)>> npcNameCalls = new();
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event ev in entry.Value.Events)
                {
                    for (int j = 0; j < ev.Instructions.Count; j++)
                    {
                        EMEVD.Instruction ins = ev.Instructions[j];
                        if (ins.Bank == 2003 && (ins.ID == 36 || ins.ID == 4))
                        {
                            EMEVD.Parameter p = ev.Parameters.Find(q => q.InstructionIndex == j);
                            if (p == null)
                            {
                                int lot = BitConverter.ToInt32(ins.ArgData, 0);
                                eventEntities.TryGetValue((int)ev.ID, out uint entity);
                                if (flagEntities.TryGetValue(entity, out uint flagEntity))
                                {
                                    // Console.WriteLine($"lot {lot} - event {ev.ID} - entity {entity} - flagEntity {flagEntity}");
                                    entity = flagEntity;
                                }
                                AddMulti(eventItemLocations, lot, (entity, $"event {ev.ID} in {entry.Key}"));
                            }
                            else
                            {
                                AddMulti(eventLotCalls, (int)ev.ID, (int)p.SourceStartByte / 4);
                            }
                        }
                        if (ins.Bank == 2003 && ins.ID == 11)
                        {
                            // Enable/disabled, entity id, slot number, name id
                            List<object> args = ins.UnpackArgs(new List<ArgType> { ArgType.Byte, ArgType.UInt32, ArgType.Int32, ArgType.Int32 });
                            if ((byte)args[0] == 0) continue;
                            List<EMEVD.Parameter> ps = ev.Parameters.Where(q => q.InstructionIndex == j).ToList();
                            if (ps.Count == 0)
                            {
                                uint entityId = (uint)args[1];
                                int nameId = (int)args[3];
                                if (entityId > 0 && nameId > 0)
                                {
                                    AddMulti(entityNpcNames, entityId, nameId);
                                }
                            }
                            else
                            {
                                EMEVD.Parameter entityParam = ps.Find(p => p.TargetStartByte == 1 * 4);
                                EMEVD.Parameter nameParam = ps.Find(p => p.TargetStartByte == 3 * 4);
                                if (entityParam != null && nameParam != null)
                                {
                                    AddMulti(npcNameCalls, (int)ev.ID, ((int)entityParam.SourceStartByte / 4, (int)nameParam.SourceStartByte / 4));
                                }
                            }
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event ev in entry.Value.Events)
                {
                    for (int j = 0; j < ev.Instructions.Count; j++)
                    {
                        EMEVD.Instruction ins = ev.Instructions[j];
                        if (ins.Bank == 2000 && (ins.ID == 0 || ins.ID == 6))
                        {
                            // TODO: Put this in a utility to avoid doing UnpackArgs so much everywhere.
                            // The offset logic needs to be general for other games, should generalize it there.
                            int offset = ins.ID == 0 ? 2 : 2;
                            int eventId = BitConverter.ToInt32(ins.ArgData, (offset - 1) * 4);
                            if (eventLotCalls.TryGetValue(eventId, out SortedSet<int> indices))
                            {
                                List<object> args = ins.UnpackArgs(Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4));
                                foreach (int index in indices)
                                {
                                    int lot = (int)args[offset + index];
                                    if (lot <= 0) continue;
                                    string entityText = "";
                                    eventEntities.TryGetValue(eventId, out uint entity);
                                    if (entity == 0 && (eventPositions.TryGetValue(eventId, out int pos)))
                                    {
                                        // Use the first argument by default for common events, or a specified value
                                        entity = (uint)(int)args[offset + pos];
                                    }
                                    if (flagEntities.TryGetValue(entity, out uint flagEntity))
                                    {
                                        // Console.WriteLine($"lot {lot} - event {eventId} - entity {entity} - flagEntity {flagEntity}");
                                        entity = flagEntity;
                                    }
                                    // ev.ID
                                    AddMulti(eventItemLocations, lot, (entity, $"event {eventId}{entityText} in {entry.Key}"));
                                }
                                // Console.WriteLine($"Calling {eventId}({string.Join(", ", args)}) - {string.Join(" ", indices)}");
                                // Console.WriteLine($"InitializeEvent({string.Join(", ", args)})");
                            }
                            if (npcNameCalls.TryGetValue(eventId, out SortedSet<(int, int)> nameIndices))
                            {
                                List<object> args = ins.UnpackArgs(Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4));
                                foreach ((int, int) val in nameIndices)
                                {
                                    (int entityIndex, int nameIndex) = val;
                                    uint entityId = (uint)(int)args[offset + entityIndex];
                                    int nameId = (int)args[offset + nameIndex];
                                    if (entityId > 0 && nameId > 0)
                                    {
                                        AddMulti(entityNpcNames, entityId, nameId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Fake lot for O Mother
            if (!opt["dumplot"])
            {
                eventItemLocations[GameData.EldenRingBase + 30] = new() { (2050451680, "event 90005570 in m61_50_45_00") };
                ret.LotsToCreate[GameData.EldenRingBase + 30] = new ItemKey(ItemType.Goods, 2009004);
            }

            // Map from NPC param ids to name ids
            // Boss names take priority over this, and CharaInitParam ids are used for humans.
            // This particular mapping is mainly used for merchant shops, to match up map point entries with Bell Bearing flags.
            Dictionary<int, int> npcParamNames = new Dictionary<int, int>();
            foreach (PARAM.Row row in game.Params["NpcParam"].Rows)
            {
                int nameId = (int)row["nameId"].Value;
                if (nameId > 0)
                {
                    npcParamNames[row.ID] = nameId;
                }
            }

            // Map from item lot to ESD ids
            Dictionary<int, HashSet<int>> talkItemLocations = new Dictionary<int, HashSet<int>>();
            // Map from starting shop id to ESD ids
            Dictionary<int, HashSet<int>> talkShopLocations = new Dictionary<int, HashSet<int>>();
            foreach (KeyValuePair<int, (int, int)> entry in parameterizedEsdShops)
            {
                AddMulti(talkShopLocations, entry.Value.Item1, entry.Key);
                ret.ShopRanges[entry.Value.Item1] = entry.Value.Item2;
            }
            HashSet<int> allEsds = new HashSet<int>();
            IEnumerable<ESD.Condition> GetConditions(List<ESD.Condition> condList) => Enumerable.Concat(condList, condList.SelectMany(cond => GetConditions(cond.Subconditions)));
            bool getEsdInt(byte[] arg, out int val)
            {
                val = 0;
                if (arg.Length == 6 && arg[0] == 0x82 && arg[5] == 0xA1)
                {
                    val = BitConverter.ToInt32(arg, 1);
                    return true;
                }
                return false;
            }
            // 22: regular shop. 111: transposition. (135: dragon) 142: alteration. 143: ash of war dupe. 144: puppet. (146: soul/puppet dupe), 148 (new transposition?)
            List<int> shopCmdTypes = new List<int> { 22, 111, 135, 142, 143, 144, 146, 148 };
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    int esdId = int.Parse(esdEntry.Key.Substring(1));
                    allEsds.Add(esdId);
                    // First pass: find item id statements
                    long talkLotCall = -1;
                    foreach ((long groupId, var machine) in esd.StateGroups)
                    {
                        foreach ((long id, ESD.State state) in machine)
                        {
                            // In practice, these only occur in EntryCommands, so this can be a shortcut if needed
                            foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, GetConditions(state.Conditions).SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                            {
                                if (cmd.CommandBank == 1 && cmd.CommandID == 104 && cmd.Arguments.Count == 1)
                                {
                                    byte[] arg = cmd.Arguments[0];
                                    if (getEsdInt(arg, out int lot))
                                    {
                                        AddMulti(talkItemLocations, lot, esdId);
                                    }
                                    // Simple parameterized case: getting argument from 0 index, at most once per ESD file, in common library func
                                    // However, it may require going a few steps up the call chain in the DLC
                                    else if (arg.Length == 3 && arg[0] == 0x40 && arg[1] == 0xB8 && arg[2] == 0xA1)
                                    {
                                        talkLotCall = groupId;
                                    }
                                }
                                else if (cmd.CommandBank == 1 && shopCmdTypes.Contains(cmd.CommandID) && cmd.Arguments.Count == 2)
                                {
                                    if (getEsdInt(cmd.Arguments[0], out int start) && getEsdInt(cmd.Arguments[1], out int end))
                                    {
                                        AddMulti(talkShopLocations, start, esdId);
                                        if (ret.ShopRanges.TryGetValue(start, out int nowEnd) && nowEnd != end)
                                        {
                                            Console.WriteLine($"Warning: For NPC shop c1_{cmd.CommandID} in t{esdId}, expected end {nowEnd}, got {end}. Overlapping shops may break randomization.");
                                        }
                                        ret.ShopRanges[start] = end;
                                    }
                                }
                                else if (false && cmd.CommandBank == 1 && cmd.CommandID == 146 && cmd.Arguments.Count == 3)
                                {
                                    // 307006000: 100300-100309 vs 100324
                                    if (getEsdInt(cmd.Arguments[1], out int start) && getEsdInt(cmd.Arguments[2], out int end))
                                    {
                                        AddMulti(talkShopLocations, start, esdId);
                                        if (ret.ShopRanges.TryGetValue(start, out int nowEnd) && nowEnd != end) throw new Exception($"{esdId}: {start}-{end} vs {nowEnd}");
                                        ret.ShopRanges[start] = end;
                                    }
                                }
                            }
                        }
                    }
                    if (talkLotCall > 0)
                    {
                        // Second pass: parameterized item calls.
                        // As of the DLC, now requires multiple passes.
                        // Assume one item lot arg per machine (lot1 in esdlang). Otherwise, make this a list/multidictionary.
                        // Map of (machine id, arg pos) where the lot is passed from a caller.
                        Dictionary<long, int> lotCalls = new() { [talkLotCall] = 0 };
                        int tries = 0;
                        while (lotCalls.Count > 0 && tries++ < 10)
                        {
                            Dictionary<long, int> newLotCalls = new();
                            foreach ((long groupId, var machine) in esd.StateGroups)
                            {
                                foreach ((long id, ESD.State state) in machine)
                                {
                                    foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, GetConditions(state.Conditions).SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                                    {
                                        if (cmd.CommandBank == 6 && lotCalls.TryGetValue(cmd.CommandID, out int argPos) && cmd.Arguments.Count > argPos)
                                        {
                                            byte[] arg = cmd.Arguments[argPos];
                                            if (getEsdInt(arg, out int lot))
                                            {
                                                // Our suffering is at its end
                                                AddMulti(talkItemLocations, lot, esdId);
                                            }
                                            else if (arg.Length == 3 && (arg[0] >= 0x40 && arg[0] <= 0x7F) && arg[1] == 0xB8 && arg[2] == 0xA1)
                                            {
                                                int recPos = arg[0] - 0x40;
                                                newLotCalls[groupId] = recPos;
                                            }
                                        }
                                    }
                                }
                            }
                            lotCalls = newLotCalls;
                        }
                    }
                }
            }

            HashSet<int> trackAssets = new HashSet<int>(game.Params["AssetEnvironmentGeometryParam"].Rows
                .Where(r => (int)r["pickUpItemLotParamId"].Value > 0)
                .Select(r => r.ID));

            bool logEntities = false;
            string entityMapName(string fullMap, string partName)
            {
                if (!fullMap.EndsWith("0") && partName.StartsWith("m") && partName.Contains('-'))
                {
                    // 02 parts will contain the actual map in the name
                    return partName.Split('-').First();
                }
                return fullMap;
            }
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                string location = entry.Key;
                MSBE msb = entry.Value;
                foreach (MSBE.Part part in msb.Parts.GetEntries())
                {
                    Entity id;
                    int esdId = 0;
                    string typeStr = part.GetType().Name.ToLowerInvariant();
                    if (part is MSBE.Part.Enemy e) // part is MSBE.Part.DummyEnemy
                    {
                        esdId = e.TalkID;
                        string partLocation = entityMapName(location, part.Name);
                        List<uint> groupIDs = part.EntityGroupIDs.Where(groupID => groupID > 0).ToList();
                        id = new Entity(partLocation, part.Name, part.EntityID, e.NPCParamID, e.CharaInitID, groupIDs, Type: typeStr);
                        id.TalkID = esdId;
                    }
                    else if (part is MSBE.Part.Asset || part is MSBE.Part.DummyAsset || logEntities)
                    {
                        string partLocation = entityMapName(location, part.Name);
                        List<uint> groupIDs = part.EntityGroupIDs.Where(groupID => groupID > 0).ToList();
                        id = new Entity(partLocation, part.Name, part.EntityID, GroupIDs: groupIDs, Type: typeStr);
                        if (part is MSBE.Part.Asset)
                        {
                            // GameEditionDisable
                            if (part.Unk44 == 1)
                            {
                                id.Type = "dummyasset";
                            }
                            int modelId = id.GetModelID();
                            // Filter these so we don't have to rifle through a million assets later
                            if (trackAssets.Contains(modelId))
                            {
                                id.AssetID = modelId;
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                    if (id.MapName == location)
                    {
                        id.Position = part.Position;
                    }
                    else
                    {
                        // Convert local coords to match MapName if they mismatch
                        // TODO: Use MapOffset method
                        Vector3 global = coord.ToGlobalCoords(location, part.Position).Item1;
                        id.Position = coord.ToLocalCoords(id.MapName, global);
                        id.OriginalMapName = location;
                    }
                    if (entityNpcNames.TryGetValue(id.EntityID, out SortedSet<int> nameIds))
                    {
                        id.NameID = nameIds.First();
                    }
                    else if (npcParamNames.TryGetValue(id.NPCParamID, out int paramNameId))
                    {
                        id.NameID = paramNameId;
                    }

                    objects[id] = id;
                    // Add another key in case there are name references (for treasure), but not actually returned
                    if (id.MapName != location)
                    {
                        Entity originalId = id.DeepCopy();
                        originalId.MapName = location;
                        objects[originalId] = id;
                    }
                    if (id.EntityID > 0)
                    {
                        AddMulti(usedEntities, id.EntityID, id);
                    }
                    if (id.NPCParamID > 0)
                    {
                        AddMulti(usedNpcs, id.NPCParamID, id);
                    }
                    if (esdId > 0)
                    {
                        AddMulti(usedEsds, esdId, id);
                        // Console.WriteLine($"{id} talk {esdId} + {allEsds.Contains(esdId)}");
                    }
                    if (id.AssetID > 0)
                    {
                        AddMulti(usedAssets, id.AssetID, id);
                    }
                }
            }
            if (logEntities)
            {
                foreach ((uint id, List<Entity> entities) in usedEntities.OrderBy(e => e.Key))
                {
                    Console.WriteLine($"{id}: {string.Join(", ", entities.Select(e => $"{game.EntityName(e, true)} in {e.MapName}"))}");
                }
            }
            List<Entity> esdEntity(int esd, string text = "") => new List<Entity> { new Entity("", $"{text}ESD {(esd)}") };
            foreach (KeyValuePair<int, HashSet<int>> entry in talkItemLocations)
            {
                AddMulti(ret.UsedItemLots["map"], entry.Key, entry.Value
                    .SelectMany(esd => usedEsds.TryGetValue(esd, out List<Entity> ents)
                        ? ents.Concat(esdEntity(esd))
                        : esdEntity(esd, "unused ")));
            }
            foreach (KeyValuePair<int, HashSet<int>> entry in talkShopLocations)
            {
                HashSet<int> esds = entry.Value;
                foreach (int esd in esds)
                {
                    if (!usedEsds.ContainsKey(esd) && !addUnused)
                    {
                        continue;
                    }
                    AddMulti(ret.UsedBaseShops, entry.Key, usedEsds.ContainsKey(esd) ? usedEsds[esd] : esdEntity(esd, "unused "));
                }
            }
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                string location = entry.Key;
                MSBE msb = entry.Value;
                foreach (MSBE.Event.Treasure treasure in msb.Events.Treasures)
                {
                    if (treasure.TreasurePartName != null)
                    {
                        Entity keyId = new Entity(location, treasure.TreasurePartName);
                        if (!objects.TryGetValue(keyId, out Entity id))
                        {
                            // This will happen for dummy assets, but that is fine
                            if (logUnused) Console.WriteLine($"Missing entity for treasure {treasure.TreasurePartName} and lot {treasure.ItemLotID}");
                            continue;
                        }
                        if (id.Type == "dummyasset") continue;
                        AddMulti(ret.UsedItemLots["map"], treasure.ItemLotID, id);
                    }
                }
            }
            List<string> npcLotTypes = new List<string> { "enemy", "map" };
            foreach (PARAM.Row row in game.Params["NpcParam"].Rows)
            {
                int npcID = row.ID;
                foreach (string lotType in npcLotTypes)
                {
                    int itemLot = (int)row[$"itemLotId_{lotType}"].Value;
                    if (itemLot == -1)
                    {
                        continue;
                    }
                    // Console.WriteLine($"item lot {i} - enemy {game.Params["ItemLotParam_enemy"][itemLot] != null} map {game.Params["ItemLotParam_map"][itemLot] != null}");
                    if (!usedNpcs.ContainsKey(npcID))
                    {
                        // if (logUnused) Console.WriteLine($"Unused NPC: {npcID}");
                        if (addUnused)
                        {
                            AddMulti(ret.UsedItemLots[lotType], itemLot, new Entity("", $"unused npc {npcID}"));
                        }
                        continue;
                    }
                    AddMulti(ret.UsedItemLots[lotType], itemLot, usedNpcs[npcID]);
                }
            }
            foreach (PARAM.Row row in game.Params["AssetEnvironmentGeometryParam"].Rows)
            {
                int itemLot = (int)row["pickUpItemLotParamId"].Value;
                if (itemLot <= 0) continue;
                if (!usedAssets.ContainsKey(row.ID))
                {
                    // if (logUnused) Console.WriteLine($"Unused NPC: {npcID}");
                    if (addUnused)
                    {
                        AddMulti(ret.UsedItemLots["map"], itemLot, new Entity("", $"unused asset {row.ID}"));
                    }
                    continue;
                }
                AddMulti(ret.UsedItemLots["map"], itemLot, usedAssets[row.ID]);
            }

            Entity eventifyEntity(Entity id)
            {
                id = id.DeepCopy();
                id.Type = "event " + (id.Type ?? "entity");
                return id;
            }
            Entity getDisplayEntity(Entity id)
            {
                // Rewrite the entity location so it can be identified in-game
                if (!opt["dumplot"]
                    && displayEntities.TryGetValue(id.EntityID, out uint display)
                    && usedEntities.TryGetValue(display, out List<Entity> displayId))
                {
                    return displayId.First();
                }
                return id;
            }
            foreach (KeyValuePair<int, List<(uint, string)>> entry in eventItemLocations)
            {
                int lot = entry.Key;
                foreach (var loc in entry.Value)
                {
                    (uint entity, string desc) = loc;
                    if (entity > 0 && usedEntities.ContainsKey(entity))
                    {
                        AddMulti(ret.UsedItemLots["map"], lot, usedEntities[entity].Select(getDisplayEntity).Select(eventifyEntity));
                        AddMulti(ret.UsedItemLots["map"], lot, new Entity("", desc));
                    }
                    else
                    {
                        // Always add this, regardless of addUnused, to easier find it
                        AddMulti(ret.UsedItemLots["map"], lot, new Entity("", $"unknown entity{(entity == 0 ? "" : $" {entity}")} {desc}"));
                    }
                    // This could perhaps be metadata on the main entity, but this representation seems good and useful
                }
            }

            foreach (string lotType in new List<string> { "map", "enemy" })
            {
                int lastLot = 0;
                foreach (PARAM.Row lot in game.Params[$"ItemLotParam_{lotType}"].Rows)
                {
                    int itemLot = lot.ID;
                    if (itemLot != lastLot + 1)
                    {
                        if (!ret.UsedItemLots[lotType].ContainsKey(itemLot))
                        {
                            // if (logUnused) Console.WriteLine($"Unconnected item lot {itemLot}: {game.LotName(itemLot)}");
                            if (addUnused)
                            {
                                AddMulti(ret.UsedItemLots[lotType], itemLot, new Entity("", "unknown"));
                            }
                        }
                    }
                    lastLot = itemLot;
                }
            }
            return ret;
        }
    }
}
