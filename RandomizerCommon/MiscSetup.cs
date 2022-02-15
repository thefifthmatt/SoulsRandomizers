using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SoulsIds;
using SoulsFormats;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class MiscSetup
    {
        // https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        private static HashSet<string> badModEngines = new HashSet<string>
        {
            // Sekiro 1.04 and before
            "dfff963c88e82dc19c5e8592f464b9ca",
            "81137f302c6905f42ddf76fc52287e8e",
            "b25ddefec3f78278be633b85101ecccb",
            "3b37d8bbbce586a6f71dff687a25eb9c",
            "22adc919abcdf24df0f6d81e00a9974e",
            "71e9e56c741ca6ff338c16f8cb37c0e4",
            "9168d85ef78d13e108c30bdb74984c46",
            "267153f2297ba189591304560aab3a3e",
            "fbf98322736d493c5048804cc7efb11c",
            // Sekiro 1.04 custom build, previously worked
            "d79551b08bee23ab1d190448894b86d1",
            // DS3 official ones
            "3977dce4190107754b3b31deaf5b3b8f",
            "ef66f24d523069504ef3ec06ed0725fe",
            "af7c8c795ac852175e7850bc03f526ca",
        };

        private static HashSet<string> justWorksModEngines = new HashSet<string>
        {
            // Sekiro 1.06
            "f785817a60c9a40f7cd57ff74f4256d3",
            // DS3 custom build
            "3405ca8f6cd084f10e46a967f2463f19",
        };

        public static bool CheckRequiredSekiroFiles(out string ret)
        {
            ret = null;
            if (!Directory.Exists("dists"))
            {
                ret = "Error: Can't find required metadata files.\r\nFor the randomizer to work, you must unpack it to disk and keep all of the files together";
            }
            else if (File.Exists("Sekiro.exe"))
            {
                ret = "Error: Running from same directory as Sekiro.exe\r\nThe randomizer and its files must be in a subdirectory";
            }
            else if (!File.Exists("oo2core_6_win64.dll"))
            {
                if (File.Exists(@"..\oo2core_6_win64.dll"))
                {
                    File.Copy(@"..\oo2core_6_win64.dll", "oo2core_6_win64.dll");
                }
                else if (File.Exists(@"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\oo2core_6_win64.dll"))
                {
                    File.Copy(@"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\oo2core_6_win64.dll", "oo2core_6_win64.dll");
                }
                else
                {
                    ret = "Error: Oodle not found\r\nCopy oo2core_6_win64.dll from Sekiro.exe directory into the randomizer directory";
                }
            }
            return ret == null;
        }

        public static bool CheckRequiredDS3Files(out string ret)
        {
            ret = null;
            if (!Directory.Exists("dist"))
            {
                ret = "Error: Can't find required metadata files.\r\nFor the randomizer to work, you must unpack it to disk and keep all of the files together";
            }
            else if (File.Exists("DarkSoulsIII.exe"))
            {
                ret = "Error: Running from same directory as DarkSoulsIII.exe\r\nThe randomizer and its files must be in a subdirectory";
            }
            return ret == null;
        }

        // Note: Doesn't return error or not (use ret != null for that), returns if fatal or not
        public static bool CheckSekiroModEngine(out string ret)
        {
            ret = null;
            if (!File.Exists(@"..\Sekiro.exe"))
            {
                ret = "Error: Sekiro.exe not found in parent directory\r\nFor randomization to work, move the randomizer folder to your Sekiro install location";
                return true;
            }
            if (!File.Exists(@"..\dinput8.dll") || !File.Exists(@"..\modengine.ini"))
            {
                ret = "Error: Sekiro Mod Engine not found in parent directory\r\nDownload dinput8.dll and modengine.ini from Sekiro Mod Engine";
                return true;
            }
            // Check Mod Engine version
            string modEngineHash = GetMD5Hash(@"..\dinput8.dll");
            if (badModEngines.Contains(modEngineHash))
            {
                // ret = "Error: Sekiro Mod Engine needs to be the unofficial version from the Sekiro Randomizer Files section\r\nCopy its dinput8.dll into parent dir or else enemy randomization will definitely crash the game!";
                ret = "Error: Sekiro Mod Engine needs to be the official 0.1.16 release for Sekiro 1.06.\r\nDownload it and copy it dinput8.dll into the parent dir.";
                return true;
            }
            // Check ini variables
            string ini = new FileInfo(@"..\modengine.ini").FullName.ToString();
            StringBuilder useMods = new StringBuilder(255);
            GetPrivateProfileString("files", "useModOverrideDirectory", "", useMods, 255, ini);
            if (useMods.ToString() != "1")
            {
                ret = "Warning: Set useModOverrideDirectory to 1 in modengine.ini\r\nOtherwise, randomization may not apply to game";
                return false;
            }
            StringBuilder modDir = new StringBuilder(255);
            GetPrivateProfileString("files", "modOverrideDirectory", "", modDir, 255, ini);
            string dirName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            string expected = $@"\{dirName}";
            if (modDir.ToString().ToLowerInvariant() != expected.ToLowerInvariant())
            {
                ret = $"Warning: Set modOverrideDirectory to \"{expected}\" in modengine.ini\r\nOtherwise, randomization may not apply to game";
                return false;
            }
            // Finally a check for future versions of mod engine. This will probably result in a bunch of user issue reports either way.
            if (!justWorksModEngines.Contains(modEngineHash))
            {
                ret = "Warning: Unknown version of Sekiro Mod Engine detected\r\nUse the latest official release, and update the randomizer if there is an update";
                return false;
            }
            return false;
        }

        public static bool CheckDS3ModEngine(bool enemyEnabled, out string ret, out bool encrypted)
        {
            encrypted = true;
            ret = null;
            if (!File.Exists(@"..\DarkSoulsIII.exe"))
            {
                ret = "Error: DarkSoulsIII.exe not found in parent directory\r\nFor randomization to work, move the randomizer folder to your DS3 install location";
                return true;
            }
            if (!File.Exists(@"..\dinput8.dll") || !File.Exists(@"..\modengine.ini"))
            {
                ret = "Error: DS3 Mod Engine not found in parent directory\r\ndinput8.dll and modengine.ini must be present";
                return true;
            }
            // Check Mod Engine version
            string modEngineHash = GetMD5Hash(@"..\dinput8.dll");
            if (enemyEnabled && badModEngines.Contains(modEngineHash))
            {
                ret = "Error: To use enemy randomizer, DS3 Mod Engine needs to be the version from randomizer\\ModEngine.\r\nCopy its dinput8.dll into the game dir or else the game will definitely crash!";
                return true;
            }
            // Check ini variables
            string ini = new FileInfo(@"..\modengine.ini").FullName.ToString();

            StringBuilder loadLoose = new StringBuilder(255);
            GetPrivateProfileString("files", "loadLooseParams", "", loadLoose, 255, ini);
            if (loadLoose.ToString() == "1")
            {
                // This doesn't seem to work.
                // encrypted = false;
            }

            StringBuilder useMods = new StringBuilder(255);
            GetPrivateProfileString("files", "useModOverrideDirectory", "", useMods, 255, ini);
            if (useMods.ToString() != "1")
            {
                ret = "Warning: Set useModOverrideDirectory to 1 in modengine.ini\r\nOtherwise, randomization may not apply to game";
                return false;
            }

            StringBuilder modDir = new StringBuilder(255);
            GetPrivateProfileString("files", "modOverrideDirectory", "", modDir, 255, ini);
            string dirName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            string expected = $@"\{dirName}";
            if (modDir.ToString().ToLowerInvariant() != expected.ToLowerInvariant())
            {
                ret = $"Warning: Set modOverrideDirectory to \"{expected}\" in modengine.ini\r\nOtherwise, randomization may not apply to game";
                return false;
            }

            return false;
        }

        private static readonly MD5 MD5 = MD5.Create();
        private static string GetMD5Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = MD5.ComputeHash(stream);
                return string.Join("", hash.Select(x => $"{x:x2}"));
            }
        }

        public static void CombineAI(List<string> maps, string outDir, bool mergeInfo)
        {
            // Merges AI into common and removes scripts from other maps.
            // Also looks at config directory for custom overrides.
            string commonPath = $@"{outDir}\..\script\aicommon.luabnd.dcx";
            BND4 aiCommon = BND4.Read(commonPath);
            HashSet<string> usedFiles = new HashSet<string>(aiCommon.Files.Select(f => f.Name));
            (LUAGNL, LUAINFO) parseMetadata(BND4 bnd)
            {
                if (!mergeInfo) return (null, null);
                BinderFile gnlFile = bnd.Files.Find(f => f.Name.EndsWith(".luagnl"));
                BinderFile infoFile = bnd.Files.Find(f => f.Name.EndsWith(".luainfo"));
                if (gnlFile == null) throw new Exception($"Missing required AI files [{gnlFile},{infoFile}]");
                return (LUAGNL.Read(gnlFile.Bytes), infoFile == null ? null : LUAINFO.Read(infoFile.Bytes));
            }
            void writeMetadata(BND4 bnd, LUAGNL gnl, LUAINFO info)
            {
                if (!mergeInfo) return;
                if (gnl != null) bnd.Files.Find(f => f.Name.EndsWith(".luagnl")).Bytes = gnl.Write();
                if (info != null) bnd.Files.Find(f => f.Name.EndsWith(".luainfo")).Bytes = info.Write();
            }
            void mergeMetadata(LUAGNL sourceGnl, LUAINFO sourceInfo, LUAGNL targetGnl, LUAINFO targetInfo)
            {
                if (!mergeInfo) return;
                if (sourceGnl != null)
                {
                    targetGnl.Globals = targetGnl.Globals.Union(sourceGnl.Globals).ToList();
                }
                if (sourceInfo != null)
                {
                    foreach (LUAINFO.Goal g in sourceInfo.Goals)
                    {
                        // Dedupe does not seem to be necessary, and tricky besides
                        // if (!sourceInfo.Goals.Any(h => h.ID == g.ID && h.Name == g.Name))
                        targetInfo.Goals.Add(g);
                    }
                }
            }
            (LUAGNL commonGnl, LUAINFO commonInfo) = parseMetadata(aiCommon);
            foreach (string map in maps)
            {
                string aiPath = $@"{outDir}\..\script\{map}.luabnd.dcx";
                if (!File.Exists(aiPath)) continue;

                BND4 ai = BND4.Read(aiPath);
                ai.Files = ai.Files.Where(file =>
                {
                    if (!file.Name.Contains("out")) return true;
                    if (!usedFiles.Contains(file.Name))
                    {
                        string overrideFile = $@"configs\dist\{Path.GetFileName(file.Name)}";
                        if (File.Exists(overrideFile))
                        {
                            Console.WriteLine("Override " + overrideFile);
                            file.Bytes = File.ReadAllBytes(overrideFile);
                        }
                        aiCommon.Files.Add(file);
                        usedFiles.Add(file.Name);
                    }
                    return false;
                }).ToList();
                (LUAGNL gnl, LUAINFO info) = parseMetadata(ai);
                mergeMetadata(gnl, info, commonGnl, commonInfo);
                ai.Write($@"{outDir}\script\{map}.luabnd.dcx");
            }
            writeMetadata(aiCommon, commonGnl, commonInfo);
            int startId = 2000;
            foreach (BinderFile file in aiCommon.Files)
            {
                if (file.ID < 3000) file.ID = startId++;
                Console.WriteLine(file);
            }
            aiCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
            aiCommon.Write($@"{outDir}\script\aicommon.luabnd.dcx");
        }

        public static void CombineDragonTpfs()
        {
            // Utility for creating Divine Dragon texbnd. Requires using Yabber to unpack these bnds, and repack after done.
            string gamePath = GameSpec.ForGame(GameSpec.FromGame.SDT).GameDir;
            string mainPath = $@"{gamePath}\chr\c5200-texbnd-dcx\chr\c5200\c5200.tpf";
            SFUtil.Backup(mainPath);
            TPF dragon = TPF.Read(mainPath);
            foreach (string p in Directory.GetFiles($@"{gamePath}\map\m25\m25_0000-tpfbhd", "m25_Dragon*.tpf.dcx"))
            {
                TPF t = TPF.Read(p);
                dragon.Textures.AddRange(t.Textures);
            }
            dragon.Write(mainPath);
        }

        public static bool CheckSFX()
        {
            string customPath = @"sfx\sfxbnd_commoneffects.ffxbnd.dcx";
            if (!File.Exists(customPath)) return false;
            // 1.04 original size: 64,029,504. New size: 78,592,543
            // 1.06 original size: 64,319,424. New size: 79,142,507
            if (new FileInfo(customPath).Length < 75000000) return false;
            return true;
        }

        public static bool CombineSFX(List<string> maps, string outDir, bool ds3 = false)
        {
            string inDir = new DirectoryInfo($@"{outDir}\..\sfx").FullName;
            string prefix = ds3 ? "frpg_" : "";
            // Note: DS3 files are 6 MB and 295 MB respectively, so, we need a more selective strategy for resources.
            string[] suffixes = ds3 ? new[] { "_effect", "_resource" } : new[] { "" };
            foreach (string suffix in suffixes)
            {
                string commonPath = $@"{inDir}\{prefix}sfxbnd_commoneffects{suffix}.ffxbnd.dcx";
                if (!File.Exists(commonPath)) return false;
                Console.WriteLine(new FileInfo(commonPath).FullName);
                BND4 sfxCommon = BND4.Read(commonPath);
                HashSet<string> sfxFiles = new HashSet<string>(sfxCommon.Files.Select(f => f.Name));
                Console.WriteLine(string.Join(",", maps));
                foreach (string map in maps.Select(m => m.Substring(0, 3)).Distinct())
                {
                    string path = $@"{inDir}\{prefix}sfxbnd_{map}{suffix}.ffxbnd.dcx";
                    if (!File.Exists(path)) continue;

                    BND4 sfx = BND4.Read(path);
                    sfx.Files = sfx.Files.Where(file =>
                    {
                        Console.WriteLine(file.Name);
                        if (!sfxFiles.Contains(file.Name))
                        {
                            sfxCommon.Files.Add(file);
                            sfxFiles.Add(file.Name);
                            return false;
                        }
                        else
                        {
                            return false;
                        }
                    }).ToList();
                    sfx.Write($@"{outDir}\sfx\{prefix}sfxbnd_{map}{suffix}.ffxbnd.dcx");
                }
                int startId = 0;
                foreach (BinderFile file in sfxCommon.Files)
                {
                    // Ignore prefixes here
                    file.ID = startId++;
                }
                sfxCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
                sfxCommon.Write($@"{outDir}\sfx\{prefix}sfxbnd_commoneffects{suffix}.ffxbnd.dcx");
            }
            return true;
        }

        public static void DS3CommonPass(GameData game, Events events, RandomizerOptions opt)
        {
            EMEVD.Event fogEvent = game.Emevds["m40_00_00_00"].Events.Find(e => e.ID == 14005102);
            if (fogEvent != null)
            {
                // Small convenience: Shorten the Firelink Shrine fog gate wait times significantly
                foreach (EMEVD.Instruction i in fogEvent.Instructions)
                {
                    Instr instr = events.Parse(i);
                    if (instr.Name == "IfElapsedSeconds" && instr.Args[1] is float wait)
                    {
                        instr[1] = Math.Min(wait, 2f);
                        instr.Save();
                    }
                }
            }
            // Easier verification for enemy randomizer stuff
            if (opt["cheat_shortcut"])
            {
                // Various shortcut flags
                List<int> flags = new List<int>
                {
                    63100420, // Settlement Greatwood
                    63100470, // Settlement sewers
                    13010461, // Lothric->Archives elevator
                    63300432, // Farron Keep shortcut
                    13410451, // Archives elevator
                    13500431, // Cathedral elevator
                    63500210, 63500211, 63500212, 63500213, // Cathedral wooden doors
                    63500221, // Cathedral gate 1
                    63500220, // Cathedral gate 2
                    63700460, // Irithyll Pontiff shortcut
                    63900440, // Dungeon->Archdragon door 1
                    63900430, // Dungeon->Archdragon door 2
                    13900401, // Dungeon->Archdragon warp elevator
                    64500572, // Ariandel basement
                    65100610, // Ringed City
                };
                game.Emevds["common"].Events[0].Instructions.AddRange(
                    flags.Select(f => events.ParseAdd($"SetEventFlag({f}, ON)")));
                EMEVD.Event warpEvent = game.Emevds["m39_00_00_00"].Events.Find(e => e.ID == 13905900);
                if (warpEvent != null)
                {
                    EventEdits edits = new EventEdits();
                    events.RemoveMacro(edits, "4400");
                    events.ApplyAllEdits(warpEvent, edits);
                }
            }
        }

        public static void SekiroCommonPass(GameData game, Events events, RandomizerOptions opt)
        {
            Dictionary<string, PARAM> Params = game.Params;

            // Snap (for convenience, but can also softlock the player)
            if (opt["snap"]) Params["EquipParamGoods"][3980]["goodsUseAnim"].Value = (sbyte)84;
            // No tutorials
            if (Params.ContainsKey("MenuTutorialParam")) Params["MenuTutorialParam"].Rows = Params["MenuTutorialParam"].Rows.Where(r => r.ID == 0).ToList();
            // Memos pop up and don't just disappear mysteriously
            Params["EquipParamGoods"][9221]["Unk20"].Value = (byte)6;
            Params["EquipParamGoods"][9223]["Unk20"].Value = (byte)6;
            Params["EquipParamGoods"][9225]["Unk20"].Value = (byte)6;

            // These are just always deleted
            HashSet<string> deleteCommands = new HashSet<string>
            {
                "Show Tutorial Text", "Show Hint Box", "Show Small Hint Box", "Award Achievement"
            };
            HashSet<int> deleteEvents = new HashSet<int>
            {
                // Putting away sword in areas
                20006200,
            };
            // Slowless slow walk
            if (opt["headlesswalk"]) deleteEvents.Add(20005431);

            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    bool commonInit = entry.Key == "common" && e.ID == 0;
                    int maxPermSlot = 0;
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = events.Parse(e.Instructions[i]);
                        bool delete = false;
                        if (instr.Init)
                        {
                            if (deleteEvents.Contains(instr.Callee)) delete = true;
                            else if (commonInit && instr.Callee == 750 && instr.Offset == 2) maxPermSlot = Math.Max(maxPermSlot, (int)instr[0]);
                        }
                        else
                        {
                            if (deleteCommands.Contains(instr.Name)) delete = true;
                        }
                        if (delete)
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            // Just in case...
                            e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                        }
                    }
                    // Add permanent shop placement flags. Also.... abuse this for headless ape bestowal lot, if enemy rando is enabled.
                    if (opt["enemy"] && opt["bosses"] && commonInit)
                    {
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { maxPermSlot + 1, (uint)750, (uint)9307, (uint)9314 }));
                    }
                }
            }
        }

        public static readonly List<string> Langs = new List<string>
        {
            "deude", "engus", "frafr", "itait", "jpnjp", "korkr", "polpl", "porbr", "rusru", "spaar", "spaes", "thath", "zhocn", "zhotw",
        };
        public static readonly List<string> NoDS3Langs = new List<string> { "thath" };

        private static readonly List<string> fileDirs = new List<string>
        {
            @".",
            @"action",
            @"action\script",
            @"chr",
            @"cutscene",
            @"event",
            @"map\mapstudio",
            @"menu",
            @"menu\hi",
            @"menu\hi\mapimage",
            @"menu\low",
            @"menu\low\mapimage",
            @"menu\knowledge",
            @"menu\$lang",
            @"msg\$lang",
            @"mtd",
            @"obj",
            @"other",
            @"param\drawparam",
            @"param\gameparam",
            @"param\graphicsconfig",
            @"parts",
            @"script",  // This should be a no-op with enemy rando
            @"script\talk",
            @"sfx",  // This should be a no-op with enemy rando
            @"shader",
            @"sound",
        }.SelectMany(t => t.Contains("$lang") ? Langs.Select(l => t.Replace("$lang", l)) : new[] { t }).ToList();
        private static List<string> extensions = new List<string>
        {
            ".hks", ".dcx", ".gfx", ".dds", ".fsb", ".fev", ".itl", ".tpf", ".entryfilelist", ".hkxbdt", ".hkxbhd", "Data0.bdt"
        };
        private static Regex extensionRe = new Regex(string.Join("|", extensions.Select(e => e + "$")));
        public static List<string> GetGameFiles(string dir, bool sekiro)
        {
            List<string> allFiles = new List<string>();
            foreach (string subdir in fileDirs)
            {
                if (subdir == "script" || subdir == "sfx") continue;
                string fulldir = $@"{dir}\{subdir}";
                if (Directory.Exists(fulldir))
                {
                    foreach (string path in Directory.GetFiles(fulldir))
                    {
                        if (extensionRe.IsMatch(path))
                        {
                            string filename = Path.GetFileName(path);
                            allFiles.Add($@"{subdir}\{filename}");
                        }
                    }
                }
            }
            return allFiles;
        }
    }
}
