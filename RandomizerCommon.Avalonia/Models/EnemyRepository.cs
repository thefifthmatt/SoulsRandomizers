using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EnemyPreset;
using static RandomizerCommon.Messages;

namespace RandomizerCommon.Models
{
    public class EnemyRepository
    {
        private readonly EnemyAnnotations ann;
        private readonly Messages messages;

        // Enemy state is extremely stringly typed and needs structured representation of enemy instances and types to support additional features
        private readonly Dictionary<string, string> toInternal = new Dictionary<string, string>();
        private readonly Dictionary<string, string> toDisplay = new Dictionary<string, string>();
        private readonly Dictionary<EnemyClass, string> toDisplayClass = new Dictionary<EnemyClass, string>();
        private readonly Dictionary<string, EnemyClass> toInternalClass = new Dictionary<string, EnemyClass>();
        // Mainly for redundant categories
        private readonly Dictionary<string, List<string>> groupParents = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, EnemyCategory> groupCats = new Dictionary<string, EnemyCategory>();

        private readonly List<AutocompleteItem> enemyOptions;
        private readonly List<AutocompleteItem> enemyPoolOptions;
        private readonly List<AutocompleteItem> enemyManualOptions;
        private readonly List<AutocompleteItem> oopsAllOptions;
        // No longer used for validation, though validation may still be a good idea
        private readonly HashSet<string> allEnemyPoolOptions;

        [Localize]
        private static readonly Text classNorandomText = new Text("Not randomized", "PresetEditForm_classNorandom");
        [Localize]
        private static readonly Text classRandomText = new Text("Randomized", "PresetEditForm_classRandom");
        [Localize]
        private static readonly Text classCustomText = new Text("Custom", "PresetEditForm_classCustom");
        [Localize]
        private static readonly Text poolDefaultText = new Text("Self", "PresetEditForm_poolDefault");
        [Localize]
        private static readonly Text poolDefaultExplainText = new Text("Same category ({0})", "PresetEditForm_poolDefaultExplain");

        public EnemyRepository(EnemyAnnotations ann)
        {
            this.ann = ann;
            this.messages = Messages.GetInstance();

            // Set up ComboBox mapping, and internal<->display names
            void mapName(string intern, string display)
            {
                toDisplay[intern] = display;
                toInternal[display] = intern;
            }
            void mapClass(EnemyClass intern, string display)
            {
                toDisplayClass[intern] = display;
                toInternalClass[display] = intern;
                mapName(intern.ToString(), display);
            }
            foreach (EnemyClass cl in (EnemyClass[])Enum.GetValues(typeof(EnemyClass)))
            {
                string text = cl.ToString();
                if (ClassNames.TryGetValue(cl, out Text t)) text = messages.Get(t);
                mapClass(cl, text);
            }
            foreach (EnemyClassGroup clg in (EnemyClassGroup[])Enum.GetValues(typeof(EnemyClassGroup)))
            {
                string text = clg.ToString();
                if (ClassGroupNames.TryGetValue(clg, out Text t)) text = messages.Get(t);
                mapName(clg.ToString(), text);
            }
            enemyOptions = [];
            enemyManualOptions = [];
            oopsAllOptions = [];
            List<string> singletons = ann.Singletons ?? new List<string>();
            void addCategories(List<EnemyCategory> cats, List<string> strs, List<EnemyCategory> subs)
            {
                if (subs != null)
                {
                    cats.AddRange(subs);
                }
                if (strs != null)
                {
                    cats.AddRange(strs.Select(s => new EnemyCategory { Name = s }));
                }
            }
            foreach (EnemyCategory cat in ann.AllCategories)
            {
                if (cat.Name == null || cat.Hidden) continue;
                List<EnemyCategory> subs = new List<EnemyCategory> { cat };
                addCategories(subs, cat.Partition, cat.Partitions);
                addCategories(subs, cat.Partial, cat.Partials);
                addCategories(subs, cat.Instance, cat.Instances);
                AutocompleteItem? firstItem = null;
                List<string> displayChildren = new();
                foreach (EnemyCategory sub in subs)
                {
                    string obj = sub.Name;
                    groupCats[obj] = sub;
                    string name = ToDisplay(obj);
                    string dropdownName = (sub == cat ? "" : "- ") + name + (sub.Count > 1 ? $" ({sub.Count})" : "");
                    List<string>? keywords = cat.Keywords?.Split(' ')?.ToList();
                    if (firstItem == null && subs.Count > 1)
                    {
                        // For adding displayChildren later
                        keywords ??= new();
                    }
                    AutocompleteItem item = new AutocompleteItem(name, dropdownName, obj, keywords);
                    enemyOptions.Add(item);
                    if (sub.Classes == null && sub.Contains == null)
                    {
                        enemyManualOptions.Add(item);
                    }
                    if (!singletons.Contains(sub.Name))
                    {
                        oopsAllOptions.Add(item);
                    }
                    if (firstItem == null)
                    {
                        firstItem = item;
                        // For now, only support Contains at top-level
                        if (sub.Contains != null)
                        {
                            // More broad category than model name. This won't result in a consistent order but whatever
                            // These are model names which currrently have the same display name and obj name
                            foreach (string contain in sub.Contains)
                            {
                                Util.AddMulti(groupParents, contain, obj);
                            }
                        }
                    }
                    else
                    {
                        // The suggestions should go from most broad to most specific
                        Util.AddMulti(groupParents, obj, firstItem.Obj);
                        // Also for keywords
                        displayChildren.Add(name);
                    }
                    // Finally, most specific of all, manually listed parents
                    if (sub.Parents != null)
                    {
                        foreach (string parent in sub.Parents)
                        {
                            Util.AddMulti(groupParents, obj, parent);
                        }
                    }
                }
                if (firstItem?.Keywords != null && displayChildren.Count > 0)
                {
                    firstItem.Keywords.Add(string.Join('\n', displayChildren));
                }
            }
            enemyPoolOptions = enemyOptions.ToList();
            // Same thing but with Self
            // This is rewritten in AddEnemyEntry per-category
            string defaultText = messages.Get(poolDefaultText);
            string norandomText = messages.Get(poolNorandomText);
            mapName("default", defaultText);
            mapName("norandom", norandomText);
            enemyPoolOptions.InsertRange(0, new[]
            {
                new AutocompleteItem(defaultText, defaultText, "default"), new AutocompleteItem(norandomText, norandomText, "norandom")
            });
            allEnemyPoolOptions = new HashSet<string>(enemyPoolOptions.Select(i => i.Name));
            foreach (EnemyClass cl in (EnemyClass[])Enum.GetValues(typeof(EnemyClass)))
            {
                // For parents, these behave similarly to classes themselves, with a few differences.
                // They are higher in priority, they do not have any parents themselves, and are the parents of those categories.
                // As ToInternal is effectively unused, and the logic is in GetRedundantTags, this may not be necessary anymore
                string text = GetSelfName(cl);
                toInternal[text] = "default";
                allEnemyPoolOptions.Add(text);
            }
        }

        public string ToDisplay(string val) => toDisplay.TryGetValue(val, out string res) ? res : val;
        public string ToInternal(string val) => toInternal.TryGetValue(val, out string res) ? res : val;
        public string GetSelfName(EnemyClass cl) => messages.Get(poolDefaultExplainText, toDisplayClass[cl]);

        public enum EnemyListType
        {
            Unspecified,
            Enemy,
            EnemyPool,
            ManualPlacement,
            OopsAll,
        }

        public IReadOnlyList<AutocompleteItem> GetAutocompleteItems(EnemyListType type)
        {
            switch (type)
            {
                case EnemyListType.Enemy:
                    return enemyOptions;
                case EnemyListType.EnemyPool:
                    return enemyPoolOptions;
                case EnemyListType.ManualPlacement:
                    return enemyManualOptions;
                case EnemyListType.OopsAll:
                    return oopsAllOptions;
                default:
                    throw new Exception($"Unsupported {type}");
            }
        }

        // Value is displayed in the list, Display is for autocomplete box only, and Name is the underlying preset item (currently string)
        public record AutocompleteItem(string Name, string DropdownName, string Obj, List<string>? Keywords = null) : IComparable<AutocompleteItem>
        {
            public int CompareTo(AutocompleteItem? other) => Name.CompareTo(other?.Name);
        }

        // EnemyAnnotations accessors
        public List<PassiveAdjustment>? PassiveAdjustments => ann.PassiveAdjustments;

        public IReadOnlyList<ClassConfig> GetClasses() => ann.Classes;

        public ClassConfig GetClassConfig(EnemyClass cl)
        {
            return ann.Classes.Find(c => c.Class == cl) ?? throw new Exception($"Class {cl} not found");
        }

        public EnemyPreset MakeDefault() => MakeClassBasedDefault(ann);
        public void MigratePreset(EnemyPreset preset) => preset.MigrateVersion(ann);

        // Other things
        public Dictionary<string, string>? GetRedundantTags(IEnumerable<string> tags, EnemyClass? defaultClass = null)
        {
            HashSet<string> tagSet = new(tags);
            // Handle two common cases of redundancy: adding enemies already covered by categories, and groups with explicit subset relationships.
            // Fully generalizing this, it's a DAG, which may become necessary to deal with if/when moving away from categories.
            List<(EnemyCategory, string)> allClassOptions = new();
            foreach (string tag in tags)
            {
                if (tag == "default" && defaultClass is EnemyClass cl && cl != default)
                {
                    // Highest priority (always suggested as parent, never suggested for removal)
                    allClassOptions.Insert(0, (new EnemyCategory { Name = tag, Classes = [cl], ClassSet = [cl] }, tag));
                }
                else if (groupCats.TryGetValue(tag, out EnemyCategory? cat) && cat.Classes != null)
                {
                    cat.ClassSet ??= new(cat.Classes);
                    allClassOptions.Add((cat, tag));
                }
            }
            Dictionary<string, string>? redundantParents = null;
            foreach (string tag in tags)
            {
                string? parent = null;
                if (groupCats.TryGetValue(tag, out EnemyCategory? cat))
                {
                    List<EnemyClass> catClasses = cat.Classes ?? cat.ParentClasses;
                    // Never suggest removing self category, since it may be different in other categories.
                    if (cat.Name != "default" && catClasses != null)
                    {
                        // There is some oddness about inherited classes *not* having redundancy in the same way.
                        // e.g. Demi-Human in Regular Enemies technically adds it to Wildlife too.
                        // There's also the two equivalent classes with 'default', which could be checked for SetEquals.
                        // For the most part, don't try to catch all false positives as it makes basic feedback useless.
                        parent = allClassOptions.Find(i => i.Item1.ClassSet.IsSupersetOf(catClasses) && i.Item1 != cat).Item2;
                    }
                }
                if (parent == null && groupParents.TryGetValue(tag, out List<string>? catParents))
                {
                    parent = catParents.Intersect(tagSet).FirstOrDefault();
                }
                if (parent != null)
                {
                    redundantParents ??= new();
                    redundantParents[tag] = parent;
                }
            }
            return redundantParents;
        }
    }
}
