using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SoulsIds;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    using EntityId = LocationData.EntityId;
    using ItemKey = LocationData.ItemKey;
    using ItemType = LocationData.ItemType;
    public class GameData
    {
        private static readonly List<string> itemParams = new List<string>() { "EquipParamWeapon", "EquipParamProtector", "EquipParamAccessory", "EquipParamGoods" };
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

        public enum MsgFile { GOODS_INFO, GOODS_CAPTION }
        private readonly Dictionary<string, MsgFile> msgFiles = new Dictionary<string, MsgFile>
        {
            { "\u30a2\u30a4\u30c6\u30e0\u3046\u3093\u3061\u304f", MsgFile.GOODS_CAPTION },
            { "\u30a2\u30a4\u30c6\u30e0\u8aac\u660e", MsgFile.GOODS_INFO },
        };

        public readonly bool Sekiro;
        public readonly GameEditor Editor;

        // TODO: Merge with GameEditor for DS3, to get rid of a lot of this.
        private string dir;
        private string modDir;
        private BND4 regulation;
        private bool encrypted;
        // Actual data
        private Dictionary<string, PARAM.Layout> layouts = new Dictionary<string, PARAM.Layout>();
        public Dictionary<string, PARAM> Params = new Dictionary<string, PARAM>();
        private Dictionary<string, MSB3> maps = new Dictionary<string, MSB3>();
        public Dictionary<string, MSBS> Smaps = new Dictionary<string, MSBS>();
        public Dictionary<string, EMEVD> Emevds = new Dictionary<string, EMEVD>();
        public Dictionary<string, FMG> BaseItemFMGs = new Dictionary<string, FMG>();
        public Dictionary<string, FMG> ItemFMGs = new Dictionary<string, FMG>();
        private HashSet<string> editedMaps = new HashSet<string>();
        private Dictionary<string, Dictionary<string, ESD>> talk = new Dictionary<string, Dictionary<string, ESD>>();
        private Dictionary<string, List<(uint, uint)>> scriptChanges = new Dictionary<string, List<(uint, uint)>>();
        private Dictionary<MsgFile, Dictionary<string, FMG>> Messages = new Dictionary<MsgFile, Dictionary<string, FMG>>();
        // Names
        private SortedDictionary<ItemKey, string> itemNames = new SortedDictionary<ItemKey, string>();
        private SortedDictionary<string, List<ItemKey>> revItemNames = new SortedDictionary<string, List<ItemKey>>();
        private SortedDictionary<int, string> qwcNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> lotNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> characterSplits = new SortedDictionary<int, string>();
        private SortedDictionary<string, string> modelNames = new SortedDictionary<string, string>();

        public GameData(string dir, bool sekiro) {
            this.dir = dir;
            Sekiro = sekiro;
            Editor = new GameEditor(sekiro ? GameSpec.FromGame.SDT : GameSpec.FromGame.DS3);
            Editor.Spec.GameDir = $@"{dir}";
            Editor.Spec.NameDir = $@"{dir}\Names";
            Editor.Spec.LayoutDir = $@"{dir}\Layouts";
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
            if (!Sekiro) key = Normalize(key);
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

        public void ReplaceScript(string script, uint from, uint to)
        {
            scriptChanges[script].Add((from, to));
        }

        public void SetMessage(MsgFile file, int id, string msg)
        {
            if (!Messages.ContainsKey(file))
            {
                Warn($"No file for message type {file} loaded in (setting {id}={msg})");
                return;
            }
            foreach (FMG fmg in Messages[file].Values)
            {
                fmg[id] = msg;
            }
        }

        public Dictionary<string, MSB3> Maps()
        {
            return maps;
        }

        public MSB3 EditMap(string name)
        {
            editedMaps.Add(name);
            return maps[name];
        }

        // TODO: Cleaner way to split between DS3 and Sekiro.
        public Dictionary<string, Dictionary<string, ESD>> Talk()
        {
            return talk;
        }

        public StreamReader NewAnnotationReader()
        {
            string testFile = $@"{dir}\Base\annotations.txt";
            if (File.Exists(testFile)) return File.OpenText(testFile);
            return File.OpenText($@"{dir}\Base\annotations.yaml");
        }

        private static ItemKey Normalize(ItemKey key)
        {
            // Maybe can put this logic in ItemKey itself
            if (key.Type == LocationData.ItemType.WEAPON && key.ID % 100 != 0)
            {
                return new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            return key;
        }

        public string Name(ItemKey key)
        {
            string suffix = "";
            if (key.Type == LocationData.ItemType.WEAPON && key.ID % 100 != 0)
            {
                suffix = $" +{key.ID % 100}";
                key = new ItemKey(key.Type, key.ID - (key.ID % 100));
            }
            // suffix += $" {key.ID}";
            return (itemNames.ContainsKey(key) ? itemNames[key] : $"?ITEM?" + $" ({(int)key.Type}:{key.ID})") + suffix;
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
            string fullName(string path)
            {
                return new FileInfo(path).FullName;
            }
            List<string> writtenFiles = new List<string>();
            void writeFile(string path)
            {
                path = fullName(path);
                Console.WriteLine($"Writing {path}");
                writtenFiles.Add(path);
            }
            foreach (KeyValuePair<string, MSBS> entry in Smaps)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string path = $@"{outPath}\map\mapstudio\{entry.Key}.msb.dcx";
                writeFile(path);
                entry.Value.Write(path, (DCX.Type)DCX.DefaultType.Sekiro);
            }
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in talk)
            {
                if (!SekiroLocationDataScraper.locations.ContainsKey(entry.Key)) continue;
                string basePath = $@"{dir}\Base\{entry.Key}.talkesdbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\script\talk\{entry.Key}.talkesdbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\script\talk\{entry.Key}.talkesdbnd.dcx";
                writeFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\script\talk", entry.Value, esd => esd.Write());
            }
            foreach (KeyValuePair<string, EMEVD> entry in Emevds)
            {
                string path = $@"{outPath}\event\{entry.Key}.emevd.dcx";
                writeFile(path);
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
                writeFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\param\gameparam", Params, f => f.Write());
            }
            {
                string basePath = $@"{dir}\Base\item.msgbnd.dcx";
                if (modDir != null)
                {
                    string modPath = $@"{modDir}\msg\engus\item.msgbnd.dcx";
                    if (File.Exists(modPath)) basePath = modPath;
                }
                string path = $@"{outPath}\msg\engus\item.msgbnd.dcx";
                writeFile(path);
                Editor.OverrideBnd(basePath, $@"{outPath}\msg\engus", ItemFMGs, f => f.Write());
            }
            Console.WriteLine("Processing extra mod files...");
            bool work = false;
            if (modDir != null)
            {
                foreach (string gameFile in MiscSetup.GetGameFiles(modDir))
                {
                    string source = fullName($@"{modDir}\{gameFile}");
                    string target = fullName($@"{outPath}\{gameFile}");
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
                string target = fullName($@"{outPath}\{gameFile}");
                if (writtenFiles.Contains(target)) continue;
                Console.WriteLine($"Found extra file (delete it if you don't want it): {target}");
                work = true;
            }
            if (!work) Console.WriteLine("No extra files found");
            Console.WriteLine("Success!");
        }

        public void SaveDS3(string outPath, bool editMaps=false)
        {
            // Save params
            string outParams = $@"{outPath}\Data0.bdt";
            foreach (BinderFile file in regulation.Files.Where(f => f.Name.EndsWith(".param")))
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                if (Params.ContainsKey(name)) {
                    file.Bytes = Params[name].Write();
                }
            }
            if (encrypted)
            {
                SFUtil.EncryptDS3Regulation(outParams, regulation);
            }
            else
            {
                regulation.Write(outParams);
            }
            // Messages
            Directory.CreateDirectory($@"{outPath}\msg\engus");
            foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.msgbnd.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                string outText = $@"{outPath}\msg\engus\{Path.GetFileName(path)}";
                BND4 bnd = BND4.Read(path);
                foreach (BinderFile file in bnd.Files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                    if (msgFiles.ContainsKey(fileName))
                    {
                        file.Bytes = Messages[msgFiles[fileName]][name].Write();
                    }
                }
                bnd.Write(outText, (DCX.Type)DCX.DefaultType.DarkSouls3);
            }
            // Edited maps, if option enabled
            HashSet<string> mapsToEdit = editMaps ? editedMaps : new HashSet<string>();
            string outMapDir = $@"{outPath}\map\mapstudio";
            Directory.CreateDirectory(outMapDir);
            foreach (string mapName in editedMaps)
            {
                MSB3 msb = maps[mapName];
                string outMap = $@"{outPath}\map\mapstudio\{mapName}.msb.dcx";
                msb.Write(outMap, (DCX.Type)DCX.DefaultType.DarkSouls3);
            }
            // Delete unedited maps, could be left over from previous run, or now disabled
            foreach (string path in Directory.GetFiles(outMapDir, "*.msb.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                if (!editedMaps.Contains(name))
                {
                    File.Delete(path);
                }
            }
            // Copy/save scripts
            Directory.CreateDirectory($@"{outPath}\event");
            foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.emevd.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                string outScript = $@"{outPath}\event\{Path.GetFileName(path)}";
                if (!scriptChanges.ContainsKey(name))
                {
                    File.Copy(path, outScript);
                    continue;
                }
                byte[] bytes = DCX.Decompress(File.ReadAllBytes(path));
                foreach ((uint, uint) change in scriptChanges[name])
                {
                    int pos = SearchInt(bytes, change.Item1);
                    if (pos == -1) throw new Exception($"Could not find int {change.Item1} in script {name}");
                    byte[] replace = BitConverter.GetBytes(change.Item2);
                    for (int i = 0; i < replace.Length; i++)
                    {
                        bytes[pos + i] = replace[i];
                    }
                }
                DCX.Compress(bytes, (DCX.Type)DCX.DefaultType.DarkSouls3, outScript);
            }
            Console.WriteLine($"Saved to {outPath}!");
        }

        private void LoadNames()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Names", "*.txt"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                // if (name == "ShopQwc") continue;
                int type = itemParams.IndexOf(name);
                foreach (var line in File.ReadLines(path))
                {
                    int spot = line.IndexOf(' ');
                    if (spot == -1)
                    {
                        throw new Exception($"Bad line {line} in {path}");
                    }
                    string idstr = line.Substring(0, spot);
                    string text = line.Substring(spot + 1);
                    if (name == "ModelName")
                    {
                        modelNames[idstr] = text;
                    }
                    else
                    {
                        int id = Int32.Parse(idstr);
                        if (type >= 0)
                        {
                            // Item
                            ItemKey key = new ItemKey((ItemType)type, id);
                            itemNames[key] = text;
                            AddMulti(revItemNames, text, key);
                        }
                        else
                        {
                            // One-off id
                            if (name == "ItemLotParam")
                            {
                                lotNames[id] = text;
                            }
                            else if (name == "CharaInitParam")
                            {
                                characterSplits[id] = text;
                            }
                            else if (name == "ShopQwc")
                            {
                                int refId;
                                if (Int32.TryParse(text, out refId))
                                {
                                    qwcNames[id] = qwcNames[refId]; // + " (handmaid)";
                                }
                                else
                                {
                                    qwcNames[id] = text;
                                }
                            }
                        }
                    }
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
            string path = Sekiro ? $@"{dir}\Base\gameparam.parambnd.dcx" : $@"{dir}\Base\Data0.bdt";
            if (Sekiro)
            {
                string modPath = $@"{modDir}\param\gameparam\gameparam.parambnd.dcx";
                if (File.Exists(modPath))
                {
                    Console.WriteLine($"Using modded file {modPath}");
                    path = modPath;
                }
            }
            // path = $@"{Editor.Spec.GameDir}\{Editor.Spec.ParamFile}";
            if (!File.Exists(path))
            {
                throw new Exception($"Missing param file: {path}");
            }

            try
            {
                // May be encrypted or not based on DS3 or Sekiro
                if (BND4.Is(path))
                {
                    regulation = BND4.Read(path);
                    encrypted = false;
                }
                else
                {
                    regulation = SFUtil.DecryptDS3Regulation(path);
                    encrypted = true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load regulation file:\r\n{path}\r\n\r\n{ex}");
            }
            foreach (BinderFile file in regulation.Files.Where(f => f.Name.EndsWith(".param")))
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);

                if (!Sekiro && !loadParams.Contains(name)) continue;

                try
                {
                    PARAM param = PARAM.Read(file.Bytes);
                    if (layouts.ContainsKey(param.ParamType))
                    {
                        PARAM.Layout layout = layouts[param.ParamType];
                        if (layout.Size == param.DetectedSize)
                        {
                            param.ApplyParamdef(layout.ToParamdef(param.ParamType, out var _));
                            Params[name] = param;
                        }
                        else
                        {
                            // if (loadParams.Contains(name)) throw new Exception ($"param {name} had size {param.DetectedSize} but expected {layout.Size}");
                        }
                    }
                    else
                    {
                        if (loadParams.Contains(name)) throw new Exception($"unknown param {param.ParamType}");
                    }
                }
                catch (Exception)
                {
                    // throw new Exception($"Failed to load param {name}: {ex}");
                    Console.WriteLine($"Failed to load param {name}");
                }
            }
        }

        private void LoadMapData()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.msb.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));

                try
                {
                    if (Sekiro)
                    {
                        MSBS msb = MSBS.Read(path);
                        Smaps[name] = msb;
                    }
                    else
                    {
                        MSB3 msb = MSB3.Read(path);
                        maps[name] = msb;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load msb {name}: {path}\r\n\r\n{ex}");
                }
            }
            if (Sekiro)
            {
                MaybeOverrideFromModDir(Smaps, name => $@"map\MapStudio\{name}.msb.dcx", path => MSBS.Read(path));
                List<string> missing = SekiroLocationDataScraper.locations.Keys.Except(Smaps.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing msbs in dists\Base: {string.Join(", ", missing)}");
            }
        }

        private void LoadTalk()
        {
            if (Sekiro)
            {
                talk = Editor.LoadBnds("Base", (data, path) => ESD.Read(data), "*.talkesdbnd.dcx");
                MaybeOverrideFromModDir(talk, name => $@"script\talk\{name}.talkesdbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => ESD.Read(data)));
                List<string> missing = SekiroLocationDataScraper.locations.Keys.Except(talk.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing talkesdbnds in dists\Base: {string.Join(", ", missing)}");
            }
        }

        private void LoadScripts()
        {
            if (Sekiro)
            {
                Emevds = Editor.Load("Base", path => EMEVD.Read(path), "*.emevd.dcx");
                MaybeOverrideFromModDir(Emevds, name => $@"event\{name}.emevd.dcx", path => EMEVD.Read(path));
                List<string> missing = SekiroLocationDataScraper.locations.Keys.Concat(new[] { "common", "common_func" }).Except(Emevds.Keys).ToList();
                if (missing.Count != 0) throw new Exception($@"Missing emevds in dists\Base: {string.Join(", ", missing)}");
            }
            else
            {
                foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.emevd.dcx"))
                {
                    string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                    scriptChanges[name] = new List<(uint, uint)>();
                }
            }
        }

        private void LoadText()
        {
            if (Sekiro)
            {
                BaseItemFMGs = ItemFMGs = Editor.LoadBnd($@"{dir}\Base\item.msgbnd.dcx", (data, path) => FMG.Read(data));
                ItemFMGs = MaybeOverrideFromModDir(ItemFMGs, @"msg\engus\item.msgbnd.dcx", path => Editor.LoadBnd(path, (data, path2) => FMG.Read(data)));
            }
            else
            {
                foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.msgbnd.dcx"))
                {
                    string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                    try
                    {
                        // Maybe just undcx it first
                        BND4 bnd = BND4.Read(path);
                        foreach (BinderFile file in bnd.Files)
                        {
                            string uname = System.Text.RegularExpressions.Regex.Replace(file.Name, @"[^\x00-\x7F]", c => string.Format(@"\u{0:x4}", (int)c.Value[0]));
                            string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                            if (msgFiles.ContainsKey(fileName))
                            {
                                MsgFile ftype = msgFiles[fileName];
                                if (!Messages.ContainsKey(ftype))
                                {
                                    Messages[ftype] = new Dictionary<string, FMG>();
                                }
                                FMG fmg = FMG.Read(file.Bytes);
                                Messages[ftype][name] = fmg;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to load file: {name}: {path}\r\n\r\n{ex}");
                    }
                }
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
