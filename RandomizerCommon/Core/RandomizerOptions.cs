using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using RandomizerCommon.Properties;
using YamlDotNet.Serialization;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class RandomizerOptions
    {
        public FromGame Game { get; init; }

        public uint Seed { get; set; }
        public uint Seed2 { get; set; }

        private string _seedStr;
        public string SeedStr
        {
            get => _seedStr;
            set
            {
                if (value == null)
                {
                    // This does not interoperate well with non-str-based seeds
                    if (_seedStr != null)
                    {
                        Seed = 0;
                    }
                    _seedStr = null;
                    return;
                }
                if (!TryParseSeed(value, out uint seed, out string seedStr))
                {
                    throw new Exception($"Invalid string seed {value}");
                }
                Seed = seed;
                _seedStr = seedStr;
            }
        }
        public string Preset { get; set; }

        public int Version { get; }
        public int StoredVersion { get; private set; }

        private static readonly Regex SeedStrRe = new Regex(@"^[a-zA-Z0-9]+$");
        public static bool TryParseSeed(string str, out uint seed, out string seedStr)
        {
            if (uint.TryParse(str, out seed))
            {
                seedStr = seed.ToString();
                return true;
            }
            else if (str != null && SeedStrRe.IsMatch(str))
            {
                seedStr = str;
                seed = Util.XxStringHash(str);
                return true;
            }
            else
            {
                seedStr = null;
                return false;
            }
        }

        private SortedDictionary<string, bool> opt = new SortedDictionary<string, bool>();
        private SortedDictionary<string, string> str = new SortedDictionary<string, string>();
        private int difficulty;

        public RandomizerOptions Copy()
        {
            // Copies most things, except not the seed and preset (maybe can revisit this when revisiting DS3)
            return new RandomizerOptions(Game)
            {
                opt = new SortedDictionary<string, bool>(opt),
                str = new SortedDictionary<string, string>(str),
                difficulty = difficulty,
                Seed = Seed,
                Seed2 = Seed2,
                SeedStr = SeedStr,
                Preset = Preset,
            };
        }

        public static readonly int EldenRingVersion = 16;
        public static readonly int NightreignVersion = 2;
        public static readonly int DS3Version = 6;
        public static readonly int DS1Version = 2;

        private readonly bool ReadIndexArguments;
        private readonly bool WriteIndexArguments;
        private readonly bool UseSeedStr;
        private readonly bool UseDifficulty;

        public RandomizerOptions(FromGame game)
        {
            Game = game;
            ReadIndexArguments = false;
            WriteIndexArguments = false;
            UseSeedStr = false;
            UseDifficulty = true;
            // TODO: Less manual
            // Also this is broken, use implementation from Fog Gate Randomizer
            if (game == FromGame.SDT)
            {
                opt["v1"] = false;
                opt["v2"] = false;
                opt["v3"] = false;
                opt["v4"] = true;
                ReadIndexArguments = true;
                WriteIndexArguments = true;

            }
            else if (game == FromGame.DS3)
            {
                Version = DS3Version;
                // Previous versions used to get cleared out here, but this didn't actually clear it out. It just wasn't read back from options
                opt[$"v{Version}"] = true;
                ReadIndexArguments = true;
            }
            else if (game == FromGame.ER)
            {
                int version = EldenRingVersion;
                for (int i = 1; i < version; i++)
                {
                    opt[$"v{i}"] = false;
                }
                opt[$"v{version}"] = true;
            }
            else if (game == FromGame.DS1R)
            {
                Version = DS1Version;
                opt[$"v{Version}"] = true;
                UseDifficulty = false;
            }
            else if (game == FromGame.NR)
            {
                opt["v1"] = false;
                opt[$"v{NightreignVersion}"] = true;
                // TODO: Use this in other randomizers (support in Avalonia)
                UseSeedStr = true;
                UseDifficulty = false;
            }
        }

        public static RandomizerOptions Parse(IEnumerable<string> args, FromGame game, Predicate<string> optionsFilter = null)
        {
            RandomizerOptions options = new RandomizerOptions(game);
            uint seed = 0;
            uint seed2 = 0;
            int difficulty = 0;
            List<string> preset = new List<string>();
            string op = null;
            int numIndex = 0;
            foreach (string arg in args)
            {
                if (arg == "--preset")
                {
                    op = "preset";
                    continue;
                }
                else if (arg.StartsWith("--"))
                {
                    op = null;
                }
                if (op == "preset")
                {
                    preset.Add(arg);
                }
                else if (options.ReadIndexArguments && uint.TryParse(arg, out uint num))
                {
                    if (numIndex == 0)
                    {
                        difficulty = (int)num;
                    }
                    else if (numIndex == 1)
                    {
                        seed = num;
                    }
                    else if (numIndex == 2)
                    {
                        seed2 = num;
                    }
                    numIndex++;
                }
                else if (arg.Contains(':'))
                {
                    string[] parts = arg.Split(new[] { ':' }, 2);
                    // optionsFilter does not include key-value options
                    options.str[parts[0]] = parts[1];
                }
                else if (arg.StartsWith('v') && byte.TryParse(arg.AsSpan(1), out byte version))
                {
                    options.StoredVersion = Math.Max(version, options.StoredVersion);
                }
                else
                {
                    if (optionsFilter != null && !optionsFilter(arg)) continue;
                    options[arg] = true;
                }
            }
            options.Difficulty = difficulty;
            options.Seed = seed;
            options.Seed2 = seed2;
            // Console.WriteLine($"{string.Join(' ', args)} -> {string.Join(' ', options.str)}");
            if (options.str.TryGetValue("bias", out string valStr) && int.TryParse(valStr, out int val))
            {
                options.Difficulty = val;
                options.str.Remove("bias");
            }
            if (options.str.TryGetValue("seed", out valStr))
            {
                if (options.UseSeedStr)
                {
                    // This also sets Seed
                    options.SeedStr = valStr;
                    options.str.Remove("seed");
                }
                else if (uint.TryParse(valStr, out uint uval))
                {
                    options.Seed = uval;
                    options.str.Remove("seed");
                }
            }
            if (options.str.TryGetValue("seed2", out valStr) && uint.TryParse(valStr, out uint uval2))
            {
                options.Seed2 = uval2;
                options.str.Remove("seed2");
            }
            if (preset.Count > 0) options.Preset = string.Join(" ", preset);
            return options;
        }

        public bool this[string name]
        {
            get
            {
                if (name.StartsWith("invert"))
                {
                    name = "no" + name.Substring(6);
                    return !(opt.ContainsKey(name) ? opt[name] : false);
                }
                return opt.ContainsKey(name) ? opt[name] : false;
            }
            set
            {
                if (name.StartsWith("invert"))
                {
                    name = "no" + name.Substring(6);
                    opt[name] = !value;
                }
                else if (!name.Contains("default"))
                {
                    opt[name] = value;
                }
            }
        }

        public bool IsSwitchEnabled(string sw)
        {
            // For now, make this a utility here, although it is somewhat config-specific
            if (sw == "always" || sw == null) return true;
            return sw.Split(' ').Any(optName => optName.StartsWith('!') ? !this[optName.Substring(1)] : this[optName]);
        }

        // Prebaked int options with inclusive ranges
        public record IntOpt(string Name, int Min, int Max, int Def = 0)
        {
            // Elden Ring
            public static readonly IntOpt RunesEnd = new IntOpt("runes_end", 0, 7);
            public static readonly IntOpt RunesRold = new IntOpt("runes_rold", 0, 7);
            public static readonly IntOpt RunesLeyndell = new IntOpt("runes_leyndell", 0, 7, 2);
            public static readonly IntOpt PlayerLevel = new IntOpt("playerlevel", 0, 200);
            public static readonly IntOpt Messmershard = new IntOpt("messmershard", 1, 20, 1);
            public static readonly IntOpt Shardreq = new IntOpt("shardreq", 0, 20);
        };

        public bool GetInt(IntOpt intOpt, out int val)
        {
            return GetInt(intOpt.Name, intOpt.Min, intOpt.Max, out val);
        }

        public int GetIntOrDefault(IntOpt intOpt)
        {
            return GetInt(intOpt, out int val) ? val : intOpt.Def;
        }

        public bool GetNonDefaultInt(IntOpt intOpt, out int val)
        {
            val = GetIntOrDefault(intOpt);
            return val != intOpt.Def;
        }

        // TODO: Make these private, unless needed for something else
        public bool GetInt(string name, out int val)
        {
            val = 0;
            return str.TryGetValue(name, out string s) && int.TryParse(s, out val);
        }

        public bool GetInt(string name, int min, int max, out int val)
        {
            val = 0;
            return str.TryGetValue(name, out string s) && int.TryParse(s, out val) && val >= min && val <= max;
        }

        public void SetNonDefaultInt(IntOpt intOpt, int? maybeVal)
        {
            if (maybeVal is int val && (val == intOpt.Def || val < intOpt.Min || val > intOpt.Max))
            {
                maybeVal = null;
            }
            SetInt(intOpt.Name, maybeVal);
        }

        public void SetInt(IntOpt intOpt, int? maybeVal)
        {
            SetInt(intOpt.Name, maybeVal);
        }

        public void SetInt(string name, int? maybeVal)
        {
            if (maybeVal is int val)
            {
                str[name] = val.ToString();
            }
            else
            {
                str.Remove(name);
            }
        }

        public bool GetStr(string name, out string val)
        {
            return str.TryGetValue(name, out val);
        }

        // Use with caution and validation
        public void SetStr(string name, string val)
        {
            if (string.IsNullOrWhiteSpace(val))
            {
                str.Remove(name);
            }
            else
            {
                str[name] = val;
            }
        }

        // Probably move this out of RandomizerOptions since it's fairly logic-heavy
        public class BiasWeights
        {
            // 0 to 1, determines how desirable unfair locations are. Not used in Elden Ring.
            public float Unfair { get; set; }
            // 0 to 1, determines how desirable veryunfair locations are. Not used in Elden Ring.
            public float VeryUnfair { get; set; }
            // 0 to 15, determines exponent for max weight
            public float ItemSpread { get; set; }
            // 0 to 20, determines exponent for max weight for key items
            public float KeyItemSpread { get; set; }
            // 1 to 100, determines area multiplier for areas behind key items
            public float KeyItemChain { get; set; }
            // 1 to 100, determines area derank without chaining
            public float KeyItemRegionSpread { get; set; }

            // Used for UI heuristics
            [YamlIgnore]
            public float SpreadRatio => ItemSpread / 15;

            public static BiasWeights Calculate(int difficulty, FromGame game)
            {
                BiasWeights ret = new();
                float FromRange(int start, int end)
                {
                    if (difficulty < start) return 0;
                    if (difficulty >= end) return 1;
                    return 1f * (difficulty - start) / (end - start);
                }
                float bias;
                if (game == FromGame.ER)
                {
                    // So far, unfair is not used in ER
                    // Weights within area, used to be 30 to 100
                    ret.KeyItemSpread = 20 * FromRange(0, 100);
                    ret.ItemSpread = 15 * FromRange(0, 100);
                    // Still go from 1 to 100, but instead of immediately jumping up at 1%, have more gradual curve
                    // Keep 60% -> 10 the same, make it so 20% has less bias
                    // Before curve: 0% -> 1. 0-20% is 2-4, 20-60% is 4-10, 60-100% is 10-100. 50% is 8.5
                    // After curve: 0-20% is 1-2, 20-40% is 2-6, 40-60% is 6-10, then same. 50% is 8
                    if (difficulty < 10) bias = 1;
                    else if (difficulty < 30) bias = 1 + 0.5f * FromRange(10, 30);
                    else if (difficulty < 50) bias = 1.5f + 2.5f * FromRange(30, 50);
                    else if (difficulty < 70) bias = 4 + 6 * FromRange(50, 70);
                    else bias = 10 + 90 * FromRange(70, 100);
                    // 1.2 makes it 10% vs 20% for Liurnia vs Altus, 1.5 is 8% vs 23%, 2 is 3% vs 30%
                }
                else
                {
                    ret.Unfair = FromRange(40, 80);
                    ret.VeryUnfair = FromRange(70, 100);
                    ret.KeyItemSpread = 20 * FromRange(20, 60);
                    ret.ItemSpread = 15 * FromRange(0, 80);
                    // This one is a multiplicative weight, but important for distributing key items throughout the game.
                    if (difficulty == 0) bias = 1;
                    else if (difficulty < 20) bias = 2 + 2 * FromRange(0, 20);
                    else if (difficulty < 60) bias = 4 + 6 * FromRange(20, 60);
                    else bias = 10 + 90 * FromRange(60, 100);
                }
                ret.KeyItemChain = bias;
                ret.KeyItemRegionSpread = 1;
                return ret;
            }

            // Call to validate external values
            public void Clamp()
            {
                Unfair = Math.Clamp(Unfair, 0, 1);
                VeryUnfair = Math.Clamp(Unfair, 0, 1);
                KeyItemSpread = Math.Clamp(KeyItemSpread, 0, 20);
                ItemSpread = Math.Clamp(ItemSpread, 0, 15);
                KeyItemChain = Math.Clamp(KeyItemChain, 1, 100);
                KeyItemRegionSpread = Math.Clamp(KeyItemRegionSpread, 1, 100);
            }

            public BiasWeights Clone() => (BiasWeights)MemberwiseClone();
        }

        public BiasWeights GetWeights() => BiasWeights.Calculate(difficulty, Game);

        public int Difficulty
        {
            get { return difficulty; }
            set { difficulty = Math.Max(0, Math.Min(100, value)); }
        }

        public string GameNameForFile => Game.ToString();

        // Options which are purely aesthetic or related to installation
        private static HashSet<string> logiclessOptions = new HashSet<string>
        {
            "mergemods", "mergegame", "uxm", "loose", "hideseed", "bossbgm", "noenvbgm", "crashfix",
            // Nightreign, grouping or runtime only
            "utility", "patchseed", "runseed",
        };

        // Boolean options which apply (not mapped options)
        public SortedSet<string> GetLogicOptions()
        {
            return new SortedSet<string>(opt.Where(e => e.Value && !logiclessOptions.Contains(e.Key)).Select(e => e.Key));
        }

        public SortedSet<string> GetOptions()
        {
            return new SortedSet<string>(opt.Where(e => e.Value).Select(e => e.Key));
        }

        public string ConfigString(bool includeSeed = false, bool includePreset = false, bool onlyLogic = true)
        {
            SortedSet<string> words = onlyLogic ? GetLogicOptions() : GetOptions();
            words.UnionWith(str.Where(e => !(onlyLogic && logiclessOptions.Contains(e.Key))).Select(e => $"{e.Key}:{e.Value}"));
            string result = string.Join(" ", words);
            // Colon syntax should be safe to use for other games, but test it out first.
            // At some point, we could switch to using the str dictionary directly.
            if (UseDifficulty)
            {
                result += WriteIndexArguments ? $" {Difficulty}" : $" bias:{Difficulty}";
            }
            if (includeSeed)
            {
                if (UseSeedStr)
                {
                    // Only supports main seed for now, and doesn't propagate default
                    if (SeedStr != null)
                    {
                        result += $" seed:{SeedStr}";
                    }
                }
                else if (WriteIndexArguments)
                {
                    result += $" {Seed}";
                    if (Seed2 != 0 && Seed2 != Seed)
                    {
                        result += $" {Seed2}";
                    }
                }
                else
                {
                    result += $" seed:{Seed}";
                    if (Seed2 != 0 && Seed2 != Seed)
                    {
                        result += $" seed2:{Seed2}";
                    }
                }
            }
            if (!string.IsNullOrEmpty(Preset) && includePreset)
            {
                result += $" --preset {Preset}";
            }
            return result;
        }

        public string FullString() => ConfigString(includeSeed: true, includePreset: true, onlyLogic: false);
        public string LogicString() => ConfigString(includeSeed: true, includePreset: false, onlyLogic: true);
        public override string ToString() => ConfigString(includeSeed: true, includePreset: true, onlyLogic: false);
        public string ConfigHash() => (Util.JavaStringHash(ConfigString(includeSeed: false, includePreset: true, onlyLogic: true)) % 99999).ToString().PadLeft(5, '0');

        // Bad simple implementation for change checking
        public override bool Equals(object obj) => obj is RandomizerOptions o && Equals(o);
        public bool Equals(RandomizerOptions o) => o.ToString() == ToString();
        public override int GetHashCode() => ToString().GetHashCode();

        // -------------------------------------------------------------------------------------------------
        // Save utilities, since the properties are specific to RandomizerCommon
        public static void SaveOptions(RandomizerOptions opt)
        {
            Settings.Default.Options = opt.FullString();
            Settings.Default.Save();
        }

        public static string ReadOptions()
        {
            // Just return string directly, since setting options from string is a per-game operation
            return Settings.Default.Options;
        }

        // Also game exe to make it available outside of the package
        public static void SaveExe(string exe)
        {
            Settings.Default.Exe = exe;
            Settings.Default.Save();
        }

        public static string ReadExe()
        {
            return Settings.Default.Exe;
        }

        public static void SaveMod(string mod)
        {
            Settings.Default.Mod = mod;
            Settings.Default.Save();
        }

        public static string ReadMod()
        {
            return Settings.Default.Mod;
        }

        public static void SaveViewedTabs(string tabs)
        {
            Settings.Default.ViewedTabs = tabs;
            Settings.Default.Save();
        }

        public static string ReadViewedTabs()
        {
            return Settings.Default.ViewedTabs;
        }

        public static void SaveLocale(string locale)
        {
            Settings.Default.Locale = locale;
            Settings.Default.Save();
        }

        public static string ReadLocale()
        {
            return Settings.Default.Locale;
        }

        public static void SaveWindowPosition(Rectangle rect)
        {
            Settings.Default.WindowPosition = rect;
            Settings.Default.Save();
        }

        public static Rectangle GetWindowPosition()
        {
            return Settings.Default.WindowPosition;
        }

        public static void SaveExtraDlls(IEnumerable<string> extraDlls)
        {
            Settings.Default.ExtraDlls ??= new();
            Settings.Default.ExtraDlls.Clear();
            Settings.Default.ExtraDlls.AddRange(extraDlls.ToArray());
            Settings.Default.Save();
        }

        // May return null
        public static List<string> ReadExtraDlls()
        {
            StringCollection extraDllList = Settings.Default.ExtraDlls;
            return extraDllList?.Cast<string>().ToList();
        }
    }
}
