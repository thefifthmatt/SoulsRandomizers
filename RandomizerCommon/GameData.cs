using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class GameData
    {
        private static readonly List<string> itemParams = new List<string>()
        {
            "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "EquipParamGoods", "EquipParamGem",
        };

        public readonly GameEditor Editor;
        public FromGame Type => Editor.Spec.Game;
        public bool Sekiro => Type == FromGame.SDT;
        public bool DS3 => Type == FromGame.DS3;
        public bool EldenRing => Type == FromGame.ER;

        public readonly string Dir;
        private string ModDir;

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
            [6] = ItemType.EQUIP,
        };

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
        public Dictionary<string, FMG> ItemFMGs = new Dictionary<string, FMG>();
        public Dictionary<string, FMG> MenuFMGs = new Dictionary<string, FMG>();
        public Dictionary<string, Dictionary<string, FMG>> OtherItemFMGs = new Dictionary<string, Dictionary<string, FMG>>();
        public Dictionary<string, Dictionary<string, FMG>> OtherMenuFMGs = new Dictionary<string, Dictionary<string, FMG>>();
        public Dictionary<string, Dictionary<string, ESD>> Talk = new Dictionary<string, Dictionary<string, ESD>>();

        // Lazily applies paramdefs
        public class ParamDictionary
        {
            public Dictionary<string, PARAM> Inner = new Dictionary<string, PARAM>();
            public Dictionary<string, PARAM.Layout> Layouts { get; set; }
            public Dictionary<string, PARAMDEF> Defs { get; set; }

            public PARAM this[string key]
            {
                get
                {
                    if (!Inner.TryGetValue(key, out PARAM param)) throw new Exception($"Internal error: Param {key} not found");
                    if (param.AppliedParamdef == null)
                    {
                        if (Defs != null && param.ApplyParamdefCarefully(Defs.Values))
                        {
                            // It worked
                        }
                        else if (Layouts != null && Layouts.TryGetValue(param.ParamType, out PARAM.Layout layout))
                        {
                            param.ApplyParamdef(layout.ToParamdef(param.ParamType, out _));
                        }
                        else throw new Exception($"Internal error: Param {key} has no def file");
                    }
                    return param;
                }
            }
            public bool ContainsKey(string key) => Inner.ContainsKey(key);
            public IEnumerable<string> Keys => Inner.Keys;
        }

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
            if (EldenRing)
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
        public Dictionary<string, MSBX> EldenMaps => Maps.ToDictionary(e => e.Key, e => e.Value as MSBX);

        public void Load(string modDir=null)
        {
            ModDir = modDir;
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
            if (key.Type == ItemType.EQUIP) return null;
            return Params[itemParams[(int) key.Type]][key.ID];
        }

        public PARAM.Row AddRow(string name, int id, int oldId = -1)
        {
            PARAM param = Params[name];
            if (param[id] != null)
            {
                // This can get quadratic? But eh good to check
                throw new Exception($"Trying to add id {id} in {name} but already exists");
            }
            PARAM.Row row = new PARAM.Row(id, "", param.AppliedParamdef);
            param.Rows.Add(row);
            if (oldId >= 0)
            {
                GameEditor.CopyRow(param[oldId], row);
            }
            return row;
        }

        private static ItemKey NormalizeWeapon(ItemKey key)
        {
            // Maybe can put this logic in ItemKey itself
            if (key.Type == ItemType.WEAPON && key.ID % 100 != 0)
            {
                return new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            return key;
        }

        public string Name(ItemKey key)
        {
            string suffix = "";
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
            if (key.Type == ItemType.EQUIP)
            {
                return "NPC Equipment";
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
            if (EldenRing)
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

        public string EntityName(EntityId entity, bool detail = false, bool mapName = false)
        {
            string mapSuffix = mapName && !string.IsNullOrEmpty(entity.MapName) ? " in " + MapLocationName(entity.MapName) : "";
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
                string fmgName = ItemFMGs["NpcName"][entity.NameID];
                if (!string.IsNullOrEmpty(fmgName))
                {
                    details.Add($"<{fmgName}>");
                }
            }
            return (entity.Type == null ? "" : $"{entity.Type} ")
                + entity.EntityName
                + (details.Count > 0 ? $" ({string.Join(" - ", details)})" : "")
                + mapSuffix;
        }

        public string MapLocationName(string mapId)
        {
            return $"{mapId}" + (LocationNames.TryGetValue(mapId, out string mapName) ? $" ({mapName})" : "");
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
            WriteModDependentBnd(outPath, $@"{Dir}\Base\item.msgbnd.dcx", $@"msg\engus\item.msgbnd.dcx", ItemFMGs);
            WriteModDependentBnd(outPath, $@"{Dir}\Base\menu.msgbnd.dcx", $@"msg\engus\menu.msgbnd.dcx", MenuFMGs);
            foreach (KeyValuePair<string, Dictionary<string, FMG>> entry in OtherItemFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Base\msg\{entry.Key}\item.msgbnd.dcx", $@"msg\{entry.Key}\item.msgbnd.dcx", entry.Value);
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
        public void SaveEldenRing(string outPath, bool uxm)
        {
            Console.WriteLine("Writing to " + outPath);
            // Sorry TK, Oodle is 2slow
            DCX.Type overrideDcx = DCX.Type.DCX_DFLT_11000_44_9;
            // overrideDcx = DCX.Type.DCX_KRAK;
            writtenFiles.Clear();
            {
                string basePath = $@"{Dir}\Vanilla\regulation.bin";
                string path = $@"{outPath}\regulation.bin";
                if (ModDir != null)
                {
                    string modPath = $@"{ModDir}\regulation.bin.randobak";
                    string modPath2 = $@"{ModDir}\regulation.bin";
                    if (File.Exists(modPath)) basePath = modPath;
                    else if (File.Exists(modPath2)) basePath = modPath2;
                }
                AddModFile(path);
                if (uxm) Backup(path);
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.AppliedParamdef == null ? null : f.Write(), dcx: overrideDcx);
            }

            // Event scripts
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                if (!AddBackupOrRestoreFile(path, WriteEmevds.Contains(entry.Key), uxm)) continue;
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

            // ESDs
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in Talk)
            {
                string path = $@"{outPath}\script\talk\{entry.Key}.talkesdbnd.dcx";
                if (!AddBackupOrRestoreFile(path, WriteESDs.Contains(entry.Key), uxm)) continue;
                string basePath = $@"{Dir}\Vanilla\{entry.Key}.talkesdbnd.dcx";
                Editor.OverrideBndRel(basePath, path, entry.Value, f => f.Write(), dcx: overrideDcx);
            }

            // Maps
            foreach (KeyValuePair<string, IMsb> entry in Maps)
            {
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                if (!AddBackupOrRestoreFile(path, WriteMSBs.Contains(entry.Key), uxm)) continue;
                entry.Value.Write(path, overrideDcx);
            }

            // Text
            {
                // Just menu for now, and no override for now. Also other languages
                string path = $@"{outPath}\msg\engus\menu.msgbnd.dcx";
                if (AddBackupOrRestoreFile(path, WriteFMGs, uxm))
                {
                    string basePath = $@"{Dir}\Vanilla\msg\engus\menu.msgbnd.dcx";
                    Editor.OverrideBndRel(basePath, path, MenuFMGs, f => f.Write(), dcx: overrideDcx);
                }
                foreach (KeyValuePair<string, Dictionary<string, FMG>> entry in OtherMenuFMGs)
                {
                    path = $@"{outPath}\msg\{entry.Key}\menu.msgbnd.dcx";
                    if (AddBackupOrRestoreFile(path, WriteFMGs, uxm))
                    {
                        string basePath = $@"{Dir}\Vanilla\msg\{entry.Key}\menu.msgbnd.dcx";
                        Editor.OverrideBndRel(basePath, path, entry.Value, f => f.Write(), dcx: overrideDcx);
                    }
                }
            }
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

        private bool AddBackupOrRestoreFile(string path, bool write, bool uxm)
        {
            if (write)
            {
                AddModFile(path);
                if (uxm) Backup(path);
                return true;
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
            return false;
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
                if (ModDir != null)
                {
                    string modPath1 = $@"{ModDir}\param\gameparam\gameparam.parambnd.dcx";
                    string modPath2 = $@"{ModDir}\Data0.bdt";
                    if (File.Exists(modPath1)) basePath = modPath1;
                    else if (File.Exists(modPath2)) basePath = modPath2;
                }
                string path = encrypted ? $@"{outPath}\Data0.bdt" : $@"{outPath}\param\gameparam\gameparam.parambnd.dcx";
                AddModFile(path);
                Editor.OverrideBndRel(basePath, path, Params.Inner, f => f.Write());
            }

            // Messages
            WriteModDependentBnd(outPath, $@"{Dir}\Base\msg\engus\item_dlc2.msgbnd.dcx", $@"msg\engus\item_dlc2.msgbnd.dcx", ItemFMGs);
            foreach (KeyValuePair<string, Dictionary<string, FMG>> entry in OtherItemFMGs)
            {
                WriteModDependentBnd(outPath, $@"{Dir}\Base\msg\{entry.Key}\item_dlc2.msgbnd.dcx", $@"msg\{entry.Key}\item_dlc2.msgbnd.dcx", entry.Value);
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
//#if !DEBUG
            Console.WriteLine($"Writing {path}");
//#endif
            writtenFiles.Add(path);
        }

        void WriteModDependentBnd<T>(string outPath, string basePath, string relOutputPath, Dictionary<string, T> diffData)
            where T : SoulsFile<T>, new()
        {
            if (ModDir != null)
            {
                string modPath = $@"{ModDir}\{relOutputPath}";
                if (File.Exists(modPath)) basePath = modPath;
            }
            string path = $@"{outPath}\{relOutputPath}";
            AddModFile(path);
            Editor.OverrideBnd(basePath, Path.GetDirectoryName(path), diffData, f => f.Write());
        }

        private void MergeMods(string outPath)
        {
            Console.WriteLine("Processing extra mod files...");
            bool work = false;
            if (ModDir != null)
            {
                foreach (string gameFile in MiscSetup.GetGameFiles(ModDir, Sekiro))
                {
                    string source = FullName($@"{ModDir}\{gameFile}");
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
            if (EldenRing)
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
            bool lazy = true;
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
                string modPath1 = $@"{ModDir}\param\gameparam\gameparam.parambnd.dcx";
                string modPath2 = $@"{ModDir}\Data0.bdt";
                if (ModDir != null && File.Exists(modPath1))
                {
                    Console.WriteLine($"Using modded file {modPath1}");
                    path = modPath1;
                }
                else if (ModDir != null && File.Exists(modPath2))
                {
                    Console.WriteLine($"Using modded file {modPath2}");
                    path = modPath2;
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
                string modPath = $@"{ModDir}\param\gameparam\gameparam.parambnd.dcx";
                if (ModDir != null && File.Exists(modPath))
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
            else if (EldenRing)
            {
                path = $@"{Dir}\Vanilla\regulation.bin";
                string modPath = $@"{ModDir}\regulation.bin.randobak";
                string modPath2 = $@"{ModDir}\regulation.bin";
                if (ModDir != null && File.Exists(modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
                else if (ModDir != null && File.Exists(modPath2))
                {
                    Console.WriteLine($"Using modded file {modPath2}");
                    path = modPath2;
                }
                if (!File.Exists(path))
                {
                    throw new Exception($"Missing param file: {path}");
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
            else
            {
                Maps = Editor.Load("Vanilla", path => (IMsb)MSBX.Read(path), "*.msb.dcx");
            }
        }

        private void LoadTalk()
        {
            if (!DS3)
            {
                Talk = Editor.LoadBnds(EldenRing ? "Vanilla" : "Base", (data, path) => ESD.Read(data), "*.talkesdbnd.dcx");
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
            Emevds = Editor.Load(EldenRing ? "Vanilla" : "Base", path => EMEVD.Read(path), "*.emevd.dcx");
            MaybeOverrideFromModDir(Emevds, name => $@"event\{name}.emevd.dcx", path => EMEVD.Read(path));
            if (!EldenRing)
            {
                List<string> missing = Locations.Keys.Concat(new[] { "common", "common_func" }).Except(Emevds.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing emevds in dist\Base: {string.Join(", ", missing)}");
            }
        }

        private void LoadText()
        {
            // TODO: Surely we can merge these
            if (Sekiro)
            {
                ItemFMGs = Editor.LoadBnd($@"{Dir}\Base\item.msgbnd.dcx", (data, path) => FMG.Read(data));
                ItemFMGs = MaybeOverrideFromModDir(ItemFMGs, @"msg\engus\item.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                MenuFMGs = Editor.LoadBnd($@"{Dir}\Base\menu.msgbnd.dcx", (data, path) => FMG.Read(data));
                MenuFMGs = MaybeOverrideFromModDir(MenuFMGs, @"msg\engus\menu.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (lang == "engus") continue;
                    OtherItemFMGs[lang] = Editor.LoadBnd($@"{Dir}\Base\msg\{lang}\item.msgbnd.dcx", (data, path) => FMG.Read(data));
                    OtherItemFMGs[lang] = MaybeOverrideFromModDir(OtherItemFMGs[lang], $@"msg\{lang}\item.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                }
            }
            else if (DS3)
            {
                ItemFMGs = Editor.LoadBnd($@"{Dir}\Base\msg\engus\item_dlc2.msgbnd.dcx", (data, path) => FMG.Read(data));
                ItemFMGs = MaybeOverrideFromModDir(ItemFMGs, @"msg\engus\item_dlc2.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (lang == "engus" || MiscSetup.NoDS3Langs.Contains(lang)) continue;
                    OtherItemFMGs[lang] = Editor.LoadBnd($@"{Dir}\Base\msg\{lang}\item_dlc2.msgbnd.dcx", (data, path) => FMG.Read(data));
                    OtherItemFMGs[lang] = MaybeOverrideFromModDir(OtherItemFMGs[lang], $@"msg\{lang}\item_dlc2.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                }
            }
            else if (EldenRing)
            {
                ItemFMGs = Editor.LoadBnd($@"{Dir}\Vanilla\msg\engus\item.msgbnd.dcx", (data, path) => FMG.Read(data));
                MenuFMGs = Editor.LoadBnd($@"{Dir}\Vanilla\msg\engus\menu.msgbnd.dcx", (data, path) => FMG.Read(data));
                foreach (string lang in MiscSetup.Langs.Keys)
                {
                    if (lang == "engus") continue;
                    OtherMenuFMGs[lang] = Editor.LoadBnd($@"{Dir}\Vanilla\msg\{lang}\menu.msgbnd.dcx", (data, path) => FMG.Read(data));
                }
            }
        }

        // TODO: Instead of doing this, make the paths themselves more editable?
        private T MaybeOverrideFromModDir<T>(T original, string path, Func<string, T> parser)
        {
            if (ModDir == null || EldenRing) return original;
            string modPath = $@"{ModDir}\{path}";
            if (File.Exists(modPath))
            {
                Console.WriteLine($"Using modded file {modPath}");
                return parser(modPath);
            }
            return original;
        }

        private void MaybeOverrideFromModDir<T>(Dictionary<string, T> files, Func<string, string> relpath, Func<string, T> parser)
        {
            if (ModDir == null || EldenRing) return;
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
