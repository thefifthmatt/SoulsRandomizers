using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomizerCommon
{
    public class RandomizerOptions
    {
        private SortedDictionary<string, bool> opt = new SortedDictionary<string, bool>();
        private Dictionary<string, float> num = new Dictionary<string, float>();
        private int difficulty;

        public RandomizerOptions Copy()
        {
            // Copies most things, except not the seed and preset (maybe can revisit this when revisiting DS3)
            return new RandomizerOptions(Sekiro)
            {
                opt = new SortedDictionary<string, bool>(opt),
                num = new Dictionary<string, float>(num),
                difficulty = difficulty,
                Seed = Seed,
                Seed2 = Seed2,
                Preset = Preset,
            };
        }

        public RandomizerOptions(bool Sekiro)
        {
            this.Sekiro = Sekiro;
            if (Sekiro)
            {
                opt["v1"] = false;
                opt["v2"] = false;
                opt["v3"] = false;
                opt["v4"] = true;
            }
            else
            {
                opt["v2"] = false;
                opt["v3"] = false;
                opt["v4"] = true;
            }
        }

        public static RandomizerOptions Parse(IEnumerable<string> args, bool Sekiro, Predicate<string> optionsFilter = null)
        {
            RandomizerOptions options = new RandomizerOptions(Sekiro);
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
                else if (uint.TryParse(arg, out uint num))
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
                else
                {
                    if (optionsFilter != null && !optionsFilter(arg)) continue;
                    options[arg] = true;
                }
            }
            options.Difficulty = difficulty;
            options.Seed = seed;
            options.Seed2 = seed2;
            if (preset.Count > 0) options.Preset = string.Join(" ", preset);
            return options;
        }

        public bool this[string name]
        {
            get
            {
                return opt.ContainsKey(name) ? opt[name] : false;
            }
            set
            {
                if (!name.StartsWith("default"))
                {
                    opt[name] = value;
                }
            }
        }
        public int Difficulty
        {
            get { return difficulty; }
            set {
                difficulty = Math.Max(0, Math.Min(100, value));
                // Linear scaling for these params, from 0 to 1. But severity may depend on game
                if (Sekiro)
                {
                    num["unfairweight"] = FromRange(40, 80);
                    num["veryunfairweight"] = FromRange(70, 100);
                    num["keyitemdifficulty"] = FromRange(20, 60);
                    num["allitemdifficulty"] = FromRange(0, 80);
                }
                else
                {
                    num["unfairweight"] = FromRange(40, 80);
                    num["veryunfairweight"] = FromRange(70, 100);
                    num["keyitemdifficulty"] = FromRange(20, 60);
                    num["allitemdifficulty"] = FromRange(0, 80);
                }
                // This one is a multiplicative weight, but important for distributing key items throughout the game.
                float key;
                if (difficulty == 0) key = 1;
                else if (difficulty < 20) key = 2 + 2 * FromRange(0, 20);
                else if (difficulty < 60) key = 4 + 6 * FromRange(20, 60);
                else key = 10 + 90 * FromRange(60, 100);
                num["keyitemchainweight"] = key;
            }
        }

        private float FromRange(int start, int end)
        {
            if (difficulty < start) return 0;
            if (difficulty >= end) return 1;
            return 1f * (difficulty - start) / (end - start);
        }

        public bool Sekiro { get; set; }
        public uint Seed { get; set; }
        public uint Seed2 { get; set; }
        public string Preset { get; set; }

        public float GetNum(string name)
        {
            return num[name];
        }

        private static HashSet<string> logiclessOptions = new HashSet<string> { "mergemods" };
        public SortedSet<string> GetLogicOptions()
        {
            return new SortedSet<string>(opt.Where(e => e.Value && !logiclessOptions.Contains(e.Key)).Select(e => e.Key));
        }
        public SortedSet<string> GetOptions()
        {
            return new SortedSet<string>(opt.Where(e => e.Value).Select(e => e.Key));
        }

        public string ConfigString(bool includeSeed = false, bool includePreset = false, bool onlyLogic = true) =>
            $"{string.Join(" ", onlyLogic ? GetLogicOptions() : GetOptions())} {Difficulty}" +
            $"{(includeSeed ? $" {Seed}" : "")}{(includeSeed && Seed2 != 0 && Seed2 != Seed ? $" {Seed2}" : "")}" +
            $"{(!string.IsNullOrEmpty(Preset) && includePreset ? $" --preset {Preset}" : "")}";
        public string FullString() => ConfigString(includeSeed: true, includePreset: true, onlyLogic: false);
        public override string ToString() => ConfigString(includeSeed: true, includePreset: true, onlyLogic: false);
        public string ConfigHash() => (JavaStringHash(ConfigString(includeSeed: false, includePreset: true, onlyLogic: true)) % 99999).ToString().PadLeft(5, '0');

        private static uint JavaStringHash(string s)
        {
            unchecked
            {
                uint hash = 0;
                foreach (char c in s)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
    }
}
