using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class Preset
    {
        // YAML fields
        // Taken from file name
        [YamlIgnore]
        public string Name { get; set; }
        public string DisplayName => Name == "Oops All" ? $"{Name} {OopsAll}" : Name;
        // To show in UI
        public string Description { get; set; }
        // Whether to change default options
        public bool RecommendFullRandomization { get; set; }
        public bool RecommendNoEnemyProgression { get; set; }
        // An enemy or enemy type to use for all possible enemies
        public string OopsAll { get; set; }
        [YamlIgnore]
        public List<int> OopsAllIDs = new List<int>();
        // Individual pool specifications
        public List<PoolAssignment> Boss { get; set; }
        public List<PoolAssignment> Miniboss { get; set; }
        public List<PoolAssignment> Basic { get; set; }
        public List<PoolAssignment> Add { get; set; }
        public List<PoolAssignment> FoldingMonkey { get; set; }
        // Whether to increase health and deathblow count of enemies as bosses. Default is true.
        public bool BuffBasicEnemiesAsBosses = true;
        // Enemies to not randomize
        public string DontRandomize { get; set; }
        [YamlIgnore]
        public HashSet<int> DontRandomizeIDs = new HashSet<int>();
        // Enemies to exclude from all pools
        public string RemoveSource { get; set; }
        [YamlIgnore]
        public HashSet<int> RemoveSourceIDs = new HashSet<int>();
        // Enemies to remove as sources from all pools
        // Specific enemies in specific spots
        // There are some special values here: 'norandom' gets added to norandom ids, and 'remove_source' is treated specially in enemy config processing
        public Dictionary<string, string> Enemies { get; set; }
        [YamlIgnore]
        public Dictionary<int, List<int>> EnemyIDs = new Dictionary<int, List<int>>();
        // Mapping of item locations. Currently only supports key items and overall logical areas/events.
        public Dictionary<string, string> Items { get; set; }

        public class PoolAssignment
        {
            // Points to use for this pool category, out of other pool categories
            public int Weight { get; set; }
            // Pool specification
            public string Pool { get; set; }
            // Whether to select by group
            public bool RandomByType { get; set; }
            // Enemies per pool, filtered by other constraints as well
            [YamlIgnore]
            public List<List<int>> PoolGroups = new List<List<int>>();
            // The total enemy count, for random selection purposes. Is this needed?
            [YamlIgnore]
            public int Count { get; set; }

            public PoolAssignment Copy()
            {
                PoolAssignment copy = (PoolAssignment)MemberwiseClone();
                copy.PoolGroups = copy.PoolGroups.Select(g => g.ToList()).ToList();
                return copy;
            }
        }

        public static List<string> GetPresetNames()
        {
            List<string> ret = new List<string>();
            if (Directory.Exists("presets"))
            {
                ret = Directory.GetFiles("presets", "*.txt").Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
                ret.Remove("README");
                ret.Remove("Template");
            }
            return ret;
        }

        // Just load it at the start, but don't do any validation yet.
        // This method may throw exceptions which the caller should probably catch.
        public static Preset LoadPreset(string name, bool extractOopsAll = false, string filename = null)
        {
            string loadName = name;
            if (extractOopsAll && name.StartsWith("Oops All "))
            {
                loadName = "Oops All";
            }
            string path = filename ?? $@"presets\{loadName}.txt";

            Preset preset;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = File.OpenText(path))
            {
                preset = deserializer.Deserialize<Preset>(reader);
            }
            preset.Name = name;
            if (name != loadName)
            {
                preset.OopsAll = name.Substring(9);
            }
            return preset;
        }

        public void ProcessEnemyPreset(GameData game, Dictionary<int, EnemyInfo> infos, List<EnemyCategory> cats, Dictionary<int, EnemyData> defaultData)
        {
            // Process enemy names
            HashSet<string> eligibleNames = new HashSet<string>();
            foreach (EnemyCategory cat in cats)
            {
                eligibleNames.Add(cat.Name);
                if (cat.Instance != null) eligibleNames.UnionWith(cat.Instance);
                if (cat.Partition != null) eligibleNames.UnionWith(cat.Partition);
                if (cat.Partial != null) eligibleNames.UnionWith(cat.Partial);
            }
            Dictionary<int, string> primaryName = new Dictionary<int, string>();
            Dictionary<string, List<int>> enemiesForName = new Dictionary<string, List<int>>();
            bool debugNames = false;
            // Guardian Ape is both a boss and a helper, so try to avoid the helper ape getting pulled into the category
            HashSet<string> bossNames = new HashSet<string>(infos.Values.Where(i => i.Class == EnemyClass.Boss && i.ExtraName != null).Select(i => i.ExtraName));
            foreach (EnemyInfo info in infos.Values)
            {
                // Do not let some enemies be randomized at this point, many will prevent the game from being completeable.
                if (info.Class == EnemyClass.None) continue;
                List<string> names = new List<string>();
                // Add all names. The first name added will be the primary name.
                if (info.ExtraName != null)
                {
                    names.Add(info.ExtraName);
                }
                if (defaultData.TryGetValue(info.ID, out EnemyData data))
                {
                    string model = game.ModelName(data.Model);
                    if (info.Class != EnemyClass.Boss && info.Category != null)
                    {
                        foreach (string cat in Regex.Split(info.Category, @"\s*;\s*"))
                        {
                            names.Add($"{cat} {model}");
                        }
                    }
                    if (info.Class == EnemyClass.Boss ? info.ExtraName == null : !bossNames.Contains(model))
                    {
                        names.Add(model);
                    }
                    if (info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.Basic)
                    {
                        names.Add($"{info.Class} {model}");
                    }
                }
                names.RemoveAll(n =>
                {
                    if (!eligibleNames.Contains(n))
                    {
                        if (debugNames) Console.WriteLine($"Name removed for {info.ID}: [{n}]");
                        return true;
                    }
                    return false;
                });
                if (names.Count > 0)
                {
                    primaryName[info.ID] = names[0];
                }
                names = names.SelectMany(n => new[] { n, $"{n} {info.ID}" }).ToList();
                names.Add(info.ID.ToString());
                if (info.Class == EnemyClass.Boss || info.Class == EnemyClass.Miniboss || info.Class == EnemyClass.Basic)
                {
                    names.Add($"{info.Class}");
                }
                if (info.Class != EnemyClass.Helper)
                {
                    // This is mainly used for "Oops All Any" so it should not include unkillable helpers
                    // like Immortal Centipede or Corrupted Monk Illusion.
                    names.Add($"Any");
                }
                if (debugNames) Console.WriteLine($"-- Names: {string.Join("; ", names)}");
                foreach (string name in names)
                {
                    AddMulti(enemiesForName, name, info.ID);
                }
            }
            bool generateEnemyList = false;
            if (generateEnemyList)
            {
                foreach (EnemyClass c in new[] { EnemyClass.Boss, EnemyClass.TutorialBoss, EnemyClass.Miniboss, EnemyClass.FoldingMonkey, EnemyClass.Basic })
                {
                    string map = null;
                    foreach (EnemyInfo info in infos.Values)
                    {
                        if (info.Class == c && primaryName.TryGetValue(info.ID, out string name))
                        {
                            string enemyMap = game.LocationNames[game.Locations[defaultData[info.ID].Map]];
                            if (map != enemyMap)
                            {
                                map = enemyMap;
                                Console.WriteLine($"  # {map}");
                            }
                            Console.WriteLine($"  {name} {info.ID}: any");
                        }
                    }
                    Console.WriteLine();
                }
            }
            foreach (EnemyCategory cat in cats)
            {
                if (cat.Contains == null) continue;
                List<int> combinedIds = new List<int>();
                foreach (string sub in cat.Contains)
                {
                    if (enemiesForName.TryGetValue(sub, out List<int> specialIds))
                    {
                        combinedIds.AddRange(specialIds);
                    }
                }
                if (combinedIds.Count > 0)
                {
                    enemiesForName[cat.Name] = combinedIds;
                }
            }

            // Process the config with these names
            List<string> errors = new List<string>();

            List<int> getIds(string name)
            {
                if (!enemiesForName.TryGetValue(name, out List<int> ids))
                {
                    string findId = "";
                    if (int.TryParse(name.Split(' ').Last(), out int id))
                    {
                        if (primaryName.TryGetValue(id, out string name2))
                        {
                            findId = $". Did you mean {name2} {id}?";
                        }
                        else
                        {
                            List<string> alts = enemiesForName.Select(e => e.Key).Where(e => e.EndsWith(id.ToString())).ToList();
                            if (alts.Count > 0)
                            {
                                findId = $". Did you mean {string.Join(", ", alts)}?";
                            }
                        }
                    }
                    errors.Add($"Unrecognized enemy name \"{name}\"{findId}");
                    return new List<int>();
                }
                return ids.ToList();
            }
            List<List<int>> getMultiIds(string name)
            {
                List<List<int>> ids = new List<List<int>>();
                foreach (string n in Regex.Split(name, @"\s*;\s*").ToList())
                {
                    ids.Add(getIds(n));
                }
                return ids;
            }

            // Fill in non-randomized ids. The individual enemy config can also add to this.
            if (DontRandomize != null && DontRandomize.ToLowerInvariant() != "none")
            {
                DontRandomizeIDs.UnionWith(getMultiIds(DontRandomize).SelectMany(i => i));
            }
            if (RemoveSource != null && RemoveSource.ToLowerInvariant() != "none")
            {
                RemoveSourceIDs.UnionWith(getMultiIds(RemoveSource).SelectMany(i => i));
            }

            // Process the specific enemy map config
            bool debug = false;
            if (Enemies != null)
            {
                foreach (KeyValuePair<string, string> entry in Enemies)
                {
                    // For now, validate the config before checking if we can continue. This could be relaxed in the future, or in release builds.
                    List<int> targets = getIds(entry.Key);
                    if (targets.Count > 1 && debug) Console.WriteLine($"Note: Enemy assigment {entry.Key}: {entry.Value} produced {targets.Count} targets");
                    if (entry.Value.ToLowerInvariant() == "any")
                    {
                        continue;
                    }
                    else if (entry.Value.ToLowerInvariant() == "norandom")
                    {
                        DontRandomizeIDs.UnionWith(targets);
                        continue;
                    }
                    List<int> sources = getIds(entry.Value);
                    if (sources.Count > 0)
                    {
                        // Allow the primary key to not be a unique enemy. This may produce some weird results.
                        foreach (int target in targets)
                        {
                            AddMulti(EnemyIDs, target, sources);
                        }
                    }
                }
            }

            bool poolFilter(int id)
            {
                return !DontRandomizeIDs.Contains(id) && !RemoveSourceIDs.Contains(id);
            }

            // If oops all mode, fill in oops all ids. And copy them to pools.
            if (OopsAll != null && OopsAll.ToLowerInvariant() != "none")
            {
                OopsAllIDs.AddRange(getMultiIds(OopsAll).SelectMany(i => i).Where(poolFilter).Distinct());
                if (debug) Console.WriteLine($"Oops All: {string.Join("; ", OopsAllIDs.Select(i => primaryName.TryGetValue(i, out string n) ? n : i.ToString()))}");
            }

            // Pool filtering
            int filterMulti(List<List<int>> groups, Predicate<int> filter)
            {
                int removed = 0;
                int groupRemoved = groups.RemoveAll(group =>
                {
                    removed += group.RemoveAll(i => !filter(i));
                    return group.Count == 0;
                });
                removed += groupRemoved;
                return removed;
            }

            // For all enemy groups, fill in their ids
            void processPool(PoolAssignment pool, string type)
            {
                if (pool.Weight < 0)
                {
                    pool.Weight = 0;
                    errors.Add($"Pool for {type} \"{pool.Pool}\" must specify a positive Weight");
                }
                if (pool.Pool == null)
                {
                    errors.Add($"Pool for {type} must include a Pool specification");
                    pool.Weight = 0;
                    return;
                }
                if (pool.Pool.ToLowerInvariant() == "default")
                {
                    return;
                }
                pool.PoolGroups = getMultiIds(pool.Pool);
                filterMulti(pool.PoolGroups, poolFilter);
                if (pool.PoolGroups.Count == 0) pool.Weight = 0;
            }
            List<PoolAssignment> processPools(List<PoolAssignment> pools, string type)
            {
                if (pools == null || pools.Count == 1 && pools[0].Pool.ToLowerInvariant() == "default")
                {
                    if (OopsAllIDs.Count > 0)
                    {
                        return new List<PoolAssignment>
                        {
                            new PoolAssignment
                            {
                                Weight = 100,
                                Pool = OopsAll,
                                PoolGroups = new List<List<int>> { OopsAllIDs },
                            },
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
                foreach (PoolAssignment pool in pools) processPool(pool, type);
                return pools;
            }
            Boss = processPools(Boss, "Boss");
            Miniboss = processPools(Miniboss, "Miniboss");
            Basic = processPools(Basic, "Basic");
            Add = processPools(Add, "Add");
            FoldingMonkey = processPools(FoldingMonkey, "FoldingMonkey");
            // Also copy 'basic' into 'add' if not specified, removing multi-phase enemies where possible
            if (Add == null && Basic != null)
            {
                Add = Basic.Select(p => p.Copy()).ToList();
                int removed = 0;
                foreach (PoolAssignment pool in Add)
                {
                    if (pool.PoolGroups.Count != 0)
                    {
                        removed += filterMulti(pool.PoolGroups, i => (infos[i].Class != EnemyClass.Boss && infos[i].Class != EnemyClass.Miniboss) || infos[i].HasTag("reasonable"));
                        if (pool.PoolGroups.Count == 0) pool.Weight = 0;
                    }
                }
                if (removed == 0)
                {
                    Add = null;
                }
            }

            HandleErrors(errors);
        }

        public void ProcessItemPreset(AnnotationData ann)
        {
            if (Items == null) return;

            // Validate the item config
            List<string> errors = new List<string>();

            Dictionary<string, string> newItems = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> entry in Items)
            {
                string item = entry.Key;
                string val = entry.Value;
                // For now, we can only do key items in general areas
                List<ItemKey> keyItems = ann.ItemGroups["keyitems"];
                if (!ann.Items.TryGetValue(item, out ItemKey key))
                {
                    errors.Add($"Unrecognized item name {item} (must use randomizer's internal short names)");
                    continue;
                }
                if (!keyItems.Contains(key))
                {
                    errors.Add($"{item} is not considered a key item, cannot be assigned somewhere");
                    continue;
                }
                // Also like enemy config, validation is done even for no-ops. This could also be moved up if the format changes too much.
                if (val == "any") continue;
                if (!ann.Areas.ContainsKey(val))
                {
                    errors.Add($"{item} assigned to location {val} which is not known area (must use randomizer's internal short names)");
                    continue;
                }
                newItems[item] = val;
            }
            Items = newItems.Count > 0 ? newItems : null;

            HandleErrors(errors);
        }
        
        private void HandleErrors(List<string> errors)
        {
            foreach (string error in errors)
            {
                Console.WriteLine(error);
            }
            if (errors.Count != 0)
            {
                if (errors.Count == 1)
                {
                    throw new Exception($"Error processing preset \"{Name}\": {errors[0]}");
                }
                else
                {
                    throw new Exception($"{errors.Count} error{(errors.Count == 1 ? "" : "s")} processing preset \"{Name}\", listed in spoiler logs");
                }
            }
        }
    }
}
