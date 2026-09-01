using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.Messages;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RandomizerCommon
{
    public class HelperOptions
    {
        public enum OptionType
        {
            // true or false
            Bool,
            // Regular int
            Int,
            // Formatted uint
            HexSeed,
            // File extension
            Extension,
        }
        public class Option
        {
            public string Name { get; set; }
            // Parent must come before it in the list
            public string Parent { get; set; }
            public OptionType Type { get; set; } = OptionType.Bool;
            // true for Bool, 0 for int. Not directly validated with min/max stuff
            public object Default { get; set; }
            public string Text { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
        }

        public static readonly List<Option> OptionList = new()
        {
            new() { Name = "autoEquip", Text = "Auto-equip" },
            new() { Name = "equipShop", Parent = "autoEquip", Text = "Auto-equip items purchased from shops" },
            new() { Name = "equipWeapons", Parent = "autoEquip", Text = "Auto-equip weapons" },
            new() { Name = "bowLeft", Parent = "equipWeapons", Default = false, Text = "Equip bows in the left hand" },
            new() { Name = "castLeft", Parent = "equipWeapons", Text = "Equip spell-casting weapons in the left hand" },
            new() { Name = "equipArmor", Parent = "autoEquip", Text = "Auto-equip armor" },
            new() { Name = "equipAccessory", Parent = "autoEquip", Text = "Auto-equip talismans" },
            new() { Name = "equipSpells", Parent = "autoEquip", Text = "Auto-equip spells" },
            new() { Name = "equipCrystalTears", Parent = "autoEquip", Text = "Auto-equip crystal tears" },
            new() { Name = "autoUpgrade", Text = "Auto-upgrade" },
            new() { Name = "autoUpgradeWeapons", Parent = "autoUpgrade", Text = "Auto-upgrade weapons to the player's max weapon level" },
            new() { Name = "regionLockWeapons", Parent = "autoUpgradeWeapons", Default = false, Text = "Limit max auto-upgrade level based on the region where the weapon is acquired" },
            new() { Name = "weaponLevelsBelowMax", Parent = "autoUpgradeWeapons", Type = OptionType.Int, Max = 25, Text = "Nerf auto-upgrade by reducing weapons' levels by a fixed amount. If set to 3, an auto-upgrade level of +20 (+8 somber) gives a +17 weapon (+7 somber)" },
            new() { Name = "weaponLevelRange", Parent = "autoUpgradeWeapons", Type = OptionType.Int, Max = 25, Text = "Nerf auto-upgrade by reducing weapons' levels by a random amount. If set to 3, an auto-upgrade level of +10 (+4 somber) gives a +8 +9 or +10 weapon (+3 or +4 somber)" },
            new() { Name = "autoUpgradeSpiritAshes", Parent = "autoUpgrade", Text = "Auto-upgrade spirit ashes to the highest level achieved so far" },
            new() { Name = "autoUpgradeEquipped", Parent = "autoUpgrade", Default = false, Text = "Auto-upgrade items in equipment slots to match the highest level achieved so far" },
            new() { Name = "autoUpgradeDropped", Parent = "autoUpgrade", Default = false, Text = "Auto-upgrade items which are left on the ground and picked up" },
        };
        public static readonly Dictionary<string, Option> Options = OptionList.ToDictionary(o => o.Name, o => o);

        public static readonly List<Option> NightreignOptionList = new()
        {
            new() { Name = "patchSeed", Text = "Patch seed" },
            new() { Name = "seed", Parent = "patchSeed", Text = "Expedition seed", Type = OptionType.HexSeed },
            new() { Name = "saveFileExtension", Text = "Alternate save file extension", Type = OptionType.Extension, Default = "co2" },
        };
        public static readonly Dictionary<string, Option> NightreignOptions = NightreignOptionList.ToDictionary(o => o.Name, o => o);

        [Localize]
        public static readonly Dictionary<string, Text> OptionNames = OptionList.ToDictionary(o => o.Name, o => new Text(o.Text, "HelperOptions_" + o.Name));

        public string Filename { get; }

        public HelperOptions(string Filename)
        {
            this.Filename = Filename;
        }

        public bool IsValid()
        {
            return File.Exists(Filename);
        }

        public bool GetBool(Option opt)
        {
            string val = Read(opt.Name, "settings");
            if (string.IsNullOrEmpty(val)) return GetDefaultBool(opt);
            return val == "true";
        }

        public int GetInt(Option opt)
        {
            string val = Read(opt.Name, "settings");
            if (!int.TryParse(val, out int num)) return GetDefaultInt(opt);
            if (opt.Min < opt.Max) num = Math.Clamp(num, opt.Min, opt.Max);
            return num;
        }

        public uint GetHexSeed(Option opt)
        {
            string val = Read(opt.Name, "settings");
            if (string.IsNullOrEmpty(val)) return GetDefaultUint(opt);
            return ParseHexSeed(val, out uint seed) ? seed : 0;
        }

        // Can return invalid extension if no default
        public string GetExtension(Option opt)
        {
            string val = Read(opt.Name, "settings");
            return IsValidExtension(val) ? val : GetDefaultString(opt);
        }

        public bool GetDefaultBool(Option opt)
        {
            return opt.Default is bool v ? v : true;
        }

        public int GetDefaultInt(Option opt)
        {
            return opt.Default is int v ? v : 0;
        }

        public uint GetDefaultUint(Option opt)
        {
            return opt.Default is uint v ? v : 0;
        }

        public string GetDefaultString(Option opt)
        {
            return opt.Default is string v ? v : "";
        }

        // Does not do much validation
        public void Set(Option opt, object value)
        {
            string str;
            if (opt.Type == OptionType.HexSeed && value is uint uval)
            {
                str = FormatHexSeed(uval);
            }
            else
            {
                // bool ToString() is uppercase??
                str = value.ToString().ToLowerInvariant();
            }
            Write(opt.Name, str, "settings");
        }

        // TODO: Does not work with hex seeds, if that's needed
        public static void ImportSettings(string targetPath, string settings)
        {
            // This should probably use a proper parser. But for now, Windows API
            string tempFile = $"{Path.GetTempPath()}.{Guid.NewGuid()}.RandomizerHelper_config.ini";
            File.WriteAllText(tempFile, settings);
            HelperOptions exportOpt = new HelperOptions(tempFile);
            HelperOptions importOpt = new HelperOptions(targetPath);
            foreach (Option opt in OptionList)
            {
                if (opt.Type == OptionType.Bool)
                {
                    importOpt.Set(opt, exportOpt.GetBool(opt));
                }
                else
                {
                    importOpt.Set(opt, exportOpt.GetInt(opt));
                }
            }
        }

        // Extension of MiscSetup stuff via https://stackoverflow.com/questions/217902/reading-writing-an-ini-file
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        private string Read(string Key, string Section)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section, Key, "", RetVal, 255, Filename);
            return RetVal.ToString();
        }

        private void Write(string Key, string Value, string Section)
        {
            WritePrivateProfileString(Section, Key, " " + Value, Filename);
        }

        // These are public utilities for input validation
        // Public utilities
        public static bool ParseHexSeed(string str, out uint seed)
        {
            seed = 0;
            if (str.StartsWith("0x"))
            {
                try
                {
                    seed = Convert.ToUInt32(str, 16);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else if (uint.TryParse(str, out seed))
            {
                return true;
            }
            return false;
        }

        public static string FormatHexSeed(uint seed) => $"0x{seed:X8}";

        private static readonly Regex AlphaNumRe = new Regex(@"^[a-zA-Z0-9]+$");
        public static bool IsValidExtension(string str)
        {
            return str.Length > 0 && str.Length <= 120
                && AlphaNumRe.IsMatch(str)
                && !str.Equals("sl2", StringComparison.OrdinalIgnoreCase);
        }
    }
}
