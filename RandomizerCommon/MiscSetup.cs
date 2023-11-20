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
using static SoulsFormats.EMEVD.Instruction;
using static RandomizerCommon.Messages;
using static RandomizerCommon.LocationData;

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

        [Localize]
        public static readonly Text CreateFileError = new Text(
            "Error: Failed to create file. Make sure the randomizer is unpacked to disk with all of the files kept together, and make sure no Antivirus programs are interfering. The randomizer will only modify files in its own directory or in the game directory.\n{0}",
            "Randomizer_createFileError");

        [Localize]
        private static readonly Text fileUnpackError = new Text(
            "Error: Can't find required metadata files.\nFor the randomizer to work, you must unpack it to disk and keep all of the files together",
            "EldenForm_fileUnpackError");

        public static bool CheckRequiredEldenFiles(Messages messages, out string ret)
        {
            // Return true if no errors
            ret = null;
            if (!Directory.Exists("diste"))
            {
                ret = messages.Get(fileUnpackError);
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

        [Localize]
        private static readonly Text fileCopyOodleError = new Text(
            "Error: Oodle not found\nCopy oo2core_6_win64.dll from your game directory into the randomizer directory",
            "EldenForm_fileCopyOodleError");

        public static bool ModifyEldenRingFiles(Messages messages, string exe, out string ret)
        {
            ret = null;
            // Make a backup save
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appData = $@"{appData}\EldenRing";
            if (Directory.Exists(appData))
            {
                string[] saveDirs = Directory.GetDirectories(appData);
                foreach (string saveDir in saveDirs)
                {
                    // Also ER0000.sl2.bak steam_autocloud.vdf
                    string saveFile = $@"{saveDir}\ER0000.sl2";
                    string saveBackup = $@"{saveDir}\ER0000.sl2.randobak";
                    if (File.Exists(saveFile) && !File.Exists(saveBackup) && !Directory.Exists(saveBackup))
                    {
                        File.Copy(saveFile, saveBackup, false);
                    }
                }
            }
            if (!File.Exists("oo2core_6_win64.dll"))
            {
                string gameDir = Path.GetDirectoryName(exe);
                if (File.Exists($@"{gameDir}\oo2core_6_win64.dll"))
                {
                    File.Copy($@"{gameDir}\oo2core_6_win64.dll", "oo2core_6_win64.dll");
                }
                else
                {
                    ret = messages.Get(fileCopyOodleError);
                }
            }
            return ret == null;
        }

        [Localize]
        private static readonly Text fileExeEmptyError = new Text(
            "Error: Game exe not provided",
            "EldenForm_fileExeEmptyError");
        [Localize]
        private static readonly Text fileExeMissingError = new Text(
            "Error: Game exe does not exist",
            "EldenForm_fileExeMissingError");
        [Localize]
        private static readonly Text fileRegulationMissingError = new Text(
            "Error: Invalid game exe (cannot find regulation.bin in game directory)",
            "EldenForm_fileMissingRegulationError");
        [Localize]
        private static readonly Text fileOodleMissingError = new Text(
            "Error: Invalid game exe (cannot find oo2core_6_win64.dll in game directory)",
            "EldenForm_fileOodleMissingError");
        [Localize]
        private static readonly Text fileUxmUnpackError = new Text(
            "Error: UXM output is selected, but the game is not unpacked (event subdirectory not found)",
            "EldenForm_fileUxmUnpackError");
        [Localize]
        private static readonly Text fileUxmPatchWarning = new Text(
            @"Warning: UXM output is selected, but the game may not be patched (_backup\eldenring.exe not found)",
            "EldenForm_fileUxmPatchWarning");

        public static bool CheckEldenRingMods(Messages messages, bool uxm, string exe, out string ret)
        {
            // Return true if fatal error
            ret = null;
            if (string.IsNullOrWhiteSpace(exe))
            {
                ret = messages.Get(fileExeEmptyError);
                return true;
            }
            if (!File.Exists(exe))
            {
                ret = messages.Get(fileExeMissingError);
                return true;
            }
            string gameDir = Path.GetDirectoryName(exe);
            if (!File.Exists($@"{gameDir}\regulation.bin"))
            {
                ret = messages.Get(fileRegulationMissingError);
                return true;
            }
            if (!File.Exists($@"{gameDir}\oo2core_6_win64.dll"))
            {
                ret = messages.Get(fileOodleMissingError);
                return true;
            }
            // Do UXM warnings only in UXM mode
            if (uxm)
            {
                // Check for a random important file
                if (!Directory.Exists($@"{gameDir}\event"))
                {
                    ret = messages.Get(fileUxmUnpackError);
                    return true;
                }
                // Check exe patched. This is less reliable, as other patch systems exist, and don't MD5 an 80 MB file here
                if (!File.Exists($@"{gameDir}\_backup\eldenring.exe"))
                {
                    ret = messages.Get(fileUxmPatchWarning);
                }
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
        public static string GetMD5Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = MD5.ComputeHash(stream);
                return string.Join("", hash.Select(x => $"{x:x2}"));
            }
        }

        public static string GetMD5TextHash(string text)
        {
            byte[] hash = MD5.ComputeHash(new MemoryStream(Encoding.UTF8.GetBytes(text)));
            return string.Join("", hash.Select(x => $"{x:x2}"));
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
                        if (false && File.Exists(overrideFile))
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
            // "_resource"
            string[] suffixes = ds3 ? new[] { "_effect" } : new[] { "" };
            foreach (string suffix in suffixes)
            {
                string commonPath = $@"{inDir}\{prefix}sfxbnd_commoneffects{suffix}.ffxbnd.dcx";
                if (!File.Exists(commonPath)) return false;
                Console.WriteLine(new FileInfo(commonPath).FullName);
                BND4 sfxCommon = BND4.Read(commonPath);
                HashSet<string> sfxFiles = new HashSet<string>(sfxCommon.Files.Select(f => f.Name));
                Console.WriteLine(string.Join(",", maps));
                foreach (string map in maps.Select(m => m.Split('_')[0]).Distinct())
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
                    // TODO: This depends on paramAwareMode being off
                    Instr instr = events.Parse(i);
                    if (instr.Name == "IfElapsedSeconds" && instr[1] is float wait)
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
            ParamDictionary Params = game.Params;

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
                    // Slight hack to ignore this for specific preset
                    if (opt["enemy"] && opt["bosses"] && commonInit)
                    {
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { maxPermSlot + 1, (uint)750, (uint)9307, (uint)9314 }));
                    }
                }
            }
        }

        [Localize]
        private static readonly Text fogRunningError = new Text(
            "Error: Unrestricted item placement was enabled in Item Randomizer,\nbut Fog Gate Randomizer was not detected",
            "GameMenu_fogRunningError");

        public static void EldenCommonPass(GameData game, RandomizerOptions opt, Messages messages, PermutationWriter.Result result = null)
        {
            // Resident speffects
            PARAM.Row baseSp = game.Params["SpEffectParam"][5020];
            for (int i = 0; i < 20; i++)
            {
                GameEditor.CopyRow(baseSp, game.AddRow("SpEffectParam", 6950 + i));
            }

            HashSet<(int, int)> deleteCommands = new HashSet<(int, int)>
            {
                (2003, 28),  // Achievement
                // (2007, 15),  // Tutorial popup
            };
            List<int> debugLots = new List<int>
            {
                997200, // Rowa
                997210, // Golden Rowa
                997220, // Rimed Rowa
                998400, // Cave Moss
                998410, // Budding Cave Moss
                998420, // Crystal Cave Moss
            };
            if (opt["nerfmalenia"])
            {
                foreach (PARAM.Row row in game.Params["SpEffectParam"].Rows)
                {
                    if (row.ID >= 18400 && row.ID < 18420)
                    {
                        row["changeHpRate"].Value = 0f;
                    }
                }
            }
            int gargSpread = 20003;
            int gargId = 477020003;
            PARAM.Row gargNerf = game.AddRow("SpEffectParam", gargId, 20003);
            gargNerf["changeHpRate"].Value = 0f;
            gargNerf["changeHpPoint"].Value = 0;
            if (opt["nerfgargoyles"])
            {
                foreach (PARAM.Row row in game.Params["AtkParam_Npc"].Rows.Where(r => r.ID == 4770860 || r.ID == 4770451))
                {
                    row["atkMag"].Value = (ushort)0;
                }
            }
            if (opt["sombermode"])
            {
                foreach (PARAM.Row row in game.Params["EquipMtrlSetParam"].Rows)
                {
                    int id = (int)row["materialId01"].Value;
                    int cat = (byte)row["materialCate01"].Value;
                    int num = (sbyte)row["itemNum01"].Value;
                    if (cat == 4 && id >= 10100 && id < 10110 && num > 1)
                    {
                        row["itemNum01"].Value = (byte)1;
                    }
                }
            }
            if (opt["nerfsh"])
            {
                PARAM.Row row = game.Params["EquipParamWeapon"][17030000];
                // Change Serpent-Hunter to match Meteorite Staff
                row["reinforceTypeId"].Value = (short)3000;
                row["materialSetId"].Value = 0;
                row["isCustom"].Value = (byte)0;
                for (int i = 1; i < 25; i++)
                {
                    row[$"originEquipWep{i}"].Value = -1;
                }
            }
            if (opt["weaponreqs"])
            {
                // Same fields used in CharacterWriter requirements gathering
                List<string> weaponFields = new List<string> { "properStrength", "properAgility", "properMagic", "properFaith", "properLuck" };
                foreach (PARAM.Row row in game.Params["EquipParamWeapon"].Rows)
                {
                    foreach (string field in weaponFields)
                    {
                        row[field].Value = (byte)0;
                    }
                }
                List<string> magicFields = new List<string> { "requirementIntellect", "requirementFaith", "requirementLuck" };
                foreach (PARAM.Row row in game.Params["Magic"].Rows)
                {
                    foreach (string field in magicFields)
                    {
                        row[field].Value = (byte)0;
                    }
                }
            }
            // 71801 is graveyard flag, 102 is "definitely in limgrave"?
            // This one should be opening the graveyard exit but it seems to activate straight away (because endif tutorial?)
            int mapUnlockFlag = 18000021;
            if (opt["allmaps"])
            {
                FMG npcNames = game.ItemFMGs["NpcName"];
                HashSet<int> merchantNames = new HashSet<int>(
                    npcNames.Entries.Where(e => e.Text != null && e.Text.Contains("Merchant")).Select(e => e.ID));
                void rewriteMerchantIcons(PARAM.Row row)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        // Ignore npc vs place name, as the ids are distinct probably
                        int name = (int)row[$"textId{i}"].Value;
                        if (merchantNames.Contains(name))
                        {
                            row[$"textEnableFlagId{i}"].Value = (uint)mapUnlockFlag;
                            int giftFlag = 0;
                            // Exclusion for Kale
                            if (result != null && result.MerchantGiftFlags.TryGetValue(name, out giftFlag) && giftFlag != 400049)
                            {
                                row[$"textDisableFlagId{i}"].Value = (uint)giftFlag;
                            }
                        }
                    }
                }
                foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
                {
                    if ((int)row["textId1"].Value > 0)
                    {
                        row["eventFlagId"].Value = (uint)mapUnlockFlag;
                    }
                    rewriteMerchantIcons(row);
                }
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    rewriteMerchantIcons(row);
                }
            }

            messages.SetFMGEntry(
                game, FMGCategory.Menu, "EventTextForMap",
                RuntimeParamChecker.FogMessageId, fogRunningError);
            EMEVD.Instruction debugLot(int i)
            {
                return new EMEVD.Instruction(2003, 4, new List<object> { debugLots[i] });
            }
            // game.Params["AssetEnvironmentGeometryParam"][277011]["HP"].Value = (short)500;
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                string map = entry.Key;
                EMEVD emevd = entry.Value;
                void addNewEvent(int id, IEnumerable<EMEVD.Instruction> instrs, EMEVD.Event.RestBehaviorType rest = EMEVD.Event.RestBehaviorType.Default)
                {
                    if (emevd.Events.Any(e => e.ID == id)) return;
                    AddSimpleEvent(emevd, id, instrs, rest);
                }
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    // Some custom edits
                    if (false && e.ID == 6905)
                    {
                        e.Instructions.Insert(1, debugLot(1));
                        e.Instructions.Insert(0, debugLot(0));
                    }
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        // Instr instr = events.Parse(e.Instructions[i]);
                        bool delete = false;
                        if (deleteCommands.Contains((ins.Bank, ins.ID)))
                        {
                            delete = true;
                        }
                        if (delete)
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                            game.WriteEmevds.Add(entry.Key);
                        }
                    }
#if DEBUG
                    if (entry.Key == "m12_02_00_00" && opt["cheat_shortcut"])
                    {
                        if (e.ID == 12022609 || e.ID == 12022629)
                        {
                            int gotoIndex = e.Instructions.FindIndex(ins => ins.Bank == 1003 && ins.ID == 101);
                            if (gotoIndex >= 0)
                            {
                                e.Instructions.Insert(gotoIndex, new EMEVD.Instruction(1000, 3, new List<object> { (byte)1 }));
                            }
                        }
                    }
#endif

                }
                if (entry.Key == "common")
                {
#if DEBUG
                    // Testing event
                    addNewEvent(19003105, new List<EMEVD.Instruction>
                    {
                        // If event flag, give lot
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 110 }), debugLot(0),
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 197 }), debugLot(1),
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 177 }), debugLot(2),
                        // Wait for event flag, give lot
                        // new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)1, (byte)0, 187 }), debugLot(3),
                        // Item posession check
                        // IfPlayerHasdoesntHaveItem(OR01, ItemType.Gem = 4, X, OwnershipState.Owns = 1)
                        // new EMEVD.Instruction(3, 4, new List<object> { (byte)5, (byte)4, 60100, (byte)1 }),
                        // new EMEVD.Instruction(1000, 1, new List<object>{ (byte)1, (byte)0, (byte)5 }), debugLot(0),
                    });
                    List<string> gifts = new List<string>();
                    // gifts.Add("Rotten Breath");
                    // gifts.Add("Mimic Tear Ashes +10");
                    // gifts.AddRange(new[] { "Godskin Stitcher", "Latenna the Albinauric +1" });
                    // gifts.AddRange(new[] { "Gargoyle's Greatsword", "Redmane Knight Helm", "Redmane Surcoat" });
                    // gifts.AddRange(new[] { "Rivers of Blood", "Grafted Blade Greatsword" });
                    // gifts.AddRange(new List<string> { "Mushroom Crown", "Mushroom Head", "Mushroom Body", "Mushroom Arms", "Mushroom Legs" });
                    // Black raptor set also pretty cool. And fingerprint set
                    Dictionary<ItemKey, int> giftAmounts = gifts.ToDictionary(g => game.ItemForName(g), g => 1);
                    // giftAmounts[new ItemKey(ItemType.GOOD, 8136)] = 1;
                    // game.Params["ItemLotParam_map"][34110080]["lotItemId01"].Value = 8151;
                    // giftAmounts[new ItemKey(ItemType.GOOD, 20760)] = 10; // Mushroom
                    // giftAmounts[new ItemKey(ItemType.GOOD, 20651)] = 10; // Trina Lily
                    if (opt["cheatgift"] && giftAmounts.Count > 0)
                    {
                        int lotId = 12020840;
                        PARAM.Row row = null;
                        ItemKey checkItem = null;
                        foreach (KeyValuePair<ItemKey, int> giftEntry in giftAmounts)
                        {
                            if (row == null)
                            {
                                row = game.Params["ItemLotParam_map"][lotId];
                            }
                            else
                            {
                                row = game.AddRow("ItemLotParam_map", lotId + 1, lotId);
                                lotId++;
                            }
                            ItemKey item = giftEntry.Key;
                            row["lotItemId01"].Value = item.ID;
                            row["lotItemCategory01"].Value = (int)game.LotValues[item.Type];
                            row["lotItemNum01"].Value = giftEntry.Value;
                            if (checkItem == null) checkItem = item;
                        }
                        game.Params["ItemLotParam_map"].Rows.Sort((a, b) => (a.ID.CompareTo(b.ID)));
                        // game.Params["ItemLotParam_map"][12020840]["ItemLotId1"].Value = 207010;
                        addNewEvent(19003106, new List<EMEVD.Instruction>
                        {
                            // IfPlayerHasdoesntHaveItem(MAIN, ItemType.Goods = 3, 207010, OwnershipState.Owns = 0)
                            new EMEVD.Instruction(3, 4, new List<object> { (byte)0, (byte)checkItem.Type, checkItem.ID, (byte)0 }),
                            // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventIDSlotNumber, 0)
                            // new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, 0 }),
                            // DirectlyGivePlayerItem(ItemType.Goods = 3, 207010, 12027885, 1), yet again fails
                            // new EMEVD.Instruction(2003, 43, new List<object> { (byte)3, 207000, 12027780, 1 }),
                            // SetEventFlag(TargetEventFlagType.EventFlag, 12027840, OFF)
                            new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 12027840, (byte)0 }),
                            // Item lot
                            new EMEVD.Instruction(2003, 4, new List<object> { 12020840 }),
                        });
                    }
                    if (opt["cheathp"])
                    {
                        addNewEvent(19003107, new List<EMEVD.Instruction>
                        {
                            // 10000: self. 35000: only helpers.
                            // SetSpEffect(35000, 110)
                            new EMEVD.Instruction(2004, 8, new List<object> { 20000, 110 }),
                            new EMEVD.Instruction(2004, 8, new List<object> { 35000, 110 }),
                            // Disable damage
                            // new EMEVD.Instruction(2004, 39, new List<object> { 10000, 0 }),
                            // Scale damage
                            new EMEVD.Instruction(2004, 8, new List<object> { 20000, 7200 }),
                            new EMEVD.Instruction(2004, 8, new List<object> { 35000, 7200 }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)2 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                        });
                    }
                    if (opt["bonfire"])
                    {
                        List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                        foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                        {
                            uint flag = (uint)row["eventflagId"].Value;
                            if (flag / 10000 == 7)
                            {
                                instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, flag, (byte)1 }));
                            }
                        }
                        addNewEvent(19003108, instrs);
                    }
#endif
                    // Event for Varre
                    // Use 19003110 as base for "real" stuff. 19003130 in PermutationWriter
                    addNewEvent(19003110, new List<EMEVD.Instruction>
                    {
                        // The dialogue trigger is 1035449207 and it sets 1035449235 (progress quest) and 3198 ("update me" flag)
                        // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 1035449235)
                        new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)0, 1035449235 }),
                        // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 1035449207)
                        new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, 1035449207 }),
                        // debugLot(2),
                        // SetEventFlag(TargetEventFlagType.EventFlag, 1035449235, ON)
                        // SetEventFlag(TargetEventFlagType.EventFlag, 3198, ON)
                        new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 1035449235, (byte)1 }),
                        new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 3198, (byte)1 }),
                    });

                    if (opt["allmaps"])
                    {
                        // Event 1600 does map gifts, but just piggyback on item flags
                        // Mapping from item id to item flag. For now though just use map-activated flags directly...
                        SortedDictionary<int, int> mapFlags = new SortedDictionary<int, int>
                        {
                            [8600] = 62010,  // Map: Limgrave, West
                            [8601] = 62011,  // Map: Weeping Peninsula
                            [8602] = 62012,  // Map: Limgrave, East
                            [8603] = 62020,  // Map: Liurnia, East
                            [8604] = 62021,  // Map: Liurnia, North
                            [8605] = 62022,  // Map: Liurnia, West
                            [8606] = 62030,  // Map: Altus Plateau
                            [8607] = 62031,  // Map: Leyndell, Royal Capital
                            [8608] = 62032,  // Map: Mt. Gelmir
                            [8609] = 62040,  // Map: Caelid
                            [8610] = 62041,  // Map: Dragonbarrow
                            [8611] = 62050,  // Map: Mountaintops of the Giants, West
                            [8612] = 62051,  // Map: Mountaintops of the Giants, East
                            [8613] = 62060,  // Map: Ainsel River
                            [8614] = 62061,  // Map: Lake of Rot
                            [8615] = 62063,  // Map: Siofra River
                            [8616] = 62062,  // Map: Mohgwyn Palace
                            [8617] = 62064,  // Map: Deeproot Depths
                            [8618] = 62052,  // Map: Consecrated Snowfield
                        };
                        // Simpler event, just wait for entry into Limgrave
                        List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                        // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 102)
                        instrs.Add(new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, mapUnlockFlag }));
                        foreach (KeyValuePair<int, int> mapFlag in mapFlags)
                        {
                            int flag = mapFlag.Value; // + 1000: just used for notification evidently
                            // SetEventFlag(TargetEventFlagType.EventFlag, flag, ON)
                            instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, flag, (byte)1 }));
                        }
                        // Underground maps visible
                        instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 82001, (byte)1 }));
                        addNewEvent(19003111, instrs);
                        EMEVD.Event grantEvent = emevd.Events.Find(e => e.ID == 1600);
                        if (grantEvent != null)
                        {
                            OldParams pre = OldParams.Preprocess(grantEvent);
                            grantEvent.Instructions.RemoveAll(ins => ins.Bank == 2003 && ins.ID == 66);
                            pre.Postprocess();
                        }
                    }

                    if (opt["fog"])
                    {
                        addNewEvent(19003112, new List<EMEVD.Instruction>
                        {
                            // DisplayGenericDialog(msg, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5);
                            new EMEVD.Instruction(2007, 1, new List<object> { RuntimeParamChecker.FogMessageId, (short)1, (short)6, 0, 5f }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)30 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 })
                        });
                    }
                    game.WriteEmevds.Add(entry.Key);
                }
                int endRunes = 0;
                if (opt["runereq"])
                {
                    endRunes = 7;
                }
                else if (opt.GetInt("runes_end", 1, 7, out int runeOpt))
                {
                    endRunes = runeOpt;
                }
                if (entry.Key == "m19_00_00_00" && endRunes > 0)
                {
                    int runeFlag = 180 + endRunes;
                    EMEVD.Event fog = emevd.Events.Find(ev => ev.ID == 19002500);
                    if (fog == null) throw new Exception($"Couldn't locate event in {entry.Key} to make final boss require all Great Runes");
                    OldParams pre = OldParams.Preprocess(fog);
                    int labelIndex = fog.Instructions.FindIndex(ins => ins.Bank == 1014 && ins.ID == 1);
                    int sfxIndex = fog.Instructions.FindIndex(Math.Max(labelIndex, 0), ins => ins.Bank == 2006 && ins.ID == 4);
                    if (labelIndex < 0 || sfxIndex < 0 || sfxIndex != labelIndex + 1)
                    {
                        throw new Exception($"Couldn't locate index to make final boss require all Great Runes ({labelIndex} {sfxIndex})");
                    }
                    fog.Instructions.InsertRange(sfxIndex, new List<EMEVD.Instruction>
                    {
                        // SkipIfEventFlag(<lines>, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(1003, 1, new List<object> { (byte)6, (byte)1, (byte)0, runeFlag }),
                        // 9320 = Examine
                        // IfActionButton(OR05, 9320, 19001500)
                        new EMEVD.Instruction(3, 24, new List<object> { (sbyte)-5, 9320, 19001500 }),
                        // IfEventFlag(OR05, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(3, 0, new List<object> { (sbyte)-5, (byte)1, (byte)0, runeFlag }),
                        // IfConditionGroup(MAIN, ON, OR05)
                        new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)-5 }),
                        // EndIfEventFlag(EventEndType.Restart, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(1003, 2, new List<object> { (byte)1, (byte)1, (byte)0, runeFlag }),
                        // WaitFixedTimeSeconds(0.5)
                        // new EMEVD.Instruction(1001, 0, new List<object> { (float)0.5 }),
                        // 20003 = You cannot proceed without more Great Runes
                        // DisplayGenericDialog(20003, PromptType.YESNO = 0, NumberofOptions.NoButtons = 6, 19001500, 3)
                        new EMEVD.Instruction(2007, 1, new List<object> { 20003, (short)0, (short)6, 19001500, 3f }),
                        // EndUnconditionally(EventEndType.Restart)
                        new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                    });
                    pre.Postprocess();

                    game.WriteEmevds.Add(entry.Key);
                }
            }
        }

        public static readonly Dictionary<string, string> Langs = new Dictionary<string, string>
        {
            // The commented out languages are technically the full CultureInfo equivalent.
            // However, we're only using this dictionary if we have a specific game language in the first place,
            // so there is no point to having finer-grained resolution because it can't be utilized in-game.
            ["deude"] = "de", // de-DE
            ["engus"] = "en", // en-US
            ["frafr"] = "fr", // fr-FR
            ["itait"] = "it", // it-IT
            ["jpnjp"] = "ja", // ja-JP
            ["korkr"] = "ko", // ko-KR
            ["polpl"] = "pl", // pl-PL
            ["porbr"] = "pt-BR", // pt-BR
            ["rusru"] = "ru", // ru-RU
            ["spaar"] = "es", // es-AR
            ["spaes"] = "es-ES", // es-ES
            ["thath"] = "th", // th-TH
            ["zhocn"] = "zh", // zh-CN
            ["zhotw"] = "zh", // zh-CN
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
        }.SelectMany(t => t.Contains("$lang") ? Langs.Keys.Select(l => t.Replace("$lang", l)) : new[] { t }).ToList();
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

        public static void UpdateEldenRing(GameData game, RandomizerOptions opt)
        {
            string gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game";
            if (opt["undcx"])
            {
                Console.WriteLine("Map");
                game.UnDcx(gameDir + @"\map\mapstudio");
                if (opt["unasset"])
                {
                    HashSet<string> badAssets = new HashSet<string> { "AEG099_660", "AEG099_720", "AEG099_721", "AEG099_723" };
                    foreach (string path in Directory.GetFiles(gameDir + @"\map\mapstudio", "*.msb.dcx"))
                    {
                        MSBE msb = MSBE.Read(path);
                        msb.Parts.Assets.RemoveAll(a => badAssets.Contains(a.ModelName));
                        msb.Parts.DummyAssets.RemoveAll(a => badAssets.Contains(a.ModelName));
                        string otherPath = gameDir + @"\vanilla\map\mapstudio\" + Path.GetFileName(path);
                        msb.Write(otherPath);
                    }
                }
                Console.WriteLine("Msg");
                foreach (string lang in Langs.Keys)
                {
                    game.DumpMessages(gameDir + $@"\msg\{lang}");
                }
                return;
            }
            if (opt["itemname"])
            {
                void printNames(string paramName, FMG fmg)
                {
                    PARAM param = game.Params[paramName];
                    int match = 0;
                    Dictionary<int, string> names = fmg.Entries
                        .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                        .ToDictionary(e => e.ID, e => e.Text);
                    List<string> lines = new List<string>();
                    foreach (PARAM.Row row in param.Rows)
                    {
                        string name = fmg[row.ID];
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            lines.Add($"{row.ID} {name}");
                            match++;
                        }
                    }
                    if (paramName == "EquipParamGoods")
                    {
                        // Hack for sword key, in EldenLocationDataScraper/PermutationWriter
                        lines.Add("9990 Imbued Sword Key 2");
                        lines.Add("9991 Imbued Sword Key 3");
                    }
                    Console.WriteLine($"{paramName}: {match} match, out of {param.Rows.Count} rows and {names.Count} names");
                    File.WriteAllText($"diste/Names/{paramName}.txt", string.Join("", lines.Select(l => $"{l}\r\n")));
                }
                printNames("EquipParamGoods", game.ItemFMGs["GoodsName"]);
                printNames("EquipParamAccessory", game.ItemFMGs["AccessoryName"]);
                printNames("EquipParamGem", game.ItemFMGs["GemName"]);
                printNames("EquipParamWeapon", game.ItemFMGs["WeaponName"]);
                printNames("EquipParamProtector", game.ItemFMGs["ProtectorName"]);
                return;
            }
            List<string> flatFiles = new List<string> { @"regulation.bin", @"event", @"sfx", @"script\talk", @"map\mapstudio" };
            List<string> nestedFiles = new List<string> { @"msg" };
            string outDir = @"diste\Vanilla";
            List<string> unusedGameFiles = new List<string>();
            List<string> unusedVanillaFiles = Directory.GetFiles(outDir).Select(Path.GetFileName).ToList();
            List<(string, string)> copy = new List<(string, string)>();
            foreach (string flat in flatFiles)
            {
                string nextFlat = $@"{gameDir}\{flat}";
                if (File.Exists(nextFlat))
                {
                    string prevFlat = $@"{outDir}\{flat}";
                    if (GetMD5Hash(prevFlat) == GetMD5Hash(nextFlat))
                    {
                        Console.WriteLine($"Same {nextFlat} -> {prevFlat}");
                    }
                    else
                    {
                        Console.WriteLine($"Different {nextFlat} -> {prevFlat}");
                        copy.Add((nextFlat, prevFlat));
                    }
                    unusedVanillaFiles.Remove(flat);
                }
                else if (Directory.Exists(nextFlat))
                {
                    unusedGameFiles.AddRange(Directory.GetFiles(nextFlat).Select(Path.GetFileName));
                    foreach (string prev in Directory.GetFiles(outDir))
                    {
                        string sub = Path.GetFileName(prev);
                        string next = $@"{nextFlat}\{sub}";
                        if (File.Exists(next))
                        {
                            unusedVanillaFiles.Remove(sub);
                            if (GetMD5Hash(prev) == GetMD5Hash(next))
                            {
                                Console.WriteLine($"Same {next} -> {prev}");
                            }
                            else
                            {
                                Console.WriteLine($"Different {next} -> {prev}");
                                copy.Add((next, prev));
                            }
                        }
                        unusedGameFiles.Remove(sub);
                    }
                }
                else throw new Exception(nextFlat);
            }
            foreach (string nest in nestedFiles)
            {
                // Don't check unused ones here, we expect a bunch of em
                string[] allFiles = Directory.GetFiles($@"{outDir}\{nest}", "*", SearchOption.AllDirectories);
                foreach (string prev in allFiles)
                {
                    string sub = prev.Substring(prev.IndexOf(nest));
                    string next = $@"{gameDir}\{sub}";
                    if (GetMD5Hash(prev) == GetMD5Hash(next))
                    {
                        Console.WriteLine($"Same {next} -> {prev}");
                    }
                    else
                    {
                        Console.WriteLine($"Different {next} -> {prev}");
                        copy.Add((next, prev));
                    }
                }
            }
            foreach (string unused in unusedGameFiles)
            {
                Console.WriteLine($"Unused {unused}");
            }
            foreach (string unused in unusedVanillaFiles)
            {
                Console.WriteLine($"Deleted {unused}");
            }
            bool wetrun = opt["wetrun"];
            string drySuffix = wetrun ? "" : " (dryrun)";
            foreach ((string next, string prev) in copy)
            {
                Console.WriteLine($"Copy {next} -> {prev}{drySuffix}");
                if (wetrun)
                {
                    File.Copy(next, prev, true);
                }
            }
        }
    }
}
