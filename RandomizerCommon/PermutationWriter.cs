using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using SoulsIds;
using static SoulsIds.Events;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.ItemScope;
using static RandomizerCommon.LocationData.LocationKey;
using static RandomizerCommon.Permutation;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class PermutationWriter
    {
        private static List<string> itemValueCells = new List<string> { "shopPrice", "Unk9", "Costvalue", "shopId" };

        private GameData game;
        private LocationData data;
        private AnnotationData ann;
        private Events events;
        private EventConfig eventConfig;

        private PARAM itemLots;
        private PARAM shops;
        private PARAM npcs;

        // Determination of whether an item is finite, for price classification purposes
        private readonly Dictionary<ItemKey, bool> isItemFiniteCache = new Dictionary<ItemKey, bool>();
        // Prices for items in a given category
        private readonly Dictionary<PriceCategory, List<int>> prices = new Dictionary<PriceCategory, List<int>>();
        // List of quantities and drop chances per category
        private readonly Dictionary<PriceCategory, List<Dictionary<int, float>>> dropChances = new Dictionary<PriceCategory, List<Dictionary<int, float>>>();
        // Reversed GameData.ItemLotTypes
        private readonly Dictionary<ItemType, uint> lotValues;

        private static readonly Dictionary<int, float> DEFAULT_CHANCES = new Dictionary<int, float> { { 1, 0.05f } };

        public PermutationWriter(GameData game, LocationData data, AnnotationData ann, Events events, EventConfig eventConfig)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
            this.events = events;
            this.eventConfig = eventConfig;
            itemLots = game.Param("ItemLotParam");
            shops = game.Param("ShopLineupParam");
            npcs = game.Param("NpcParam");
            lotValues = game.LotItemTypes.ToDictionary(e => e.Value, e => e.Key);
        }

        public enum PriceCategory
        {
            // First three should match ItemType ordering. Non-goods are very broad, to give the chance for some really good deals.
            WEAPON, ARMOR, RING,
            // The rest are mainly goods
            SPELLS, ARROWS, FINITE_GOOD, INFINITE_GOOD, UPGRADE, TRANSPOSE,
            // Some Sekiro categories
            REGULAR_GOOD, UNIQUE_GOOD,
        }

        public void Write(Random random, Permutation permutation, RandomizerOptions opt)
        {
            foreach (string hintType in ann.HintCategories)
            {
                Console.WriteLine($"-- Hints for {hintType}:");
                bool hasHint = false;
                foreach (KeyValuePair<SlotKey, SlotKey> assign in permutation.Hints[hintType].OrderBy(e => (game.DisplayName(e.Key.Item), permutation.GetLogOrder(e.Value))))
                {
                    LocationScope scope = data.Location(assign.Value).LocScope;
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
            }
            Console.WriteLine("-- End of hints");
#if !DEBUG
            for (int i = 0; i < 30; i++) Console.WriteLine();
#endif

            // Gather all potential prices to select from
            // TODO: More model stuff for Sekiro
            if (game.Sekiro)
            {
                foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
                {
                    ItemKey item = entry.Key;
                    if (new[] { "Bulging Coin Purse", "Heavy Coin Purse", "Light Coin Purse", "Dragon's Blood Droplet" }.Contains(game.Name(item))) continue;
                    PARAM.Row row = game.Item(item);
                    foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                    {
                        foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationType.SHOP))
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
                foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
                {
                    ItemKey item = entry.Key;
                    PARAM.Row row = game.Item(item);
                    int price = (int)row[itemValueCells[(int)item.Type]].Value;
                    int sellPrice = (int)row["sellValue"].Value;
                    PriceCategory cat = GetPriceCategory(item);
                    foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                    {
                        foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationType.SHOP))
                        {
                            PARAM.Row shop = shops[loc.ID];
                            int shopPrice = (int)shop["value"].Value;
                            if (price == -1 && shopPrice == -1) continue;
                            // Don't price regular items toooo high - looking at you, 20k for Tower Key. Key items are priced separately anyway
                            if (cat == PriceCategory.FINITE_GOOD && price > 10000) continue;
                            AddMulti(prices, cat, shopPrice == -1 ? price : shopPrice);
                        }
                        if (itemLoc.Scope.Type == ScopeType.MODEL)
                        {
                            // Console.WriteLine($"Location for {game.Name(item)}: {itemLoc}");
                            Dictionary<int, float> chances = GetDropChances(item, itemLoc);
                            if (chances.Count > 0)
                            {
                                AddMulti(dropChances, cat, chances);
                            }
                        }
                    }
                }
            }

            // TODO: Make this work for DS3 again.
            List<int> spiritfalls = new List<int> { 51100950, 51300910, 51700990, 51500910, 51110973, 52500940 };
            bool isPermanent(int eventFlag)
            {
                return eventFlag >= 6500 && eventFlag < 6800 || eventFlag == 6022 || spiritfalls.Contains(eventFlag);
            }
            HashSet<int> allEventFlags = new HashSet<int>(data.Data.Values.SelectMany(locs => locs.Locations.Values.Select(l => l.Scope.EventID).Where(l => l != -1)));
            List<(int, int)> itemLotFlags = itemLots.Rows.Select(r => ((int)r.ID, (int)r["getItemFlagId"].Value)).OrderBy(r => r.Item1).ToList();
            int eventFlagForLot(int itemLot)
            {
                int index = itemLotFlags.FindIndex(r => r.Item1 == itemLot);
                for (int i = index + 1; i < itemLotFlags.Count; i++)
                {
                    (int newLot, int flag) = itemLotFlags[i];
                    if (flag >= 50000000)
                    {
                        while (allEventFlags.Contains(flag))
                        {
                            flag++;
                        }
                        allEventFlags.Add(flag);
                        return flag;
                    }
                }
                throw new Exception($"{itemLot}, found at index {index}, can't event a dang flag");
            }
            int start = 71103000;
            int shopFlagForPermanentFlag(int flag)
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
                return start + offset * 10;
            }
            // foreach (int flag in shops.Rows.Select(r => (int)r["EventFlag"].Value).Where(f => f > 1000).Distinct().OrderBy(f => f)) Console.WriteLine($"shop {flag}");
            // Mapping from old permanent event flag to slot key
            Dictionary<SlotKey, int> permanentSlots = new Dictionary<SlotKey, int>();
            if (game.Sekiro)
            {
                foreach (KeyValuePair<ItemKey, ItemLocations> item in data.Data)
                {
                    foreach (ItemLocation loc in item.Value.Locations.Values)
                    {
                        if (loc.Scope.Type == ScopeType.EVENT)
                        {
                            int eventFlag = loc.Scope.ID;
                            if (isPermanent(eventFlag))
                            {
                                // Console.WriteLine($"Permanent {eventFlag}: {game.Name(item.Key)}");
                                SlotKey source = new SlotKey(item.Key, loc.Scope);
                                if (permanentSlots.ContainsKey(source)) throw new Exception($"{eventFlag}");
                                permanentSlots[source] = eventFlag;
                            }
                        }
                    }
                }
            }
            Dictionary<int, ItemKey> randomMaterialItems = new Dictionary<int, ItemKey>();
            Dictionary<ItemKey, int> randomMaterialEventFlags = new Dictionary<ItemKey, int>();
            if (!game.Sekiro)
            {
                Dictionary<int, PARAM.Row> materials = game.Params["EquipMtrlSetParam"].Rows.ToDictionary(e => (int)e.ID, e => e);
                foreach (PARAM.Row row in shops.Rows)
                {
                    int mat = (int)row["mtrlId"].Value;
                    if (mat > 0)
                    {
                        if (materials.TryGetValue(mat, out PARAM.Row mtrl))
                        {
                            int matGood = (int)mtrl["MaterialId01"].Value;
                            if (matGood >= 700 && matGood < 800)
                            {
                                ItemKey matItem = new ItemKey(ItemType.GOOD, matGood);
                                randomMaterialItems[mat] = matItem;
                                randomMaterialEventFlags[matItem] = -1;
                            }
                        }
                    }
                }
            }

            Dictionary<SlotKey, ItemSource> newRows = new Dictionary<SlotKey, ItemSource>();
            HashSet<int> deleteRows = new HashSet<int>();
            // Dump all target data per-source, before wiping it out
            foreach (KeyValuePair<RandomSilo, SiloPermutation> entry in permutation.Silos)
            {
                SiloPermutation silo = entry.Value;
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
                    PARAM.Row row;
                    if (key.Type == LocationType.LOT)
                    {
                        row = itemLots[key.ID];
                    }
                    else
                    {
                        row = shops[key.ID];
                    }
                    Dictionary<string, object> rowDict = row.Cells.ToDictionary(c => c.Def.InternalName, c => c.Value);
                    newRows[sourceKey] = new ItemSource(source, rowDict);
                }
            }
            int dragonFlag = 0;
            Dictionary<int, int> memoryFlags = new Dictionary<int, int>();
            Dictionary<int, byte> itemRarity = Enumerable.ToDictionary(itemLots.Rows.Where(row => deleteRows.Contains((int)row.ID)), row => (int)row.ID, row => (byte)row["LotItemRarity"].Value); // new Dictionary<int, byte>();
            itemLots.Rows = itemLots.Rows.FindAll(row => !deleteRows.Contains((int)row.ID));

            List<string> raceModeInfo = new List<string>();
            Dictionary<int, int> rewrittenFlags = new Dictionary<int, int>();
            Dictionary<int, int> shopPermanentFlags = new Dictionary<int, int>();
            Console.WriteLine($"-- Spoilers:");
            foreach (KeyValuePair<RandomSilo, SiloPermutation> siloEntry in permutation.Silos)
            {
                RandomSilo siloType = siloEntry.Key;
                SiloPermutation silo = siloEntry.Value;
                if (siloType == RandomSilo.REMOVE) continue;
                foreach (KeyValuePair<SlotKey, List<SlotKey>> mapping in silo.Mapping.OrderBy(e => permutation.GetLogOrder(e.Key)))
                {
                    SlotKey targetKey = mapping.Key;
                    ItemLocation targetLocation = data.Location(targetKey);
                    // Event flag - it just so happens that most of the time, we can use the scope to find the one event flag to use - scripts don't specially care about one vs the other.
                    int eventFlag = targetLocation.Scope.EventID;
                    foreach (SlotKey sourceKey in mapping.Value)
                    {
                        ItemKey item = sourceKey.Item;
                        int quantity = data.Location(sourceKey).Quantity;
                        string quantityStr = quantity == 1 ? "" : $" {quantity}x";
                        Console.WriteLine($"{game.DisplayName(item)}{quantityStr}{ann.GetLocationDescription(targetKey, targetLocation.Keys)}");
                        bool printChances = true;
                        if (opt["racemodeinfo"])
                        {
                            HashSet<string> filterTags = ann.RaceModeTags;
                            filterTags = new HashSet<string> { "lizard" };
                            string desc = ann.GetLocationDescription(targetKey, targetLocation.Keys, filterTags);
                            if (!string.IsNullOrEmpty(desc)) raceModeInfo.Add(desc);
                        }
                        bool isDragon = !game.Sekiro && item.Equals(new ItemKey(ItemType.GOOD, 9030));
                        // Don't need to add own item if there is a separate carrier for the event flag
                        if (isDragon && mapping.Value.Count > 1)
                        {
                            dragonFlag = eventFlag;
                            continue;
                        }

                        ItemSource source = newRows[sourceKey];
                        Dictionary<string, object> shopCells = null;
                        Dictionary<string, object> lotCells = null;
                        int price = -1;
                        bool originalShop = false;
                        if (source.Row == null)
                        {
                            // Synthetic item - make up shop entry
                            shopCells = ShopCellsForItem(item);
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
                        int setEventFlag = -1;
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
                                    lotCells = ShopToItemLot(sourceShop, item, random);
                                }
                                else if (targetLocation.Scope.Type == ScopeType.MODEL)
                                {
                                    if (originalShop)
                                    {
                                        lotCells = ShopToItemLot(shopCells, item, random);
                                    }
                                    else
                                    {
                                        Dictionary<int, float> chances = GetDropChances(item, data.Location(sourceKey));
                                        if (chances.Count == 0) chances = DEFAULT_CHANCES;
                                        lotCells = ProcessModelLot(lotCells, item, chances, printChances);
                                        printChances = false;
                                    }
                                }
                                if (permanentSlots.TryGetValue(sourceKey, out int permanentFlag))
                                {
                                    lotCells["getItemFlagId"] = permanentFlag;
                                    rewrittenFlags[eventFlag] = permanentFlag;
                                }
                                else if (permanentSlots.TryGetValue(targetKey, out int flagToClear))
                                {
                                    int tempFlag = rewrittenFlags.TryGetValue(eventFlag, out int existingFlag) ? existingFlag : eventFlagForLot(target.BaseID);
                                    lotCells["getItemFlagId"] = tempFlag;
                                    rewrittenFlags[eventFlag] = tempFlag;
                                }
                                else
                                {
                                    lotCells["getItemFlagId"] = eventFlag;
                                }
                                setEventFlag = (int)lotCells["getItemFlagId"];
                                // Crow sources are special items so they won't be removed, they must be overwritten
                                AddLot(target.BaseID, lotCells, itemRarity, siloType == RandomSilo.CROW);
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
                                if (permanentSlots.TryGetValue(sourceKey, out int permanentFlag))
                                {
                                    // Way too many event flags involved here.
                                    // There is permanent flag (persists across NG, applies to item only)
                                    // There is shop permanent flag (previously unused, always set with permanent flag)
                                    // There is old shop flag (does not apply to item)
                                    int shopPermanentFlag = shopFlagForPermanentFlag(permanentFlag);
                                    rewrittenFlags[shopEventFlag] = shopPermanentFlag;
                                    shopPermanentFlags[shopPermanentFlag] = permanentFlag;
                                    shopEventFlag = shopPermanentFlag;
                                }
                                shopCells["EventFlag"] = infiniteMixed ? -1 : shopEventFlag;
                                setEventFlag = (int)shopCells["EventFlag"];
                                int baseShop = target.ID / 100;
                                if (price == -1)
                                {
                                    if (siloType == RandomSilo.SELF && shopCells.ContainsKey("value"))
                                    {
                                        price = (int)shopCells["value"];
                                    }
                                    else
                                    {
                                        bool isTranspose = game.Sekiro ? (baseShop == 10000 || baseShop == 25000) : targetLocation.Scope.Type == ScopeType.MATERIAL;
                                        price = Price(permutation, siloType, item, isTranspose, random);
                                    }
                                    Console.WriteLine($"  (cost: {price})");
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
                                shopCells["value"] = targetPrice;
                                if (target.ID == 110006) Console.WriteLine($"Staff shop: {string.Join(", ", shopCells)}");
                                SetShop(target.ID, shopCells);
                            }
                        }
                        // Add special flags for specific items
                        if (game.Sekiro)
                        {
                            // Memories
                            if (item.Type == ItemType.GOOD && item.ID >= 5200 && item.ID <= 5213)
                            {
                                // -1 event flag probably shouldn't happen, but some finite items may still lack event flags, so just don't update memory count in this case
                                if (setEventFlag > 0)
                                {
                                    memoryFlags[item.ID] = setEventFlag;
                                }
                            }
                        }
                        else
                        {
                            if (isDragon)
                            {
                                if (setEventFlag == -1) throw new Exception("Path of the Dragon added to lot without event flag");
                                dragonFlag = setEventFlag;
                            }
                            if (randomMaterialEventFlags.ContainsKey(item) && setEventFlag > 0)
                            {
                                randomMaterialEventFlags[item] = setEventFlag;
                            }
                        }
                    }
                }
            }
            itemLots.Rows = itemLots.Rows.OrderBy(r => r.ID).ToList();
            Console.WriteLine("-- End of item spoilers");
            Console.WriteLine();

            // Hacky convenience function for generating race mode list
            if (opt["racemodeinfo"])
            {
                HashSet<string> visited = new HashSet<string>();
                foreach (AnnotationData.AreaAnnotation areaAnn in ann.Areas.Values)
                {
                    if (!visited.Add(areaAnn.Text)) continue;
                    List<string> items = raceModeInfo.Where(r => r != null && r.StartsWith($" in {areaAnn.Text}:")).ToList();
                    foreach (string item in Enumerable.Reverse(items.Distinct()))
                    {
                        Console.WriteLine("In" + item.Substring(3));
                    }
                }
            }

            // Events
            if (game.Sekiro)
            {
                // Sekiro edits
                Dictionary<string, Dictionary<string, ESD>> talks = game.Talk;
                Dictionary<int, EventSpec> talkTemplates = eventConfig.ItemTalks.ToDictionary(e => e.ID, e => e);
                bool parseMachineName(string mIdStr, out int mId)
                {
                    if (!int.TryParse(mIdStr, out mId))
                    {
                        if (mIdStr.StartsWith("x") && int.TryParse(mIdStr.Substring(1), out int diffpart))
                        {
                            mId = 0x7FFFFFFF - diffpart;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return true;
                }
                bool debugEsd = false;
                foreach (KeyValuePair<string, ESD> entry in talks.SelectMany(s => s.Value))
                {
                    void rewriteCondition(ESD.Condition cond, Action<byte[]> rewriteExpr)
                    {
                        rewriteExpr(cond.Evaluator);
                        cond.PassCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                        cond.Subconditions.ForEach(c => rewriteCondition(c, rewriteExpr));
                    }
                    void rewriteCommand(ESD.CommandCall cmd, Action<byte[]> rewriteExpr)
                    {
                        cmd.Arguments.ForEach(rewriteExpr);
                    }
                    string esdName = entry.Key;
                    if (esdName.StartsWith("t") && int.TryParse(esdName.Substring(1), out int esdId) && talkTemplates.TryGetValue(esdId, out EventSpec spec))
                    {
                        ESD esd = entry.Value;
                        Dictionary<int, int> replaceInts = new Dictionary<int, int>();
                        HashSet<int> machines = new HashSet<int>();
                        int tearsMachine = -1;
                        foreach (ItemTemplate t in spec.ItemTemplate)
                        {
                            List<int> templateMachines = new List<int>();
                            foreach (string machineStr in phraseRe.Split(t.Machine))
                            {
                                int machine = parseMachineName(machineStr, out int mId) ? mId : throw new Exception($"Unknown machine id {t.Machine} of {esdName}");
                                templateMachines.Add(machine);
                            }
                            machines.UnionWith(templateMachines);
                            if (t.EventFlag != null)
                            {
                                int flag = int.Parse(t.EventFlag);
                                if (t.Type == "loc")
                                {
                                    if (rewrittenFlags.TryGetValue(flag, out int newFlag))
                                    {
                                        replaceInts[flag] = newFlag;
                                    }
                                }
                                else if (t.Type == "isshin")
                                {
                                    replaceInts[flag] = int.Parse(t.Replace);
                                }
                                else if (opt["extraitems"] && t.Type == "extraitems")
                                {
                                    replaceInts[flag] = int.Parse(t.Replace);
                                }
                                else if (opt["splitskills"] && t.Type == "splitskills")
                                {
                                    replaceInts[flag] = int.Parse(t.Replace);
                                }
                            }
                            if (opt["extraitems"] && t.Type == "tears")
                            {
                                tearsMachine = templateMachines[0];
                            }
                        }
                        if (replaceInts.Count == 0 && tearsMachine == -1)
                        {
                            continue;
                        }
                        void rewriteExpr(byte[] b)
                        {
                            // For now, these can be rewritten in place. If AST were used instead, it could not be in place, as lengths may change.
                            foreach (KeyValuePair<int, int> replace in replaceInts)
                            {
                                int search = SearchInt(b, (uint)replace.Key);
                                if (search != -1)
                                {
                                    if (debugEsd) Console.WriteLine($"In ESD {esdName}, replacing {replace.Key} -> {replace.Value}");
                                    Array.Copy(BitConverter.GetBytes(replace.Value), 0, b, search, 4);
                                }
                            }
                        }
                        foreach (KeyValuePair<long, Dictionary<long, ESD.State>> machine in esd.StateGroups)
                        {
                            if (!machines.Contains((int)machine.Key)) continue;
                            foreach (KeyValuePair<long, ESD.State> stateEntry in machine.Value)
                            {
                                ESD.State state = stateEntry.Value;
                                state.Conditions.ForEach(c => rewriteCondition(c, rewriteExpr));
                                state.EntryCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                                state.ExitCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                                state.WhileCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                                if ((int)machine.Key == tearsMachine)
                                {
                                    // The ESDLang translation of the base game item checks are:
                                    //
                                    // AddTalkListData(1, 14000100, -1)
                                    // c1_19 (41 a1, 82 e4 9f d5 00 a1, 3f a1)
                                    //
                                    // AddTalkListDataIf(ComparePlayerInventoryNumber(3, 2503, 2, 0, 0) == 1, 2, 14000101, -1)
                                    // c5_19 (6f 43 82 c7 09 00 00 42 40 40 89 41 95 a1, 42 a1, 82 e5 9f d5 00 a1, 3f a1)
                                    //
                                    // AddTalkListDataIf(ComparePlayerInventoryNumber(3, 9091, 2, 0, 0) == 1, 3, 14000102, -1)
                                    // c5_19 (6f 43 82 83 23 00 00 42 40 40 89 41 95 a1, 43 a1, 82 e6 9f d5 00 a1, 3f a1)

                                    byte[] tearsHave = new byte[] { 0x6f, 0x43, 0x82, 0x28, 0x23, 0x00, 0x00, 0x42, 0x40, 0x40, 0x89, 0x41, 0x95, 0xa1 };
                                    byte[] tearsLack = new byte[] { 0x6f, 0x43, 0x82, 0x28, 0x23, 0x00, 0x00, 0x42, 0x40, 0x40, 0x89, 0x40, 0x95, 0xa1 };
                                    bool madeEdit = false;
                                    foreach (ESD.CommandCall cmd in state.EntryCommands)
                                    {
                                        // AddTalkListData
                                        if (cmd.CommandID != 19) continue;
                                        // A bit hacky, but just do this inline. Change every condition to also depend on dragon tears, plus add a default one.
                                        if (debugEsd) Console.WriteLine($"tearsA {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                        if (cmd.CommandBank == 1)
                                        {
                                            cmd.CommandBank = 5;
                                            cmd.Arguments.Insert(0, tearsHave);
                                        }
                                        else
                                        {
                                            List<byte> cond = cmd.Arguments[0].Where(b => b != 0xa1).ToList();
                                            cond.AddRange(tearsHave.Where(b => b != 0xa1));
                                            cond.Add(0x98);  // &&
                                            cond.Add(0xa1);  // end
                                            cmd.Arguments[0] = cond.ToArray();
                                        }
                                        if (debugEsd) Console.WriteLine($"tearsB {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                        madeEdit = true;
                                    }
                                    if (madeEdit)
                                    {
                                        // Use dialogue id 14025002, "Do nothing"
                                        ESD.CommandCall cmd = new ESD.CommandCall(5, 19, tearsLack, new byte[] { 0x48, 0xa1 }, new byte[] { 0x82, 0x2a, 0x01, 0xd6, 0x00, 0xa1 }, new byte[] { 0x3f, 0xa1 });
                                        if (debugEsd) Console.WriteLine($"add {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                        state.EntryCommands.Add(cmd);
                                    }
                                }
                            }
                        }
                    }
                }

                Dictionary<string, EMEVD> emevds = game.Emevds;
                Dictionary<int, EventSpec> templates = eventConfig.ItemEvents.ToDictionary(e => e.ID, e => e);

                HashSet<ItemTemplate> completedTemplates = new HashSet<ItemTemplate>();
                foreach (KeyValuePair<string, EMEVD> entry in emevds)
                {
                    HashSet<long> ids = new HashSet<long>();
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        if (ids.Contains(e.ID)) Console.WriteLine($"Duplicate!!! {e.ID} in {entry.Key}");
                        ids.Add(e.ID);
                    }
                    Dictionary<int, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (int)e.ID, e => e);
                    foreach (EMEVD.Event e in entry.Value.Events)
                    {
                        OldParams initOld = OldParams.Preprocess(e);
                        for (int i = 0; i < e.Instructions.Count; i++)
                        {
                            Instr init = events.Parse(e.Instructions[i]);
                            if (!init.Init) continue;
                            int callee = init.Callee;
                            if (!templates.TryGetValue(callee, out EventSpec ev)) continue;
                            if (ev.ItemTemplate.Count == 0) throw new Exception($"event {callee} has no templates");
                            // Types: item, loc, any, remove
                            if (ev.ItemTemplate[0].Type == "remove")
                            {
                                // Remove action by removing initialization, for now. Can garbage collect later if desired.
                                e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                                continue;
                            }
                            // Source flag and event to edit
                            List<(int, EMEVD.Event, ItemTemplate)> eventCopies = new List<(int, EMEVD.Event, ItemTemplate)>();
                            foreach (ItemTemplate t in ev.ItemTemplate)
                            {
                                if (t.Type != "loc")
                                {
                                    continue;
                                }
                                int argFlag = 0;
                                int flag;
                                if (events.ParseArgSpec(t.EventFlag, out int pos))
                                {
                                    argFlag = (int)init.Args[init.Offset + pos];
                                    if (argFlag == 0) continue;
                                    flag = argFlag;
                                }
                                else if (int.TryParse(t.EventFlag, out flag))
                                {
                                    // Use parsed value
                                }
                                else throw new Exception($"Internal error: Could not extract item flag {t.EventFlag} from {callee}");
                                if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                                {
                                    eventCopies.Add((flag, theEvent, t));
                                }
                                else if (argFlag != 0)
                                {
                                    eventCopies.Add((flag, null, t));
                                }
                                else
                                {
                                    Console.WriteLine($"{t.Type} - {t.EventFlag} - {flag} - {argFlag}");
                                    throw new Exception($"Initialized event {callee} but absent from this file and not specified in args");
                                }
                            }
                            // Do in-place pass
                            foreach (ItemTemplate t in ev.ItemTemplate)
                            {
                                if (t.Type != "loc")
                                {
                                    if (t.IsDefault())
                                    {
                                        // This is fine, nothing to do if not chr
                                    }
                                    else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                                    {
                                        if (completedTemplates.Contains(t)) continue;
                                        completedTemplates.Add(t);
                                        eventCopies.Add((0, theEvent, t));
                                    }
                                    else if (t.Type == "carp")
                                    {
                                        eventCopies.Add((0, null, t));
                                    }
                                    else throw new Exception($"Can't find event {callee} with {t.Type} template");
                                }
                            }
                            foreach (var copy in eventCopies)
                            {
                                (int flag, EMEVD.Event e2, ItemTemplate t) = copy;
                                // Main entities to replace
                                Dictionary<int, int> reloc = new Dictionary<int, int>();
                                if (flag != 0)
                                {
                                    if (rewrittenFlags.TryGetValue(flag, out int newFlag) && flag != newFlag)
                                    {
                                        reloc[flag] = newFlag;
                                    }
                                    // TODO: Item lot replacement
                                }
                                if (t.Type == "carp")
                                {
                                    if (!events.ParseArgSpec(t.Entity, out int entityPos)) throw new Exception($"{callee}");
                                    if (!events.ParseArgSpec(t.ItemLot, out int lotPos)) throw new Exception($"{callee}");
                                    if (data.NewEntityLots.TryGetValue((int)init[init.Offset + entityPos], out int newLot))
                                    {
                                        reloc[(int)init[init.Offset + lotPos]] = newLot;
                                    }
                                }
                                // Add all edits
                                EventEdits edits = new EventEdits();
                                if (t.Remove != null)
                                {
                                    foreach (string remove in Regex.Split(t.Remove, @"\s*;\s*"))
                                    {
                                        events.RemoveMacro(edits, remove);
                                    }
                                }
                                if (t.Replace != null)
                                {
                                    foreach (string replace in Regex.Split(t.Replace, @"\s*;\s*"))
                                    {
                                        events.ReplaceMacro(edits, replace);
                                    }
                                }
                                // Also edit conditional flags present in both the event and the target
                                if (flag > 0)
                                {
                                    // TODO: rewrite flags to another value
                                }
                                if (t.RemoveArg != null)
                                {
                                    if (!events.ParseArgSpec(t.RemoveArg, out int removePos)) throw new Exception($"{callee} {t.RemoveArg}");
                                    init[init.Offset + removePos] = 0;
                                }

                                if (e2 != null)
                                {
                                    // TODO: check that all remove/replaces has been activated
                                    OldParams pre = OldParams.Preprocess(e2);
                                    // Also do startcmds here? Add any additional commands before any other processing
                                    if (t.Add != null)
                                    {
                                        events.AddMacro(edits, t.Add);
                                    }
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j]);
                                        // Randomized events shouldn't have initializations, although we could probably also ignore them
                                        if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e2.ID}");
                                        // We are either dealing with a copy of the event or the original one. So all edits are in-place
                                        // Remove/replace cases
                                        edits.ApplyEdits(instr, j);
                                        instr.Save();
                                        e2.Instructions[j] = instr.Val;
                                    }
                                    events.ApplyAdds(edits, e2);
                                    pre.Postprocess();
                                }
                                if (edits.PendingEdits.Count != 0)
                                {
                                    throw new Exception($"{callee} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                                }
                                // Also add regions and multichr
                                // How to handle startcmd? Answer: maybe don't...
                                if (reloc.Count > 0)
                                {
                                    events.RewriteInts(init, reloc);
                                    init.Save();
                                    if (e2 != null)
                                    {
                                        for (int j = 0; j < e2.Instructions.Count; j++)
                                        {
                                            Instr instr = events.Parse(e2.Instructions[j]);
                                            if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                            events.RewriteInts(instr, reloc);
                                            instr.Save();
                                        }
                                    }
                                }
                            }
                        }
                        initOld.Postprocess();
                    }
                    if (entry.Key == "common")
                    {
                        // Copy event from Ashina Outskirts for getting grapple, to show green circle
                        EMEVD.Event grappleEvent = emevds["m11_00_00_00"].Events.Find(e => e.ID == 11100620);
                        if (grappleEvent != null)
                        {
                            grappleEvent = events.CopyEvent(grappleEvent, 929);
                            entry.Value.Events.Add(grappleEvent);
                            entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, 929, 0 }));
                        }

                        // Add custom event and item lot for granting display-only memories
                        // Use unused lot 3440 and unused event flag range 930-950ish
                        ItemKey memory = new ItemKey(ItemType.GOOD, 5400);
                        int memoryLot = 3440;  // unused lot
                        Dictionary<string, object> memCells = ShopToItemLot(ShopCellsForItem(memory), memory, random);
                        memCells["getItemFlagId"] = -1;
                        AddLot(memoryLot, memCells, itemRarity);

                        EMEVD.Event memEv = new EMEVD.Event(930, EMEVD.Event.RestBehaviorType.Default);
                        memEv.Instructions.Add(new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, 0 }));  // End if self event flag + slot
                        memEv.Instructions.Add(new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)0, (byte)0, 0 }));  // If flag off
                        memEv.Instructions.Add(new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)1, (byte)0, 0 }));  // If flag on
                        memEv.Instructions.Add(new EMEVD.Instruction(2003, 4, new List<object> { memoryLot }));  // Grant item lot
                        memEv.Parameters.Add(new EMEVD.Parameter(1, 4, 0, 4));
                        memEv.Parameters.Add(new EMEVD.Parameter(2, 4, 0, 4));
                        entry.Value.Events.Add(memEv);
                        
                        int slot = 0;
                        foreach (KeyValuePair<int, int> mem in memoryFlags)
                        {
                            entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { slot++, (uint)930, (uint)mem.Value }));
                        }

                        // Mibu Breathing Technique item in shops should also grant the skill
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 6, (uint)460, (uint)11100621, (uint)2420, (uint)610 }));

                        // Add permanent shop placement flags
                        // Don't use this mechanism for the time being, so NG+ works better
                        slot = 0;
                        foreach (KeyValuePair<int, int> shop in shopPermanentFlags)
                        {
                            entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { slot++, (uint)750, (uint)shop.Key, (uint)shop.Value }));
                        }
                    }
                }
            }
            else
            {
                // DS3 edits
                if (dragonFlag <= 0 && !opt["norandom"])
                {
                    throw new Exception("Internal error: Path of the dragon not assigned to any location, but key items are randomized");
                }

                // Disable Firelink Shrine bonfire without Coiled Sword, with special event flag
                game.Params["ActionButtonParam"][9351]["grayoutFlag"].Value = 14005108;

                // Description for path of the dragon so it's not ?GoodsInfo?
                FMG goodsShort = game.ItemFMGs["アイテム説明"];
                FMG goodsLong = game.ItemFMGs["アイテムうんちく"];
                string dragonInfo = "A gesture of meditation channeling the eternal essence of the ancient dragons";
                goodsShort[9030] = dragonInfo;
                goodsLong[9030] = $"{dragonInfo}.\n\nThe path to ascendence can be achieved only by the most resolute of seekers. Proper utilization of this technique can grant deep inner focus.";
                // Make it appear as a key item in shops
                game.Params["EquipParamGoods"][9030]["goodsType"].Value = (byte)1;

                // Replace transpose qwc flags with soul get flags
                foreach (PARAM.Row row in shops.Rows)
                {
                    int mat = (int)row["mtrlId"].Value;
                    if (mat > 0 && randomMaterialItems.TryGetValue(mat, out ItemKey soul) && randomMaterialEventFlags.TryGetValue(soul, out int soulFlag) && soulFlag > 0)
                    {
                        row["qwcID"].Value = soulFlag;
                    }
                }

                Dictionary<string, EMEVD> emevds = game.Emevds;

                // Do this all manually for the moment, rather than from config
                // Can revisit this later if it needs to scale up
                List<string> toEdit = new List<string> { "common", "common_func", "m30_00_00_00", "m30_01_00_00", "m31_00_00_00", "m40_00_00_00" };
                foreach (KeyValuePair<string, EMEVD> entry in emevds)
                {
                    if (!toEdit.Contains(entry.Key)) continue;
                    string map = entry.Key;
                    EMEVD emevd = entry.Value;
                    foreach (EMEVD.Event ev in emevd.Events)
                    {
                        EventEdits edits = null;
                        if (ev.ID == 0 && map == "m30_00_00_00" && dragonFlag > 0)
                        {
                            // Remove visual sfx for Path of the Dragon pickup
                            ev.Instructions.RemoveAll(i =>
                            {
                                Instr instr = events.Parse(i);
                                return instr.Init && instr.Callee == 13000901;
                            });
                        }
                        else if (ev.ID == 0 && map == "m30_01_00_00")
                        {
                            // Fix Lothric Castle Crystal Lizard so one doesn't despawn when the other one gets killed (use new event flag)
                            foreach (EMEVD.Instruction i in ev.Instructions)
                            {
                                Instr instr = events.Parse(i);
                                if (instr.Init && instr.Callee == 20005341 && (int)instr.Args[instr.Offset + 1] == 3010311)
                                {
                                    instr[instr.Offset] = 13010594;
                                    instr.Save();
                                }
                            }
                        }
                        else if (ev.ID == 0 && map == "m31_00_00_00")
                        {
                            // Remove Undead Settlement birch tree, which is a duplicate pickup which makes one unavailable
                            ev.Instructions.RemoveAll(i =>
                            {
                                Instr instr = events.Parse(i);
                                return instr.Init && instr.Callee == 20005525 && (int)instr.Args[instr.Offset] == 53100660;
                            });
                        }
                        else if (ev.ID == 710)
                        {
                            // Grand Archives Key softlock fix
                            edits = new EventEdits();
                            events.RemoveMacro(edits, "EndIfConditionGroupStateUncompiled(EventEndType.End, PASS, AND_15)");
                        }
                        else if (ev.ID == 20005523)
                        {
                            if (opt["ngplusrings"])
                            {
                                edits = new EventEdits();
                                events.AddMacro(edits, EditType.AddBefore, "GOTO Unconditionally (0)");
                            }
                        }
                        else if (ev.ID == 13905870)
                        {
                            // Prevent Storm Ruler infinite shiny from appearing, since it's randomized elsewhere
                            edits = new EventEdits();
                            events.AddMacro(edits, EditType.AddAfter, "EndUnconditionally(EventEndType.End)", "SetObjectTreasureState");
                        }
                        if (edits != null)
                        {
                            events.ApplyAllEdits(ev, edits);
                            if (edits.PendingEdits.Count != 0)
                            {
                                throw new Exception($"{ev.ID} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                            }
                        }
                    }
                    // These should probably be in a config, although some of them would need to take args
                    void addNewEvent(int id, IEnumerable<string> instrs, EMEVD.Event.RestBehaviorType rest = EMEVD.Event.RestBehaviorType.Default)
                    {
                        EMEVD.Event ev = new EMEVD.Event(id, rest);
                        ev.Instructions.AddRange(instrs.Select(t => events.ParseAdd(t)));
                        emevd.Events.Add(ev);
                        emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)id, (uint)0 }));
                    }
                    if (map == "common")
                    {
                        // Hacky Greirat Lothric Castle softlock fix
                        // If you don't have Grand Archives key yet, mark him as having talked about looting Lothric
                        // (74000308) so the actual looting flag (74000309) isn't touched by ESD.
                        addNewEvent(13000905, new string[]
                        {
                            "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventIDSlotNumber, 0)",
                            "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 74000309)",
                            "SetEventFlag(74000308, ON)",
                            "IfPlayerHasdoesntHaveItem(MAIN, ItemType.Goods, 2014, OwnershipState.Owns)",
                            "SetEventFlag(74000308, OFF)",
                        });
                        // Make Firelink Shrine greyed out by default, without having the Coiled Sword, in combination with param change above
                        // This doesn't always work just on its own, so there is a backup edit above.
                        addNewEvent(14005107, new string[]
                        {
                            "Set Event Flag (14005108,1)",
                            "IF Player Has/Doesn't Have Item (0,3,2137,1)",
                            "Set Event Flag (14005108,0)",
                        }, EMEVD.Event.RestBehaviorType.Restart);
                        if (dragonFlag > 0)
                        {
                            // Do the Path of the Dragon swap
                            // We can't just use the item all of the time, since it would appear as a double drop.
                            addNewEvent(13000904, new string[]
                            {
                                "END IF Event Flag (0,1,0,6079)",
                                $"IF Event Flag (0,1,0,{dragonFlag})",
                                "Remove Item From Player (3,9030,1)",
                                "Award Gesture Item (29,3,9030)",
                                "Set Event Flag (6079,1)",
                            });
                        }
                    }
                }
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
        private static readonly Regex phraseRe = new Regex(@"\s*;\s*");

        private PriceCategory GetSekiroPriceCategory(ItemKey key)
        {
            return data.Data[key].Unique && !game.Name(key).Contains("Jizo")
                ? PriceCategory.UNIQUE_GOOD : PriceCategory.REGULAR_GOOD;
        }

        private PriceCategory GetPriceCategory(ItemKey key)
        {
            // Effectively don't use transpose category - instead use rules for base category.
            // if (isTranspose) return PriceCategory.TRANSPOSE;
            if (key.Type != ItemType.GOOD)
            {
                if (key.Type == ItemType.WEAPON && key.ID >= 400000 && key.ID < 500000)
                {
                    return PriceCategory.ARROWS;
                }
                return (PriceCategory)key.Type;
            }
            if (key.ID >= 1200000) return PriceCategory.SPELLS;
            if (key.ID >= 1000 & key.ID <= 1030) return PriceCategory.UPGRADE;
            if (!isItemFiniteCache.ContainsKey(key))
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
                isItemFiniteCache[key] = !isInfinite;
            }
            return isItemFiniteCache[key] ? PriceCategory.FINITE_GOOD : PriceCategory.INFINITE_GOOD;
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
                if (permutation.ItemLateness.ContainsKey(item) && item.Type == ItemType.GOOD)
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
                PARAM.Row row = game.Item(item);
                // Upgrade materials roughly same. Unique ones on sale because of how many are moved to shops usually.
                if (cat == PriceCategory.UPGRADE)
                {
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
        }

        private void AddLot(int baseLot, Dictionary<string, object> cells, Dictionary<int, byte> itemRarity, bool overwrite = false)
        {
            PARAM itemLots = game.Param("ItemLotParam");
            int targetLot = baseLot;
            PARAM.Row row = null;
            if (overwrite)
            {
                row = itemLots[targetLot];
            }
            if (row == null)
            {
                while (!overwrite && itemLots[targetLot] != null)
                {
                    targetLot++;
                }
                row = game.AddRow("ItemLotParam", targetLot);
            }
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
            PARAM shops = game.Param("ShopLineupParam");
            PARAM.Row row = shops[targetShop];
            foreach (KeyValuePair<string, object> cell in cells)
            {
                if (cell.Key != "qwcID" && cell.Key != "mtrlId")  // This is right... right?
                {
                    row[cell.Key].Value = cell.Value;
                }
            }
        }

        public Dictionary<int, float> GetDropChances(ItemKey key, ItemLocation itemLoc)
        {
            Dictionary<int, float> chances = new Dictionary<int, float>();
            foreach (LocationKey loc in itemLoc.Keys.Where(k => k.Type == LocationType.LOT))
            {
                float chance = chances.TryGetValue(loc.Quantity, out float c) ? c : 1;
                chances[loc.Quantity] = Math.Min(chance, loc.Chance);
            }
            // From the way the rest of the randomizer is balanced, exclude DS3 upgrade materials from being too good
            if (key.Type == ItemType.GOOD && key.ID >= 1000 && key.ID <= 1030)
            {
                float chanceMult = 0.2f / chances.Values.Sum();
                if (chanceMult < 1)
                {
                    chances = chances.ToDictionary(e => e.Key, e => e.Value * chanceMult);
                }
            }
            return chances;
        }

        private Dictionary<string, object> ProcessModelLot(Dictionary<string, object> lotCells, ItemKey key, Dictionary<int, float> sourceChances, bool print)
        {
            lotCells = new Dictionary<string, object>(lotCells);
            // Clear existing items out
            for (int i = 1; i <= 8; i++)
            {
                lotCells[$"ItemLotId{i}"] = 0;
                lotCells[$"LotItemCategory0{i}"] = 0xFFFFFFFF;
                lotCells[$"LotItemBasePoint0{i}"] = (short)0;
                SetItemLotCount(lotCells, i, 0);
            }
            // Disable resource drops in Sekiro as well
            lotCells["LotItemNum1"] = (byte)0;
            SetItemLotChances(lotCells, key, sourceChances, print);
            return lotCells;
        }

        private Dictionary<string, object> ShopCellsForItem(ItemKey item)
        {
            return new Dictionary<string, object>()
            {
                { "EquipId", item.ID },
                { "equipType", (byte)item.Type },
                { "sellQuantity", (short)1 }
            };
        }

        private void SetItemLotCount(Dictionary<string, object> cells, int i, int quantity)
        {
            if (game.Sekiro)
            {
                cells[$"NewLotItemNum{i}"] = (ushort)quantity;
            }
            else
            {
                cells[$"LotItemNum{i}"] = (byte)quantity;
            }
        }

        private void SetItemLotChances(Dictionary<string, object> cells, ItemKey key, Dictionary<int, float> quants, bool print)
        {
            int drop = 0;
            int i = 1;
            foreach (KeyValuePair<int, float> quant in quants)
            {
                cells[$"ItemLotId{i}"] = key.ID;
                cells[$"LotItemCategory0{i}"] = lotValues[key.Type];
                SetItemLotCount(cells, i, quant.Key);
                int points = (int)Math.Round(1000 * quant.Value);
                cells[$"LotItemBasePoint0{i}"] = (short)points;
                if (print) Console.WriteLine($"  Drop chance for {quant.Key}: {points / 10.0}%");
                drop += points;
                i++;
                if (i >= 8) break;
            }
            cells[$"LotItemCategory0{i}"] = 0xFFFFFFFF;
            cells[$"LotItemBasePoint0{i}"] = (short)Math.Max(0, 1000 - drop);
        }

        private Dictionary<string, object> ShopToItemLot(Dictionary<string, object> shopCells, ItemKey key, Random random)
        {
            Dictionary<string, object> lotCells = new Dictionary<string, object>();
            lotCells["ItemLotId1"] = (int)shopCells["EquipId"];
            // Make default quantity 0, and also disable resource drop flag in Sekiro
            lotCells["LotItemNum1"] = (byte)0;
            lotCells["LotItemCategory01"] = lotValues[(ItemType)(byte)shopCells["equipType"]];
            int quantity = (short)shopCells["sellQuantity"];
            if (quantity > 0)
            {
                // Ring of sacrifice multi-drops do not work
                if (key.Equals(new ItemKey(ItemType.RING, 20210)) && quantity > 1)
                {
                    quantity = 1;
                }
                SetItemLotCount(lotCells, 1, quantity);
                lotCells["LotItemBasePoint01"] = (short)100;
            }
            else
            {
                PriceCategory cat = GetPriceCategory(key);
                Dictionary<int, float> chances;
                if (dropChances.TryGetValue(cat, out List<Dictionary<int, float>> allChances))
                {
                    chances = Choice(random, allChances);
                }
                else
                {
                    chances = DEFAULT_CHANCES;
                }
                SetItemLotChances(lotCells, key, chances, true);
            }
            lotCells["cumulateNumFlagId"] = -1;
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
                lotKey = new ItemKey(game.LotItemTypes[(uint)lotCells[$"LotItemCategory0{i}"]], (int)lotCells[$"ItemLotId{i}"]);
                if (!lotKey.Equals(itemKey))
                {
                    lotKey = null;
                    continue;
                }
                if (game.Sekiro && lotKey.Type == ItemType.WEAPON && lotKey.ID == 680000)
                {
                    shopCells["EquipId"] = 2420;
                    shopCells["equipType"] = (byte)3;
                }
                else
                {
                    shopCells["EquipId"] = lotKey.ID;
                    shopCells["equipType"] = (byte)lotKey.Type;
                }
                int basePoints = (short)lotCells[$"LotItemBasePoint0{i}"];
                if (basePoints == totalPoints)
                {
                    // TODO: If no event id or material id, this won't do much. But that is intended?
                    shopCells["sellQuantity"] = (short)(game.Sekiro ? (ushort)lotCells[$"NewLotItemNum{i}"] : (byte)lotCells[$"LotItemNum{i}"]);
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
            if (game.Sekiro)
            {
                // Low-level substitution done for Mibu Breathing Technique. Better to do it at low level, since item key is used for other things at high level.
                if (key.Type == ItemType.WEAPON && key.ID == 680000)
                {
                    key = new ItemKey(ItemType.GOOD, 2420);
                }
                if (key.Type != ItemType.GOOD) throw new Exception($"Trying to sell non-item {key} in Sekiro, todo need to handle this");
                PARAM.Row itemRow = game.Item(key);
                if ((int)itemRow["shopId"].Value == -1)
                {
                    itemRow["shopId"].Value = 100;
                }
            }
            else
            {
                PARAM.Row itemRow = game.Item(key);
                PARAM.Cell costCell = itemRow[itemValueCells[(int)key.Type]];
                // Items must have this field to be sold in shops
                if ((int)costCell.Value <= 0)
                {
                    costCell.Value = 1000;
                }
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
