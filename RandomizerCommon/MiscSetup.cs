using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SoulsFormats;
using static RandomizerCommon.Events;

namespace RandomizerCommon
{
    public class MiscSetup
    {
        // https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        private static HashSet<string> badModEngines = new HashSet<string>
        {
            "dfff963c88e82dc19c5e8592f464b9ca",
            "81137f302c6905f42ddf76fc52287e8e",
            "b25ddefec3f78278be633b85101ecccb",
            "3b37d8bbbce586a6f71dff687a25eb9c",
            "22adc919abcdf24df0f6d81e00a9974e",
            "71e9e56c741ca6ff338c16f8cb37c0e4",
            "9168d85ef78d13e108c30bdb74984c46",
            "267153f2297ba189591304560aab3a3e",
            "fbf98322736d493c5048804cc7efb11c",
        };
        private static HashSet<string> justWorksModEngines = new HashSet<string>
        {
            "d79551b08bee23ab1d190448894b86d1",
        };
        // May need to add a new section for requiresFurtherInstructionsModEngines later...

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
                ret = "Error: Running from same directory as Sekiro.exe\r\nThe randomizer and its files must be in a subdirectory";
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
                ret = "Error: Sekiro Mod Engine not found in parent directory\r\nDownload dinput8.dll and modengine.ini from the Sekiro Randomizer Files section";
                return true;
            }
            // Check Mod Engine version
            string modEngineHash = GetMD5Hash(@"..\dinput8.dll");
            if (badModEngines.Contains(modEngineHash))
            {
                // TODO: Fix this up when it official releases
                // return $"Sekiro Mod Engine is out of date (needs version >= 420)\r\nDownload the latest version or else enemy randomization will definitely crash the game";
                // ret = "Sekiro Mod Engine needs the latest version (not yet officially released)\r\nCopy dinput8.dll into parent dir or else enemy randomization will definitely crash the game";
                ret = "Error: Sekiro Mod Engine needs to be the unofficial version from the Sekiro Randomizer Files section\r\nCopy its dinput8.dll into parent dir or else enemy randomization will definitely crash the game!";
                // "Unrecognized version of Sekiro Mod Engine\r\nEither use the one packaged with the randomizer (see install instructions) or copy."
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
                ret = "Warning: Unknown version of Sekiro Mod Engine detected\r\nUse the one from the files section of Sekiro Randomizer, and update the randomizer if there is an update";
                return false;
            }
            return false;
        }

        public static bool CheckDS3ModEngine(out string ret, out bool encrypted)
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
            // Check ini variables
            string ini = new FileInfo(@"..\modengine.ini").FullName.ToString();

            StringBuilder loadLoose = new StringBuilder(255);
            GetPrivateProfileString("files", "loadLooseParams", "", loadLoose, 255, ini);
            if (loadLoose.ToString() == "1")
            {
                encrypted = false;
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

        public static void CombineAI(List<string> maps, string outDir)
        {
            // Only works for Sekiro, since all other games have additional per-map metadata that actually matters
            string commonPath = $@"{outDir}\..\script\aicommon.luabnd.dcx";
            BND4 aiCommon = BND4.Read(commonPath);
            HashSet<string> aiFiles = new HashSet<string>(aiCommon.Files.Select(f => f.Name));
            foreach (string map in maps)
            {
                string aiPath = $@"{outDir}\..\script\{map}.luabnd.dcx";
                if (!File.Exists(aiPath)) continue;

                BND4 ai = BND4.Read(aiPath);
                ai.Files = ai.Files.Where(file =>
                {
                    if (!file.Name.Contains("out")) return true;
                    if (!aiFiles.Contains(file.Name))
                    {
                        aiCommon.Files.Add(file);
                        aiFiles.Add(file.Name);

                    }
                    return false;
                }).ToList();
                ai.Write($@"{outDir}\script\{map}.luabnd.dcx");
            }
            int startId = 2000;
            foreach (BinderFile file in aiCommon.Files)
            {
                if (file.ID < 3000) file.ID = startId++;
                Console.WriteLine(file);
            }
            aiCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
            aiCommon.Write($@"{outDir}\script\aicommon.luabnd.dcx");
        }

        public static bool CheckSFX()
        {
            string customPath = @"sfx\sfxbnd_commoneffects.ffxbnd.dcx";
            if (!File.Exists(customPath)) return false;
            // Original size: 64,029,504. New size: 78,592,543
            if (new FileInfo(customPath).Length < 75000000) return false;
            return true;
        }

        public static bool CombineSFX(List<string> maps, string outDir)
        {
            string inDir = new DirectoryInfo($@"{outDir}\..\sfx").FullName;
            string commonPath = $@"{inDir}\sfxbnd_commoneffects.ffxbnd.dcx";
            if (!File.Exists(commonPath)) return false;
            Console.WriteLine(commonPath);
            Console.WriteLine(new FileInfo(commonPath).FullName);
            BND4 sfxCommon = BND4.Read(commonPath);
            HashSet<string> sfxFiles = new HashSet<string>(sfxCommon.Files.Select(f => f.Name));
            Console.WriteLine(string.Join(",", maps));
            foreach (string map in maps.Select(m => m.Substring(0, 3)).Distinct())
            {
                string path = $@"{inDir}\sfxbnd_{map}.ffxbnd.dcx";
                if (!File.Exists(path)) continue;

                BND4 sfx = BND4.Read(path);
                sfx.Files = sfx.Files.Where(file =>
                {
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
                sfx.Write($@"{outDir}\sfx\sfxbnd_{map}.ffxbnd.dcx");
            }
            int startId = 0;
            foreach (BinderFile file in sfxCommon.Files)
            {
                // Ignore prefixes here
                file.ID = startId++;
            }
            sfxCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
            sfxCommon.Write($@"{outDir}\sfx\sfxbnd_commoneffects.ffxbnd.dcx");
            return true;
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

        private static List<string> langs = new List<string>
        {
            "engus", "frafr", "itait", "jpnjp", "korkr", "polpl", "porbr", "rusru", "spaar", "spaes", "thath", "zhocn", "zhotw",
        };
        private static List<string> fileDirs = new List<string>
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
            @"script",  // This should be a no-op in Sekiro
            @"script\talk",
            @"sfx",  // This should be a no-op in Sekiro
            @"shader",
            @"sound",
        }.SelectMany(t => t.Contains("$lang") ? langs.Select(l => t.Replace("$lang", l)) : new[] { t }).ToList();
        private static List<string> extensions = new List<string>
        {
            ".hks", ".dcx", ".gfx", ".dds", ".fsb", ".fev", ".itl", ".tpf", ".entryfilelist", ".hkxbdt", ".hkxbhd", "Data0.bdt"
        };
        private static Regex extensionRe = new Regex(string.Join("|", extensions.Select(e => e + "$")));
        public static List<string> GetGameFiles(string dir)
        {
            List<string> allFiles = new List<string>();
            foreach (string subdir in fileDirs)
            {
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
