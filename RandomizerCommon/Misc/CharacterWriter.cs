using Org.BouncyCastle.Ocsp;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Numerics;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class CharacterWriter
    {
        private class GameCharacters
        {
            public int StartId { get; set; }
            public int GiftStartId { get; set; }
            public int GiftDescBase { get; set; }
            public Dictionary<string, EquipCategory> BaseStart { get; set; }
            public List<CharacterClass> Classes { get; set; }
            public List<StartingGift> Gifts { get; set; }
            public List<string> Stats { get; set; }
            public List<string> WeaponSlots { get; set; }
            // Items which wouldn't be considered weapons, and optional category
            public Dictionary<int, EquipCategory> WeaponCategories { get; set; }
            public Dictionary<int, EquipCategory> WeaponTypes { get; set; }
            public List<string> ArmorTypes { get; set; }
            public List<string> ArmorSlots { get; set; }
            public List<EquipCategory> MagicTypes { get; set; }
            public Dictionary<string, string> CustomWeaponTypes { get; set; }
        }

        public enum EquipCategory
        {
            Unset,
            Weapon, Shield, DoubleWeapon,
            Catalyst, Talisman, Flame,
            Sorcery, Miracle, Pyromancy,
            Bow, Arrow, Bolt,
            Head, Body, Arm, Leg,
            Accessory, SpiritAsh,
        }

        public enum GiftCategory
        {
            None,
            Accessory, SpiritAsh, Gem,
            KeyItem, Buff,
            Soul, Health, Throwable,
            Misc,
        }

        public enum ShopCat
        {
            None,
            Weapon,
            Sorcery,
            Incantation,
            AshOfWar,
            Armor,
            Talisman,
            CrystalTear,
            ReusableTool,
            SpiritAsh,
            CraftingContainers,
            Cookbooks,
        }

        [Localize]
        private static readonly Dictionary<ShopCat, Text> itemCatNames = MapText<ShopCat>("GameMenu_ShopCat", new()
        {
            [ShopCat.None] = "",
            [ShopCat.Weapon] = "Weapons",
            [ShopCat.Sorcery] = "Sorceries",
            [ShopCat.Incantation] = "Incantations",
            [ShopCat.AshOfWar] = "Ashes of War",
            [ShopCat.Armor] = "Armor",
            [ShopCat.Talisman] = "Talismans",
            [ShopCat.CrystalTear] = "Crystal tears",
            [ShopCat.ReusableTool] = "Reusable tools",
            [ShopCat.SpiritAsh] = "Spirit ashes",
            [ShopCat.CraftingContainers] = "Crafting containers",
            [ShopCat.Cookbooks] = "Cookbooks",
        });
        [Localize]
        private static readonly Text startShop = new Text("Base game shop", "GameMenu_dlcStart_shop");
        [Localize]
        private static readonly Text startEnterBase = new Text("Enter base game", "GameMenu_dlcStart_enterBase");
        [Localize]
        private static readonly Text startEnter = new Text("Enter DLC", "GameMenu_dlcStart_enter");
        [Localize]
        private static readonly Text startEnterConfirm = new Text("Forfeit all runes and enter the realm of shadow?", "GameMenu_dlcStart_enterConfirm");
        [Localize]
        private static readonly Text startEnterConfirmBase = new Text("Forfeit all runes and enter the base game?", "GameMenu_dlcStart_enterConfirmBase");
        [Localize]
        private static readonly Text startMenu = new Text("DLC Start", "GameMenu_dlcStart_menu");
        [Localize]
        private static readonly Text startDoor = new Text("Closed because DLC Start is enabled. Use the grace instead.", "GameMenu_dlcStart_door");

        private static readonly HashSet<ShopCat> flagRequiredItems = new()
        {
            ShopCat.CrystalTear, ShopCat.CraftingContainers, ShopCat.Cookbooks,
        };

        // (goodsType, sortGroupId) -> cat. Group id may be -1
        private static readonly Dictionary<(int, int), ShopCat> goodsGroupCats = new()
        {
            [(0, 60)] = ShopCat.ReusableTool,
            [(5, -1)] = ShopCat.Sorcery,
            [(7, -1)] = ShopCat.SpiritAsh,
            [(8, -1)] = ShopCat.SpiritAsh,
            [(10, -1)] = ShopCat.CrystalTear,
            [(11, -1)] = ShopCat.CraftingContainers,
            [(16, -1)] = ShopCat.Incantation,
            [(17, -1)] = ShopCat.Sorcery, // Buff
            [(18, -1)] = ShopCat.Incantation, // Buff
        };

        // Overrides for goods
        private static readonly Dictionary<int, ShopCat> goodsIdCats = new()
        {
            [3360] = ShopCat.None, // Baldachin
            [3361] = ShopCat.None, // Baldachin
        };

        private static readonly Dictionary<EquipCategory, int> careEquips = new()
        {
            [EquipCategory.Weapon] = 11, // 20,
            [EquipCategory.Shield] = 5, // 10,
            // For now, only bows are given
            [EquipCategory.Bow] = 2,
            [EquipCategory.Arrow] = 2,
            [EquipCategory.Catalyst] = 2,
            [EquipCategory.Talisman] = 2,
            [EquipCategory.Sorcery] = 7, // 10
            [EquipCategory.Miracle] = 8, // 10
            [EquipCategory.Head] = 6, // 10,
            [EquipCategory.Body] = 8, // 10,
            [EquipCategory.Arm] = 5, // 10,
            [EquipCategory.Leg] = 6, // 10,
        };
        private static readonly Dictionary<ShopCat, int> careShops = new()
        {
            // Choose this here instead of careEquips to get upgrade versions
            [ShopCat.Talisman] = 9, // 15,
            [ShopCat.CrystalTear] = 7,
            // Could do spirit ashes or crafting stuff, but upgrade situation is tricky
        };

        private static readonly List<EquipCategory> DescriptionPriority = new List<EquipCategory>
        {
            // Increasing order
            EquipCategory.Arrow, EquipCategory.Bolt,
            EquipCategory.Shield,
            EquipCategory.Bow,
            EquipCategory.Catalyst, EquipCategory.Talisman, EquipCategory.Flame,
            EquipCategory.Sorcery, EquipCategory.Miracle, EquipCategory.Pyromancy,
            EquipCategory.Weapon,
        };

        public class CharacterClass
        {
            public string Name { get; set; }
            public Dictionary<string, EquipCategory> Start { get; set; }
            public int Desc { get; set; }
        }

        private class StartingGift
        {
            public int Desc { get; set; }
            public GiftCategory Category { get; set; }
        }

        private class ArmorSet
        {
            public int[] Ids = new int[4];
            public float Weight { get; set; }
        }

        private struct StatReq
        {
            public int Str { get; set; }
            public int Dex { get; set; }
            public int Int { get; set; }
            public int Fai { get; set; }
            public int Arc { get; set; }
            public int Att { get; set; }
            public int Eligible(StatReq ch)
            {
                int[] comps = new int[] {
                    Str == 0 ? 0 : ch.Str - Str,
                    Dex == 0 ? 0 : ch.Dex - Dex,
                    Int == 0 ? 0 : ch.Int - Int,
                    Arc == 0 ? 0 : ch.Arc - Arc,
                    Fai == 0 ? 0 : ch.Fai - Fai,
                };
                int miss = comps.Where(c => c < 0).Sum();
                if (miss < 0) return miss;
                // Weapons with no requirements, slight penalty
                if (Str == 0 && Dex == 0 && Int == 0 && Fai == 0 && Arc == 0) return 4;
                return comps.Sum();
            }
            public int GetMaxStat()
            {
                return new[] { Str, Dex, Int, Arc, Fai }.Max();
            }
            public void Adjust(StatReq wep)
            {
                if (wep.Str > Str) Str = wep.Str;
                if (wep.Dex > Dex) Dex = wep.Dex;
                if (wep.Int > Int) Int = wep.Int;
                if (wep.Fai > Fai) Fai = wep.Fai;
                if (wep.Arc > Arc) Arc = wep.Arc;
            }
            public override string ToString() => $"StatReq[Str={Str}, Dex={Dex}, Int={Int}, Fai={Fai}, Arc={Arc}, Att={Att}]";
        }

        private static readonly Dictionary<string, EquipCategory> DS3BaseStart = new Dictionary<string, EquipCategory>()
        {
                { "equip_Wep_Right", EquipCategory.Weapon },
                { "equip_Wep_Left", EquipCategory.Shield },
        };
        private static readonly List<CharacterClass> DS3Classes = new List<CharacterClass>()
        {
            new CharacterClass
            {
                Name = "Knight",
                Start = new Dictionary<string, EquipCategory>(),
                Desc = 402401,
            },
            new CharacterClass
            {
                Name = "Mercenary",
                Start = new Dictionary<string, EquipCategory>(),
                Desc = 402402,
            },
            new CharacterClass
            {
                Name = "Warrior",
                Start = new Dictionary<string, EquipCategory>(),
                Desc = 402403,
            },
            new CharacterClass
            {
                Name = "Herald",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.Talisman },
                    { "equip_Spell_01", EquipCategory.Miracle },
                },
                Desc = 402404,
            },
            new CharacterClass
            {
                Name = "Thief",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.Bow },
                    // Depends on selection of bow/crossbow
                    { "equip_Arrow", EquipCategory.Arrow },
                    { "equip_Bolt", EquipCategory.Bolt },
                },
                Desc = 402405,
            },
            new CharacterClass
            {
                Name = "Assassin",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.Catalyst },
                    { "equip_Spell_01", EquipCategory.Sorcery },
                },
                Desc = 402406,
            },
            new CharacterClass
            {
                Name = "Sorcerer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Right", EquipCategory.Catalyst },
                    { "equip_Spell_01", EquipCategory.Sorcery },
                    { "equip_Spell_02", EquipCategory.Sorcery },
                },
                Desc = 402407,
            },
            new CharacterClass
            {
                Name = "Pyromancer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Left", EquipCategory.Flame },
                    { "equip_Spell_01", EquipCategory.Pyromancy },
                },
                Desc = 402408,
            },
            new CharacterClass
            {
                Name = "Cleric",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Subwep_Right", EquipCategory.Talisman },
                    { "equip_Spell_01", EquipCategory.Miracle },
                    { "equip_Spell_02", EquipCategory.Miracle },
                },
                Desc = 402409,
            },
            new CharacterClass
            {
                Name = "Deprived",
                Start = new Dictionary<string, EquipCategory>(),
                Desc = 402410,
            },
        };
        private static readonly List<StartingGift> DS3Gifts = new List<StartingGift>()
        {
            new StartingGift
            {
                // 402501 None
                Desc = 1,
                Category = GiftCategory.None,
            },
            new StartingGift
            {
                // 402502 Life Ring
                Desc = 2,
                Category = GiftCategory.Accessory,
            },
            new StartingGift
            {
                // 402503 Divine Blessing
                Desc = 3,
                Category = GiftCategory.Buff,
            },
            new StartingGift
            {
                // 402504 Hidden Blessing
                Desc = 4,
                Category = GiftCategory.Buff,
            },
            new StartingGift
            {
                // 402505 Black Firebomb
                Desc = 5,
                Category = GiftCategory.Throwable,
            },
            new StartingGift
            {
                // 402507 Fire Gem
                Desc = 7,
                Category = GiftCategory.Gem,
            },
            new StartingGift
            {
                // 402508 Sovereignless Soul
                Desc = 8,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // 402511 Rusted Gold Coin
                Desc = 11,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // 402509 Cracked Red Eye Orb
                Desc = 9,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // 402510 Young White Branch
                Desc = 10,
                Category = GiftCategory.KeyItem,
            },
        };

        private static readonly Dictionary<string, EquipCategory> EldenBaseStart = new Dictionary<string, EquipCategory>();
        private static readonly List<CharacterClass> EldenClasses = new List<CharacterClass>()
        {
            new CharacterClass
            {
                Name = "Vagabond",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Subwep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                },
                Desc = 297130,
            },
            new CharacterClass
            {
                Name = "Warrior",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.DoubleWeapon },
                    { "equip_Subwep_Left", EquipCategory.Shield },
                },
                Desc = 297131,
            },
            new CharacterClass
            {
                Name = "Hero",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                },
                Desc = 297132,
            },
            new CharacterClass
            {
                Name = "Bandit",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                    { "equip_Subwep_Left", EquipCategory.Bow },
                    { "equip_Arrow", EquipCategory.Arrow },
                },
                Desc = 297133,
            },
            new CharacterClass
            {
                Name = "Astrologer",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Catalyst },
                    { "equip_Subwep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                    { "equip_Spell_01", EquipCategory.Sorcery },
                    { "equip_Spell_02", EquipCategory.Sorcery },
                },
                Desc = 297134,
            },
            new CharacterClass
            {
                Name = "Prophet",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Talisman },
                    { "equip_Subwep_Left", EquipCategory.Shield },
                    { "equip_Spell_01", EquipCategory.Miracle },
                    { "equip_Spell_02", EquipCategory.Miracle },
                },
                Desc = 297135,
            },
            new CharacterClass
            {
                Name = "Confessor",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                    { "equip_Subwep_Left", EquipCategory.Talisman },
                    { "equip_Spell_01", EquipCategory.Miracle },
                    { "equip_Spell_02", EquipCategory.Miracle },
                },
                Desc = 297138,
            },
            new CharacterClass
            {
                Name = "Samurai",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Bow },
                    { "equip_Subwep_Left", EquipCategory.Shield },
                    { "equip_Arrow", EquipCategory.Arrow },
                    { "equip_SubArrow", EquipCategory.Arrow },
                },
                Desc = 297136,
            },
            new CharacterClass
            {
                Name = "Prisoner",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Catalyst },
                    { "equip_Subwep_Left", EquipCategory.Shield },
                    { "equip_Spell_01", EquipCategory.Sorcery },
                },
                Desc = 297137,
            },
            new CharacterClass
            {
                Name = "Wretch",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                },
                Desc = 297139,
            },
            // Always include since Tarnished Pack may be active even if not randomized
            new CharacterClass
            {
                Name = "Idus Knight",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                    { "equip_Wep_Left", EquipCategory.Shield },
                },
                Desc = 297140,
            },
            new CharacterClass
            {
                Name = "Heavy Knight",
                Start = new Dictionary<string, EquipCategory>
                {
                    { "equip_Wep_Right", EquipCategory.Weapon },
                },
                Desc = 297141,
            },
        };
        private static readonly List<StartingGift> EldenGifts = new List<StartingGift>()
        {
            new StartingGift
            {
                // None
                Desc = 0,
                Category = GiftCategory.None,
            },
            new StartingGift
            {
                // Crimson Amber Medallion
                Desc = 1,
                Category = GiftCategory.Accessory,
            },
            new StartingGift
            {
                // Lands Between Rune
                Desc = 2,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // Golden Seed
                Desc = 3,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // Fanged Imp Ashes
                Desc = 4,
                Category = GiftCategory.SpiritAsh,
            },
            new StartingGift
            {
                // Cracked Pot
                Desc = 6,
                Category = GiftCategory.Misc,
            },
            new StartingGift
            {
                // Stonesword Key
                Desc = 7,
                Category = GiftCategory.KeyItem,
            },
            new StartingGift
            {
                // Bewitching Branch
                Desc = 8,
                Category = GiftCategory.Buff,
            },
            new StartingGift
            {
                // Boiled Prawn
                Desc = 9,
                Category = GiftCategory.Buff,
            },
            new StartingGift
            {
                // Shabriri's Woe
                Desc = 10,
                Category = GiftCategory.Accessory,
            },
        };

        private readonly List<EquipCategory> ArmorCats = new List<EquipCategory> { EquipCategory.Head, EquipCategory.Body, EquipCategory.Arm, EquipCategory.Leg };

        private readonly GameData game;
        private readonly LocationData data;
        private readonly Messages messages;
        private readonly AnnotationData ann;
        private readonly GameCharacters g;
        private readonly Events events;

        // Must be set externally
        public static Func<string, Font, int> MeasureText;

        public CharacterWriter(GameData game, LocationData data, Messages messages, Events events, AnnotationData ann)
        {
            this.game = game;
            this.data = data;
            this.messages = messages;
            this.events = events;
            // Does not strictly require loaded slots, but optionally loading them is probably more trouble than it's worth
            this.ann = ann;

            // Put this somewhere which will come up regardless of selected options
            if (game.EldenRing && MeasureText == null)
            {
                throw new Exception("Internal error: Font utility not injected from UI framework");
            }

            // Fixed initialization
            g = new GameCharacters
            {
                StartId = 3000,
                WeaponSlots = new List<string> { "equip_Wep_Right", "equip_Subwep_Right", "equip_Wep_Left", "equip_Subwep_Left" },
                ArmorTypes = new List<string> { "headEquip", "bodyEquip", "armEquip", "legEquip" },
            };
            if (game.DS3)
            {
                g.Stats = new List<string> { "Vit", /* vigor */ "Wil" /* attunement */, "End", "Str", "Dex", "Mag", "Fai", "Luc", "Durability" /* vit */ };
                g.ArmorSlots = new List<string> { "equip_Helm", "equip_Armor", "equip_Gaunt", "equip_Leg" };
                g.BaseStart = DS3BaseStart;
                g.Classes = DS3Classes;
                g.WeaponCategories = new Dictionary<int, EquipCategory>
                {
                    { 8, EquipCategory.Unset },  // catalyst/talisman/flame - will be added separately
                    { 10, EquipCategory.Bow },
                    { 11, EquipCategory.Bow },
                    { 13, EquipCategory.Arrow },
                    { 14, EquipCategory.Bolt },
                };
                g.MagicTypes = new List<EquipCategory> { EquipCategory.Sorcery, EquipCategory.Miracle, EquipCategory.Pyromancy };
                g.GiftStartId = 2400;
                g.GiftDescBase = 402500;
                g.Gifts = DS3Gifts;
            }
            else if (game.EldenRing)
            {
                g.Stats = new List<string> { "Vit", /* vigor */ "Wil" /* mind */, "End", "Str", "Dex", "Mag", "Fai", "Luc" /* arcane */ };
                g.ArmorSlots = new List<string> { "equip_Helm", "equip_Armer", "equip_Gaunt", "equip_Leg" };
                g.BaseStart = EldenBaseStart;
                g.Classes = EldenClasses;
                g.WeaponCategories = new Dictionary<int, EquipCategory>
                {
                    { 8, EquipCategory.Unset },  // Also magic-doers
                    { 10, EquipCategory.Bow },
                    { 11, EquipCategory.Unset },  // Don't deal with crossbows
                    { 13, EquipCategory.Arrow },
                    { 14, EquipCategory.Unset },
                };
                Dictionary<EquipCategory, List<int>> catTypes = new()
                {
                    [EquipCategory.Shield] = new() { 65, 67, 69, 90 },
                    // Just regular bows for now
                    [EquipCategory.Bow] = new() { 50, 51 },
                    [EquipCategory.Arrow] = new() { 81 },
                    [EquipCategory.Catalyst] = new() { 57 },
                    [EquipCategory.Talisman] = new() { 61 },
                    [EquipCategory.Unset] = new()
                    {
                        // Other bows
                        53, 55, 56,
                        // Other arrows
                        83, 85, 86
                    },
                };
                g.WeaponTypes = catTypes.SelectMany(e => e.Value.Select(type => (type, e.Key))).ToDictionary(e => e.Item1, e => e.Item2);
                // Apparently pyromancy works in Elden Ring, but no vanilla usages
                g.MagicTypes = new List<EquipCategory> { EquipCategory.Sorcery, EquipCategory.Miracle, EquipCategory.Pyromancy };
                g.GiftStartId = 2400;
                g.GiftDescBase = 297150;
                g.Gifts = EldenGifts;
                g.CustomWeaponTypes = new()
                {
                    ["equip_Wep_Left"] = "wepParamType_Left1",
                    ["equip_Subwep_Left"] = "wepParamType_Left2",
                    ["equip_Subwep_Left3"] = "wepParamType_Left3",
                    ["equip_Wep_Right"] = "wepParamType_Right1",
                    ["equip_Subwep_Right"] = "wepParamType_Right2",
                    ["equip_Subwep_Right3"] = "wepParamType_Right3",
                };
            }
        }

        // Starting class things
        HashSet<ItemKey> dualWeapons = new();
        // Used for shops
        SortedDictionary<ShopCat, List<ItemKey>> shopItems = new();
        // Unused currently, but could be used for options
        HashSet<int> regularWeapons = new();
        HashSet<int> somberWeapons = new();
        SortedDictionary<int, List<ItemKey>> weapons = new();
        Dictionary<ItemKey, List<int>> permanentFlags = new();
        Dictionary<int, int> weaponTypeIds = new();

        public void Write(int seed, RandomizerOptions opt)
        {
            Dictionary<EquipCategory, List<ItemKey>> items = new Dictionary<EquipCategory, List<ItemKey>>();
            Dictionary<ItemKey, float> weights = new Dictionary<ItemKey, float>();
            Dictionary<ItemKey, StatReq> requirements = new Dictionary<ItemKey, StatReq>();

            HashSet<ItemKey> crossbows = new HashSet<ItemKey>();
            PARAM magics = game.Param("Magic");
            bool twoHand = !opt["onehand"];
            // For items which combine into the same base item
            HashSet<ItemKey> processedKeys = new();
            // Map from base weapon to custom weapon, when writing weapons to character
            Dictionary<ItemKey, ItemKey> customWeapons = new();
            foreach ((ItemKey dataKey, ItemLocations itemLocs) in data.Items)
            {
                ItemKey key = game.GetBaseWeapon(dataKey);
                if (!processedKeys.Add(key)) continue;
                string name = game.Name(key);
                bool disallowStarting = opt["nerfsh"] && name == "Serpent-Hunter";
                PARAM.Row row = game.Item(key);
                if (row == null)
                {
                    // This doesn't happen for non-goods in vanilla at least
                    if (key.Type != ItemType.Goods)
                    {
                        Console.WriteLine($"Warning: nonexistent {key} found in character equipment data");
                    }
                    continue;
                }
                List<int> itemFlags = itemLocs.Locations.Values
                    .Where(itemLoc => itemLoc.Scope.Type == ScopeType.Event && (itemLoc.Scope.ID >= 60000 && itemLoc.Scope.ID < 70000))
                    .Select(itemLoc => itemLoc.Scope.ID)
                    .ToList();
                if (itemFlags.Count > 0)
                {
                    permanentFlags[key] = itemFlags;
                }
                if (key.Type == ItemType.Weapon)
                {
                    EquipCategory mainCat = EquipCategory.Weapon;
                    if (game.DS3)
                    {
                        int weaponCategory = (byte)row["weaponCategory"].Value;
                        if (g.WeaponCategories.ContainsKey(weaponCategory))
                        {
                            mainCat = g.WeaponCategories[weaponCategory];
                        }
                        if ((byte)row["enableGuard"].Value == 1)
                        {
                            mainCat = EquipCategory.Shield;
                        }
                        if (mainCat == EquipCategory.Bow || mainCat == EquipCategory.Arrow || mainCat == EquipCategory.Bolt)
                        {
                            // Disable greatbow for starting - requirements too far off
                            if (game.DS3)
                            {
                                if ((byte)row["DisableShoot"].Value == 1) continue;
                            }
                            else
                            {
                                // Who knows what this is in ER
                                if (game.Name(key).Contains("Great")) continue;
                            }
                        }
                        if (mainCat == EquipCategory.Bow)
                        {
                            if ((byte)row["boltSlotEquipable"].Value == 1) crossbows.Add(key);
                        }
                        if (mainCat != EquipCategory.Unset && !disallowStarting)
                        {
                            AddMulti(items, mainCat, key);
                        }
                        if ((byte)row["enableMagic"].Value == 1) AddMulti(items, EquipCategory.Catalyst, key);
                        if ((byte)row["enableMiracle"].Value == 1) AddMulti(items, EquipCategory.Talisman, key);
                        if (game.DS3 && (byte)row["enablePyromancy"].Value == 1) AddMulti(items, EquipCategory.Flame, key);
                    }
                    else if (game.EldenRing)
                    {
                        int wepType = (ushort)row["wepType"].Value;
                        // Hardcode main-gauche (1500000) and parrying daggers (1020000) as shields
                        // Except not parrying dagger for now - need to including in starting class, exclude from care package
                        if (key.ID == 1500000)
                        {
                            mainCat = EquipCategory.Shield;
                        }
                        else if (g.WeaponTypes.TryGetValue(wepType, out EquipCategory typeCat))
                        {
                            mainCat = typeCat;
                        }
                        // Console.WriteLine(game.Name(key) + ": " + mainCat);
                        if (mainCat != EquipCategory.Unset && !disallowStarting)
                        {
                            AddMulti(items, mainCat, key);
                        }
                        if ((byte)row["isDualBlade"].Value == 1)
                        {
                            dualWeapons.Add(key);
                        }
                    }

                    int str = (byte)row["properStrength"].Value;
                    // Add two hand adjustment for weapons. Note this doesn't work exactly for casting items, but does not affect casting.
                    // TODO: Double-check DS3 requirement here, the Elden Ring req was wrong for a while
                    if (twoHand
                        && (mainCat == EquipCategory.Weapon || mainCat == EquipCategory.Unset)
                        // DS3 previously only checked isDualBlade, but this is probably correct
                        && (byte)row["bothHandEquipable"].Value == 1 && (byte)row["isDualBlade"].Value == 0)
                    {
                        str = (int)Math.Ceiling(str / 1.5);
                    }
                    requirements[key] = new StatReq
                    {
                        Str = str,
                        Dex = (byte)row["properAgility"].Value,
                        Int = (byte)row["properMagic"].Value,
                        Fai = (byte)row["properFaith"].Value,
                        Arc = game.EldenRing ? (byte)row["properLuck"].Value : 0,
                    };
                    weights[key] = (float)row["weight"].Value;
                    // Shop classification for Elden Ring
                    if (!game.EldenRing) continue;
                    int weaponType = (ushort)row["wepType"].Value;
                    if (row.ID % 10000 == 0 && weaponType > 0)
                    {
                        AddMulti(weapons, weaponType, key);
                        AddMulti(shopItems, ShopCat.Weapon, key);
                    }
                    if ((int)row["originEquipWep25"].Value > 0) regularWeapons.Add(row.ID);
                    else if ((int)row["originEquipWep10"].Value > 0) somberWeapons.Add(row.ID);
                    if (game.GetCustomGem(dataKey, out ItemKey customGem))
                    {
                        if (processedKeys.Add(customGem))
                        {
                            AddMulti(shopItems, ShopCat.AshOfWar, customGem);
                        }
                        // If any custom weapon exists, use it. It will be de-leveled if it's used
                        customWeapons[key] = dataKey;
                    }
                }
                else if (key.Type == ItemType.Gem)
                {
                    AddMulti(shopItems, ShopCat.AshOfWar, key);
                }
                else if (key.Type == ItemType.Protector)
                {
                    int armorCat = (byte)row["protectorCategory"].Value;
                    if (armorCat < ArmorCats.Count)
                    {
                        EquipCategory cat = ArmorCats[armorCat];
                        AddMulti(items, cat, key);
                        weights[key] = (float)row["weight"].Value;
                        AddMulti(shopItems, ShopCat.Armor, key);
                    }
                }
                else if (key.Type == ItemType.Goods)
                {
                    PARAM.Row magic = magics[key.ID];
                    // Exclude Spook and Tears of Denial as they can be a key item, useful though they are
                    // Also, no Stonesword Key (Briars of Sin)
                    if (magic != null && key.ID != 1354000 && key.ID != 3520000 && key.ID != 8000)
                    {
                        int magicCat = (byte)magic["ezStateBehaviorType"].Value;
                        AddMulti(items, g.MagicTypes[magicCat], key);
                        requirements[key] = new StatReq
                        {
                            Str = 0,
                            Dex = 0,
                            Int = (byte)magic["requirementIntellect"].Value,
                            Fai = (byte)magic["requirementFaith"].Value,
                            Arc = game.EldenRing ? (byte)magic["requirementLuck"].Value : 0,
                            Att = (byte)magic["slotLength"].Value,
                        };
                    }
                    // TODO: See if there's a better condition to use here
                    else if (game.EldenRing && (byte)row["useLimitSummonBuddy"].Value > 0 && key.ID % 100 == 0)
                    {
                        AddMulti(items, EquipCategory.SpiritAsh, key);
                    }
                    // Shop classification
                    if (!game.EldenRing) continue;
                    int goodsType = (byte)row["goodsType"].Value;
                    int group = (byte)row["sortGroupId"].Value;
                    if (goodsIdCats.TryGetValue(row.ID, out ShopCat cat)
                        || goodsGroupCats.TryGetValue((goodsType, group), out cat)
                        || goodsGroupCats.TryGetValue((goodsType, -1), out cat))
                    {
                        if (cat != ShopCat.None)
                        {
                            AddMulti(shopItems, cat, key);
                        }
                    }
                    else if ((row.ID >= 9300 && row.ID < 9500) || (row.ID >= 2009300 && row.ID < 2009500))
                    {
                        AddMulti(shopItems, ShopCat.Cookbooks, key);
                    }
                }
                else if (key.Type == ItemType.Accessory)
                {
                    // For character purposes, only use base talismans. Probably allow really good ones, since it's all uniform odds.
                    // For DLC start, allow the base game ones randomly, since +0 ones might be undesirable
                    if ((key.ID % 10 == 0 || (opt["dlc"] && opt["dlcstart"])) && !(game.EldenRing ? key.ID >= 7000 && key.ID < 8000 : key.ID < 20000))
                    {
                        AddMulti(items, EquipCategory.Accessory, key);
                    }
                    AddMulti(shopItems, ShopCat.Talisman, key);
                }
            }
            if (game.EldenRing)
            {
                foreach (PARAM.Row row in game.Params["MenuValueTableParam"].Rows)
                {
                    if (row.ID < 120 || row.ID >= 300) continue;
                    int value = (int)row["value"].Value;
                    int textId = (int)row["textId"].Value;
                    string name = game.MenuFMGs["GR_MenuText"][textId];
                    if (value <= 0 || string.IsNullOrEmpty(name)) continue;
                    weaponTypeIds[value] = textId;
                }
            }

            bool allowCheat = false;
            bool printChars = true;
#if DEBUG
            allowCheat = true;
            printChars = opt["printchars"];
#endif
            bool cheat = allowCheat && opt["cheat"];

            if (opt["dlc"] && (opt["dlcstart"] || opt["quickstart"]))
            {
                List<ItemKey> carePackageItems = new();
                if (opt["dlcpackage"])
                {
                    HashSet<EquipCategory> skipEquips = new();
                    if (opt["packagelite"])
                    {
                        skipEquips = new() { EquipCategory.Weapon, EquipCategory.Bow, EquipCategory.Arrow, EquipCategory.Sorcery, EquipCategory.Miracle };
                    }
                    Random careRandom = new Random(seed);
                    void addSample(List<ItemKey> keys, int count)
                    {
                        List<ItemKey> baseItems = keys.Where(i => !game.IsEldenDlcItem(i)).ToList();
                        carePackageItems.AddRange(ChoiceN(careRandom, baseItems, count));
                    }
                    foreach ((EquipCategory cat, int count) in careEquips)
                    {
                        if (skipEquips.Contains(cat)) continue;
                        if (items.TryGetValue(cat, out List<ItemKey> keys)) addSample(keys, count);
                    }
                    foreach ((ShopCat cat, int count) in careShops)
                    {
                        if (shopItems.TryGetValue(cat, out List<ItemKey> keys)) addSample(keys, count);
                    }
                    if (printChars || true)
                    {
                        Console.WriteLine($"Adding randomized DLC care package");
                        Console.WriteLine($"{carePackageItems.Count} items: {string.Join(", ", carePackageItems.Select(game.Name))}");
                        Console.WriteLine();
                    }
                }
                WriteDlcStart(opt, carePackageItems);
            }

            // If no DLC, remove everything from the following selection.
            if (game.EldenRing)
            {
                foreach ((EquipCategory cat, List<ItemKey> keys) in items)
                {
                    if (!opt["dlc"])
                    {
                        keys.RemoveAll(game.IsEldenDlcItem);
                    }
                    if (!opt["tarnished"])
                    {
                        keys.RemoveAll(GameData.CheckEldenTarnishedItem);
                    }
                }
            }
            // Console.WriteLine($"Shields: {string.Join(", ", items[EquipCategory.Shield].Where(game.IsEldenDlcItem).Select(game.Name))}");

            // This is used for starting armor sets, starting classes, and npc armor sets
            Random random = new Random(seed);
            // foreach (var v in cats) Console.WriteLine($"{v.Key}: {string.Join(", ", v.Value.Select(k => game.Name(k)))}");
            // Generate some armor sets. One downside of this approach is that each piece is represented only once -
            // but it is just one shuffle per category, and tends to result in a similar distribution to normal.
            List<ArmorSet> generateSets(Random random, Func<ItemKey, bool> armorFilter = null)
            {
                List<List<ItemKey>> weightedArmors = new List<List<ItemKey>>();
                for (int i = 0; i < 4; i++)
                {
                    List<ItemKey> armorItems = items[ArmorCats[i]];
                    if (armorFilter != null)
                    {
                        armorItems = armorItems.Where(armorFilter).ToList();
                    }
                    weightedArmors.Add(WeightedShuffle(random, armorItems, item => 1 / weights[item]));
                }
                List<ArmorSet> armors = new List<ArmorSet>();
                int maxArmors = weightedArmors.Select(rank => rank.Count).Min();
                for (int num = 0; num < maxArmors; num++)
                {
                    ArmorSet armor = new ArmorSet();
                    for (int i = 0; i < 4; i++)
                    {
                        ItemKey item = weightedArmors[i][num];
                        armor.Ids[i] = item.ID;
                        armor.Weight += weights[item];
                    }
                    armors.Add(armor);
                }
                armors.Sort((a, b) => a.Weight.CompareTo(b.Weight));
                return armors;
            }
            List<ArmorSet> armors = generateSets(random);
            List<ArmorSet> startingArmors = armors;
            if (opt["dlc"] && opt["dlconlyhand"])
            {
                // Use a separate set in this case, so that NPC ones don't run out.
                // They should probably be independent anyway
                startingArmors = generateSets(random, game.IsEldenDlcItem);
            }

            PARAM chara = game.Param("CharaInitParam");

            List<float> eldenWeights = new List<float>
            {
                45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 45.0f, 46.6f,
                48.2f, 49.8f, 51.4f, 52.9f, 54.5f, 56.1f, 57.7f, 59.3f, 60.9f, 62.5f,
                64.1f, 65.6f, 67.2f, 68.8f, 70.4f, 72.0f, 73.0f, 74.1f, 75.2f, 76.4f,
                77.6f, 78.9f, 80.2f, 81.5f, 82.8f, 84.1f, 85.4f, 86.8f, 88.1f, 89.5f,
                90.9f, 92.3f, 93.7f, 95.1f, 96.5f, 97.9f, 99.4f, 100.8f, 102.2f, 103.7f,
                105.2f, 106.6f, 108.1f, 109.6f, 111.0f, 112.5f, 114.0f, 115.5f, 117.0f, 118.5f,
                120.0f, 121.0f, 122.1f, 123.1f, 124.1f, 125.1f, 126.2f, 127.2f, 128.2f, 129.2f,
                130.3f, 131.3f, 132.3f, 133.3f, 134.4f, 135.4f, 136.4f, 137.4f, 138.5f, 139.5f,
                140.5f, 141.5f, 142.6f, 143.6f, 144.6f, 145.6f, 146.7f, 147.7f, 148.7f, 149.7f,
                150.8f, 151.8f, 152.8f, 153.8f, 154.9f, 155.9f, 156.9f, 157.9f, 159.0f, 160.0f,
            };
            float getMaxWeight(PARAM.Row row)
            {
                if (game.DS3)
                {
                    return 40 + (sbyte)row["baseDurability"].Value;
                }
                else
                {
                    int end = (byte)row["baseEnd"].Value;
                    return eldenWeights[end];
                }
            }

            for (int i = 0; i < g.Classes.Count; i++)
            {
                if (opt["nostarting"]) break;
                PARAM.Row row = chara[g.StartId + i];
                int getStat(string name)
                {
                    return game.EldenRing ? (byte)row[name].Value : (sbyte)row[name].Value;
                }
                void setStat(string name, int val)
                {
                    if (game.EldenRing)
                    {
                        row[name].Value = (byte)val;
                    }
                    else
                    {
                        row[name].Value = (sbyte)val;
                    }
                }
                // First, always fudge magic to 10, so that Orbeck quest is possible.
                // This could alternatively be an ESD edit.
                if (game.DS3 && getStat("baseMag") < 10)
                {
                    setStat("baseMag", 10);
                }
                if (cheat)
                {
                    foreach (string stat in g.Stats) setStat($"base{stat}", 90);
                }

                // Then, see stat diffs for weapons/spells/catalysts, and fudge if necessary
                CharacterClass chClass = g.Classes[i];
                // In Elden Ring, everyone starts with 2
                int attAmt = 2;
                if (game.DS3)
                {
                    int attStat = getStat("baseWil");
                    attAmt = attStat < 10 ? 0 : attStat < 14 ? 1 : 2;
                }
                StatReq chReqs = new StatReq
                {
                    Str = getStat("baseStr"),
                    Dex = getStat("baseDex"),
                    Int = getStat("baseMag"),
                    Fai = getStat("baseFai"),
                    Arc = getStat("baseLuc"),
                    Att = attAmt,
                };
                StatReq dynamicReqs = chReqs;
                double fudgeFactor = 1.5;
                float weaponWeight = 0f;
                int attSlots = 0;
                bool crossbowSelected = false;
                if (printChars) Console.WriteLine($"Randomizing starting equipment for {chClass.Name}");
                Dictionary<ItemKey, (EquipCategory, int)> selectedItems = new Dictionary<ItemKey, (EquipCategory, int)>();
                foreach (KeyValuePair<string, EquipCategory> entry in g.BaseStart.Concat(chClass.Start))
                {
                    EquipCategory originalCat = entry.Value;
                    EquipCategory cat = originalCat;
                    if (cat == EquipCategory.DoubleWeapon)
                    {
                        cat = EquipCategory.Weapon;
                    }
                    // TODO: If a catalyst etc also doubles as a weapon, maybe skip its slot.
                    // This crossbow/bow logic relies on iteration order - try to make the order fixed...
                    if ((cat == EquipCategory.Arrow && crossbowSelected) || (cat == EquipCategory.Bolt && !crossbowSelected)) continue;
                    // Console.WriteLine(originalCat);
                    // Instead of using Distinct, could also make it a SortedSet. It's necessary because of normalization.
                    Dictionary<ItemKey, int> statDiffs = items[cat].Distinct().ToDictionary(item => item, item => requirements[item].Eligible(dynamicReqs));
                    List<ItemKey> candidates = items[cat].ToList();
                    // TODO: Make dlconlyhand work with items
                    if (!opt["nohand"] && (cat == EquipCategory.Shield || chClass.Name == "Deprived" || chClass.Name == "Wretch" || !opt["changestats"] || opt["dlconlyhand"]))
                    {
                        candidates.RemoveAll(item => statDiffs[item] < 0);
                    }
                    if (cat == EquipCategory.Sorcery || cat == EquipCategory.Miracle || cat == EquipCategory.Pyromancy)
                    {
                        // Fit within attunement slots. Alternatively could increase attunement, but that unbalances things potentially.
                        // Unfortunately means that pyromancer can't start with Chaos Bed Vestiges. Maybe for the best.
                        if (attSlots == chReqs.Att)
                        {
                            row[entry.Key].Value = -1;
                            continue;
                        }
                        candidates.RemoveAll(item => attSlots + requirements[item].Att > chReqs.Att);
                    }
                    if (originalCat == EquipCategory.DoubleWeapon)
                    {
                        candidates.RemoveAll(item => dualWeapons.Contains(item));
                    }
                    if (opt["dlc"] && opt["dlconlyhand"])
                    {
                        // Depending on options, some categories may be missing viable things, like shields, catalysts
                        if (candidates.Any(game.IsEldenDlcItem))
                        {
                            candidates.RemoveAll(i => !game.IsEldenDlcItem(i));
                        }
                    }
                    // Select weapon and adjust stats if necessary
                    // This could just be WeightedChoice?
                    List<ItemKey> weightKeys = WeightedShuffle(random, candidates, item =>
                    {
                        if (opt["nohand"]) return 1;
                        int diff = statDiffs[item];
                        float weight;
                        if (diff >= 4)
                        {
                            // A dropoff from 1 to 0.0625 to not have bad weapons (big gap) chosen too often
                            weight = (float)Math.Pow(2, -4 * (Math.Min(diff - 4, 20) / 20.0));
                        }
                        else if (diff >= 0)
                        {
                            weight = 2;
                        }
                        else if (diff < -15)
                        {
                            // Try to avoid SL much more than 20
                            weight = 0;
                        }
                        else
                        {
                            // For unwieldable weapons, start at 1.5^-1 (0.666), then 1.5^-2 (0.444), etc.
                            weight = (float)Math.Pow(fudgeFactor, diff);
                            // To prevent something too absurd from happening, keep all stats under 20. Multi-stat increases don't look as bad by comparison.
                            StatReq req = requirements[item];
                            if (req.GetMaxStat() >= 20) weight = 0;
                        }
                        // Console.WriteLine($"{game.Name(item)}: {diff} -> {weight}");
                        return weight;
                    });
                    if (cat == EquipCategory.Weapon && chClass.Name == "Wretch")
                    {
                        foreach (ItemKey key in candidates)
                        {
                            // Console.WriteLine($"{game.Name(key)}: stats {requirements[key]}, diff {statDiffs[key]}");
                        }
                    }
                    ItemKey selected = weightKeys[0];
                    items[cat].Remove(selected);
                    if (statDiffs[selected] < 0 && !opt["nohand"])
                    {
                        dynamicReqs.Adjust(requirements[selected]);
                        fudgeFactor *= -statDiffs[selected];
                    }
                    ItemKey writeItem = selected;
                    if (g.CustomWeaponTypes != null && g.CustomWeaponTypes.ContainsKey(entry.Key) && customWeapons.TryGetValue(writeItem, out ItemKey customItem))
                    {
                        writeItem = game.AutoUpgrade(customItem, 0);
                    }
                    void setItem(string field)
                    {
                        row[field].Value = writeItem.ID;
                        if (g.CustomWeaponTypes != null && g.CustomWeaponTypes.TryGetValue(field, out string typeField))
                        {
                            row[typeField].Value = (byte)(writeItem.Type == ItemType.Custom ? 1 : 0);
                        }
                        else if (writeItem.Type == ItemType.Custom)
                        {
                            throw new Exception($"Internal error: can't set starting class item {field} to weapon {writeItem}");
                        }
                    }
                    int quantity = 1;
                    setItem(entry.Key);
                    if (originalCat == EquipCategory.DoubleWeapon)
                    {
                        setItem("equip_Wep_Left");
                        quantity = 2;
                    }
                    if (weights.ContainsKey(selected))
                    {
                        weaponWeight += quantity * weights[selected];
                    }
                    attSlots += requirements[selected].Att;
                    if (printChars) Console.WriteLine($"  {entry.Key} is now {game.Name(selected)}, meets requirements by {statDiffs[selected]}");
                    selectedItems[selected] = (cat, opt["nohand"] ? statDiffs[selected] : 0);
                }
                // In Elden Ring, also change display characters, and add text descriptions
                if (game.EldenRing)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        // This isn't the fastest. The only difference with this version is npcPlayerFaceGenId
                        PARAM.Row row2 = chara[3100 + i * 2 + j];
                        foreach (string field in g.BaseStart.Keys
                            .Concat(chClass.Start.Keys)
                            .Concat(g.Stats.Select(s => $"base{s}"))
                            .Concat(g.CustomWeaponTypes.Values))
                        {
                            row2[field].Value = row[field].Value;
                        }
                    }
                }
                List<(ItemKey, int)> showItems = selectedItems
                    .OrderByDescending(e => DescriptionPriority.IndexOf(e.Value.Item1))
                    .Select(e => (e.Key, e.Value.Item2))
                    .ToList();
                foreach ((string lang, FMGDictionary itemFmgs) in game.AllItemFMGs)
                {
                    bool useSpaces = !lang.StartsWith("jpn") && !lang.StartsWith("zho");
                    FMGDictionary menuFmgs = game.AllMenuFMGs[lang];
                    List<string> itemStrs = new List<string>();
                    foreach ((ItemKey item, int diff) in showItems)
                    {
                        // Currently, the only two starting weapon types
                        // TODO: Soapstone FMG things, this won't include DS3 DLCs
                        string fmgName = item.Type == ItemType.Weapon ? (game.DS3 ? "武器名" : "WeaponName") : (game.DS3 ? "アイテム名" : "GoodsName");
                        string itemName = itemFmgs[fmgName][item.ID];
                        if (!string.IsNullOrWhiteSpace(itemName))
                        {
                            if (diff < 0)
                            {
                                itemName = useSpaces ? $"{itemName} ({diff})" : $"{itemName}（{diff}）";
                            }
                            itemStrs.Add(itemName);
                        }
                    }
                    if (chClass.Desc > 0)
                    {
                        string split = SplitCharacterText(useSpaces, itemStrs);
                        if (!string.IsNullOrWhiteSpace(split))
                        {
                            if (game.DS3)
                            {
                                menuFmgs["FDP_一行ヘルプ"][chClass.Desc] = split;
                            }
                            else
                            {
                                menuFmgs["GR_LineHelp"][chClass.Desc] = split;
                            }
                            // if (lang == "engus") Console.WriteLine($"{chClass.Name}: {split.Replace("\n", "\\n")}");
                        }
                    }
                }
                int statChange = dynamicReqs.Eligible(chReqs);
                if (statChange < 0 && !opt["nohand"])
                {
                    setStat("baseStr", dynamicReqs.Str);
                    setStat("baseDex", dynamicReqs.Dex);
                    setStat("baseMag", dynamicReqs.Int);
                    setStat("baseFai", dynamicReqs.Fai);
                    if (game.EldenRing)
                    {
                        setStat("baseLuc", dynamicReqs.Arc);
                        row["soulLv"].Value = (short)((short)row["soulLv"].Value - statChange);
                    }
                    else
                    {
                        row["soulLvl"].Value = (short)((short)row["soulLvl"].Value - statChange);
                    }
                }
                // Armor time
                float totalWeight = getMaxWeight(row);
                List<ArmorSet> availableSets = startingArmors.TakeWhile(armor => armor.Weight + weaponWeight < totalWeight * 0.69f).ToList();
                if (availableSets.Count == 0) availableSets = new List<ArmorSet> { startingArmors[0] };
                ArmorSet selectedArmor = Choice(random, availableSets);
                startingArmors.Remove(selectedArmor);
                if (printChars)
                {
                    Console.WriteLine($"  Armor: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.Protector, id))))}");
                    Console.WriteLine($"  Weight: weapons {weaponWeight:0.##} + armor {selectedArmor.Weight:0.##} / {totalWeight:0.##} = {100 * (weaponWeight + selectedArmor.Weight) / totalWeight:0.##}%");
                }
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[g.ArmorSlots[j]].Value != -1)
                    {
                        row[g.ArmorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }

                if (cheat)
                {
                    PARAM reinforce = game.Param("ReinforceParamWeapon");
                    HashSet<int> reinforceLevels = new HashSet<int>(reinforce.Rows.Select(r => (int)r.ID));
                    foreach (string wep in g.WeaponSlots)
                    {
                        int id = (int)row[wep].Value;
                        if (id > 0)
                        {
                            id = id - (id % 100);
                            PARAM.Row item = game.Item(new ItemKey(ItemType.Weapon, id));
                            int reinforceId = (short)item["reinforceTypeId"].Value;
                            while (reinforceLevels.Contains(reinforceId + 5))
                            {
                                reinforceId += 5;
                                id += 5;
                            }
                            row[wep].Value = id;
                        }
                    }
                }
            }
            if (printChars) Console.WriteLine();

            // Extra pass for upgrades, so this also happens when starting class is disabled. Taken straight from fog rando
            // DLC Start only!!!
            if (opt["dlc"] && opt["dlcstart"] && opt["maxweapon"])
            {
                foreach (PARAM.Row row in game.Params["CharaInitParam"].Rows)
                {
                    if ((row.ID >= 3000 && row.ID < 3010) || (row.ID >= 3100 && row.ID < 3120))
                    {
                        foreach (PARAM.Cell cell in row.Cells)
                        {
                            string name = cell.Def.InternalName;
                            if (name.StartsWith("equip_Wep") || name.StartsWith("equip_Subwep"))
                            {
                                int wep = (int)cell.Value;
                                if (wep > 0)
                                {
                                    ItemType type = ItemType.Weapon;
                                    if (g.CustomWeaponTypes.TryGetValue(name, out string typeField) && (byte)row[typeField].Value == 1)
                                    {
                                        type = ItemType.Custom;
                                    }
                                    ItemKey item = new ItemKey(type, wep);
                                    item = game.AutoUpgrade(item, 25);
                                    cell.Value = item.ID;
                                }
                            }
                        }
                    }
                }
            }

            // Starting gifts in Elden Ring (keepsakes)
            // None, Crimson Amber Medallion, Lands Between Rune, Golden Seed, Fanged Imp Ashes
            // 3x Cracked Pot, 2x Stonesword Key, 5x Bewitching Branch, 5x Boiled Prawn, Shabriri's Woe
            // Main types:
            // - (== 2) Talismans - +0 only, no big stat ones probably
            // - (== 1) Ash (cheap ones / good ones)
            // Other item types:
            // - (<= 1) Lands Between Rune is 3000, otherwise go 4-10
            // - (<= 1) Golden Seed / Sacred Tear
            // - Throwables, a few good ones or many okay ones
            // - (>= 2) Buffs (min 2) - Bewitching Branch, Boiled Prawn/Crab, Exalted Flesh, Starlight Shard, Grease, Warming Stone, Fowl Foot
            // - (>= 1) Key-ish items - Shackle, Telescope, Claw, Larval Tear, Seedbed Curse
            // Overall, min 6 allocated from main list, 3 remaining. Use more weight for unused categories?
            // Any other upgrade materials?
            // Probably not permanent items
            int p = 0;
            // for (p = 0; p < 100; p++)
            if (!opt["nogifts"] && g.Gifts != null && ann != null)
            {
                List<StartingItemAnnotation> getGroupEntries(string name)
                {
                    if (!ann.StartingGroups.TryGetValue(name, out StartingGroupAnnotation group) || group.Entries == null)
                    {
                        throw new Exception($"Internal error: missing starting gift listing config for {name} in [{string.Join(", ", ann.StartingGroups.Keys)}]");
                    }
                    // Filter DLC stuff here, just mutate the item lists. If DLC is enabled, DLC items should be interchangeable with base game items
                    List<StartingItemAnnotation> entries = group.Entries.ToList();
                    if (game.EldenRing)
                    {
                        if (!opt["tarnished"])
                        {
                            entries.RemoveAll(entry =>
                            {
                                entry.Items.RemoveAll(item => item.Keys.Any(GameData.CheckEldenTarnishedItem));
                                return entry.Items.Count == 0;
                            });
                        }
                        if (!opt["dlc"])
                        {
                            entries.RemoveAll(entry =>
                            {
                                entry.Items.RemoveAll(item => item.Keys.Any(game.IsEldenDlcItem));
                                return entry.Items.Count == 0;
                            });
                        }
                        else if (opt["dlc"] && opt["dlconlyhand"])
                        {
                            entries.RemoveAll(entry =>
                            {
                                if (!entry.HasTag("base")) return false;
                                entry.Items.RemoveAll(item => item.Keys.Any(i => !game.IsEldenDlcItem(i)));
                                return entry.Items.Count == 0;
                            });
                        }
                    }
                    // Console.WriteLine($"{name} entries: {string.Join("; ", entries.Select(entry => string.Join(", ", entry.Items.Select(i => i.Name))))}");
                    if (entries.Count == 0)
                    {
                        throw new Exception($"Internal error: no starting gift items configured for {name}");
                    }
                    return entries;
                }
                // Categories: None, Accessory, SpiritAsh, KeyItem, Buff, Soul, Health, Throwable, Misc
                SortedDictionary<GiftCategory, List<StartingItemAnnotation>> giftTypes = new()
                {
                    [GiftCategory.KeyItem] = getGroupEntries("keys"),
                    [GiftCategory.Buff] = getGroupEntries("buffs"),
                    [GiftCategory.Soul] = getGroupEntries("souls"),
                    [GiftCategory.Throwable] = getGroupEntries("throwables"),
                };
                if (game.EldenRing)
                {
                    if (opt["dlc"] && (opt["dlcstart"] || opt["quickstart"]) && (opt["maxflask"] || opt["maxflasknum"]))
                    {
                        // Don't deal with filtering this within the category, do it if either are enabled
                    }
                    else
                    {
                        giftTypes[GiftCategory.Health] = getGroupEntries("health");
                    }
                }
                else if (game.DS3)
                {
                    giftTypes[GiftCategory.Gem] = getGroupEntries("gems");
                }
                // Use other random to this independent from character stuff (but NPC armor doesn't really matter, can be dependent)
                Random giftRandom = new Random(seed + 10 + p);

                (ItemKey, int) selectItemFromCategory(GiftCategory cat)
                {
                    if (!giftTypes.TryGetValue(cat, out List<StartingItemAnnotation> items) || items.Count == 0)
                    {
                        throw new Exception($"Internal error: no more starting gifts left of type {cat}");
                    }
                    StartingItemAnnotation itemAnn = Choice(giftRandom, items);
                    items.Remove(itemAnn);
                    if (items.Count == 0) giftTypes.Remove(cat);
                    // Select item
                    List<ItemKey> itemKeys = itemAnn.Items.SelectMany(e => e.Keys).ToList();
                    ItemKey item = itemKeys.Count == 1 ? itemKeys[0] : Choice(giftRandom, itemKeys);
                    int quantity = itemAnn.Quantities.Count == 1 ? itemAnn.Quantities[0] : Choice(giftRandom, itemAnn.Quantities);
                    return (item, quantity);
                }
                string fmgStr(string s) => string.IsNullOrEmpty(s) ? "???" : s;
                SortedDictionary<int, (string, string)> newGifts = new();
                void setGift(int i, ItemKey item, int quantity)
                {
                    StartingGift gift = g.Gifts[i];
                    PARAM.Row row = chara[g.GiftStartId + i];
                    // Clear whatever's there first, just in case. There are 4 accessory, 10+10 item field, so this is not the most efficient
                    for (int k = 1; k < 4; k++)
                    {
                        row[game.DS3 ? $"equip_Accessory{k}" : $"equip_Accessory{k:d2}"].Value = -1;
                    }
                    for (int k = 1; k < 10; k++)
                    {
                        row[game.DS3 ? $"Item_{k:d2}" : $"item_{k:d2}"].Value = -1;
                        row[$"itemNum_{k:d2}"].Value = (byte)0;
                    }
                    // Hardcode the placement the vanilla game uses
                    string nameFmg;
                    string infoFmg;
                    if (item.Type == ItemType.Goods)
                    {
                        row[game.DS3 ? "Item_02" : "item_03"].Value = item.ID;
                        row[game.DS3 ? "itemNum_02" : "itemNum_03"].Value = (byte)quantity;
                        nameFmg = game.DS3 ? "アイテム名" : "GoodsName";
                        infoFmg = game.DS3 ? "アイテム説明" : "GoodsInfo";
                    }
                    else if (item.Type == ItemType.Accessory)
                    {
                        row[game.DS3 ? "equip_Accessory2" : "equip_Accessory01"].Value = item.ID;
                        nameFmg = game.DS3 ? "アクセサリ名" : "AccessoryName";
                        infoFmg = game.DS3 ? "アクセサリ説明" : "AccessoryInfo";
                    }
                    else throw new Exception($"Invalid starting gift {item}");
                    int descId = g.GiftDescBase + gift.Desc;
                    string prevName = null;
                    string newName = null;
                    foreach ((string lang, FMGDictionary itemFmgs) in game.AllItemFMGs)
                    {
                        bool useSpaces = true; // !lang.StartsWith("jpn") && !lang.StartsWith("zho");
                        FMGDictionary menuFmgs = game.AllMenuFMGs[lang];
                        string itemName = fmgStr(itemFmgs[nameFmg][item.ID]);
                        string itemDesc = fmgStr(itemFmgs[infoFmg][item.ID]);
                        if (lang == "engus")
                        {
                            prevName = fmgStr(menuFmgs[game.DS3 ? "FDP_メニューテキスト" : "GR_MenuText"][descId]);
                            newName = itemName;
                        }
                        menuFmgs[game.DS3 ? "FDP_メニューテキスト" : "GR_MenuText"][descId] = itemName;
                        // TODO does this need to be broken up manually? Also localize?
                        menuFmgs[game.DS3 ? "FDP_一行ヘルプ" : "GR_LineHelp"][descId] = quantity == 1 ? itemDesc : $"({quantity}x){(useSpaces ? " " : "")}{itemDesc}";
                    }
                    if (newName != null)
                    {
                        newGifts[i] = (prevName, newName + (quantity == 1 ? "" : $" {quantity}x"));
                    }
                }

                // First do fixed category items. Calculate list of rings/spirit ashes in advance
                List<ItemKey> acc = items[EquipCategory.Accessory].ToList();
                List<ItemKey> ashes = game.EldenRing ? items[EquipCategory.SpiritAsh].ToList() : new();
                if (game.EldenRing && opt["dlc"] && opt["dlconlyhand"])
                {
                    acc.RemoveAll(i => !game.IsEldenDlcItem(i));
                    ashes.RemoveAll(i => !game.IsEldenDlcItem(i));
                }
                for (int i = 0; i < g.Gifts.Count; i++)
                {
                    StartingGift gift = g.Gifts[i];
                    if (gift.Category == GiftCategory.None || gift.Category == GiftCategory.Misc) continue;
                    if (gift.Category == GiftCategory.Accessory || gift.Category == GiftCategory.SpiritAsh)
                    {
                        // These are already filtered when DLC is disabled
                        List<ItemKey> giftItems = gift.Category == GiftCategory.Accessory ? acc : ashes;
                        ItemKey item = Choice(giftRandom, giftItems);
                        giftItems.Remove(item);
                        setGift(i, item, 1);
                    }
                    else
                    {
                        (ItemKey item, int quantity) = selectItemFromCategory(gift.Category);
                        setGift(i, item, quantity);
                    }
                }
                for (int i = 0; i < g.Gifts.Count; i++)
                {
                    StartingGift gift = g.Gifts[i];
                    if (gift.Category != GiftCategory.Misc) continue;
                    List<GiftCategory> giftCats = giftTypes.Keys.ToList();
                    GiftCategory giftCat = Choice(giftRandom, giftCats);
                    (ItemKey item, int quantity) = selectItemFromCategory(giftCat);
                    setGift(i, item, quantity);
                }
                if (printChars)
                {
                    foreach ((string from, string to) in newGifts.Values)
                    {
                        Console.WriteLine($"Replacing starting gift {from}: {to}");
                    }
                    Console.WriteLine();
                }
            }

            // Now, have fun with NPCs
            if (opt["nooutfits"]) return;

            // Elden Ring only, enemy recolors, and only Nomadic Merchants to start
            // This doesn't affect many other friendly NPCs, at least not with existing materials.
            if (game.EldenRing)
            {
                Dictionary<int, Dictionary<int, List<int>>> modelColors = new();
                foreach (PARAM.Row npc in game.Params["NpcParam"].Rows)
                {
                    int model = npc.ID / 10000;
                    if (model != 3200 && model != 3210) continue;
                    if ((int)npc["residentMaterialExParamId00"].Value == -1) continue;
                    for (int i = 0; i <= 4; i++)
                    {
                        int mat = (int)npc[$"residentMaterialExParamId0{i}"].Value;
                        AddMultiNest(modelColors, model, i, mat);
                    }
                }
                // Some interesting mats contain Tint and [Color]
                // int[] torrentMats = new[] { 20300, 20302, 20404, 21701, 23001, 23201, 23202, 23204, 23401, 23500 };
                Random matRandom = new Random(seed);
                foreach (PARAM.Row npc in game.Params["NpcParam"].Rows)
                {
                    int model = npc.ID / 10000;
                    if (!modelColors.TryGetValue(model, out var slots)) continue;
                    for (int i = 0; i <= 4; i++)
                    {
                        if (!slots.TryGetValue(i, out var vals)) continue;
                        npc[$"residentMaterialExParamId0{i}"].Value = Choice(matRandom, vals);
                    }
                }
            }

            // Just remove Symbol of Avarice first (may not matter with Irregulator, but those are more chaotic anyway)
            if (game.DS3) armors.RemoveAll(set => set.Ids[0] == 78500000);
            Dictionary<int, ArmorSet> npcArmors = new Dictionary<int, ArmorSet>();
            Dictionary<string, int> npcNameRows = new Dictionary<string, int>();
            Func<ItemType, PARAM.Cell, float> cellWeight = (type, cell) =>
            {
                int id = (int)cell.Value;
                if (id == -1) return 0;
                ItemKey key = new ItemKey(type, id);
                if (!weights.ContainsKey(key)) return 0;
                return weights[key];
            };
            HashSet<string> excludeArmorChara = new()
            {
                "Human", "Lamenter", "Ancient Dragon-Man", "Dragon Communion Priestess",
            };
            foreach (PARAM.Row row in chara.Rows.Where(r => r.ID >= 10000))
            {
                // DLC items are removed from above, so there won't be enough armor for DLC NPCs anyway.
                // Don't bother with silos for now, it's cosmetic and very complicated to calculate.
                if (row.ID >= 2000000 && !opt["dlc"]) continue;
                string name = game.CharacterName(row.ID);
                if (name == null || name.Contains("Mimic Tear") || excludeArmorChara.Contains(name)) continue;
                ArmorSet selectedArmor;
                if (!npcNameRows.TryGetValue(name, out int baseId))
                {
                    npcNameRows[name] = baseId = row.ID / 10;
                }
                if (!npcArmors.ContainsKey(baseId))
                {
                    float weaponWeight = g.WeaponSlots.Select(slot => cellWeight(ItemType.Weapon, row[slot])).Sum();
                    float armorWeight = g.ArmorSlots.Select(slot => cellWeight(ItemType.Protector, row[slot])).Sum();
                    float weightLimit = weaponWeight + armorWeight;
                    float totalWeight = getMaxWeight(row);
                    int armorLimit = armors.FindIndex(armor => armor.Weight + weaponWeight > weightLimit);
                    if (armorLimit == -1)
                    {
                        armorLimit = armors.Count - 1;
                    }
                    armorLimit = Math.Min(20, armorLimit);
                    npcArmors[baseId] = selectedArmor = armors[random.Next(armorLimit)];
                    armors.Remove(selectedArmor);
                    if (printChars) Console.WriteLine($"Armor for {name}: {100 * weightLimit / totalWeight:0.##}% -> {100 * (selectedArmor.Weight + weaponWeight) / totalWeight:0.##}%: {string.Join(", ", selectedArmor.Ids.Select(id => game.Name(new ItemKey(ItemType.Protector, id))))}");
                }
                selectedArmor = npcArmors[baseId];
                for (int j = 0; j < 4; j++)
                {
                    if ((int)row[g.ArmorSlots[j]].Value != -1)
                    {
                        row[g.ArmorSlots[j]].Value = selectedArmor.Ids[j];
                    }
                }
            }
            if (printChars) Console.WriteLine();
        }

        public enum ActionType
        {
            None,
            Shop,
            Rebirth,
            Warp,
            WarpBase,
        }

        public class StartMenu : ESDEdits.MenuTree<StartMenu>
        {
            public ActionType Action { get; set; }
            public ShopCat Type { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
            internal List<ItemKey> Items { get; set; }
        }

        public class InitialItem
        {
            // Only script-giveable item types
            public ItemKey Key { get; set; }
            public int Quantity { get; set; }
            // Each of these is set when the item is given.
            public List<int> EventFlags { get; set; }
        }

        // Handy utils
        private static AST.Expr EventFlagExpr(int flag) => AST.MakeFunction("f15", flag);
        // Dependency on ItemType value for these. Does not support gems or custom weapons
        private static AST.Expr HasItemExpr(ItemKey key, ESDEdits.ComparisonType cmp, int val)
        {
            // f47 ComparePlayerInventoryNumber(itemType, itemId, comparisonType, amt, 0)
            return AST.MakeFunction("f47", (int)key.Type, key.ID, (int)cmp, val, 0);
        }

        private void WriteDlcStart(RandomizerOptions opt, List<ItemKey> carePackageItems)
        {
#if ER
            int baseNewMsg = 36000500;
            int addMsg(Text text, params object[] args)
            {
                int msgId = baseNewMsg++;
                game.WriteFMGs = true;
                messages.SetFMGEntry(game, FMGCategory.Menu, "EventTextForTalk", msgId, text, args);
                return msgId;
            }
            int addEventMsg(Text text, params object[] args)
            {
                int msgId = baseNewMsg++;
                game.WriteFMGs = true;
                messages.SetFMGEntry(game, FMGCategory.Menu, "EventTextForMap", msgId, text, args);
                return msgId;
            }
            // Make all item lists
            List<StartMenu> menus = new();
            bool shopEnabled = opt["dlcshop"];
            if (shopEnabled)
            {
                menus.Add(new StartMenu { Action = ActionType.Shop, ID = "Shop", Name = "Base game shop", MsgID = addMsg(startShop) });
            }
            // "Reallocate attributes", but just use base game name
            menus.Add(new StartMenu { Action = ActionType.Rebirth, Name = "Rebirth", MsgID = 22000000 });
            if (opt["quickstart"])
            {
                menus.Add(new StartMenu { Action = ActionType.WarpBase, Name = "Enter base game", MsgID = addMsg(startEnterBase) });
            }
            menus.Add(new StartMenu { Action = ActionType.Warp, Name = "Enter DLC", MsgID = addMsg(startEnter) });
            if (shopEnabled)
            {
                // Could make the shops anyway even if they're not enabled
                foreach ((ShopCat cat, List<ItemKey> items) in shopItems)
                {
                    if (cat == ShopCat.None) continue;
                    if (!itemCatNames.TryGetValue(cat, out Text text)) throw new Exception($"Internal error: no name defined for {cat}");
                    if (cat == ShopCat.Weapon)
                    {
                        StartMenu parent = new StartMenu { Action = ActionType.Shop, ParentID = "Shop", Type = cat, Name = text.Str, MsgID = addMsg(text) };
                        menus.Add(parent);
                        foreach ((int type, List<ItemKey> weps) in weapons)
                        {
                            if (!weaponTypeIds.TryGetValue(type, out int textId)) throw new Exception($"Error: no name defined for {type}");
                            // Checked to exist
                            string wepName = game.MenuFMGs["GR_MenuText"][textId];
                            int wepMsgId = addMsg(IdentityMessage, FMGArg.Menu("GR_MenuText", textId, wepName));
                            List<ItemKey> baseItems = weps.Where(i => !game.IsEldenDlcItem(i)).ToList();
                            if (baseItems.Count == 0) continue;
                            StartMenu shop = new StartMenu { Action = ActionType.Shop, Type = cat, Name = wepName, ParentID = text.Str, Items = baseItems, MsgID = wepMsgId };
                            menus.Add(shop);
                        }
                    }
                    else
                    {
                        List<ItemKey> baseItems = items.Where(i => !game.IsEldenDlcItem(i)).ToList();
                        if (baseItems.Count == 0) continue;
                        StartMenu shop = new StartMenu { Action = ActionType.Shop, ParentID = "Shop", Type = cat, Name = text.Str, Items = baseItems, MsgID = addMsg(text) };
                        menus.Add(shop);
                    }
                }

                // Populate all shops
                int baseShop = 651260000;
                Dictionary<ItemKey, int> minPrice = new();
                HashSet<ItemType> priceTypes = new();
                PARAM.Row createNewShop(int shopId, ItemKey baseItem, ItemKey item, int sellPrice, uint flag)
                {
                    PARAM.Row row = GameEditor.AddRow(game.Params["ShopLineupParam"], shopId);
                    row["equipId"].Value = item.ID;
                    // Can add more options for this, but just do free shop for now
                    sellPrice = 0;
                    row["value"].Value = sellPrice;
                    minPrice[baseItem] = minPrice.TryGetValue(baseItem, out int existPrice) ? Math.Min(existPrice, sellPrice) : sellPrice;
                    priceTypes.Add(baseItem.Type);
                    row["equipType"].Value = (byte)item.Type;
                    if (flag == 0)
                    {
                        row["sellQuantity"].Value = (short)-1;
                    }
                    else
                    {
                        row["eventFlag_forStock"].Value = flag;
                        row["sellQuantity"].Value = (short)1;
                    }
                    return row;
                }
                foreach (StartMenu shop in menus)
                {
                    int shopId = baseShop;
                    int defaultCost = 0;
                    if (shop.Items == null) continue;
                    foreach (ItemKey item in shop.Items)
                    {
                        int cost = defaultCost;
                        ItemKey add = item;
                        if (item.Type == ItemType.Weapon && opt["maxweapon"])
                        {
                            add = game.AutoUpgrade(add, 25);
                        }
                        if (permanentFlags.TryGetValue(item, out List<int> flags) && flags.Count > 0)
                        {
                            foreach (int flag in flags)
                            {
                                createNewShop(shopId++, item, add, cost, (uint)flag);
                            }
                        }
                        else if (!flagRequiredItems.Contains(shop.Type))
                        {
                            createNewShop(shopId++, item, add, cost, 0);
                        }
                    }
                    if (baseShop != shopId)
                    {
                        shop.Start = baseShop;
                        shop.End = shopId;
                        baseShop += 1000;
                    }
                }
                foreach (ItemType type in priceTypes)
                {
                    if (type == ItemType.Custom) continue;
                    foreach (PARAM.Row row in game.ItemParam(type).Rows)
                    {
                        ItemKey key = new ItemKey(type, row.ID);
                        if (minPrice.TryGetValue(key, out int price))
                        {
                            int currentPrice = (int)row["sellValue"].Value;
                            if (price < currentPrice)
                            {
                                row["sellValue"].Value = price;
                            }
                        }
                    }
                }
            }

            // Install the menu - ESD
            int warpFlag = GameData.EldenRingBase + 2040;
            int warpBaseFlag = GameData.EldenRingBase + 2041;
            // Already-started flags: 101, 60000
            // Used for highlighting menu option and roundtable unlock
            int warpedFlag = GameData.EldenRingBase + 20;
            // Used for one-time init guard (but it also checks 101)
            int initFlag = GameData.EldenRingBase + 21;
            // Used if player has level-up runes
            int runesFlag = GameData.EldenRingBase + 22;
            // Called if warp was just used, for time changes to stick. idk why stranded graveyard doesn't work
            int warpTimeFlag = GameData.EldenRingBase + 23;

            ESD.CommandCall giveItem(ItemKey key, int quantity)
            {
                // 1_52 PlayerEquipmentQuantityChange(3, goodsId, amt)
                return AST.MakeCommand(1, 52, (int)key.Type, key.ID, quantity);
            }

            ESD grace = game.Talk["m00_00_00_00"]["t000001000"];
            game.WriteESDs.Add("m00_00_00_00");
            // "Memorize spell" msg
            List<long> graceMachineIds = ESDEdits.FindMachinesWithTalkData(grace, 15000390);
            if (graceMachineIds.Count != 1)
            {
                throw new Exception($"Can't edit grace menu: 'Memorize spell' message not found, or found multiple times (machines: [{string.Join(", ", graceMachineIds.Select(AST.FormatMachine))}])");
            }
            Dictionary<long, ESD.State> graceMachine = grace.StateGroups[graceMachineIds[0]];
            int msgId = addMsg(startMenu);
            ESDEdits.CustomTalkData data = new ESDEdits.CustomTalkData
            {
                LeaveMsg = 20000009,
                Msg = msgId,
                ConsistentID = 72,
                HighlightCondition = AST.NegateCond(EventFlagExpr(warpedFlag)),
                // Limit initial levelup starting grace, add special roundtable invite condition
                // Condition = getEventFlag(11102790),
            };
            ESDEdits.ModifyCustomTalkEntry(graceMachine, data, true, true, out long startShopId);
            if (!graceMachine.TryGetValue(startShopId, out ESD.State startShopState)) throw new Exception("Could not add shop to grace menu");
            long loopId = (long)startShopState.Conditions[0].TargetState;
            startShopState.Conditions.Clear();
            void makeShop(StartMenu menu, ESD.State menuState, long returnId)
            {
                long refId = 0;
                if (menu.Action == ActionType.Shop && shopEnabled)
                {
                    if (menu.Start <= 0)
                    {
                        throw new Exception($"Internal error: bad shop {menu.Name}");
                    }
                    menuState.EntryCommands.Add(AST.MakeCommand(1, 22, menu.Start, menu.End));
                    // f59 f58 assert CheckSpecificPersonMenuIsOpen(5, 0) == 0 or CheckSpecificPersonGenericDialogIsOpen(0)
                    AST.Expr waitExpr = ESDEdits.MenuCloseExpr(5);
                    // f103 GetCurrentStateElapsedTime
                    // AST.Expr waitExpr = new AST.BinaryExpr { Op = "#>", Lhs = AST.MakeFunction("f103"), Rhs = AST.MakeVal(10) };
                    menuState.Conditions.Add(new ESD.Condition(returnId, AST.AssembleExpression(waitExpr)));
                }
                else if (menu.Action == ActionType.Rebirth)
                {
                    // Check larval tear
                    ItemKey tear1 = new ItemKey(ItemType.Goods, 8185);
                    ItemKey tear2 = new ItemKey(ItemType.Goods, 2008033);
                    AST.Expr has1 = HasItemExpr(tear1, ESDEdits.ComparisonType.Greater, 0);
                    AST.Expr has2 = HasItemExpr(tear2, ESDEdits.ComparisonType.Greater, 0);
                    AST.Expr itemCond = AST.Binop(has1, "||", has2);
                    (ESD.State haveState, ESD.State notHaveState) = AST.SimpleBranch(graceMachine, menuState, itemCond, ref refId);
                    // action:22001002:"A Larval Tear is required for rebirth"
                    ESDEdits.ShowDialog(notHaveState, returnId, 22001002);
                    // action:22001000:"Use a Larval Tear to accept rebirth?"
                    (long confirmId, ESD.State confirmState) = AST.AllocateState(graceMachine, ref refId);
                    ESDEdits.ShowConfirmationDialog(haveState, confirmId, 22001000);
                    (ESD.State yesState, ESD.State noState) = AST.SimpleBranch(graceMachine, confirmState, ESDEdits.CheckDialogResult(1), ref refId);
                    // Go back
                    AST.CallState(noState, returnId);
                    // Continue
                    (long cleanupId, ESD.State cleanupState) = AST.AllocateState(graceMachine, ref refId);
                    // ReallocateAttributes
                    yesState.EntryCommands.Add(AST.MakeCommand(1, 113));
                    // ClearTalkActionState, just in case
                    yesState.EntryCommands.Add(AST.MakeCommand(1, 35));
                    yesState.Conditions.Add(new ESD.Condition(cleanupId, AST.AssembleExpression(ESDEdits.MenuCloseExpr(19))));
                    game.ESDOptionStates.Add(yesState);
                    // Rebirthed or not: DidYouDoSomethingInTheMenu(2) == 1
                    (ESD.State rebirthState, ESD.State noRebirthState) = AST.SimpleBranch(graceMachine, cleanupState, AST.Binop(AST.MakeFunction("f28", 2), "==", 1), ref refId);
                    // action:22001001:"Since you weren't reborn with new attributes,\nthe Larval Tear was not spent"
                    ESDEdits.ShowDialog(noRebirthState, returnId, 22001001);
                    (ESD.State remove1, ESD.State remove2) = AST.SimpleBranch(graceMachine, rebirthState, has1, ref refId);
                    remove1.EntryCommands.Add(giveItem(tear1, -1));
                    remove2.EntryCommands.Add(giveItem(tear2, -1));
                    AST.CallState(remove1, returnId);
                    AST.CallState(remove2, returnId);
                }
                else if (menu.Action == ActionType.Warp || menu.Action == ActionType.WarpBase)
                {
                    (long confirmId, ESD.State confirmState) = AST.AllocateState(graceMachine, ref refId);
                    int confirmMsg = addMsg(menu.Action == ActionType.Warp ? startEnterConfirm : startEnterConfirmBase);
                    ESDEdits.ShowConfirmationDialog(menuState, confirmId, confirmMsg);
                    (ESD.State yesState, ESD.State noState) = AST.SimpleBranch(graceMachine, confirmState, ESDEdits.CheckDialogResult(1), ref refId);
                    AST.CallState(noState, returnId);
                    // 1_47 ChangePlayerStat(stat, op, amt)
                    yesState.EntryCommands.Add(AST.MakeCommand(1, 47, (int)ESDEdits.EldenStat.Runes, (int)ESDEdits.ChangeType.Subtract, 999999999));
                    yesState.EntryCommands.Add(AST.MakeCommand(1, 11, runesFlag, 0));
                    int actFlag = menu.Action == ActionType.Warp ? warpFlag : warpBaseFlag;
                    yesState.EntryCommands.Add(AST.MakeCommand(1, 11, actFlag, 1));
                    // f103 GetCurrentStateElapsedTime
                    AST.Expr waitExpr = new AST.BinaryExpr { Op = "#>", Lhs = AST.MakeFunction("f103"), Rhs = AST.MakeVal(1.5f) };
                    yesState.Conditions.Add(new ESD.Condition(returnId, AST.AssembleExpression(waitExpr)));
                }
                else
                {
                    // f103 GetCurrentStateElapsedTime
                    AST.Expr waitExpr = new AST.BinaryExpr { Op = "#>", Lhs = AST.MakeFunction("f103"), Rhs = AST.MakeVal(0.5f) };
                    menuState.Conditions.Add(new ESD.Condition(returnId, AST.AssembleExpression(waitExpr)));
                }
            }
            // State: Open doors to Enia, Progress Roderika quest
            ESDEdits.MakeMenuTree(graceMachine, startShopId, loopId, menus, str => throw new Exception($"Internal error: no localization for {str}"), makeShop);

            MiscMapEdits edits = new MiscMapEdits(game);
            edits.AddGrace(
                "m10_01_00_00", new Vector3(-26.904f, 39.146f, -175.466f), new Vector3(0, -120, 0),
                10001950, "AEG210_375_2000", "c0000_0000", "c0000_9010",
                GameData.EldenRingBase + 100, 10010, 9999, 10011953, 100103);

            // TODO: Switch to emedf and more organized initialization routine
            Events.AddSimpleEvent(game.Emevds["common"], 901718550, new List<EMEVD.Instruction>
            {
                // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, warpFlag)
                new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, warpFlag }),
                // SetEventFlag(TargetEventFlagType.EventFlag, warpFlag, OFF)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpFlag, (byte)0 }),
                // SetEventFlag(TargetEventFlagType.EventFlag, warpTimeFlag, ON)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpTimeFlag, (byte)1 }),
                // SetEventFlag(TargetEventFlagType.EventFlag, <flags>, ON)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 62002, (byte)1 }),
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 82002, (byte)1 }),
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpedFlag, (byte)1 }),
                // SetPlayerRespawnPoint(2046402020)
                new EMEVD.Instruction(2003, 23, new List<object> { 2046402020 }),
                // ShowTextOnLoadingScreen(Disabled)
                new EMEVD.Instruction(2003, 80, new List<object> { (byte)0 }),
                // WarpPlayer(61, 46, 40, 0, 2046402020, 120500)
                new EMEVD.Instruction(2003, 14, new List<object>{ (byte)61, (byte)46, (byte)40, (byte)0, 2046402020, 0 }),
            });
            // Alt version for base game
            Events.AddSimpleEvent(game.Emevds["common"], 901718559, new List<EMEVD.Instruction>
            {
                // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, warpBaseFlag)
                new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, warpBaseFlag }),
                // SetEventFlag(TargetEventFlagType.EventFlag, warpBaseFlag, OFF)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpBaseFlag, (byte)0 }),
                // SetEventFlag(TargetEventFlagType.EventFlag, warpTimeFlag, ON)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpTimeFlag, (byte)1 }),
                // SetEventFlag(TargetEventFlagType.EventFlag, <flags>, ON)
                // Don't bother with DLC map unlock yet
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpedFlag, (byte)1 }),
                // SetPlayerRespawnPoint(18002020)
                new EMEVD.Instruction(2003, 23, new List<object> { 18002020 }),
                // ShowTextOnLoadingScreen(Disabled)
                new EMEVD.Instruction(2003, 80, new List<object> { (byte)0 }),
                // WarpPlayer(18, 0, 0, 0, 18002020, 0)
                new EMEVD.Instruction(2003, 14, new List<object>{ (byte)18, (byte)0, (byte)0, (byte)0, 18002020, 0 }),
            });
            // Time hack
            Events.AddSimpleEvent(game.Emevds["common"], 901718558, new List<EMEVD.Instruction>
            {
                // EndIfEventFlag(EventEndType.End, OFF, TargetEventFlagType.EventFlag, warpTimeFlag)
                new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)0, (byte)0, warpTimeFlag }),
                // SetEventFlag(TargetEventFlagType.EventFlag, warpTimeFlag, OFF)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, warpTimeFlag, (byte)0 }),
                // FreezeTime(false)
                new EMEVD.Instruction(2001, 5, new List<object> { (byte)0 }),
                // SetCurrentTime(10, 30, 0, false, false, false, 0, 0, 0)
                new EMEVD.Instruction(2001, 4, new List<object> { (byte)10, (byte)30, (byte)0, (byte)0, (byte)0, (byte)0, 0f, 0f, 0f }),
            });
            // Consider the player to be in Liurnia, so we get both Melina and Roderika. Using dumpbonfire flag
            // Use 3062, but it gets unset if a Liurnia grace is not enabled, so use 76200 (Lake Shore)
            // Also unlock First Step for softlock avoidance, mainly if torrent is randomized
            // Other conditions: encountered Dancing Lion (20000800 -> 20000801), Rellana? (2048440800 -> 2048442805 fog entry)
            List<EMEVD.Instruction> roundtableInstrs = new();
            if (opt["dlcroundtable"])
            {
                // From common_roundtable
                roundtableInstrs.AddRange(new List<EMEVD.Instruction>
                {
                    // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, <flag>)
                    new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, warpedFlag }),
                    // SetEventFlag(TargetEventFlagType.EventFlag, <flag>, ON)
                    new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 10009655, (byte)1 }),
                    new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 71190, (byte)1 }),
                    new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 105, (byte)1 }),
                    // OpenWorldMapPoint(<id>, distance)
                    new EMEVD.Instruction(2003, 78, new List<object> { 111000, 100f }),
                });
            }
            else
            {
                // Gravesite Plain starts at 76800
                int startFlag = 76831;
                roundtableInstrs.AddRange(new List<EMEVD.Instruction>
                {
                    // IfBatchEventFlags(OR01, LogicalOperationType.NotAllOFF = 2, TargetEventFlagType.EventFlag, startFlag, endFlag)
                    new EMEVD.Instruction(3, 1, new List<object> { (sbyte)-1, (byte)2, (byte)0, startFlag, 76960 }),
                    // IfEventFlag(OR01, ON, TargetEventFlagType.EventFlag, bossFlag)
                    new EMEVD.Instruction(3, 0, new List<object> { (sbyte)-1, (byte)1, (byte)0, 20000801 }),
                    new EMEVD.Instruction(3, 0, new List<object> { (sbyte)-1, (byte)1, (byte)0, 2048442805 }),
                    // IfConditionGroup(MAIN, ON, OR01)
                    new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)-1 }),
                });
            }
            roundtableInstrs.AddRange(new List<EMEVD.Instruction>
            {
                // SetEventFlag(TargetEventFlagType.EventFlag, <flag>, ON)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 76101, (byte)1 }),
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 76200, (byte)1 }),
                // OpenWorldMapPoint(<id>, distance). Use Stranded Graveyard, Stormveil Castle. This shows the points
                new EMEVD.Instruction(2003, 78, new List<object> { 180000, 100f }),
                new EMEVD.Instruction(2003, 78, new List<object> { 100000, 550f }),
            });
            Events.AddSimpleEvent(game.Emevds["common"], 901718551, roundtableInstrs);
            // Finally, just to be mean, steal runes if outside of Chapel of Anticipation if rune flag is set
            PARAM.Row stealSp = game.AddRow("SpEffectParam", 901718552, 3280);
            stealSp["soul"].Value = -999999999;
            Events.AddSimpleEvent(game.Emevds["common"], 901718552, new List<EMEVD.Instruction>
            {
                // IfEventFlag(AND01, ON, TargetEventFlagType.EventFlag, runesFlag)
                new EMEVD.Instruction(3, 0, new List<object> { (sbyte)1, (byte)1, (byte)0, runesFlag }),
                // IfPlayerInoutMap(AND01, false, 10, 1, 0, 0)
                new EMEVD.Instruction(3, 8, new List<object> { (sbyte)1, (byte)0, (sbyte)10, (sbyte)1, (sbyte)0, (sbyte)0 }),
                // IfConditionGroup(MAIN, ON, AND01)
                new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)1 }),
                // SetSpEffect(20000, 901718552)
                new EMEVD.Instruction(2004, 8, new List<object> { 20000, 901718552 }),
                // SetEventFlag(TargetEventFlagType.EventFlag, runesFlag, OFF)
                new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, runesFlag, (byte)0 }),
            }, EMEVD.Event.RestBehaviorType.Restart);
            // This is a fixit event for unlocking Stranded Graveyard grace, among other things, but disable it here in dlcstart
            // Also time-setting event?
            game.Emevds["common"].Events.RemoveAll(e => e.ID == 6907);
            game.WriteEmevds.Add("common");
            // ESD fix for Melina, to remove DLC exclusion for it
            // Find t000001000_x84 in a future-proof way
            bool checkDlcGrace(AST.Expr cond)
            {
                bool check = false;
                cond.Visit(AST.AstVisitor.PreAct(expr =>
                {
                    // FunctionCall is a bit obscured with register setting
                    check |= expr is AST.BinaryExpr bin && bin.Op == "#>=" && bin.Rhs.TryAsInt(out int val) && val == 20000000;
                }));
                return check;
            }
            List<long> checkDlcMachines = ESDEdits.FindMachinesWithCondition(grace, checkDlcGrace);
            bool checkReturn1(ESD.CommandCall call)
            {
                return call.CommandBank == 7 && call.CommandID == -1 && call.Arguments.Count == 1
                    && AST.DisassembleExpression(call.Arguments[0]).TryAsInt(out int val) && val == 1;
            }
            List<long> return1Machines = ESDEdits.FindMachinesWithPassCommand(grace, checkReturn1);
            List<long> checkMelinaMachines = checkDlcMachines.Intersect(return1Machines).ToList();
            if (checkMelinaMachines.Count > 1) throw new Exception($"Can't edit grace menu for dlcstart: multiple DLC-checking functions found [{string.Join(", ", checkMelinaMachines.Select(AST.FormatMachine))}]");
            if (checkMelinaMachines.Count == 1)
            {
                Dictionary<long, ESD.State> checkMachine = grace.StateGroups[checkMelinaMachines[0]];
                // Make it all return 1
                void rewriteReturn(ESD.CommandCall call)
                {
                    if (call.CommandBank == 7 && call.CommandID == -1 && call.Arguments.Count == 1)
                    {
                        call.Arguments[0] = AST.AssembleExpression(AST.MakeVal(1));
                    }
                }
                void checkCond(ESD.Condition cond)
                {
                    cond.PassCommands.ForEach(rewriteReturn);
                    cond.Subconditions.ForEach(checkCond);
                }
                foreach (ESD.State state in checkMachine.Values)
                {
                    state.Conditions.ForEach(checkCond);
                }
            }

            // DLC start
            // Base game items are either: given automatically (removed from game), in shops. If in shops, placed anywhere (not important) or in DLC (important items)
            // Always automatic: Spirit Calling Bell, Spectral Steed Whistle, Crafting Kit, Flask of Wondrous Physick, Lantern, Whetblades, Bell Bearings + Sacred Flasks of course
            // Optionally automatic: Golden Seed, Sacred Tear, Memory Stone, Talisman Pouches
            // Two optional types of gifts: Shops (all base game equipment), Care packages (20 random pieces of base game equipment)

            List<InitialItem> initItems = new();
            // if (!opt.GetInt("flaskamt", 4, 14, out int flaskAmt)) flaskAmt = 4;
            // if (!opt.GetInt("flasklevel", 0, 12, out int flaskLevel)) flaskLevel = 0;
            int flaskAmt = opt["maxflasknum"] ? 14 : 4;
            int flaskLevel = opt["maxflask"] ? 12 : 0;
            ItemKey hpFlask = new ItemKey(ItemType.Goods, 1001 + flaskLevel * 2);
            ItemKey mpFlask = new ItemKey(ItemType.Goods, 1051 + flaskLevel * 2);
            // As a bit of a hack, this key is hardcoded to give mpFlask below as well, since they share an event flag (TODO: but event flag is not read?)
            initItems.Add(new InitialItem { Key = hpFlask, Quantity = flaskAmt - 1, EventFlags = new() { 60000 } });
            // initItems.Add(new InitialItem { Key = mpFlask, Quantity = 1, EventFlags = new() { 60000 } });
            List<ItemKey> startKeys = ann.ItemGroups["dlcstartitems"].ToList();
            foreach (string group in new[] { "maxpouch", "maxmemslot", "maxwhetblade" })
            {
                if (!ann.ItemGroups.TryGetValue(group, out List<ItemKey> groupKeys)) throw new Exception($"Missing configured group {group} for corresponding option");
                if (opt[group])
                {
                    startKeys.AddRange(groupKeys);
                }
            }
            foreach (ItemKey key in startKeys)
            {
                // By default give 1, unless there are multiple unique event flags
                InitialItem item = new InitialItem { Key = key, Quantity = 1 };
                if (permanentFlags.TryGetValue(key, out List<int> flags) && flags.Count > 0)
                {
                    item.Quantity = flags.Count;
                    item.EventFlags = flags;
                }
                initItems.Add(item);
            }
            foreach (ItemKey baseKey in carePackageItems)
            {
                ItemKey key = baseKey;
                if (opt["maxweapon"])
                {
                    key = game.AutoUpgrade(key, 25);
                }
                InitialItem item = new InitialItem { Key = key, Quantity = 1 };
                if (game.IsEldenArrow(key))
                {
                    // Just regular arrows here. Due to item limits don't do this for great arrows/bolts
                    item.Quantity = 50;
                }
                else if (permanentFlags.TryGetValue(key, out List<int> flags) && flags.Count > 0)
                {
                    item.Quantity = flags.Count;
                    item.EventFlags = flags;
                }
                initItems.Add(item);
            }

            bool clearDamage(ESD.CommandCall c)
            {
                return c.CommandID == 39 && c.CommandBank == 1;
            }
            List<long> startMachineIds = ESDEdits.FindMachinesWithCommand(grace, clearDamage);
            if (startMachineIds.Count != 1)
            {
                throw new Exception($"Can't edit grace menu: ClearPlayerDamageInfo not found, or found multiple times (machines: [{string.Join(", ", startMachineIds.Select(AST.FormatMachine))}])");
            }
            Dictionary<long, ESD.State> startMachine = grace.StateGroups[startMachineIds[0]];
            ESD.State startState = startMachine[0];
            long toState = AST.GetFollowState(startMachine, startState);
            // Player level - level 60, 90, 120, 150, 200
            // Items - Core items + Care package
            {
                long refId = 0;
                startState.Conditions.Clear();
                (long branchId, ESD.State branchState) = AST.AllocateState(startMachine, ref refId);
                AST.CallState(startState, branchId);
                // Allow init if not previous init and still in tutorial
                AST.Expr initCond = AST.Binop(EventFlagExpr(initFlag), "||", EventFlagExpr(101));
                (ESD.State initedState, ESD.State doInitState) = AST.SimpleBranch(startMachine, branchState, initCond, ref refId);
                AST.CallState(initedState, toState);

                // Set flask allocation in initialization. But check the player doesn't have it first by flag, to make sure we're not getting rid of flasks
                {
                    (ESD.State flaskState, ESD.State noFlaskState) = AST.SimpleBranch(startMachine, doInitState, EventFlagExpr(60000), ref refId);
                    if (flaskAmt > 4)
                    {
                        // 1_108 EstusAllocationUpdate(GetEstusAllocation(0) + 1, 0)
                        noFlaskState.EntryCommands.Add(AST.MakeCommand(1, 108, flaskAmt - 1, 0));
                    }
                    if (flaskLevel > 0) {
                        // Bonfire level (f110) of 1 is default, 2 is +0, then 14 is +12
                        // 1_109 BonfireActivation
                        noFlaskState.EntryCommands.Add(AST.MakeCommand(1, 109, 2 + flaskLevel));
                    }
                    (long nextId, doInitState) = AST.AllocateState(startMachine, ref refId);
                    AST.CallState(flaskState, nextId);
                    AST.CallState(noFlaskState, nextId);
                }
                // Give cash for leveling
                if (opt.GetInt(RandomizerOptions.IntOpt.PlayerLevel, out int targetLevel) && targetLevel > 1)
                {
                    AST.Expr getStat(ESDEdits.EldenStat stat) => AST.MakeFunction("f104", (int)stat);
                    // AST.Expr isLower = AST.Binop(getStat(ESDEdits.EldenStat.RuneLevel), "#<", targetLevel);
                    // f45 ComparePlayerStat(stat, compare, amt)
                    AST.Expr isLower = AST.MakeFunction("f45", (int)ESDEdits.EldenStat.RuneLevel, (int)ESDEdits.ComparisonType.Less, targetLevel);
                    AST.Expr noCash = AST.MakeFunction("f45", (int)ESDEdits.EldenStat.Runes, (int)ESDEdits.ComparisonType.Equal, 0);
                    (ESD.State noSoulState, ESD.State soulState) = AST.SimpleBranch(startMachine, doInitState, AST.Binop(isLower, "&&", noCash), ref refId);
                    // f105 GetLevelUpSoulCost(currentLevel, newLevel)
                    AST.Expr cost = AST.MakeFunction("f105", getStat(ESDEdits.EldenStat.RuneLevel), targetLevel);
                    // 1_47 ChangePlayerStat(stat, op, amt)
                    noSoulState.EntryCommands.Add(AST.MakeCommand(1, 47, (int)ESDEdits.EldenStat.Runes, (int)ESDEdits.ChangeType.Add, cost));
                    noSoulState.EntryCommands.Add(AST.MakeCommand(1, 11, runesFlag, 1));
                    (long nextId, doInitState) = AST.AllocateState(startMachine, ref refId);
                    AST.CallState(noSoulState, nextId);
                    AST.CallState(soulState, nextId);
                }
                game.ESDOptionStates.Add(doInitState);
                // Give items
                foreach (InitialItem item in initItems)
                {
                    // Could also check flag here, but fine to backfill the item itself if the flags are set I guess
                    AST.Expr cmp = HasItemExpr(item.Key, ESDEdits.ComparisonType.Equal, 0);
                    if (item.Key.Equals(hpFlask))
                    {
                        cmp = AST.Binop(cmp, "&&", HasItemExpr(mpFlask, ESDEdits.ComparisonType.Equal, 0));
                    }
                    (ESD.State noItemState, ESD.State itemState) = AST.SimpleBranch(startMachine, doInitState, cmp, ref refId);
                    // 1_52 PlayerEquipmentQuantityChange(3, goodsId, amt)
                    noItemState.EntryCommands.Add(giveItem(item.Key, item.Quantity));
                    if (item.Key.Equals(hpFlask))
                    {
                        noItemState.EntryCommands.Add(giveItem(mpFlask, 1));
                    }
                    foreach (int itemFlag in item.EventFlags ?? new())
                    {
                        // SetEventFlag
                        noItemState.EntryCommands.Add(AST.MakeCommand(1, 11, itemFlag, 1));
                    }
                    (long nextId, doInitState) = AST.AllocateState(startMachine, ref refId);
                    AST.CallState(noItemState, nextId);
                    AST.CallState(itemState, nextId);
                }
                // Set other flags
                {
                    // Skip tutorial, auto-accept Melina's offer, open roundtable door
                    foreach (int flag in new[] { 101, 102, 951, 953, 4680, 4681, 10010030, 18000020, 18000021, 11108548 })
                    {
                        doInitState.EntryCommands.Add(AST.MakeCommand(1, 11, flag, 1));
                    }
                    // Bell Bearing flags
                    if (opt["maxbb"])
                    {
                        for (int flag = 11109751; flag <= 11109765; flag++)
                        {
                            doInitState.EntryCommands.Add(AST.MakeCommand(1, 11, flag, 1));
                        }
                    }
                }
                // Set init flag
                doInitState.EntryCommands.Add(AST.MakeCommand(1, 11, initFlag, 1));
                AST.CallState(doInitState, toState);
            }

            // Remembrance shop in Roundtable
            ESD enia = game.Talk["m11_10_00_00"]["t102001110"];
            ESDEdits.ForEachCondition(enia, cond =>
            {
                bool changed = false;
                AST.Expr rewriteFlag(AST.Expr expr)
                {
                    // AST.MakeFunction("f15", flag)
                    // Use 9104 (Morgott)
                    if (expr is AST.FunctionCall f && f.Name == "f15" && f.Args.Count > 0 && f.Args[0].TryAsInt(out int flag) && flag == 9104)
                    {
                        changed = true;
                        return EventFlagExpr(6001);
                    }
                    return null;
                }
                AST.Expr expr = AST.DisassembleExpression(cond.Evaluator);
                expr = expr.Visit(AST.AstVisitor.Post(rewriteFlag));
                if (changed)
                {
                    cond.Evaluator = AST.AssembleExpression(expr);
                    game.WriteESDs.Add("m11_10_00_00");
                }
            });

            MSBE chapelMsb = game.EldenMaps["m10_01_00_00"];
            game.WriteMSBs.Add("m10_01_00_00");
            // Close door 10011540 and make it unscriptable. Finger should still be acquirable.
            MSBE.Part.Asset door = chapelMsb.Parts.Assets.Find(a => a.Name == "AEG219_002_0500");
            // 9980, 10001690
            int doorMsgId = addEventMsg(startDoor);
            if (door != null)
            {
                door.EntityID = 901718560;
                // Also because people are blind
                // Door uses 7200, event based on 10002690
                // TODO: Common utility
                Events.AddSimpleEvent(game.Emevds["m10_01_00_00"], 901718560, new[]
                {
                    "SetNetworkSyncState(Disabled)",
                    "IfEventFlag(AND_01, OFF, TargetEventFlagType.EventFlag, 10018540)",
                    "IfActionButtonInArea(AND_01, 7200, 901718560)",
                    "IfConditionGroup(MAIN, PASS, AND_01)",
                    $"DisplayGenericDialog({doorMsgId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, 901718560, 3)",
                    "WaitFixedTimeSeconds(1)",
                    "EndUnconditionally(EventEndType.Restart)",
                }.Select(events.ParseAdd), EMEVD.Event.RestBehaviorType.Restart);
                game.WriteEmevds.Add("m10_01_00_00");
            }
            chapelMsb.Events.ObjActs.RemoveAll(oa => oa.ObjActPartName == "AEG219_002_0500");
        }

        public static void WriteUpgrade(Messages messages, GameData game)
        {
            // Just do this here since it has a lot in common with other edits
            ESD grace = game.Talk["m00_00_00_00"]["t000001000"];
            // Scadutree update is in x75: if <= 20, calls x81 with (1, 2, 3, [work val])
            // Revered update is in x76: if <= 10, calls x77 with (1, 1, 1, 2, 2, 3, 3, 3, 4, 5, [work val], 1)
            // But just remove confirmations for now tbh, in the presence of other alternate logic
            long confirmMachine = -1;
            ESDEdits.ForEachCommand(grace, c =>
            {
                // Used statically for "Begin Journey <?nextLoopCount?>?"
                // if (c.CommandBank == 6) Console.WriteLine($"{AST.FormatMachine(c.CommandID)}: {string.Join(", ", c.Arguments.Select(AST.DisassembleExpression))}");
                if (c.CommandBank == 6 && c.Arguments.Count == 1
                    && AST.DisassembleExpression(c.Arguments[0]).TryAsInt(out int val) && val == 20011080)
                {
                    confirmMachine = c.CommandID;
                }
            });
            int emptyId = AST.MachineForIndex(510);
            if (confirmMachine == -1 && grace.StateGroups.ContainsKey(emptyId))
            {
                return;
            }
            Dictionary<long, ESD.State> emptyMachine = new Dictionary<long, ESD.State>();
            ESD.State waitState = emptyMachine[0] = new ESD.State();
            ESD.State endState = emptyMachine[1] = new ESD.State();
            // f102 GetCurrentStateElapsedFrames
            waitState.Conditions.Add(new ESD.Condition(1, AST.AssembleExpression(AST.MakeFunction("f102", 1))));
            AST.CallReturn(endState, 0);
            grace.StateGroups[emptyId] = emptyMachine;
            foreach ((long machineId, Dictionary<long, ESD.State> states) in grace.StateGroups)
            {
                bool inMenu = false;
                foreach ((long stateId, ESD.State state) in states)
                {
                    ESD.CommandCall confirmCall = state.EntryCommands.Find(
                        c => c.CommandBank == 6 && c.CommandID == confirmMachine && c.Arguments.Count == 1);
                    if (confirmCall == null) continue;
                    AST.Expr actionExpr = AST.DisassembleExpression(confirmCall.Arguments[0]);
                    if (!(actionExpr is AST.BinaryExpr bin && bin.Lhs.TryAsInt(out int actionId))) continue;
                    // Sacred Tear, Golden Seed
                    if (actionId == 20011000 || actionId == 20011010)
                    {
                        // Just remove confirm
                    }
                    else if (actionId == 20011039 || actionId == 20011049)
                    {
                        // Also remove post-confirm
                        inMenu = true;
                    }
                    else continue;
                    confirmCall.CommandID = emptyId;
                }
                if (!inMenu) continue;
                foreach ((long stateId, ESD.State state) in states)
                {
                    ESD.CommandCall confirmCall = state.EntryCommands.Find(
                        c => c.CommandBank == 6 && c.Arguments.Count == 1
                            && AST.DisassembleExpression(c.Arguments[0]).TryAsInt(out int textId)
                            && (textId == 20011062 || textId == 20011063));
                    if (confirmCall == null) continue;
                    confirmCall.CommandID = emptyId;
                }
            }
            game.WriteESDs.Add("m00_00_00_00");
            if (grace == null)
            {
                // "Memorize spell" msg
                List<long> graceMachineIds = ESDEdits.FindMachinesWithTalkData(grace, 20010004);
                if (graceMachineIds.Count != 1)
                {
                    // throw new Exception($"Can't edit grace menu: 'Memorize spell' message not found, or found multiple times (machines: [{string.Join(", ", graceMachineIds.Select(AST.FormatMachine))}])");
                    // Just don't do it in this case
                    return;
                }
                Dictionary<long, ESD.State> graceMachine = grace.StateGroups[graceMachineIds[0]];
                // Alternate approach
                int baseNewMsg = 37000500;
                int addMsg(Text text, params object[] args)
                {
                    int msgId = baseNewMsg++;
                    game.WriteFMGs = true;
                    messages.SetFMGEntry(game, FMGCategory.Menu, "EventTextForTalk", msgId, text, args);
                    return msgId;
                }
                int msgId = 0; // addMsg(upgradeScadu);
                // TODO: We have to do the math ourselves. Nevermind
                // f237() <= 20 && ComparePlayerInventoryNumber(3, goods3, 4, GetWorkValue(2), 0) == 1
                ESDEdits.CustomTalkData data = new ESDEdits.CustomTalkData
                {
                    LeaveMsg = 20010004,
                    Msg = msgId,
                    ConsistentID = 55,
                    // No HighlightCondition, don't handle if upgrade not eligible
                    // Condition = getEventFlag(11102790),
                };
                ESDEdits.ModifyCustomTalkEntry(graceMachine, data, true, true, out long startShopId);
                if (!graceMachine.TryGetValue(startShopId, out ESD.State startShopState)) throw new Exception("Could not add shop to grace menu");
                long loopId = (long)startShopState.Conditions[0].TargetState;
                startShopState.Conditions.Clear();
            }
#endif
        }

        public void SetSpecialOutfits(RandomizerOptions opt, EnemyRandomizer.Result enemyLocs)
        {
            if (!game.EldenRing || enemyLocs.Outfit == null) return;
            if (opt["testoutfit"])
            {
                List<string> outfits = enemyLocs.Outfit.Split('|').Skip(0).ToList();
                for (int id = 0; id < Math.Min(10, outfits.Count); id++)
                {
                    string outfit = outfits[id];
                    List<ItemKey> items = ParseOutfit(outfit);
                    PARAM.Row row = game.Params["CharaInitParam"][3000 + id];
                    for (int i = 0; i < 4; i++)
                    {
                        ItemKey item = items[i];
                        row[g.ArmorSlots[i]].Value = item == null ? -1 : item.ID;
                    }
                }
                return;
            }
            try
            {
                List<ItemKey> items = ParseOutfit(enemyLocs.Outfit);
                for (int chrId = 23241; chrId <= 23248; chrId++)
                {
                    PARAM.Row row = game.Params["CharaInitParam"][chrId];
                    for (int i = 0; i < 4; i++)
                    {
                        ItemKey item = items[i];
                        row[g.ArmorSlots[i]].Value = item == null ? -1 : item.ID;
                        // Silver Tear Mask. Is this necessary
                        if (item?.ID == 1090000) break;
                    }
                }
            }
#if !DEBUG
            catch (Exception e)
            {
                // It's really not that important
                return;
            }
#endif
            finally { }
        }

        internal string PrintOutfit(int chara)
        {
            PARAM.Row row = game.Params["CharaInitParam"][chara];
            List<string> ret = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                int id = (int)row[g.ArmorSlots[i]].Value;
                ret.Add(id == -1 ? "None" : game.Name(new ItemKey(ItemType.Protector, id)));
            }
            return string.Join("; ", ret);
        }

        internal List<ItemKey> ParseOutfit(string outfit)
        {
            string[] parts = outfit.Split(';');
            if (parts.Length != 4) throw new ArgumentException($"Invalid outfit specification: {outfit}");
            List<ItemKey> ret = new List<ItemKey>();
            for (int i = 0; i < 4; i++)
            {
                string name = parts[i].Trim();
                if (name == "None")
                {
                    ret.Add(null);
                    continue;
                }
                ItemKey armor = game.ItemForName(name);
                if (armor.Type != ItemType.Protector) throw new ArgumentException($"Invalid outfit specification contains non-armor: {outfit} index {i}");
                PARAM.Row item = game.Item(armor);
                if (item == null) throw new ArgumentException($"Armor {armor} ({name}) missing from params");
                if ((byte)item[g.ArmorTypes[i]].Value != 1 && armor.ID % 1000 != i * 100)
                {
                    throw new ArgumentException($"Armor {armor} ({name}) is an invalid {g.ArmorTypes[i]}");
                }
                ret.Add(armor);
            }
            return ret;
        }

        private static string SplitCharacterText(bool useSpaces, List<string> items)
        {
            int lineCount = 3;
            int sizeLimit = 250;
            // Pick some generic serif font to approximate Garamond
            Font f = new Font("Times New Roman", 12);
            // Hardcode this for the time being
            string delimeter = useSpaces ? ", " : "，";
            List<string> committed = new List<string>();
            if (MeasureText == null)
            {
                throw new Exception("Replace winforms :'(");
            }
            foreach (string item in items)
            {
                List<string> cand = committed.ToList();
                if (cand.Count == 0)
                {
                    cand.Add("");
                }
                else
                {
                    // This adds a space, trim it later if it matters
                    cand[cand.Count - 1] += delimeter;
                }
                // Japanese, Chinese, and Thai lack ascii spaces
                // Otherwise, add one word at a time, also measuring the space before.
                foreach (string token in item.Split(' ').Select((s, i) => (i == 0 ? "" : " ") + s))
                {
                    string lastLine = cand[cand.Count - 1];
                    string addedLine = (lastLine + token).Trim(' ');
                    if (MeasureText(addedLine, f) < sizeLimit)
                    {
                        cand[cand.Count - 1] = addedLine;
                    }
                    else
                    {
                        cand.Add(token.Trim(' '));
                    }
                }
                if (cand.Count > lineCount)
                {
                    break;
                }
                committed = cand;
            }
            if (committed.Count == 0)
            {
                // Make each item a line, hope it goes well
                committed = items.Take(lineCount).ToList();
            }
            return string.Join("\n", committed.Select(t => t.Trim(' ')));
        }

#if WINFORMS
        internal void MeasureText()
        {
            List<int> lineLens = new List<int>();
            List<int> lineSizes = new List<int>();
            Font f = new Font("Times New Roman", 12);
            Console.WriteLine(f.Name + " " + f.FontFamily);
            foreach (CharacterClass c in g.Classes)
            {
                string[] lines = game.MenuFMGs["GR_LineHelp"][c.Desc].Split('\n');
                foreach (string line in lines)
                {
                    // Can use character count as a backup, but the measuring methods don't seem to throw exceptions
                    lineLens.Add(line.Length);
                    // https://stackoverflow.com/questions/6704923/textrenderer-measuretext-and-graphics-measurestring-mismatch-in-size
                    // We're using winforms anyway, this just needs to be approximately correct
                    int size = TextRenderer.MeasureText(line, f).Width;
                    lineSizes.Add(size);
                }
            }
            foreach (string line in new[] { "", " ", "some text", " some text" })
            {
                int size = TextRenderer.MeasureText(line, f).Width;
                Console.WriteLine($"[{line}] = {size}");
            }
            Console.WriteLine($"Test: {SplitCharacterText(true, Enumerable.Repeat("Some Kind of Item Text", 20).ToList())}");
            Console.WriteLine($"Lens: {string.Join(",", lineLens.OrderBy(x => x))}");
            Console.WriteLine($"Size: {string.Join(",", lineSizes.OrderBy(x => x))}");
        }
#endif
    }
}