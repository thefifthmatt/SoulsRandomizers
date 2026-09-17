using LightInject;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.ItemEventConfig;
using static RandomizerCommon.ItemLocEditor;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.Location;
using static RandomizerCommon.Permutation;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class PermutationWriter
    {
        private readonly GameData game;
        private readonly LocationData data;
        private readonly AnnotationData ann;
        private readonly ExternalLocationData externalData;
        private readonly ItemEditor editor;
        private readonly ItemLocEditor locEditor;
        private readonly ItemEventConfig eventConfig;
        private readonly EldenCoordinator coord;

        private readonly PARAM shops;

        // Determination of whether an item is finite, for price classification purposes
        private readonly Dictionary<ItemKey, bool> isItemFiniteCache = new Dictionary<ItemKey, bool>();
        // Prices for items in a given category
        private readonly Dictionary<PriceCategory, List<int>> prices = new Dictionary<PriceCategory, List<int>>();
        private readonly Dictionary<ItemKey, int> upgradePrices = new Dictionary<ItemKey, int>();
        // List of quantities and drop chances per category
        private readonly Dictionary<PriceCategory, List<Dictionary<int, float>>> dropChances = new Dictionary<PriceCategory, List<Dictionary<int, float>>>();
        // Reversed GameData.ItemLotTypes
        // private readonly Dictionary<ItemType, uint> lotValues;

        private static readonly Dictionary<int, float> defaultChances = new Dictionary<int, float> { { 1, 0.05f } };

        // For Elden Ring, map from unpowered flag -> (unpowered item, powered item)
        public static readonly Dictionary<int, (ItemKey, ItemKey)> GreatRuneItems = new Dictionary<int, (ItemKey, ItemKey)>
        {
            // Godrick's Great Rune
            [171] = (new ItemKey(ItemType.Goods, 8148), new ItemKey(ItemType.Goods, 191)),
            // Radahn's Great Rune
            [172] = (new ItemKey(ItemType.Goods, 8149), new ItemKey(ItemType.Goods, 192)),
            // Morgott's Great Rune
            [173] = (new ItemKey(ItemType.Goods, 8150), new ItemKey(ItemType.Goods, 193)),
            // Rykard's Great Rune
            [174] = (new ItemKey(ItemType.Goods, 8151), new ItemKey(ItemType.Goods, 194)),
            // Mohg's Great Rune
            [175] = (new ItemKey(ItemType.Goods, 8152), new ItemKey(ItemType.Goods, 195)),
            // Malenia's Great Rune
            [176] = (new ItemKey(ItemType.Goods, 8153), new ItemKey(ItemType.Goods, 196)),
            // Great Rune of the Unborn
            [177] = (new ItemKey(ItemType.Goods, 10080), new ItemKey(ItemType.Goods, 10080)),
        };

        public PermutationWriter(
            GameData game,
            LocationData data,
            AnnotationData ann,
            ExternalLocationData externalData,
            ItemEditor editor,
            ItemLocEditor locEditor,
            ItemEventConfig eventConfig,
            EldenCoordinator coord = null)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.externalData = externalData;
            this.editor = editor;
            this.locEditor = locEditor;
            this.eventConfig = eventConfig;
            this.coord = coord;
            // itemLots = game.Param("ItemLotParam");
            shops = game.Param("ShopLineupParam");
            // lotValues = game.LotItemTypes.ToDictionary(e => e.Value, e => e.Key);
        }

        public enum PriceCategory
        {
            // First few should match ItemType ordering. Non-goods are very broad, to give the chance for some really good deals.
            Weapon, Armor, Ring, GoodPlaceholder, Gem,
            // The rest are mainly goods
            Spell, Arrow, FiniteGood, InfiniteGood, Upgrade, Transpose,
            // Some Sekiro categories
            RegularGood, UniqueGood,
        }

        public class Result
        {
            public Dictionary<ItemKey, int> ItemEventFlags { get; set; }
            public Dictionary<ItemLocKey, int> SlotEventFlags { get; set; }
            public Dictionary<int, int> MerchantGiftFlags { get; set; }
        }

        public Result Write(Random random, Permutation permutation, RandomizerOptions opt)
        {
            bool globalSwitch = true;
            Console.WriteLine();
            foreach (string hintType in ann.HintCategories)
            {
                Console.WriteLine($"-- Hints for {hintType}:");
                bool hasHint = false;
                foreach (KeyValuePair<ItemLocKey, ItemLocKey> assign in permutation.Hints[hintType].OrderBy(e => (game.DisplayName(e.Key.Item), permutation.GetLogOrder(e.Value))))
                {
                    LocationScope scope = data.GetItemLoc(assign.Value).LocScope;
                    Console.WriteLine($"{game.DisplayName(assign.Key.Item)}: {ann.GetLocationHint(assign.Value, permutation.SpecialLocation(scope))}");
                    hasHint = true;
                    if (opt["fullhint"])
                    {
                        Console.WriteLine($"- {ann.GetLocationDescription(assign.Value)}");
                    }
                }
                if (!hasHint)
                {
                    Console.WriteLine("(not randomized)");
                }
                Console.WriteLine();
                if (opt["silent"])
                {
                    globalSwitch = false;
                    break;
                }
            }
            Console.WriteLine("-- End of hints");
#if !DEBUG
            for (int i = 0; i < 30; i++) Console.WriteLine();
#endif

            // Gather all potential prices to select from
            // TODO: More model stuff for Sekiro
            if (game.Sekiro)
            {
                foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Items)
                {
                    ItemKey item = entry.Key;
                    if (new[] { "Bulging Coin Purse", "Heavy Coin Purse", "Light Coin Purse", "Dragon's Blood Droplet" }.Contains(game.Name(item))) continue;
                    PARAM.Row row = game.Item(item);
                    foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                    {
                        foreach (Location loc in itemLoc.Locs.Where(k => k.Type == LocationType.Shop))
                        {
                            // No Promissory note
                            if (loc.ID % 100 >= 50) continue;
                            // No Offering box
                            if (loc.ID / 100 == 11005) continue;
                            PARAM.Row shop = shops[loc.ID];
                            int shopPrice = (int)shop["value"].Value;
                            if (shopPrice <= 0) continue;
                            AddMulti(prices, GetSekiroPriceCategory(item), shopPrice);
                            // Console.WriteLine($"Category for {game.Name(item)}: {shopPrice} / {GetSekiroPriceCategory(item)}");
                        }
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Items)
                {
                    ItemKey item = entry.Key;
                    // Only Elden Ring has custom weapons, where itemValueCells is not used
                    PARAM.Row row = game.Item(item);
                    int price = game.EldenRing ? -1 : (int)row[DS3SellFields[item.Type]].Value;
                    // int sellPrice = (int)row["sellValue"].Value;
                    PriceCategory cat = GetPriceCategory(item);
                    foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                    {
                        foreach (Location loc in itemLoc.Locs.Where(k => k.Type == LocationType.Shop))
                        {
                            PARAM.Row shop = shops[loc.ID];
                            if (shop == null) continue;
                            int shopPrice = (int)shop["value"].Value;
                            if (price == -1 && shopPrice == -1) continue;
                            // No custom shops
                            if (game.EldenRing && (byte)shop["costType"].Value > 0) continue;
                            shopPrice = shopPrice == -1 ? price : shopPrice;
                            // Don't price regular items toooo high - looking at you, 20k for Tower Key. Key items are priced separately anyway
                            if (cat == PriceCategory.FiniteGood && shopPrice > 10000) continue;
                            // And Thiollier's concoction
                            if (cat == PriceCategory.InfiniteGood && shopPrice > 20000) continue;
                            // 0 shop prices are okay in transpose shops in DS3, but does not get categorized
                            // in the same way in Elden Ring
                            if (game.EldenRing && shopPrice <= 0) continue;
                            AddMulti(prices, cat, shopPrice);
                            if (cat == PriceCategory.Upgrade && itemLoc.Scope.Type == ScopeType.ShopInfinite)
                            {
                                upgradePrices[item] = shopPrice;
                            }
                        }
                        if (itemLoc.Scope.Type == ScopeType.Model)
                        {
                            Dictionary<int, float> chances = locEditor.GetDropChances(item, itemLoc);
                            // Console.WriteLine($"Location for {game.Name(item)}. Chances {string.Join(",", chances)}");
                            if (chances.Count > 0)
                            {
                                AddMulti(dropChances, cat, chances);
                            }
                        }
                    }
                }
            }
            Dictionary<int, float> chooseDropChances(ItemKey item)
            {
                PriceCategory cat = GetPriceCategory(item);
                Dictionary<int, float> chances;
                if (dropChances.TryGetValue(cat, out List<Dictionary<int, float>> allChances))
                {
                    return Choice(random, allChances);
                }
                else
                {
                    return defaultChances;
                }
            }

            // Set up mapping initial state, then fill it in
            // TODO: Some of the initial state which is very game-specific should be in item editors
            ItemFlagMapping mapping = new();

            // TODO: Make this work for DS3 again.
            Func<int, bool> isPermanent = null;
            Func<int, int> shopFlagForFixedFlag = null;
            List<(int, int)> itemLotFlags = null;
            List<(int, int)> shopFlags = null;
            int greatRuneBaseFlag = GreatRuneItems.Keys.Min();
            int minimumGoodFlag = 50000000;
            if (game.Sekiro)
            {
                List<int> spiritfalls = new List<int> { 51100950, 51300910, 51700990, 51500910, 51110973, 52500940 };
                isPermanent = eventFlag =>
                {
                    return eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022 || spiritfalls.Contains(eventFlag);
                };
                int fixedShopStart = 71103000;
                shopFlagForFixedFlag = flag =>
                {
                    int offset;
                    if (flag >= 6700 && flag < 6800)
                    {
                        offset = flag - 6700;
                    }
                    else if (flag >= 6500 && flag < 6510)
                    {
                        offset = 100 + (flag - 6500);
                    }
                    else if (spiritfalls.Contains(flag))
                    {
                        offset = 110 + spiritfalls.IndexOf(flag);
                    }
                    else throw new Exception($"Internal error: can't place {flag} in shop");
                    return fixedShopStart + offset * 10;
                };
                itemLotFlags = game.Params["ItemLotParam"].Rows
                    .Where(r => (int)r["getItemFlagId"].Value > 0)
                    .Select(r => ((int)r.ID, (int)r["getItemFlagId"].Value)).OrderBy(r => r.Item1).ToList();
                shopFlags = game.Params["ShopLineupParam"].Rows
                    .Where(r => (int)r["eventFlag"].Value > 0)
                    .Select(r => ((int)r.ID, (int)r["eventFlag"].Value)).OrderBy(r => r.Item1).ToList();
            }
            else if (game.EldenRing)
            {
                minimumGoodFlag = 100000;
                // Do some early processing of config to find new fixed lots, and also item lots to track
                HashSet<int> fixedFlags = new HashSet<int>();
                foreach (ItemEventSpec spec in eventConfig.ItemEvents.Concat(eventConfig.ItemTalks))
                {
                    if (spec.FlagTemplate == null) continue;
                    foreach (ItemFlagTemplate t in spec.FlagTemplate)
                    {
                        if (t.Type.Contains("item") && t.EventFlag != null)
                        {
                            List<int> flags = t.EventFlag.Split(' ').Select(w => int.Parse(w)).ToList();
                            if (t.Type == "fixeditem")
                            {
                                // Only Great Runes are in non-permanent-flag range
                                fixedFlags.UnionWith(flags);
                            }
                            else
                            {
                                foreach (int flag in flags)
                                {
                                    mapping.TrackedFlagItems[flag] = null;
                                }
                            }
                        }
                    }
                }
                isPermanent = eventFlag =>
                {
                    return (eventFlag >= 60000 && eventFlag < 70000) || fixedFlags.Contains(eventFlag);
                };
                int fixedShopStart = 100400;
                shopFlagForFixedFlag = flag =>
                {
                    if (GreatRuneItems.ContainsKey(flag))
                    {
                        int runeBase = flag - greatRuneBaseFlag;
                        return fixedShopStart + runeBase * 10;
                    }
                    return flag;
                };
                itemLotFlags = (game.Params["ItemLotParam_map"].Rows.Concat(game.Params["ItemLotParam_enemy"].Rows))
                    .Where(r => (uint)r["getItemFlagId"].Value > 0)
                    .Select(r => ((int)r.ID, (int)(uint)r["getItemFlagId"].Value)).OrderBy(r => r.Item1).ToList();
                shopFlags = game.Params["ShopLineupParam"].Rows
                    .Where(r => r.ID < 600000 &&(uint)r["eventFlag_forStock"].Value > 0)
                    .Select(r => ((int)r.ID, (int)(uint)r["eventFlag_forStock"].Value)).OrderBy(r => r.Item1).ToList();
            }
            HashSet<int> allEventFlags = new HashSet<int>(data.Items.Values.SelectMany(locs => locs.Locations.Values.Select(l => l.Scope.EventID).Where(l => l > 0)));
            if (itemLotFlags != null)
            {
                allEventFlags.UnionWith(itemLotFlags.Select(pair => pair.Item2));
            }
            if (shopFlags != null)
            {
                allEventFlags.UnionWith(shopFlags.Select(pair => pair.Item2));
            }
            int findNextFlag(int flag, int searchStep)
            {
                // Scan for an unused flag
                while (allEventFlags.Contains(flag))
                {
                    flag += searchStep;
                }
                allEventFlags.Add(flag);
                return flag;
            }
            int eventFlagForLocation(int id, LocationType type)
            {
                List<(int, int)> flagList = type == LocationType.Lot ? itemLotFlags : shopFlags;
                int searchStep = type == LocationType.Lot ? 1 : 10;
                int index = flagList.FindIndex(r => r.Item1 == id);
                // Scan forwards then scan backwards through all flags
                for (int i = index + 1; i < flagList.Count; i++)
                {
                    (int newLot, int flag) = flagList[i];
                    if (flag >= minimumGoodFlag)
                    {
                        return findNextFlag(flag, searchStep);
                    }
                }
                for (int i = index - 1; i >= 0; i--)
                {
                    (int newLot, int flag) = flagList[i];
                    if (flag >= minimumGoodFlag)
                    {
                        return findNextFlag(flag, searchStep);
                    }
                }
                throw new Exception($"{type} {id}, found at index {index}/{flagList.Count}, can't allocate a new event flag as too many {type} flags are already used");
            }
            // foreach (int flag in shops.Rows.Select(r => (int)r["EventFlag"].Value).Where(f => f > 1000).Distinct().OrderBy(f => f)) Console.WriteLine($"shop {flag}");

            // Mapping from old permanent event flag to slot key
            Dictionary<ItemLocKey, int> permanentSlots = new Dictionary<ItemLocKey, int>();
            if (isPermanent != null)
            {
                foreach (KeyValuePair<ItemKey, ItemLocations> item in data.Items)
                {
                    foreach (ItemLocation loc in item.Value.Locations.Values)
                    {
                        if (loc.Scope.Type == ScopeType.Event)
                        {
                            int eventFlag = loc.Scope.ID;
                            if (isPermanent(eventFlag))
                            {
                                // Console.WriteLine($"Permanent {eventFlag}: {game.Name(item.Key)}");
                                ItemLocKey source = loc.Key;
                                if (permanentSlots.ContainsKey(source)) throw new Exception($"{eventFlag}");
                                permanentSlots[source] = eventFlag;
                            }
                            else if (mapping.TrackedFlagItems.TryGetValue(eventFlag, out ItemKey assign) && assign == null)
                            {
                                // This is pretty hacky: only record the first item, using ItemKey order.
                                // This distinguishes between Fingerslayer Blade (preferred) and Great Ghost Glovewort (not).
                                // However, TODO: This doesn't work for ashdupe multi-drops as the first item is generally not the AoW
                                mapping.TrackedFlagItems[eventFlag] = item.Key;
                                mapping.ItemEventFlags[item.Key] = -1;
                                // Console.WriteLine($"Tracking {game.Name(item.Key)} for {eventFlag}");
                            }
                        }
                    }
                }
            }
            // Set up tracking for boss souls
            if (game.DS3)
            {
                Dictionary<int, PARAM.Row> materials = game.Params["EquipMtrlSetParam"].Rows.ToDictionary(e => e.ID, e => e);
                foreach (PARAM.Row row in shops.Rows)
                {
                    int mat = (int)row["mtrlId"].Value;
                    if (mat > 0 && materials.TryGetValue(mat, out PARAM.Row mtrl))
                    {
                        int matGood = (int)mtrl["materialId01"].Value;
                        if (matGood >= 700 && matGood < 800)
                        {
                            ItemKey matItem = new ItemKey(ItemType.Goods, matGood);
                            mapping.BossShopItems[row.ID] = matItem;
                            mapping.ItemEventFlags[matItem] = -1;
                        }
                    }
                }
            }
            else if (game.EldenRing)
            {
                bool isSoulId(int id) => (id >= 2950 && id < 2990) || (id >= 2002900 && id <= 2002910);
                Dictionary<int, PARAM.Row> materials = GameEditor.ParamToDictionary(game.Params["EquipMtrlSetParam"]);
                foreach (PARAM.Row row in shops.Rows)
                {
                    // Remembrance dupe, remove requirements. For old and new ranges
                    if ((row.ID >= 101775 && row.ID < 101800) || (row.ID >= 102700 && row.ID < 102850))
                    {
                        ItemKey item = new ItemKey(game.ShopItemTypes[(byte)row["equipType"].Value], (int)row["equipId"].Value);
                        if (item.Type == ItemType.Goods && isSoulId(item.ID))
                        {
                            mapping.BossDupeItems[row.ID] = item;
                            mapping.ItemEventFlags[item] = 0;
                        }
                    }
                    // Remembrance shop
                    int mat = (int)row["mtrlId"].Value;
                    if (mat > 0 && materials.TryGetValue(mat, out PARAM.Row mtrl))
                    {
                        int matGood = (int)mtrl["materialId01"].Value;
                        if (isSoulId(matGood) && (byte)mtrl["materialCate01"].Value == 4)
                        {
                            mapping.BossShopItems[row.ID] = new ItemKey(ItemType.Goods, matGood);
                        }
                    }
                }
                // Also, record key item event flags for hints later (TODO: with multi-items, move hints to slot tracking)
                foreach (ItemKey keyItem in ann.ItemGroups["keyitems"])
                {
                    mapping.ItemEventFlags[keyItem] = 0;
                }
                // Other items for hints, which may not be singletons and are tracked by slot
                mapping.TrackedSlotItems.UnionWith(ann.ItemGroups["keyitems"]);
                mapping.TrackedSlotItems.UnionWith(ann.ItemGroups["markhints"]);

                // There are a few events/qwcs dependent on getting other items
                // Duplicating boss souls: should depend on getting the boss soul (QWC edit)
                // Duplicating ashes of war: should depend on having the item (event edit)
                // Accessing cookbooks: unfortunately, should depend on having the item (permanent flag)
                // 60150 golden tailoring tools (permanent flag)
                // 11109770 etc. has a bunch of equivalent event flags (equivalent)
                // 1042369416 Lone Wolf Ashes are similar (TODO look through QWCs, but also equivalent-ify these)
            }

            Dictionary<ItemLocKey, ItemSource> newRows = new Dictionary<ItemLocKey, ItemSource>();
            Dictionary<string, HashSet<int>> deleteRows = new Dictionary<string, HashSet<int>>();
            // Dump all target data per-source, before wiping it out
            foreach (SiloPermutation silo in permutation.Silos.Values)
            {
                if (externalData.IsActive && silo.Type.Type == RandomSilo.Finite)
                {
                    // TODO: This should probably be done per-target and not per-source across the board, but limit it to this case for now
                    // This is because sources don't automatically become targets for external mapping
                    foreach (ItemLocKey targetKey in silo.Mapping.Keys)
                    {
                        ItemLocation target = data.GetItemLoc(targetKey);
                        foreach (Location locKey in target.Locs)
                        {
                            if (locKey.Type == LocationType.Lot)
                            {
                                AddMulti(deleteRows, locKey.ParamName, locKey.ID);
                            }
                        }
                    }
                }
                foreach (ItemLocKey sourceKey in silo.Mapping.Values.SelectMany(v => v))
                {
                    ItemLocation source = data.GetItemLoc(sourceKey);
                    foreach (Location locKey in source.Locs)
                    {
                        if (locKey.Type == LocationType.Lot)
                        {
                            AddMulti(deleteRows, locKey.ParamName, locKey.ID);
                        }
                    }
                    // Pick one of the source for item data - they should be equivalent.
                    Location key = source.Locs.Find(l => l.Type == LocationType.Lot || l.Type == LocationType.Shop);
                    if (key == null || source.CreateLocation)
                    {
                        // Synthetic items, like Path of the Dragon, or external items
                        newRows[sourceKey] = new ItemSource(source, null);
                        continue;
                    }
                    object itemRow;
                    if (key.Type == LocationType.Lot)
                    {
                        // TODO: Quadratic :/
                        PARAM.Row row = game.Params[key.ParamName][key.ID];
                        itemRow = new LotCells { Game = game, Cells = row.Cells.ToDictionary(c => c.Def.InternalName, c => c.Value) };
                    }
                    else if (key.Type == LocationType.Shop)
                    {
                        PARAM.Row row = game.Params[key.ParamName][key.ID];
                        itemRow = new ShopCells { Game = game, Cells = row.Cells.ToDictionary(c => c.Def.InternalName, c => c.Value) };
                    }
                    else throw new Exception($"Unknown source location {key}");
                    newRows[sourceKey] = new ItemSource(source, itemRow);
                }
            }
            // Hack for 177 event flag Rennala drop.
            // It's the only case a no-item event flag drop is used by the game, it seems, so it's not picked up in LocationData.
            // So don't accidentally automatically grant 177 from Rennala and have it count as acquiring the Great Rune.
            if (game.EldenRing)
            {
                PARAM.Row row = game.Params["ItemLotParam_map"][10182];
                if (row != null && (uint)row["getItemFlagId"].Value == 177)
                {
                    deleteRows["ItemLotParam_map"].Add(10182);
                }
            }
            // Path of the Dragon or O Mother
            if (game.DS3)
            {
                mapping.GestureItem = new ItemKey(ItemType.Goods, 9030);
            }
            else if (game.EldenRing)
            {
                mapping.GestureItem = new ItemKey(ItemType.Goods, 2009004);
            }
            Dictionary<int, int> memoryFlags = new Dictionary<int, int>();
            Dictionary<int, byte> itemRarity = new Dictionary<int, byte>();
            // Doesn't do anything in Elden Ring. Don't bother with this for DS3 either, it's subtle enough to not matter very much
            // Maybe should be an option in Sekiro
            if (game.Sekiro)
            {
                itemRarity = game.Params["ItemLotParam"].Rows
                    .Where(row => deleteRows["ItemLotParam"].Contains(row.ID))
                    .ToDictionary(row => row.ID, row => (byte)row["lotItem_Rarity"].Value);
            }
            foreach (string paramType in deleteRows.Keys)
            {
                game.Params[paramType].Rows.RemoveAll(row => deleteRows[paramType].Contains(row.ID));
            }
            
            // ---------------------------------------- Main loop
            // Tuple of (area, tag type, description)
            List<(string, string, string)> raceModeInfo = new List<(string, string, string)>();
            HashSet<string> defaultFilter = new HashSet<string> { "ignore" };
            bool upgradeDlc = opt["dlc"] && opt["maxweapon"];
            bool debugPerm = false;
            Console.WriteLine($"-- Spoilers:");
            // This used to print silo-by-silo, but game order is more useful for keeping locations together
            Dictionary<RandomSilo, int> printOrder = new()
            {
                [RandomSilo.Finite] = 1,
                [RandomSilo.Infinite] = 4,
                [RandomSilo.Mixed] = 1,
                [RandomSilo.Self] = 1,
                // 'Remove' is intentionally excluded
                [RandomSilo.Crow] = 2,
                [RandomSilo.InfiniteShop] = 2,
                [RandomSilo.InfiniteGear] = 3,
                [RandomSilo.InfiniteCertain] = 4,
            };

            foreach (int siloOrder in printOrder.Values.OrderBy(x => x).Distinct())
            {
                List<SiloPermutation> silos = permutation.Silos.Values.Where(p => printOrder.TryGetValue(p.Type.Type, out int order) && order == siloOrder).ToList();
#if DEBUG
                Console.WriteLine($"-- {siloOrder}. {string.Join(", ", silos.Select(s => s.Type))}");
#endif
                foreach ((LocationSilo siloType, ItemLocKey targetKey, List<ItemLocKey> sourceKeys) in
                    silos.SelectMany(s => s.Mapping.Select(e => (s.Type, e.Key, e.Value))).OrderBy(e => permutation.GetLogOrder(e.Item2)))
                {
                    ItemLocation targetLocation = data.GetItemLoc(targetKey);
                    bool autoUpgrade = upgradeDlc && targetLocation.DLC;
                    // This should probably go somewhere more generic, but for now it's only the remembrance shop which has this behavior
                    if (ann.Slots.TryGetValue(targetKey.Scope, out AnnotationData.SlotAnnotation slotAnn) && slotAnn.HasTag("partdlc"))
                    {
                        autoUpgrade = false;
                        Location shopTarget = targetLocation.Locs.Find(l => l.Type == LocationType.Shop);
                        // Prior heuristic is (targetLocation.DLC && opt["dlcsilo"]) || opt["dlcstart"])
                        // But this can be done more precisely
                        if (upgradeDlc && shopTarget != null && mapping.BossShopItems.TryGetValue(shopTarget.ID, out ItemKey soulItem))
                        {
                            ItemLocKey soulTarget = permutation.GetFiniteTargetKey(soulItem);
                            autoUpgrade = soulTarget == null ? false : data.GetItemLoc(soulTarget).DLC;
                        }
                        // Console.WriteLine($"Upgrading target of {game.DisplayName(targetKey.Item)}: {autoUpgrade}");
                    }
                    // Event flag - it just so happens that most of the time, we can use the scope to find the one event flag to use - scripts don't specially care about one vs the other.
                    int eventFlag = targetLocation.Scope.EventID;
                    foreach (ItemLocKey sourceKey in sourceKeys)
                    {
                        ItemKey item = sourceKey.Item;
                        ItemLocation sourceLoc = data.GetItemLoc(sourceKey);
                        int quantity = sourceLoc.Quantity;
                        string quantityStr = quantity == 1 ? "" : $" {quantity}x";
                        string desc = ann.GetLocationDescription(targetKey, excludeTags: defaultFilter, coord: coord);
                        bool writeSwitch = globalSwitch;
                        if (targetLocation.DLC && !opt["dlc"] && targetKey.Scope.Type == ScopeType.Model)
                        {
                            writeSwitch = false;
                        }
                        if (desc != null && writeSwitch)
                        {
                            Console.WriteLine($"{game.DisplayName(item, quantity)}{desc}");
                        }
#if DEBUG
                        if (desc == null && writeSwitch)
                        {
                            desc = ann.GetLocationDescription(targetKey, coord: coord);
                            Console.WriteLine($"{game.DisplayName(item, quantity)}{desc} - ignored");
                        }
#endif
                        bool printChances = true;
                        if (opt["racemodeinfo"])
                        {
                            HashSet<string> filterTags = ann.RaceModeTags;
                            string raceDesc = ann.GetLocationDescription(targetKey, filterTags);
                            if (!string.IsNullOrEmpty(raceDesc))
                            {
                                // Look up tags just to include better filtering info.
                                // This is slightly outside of the scope of this module.
                                if (ann.Slots.TryGetValue(data.GetItemLoc(targetKey).LocScope, out AnnotationData.SlotAnnotation slot))
                                {
                                    filterTags = new HashSet<string>(filterTags);
                                    filterTags.UnionWith(new[] { "night", "minidungeon", "missable", "norandom" });
                                    string raceTags = string.Join(" ", slot.TagList.Intersect(filterTags).OrderBy(x => x));
                                    raceModeInfo.Add((slot.Area, raceTags, raceDesc));
                                }
                            }
                        }
                        bool isGesture = item.Equals(mapping.GestureItem);
                        // Don't need to add own item if there is a separate carrier for the event flag
                        // In DS3 at least - Elden Ring hides it
                        // TODO: Add item even in normal DS3
                        if (game.DS3 && isGesture && sourceKeys.Count > 1)
                        {
                            mapping.GestureFlag = eventFlag;
                            continue;
                        }

                        if (!newRows.TryGetValue(sourceKey, out ItemSource source))
                        {
                            throw new Exception($"Error: Expected a param row for {sourceKey} in {siloType}");
                        }
                        ShopCells shopSource = null;
                        LotCells lotSource = null;
                        int price = -1;
                        bool originalShop = false;
                        if (source.Row == null)
                        {
                            // Synthetic item - make up shop entry
                            shopSource = locEditor.ShopCellsForItem(item);
                            locEditor.MakeSellable(item);
                        }
                        else if (source.Row is ShopCells)
                        {
                            shopSource = (ShopCells)source.Row;
                            originalShop = true;
                        }
                        else if (source.Row is LotCells)
                        {
                            lotSource = (LotCells)source.Row;
                        }
                        else throw new Exception($"Unknown item source");
                        // TODO: Assigning enemy drops to other enemy drops/infinite shops, should scope which item is being referred to
                        int setEventFlag = -1;
                        foreach (Location target in targetLocation.Locs)
                        {
                            // Console.WriteLine($"{game.Name(item)}: {source.Loc} -> {target.Text}");
                            if (target.Type == LocationType.Lot)
                            {
                                LotCells lotCells = lotSource?.DeepCopy();
                                // Console.WriteLine($"{game.Name(item)}: setting lot target, source is {source.Loc}. lotCells {lotCells} shopCells {shopCells}.");
                                if (siloType.Type == RandomSilo.Mixed)
                                {
                                    Warn($"Mixed silo {source.Loc} going to {target}");
                                    continue;
                                }
                                if (lotCells == null)
                                {
                                    ShopCells lotShop = shopSource;
                                    if (isGesture && game.DS3)
                                    {
                                        // If path of the dragon, there is an additional scripted award, so change base to ember to avoid confusing duplication
                                        // This path is probably not taken in Elden Ring? Since it's a fake lot, but still a lot
                                        lotShop = lotShop.DeepCopy();
                                        lotShop.Item = game.DS3 ? game.ItemForName("Ember") : game.ItemForName("Rune Arc");
                                        // sourceShop["EquipId"] = 500;
                                    }
                                    lotCells = locEditor.ShopToItemLot(lotShop, item, chooseDropChances, writeSwitch);
                                }
                                else if (targetLocation.Scope.Type == ScopeType.Model)
                                {
                                    if (originalShop)
                                    {
                                        lotCells = locEditor.ShopToItemLot(shopSource, item, chooseDropChances, writeSwitch);
                                    }
                                    else
                                    {
                                        Dictionary<int, float> chances = locEditor.GetDropChances(item, data.GetItemLoc(sourceKey));
                                        if (chances.Count == 0) chances = defaultChances;
                                        lotCells = locEditor.ProcessModelLot(lotCells, item, chances, printChances && writeSwitch);
                                        printChances = false;
                                    }
                                }
                                if (permanentSlots.TryGetValue(sourceKey, out int permanentFlag))
                                {
                                    if (debugPerm) Console.WriteLine($"Changing lot flag from temp {eventFlag} to permanent {permanentFlag}");
                                    mapping.RewrittenFlags[eventFlag] = permanentFlag;
                                    lotCells.EventFlag = permanentFlag;
                                }
                                else if (permanentSlots.TryGetValue(targetKey, out int flagToClear))
                                {
                                    if (!mapping.RewrittenFlags.TryGetValue(eventFlag, out int tempFlag))
                                    {
                                        tempFlag = eventFlagForLocation(target.BaseID, LocationType.Lot);
                                    }
                                    if (debugPerm) Console.WriteLine($"Changing lot flag from permanent {flagToClear} to temp {tempFlag}");
                                    mapping.RewrittenFlags[eventFlag] = tempFlag;
                                    lotCells.EventFlag = tempFlag;
                                }
                                else
                                {
                                    lotCells.EventFlag = eventFlag;
                                }
                                setEventFlag = lotCells.EventFlag;
                                if (autoUpgrade)
                                {
                                    for (int i = 1; i <= 8; i++)
                                    {
                                        ItemKey lotKey = lotCells[i];
                                        if (lotKey != null)
                                        {
                                            lotCells[i] = game.AutoUpgrade(lotKey, 25);
                                        }
                                    }
                                }
                                if (externalData.IsActive)
                                {
                                    editor.ExternalItemOverride(sourceLoc, target, lotCells);
                                }
                                // Crow sources are special items so they won't be removed, they must be overwritten
                                locEditor.AddLot(target.ParamName, target.BaseID, lotCells, itemRarity, siloType.Type == RandomSilo.Crow);
                            }
                            else
                            {
                                ShopCells shopCells = shopSource?.DeepCopy();
                                // Do some filtering for RandomSilo.MIXED
                                if (shopCells == null)
                                {
                                    if (siloType.Type == RandomSilo.Mixed)
                                    {
                                        Warn($"Mixed silo {source.Loc} going to {target}");
                                        continue;
                                    }
                                    shopCells = locEditor.ItemLotToShop(lotSource, item);
                                }
                                // If mixed, event flag is present or not based on which shop entry this is (infinite or not)
                                bool infiniteMixed = siloType.Type == RandomSilo.Mixed && shopCells.Quantity <= 0;
                                // Ignore scope event flag for shop assignment, because some shops also form multidrops
                                string flagField = !game.Sekiro ? "eventFlag_forStock" : "eventFlag";
                                object originalFlag = game.Params[target.ParamName][target.ID][flagField].Value;
                                int shopEventFlag = game.EldenRing ? (int)(uint)originalFlag : (int)originalFlag;
                                if (permanentSlots.TryGetValue(sourceKey, out int permanentFlag))
                                {
                                    if (shopFlagForFixedFlag != null)
                                    {
                                        // Way too many event flags involved here.
                                        // There is permanent flag (persists across NG, applies to item only)
                                        // There is shop permanent flag (previously unused, always set with permanent flag)
                                        // There is old shop flag (does not apply to item)
                                        // TODO: This needs to be reverified after merge with Elden logic
                                        int shopPermanentFlag = shopFlagForFixedFlag(permanentFlag);
                                        mapping.ShopPermanentFlags[shopPermanentFlag] = permanentFlag;
                                        permanentFlag = shopPermanentFlag;
                                    }
                                    if (debugPerm) Console.WriteLine($"Changing shop flag from temp {shopEventFlag} to permanent {permanentFlag}");
                                    mapping.RewrittenFlags[shopEventFlag] = permanentFlag;
                                    shopEventFlag = permanentFlag;
                                }
                                else if (permanentSlots.TryGetValue(targetKey, out int flagToClear))
                                {
                                    if (!mapping.RewrittenFlags.TryGetValue(shopEventFlag, out int tempFlag))
                                    {
                                        tempFlag = eventFlagForLocation(target.BaseID, LocationType.Shop);
                                    }
                                    if (debugPerm) Console.WriteLine($"Changing shop flag from permanent {flagToClear} to temp {tempFlag}");
                                    mapping.RewrittenFlags[shopEventFlag] = tempFlag;
                                    shopEventFlag = tempFlag;
                                }
                                shopCells.EventFlag = infiniteMixed ? -1 : shopEventFlag;
                                setEventFlag = shopCells.EventFlag;
                                int baseShop = target.ID / 100;
                                if (price == -1)
                                {
                                    if (siloType.Type == RandomSilo.Self && shopCells.Cells.ContainsKey("value"))
                                    {
                                        // Don't use price calculation for non-randomized shops (can this ever be not defined?)
                                        // TODO: Why go through this shuffling routine at all?
                                        price = shopCells.Value;
                                    }
                                    else
                                    {
                                        bool isTranspose = game.Sekiro
                                            ? (baseShop == 10000 || baseShop == 25000)
                                            : targetLocation.Scope.Type == ScopeType.Material;
                                        price = Price(permutation, siloType.Type, item, isTranspose, random);
                                    }
                                    if (desc != null && writeSwitch)
                                    {
                                        Console.WriteLine($"  (cost: {price})");
                                    }
                                }
                                // Ignoring selected price for offering box
                                int targetPrice = price;
                                if (game.Sekiro && baseShop == 11005)
                                {
                                    targetPrice = (int)shops[target.ID]["value"].Value;
                                    targetPrice = targetPrice / Math.Max(1, quantity);
                                }
                                // Dragon's Tally Board adjustments
                                if (game.Sekiro && target.ID % 100 >= 50 && targetPrice > 10)
                                {
                                    targetPrice = price - price / 10;
                                }
                                // Likewise for custom shops in Elden Ring
                                if (game.EldenRing && target.Subtype == null && (byte)shops[target.ID]["costType"].Value > 0)
                                {
                                    targetPrice = (int)shops[target.ID]["value"].Value;
                                }
                                shopCells.Value = targetPrice;
                                if (autoUpgrade)
                                {
                                    shopCells.Item = game.AutoUpgrade(shopCells.Item, 25);
                                }
                                if (externalData.IsActive)
                                {
                                    editor.ExternalItemOverride(sourceLoc, target, shopCells);
                                }
                                locEditor.SetShop(target, shopCells);
                            }
                        }
                        // Add special flags for specific items
                        if (game.Sekiro)
                        {
                            // Memories
                            if (item.Type == ItemType.Goods && item.ID >= 5200 && item.ID <= 5213)
                            {
                                // -1 event flag probably shouldn't happen, but some finite items may still lack event flags, so just don't update memory count in this case
                                if (setEventFlag > 0)
                                {
                                    memoryFlags[item.ID] = setEventFlag;
                                }
                            }
                        }
                        if (isGesture)
                        {
                            if (setEventFlag == -1) throw new Exception($"Gesture key item added to lot without event flag ({sourceKey} -> {targetKey})");
                            // TODO: Track using ItemEventFlags instead
                            mapping.GestureFlag = setEventFlag;
                        }
                        // Use sourceKey.Item, instead of item, for synthetic item tracking per source
                        if (mapping.ItemEventFlags.TryGetValue(sourceKey.Item, out int existFlag) && existFlag <= 0 && setEventFlag > 0)
                        {
                            mapping.ItemEventFlags[sourceKey.Item] = setEventFlag;
                        }
                        if (mapping.TrackedSlotItems.Contains(sourceKey.Item) && setEventFlag > 0)
                        {
                            mapping.SlotEventFlags[sourceKey] = setEventFlag;
                        }
                    }
                }
            }
            List<string> lotSuffixes = game.EldenRing ? new List<string> { "_map", "_enemy" } : new List<string> { "" };
            foreach (string suffix in lotSuffixes)
            {
                PARAM itemLots = game.Params["ItemLotParam" + suffix];
                itemLots.Rows = itemLots.Rows.OrderBy(r => r.ID).ToList();
            }
            Console.WriteLine("-- End of item spoilers");
            Console.WriteLine();

            // Hacky convenience function for generating race mode list
            if (opt["racemodeinfo"])
            {
                List<string> locationDocs = new List<string>
                {
                    "This is a list of all possible important locations: places you may need to check to finish an item randomizer run, depending on options.",
                    "You may configure which categories apply to a run by checking or unchecking important location categories in the randomizer program.",
                    "To see the actual locations of items in a run, check the latest file in the spoiler_logs directory.",
                };
                foreach (string locationDoc in locationDocs)
                {
                    Console.WriteLine(locationDoc);
                    Console.WriteLine();
                }
                Dictionary<string, List<(string, string)>> areaEntries = new Dictionary<string, List<(string, string)>>();
                SortedDictionary<string, string> tagTypes = new SortedDictionary<string, string>
                {
                    ["altboss"] = "50Minor boss",
                    ["altboss minidungeon"] = "51Minor minidungeon boss",
                    ["altboss night"] = "52Minor night boss",
                    ["boss"] = "10Major boss",
                    ["church"] = "20Flask upgrade",
                    ["minidungeon raceshop"] = "41Minidungeon shop",
                    ["minidungeon talisman"] = "61Minidungeon talisman",
                    ["racemode"] = "00Key item",
                    ["raceshop"] = "30Shop",
                    ["seedtree"] = "21Flask upgrade",
                    ["talisman"] = "60Talisman",
                };
                if (game.DS3)
                {
                    tagTypes = new SortedDictionary<string, string>
                    {
                        ["racemode"] = "00Key item",
                        ["chest racemode"] = "00Key item",
                        ["miniboss racemode"] = "00Key item",
                        ["racemode raceshop"] = "00Key item",
                        ["boss"] = "10Major boss",
                        ["health"] = "20Flask upgrade",
                        ["health miniboss"] = "20Flask upgrade + Powerful enemy",
                        ["chest health"] = "30Flask upgrade + Chest",
                        ["chest"] = "30Chest",
                        ["chest ring"] = "30Chest + Ring",
                        ["raceshop"] = "40Shop",
                        ["ashes"] = "40Ashes",
                        ["miniboss"] = "50Powerful enemy",
                        ["lizard"] = "60Small crystal lizard",
                        ["ring"] = "70Ring",
                    };
                }
                HashSet<string> actualTypes = new HashSet<string>();
                Dictionary<string, List<string>> replaces = new Dictionary<string, List<string>>();
                // Sorta terrible but better than doing it by hand
                string hack = ". Replaces ";
                foreach (var val in raceModeInfo)
                {
                    (string area, string tags, string desc) = val;
                    if (!tagTypes.ContainsKey(tags)) tagTypes[tags] = null;
                    actualTypes.Add(tags);
                    string replace = desc.Substring(desc.IndexOf(hack) + hack.Length).TrimEnd('.');
                    desc = desc.Substring(0, desc.IndexOf(hack));
                    string fullDesc = $"{tagTypes[tags] ?? $"???? {tags}"}{desc}";
                    if (game.EldenRing && ann.Areas[area].Tags != null && ann.Areas[area].Tags.Contains("minidungeon"))
                    {
                        // Hardcode this one, rare since it's a minidungeon following a key item
                        area = area == "snowfield_hiddenpath" ? "snowfield" : ann.Areas[area].Req.Split(' ')[0];
                    }
                    AddMulti(areaEntries, ann.Areas[area].Text, (fullDesc, tags));
                    if (!tags.Contains("raceshop"))
                    {
                        AddMulti(replaces, fullDesc, replace);
                    }
                }
                HashSet<string> visited = new HashSet<string>();
                Dictionary<string, List<string>> finalEntries = new Dictionary<string, List<string>>();
                string titleCase(string s) => s[0].ToString().ToUpperInvariant() + s.Substring(1).ToString();
                foreach (AnnotationData.AreaAnnotation areaAnn in ann.Areas.Values)
                {
                    if (areaAnn.Name == "chapel_start") continue;
                    if (!visited.Add(areaAnn.Text)) continue;
                    if (!areaEntries.TryGetValue(areaAnn.Text, out List<(string, string)> descs)) continue;
                    Console.WriteLine("--- " + titleCase(areaAnn.Text));
                    foreach ((string desc, string tags) in descs.OrderBy(x => x).Distinct())
                    {
                        string text = desc.Substring(2);
                        if (replaces.ContainsKey(desc)) text += $". Replaces {string.Join(", ", replaces[desc].Distinct())}.";
                        Console.WriteLine(text);
                        AddMulti(finalEntries, tags, text);
                    }
                    Console.WriteLine();
                }
                foreach (KeyValuePair<string, string> entry in tagTypes)
                {
                    if (!actualTypes.Contains(entry.Key)) continue;
                    Console.WriteLine($"[\"{entry.Key}\"] = \"{entry.Value}\",  // {finalEntries[entry.Key].Count}");
                }
            }

            // Very game-specific stuff here
            editor.EditLocations(opt, mapping);

            return new Result { ItemEventFlags = mapping.ItemEventFlags, SlotEventFlags = mapping.SlotEventFlags, MerchantGiftFlags = mapping.MerchantGiftFlags };
        }

        public void AutoUpgradeDlc()
        {
            // Attempt to upgrade all DLC locations without a permutation
            // Still do this directly from param data, in case any modifications have been made
            // Can also get name list from location data
            // TODO: This could be split out of PermutationWriter as the main helper routines were split into ItemLocEditor
            List<string> paramNames = new(){ "ShopLineupParam", "ItemLotParam_map", "ItemLotParam_enemy" };
            Dictionary<string, Dictionary<int, PARAM.Row>> allRows = paramNames.ToDictionary(e => e, e => GameEditor.ParamToDictionary(game.Params[e]));
            foreach ((ItemKey locItem, ItemLocations itemLocs) in data.Items)
            {
                foreach (ItemLocation itemLoc in itemLocs.Locations.Values)
                {
                    if (!itemLoc.DLC) continue;
                    foreach (Location loc in itemLoc.Locs)
                    {
                        if (!allRows.TryGetValue(loc.ParamName, out var paramRows)) continue;
                        if (loc.Type == LocationType.Lot && paramRows.TryGetValue(loc.ID, out PARAM.Row row))
                        {
                            LotCells cells = new LotCells { Game = game, Cells = row.Cells.ToDictionary(c => c.Def.InternalName, c => c.Value) };
                            for (int i = 1; i <= 8; i++)
                            {
                                ItemKey item = cells[i];
                                if (item != null)
                                {
                                    cells[i] = game.AutoUpgrade(item, 25);
                                }
                            }
                            locEditor.SetLot(row, cells);
                        }
                        else if (loc.Type == LocationType.Shop && paramRows.TryGetValue(loc.ID, out row))
                        {
                            ShopCells cells = new ShopCells { Game = game, Cells = row.Cells.ToDictionary(c => c.Def.InternalName, c => c.Value) };
                            ItemKey item = cells.Item;
                            cells.Item = game.AutoUpgrade(item, 25);
                            locEditor.SetShop(row, cells);
                        }
                    }
                }
            }
        }

        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");

        private PriceCategory GetSekiroPriceCategory(ItemKey key)
        {
            return data.Items[key].Unique && !game.Name(key).Contains("Jizo")
                ? PriceCategory.UniqueGood : PriceCategory.RegularGood;
        }

        private PriceCategory GetPriceCategory(ItemKey key)
        {
            // Effectively don't use transpose category - instead use rules for base category.
            // if (isTranspose) return PriceCategory.TRANSPOSE;
            if (game.EldenRing)
            {
                if (key.Type != ItemType.Goods)
                {
                    if (game.IsEldenArrow(key))
                    {
                        return PriceCategory.Arrow;
                    }
                    if (key.Type == ItemType.Custom)
                    {
                        return PriceCategory.Weapon;
                    }
                    return (PriceCategory)key.Type;
                }
                if (key.ID >= 4000 && key.ID < 8000 || key.ID >= 2004000 && key.ID < 2008000) return PriceCategory.Spell;
                if (key.ID >= 10100 & key.ID < 11000) return PriceCategory.Upgrade;
            }
            else
            {
                if (key.Type != ItemType.Goods)
                {
                    if (key.Type == ItemType.Weapon && key.ID >= 400000 && key.ID < 500000)
                    {
                        return PriceCategory.Arrow;
                    }
                    return (PriceCategory)key.Type;
                }
                if (key.ID >= 1200000) return PriceCategory.Spell;
                if (key.ID >= 1000 & key.ID <= 1030) return PriceCategory.Upgrade;
            }
            if (!isItemFiniteCache.ContainsKey(key))
            {
                // If infinite shop, item is infinite
                // If finite shop, item is finite
                // If not in any shops, use lot finiteness
                bool finiteShop = false, infiniteShop = false, infiniteLot = false;
                foreach (ItemLocation loc in data.Items[key].Locations.Values)
                {
                    if (loc.Scope.Type == ScopeType.ShopInfinite)
                    {
                        infiniteShop = true;
                    }
                    else if (loc.Scope.Type == ScopeType.Model)
                    {
                        infiniteLot = true;
                    }
                    else if (loc.Scope.Type == ScopeType.Material || (loc.Scope.Type == ScopeType.Event && loc.Locs.Any(k => k.Type == LocationType.Shop)))
                    {
                        finiteShop = true;
                    }
                }
                bool isInfinite = infiniteShop || (!finiteShop && infiniteLot);
                isItemFiniteCache[key] = !isInfinite;
            }
            return isItemFiniteCache[key] ? PriceCategory.FiniteGood : PriceCategory.InfiniteGood;
        }

        // Use simple DS1 item randomizer type system for the moment
        private int Price(Permutation permutation, RandomSilo siloType, ItemKey item, bool isTranspose, Random random)
        {
            if (game.Sekiro)
            {
                // Simpler algorithm here
                if (isTranspose)
                {
                    return 0;
                }
                int price;
                if (permutation.ItemLateness.ContainsKey(item) && item.Type == ItemType.Goods)
                {
                    // From 100 (with range) to 2000 (without range) based on game lateness. But shops are rarely super late.
                    double basePrice = 100 + permutation.ItemLateness[item] * (2000 / 1.5 - 100);
                    // On sale if not a key item
                    if (!permutation.KeyItems.Contains(item)) basePrice /= 2;
                    // 50% in either direction
                    basePrice = basePrice * (random.NextDouble() + 0.5);
                    // Rounding
                    List<int> rounds = new List<int> { 100, 100, 200 };
                    if (basePrice < 300) rounds.Add(50);
                    int round = Choice(random, rounds);
                    price = (((int)basePrice / round) + 1) * round;
                }
                else
                {
                    price = Choice(random, prices[GetSekiroPriceCategory(item)]);
                    // Could use a category for this, but meanwhile just make sure esoteric texts are reasonable
                    if (item.ID >= 2920 && item.ID < 2930) price = Math.Min(price, 200);
                    // And skill prices as well. This is an especially hacky way of being nice.
                    if (item.ID >= 6405 && item.ID < 6500 && game.Name(item) == "Mikiri Counter") price = Math.Min(price, 500);
                }
                PARAM.Row row = game.Item(item);
                int sellPrice = (int)row["sellValue"].Value;
                if (price < sellPrice)
                {
                    price = sellPrice;
                }
                return price;
            }
            else
            {
                PriceCategory cat = GetPriceCategory(item);
                ItemKey rowKey = game.FromCustomWeapon(item);
                PARAM.Row row = game.Item(rowKey);
                // Upgrade materials roughly same. Unique ones on sale because of how many are moved to shops usually.
                if (cat == PriceCategory.Upgrade)
                {
                    if (game.EldenRing && upgradePrices.TryGetValue(item, out int upgradePrice))
                    {
                        return siloType == RandomSilo.Finite ? upgradePrice * 8 / 10 : upgradePrice;
                    }
                    else if (game.DS3)
                    {
                        int basePrice = (int)row[DS3SellFields[item.Type]].Value;
                        return siloType == RandomSilo.Finite ? basePrice / 2 : basePrice;
                    }
                }
                int sellPrice = 0;
                if (rowKey.Type != ItemType.Custom)
                {
                    if (row == null)
                    {
                        // throw new Exception($"{item} was randomized but it doesn't exist in params, likely due to a merged mod");
                    }
                    else
                    {
                        sellPrice = (int)row["sellValue"].Value;
                    }
                }
                // If it's a soul, make it cost a more than the soul cost.
                if (cat == PriceCategory.FiniteGood && sellPrice >= 2000)
                {
                    return sellPrice + 1000;
                }
                int price;
                if (permutation.ItemLateness.ContainsKey(item) && item.Type == ItemType.Goods)
                {
                    // From 500 (with range) to 10k (without range) based on game lateness
                    double basePrice = 500 + permutation.ItemLateness[item] * (10000 / 1.5 - 500);
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
                    // For instance, Scadutree Fragments
                    if (item.Equals(new ItemKey(ItemType.Goods, 2010000)))
                    {
                        price *= 2;
                    }
                }
                if (price < sellPrice)
                {
                    price = sellPrice;
                }
                if (isTranspose && random.NextDouble() < 0.4)
                {
                    // TODO: Elden Ring boss shops?
                    price = 0;
                }
                return price;
            }
        }

        private class ItemSource
        {
            public readonly ItemLocation Loc;
            // Maybe use a Row object? But it might be nice to edit shops in place...
            public readonly object Row;
            public ItemSource(ItemLocation loc, object row)
            {
                this.Loc = loc;
                this.Row = row;
            }
        }
    }
}
