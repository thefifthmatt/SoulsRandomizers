using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SoulsIds;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class GameData
    {
        private static readonly List<string> itemParams = new List<string>() { "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "EquipParamGoods" };
        // TODO: Is this really needed?
        private static readonly List<string> loadParams = itemParams.Concat(new List<string> {
            // Data scraper and elsewhere
            "ShopLineupParam",
            "ItemLotParam",
            "EquipMtrlSetParam",
            "NpcParam",
            // Misc edits
            "ActionButtonParam",
            // Character writing
            "CharaInitParam",
            "Magic",
            "ReinforceParamWeapon",
        }).ToList();

        public readonly bool Sekiro;
        public readonly GameEditor Editor;

        // TODO: Merge with GameEditor for DS3, to get rid of a lot of this.
        private string dir;
        private string modDir;
        // private BND4 regulation;
        // private bool encrypted;

        // Informational data
        private static readonly Dictionary<string, string> ds3LocationNames = new Dictionary<string, string>
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
            { "m51_01_00_00", "filianore" }
        };
        private static readonly Dictionary<string, string> sekiroLocationNames = new Dictionary<string, string>
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
        private static Dictionary<string, string> sekiroMapName = new Dictionary<string, string>
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

        public readonly Dictionary<string, string> Locations;
        public readonly Dictionary<string, string> RevLocations;
        public readonly Dictionary<string, string> LocationNames;

        // Actual data
        private Dictionary<string, PARAM.Layout> layouts = new Dictionary<string, PARAM.Layout>();
        public Dictionary<string, PARAM> Params = new Dictionary<string, PARAM>();
        public Dictionary<string, MSB3> Maps = new Dictionary<string, MSB3>();
        public Dictionary<string, MSBS> Smaps = new Dictionary<string, MSBS>();
        public Dictionary<string, EMEVD> Emevds = new Dictionary<string, EMEVD>();
        public Dictionary<string, FMG> ItemFMGs = new Dictionary<string, FMG>();
        public Dictionary<string, FMG> MenuFMGs = new Dictionary<string, FMG>();
        public Dictionary<string, Dictionary<string, ESD>> Talk = new Dictionary<string, Dictionary<string, ESD>>();

        // Names
        private SortedDictionary<ItemKey, string> itemNames = new SortedDictionary<ItemKey, string>();
        private SortedDictionary<string, List<ItemKey>> revItemNames = new SortedDictionary<string, List<ItemKey>>();
        private SortedDictionary<int, string> qwcNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> lotNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> characterSplits = new SortedDictionary<int, string>();
        private SortedDictionary<string, string> modelNames = new SortedDictionary<string, string>();

        private List<string> writtenFiles = new List<string>();

        public GameData(string dir, bool sekiro)
        {
            this.dir = dir;
            Sekiro = sekiro;
            Editor = new GameEditor(sekiro ? GameSpec.FromGame.SDT : GameSpec.FromGame.DS3);
            Editor.Spec.GameDir = $@"{dir}";
            Editor.Spec.NameDir = $@"{dir}\Names";
            Editor.Spec.LayoutDir = $@"{dir}\Layouts";
            Locations = Sekiro ? sekiroLocationNames : ds3LocationNames;
            RevLocations = Locations.ToDictionary(e => e.Value, e => e.Key);
            LocationNames = Sekiro ? sekiroMapName : null;

    }

        public void Load(string modDir=null)
        {
            this.modDir = modDir;
            LoadNames();
            LoadLayouts();
            LoadParams();
            LoadMapData();
            LoadTalk();
            LoadScripts();
            LoadText();
        }

        public void UnDcx(string dir)
        {
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
            return Params[itemParams[(int) key.Type]][key.ID];
        }

        public PARAM.Row AddRow(string name, int id)
        {
            PARAM param = Params[name];
            if (param[id] != null)
            {
                // This can get quadratic? But eh good to check
                throw new Exception($"Trying to add id {id} in {name} but already exists");
            }
            PARAM.Row row = new PARAM.Row(id, "", param.AppliedParamdef);
            param.Rows.Add(row);
            return row;
        }

        public StreamReader NewAnnotationReader()
        {
            string testFile = $@"{dir}\Base\annotations.txt";
            if (File.Exists(testFile)) return File.OpenText(testFile);
            return File.OpenText($@"{dir}\Base\annotations.yaml");
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
            return (itemNames.ContainsKey(key) ? itemNames[key] : $"?ITEM?" + $" ({(int)key.Type}:{key.ID})") + suffix;
        }

        private static readonly Dictionary<ItemKey, string> customNames = new Dictionary<ItemKey, string>
        {
            { new ItemKey(ItemType.GOOD, 2123), "Cinders of a Lord (Abyss Watchers)" },
            { new ItemKey(ItemType.GOOD, 2124), "Cinders of a Lord (Aldrich)" },
            { new ItemKey(ItemType.GOOD, 2125), "Cinders of a Lord (Yhorm)" },
            { new ItemKey(ItemType.GOOD, 2126), "Cinders of a Lord (Lothric)" },
        };
        public string DisplayName(ItemKey key)
        {
            if (!Sekiro && customNames.TryGetValue(key, out string name))
            {
                return name;
            }
            return Name(key);
        }


        public ItemKey ItemForName(string name)
        {
            if (!revItemNames.ContainsKey(name)) throw new Exception($"Internal error: missing name {name}");
            if (revItemNames[name].Count != 1) throw new Exception($"Internal error: ambiguous name {name} could be {string.Join(" or ", revItemNames[name])}");
            return revItemNames[name][0];
        }

        public SortedDictionary<ItemKey, string> Names()
        {
            return itemNames;
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
            return name == "UNUSED" ? "?CHARACTER?" : name;
        }

        public string ModelName(string chr)
        {
            return modelNames.TryGetValue(chr, out string m) ? m : "???";
        }

        public string EntityName(EntityId entity, bool detail=false)
        {
            int split = entity.EntityName.IndexOf('_');
            if (split == -1)
            {
                return entity.EntityName;
            }
            string model = entity.EntityName.Substring(0, split);
            string modelDisplay = model;
            if (modelNames.ContainsKey(model))
            {
                modelDisplay = modelNames[model];
            }
            if (!detail)
            {
                return modelDisplay;
            }
            List<int> ids = entity.GetEntityIds();
            string idInfo = ids.Count == 0 ? "" : $" #{string.Join(",", ids)}";
            string info = $" ({entity.EntityName}{idInfo})";
            if (model == "c0000" && entity.CharaInitID != -1) {
                return CharacterName(entity.CharaInitID) + info;
            }
            return modelDisplay + info;
        }

        public void SaveSekiro(string outPath)
        {
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            foreach (KeyValuePair<string, MSBS> entry in Smaps)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                AddModFile(path);
                entry.Value.Write(path, (DCX.Type)DCX.DefaultType.Sekiro);
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in Talk)
            {
                if (!Locations.ContainsKey(entry.Key)) continue;
                string basePath = $@"{dir}\Base\{entry.Key}.talkesdbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\script\talk\{entry.Key}.talkesdbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\script\talk\{entry.Key}.talkesdbnd.dcx";
                AddModFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\script\talk", entry.Value, esd => esd.Write());
            }
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddModFile(path);
                entry.Value.Write(path, (DCX.Type)DCX.DefaultType.Sekiro);
#if DEBUG
                // This is only needed for easier use of EventScriptTool
                entry.Value.Write($@"{outPath}\event\{entry.Key}.emevd", DCX.Type.None);
#endif
            }
            {
                string basePath = $@"{dir}\Base\gameparam.parambnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\param\gameparam\gameparam.parambnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\param\gameparam\gameparam.parambnd.dcx";
                AddModFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\param\gameparam", Params, f => f.Write());
            }
            // At least for now, only do FMGs for English
            {
                string basePath = $@"{dir}\Base\item.msgbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\msg\engus\item.msgbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\msg\engus\item.msgbnd.dcx";
                AddModFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\msg\engus", ItemFMGs, f => f.Write());
            }
            {
                string basePath = $@"{dir}\Base\menu.msgbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\msg\engus\menu.msgbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\msg\engus\menu.msgbnd.dcx";
                AddModFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\msg\engus", MenuFMGs, f => f.Write());
            }

            MergeMods(outPath);
            Console.WriteLine("Success!");
        }

        public void SaveDS3(string outPath, bool encrypted)
        {
            // This is a bit duplicate of Sekiro code, although boilerplate is also pretty plain to read.
            Console.WriteLine("Writing to " + outPath);
            writtenFiles.Clear();

            // Save params
            {
                string basePath = $@"{dir}\Base\Data0.bdt";
                if (modDir != null)
                {
                    string modPath1 = $@"{modDir}\param\gameparam\gameparam.parambnd.dcx";
                    string modPath2 = $@"{modDir}\Data0.bdt";
                    if (File.Exists(modPath1)) basePath = modPath1;
                    else if (File.Exists(modPath2)) basePath = modPath2;
                }
                string path = encrypted ? $@"{outPath}\Data0.bdt" : $@"{outPath}\param\gameparam\gameparam.parambnd.dcx";
                AddModFile(path);
                Editor.OverrideBndRel(basePath, path, Params, f => f.Write());
            }

            // Messages
            // At least for now, only do FMGs for English
            {
                string basePath = $@"{dir}\Base\item_dlc2.msgbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\msg\engus\item_dlc2.msgbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\msg\engus\item_dlc2.msgbnd.dcx";
                AddModFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\msg\engus", ItemFMGs, f => f.Write());
            }

            // Event scripts
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                AddModFile(path);
                entry.Value.Write(path);
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
            Console.WriteLine($"Writing {path}");
            writtenFiles.Add(path);
        }
        private void MergeMods(string outPath)
        {
            Console.WriteLine("Processing extra mod files...");
            bool work = false;
            if (modDir != null)
            {
                foreach (string gameFile in MiscSetup.GetGameFiles(modDir))
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
            foreach (string gameFile in MiscSetup.GetGameFiles(outPath))
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
            characterSplits = new SortedDictionary<int, string>(Editor.LoadNames("CharaInitParam", n => int.Parse(n), false));
            lotNames = new SortedDictionary<int, string>(Editor.LoadNames("ItemLotParam", n => int.Parse(n), true));
            qwcNames = new SortedDictionary<int, string>(Editor.LoadNames("ShopQwc", n => int.Parse(n), true));
            for (int i = 0; i < itemParams.Count; i++)
            {
                foreach (KeyValuePair<ItemKey, string> entry in Editor.LoadNames(itemParams[i], n => new ItemKey((ItemType)i, int.Parse(n))))
                {
                    itemNames[entry.Key] = entry.Value;
                    AddMulti(revItemNames, entry.Value, entry.Key);
                }
            }
            if (characterSplits.Count == 0)
            {
                characterSplits[0] = "UNUSED";
            }
        }

        // https://github.com/JKAnderson/Yapped/blob/master/Yapped/FormMain.cs
        private void LoadLayouts()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Layouts", "*.xml"))
            {
                string paramID = Path.GetFileNameWithoutExtension(path);
                try
                {
                    PARAM.Layout layout = PARAM.Layout.ReadXMLFile(path);
                    layouts[paramID] = layout;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load layout {paramID}.txt\r\n\r\n{ex}");
                }
            }
        }

        private void LoadParams()
        {
            string path;
            if (Sekiro)
            {
                path = $@"{dir}\Base\gameparam.parambnd.dcx";
                string modPath = $@"{modDir}\param\gameparam\gameparam.parambnd.dcx";
                if (File.Exists(modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
            }
            else
            {
                path = $@"{dir}\Base\Data0.bdt";
                string modPath1 = $@"{modDir}\param\gameparam\gameparam.parambnd.dcx";
                string modPath2 = $@"{modDir}\Data0.bdt";
                if (File.Exists(modPath1))
                {
                    Console.WriteLine($"Using modded file {modPath1}");
                    path = modPath1;
                }
                else if (File.Exists(modPath2))
                {
                    Console.WriteLine($"Using modded file {modPath2}");
                    path = modPath2;
                }
            }
            if (!File.Exists(path))
            {
                throw new Exception($"Missing param file: {path}");
            }
            Params = Editor.LoadParams(path, layouts, allowError: !Sekiro);
        }

        private void LoadMapData()
        {
            if (Sekiro)
            {
                Smaps = Editor.Load("Base", path => MSBS.Read(path), "*.msb.dcx");
                MaybeOverrideFromModDir(Smaps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBS.Read(path));
                List<string> missing = Locations.Keys.Except(Smaps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dists\Base: {string.Join(", ", missing)}");
            }
            else
            {
                Maps = Editor.Load("Base", path => MSB3.Read(path), "*.msb.dcx");
                MaybeOverrideFromModDir(Maps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSB3.Read(path));
                List<string> missing = Locations.Keys.Except(Maps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dist\Base: {string.Join(", ", missing)}");
            }
        }

        private void LoadTalk()
        {
            if (Sekiro)
            {
                Talk = Editor.LoadBnds("Base", (data, path) => ESD.Read(data), "*.talkesdbnd.dcx");
                MaybeOverrideFromModDir(Talk, name => $@"script\talk\{name}.talkesdbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => ESD.Read(data)));
                List<string> missing = Locations.Keys.Except(Talk.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing talkesdbnds in dist\Base: {string.Join(", ", missing)}");
            }
        }

        private void LoadScripts()
        {
            Emevds = Editor.Load("Base", path => EMEVD.Read(path), "*.emevd.dcx");
            MaybeOverrideFromModDir(Emevds, name => $@"event\{name}.emevd.dcx", path => EMEVD.Read(path));
            List<string> missing = Locations.Keys.Concat(new[] { "common", "common_func" }).Except(Emevds.Keys).ToList();
            if (missing.Count != 0) throw new Exception($@"Missing emevds in dist\Base: {string.Join(", ", missing)}");
        }

        private void LoadText()
        {
            if (Sekiro)
            {
                ItemFMGs = Editor.LoadBnd($@"{dir}\Base\item.msgbnd.dcx", (data, path) => FMG.Read(data));
                ItemFMGs = MaybeOverrideFromModDir(ItemFMGs, @"msg\engus\item.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
                MenuFMGs = Editor.LoadBnd($@"{dir}\Base\menu.msgbnd.dcx", (data, path) => FMG.Read(data));
                MenuFMGs = MaybeOverrideFromModDir(MenuFMGs, @"msg\engus\menu.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
            }
            else
            {
                ItemFMGs = Editor.LoadBnd($@"{dir}\Base\item_dlc2.msgbnd.dcx", (data, path) => FMG.Read(data));
                ItemFMGs = MaybeOverrideFromModDir(ItemFMGs, @"msg\engus\item_dlc2.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
            }
        }

        // TODO: Instead of doing this, make the paths themselves more editable?
        private T MaybeOverrideFromModDir<T>(T original, string path, Func<string, T> parser)
        {
            if (modDir == null) return original;
            string modPath = $@"{modDir}\{path}";
            if (File.Exists(modPath))
            {
                Console.WriteLine($"Using modded file {modPath}");
                return parser(modPath);
            }
            return original;
        }

        private void MaybeOverrideFromModDir<T>(Dictionary<string, T> files, Func<string, string> relpath, Func<string, T> parser)
        {
            if (modDir == null) return;
            foreach (string key in files.Keys.ToList())
            {
                files[key] = MaybeOverrideFromModDir(files[key], relpath(key), parser);
            }
        }

        // Some helper functionality things
        public void SearchParamInt(uint id, string field=null)
        {
            bool matches(string cell)
            {
                if (cell == id.ToString()) return true;
                // if (int.TryParse(cell, out int val)) return val >= 11000000 && val <= 13000000 && (val / 1000) % 10 == 5;
                return false;
            }
            Console.WriteLine($"-- Searching params for {id}");
            foreach (KeyValuePair<string, PARAM> param in Params)
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
            foreach (KeyValuePair<string, PARAM> param in Params)
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

        public void SearchSmapInt(uint id)
        {
            Console.WriteLine($"-- Searching map for {id}");
            foreach (KeyValuePair<string, MSBS> mapEntry in Smaps)
            {
                MSBS msb = mapEntry.Value;
                foreach (MSBS.Part part in msb.Parts.GetEntries())
                {
                    // This no longer works - cannot write individual part w current permissions
                    byte[] partBytes = new byte[] { };
                    if (SearchInt(partBytes, id) != -1)
                    {
                        Console.WriteLine($"{mapEntry.Key}[{part.Name}] has {id}");
                    }
                }
            }
        }

        public void DumpMessages(string dir)
        {
            foreach (string path in Directory.GetFiles(dir, "*.msgbnd*"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                try
                {
                    IBinder bnd = BND3.Read(path);
                    foreach (BinderFile file in bnd.Files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                        string uname = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\x00-\x7F]", c => string.Format(@"u{0:x4}", (int)c.Value[0]));
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
