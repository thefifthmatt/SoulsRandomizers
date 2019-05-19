using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using static DS3StaticItemRandomizer.LocationData;
using static DS3StaticItemRandomizer.LocationData.ItemScope;
using static DS3StaticItemRandomizer.LocationData.LocationKey;
using static DS3StaticItemRandomizer.Permutation;
using static DS3StaticItemRandomizer.Util;

namespace DS3StaticItemRandomizer
{
    public class PermutationWriter
    {
        private static List<string> itemValueCells = new List<string> { "shopPrice", "Unk9", "Costvalue", "shopId" };

        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private PARAM64 itemLots;
        private PARAM64 shops;
        private PARAM64 npcs;

        private readonly Dictionary<ItemKey, bool> finiteShopClassification = new Dictionary<ItemKey, bool>();
        private readonly Dictionary<PriceCategory, List<int>> prices = new Dictionary<PriceCategory, List<int>>();
        private readonly Dictionary<ItemKey, float> dropCost = new Dictionary<ItemKey, float>();
        private readonly Dictionary<int, int> lotCost = new Dictionary<int, int>();

        public PermutationWriter(GameData game, LocationData data, AnnotationData ann)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            itemLots = game.Param("ItemLotParam");
            shops = game.Param("ShopLineupParam");
            npcs = game.Param("NpcParam");
        }

        public enum PriceCategory
        {
            // First three should match ItemType ordering. Non-goods are very broad, to give the chance for some really good deals.
            WEAPON, ARMOR, RING,
            // The rest are mainly goods
            SPELLS, FINITE_GOOD, INFINITE_GOOD, UPGRADE, TRANSPOSE
        }

        public void Write(Random random, Permutation permutation)
        {
            foreach (KeyValuePair<HintCategory, Dictionary<SlotKey, SlotKey>> entry in permutation.Hints)
            {
                Console.WriteLine($"-- Hints for {HintCategories[entry.Key]}:");
                foreach (KeyValuePair<SlotKey, SlotKey> assign in entry.Value.OrderBy(e => (game.Name(e.Key.Item), permutation.GetLogOrder(e.Value))))
                {
                    LocationScope scope = data.Location(assign.Value).LocScope;
                    Console.WriteLine($"{game.Name(assign.Key.Item)}: {ann.GetLocationHint(assign.Value, permutation.SpecialLocation(scope))}");
                }
                Console.WriteLine();
            }
            Console.WriteLine("-- End of hints");
            // Gather all potential prices to select from
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
            {
                ItemKey item = entry.Key;
                PARAM64.Row row = game.Item(item);
                int price = (int) row[itemValueCells[(int)item.Type]].Value;
                int sellPrice = (int)row["sellValue"].Value;
                foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                {
                    bool material = itemLoc.Scope.Type == ScopeType.MATERIAL;
                    foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationType.SHOP))
                    {
                        PARAM64.Row shop = shops[loc.ID];
                        int shopPrice = (int)shop["value"].Value;
                        if (price == -1 && shopPrice == -1) continue;
                        PriceCategory cat = GetPriceCategory(item, material);
                        // Don't price regular items toooo high - looking at you, 20k for Tower Key. Key items are priced separately anyway
                        if (cat == PriceCategory.FINITE_GOOD && price > 10000) continue;
                        AddMulti(prices, cat, shopPrice == -1 ? price : shopPrice);
                    }
                }
            }
            // Gather 'costs' per item based on existing drop percentages, and the difficulty of enemies which drop them
            // Enemy difficulty is calculated as 2 * (HP of enemy) + (# of souls acquired after enemy is killed).
            float itemCost = 500f;
            HashSet<ScopeType> infiniteScopes = new HashSet<ScopeType> { ScopeType.MODEL, ScopeType.SHOP_INFINITE, ScopeType.SHOP_INFINITE_EVENT };
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
            {
                ItemKey item = entry.Key;
                ItemLocations locs = entry.Value;
                if (!locs.Locations.Keys.Any(loc => infiniteScopes.Contains(loc.Type))) continue;
                double percentages = 0;
                int costs = 0;
                foreach (ItemLocation itemLoc in entry.Value.Locations.Values.Where(loc => loc.Scope.Type == ScopeType.MODEL))
                {
                    double modelPercentages = 0;
                    int modelCosts = 0;
                    int count = 0;
                    foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationType.LOT))
                    {
                        SortedDictionary<int, string> npcInfo = new SortedDictionary<int, string>();
                        double locPercentages = 0;
                        int subcount = 0;
                        if (!lotCost.ContainsKey(loc.BaseID))
                        {
                            int locCosts = 0;
                            foreach (EntityId id in loc.Entities.Where(e => e.NPCParamID != -1))
                            {
                                int npcId = id.NPCParamID;
                                PARAM64.Row npc = npcs[npcId];
                                int hp = (int)npc["Hp"].Value;
                                int soul = (int)npc["getSoul"].Value;
                                locCosts += hp * 2 + soul;
                                subcount++;
                            }
                            if (subcount == 0) continue;
                            locCosts /= subcount;
                            lotCost[loc.BaseID] = locCosts;
                        }
                        PARAM64.Row row = itemLots[loc.ID];
                        int totalPoints = 0;
                        for (int i = 1; i <= 8; i++)
                        {
                            totalPoints += (short)row[$"LotItemBasePoint0{i}"].Value;
                        }
                        subcount = 0;
                        for (int i = 1; i <= 8; i++)
                        {
                            int id = (int)row[$"ItemLotId{i}"].Value;
                            if (id == item.ID)
                            {
                                int points = (short)row[$"LotItemBasePoint0{i}"].Value;
                                int quantity = (byte)row[$"LotItemNum{i}"].Value;
                                double drop = (double)points / totalPoints;
                                if (quantity > 1)
                                {
                                    // If dropping more than one, reverse engineer drop rate for 1
                                    drop = Math.Pow(drop, 1.0 / quantity);
                                }
                                locPercentages += drop;
                                subcount++;
                            }
                        }
                        if (subcount == 0) continue;
                        locPercentages /= subcount;
                        if (locPercentages < 0.75)
                        {
                            modelCosts += lotCost[loc.BaseID];
                            modelPercentages += locPercentages;
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        percentages += modelPercentages / count;
                        costs += modelCosts / count;
                    }
                }
                if (costs > 0)
                {
                    // If any drops were observed for this item, update the item cost.
                    // Otherwise, use the same drop as the previous item, as adjacent items usually have similar desirability.
                    itemCost = (float)(costs / percentages);
                }
                dropCost[item] = itemCost;
            }

            Dictionary<SlotKey, ItemSource> newRows = new Dictionary<SlotKey, ItemSource>();
            HashSet<int> deleteRows = new HashSet<int>();
            // Dump all target data per-source, before wiping it out
            foreach (SiloPermutation silo in permutation.Silos.Values)
            {
                foreach (SlotKey sourceKey in silo.Mapping.Values.SelectMany(v => v))
                {
                    ItemLocation source = data.Location(sourceKey);
                    foreach (LocationKey locKey in source.Keys)
                    {
                        if (locKey.Type == LocationType.LOT)
                        {
                            deleteRows.Add(locKey.ID);
                        }
                    }
                    // Synthetic items, like Path of the Dragon
                    if (source.Keys.Count() == 0)
                    {
                        newRows[sourceKey] = new ItemSource(source, null);
                        continue;
                    }
                    // Pick one of the source for item data - they should be equivalent.
                    LocationKey key = source.Keys[0];
                    PARAM64.Row row;
                    if (key.Type == LocationType.LOT)
                    {
                        row = itemLots[key.ID];
                    }
                    else
                    {
                        row = shops[key.ID];
                    }
                    Dictionary<string, object> rowDict = row.Cells.ToDictionary(c => c.Name, c => c.Value);
                    newRows[sourceKey] = new ItemSource(source, rowDict);
                }
            }
            int dragonFlag = 0;
            Dictionary<int, byte> itemRarity = Enumerable.ToDictionary(itemLots.Rows.Where(row => deleteRows.Contains((int)row.ID)), row => (int)row.ID, row => (byte)row["LotItemRarity"].Value); // new Dictionary<int, byte>();
            itemLots.Rows = itemLots.Rows.FindAll(row => !deleteRows.Contains((int)row.ID));

            for (int i = 0; i < 20; i++) Console.WriteLine();
            Console.WriteLine($"-- Spoilers:");
            foreach (KeyValuePair<RandomSilo, SiloPermutation> siloEntry in permutation.Silos)
            {
                RandomSilo siloType = siloEntry.Key;
                SiloPermutation silo = siloEntry.Value;
                if (siloType == RandomSilo.REMOVE) continue;
                foreach (KeyValuePair<SlotKey, List<SlotKey>> mapping in silo.Mapping.OrderBy(e => permutation.GetLogOrder(e.Key)))
                {
                    ItemLocation targetLocation = data.Location(mapping.Key);
                    // Event flag - it just so happens that most of the time, we can use the scope to find the one event flag to use - scripts don't specially care about one vs the other.
                    int eventFlag = targetLocation.Scope.EventID;
                    foreach (SlotKey sourceKey in mapping.Value)
                    {
                        ItemKey item = sourceKey.Item;
                        int quantity = data.Location(sourceKey).Quantity;
                        string quantityStr = quantity == 1 ? "" : $" {quantity}x";
                        Console.WriteLine($"{game.Name(item)}{quantityStr}{ann.GetLocationDescription(mapping.Key)}");
                        // TODO: Path of the Dragon
                        bool isDragon = item.Equals(new ItemKey(ItemType.GOOD, 9030));
                        if (isDragon)
                        {
                            if (eventFlag == -1) throw new Exception("Path of the Dragon added to lot without event flag");
                            dragonFlag = eventFlag;
                            // Don't need to add own item if there is a separate carrier for the event flag
                            if (mapping.Value.Count > 1) continue;
                        }
                        ItemSource source = newRows[sourceKey];
                        Dictionary<string, object> shopCells = null;
                        Dictionary<string, object> lotCells = null;
                        int price = -1;
                        bool originalShop = false;
                        if (source.Row == null)
                        {
                            // Synthetic item - make up shop entry
                            shopCells = new Dictionary<string, object>()
                            {
                                { "EquipId", item.ID },
                                { "equipType", (byte)item.Type },
                                { "sellQuantity", (short)1 }
                            };
                            MakeSellable(item);
                        }
                        else if (source.Row.ContainsKey("shopType"))
                        {
                            shopCells = source.Row;
                            originalShop = true;
                        }
                        else if (source.Row.ContainsKey("ItemLotId1"))
                        {
                            lotCells = source.Row;
                        }
                        else throw new Exception($"Unknown item source");
                        // TODO: Assigning enemy drops to other enemy drops/infinite shops, should scope which item is being referred to
                        foreach (LocationKey target in targetLocation.Keys)
                        {
                            // Console.WriteLine($"{game.Name(item)}: {source.Loc} -> {target.Text}");
                            if (target.Type == LocationType.LOT)
                            {
                                if (siloType == RandomSilo.MIXED)
                                {
                                    Warn($"Mixed silo {source.Loc} going to {target}");
                                    continue;
                                }
                                if (lotCells == null)
                                {
                                    Dictionary<string, object> sourceShop = shopCells;
                                    if (isDragon)
                                    {
                                        // If path of the dragon, there is an additional scripted award, so change base to ember to avoid confusing duplication
                                        sourceShop = new Dictionary<string, object>(sourceShop);
                                        sourceShop["EquipId"] = 500;
                                    }
                                    lotCells = ShopToItemLot(sourceShop, item, target.BaseID);
                                }
                                else if (targetLocation.Scope.Type == ScopeType.MODEL)
                                {
                                    lotCells = originalShop ? ShopToItemLot(shopCells, item, target.BaseID) : ProcessModelLot(lotCells, item, target.BaseID);
                                }
                                lotCells["getItemFlagId"] = eventFlag;
                                AddLot(target.BaseID, lotCells, itemRarity);
                            }
                            else
                            {
                                // Do some filtering for RandomSilo.MIXED
                                if (shopCells == null)
                                {
                                    if (siloType == RandomSilo.MIXED)
                                    {
                                        Warn($"Mixed silo {source.Loc} going to {target}");
                                        continue;
                                    }
                                    shopCells = ItemLotToShop(lotCells, item);
                                }
                                // If mixed, event flag is present or not based on which shop entry this is (infinite or not)
                                bool infiniteMixed = siloType == RandomSilo.MIXED && (short)shopCells["sellQuantity"] <= 0;
                                // Ignore scope event flag for shop assignment, because some shops also form multidrops
                                int shopEventFlag = (int)shops[target.ID]["EventFlag"].Value;
                                shopCells["EventFlag"] = infiniteMixed ? -1 : shopEventFlag; // (int) lotCells["getItemFlagId"];
                                if (price == -1)
                                {
                                    price = Price(permutation, siloType, item, targetLocation.Scope.Type == ScopeType.MATERIAL, random);
                                    Console.WriteLine($"  (cost: {price})");
                                }
                                shopCells["value"] = price;
                                SetShop(target.ID, shopCells);
                            }
                        }
                    }
                }
            }

            // Remove Storm Ruler infinite shiny (gives Storm Ruler if got the original but somehow dropped it). This lot is not randomized
            itemLots[4600]["LotItemNum1"].Value = (byte)0;
            // Disable Firelink Shrine bonfire without Coiled Sword, with special event flag
            game.Param("ActionButtonParam")[9351]["grayoutFlag"].Value = 14005108;
            // Description for path of the dragon so it's not ?GoodsInfo?
            string dragonInfo = "A gesture of meditation channeling the eternal essence of the ancient dragons";
            string dragonCap = $"{dragonInfo}.\n\nThe path to ascendence can be achieved only by the most resolute of seekers. Proper utilization of this technique can grant deep inner focus.";
            game.SetMessage(GameData.MsgFile.GOODS_INFO, 9030, dragonInfo);
            game.SetMessage(GameData.MsgFile.GOODS_CAPTION, 9030, dragonCap);

            // Do the Path of the Dragon swap
            if (dragonFlag == 0) throw new Exception("Path of the Dragon not assigned to an event");
            game.ReplaceScript("common", 0x2BADCAFE, (uint)dragonFlag);

            // Swap chests
            foreach (KeyValuePair<EntityId, EntityId> swap in permutation.EntitySwaps)
            {
                EntityId e1 = swap.Key;
                EntityId e2 = swap.Value;
                if (e1.MapName != e2.MapName) throw new Exception($"Different maps for entities to swap {e1}, {e2}");
                string col1 = ChestCollisionNames.ContainsKey(e1) ? ChestCollisionNames[e1] : null;
                string col2 = ChestCollisionNames.ContainsKey(e2) ? ChestCollisionNames[e2] : null;
                MSB3 msb = game.EditMap(RevLocationNames[e1.MapName]);
                MSB3.Part thing1 = msb.Parts.Objects.Find(p => p.Name == e1.EntityName);
                MSB3.Part thing2 = msb.Parts.Enemies.Find(p => p.Name == e2.EntityName);
                BasicLocation tmp = BasicLocation.Get(thing2, col2);
                BasicLocation.Get(thing1, col1).Set(thing2);
                tmp.Set(thing1);
            }
        }
        private class BasicLocation
        {
            public string CollisionName;
            public Vector3 Position;
            public Vector3 Rotation;
            public static BasicLocation Get(MSB3.Part part, string collisionName=null)
            {
                string col;
                if (part is MSB3.Part.Enemy e) col = e.CollisionName;
                else if (part is MSB3.Part.Object o) col = o.CollisionName;
                else throw new Exception(part.ModelName);
                if (col == null || col == "")
                {
                    if (collisionName == null) throw new Exception($"Bad location randomization for {part.Name}");
                    col = collisionName;
                }
                return new BasicLocation { CollisionName = col, Position = part.Position, Rotation = part.Rotation };
            }
            public void Set(MSB3.Part part)
            {
                if (part is MSB3.Part.Enemy e) e.CollisionName = CollisionName;
                else if (part is MSB3.Part.Object o) o.CollisionName = CollisionName;
                else throw new Exception(part.ModelName);
                part.Position = Position;
                part.Rotation = Rotation;
            }
        }

        private PriceCategory GetPriceCategory(ItemKey key, bool isTranspose)
        {
            // Effectively don't use transpose category - instead use rules for base category.
            // if (isTranspose) return PriceCategory.TRANSPOSE;
            if (key.Type != ItemType.GOOD) return (PriceCategory)key.Type;
            if (key.ID >= 1200000) return PriceCategory.SPELLS;
            if (key.ID >= 1000 & key.ID <= 1030) return PriceCategory.UPGRADE;
            if (!finiteShopClassification.ContainsKey(key))
            {
                // If infinite shop, item is infinite
                // If finite shop, item is finite
                // If not in any shops, use lot finiteness
                bool finiteShop = false, infiniteShop = false, infiniteLot = false;
                foreach (ItemLocation loc in data.Data[key].Locations.Values)
                {
                    if (loc.Scope.Type == ScopeType.SHOP_INFINITE)
                    {
                        infiniteShop = true;
                    }
                    else if (loc.Scope.Type == ScopeType.MODEL)
                    {
                        infiniteLot = true;
                    }
                    else if (loc.Scope.Type == ScopeType.MATERIAL || (loc.Scope.Type == ScopeType.EVENT && loc.Keys.Any(k => k.Type == LocationType.SHOP)))
                    {
                        finiteShop = true;
                    }
                }
                bool isInfinite = infiniteShop || (!finiteShop && infiniteLot);
                finiteShopClassification[key] = !isInfinite;
            }
            return finiteShopClassification[key] ? PriceCategory.FINITE_GOOD : PriceCategory.INFINITE_GOOD;
        }

        private int Price(Permutation permutation, RandomSilo siloType, ItemKey item, bool isTranspose, Random random)
        {
            PriceCategory cat = GetPriceCategory(item, isTranspose);
            PARAM64.Row row = game.Item(item);
            // Upgrade materials roughly same. Unique ones on sale because of how many are moved to shops usually.
            if (cat == PriceCategory.UPGRADE) {
                int basePrice = (int)row[itemValueCells[(int)item.Type]].Value;
                return siloType == RandomSilo.FINITE ? basePrice / 2 : basePrice;
            }
            int sellPrice = (int)row["sellValue"].Value;
            // If it's a soul, make it cost a more than the soul cost.
            if (cat == PriceCategory.FINITE_GOOD && sellPrice >= 2000)
            {
                return sellPrice + 1000;
            }
            int price;
            if (permutation.ItemLateness.ContainsKey(item) && item.Type == ItemType.GOOD)
            {
                // From 500 (with range) to 10k (without range) based on game lateness
                double basePrice = 500 + permutation.ItemLateness[item] * (10000/1.5 - 500);
                // On sale if not a key item
                if (!permutation.KeyItems.Contains(item)) basePrice /= 2;
                // 50% in either direction
                basePrice = basePrice * (random.NextDouble() + 0.5);
                // Round to next 100 (if less than 2000), 500 or 1000
                List<int> rounds = new List<int> { 500, 1000 };
                if (basePrice < 2000) rounds.Add(100);
                int round = Choice(random, rounds);
                price = (((int)basePrice / round) + 1) * round;
            }
            else
            {
                price = Choice(random, prices[cat]);
                // Here we could also hike up the price for especially good items
            }
            if (price < sellPrice)
            {
                price = sellPrice;
            }
            if (isTranspose && random.NextDouble() < 0.4)
            {
                price = 0;
            }
            return price;
        }

        private void AddLot(int baseLot, Dictionary<string, object> cells, Dictionary<int, byte> itemRarity)
        {
            PARAM64 itemLots = game.Param("ItemLotParam");
            int targetLot = baseLot;
            while (itemLots[targetLot] != null)
            {
                targetLot++;
            }
            PARAM64.Row row = game.AddRow("ItemLotParam", targetLot);
            foreach (KeyValuePair<string, object> cell in cells)
            {
                if (cell.Key == "LotItemRarity")
                {
                    continue;
                }
                row[cell.Key].Value = cell.Value;
            }
            if (itemRarity.ContainsKey(baseLot))
            {
                row["LotItemRarity"].Value = itemRarity[baseLot];
            }
        }

        private void SetShop(int targetShop, Dictionary<string, object> cells)
        {
            PARAM64 shops = game.Param("ShopLineupParam");
            PARAM64.Row row = shops[targetShop];
            foreach (KeyValuePair<string, object> cell in cells)
            {
                if (cell.Key != "qwcID" && cell.Key != "mtrlId")  // This is right... right?
                {
                    row[cell.Key].Value = cell.Value;
                }
            }
        }

        private (int, int) DropRate(ItemKey key, int baseLot, int quantity)
        {
            // Cost is ratio of enemy cost to drop %
            float cost = dropCost.ContainsKey(key) ? dropCost[key] : 10000;
            int enemy = lotCost.ContainsKey(baseLot) ? lotCost[baseLot] : 500;
            double oneDrop = enemy / cost;
            // Keep increasing item quantity until drop rate becomes reasonable. If that is possible.
            while (true)
            {
                // Forward: oneDrop = drop ^ (1/quantity)
                double drop = Math.Pow(oneDrop, quantity);
                int dropNum = (int)(drop * 1000);
                if (dropNum <= 900 || quantity >= 5)
                {
                    return (Math.Max(5, Math.Min(1000, dropNum)), quantity);
                }
                quantity *= 2;
                oneDrop /= 2;
            }
        }

        private Dictionary<string, object> ProcessModelLot(Dictionary<string, object> lotCells, ItemKey key, int baseLot)
        {
            lotCells = new Dictionary<string, object>(lotCells);
            List<int> quantities = new List<int>();
            int totalPoints = 0;
            for (int i = 1; i <= 8; i++)
            {
                totalPoints += (short)lotCells[$"LotItemBasePoint0{i}"];
            }
            for (int i = 1; i <= 8; i++)
            {
                if ((int)lotCells[$"ItemLotId{i}"] == 0)
                {
                    continue;
                }
                ItemKey lotKey = new ItemKey(LocationData.LotTypes[(uint)lotCells[$"LotItemCategory0{i}"]], (int)lotCells[$"ItemLotId{i}"]);
                if (lotKey.Equals(key))
                {
                    quantities.Add((byte)lotCells[$"LotItemNum{i}"]);
                }
                lotCells[$"ItemLotId{i}"] = 0;
                lotCells[$"LotItemCategory0{i}"] = 0xFFFFFFFF;
                lotCells[$"LotItemBasePoint0{i}"] = (short)0;
                lotCells[$"LotItemNum{i}"] = (byte)0;
            }
            if (quantities.Count == 0) quantities.Add(1);
            int cumDrop = 0;
            for (int i = 1; i <= 8; i++)
            {
                if (i <= quantities.Count)
                {
                    int count = quantities[i - 1];
                    (int drop, int quantity) = DropRate(key, baseLot, count);
                    lotCells[$"ItemLotId{i}"] = key.ID;
                    lotCells[$"LotItemCategory0{i}"] = RevLotTypes[key.Type];
                    lotCells[$"LotItemBasePoint0{i}"] = (short)drop;
                    lotCells[$"LotItemNum{i}"] = (byte)quantity;
                    cumDrop += drop;
                    Console.WriteLine($"  Drop chance for {quantity}: {100.0 * drop / 1000}%");
                }
                else if (i == quantities.Count + 1)
                {
                    lotCells[$"LotItemBasePoint0{i}"] = (short)Math.Max(0, 1000 - cumDrop);
                    break;
                }
            }
            return lotCells;
        }

        private Dictionary<string, object> ShopToItemLot(Dictionary<string, object> shopCells, ItemKey key, int baseLot)
        {
            Dictionary<string, object> lotCells = new Dictionary<string, object>();
            lotCells["ItemLotId1"] = (int)shopCells["EquipId"];
            lotCells["LotItemCategory01"] = LocationData.RevLotTypes[(ItemType)(byte)shopCells["equipType"]];
            int quantity = (short)shopCells["sellQuantity"];
            if (quantity > 0)
            {
                // Ring of sacrifice multi-drops do not work
                if (key.Equals(new ItemKey(ItemType.RING, 20210)) && quantity > 1)
                {
                    quantity = 1;
                }
                lotCells["LotItemNum1"] = (byte)quantity;
                lotCells["LotItemBasePoint01"] = (short)100;
            }
            else
            {
                (int drop, int quant) = DropRate(key, baseLot, 1);
                Console.WriteLine($"  Drop chance for {quant}: {100.0 * drop / 1000}%");
                lotCells["LotItemNum1"] = (byte)quant;
                lotCells["LotItemBasePoint01"] = (short)drop;
                lotCells["LotItemCategory02"] = 0xFFFFFFFF;
                lotCells["LotItemBasePoint02"] = (short)(1000 - drop);
            }
            lotCells["cumulateNumFlagId"] = -1; // ?
            return lotCells;
        }

        private Dictionary<string, object> ItemLotToShop(Dictionary<string, object> lotCells, ItemKey itemKey)
        {
            Dictionary<string, object> shopCells = new Dictionary<string, object>();
            // For an item like this, assume QWC id stays the same
            ItemKey lotKey = null;
            int totalPoints = 0;
            for (int i = 1; i <= 8; i++)
            {
                totalPoints += (short)lotCells[$"LotItemBasePoint0{i}"];
            }
            for (int i = 1; i <= 8; i++)
            {
                if ((int)lotCells[$"ItemLotId{i}"] == 0)
                {
                    continue;
                }
                lotKey = new ItemKey(LocationData.LotTypes[(uint)lotCells[$"LotItemCategory0{i}"]], (int)lotCells[$"ItemLotId{i}"]);
                if (!lotKey.Equals(itemKey))
                {
                    lotKey = null;
                    continue;
                }
                shopCells["EquipId"] = lotKey.ID;
                shopCells["equipType"] = (byte)lotKey.Type;
                int basePoints = (short)lotCells[$"LotItemBasePoint0{i}"];
                if (basePoints == totalPoints)
                {
                    // TODO: If no event id or material id, this won't do much. But that is intended?
                    shopCells["sellQuantity"] = (short)(byte)lotCells[$"LotItemNum{i}"];
                }
                else
                {
                    shopCells["sellQuantity"] = (short)-1;
                }
                break;
            }
            if (lotKey == null)
            {
                Console.WriteLine($"XX Invalid source location for {itemKey}!! {String.Join(", ", lotCells.Select(e => e.Key + " = " + e.Value))}");
                return null;
            }
            MakeSellable(lotKey);
            return shopCells;
        }

        private void MakeSellable(ItemKey key)
        {
            PARAM64.Row itemRow = game.Item(key);
            PARAM64.Cell costCell = itemRow[itemValueCells[(int)key.Type]];
            // Items must have this field to be sold in shops
            if ((int)costCell.Value <= 0)
            {
                costCell.Value = 1000;
            }
        }

        private class ItemSource
        {
            public readonly ItemLocation Loc;
            // Maybe use a Row object? But it might be nice to edit shops in place...
            public readonly Dictionary<string, object> Row;
            public ItemSource(ItemLocation loc, Dictionary<string, object> row)
            {
                this.Loc = loc;
                this.Row = row;
            }
        }
    }
}
