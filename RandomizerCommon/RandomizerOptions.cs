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
            // Copies most things, except not the seed (maybe can revisit this when revisiting DS3)
            return new RandomizerOptions(Sekiro)
            {
                opt = new SortedDictionary<string, bool>(opt),
                num = new Dictionary<string, float>(num),
                difficulty = difficulty,
            };
        }

        public RandomizerOptions(bool Sekiro)
        {
            this.Sekiro = Sekiro;
        }

        public static RandomizerOptions Parse(IEnumerable<string> args, bool Sekiro)
        {
            RandomizerOptions options = new RandomizerOptions(Sekiro);
            uint seed = 0;
            int difficulty = -1;
            foreach (string arg in args)
            {
                if (uint.TryParse(arg, out uint num))
                {
                    if (difficulty == -1) difficulty = (int)num;
                    else seed = num;
                }
                else
                {
                    options[arg] = true;
                }
            }
            options.Difficulty = difficulty;
            options.Seed = seed;
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

        public float GetNum(string name)
        {
            return num[name];
        }

        public HashSet<string> GetEnabled()
        {
            return new HashSet<string>(opt.Where(e => e.Value).Select(e => e.Key));
        }

        public override string ToString() => $"{string.Join(" ", GetEnabled())} {Difficulty} {Seed}";
        public string ConfigHash() => (JavaStringHash($"{string.Join(" ", GetEnabled())} {Difficulty}") % 99999).ToString().PadLeft(5, '0');

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
