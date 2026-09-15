using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;
using YamlDotNet.Serialization;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class GameData
    {
        public static readonly ISerializer Serializer = new SerializerBuilder()
            .DisableAliases()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();
        // Permanent flags
        // 0010: Rold event id (obsolete)
        // 0011: Auto-Rold lot event flag (obsolete)
        // 0020: warped to dlc
        // 0021: dlc inited items + levels etc
        // 0022: currently doing dlc init
        // 0030: O Mother replacement event flag (for DLC)
        // 0100: bonfire flags (1 currently)
        // 0200: logic gate flags (<20 currently)
        // Temporary flags
        // 2001: param flag
        // 2010: old rold flag
        // 2020 2021 2022: sending gate flags
        // 2030 2031 2032: tibia mariner random flags
        // 2040: dlc warp flag
        // 2050: event value for item count check
        // 2100 up: enemy randomizer
        public static readonly int EldenRingBase = 1032500000;

        public readonly GameEditor Editor;
        public FromGame Type => Editor.Spec.Game;
        public bool Sekiro => Type == FromGame.SDT;
        public bool DS3 => Type == FromGame.DS3;
        public bool EldenRing => Type == FromGame.ER;
        public bool AC6 => Type == FromGame.AC6;
        public bool DS1 => Type == FromGame.DS1R;
        public bool Nightreign => Type == FromGame.NR;
        public bool HasMods => Mods != null && Mods.Count > 0;

        public readonly string Dir;
        private MergedMods Mods { get; set; }

        // Informational data
        // TODO: Perhaps have this data in configs
        private static readonly Dictionary<string, string> DS3LocationNames = new Dictionary<string, string>
        {
            { "m30_00_00_00", "highwall" },
            { "m30_01_00_00", "lothric" },
            { "m34_01_00_00", "archives" },
            { "m31_00_00_00", "settlement" },
            { "m32_00_00_00", "archdragon" },
            { "m33_00_00_00", "farronkeep" },
            { "m35_00_00_00", "cathedral" },
            { "m37_00_00_00", "irithyll" },
            { "m38_00_00_00", "catacombs" },
            { "m39_00_00_00", "dungeon" },
            { "m40_00_00_00", "firelink" },
            { "m41_00_00_00", "kiln" },
            { "m45_00_00_00", "ariandel" },
            { "m50_00_00_00", "dregheap" },
            { "m51_00_00_00", "ringedcity" },
            { "m51_01_00_00", "filianore" },
        };
        private static Dictionary<string, string> DS3MapNames = new Dictionary<string, string>
        {
            { "", "Global" },
            { "highwall", "High Wall" },
            { "lothric", "Lothric Castle" },
            { "archives", "Grand Archives" },
            { "settlement", "Undead Settlement" },
            { "archdragon", "Archdragon Peak" },
            { "farronkeep", "Farron Keep" },
            { "cathedral", "Cathedral" },
            { "irithyll", "Irithyll" },
            { "catacombs", "Catacombs" },
            { "dungeon", "Irithyll Dungeon" },
            { "firelink", "Firelink Shrine" },
            { "kiln", "Kiln" },
            { "ariandel", "Ariandel" },
            { "dregheap", "Dreg Heap" },
            { "ringedcity", "Ringed City" },
            { "filianore", "Filianore's Rest" },
            // Overriden names for more specific display for bosses
            { "cemetery", "Cemetery of Ash" },
            { "lake", "Smouldering Lake" },
            { "anorlondo", "Anor Londo" },
            { "profaned", "Profaned Capital" },
            { "garden", "Consumed King's Garden" },
            { "untended", "Untended Graves" },
        };
        private static readonly Dictionary<string, string> SekiroLocationNames = new Dictionary<string, string>
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
        private static Dictionary<string, string> SekiroMapNames = new Dictionary<string, string>
        {
            { "", "Global" },
            { "hirata", "Hirata Estate" },
            { "ashinaoutskirts", "Ashina Outskirts" },
            { "ashinacastle", "Ashina Castle" },
            { "ashinareservoir", "Ashina Reservoir" },
            { "dungeon", "Abandoned Dungeon" },
            { "mibuvillage", "Ashina Depths" },
            { "sunkenvalley", "Sunken Valley" },
            { "senpou", "Senpou Temple" },
            { "fountainhead", "Fountainhead Palace" },
        };
        private readonly static Dictionary<uint, ItemType> MaskItemTypes = new Dictionary<uint, ItemType>
        {
            [0x0000_0000] = ItemType.Weapon,
            [0x1000_0000] = ItemType.Protector,
            [0x2000_0000] = ItemType.Accessory,
            [0x4000_0000] = ItemType.Goods,
            [0x8000_0000] = ItemType.Gem,
        };
        private readonly static Dictionary<ItemType, uint> ItemTypeMasks = MaskItemTypes.ToDictionary(e => e.Value, e => e.Key);
        private readonly static Dictionary<uint, ItemType> ErLotItemTypes = new Dictionary<uint, ItemType>
        {
            [1] = ItemType.Goods,
            [2] = ItemType.Weapon,
            [3] = ItemType.Protector,
            [4] = ItemType.Accessory,
            [5] = ItemType.Gem,
            [6] = ItemType.Custom,
        };

        private static readonly Dictionary<string, string> eldenNeighborMaps = new Dictionary<string, string>
        {
            ["m60_45_32_00"] = "m60_44_32_00",
            ["m60_45_40_00"] = "m60_45_39_00",
            ["m60_46_37_00"] = "m60_45_37_00",
            // Diagonal map, Belurat stairs
            ["m61_44_43_00"] = "m61_45_42_00",
            // Rauh Base north cliffedge
            ["m61_47_48_00"] = "m61_47_47_00",
            ["m61_48_48_00"] = "m61_48_47_00",
            // West scaduview
            ["m61_48_49_00"] = "m61_49_49_00",
            // Fingerstone Hill north
            ["m61_51_49_00"] = "m61_52_48_00",
            ["m61_54_48_00"] = "m61_54_47_00",
            // Church District Highroad east
            ["m61_52_45_00"] = "m61_51_45_00",
            // Dheo
            ["m61_53_45_00"] = "m61_53_46_00",
            ["m61_54_46_00"] = "m61_53_46_00",
        };
        // echo $(ls | grep -E '_[1][0-2].msb') | sed -e 's/.msb[^ ]* /", "/g'
        // TODO: why not m60_45_36_10 edits
        private static readonly List<string> dupeMsbs = new List<string>
        {
            // Just be comprehensive here, even if files are not included in map dir
            "m60_11_09_12", "m60_11_13_12",
            "m60_21_20_11", "m60_22_18_11", "m60_22_19_11", "m60_22_26_11",
            "m60_22_27_11", "m60_23_18_11", "m60_23_19_11", "m60_23_21_11",
            "m60_23_26_11", "m60_23_27_11",
            "m60_44_36_10", "m60_44_37_10", "m60_44_38_10", "m60_44_39_10",
            "m60_44_52_10", "m60_44_53_10", "m60_44_54_10", "m60_44_55_10",
            "m60_45_36_10", "m60_45_37_10", "m60_45_38_10", "m60_45_39_10",
            "m60_45_52_10", "m60_45_53_10", "m60_45_54_10", "m60_45_55_10",
            "m60_46_36_10", "m60_46_37_10", "m60_46_38_10", "m60_46_39_10",
            "m60_46_52_10", "m60_46_53_10", "m60_46_54_10", "m60_46_55_10",
            "m60_47_36_10", "m60_47_37_10", "m60_47_38_10", "m60_47_39_10",
            "m60_47_52_10", "m60_47_53_10", "m60_47_54_10", "m60_47_55_10",
            "m61_11_11_12",
            "m61_22_22_11", "m61_22_23_11", "m61_23_22_11", "m61_23_23_11",
            "m61_44_44_10", "m61_44_45_10", "m61_44_46_10", "m61_44_47_10",
            "m61_45_44_10", "m61_45_45_10", "m61_45_46_10", "m61_45_47_10",
            "m61_46_44_10", "m61_46_45_10", "m61_46_46_10", "m61_46_47_10",
            "m61_47_44_10", "m61_47_45_10", "m61_47_46_10", "m61_47_47_10",
        };
        private Dictionary<string, string> MapDupes { get; set; }

        public Dictionary<string, string> Locations;
        public Dictionary<string, string> RevLocations;
        public Dictionary<string, string> LocationNames;
        public readonly Dictionary<uint, ItemType> LotItemTypes;
        public readonly Dictionary<ItemType, uint> LotValues;
        // Currently unused, as int/byte conversions with equipType are valid... currently.
        // TODO see if gem is sellable in Elden Ring.
        public readonly Dictionary<int, ItemType> ShopItemTypes = new Dictionary<int, ItemType>
        {
            [0] = ItemType.Weapon,
            [1] = ItemType.Protector,
            [2] = ItemType.Accessory,
            [3] = ItemType.Goods,
            // Only available in Elden Ring
            [4] = ItemType.Gem,
            [5] = ItemType.Custom,
        };
        public readonly Dictionary<ItemType, int> ShopTypeItems;
        public readonly Dictionary<string, string> NeighborMaps;

        private static readonly Dictionary<ItemType, string> itemParams = new()
        {
            [ItemType.Weapon] = "EquipParamWeapon",
            [ItemType.Protector] = "EquipParamProtector",
            [ItemType.Accessory] = "EquipParamAccessory",
            [ItemType.Goods] = "EquipParamGoods",
            [ItemType.Gem] = "EquipParamGem",
            [ItemType.Custom] = "EquipParamCustomWeapon",
        };

        // Actual data
        private Dictionary<string, PARAM.Layout> Layouts = new();
        private Dictionary<string, PARAMDEF> Defs = new();
        public ParamDictionary Params { get; private set; } = new();
        public Dictionary<string, EMEVD> Emevds { get; private set; } = new();
        public readonly string EnglishName;
        public FMGDictionary ItemFMGs => AllItemFMGs[EnglishName];
        public FMGDictionary MenuFMGs => AllMenuFMGs[EnglishName];
        public Dictionary<string, FMGDictionary> AllItemFMGs { get; private set; } = new();
        public Dictionary<string, FMGDictionary> AllMenuFMGs { get; private set; } = new();
        public Dictionary<string, Dictionary<string, ESD>> Talk { get; private set; } = new();
        public Dictionary<string, string> AI { get; private set; } = new();
#if DS1
        public Dictionary<string, Breakobj> Breakobjs = new();
#endif

        // Previously, Dictionary<string, IMsb> was used across the board, but as dictionaries cannot be covariant and IMsb by itself is not useful, use per-game only
        // TODO: Guard all MSB fields with preprocessor
        public IReadOnlyDictionary<string, MSB3> DS3Maps { get; private set; }
        public IReadOnlyDictionary<string, MSBS> SekiroMaps { get; private set; }
        public IReadOnlyDictionary<string, MSBE> EldenMaps { get; private set; }
#if AC6
        public IReadOnlyDictionary<string, MSBAC6> AC6Maps { get; private set; }
#endif
        public IReadOnlyDictionary<string, MSB1> DS1Maps { get; private set; }

        // Names
        public SortedDictionary<ItemKey, string> ItemNames = new();
        public SortedDictionary<string, List<ItemKey>> RevItemNames = new();
        public SortedDictionary<int, string> BonfireNames = new();
        public SortedDictionary<int, string> BonfireCats = new();
        public SortedDictionary<int, string> QwcNames = new();
        public SortedDictionary<string, string> ModelNames = new();
        public Submaps Submaps = new();

        private SortedDictionary<int, string> lotNames = new();
        private SortedDictionary<int, string> characterSplits = new();

        private List<string> writtenFiles = new();
        private List<string> unpackPaths = new();

        public GameData(string dir, FromGame game)
        {
            Dir = dir;
            Editor = new GameEditor(game);
            Editor.Spec.GameDir = $@"{dir}";
            Editor.Spec.NameDir = $@"{dir}\Names";
            Editor.Spec.LayoutDir = null;
            Editor.Spec.DefDir = $@"{dir}\Defs";
            EnglishName = DS1 ? "ENGLISH" : "engus";
            LotItemTypes = EldenRing ? ErLotItemTypes : MaskItemTypes;
            LotValues = LotItemTypes.ToDictionary(e => e.Value, e => e.Key);
            ShopTypeItems = ShopItemTypes.ToDictionary(e => e.Value, e => e.Key);
            NeighborMaps = EldenRing ? eldenNeighborMaps : new();
        }

        public void Load(MergedMods mods)
        {
            foreach (string dir in mods.Dirs)
            {
                Console.WriteLine($"Checking other mod directory {dir}");
            }
            Mods = mods;
            LoadNames();
            Stopwatch sw = null;
#if DEBUG
            // sw = Stopwatch.StartNew();
#endif
            LoadParams();
            if (sw != null) Console.WriteLine($"Read params in {sw.ElapsedMilliseconds} ms");
            sw?.Restart();
            LoadMapData();
            if (sw != null) Console.WriteLine($"Read maps in {sw.ElapsedMilliseconds} ms");
            sw?.Restart();
            LoadTalk();
            if (sw != null) Console.WriteLine($"Read ESDs in {sw.ElapsedMilliseconds} ms");
            sw?.Restart();
            LoadScripts();
            if (sw != null) Console.WriteLine($"Read scripts in {sw.ElapsedMilliseconds} ms");
            sw?.Restart();
            LoadText();
            if (sw != null) Console.WriteLine($"Read FMGs in {sw.ElapsedMilliseconds} ms");
        }

        public void UnDcx(string dir)
        {
            Directory.CreateDirectory($@"{dir}\dcx");
            foreach (string path in Directory.GetFiles(dir, "*.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                byte[] f = DCX.Decompress(path);
                File.WriteAllBytes($@"{dir}\dcx\{name}", f);
            }
        }

        public void ReDcx(string dir, string ext)
        {
            foreach (string path in Directory.GetFiles($@"{dir}\dcx", "*." + ext))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                DCX.Compress(File.ReadAllBytes(path), (DCX.Type)DCX.DefaultType.Sekiro, $@"{dir}\{name}.{ext}.dcx");
            }
        }

        // There's a lot of utility stuff here which could be put elsewhere
        public PARAM Param(string name)
        {
            return Params[name];
        }

        public PARAM.Row Item(ItemKey key)
        {
            if (!Sekiro) key = NormalizeWeapon(key);
            return Params[itemParams[key.Type]][key.ID];
        }

        public PARAM ItemParam(ItemType type) => Params[itemParams[type]];
        public PARAM ItemParam(ItemKey key) => Params[itemParams[key.Type]];

        public PARAM.Row AddRow(string name, int id, int oldId = -1)
        {
            PARAM param = Params[name];
            PARAM.Row row = param[id];
            if (row == null)
            {
                row = new PARAM.Row(id, "", param.AppliedParamdef);
                param.Rows.Add(row);
            }
            else if (oldId < 0)
            {
                // This can get quadratic, but good to check
                // If the contents are getting overwritten anyway, probably fine not to throw on this.
                throw new Exception($"Trying to add id {id} in {name} but already exists");
            }
            if (oldId >= 0)
            {
                GameEditor.CopyRow(param[oldId], row);
            }
            return row;
        }

        // Currently only works for Elden Ring - different amounts for other games and names for DS3
        public bool AddNpcSpEffect(PARAM.Row npc, int newSp, bool checkExisting = false)
        {
            int setSp = -1;
            for (int i = 31; i >= 0; i--)
            {
                int sp = (int)npc[$"spEffectID{i}"].Value;
                if (sp == newSp)
                {
                    return true;
                }
                if (setSp == -1 && sp <= 0)
                {
                    setSp = i;
                    if (!checkExisting)
                    {
                        break;
                    }
                }
            }
            if (setSp >= 0)
            {
                npc[$"spEffectID{setSp}"].Value = newSp;
                return true;
            }
            else
            {
                return false;
            }
        }

        public ItemKey NormalizeWeapon(ItemKey key)
        {
            // Maybe can put this logic in ItemKey itself
            if (key.Type == ItemType.Weapon && key.ID % 100 != 0)
            {
                return new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            return key;
        }

        public ItemKey FromCustomWeapon(ItemKey key)
        {
            if (key.Type == ItemType.Custom)
            {
                PARAM.Row wepRow = Item(key);
                if (wepRow != null)
                {
                    return new ItemKey(ItemType.Weapon, (int)wepRow["baseWepId"].Value + (byte)wepRow["reinforceLv"].Value);
                }
            }
            return key;
        }

        // Combination of the above
        public ItemKey GetBaseWeapon(ItemKey key)
        {
            if (key.Type == ItemType.Custom)
            {
                key = FromCustomWeapon(key);
            }
            if (key.Type == ItemType.Weapon)
            {
                key = NormalizeWeapon(key);
            }
            return key;
        }

        public int? GetBaseAccessory(ItemKey key)
        {
            if (key.Type == ItemType.Accessory)
            {
                if (EldenRing)
                {
                    // In Elden Ring DLC, there is Haligdrake Talisman +2 4042 and Golden Braid 7120, so use the correct approach for this
                    PARAM.Row row = Item(key);
                    if (row != null)
                    {
                        return (short)row["accessoryGroup"].Value;
                    }
                }
                else
                {
                    // Maybe a param field is available in other games but it doesn't seem to be necessary
                    return key.ID - (key.ID % 10);
                }
            }
            return null;
        }

        public bool GetCustomGem(ItemKey key, out ItemKey gem)
        {
            if (key.Type == ItemType.Custom)
            {
                PARAM.Row wepRow = Item(key);
                if (wepRow != null)
                {
                    int gemId = (int)wepRow["gemId"].Value;
                    if (gemId > 0)
                    {
                        gem = new ItemKey(ItemType.Gem, gemId);
                        return true;
                    }
                }
            }
            gem = null;
            return false;
        }

        public bool TryItemAsUint(ItemKey key, out uint item)
        {
            if (ItemTypeMasks.TryGetValue(key.Type, out uint mask) && key.ID >= 0 && key.ID <= 0x0FFF_FFFF)
            {
                item = mask | (uint)key.ID;
                return true;
            }
            else
            {
                item = 0xFFFF_FFFF;
                return false;
            }
        }

        public bool TryUintAsItem(uint val, out ItemKey item)
        {
            uint mask = val & 0xF000_0000;
            uint id = val & 0x0FFF_FFFF;
            if (MaskItemTypes.TryGetValue(mask, out ItemType type))
            {
                item = new ItemKey(type, (int)id);
                return true;
            }
            else
            {
                item = null;
                return false;
            }
        }

        public string Name(ItemKey key)
        {
            string suffix = "";
            if (key.Type == ItemType.Custom)
            {
                key = FromCustomWeapon(key);
            }
            if (key.Type == ItemType.Weapon && key.ID % 100 != 0)
            {
                suffix = $" +{key.ID % 100}";
                key = new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            // suffix += $" {key.ID}";
            return (ItemNames.ContainsKey(key) ? ItemNames[key] : $"?ITEM?" + $" ({key.Type}:{key.ID})") + suffix;
        }

        public static uint Convert(int id)
        {
            return id > 0 ? (uint)id : 0;
        }

        public static readonly Dictionary<ItemKey, string> DS3CustomNames = new Dictionary<ItemKey, string>
        {
            { new ItemKey(ItemType.Goods, 2123), "Cinders of a Lord (Abyss Watchers)" },
            { new ItemKey(ItemType.Goods, 2124), "Cinders of a Lord (Aldrich)" },
            { new ItemKey(ItemType.Goods, 2125), "Cinders of a Lord (Yhorm)" },
            { new ItemKey(ItemType.Goods, 2126), "Cinders of a Lord (Lothric)" },
        };
        public static readonly Dictionary<ItemKey, string> EldenCustomNames = new Dictionary<ItemKey, string>
        {
            // TODO: Config really needed here. Maybe only include the paren part
            { new ItemKey(ItemType.Goods, 8127), "Letter from Volcano Manor (Istvan)" },
            { new ItemKey(ItemType.Goods, 8132), "Letter from Volcano Manor (Rileigh)" },
            { new ItemKey(ItemType.Goods, 8174), "Academy Glintstone Key (Thops)" },
            { new ItemKey(ItemType.Goods, 8196), "Unalloyed Gold Needle (Milicent)" },
            { new ItemKey(ItemType.Goods, 8975), "Unalloyed Gold Needle (Broken)" },
            { new ItemKey(ItemType.Goods, 8976), "Unalloyed Gold Needle (Fixed)" },
            { new ItemKey(ItemType.Goods, 191), "Godrick's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 192), "Radahn's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 193), "Morgott's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 194), "Rykard's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 195), "Mohg's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 196), "Malenia's Great Rune (Restored)" },
            { new ItemKey(ItemType.Goods, 6600), "Golden Vow (Incantation)" },
            { new ItemKey(ItemType.Goods, 6820), "Beast Claw (Incantation)" },
            { new ItemKey(ItemType.Goods, 8154), "Lord of Blood's Favor" },
            { new ItemKey(ItemType.Goods, 8155), "Lord of Blood's Favor (Soaked)" },
            { new ItemKey(ItemType.Goods, 2003170), "Golden Vow (Consumable)" },
            { new ItemKey(ItemType.Goods, 2008033), "Larval Tear (DLC)" },
            { new ItemKey(ItemType.Goods, 11003), "Crimson Crystal Tear (Alternate)" },
            { new ItemKey(ItemType.Goods, 11005), "Cerulean Crystal Tear (Alternate)" },
            { new ItemKey(ItemType.Goods, 11017), "Ruptured Crystal Tear (Alternate)" },
            { new ItemKey(ItemType.Goods, 2001202), "Scorpion Stew (Drop)" },
            { new ItemKey(ItemType.Goods, 2001203), "Gourmet Scorpion Stew (Drop)" },
        };

        // TODO: Should probably be static if possible
        // Ideally there should be a generic mechanism for excluding content that doesn't require a ton of checks everywhere
        private static readonly Regex dlcMap = new Regex(@"^(m2|m4[0-3]|m61)");
        public bool IsEldenDlcMap(string map) => dlcMap.IsMatch(map);

        // Migrating towards static
        public bool IsEldenDlcItem(ItemKey key) => CheckEldenDlcItem(key);
        public static bool CheckEldenDlcItem(ItemKey key)
        {
            return key.Type switch
            {
                ItemType.Weapon => key.ID % 1000000 >= 500000 && !CheckEldenTarnishedItem(key),
                ItemType.Protector => key.ID >= 3000000 && key.ID < 5340000,
                // 7000 is +2 +3, 8000 is new ones
                ItemType.Accessory => key.ID >= 7000 && key.ID < 9000,
                ItemType.Goods => key.ID >= 2000000 && key.ID < 2300000 && !CheckEldenTarnishedItem(key),
                ItemType.Gem => key.ID >= 200000 && key.ID < 1000000,
                ItemType.Custom => key.ID >= 1000000 && key.ID < 5000000,
                _ => false
            };
        }
        // Unfortunately, for how widely CheckEldenDlcItem is used (including in the UI), Tarnished Pack complicates this as it's also DLC but interleaved
        // For now, go for even more hardcoding. This may need to move somewhere else that has knowledge of item annotations/slots and/or rewrite in terms of accessible locations
        private static readonly HashSet<int> tarnishedWeapons = new() { 3560000, 8530000, 13510000, 31540000, 62520000, 64530000, 66530000, 67530000 };
        public static bool CheckEldenTarnishedItem(ItemKey key)
        {
            return key.Type switch
            {
                ItemType.Weapon => tarnishedWeapons.Contains(key.ID / 10000 * 10000),
                ItemType.Protector => key.ID >= 5340000 && key.ID < 5380000,
                ItemType.Goods => key.ID >= 2009600 && key.ID < 2010000,
                _ => false
            };
        }
        public bool IsEldenDlcModel(int modelID) => (modelID >= 5000 && modelID < 6000) || (modelID >= 6200 && modelID < 7000);
        public bool IsEldenDlcModel(string model) => model.StartsWith('c') && int.TryParse(model.Substring(1), out int modelID) && IsEldenDlcModel(modelID);
        public bool IsEldenArrow(ItemKey item) => item.Type == ItemType.Weapon && item.ID >= 50000000 && item.ID < 60000000;

        private HashSet<int> regularWeapons;
        private HashSet<int> somberWeapons;

        // Based on ReinforceWeaponParam, but do it across the board
        private static readonly List<int> regularToSomber = new()
        {
            0, 0, 1, 1, 1,
            2, 2, 3, 3, 3,
            4, 4, 5, 5, 5,
            6, 6, 7, 7, 7,
            8, 8, 9, 9, 9, 10
        };

        public int AutoUpgrade(int id, int level)
        {
            return AutoUpgrade(new ItemKey(ItemType.Weapon, id), level).ID;
        }

        private readonly int customWepBase = 91200;
        private readonly Dictionary<(ItemKey, int), ItemKey> customWeaponRelevel = new();
        public ItemKey AutoUpgrade(ItemKey item, int level)
        {
            if (regularWeapons == null)
            {
                regularWeapons = new();
                somberWeapons = new();
                foreach (PARAM.Row row in Params["EquipParamWeapon"].Rows)
                {
                    if ((int)row["originEquipWep25"].Value > 0) regularWeapons.Add(row.ID);
                    else if ((int)row["originEquipWep10"].Value > 0) somberWeapons.Add(row.ID);
                }
            }
            if (item.Type == ItemType.Weapon)
            {
                int baseId = item.ID - item.ID % 100;
                int newLevel = 0;
                if (regularWeapons.Contains(baseId)) newLevel = level;
                else if (somberWeapons.Contains(baseId)) newLevel = regularToSomber[level];
                item = new ItemKey(item.Type, baseId + newLevel);
            }
            else if (item.Type == ItemType.Custom)
            {
                PARAM.Row wepRow = Item(item);
                if (wepRow != null)
                {
                    int baseId = (int)wepRow["baseWepId"].Value;
                    int currentLevel = (byte)wepRow["reinforceLv"].Value;
                    if (currentLevel != level)
                    {
                        if (!customWeaponRelevel.TryGetValue((item, level), out ItemKey relevel))
                        {
                            relevel = new ItemKey(ItemType.Custom, customWepBase + customWeaponRelevel.Count);
                            PARAM.Row newWep = GameEditor.AddRow(ItemParam(item), relevel.ID, wepRow);
                            int newLevel = 0;
                            if (regularWeapons.Contains(baseId)) newLevel = level;
                            else if (somberWeapons.Contains(baseId)) newLevel = regularToSomber[level];
                            newWep["reinforceLv"].Value = (byte)newLevel;
                            customWeaponRelevel[(item, level)] = relevel;
                        }
                        item = relevel;
                    }
                }
            }
            return item;
        }

        public string DisplayName(ItemKey key, int quantity = 1)
        {
            if (DS3 && DS3CustomNames.TryGetValue(key, out string name))
            {
                return name;
            }
            if (EldenRing && EldenCustomNames.TryGetValue(key, out name))
            {
                return name;
            }
            string quantityStr = quantity <= 1 ? "" : $" {quantity}x";
            return Name(key) + quantityStr;
        }

        public ItemKey ItemForName(string name)
        {
            if (!RevItemNames.ContainsKey(name)) throw new Exception($"Internal error: missing name {name}");
            if (RevItemNames[name].Count != 1) throw new Exception($"Internal error: ambiguous name {name} could be {string.Join(" or ", RevItemNames[name])}");
            return RevItemNames[name][0];
        }

        public SortedDictionary<ItemKey, string> Names()
        {
            return ItemNames;
        }

        public string LotName(int id)
        {
            return lotNames.ContainsKey(id) ? lotNames[id] : "?LOT?";
        }

        public string QwcName(int id)
        {
            return QwcNames.ContainsKey(id) ? QwcNames[id] : $"after {id}";
        }

        public string CharacterName(int id)
        {
            if (DS1)
            {
                if (!characterSplits.TryGetValue(id, out string n))
                {
                    characterSplits.TryGetValue(id - (id % 10), out n);
                }
                return n;
            }
            else if (!DS3)
            {
                return characterSplits.TryGetValue(id, out string n) ? n : null;
            }
            int chType = 0;
            foreach (KeyValuePair<int, string> entry in characterSplits)
            {
                if (entry.Key > id)
                {
                    break;
                }
                chType = entry.Key;
            }
            string name = characterSplits[chType];
            return name == "UNUSED" ? null : name;
        }

        public string ModelName(string chr)
        {
            return ModelNames.TryGetValue(chr, out string m) ? m : chr;
        }

        public string ModelCharacterName(string chr, int id)
        {
            return id > 0 ? (CharacterName(id) ?? ModelName(chr)) : ModelName(chr);
        }

        public List<string> GetModelNames()
        {
            return ModelNames.Values.ToList();
        }

        public static bool ExtractModelName(string name, out string modelName)
        {
            modelName = null;
            int split = name.LastIndexOf('_');
            if (split == -1)
            {
                return false;
            }
            modelName = name.Substring(0, split);
            // Elden Ring cross-map names
            if (modelName.StartsWith("m") && modelName.Contains('-'))
            {
                modelName = modelName.Split('-')[1];
            }
            return true;
        }

        public string AC6NpcName(int accountId)
        {
            // Skip AccountParam and assume the name for the time being
            FMGX fmg = ItemFMGs["NPC名"];
            string type = fmg[accountId];
            string name = fmg[accountId + 1];
            if (name == "<?null?>") name = null;
            if (string.IsNullOrEmpty(name)) return type;
            if (string.IsNullOrEmpty(type)) return name;
            return $"{type} / {name}";
        }

        public string EntityName(Entity entity, bool detail = false, bool mapName = false)
        {
            string mapSuffix = mapName && !string.IsNullOrEmpty(entity.MapName)
                ? " in " + MapLocationName(entity.MapName, entity.OriginalMapName, entity.Position)
                : "";
            if (!ExtractModelName(entity.Name, out string model))
            {
                return entity.Name + mapSuffix;
            }
            string modelName = model;
            if (modelName == "c0000")
            {
                modelName = CharacterName(entity.CharaInitID) ?? (EldenRing ? $"Human {entity.CharaInitID}" : "c0000");
            }
            if (ModelNames.ContainsKey(modelName))
            {
                modelName = ModelNames[modelName];
            }
            if (!detail)
            {
                // Note this doesn't do a CharacterName override, so using sparingly, or fix this
                return modelName + mapSuffix;
            }
            List<string> details = new List<string>();
            if (modelName != model)
            {
                details.Add(modelName);
            }
            if (entity.EntityID > 0)
            {
                details.Add($"id {entity.EntityID}");
            }
            if (entity.GroupIDs != null && entity.GroupIDs.Count > 0)
            {
                details.Add($"group {string.Join(",", entity.GroupIDs)}");
            }
            if (entity.NameID > 0)
            {
                string fmgName = AC6 ? AC6NpcName(entity.NameID) : ItemFMGs[EldenRing ? "NpcName" : "NPC名"][entity.NameID];
                if (!string.IsNullOrEmpty(fmgName))
                {
                    details.Add($"[{fmgName}]");
                }
            }
            return (entity.Type == null ? "" : $"{entity.Type} ")
                + entity.Name
                + (details.Count > 0 ? $" ({string.Join(" - ", details)})" : "")
                + mapSuffix;
        }

        public string MapLocationName(string mapId, string lowLevelMapId = null, Vector3? pos = null)
        {
            if (LocationName(mapId, out string extra, lowLevelMapId, pos))
            {
                extra = $" ({extra})";
            }
            else
            {
                extra = "";
            }
            return $"{lowLevelMapId ?? mapId}" + extra;
        }

        // TODO: Use instead of LocationNames dict
        public bool LocationName(string mapId, out string extra, string lowLevelMapId = null, Vector3? pos = null)
        {
            extra = null;
            if (Submaps.TryGetValue(mapId, out var multi) && pos is Vector3 v)
            {
                if (multi.Resolve(v, out var bound))
                {
                    extra = bound.Name;
                }
            }
            else if (LocationNames.TryGetValue(mapId, out string mapName))
            {
                extra = mapName;
            }
            return extra != null;
        }

        // Map name utility functions
        // Especially in Elden Ring, maps are stored in param field bytes
        public static List<byte> ParseMap(string map) => map.TrimStart('m').Split('_').Select(p => byte.Parse(p)).ToList();

        public static string FormatMap(IEnumerable<byte> bytes)
        {
            return "m" + string.Join("_", bytes.Select(b => b == 0xFF ? "XX" : $"{b:d2}"));
        }

        private readonly List<string> ParamMapIdFields = new List<string> { "areaNo", "gridXNo", "gridZNo" };
        public List<byte> GetMapParts(PARAM.Row row, List<string> fields = null)
        {
            if (fields == null) fields = ParamMapIdFields;
            List<byte> bytes = fields.Select(f => (byte)row[f].Value).ToList();
            while (bytes.Count < 4) bytes.Add(0);
            return bytes;
        }

        public HashSet<string> GetEldenFrameMaps()
        {
            // TODO: Should only be called by Elden Ring code
            if (EldenMaps == null) return new();
            // TODO: General system with FLVER reading, maybe
            HashSet<string> eldenFrameMaps = new HashSet<string>
            {
                // Mainly academy and redmane have confirmed issues
                "m10_00_00_00", "m12_05_00_00", "m14_00_00_00", "m15_00_00_00", "m16_00_00_00",
                "m18_00_00_00", "m35_00_00_00", "m39_20_00_00",
                "m60_39_54_00", // Shaded Castle
                "m60_43_31_00", // Morne
                "m60_51_36_00", // Redmane
                "m60_51_57_00", // Sol
                "m60_46_36_00", // Haight
                "m60_51_39_00", // Faroth
                // Try to guess for DLC. Exclude Fissure for now, and Enir-Ilim
                "m20_00_00_00", "m21_00_00_00", "m21_01_00_00", "m21_02_00_00", "m28_00_00_00",
                "m61_47_44_00", "m61_48_44_00", // Ensis (exclude small room in Fog Rift Fort m61_47_45_00)
                "m61_49_43_00", // Reprimand
            };
            // Plus all side-dungeons, m30 m31 m32 m34
            // Octopus: 2.4. Lobster: 4.4. Crab: 1.9.
            // If >2.5, allowframes
            Regex tightRe = new Regex(@"^m3[0-4]|^m4[0-3]");
            eldenFrameMaps.UnionWith(EldenMaps.Keys.Where(m => tightRe.IsMatch(m)));
            // Console.WriteLine($"---- {string.Join(", ", eldenFrameMaps.OrderBy(x => x))}");
            return eldenFrameMaps;
        }

#if SDT
        public void SaveSekiro(string outPath)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            // (DCX.Type)DCX.DefaultType.Sekiro is also unnecessarily slow
            DCX.Type overrideDcx = DCX.Type.DCX_DFLT_11000_44_9;
            foreach (KeyValuePair<string, MSBS> entry in SekiroMaps)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                AddModFile(path);
                entry.Value.Write(path, overrideDcx);
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in Talk)
            {
                if (!Locations.ContainsKey(entry.Key) && entry.Key != "m00_00_00_00") continue;
                WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\{entry.Key}.talkesdbnd.dcx", $@"script\talk\{entry.Key}.talkesdbnd.dcx", entry.Value, overrideDcx);
            }
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddModFile(path);
                entry.Value.Write(path, overrideDcx);
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }

            WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\gameparam.parambnd.dcx", $@"param\gameparam\gameparam.parambnd.dcx", Params.Inner, overrideDcx);
            foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\msg\{entry.Key}\item.msgbnd.dcx", $@"msg\{entry.Key}\item.msgbnd.dcx", entry.Value.FMGs, overrideDcx);
            }
            foreach (KeyValuePair<string, FMGDictionary> entry in AllMenuFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\msg\{entry.Key}\menu.msgbnd.dcx", $@"msg\{entry.Key}\menu.msgbnd.dcx", entry.Value.FMGs, overrideDcx);
            }

            MergeMods(outPath);
            Console.WriteLine("Success!");
        }
#endif

#if DS1
        public void SaveDS1(string outPath, string gameDir, RandomizerOptions opt, Events events)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            RuntimeParamChecker checker = new RuntimeParamChecker(11805890);
            checker.ScanMaps(DS1Maps);
            checker.CheckEntries(this);

            bool uxm = opt["loose"];

            // Params
            {
                string paramPath = @$"{outPath}\param\GameParam\GameParam.parambnd.dcx";
                AddBackupOrRestoreFile(paramPath, write: true, uxm: uxm);
                string basePath = $@"{Dir}\Vanilla\gameparam.parambnd.dcx";
                if (Mods.Resolve(@"param\GameParam\GameParam.parambnd.dcx", out string modPath))
                {
                    basePath = modPath;
                }
                Editor.OverrideBndRel(basePath, paramPath, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write());
            }

            string getOutMap(string map) => map == "m12_00_00_00" ? "m12_00_00_01" : map;
            // TODO: merged mods
            foreach (KeyValuePair<string, MSB1> entry in DS1Maps)
            {
                string outMap = getOutMap(entry.Key);
                string path = $@"{outPath}\map\MapStudio\{outMap}.msb";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                entry.Value.Write(path);
            }
            foreach (KeyValuePair<string, Breakobj> entry in Breakobjs)
            {
                string outMap = getOutMap(entry.Key);
                string path = $@"{outPath}\map\breakobj\{outMap}.breakobj";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                entry.Value.Write(path);
            }

            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                // TODO: Do not
                if (entry.Key == "common_func") continue;
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
#if DEBUG
                if (!opt["wrongnames"])
#endif
                {
                    checker.EditEvents(entry.Key, entry.Value, events, this);
                }
                entry.Value.Write(path);
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }

            void updateFmg(string lang, string type, FMGDictionary fmgs)
            {
                string path = $@"{outPath}\msg\{lang}\{type}.msgbnd.dcx";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                // Previously this checked WriteFMGs but it's fine, if people can't merge correctly they won't check the option anyway
                string basePath = $@"{Dir}\Vanilla\msg\{lang}\{type}.msgbnd.dcx";
                if (Mods.Resolve($@"msg\{lang}\{type}.msgbnd.dcx", out string modPath))
                {
                    basePath = modPath;
                }
                Editor.OverrideBndRel(basePath, path, fmgs.FMGs, f => f.Write());
            }
            {
                foreach (KeyValuePair<string, FMGDictionary> entry in AllMenuFMGs)
                {
                    updateFmg(entry.Key, "menu", entry.Value);
                }
                foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
                {
                    updateFmg(entry.Key, "item", entry.Value);
                }
            }

            // Hellkite Drake change
            {
                string drake = $@"chr\c3430.anibnd.dcx";
                string path = $@"{outPath}\{drake}";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                string basePath = $@"{Dir}\Vanilla\{drake}";
                string drakeTae = $@"{Dir}\Vanilla\chr\c3430.tae";
                if (Mods.Resolve(drake, out string modPath))
                {
                    basePath = modPath;
                }
                BND3 drakeBnd = BND3.Read(basePath);
                DS1MiscSetup.DS1DrakeFix(drakeBnd, drakeTae);
                drakeBnd.Write(path);
            }
            DS1MiscSetup.CombineDS1AI(DS1Maps.Keys, true, AI, (name, bnd) =>
            {
                string path = $@"{outPath}\script\{name}";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                bnd.Write(path);
            });

            BND3 readSfx(string name)
            {
                // This is the only case we'll read from randobak files, and from the game directory unconditionally
                // This may be called on maps without specific sfx bnds (e.g. m11 but no m11_00)
                string path = $@"{gameDir}\sfx\{name}";
                string modPath = path + ".randobak";
                if (File.Exists(modPath))
                {
                    return BND3.Read(modPath);
                }
                else if (File.Exists(path))
                {
                    return BND3.Read(path);
                }
                else
                {
                    return null;
                }
            }
            void writeSfx(string name, BND3 bnd)
            {
                string path = $@"{outPath}\sfx\{name}";
                AddBackupOrRestoreFile(path, write: true, uxm: uxm);
                bnd.Write(path);
            }
            bool isSfxDone(string name)
            {
                // This can get called on missing stuff still.
                string path = $@"{gameDir}\sfx\{name}";
                // For gamedir, use the presence of randobak to see if writing has happened before.
                // Otherwise, only require if also in gamedir.
                // Note if it was written incorrectly, restoring is necessary.
                string modPath = uxm ? path + ".randobak" : $@"{outPath}\sfx\{name}";
                return !File.Exists(path) || File.Exists(modPath);
            }
            DS1MiscSetup.CombineDS1SFX(DS1Maps.Keys, readSfx, writeSfx, isSfxDone);

            if (opt["crashfix"] && uxm)
            {
                string dllPath = $@"{outPath}\dinput8.dll";
                if (!MiscSetup.IsDS1HeapPatch(dllPath))
                {
                    if (File.Exists(dllPath))
                    {
                        AddBackupOrRestoreFile(dllPath, write: true, uxm: uxm);
                    }
                    File.Copy(@"dist1\DLL\DS1HeapPatch.dll", dllPath, true);
                }
            }

            Console.WriteLine("Success!");
        }
#endif

        // Elden Ring specifically doesn't write everything unconditionally
        public HashSet<string> WriteEmevds = new();
        public HashSet<string> WriteESDs = new();
        public List<ESD.State> ESDOptionStates = new();
        public HashSet<string> WriteMSBs = new();
        public bool WriteFMGs = false;

#if ER
        public void SaveEldenRing(string outPath, bool uxm, string optionsStr, Events events, Action<double> notify = null)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();
            Stopwatch sw = null;
#if DEBUG
            sw = Stopwatch.StartNew();
#endif

            RuntimeParamChecker checker = new RuntimeParamChecker(EldenRingBase + 2001);
            checker.ScanMaps(EldenMaps);
            checker.CheckEntries(this);

            // Sorry TK, Oodle is 2slow
            DCX.Type overrideDcx = DCX.Type.DCX_DFLT_11000_44_9;
            byte[] optionsByte = Encoding.ASCII.GetBytes(optionsStr);
            // overrideDcx = DCX.Type.DCX_KRAK;
            sw?.Restart();
            {
                string basePath = $@"{Dir}\Vanilla\regulation.bin";
                string path = $@"{outPath}\regulation.bin";
                if (Mods.Resolve("regulation.bin", out string modPath))
                {
                    basePath = modPath;
                }
                AddModFile(path);
                if (uxm) Backup(path);
                // Hack to add options string to params
                List<int> textIds = Enumerable.Range(777777771, 4).ToList();
                Params["CutSceneTextureLoadParam"].Rows.RemoveAll(r => textIds.Contains(r.ID));
                int offset = 0;
                foreach (int textId in textIds)
                {
                    PARAM.Row row = AddRow("CutSceneTextureLoadParam", textId, 0);
                    for (int i = 0; i < 16; i++)
                    {
                        int remaining = optionsByte.Length - offset;
                        if (remaining <= 0) break;
                        row[$"texName_{i:d2}"].Value = Encoding.ASCII.GetString(optionsByte, offset, Math.Min(remaining, 16));
                        offset += 16;
                    }
                }
                // New 2.3k limit, need to use better compression
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write(), dcx: DCX.Type.DCX_ZSTD);
                if (new FileInfo(path).Length >= 2300000)
                {
                    // This may not be needed with RandomizerCrashFix or me3, but issues like there are very hard to debug
                    Console.WriteLine("Params bigger than 2.3 MB, re-compressing with Oodle (much slower)");
                    Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write(), dcx: DCX.Type.DCX_KRAK);
                }
            }
            if (sw != null) Console.WriteLine($"Wrote params in {sw.ElapsedMilliseconds} ms");

            // This could be ConcurrentHashSet if it existed
            ConcurrentDictionary<string, string> dupedPaths = new();
            string getDupePath(ICollection<string> writeMaps, string map, string path)
            {
                MapDupes.TryGetValue(map, out string dupe);
                // If map is written and dupe also is, write nothing
                // If map is written and dupe is not, write dupe
                // If map is not written and dupe is, do nothing
                // If map is not written and dupe is not, delete/restore dupe
                // tl;dr if dupe is written, do nothing
                if (dupe == null || writeMaps.Contains(dupe)) return null;
                string dupePath = path.Replace(map, dupe);
                if (path == dupePath) throw new Exception($"Internal error: identical duplicate {path}");
                dupedPaths[dupePath] = path;
                return dupePath;
            }

            // Event scripts
            sw?.Restart();
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string map = entry.Key;
                string path = $@"{outPath}\event\{map}.emevd.dcx";
                if (dupedPaths.ContainsKey(path)) continue;
                string dupePath = getDupePath(WriteEmevds, map, path);
                bool write = WriteEmevds.Contains(map);
                AddBackupOrRestoreFile(path, write, uxm);
                if (dupePath != null)
                {
                    AddBackupOrRestoreFile(dupePath, write, uxm);
                }
                if (!write) continue;
#if !DEBUG
                checker.EditEvents(map, entry.Value, events, this);
#endif
                entry.Value.StringData = entry.Value.StringData.Concat(new byte[] { 0 }).Concat(optionsByte).ToArray();
                entry.Value.Write(path, overrideDcx);
                if (dupePath != null)
                {
                    File.Copy(path, dupePath, true);
                }
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }
            if (sw != null) Console.WriteLine($"Wrote event scripts in {sw.ElapsedMilliseconds} ms");

            // Hoarah Loux standalone SFX
            {
                string path = $@"{outPath}\sfx\sfxbnd_c4721.ffxbnd.dcx";
                string basePath = $@"{Dir}\Vanilla\sfxbnd_c4720.ffxbnd.dcx";
                if (File.Exists(basePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(basePath, path, true);
                }
            }

            void updateFmg(string lang, string type, FMGDictionary fmgs)
            {
                string path = $@"{outPath}\msg\{lang}\{type}_dlc02.msgbnd.dcx";
                AddBackupOrRestoreFile(path, WriteFMGs, uxm);
                string oldPath = $@"{outPath}\msg\{lang}\{type}.msgbnd.dcx";
                AddBackupOrRestoreFile(oldPath, false, uxm);
                if (WriteFMGs)
                {
                    string basePath = $@"{Dir}\Vanilla\msg\{lang}\{type}_dlc02.msgbnd.dcx";
                    if (Mods.Resolve($@"msg\{lang}\{type}_dlc02.msgbnd.dcx", out string modPath))
                    {
                        basePath = modPath;
                    }
                    Editor.OverrideBndRel(basePath, path, fmgs.FMGs, f => f.Write(), dcx: overrideDcx);
                }
            }

            // Text
            // Early on (as modengine can't reload it), but after events
            sw?.Restart();
            {
                foreach (KeyValuePair<string, FMGDictionary> entry in AllMenuFMGs)
                {
                    updateFmg(entry.Key, "menu", entry.Value);
                }
                foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
                {
                    updateFmg(entry.Key, "item", entry.Value);
                }
            }

            // ESDs
            foreach (ESD.State state in ESDOptionStates)
            {
                state.ExitCommands.Add(AST.MakeCommand(1, 0, optionsStr));
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in Talk)
            {
                string path = $@"{outPath}\script\talk\{entry.Key}.talkesdbnd.dcx";
                bool write = WriteESDs.Contains(entry.Key);
                AddBackupOrRestoreFile(path, write, uxm);
                if (!write) continue;
                string basePath = $@"{Dir}\Vanilla\{entry.Key}.talkesdbnd.dcx";
                if (Mods.Resolve($@"script\talk\{entry.Key}.talkesdbnd.dcx", out string modPath))
                {
                    basePath = modPath;
                }
                Editor.OverrideBndRel(basePath, path, entry.Value, f => f.Write(), dcx: overrideDcx);
            }
            if (sw != null) Console.WriteLine($"Wrote FMGs and ESDs in {sw.ElapsedMilliseconds} ms");

            // Maps
            void writeMap(string map)
            {
                string path = $@"{outPath}\map\mapstudio\{map}.msb.dcx";
                if (dupedPaths.ContainsKey(path)) return;
                // This can mutate dupedPaths
                string dupePath = getDupePath(WriteMSBs, map, path);
                bool write = WriteMSBs.Contains(map) && EldenMaps.ContainsKey(map);
                AddBackupOrRestoreFile(path, write, uxm);
                if (dupePath != null)
                {
                    AddBackupOrRestoreFile(dupePath, write, uxm);
                }
                if (!write) return;
                MSBE msb = EldenMaps[map];
                msb.Events.Navmeshes.Add(new MSBE.Event.Navmesh
                {
                    Name = optionsStr,
                    NavmeshRegionName = null,
                });
                msb.Write(path, overrideDcx);
                // entry.Value.Write(path.Replace(".dcx", ""), DCX.Type.None);
                if (dupePath != null)
                {
                    File.Copy(path, dupePath, true);
                }
            }
            notify?.Invoke(0);
            bool parallelize = true;
            int count = 0;
            sw?.Restart();
            // Include maps which are not loaded, like DLC maps, in case past randomizations had them
            List<string> knownMaps = unpackPaths
                .Where(p => p.EndsWith(".msb.dcx"))
                .Select(GameEditor.BaseName)
                .Union(EldenMaps.Keys)
                .ToList();
            if (parallelize)
            {
                Parallel.ForEach(ImmutableList.CreateRange(knownMaps), map =>
                {
                    writeMap(map);
                    // Could use return value of this but may as well be up to date
                    Interlocked.Increment(ref count);
                    notify?.Invoke((double)count / EldenMaps.Count);
                });
            }
            else
            {
                foreach (string map in knownMaps)
                {
                    notify?.Invoke((double)count++ / EldenMaps.Count);
                    writeMap(map);
                }
            }
            if (EldenMaps.Count > 0) notify?.Invoke(1);
            if (sw != null) Console.WriteLine($"Wrote maps in {sw.ElapsedMilliseconds} ms");
            sw?.Stop();
        }
#endif

#if NR
        public void SaveNightreign(string outPath)
        {
            Console.WriteLine("Writing to " + outPath);
            DCX.Type overrideDcx = DCX.Type.DCX_DFLT_11000_44_9;
            // DCX.Type? overrideDcx = DCX.Type.Unknown;
            writtenFiles.Clear();
            {
                string basePath = $@"{Dir}\Vanilla\regulation.bin";
                string path = $@"{outPath}\regulation.bin";
                DCX.Type regulationDcx = overrideDcx;
                if (Mods.Resolve("regulation.bin", out string modPath))
                {
                    basePath = modPath;
                    // Input: 1503 KB vanilla, 1664 KB with big mod
                    // Gzip: 2187 KB default, 2333 KB with big mod
                    // Krak: 1159 KB with big mod
                    // Zstd: 1664 KB with big mod
                    // regulationDcx = DCX.Type.DCX_ZSTD;
                }
                regulationDcx = DCX.Type.DCX_ZSTD;
                AddModFile(path);
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write(), dcx: regulationDcx);
            }

            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddBackupOrRestoreFile(path, write: true, uxm: false);
                entry.Value.Write(path, overrideDcx);
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }

            // Yeah idk
            WriteFMGs = true;
            void updateFmg(string lang, string type, FMGDictionary fmgs)
            {
                string path = $@"{outPath}\msg\{lang}\{type}.msgbnd.dcx";
                AddBackupOrRestoreFile(path, WriteFMGs, uxm: false);
                if (WriteFMGs)
                {
                    string basePath = $@"{Dir}\Vanilla\msg\{lang}\{type}.msgbnd.dcx";
                    if (Mods.Resolve($@"msg\{lang}\{type}.msgbnd.dcx", out string modPath))
                    {
                        basePath = modPath;
                    }
                    Editor.OverrideBndRel(basePath, path, fmgs.FMGs, f => f.Write(), dcx: overrideDcx);
                }
            }

            // Text
            // Early on (as modengine can't reload it), but after events
            {
                foreach (KeyValuePair<string, FMGDictionary> entry in AllMenuFMGs)
                {
                    updateFmg(entry.Key, "menu", entry.Value);
                }
                foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
                {
                    updateFmg(entry.Key, "item", entry.Value);
                }
            }
        }
#endif

#if DS3
        public void SaveDS3(string outPath, string gameDir)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            // Maps
            foreach (KeyValuePair<string, MSB3> entry in DS3Maps)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                AddModFile(path);
                entry.Value.Write(path);
            }

            // Save params
            // This is complicated enough (and probably also a bit wrong) such that WriteModDependentBnd is too simple.
            {
                string basePath = $@"{Dir}\Vanilla\Data0.bdt";
                if (Mods.Resolve($@"Data0.bdt", out string modPath))
                {
                    basePath = modPath;
                }
                string path = $@"{outPath}\Data0.bdt";
                AddModFile(path);
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.Write());
            }

            // Messages
            foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\msg\{entry.Key}\item_dlc2.msgbnd.dcx", $@"msg\{entry.Key}\item_dlc2.msgbnd.dcx", entry.Value.FMGs);
            }
            foreach (KeyValuePair<string, FMGDictionary> entry in AllMenuFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Vanilla\msg\{entry.Key}\menu_dlc2.msgbnd.dcx", $@"msg\{entry.Key}\menu_dlc2.msgbnd.dcx", entry.Value.FMGs);
            }

            // Event scripts
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddModFile(path);
                entry.Value.Write(path);
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }

            MiscSetup.CombineAI(DS3Maps.Keys, $@"{Dir}\Vanilla\script", true, AI, (name, bnd) =>
            {
                string path = $@"{outPath}\script\{name}";
                AddBackupOrRestoreFile(path, write: true, uxm: false);
                bnd.Write(path);
            });

            void writeSfx(string name, BND4 bnd)
            {
                string path = $@"{outPath}\sfx\{name}";
                AddBackupOrRestoreFile(path, write: true, uxm: false);
                bnd.Write(path);
            }
            DS3MiscSetup.CombineDS3SFX(DS3Maps.Keys, Mods, gameDir, $@"{outPath}\sfx", writeSfx);

            // MergeMods(outPath);
            Console.WriteLine("Success!");
        }
#endif

        private static string Backup(string file)
        {
            string bak = file + ".randobak";
            if (!File.Exists(bak))
            {
                File.Copy(file, bak, false);
            }
            return bak;
        }

        private void AddBackupOrRestoreFile(string path, bool write, bool uxm)
        {
            if (write)
            {
                AddModFile(path);
                if (uxm) Backup(path);
            }
            else if (uxm)
            {
                string bak = path + ".randobak";
                if (File.Exists(bak))
                {
                    RestoreBackup(bak);
                }
            }
            else if (File.Exists(path))
            {
                Console.WriteLine($"Deleting {path}");
                File.Delete(path);
            }
        }

        private static string FullName(string path)
        {
            return new FileInfo(path).FullName;
        }

        private void AddModFile(string path)
        {
            path = FullName(path);
            bool suppress = false;
#if DEBUG
            suppress = true;
#endif
            if (!suppress) Console.WriteLine($"Writing {path}");
            writtenFiles.Add(path);
        }

        void WriteModDependentBnd<T>(string outPath, string basePath, string relOutputPath, Dictionary<string, T> diffData, DCX.Type overrideDcx = DCX.Type.Unknown)
            where T : SoulsFile<T>, new()
        {
            if (Mods.Resolve(relOutputPath, out string modPath))
            {
                basePath = modPath;
            }
            string path = $@"{outPath}\{relOutputPath}";
            AddModFile(path);
            Editor.OverrideBnd(basePath, Path.GetDirectoryName(path), diffData, f => f.Write(), overrideDcx: overrideDcx);
        }

        private void MergeMods(string outPath)
        {
            Console.WriteLine("Processing extra mod files...");
            bool work = false;
            // If using this feature, assume a single mod dir for now
            string modDir = Mods.Dirs.FirstOrDefault();
            if (modDir != null)
            {
                foreach (string gameFile in MiscSetup.GetGameFiles(modDir, true))
                {
                    string source = FullName($@"{modDir}\{gameFile}");
                    string target = FullName($@"{outPath}\{gameFile}");
                    if (writtenFiles.Contains(target)) continue;
                    Console.WriteLine($"Copying {source}");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(source, target, true);
                    writtenFiles.Add(target);
                    work = true;
                }
            }
            foreach (string gameFile in MiscSetup.GetGameFiles(outPath, true))
            {
                string target = FullName($@"{outPath}\{gameFile}");
                if (writtenFiles.Contains(target)) continue;
                Console.WriteLine($"Found extra file (delete it if you don't want it): {target}");
                work = true;
            }
            if (!work) Console.WriteLine("No extra files found");
        }

        // Static restore utilities
        public static void RestoreBackupsInternal(string outPath, FromGame game)
        {
            Console.WriteLine("Restoring from " + outPath);

            foreach (string bakPath in GetBackupFiles(outPath, game))
            {
                string dest = GetRestoreName(bakPath);
                Console.WriteLine($"Restoring {dest}");
                RestoreBackup(bakPath);
            }
        }

        public static List<string> GetBackupFiles(string outPath, FromGame game)
        {
            List<string> restoreDirs = new List<string>
            {
                $@"{outPath}",
                $@"{outPath}\event",
                $@"{outPath}\script\talk",
                $@"{outPath}\map\mapstudio",
            };
            if (game == FromGame.DS1R)
            {
                restoreDirs.AddRange(new[]
                {
                    $@"{outPath}\map\breakobj",
                    $@"{outPath}\param\GameParam",
                    $@"{outPath}\script",
                    $@"{outPath}\sfx",
                    $@"{outPath}\chr",
                });
                restoreDirs.AddRange(MiscSetup.DS1Langs.Keys.Select(lang => $@"{outPath}\msg\{lang}"));
            }
            else
            {
                restoreDirs.AddRange(MiscSetup.Langs.Keys.Select(lang => $@"{outPath}\msg\{lang}"));
            }
            List<string> backups = new List<string>();
            foreach (string restoreDir in restoreDirs)
            {
                if (Directory.Exists(restoreDir))
                {
                    backups.AddRange(Directory.GetFiles(restoreDir, "*.randobak"));
                }
            }
            return backups;
        }

        public static string GetRestoreName(string bakPath)
        {
            if (!bakPath.EndsWith(".randobak")) throw new Exception($"Cannot restore {bakPath}, must end in .randobak");
            return bakPath.Substring(0, bakPath.Length - ".randobak".Length);
        }

        public static void RestoreBackup(string bakPath)
        {
            string dest = GetRestoreName(bakPath);
            if (!File.Exists(dest))
            {
                // This will generally only happen if the user deleted it.
                // We can warn that we're restoring it? idk
                // Console.WriteLine($"Warning: {dest} does not exist");
                // This is expected to happen with crash fix dll, since dinput8.dll may not exist
            }
            else
            {
                File.Delete(dest);
            }
            File.Move(bakPath, dest);
        }

        private void LoadNames()
        {
            ModelNames = new SortedDictionary<string, string>(Editor.LoadNames("ModelName", n => n, false));
            characterSplits = new SortedDictionary<int, string>(Editor.LoadNames("CharaInitParam", n => int.Parse(n), true));
            lotNames = new SortedDictionary<int, string>(Editor.LoadNames("ItemLotParam", n => int.Parse(n), true));
            QwcNames = new SortedDictionary<int, string>(Editor.LoadNames("ShopQwc", n => int.Parse(n), true));
            BonfireNames = new SortedDictionary<int, string>(Editor.LoadNames("BonfireWarpParam", n => int.Parse(n), !EldenRing));
            BonfireCats = new SortedDictionary<int, string>(Editor.LoadNames("BonfireWarpSubCategoryParam", n => int.Parse(n), !EldenRing));
            foreach ((ItemType type, string paramName) in itemParams)
            {
                foreach (KeyValuePair<ItemKey, string> entry in Editor.LoadNames(paramName, n => new ItemKey(type, int.Parse(n)), true))
                {
                    // Why??
                    ItemNames[entry.Key] = entry.Value;
                    AddMulti(RevItemNames, entry.Value, entry.Key);
                }
            }
            if (characterSplits.Count == 0)
            {
                characterSplits[0] = "UNUSED";
            }
            if (DS3)
            {
                LocationNames = DS3MapNames;
                Locations = DS3LocationNames;
            }
            else if (Sekiro)
            {
                LocationNames = SekiroMapNames;
                Locations = SekiroLocationNames;
            }
            else
            {
                LocationNames = Editor.LoadNames("MapName", n => n, false);
                // For now, don't have special location names, but we can maybe do this for legacy dungeons or have prefixes
                Locations = LocationNames.ToDictionary(e => e.Key, e => e.Key);
                if (EldenRing)
                {
                    Submaps = Submaps.Read($@"{Dir}\Base\submaps.txt");
#if DEBUG
                    Submaps.Write("MapName.json", LocationNames);
#endif
                }
            }
            RevLocations = Locations.ToDictionary(e => e.Value, e => e.Key);
        }

        public void AddItemName(ItemKey item, string name)
        {
            ItemNames[item] = name;
            AddMulti(RevItemNames, name, item);
        }

        // https://github.com/JKAnderson/Yapped/blob/master/Yapped/FormMain.cs
        private void LoadLayouts()
        {
            if (Editor.Spec.LayoutDir == null)
            {
                Defs = Editor.LoadDefs();
            }
            else
            {
                Layouts = Editor.LoadLayouts();
            }
        }

        private void LoadParams()
        {
            bool lazy = !DS3 && !Sekiro;
            Dictionary<string, PARAM> dict;
            string path;
            if (!lazy)
            {
                // Delay loading layouts if we'll do it in ParamDictionary
                LoadLayouts();
            }
            if (DS3)
            {
                path = $@"{Dir}\Vanilla\Data0.bdt";
                if (Mods.Resolve("Data0.bdt", out string modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file: {path}");
                }
                dict = Editor.LoadParams(path, defs: Defs);
            }
            else if (Sekiro)
            {
                path = $@"{Dir}\Vanilla\gameparam.parambnd.dcx";
                if (Mods.Resolve(@"param\gameparam\gameparam.parambnd.dcx", out string modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file: {path}");
                }
                dict = Editor.LoadParams(path, defs: Defs);
            }
            else if (EldenRing || AC6 || Nightreign)
            {
                path = $@"{Dir}\Vanilla\regulation.bin";
                if (Mods.Resolve(@"regulation.bin", out string modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file {path} - make sure to completely extract *all* files from the randomizer zip");
                }
                dict = Editor.LoadParams(path, defs: Defs);
            }
            else if (DS1)
            {
                path = $@"{Dir}\Vanilla\GameParam.parambnd.dcx";
                dict = Editor.LoadParams(path, defs: Defs);
                dict = MaybeOverrideFromModDir(dict, @"param\GameParam\GameParam.parambnd.dcx", p => Editor.LoadParams(p, defs: Defs));
            }
            else throw new Exception();
            if (lazy)
            {
                LoadLayouts();
            }
            Params = new ParamDictionary
            {
                Inner = dict,
                Layouts = Layouts,
                Defs = Defs,
            };
        }

        private void LoadMapData()
        {
            if (Sekiro)
            {
                SekiroMaps = Editor.Load("Vanilla", path => MSBS.Read(path), "*.msb.dcx");
                SekiroMaps = OverriddenFilesFromModDir(SekiroMaps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBS.Read(path));
                List<string> missing = Locations.Keys.Except(SekiroMaps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dists\Vanilla: {string.Join(", ", missing)}");
            }
            else if (DS3)
            {
                DS3Maps = Editor.Load("Vanilla", path => MSB3.Read(path), "*.msb.dcx");
                DS3Maps = OverriddenFilesFromModDir(DS3Maps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSB3.Read(path));
            }
            else if (AC6)
            {
#if AC6
                AC6Maps = Editor.Load("Vanilla", path => MSBAC6.Read(path), "*.msb.dcx");
#endif
            }
            else if (DS1)
            {
                // Rewrite m12_00_00_01 to match the emevd
                Dictionary<string, T> remapDarkroot<T>(Dictionary<string, T> dict)
                {
                    dict["m12_00_00_00"] = dict["m12_00_00_01"];
                    dict.Remove("m12_00_00_01");
                    // Continue to rely on semi-undocumented iteration order
                    return dict.OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value);
                }
                DS1Maps = remapDarkroot(Editor.Load("Vanilla", path => MSB1.Read(path), "*.msb"));
#if DS1
                Breakobjs = remapDarkroot(Editor.Load("Vanilla", path => Breakobj.Read(path), "*.breakobj"));
#endif
            }
            else if (EldenRing)
            {
                var sw = Stopwatch.StartNew();
                EldenMaps = Editor.LoadParallel("Vanilla", path => MSBE.Read(path), "*.msb.dcx");
                // It is fairly important that this is deterministic (although C# doesn't guarantee iteration order per se)
                EldenMaps = EldenMaps.OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value);
                EldenMaps = OverriddenFilesFromModDir(EldenMaps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBE.Read(path));
                sw.Stop();

                // Set up copying dupe maps which are not included in our vanilla files.
                MapDupes = dupeMsbs
                    .Where(m => !EldenMaps.ContainsKey(m))
                    .ToDictionary(m => EldenDupeMap(m), m => m);
                if (MapDupes.Any(e => e.Key == e.Value))
                {
                    throw new Exception($"Invalid dupe map {string.Join(" ", MapDupes)}");
                }
            }
        }

        private static readonly Regex lastRe = new Regex(@"_1([0-2])$");
        public static string EldenDupeMap(string mapId) => lastRe.Replace(mapId, @"_0$1");

        private void LoadTalk()
        {
            if (Sekiro || EldenRing)
            {
                Talk = Editor.LoadBnds("Vanilla", (data, path) => ESD.Read(data), "*.talkesdbnd.dcx");
                OverrideFilesFromModDir(Talk, name => $@"script\talk\{name}.talkesdbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => ESD.Read(data)));

                if (Sekiro)
                {
                    List<string> missing = Locations.Keys.Concat(new[] { "m00_00_00_00" }).Except(Talk.Keys).ToList();
                    if (missing.Count != 0) throw new Exception($@"Missing talkesdbnds in dist\Base: {string.Join(", ", missing)}");
                }
            }
        }

        private void LoadScripts()
        {
            if (DS1)
            {
                Emevds = Editor.Load("Vanilla", path => EMEVD.Read(path), "*.emevd.dcx");
                // TODO: Remove lookup in enemy randomizer
                Emevds["common_func"] = new EMEVD();
                // Also AI is technically a script
                AI = Editor.Load("AI", path => File.ReadAllText(path), "*.lua");
                return;
            }
            Emevds = Editor.Load("Vanilla", path => EMEVD.Read(path), "*.emevd.dcx");
            OverrideFilesFromModDir(Emevds, name => $@"event\{name}.emevd.dcx", path => EMEVD.Read(path));
            if (!EldenRing && !AC6 && !Nightreign)
            {
                List<string> missing = Locations.Keys.Concat(new[] { "common", "common_func" }).Except(Emevds.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing emevds in dist\Vanilla: {string.Join(", ", missing)}");
            }
            if (DS3)
            {
                AI = Editor.Load("AI", path => File.ReadAllText(path), "*.lua");
            }
        }

        private void LoadText()
        {
            // TODO: Surely we can merge more of these
            FMGDictionary read(string path)
            {
                Dictionary<string, byte[]> fmgBytes = Editor.LoadBnd(path, (data, _) => data);
                return new FMGDictionary { Inner = fmgBytes };
            }
            if (Sekiro)
            {
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    AllItemFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\item.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item.msgbnd.dcx", read);
                    AllMenuFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\menu.msgbnd.dcx");
                    AllMenuFMGs[lang] = MaybeOverrideFromModDir(AllMenuFMGs[lang], $@"msg\{lang}\menu.msgbnd.dcx", read);
                }
            }
            else if (DS3)
            {
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (MiscSetup.NoDS3Langs.Contains(lang)) continue;
                    AllItemFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\item_dlc2.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item_dlc2.msgbnd.dcx", read);
                    // Menu FMGs just for starting gifts
                    AllMenuFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\menu_dlc2.msgbnd.dcx");
                    AllMenuFMGs[lang] = MaybeOverrideFromModDir(AllMenuFMGs[lang], $@"msg\{lang}\menu_dlc2.msgbnd.dcx", read);
                }
            }
            // Nightreign can go here. Also AC6 no longer work
            else if (EldenRing || AC6 || Nightreign)
            {
                string suffix = EldenRing ? "_dlc02" : "";
                // Excludes araae for now
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    // TODO: Multilang if needed
                    if (AC6 && lang != "engus") continue;
                    AllMenuFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\menu{suffix}.msgbnd.dcx");
                    AllMenuFMGs[lang] = MaybeOverrideFromModDir(AllMenuFMGs[lang], $@"msg\{lang}\menu{suffix}.msgbnd.dcx", read);
                    AllItemFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\item{suffix}.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item{suffix}.msgbnd.dcx", read);
                }
            }
            else if (DS1)
            {
                foreach (string lang in MiscSetup.DS1Langs.Keys)
                {
                    AllMenuFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\menu.msgbnd.dcx");
                    AllMenuFMGs[lang] = MaybeOverrideFromModDir(AllMenuFMGs[lang], $@"msg\{lang}\menu.msgbnd.dcx", read);
                    AllItemFMGs[lang] = read($@"{Dir}\Vanilla\msg\{lang}\item.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item.msgbnd.dcx", read);
                }
            }
        }

        // TODO: Instead of doing this, make the paths themselves more editable?
        private T MaybeOverrideFromModDir<T>(T original, string path, Func<string, T> parser)
        {
            if (Mods.Resolve(path, out string modPath))
            {
                string bakFile = modPath + ".randobak";
                if (File.Exists(bakFile))
                {
                    throw new Exception($"Error merging {path}: You cannot output to the game directory if you're also merging mods (randobak file exists). Restore any previously written files using the File menu.");
                }
                Console.WriteLine($"Using modded file {modPath}");
                return parser(modPath);
            }
            return original;
        }

        private void OverrideFilesFromModDir<T>(Dictionary<string, T> files, Func<string, string> relpath, Func<string, T> parser)
        {
            if (Mods.Count == 0) return;
            foreach (string key in files.Keys.ToList())
            {
                files[key] = MaybeOverrideFromModDir(files[key], relpath(key), parser);
            }
        }

        private IReadOnlyDictionary<string, T> OverriddenFilesFromModDir<T>(IReadOnlyDictionary<string, T> files, Func<string, string> relpath, Func<string, T> parser)
        {
            if (Mods.Count == 0) return files;
            return files.ToDictionary(e => e.Key, e => MaybeOverrideFromModDir(e.Value, relpath(e.Key), parser));
        }

        // This should probably be used in other places, to standardize around game filepaths rather than having separate vanilla locations
        // (or just keep full hierarchy in Vanilla directory)
        private string GetVanillaPath(string bhdPath)
        {
            if (bhdPath.Contains("msgbnd") || bhdPath.Contains("luabnd"))
            {
                // Keep structure in this cases
                bhdPath = bhdPath.TrimStart('/');
            }
            else
            {
                // Otherwise flatten
                bhdPath = Path.GetFileName(bhdPath);
            }
            return Path.Combine($@"{Dir}\Vanilla", bhdPath);
        }

        // This is kept here just for UI purposes. If Randomizer can return anything other than an exception, use that instead
        private static List<string> mismatchedFiles = new();
        public static IReadOnlyList<string> GetMismatchedFiles() => mismatchedFiles;

        private record VanillaFile(string Path, string Archive, long Length, string Hash);

        public void UnpackVanillaFiles(string gameDir, RandomizerOptions opt, Action notifyExtract = null)
        {
            mismatchedFiles.Clear();
            if (!EldenRing && !DS3 && !Sekiro)
            {
                return;
            }
            if (gameDir == null) throw new ArgumentNullException(nameof(gameDir));
            List<string> missingDlc = new();
            void checkDlc(string archive, string optName)
            {
                FileInfo dlcBdt = new FileInfo(Path.Combine(gameDir, archive + ".bdt"));
                bool exists = dlcBdt.Exists && dlcBdt.Length > 1000;
                if (!exists)
                {
                    if (opt[optName])
                    {
                        // Preempt this error from extractor
                        throw new Exception($"{optName} option is enabled, but no content found at {dlcBdt.FullName}");
                    }
                    missingDlc.Add(archive);
                }
            }
            if (EldenRing)
            {
                checkDlc("DLC", "dlc");
            }
            if (DS3)
            {
                checkDlc("DLC1", "dlc1");
                checkDlc("DLC2", "dlc2");
            }
            Dictionary<string, VanillaFile> allPaths = new();
            string fileList = $@"{Dir}\Base\files.txt";
            foreach (var line in File.ReadLines(fileList))
            {
                string[] parts = line.Split(' ');
                if (parts.Length < 4)
                {
                    throw new Exception($"Bad line {line} in {fileList}");
                }
                string path = parts[0];
                string archive = parts[1] == "None" ? null : parts[1];
                long length = long.Parse(parts[2]);
                string hash = parts[3];
                allPaths[parts[0]] = new VanillaFile(path, archive, length, hash);
            }
            // One possible edge case is if files go away and randomizer was updated in-place, and the continued existence of these files causes problems.
            // In this case it will be necessary to do additional tracking like adding a version.txt, or just use the file list to filter bad files out.
            bool fileRequired(VanillaFile file)
            {
                return !missingDlc.Contains(file.Archive);
            }
            bool fileOkay(VanillaFile file)
            {
                FileInfo localFile = new FileInfo(GetVanillaPath(file.Path));
                if (!localFile.Exists)
                {
                    return !fileRequired(file);
                }
                // Use lengths to verify file contents.
                // Hash could be checked for all files, but that's a lot of I/O the way things are currently set up.
                if (file.Length != localFile.Length)
                {
                    return false;
                }
                return true;
            }
            unpackPaths = allPaths.Keys.ToList();
            Dictionary<string, VanillaFile> fixPaths = new(allPaths.Where(e => !fileOkay(e.Value)));
            if (fixPaths.Count == 0)
            {
                return;
            }
            notifyExtract?.Invoke();
#if DEBUG
            // Avoid mutating during development unless it's the first time setting it up
            if (Directory.Exists($@"{Dir}\Vanilla"))
            {
                if (opt["runbad"])
                {
                    return;
                }
                throw new Exception(
                    "Incorrect or incomplete files were found in Vanilla directory!"
                    + " Won't overwrite them in development build - delete the Vanilla directory so they can be regenerated."
                    + $"\nFiles: {string.Join(", ", fixPaths.Keys)}");
            }
#endif
            Dictionary<string, VanillaFile> copyPaths = new(fixPaths.Where(e => e.Value.Archive == null));
            // For now, do full extract on any issues, since as of ER 1.17 there are some identical file lengths (albeit still compatible) and this system is unproven.
            // We may need to check Vanilla file hashes after all.
            Dictionary<string, string> extractPaths = allPaths.Where(e => e.Value.Archive != null && fileRequired(e.Value)).ToDictionary(e => e.Key, e => e.Value.Archive);
            SortedSet<string> mismatchFiles = new();
            foreach (VanillaFile file in copyPaths.Values)
            {
                FileInfo gameFile = new FileInfo(Path.Combine(gameDir, file.Path.TrimStart('/')));
                // Because it's easy for users to replace these files with modded ones, don't copy to Vanilla unless checks succeed.
                if (gameFile.Exists)
                {
                    if (gameFile.Length == file.Length && GetSHA1FileHash(gameFile.FullName) == file.Hash)
                    {
                        string outPath = GetVanillaPath(file.Path);
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                        gameFile.CopyTo(outPath, overwrite: true);
                    }
                    else
                    {
                        // In the case of regulations, we can also check version for extra info
                        throw new Exception(
                            $"Error: {gameFile.FullName} is not compatible with this version of randomizer."
                            + " Make sure the randomizer version and game version are compatible. If they are, verify integrity of installed files in Steam."
                            + $" (expected {file.Hash}, got {GetSHA1FileHash(gameFile.FullName)})");
                    }
                }
                else
                {
                    throw new Exception($"Error: {gameFile.FullName} not found");
                }
            }
            if (extractPaths.Count > 0)
            {
                // This throws an error if any of the files could not be found
                foreach ((string path, byte[] data) in BhdExtractor.EnumerateBdtFiles(Type, gameDir, extractPaths))
                {
                    if (allPaths.TryGetValue(path, out VanillaFile file) && data.Length != file.Length)
                    {
                        mismatchFiles.Add(path);
                    }
                    string outPath = GetVanillaPath(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    File.WriteAllBytes(outPath, data);
                }
            }
            mismatchedFiles = mismatchFiles.Select(s => s.TrimStart('/')).ToList();
            if (mismatchedFiles.Count == 0)
            {
                Console.WriteLine($"Extracted {fixPaths.Count} game files");
            }
            else
            {
                Console.WriteLine($"Extracted {fixPaths.Count} game files, but at least {mismatchedFiles.Count} files have unexpected contents. This could indicate this version of the randomizer may be incompatible with the installed game version.");
                Console.WriteLine($"Mismatched files in Vanilla directory: {string.Join(", ", mismatchedFiles)}");
            }
            // This can be slightly memory-intensive, around 50 MB, so try this if it does anything.
            // (Alternatively: keep the byte data around for loads)
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Some helper functionality things. These require strict params
        public void SearchParamInt(uint id, string field=null)
        {
            bool matches(string cell)
            {
                // if (cell == id.ToString()) return true;
                if (cell.Contains(id.ToString())) return true;
                // if (int.TryParse(cell, out int val)) return val >= 11000000 && val <= 13000000 && (val / 1000) % 10 == 5;
                return false;
            }
            Console.WriteLine($"-- Searching params for {id}");
            foreach (KeyValuePair<string, PARAM> param in Params.Inner)
            {
                foreach (PARAM.Row row in param.Value.Rows)
                {
                    if (field == null && row.ID == id)
                    {
                        Console.WriteLine($"{param.Key}[{row.ID}]");
                    }
                    foreach (PARAM.Cell cell in row.Cells)
                    {
                        if ((field == null || cell.Def.InternalName == field) && cell.Value != null && matches(cell.Value.ToString()))
                        {
                            Console.WriteLine($"{param.Key}[{row.ID}].{cell.Def.InternalName} = {cell.Value}");
                        }
                    }
                }
            }
        }

        public void SearchParamFloat(float id)
        {
            Console.WriteLine($"-- Searching params for {id}");
            foreach (KeyValuePair<string, PARAM> param in Params.Inner)
            {
                foreach (PARAM.Row row in param.Value.Rows)
                {
                    foreach (PARAM.Cell cell in row.Cells)
                    {
                        if (cell.Value != null && cell.Value.GetType() == 0f.GetType() && Math.Abs((float)cell.Value - id) < 0.0001)
                        {
                            Console.WriteLine($"{param.Key}[{row.ID}].{cell.Def.InternalName} = {cell.Value}");
                        }
                    }
                }
            }
        }

        public void SplitMaps()
        {
            Dictionary<string, List<string>> areas = new();
            SortedDictionary<(string, string), List<string>> maps = new();
            foreach ((string map, string name) in LocationNames.OrderBy(x => x.Key))
            {
                if (!(map.StartsWith("m61") && map.EndsWith("0"))) continue;
                SortedSet<string> subareas = new();
                string[] partNames = name.Split("; ");
                foreach (string part in partNames)
                {
                    string area = part.Split(" - ")[0];
                    AddMulti(areas, area, $"{map} {part}");
                    subareas.Add(area);
                }
                if (name.Contains(',') || name.Contains(';'))
                {
                    AddMulti(maps, (string.Join(", ", subareas), map), partNames);
                }
            }
            foreach (var entry in areas)
            {
                foreach (var name in entry.Value)
                {
                    Console.WriteLine(name);
                }
            }
            Console.WriteLine();
            foreach (var entry in maps)
            {
                Console.WriteLine(entry.Key.Item2);
                foreach (var name in entry.Value)
                {
                    Console.WriteLine(name);
                }
            }
        }

        public void DumpMessages(string dir)
        {
            foreach (string path in Directory.GetFiles(dir, "*.msgbnd*"))
            {
                if (path.Contains("dlc1")) continue;
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                // Hack to avoid noise here
                if (path.Contains("ELDEN") && (name.Contains("item") || name.Contains("menu")))
                {
                    if (!name.Contains("_dlc02")) continue;
                    name = name.Replace("_dlc02", "");
                }
                try
                {
                    IBinder bnd = BND3.Is(path) ? (IBinder)BND3.Read(path) : BND4.Read(path);
                    foreach (BinderFile file in bnd.Files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                        string uname = fileName;
                        // uname = System.Text.RegularExpressions.Regex.Replace(uname, @"[^\x00-\x7F]", c => string.Format(@"u{0:x4}", (int)c.Value[0]));
                        string fname = $"{name}_{uname}.txt";
                        // Console.WriteLine(fname);
                        // string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                        FMG fmg = FMG.Read(file.Bytes);
                        if (path.Contains("engus")) Console.WriteLine($"{file.ID} -> {fname}");
                        if (fmg.Entries != null)
                        {
                            File.WriteAllLines($@"{dir}\{fname}", fmg.Entries.Select(e => $"{e.ID} {(e.Text == null ? "" : e.Text.Replace("\r", "").Replace("\n", "\\n"))}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load file: {name}: {path}\r\n\r\n{ex}");
                }
            }
            foreach (string path in Directory.GetFiles(dir, "*.fmg"))
            {
                FMG fmg = FMG.Read(path);
                string fname = Path.GetFileNameWithoutExtension(path);
                if (fmg.Entries != null)
                {
                    File.WriteAllLines($@"{dir}\{fname}.txt", fmg.Entries.Select(e => $"{e.ID} {(e.Text == null ? "" : e.Text.Replace("\r", "").Replace("\n", "\\n"))}"));
                }
            }
        }
    }
}
