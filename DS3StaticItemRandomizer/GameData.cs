using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using static DS3StaticItemRandomizer.Util;

namespace DS3StaticItemRandomizer
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

        private string dir;
        private BND4 regulation;
        private bool encrypted;
        // Actual data
        private Dictionary<string, PARAM64.Layout> layouts = new Dictionary<string, PARAM64.Layout>();
        private Dictionary<string, PARAM64> allParams = new Dictionary<string, PARAM64>();
        private Dictionary<string, MSB3> maps = new Dictionary<string, MSB3>();
        private Dictionary<string, MSBS> smaps = new Dictionary<string, MSBS>();
        private HashSet<string> editedMaps = new HashSet<string>();
        private Dictionary<string, List<ESD>> talk = new Dictionary<string, List<ESD>>();
        private Dictionary<string, List<(uint, uint)>> scriptChanges = new Dictionary<string, List<(uint, uint)>>();
        private Dictionary<MsgFile, Dictionary<string, FMG>> Messages = new Dictionary<MsgFile, Dictionary<string, FMG>>();
        // Names
        private SortedDictionary<ItemKey, string> itemNames = new SortedDictionary<ItemKey, string>();
        private SortedDictionary<string, List<ItemKey>> revItemNames = new SortedDictionary<string, List<ItemKey>>();
        private SortedDictionary<int, string> qwcNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> lotNames = new SortedDictionary<int, string>();
        private SortedDictionary<int, string> characterSplits = new SortedDictionary<int, string>();
        private SortedDictionary<string, string> modelNames = new SortedDictionary<string, string>();

        public GameData(string dir) {
            this.dir = dir;
        }

        public void Load()
        {
            LoadNames();
            LoadLayouts();
            LoadParams();
            LoadMapData();
            LoadTalk();
            LoadScripts();
            LoadText();
        }

        public PARAM64 Param(string name)
        {
            return allParams[name];
        }
        public PARAM64.Row Item(ItemKey key)
        {
            key = Normalize(key);
            return allParams[itemParams[(int) key.Type]][key.ID];
        }
        public PARAM64.Row AddRow(string name, int id)
        {
            PARAM64 param = allParams[name];
            if (param[id] != null)
            {
                // This can get quadratic? But eh good to check
                throw new Exception($"Trying to add id {id} in {name} but already exists");
            }
            PARAM64.Row row = new PARAM64.Row(id, "", layouts[param.ID]);
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
        public Dictionary<string, MSBS> Smaps()
        {
            return smaps;
        }
        public Dictionary<string, List<ESD>> Talk()
        {
            return talk;
        }
        public StreamReader NewAnnotationReader()
        {
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
            return (itemNames.ContainsKey(key) ? itemNames[key] : $"?ITEM?" + $" ({(int)key.Type}:{key.ID})") + suffix;
        }
        public ItemKey ItemForName(string name)
        {
            if (!revItemNames.ContainsKey(name) || revItemNames[name].Count != 1) throw new Exception($"Ambiguous or missing name {name}");
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
            return qwcNames.ContainsKey(id) ? qwcNames[id] : "?SHOPCOND?";
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
        public string ModelName(EntityId entity)
        {
            int split = entity.EntityName.IndexOf('_');
            if (split == -1)
            {
                return entity.EntityName;
            }
            string model = entity.EntityName.Substring(0, split);
            if (!modelNames.ContainsKey(model))
            {
                return "?MODEL?" + $" ({model})";
            }
            if (model == "c0000" && entity.CharaInitID != -1) {
                return CharacterName(entity.CharaInitID);
            }
            return modelNames[model];
        }
        public void Save(string outPath, bool editMaps=false)
        {
            // Save params
            string outParams = $@"{outPath}\Data0.bdt";
            foreach (BinderFile file in regulation.Files.Where(f => f.Name.EndsWith(".param")))
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                if (allParams.ContainsKey(name)) {
                    file.Bytes = allParams[name].Write();
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
                bnd.Write(outText, DCX.Type.DarkSouls3);
            }
            // Edited maps, if option enabled
            HashSet<string> mapsToEdit = editMaps ? editedMaps : new HashSet<string>();
            string outMapDir = $@"{outPath}\map\mapstudio";
            Directory.CreateDirectory(outMapDir);
            foreach (string mapName in editedMaps)
            {
                MSB3 msb = maps[mapName];
                string outMap = $@"{outPath}\map\mapstudio\{mapName}.msb.dcx";
                msb.Write(outMap, DCX.Type.DarkSouls3);
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
                DCX.Compress(bytes, DCX.Type.DarkSouls3, outScript);
            }
            Console.WriteLine($"Saved to {outPath}!");
        }

        private void LoadNames()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Names", "*.txt"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
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
        }

        // https://github.com/JKAnderson/Yapped/blob/master/Yapped/FormMain.cs
        private void LoadLayouts()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Layouts", "*.xml"))
            {
                string paramID = Path.GetFileNameWithoutExtension(path);
                try
                {
                    PARAM64.Layout layout = PARAM64.Layout.ReadXMLFile(path);
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
            string path = $@"{dir}\Base\Data0.bdt";
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

                if (!loadParams.Contains(name)) continue;

                try
                {
                    PARAM64 param = PARAM64.Read(file.Bytes);
                    if (layouts.ContainsKey(param.ID))
                    {
                        PARAM64.Layout layout = layouts[param.ID];
                        if (layout.Size == param.DetectedSize)
                        {
                            param.SetLayout(layout);
                            allParams[name] = param;
                        }
                        else
                        {
                            if (loadParams.Contains(name)) throw new Exception ($"param {name} had size {param.DetectedSize} but expected {layout.Size}");
                        }
                    }
                    else
                    {
                        if (loadParams.Contains(name)) throw new Exception($"unknown param {param.ID}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load param {name}: {ex}");
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
                    MSB3 msb = MSB3.Read(path);
                    maps[name] = msb;
                    // Hacky Sekiro switch!
                    // MSBS msb = MSBS.Read(mapPath);
                    // smaps[name] = msb;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load msb {name}: {path}\r\n\r\n{ex}");
                }
            }
        }
        private void LoadTalk()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.talkesdbnd.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));

                try
                {
                    BND4 bnd = BND4.Read(path);
                    foreach (BinderFile file in bnd.Files)
                    {
                        ESD esd = ESD.Read(file.Bytes);
                        esd.Name = Path.GetFileNameWithoutExtension(file.Name);
                        AddMulti(talk, name, esd);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load esd file: {name} {path}\r\n\r\n{ex}");
                }
            }
        }
        private void LoadScripts()
        {
            foreach (string path in Directory.GetFiles($@"{dir}\Base", "*.emevd.dcx"))
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                scriptChanges[name] = new List<(uint, uint)>();
            }
        }
        private void LoadText()
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
}
