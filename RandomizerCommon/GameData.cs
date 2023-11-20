using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class GameData
    {
        private static readonly List<string> itemParams = new List<string>()
        {
            "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "EquipParamGoods", "EquipParamGem", null, "EquipParamCustomWeapon",
        };

        public static readonly ISerializer Serializer = new SerializerBuilder()
            .DisableAliases()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();
        public static readonly int EldenRingBase = 1032500000;

        public readonly GameEditor Editor;
        public FromGame Type => Editor.Spec.Game;
        public bool Sekiro => Type == FromGame.SDT;
        public bool DS3 => Type == FromGame.DS3;
        public bool EldenRing => Type == FromGame.ER;
        public bool AC6 => Type == FromGame.AC6;
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
        private readonly static Dictionary<uint, ItemType> MaskLotItemTypes = new Dictionary<uint, ItemType>
        {
            [0x00000000] = ItemType.WEAPON,
            [0x10000000] = ItemType.ARMOR,
            [0x20000000] = ItemType.RING,
            [0x40000000] = ItemType.GOOD,
        };
        private readonly static Dictionary<uint, ItemType> ErLotItemTypes = new Dictionary<uint, ItemType>
        {
            [1] = ItemType.GOOD,
            [2] = ItemType.WEAPON,
            [3] = ItemType.ARMOR,
            [4] = ItemType.RING,
            [5] = ItemType.GEM,
            [6] = ItemType.CUSTOM,
        };

        // echo $(ls | grep -E '_[1][0-2].msb') | sed -e 's/.msb[^ ]* /", "/g'
        // TODO: why not m60_45_36_10 edits
        private static readonly List<string> dupeMsbs = new List<string>
        {
            "m60_11_09_12", "m60_11_13_12",
            "m60_22_18_11", "m60_22_19_11", "m60_22_26_11", "m60_22_27_11",
            "m60_23_18_11", "m60_23_19_11", "m60_23_26_11", "m60_23_27_11",
            "m60_44_36_10", "m60_44_37_10", "m60_44_38_10", "m60_44_39_10",
            "m60_44_52_10", "m60_44_53_10", "m60_44_54_10", "m60_44_55_10",
            "m60_45_36_10", "m60_45_37_10", "m60_45_38_10", "m60_45_39_10",
            "m60_45_52_10", "m60_45_53_10", "m60_45_54_10", "m60_45_55_10",
            "m60_46_36_10", "m60_46_37_10", "m60_46_38_10", "m60_46_39_10",
            "m60_46_52_10", "m60_46_53_10", "m60_46_54_10", "m60_46_55_10",
            "m60_47_36_10", "m60_47_37_10", "m60_47_38_10", "m60_47_39_10",
            "m60_47_52_10", "m60_47_53_10", "m60_47_54_10", "m60_47_55_10",
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
            [0] = ItemType.WEAPON,
            [1] = ItemType.ARMOR,
            [2] = ItemType.RING,
            [3] = ItemType.GOOD,
            [4] = ItemType.GEM,
        };

        // Actual data
        private Dictionary<string, PARAM.Layout> Layouts = new Dictionary<string, PARAM.Layout>();
        private Dictionary<string, PARAMDEF> Defs = new Dictionary<string, PARAMDEF>();
        public ParamDictionary Params = new ParamDictionary();
        public Dictionary<string, IMsb> Maps = new Dictionary<string, IMsb>();
        public Dictionary<string, EMEVD> Emevds = new Dictionary<string, EMEVD>();
        public FMGDictionary ItemFMGs => AllItemFMGs["engus"];
        public FMGDictionary MenuFMGs => AllMenuFMGs["engus"];
        public Dictionary<string, FMGDictionary> AllItemFMGs = new Dictionary<string, FMGDictionary>();
        public Dictionary<string, FMGDictionary> AllMenuFMGs = new Dictionary<string, FMGDictionary>();
        public Dictionary<string, Dictionary<string, ESD>> Talk = new Dictionary<string, Dictionary<string, ESD>>();

        // Names
        public SortedDictionary<ItemKey, string> ItemNames = new SortedDictionary<ItemKey, string>();
        public SortedDictionary<string, List<ItemKey>> RevItemNames = new SortedDictionary<string, List<ItemKey>>();

        private SortedDictionary<int, string> qwcNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> lotNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> characterSplits = new SortedDictionary<int, string>();
        private SortedDictionary<string, string> modelNames = new SortedDictionary<string, string>();

        private List<string> writtenFiles = new List<string>();

        public GameData(string dir, FromGame game)
        {
            Dir = dir;
            Editor = new GameEditor(game);
            Editor.Spec.GameDir = $@"{dir}";
            Editor.Spec.NameDir = $@"{dir}\Names";
            if (EldenRing || AC6)
            {
                Editor.Spec.DefDir = $@"{dir}\Defs";
                // Editor.Spec.DefDir = $@"..\ParamdexNew\ER\Defs";
            }
            else
            {
                Editor.Spec.LayoutDir = $@"{dir}\Layouts";
            }
            LotItemTypes = EldenRing ? ErLotItemTypes : MaskLotItemTypes;
            LotValues = LotItemTypes.ToDictionary(e => e.Value, e => e.Key);
        }

        // The IMsb interface is not usable directly, so in lieu of making GameData extremely generic, add these casts
        public Dictionary<string, MSB3> DS3Maps => Maps.ToDictionary(e => e.Key, e => e.Value as MSB3);
        public Dictionary<string, MSBS> SekiroMaps => Maps.ToDictionary(e => e.Key, e => e.Value as MSBS);
        public Dictionary<string, MSBE> EldenMaps =>
            Maps.Where(e => e.Value is MSBE).ToDictionary(e => e.Key, e => e.Value as MSBE);
#if DEV
        public Dictionary<string, MSBAC6> AC6Maps => Maps.ToDictionary(e => e.Key, e => e.Value as MSBAC6);
#endif

        public void Load(MergedMods mods)
        {
            foreach (string dir in mods.Dirs)
            {
                Console.WriteLine($"Checking other mod directory {dir}");
            }
            Mods = mods;
            LoadNames();
            LoadParams();
            LoadMapData();
            LoadTalk();
            LoadScripts();
            LoadText();
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

        public PARAM Param(string name)
        {
            return Params[name];
        }

        public PARAM.Row Item(ItemKey key)
        {
            if (!Sekiro) key = NormalizeWeapon(key);
            return Params[itemParams[(int)key.Type]][key.ID];
        }

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

        public ItemKey NormalizeWeapon(ItemKey key)
        {
            // Maybe can put this logic in ItemKey itself
            if (key.Type == ItemType.WEAPON && key.ID % 100 != 0)
            {
                return new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            return key;
        }

        public ItemKey FromCustomWeapon(ItemKey key)
        {
            if (key.Type == ItemType.CUSTOM)
            {
                PARAM.Row wepRow = Item(key);
                if (wepRow != null)
                {
                    return new ItemKey(ItemType.WEAPON, (int)wepRow["baseWepId"].Value + (byte)wepRow["reinforceLv"].Value);
                }
            }
            return key;
        }

        public string Name(ItemKey key)
        {
            string suffix = "";
            if (key.Type == ItemType.CUSTOM)
            {
                key = FromCustomWeapon(key);
            }
            if (key.Type == ItemType.WEAPON && key.ID % 100 != 0)
            {
                suffix = $" +{key.ID % 100}";
                key = new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            // suffix += $" {key.ID}";
            return (ItemNames.ContainsKey(key) ? ItemNames[key] : $"?ITEM?" + $" ({(int)key.Type}:{key.ID})") + suffix;
        }

        private static readonly Dictionary<ItemKey, string> customNamesDS3 = new Dictionary<ItemKey, string>
        {
            { new ItemKey(ItemType.GOOD, 2123), "Cinders of a Lord (Abyss Watchers)" },
            { new ItemKey(ItemType.GOOD, 2124), "Cinders of a Lord (Aldrich)" },
            { new ItemKey(ItemType.GOOD, 2125), "Cinders of a Lord (Yhorm)" },
            { new ItemKey(ItemType.GOOD, 2126), "Cinders of a Lord (Lothric)" },
        };
        private static readonly Dictionary<ItemKey, string> customNamesElden = new Dictionary<ItemKey, string>
        {
            { new ItemKey(ItemType.GOOD, 8127), "Letter from Volcano Manor (Istvan)" },
            { new ItemKey(ItemType.GOOD, 8132), "Letter from Volcano Manor (Rileigh)" },
            { new ItemKey(ItemType.GOOD, 8174), "Academy Glintstone Key (Thops)" },
            { new ItemKey(ItemType.GOOD, 8196), "Unalloyed Gold Needle (Milicent)" },
            { new ItemKey(ItemType.GOOD, 8975), "Unalloyed Gold Needle (Broken)" },
            { new ItemKey(ItemType.GOOD, 8976), "Unalloyed Gold Needle (Fixed)" },
        };

        public string DisplayName(ItemKey key, int quantity = 1)
        {
            if (DS3 && customNamesDS3.TryGetValue(key, out string name))
            {
                return name;
            }
            if (EldenRing && customNamesElden.TryGetValue(key, out name))
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
            return qwcNames.ContainsKey(id) ? qwcNames[id] : $"after {id}";
        }

        public string CharacterName(int id)
        {
            if (EldenRing || AC6)
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
            return modelNames.TryGetValue(chr, out string m) ? m : chr;
        }

        public string ModelCharacterName(string chr, int id)
        {
            return id > 0 ? (CharacterName(id) ?? ModelName(chr)) : ModelName(chr);
        }

        public List<string> GetModelNames()
        {
            return modelNames.Values.ToList();
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
            FMG fmg = ItemFMGs["NPC名"];
            string type = fmg[accountId];
            string name = fmg[accountId + 1];
            if (name == "<?null?>") name = null;
            if (string.IsNullOrEmpty(name)) return type;
            if (string.IsNullOrEmpty(type)) return name;
            return $"{type} / {name}";
        }

        public string EntityName(EntityId entity, bool detail = false, bool mapName = false)
        {
            string mapSuffix = mapName && !string.IsNullOrEmpty(entity.MapName)
                ? " in " + MapLocationName(entity.MapName, entity.OriginalMapName)
                : "";
            if (!ExtractModelName(entity.EntityName, out string model))
            {
                return entity.EntityName + mapSuffix;
            }
            string modelName = model;
            if (modelName == "c0000")
            {
                modelName = CharacterName(entity.CharaInitID) ?? (EldenRing ? $"Human {entity.CharaInitID}" : "c0000");
            }
            if (modelNames.ContainsKey(modelName))
            {
                modelName = modelNames[modelName];
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
            if (entity.GroupIds != null && entity.GroupIds.Count > 0)
            {
                details.Add($"group {string.Join(",", entity.GroupIds)}");
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
                + entity.EntityName
                + (details.Count > 0 ? $" ({string.Join(" - ", details)})" : "")
                + mapSuffix;
        }

        public string MapLocationName(string mapId, string lowLevelMapId = null)
        {
            return $"{lowLevelMapId ?? mapId}" + (LocationNames.TryGetValue(mapId, out string mapName) ? $" ({mapName})" : "");
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
            };
            // Plus all side-dungeons, m30 m31 m32 m34
            // Octopus: 2.4. Lobster: 4.4. Crab: 1.9.
            // If >2.5, allowframes
            Regex tightRe = new Regex(@"^m3[0-4]");
            eldenFrameMaps.UnionWith(Maps.Keys.Where(m => tightRe.IsMatch(m)));
            return eldenFrameMaps;
        }

        public void SaveSekiro(string outPath)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            foreach (KeyValuePair<string, IMsb> entry in Maps)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                AddModFile(path);
                entry.Value.Write(path, (DCX.Type)DCX.DefaultType.Sekiro);
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in Talk)
            {
                if (!Locations.ContainsKey(entry.Key) && entry.Key != "m00_00_00_00") continue;
                WriteModDependentBnd(outPath, $@"{Dir}\Base\{entry.Key}.talkesdbnd.dcx", $@"script\talk\{entry.Key}.talkesdbnd.dcx", entry.Value);
            }
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddModFile(path);
                entry.Value.Write(path, (DCX.Type)DCX.DefaultType.Sekiro);
#if DEBUG
                string scriptFile = path + ".js";
                if (File.Exists(scriptFile))
                {
                    Console.WriteLine($"Deleting {scriptFile}");
                    File.Delete(scriptFile);
                }
#endif
            }

            WriteModDependentBnd(outPath, $@"{Dir}\Base\gameparam.parambnd.dcx", $@"param\gameparam\gameparam.parambnd.dcx", Params.Inner);
            WriteModDependentBnd(outPath, $@"{Dir}\Base\item.msgbnd.dcx", $@"msg\engus\item.msgbnd.dcx", ItemFMGs.FMGs);
            WriteModDependentBnd(outPath, $@"{Dir}\Base\menu.msgbnd.dcx", $@"msg\engus\menu.msgbnd.dcx", MenuFMGs.FMGs);
            foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
            {
                if (entry.Key == "engus") continue;
                WriteModDependentBnd(outPath, $@"{Dir}\Base\msg\{entry.Key}\item.msgbnd.dcx", $@"msg\{entry.Key}\item.msgbnd.dcx", entry.Value.FMGs);
            }

            MergeMods(outPath);
            Console.WriteLine("Success!");
        }

        public static void RestoreBackupsInternal(string outPath)
        {
            Console.WriteLine("Restoring from " + outPath);

            foreach (string bakPath in GetBackupFiles(outPath))
            {
                string dest = GetRestoreName(bakPath);
                Console.WriteLine($"Restoring {dest}");
                RestoreBackup(bakPath);
            }
        }

        public static List<string> GetBackupFiles(string outPath)
        {
            List<string> restoreDirs = new List<string>
            {
                $@"{outPath}",
                $@"{outPath}\event",
                $@"{outPath}\script\talk",
                $@"{outPath}\map\mapstudio",
            };
            restoreDirs.AddRange(MiscSetup.Langs.Keys.Select(lang => $@"{outPath}\msg\{lang}"));
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
            }
            else
            {
                File.Delete(dest);
            }
            File.Move(bakPath, dest);
        }

        public HashSet<string> WriteEmevds = new HashSet<string>();
        public HashSet<string> WriteESDs = new HashSet<string>();
        public HashSet<string> WriteMSBs = new HashSet<string>();
        public bool WriteFMGs = false;
        public void SaveEldenRing(string outPath, bool uxm, string optionsStr, Action<double> notify = null)
        {
            Console.WriteLine("Writing to " + outPath);
            RuntimeParamChecker checker = new RuntimeParamChecker();
            checker.ScanMaps(Maps);
            checker.CheckEntries(this);
            // Sorry TK, Oodle is 2slow
            DCX.Type overrideDcx = DCX.Type.DCX_DFLT_11000_44_9;
            byte[] optionsByte = Encoding.ASCII.GetBytes(optionsStr);
            // overrideDcx = DCX.Type.DCX_KRAK;
            writtenFiles.Clear();
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
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write(), dcx: overrideDcx);
            }

            HashSet<string> dupedPaths = new HashSet<string>();
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
                dupedPaths.Add(dupePath);
                return dupePath;
            }

            // Event scripts
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string map = entry.Key;
                string path = $@"{outPath}\event\{map}.emevd.dcx";
                if (dupedPaths.Contains(path)) continue;
                string dupePath = getDupePath(WriteEmevds, map, path);
                bool write = WriteEmevds.Contains(map);
                AddBackupOrRestoreFile(path, write, uxm);
                if (dupePath != null)
                {
                    AddBackupOrRestoreFile(dupePath, write, uxm);
                }
                if (!write) continue;
#if !DEBUG
                checker.EditEvents(map, entry.Value);
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
#if DEBUG
            Console.WriteLine("Wrote event scripts");
#endif

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
                string path = $@"{outPath}\msg\{lang}\{type}.msgbnd.dcx";
                AddBackupOrRestoreFile(path, WriteFMGs, uxm);
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

            // ESDs
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

            // Maps
            int count = 0;
            foreach (KeyValuePair<string, IMsb> entry in Maps)
            {
                notify?.Invoke((double)count++ / Maps.Count);
                string map = entry.Key;
                string path = $@"{outPath}\map\mapstudio\{map}.msb.dcx";
                if (dupedPaths.Contains(path)) continue;
                string dupePath = getDupePath(WriteMSBs, map, path);
                bool write = WriteMSBs.Contains(map);
                AddBackupOrRestoreFile(path, write, uxm);
                if (dupePath != null)
                {
                    AddBackupOrRestoreFile(dupePath, write, uxm);
                }
                if (!write) continue;
                if (entry.Value is MSBE msb)
                {
                    msb.Events.Navmeshes.Add(new MSBE.Event.Navmesh
                    {
                        Name = optionsStr,
                        NavmeshRegionName = null,
                    });
                }
                entry.Value.Write(path, overrideDcx);
                // entry.Value.Write(path.Replace(".dcx", ""), DCX.Type.None);
                if (dupePath != null)
                {
                    File.Copy(path, dupePath, true);
                }
            }
            if (Maps.Count > 0) notify?.Invoke(1);
        }

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

        public void SaveDS3(string outPath, bool encrypted)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            // Maps
            foreach (KeyValuePair<string, IMsb> entry in Maps)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                AddModFile(path);
                entry.Value.Write(path);
            }

            // Save params
            // This is complicated enough (and probably also a bit wrong) such that WriteModDependentBnd is too simple.
            {
                string basePath = $@"{Dir}\Base\Data0.bdt";
                if (Mods.Resolve($@"param\gameparam\gameparam.parambnd.dcx", out string modPath))
                {
                    basePath = modPath;
                }
                else if (Mods.Resolve($@"Data0.bdt", out modPath))
                {
                    basePath = modPath;
                }
                string path = encrypted ? $@"{outPath}\Data0.bdt" : $@"{outPath}\param\gameparam\gameparam.parambnd.dcx";
                AddModFile(path);
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.Write());
            }

            // Messages
            foreach (KeyValuePair<string, FMGDictionary> entry in AllItemFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Base\msg\{entry.Key}\item_dlc2.msgbnd.dcx", $@"msg\{entry.Key}\item_dlc2.msgbnd.dcx", entry.Value.FMGs);
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

            MergeMods(outPath);
            Console.WriteLine("Success!");
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

        void WriteModDependentBnd<T>(string outPath, string basePath, string relOutputPath, Dictionary<string, T> diffData)
            where T : SoulsFile<T>, new()
        {
            if (Mods.Resolve(relOutputPath, out string modPath))
            {
                basePath = modPath;
            }
            string path = $@"{outPath}\{relOutputPath}";
            AddModFile(path);
            Editor.OverrideBnd(basePath, Path.GetDirectoryName(path), diffData, f => f.Write());
        }

        private void MergeMods(string outPath)
        {
            Console.WriteLine("Processing extra mod files...");
            bool work = false;
            // If using this feature, assume a single mod dir for now
            string modDir = Mods.Dirs.FirstOrDefault();
            if (modDir != null)
            {
                foreach (string gameFile in MiscSetup.GetGameFiles(modDir, Sekiro))
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
            foreach (string gameFile in MiscSetup.GetGameFiles(outPath, Sekiro))
            {
                string target = FullName($@"{outPath}\{gameFile}");
                if (writtenFiles.Contains(target)) continue;
                Console.WriteLine($"Found extra file (delete it if you don't want it): {target}");
                work = true;
            }
            if (!work) Console.WriteLine("No extra files found");
        }

        private void LoadNames()
        {
            modelNames = new SortedDictionary<string, string>(Editor.LoadNames("ModelName", n => n, false));
            characterSplits = new SortedDictionary<int, string>(Editor.LoadNames("CharaInitParam", n => int.Parse(n), true));
            lotNames = new SortedDictionary<int, string>(Editor.LoadNames("ItemLotParam", n => int.Parse(n), true));
            qwcNames = new SortedDictionary<int, string>(Editor.LoadNames("ShopQwc", n => int.Parse(n), true));
            for (int i = 0; i < itemParams.Count; i++)
            {
                if (!EldenRing && itemParams[i] == "EquipParamGem") continue;
                foreach (KeyValuePair<ItemKey, string> entry in Editor.LoadNames(itemParams[i], n => new ItemKey((ItemType)i, int.Parse(n)), true))
                {
                    ItemNames[entry.Key] = entry.Value;
                    AddMulti(RevItemNames, entry.Value, entry.Key);
                }
            }
            if (characterSplits.Count == 0)
            {
                characterSplits[0] = "UNUSED";
            }
            if (EldenRing || AC6)
            {
                LocationNames = Editor.LoadNames("MapName", n => n, false);
                // For now, don't have special location names, but we can maybe do this for legacy dungeons or have prefixes
                Locations = LocationNames.ToDictionary(e => e.Key, e => e.Key);
            }
            else if (DS3)
            {
                LocationNames = DS3MapNames;
                Locations = DS3LocationNames;
            }
            else if (Sekiro)
            {
                LocationNames = SekiroMapNames;
                Locations = SekiroLocationNames;
            }
            RevLocations = Locations.ToDictionary(e => e.Value, e => e.Key);
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
            bool lazy = !DS3;
            Dictionary<string, PARAM> dict;
            string path;
            if (!lazy)
            {
                // Delay loading layouts if we'll do it in ParamDictionary
                LoadLayouts();
            }
            if (DS3)
            {
                path = $@"{Dir}\Base\Data0.bdt";
                if (Mods.Resolve(@"param\gameparam\gameparam.parambnd.dcx", out string modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                else if (Mods.Resolve("Data0.bdt", out modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file: {path}");
                }
                dict = Editor.LoadParams(path, layouts: Layouts, allowError: true);
            }
            else if (Sekiro)
            {
                path = $@"{Dir}\Base\gameparam.parambnd.dcx";
                if (Mods.Resolve(@"param\gameparam\gameparam.parambnd.dcx", out string modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file: {path}");
                }
                dict = Editor.LoadParams(path, layouts: Layouts, allowError: false);
            }
            else if (EldenRing || AC6)
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
                Maps = Editor.Load("Base", path => (IMsb)MSBS.Read(path), "*.msb.dcx");
                MaybeOverrideFromModDir(Maps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBS.Read(path));
                List<string> missing = Locations.Keys.Except(Maps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dists\Base: {string.Join(", ", missing)}");
            }
            else if (DS3)
            {
                Maps = Editor.Load("Base", path => (IMsb)MSB3.Read(path), "*.msb.dcx");
                MaybeOverrideFromModDir(Maps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSB3.Read(path));
                List<string> missing = Locations.Keys.Except(Maps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dist\Base: {string.Join(", ", missing)}");
            }
            else if (AC6)
            {
#if DEV
                Maps = Editor.Load("Vanilla", path => (IMsb)MSBAC6.Read(path), "*.msb.dcx");
#endif
            }
            else
            {
                Maps = Editor.Load("Vanilla", path => (IMsb)MSBE.Read(path), "*.msb.dcx");
                MaybeOverrideFromModDir(Maps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBE.Read(path));

                // Set up copying dupe maps which are not included in our vanilla files
                Regex lastRe = new Regex(@"_1([0-2])$");
                MapDupes = dupeMsbs
                    .Where(m => !Maps.ContainsKey(m))
                    .ToDictionary(m => lastRe.Replace(m, @"_0$1"), m => m);
                if (MapDupes.Any(e => e.Key == e.Value))
                {
                    throw new Exception($"Invalid dupe map {string.Join(" ", MapDupes)}");
                }
            }
        }

        private bool UseVanilla => EldenRing || AC6;

        private void LoadTalk()
        {
            if (!DS3 && !AC6)
            {
                Talk = Editor.LoadBnds(UseVanilla ? "Vanilla" : "Base", (data, path) => ESD.Read(data), "*.talkesdbnd.dcx");
                MaybeOverrideFromModDir(Talk, name => $@"script\talk\{name}.talkesdbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => ESD.Read(data)));
                if (Sekiro)
                {
                    List<string> missing = Locations.Keys.Concat(new[] { "m00_00_00_00" }).Except(Talk.Keys).ToList();
                    if (missing.Count != 0) throw new Exception($@"Missing talkesdbnds in dist\Base: {string.Join(", ", missing)}");
                }
            }
        }

        private void LoadScripts()
        {
            Emevds = Editor.Load(UseVanilla ? "Vanilla" : "Base", path => EMEVD.Read(path), "*.emevd.dcx");
            MaybeOverrideFromModDir(Emevds, name => $@"event\{name}.emevd.dcx", path => EMEVD.Read(path));
            if (!EldenRing && !AC6)
            {
                List<string> missing = Locations.Keys.Concat(new[] { "common", "common_func" }).Except(Emevds.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing emevds in dist\Base: {string.Join(", ", missing)}");
            }
            if (EldenRing && false)
            {
                EMEVD template = Emevds.Where(e => e.Key.StartsWith("m")).Select(e => e.Value).FirstOrDefault();
                if (template == null) throw new Exception(@"Missing emevds in diste\Vanilla");
                // TODO: For this to work, we'd need to modify the loadlist, unfortunately
                foreach (string map in Maps.Keys)
                {
                    if (!Emevds.ContainsKey(map))
                    {
                        EMEVD emevd = new EMEVD
                        {
                            Format = template.Format,
                            Compression = template.Compression,
                            StringData = template.StringData,
                        };
                        Emevds[map] = emevd;
                    }
                }
            }
        }

        private void LoadText()
        {
            // TODO: Surely we can merge these
            FMGDictionary read(string path)
            {
                Dictionary<string, byte[]> fmgBytes = Editor.LoadBnd(path, (data, _) => data);
                return new FMGDictionary { Inner = fmgBytes };
            }
            if (Sekiro)
            {
                // Sekiro has a different location for English fmgs
                FMGDictionary itemFmgs = read($@"{Dir}\Base\item.msgbnd.dcx");
                itemFmgs = MaybeOverrideFromModDir(itemFmgs, @"msg\engus\item.msgbnd.dcx", read);
                AllItemFMGs["engus"] = itemFmgs;
                FMGDictionary menuFmgs = read($@"{Dir}\Base\menu.msgbnd.dcx");
                menuFmgs = MaybeOverrideFromModDir(menuFmgs, @"msg\engus\menu.msgbnd.dcx", read);
                AllMenuFMGs["engus"] = menuFmgs;
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (lang == "engus") continue;
                    AllItemFMGs[lang] = read($@"{Dir}\Base\msg\{lang}\item.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item.msgbnd.dcx", read);
                }
            }
            else if (DS3)
            {
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (MiscSetup.NoDS3Langs.Contains(lang)) continue;
                    AllItemFMGs[lang] = read($@"{Dir}\Base\msg\{lang}\item_dlc2.msgbnd.dcx");
                    AllItemFMGs[lang] = MaybeOverrideFromModDir(AllItemFMGs[lang], $@"msg\{lang}\item_dlc2.msgbnd.dcx", read);
                }
            }
            else if (EldenRing || AC6)
            {
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    // TODO: Multilang if needed
                    if (AC6 && lang != "engus") continue;
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
                Console.WriteLine($"Using modded file {modPath}");
                return parser(modPath);
            }
            return original;
        }

        private void MaybeOverrideFromModDir<T>(Dictionary<string, T> files, Func<string, string> relpath, Func<string, T> parser)
        {
            if (Mods.Count == 0) return;
            foreach (string key in files.Keys.ToList())
            {
                files[key] = MaybeOverrideFromModDir(files[key], relpath(key), parser);
            }
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

        public void DumpMessages(string dir)
        {
            foreach (string path in Directory.GetFiles(dir, "*.msgbnd*"))
            {
                if (path.Contains("dlc1")) continue;
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
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
