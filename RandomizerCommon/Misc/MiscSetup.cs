using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SoulsIds;
using SoulsFormats;
using static RandomizerCommon.Messages;

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
            else if (File.Exists("sekiro.exe"))
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

        public static bool CheckRequiredDS3Files(Messages messages, out string ret)
        {
            ret = null;
            DirectoryInfo dist = new DirectoryInfo("dist");
            if (!dist.Exists)
            {
                ret = messages.Get(fileUnpackError, "dist", dist.FullName);
            }
            else if (File.Exists("DarkSoulsIII.exe"))
            {
                ret = "Error: Running from same directory as DarkSoulsIII.exe\r\nThe randomizer and its files must be in a subdirectory";
            }
            return ret == null;
        }

        public static bool CheckDS1GameFiles(Messages messages, string exe, out string ret)
        {
            // Return true if no errors
            ret = null;
            if (!File.Exists(exe) || !Path.GetFileName(exe).Equals("DarkSoulsRemastered.exe", StringComparison.OrdinalIgnoreCase))
            {
                ret = messages.Get(fileExeMissingError);
            }
            return ret == null;
        }

        public static bool CheckNightreignGameFiles(Messages messages, string exe, string helperPath, out string ret)
        {
            // Return true if no errors
            ret = null;
            if (!File.Exists(exe) || !Path.GetFileName(exe).Equals("nightreign.exe", StringComparison.OrdinalIgnoreCase))
            {
                ret = messages.Get(fileExeMissingError);
            }
            // Put this here since it's not needed beforehand
            if (!File.Exists(helperPath))
            {
                ret = $"Error: Helper dll not found at {helperPath}";
            }
            return ret == null;
        }

        [Localize]
        public static readonly Text CreateFileError = new Text(
            "Error: Failed to create file. Make sure the randomizer is unpacked to disk with all of the files kept together, and make sure no Antivirus programs are interfering. The randomizer will only modify files in its own directory or in the game directory.\n{0}",
            "Randomizer_createFileError");

        [Localize]
        private static readonly Text fileUnpackError = new Text(
            "Error: Required metadata directory \"{0}\" not found at {1}\r\nFirst use \"Extract here\" on the entire zip file, then run the program from inside of the extracted \"randomizer\" directory. Do not drag-and-drop individual files.",
            // "Error: Can't find required metadata files.\nFor the randomizer to work, you must unpack it to disk and keep all of the files together",
            "EldenForm_fileUnpackError");

        // [Localize]
        private static readonly Text fileUnpackAnywhereError = new Text(
            "Error: Required metadata directory \"{0}\" not found at {1}\r\nUse \"Extract here\" on the entire zip file before running randomizer. Do not drag-and-drop individual files.",
            // "Error: Can't find required metadata files.\nFor the randomizer to work, you must unpack it to disk and keep all of the files together",
            "MainForm_fileUnpackAnywhereError");

        public static bool CheckRequiredEldenFiles(Messages messages, out string ret)
        {
            // Return true if no errors
            ret = null;
            DirectoryInfo dist = new DirectoryInfo("diste");
            if (!dist.Exists)
            {
                ret = messages.Get(fileUnpackError, "diste", dist.FullName);
            }
            return ret == null;
        }

        public static bool CheckRequiredDS1Files(Messages messages, out string ret)
        {
            // Return true if no errors
            ret = null;
            DirectoryInfo dist = new DirectoryInfo("dist1");
            if (!dist.Exists)
            {
                ret = messages.Get(fileUnpackAnywhereError, "dist1", dist.FullName);
            }
            return ret == null;
        }

        public static bool CheckRequiredNightreignFiles(Messages messages, out string ret)
        {
            // Return true if no errors
            ret = null;
            DirectoryInfo dist = new DirectoryInfo("distnr");
            if (!dist.Exists)
            {
                ret = messages.Get(fileUnpackAnywhereError, "distnr", dist.FullName);
            }
            return ret == null;
        }

        private static string originalWorkingDirectory;
        public static string GetModEngineOutputDirectory()
        {
            return originalWorkingDirectory ?? Directory.GetCurrentDirectory();
        }

#if DEBUG
        public static void SetWorkingDirectoryForDevelopment()
        {
            // Before accessing anything, allow running randomizer from VS build dir for easier development
            if (originalWorkingDirectory != null)
            {
                return;
            }
            const string rootName = "SoulsRandomizers";
            DirectoryInfo parent = new DirectoryInfo(".");
            string currentDir = parent.FullName;
            if (parent.Name != rootName && currentDir.Contains(rootName) && currentDir.Contains("bin"))
            {
                while (parent != null && parent.Name != rootName)
                {
                    parent = parent.Parent;
                }
                if (parent?.Name == rootName && parent.Exists)
                {
                    originalWorkingDirectory = currentDir;
                    Directory.SetCurrentDirectory(parent.FullName);
                }
            }
        }
#endif

        // Currently unused. The main lookup site in Randomizer could use this, but a lot of other places have their own checks.
        public static bool ResolveDistDir(string name, out string distPath)
        {
            // Returns true if directory found, alongside the simplest path to it.
            // Returns false if directory not found, alongside the full expected path.
            DirectoryInfo dist = new DirectoryInfo(name);
            if (dist.Exists)
            {
                distPath = name;
                return true;
            }
#if DEBUG
            const string root = "SoulsRandomizers";
            if (dist.FullName.Contains(root))
            {
                DirectoryInfo parent = new DirectoryInfo(".");
                while (parent != null && parent.Name != root)
                {
                    parent = parent.Parent;
                }
                if (parent?.Name == root)
                {
                    parent = parent.GetDirectories(name).FirstOrDefault();
                    if (parent != null && parent.Exists)
                    {
                        distPath = parent.FullName;
                        return true;
                    }
                }
            }
#endif
            distPath = dist.FullName;
            return true;
        }

        // Note: Doesn't return error or not (use ret != null for that), returns if fatal or not
        public static bool CheckSekiroModEngine(out string ret)
        {
            ret = null;
            if (!File.Exists(@"..\sekiro.exe"))
            {
                ret = "Error: sekiro.exe not found in parent directory\r\nFor randomization to work, move the randomizer folder to your Sekiro install location";
                return true;
            }
            if (!File.Exists(@"..\dinput8.dll") || !File.Exists(@"..\modengine.ini"))
            {
                ret = "Error: Sekiro Mod Engine not found in parent directory\r\nDownload dinput8.dll and modengine.ini from Sekiro Mod Engine";
                return true;
            }
            // Check Mod Engine version
            string modEngineHash = Util.GetMD5FileHash(@"..\dinput8.dll");
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

        public static void ModifyDS3Files()
        {
            // Make a backup save
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appData = $@"{appData}\DarkSoulsIII";
            if (Directory.Exists(appData))
            {
                string[] saveDirs = Directory.GetDirectories(appData);
                foreach (string saveDir in saveDirs)
                {
                    string saveFile = $@"{saveDir}\DS30000.sl2";
                    string saveBackup = $@"{saveDir}\DS30000.sl2.randobak";
                    if (File.Exists(saveFile) && !File.Exists(saveBackup) && !Directory.Exists(saveBackup))
                    {
                        File.Copy(saveFile, saveBackup, false);
                    }
                }
            }
        }

        // [Localize]
        private static readonly Text fileCopyOodle9Error = new Text(
            "Error: Oodle not found\nCopy oo2core_9_win64.dll from your game directory into the randomizer directory",
            "MainForm_fileCopyOodle9Error");

        // Returns true if succeeded
        public static bool ModifyNightreignFiles(Messages messages, string exe, out string ret)
        {
            ret = null;
            if (!File.Exists("oo2core_9_win64.dll"))
            {
                string gameDir = Path.GetDirectoryName(exe);
                if (File.Exists($@"{gameDir}\oo2core_9_win64.dll"))
                {
                    File.Copy($@"{gameDir}\oo2core_9_win64.dll", "oo2core_9_win64.dll");
                }
                else
                {
                    ret = messages.Get(fileCopyOodle9Error);
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
            "Error: Nuxe output is selected, but the game is not unpacked (event subdirectory not found)",
            "EldenForm_fileUxmUnpackError");
        [Localize]
        private static readonly Text fileUxmPatchWarning = new Text(
            @"Warning: Nuxe output is selected, but the game is not patched (_backup\eldenring.exe not found)",
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

        public static bool CheckDS3Mods(Messages messages, string exe, out string ret)
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
            // This shouldn't check for everything, just a loose heuristic. Don't use Data0 since other mods can have it, and it is required for sfx extraction
            string gameDir = Path.GetDirectoryName(exe);
            if (!File.Exists($@"{gameDir}\Data1.bdt"))
            {
                ret = "Error: Invalid game exe (cannot find Data1.bdt in game directory)";
                return true;
            }
            return false;
        }

        // Deprecated
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
            string modEngineHash = Util.GetMD5FileHash(@"..\dinput8.dll");
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

        // Checking byte equality is faster, but this is simple to configure
        // It's fine to miss something, this is just for uninstall
        private static HashSet<string> ds1HeapPatch = new HashSet<string>
        {
            "2368e571244eb15d58af1f974528857a",
        };

        public static bool IsDS1HeapPatch(string path)
        {
            FileInfo info = new FileInfo(path);
            // ~100K bytes
            if (!info.Exists || info.Length > 500_000)
            {
                return false;
            }
            string hash = Util.GetMD5FileHash(path);
            return ds1HeapPatch.Contains(hash);
        }

        public static bool RemoveDS1HeapPatch(string path)
        {
            if (IsDS1HeapPatch(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }

        // Adapter for ME1-style locations of randomizer and UXM unpacking
        public static void CombineAI(List<string> maps, string outDir, bool mergeInfo)
        {
            CombineAI(maps, $@"{outDir}\..\script", mergeInfo, new(), (name, bnd) =>
            {
                string path = $@"{outDir}\script\{name}";
                bnd.Write(path);
            });
        }

        // TODO: See if this works for Sekiro. If not, move to DS3-specific directory
        public static void CombineAI(IEnumerable<string> maps, string readDir, bool mergeInfo, Dictionary<string, string> fileOverride, Action<string, BND4> write)
        {
            // Merges AI into common and removes scripts from other maps.
            // Also looks at config directory for custom overrides.
            string commonPath = $@"{readDir}\aicommon.luabnd.dcx";
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
                string aiPath = $@"{readDir}\{map}.luabnd.dcx";
                if (!File.Exists(aiPath)) continue;

                BND4 ai = BND4.Read(aiPath);
                ai.Files = ai.Files.Where(file =>
                {
                    if (!file.Name.Contains("out")) return true;
                    if (!usedFiles.Contains(file.Name))
                    {
                        aiCommon.Files.Add(file);
                        usedFiles.Add(file.Name);
                    }
                    return false;
                }).ToList();
                (LUAGNL gnl, LUAINFO info) = parseMetadata(ai);
                mergeMetadata(gnl, info, commonGnl, commonInfo);
                write($"{map}.luabnd.dcx", ai);
            }
            writeMetadata(aiCommon, commonGnl, commonInfo);
            int startId = 2000;
            foreach (BinderFile file in aiCommon.Files)
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                // Do this here because some AI scripts are only in common to begin with (DLC enemies)
                if (fileOverride.TryGetValue(name, out string contents))
                {
                    file.Bytes = Encoding.UTF8.GetBytes(contents);
                }
                if (file.ID < 3000) file.ID = startId++;
                // Console.WriteLine(file);
            }
            aiCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
            write("aicommon.luabnd.dcx", aiCommon);
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

        public static bool CheckSekiroSFX()
        {
            string customPath = @"sfx\sfxbnd_commoneffects.ffxbnd.dcx";
            if (!File.Exists(customPath)) return false;
            // 1.04 original size: 64,029,504. New size: 78,592,543
            // 1.06 original size: 64,319,424. New size: 79,142,507
            if (new FileInfo(customPath).Length < 75000000) return false;
            return true;
        }

        public static bool CombineSekiroSFX(List<string> maps, string outDir)
        {
            string inDir = new DirectoryInfo($@"{outDir}\..\sfx").FullName;
            string commonPath = $@"{inDir}\sfxbnd_commoneffects.ffxbnd.dcx";
            if (!File.Exists(commonPath)) return false;
            Console.WriteLine(new FileInfo(commonPath).FullName);
            BND4 sfxCommon = BND4.Read(commonPath);
            HashSet<string> sfxFiles = new HashSet<string>(sfxCommon.Files.Select(f => f.Name));
            Console.WriteLine(string.Join(",", maps));
            foreach (string map in maps.Select(m => m.Split('_')[0]).Distinct())
            {
                string path = $@"{inDir}\sfxbnd_{map}.ffxbnd.dcx";
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

        public static bool ProcessRandomaniaTextures(string modDir)
        {
            return File.Exists(Path.Combine(modDir, "MENU_Knowledge_02550.dds"));
        }

        public static bool ProcessedRandomaniaTextures(string modDir)
        {
            return File.Exists(Path.Combine(modDir, "menu/hi/00_solo.tpfbdt"));
        }

        public static void CopyRandomaniaTextures(string gameDir, string modDir)
        {
            // 1.5 GB disk space, 4 GB memory
            string iconName(int id) => $"MENU_Knowledge_{id:d5}";
            // string iconLowTexName(int id) => iconName(id) + ".dds";
            string iconHighTexName(int id) => iconName(id) + ".dds";
            // e.g. 00_Solo\MENU_Knowledge_06201.tpf.dcx
            string iconTpfName(int id) => @$"00_Solo\{iconName(id)}.tpf.dcx";
            string menuName = "SB_Title_01";
            string menuTexName = menuName + ".dds";
            string smallName = "SB_Icon_Randomania";
            string smallTexName = smallName + ".dds";
            string smallLayoutName = smallName + ".layout";
            string smallLayoutBndName = $@"N:\GR\data\Menu\ScaleForm\SBLayout\01_Common\Hi\{smallLayoutName}";
            List<int> iconIds = new() { 2550, 2551, 2552 };

            // Check base files. We've already checked one of them exists
            List<string> baseFiles = new[] { menuTexName, smallTexName, smallLayoutName }.Select(n => Path.Combine(modDir, n)).ToList();
            // baseFiles.AddRange(iconIds.Select(i => Path.Combine(modDir, iconLowTexName(i))));
            baseFiles.AddRange(iconIds.Select(i => Path.Combine(modDir, iconHighTexName(i))));
            foreach (string baseFile in baseFiles)
            {
                if (!File.Exists(baseFile)) throw new Exception($"Expected file {baseFile}");
            }

            // Check outfiles
            Directory.CreateDirectory($@"{modDir}\menu\hi");
            // Directory.CreateDirectory($@"{modDir}\menu\low");

            string iconBhdName(string type) => $"/menu/{type}/00_solo.tpfbhd";
            string iconBdtName(string type) => $"/menu/{type}/00_solo.tpfbdt";
            string menuTpfName = "/menu/hi/01_common.tpf.dcx";
            string menuLayoutName = "/menu/hi/01_common.sblytbnd.dcx";
            // iconBhdName("low"), iconBdtName("low")
            List<string> names = new() { iconBhdName("hi"), iconBdtName("hi"), menuTpfName, menuLayoutName };
            // Rely on the names starting with slash
            string outFile(string name) => $@"{modDir}{name}";

            // Load everything
            Dictionary<string, string> pathArchives = names.ToDictionary(p => p, _ => "Data0");
            Dictionary<string, byte[]> pathContents = BhdExtractor.ReadBdtFiles(GameSpec.FromGame.ER, gameDir, pathArchives);
            foreach (string name in names)
            {
                if (!pathContents.ContainsKey(name)) throw new Exception($"Could not find {name} in game archive");
            }

            // Replace menu texture
            // TODO: dds out of date? needs reextracting
            {
                string mainTexPath = Path.Combine(modDir, menuTexName);
                TPF commonTpf = TPF.Read(pathContents[menuTpfName]);
                TPF.Texture mainTex = commonTpf.Textures.Find(t => t.Name == menuName);
                if (mainTex == null) throw new Exception($"Could not find {menuName} in main game archive");
                mainTex.Bytes = File.ReadAllBytes(mainTexPath);
                // Add new icon sheet
                string smallTexPath = Path.Combine(modDir, smallTexName);
                TPF.Texture smallTex = new TPF.Texture() { Name = smallName, Format = 102, Bytes = File.ReadAllBytes(smallTexPath) };
                commonTpf.Textures.Add(smallTex);
                commonTpf.Write(outFile(menuTpfName));
                // Maybe free up space before BXF4 read
                pathContents.Remove(menuTpfName);
            }
            {
                string smallLayoutPath = Path.Combine(modDir, smallLayoutName);
                BND4 layoutBnd = BND4.Read(pathContents[menuLayoutName]);
                int maxFile = layoutBnd.Files.Max(f => f.ID);
                // Console.WriteLine(string.Join(" | ", layoutBnd.Files.Select(f => f.Name)));
                layoutBnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, ++maxFile, smallLayoutBndName, File.ReadAllBytes(smallLayoutPath)));
                layoutBnd.Write(outFile(menuLayoutName));
                pathContents.Remove(menuLayoutName);
            }

            {
                // Add the icon files
                string type = "hi";
                BXF4 iconBnd = BXF4.Read(pathContents[iconBhdName(type)], pathContents[iconBdtName(type)]);
                int maxFile = iconBnd.Files.Max(f => f.ID);
                foreach (int iconId in iconIds)
                {
                    string iconTexPath = Path.Combine(modDir, iconHighTexName(iconId));
                    byte[] iconBytes = File.ReadAllBytes(iconTexPath);
                    // Create TPF
                    TPF iconTpf = new TPF() { Compression = DCX.Type.DCX_KRAK };
                    TPF.Texture iconTpfTex = new TPF.Texture() { Name = iconName(iconId), Format = 102, Bytes = iconBytes };
                    iconTpf.Textures.Add(iconTpfTex);
                    // Add entry
                    string tpfName = iconTpfName(iconId);
                    iconBnd.Files.Add(new BinderFile(Binder.FileFlags.Flag1, ++maxFile, tpfName, iconTpf.Write()));
                }
                iconBnd.Write(outFile(iconBhdName(type)), outFile(iconBdtName(type)));
            }
        }

        // Simplified version
        public static void CopyTitleScreen(string gameDir, string modDir)
        {
            string menuName = "SB_Title_01";
            string menuTexName = menuName + ".dds";

            // Check base files. We've already checked one of them exists
            List<string> baseFiles = new[] { menuTexName }.Select(n => Path.Combine(modDir, n)).ToList();
            foreach (string baseFile in baseFiles)
            {
                if (!File.Exists(baseFile)) throw new Exception($"Expected file {baseFile}");
            }

            string menuTpfName = "/menu/hi/01_common.tpf.dcx";
            List<string> names = new() { menuTpfName };
            // Rely on the names starting with slash
            string outFile(string name) => $@"{modDir}{name}";

            // Load everything
            Dictionary<string, string> pathArchives = names.ToDictionary(p => p, _ => "Data0");
            Dictionary<string, byte[]> pathContents = BhdExtractor.ReadBdtFiles(GameSpec.FromGame.ER, gameDir, pathArchives);
            foreach (string name in names)
            {
                if (!pathContents.ContainsKey(name)) throw new Exception($"Could not find {name} in game archive");
            }

            // Replace menu texture
            {
                string mainTexPath = Path.Combine(modDir, menuTexName);
                TPF commonTpf = TPF.Read(pathContents[menuTpfName]);
                TPF.Texture mainTex = commonTpf.Textures.Find(t => t.Name == menuName);
                if (mainTex == null) throw new Exception($"Could not find {menuName} in main game archive");
                mainTex.Bytes = File.ReadAllBytes(mainTexPath);
                // Add new icon sheet
                commonTpf.Write(outFile(menuTpfName));
                pathContents.Remove(menuTpfName);
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
        public static readonly Dictionary<string, string> DS1Langs = new()
        {
            ["GERMAN"] = "de", // de-DE
            ["ENGLISH"] = "en", // en-US
            ["FRENCH"] = "fr", // fr-FR
            ["ITALIAN"] = "it", // it-IT
            ["JAPANESE"] = "ja", // ja-JP
            ["KOREAN"] = "ko", // ko-KR
            ["POLISH"] = "pl", // pl-PL
            ["PORTUGUESE"] = "pt-BR", // pt-BR
            ["RUSSIAN"] = "ru", // ru-RU
            ["NSPANISH"] = "es", // es-AR
            ["SPANISH"] = "es-ES", // es-ES
            ["SCHINESE"] = "zh", // zh-CN? TODO check
            ["TCHINESE"] = "zh", // zh-CN
        };

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
            @"script",  // Not included with pre-ER rando
            @"script\talk",
            @"sfx",  // Not included with pre-ER rando
            @"shader",
            @"sound",
            // Elden Ring has others, including with map/asset names, which can't be enumerated here. Reconsider this approach...
        }.SelectMany(t => t.Contains("$lang") ? Langs.Keys.Select(l => t.Replace("$lang", l)) : new[] { t }).ToList();
        private static List<string> extensions = new List<string>
        {
            ".hks", ".dcx", ".gfx", ".dds", ".fsb", ".fev", ".itl", ".tpf", ".entryfilelist", ".hkxbdt", ".hkxbhd", "Data0.bdt",
            // Elden Ring
            "regulation.bin", ".bnk", ".wem", "id.txt", ".tpfbdt", ".tpfbhd",
        };
        private static Regex extensionRe = new Regex(string.Join("|", extensions.Select(e => Regex.Escape(e) + "$")), RegexOptions.IgnoreCase);

        public static List<string> GetGameFiles(string dir, bool me1)
        {
            List<string> allFiles = new List<string>();
            foreach (string subdir in fileDirs)
            {
                if (me1 && (subdir == "script" || subdir == "sfx")) continue;
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

        public static void GetExcludedEldenFiles()
        {
            string gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game";
            HashSet<string> gameFiles = new(GetGameFiles(gameDir, false).Select(f => Path.Combine(gameDir, f)));
            DirectoryInfo dirInfo = new DirectoryInfo(gameDir);
            foreach (FileInfo fileInfo in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                if (!gameFiles.Contains(fileInfo.FullName))
                {
                    Console.WriteLine(fileInfo.FullName);
                }
            }
        }
    }
}
