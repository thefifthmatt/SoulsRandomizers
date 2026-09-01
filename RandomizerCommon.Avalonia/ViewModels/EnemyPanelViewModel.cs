using DynamicData;
using DynamicData.Binding;
using RandomizerCommon.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.EnemyPreset;
using static RandomizerCommon.Messages;
using static RandomizerCommon.Models.EnemyRepository;

namespace RandomizerCommon.ViewModels
{
    public partial class EnemyPanelViewModel : ViewModelBase
    {
        // This can become more like ItemPanelViewModel over time, but keep this closer to WinForms impl for the time being
        public enum TabType
        {
            GlobalSettings,
            MiscOptions,
            // Enemies by targets, currently a fixed list
            EnemyCategories,
            EnemyCategory,
            AdjustSource,
            ManualPlacement,
        }

        // This excludes strings already covered in ItemPreset
        [Localize]
        public static readonly Dictionary<TabType, Text> TabTypeDesc = MapText<TabType>("EnemyPanel_TabTypeDesc", new()
        {
            [TabType.GlobalSettings] = "Global Settings",
            [TabType.MiscOptions] = "Misc Options",
        });

        [Localize]
        private static readonly Text enemyCategoryText = new Text("Enemy Categories", "PresetEditForm_enemyCategory");
        [Localize]
        private static readonly Text adjustSourceText = new Text("Frequency Adjustments", "PresetEditForm_adjustSource");
        [Localize]
        private static readonly Text manualText = new Text("Manual Placement", "PresetEditForm_manual");

        public static readonly Dictionary<TabType, Text> ExistingText = new()
        {
            [TabType.EnemyCategories] = enemyCategoryText,
            [TabType.AdjustSource] = adjustSourceText,
            [TabType.ManualPlacement] = manualText,
        };

        [Localize]
        public static readonly Dictionary<TabType, Text> TabTypeExplain = MapText<TabType>("EnemyPanel_TabTypeExplain", new()
        {
        });

        [Localize]
        internal static readonly Text dontRandomizeText = new Text("Not randomized:", "PresetEditForm_dontRandomize");
        [Localize]
        internal static readonly Text removeSourceText = new Text("Excluded everywhere:", "PresetEditForm_removeSource");
        [Localize]
        private static readonly Text classRemoveSourceText = new Text("Excluded:", "PresetEditForm_classRemoveSource");
        [Localize]
        private static readonly Text poolTargetText = new Text("Enemy:", "PresetEditForm_poolTarget");
        [Localize]
        private static readonly Text poolSourceText = new Text("Replacement:", "PresetEditForm_poolSource");

        [Localize]
        public static readonly Text CustomHelpText = new Text(@"Enemy randomizer works by swapping out enemies with other enemies taken from elsewhere in the game. It does this independently for each enemy category. Here you can configure a custom list of source enemies which can replace enemies in this category. You can also merge some categories together, by selecting ""Merge with"" in subcategories, or clicking on ""Quick edit"" links below to do this automatically.

When configuring source enemies, you can click the + button to add a new source group. The % probability across all groups in a category must add up to 100%. If you want 10% of enemies in this category to become any boss, use a 90% group with ""Same category"" and a 10% group with ""All Bosses"". If you want only 50% of enemies in this category to be randomized, use a 50% group with ""Same category"" and a 50% group with ""Not randomized"".", "PresetEditForm_customHelpText");
        [Localize]
        private static readonly Text redundantEnemyTooltipText = new Text("\"{0}\" can be removed from this list because it's already included in \"{1}\".", "PresetEditForm_redundantEnemyTooltip");
        [Localize]
        private static readonly Text redundantEnemyTooltipPoolText = new Text("Use the + button if you want to make a separate source group with this enemy.", "PresetEditForm_redundantEnemyTooltipPool");

        public enum PlacementMode
        {
            Unspecified,
            Inherit,
            Merge,
            Merge2,
            Norandom,
            Random,
            Custom,
        }

        public EnemyPresetViewModel Preset { get; init; }

        [Reactive] private TabType _type;
        [Reactive] private Text? _nameText;
        [Reactive] private ObservableCollection<EnemyPanelViewModel>? _children;

        // Different cases for this ViewModel. The reference is not updated after the constructor
        [Reactive] private EnemyClass _class;
        [Reactive] private ClassAssignment? _assign;
        [Reactive] private Dictionary<string, string>? _manualEnemies;

        // State splatted out from different cases
        [Reactive] private DisplayEnemyList? _removeSource;
        [Reactive] private ObservableCollection<DisplayEnemyList>? _pools;
        [Reactive] private PlacementMode _mode;
        [Reactive] private bool _dlcSilo;
        [Reactive] private bool _overrideMultiplier;
        [Reactive] private int? _multiplier;
        [Reactive] private bool _autoBalance = true;

        // UI interaction state, most of it to avoid BoolConverters boilerplate
        [Reactive] private bool _showPools;
        [Reactive] private bool _showModes;
        // Visual-only overrides just for clarity
        [Reactive] private bool _modeOverride;
        [Reactive] private bool _oopsAllOverride;
        [Reactive] private bool _norandomOverride;
        [Reactive] private bool _dlcSiloForced;
        [Reactive] private bool _showMultiplier;
        // For pool-editing
        [Reactive] private string _percentTotal = "";
        [Reactive] private bool _showPercentTotal;

        // Immutable based on tab type
        [Reactive] private ImmutableList<PlacementMode>? _modes;
        [Reactive] private PlacementMode _defaultMode;
        [Reactive] private bool _canEditDlcSilo;
        [Reactive] private bool _canOopsAllOverride;
        [Reactive] private Text? _parentLabel;
        [Reactive] private Text? _parentLabel2;
        [Reactive] private Text? _explainLabel;

        // Manual UI interaction. Logic should maybe exist in codebehind but there is no distinct UserControl for a panel
        internal readonly Subject<Unit> WeightFocusChanged = new();
        private int lastFocusedWeight = -1;

        public EnemyPanelViewModel(EnemyPresetViewModel parent, TabType type, object? model = null, EnemyClass cl = EnemyClass.Default, IEnumerable<EnemyPanelViewModel>? children = null)
        {
            Preset = parent;
            Type = type;
            Children = children is null ? null : new(children);
            Messages messages = Messages.GetInstance();
            if (TabTypeDesc.TryGetValue(Type, out Text? nameText) || ExistingText.TryGetValue(Type, out nameText))
            {
                NameText = nameText;
            }
            if (TabTypeExplain.TryGetValue(Type, out Text? explainText))
            {
                ExplainLabel = explainText;
            }
            if (Type == TabType.EnemyCategory)
            {
                // Calculate class-based capabilities before checking model
                // This data model is a nightmare and needs to the thrown away but just deal with it for now
                Class = cl;
                NameText = ClassNames[cl];
                ClassConfig conf = Preset.Repo.GetClassConfig(Class);
                bool hasParent = conf.Parent != EnemyClass.Default;
                EnemyClass altParent = EnemyClass.Default;
                if (hasParent && conf.AltParent != null)
                {
                    altParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                }
                List<PlacementMode> supportedModes = new();
                if (hasParent && !conf.NoMerge)
                {
                    ParentLabel = ClassNames[conf.Parent];
                    if (!conf.NoSelfRandom)
                    {
                        supportedModes.Add(PlacementMode.Inherit);
                    }
                    supportedModes.Add(PlacementMode.Merge);
                    if (altParent != EnemyClass.Default)
                    {
                        ParentLabel2 = ClassNames[altParent];
                        supportedModes.Add(PlacementMode.Merge2);
                    }
                }
                // Spectator is Sekiro-only and isn't a target class
                if (cl != EnemyClass.Spectator)
                {
                    supportedModes.Add(PlacementMode.Norandom);
                    if (!conf.NoSelfRandom)
                    {
                        supportedModes.Add(conf.NoMerge ? PlacementMode.Random : PlacementMode.Custom);
                    }
                }
                if (supportedModes.Count == 0)
                {
                    // Can happen with Spectator probably
                    supportedModes.Add(PlacementMode.Custom);
                }
                if (conf.DefaultInherit)
                {
                    DefaultMode = PlacementMode.Inherit;
                }
                else if (conf.NoSelfRandom)
                {
                    DefaultMode = PlacementMode.Norandom;
                }
                else
                {
                    // Usually Random or Custom
                    DefaultMode = supportedModes.Last();
                }
                Modes = ImmutableList.CreateRange(supportedModes);
                CanOopsAllOverride = !conf.NoMerge;
                bool allowSource = (!conf.NoSelfRandom && !conf.NoMerge) || cl == EnemyClass.Spectator;
                // Extract model state. Assign can be null as not everything has to be specified in the config I guess
                Assign = (ClassAssignment?)model;
                if (Assign == null)
                {
                    // Minimal default assignment
                    Assign = new ClassAssignment();
                    if (conf.DefaultInherit)
                    {
                        Assign.InheritParent = true;
                    }
                    else if (conf.NoSelfRandom)
                    {
                        Assign.NoRandom = true;
                    }
                }
                if (conf.DlcSilo)
                {
                    CanEditDlcSilo = true;
                    DlcSilo = Assign.ClassSilo == LocationData.AreaSiloType.DLC;
                }
                if (Assign.InheritParent && Modes.Contains(PlacementMode.Inherit))
                {
                    Mode = PlacementMode.Inherit;
                }
                else if (Assign.MergeParent && Modes.Contains(PlacementMode.Merge))
                {
                    if (Modes.Contains(PlacementMode.Merge2) && Assign.ManualParent == altParent)
                    {
                        Mode = PlacementMode.Merge2;
                    }
                    else
                    {
                        Mode = PlacementMode.Merge;
                    }
                }
                else if (Assign.NoRandom && Modes.Contains(PlacementMode.Norandom))
                {
                    Mode = PlacementMode.Norandom;
                }
                else if (Modes.Contains(PlacementMode.Random))
                {
                    Mode = PlacementMode.Random;
                }
                else if (Modes.Contains(PlacementMode.Custom))
                {
                    Mode = PlacementMode.Custom;
                }
                else
                {
                    Mode = DefaultMode;
                }
                if (allowSource)
                {
                    Pools = [];
                    foreach (PoolAssignment pool in Assign.Pools ?? [])
                    {
                        Pools.Add(Preset.ToDisplayList(EnemyControlType.ClassPool, null, this, pool.Pool, pool.Weight, pool.RandomByType));
                    }
                    RemoveSource = Preset.ToDisplayList(EnemyControlType.GlobalPool, classRemoveSourceText, this, Assign.RemoveSource);
                }
                // Set multipler if it's in override state. Otherwise, let subscriber below set it up.
                if (Preset.GetMultiplier(out int globalMult) && Assign.EnemyMultiplier > 0 && Assign.EnemyMultiplier != globalMult)
                {
                    OverrideMultiplier = true;
                    Multiplier = Assign.EnemyMultiplier;
                }
            }
            else if (Type == TabType.AdjustSource)
            {
                List<SourceAdjustment>? adjustSource = (List<SourceAdjustment>?)model ?? [];
                Pools = [];
                if (adjustSource != null)
                {
                    foreach (SourceAdjustment adjust in adjustSource)
                    {
                        // Just reuse PoolAssignment here
                        // Assume total of 1000 in all cases. There is no global adjust here
                        int amount = 1000;
                        if (adjust.Total > 0)
                        {
                            float mult = Math.Clamp((float)adjust.Keep / adjust.Total, 0, 1);
                            amount = (int)Math.Round(mult * 1000);
                        }
                        Pools.Add(Preset.ToDisplayList(EnemyControlType.AdjustSource, null, this, adjust.Source, amount));
                    }
                }
            }
            else if (Type == TabType.ManualPlacement)
            {
                Dictionary<string, string>? enemies = (Dictionary<string, string>?)model ?? [];
                Pools = [];
                // These are added as flat entries and updating them requires pairing
                foreach ((string target, string source) in enemies)
                {
                    Pools.Add(Preset.ToDisplayList(EnemyControlType.ManualTarget, poolTargetText, this, target));
                    Pools.Add(Preset.ToDisplayList(EnemyControlType.ManualSource, poolSourceText, this, source));
                }
            }
            else if (model is not null)
            {
                throw new ArgumentException("Unexpected model");
            }

            this.WhenAnyValue(x => x.Mode, x => x.ModeOverride)
                .Subscribe(_ => ShowPools = !ModeOverride && Pools != null && (Assign == null || Mode == PlacementMode.Custom));
            this.WhenAnyValue(x => x.ModeOverride)
                .Subscribe(_ =>
                {
                    ShowModes = Modes != null && !ModeOverride;
                    // Hidden on override
                    UpdatePercent();
                });
            this.WhenAnyValue(x => x.OopsAllOverride, x => x.NorandomOverride)
                .Subscribe(_ => ModeOverride = OopsAllOverride || NorandomOverride);
            Preset.WhenAnyValue(x => x.EnableMultiplier, x => x.Multiplier).CombineLatest(this.WhenAnyValue(x => x.OverrideMultiplier, x => x.Mode))
                .Subscribe(_ =>
                {
                    if (Preset.GetMultiplier(out int globalMult))
                    {
                        // This is confusing and might be subject to weird bugs where state changes back and forth which clears the local multiplier.
                        // It is probably better to avoid stale values from previous multipliers than anything else though
                        ShowMultiplier = Mode == PlacementMode.Norandom || Mode == PlacementMode.Random || Mode == PlacementMode.Custom;
                        if (!ShowMultiplier || !OverrideMultiplier)
                        {
                            // This can be responsible for a *lot* of preset file writes, as each individual panel sends an UpdateModel.
                            // Throttle can't be used as-is because it creates file write race conditions, so TODO ratelimit better
                            Multiplier = globalMult;
                        }
                    }
                    else
                    {
                        ShowMultiplier = false;
                    }
                });
            Preset.WhenAnyValue(x => x.OverallDlcSilo)
                .Subscribe(_ => DlcSiloForced = Preset.OverallDlcSilo || Preset.DlcSiloForced);
            Preset.WhenAnyValue(x => x.NorandomAll)
                .Subscribe(_ => NorandomOverride = Preset.NorandomAll && Modes != null && Modes.Contains(PlacementMode.Norandom));
            Preset.WhenAnyValue(x => x.EnableOopsAll, x => x.NorandomAll)
                .Subscribe(_ => OopsAllOverride = Preset.EnableOopsAll && !Preset.NorandomAll && CanOopsAllOverride);


            if (Pools != null)
            {
                var poolChange = Pools.ToObservableChangeSet();
                poolChange.Subscribe(a => UpdatePools());
                poolChange.MergeMany(DisplayEnemyList.WhenModified).Subscribe(a => UpdatePools());
                Observable
                    .Merge(
                        WeightFocusChanged,
                        poolChange.Select(_ => Unit.Default),
                        this.WhenAnyValue(x => x.AutoBalance).Skip(1).Select(_ => Unit.Default))
                    .Throttle(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => AutoFixPercentages());
            }

            // Non-pool update
            Observable.Merge(
                this.WhenAnyValue(x => x.RemoveSource).SelectMany(DisplayEnemyList.WhenModified),
                this.WhenAnyValue(x => x.Mode).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.DlcSilo).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.OverrideMultiplier).Select(_ => Unit.Default).Skip(1),
                this.WhenAnyValue(x => x.Multiplier).Select(_ => Unit.Default).Skip(1))
                .Subscribe(_ =>
                {
                    Preset.UpdateModel();
                });
        }

        private void UpdatePercent()
        {
            if (Type == TabType.EnemyCategory && Pools != null)
            {
                int totalWeight = 0;
                foreach (DisplayEnemyList pool in Pools)
                {
                    totalWeight += WeightToInt(pool.Source.Weight ?? 0);
                }
                // Explicitly do not clamp
                PercentTotal = $"{(totalWeight / 10.0):0.#}";
                if (!ShowPercentTotal)
                {
                    ShowPercentTotal = Pools.Count > 1 || (Pools.Count == 1 && totalWeight != 1000);
                }
                if (Pools.Count == 0 || ModeOverride)
                {
                    ShowPercentTotal = false;
                }
                // Index may be invalidated
                lastFocusedWeight = -1;
            }
        }

        private void UpdatePools()
        {
            UpdatePercent();
            Preset.UpdateModel();
        }

        private void AutoFixPercentages()
        {
            if (Pools == null || Type != TabType.EnemyCategory)
            {
                return;
            }
            int focusedWeight = Pools.Select((p, i) => (p.WeightFocused, i)).Where(e => e.Item1).Select(e => e.Item2).FirstOrDefault(-1);
            // In previous implementation, auto-balance was not done on switching focus directly between weights, but with the new checkbox include that case
            // This probably means it can be simplified to just use LostFocus without WeightFocused state but this is fine
            if (AutoBalance && (focusedWeight == -1 || focusedWeight != lastFocusedWeight))
            {
                FixPercentages(keepSameIndex: lastFocusedWeight);
            }
            if (focusedWeight != -1)
            {
                lastFocusedWeight = focusedWeight;
            }
        }

        private void FixPercentages(bool clearEmpty = false, int keepSameIndex = -1)
        {
            if (Pools == null || Type != TabType.EnemyCategory)
            {
                return;
            }
            List<int> weights = new();
            foreach (DisplayEnemyList pool in Pools)
            {
                if (clearEmpty && pool.Items.Count == 0)
                {
                    // Pre-save, clear empty pools which might be too unintuitive in regular auto-balance.
                    weights.Add(0);
                    continue;
                }
                weights.Add(WeightToInt(pool.Source.Weight ?? 0));
            }
            AdjustClassPercentages(weights, keepSameIndex);
            foreach ((DisplayEnemyList pool, int newWeight) in Pools.Zip(weights))
            {
                if (pool.Source.HasWeight)
                {
                    pool.Source.Weight = IntToWeight(newWeight);
                }
            }
        }

        public void PreSaveValidate()
        {
            if (Children != null)
            {
                foreach (EnemyPanelViewModel child in Children)
                {
                    child.PreSaveValidate();
                }
            }
            FixPercentages(true);
        }

        public T? GetModelObject<T>() where T : class
        {
            object? model = null;
            // Do not validate percentages at this point, do it before properly saving
            if (Type == TabType.EnemyCategory)
            {
                ClassConfig conf = Preset.Repo.GetClassConfig(Class);
                ClassAssignment assign = new ClassAssignment();

                assign.RemoveSource = Preset.FromDisplayList(RemoveSource);
                if (CanEditDlcSilo)
                {
                    // This may hidden if forced, but save it nonetheless
                    assign.ClassSilo = DlcSilo ? LocationData.AreaSiloType.DLC : LocationData.AreaSiloType.None;
                }
                if (ShowMultiplier && OverrideMultiplier && Multiplier is int mult)
                {
                    assign.EnemyMultiplier = mult;
                }

                if (Mode == PlacementMode.Norandom)
                {
                    assign.NoRandom = true;
                }
                else if (Mode == PlacementMode.Merge)
                {
                    assign.MergeParent = true;
                }
                else if (Mode == PlacementMode.Merge2)
                {
                    assign.MergeParent = true;
                    if (conf != null && conf.AltParent != null)
                    {
                        // If not present, this will fallback to main parent
                        assign.ManualParent = conf.AltParent.Where(c => c != conf.Parent).FirstOrDefault();
                    }
                }
                else if (Mode == PlacementMode.Inherit)
                {
                    assign.InheritParent = true;
                }
                if (Pools != null && Pools.Count > 0)
                {
                    assign.Pools = [];
                    foreach (DisplayEnemyList pool in Pools)
                    {
                        if (pool.Items.Count == 0)
                        {
                            continue;
                        }
                        int amt = pool.Source.HasWeight ? WeightToInt(pool.Source.Weight ?? 0) : 0;
                        assign.Pools.Add(new PoolAssignment
                        {
                            Pool = Preset.FromDisplayList(pool),
                            Weight = amt,
                            RandomByType = pool.Source.RandomByType,
                        });
                    }
                    // Roundtrip nullability for now
                    if (assign.Pools.Count == 0) assign.Pools = null;
                }
                if (Preset.GetMultiplier(out int globalMult))
                {
                    assign.EnemyMultiplier = (OverrideMultiplier ? Multiplier : null) ?? globalMult;
                }
                model = assign;
            }
            else if (Type == TabType.AdjustSource)
            {
                List<SourceAdjustment>? adjustSource = [];
                foreach (DisplayEnemyList pool in Pools!)
                {
                    if (pool.Items.Count == 0)
                    {
                        continue;
                    }
                    int amt = pool.Source.Weight is double weight ? WeightToInt(weight) : 0;
                    adjustSource.Add(new SourceAdjustment
                    {
                        Source = Preset.FromDisplayList(pool),
                        Keep = amt,
                        Total = 1000,
                    });
                }
                model = adjustSource.Count == 0 ? null : adjustSource;
            }
            else if (Type == TabType.ManualPlacement)
            {
                Dictionary<string, string>? enemies = [];
                for (int i = 0; i < Pools!.Count - 1; i += 2)
                {
                    string? target = Preset.FromDisplayList(Pools[i]);
                    string? source = Preset.FromDisplayList(Pools[i + 1]);
                    if (target != null && source != null && !target.Contains(';'))
                    {
                        enemies[target] = source;
                    }
                }
                model = enemies.Count == 0 ? null : enemies;
            }
            return (T?)model;
        }

        public void UpdateItems(Action<DisplayEnemy> action)
        {
            if (Children != null)
            {
                foreach (EnemyPanelViewModel child in Children)
                {
                    child.UpdateItems(action);
                }
            }
            if (Pools != null)
            {
                foreach (DisplayEnemyList pool in Pools)
                {
                    pool.UpdateItems(action);
                }
            }
        }

        [ReactiveCommand]
        public void AddPool() => AddNewPool();

        // Some overlap with constructor, but for interactive use only
        private DisplayEnemyList? AddNewPool(string? init = null, int? weight = null)
        {
            if (Pools == null)
            {
                return null;
            }
            if (Type == TabType.EnemyCategory)
            {
                weight ??= 0;
                if (Pools.Count == 0)
                {
                    init ??= "default";
                    weight = 1000;
                }
                DisplayEnemyList pool = Preset.ToDisplayList(EnemyControlType.ClassPool, null, this, init, weight);
                Pools.Add(pool);
                return pool;
            }
            else if (Type == TabType.AdjustSource)
            {
                weight ??= 1000;
                DisplayEnemyList pool = Preset.ToDisplayList(EnemyControlType.AdjustSource, null, this, init, weight);
                Pools.Add(pool);
                return pool;
            }
            else if (Type == TabType.ManualPlacement)
            {
                DisplayEnemyList targetPool = Preset.ToDisplayList(EnemyControlType.ManualTarget, poolTargetText, this);
                DisplayEnemyList sourcePool = Preset.ToDisplayList(EnemyControlType.ManualSource, poolSourceText, this);
                Pools.AddRange([targetPool, sourcePool]);
            }
            return null;
        }

        [ReactiveCommand]
        public void RemovePool(DisplayEnemyList list)
        {
            if (Pools == null)
            {
                return;
            }
            int index = Pools.IndexOf(list);
            if (index == -1)
            {
                return;
            }
            EnemyControlType type = list.Source.Type;
            if (type == EnemyControlType.ManualTarget)
            {
                if (index + 1 < Pools.Count && Pools[index + 1].Source.Type == EnemyControlType.ManualSource)
                {
                    Pools.RemoveAt(index);
                    Pools.RemoveAt(index);
                }
            }
            else if (type == EnemyControlType.AdjustSource || type == EnemyControlType.ClassPool)
            {
                Pools.RemoveAt(index);
            }
        }

        [ReactiveCommand]
        public void QuickBoss()
        {
            // Previous impl edited both Basic and Wildlife, but just edit Basic since it copies config by default,
            // is maybe preferable in terms of play experience, and this is partly a tutorial anyway.
            if (Class != EnemyClass.Basic || Pools == null)
            {
                return;
            }
            using var _ = DelayChangeNotifications();
            Mode = PlacementMode.Custom;
            if (Pools.Count == 0)
            {
                AddNewPool();
            }
            DisplayEnemyList? pool = Pools.Where(p => p.Items.Count == 1 && p.Items[0].Obj == "AllBosses").FirstOrDefault();
            pool ??= AddNewPool("AllBosses");
            if (pool == null)
            {
                return;
            }
            // This is 10%, could be parameterized
            pool.Source.Weight = 10;
            FixPercentages(keepSameIndex: Pools.Count - 1);
        }

        [ReactiveCommand]
        public void QuickWildlife()
        {
            if (Class != EnemyClass.Wildlife || Pools == null)
            {
                return;
            }
            using var _ = DelayChangeNotifications();
            Mode = PlacementMode.Custom;
            if (Pools.Count == 0 || Pools.All(p => p.Items.Count == 0))
            {
                Pools.Clear();
                AddNewPool("Basic");
            }
            else
            {
                foreach (DisplayEnemyList pool in Pools)
                {
                    for (int i = 0; i < pool.Items.Count; i++)
                    {
                        DisplayEnemy obj = pool.Items[i];
                        if ((obj.Obj == "default" || obj.Obj == "Wildlife") && pool.Source.LookupEnemy("Basic", out DisplayEnemy? basic))
                        {
                            pool.Items[i] = basic;
                        }
                    }
                }
            }
        }

        [ReactiveCommand]
        public void QuickPassive()
        {
            List<PassiveAdjustment>? adjs = Preset.Repo.PassiveAdjustments;
            if (Type != TabType.AdjustSource || Pools == null || adjs == null)
            {
                return;
            }
            using var _ = DelayChangeNotifications();
            HashSet<string> existing = new(Pools.SelectMany(pool => pool.Items.Select(obj => obj.Obj)));
            foreach (PassiveAdjustment adj in adjs)
            {
                if (adj.Source == null) continue;
                List<string> sources = Util.PhraseRe.Split(adj.Source).Where(s => !existing.Contains(s)).ToList();
                if (sources.Count > 0)
                {
                    AddNewPool(string.Join("; ", sources), adj.Percent * 10);
                }
            }
        }

        // Different configurations for populating the list source and available controls
        public enum EnemyControlType
        {
            GlobalPool,
            ClassPool,
            AdjustSource,
            ManualTarget,
            ManualSource,
            OopsAll,
        }

        public partial class DisplayEnemyList : ReactiveObject
        {
            public readonly EnemyPresetViewModel Preset;
            public readonly EnemyPanelViewModel? Panel;
            [Reactive] private DisplayEnemySource _source;
            [Reactive] private ObservableCollection<DisplayEnemy> _items;

            // UI state
            private Subject<Unit> _modified = new();
            public static IObservable<Unit> WhenModified(DisplayEnemyList? list) => list is null ? Observable.Empty<Unit>() : list._modified;
            public static IObservable<Unit> WhenValue(DisplayEnemyList? list) => list is null ? Observable.Empty<Unit>() : list._modified.StartWith(Unit.Default);
            [Reactive] private bool _weightFocused;

            public DisplayEnemyList(
                EnemyPresetViewModel preset, EnemyPanelViewModel? panel, DisplayEnemySource source, List<DisplayEnemy> items)
            {
                Preset = preset;
                Panel = panel;
                Source = source;
                Source.List = this;
                if (items.Count > 1)
                {
                    items.RemoveAll(i => i.Obj == "norandom");
                }
                // Anything else added to Items should have this list assigned
                items.ForEach(item => item.List = this);
                if (Source.Type == EnemyControlType.ManualTarget)
                {
                    Items = new(items.Take(1));
                }
                else
                {
                    Items = new(items);
                }
                UpdateRandomByType();
                UpdateRedundant();

                bool loading = true;
                var itemChange = Items.ToObservableChangeSet();
                // List items are effectively immutable so no property changes needed
                itemChange.Subscribe(_ =>
                {
                    if (loading)
                    {
                        // Non-empty list starts with ChangeSet with Adds
                        return;
                    }
                    // Console.WriteLine($"Updating list ({Items.Count}, {e.Count} change {e.Adds} add {e.Replaced} replace {e.Refreshes} refresh) in {Panel?.Type} {Panel?.Class} {Source.Type}");
                    UpdateRandomByType();
                    SaveChanges();
                });
                this.WhenAnyValue(x => x.Source.Weight, x => x.Source.RandomByType).Skip(1).Subscribe(_ =>
                {
                    // Console.WriteLine($"Source update in {Panel?.Type} {Panel?.Class}");
                    SaveChanges();
                });
                if (Panel != null)
                {
                    // Doesn't reliably work from parent so explicit observable it is
                    this.WhenAnyValue(x => x.WeightFocused).Select(_ => Unit.Default).Subscribe(Panel.WeightFocusChanged);
                }
                loading = false;
            }

            public void UpdateItems(Action<DisplayEnemy> action)
            {
                foreach (DisplayEnemy item in Items)
                {
                    action(item);
                }
            }

            public void AddItem(AutocompleteItem item)
            {
                DisplayEnemy? existing = Items.Where(i => i.Obj == item.Obj).FirstOrDefault();
                if (existing != null && !Source.RandomByType)
                {
                    return;
                }
                // Currently norandom is an odd case which must always be by itself
                if (item.Obj == "norandom" && Items.Count > 0)
                {
                    // This used to confirm but it should be fine to do it automatically
                    Panel?.AddNewPool("norandom");
                    return;
                }
                else if (Items.Count == 1 && Items[0].Obj == "norandom")
                {
                    return;
                }
                DisplayEnemy add = new DisplayEnemy(item.Obj, item.Name, this);
                if (Source.Type == EnemyControlType.ManualTarget)
                {
                    if (Items.Count == 1)
                    {
                        Items[0] = add;
                        return;
                    }
                    Items.Clear();
                }
                Items.Add(add);
            }

            public void RemoveSelf()
            {
                Panel?.RemovePool(this);
            }

            public void RemoveItem(DisplayEnemy item)
            {
                Items.Remove(item);
            }

            private void SaveChanges()
            {
                UpdateRedundant();
                _modified.OnNext(Unit.Default);
            }

            private void UpdateRandomByType()
            {
                Source.ShowRandomByType = Source.HasRandomByType && Items.Count > 1;
            }

            private void UpdateRedundant()
            {
                Dictionary<string, string>? parents = Source.RandomByType ? null : Preset.Repo.GetRedundantTags(Items.Select(i => i.Obj), Panel?.Class);
                foreach (DisplayEnemy enemy in Items)
                {
                    if (parents != null && parents.TryGetValue(enemy.Obj, out string? parentObj))
                    {
                        Messages messages = Messages.GetInstance();
                        string tip = messages.Get(redundantEnemyTooltipText, enemy.LocalizedText, Preset.Repo.ToDisplay(parentObj));
                        if (Source.IsDeletable)
                        {
                            tip += "\n" + messages.Get(redundantEnemyTooltipPoolText);
                        }
                        enemy.RedundantTo = tip;
                    }
                    else
                    {
                        enemy.RedundantTo = null;
                    }
                }
            }
        }

        private static int WeightToInt(double weight) => Math.Clamp((int)Math.Round(weight * 10), 0, 1000);
        private static double IntToWeight(int value) => Math.Clamp(value, 0, 1000) / 10.0;

        public partial class DisplayEnemySource : ReactiveObject
        {
            public EnemyControlType Type { get; init; }
            [Reactive] private IEnumerable<AutocompleteItem> _items;

            [Reactive] private Text? _label;
            // Misc conditional controls
            public bool IsDeletable { get; init; }
            public bool HasWeight { get; init; }
            [Reactive] public double? _weight;
            public bool HasRandomByType { get; init; }
            [Reactive] public bool _randomByType;
            // Set by parent
            [Reactive] public bool _showRandomByType;

            public DisplayEnemyList? List { get; set; }

            public DisplayEnemySource(EnemyControlType type, IEnumerable<AutocompleteItem> items, int? weight, Text? label, bool randomByType = false)
            {
                Type = type;
                Items = items;
                Label = label;
                IsDeletable = Type == EnemyControlType.ClassPool || Type == EnemyControlType.AdjustSource || Type == EnemyControlType.ManualTarget;
                // Weight can become null while being edited
                HasWeight = weight != null;
                Weight = weight is int val ? IntToWeight(val) : null;
                HasRandomByType = Type == EnemyControlType.ClassPool;
                RandomByType = randomByType;
            }

            public bool LookupEnemy(string obj, [NotNullWhen(true)] out AutocompleteItem? enemy)
            {
                enemy = Items.Where(i => i.Obj == obj).FirstOrDefault();
                return obj != null;
            }

            public bool LookupEnemy(string obj, [NotNullWhen(true)] out DisplayEnemy? enemy)
            {
                if (LookupEnemy(obj, out AutocompleteItem? item))
                {
                    enemy = new DisplayEnemy(item.Obj, item.Name, List);
                    return true;
                }
                else
                {
                    enemy = null;
                    return false;
                }
            }

            [ReactiveCommand]
            public void RemoveSelf()
            {
                List?.RemoveSelf();
            }

            // See DisplayItemSource for more background on how this is set up
            public Avalonia.Controls.AutoCompleteFilterPredicate<object?> ItemFilter => (search, obj) =>
            {
                if (string.IsNullOrWhiteSpace(search))
                {
                    return true;
                }
                if (obj is not AutocompleteItem item)
                {
                    return false;
                }
                if (item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (item.Keywords != null && item.Keywords.Any(name => name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
                return false;
            };
        }

        // These are used for displayed state
        // The only 'model' field is Obj
        public partial class DisplayEnemy : ReactiveObject
        {
            // Can be ItemRef or LocationRef, obviously should be consistent within a given list
            public string Obj { get; }
            [Reactive] private string _localizedText;

            // Needed for add/remove operations, and access to overall preset state
            // DisplayEnemy can sorta function without it, as initial setup is recursive
            public DisplayEnemyList? List { get; set; }

            // Set based on other tags
            [Reactive] private string? _redundantTo;

            public DisplayEnemy(string obj, string name, DisplayEnemyList? list = null)
            {
                Obj = obj;
                LocalizedText = name;
                List = list;
            }

            [ReactiveCommand]
            public void Remove()
            {
                List?.RemoveItem(this);
            }

            public void UpdateText(EnemyRepository repo)
            {
                // TODO: Also update RedundantTo
                LocalizedText = repo.ToDisplay(Obj);
            }

            public override string ToString() => $"[{LocalizedText}]";
        }

        public static void AdjustClassPercentages(List<int> amts, int keepSameIndex = -1)
        {
            // Edit the amts to add up to 1000.
            int iter = 0;
            int total = amts.Sum();
            while (iter++ < 100 && total != 1000)
            {
                if (total > 1000)
                {
                    int max = amts.Max();
                    List<int> maxIndices = amts
                        .Select((a, i) => (a, i))
                        .Where(e => e.Item1 == max)
                        .Select(e => e.Item2)
                        .ToList();
                    if (maxIndices.Count == 0) break;
                    // Somewhat tricky thing: if there are multiple max values,
                    // potentially decrease them below 1000. next iteration should
                    // bump one of them back up again.
                    // e.g. if two values are 800 out of total 1601, reduction is 601,
                    // individual reductions are 301.
                    // More complicated resolution would involve looking at second highest
                    // and not reducing it lower than that, but meh even more edge cases there.
                    int reduce = (int)Math.Ceiling((total - 1000.0) / maxIndices.Count);
                    int amt = Math.Max(0, max - reduce);
                    foreach (int index in maxIndices)
                    {
                        amts[index] = amt;
                    }
                }
                else if (total < 1000)
                {
                    // Find the biggest entry and expand it further
                    // There shouldn't be more than one pass of this, probably?
                    int index = -1;
                    int amt = -1;
                    int avoidIndex = keepSameIndex;
                    while (true)
                    {
                        List<int> partialMaxes = amts.Where((a, i) => a < 1000 && (avoidIndex < 0 || i != avoidIndex)).ToList();
                        if (partialMaxes.Count > 0)
                        {
                            int max = partialMaxes.Max();
                            index = amts.IndexOf(max);
                            if (index >= 0)
                            {
                                amt = max + Math.Min(1000 - total, 1000 - max);
                                break;
                            }
                        }
                        if (avoidIndex < 0)
                        {
                            break;
                        }
                        else
                        {
                            avoidIndex = -1;
                        }
                    }
                    if (amt == -1) break;
                    amts[index] = amt;
                }
                total = amts.Sum();
            }
            while (iter++ < 100 && total < 1000)
            {
                // Find the biggest entry and shrink it down
                total = amts.Sum();
            }
        }
    }
}