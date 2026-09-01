using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.ItemLocEditor;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.LocationData.Location;
using static RandomizerCommon.PermutationWriter;

namespace RandomizerCommon
{
    public class ItemLocEditor
    {
        public static readonly Dictionary<ItemType, string> DS3SellFields = new()
        {
            [ItemType.Weapon] = "saleValue",
            [ItemType.Protector] = "saleValue",
            [ItemType.Accessory] = "saleValue",
            [ItemType.Goods] = "shopId",
        };

        private readonly GameData game;

        public ItemLocEditor(GameData game)
        {
            this.game = game;
        }

        public void AddLot(string paramName, int baseLot, LotCells cells, Dictionary<int, byte> itemRarity = null, bool overwrite = false)
        {
            PARAM itemLots = game.Param(paramName);
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
                row = game.AddRow(paramName, targetLot);
            }
            SetLot(row, cells);
            // Console.WriteLine($"Setting lot {baseLot}: {string.Join(", ", cells.Cells)}");
            if (itemRarity != null && itemRarity.ContainsKey(baseLot))
            {
                row["lotItem_Rarity"].Value = itemRarity[baseLot];
            }
            // For Leda's Sword drop. This could also be treated similar to itemRarity
            if (baseLot == 10420 && game.EldenRing)
            {
                row["canExecByHostileGhost"].Value = (byte)1;
            }
        }

        public void SetLot(PARAM.Row row, LotCells cells)
        {
            foreach (KeyValuePair<string, object> cell in cells.Cells)
            {
                if (cell.Key == "LotItemRarity")
                {
                    continue;
                }
                row[cell.Key].Value = cell.Value;
            }
        }

        public void SetShop(PARAM.Row row, ShopCells cells)
        {
            // This filtering probably not necessary
            foreach (KeyValuePair<string, object> cell in cells.Cells)
            {
                if (cell.Key == "eventFlag_forRelease" || cell.Key == "mtrlId"
                    || cell.Key == "costType") continue;
                row[cell.Key].Value = cell.Value;
            }
            if (game.EldenRing)
            {
                if (!cells.Cells.ContainsKey("nameMsgId"))
                {
                    row["nameMsgId"].Value = -1;
                }
                if (!cells.Cells.ContainsKey("iconId"))
                {
                    row["iconId"].Value = -1;
                }
            }
        }

        public void SetShop(Location target, ShopCells cells)
        {
            PARAM.Row row = game.Params[target.ParamName][target.ID];
            SetShop(row, cells);
        }

        public Dictionary<int, float> GetDropChances(ItemKey key, ItemLocation itemLoc)
        {
            Dictionary<int, float> chances = new Dictionary<int, float>();
            foreach (Location loc in itemLoc.Keys.Where(k => k.Type == LocationType.Lot))
            {
                float chance = chances.TryGetValue(loc.Quantity, out float c) ? c : 1;
                chances[loc.Quantity] = Math.Min(chance, loc.Chance);
            }
            // DS3 does not have easily farmable upgrade materials, so add a ceiling based on static placement of those materials works
            if (game.DS3 && key.Type == ItemType.Goods && key.ID >= 1000 && key.ID <= 1030)
            {
                float chanceMult = 0.2f / chances.Values.Sum();
                if (chanceMult < 1)
                {
                    chances = chances.ToDictionary(e => e.Key, e => e.Value * chanceMult);
                }
            }
            return chances;
        }

        public LotCells ProcessModelLot(LotCells lotCells, ItemKey key, Dictionary<int, float> sourceChances, bool print)
        {
            lotCells = lotCells.DeepCopy();
            // Clear existing items out
            for (int i = 1; i <= 8; i++)
            {
                lotCells[i] = null;
                lotCells.SetPoints(i, 0);
                lotCells.SetQuantity(i, 0);
            }
            // Disable resource drops in Sekiro as well
            if (game.Sekiro)
            {
                lotCells.Cells["isProgressOffset"] = (byte)0;
            }
            SetItemLotChances(lotCells, key, sourceChances, print);
            return lotCells;
        }

        public LotCells LotCellsForItem(ItemKey item)
        {
            LotCells cells = new LotCells
            {
                Game = game,
                Cells = new(),
            };
            cells[1] = item;
            cells.SetQuantity(1, 1);
            cells.SetPoints(1, 100);
            return cells;
        }

        public ShopCells ShopCellsForItem(ItemKey item)
        {
            ShopCells cells = new ShopCells
            {
                Game = game,
                Cells = new(),
            };
            // TODO: Transform not needed in Elden Ring anymore, test removing it
            cells.Item = game.FromCustomWeapon(item);
            cells.Quantity = 1;
            return cells;
        }

        private void SetItemLotChances(LotCells cells, ItemKey key, Dictionary<int, float> quants, bool print)
        {
            int drop = 0;
            int i = 1;
            foreach (KeyValuePair<int, float> quant in quants)
            {
                cells[i] = key;
                cells.SetQuantity(i, quant.Key);
                int points = (int)Math.Round(1000 * quant.Value);
                cells.SetPoints(i, points);
                if (print) Console.WriteLine($"  Drop chance for {quant.Key}: {points / 10.0}%");
                drop += points;
                i++;
                if (i >= 8) break;
            }
            cells[i] = null;
            cells.SetPoints(i, (short)Math.Max(0, 1000 - drop));
        }

        public LotCells ShopToItemLot(ShopCells shopCells, ItemKey key, Func<ItemKey, Dictionary<int, float>> getChances, bool print)
        {
            LotCells lotCells = new LotCells { Game = game, Cells = new() };
            // Disable resource drop flag in Sekiro
            if (game.Sekiro)
            {
                lotCells.Cells["isProgressOffset"] = (byte)0;
            }
            if (!game.EldenRing)
            {
                lotCells.Cells["cumulateNumFlagId"] = -1;
            }

            lotCells[1] = shopCells.Item;
            lotCells.SetQuantity(1, 0);
            int quantity = shopCells.Quantity;
            if (quantity > 0)
            {
                // Ring of sacrifice multi-drops do not work in DS3
                // ...make this shop-only instead?
                if (key.Equals(new ItemKey(ItemType.Accessory, 20210)) && quantity > 1)
                {
                    quantity = 1;
                }
                lotCells.SetQuantity(1, quantity);
                lotCells.SetPoints(1, 100);
            }
            else
            {
                /*PriceCategory cat = GetPriceCategory(key);
                Dictionary<int, float> chances;
                if (dropChances.TryGetValue(cat, out List<Dictionary<int, float>> allChances))
                {
                    chances = Choice(random, allChances);
                }
                else
                {
                    chances = defaultChances;
                }*/
                Dictionary<int, float> chances = getChances(key);
                SetItemLotChances(lotCells, key, chances, print);
            }
            return lotCells;
        }

        public ShopCells ItemLotToShop(LotCells lotCells, ItemKey itemKey)
        {
            ShopCells shopCells = new ShopCells { Game = game, Cells = new Dictionary<string, object>() };
            // For an item like this, assume QWC id stays the same
            ItemKey lotKey = null;
            int totalPoints = 0;
            for (int i = 1; i <= 8; i++)
            {
                totalPoints += lotCells.GetPoints(i);
            }
            for (int i = 1; i <= 8; i++)
            {
                lotKey = lotCells[i];
                if (!itemKey.Equals(lotKey))
                {
                    lotKey = null;
                    continue;
                }
                if (game.Sekiro && lotKey.Type == ItemType.Weapon && lotKey.ID == 680000)
                {
                    // Selling Mibu Breathing Technique
                    shopCells.Item = new ItemKey(ItemType.Goods, 2420);
                }
                else if (false && game.EldenRing && lotKey.Type == ItemType.Custom)
                {
                    // If this still returns a custom id, it'll just be invisible
                    // TODO: In 1.12 this now works?
                    shopCells.Item = game.FromCustomWeapon(lotKey);
                }
                else
                {
                    shopCells.Item = lotKey;
                }
                int basePoints = lotCells.GetPoints(i);
                if (basePoints == totalPoints)
                {
                    // TODO: If no event id or material id, this won't do much. But that is intended?
                    shopCells.Quantity = lotCells.GetQuantity(i);
                }
                else
                {
                    shopCells.Quantity = -1;
                }
                break;
            }
            if (lotKey == null)
            {
                throw new Exception($"Internal error: Invalid source location for {itemKey} from {string.Join(", ", lotCells.Cells.Select(e => e.Key + " = " + e.Value))}");
            }
            MakeSellable(lotKey);
            return shopCells;
        }

        public void MakeSellable(ItemKey key)
        {
            if (game.Sekiro)
            {
                // Low-level substitution done for Mibu Breathing Technique. Better to do it at low level, since item key is used for other things at high level.
                if (key.Type == ItemType.Weapon && key.ID == 680000)
                {
                    key = new ItemKey(ItemType.Goods, 2420);
                }
                if (key.Type != ItemType.Goods) throw new Exception($"Trying to sell non-item {key} in Sekiro, todo need to handle this");
                PARAM.Row itemRow = game.Item(key);
                if ((int)itemRow["sellValue"].Value == -1)
                {
                    itemRow["sellValue"].Value = 100;
                }
            }
            else if (game.DS3)
            {
                PARAM.Row itemRow = game.Item(key);
                PARAM.Cell costCell = itemRow[DS3SellFields[key.Type]];
                // Items must have this field to be sold in shops
                if ((int)costCell.Value <= 0)
                {
                    costCell.Value = 1000;
                }
            }
            // Elden Ring just needs eligible price
        }

        public abstract class ItemRow<T> where T : ItemRow<T>, new()
        {
            public GameData Game { get; set; }
            public Dictionary<string, object> Cells { get; set; }
            public T DeepCopy()
            {
                return new T { Game = Game, Cells = new Dictionary<string, object>(Cells) };
            }
        }

        // Some of this could be moved into class-specific item editors. There's also overlap with the location data scrapers.
        // The solution to both of these is maybe generated param row interfaces.
        public class ShopCells : ItemRow<ShopCells>
        {
            public ItemKey Item
            {
                get
                {
                    return new ItemKey(Game.ShopItemTypes[(byte)Cells["equipType"]], (int)Cells["equipId"]);
                }
                set
                {
                    Cells["equipId"] = value.ID;
                    Cells["equipType"] = (byte)Game.ShopTypeItems[value.Type];
                }
            }
            public int EventFlag
            {
                get => Game.Sekiro ? (int)Cells["eventFlag"] : (Game.EldenRing ? (int)(uint)Cells["eventFlag_forStock"] : (int)Cells["eventFlag_forStock"]);
                set
                {
                    if (Game.EldenRing)
                    {
                        Cells["eventFlag_forStock"] = value > 0 ? (uint)value : 0u;
                    }
                    else if (Game.DS3)
                    {
                        Cells["eventFlag_forStock"] = value > 0 ? value : -1;
                    }
                    else
                    {
                        Cells["eventFlag"] = value > 0 ? value : -1;
                    }
                }
            }
            public int Quantity
            {
                get => (short)Cells["sellQuantity"];
                set
                {
                    Cells["sellQuantity"] = (short)value;
                }
            }
            public int Value
            {
                get => (int)Cells["value"];
                set
                {
                    Cells["value"] = value;
                }
            }
        }

        public class LotCells : ItemRow<LotCells>
        {
            public int EventFlag
            {
                get => Game.EldenRing ? (int)(uint)Cells["getItemFlagId"] : (int)Cells["getItemFlagId"];
                set
                {
                    if (Game.EldenRing)
                    {
                        Cells["getItemFlagId"] = value > 0 ? (uint)value : 0u;
                    }
                    else
                    {
                        Cells["getItemFlagId"] = value > 0 ? value : -1;
                    }
                }
            }
            public ItemKey this[int i]
            {
                get
                {
                    if (!Game.DS3)
                    {
                        if (!Cells.TryGetValue($"lotItemId0{i}", out object idObj)) return null;
                        int id = (int)idObj;
                        if (id == 0) return null;
                        return new ItemKey(Game.LotItemTypes[(uint)(int)Cells[$"lotItemCategory0{i}"]], id);
                    }
                    else
                    {
                        // DS3 might be incorrect here, either from Paramdex or Smithbox import
                        int id = (int)Cells[$"ItemLotId{i}"];
                        if (id == 0) return null;
                        return new ItemKey(Game.LotItemTypes[(uint)Cells[$"LotItemCategory0{i}"]], id);
                    }
                }
                set
                {
                    if (!Game.DS3)
                    {
                        Cells[$"lotItemId0{i}"] = value == null ? 0 : value.ID;
                        Cells[$"lotItemCategory0{i}"] = value == null ? 0 : (int)Game.LotValues[value.Type];
                        // TODO: Do this for other games as well? Does this work?
                        // Normally this is not set for non-random drops, but it shouldn't be incorrect to add it either.
                        Cells[$"enableLuck0{i}"] = value == null ? (ushort)0 : (ushort)1;
                    }
                    else
                    {
                        Cells[$"ItemLotId{i}"] = value == null ? 0 : value.ID;
                        Cells[$"LotItemCategory0{i}"] = value == null ? 0xFFFFFFFFu : Game.LotValues[value.Type];
                    }
                }
            }
            public int GetPoints(int i)
            {
                if (!Game.DS3)
                {
                    return (ushort)Cells[$"lotItemBasePoint0{i}"];
                }
                else
                {
                    return (short)Cells[$"LotItemBasePoint0{i}"];
                }
            }
            public void SetPoints(int i, int points)
            {
                if (!Game.DS3)
                {
                    Cells[$"lotItemBasePoint0{i}"] = (ushort)points;
                }
                else
                {
                    Cells[$"LotItemBasePoint0{i}"] = (short)points;
                }
            }
            public int GetQuantity(int i)
            {
                if (Game.EldenRing)
                {
                    return (byte)Cells[$"lotItemNum0{i}"];
                }
                else if (Game.Sekiro)
                {
                    return (ushort)Cells[$"lotItemNum0{i}"];
                }
                else
                {
                    return (byte)Cells[$"LotItemNum{i}"];
                }
            }
            public void SetQuantity(int i, int quantity)
            {
                if (Game.EldenRing)
                {
                    Cells[$"lotItemNum0{i}"] = (byte)quantity;
                }
                else if (Game.Sekiro)
                {
                    Cells[$"lotItemNum0{i}"] = (ushort)quantity;
                }
                else
                {
                    Cells[$"LotItemNum{i}"] = (byte)quantity;
                }
            }
        }
    }
}
