using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Messages;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.Util;
using System.Text;

namespace RandomizerCommon
{
    public class Preset
    {
        // YAML fields
        // Taken from file name
        [YamlIgnore]
        public string Name { get; set; }
        // If true, should not be edited
        public bool Builtin { get; set; }
        [YamlIgnore]
        public string DisplayName => Name == "Oops All" ? $"Oops All {OopsAll}" : Name;
        [YamlIgnore]
        public string FileName => $@"presets\{Name}.txt";
        // To show in UI
        public string Description { get; set; }
        // Whether to change default options, in DS3/Sekiro
        public bool RecommendFullRandomization { get; set; }
        public bool RecommendNoEnemyProgression { get; set; }
        // Options string, and underlying set (accessed by index method)
        private static readonly string optVersion = "v1";
        [YamlIgnore]
        private string options_;
        [YamlIgnore]
        private SortedSet<string> OptionsSet = new SortedSet<string> { optVersion };
        public string Options
        {
            get => options_;
            set
            {
                // This is mainly meant for setting from yaml parse.
                // TODO: Find a cleaner way to process this, doing it in property setters is awkward.
                options_ = value;
                OptionsSet.Clear();
                OptionsSet.Add(optVersion);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    OptionsSet.UnionWith(value.Split(' '));
                    // OptionsSet.IntersectWith(new[] { "v1" }.Concat(OptionsText.Keys));
                }
            }
        }
        // An enemy or enemy type to use for all possible enemies
        public string OopsAll { get; set; }
        [YamlIgnore]
        public List<int> OopsAllIDs = new List<int>();
        // For enemy onslaught runs (>2 may cause crashes)
        public int EnemyMultiplier { get; set; }
        // Individual pool specifications
        public List<PoolAssignment> Boss { get; set; }
        public List<PoolAssignment> Miniboss { get; set; }
        public List<PoolAssignment> Basic { get; set; }
        public List<PoolAssignment> Add { get; set; }
        public List<PoolAssignment> FoldingMonkey { get; set; }
        // General pool system
        public Dictionary<EnemyClass, ClassAssignment> Classes = new Dictionary<EnemyClass, ClassAssignment>();
        // Reduction of enemy types, presently
        public List<SourceAdjustment> AdjustSource { get; set; }
        [YamlIgnore]
        public Dictionary<int, float> AdjustSourceIDs = new Dictionary<int, float>();
        // Whether to increase health and deathblow count of enemies as bosses.
        // TODO: Handle this in DS3/Sekiro. This is probably not worth having as an option, ignored for now.
        public bool BuffBasicEnemiesAsBosses = false;
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
        // Temporary option
        public bool RandomScaling = false;

        public bool this[string opt]
        {
            get => OptionsSet.Contains(opt);
            set
            {
                if (value)
                {
                    OptionsSet.Add(opt);
                }
                else
                {
                    OptionsSet.Remove(opt);
                }
                if (OptionsSet.Count == 0)
                {
                    // This shouldn't be the case because of version, but it is different from other cases
                    options_ = "none";
                }
                else
                {
                    options_ = string.Join(" ", OptionsSet);
                }
            }
        }

        public static readonly Regex PhraseRe = new Regex(@"\s*;\s*");

        public class ClassAssignment
        {
            // Not randomized. If true, all below configuration is ignored.
            public bool NoRandom { get; set; }
            // Whether to merge into enemy config parent. If true, all below configuration is ignored.
            public bool MergeParent { get; set; }
            // Whether to copy the parent's pools and sources. If true, all below configuration is copied from the parent.
            public bool InheritParent { get; set; }
            // Other parent if not default. Must be part of ClassConfig
            public EnemyClass ManualParent { get; set; }
            // Custom multiplier. Requires overall EnemyMultiplier to be >0, and inherits its value if this is 0.
            public int EnemyMultiplier { get; set; }
            // Generic pool system
            public List<PoolAssignment> Pools { get; set; }
            // Sources to remove
            public string RemoveSource { get; set; }
            [YamlIgnore]
            public HashSet<int> RemoveSourceIDs = new HashSet<int>();
            [YamlIgnore]
            public EnemyClass RootParent { get; set; }
        }

        public class SourceAdjustment
        {
            public string Source { get; set; }
            public int Keep { get; set; }
            public int Total { get; set; }
            [YamlIgnore]
            public HashSet<int> SourceIDs = new HashSet<int>();
            [YamlIgnore]
            public float Multiplier = 1f;
        }

        public class PoolAssignment
        {
            // Points to use for this pool category, out of other pool categories
            public int Weight { get; set; }
            // Pool specification
            public string Pool { get; set; }
            // Whether to select by group
            public bool RandomByType { get; set; }
            // Whether this pool should
            public bool Norandom { get; set; }
            // Enemies per pool, filtered by other constraints as well
            [YamlIgnore]
            public List<List<int>> PoolGroups = new List<List<int>>();
            // Whether to also add default as its own PoolGroup, alongside non-default groups, because "default" cannot be resolved here
            [YamlIgnore]
            public int DefaultCount { get; set; }

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
                ret.Sort();
            }
            return ret;
        }

        public static readonly string DefaultName = "Custom";
        public static Preset MakeClassBasedDefault(EnemyAnnotations ann)
        {
            // Name, Description, OopsAll, EnemyMultiplier, Classes, AdjustSource, DontRandomize, RemoveSource
            // Most things can be null, but Classes should be initialized, w special Spectator treatment
            // TODO: Also individual enemy list
            Preset preset = new Preset
            {
                Name = DefaultName,
            };
            foreach (ClassConfig conf in ann.Classes)
            {
                ClassAssignment assign = new ClassAssignment
                {
                    Pools = new List<PoolAssignment>
                    {
                        new PoolAssignment
                        {
                            Pool = "default",
                            Weight = 1000,
                            RandomByType = false,
                        },
                    },
                };
                if (conf.DefaultInherit)
                {
                    assign.InheritParent = true;
                }
                if ((conf.NoSelfRandom || conf.NoMerge) && conf.Class != EnemyClass.Spectator)
                {
                    assign.Pools = null;
                }
                preset.Classes[conf.Class] = assign;
            }
            return preset;
        }

        public string ToYamlString()
        {
            StringWriter s = new StringWriter();
            GameData.Serializer.Serialize(s, this);
            return s.ToString();
        }

        public Preset CloneConfiguration()
        {
            string s = ToYamlString();
            IDeserializer deserializer = new DeserializerBuilder().Build();
            Preset other = deserializer.Deserialize<Preset>(s);
            other.Name = Name;
            return other;
        }

        public void SavePreset()
        {
            Directory.CreateDirectory("presets");
            using (var writer = File.CreateText(FileName))
            {
                GameData.Serializer.Serialize(writer, this);
            }
        }

        public string ToStableString()
        {
            // For basic cross-person comparison, even when order of things is different.
            // Default preset should return an empty string here, despite not having the EnemyAnnotations available here.
            // Main fields: OopsAll, EnemyMultiplier, Classes, AdjustSource.Source, DontRandomize, RemoveSource
            // In Classes: NoRandom, MergeParent, InheritParent.
            // If not merge/inherit and main one >0 and different: EnemyMultiplier. If none: Pools.Pool (when not default), RemoveSource
            // But... the same logic is replicated several times over :NotLikeThis:
            string sortUniq(IEnumerable<string> phrases) => string.Join("; ", phrases.Where(s => s != null).SelectMany(s => PhraseRe.Split(s.ToLowerInvariant())).Distinct().OrderBy(x => x, StringComparer.InvariantCulture));
            SortedDictionary<string, string> data = new SortedDictionary<string, string>();
            bool exists(string pool)
            {
                return pool != null && pool.ToLowerInvariant() != "none";
            }
            if (exists(OopsAll))
            {
                data["OopsAll"] = sortUniq(new[] { OopsAll });
            }
            if (exists(RemoveSource))
            {
                data["RemoveSource"] = sortUniq(new[] { RemoveSource });
            }
            if (exists(DontRandomize))
            {
                data["DontRandomize"] = sortUniq(new[] { DontRandomize });
            }
            if (AdjustSource != null)
            {
                data["AdjustSource"] = sortUniq(AdjustSource.Select(a => a.Source));
            }
            if (EnemyMultiplier > 0)
            {
                data["EnemyMultiplier"] = EnemyMultiplier.ToString();
            }
            if (Classes != null)
            {
                foreach ((EnemyClass cl, ClassAssignment assign) in Classes)
                {
                    // Hardcoded, NoRandom is inconsistent here
                    if (cl == EnemyClass.Scarab || assign == null) continue;
                    SortedDictionary<string, string> classData = new SortedDictionary<string, string>();
                    if (assign.MergeParent)
                    {
                        classData["MergeParent"] = "true";
                    }
                    else if (assign.InheritParent)
                    {
                        // This is potentially ambiguous, since there are default inherits too,
                        // but it's more important to say that default preset is default.
                        // classData["InheritParent"] = "true";
                    }
                    else if (assign.NoRandom)
                    {
                        classData["NoRandom"] = "true";
                    }
                    if (!assign.InheritParent && !assign.MergeParent)
                    {
                        if (EnemyMultiplier > 0 && assign.EnemyMultiplier > 0 && assign.EnemyMultiplier != EnemyMultiplier)
                        {

                            classData["EnemyMultiplier"] = assign.EnemyMultiplier.ToString();
                        }
                        if (!assign.NoRandom)
                        {
                            if (assign.Pools != null)
                            {
                                string pool = sortUniq(assign.Pools.Select(s => s.Pool));
                                if (pool.ToLowerInvariant() != "default")
                                {
                                    classData["Pools"] = pool;
                                }
                            }
                            if (exists(assign.RemoveSource))
                            {
                                classData["RemoveSource"] = sortUniq(new[] { assign.RemoveSource });
                            }
                        }
                    }
                    data[cl.ToString()] = string.Join("|", classData.Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{e.Key}={e.Value}"));
                }
            }
            return string.Join("||", data.Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{e.Key}={e.Value}"));
        }

        // Misc options. Some of these were originally part of the main form in Elden Ring (bosshp, multcolor, multhp, regularhp)
        // TODO: Add a way to rename texts
        [Localize]
        public static readonly Dictionary<string, Text> OptionsText = new Dictionary<string, Text>
        {
            ["bosshp"] = new Text("Increase HP for regular enemies as bosses", "EldenForm_bosshp"),
            ["regularhp"] = new Text("Reduce HP for bosses as regular enemies", "EldenForm_regularhp"),
            ["multhp"] = new Text("Reduce HP for bosses in Enemy Onslaught slightly", "EldenForm_multhp"),
            ["multcolor"] = new Text("Color-code bosses in Enemy Onslaught", "EldenForm_multcolor"),
            ["scalerandom"] = new Text("Randomly scale enemies", "EldenForm_scalerandom"),
            ["scaleup"] = new Text("Only scale enemies up", "EldenForm_scaleup"),
        };
        public static readonly HashSet<string> DefaultOptions = new HashSet<string> { "bosshp", "regularhp", "v1" };

        [Localize]
        private static readonly Text presetNameText = new Text("Preset: {0}", "Preset_presetName");
        [Localize]
        private static readonly Text oopsAllText = new Text("Oops All {0}", "Preset_oopsAll");
        [Localize]
        private static readonly Text enemyMultiplierText = new Text("Enemy Onslaught {0}x", "Preset_enemyMultiplier");
        [Localize]
        private static readonly Text enemyMultiplierDescText = new Text("{0}x {1}", "Preset_enemyMultiplierDesc");
        [Localize]
        private static readonly Text dontRandomizeText = new Text("Not randomized: {0}", "Preset_dontRandomize");
        [Localize]
        private static readonly Text removeSourceText = new Text("Excluded: {0}", "Preset_removeSource");
        [Localize]
        private static readonly Text optionsText = new Text("Options: {0}", "Preset_options");
        [Localize]
        private static readonly Text poolDescText = new Text("Replacing {0}:", "Preset_poolDesc");
        [Localize]
        private static readonly Text poolDescOneLineText = new Text("Replacing {0}: {1}", "Preset_poolDescOneLine");
        [Localize]
        private static readonly Text poolDescInheritText = new Text("Replacing {0}: Same as {1}", "Preset_poolDescInherit");
        [Localize]
        private static readonly Text poolEntriesText = new Text("- {0}% {1}", "Preset_poolEntries");
        [Localize]
        private static readonly Text poolExcludeText = new Text("- (excluding {0})", "Preset_poolExclude");
        [Localize]
        private static readonly Text poolDefaultText = new Text("Themselves", "Preset_poolDefault");
        // Also used in PresetEditForm
        [Localize]
        internal static readonly Text poolNorandomText = new Text("Not randomized", "Preset_poolNorandom");
        [Localize]
        private static readonly Text poolDelimitTypeText = new Text(", ", "Preset_poolDelimitType");
        // Should not be a comma
        [Localize]
        private static readonly Text poolDelimitEntryText = new Text("; ", "Preset_poolDelimitEntry");
        [Localize]
        private static readonly Text adjustSourceText = new Text("Adjusted to {0}%: {1}", "Preset_adjustSource");

        public static string FormatPercent(double val)
        {
            return $"{val:0.##}";
        }

        public string FullDescription(Messages messages, EnemyAnnotations ann = null)
        {
            Dictionary<string, EnemyClass> classNames =
                ((EnemyClass[])Enum.GetValues(typeof(EnemyClass))).ToDictionary(e => e.ToString(), e => e);
            Dictionary<string, EnemyClassGroup> classGroupNames =
                ((EnemyClassGroup[])Enum.GetValues(typeof(EnemyClassGroup))).ToDictionary(e => e.ToString(), e => e);
            string sep = messages.Get(poolDelimitEntryText);
            string classText(EnemyClass cl)
            {
                return ClassNames.TryGetValue(cl, out Text t) ? messages.Get(t) : cl.ToString();
            }
            string desc(string text, string defaultText = null)
            {
                text = text ?? "default";
                // For now, don't localize this much outside of overall categories
                List<string> parts = new List<string>();
                foreach (string part in PhraseRe.Split(text))
                {
                    if (classNames.TryGetValue(part, out EnemyClass cl) && ClassNames.TryGetValue(cl, out Text t))
                    {
                        parts.Add(messages.Get(t));
                    }
                    else if (classGroupNames.TryGetValue(part, out EnemyClassGroup g)
                        && ClassGroupNames.TryGetValue(g, out Text t2))
                    {
                        parts.Add(messages.Get(t2));
                    }
                    else if (part == "default")
                    {
                        parts.Add(defaultText ?? messages.Get(poolDefaultText));
                    }
                    else if (part == "norandom")
                    {
                        parts.Add(messages.Get(poolNorandomText));
                    }
                    else
                    {
                        parts.Add(part);
                    }
                }
                return string.Join(sep, parts);
            }
            bool exists(string pool)
            {
                return pool != null && pool.ToLowerInvariant() != "none";
            }

            // Name, Description, OopsAll, EnemyMultiplier, Classes, AdjustSource, DontRandomize, RemoveSource
            StringBuilder ret = new StringBuilder();
            // Ideally, this could be bolded
            if (Name != null) ret.AppendLine(messages.Get(presetNameText, Name));
            if (!string.IsNullOrWhiteSpace(Description)) ret.AppendLine(Description);
            if (exists(OopsAll)) ret.AppendLine(messages.Get(oopsAllText, desc(OopsAll)));
            if (EnemyMultiplier > 0) ret.AppendLine(messages.Get(enemyMultiplierText, EnemyMultiplier));
            string sepClass = messages.Get(poolDelimitTypeText);
            void appendPool(
                List<EnemyClass> poolClasses,
                List<PoolAssignment> pools,
                ClassAssignment assign = null,
                bool selfRandom = true)
            {
                string classes = string.Join(sepClass, poolClasses.Select(classText));
                string replacing = classes;
                if (EnemyMultiplier > 0 && assign != null && assign.EnemyMultiplier > 0 && assign.EnemyMultiplier != EnemyMultiplier)
                {
                    replacing = messages.Get(enemyMultiplierDescText, assign.EnemyMultiplier, replacing);
                }
                if (assign != null && assign.NoRandom)
                {
                    ret.AppendLine(messages.Get(poolDescOneLineText, replacing, messages.Get(poolNorandomText)));
                    return;
                }
                string defaultText = classes;
                if (pools == null || pools.Count == 0
                    || pools.All(p => p.Pool == null || p.Pool.ToLowerInvariant() == "default" || p.Weight <= 0))
                {
                    if (selfRandom)
                    {
                        ret.AppendLine(messages.Get(poolDescOneLineText, replacing, defaultText));
                    }
                    else
                    {
                        ret.AppendLine(messages.Get(poolDescOneLineText, replacing, messages.Get(poolNorandomText)));
                    }
                }
                else if (pools.Count == 1)
                {
                    ret.AppendLine(messages.Get(poolDescOneLineText, replacing, desc(pools[0].Pool, defaultText)));
                }
                else
                {
                    ret.AppendLine(messages.Get(poolDescText, replacing));
                    int total = 0;
                    foreach (PoolAssignment p in pools)
                    {
                        if (p.Weight <= 0) continue;
                        total += p.Weight;
                    }
                    foreach (PoolAssignment p in pools)
                    {
                        if (p.Weight <= 0) continue;
                        string percent = FormatPercent(p.Weight * 100f / total);
                        string pool = p.Pool ?? "default";
                        ret.AppendLine(messages.Get(poolEntriesText, percent, desc(pool, defaultText)));
                    }
                }
                if (exists(assign?.RemoveSource))
                {
                    ret.AppendLine(messages.Get(poolExcludeText, desc(assign?.RemoveSource)));
                }
            }
            void appendClass(
                ClassConfig conf,
                ClassAssignment assign,
                List<EnemyClass> children)
            {
                List<EnemyClass> poolClasses = new List<EnemyClass> { conf.Class };
                poolClasses.AddRange(children);
                appendPool(poolClasses, assign?.Pools, assign, !conf.NoSelfRandom);
            }
            void appendSimple(EnemyClass cl, List<PoolAssignment> pools)
            {
                appendPool(new List<EnemyClass> { cl }, pools);
            }
            // Oops All ignores other randomization, Enemy Onslaught does not
            if (OopsAll == null)
            {
                if (ann?.Classes == null)
                {
                    appendSimple(EnemyClass.Basic, Basic);
                    appendSimple(EnemyClass.Boss, Boss);
                    appendSimple(EnemyClass.Miniboss, Miniboss);
                    if (FoldingMonkey != null) appendSimple(EnemyClass.FoldingMonkey, FoldingMonkey);
                    appendSimple(EnemyClass.Spectator, Add);
                }
                else
                {
                    Dictionary<EnemyClass, EnemyClass> mergeParents = new Dictionary<EnemyClass, EnemyClass>();
                    foreach (ClassConfig conf in ann.Classes)
                    {
                        EnemyClass parent = GetParent(conf.Class, ann, checkMerge: true);
                        if (parent != conf.Class)
                        {
                            mergeParents[conf.Class] = parent;
                        }
                    }
                    foreach (ClassConfig conf in ann.Classes)
                    {
                        if (mergeParents.ContainsKey(conf.Class)) continue;
                        EnemyClass inheritParent = GetParent(conf.Class, ann, checkMerge: false);
                        EnemyClass showClass = conf.Class;
                        if (inheritParent != conf.Class)
                        {
                            // A bit too confusing, just write out the full thing, even if it's super verbose
                            // ret.AppendLine(messages.Get(poolDescInheritText, classText(conf.Class), classText(inheritParent)));
                            showClass = inheritParent;
                        }
                        Classes.TryGetValue(showClass, out ClassAssignment assign);
                        appendClass(
                            conf, assign,
                            mergeParents.Where(e => e.Value == conf.Class).Select(e => e.Key).ToList());
                    }
                }
            }
            if (exists(DontRandomize)) ret.AppendLine(messages.Get(dontRandomizeText, desc(DontRandomize)));
            if (exists(RemoveSource)) ret.AppendLine(messages.Get(removeSourceText, desc(RemoveSource)));
            // This treatment of defaults is a bit ambiguous. Hopefully version string will address default changes.
            if (exists(Options) && !DefaultOptions.SetEquals(OptionsSet))
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, Text> entry in OptionsText)
                {
                    if (OptionsSet.Contains(entry.Key))
                    {
                        // I guess do one-per-line for now
                        ret.AppendLine(messages.Get(entry.Value));
                    }
                }
                if (parts.Count > 0) ret.AppendLine(messages.Get(optionsText, string.Join(sep, parts)));
            }
            if (AdjustSource != null)
            {
                foreach (SourceAdjustment adjust in AdjustSource)
                {
                    if (exists(adjust.Source) && adjust.Total != 0)
                    {
                        float mult = Math.Max(0, Math.Min(1, (float)adjust.Keep / adjust.Total));
                        string percent = FormatPercent(mult * 100);
                        ret.AppendLine(messages.Get(adjustSourceText, percent, desc(adjust.Source)));
                    }
                }
            }
            return ret.ToString();
        }

        private EnemyClass GetParent(EnemyClass cl, EnemyAnnotations ann, bool checkMerge)
        {
            Classes.TryGetValue(cl, out ClassAssignment assign);
            ClassConfig conf = ann.Classes.Find(c => c.Class == cl);
            EnemyClass targetClass = cl;
            int iters = 0;
            // This assumes that InheritParent and MergeParent are not set together.
            // Merge will always take precedent in the randomizer itself.
            while (iters++ < 10 && conf != null)
            {
                EnemyClass parent = EnemyClass.Default;
                if (checkMerge)
                {
                    if (!conf.NoMerge && assign != null && assign.MergeParent)
                    {
                        parent = conf.Parent;
                        if (conf.AltParent != null && conf.AltParent.Contains(assign.ManualParent))
                        {
                            parent = assign.ManualParent;
                        }
                    }
                }
                else
                {
                    if (assign == null ? conf.DefaultInherit : assign.InheritParent)
                    {
                        parent = conf.Parent;
                    }
                }
                if (parent == EnemyClass.Default) break;
                conf = ann.Classes.Find(c => c.Class == parent);
                if (conf == null) break;
                Classes.TryGetValue(parent, out assign);
                targetClass = parent;
            }
            if (iters >= 10) throw new Exception($"Internal error: loop in parent hierarchy for {conf.Class}");
            return targetClass;
        }

        public static Preset ParsePreset(string name, string text)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            Preset preset = deserializer.Deserialize<Preset>(text);
            preset.Name = name;
            return preset;
        }

        // Just load it at the start, but don't do any validation yet.
        // This method may throw exceptions which the caller should probably catch.
        public static Preset LoadPreset(string name, bool extractOopsAll = false, string checkDir = null)
        {
            string loadName = name;
            if (extractOopsAll && name.StartsWith("Oops All "))
            {
                loadName = "Oops All";
            }
            string path = $@"{checkDir ?? "presets"}\{loadName}.txt";

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

        public void ProcessParents(EnemyAnnotations ann)
        {
            // Before having any game-specific data, calculate root parents and explicitly add classes with default behaviors
            ClassAssignment processClass(ClassConfig conf, ClassAssignment assign)
            {
                if (assign == null)
                {
                    if (!conf.NoMerge && conf.DefaultInherit)
                    {
                        // Add new class if default inherit.
                        // This should not be dependent on class order.
                        assign = new ClassAssignment
                        {
                            InheritParent = true,
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
                if (assign.MergeParent)
                {
                    assign.RootParent = GetParent(conf.Class, ann, checkMerge: true);
                }
                else
                {
                    assign.RootParent = GetParent(conf.Class, ann, checkMerge: false);
                    // Inherit EnemyMultiplier here, since it's needed in first pass, and doesn't require pool/enemy info
                    Classes.TryGetValue(assign.RootParent, out ClassAssignment baseAssign);
                    assign.EnemyMultiplier = baseAssign?.EnemyMultiplier ?? 0;
                }
                return assign;
            }
            if (ann.Classes != null)
            {
                foreach (ClassConfig conf in ann.Classes)
                {
                    Classes.TryGetValue(conf.Class, out ClassAssignment assign);
                    Classes[conf.Class] = processClass(conf, assign);
                }
            }
        }

        public void ProcessEnemyPreset(
            GameData game,
            Dictionary<int, EnemyInfo> infos,
            EnemyAnnotations ann,
            Dictionary<int, EnemyData> defaultData)
        {
            // Go through full enemy config
            List<EnemyCategory> cats = ann.Categories;
            // Process enemy names
            HashSet<string> eligibleNames = new HashSet<string>();
            Dictionary<string, string> impliedNames = new Dictionary<string, string>();
            Dictionary<EnemyClass, List<string>> impliedClasses = new Dictionary<EnemyClass, List<string>>();
            foreach (EnemyCategory cat in cats)
            {
                eligibleNames.Add(cat.Name);
                HashSet<string> subNames = new HashSet<string>();
                if (cat.Instance != null) subNames.UnionWith(cat.Instance);
                if (cat.Partition != null) subNames.UnionWith(cat.Partition);
                if (cat.Partial != null) subNames.UnionWith(cat.Partial);
                if (subNames.Count > 0)
                {
                    eligibleNames.UnionWith(subNames);
                    foreach (string subName in subNames)
                    {
                        impliedNames[subName] = cat.Name;
                    }
                }
                if (cat.Classes != null)
                {
                    foreach (EnemyClass cl in cat.Classes)
                    {
                        AddMulti(impliedClasses, cl, cat.Name);
                    }
                }
            }
            if (ann.Classes != null)
            {
                foreach (ClassConfig conf in ann.Classes)
                {
                    if (!conf.NoMerge)
                    {
                        AddMulti(impliedClasses, conf.Class, conf.Class.ToString());
                    }
                }
            }
            if (eligibleNames.Count == 0)
            {
                eligibleNames.UnionWith(game.GetModelNames());
            }
            bool printCategories = false;
            if (printCategories)
            {
                SortedSet<string> categories = new SortedSet<string>();
                foreach (EnemyInfo info in infos.Values)
                {
                    if (info.Class == EnemyClass.None || info.Class == EnemyClass.Helper) continue;
                    if (info.ExtraName != null)
                    {
                        categories.Add(info.ExtraName);
                    }
                    else
                    {
                        categories.Add(game.ModelName(info.ModelName));
                    }
                }
                foreach (string cat in categories)
                {
                    Console.WriteLine($"- Name: {cat}");
                }
            }
            Dictionary<int, string> primaryName = new Dictionary<int, string>();
            Dictionary<string, SortedSet<int>> enemiesForName = new Dictionary<string, SortedSet<int>>();
            bool debugNames = false;
            // Guardian Ape is both a boss and a helper, so try to avoid the helper ape getting pulled into the category
            HashSet<string> bossNames = new HashSet<string>(infos.Values.Where(i => i.Class == EnemyClass.Boss && i.ExtraName != null).Select(i => i.ExtraName));
            foreach (EnemyInfo info in infos.Values)
            {
                // Do not let some enemies be randomized at this point, many will prevent the game from being completeable.
                if (info.Class == EnemyClass.None) continue;
                // Don't do helpers by default in DS3, as many are nonfunctional on their own. Tags could be used to override this.
                // In Sekiro, bossNames handles some cases like this.
                if (!game.Sekiro && info.Class == EnemyClass.Helper && !info.HasTag("standalone")) continue;
                // Skip synthetic enemies from dupes, when creating pools
                // This means they can't be used for the enemy mapping, but those ids are not stable anyway (need different config keying)
                if (info.DupeFrom > 0) continue;
                List<string> names = new List<string>();
                // Add all names. The first name added will be the primary name.
                if (info.ExtraName != null)
                {
                    names.Add(info.ExtraName);
                }
                if (defaultData.TryGetValue(info.ID, out EnemyData data))
                {
                    string model = game.ModelName(data.Model);
                    if (game.EldenRing)
                    {
                        model = Regex.Replace(model, @"[0-9 ]*$", "");
                    }
                    if (info.Class != EnemyClass.Boss && info.Category != null)
                    {
                        foreach (string cat in PhraseRe.Split(info.Category))
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
                    foreach (string name in names.ToList())
                    {
                        if (impliedNames.TryGetValue(name, out string broadName) && !names.Contains(broadName))
                        {
                            names.Add(broadName);
                        }
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
                if (!game.EldenRing)
                {
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
                }
                if (impliedClasses.TryGetValue(info.Class, out List<string> classNames))
                {
                    names.AddRange(classNames);
                }
                if (debugNames) Console.WriteLine($"-- Names: {string.Join("; ", names)}");
                foreach (string name in names)
                {
                    AddMulti(enemiesForName, name, info.ID);
                }
            }
            // Mapping Enemy name: any
            bool generateEnemyList = false;
            if (generateEnemyList)
            {
                List<List<EnemyClass>> classGroups = new List<List<EnemyClass>>
                {
                    new List<EnemyClass> { EnemyClass.Boss },
                    new List<EnemyClass> { EnemyClass.TutorialBoss },
                    new List<EnemyClass> { EnemyClass.Miniboss },
                    new List<EnemyClass> { EnemyClass.FoldingMonkey },
                    new List<EnemyClass> { EnemyClass.Basic, EnemyClass.CrystalLizard, EnemyClass.Mimic },
                };
                foreach (List<EnemyClass> cs in classGroups)
                {
                    string map = null;
                    (string, string, int) sortKey(EnemyInfo info)
                    {
                        string enemyMap = defaultData[info.ID].MainMap;
                        return (enemyMap, info.ModelName, info.ID);
                    }
                    foreach (EnemyInfo info in infos.Values.OrderBy(sortKey))
                    {
                        if (!cs.Contains(info.Class)) continue;
                        if (primaryName.TryGetValue(info.ID, out string name))
                        {
                            string enemyMap = game.LocationNames[game.Locations[defaultData[info.ID].MainMap]];
                            if (map != enemyMap)
                            {
                                map = enemyMap;
                                Console.WriteLine($"  # {map}");
                            }
                            Console.WriteLine($"  {name} {info.ID}: any");
                        }
                        else
                        {
                            throw new Exception($"No name for {info.Class} {info.ID}: {info.DebugText}");
                        }
                    }
                    Console.WriteLine();
                }
            }
            foreach (EnemyCategory cat in cats)
            {
                if (cat.Contains == null) continue;
                SortedSet<int> combinedIds = new SortedSet<int>();
                foreach (string sub in cat.Contains)
                {
                    if (enemiesForName.TryGetValue(sub, out SortedSet<int> specialIds))
                    {
                        combinedIds.UnionWith(specialIds);
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
                if (!enemiesForName.TryGetValue(name, out SortedSet<int> ids))
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
                    else if (name == "norandom")
                    {
                        findId = ". \"norandom\" must appear by itself in pools, without any other enemy names.";
                    }
                    errors.Add($"Unrecognized enemy name \"{name}\"{findId}");
                    return new List<int>();
                }
                return ids.ToList();
            }
            List<List<int>> getMultiIds(string name, bool allToAny = false)
            {
                List<List<int>> ids = new List<List<int>>();
                foreach (string n in PhraseRe.Split(name))
                {
                    ids.Add(getIds(n));
                    // Mega-hack for DontRandomize + enemy multiplier combination - use it as shorthand for ignoring
                    // individual category randomization.
                    if (allToAny && n == "AllEnemies" && game.EldenRing)
                    {
                        ids.Add(getIds("HostileNPC"));
                    }
                }
                return ids;
            }
            List<List<int>> getPoolMultiIds(string name, out int defaultCount)
            {
                defaultCount = 0;
                List<List<int>> ids = new List<List<int>>();
                foreach (string n in PhraseRe.Split(name))
                {
                    if (n == "default")
                    {
                        defaultCount++;
                        continue;
                    }
                    ids.Add(getIds(n));
                }
                return ids;
            }

            // Fill in non-randomized ids. The individual enemy config can also add to this.
            if (DontRandomize != null && DontRandomize.ToLowerInvariant() != "none")
            {
                DontRandomizeIDs.UnionWith(getMultiIds(DontRandomize, true).SelectMany(i => i));
            }
            if (RemoveSource != null && RemoveSource.ToLowerInvariant() != "none")
            {
                RemoveSourceIDs.UnionWith(getMultiIds(RemoveSource).SelectMany(i => i));
            }
            if (AdjustSource != null)
            {
                foreach (SourceAdjustment adjust in AdjustSource)
                {
                    adjust.SourceIDs.UnionWith(getMultiIds(adjust.Source).SelectMany(i => i));
                    if (adjust.Total != 0)
                    {
                        adjust.Multiplier = Math.Max(0, Math.Min(1, (float)adjust.Keep / adjust.Total));
                    }
                    foreach (int id in adjust.SourceIDs)
                    {
                        AdjustSourceIDs[id] = adjust.Multiplier;
                    }
                }
            }

            // Process the specific enemy map config
            bool debug = false;
            if (Enemies != null)
            {
                bool otherNorandom = false;
                foreach (KeyValuePair<string, string> entry in Enemies)
                {
                    if (entry.Key == "Other" && game.EldenRing)
                    {
                        if (entry.Value.ToLowerInvariant() == "norandom")
                        {
                            otherNorandom = true;
                        }
                        else
                        {
                            errors.Add($"The only supported value for enemy assignment \"Other\" is \"norandom\"");
                        }
                        continue;
                    }
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
                if (otherNorandom)
                {
                    // TODO: Where is Any? Bring it back?
                    DontRandomizeIDs.UnionWith(getMultiIds("AllEnemies", true).SelectMany(x => x).Except(EnemyIDs.Keys));
                }
            }

            bool enemyFilter(int id)
            {
                return !DontRandomizeIDs.Contains(id) && !RemoveSourceIDs.Contains(id);
            }
            bool poolFilter(int id, ClassAssignment assign = null)
            {
                return enemyFilter(id) && (assign == null || !assign.RemoveSourceIDs.Contains(id));
            }

            // If oops all mode, fill in oops all ids. And copy them to pools.
            if (OopsAll != null && OopsAll.ToLowerInvariant() != "none")
            {
                OopsAllIDs.AddRange(getMultiIds(OopsAll).SelectMany(i => i).Where(enemyFilter).Distinct());
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
            void processPool(PoolAssignment pool, string type, ClassAssignment assign = null)
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
                else if (pool.Pool.ToLowerInvariant() == "norandom")
                {
                    pool.Norandom = true;
                    return;
                }
                pool.PoolGroups = getPoolMultiIds(pool.Pool, out int defaultCount);
                pool.DefaultCount = defaultCount;
                filterMulti(pool.PoolGroups, id => poolFilter(id, assign));
                // Console.WriteLine($"Processed {type} pool groups to [{string.Join(", ", pool.PoolGroups.Select(p => p.Count))}]");
                if (pool.PoolGroups.Count == 0) pool.Weight = 0;
            }
            List<PoolAssignment> processPools(
                List<PoolAssignment> pools, string type, bool allowOops = true, ClassAssignment assign = null)
            {
                if (OopsAllIDs.Count > 0 && allowOops)
                {
                    return new List<PoolAssignment>
                    {
                        new PoolAssignment
                        {
                            Weight = 100,
                            Pool = OopsAll,
                            // For now, ignore removeIds, mainly to avoid weird case of partially removing everything.
                            // Use global exclusion for that instead.
                            PoolGroups = new List<List<int>> { OopsAllIDs },
                        },
                    };
                }
                else if (pools == null || (pools.Count == 1 && pools[0].Pool.ToLowerInvariant() == "default"))
                {
                    return null;
                }
                foreach (PoolAssignment pool in pools)
                {
                    processPool(pool, type, assign);
                }
                return pools;
            }
            ClassAssignment processClass(ClassConfig conf, ClassAssignment assign)
            {
                bool allowOops = !conf.NoMerge;
                if (assign == null)
                {
                    List<PoolAssignment> autoPools = processPools(null, conf.Class.ToString(), allowOops);
                    if (autoPools == null)
                    {
                        return null;
                    }
                    else
                    {
                        // Default Oops All mode for this class. It can be its own parent
                        return new ClassAssignment
                        {
                            RootParent = conf.Class,
                            Pools = autoPools,
                        };
                    }
                }
                if (assign.MergeParent)
                {
                    // RootParent is already set, nothing else to do
                }
                else
                {
                    Classes.TryGetValue(assign.RootParent, out ClassAssignment baseAssign);
                    assign.NoRandom = baseAssign?.NoRandom ?? false;
                    assign.Pools = baseAssign?.Pools;
                    assign.RemoveSource = baseAssign?.RemoveSource;

                    // Probably fine to ignore processing if NoRandom
                    if (!assign.NoRandom)
                    {
                        if (assign.RemoveSource != null && assign.RemoveSource.ToLowerInvariant() != "none")
                        {
                            assign.RemoveSourceIDs.UnionWith(getMultiIds(assign.RemoveSource).SelectMany(i => i));
                        }
                        assign.Pools = processPools(assign.Pools, conf.Class.ToString(), allowOops, assign);
                    }
                }
                return assign;
            }
            if (ann.Classes == null)
            {
                Boss = processPools(Boss, "Boss");
                Miniboss = processPools(Miniboss, "Miniboss");
                Basic = processPools(Basic, "Basic");
                Add = processPools(Add, "Add");
                FoldingMonkey = processPools(FoldingMonkey, "FoldingMonkey");
                // Also copy 'basic' into 'add' if not specified, removing multi-phase enemies where possible
                // This probably isn't necessary in ER? If it's always present in the dictionary
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
            }
            else
            {
                foreach (ClassConfig conf in ann.Classes)
                {
                    Classes.TryGetValue(conf.Class, out ClassAssignment assign);
                    Classes[conf.Class] = processClass(conf, assign);
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
