using SoulsIds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using static RandomizerCommon.LocationData;

namespace RandomizerCommon
{
    public class MergeModManifest
    {
        public readonly NumChecker NorandomEntities = new(nameof(NorandomEntities));
        public readonly NumChecker NorandomLots = new(nameof(NorandomLots));
        public readonly NumChecker NorandomEnemyLots = new(nameof(NorandomEnemyLots));
        public readonly NumChecker NorandomLotFlags = new(nameof(NorandomLotFlags));
        public readonly NumChecker NorandomShops = new(nameof(NorandomShops));
        public readonly HashSet<ItemKey> NorandomItems = new();

        // All uses of this should also check other config fields, but none others are used currently
        private IEnumerable<NumChecker> Checkers => new[] { NorandomEntities, NorandomLots, NorandomEnemyLots, NorandomLotFlags, NorandomShops };

        public bool Any() => Checkers.Any(c => c.Any());

        public List<string> GetDescription()
        {
            List<string> ret = new List<string>();
            foreach (NumChecker c in Checkers)
            {
                if (c.Any()) ret.Add(c.ToString());
            }
            return ret;
        }

        // Returned merged merge manifest. This returns null if there are no merged mods, no configs, or no data in the configs.
        public static MergeModManifest FromMods(GameData game, MergedMods mods)
        {
            if (mods.Count == 0) return null;
            MergeModManifest manifest = new();
            // We could IgnoreUnmatched, but generally the preference would be for everyone to be on the latest version of both mods
            IDeserializer deserializer = new DeserializerBuilder().Build();
            foreach (string dir in mods.Dirs)
            {
                string path = Path.Combine(dir, "randomizer_merge_config.yml");
                if (!File.Exists(path)) continue;
                ManifestFile file;
                using (var reader = File.OpenText(path))
                {
                    try
                    {
                        file = deserializer.Deserialize<ManifestFile>(reader);
                    }
                    catch (YamlException ex)
                    {
                        throw new Exception($"Error parsing merged mod config at {path}\nEither clear the merged mod, fix its config, or delete its config (but this may cause further issues during randomization).\n{ex} {ex.InnerException}");
                    }
                }
                if (file == null) continue;
                manifest.NorandomEntities.AddEntries(path, file.NorandomEntities);
                manifest.NorandomLots.AddEntries(path, file.NorandomLots);
                manifest.NorandomEnemyLots.AddEntries(path, file.NorandomEnemyLots);
                manifest.NorandomLotFlags.AddEntries(path, file.NorandomLotFlags);
                manifest.NorandomShops.AddEntries(path, file.NorandomShops);
                if (file.NorandomItems != null)
                {
                    // Avoid using this for now.
                    foreach (string item in file.NorandomItems)
                    {
                        if (game.RevItemNames.TryGetValue(item, out var keys))
                        {
                            if (keys.Count != 1)
                            {
                                throw new Exception(
                                    $"Error in field {nameof(NorandomItems)} in {file}: name '{item}' is ambiguous "
                                    + $"between {string.Join(" and ", keys.Select(ItemName))}. Use those names in the config instead.");
                            }
                            manifest.NorandomItems.UnionWith(keys);
                        }
                        else
                        {
                            string[] parts = item.Split(':');
                            if (parts.Length == 2 && nameTypes.TryGetValue(parts[0], out ItemType type) && int.TryParse(parts[1], out int id))
                            {
                                ItemKey key = new ItemKey(type, id);
                                manifest.NorandomItems.Add(key);
                            }
                            else throw new Exception($"Error in field {nameof(NorandomItems)} in {file}: name '{item}' is not a vanilla item name or an item id.");
                        }
                    }
                }
            }
            if (!manifest.Any()) return null;
            return manifest;
        }

        private static string ItemName(ItemKey key)
        {
            if (!typeNames.TryGetValue(key.Type, out string typeName)) typeName = "unknown";
            return $"{typeName}:{key.ID}";
        }
        private static readonly Dictionary<string, ItemType> nameTypes = new()
        {
            ["weapon"] = ItemType.Weapon,
            ["protector"] = ItemType.Protector,
            ["accessory"] = ItemType.Accessory,
            ["goods"] = ItemType.Goods,
            ["gem"] = ItemType.Gem,
            ["custom"] = ItemType.Custom,
        };
        private static readonly Dictionary<ItemType, string> typeNames = nameTypes.ToDictionary(e => e.Value, e => e.Key);

        // Keep storage format separate for simplicity
        public class ManifestFile
        {
            public List<string> NorandomEntities { get; set; }
            public List<string> NorandomLots { get; set; }
            public List<string> NorandomEnemyLots { get; set; }
            public List<string> NorandomLotFlags { get; set; }
            public List<string> NorandomShops { get; set; }
            public List<string> NorandomItems { get; set; }
        }

        private static readonly Regex rangeRegex = new Regex(@"\s*-\s*");
        public class NumChecker
        {
            public string Name { get; }

            public NumChecker(string Name)
            {
                this.Name = Name;
            }

            // All of these may be null
            private HashSet<int> Values { get; set; }
            private Dictionary<int, List<(int, int)>> ThousandRanges { get; set; }
            private List<(int, int)> BigRanges { get; set; }

            public override string ToString()
            {
                List<(int, int)> allRanges = new();
                if (Values != null) allRanges.AddRange(Values.Select(v => (v, v)));
                if (ThousandRanges != null) allRanges.AddRange(ThousandRanges.SelectMany(e => e.Value));
                if (BigRanges != null) allRanges.AddRange(BigRanges);
                if (allRanges.Count == 0) return $"{Name}: No ids";
                allRanges.Sort();
                return $"{Name}: {string.Join(", ", allRanges.Select(r => r.Item1 == r.Item2 ? $"{r.Item1}" : $"{r.Item1}-{r.Item2}"))}";
            }

            public bool Any() => Values != null || ThousandRanges != null || BigRanges != null;

            public bool GetAllValues(out IEnumerable<int> values)
            {
                if (ThousandRanges == null && BigRanges == null)
                {
                    values = Values;
                    return true;
                }
                else
                {
                    values = null;
                    return false;
                }
            }

            public bool Includes(int val)
            {
                if (Values != null && Values.Contains(val))
                {
                    return true;
                }
                if (ThousandRanges != null && ThousandRanges.TryGetValue(val / 1000, out List<(int, int)> ranges)
                    && ranges.Any(range => val >= range.Item1 && val <= range.Item2))
                {
                    return true;
                }
                if (BigRanges != null && BigRanges.Any(range => val >= range.Item1 && val <= range.Item2))
                {
                    return true;
                }
                return false;
            }

            public void AddEntries(string path, List<string> entries)
            {
                if (entries == null || entries.Count == 0) return;
                foreach (string entry in entries)
                {
                    if (int.TryParse(entry, out int val) && val >= 0)
                    {
                        Values ??= new();
                        Values.Add(val);
                    }
                    else
                    {
                        string[] parts = rangeRegex.Split(entry);
                        if (parts.Length == 2 && int.TryParse(parts[0], out val) && int.TryParse(parts[1], out int val2) && val2 >= val)
                        {
                            if (val / 1000 == val2 / 1000)
                            {
                                ThousandRanges ??= new();
                                Util.AddMulti(ThousandRanges, val / 1000, (val, val2));
                            }
                            else
                            {
                                BigRanges ??= new();
                                BigRanges.Add((val, val2));
                            }
                        }
                        else throw new Exception($"Error parsing field {Name} in {path}: entry '{entry}' is not a number or an inclusive range of two numbers");
                    }
                }
            }
        }
    }
}
