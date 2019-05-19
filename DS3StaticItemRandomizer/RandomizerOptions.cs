using System;
using System.Collections.Generic;
using System.Linq;

namespace DS3StaticItemRandomizer
{
    public class RandomizerOptions
    {
        private SortedDictionary<string, bool> opt = new SortedDictionary<string, bool>();
        private Dictionary<string, float> num = new Dictionary<string, float>();
        private int difficulty;

        public RandomizerOptions Copy()
        {
            return new RandomizerOptions
            {
                opt = new SortedDictionary<string, bool>(opt),
                num = new Dictionary<string, float>(num),
                difficulty = difficulty
            };
        }

        public bool this[string name]
        {
            get { return opt.ContainsKey(name) ? opt[name] : false; }
            set { opt[name] = value; }
        }
        public int Difficulty
        {
            get { return difficulty; }
            set {
                difficulty = Math.Max(0, Math.Min(100, value));
                // Linear scaling for these params, from 0 to 1
                num["unfairweight"] = FromRange(40, 80);
                num["veryunfairweight"] = FromRange(70, 100);
                num["keyitemdifficulty"] = FromRange(20, 60);
                num["allitemdifficulty"] = FromRange(0, 80);
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
